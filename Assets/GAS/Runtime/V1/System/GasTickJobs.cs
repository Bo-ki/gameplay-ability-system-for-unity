using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace GAS.Runtime
{
    /// <summary>
    /// 在任何 Session cardinality 分支前记录已接管 cleanup shell 的标准 EndFixed 移除。
    /// </summary>
    internal struct GasCleanupAcceptedPrepassJob : IJob
    {
        [ReadOnly]
        public NativeArray<Entity> AcceptedShells;
        [ReadOnly]
        public ComponentLookup<BoundaryDrainState> DrainStates;
        [ReadOnly]
        public BufferLookup<BoundaryFactBuffer> FactBuffers;
        public EntityCommandBuffer EndFixed;

        /// <summary>
        /// 只移除已 Accepted 且无 late tail 的 cleanup 载体，结构变化交给标准 EndFixed playback。
        /// </summary>
        public void Execute()
        {
            for (var index = 0; index < AcceptedShells.Length; index++)
            {
                var entity = AcceptedShells[index];
                if (!DrainStates.HasComponent(entity) || !FactBuffers.HasBuffer(entity))
                    continue;

                var state = DrainStates[entity];
                if (state.Phase != GasBoundaryDrainPhase.Accepted ||
                    state.SimulationEpoch == 0 ||
                    state.OwnerKind == GasBoundaryOwnerKind.None ||
                    state.OwnerStableId == 0 ||
                    state.OwnerGeneration == 0 ||
                    state.NextOwnerSequence == 0 ||
                    FactBuffers[entity].Length != 0)
                    continue;

                EndFixed.RemoveComponent<BoundaryDrainState>(entity);
                EndFixed.RemoveComponent<BoundaryFactBuffer>(entity);
                EndFixed.DestroyEntity(entity);
            }
        }
    }

    /// <summary>
    /// 在 SpawnFinalize 之前冻结本 update 的 lifecycle dispatch，阻止 Pending→Ready 后同 update 误跑 gameplay Tick。
    /// </summary>
    internal struct GasCaptureKernelModeJob : IJob
    {
        public Entity Session;
        [ReadOnly] public ComponentLookup<GasSessionLifecycle> Lifecycles;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 只有 update 开始时已为 Ready/Running 的 Session 才允许后续完整 gameplay DAG 生效。
        /// </summary>
        public void Execute()
        {
            var lifecycle = Lifecycles[Session].State;
            var execution = Execution[0];
            execution.GameplayEnabled = lifecycle == GasSessionLifecycleState.Ready ||
                                        lifecycle == GasSessionLifecycleState.Running
                ? (byte)1
                : (byte)0;
            Execution[0] = execution;
        }
    }

    /// <summary>
    /// seal 当前 candidate tick 的 due inbox，并冻结 transport 审计证据与 canonical command scratch。
    /// </summary>
    internal struct GasGatherTickStartSnapshotJob : IJob
    {
        private const ulong HashOffsetBasis = 14695981039346656037UL;
        private const ulong HashPrime = 1099511628211UL;

        public Entity Session;
        [ReadOnly] public ComponentLookup<SimulationTickState> Ticks;
        public BufferLookup<BoundaryCommandInbox> Inboxes;
        [ReadOnly] public BufferLookup<BoundaryCommandFrozenPayload> FrozenPayloads;
        public NativeArray<GasTickExecutionState> Execution;
        public NativeArray<GasSealedCommand> SealedCommands;

        /// <summary>
        /// 以 CurrentTick+1 为唯一候选 tick，seal due command 且拒绝非法状态或容量越界。
        /// </summary>
        public void Execute()
        {
            var execution = Execution[0];
            if (execution.GameplayEnabled == 0)
                return;
            execution.LaneMask |= GasTickLaneMask.Gather;
            var tick = Ticks[Session];
            if (tick.CurrentTick == ulong.MaxValue)
            {
                execution.PreAdmissionFailure = GasTickAdmissionFailureReason.TickOverflow;
                Execution[0] = execution;
                return;
            }

            execution.CandidateTick = tick.CurrentTick + 1;
            var inbox = Inboxes[Session];
            SealDueCommands(ref execution, tick.CurrentTick, ref inbox);
            SortAndValidateCanonicalKeys(ref execution);
            Execution[0] = execution;
        }

        /// <summary>
        /// 将所有 due Pending 条目标记为 Sealed，并在定长 scratch 范围内复制完整 record。
        /// </summary>
        private void SealDueCommands(
            ref GasTickExecutionState execution,
            ulong dueThroughTick,
            ref DynamicBuffer<BoundaryCommandInbox> inbox)
        {
            var hash = HashOffsetBasis;
            var payload = FrozenPayloads[Session];
            for (var index = 0; index < inbox.Length; index++)
            {
                var command = inbox[index];
                if (!IsKnownState(command.State))
                {
                    SetFirstFailure(ref execution, GasTickAdmissionFailureReason.InboxStateInvalid);
                    continue;
                }
                if (command.State != GasBoundaryCommandState.Pending ||
                    command.AvailableTick > dueThroughTick)
                    continue;
                if (!HasValidFrozenPayload(in command, payload))
                {
                    SetFirstFailure(ref execution, GasTickAdmissionFailureReason.InboxStateInvalid);
                    continue;
                }

                command.State = GasBoundaryCommandState.Sealed;
                command.SealedTick = execution.CandidateTick;
                inbox[index] = command;
                AccumulateEvidence(ref execution, ref hash, in command);
                StoreSealedCommand(ref execution, index, in command);
            }
            execution.SealedRequestHash = execution.SealedCommandCount == 0 ? 0 : hash;
        }

        /// <summary>
        /// 在 scratch 满后仍 seal 并计入 fault 证据，但绝不越界写定长容器。
        /// </summary>
        private void StoreSealedCommand(
            ref GasTickExecutionState execution,
            int inboxIndex,
            in BoundaryCommandInbox command)
        {
            execution.SealedCommandCount++;
            if (execution.StoredSealedCommandCount >= SealedCommands.Length)
            {
                SetFirstFailure(ref execution, GasTickAdmissionFailureReason.BoundaryCommandLimit);
                return;
            }

            SealedCommands[execution.StoredSealedCommandCount++] = new GasSealedCommand
            {
                InboxIndex = inboxIndex,
                Command = command,
            };
        }

        /// <summary>
        /// 按 RequestSequence 顺序冻结 fault subset 首尾，并将 sequence/完整 command hash 纳入 FNV-1a。
        /// </summary>
        private static void AccumulateEvidence(
            ref GasTickExecutionState execution,
            ref ulong hash,
            in BoundaryCommandInbox command)
        {
            if (execution.SealedCommandCount == 0)
                execution.SealedFirstRequestSequence = command.RequestSequence;
            execution.SealedLastRequestSequence = command.RequestSequence;
            AddEvidenceValue(ref hash, command.RequestSequence);
            AddEvidenceValue(ref hash, command.CommandHash);
        }

        /// <summary>
        /// 按固定小端顺序把一个审计值加入 sealed subset 的 FNV-1a 指纹。
        /// </summary>
        private static void AddEvidenceValue(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash = unchecked(hash * HashPrime);
            }
        }

        /// <summary>
        /// 排除 transport sequence 后排序，并把同 key、任一完整语义差异提升为 deterministic fault。
        /// </summary>
        private void SortAndValidateCanonicalKeys(ref GasTickExecutionState execution)
        {
            if (execution.StoredSealedCommandCount <= 1)
                return;
            var commands = SealedCommands.GetSubArray(0, execution.StoredSealedCommandCount);
            var comparer = new GasSealedCommandComparer();
            commands.Sort(comparer);
            for (var index = 1; index < commands.Length; index++)
            {
                var previous = commands[index - 1];
                var current = commands[index];
                if (comparer.Compare(previous, current) == 0 &&
                    !AreSemanticallyEquivalent(in previous.Command, in current.Command))
                    SetFirstFailure(ref execution, GasTickAdmissionFailureReason.CanonicalKeyCollision);
            }
        }

        /// <summary>
        /// 逐字段并逐 payload byte 比较同 canonical key 的 work，哈希相同也不得掩盖碰撞。
        /// </summary>
        private bool AreSemanticallyEquivalent(
            in BoundaryCommandInbox left,
            in BoundaryCommandInbox right)
        {
            if (left.SemanticHash != right.SemanticHash ||
                left.HasSource != right.HasSource ||
                !left.BattleInstance.Equals(right.BattleInstance) ||
                !left.Target.Equals(right.Target) ||
                !SameHandle(left.SubjectHandle, right.SubjectHandle) ||
                left.DefinitionId != right.DefinitionId ||
                left.PayloadSchemaVersion != right.PayloadSchemaVersion ||
                left.PayloadKind != right.PayloadKind ||
                left.FrozenPayloadLength != right.FrozenPayloadLength)
                return false;
            return SamePayloadBytes(in left, in right);
        }

        /// <summary>
        /// 比较 typed handle 诊断载体的完整稳定身份。
        /// </summary>
        private static bool SameHandle(
            in StableHandleDiagnosticCarrier left,
            in StableHandleDiagnosticCarrier right)
        {
            return left.SimulationEpoch == right.SimulationEpoch &&
                   left.OwnerAsc.Equals(right.OwnerAsc) &&
                   left.SlotIndex == right.SlotIndex &&
                   left.SlotGeneration == right.SlotGeneration &&
                   left.Kind == right.Kind;
        }

        /// <summary>
        /// 从 Session 冻结 byte buffer 比较两个合法 range，拒绝用 payload hash 代替内容等价。
        /// </summary>
        private bool SamePayloadBytes(
            in BoundaryCommandInbox left,
            in BoundaryCommandInbox right)
        {
            var payload = FrozenPayloads[Session];
            if (!HasValidFrozenPayload(in left, payload) ||
                !HasValidFrozenPayload(in right, payload))
                return false;
            for (var index = 0; index < left.FrozenPayloadLength; index++)
            {
                if (payload[left.FrozenPayloadOffset + index].Value !=
                    payload[right.FrozenPayloadOffset + index].Value)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 校验冻结 payload range 完整位于 Session byte buffer 且真实字节哈希匹配描述。
        /// </summary>
        private static bool HasValidFrozenPayload(
            in BoundaryCommandInbox command,
            DynamicBuffer<BoundaryCommandFrozenPayload> payload)
        {
            var end = (long)command.FrozenPayloadOffset + command.FrozenPayloadLength;
            if (command.FrozenPayloadOffset < 0 || command.FrozenPayloadLength < 0 ||
                end > payload.Length)
                return false;
            var hash = GasBoundaryFnv1A64.Create();
            for (var index = 0; index < command.FrozenPayloadLength; index++)
                hash.AddByte(payload[command.FrozenPayloadOffset + index].Value);
            return hash.Value == command.PayloadHash;
        }

        /// <summary>
        /// 只接受 Pending、Sealed、Consumed 与 FaultTerminated 四种冻结状态。
        /// </summary>
        private static bool IsKnownState(GasBoundaryCommandState state)
        {
            return state == GasBoundaryCommandState.Pending || state == GasBoundaryCommandState.Sealed ||
                   state == GasBoundaryCommandState.Consumed ||
                   state == GasBoundaryCommandState.FaultTerminated;
        }

        /// <summary>
        /// 保留 canonical 执行顺序遇到的第一个准入失败原因。
        /// </summary>
        private static void SetFirstFailure(
            ref GasTickExecutionState execution,
            GasTickAdmissionFailureReason reason)
        {
            if (execution.PreAdmissionFailure == GasTickAdmissionFailureReason.None)
                execution.PreAdmissionFailure = reason;
        }
    }

    /// <summary>
    /// 用 sealed count 与 Catalog 全局保守 maxima 在 Plan/Expand 写入前证明 scratch envelope。
    /// </summary>
    internal struct GasPlanExpandScratchProvisionJob : IJob
    {
        [ReadOnly] public BlobAssetReference<GasDefinitionCatalogBlob> Catalog;
        [ReadOnly] public GasScaleProfile Profile;
        public NativeArray<GasTickExecutionState> Execution;
        public NativeArray<PlanExpandScratchEnvelopeToken> Envelope;

        /// <summary>
        /// 以 checked 乘法构造三类上界，任一逻辑上限失败只写 token fault candidate。
        /// </summary>
        public void Execute()
        {
            var execution = Execution[0];
            if (execution.GameplayEnabled == 0)
                return;
            execution.LaneMask |= GasTickLaneMask.Provision;
            var token = new PlanExpandScratchEnvelopeToken
            {
                CandidateTick = execution.CandidateTick,
                FailureReason = execution.PreAdmissionFailure,
            };
            if (token.FailureReason == GasTickAdmissionFailureReason.None)
                BuildEnvelope(execution.StoredSealedCommandCount, ref token);
            Envelope[0] = token;
            Execution[0] = execution;
        }

        /// <summary>
        /// 扫描不可变 Catalog maxima 后建立保守上界，阶段 D 可按 resolved Definition 收紧但不得放宽。
        /// </summary>
        private void BuildEnvelope(int commandCount, ref PlanExpandScratchEnvelopeToken token)
        {
            FindCatalogMaxima(out var maximumTargets, out var maximumOutputs);
            token.MaximumOwnerPlanCount = commandCount;
            if (!TryMultiply(commandCount, maximumTargets, out token.MaximumResolvedTargetCount) ||
                !TryMultiply(commandCount, maximumOutputs, out token.MaximumEffectOperationCount))
            {
                token.FailureReason = GasTickAdmissionFailureReason.EnvelopeArithmeticOverflow;
                return;
            }
            token.FailureReason = ValidateProfileLimits(in token);
        }

        /// <summary>
        /// 计算 Session Catalog 中任一定义可声明的 target 与 effect output 最大值。
        /// </summary>
        private void FindCatalogMaxima(out int maximumTargets, out int maximumOutputs)
        {
            maximumTargets = 1;
            maximumOutputs = 1;
            ref var catalog = ref Catalog.Value;
            for (var index = 0; index < catalog.Abilities.Length; index++)
                AccumulateMaxima(in catalog.Abilities[index].Maxima, ref maximumTargets, ref maximumOutputs);
            for (var index = 0; index < catalog.GameplayEffects.Length; index++)
                AccumulateMaxima(in catalog.GameplayEffects[index].Maxima, ref maximumTargets, ref maximumOutputs);
        }

        /// <summary>
        /// 把单一定义 maxima 合并到 Session 保守 envelope，零声明仍保留直接命令的一条工作容量。
        /// </summary>
        private static void AccumulateMaxima(
            in GasDefinitionMaxima maxima,
            ref int maximumTargets,
            ref int maximumOutputs)
        {
            maximumTargets = System.Math.Max(maximumTargets, maxima.MaximumTargetCount);
            maximumOutputs = System.Math.Max(maximumOutputs, maxima.MaximumPlannedApplicationCount);
            maximumOutputs = System.Math.Max(maximumOutputs, maxima.MaximumDirectProgramOutputCount);
        }

        /// <summary>
        /// 用 long 中间值拒绝负数或 int 溢出，不依赖 unchecked 运行配置。
        /// </summary>
        private static bool TryMultiply(int left, int right, out int result)
        {
            var product = (long)left * right;
            if (left < 0 || right < 0 || product > int.MaxValue)
            {
                result = 0;
                return false;
            }
            result = (int)product;
            return true;
        }

        /// <summary>
        /// 依 owner plan、target 与 effect op 顺序返回第一个 profile 逻辑上限失败。
        /// </summary>
        private GasTickAdmissionFailureReason ValidateProfileLimits(
            in PlanExpandScratchEnvelopeToken token)
        {
            if (token.MaximumOwnerPlanCount > Profile.MaxOwnerPlanCount)
                return GasTickAdmissionFailureReason.OwnerPlanLimit;
            if (token.MaximumResolvedTargetCount > Profile.MaxResolvedTargetCount)
                return GasTickAdmissionFailureReason.ResolvedTargetLimit;
            return token.MaximumEffectOperationCount > Profile.MaxEffectOperationCount
                ? GasTickAdmissionFailureReason.EffectOperationLimit
                : GasTickAdmissionFailureReason.None;
        }
    }

    /// <summary>
    /// 在 ASC-local shadow 形成阶段 C 的命令计划外壳；不写任何长期 gameplay authority。
    /// </summary>
    internal struct GasOwnerPlanBuildJob : IJob
    {
        public Entity Session;
        public ulong SimulationEpoch;
        [ReadOnly] public BlobAssetReference<GasDefinitionCatalogBlob> Catalog;
        [ReadOnly] public ComponentLookup<SimulationTickState> Ticks;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public ComponentLookup<AscLifecycle> AscLifecycles;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public BufferLookup<GrantedAbilitySlot> Grants;
        [ReadOnly] public BufferLookup<AbilityActivationSlot> Activations;
        [ReadOnly] public BufferLookup<AbilityContinuationSlot> Continuations;
        [ReadOnly] public BufferLookup<AbilitySubscriptionSlot> Subscriptions;
        [ReadOnly] public BufferLookup<ActiveEffectSlot> ActiveEffects;
        [ReadOnly] public BufferLookup<PendingCommand> PendingCommands;
        [ReadOnly] public BufferLookup<AttributeValueSlot> Attributes;
        [ReadOnly] public BufferLookup<TagCountSlot> TagCounts;
        [ReadOnly] public BufferLookup<CooldownGateSlot> Cooldowns;
        [ReadOnly] public NativeArray<GasSealedCommand> SealedCommands;
        [ReadOnly] public NativeArray<PlanExpandScratchEnvelopeToken> Envelope;
        public NativeArray<GasOwnerPlanRecord> OwnerPlans;
        public NativeArray<GasAbilityRouteRecord> AbilityRoutes;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 有效 envelope 下按 canonical order 解释 Ability command，并冻结 source-local CommitPlan。
        /// </summary>
        public void Execute()
        {
            var execution = Execution[0];
            if (execution.GameplayEnabled == 0)
                return;
            execution.LaneMask |= GasTickLaneMask.OwnerPlan;
            if (Envelope[0].FailureReason == GasTickAdmissionFailureReason.None)
            {
                var nextStableSequence = Ticks[Session].NextStableSequence;
                ref var catalog = ref Catalog.Value;
                for (var index = 0; index < execution.StoredSealedCommandCount; index++)
                {
                    var command = SealedCommands[index].Command;
                    var plan = BuildPlan(in command, ref catalog, in execution, index);
                    if (plan.BusinessAccepted != 0 &&
                        (plan.CommandKind == GasBoundaryCommandKind.Activate ||
                         plan.CommandKind == GasBoundaryCommandKind.Commit ||
                         plan.CommandKind == GasBoundaryCommandKind.ApplyEffect))
                    {
                        if (nextStableSequence == ulong.MaxValue)
                            execution.PreAdmissionFailure =
                                GasTickAdmissionFailureReason.EnvelopeArithmeticOverflow;
                        else
                            plan.StableSequence = nextStableSequence++;
                    }
                    OwnerPlans[index] = plan;
                }
                execution.OwnerPlanCount = execution.StoredSealedCommandCount;
                PlanAbilityRoutes(ref execution, ref nextStableSequence);
                execution.NextStableSequenceAfterPlan = nextStableSequence;
            }
            Execution[0] = execution;
        }

        /// <summary>
        /// 冻结所有 Ending Continuation unsubscribe 与 due unsubscribe Ack 路由，再分配稳定序号。
        /// </summary>
        private void PlanAbilityRoutes(
            ref GasTickExecutionState execution,
            ref ulong nextStableSequence)
        {
            var routeCount = 0;
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var ownerSlot = registry[index];
                if (ownerSlot.State != GasAscRegistryState.Ready)
                    continue;
                var asc = ownerSlot.ResolveRuntimeEntity();
                if (!IsReadyOwner(ownerSlot.OwnerAsc, asc))
                    continue;
                if (!Continuations.HasBuffer(asc) || !Activations.HasBuffer(asc) ||
                    !PendingCommands.HasBuffer(asc))
                {
                    execution.PreAdmissionFailure =
                        GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                    return;
                }
                if (HasOverdueWaitCommand(
                        PendingCommands[asc], execution.CandidateTick))
                {
                    execution.PreAdmissionFailure =
                        GasTickAdmissionFailureReason.WaitRouteOverdue;
                    return;
                }
                if (!PlanEndingRoutes(
                        asc, ref execution, ref routeCount) ||
                    !PlanObservedOwnerGoneRoutes(
                        asc, ref execution, ref routeCount) ||
                    !PlanForwardedWaitRoutes(
                        ownerSlot.OwnerAsc, asc, ref execution, ref routeCount) ||
                    !PlanControlResponseRoutes(
                        ownerSlot.OwnerAsc, asc, ref execution, ref routeCount))
                    return;
            }
            AssignRouteSequences(ref execution, routeCount, ref nextStableSequence);
        }

        /// <summary>
        /// 只有仍处于 Ready/Alive 的 registry 实体可充当 Ability route 的 owner writer。
        /// </summary>
        private bool IsReadyOwner(in OwnerAscHandle owner, Entity asc)
        {
            if (!AscIdentities.HasComponent(asc) || !AscLifecycles.HasComponent(asc))
                return false;
            var identity = AscIdentities[asc];
            var state = AscLifecycles[asc].State;
            return identity.SimulationEpoch == SimulationEpoch && identity.OwnerAsc.Equals(owner) &&
                   (state == GasAscLifecycleState.Ready || state == GasAscLifecycleState.Alive);
        }

        /// <summary>
        /// 为当前或本 Tick 即将进入 Ending 的 Activation 冻结逐 Continuation unsubscribe。
        /// </summary>
        private bool PlanEndingRoutes(
            Entity asc,
            ref GasTickExecutionState execution,
            ref int routeCount)
        {
            var activations = Activations[asc];
            var continuations = Continuations[asc];
            for (var activationIndex = 0; activationIndex < activations.Length; activationIndex++)
            {
                var activation = activations[activationIndex];
                if (activation.Header.StorageState != GasSlabSlotState.Live ||
                    !WillEnterEnding(in activation, asc, in execution))
                    continue;
                for (var index = 0; index < continuations.Length; index++)
                {
                    var slot = continuations[index];
                    if (!slot.Activation.Equals(activation.Handle) ||
                        !GasAbilityWaitSlabTransaction.RequiresOwnerCancellation(in slot))
                        continue;
                    if (!GasAbilityWaitSlabTransaction.TryPlanOwnerCancellation(
                            in slot, execution.CandidateTick, out var command))
                    {
                        execution.PreAdmissionFailure =
                            GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                        return false;
                    }
                    if (!TryAddRoute(command.TargetAsc, in command, ref routeCount))
                    {
                        execution.PreAdmissionFailure = GasTickAdmissionFailureReason.NextTickRouteLimit;
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// 将 owner slab 中尚未到 observed writer 的 registration/signal/unsubscribe 原命令转发到目标 ASC。
        /// </summary>
        private bool PlanForwardedWaitRoutes(
            in OwnerAscHandle owner,
            Entity asc,
            ref GasTickExecutionState execution,
            ref int routeCount)
        {
            var commands = PendingCommands[asc];
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Header.StorageState != GasSlabSlotState.Live ||
                    command.State != GasSlotBusinessState.Pending ||
                    command.AvailableTick != execution.CandidateTick ||
                    !command.SourceAsc.Equals(owner) || command.TargetAsc.Equals(owner) ||
                    !IsForwardedWaitKind(command.CommandKind))
                    continue;
                if (execution.CandidateTick == ulong.MaxValue)
                {
                    execution.PreAdmissionFailure =
                        GasTickAdmissionFailureReason.EnvelopeArithmeticOverflow;
                    return false;
                }
                var forwarded = command;
                forwarded.AvailableTick = execution.CandidateTick + 1;
                if (!TryAddRoute(forwarded.TargetAsc, in forwarded, ref routeCount))
                {
                    execution.PreAdmissionFailure = GasTickAdmissionFailureReason.NextTickRouteLimit;
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 仅允许跨 ASC wait 的原始输入跨 owner 边界转发，Ack/Completion 不得被再次转发。
        /// </summary>
        private static bool IsForwardedWaitKind(int commandKind)
        {
            return commandKind == (int)GasAbilityPendingCommandKind.WaitRegistration ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitSignal ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitUnsubscribe;
        }

        /// <summary>
        /// 过期 wait 命令不允许静默滞留；它们必须在 admission 前锁存确定性 fault。
        /// </summary>
        private static bool HasOverdueWaitCommand(
            DynamicBuffer<PendingCommand> commands,
            ulong candidateTick)
        {
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Header.StorageState == GasSlabSlotState.Live &&
                    command.State == GasSlotBusinessState.Pending &&
                    IsWaitRouteKind(command.CommandKind) &&
                    command.AvailableTick < candidateTick)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 覆盖原始 wait 输入与 response 的完整闭世界命令集合。
        /// </summary>
        private static bool IsWaitRouteKind(int commandKind)
        {
            return IsForwardedWaitKind(commandKind) ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitRegistrationAck ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitCompletion ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitUnsubscribeAck;
        }

        /// <summary>
        /// 只为仍存活的 subscriber 规划 observed 输入，避免 signal 先改写后再丢弃。
        /// </summary>
        private bool CanDeliverObservedWaitInput(
            Entity observedAsc,
            in PendingCommand command)
        {
            if (command.CommandKind != (int)GasAbilityPendingCommandKind.WaitSignal)
                return TryResolveOwner(command.SourceAsc, out _);
            if (!TryResolveSubscription(
                    command.Subscription,
                    Subscriptions[observedAsc],
                    out var subscription))
                return false;
            return TryResolveOwner(subscription.SubscriberAsc, out _);
        }

        /// <summary>
        /// observed owner 已退出 Ready/Alive 时为非 Ending Continuation 冻结唯一 T+1 completion。
        /// </summary>
        private bool PlanObservedOwnerGoneRoutes(
            Entity asc,
            ref GasTickExecutionState execution,
            ref int routeCount)
        {
            var activations = Activations[asc];
            var continuations = Continuations[asc];
            var commands = PendingCommands[asc];
            for (var index = 0; index < continuations.Length; index++)
            {
                var slot = continuations[index];
                if (!GasAbilityWaitSlabTransaction.RequiresOwnerCancellation(in slot) ||
                    TryResolveOwner(slot.ObservedAsc, out _) ||
                    HasPendingRegistrationCommand(in slot, commands) ||
                    HasPendingOwnerGoneCompletion(in slot, commands))
                    continue;
                if (!TryResolveActivation(in slot.Activation, activations, out var activation))
                {
                    execution.PreAdmissionFailure =
                        GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                    return false;
                }
                if (WillEnterEnding(in activation, asc, in execution))
                    continue;
                if (!GasAbilityWaitSlabTransaction.TryPlanObservedOwnerGoneCompletion(
                        in slot, execution.CandidateTick, out var completion))
                {
                    execution.PreAdmissionFailure =
                        GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                    return false;
                }
                if (!TryAddRoute(completion.TargetAsc, in completion, ref routeCount))
                {
                    execution.PreAdmissionFailure = GasTickAdmissionFailureReason.NextTickRouteLimit;
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// PendingRegistration 由缺失 destination 的原始 route 统一转回 completion，避免双回执。
        /// </summary>
        private static bool HasPendingRegistrationCommand(
            in AbilityContinuationSlot continuation,
            DynamicBuffer<PendingCommand> commands)
        {
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Header.StorageState == GasSlabSlotState.Live &&
                    command.State == GasSlotBusinessState.Pending &&
                    command.CommandKind == (int)GasAbilityPendingCommandKind.WaitRegistration &&
                    command.SourceAsc.Equals(continuation.Handle.OwnerAsc) &&
                    command.TargetAsc.Equals(continuation.ObservedAsc) &&
                    command.Activation.Equals(continuation.Activation) &&
                    command.Continuation.Equals(continuation.Handle) &&
                    command.RegistrationGeneration == continuation.RegistrationGeneration &&
                    command.WaitSemantic == continuation.WaitSemantic &&
                    command.WaitPolicy == continuation.WaitPolicy &&
                    command.CommandSequence != 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 防止 owner 在消费上一 Tick owner-gone completion 前重复规划同一终态消息。
        /// </summary>
        private static bool HasPendingOwnerGoneCompletion(
            in AbilityContinuationSlot continuation,
            DynamicBuffer<PendingCommand> commands)
        {
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Header.StorageState == GasSlabSlotState.Live &&
                    command.State == GasSlotBusinessState.Pending &&
                    command.CommandKind == (int)GasAbilityPendingCommandKind.WaitCompletion &&
                    command.CompletionReason == GasAbilityWaitCompletionReason.ObservedOwnerGone &&
                    command.SourceAsc.Equals(continuation.ObservedAsc) &&
                    command.TargetAsc.Equals(continuation.Handle.OwnerAsc) &&
                    command.Activation.Equals(continuation.Activation) &&
                    command.Continuation.Equals(continuation.Handle) &&
                    command.RegistrationGeneration == continuation.RegistrationGeneration &&
                    command.WaitSemantic == continuation.WaitSemantic &&
                    command.WaitPolicy == continuation.WaitPolicy &&
                    command.CommandSequence != 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 解析 owner-local live Activation，并复验完整 handle 以供 route shadow 使用。
        /// </summary>
        private static bool TryResolveActivation(
            in AbilityActivationHandle handle,
            DynamicBuffer<AbilityActivationSlot> activations,
            out AbilityActivationSlot activation)
        {
            activation = default;
            if (!handle.IsValid || handle.SlotIndex < 0 || handle.SlotIndex >= activations.Length)
                return false;
            activation = activations[handle.SlotIndex];
            return activation.Header.StorageState == GasSlabSlotState.Live &&
                   activation.Header.Generation == handle.SlotGeneration &&
                   activation.Handle.Equals(handle);
        }

        /// <summary>
        /// 为本 Tick due registration、signal 与 unsubscribe 冻结 observed writer 的完整响应路由。
        /// </summary>
        private bool PlanControlResponseRoutes(
            in OwnerAscHandle owner,
            Entity asc,
            ref GasTickExecutionState execution,
            ref int routeCount)
        {
            var commands = PendingCommands[asc];
            var previousIndex = -1;
            for (var ordinal = 0; ordinal < commands.Length; ordinal++)
            {
                var index = FindNextPlannableWaitCommand(
                    commands, execution.CandidateTick, previousIndex);
                if (index < 0)
                    break;
                var command = commands[index];
                previousIndex = index;
                if (!command.TargetAsc.Equals(owner))
                    continue;
                if (!CanDeliverObservedWaitInput(asc, in command))
                    continue;
                if (command.CommandKind ==
                        (int)GasAbilityPendingCommandKind.WaitRegistration &&
                    HasPlannedRegistrationResponse(in command, routeCount))
                    continue;
                if (!TryPlanControlResponse(
                        in owner, asc, in command, execution.CandidateTick,
                        routeCount, out _, out var response))
                {
                    if (command.CommandKind ==
                        (int)GasAbilityPendingCommandKind.WaitUnsubscribe)
                    {
                        execution.PreAdmissionFailure =
                            GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                        return false;
                    }
                    continue;
                }
                if (!TryAddControlRoute(
                        response.TargetAsc, in response, in command, ref routeCount))
                {
                    execution.PreAdmissionFailure = GasTickAdmissionFailureReason.NextTickRouteLimit;
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 同一 Continuation 的重复注册只保留首条 Ack/Completion route，后续原命令仍会被终结。
        /// </summary>
        private bool HasPlannedRegistrationResponse(
            in PendingCommand registration,
            int routeCount)
        {
            for (var index = 0; index < routeCount; index++)
            {
                var route = AbilityRoutes[index];
                if (route.Used == 0 || route.RequiresOwnerApply == 0 ||
                    route.OriginCommandKind !=
                    (int)GasAbilityPendingCommandKind.WaitRegistration)
                    continue;
                var response = route.Command;
                if ((response.CommandKind !=
                         (int)GasAbilityPendingCommandKind.WaitRegistrationAck &&
                     response.CommandKind !=
                         (int)GasAbilityPendingCommandKind.WaitCompletion) ||
                    !response.SourceAsc.Equals(registration.TargetAsc) ||
                    !response.TargetAsc.Equals(registration.SourceAsc) ||
                    !response.Activation.Equals(registration.Activation) ||
                    !response.Continuation.Equals(registration.Continuation) ||
                    response.RegistrationGeneration != registration.RegistrationGeneration ||
                    response.WaitSemantic != registration.WaitSemantic ||
                    response.WaitPolicy != registration.WaitPolicy)
                    continue;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 按正式 PendingCommand key 选择下一条需 observed writer 处理的 due 输入。
        /// </summary>
        private static int FindNextPlannableWaitCommand(
            DynamicBuffer<PendingCommand> commands,
            ulong candidateTick,
            int previousIndex)
        {
            var selectedIndex = -1;
            var previous = previousIndex >= 0 ? commands[previousIndex] : default;
            for (var index = 0; index < commands.Length; index++)
            {
                var candidate = commands[index];
                if (!IsPlannableWaitCommand(in candidate, candidateTick) ||
                    previousIndex >= 0 && CompareCommandSlot(
                        in candidate, index, in previous, previousIndex) <= 0)
                    continue;
                var selected = selectedIndex >= 0 ? commands[selectedIndex] : default;
                if (selectedIndex < 0 || CompareCommandSlot(
                        in candidate, index, in selected, selectedIndex) < 0)
                    selectedIndex = index;
            }
            return selectedIndex;
        }

        /// <summary>
        /// 判断命令是否属于 sample/signal/unsubscribe observed-side 闭世界输入。
        /// </summary>
        private static bool IsPlannableWaitCommand(
            in PendingCommand command,
            ulong candidateTick)
        {
            if (command.Header.StorageState != GasSlabSlotState.Live ||
                command.State != GasSlotBusinessState.Pending ||
                command.AvailableTick != candidateTick)
                return false;
            return command.CommandKind == (int)GasAbilityPendingCommandKind.WaitRegistration ||
                   command.CommandKind == (int)GasAbilityPendingCommandKind.WaitSignal ||
                   command.CommandKind == (int)GasAbilityPendingCommandKind.WaitUnsubscribe;
        }

        /// <summary>
        /// 用正式命令键比较两个 slab 槽，完全等价时只以稳定 non-compacting slot 收口。
        /// </summary>
        private static int CompareCommandSlot(
            in PendingCommand left,
            int leftIndex,
            in PendingCommand right,
            int rightIndex)
        {
            var comparison = GasAbilityPendingCommandOrder.Compare(in left, in right);
            return comparison != 0 ? comparison : leftIndex.CompareTo(rightIndex);
        }

        /// <summary>
        /// 只读解释 observed 输入；无匹配/stale 是业务 no-op，绝不伪造 response route。
        /// </summary>
        private bool TryPlanControlResponse(
            in OwnerAscHandle owner,
            Entity asc,
            in PendingCommand command,
            ulong candidateTick,
            int routeCount,
            out GasAbilityWaitProtocolStatus status,
            out PendingCommand response)
        {
            response = default;
            if (command.CommandKind == (int)GasAbilityPendingCommandKind.WaitRegistration)
                return TryPlanRegistrationResponse(
                    in owner, asc, in command, candidateTick, out status, out response);
            if (command.CommandKind == (int)GasAbilityPendingCommandKind.WaitSignal)
                return TryPlanSignalResponse(
                    in owner, asc, in command, candidateTick, routeCount, out status, out response);
            status = GasAbilityWaitProtocolStatus.InvalidRequest;
            if (!GasAbilityWaitSlabTransaction.TryPlanUnsubscribeAck(
                    in owner, in command, candidateTick, out response))
                return false;
            status = GasAbilityWaitProtocolStatus.Ending;
            return true;
        }

        /// <summary>
        /// 在 observed tick-start authority 上 sample+register，并冻结 Ack 或即时 Completion 模板。
        /// </summary>
        private bool TryPlanRegistrationResponse(
            in OwnerAscHandle owner,
            Entity asc,
            in PendingCommand command,
            ulong candidateTick,
            out GasAbilityWaitProtocolStatus status,
            out PendingCommand response)
        {
            status = GasAbilityWaitProtocolStatus.InvalidRequest;
            response = default;
            if (!GasAbilityWaitObservationUtility.TrySampleRegistration(
                    in owner, in command, TagCounts[asc], Grants[asc], Activations[asc],
                    Continuations[asc], Subscriptions[asc], Cooldowns[asc], ActiveEffects[asc],
                    out var sample))
                return false;
            status = GasAbilityWaitSlabTransaction.PlanSampleAndRegisterObserved(
                in owner, in command, in sample, candidateTick, Subscriptions[asc],
                out response, out _);
            return status == GasAbilityWaitProtocolStatus.Registered ||
                   status == GasAbilityWaitProtocolStatus.Completed;
        }

        /// <summary>
        /// 在叠加本 Tick前序 wake 的虚拟 Subscription 上冻结独立 T+1 completion。
        /// </summary>
        private bool TryPlanSignalResponse(
            in OwnerAscHandle owner,
            Entity asc,
            in PendingCommand command,
            ulong candidateTick,
            int routeCount,
            out GasAbilityWaitProtocolStatus status,
            out PendingCommand response)
        {
            status = GasAbilityWaitProtocolStatus.NoOpStale;
            response = default;
            if (!TryResolveSubscription(command.Subscription, Subscriptions[asc], out var subscription))
                return false;
            ApplyPlannedWakeShadow(ref subscription, routeCount);
            status = GasAbilityWaitSlabTransaction.PlanWakeObserved(
                in owner, in subscription, in command, candidateTick, out response);
            if (status != GasAbilityWaitProtocolStatus.Completed &&
                status != GasAbilityWaitProtocolStatus.PersistentWake)
                return false;
            response.RegistrationSequence = subscription.RegistrationSequence;
            response.RecipientKindPriority = command.RecipientKindPriority;
            response.MatchedTagDepth = command.MatchedTagDepth;
            return true;
        }

        /// <summary>
        /// 把已冻结的同 Subscription wake 路由叠加到只读副本，保证 persistent ordinal 连续。
        /// </summary>
        private void ApplyPlannedWakeShadow(
            ref AbilitySubscriptionSlot subscription,
            int routeCount)
        {
            for (var index = 0; index < routeCount; index++)
            {
                var route = AbilityRoutes[index];
                var response = route.Command;
                if (route.Used == 0 || route.OriginCommandKind !=
                    (int)GasAbilityPendingCommandKind.WaitSignal ||
                    response.CommandKind != (int)GasAbilityPendingCommandKind.WaitCompletion ||
                    !response.Subscription.Equals(subscription.Handle))
                    continue;
                subscription.WakeOrdinal = response.WakeOrdinal;
                subscription.ObservedRevisionAtRegister = response.ObservedRevision;
                if (subscription.WaitPolicy == GasAbilityWaitPolicy.OneShot)
                    subscription.State = GasAbilitySubscriptionState.Completed;
            }
        }

        /// <summary>
        /// 解析 observed owner-local live Subscription 并复验完整 typed handle。
        /// </summary>
        private static bool TryResolveSubscription(
            in AbilitySubscriptionHandle handle,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            out AbilitySubscriptionSlot subscription)
        {
            subscription = default;
            if (!handle.IsValid || handle.SlotIndex < 0 || handle.SlotIndex >= subscriptions.Length)
                return false;
            subscription = subscriptions[handle.SlotIndex];
            return subscription.Header.StorageState == GasSlabSlotState.Live &&
                   subscription.Header.Generation == handle.SlotGeneration &&
                   subscription.Handle.Equals(handle);
        }

        /// <summary>
        /// 由当前 phase、accepted Cancel 或 due CancelImmediately grant removal 判定 Ending shadow。
        /// </summary>
        private bool WillEnterEnding(
            in AbilityActivationSlot activation,
            Entity asc,
            in GasTickExecutionState execution)
        {
            if (activation.Phase == GasAbilityActivationPhase.Ending)
                return true;
            for (var index = 0; index < execution.OwnerPlanCount; index++)
            {
                var plan = OwnerPlans[index];
                if (plan.BusinessAccepted != 0 && plan.CommandKind == GasBoundaryCommandKind.Cancel &&
                    plan.Activation.Equals(activation.Handle))
                    return true;
            }
            var commands = PendingCommands[asc];
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Header.StorageState == GasSlabSlotState.Live &&
                    command.State == GasSlotBusinessState.Pending &&
                    command.AvailableTick <= execution.CandidateTick &&
                    command.CommandKind == (int)GasAbilityPendingCommandKind.RequestGrantedRemoval &&
                    command.GrantedRemovalPolicy == GasGrantedAbilityRemovalPolicy.CancelImmediately &&
                    command.GrantedAbility.Equals(activation.GrantedAbility))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 在固定 scratch 内追加一条未分配持久身份的 route。
        /// </summary>
        private bool TryAddRoute(
            in OwnerAscHandle destination,
            in PendingCommand command,
            ref int routeCount)
        {
            if (!destination.IsValid || routeCount < 0 || routeCount >= AbilityRoutes.Length)
                return false;
            AbilityRoutes[routeCount++] = new GasAbilityRouteRecord
            {
                DestinationAsc = destination,
                Command = command,
                OriginCommandSequence = command.CommandSequence,
                OriginCommandKind = command.CommandKind,
                RequiresSourceConsume = command.CommandSequence != 0 &&
                    IsForwardedWaitKind(command.CommandKind) ? (byte)1 : (byte)0,
                Used = 1,
            };
            return true;
        }

        /// <summary>
        /// 追加一条需 OwnerWave 以 origin command 实际提交或撤销的 response route。
        /// </summary>
        private bool TryAddControlRoute(
            in OwnerAscHandle destination,
            in PendingCommand response,
            in PendingCommand origin,
            ref int routeCount)
        {
            if (!destination.IsValid || origin.CommandSequence == 0 ||
                routeCount < 0 || routeCount >= AbilityRoutes.Length)
                return false;
            AbilityRoutes[routeCount++] = new GasAbilityRouteRecord
            {
                DestinationAsc = destination,
                Command = response,
                OriginCommandSequence = origin.CommandSequence,
                OriginCommandKind = origin.CommandKind,
                RequiresOwnerApply = 1,
                Used = 1,
            };
            return true;
        }

        /// <summary>
        /// 按正式 route key 排序并从 Session 单调序列一次冻结全部内部消息序号。
        /// </summary>
        private void AssignRouteSequences(
            ref GasTickExecutionState execution,
            int routeCount,
            ref ulong nextStableSequence)
        {
            execution.AbilityRouteCount = routeCount;
            execution.FirstAbilityRouteStableSequence = nextStableSequence;
            if ((ulong)routeCount > ulong.MaxValue - nextStableSequence)
            {
                execution.PreAdmissionFailure =
                    GasTickAdmissionFailureReason.EnvelopeArithmeticOverflow;
                return;
            }
            AbilityRoutes.GetSubArray(0, routeCount).Sort(new GasAbilityRouteComparer());
            for (var index = 0; index < routeCount; index++)
            {
                var route = AbilityRoutes[index];
                route.Command.CommandSequence = nextStableSequence++;
                AbilityRoutes[index] = route;
            }
        }

        /// <summary>
        /// 解析 command source ASC 后委托纯 planner，owner 缺失仅形成业务拒绝。
        /// </summary>
        private GasOwnerPlanRecord BuildPlan(
            in BoundaryCommandInbox command,
            ref GasDefinitionCatalogBlob catalog,
            in GasTickExecutionState execution,
            int planOrdinal)
        {
            var owner = ResolveCommandOwner(in command);
            var planningCommand = command;
            planningCommand.SourceAsc = owner;
            if (!TryResolveOwner(owner, out var asc))
            {
                return new GasOwnerPlanRecord
                {
                    SealedCommandOrdinal = planOrdinal,
                    OwnerAsc = owner,
                    SourceSequence = command.SourceSequence,
                    SubjectHandle = command.SubjectHandle,
                    CommandKind = command.CommandKind,
                    Result = GasAbilityCommandResult.OwnerNotReady,
                    CostAttributeLayoutIndex = -1,
                    CooldownOwnedTagIndex = -1,
                };
            }
            var plan = GasAbilityOwnerPlanUtility.Build(
                in planningCommand,
                SimulationEpoch,
                execution.CandidateTick,
                ref catalog,
                Grants[asc],
                Activations[asc],
                Attributes[asc],
                TagCounts[asc],
                Cooldowns[asc],
                PendingCommands[asc],
                OwnerPlans,
                planOrdinal);
            plan.SealedCommandOrdinal = planOrdinal;
            return plan;
        }

        /// <summary>
        /// 解析 command 的 source owner；无显式 source 的直接 Effect 以显式 target owner 作为 source。
        /// </summary>
        private static OwnerAscHandle ResolveCommandOwner(
            in BoundaryCommandInbox command)
        {
            return command.CommandKind == GasBoundaryCommandKind.ApplyEffect &&
                   command.HasSource == 0 &&
                   command.Target.Kind == GasBoundaryTargetKind.Asc
                ? command.Target.TargetAsc
                : command.SourceAsc;
        }

        /// <summary>
        /// 通过 Session registry 解析唯一 Ready ASC Entity，并复验 ASC 稳定身份与 lifecycle。
        /// </summary>
        private bool TryResolveOwner(in OwnerAscHandle owner, out Entity asc)
        {
            asc = Entity.Null;
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State != GasAscRegistryState.Ready || !slot.OwnerAsc.Equals(owner))
                    continue;
                var candidate = slot.ResolveRuntimeEntity();
                if (!AscIdentities.HasComponent(candidate) || !AscLifecycles.HasComponent(candidate) ||
                    !Grants.HasBuffer(candidate) || !Activations.HasBuffer(candidate) ||
                    !Continuations.HasBuffer(candidate) || !Subscriptions.HasBuffer(candidate) ||
                    !ActiveEffects.HasBuffer(candidate) || !Attributes.HasBuffer(candidate) ||
                    !TagCounts.HasBuffer(candidate) || !Cooldowns.HasBuffer(candidate) ||
                    !PendingCommands.HasBuffer(candidate))
                    return false;
                var lifecycle = AscLifecycles[candidate].State;
                if (!AscIdentities[candidate].OwnerAsc.Equals(owner) ||
                    (lifecycle != GasAscLifecycleState.Ready && lifecycle != GasAscLifecycleState.Alive))
                    return false;
                asc = candidate;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// 将 sealed ApplyEffect 命令解析为稳定 target binding 与 effect operation scratch。
    /// </summary>
    internal struct GasTargetResolveExpandJob : IJob
    {
        public Entity Session;
        public ulong SimulationEpoch;
        [ReadOnly] public BlobAssetReference<GasDefinitionCatalogBlob> Catalog;
        [ReadOnly] public NativeArray<PlanExpandScratchEnvelopeToken> Envelope;
        [ReadOnly] public NativeArray<GasSealedCommand> SealedCommands;
        [ReadOnly] public NativeArray<GasOwnerPlanRecord> OwnerPlans;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public ComponentLookup<AscLifecycle> AscLifecycles;
        [ReadOnly] public ComponentLookup<GasActorBinding> ActorBindings;
        public NativeArray<GasResolvedTargetRecord> ResolvedTargets;
        public NativeArray<GasEffectOperationRecord> EffectOperations;
        public NativeArray<GasTargetResolveRejectionRecord> TargetResolveRejections;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 展开成功 operation；无法建立 binding 的 planned application 写入 bounded typed rejection。
        /// </summary>
        public void Execute()
        {
            var execution = Execution[0];
            if (execution.GameplayEnabled == 0)
                return;
            execution.LaneMask |= GasTickLaneMask.TargetResolve;
            if (Envelope[0].FailureReason == GasTickAdmissionFailureReason.None)
            {
                execution.ResolvedTargetCount = 0;
                execution.EffectOperationCount = 0;
                ref var catalog = ref Catalog.Value;
                execution.TargetResolveRejectionCount = 0;
                for (var index = 0; index < execution.StoredSealedCommandCount; index++)
                {
                    var command = SealedCommands[index].Command;
                    if (command.CommandKind != GasBoundaryCommandKind.ApplyEffect ||
                        index >= execution.OwnerPlanCount ||
                        OwnerPlans[index].BusinessAccepted == 0 ||
                        OwnerPlans[index].StableSequence == 0)
                        continue;
                    var plan = OwnerPlans[index];
                    if (!TryComposeApplicationId(
                            plan.StableSequence,
                            0,
                            out var directApplicationId))
                    {
                        execution.PreAdmissionFailure =
                            GasTickAdmissionFailureReason.EnvelopeArithmeticOverflow;
                        break;
                    }
                    var directSourceAsc = command.HasSource != 0 && command.SourceAsc.IsValid
                        ? command.SourceAsc
                        : plan.OwnerAsc;
                    if (!GasDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                            ref catalog, command.DefinitionId, out var definitionIndex))
                    {
                        if (!TryRecordRejection(
                                ref execution,
                                index,
                                0,
                                -1,
                                directSourceAsc,
                                ExtractTargetAsc(in command.Target),
                                directApplicationId,
                                plan.StableSequence,
                                GasGameplayEffectApplicationOutcome.RejectedDefinition,
                                GasGameplayEffectTransactionFailure.InvalidDefinition))
                            break;
                        continue;
                    }
                    var definition = catalog.GameplayEffects[definitionIndex];
                    if (!TryResolveTarget(
                            in command.Target,
                            ref catalog,
                            in definition,
                            in directSourceAsc,
                            out var targetAsc,
                            out var targetAlive))
                    {
                        if (!TryRecordRejection(
                                ref execution,
                                index,
                                0,
                                definitionIndex,
                            directSourceAsc,
                            ExtractTargetAsc(in command.Target),
                                directApplicationId,
                                plan.StableSequence,
                                GasGameplayEffectApplicationOutcome.RejectedStaleBinding,
                                GasGameplayEffectTransactionFailure.InvalidIdentity))
                            break;
                        continue;
                    }
                    var sourceAsc = command.HasSource != 0 ? command.SourceAsc : targetAsc;
                        if (!TryAddOperation(
                            ref execution,
                            index,
                            0,
                            definitionIndex,
                            sourceAsc,
                            targetAsc,
                            in command.Target,
                            directApplicationId,
                            execution.CandidateTick,
                            plan.StableSequence,
                            targetAlive))
                        break;
                }
                for (var index = 0; index < execution.OwnerPlanCount; index++)
                {
                    var plan = OwnerPlans[index];
                    if (plan.BusinessAccepted == 0 ||
                        plan.CommandKind != GasBoundaryCommandKind.Commit ||
                        plan.StableSequence == 0 ||
                        plan.DefinitionIndex < 0 ||
                        plan.DefinitionIndex >= catalog.Abilities.Length)
                        continue;
                    var ability = catalog.Abilities[plan.DefinitionIndex];
                    var nodes = ability.DirectEffectProgramRange;
                    if (nodes.Count == 0)
                        continue;
                    for (var nodeOffset = 0; nodeOffset < nodes.Count; nodeOffset++)
                    {
                        var node = catalog.DirectEffectProgramNodes[nodes.Start + nodeOffset];
                        if (!TryComposeApplicationId(
                                plan.StableSequence,
                                node.NodeOrdinal,
                                out var applicationId))
                        {
                            execution.PreAdmissionFailure =
                                GasTickAdmissionFailureReason.EnvelopeArithmeticOverflow;
                            break;
                        }
                        var target = plan.HasTarget != 0
                            ? plan.Target
                            : BoundaryTargetRef.None;
                        if (!GasDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                                ref catalog, node.EffectDefinitionId, out var definitionIndex))
                        {
                            if (!TryRecordRejection(
                                    ref execution,
                                    index,
                                    node.NodeOrdinal,
                                    -1,
                                    plan.OwnerAsc,
                                    ExtractTargetAsc(in target),
                                    applicationId,
                                    plan.StableSequence,
                                    GasGameplayEffectApplicationOutcome.RejectedDefinition,
                                    GasGameplayEffectTransactionFailure.InvalidDefinition))
                                break;
                            continue;
                        }
                        var definition = catalog.GameplayEffects[definitionIndex];
                        target = ResolveAbilityTarget(in plan, in ability, in definition.TargetPolicy);
                        if (!TryResolveTarget(
                                in target,
                                ref catalog,
                                in definition,
                                in plan.OwnerAsc,
                                out var targetAsc,
                                out var targetAlive))
                        {
                            if (!TryRecordRejection(
                                    ref execution,
                                    index,
                                    node.NodeOrdinal,
                                    definitionIndex,
                                    plan.OwnerAsc,
                                    ExtractTargetAsc(in target),
                                    applicationId,
                                    plan.StableSequence,
                                    GasGameplayEffectApplicationOutcome.RejectedStaleBinding,
                                    GasGameplayEffectTransactionFailure.InvalidIdentity))
                                break;
                            continue;
                        }
                        if (!TryAddOperation(
                            ref execution,
                            index,
                            node.NodeOrdinal,
                            definitionIndex,
                            plan.OwnerAsc,
                            targetAsc,
                            in target,
                            applicationId,
                                execution.CandidateTick,
                                plan.StableSequence,
                                targetAlive))
                            break;
                    }
                }
            }
            Execution[0] = execution;
        }

        /// <summary>
        /// 把 TargetResolve 失败固定为可排序 rejection，防止 planned application 被 continue 丢失。
        /// </summary>
        private bool TryRecordRejection(
            ref GasTickExecutionState execution,
            int ownerPlanOrdinal,
            int programNodeOrdinal,
            int definitionIndex,
            in OwnerAscHandle sourceAsc,
            in OwnerAscHandle targetAsc,
            ulong applicationId,
            ulong causalityId,
            GasGameplayEffectApplicationOutcome outcome,
            GasGameplayEffectTransactionFailure failure)
        {
            if (applicationId == 0 ||
                execution.TargetResolveRejectionCount >= TargetResolveRejections.Length)
            {
                execution.PreAdmissionFailure =
                    GasTickAdmissionFailureReason.EffectOperationLimit;
                return false;
            }
            TargetResolveRejections[execution.TargetResolveRejectionCount++] =
                new GasTargetResolveRejectionRecord
                {
                    OwnerPlanOrdinal = ownerPlanOrdinal,
                    ProgramNodeOrdinal = programNodeOrdinal,
                    DefinitionIndex = definitionIndex,
                    SourceAsc = sourceAsc,
                    TargetAsc = targetAsc,
                    ApplicationId = applicationId,
                    CausalityId = causalityId,
                    Scope = GasBoundaryFactScope.Session,
                    Outcome = outcome,
                    Failure = failure,
                };
            return true;
        }

        /// <summary>
        /// 从输入目标提取稳定 ASC 身份；selector/rule 不伪造目标句柄。
        /// </summary>
        private static OwnerAscHandle ExtractTargetAsc(in BoundaryTargetRef target)
        {
            return target.Kind == GasBoundaryTargetKind.Asc && target.TargetAsc.IsValid
                ? target.TargetAsc
                : default;
        }

        /// <summary>
        /// 解析 Ability 的稳定 ASC target，并拒绝 Self 定义携带远端显式目标。
        /// </summary>
        private BoundaryTargetRef ResolveAbilityTarget(
            in GasOwnerPlanRecord plan,
            in GasAbilityDefinitionBlob ability,
            in GasTargetPolicyBlob applicationPolicy)
        {
            if (ability.TargetPolicy.LogicalTarget == GasLogicalTargetPolicy.Self)
            {
                if (plan.HasTarget != 0 &&
                    (plan.Target.Kind != GasBoundaryTargetKind.Asc ||
                     !plan.Target.TargetAsc.Equals(plan.OwnerAsc)))
                    return BoundaryTargetRef.None;
                var registry = Registries[Session];
                for (var index = 0; index < registry.Length; index++)
                {
                    var slot = registry[index];
                    if (!slot.OwnerAsc.Equals(plan.OwnerAsc))
                        continue;
                    var battle = slot.BattleInstance;
                    if (!TryResolveSelfSpatial(in plan, in battle, applicationPolicy.Spatial,
                            out var spatialSnapshot))
                        return BoundaryTargetRef.None;
                    return TryResolveSelfAvatar(
                        in plan,
                        in battle,
                        applicationPolicy.Avatar,
                        in spatialSnapshot,
                        slot.ResolveRuntimeEntity(),
                        out var target)
                        ? target
                        : BoundaryTargetRef.None;
                }
            }
            return plan.HasTarget != 0 ? plan.Target : BoundaryTargetRef.None;
        }

        /// <summary>
        /// 为 Self application 复制已冻结空间样本；FrozenSpatial 缺失时明确拒绝而不降级为 ASC。
        /// </summary>
        private static bool TryResolveSelfSpatial(
            in GasOwnerPlanRecord plan,
            in BattleInstanceHandle battle,
            GasSpatialTargetPolicy policy,
            out GasBoundarySpatialSnapshot snapshot)
        {
            snapshot = GasBoundarySpatialSnapshot.None;
            var hasTarget = plan.HasTarget != 0;
            var target = plan.Target;
            var hasSnapshot = hasTarget &&
                              !target.SpatialSnapshot.Equals(GasBoundarySpatialSnapshot.None);
            if (policy == GasSpatialTargetPolicy.None)
                return !hasSnapshot;
            if (policy != GasSpatialTargetPolicy.FrozenSpatial || !hasTarget ||
                target.Kind != GasBoundaryTargetKind.Asc ||
                !target.TargetAsc.Equals(plan.OwnerAsc) ||
                !target.BattleInstance.Equals(battle) || !target.HasFrozenSpatial)
                return false;
            snapshot = target.SpatialSnapshot;
            return true;
        }

        /// <summary>
        /// 为 Self application 读取当前 ActorBinding generation，并拒绝与已冻结 context 不一致的身份。
        /// </summary>
        private bool TryResolveSelfAvatar(
            in GasOwnerPlanRecord plan,
            in BattleInstanceHandle battle,
            GasAvatarTargetPolicy policy,
            in GasBoundarySpatialSnapshot spatialSnapshot,
            Entity asc,
            out BoundaryTargetRef target)
        {
            target = BoundaryTargetRef.None;
            var hasTargetAvatar = plan.HasTarget != 0 &&
                                  (plan.Target.TargetAvatarStableId != 0 ||
                                   plan.Target.TargetAvatarBindingGeneration != 0);
            if (hasTargetAvatar &&
                (plan.Target.Kind != GasBoundaryTargetKind.Asc ||
                 !plan.Target.TargetAsc.Equals(plan.OwnerAsc) ||
                 !plan.Target.BattleInstance.Equals(battle)))
                return false;
            if (policy == GasAvatarTargetPolicy.FollowAsc)
                return !hasTargetAvatar &&
                       TryCreateAscTarget(in battle, in plan.OwnerAsc, in spatialSnapshot, out target);
            if (policy != GasAvatarTargetPolicy.RequireSameAvatar || !ActorBindings.HasComponent(asc))
                return false;
            var binding = ActorBindings[asc];
            if (binding.AvatarActorStableId == 0 || binding.BindingGeneration == 0)
                return false;
            if (hasTargetAvatar &&
                (plan.Target.TargetAvatarStableId != binding.AvatarActorStableId ||
                 plan.Target.TargetAvatarBindingGeneration != binding.BindingGeneration))
                return false;
            target = BoundaryTargetRef.ForAsc(
                in battle,
                in plan.OwnerAsc,
                binding.AvatarActorStableId,
                binding.BindingGeneration,
                in spatialSnapshot);
            return true;
        }

        /// <summary>
        /// 构造带空间快照的 ASC 目标引用，保持 FollowAsc 的 avatar 字段规范为空。
        /// </summary>
        private static bool TryCreateAscTarget(
            in BattleInstanceHandle battle,
            in OwnerAscHandle owner,
            in GasBoundarySpatialSnapshot spatialSnapshot,
            out BoundaryTargetRef target)
        {
            target = BoundaryTargetRef.ForAsc(in battle, in owner, 0, 0, in spatialSnapshot);
            return true;
        }

        /// <summary>
        /// 通过 Ready registry 解析一个稳定 target，并冻结生命策略看到的 snapshot。
        /// </summary>
        private bool TryResolveTarget(
            in BoundaryTargetRef target,
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition,
            in OwnerAscHandle sourceAsc,
            out OwnerAscHandle owner,
            out byte targetAlive)
        {
            owner = default;
            targetAlive = 0;
            var targetPolicy = definition.TargetPolicy;
            if (targetPolicy.LogicalTarget == GasLogicalTargetPolicy.ResolveAtCommit ||
                !IsSupportedTargetPolicy(targetPolicy) ||
                target.Kind != GasBoundaryTargetKind.Asc || !target.TargetAsc.IsValid)
                return false;
            if (targetPolicy.LogicalTarget == GasLogicalTargetPolicy.Self &&
                !target.TargetAsc.Equals(sourceAsc))
                return false;
            if (!ValidateAvatarSnapshot(in target, targetPolicy.Avatar, out var avatarBindingRequired))
                return false;
            if (!ValidateSpatialSnapshot(ref catalog, in definition, in target, targetPolicy.Spatial))
                return false;
            if (target.SimulationEpoch != SimulationEpoch ||
                !target.BattleInstance.IsValid)
                return false;
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State != GasAscRegistryState.Ready ||
                    !slot.OwnerAsc.Equals(target.TargetAsc) ||
                    !slot.BattleInstance.Equals(target.BattleInstance))
                    continue;
                var asc = slot.ResolveRuntimeEntity();
                if (!AscIdentities.HasComponent(asc) || !AscLifecycles.HasComponent(asc))
                    return false;
                if (avatarBindingRequired && !MatchesFrozenAvatar(asc, in target))
                    return false;
                var identity = AscIdentities[asc];
                var lifecycle = AscLifecycles[asc].State;
                if (!identity.OwnerAsc.Equals(target.TargetAsc) ||
                    identity.SimulationEpoch != target.SimulationEpoch)
                    return false;
                var alive = lifecycle == GasAscLifecycleState.Ready ||
                            lifecycle == GasAscLifecycleState.Alive;
                if (!alive && lifecycle != GasAscLifecycleState.Terminal &&
                    lifecycle != GasAscLifecycleState.Dead)
                    return false;
                owner = target.TargetAsc;
                targetAlive = alive ? (byte)1 : (byte)0;
                return MatchesLifePolicy(targetPolicy.Life, targetAlive);
            }
            return false;
        }

        /// <summary>
        /// 判断 TargetPolicy 四元组是否为 Runtime v1 当前可执行的闭世界组合。
        /// </summary>
        private static bool IsSupportedTargetPolicy(in GasTargetPolicyBlob policy)
        {
            return policy.LogicalTarget >= GasLogicalTargetPolicy.Self &&
                   policy.LogicalTarget <= GasLogicalTargetPolicy.FrozenAsc &&
                   policy.Avatar >= GasAvatarTargetPolicy.FollowAsc &&
                   policy.Avatar <= GasAvatarTargetPolicy.RequireSameAvatar &&
                   policy.Spatial >= GasSpatialTargetPolicy.None &&
                   policy.Spatial <= GasSpatialTargetPolicy.FrozenSpatial &&
                   policy.Life >= GasTargetLifePolicy.AliveOnly &&
                   policy.Life <= GasTargetLifePolicy.AnyLifeState;
        }

        /// <summary>
        /// 校验 Avatar 字段完整性；RequireSameAvatar 必须携带冻结 stable id 与 generation。
        /// </summary>
        private static bool ValidateAvatarSnapshot(
            in BoundaryTargetRef target,
            GasAvatarTargetPolicy policy,
            out bool required)
        {
            required = false;
            var hasStableId = target.TargetAvatarStableId != 0;
            var hasGeneration = target.TargetAvatarBindingGeneration != 0;
            if (hasStableId != hasGeneration)
                return false;
            switch (policy)
            {
                case GasAvatarTargetPolicy.FollowAsc:
                    return !hasStableId && !hasGeneration;
                case GasAvatarTargetPolicy.RequireSameAvatar:
                    required = true;
                    return target.HasAvatarBinding;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 校验空间策略与冻结样本一一对应，ResampleAtApplication 在 v1 中显式拒绝。
        /// </summary>
        private static bool ValidateSpatialSnapshot(
            ref GasDefinitionCatalogBlob catalog,
            in GasGameplayEffectDefinitionBlob definition,
            in BoundaryTargetRef target,
            GasSpatialTargetPolicy policy)
        {
            var hasSnapshot = !target.SpatialSnapshot.Equals(GasBoundarySpatialSnapshot.None);
            switch (policy)
            {
                case GasSpatialTargetPolicy.None:
                    return !hasSnapshot;
                case GasSpatialTargetPolicy.FrozenSpatial:
                    return target.HasFrozenSpatial &&
                           HasRequiredSpatialVariant(
                               ref catalog,
                               definition.TargetDataRange,
                               target.SpatialSnapshot.Variant);
                case GasSpatialTargetPolicy.ResampleAtApplication:
                default:
                    return false;
            }
        }

        /// <summary>
        /// 验证 FrozenSpatial 快照 variant 与 Definition 唯一 Required TargetData descriptor 完全一致。
        /// </summary>
        private static bool HasRequiredSpatialVariant(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            GasTargetDataVariant variant)
        {
            var count = 0;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var descriptor = catalog.TargetDataDescriptors[range.Start + offset];
                if (descriptor.Variant < GasTargetDataVariant.FrozenSpatialPoint ||
                    descriptor.Variant > GasTargetDataVariant.FrozenSpatialShape)
                    continue;
                count++;
                if (descriptor.Variant != variant || descriptor.Required == 0)
                    return false;
            }
            return count == 1;
        }

        /// <summary>
        /// 比较目标 ASC 当前 ActorBinding 与 command 冻结的 Avatar stable identity。
        /// </summary>
        private bool MatchesFrozenAvatar(
            Entity asc,
            in BoundaryTargetRef target)
        {
            if (!ActorBindings.HasComponent(asc))
                return false;
            var binding = ActorBindings[asc];
            return binding.AvatarActorStableId != 0 &&
                   binding.BindingGeneration != 0 &&
                   binding.AvatarActorStableId == target.TargetAvatarStableId &&
                   binding.BindingGeneration == target.TargetAvatarBindingGeneration;
        }

        /// <summary>
        /// 判断冻结 target life policy 是否允许当前 snapshot，禁止未知枚举隐式放行。
        /// </summary>
        private static bool MatchesLifePolicy(GasTargetLifePolicy policy, byte targetAlive)
        {
            switch (policy)
            {
                case GasTargetLifePolicy.AliveOnly:
                    return targetAlive != 0;
                case GasTargetLifePolicy.RequireDead:
                    return targetAlive == 0;
                case GasTargetLifePolicy.AnyLifeState:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 追加一条已完成 target binding 的 Effect operation，并执行 scratch 上限检查。
        /// </summary>
        private bool TryAddOperation(
            ref GasTickExecutionState execution,
            int ownerPlanOrdinal,
            int programNodeOrdinal,
            int definitionIndex,
            in OwnerAscHandle sourceAsc,
            in OwnerAscHandle targetAsc,
            in BoundaryTargetRef target,
            ulong applicationId,
            ulong startTick,
            ulong causalityId,
            byte targetAlive)
        {
            if (applicationId == 0 || execution.ResolvedTargetCount >= ResolvedTargets.Length ||
                execution.EffectOperationCount >= EffectOperations.Length)
            {
                execution.PreAdmissionFailure =
                    GasTickAdmissionFailureReason.EffectOperationLimit;
                return false;
            }
            var targetOrdinal = execution.ResolvedTargetCount++;
            ResolvedTargets[targetOrdinal] = new GasResolvedTargetRecord
            {
                OwnerPlanOrdinal = ownerPlanOrdinal,
                TargetAsc = targetAsc,
                TargetOrdinal = targetOrdinal,
                DefinitionIndex = definitionIndex,
                ApplicationId = applicationId,
                TargetIsAlive = targetAlive,
                CausalityId = causalityId,
                TargetAvatarStableId = target.TargetAvatarStableId,
                TargetAvatarBindingGeneration = target.TargetAvatarBindingGeneration,
                SpatialSnapshot = target.SpatialSnapshot,
            };
            EffectOperations[execution.EffectOperationCount++] = new GasEffectOperationRecord
            {
                OwnerPlanOrdinal = ownerPlanOrdinal,
                TargetOrdinal = targetOrdinal,
                ProgramNodeOrdinal = programNodeOrdinal,
                DefinitionIndex = definitionIndex,
                SourceAsc = sourceAsc,
                TargetAsc = targetAsc,
                ApplicationId = applicationId,
                StartTick = startTick,
                TargetIsAlive = targetAlive,
                CausalityId = causalityId,
                TargetAvatarStableId = target.TargetAvatarStableId,
                TargetAvatarBindingGeneration = target.TargetAvatarBindingGeneration,
                SpatialSnapshot = target.SpatialSnapshot,
            };
            return true;
        }

        /// <summary>
        /// 由 Commit stable sequence 与 DirectEffect node ordinal 构造不重复 application identity。
        /// </summary>
        private static bool TryComposeApplicationId(
            ulong stableSequence,
            int nodeOrdinal,
            out ulong applicationId)
        {
            applicationId = 0;
            if (stableSequence == 0 || nodeOrdinal < 0 || nodeOrdinal >= ushort.MaxValue)
                return false;
            if (stableSequence > (ulong.MaxValue >> 16))
                return false;
            applicationId = (stableSequence << 16) | (uint)(nodeOrdinal + 1);
            return applicationId != 0;
        }

    }

    /// <summary>
    /// 在任何 gameplay authority writer 前统一裁决完整 Tick 的逻辑预算与 reservation contract。
    /// </summary>
    internal struct GasWholeTickInfrastructureAdmissionJob : IJob
    {
        public Entity Session;
        public ulong SimulationEpoch;
        [ReadOnly] public GasScaleProfile Profile;
        [ReadOnly] public BlobAssetReference<GasDefinitionCatalogBlob> Catalog;
        [ReadOnly] public NativeArray<PlanExpandScratchEnvelopeToken> Envelope;
        [ReadOnly] public NativeArray<GasOwnerPlanRecord> OwnerPlans;
        [ReadOnly] public NativeArray<GasResolvedTargetRecord> ResolvedTargets;
        [ReadOnly] public NativeArray<GasEffectOperationRecord> EffectOperations;
        [ReadOnly] public NativeArray<GasTargetResolveRejectionRecord> TargetResolveRejections;
        [ReadOnly] public NativeArray<GasAbilityRouteRecord> AbilityRoutes;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public ComponentLookup<AscLifecycle> AscLifecycles;
        [ReadOnly] public ComponentLookup<AscBattleMembership> Memberships;
        [ReadOnly] public BufferLookup<BattleInstanceSlot> Battles;
        [ReadOnly] public ComponentLookup<AscSlabHeads> SlabHeads;
        [ReadOnly] public BufferLookup<GrantedAbilitySlot> Grants;
        [ReadOnly] public BufferLookup<AbilityActivationSlot> Activations;
        [ReadOnly] public BufferLookup<AbilityContinuationSlot> Continuations;
        [ReadOnly] public BufferLookup<AbilitySubscriptionSlot> Subscriptions;
        [ReadOnly] public BufferLookup<ActiveEffectSlot> ActiveEffects;
        [ReadOnly] public BufferLookup<CooldownGateSlot> Cooldowns;
        [ReadOnly] public BufferLookup<PendingCommand> PendingCommands;
        [ReadOnly] public BufferLookup<AttributeValueSlot> Attributes;
        [ReadOnly] public BufferLookup<AttributeDirtyWord> AttributeDirtyWords;
        [ReadOnly] public BufferLookup<TagCountSlot> TagCounts;
        [ReadOnly] public BufferLookup<TagPresenceWord> TagPresenceWords;
        [ReadOnly] public ComponentLookup<GasPayloadRangeAllocatorState> PayloadStates;
        [ReadOnly] public BufferLookup<GasPayloadRangeRecord> PayloadRanges;
        [ReadOnly] public BufferLookup<GasPayloadValueSlot> PayloadValues;
        [ReadOnly] public ComponentLookup<BoundaryDrainState> BoundaryDrains;
        [ReadOnly] public BufferLookup<BoundaryFactBuffer> BoundaryFacts;
        [ReadOnly] public BufferLookup<GasRequestTerminalIntent> RequestTerminalBuffers;
        [ReadOnly] public NativeArray<GasSourceSpecRecord> SourceSpecs;
        [ReadOnly] public NativeArray<GasApplicationOutcomeRecord> ApplicationOutcomes;
        [ReadOnly] public NativeArray<GasAttributeMutationRecord> AttributeMutations;
        [ReadOnly] public NativeArray<GasAttributeMutationOutcomeRecord> MutationOutcomes;
        [ReadOnly] public NativeArray<GasCoreFactRecord> CoreFacts;
        [ReadOnly] public NativeArray<float> EvaluatorStack;
        [ReadOnly] public NativeArray<GasTargetShadowState> TargetShadows;
        [ReadOnly] public NativeArray<GasFinalPublishDecision> FinalPublishDecision;
        [ReadOnly] public NativeArray<GasTerminalBattlePublishIntent> TerminalBattleIntents;
        [ReadOnly] public NativeArray<GasSessionLifecyclePublishIntent> SessionLifecycleIntent;
        [ReadOnly] public NativeArray<GasPendingCommandSlotPublishIntent> PendingCommandSlotIntents;
        [ReadOnly] public NativeArray<GasPendingCommandHeadPublishIntent> PendingCommandHeadIntents;
        [ReadOnly] public NativeArray<GasBoundaryFactPublishIntent> BoundaryFactIntents;
        [ReadOnly] public NativeArray<GasRequestTerminalIntent> RequestTerminalIntents;
        [ReadOnly] public NativeArray<ActiveEffectSlot> TargetActiveEffects;
        [ReadOnly] public NativeArray<AttributeValueSlot> TargetAttributes;
        [ReadOnly] public NativeArray<AttributeDirtyWord> TargetAttributeDirtyWords;
        [ReadOnly] public NativeArray<TagCountSlot> TargetTagCounts;
        [ReadOnly] public NativeArray<TagPresenceWord> TargetTagPresenceWords;
        [ReadOnly] public NativeArray<GasPayloadRangeRecord> TargetPayloadRanges;
        [ReadOnly] public NativeArray<GasPayloadValueSlot> TargetPayloadValues;
        public int TargetActiveEffectStride;
        public int TargetAttributeStride;
        public int TargetAttributeDirtyWordStride;
        public int TargetTagStride;
        public int TargetTagPresenceWordStride;
        public int TargetPayloadRangeStride;
        public int TargetPayloadValueStride;
        public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 把 envelope fault 或实际 downstream 逻辑上限提升为唯一 AdmissionResult。
        /// </summary>
        public void Execute()
        {
            var execution = Execution[0];
            if (execution.GameplayEnabled == 0)
                return;
            execution.LaneMask |= GasTickLaneMask.Admission;
            var failure = execution.PreAdmissionFailure != GasTickAdmissionFailureReason.None
                ? execution.PreAdmissionFailure
                : Envelope[0].FailureReason;
            if (failure == GasTickAdmissionFailureReason.None)
                failure = ValidateActualCounts(in execution);
            if (failure == GasTickAdmissionFailureReason.None)
                failure = ValidateFinalPublishInfrastructure();
            if (failure == GasTickAdmissionFailureReason.None)
                failure = ValidateEffectOperationIdentities(in execution);
            if (failure == GasTickAdmissionFailureReason.None)
                failure = CalculateFactDemand(ref execution);
            if (failure == GasTickAdmissionFailureReason.None)
                failure = ValidateAbilityReservations(in execution);
            if (failure == GasTickAdmissionFailureReason.None)
                failure = ValidateTargetReservations(in execution);
            if (failure == GasTickAdmissionFailureReason.None)
                failure = ValidateBoundaryReservations(in execution);
            Admission[0] = new GasAdmissionResult
            {
                CandidateTick = execution.CandidateTick,
                FailureReason = failure,
                Succeeded = failure == GasTickAdmissionFailureReason.None ? (byte)1 : (byte)0,
            };
            Execution[0] = execution;
        }

        /// <summary>
        /// 以完整 downstream 类别顺序校验实际 count，零业务需求仍固定 reservation contract。
        /// </summary>
        private GasTickAdmissionFailureReason ValidateActualCounts(in GasTickExecutionState execution)
        {
            if (execution.OwnerPlanCount < 0)
                return GasTickAdmissionFailureReason.OwnerPlanLimit;
            if (execution.ResolvedTargetCount < 0)
                return GasTickAdmissionFailureReason.ResolvedTargetLimit;
            if (execution.EffectOperationCount < 0)
                return GasTickAdmissionFailureReason.EffectOperationLimit;
            if (execution.TargetResolveRejectionCount < 0)
                return GasTickAdmissionFailureReason.EffectOperationLimit;
            if (execution.AttributeMutationCount < 0)
                return GasTickAdmissionFailureReason.CoreFactLimit;
            if (execution.PeriodTickDemand < 0 || execution.PeriodMutationDemand < 0)
                return GasTickAdmissionFailureReason.CoreFactLimit;
            if (execution.AbilityRouteCount < 0)
                return GasTickAdmissionFailureReason.NextTickRouteLimit;
            if (!HasActualCapacity(OwnerPlans, execution.OwnerPlanCount))
                return GasTickAdmissionFailureReason.OwnerPlanLimit;
            if (!HasActualCapacity(AbilityRoutes, execution.AbilityRouteCount))
                return GasTickAdmissionFailureReason.NextTickRouteLimit;
            if (execution.OwnerPlanCount > Profile.MaxOwnerPlanCount)
                return GasTickAdmissionFailureReason.OwnerPlanLimit;
            if (execution.ResolvedTargetCount > Profile.MaxResolvedTargetCount)
                return GasTickAdmissionFailureReason.ResolvedTargetLimit;
            if (execution.EffectOperationCount > Profile.MaxEffectOperationCount)
                return GasTickAdmissionFailureReason.EffectOperationLimit;
            if (execution.TargetResolveRejectionCount > Profile.MaxEffectOperationCount)
                return GasTickAdmissionFailureReason.EffectOperationLimit;
            if (!HasActualCapacity(EffectOperations, execution.EffectOperationCount))
                return GasTickAdmissionFailureReason.EffectOperationLimit;
            if (!HasActualCapacity(ResolvedTargets, execution.ResolvedTargetCount))
                return GasTickAdmissionFailureReason.ResolvedTargetLimit;
            var sourceSpecCount = execution.EffectOperationCount + execution.TargetResolveRejectionCount;
            if (sourceSpecCount < execution.EffectOperationCount ||
                !HasActualCapacity(TargetResolveRejections, execution.TargetResolveRejectionCount) ||
                !HasActualCapacity(SourceSpecs, sourceSpecCount) ||
                !HasActualCapacity(ApplicationOutcomes, sourceSpecCount))
                return GasTickAdmissionFailureReason.EffectOperationLimit;
            if (!HasActualCapacity(CoreFacts, execution.EffectOperationCount))
                return GasTickAdmissionFailureReason.CoreFactLimit;
            if (execution.EffectOperationCount > Profile.MaxCoreFactCount)
                return GasTickAdmissionFailureReason.CoreFactLimit;
            if (execution.AbilityRouteCount > Profile.MaxNextTickRouteCount)
                return GasTickAdmissionFailureReason.NextTickRouteLimit;
            if (Profile.MaxOwnerReservationCount < 0)
                return GasTickAdmissionFailureReason.OwnerReservationLimit;
            if (Profile.MaxTargetReservationCount < 0)
                return GasTickAdmissionFailureReason.TargetReservationLimit;
            if (Profile.MaxCoreFactCount < 0)
                return GasTickAdmissionFailureReason.CoreFactLimit;
            if (Profile.MaxSessionBoundaryFactCount < 0 || Profile.MaxAscBoundaryFactCount < 0)
                return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
            if (Profile.MaxNextTickRouteCount < 0)
                return GasTickAdmissionFailureReason.NextTickRouteLimit;
            return Profile.MaxStructuralIntentCount < 0
                ? GasTickAdmissionFailureReason.StructuralIntentLimit
                : GasTickAdmissionFailureReason.None;
        }

        /// <summary>
        /// 在 owner authority writer 前证明完整 target shadow 与 final-publish intent 均已定长预留。
        /// </summary>
        private GasTickAdmissionFailureReason ValidateFinalPublishInfrastructure()
        {
            var targetCount = Profile.MaxAscRegistryCount;
            var attributeCount = Catalog.Value.AttributeLayout.Entries.Length;
            var tagCount = Catalog.Value.TagCatalog.Entries.Length;
            var routeIntentDemand = (long)Profile.MaxNextTickRouteCount * 2L;
            if (targetCount < 0 || FinalPublishDecision.Length < 1 ||
                TerminalBattleIntents.Length < Profile.MaxBattleInstanceCount ||
                SessionLifecycleIntent.Length < 1 ||
                routeIntentDemand < 0 || routeIntentDemand > int.MaxValue ||
                PendingCommandSlotIntents.Length < routeIntentDemand ||
                PendingCommandHeadIntents.Length < targetCount ||
                BoundaryFactIntents.Length < Profile.MaxCoreFactCount ||
                RequestTerminalIntents.Length < Profile.MaxBoundaryCommandCount ||
                TargetShadows.Length < targetCount ||
                TargetActiveEffectStride < Profile.MaxActiveEffectCount ||
                TargetAttributeStride < attributeCount ||
                TargetAttributeDirtyWordStride < WordCount(attributeCount) ||
                TargetTagStride < tagCount ||
                TargetTagPresenceWordStride < WordCount(tagCount) ||
                TargetPayloadRangeStride < Profile.MaxPayloadRangeRecordCount ||
                TargetPayloadValueStride < Profile.MaxPayloadValueCount)
                return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
            return HasTargetPlaneCapacity(TargetActiveEffects, targetCount,
                       TargetActiveEffectStride) &&
                   HasTargetPlaneCapacity(TargetAttributes, targetCount,
                       TargetAttributeStride) &&
                   HasTargetPlaneCapacity(TargetAttributeDirtyWords, targetCount,
                       TargetAttributeDirtyWordStride) &&
                   HasTargetPlaneCapacity(TargetTagCounts, targetCount, TargetTagStride) &&
                   HasTargetPlaneCapacity(TargetTagPresenceWords, targetCount,
                       TargetTagPresenceWordStride) &&
                   HasTargetPlaneCapacity(TargetPayloadRanges, targetCount,
                       TargetPayloadRangeStride) &&
                   HasTargetPlaneCapacity(TargetPayloadValues, targetCount,
                       TargetPayloadValueStride)
                ? GasTickAdmissionFailureReason.None
                : GasTickAdmissionFailureReason.DurableCapacityUnavailable;
        }

        /// <summary>
        /// 用 64 位乘法验证 target×stride 平面，拒绝溢出后被折叠为空数组的 scratch。
        /// </summary>
        private static bool HasTargetPlaneCapacity<T>(
            NativeArray<T> plane,
            int targetCount,
            int stride)
            where T : struct
        {
            if (targetCount < 0 || stride < 0)
                return false;
            var required = (long)targetCount * stride;
            return required <= int.MaxValue && plane.Length >= required;
        }

        /// <summary>
        /// 将 dense 元素数转换为 presence/dirty word 数量。
        /// </summary>
        private static int WordCount(int elementCount)
        {
            return elementCount <= 0 ? 0 : ((elementCount - 1) / 64) + 1;
        }

        /// <summary>
        /// 以 effect outcome 加每个 modifier mutation 的保守闭包需求完成事实与 outbox 预留。
        /// </summary>
        private GasTickAdmissionFailureReason CalculateFactDemand(
            ref GasTickExecutionState execution)
        {
            long mutationDemand = 0;
            for (var index = 0; index < execution.EffectOperationCount; index++)
            {
                var operation = EffectOperations[index];
                if (operation.DefinitionIndex < 0 ||
                    operation.DefinitionIndex >= Catalog.Value.GameplayEffects.Length)
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                var range = Catalog.Value.GameplayEffects[operation.DefinitionIndex].ModifierRange;
                if (range.Start < 0 || range.Count < 0 ||
                    range.Start > Catalog.Value.Modifiers.Length ||
                    range.Count > Catalog.Value.Modifiers.Length - range.Start)
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                mutationDemand += range.Count;
                if (mutationDemand > int.MaxValue)
                    return GasTickAdmissionFailureReason.CoreFactLimit;
            }

            if (!TryCalculatePeriodDemand(
                    in execution,
                    out var periodTickDemand,
                    out var periodMutationDemand))
                return GasTickAdmissionFailureReason.CoreFactLimit;
            execution.PeriodTickDemand = periodTickDemand;
            execution.PeriodMutationDemand = periodMutationDemand;
            var totalMutationDemand = mutationDemand + periodMutationDemand;
            if (totalMutationDemand > int.MaxValue)
                return GasTickAdmissionFailureReason.CoreFactLimit;
            execution.AttributeMutationDemand = (int)totalMutationDemand;
            var deathDemand = CountDistinctTargetsIncludingPeriod(
                execution.EffectOperationCount);
            var definitionFactDemand = CountDefinitionFactDemand(
                execution.EffectOperationCount);
            var factDemand = totalMutationDemand + execution.EffectOperationCount +
                             execution.TargetResolveRejectionCount + deathDemand +
                             periodTickDemand + definitionFactDemand;
            var terminalOutcomeDemand = CountTerminalOutcomeDemand(in execution);
            execution.TerminalOutcomeDemand = terminalOutcomeDemand;
            factDemand += terminalOutcomeDemand;
            var outcomeDemand = execution.EffectOperationCount +
                                execution.TargetResolveRejectionCount + periodTickDemand;
            if (factDemand > int.MaxValue || factDemand > Profile.MaxCoreFactCount ||
                outcomeDemand > int.MaxValue ||
                !HasActualCapacity(AttributeMutations, (int)totalMutationDemand) ||
                !HasActualCapacity(MutationOutcomes, (int)totalMutationDemand) ||
                !HasActualCapacity(ApplicationOutcomes, (int)outcomeDemand) ||
                !HasActualCapacity(CoreFacts, (int)factDemand))
                return GasTickAdmissionFailureReason.CoreFactLimit;
            return GasTickAdmissionFailureReason.None;
        }

        /// <summary>
        /// 计算可能在本 Tick 终局的 BattleInstance 上界，提前覆盖 BattleOutcome scratch 容量。
        /// </summary>
        private int CountTerminalOutcomeDemand(in GasTickExecutionState execution)
        {
            if (!Battles.HasBuffer(Session))
                return 0;

            var demand = 0;
            var battles = Battles[Session];
            for (var index = 0; index < battles.Length; index++)
            {
                var battle = battles[index];
                if (battle.Header.StorageState != GasSlabSlotState.Live ||
                    (battle.State != GasBattleInstanceState.Ready &&
                     battle.State != GasBattleInstanceState.Running) ||
                    battle.IngressClosed != 0 || !battle.Handle.IsValid)
                    continue;

                if (execution.EffectOperationCount > 0 || execution.PeriodTickDemand > 0 ||
                    HasTerminalOutcomeCandidate(in battle))
                    demand++;
            }
            return demand;
        }

        /// <summary>
        /// 读取 Ready registry 成员的阵营与生命周期，覆盖无新 operation 时的终局候选。
        /// </summary>
        private bool HasTerminalOutcomeCandidate(in BattleInstanceSlot battle)
        {
            if (battle.MemberStart < 0 || battle.MemberCount <= 0 ||
                !Battles.HasBuffer(Session))
                return false;
            var registry = Registries[Session];
            if (battle.MemberStart > registry.Length ||
                battle.MemberCount > registry.Length - battle.MemberStart)
                return false;

            var factionCount = 0;
            var aliveFactionCount = 0;
            for (var offset = 0; offset < battle.MemberCount; offset++)
            {
                var slot = registry[battle.MemberStart + offset];
                if (slot.State != GasAscRegistryState.Ready || slot.BattleInstance != battle.Handle)
                    return false;
                var asc = slot.ResolveRuntimeEntity();
                if (!AscIdentities.HasComponent(asc) || !AscLifecycles.HasComponent(asc) ||
                    !Memberships.HasComponent(asc))
                    return false;
                var identity = AscIdentities[asc];
                var membership = Memberships[asc];
                if (identity.SimulationEpoch != SimulationEpoch ||
                    !identity.OwnerAsc.Equals(slot.OwnerAsc) ||
                    membership.BattleInstance != battle.Handle)
                    return false;
                if (!HasPriorAdmissionFaction(in registry, battle.MemberStart, offset,
                        in membership))
                    factionCount++;
                var lifecycle = AscLifecycles[asc].State;
                if ((lifecycle == GasAscLifecycleState.Ready ||
                     lifecycle == GasAscLifecycleState.Alive) &&
                    !HasPriorAdmissionAliveFaction(
                        in registry, battle.MemberStart, offset, in membership))
                    aliveFactionCount++;
            }
            return factionCount > 1 && aliveFactionCount <= 1;
        }

        /// <summary>
        /// 判断 admission 当前成员是否命中前序同一 SideId/TeamId 阵营。
        /// </summary>
        private bool HasPriorAdmissionFaction(
            in DynamicBuffer<AscRegistrySlot> registry,
            int memberStart,
            int offset,
            in AscBattleMembership membership)
        {
            for (var prior = 0; prior < offset; prior++)
            {
                var asc = registry[memberStart + prior].ResolveRuntimeEntity();
                if (!Memberships.HasComponent(asc))
                    continue;
                var priorMembership = Memberships[asc];
                if (priorMembership.SideId == membership.SideId &&
                    priorMembership.TeamId == membership.TeamId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断 admission 当前成员是否命中前序同阵营且存活成员。
        /// </summary>
        private bool HasPriorAdmissionAliveFaction(
            in DynamicBuffer<AscRegistrySlot> registry,
            int memberStart,
            int offset,
            in AscBattleMembership membership)
        {
            for (var prior = 0; prior < offset; prior++)
            {
                var asc = registry[memberStart + prior].ResolveRuntimeEntity();
                if (!Memberships.HasComponent(asc) || !AscLifecycles.HasComponent(asc))
                    continue;
                var priorMembership = Memberships[asc];
                var lifecycle = AscLifecycles[asc].State;
                if (priorMembership.SideId == membership.SideId &&
                    priorMembership.TeamId == membership.TeamId &&
                    (lifecycle == GasAscLifecycleState.Ready ||
                     lifecycle == GasAscLifecycleState.Alive))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 统计既有 ActiveEffect due 与本 Tick 新建/重置 effect 的 period 上界，避免 period body 静默超出 scratch。
        /// </summary>
        private bool TryCalculatePeriodDemand(
            in GasTickExecutionState execution,
            out int periodTickDemand,
            out int periodMutationDemand)
        {
            periodTickDemand = 0;
            periodMutationDemand = 0;
            long claimDemand = 0;
            long mutationDemand = 0;
            var registry = Registries[Session];
            for (var registryIndex = 0; registryIndex < registry.Length; registryIndex++)
            {
                var ownerSlot = registry[registryIndex];
                if (ownerSlot.State != GasAscRegistryState.Ready)
                    continue;
                var asc = ownerSlot.ResolveRuntimeEntity();
                if (!ActiveEffects.HasBuffer(asc) || !TagCounts.HasBuffer(asc) ||
                    !Attributes.HasBuffer(asc) || !AttributeDirtyWords.HasBuffer(asc) ||
                    !TagPresenceWords.HasBuffer(asc) || !SlabHeads.HasComponent(asc))
                    return false;
                var activeEffects = ActiveEffects[asc];
                var activeStorage = new GasActiveEffectSlabStorage { Buffer = activeEffects };
                var heads = SlabHeads[asc];
                if (GasNonCompactingSlabAllocator.Validate(
                        in heads.ActiveEffect,
                        ref activeStorage,
                        Profile.MaxActiveEffectCount) != GasSlabStorageFailure.None)
                    return false;
                ref var catalog = ref Catalog.Value;
                if (!GasGameplayEffectLifecycleUtility.TryEstimateDue(
                        ref catalog,
                        SimulationEpoch,
                        execution.CandidateTick,
                        activeEffects,
                        TagCounts[asc],
                        Profile.MaxActiveEffectCount,
                        out var claims,
                        out var mutations,
                        out _))
                    return false;
                claimDemand += claims;
                mutationDemand += mutations;
                for (var operationIndex = 0;
                     operationIndex < execution.EffectOperationCount;
                     operationIndex++)
                {
                    var operation = EffectOperations[operationIndex];
                    if (!operation.TargetAsc.Equals(ownerSlot.OwnerAsc) ||
                        operation.DefinitionIndex < 0 ||
                        operation.DefinitionIndex >= catalog.GameplayEffects.Length)
                        continue;
                    var definition = catalog.GameplayEffects[operation.DefinitionIndex];
                    if (definition.PeriodTicks <= 0)
                        continue;
                    claimDemand++;
                    mutationDemand += definition.ModifierRange.Count;
                }
                if (claimDemand > int.MaxValue || mutationDemand > int.MaxValue)
                    return false;
            }
            periodTickDemand = (int)claimDemand;
            periodMutationDemand = (int)mutationDemand;
            return true;
        }

        /// <summary>
        /// 统计普通 application、既有 period 与本 Tick period 上界覆盖的唯一 target death 需求。
        /// </summary>
        private int CountDistinctTargetsIncludingPeriod(int operationCount)
        {
            var count = CountDistinctTargets(operationCount);
            var registry = Registries[Session];
            for (var registryIndex = 0; registryIndex < registry.Length; registryIndex++)
            {
                var ownerSlot = registry[registryIndex];
                if (ownerSlot.State != GasAscRegistryState.Ready)
                    continue;
                var asc = ownerSlot.ResolveRuntimeEntity();
                if (!ActiveEffects.HasBuffer(asc))
                    continue;
                var hasPeriod = false;
                var effects = ActiveEffects[asc];
                for (var effectIndex = 0; effectIndex < effects.Length; effectIndex++)
                {
                    var effect = effects[effectIndex];
                    if (effect.Header.StorageState == GasSlabSlotState.Live &&
                        effect.State == GasSlotBusinessState.Active &&
                        effect.NextPeriodTick != 0 &&
                        effect.NextPeriodTick <= Execution[0].CandidateTick)
                    {
                        hasPeriod = true;
                        break;
                    }
                }
                if (!hasPeriod)
                {
                    for (var operationIndex = 0;
                         operationIndex < operationCount;
                         operationIndex++)
                    {
                        if (EffectOperations[operationIndex].TargetAsc.Equals(ownerSlot.OwnerAsc) &&
                            EffectOperations[operationIndex].DefinitionIndex >= 0 &&
                            EffectOperations[operationIndex].DefinitionIndex < Catalog.Value.GameplayEffects.Length &&
                            Catalog.Value.GameplayEffects[
                                EffectOperations[operationIndex].DefinitionIndex].PeriodTicks > 0)
                        {
                            hasPeriod = true;
                            break;
                        }
                    }
                }
                if (hasPeriod && !HasPriorOperationTarget(ownerSlot.OwnerAsc, operationCount))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 判断 period target 是否已经由普通 effect operation 贡献 death reservation。
        /// </summary>
        private bool HasPriorOperationTarget(in OwnerAscHandle target, int operationCount)
        {
            for (var index = 0; index < operationCount; index++)
                if (EffectOperations[index].TargetAsc.Equals(target))
                    return true;
            return false;
        }

        /// <summary>
        /// 按 definition lifetime/cue 语义保守预留 stabilization 额外事实。
        /// </summary>
        private int CountDefinitionFactDemand(int operationCount)
        {
            long count = 0;
            for (var index = 0; index < operationCount; index++)
            {
                var definitionIndex = EffectOperations[index].DefinitionIndex;
                if (definitionIndex < 0 || definitionIndex >= Catalog.Value.GameplayEffects.Length)
                    continue;
                var definition = Catalog.Value.GameplayEffects[definitionIndex];
                if (definition.Lifetime == GasEffectLifetimePolicy.InstantExecution)
                    count += 2;
                else if (definition.CueRange.Count > 0)
                    count++;
            }
            return count > int.MaxValue ? int.MaxValue : (int)count;
        }

        /// <summary>
        /// 统计本 Tick 可能产生首个 death crossing 的 target 数量；同一 target 最多一条 Death fact。
        /// </summary>
        private int CountDistinctTargets(int operationCount)
        {
            var count = 0;
            for (var index = 0; index < operationCount; index++)
            {
                var target = EffectOperations[index].TargetAsc;
                var seen = false;
                for (var prior = 0; prior < index; prior++)
                {
                    if (EffectOperations[prior].TargetAsc.Equals(target))
                    {
                        seen = true;
                        break;
                    }
                }
                if (!seen)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 检查新 scratch 容器已创建且实际长度覆盖本 Tick 的最小产出数量。
        /// </summary>
        private static bool HasActualCapacity<T>(NativeArray<T> values, int required)
            where T : struct
        {
            return required == 0 || (values.IsCreated && values.Length >= required);
        }

        /// <summary>
        /// 验证每条 effect operation 的稳定身份、目标引用和 application 唯一性，确保后续 SourceSpec 不会静默跳过。
        /// </summary>
        private GasTickAdmissionFailureReason ValidateEffectOperationIdentities(
            in GasTickExecutionState execution)
        {
            if (execution.EffectOperationCount == 0)
                return GasTickAdmissionFailureReason.None;
            if (!Registries.HasBuffer(Session) || !BoundaryDrains.HasComponent(Session) ||
                !BoundaryFacts.HasBuffer(Session) || !Catalog.IsCreated)
                return GasTickAdmissionFailureReason.DurableCapacityUnavailable;

            ref var catalog = ref Catalog.Value;
            for (var index = 0; index < execution.EffectOperationCount; index++)
            {
                var operation = EffectOperations[index];
                if (!operation.SourceAsc.IsValid || !operation.TargetAsc.IsValid ||
                    operation.ApplicationId == 0 ||
                    operation.DefinitionIndex < 0 ||
                    operation.DefinitionIndex >= catalog.GameplayEffects.Length ||
                    operation.OwnerPlanOrdinal < -1 ||
                    operation.OwnerPlanOrdinal >= execution.OwnerPlanCount ||
                    operation.TargetOrdinal < 0 ||
                    operation.TargetOrdinal >= execution.ResolvedTargetCount ||
                    operation.ProgramNodeOrdinal < -1 ||
                    operation.TargetIsAlive > 1)
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;

                if (operation.OwnerPlanOrdinal < 0 && operation.ProgramNodeOrdinal >= 0)
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;

                if (operation.OwnerPlanOrdinal < 0)
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                var plan = OwnerPlans[operation.OwnerPlanOrdinal];
                if (plan.BusinessAccepted == 0 || plan.StableSequence == 0 ||
                    !plan.OwnerAsc.Equals(operation.SourceAsc) ||
                    !TryComposeApplicationId(
                        plan.StableSequence,
                        operation.ProgramNodeOrdinal,
                        out var expectedApplicationId) ||
                    expectedApplicationId != operation.ApplicationId)
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;

                var resolved = ResolvedTargets[operation.TargetOrdinal];
                if (resolved.OwnerPlanOrdinal != operation.OwnerPlanOrdinal ||
                    resolved.TargetOrdinal != operation.TargetOrdinal ||
                    !resolved.TargetAsc.Equals(operation.TargetAsc) ||
                    resolved.DefinitionIndex != operation.DefinitionIndex ||
                    resolved.ApplicationId != operation.ApplicationId ||
                    resolved.TargetIsAlive != operation.TargetIsAlive)
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;

                for (var prior = 0; prior < index; prior++)
                {
                    if (EffectOperations[prior].ApplicationId == operation.ApplicationId)
                        return GasTickAdmissionFailureReason.CanonicalKeyCollision;
                }
            }
            return GasTickAdmissionFailureReason.None;
        }

        /// <summary>
        /// 复算 Owner Commit sequence 与 direct-program ordinal 的正式 ApplicationId，拒绝 transport 序号冒充身份。
        /// </summary>
        private static bool TryComposeApplicationId(
            ulong stableSequence,
            int nodeOrdinal,
            out ulong applicationId)
        {
            applicationId = 0;
            if (stableSequence == 0 || nodeOrdinal < 0 || nodeOrdinal >= ushort.MaxValue ||
                stableSequence > (ulong.MaxValue >> 16))
                return false;
            applicationId = (stableSequence << 16) | (uint)(nodeOrdinal + 1);
            return applicationId != 0;
        }

        /// <summary>
        /// 按 owner 汇总 accepted plan，并证明 activation、cooldown 与 cost revision 均可 no-fail 提交。
        /// </summary>
        private GasTickAdmissionFailureReason ValidateAbilityReservations(
            in GasTickExecutionState execution)
        {
            var registry = Registries[Session];
            for (var registryIndex = 0; registryIndex < registry.Length; registryIndex++)
            {
                var ownerSlot = registry[registryIndex];
                if (ownerSlot.State != GasAscRegistryState.Ready)
                    continue;
                var asc = ownerSlot.ResolveRuntimeEntity();
                if (!TryResolveAbilityOwner(ownerSlot.OwnerAsc, asc, out var lifecycle))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                if (!SlabHeads.HasComponent(asc) || !Grants.HasBuffer(asc) ||
                    !Activations.HasBuffer(asc) || !Continuations.HasBuffer(asc) ||
                    !Subscriptions.HasBuffer(asc) || !Cooldowns.HasBuffer(asc) ||
                    !PendingCommands.HasBuffer(asc) || !Attributes.HasBuffer(asc) ||
                    !AttributeDirtyWords.HasBuffer(asc) || !TagCounts.HasBuffer(asc) ||
                    !TagPresenceWords.HasBuffer(asc))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                CountOwnerDemands(ownerSlot.OwnerAsc, execution.OwnerPlanCount,
                    out var activationDemand, out var cooldownDemand);
                var heads = SlabHeads[asc];
                if (!ValidateOwnerSlabs(asc, in heads, execution.CandidateTick) ||
                    !ValidateAbilityRelations(ownerSlot.OwnerAsc, asc))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                if (IsOwnerTerminal(lifecycle))
                    continue;
                if (!CanReserve(in heads.AbilityActivation, Activations[asc].Length,
                        Activations[asc].Capacity, Profile.MaxAbilityActivationCount,
                        activationDemand, CountRecyclableActivationTombstones(Activations[asc])))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                var subscriptionDemand = CountCancellationFenceDemand(
                    ownerSlot.OwnerAsc,
                    PendingCommands[asc],
                    Subscriptions[asc],
                    execution.CandidateTick) + CountRegistrationAllocationDemand(
                    ownerSlot.OwnerAsc, execution.AbilityRouteCount);
                if (!CanReserve(in heads.AbilitySubscription, Subscriptions[asc].Length,
                        Subscriptions[asc].Capacity, Profile.MaxAbilitySubscriptionCount,
                        subscriptionDemand, CountRecyclableSubscriptionTombstones(Subscriptions[asc])))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                var dueCooldownCount = CountRecyclableDueCooldowns(
                    Cooldowns[asc], execution.CandidateTick);
                if (!CanReserve(in heads.CooldownGate, Cooldowns[asc].Length,
                        Cooldowns[asc].Capacity, Profile.MaxCooldownGateCount,
                        cooldownDemand, dueCooldownCount) ||
                    !CanApplyOwnerCosts(ownerSlot.OwnerAsc, execution.OwnerPlanCount, Attributes[asc]) ||
                    !CanApplyOwnerCooldownTicks(
                        ownerSlot.OwnerAsc, execution.OwnerPlanCount, execution.CandidateTick))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                var pendingDemand = CountPendingRouteDemand(
                    ownerSlot.OwnerAsc, execution.AbilityRouteCount);
                if (!CanReserve(in heads.PendingCommand, PendingCommands[asc].Length,
                        PendingCommands[asc].Capacity, Profile.MaxPendingCommandCount,
                        pendingDemand, CountRecyclablePendingTombstones(PendingCommands[asc])))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
            }
            return GasTickAdmissionFailureReason.None;
        }

        /// <summary>
        /// 解析 registry Ready owner 的完整身份，并保留 Dead/Terminal 供同 Tick lifecycle 收尾。
        /// </summary>
        private bool TryResolveAbilityOwner(
            in OwnerAscHandle owner,
            Entity asc,
            out GasAscLifecycleState lifecycle)
        {
            lifecycle = default;
            if (!AscIdentities.HasComponent(asc) || !AscLifecycles.HasComponent(asc))
                return false;
            var identity = AscIdentities[asc];
            lifecycle = AscLifecycles[asc].State;
            return identity.SimulationEpoch == SimulationEpoch && identity.OwnerAsc.Equals(owner) &&
                   (lifecycle == GasAscLifecycleState.Ready ||
                    lifecycle == GasAscLifecycleState.Alive ||
                    lifecycle == GasAscLifecycleState.Terminal ||
                    lifecycle == GasAscLifecycleState.Dead);
        }

        /// <summary>
        /// 判断 owner 是否已经失去继续执行 normal ability 生命周期的资格。
        /// </summary>
        private static bool IsOwnerTerminal(GasAscLifecycleState lifecycle)
        {
            return lifecycle == GasAscLifecycleState.Terminal ||
                   lifecycle == GasAscLifecycleState.Dead;
        }

        /// <summary>
        /// 为每条 target operation 证明目标 ASC 的 ActiveEffect/Attribute authority 可安全写入。
        /// </summary>
        private GasTickAdmissionFailureReason ValidateTargetReservations(
            in GasTickExecutionState execution)
        {
            for (var index = 0; index < execution.EffectOperationCount; index++)
            {
                var operation = EffectOperations[index];
                if (!TryResolveTarget(operation.TargetAsc, out var target))
                    continue;
                if (!ActiveEffects.HasBuffer(target) || !Attributes.HasBuffer(target) ||
                    !AttributeDirtyWords.HasBuffer(target) || !TagCounts.HasBuffer(target) ||
                    !TagPresenceWords.HasBuffer(target) || !SlabHeads.HasComponent(target))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                var definition = Catalog.Value.GameplayEffects[operation.DefinitionIndex];
                if (!HasEvaluatorCapacity(in definition))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                if (!ValidatePayloadReservation(target, in definition, in operation))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                var activeEffects = ActiveEffects[target];
                var activeStorage = new GasActiveEffectSlabStorage { Buffer = activeEffects };
                var heads = SlabHeads[target];
                if (GasNonCompactingSlabAllocator.Validate(
                        in heads.ActiveEffect,
                        ref activeStorage,
                        Profile.MaxActiveEffectCount) != GasSlabStorageFailure.None)
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                var demand = IsInstant(in definition)
                    ? 0
                    : CountTargetOperations(in operation, index, execution.EffectOperationCount);
                if (!CanReserve(
                        in heads.ActiveEffect,
                        activeEffects.Length,
                        activeEffects.Capacity,
                        Profile.MaxActiveEffectCount,
                         demand,
                        CountRecyclableActiveTombstones(activeEffects)))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
            }
            return GasTickAdmissionFailureReason.None;
        }

        /// <summary>
        /// 预留 ActiveEffect Capture range 的 record/value 高水位，避免 TargetWave 事务后才发现物理容量不足。
        /// </summary>
        private bool ValidatePayloadReservation(
            Entity target,
            in GasGameplayEffectDefinitionBlob definition,
            in GasEffectOperationRecord operation)
        {
            var isInstant = definition.Lifetime == GasEffectLifetimePolicy.Instant ||
                            definition.Lifetime == GasEffectLifetimePolicy.InstantExecution;
            if (isInstant || definition.CaptureRange.Count == 0)
                return true;
            if (!PayloadStates.HasComponent(target) || !PayloadRanges.HasBuffer(target) ||
                !PayloadValues.HasBuffer(target))
                return false;
            var state = PayloadStates[target];
            var records = PayloadRanges[target];
            var values = PayloadValues[target];
            if (state.SimulationEpoch != SimulationEpoch ||
                !state.OwnerAsc.Equals(operation.TargetAsc) ||
                state.Records.HighWater != records.Length ||
                state.Records.HighWater < 0 || state.ValueHighWater < 0 ||
                state.Records.HighWater > Profile.MaxPayloadRangeRecordCount ||
                state.ValueHighWater > Profile.MaxPayloadValueCount ||
                records.Capacity < state.Records.HighWater ||
                values.Capacity < state.ValueHighWater)
                return false;
            // 预分配发生在旧 stack range 回收之前，必须为一个新代际 record/value 预留上界。
            return state.Records.HighWater < Profile.MaxPayloadRangeRecordCount &&
                   definition.CaptureRange.Count <=
                   Profile.MaxPayloadValueCount - state.ValueHighWater;
        }

        /// <summary>
        /// 按 target 汇总本 Tick CoreFact demand，并证明 ASC cleanup outbox 可在 BoundaryProject 中无扩容追加。
        /// </summary>
        private GasTickAdmissionFailureReason ValidateBoundaryReservations(
            in GasTickExecutionState execution)
        {
            var terminalFailure = ValidateRequestTerminalReservation(in execution);
            if (terminalFailure != GasTickAdmissionFailureReason.None)
                return terminalFailure;
            if (execution.EffectOperationCount == 0 && execution.TargetResolveRejectionCount == 0 &&
                execution.PeriodTickDemand == 0 && execution.TerminalOutcomeDemand == 0)
                return GasTickAdmissionFailureReason.None;
            if (Profile.MaxAscBoundaryFactCount < 0 || Profile.MaxSessionBoundaryFactCount < 0)
                return GasTickAdmissionFailureReason.DurableCapacityUnavailable;

            if (!BoundaryDrains.HasComponent(Session) || !BoundaryFacts.HasBuffer(Session))
                return GasTickAdmissionFailureReason.DurableCapacityUnavailable;

            var sessionState = BoundaryDrains[Session];
            if (sessionState.SimulationEpoch != SimulationEpoch ||
                sessionState.OwnerKind != GasBoundaryOwnerKind.Session ||
                sessionState.OwnerStableId == 0 || sessionState.OwnerGeneration == 0 ||
                sessionState.NextOwnerSequence == 0 ||
                sessionState.NextOwnerSequence == ulong.MaxValue ||
                 (sessionState.Phase != GasBoundaryDrainPhase.Idle &&
                  sessionState.Phase != GasBoundaryDrainPhase.Pending &&
                  sessionState.Phase != GasBoundaryDrainPhase.InFlight))
                return GasTickAdmissionFailureReason.DurableCapacityUnavailable;

            var sessionBoundaryDemand = execution.TargetResolveRejectionCount +
                                         execution.TerminalOutcomeDemand;
            if (sessionBoundaryDemand < execution.TargetResolveRejectionCount ||
                sessionBoundaryDemand < execution.TerminalOutcomeDemand ||
                sessionBoundaryDemand > 0 && !ValidateSessionBoundaryOwner(sessionBoundaryDemand))
                return GasTickAdmissionFailureReason.DurableCapacityUnavailable;

            for (var index = 0; index < execution.EffectOperationCount; index++)
            {
                var operation = EffectOperations[index];
                if (HasPriorTarget(in operation.TargetAsc, index))
                    continue;
                if (!TryResolveTarget(operation.TargetAsc, out var target) ||
                    !BoundaryDrains.HasComponent(target) || !BoundaryFacts.HasBuffer(target))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;

                var demand = CountBoundaryDemand(in operation.TargetAsc, execution.EffectOperationCount);
                if (!ValidateBoundaryOwner(target, in operation.TargetAsc, demand))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
            }
            var registry = Registries[Session];
            for (var registryIndex = 0; registryIndex < registry.Length; registryIndex++)
            {
                var ownerSlot = registry[registryIndex];
                if (ownerSlot.State != GasAscRegistryState.Ready ||
                    HasPriorOperationTarget(ownerSlot.OwnerAsc, execution.EffectOperationCount))
                    continue;
                var targetOwner = ownerSlot.OwnerAsc;
                if (!HasPeriodBoundaryDemand(in targetOwner, in execution))
                    continue;
                if (!TryResolveTarget(targetOwner, out var target) ||
                    !BoundaryDrains.HasComponent(target) || !BoundaryFacts.HasBuffer(target))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
                var demand = CountBoundaryDemand(in targetOwner, execution.EffectOperationCount);
                if (!ValidateBoundaryOwner(target, in targetOwner, demand))
                    return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
            }
            return GasTickAdmissionFailureReason.None;
        }

        /// <summary>
        /// 为每条 sealed command 预留一个 Session durable RequestTerminal intent，禁止双事实源或隐式扩容。
        /// </summary>
        private GasTickAdmissionFailureReason ValidateRequestTerminalReservation(
            in GasTickExecutionState execution)
        {
            if (execution.StoredSealedCommandCount < 0 ||
                execution.OwnerPlanCount != execution.StoredSealedCommandCount ||
                execution.StoredSealedCommandCount > Profile.MaxBoundaryCommandCount ||
                !RequestTerminalBuffers.HasBuffer(Session))
                return GasTickAdmissionFailureReason.DurableCapacityUnavailable;

            var intents = RequestTerminalBuffers[Session];
            var demand = execution.StoredSealedCommandCount;
            if (intents.Length > Profile.MaxBoundaryCommandCount - demand ||
                intents.Length > int.MaxValue - demand ||
                intents.Capacity < intents.Length + demand)
                return GasTickAdmissionFailureReason.DurableCapacityUnavailable;
            return GasTickAdmissionFailureReason.None;
        }

        /// <summary>
        /// 为无法绑定实体的 TargetResolve rejection 预留 Session scoped outbox 容量。
        /// </summary>
        private bool ValidateSessionBoundaryOwner(int demand)
        {
            var state = BoundaryDrains[Session];
            if (state.SimulationEpoch != SimulationEpoch ||
                state.OwnerKind != GasBoundaryOwnerKind.Session ||
                state.OwnerStableId == 0 || state.OwnerGeneration == 0 ||
                state.NextOwnerSequence == 0 || state.NextOwnerSequence == ulong.MaxValue ||
                (state.Phase != GasBoundaryDrainPhase.Idle &&
                 state.Phase != GasBoundaryDrainPhase.Pending &&
                 state.Phase != GasBoundaryDrainPhase.InFlight) ||
                (state.Phase == GasBoundaryDrainPhase.InFlight &&
                 state.InFlightWatermark >= state.NextOwnerSequence))
                return false;

            var outbox = BoundaryFacts[Session];
            return demand > 0 &&
                   demand <= Profile.MaxSessionBoundaryFactCount &&
                   outbox.Length <= Profile.MaxSessionBoundaryFactCount - demand &&
                   outbox.Length <= int.MaxValue - demand &&
                   outbox.Capacity >= outbox.Length + demand &&
                   (ulong)demand <= ulong.MaxValue - state.NextOwnerSequence;
        }

        /// <summary>
        /// 验证目标 outbox 的 owner、阶段、序号、profile 逻辑上限和物理容量均可承载 demand。
        /// </summary>
        private bool ValidateBoundaryOwner(
            Entity target,
            in OwnerAscHandle owner,
            int demand)
        {
            var state = BoundaryDrains[target];
            if (state.SimulationEpoch != SimulationEpoch ||
                state.OwnerKind != GasBoundaryOwnerKind.Asc ||
                state.OwnerStableId != owner.AscStableId ||
                state.OwnerGeneration != owner.AscGeneration ||
                state.NextOwnerSequence == 0 || state.NextOwnerSequence == ulong.MaxValue ||
                (state.Phase != GasBoundaryDrainPhase.Idle &&
                 state.Phase != GasBoundaryDrainPhase.Pending &&
                 state.Phase != GasBoundaryDrainPhase.InFlight) ||
                (state.Phase == GasBoundaryDrainPhase.InFlight &&
                 state.InFlightWatermark >= state.NextOwnerSequence))
                return false;

            var outbox = BoundaryFacts[target];
            if (demand <= 0 || demand > Profile.MaxAscBoundaryFactCount ||
                outbox.Length > Profile.MaxAscBoundaryFactCount - demand ||
                outbox.Length > int.MaxValue - demand ||
                outbox.Capacity < outbox.Length + demand)
                return false;

            return (ulong)demand <= ulong.MaxValue - state.NextOwnerSequence;
        }

        /// <summary>
        /// 统计同一 target 在当前 Tick 必须投影的最小 CoreFact 数量。
        /// </summary>
        private int CountBoundaryDemand(in OwnerAscHandle target, int operationCount)
        {
            long demand = 0;
            for (var index = 0; index < operationCount; index++)
            {
                var operation = EffectOperations[index];
                if (!operation.TargetAsc.Equals(target))
                    continue;
                demand++;
                if (operation.DefinitionIndex >= 0 &&
                    operation.DefinitionIndex < Catalog.Value.GameplayEffects.Length)
                {
                    var definition = Catalog.Value.GameplayEffects[operation.DefinitionIndex];
                    demand += definition.ModifierRange.Count;
                    if (definition.Lifetime == GasEffectLifetimePolicy.InstantExecution)
                        demand += 2;
                    else if (definition.CueRange.Count > 0)
                        demand++;
                }
            }
            demand += CountPeriodBoundaryDemand(in target, operationCount, out var hasPeriod);
            if (demand < int.MaxValue &&
                (HasFirstTargetDeathCandidate(in target, operationCount) || hasPeriod))
                demand++;
            return demand > int.MaxValue ? int.MaxValue : (int)demand;
        }

        /// <summary>
        /// 按忽略 inhibition 的结构 due 上界统计单 target 的 period claim/body 事实需求。
        /// </summary>
        private int CountPeriodBoundaryDemand(
            in OwnerAscHandle target,
            int operationCount,
            out bool hasPeriod)
        {
            long demand = 0;
            hasPeriod = false;
            if (!TryResolveTarget(target, out var asc) ||
                !ActiveEffects.HasBuffer(asc) || !TagCounts.HasBuffer(asc))
                return 0;
            var effects = ActiveEffects[asc];
            ref var catalog = ref Catalog.Value;
            for (var index = 0; index < effects.Length; index++)
            {
                var effect = effects[index];
                if (effect.Header.StorageState != GasSlabSlotState.Live ||
                    effect.State != GasSlotBusinessState.Active ||
                    effect.DefinitionIndex < 0 ||
                    effect.DefinitionIndex >= catalog.GameplayEffects.Length)
                    continue;
                var definition = catalog.GameplayEffects[effect.DefinitionIndex];
                if (!GasGameplayEffectLifecycleUtility.IsStructurallyPeriodDue(
                        in definition,
                        in effect,
                        Execution[0].CandidateTick))
                    continue;
                hasPeriod = true;
                demand += 1 + definition.ModifierRange.Count;
            }
            for (var index = 0; index < operationCount; index++)
            {
                var operation = EffectOperations[index];
                if (!operation.TargetAsc.Equals(target) ||
                    operation.DefinitionIndex < 0 ||
                    operation.DefinitionIndex >= catalog.GameplayEffects.Length)
                    continue;
                var definition = catalog.GameplayEffects[operation.DefinitionIndex];
                if (definition.PeriodTicks <= 0)
                    continue;
                hasPeriod = true;
                demand += 1 + definition.ModifierRange.Count;
            }
            return demand > int.MaxValue ? int.MaxValue : (int)demand;
        }

        /// <summary>
        /// 判断 target 是否存在当前 due 或本 Tick effect operation 引入的 period 需求。
        /// </summary>
        private bool HasPeriodBoundaryDemand(
            in OwnerAscHandle target,
            in GasTickExecutionState execution)
        {
            CountPeriodBoundaryDemand(in target, execution.EffectOperationCount, out var hasPeriod);
            return hasPeriod;
        }

        /// <summary>
        /// 以 target 的 canonical operation 集合判断是否需要为最多一条 Death fact 预留空间。
        /// </summary>
        private bool HasFirstTargetDeathCandidate(
            in OwnerAscHandle target,
            int operationCount)
        {
            for (var index = 0; index < operationCount; index++)
            {
                if (EffectOperations[index].TargetAsc.Equals(target))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断 target 是否已在更早 operation 中计数，避免重复验证同一物理 outbox。
        /// </summary>
        private bool HasPriorTarget(in OwnerAscHandle target, int currentIndex)
        {
            for (var index = 0; index < currentIndex; index++)
            {
                if (EffectOperations[index].TargetAsc.Equals(target))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 证明当前 Catalog 的最大 evaluator program 可在 Kernel scratch 栈内解释。
        /// </summary>
        private bool HasEvaluatorCapacity(in GasGameplayEffectDefinitionBlob definition)
        {
            if (definition.ModifierRange.Count == 0)
                return true;
            var maximum = 0;
            for (var offset = 0; offset < definition.ModifierRange.Count; offset++)
            {
                var modifier = Catalog.Value.Modifiers[definition.ModifierRange.Start + offset];
                maximum = System.Math.Max(maximum, modifier.EvaluatorProgramRange.Count);
            }
            return maximum > 0 && maximum <= EvaluatorStack.Length;
        }

        /// <summary>
        /// 判断 Effect 是否只执行瞬时 modifier、无需 ActiveEffect slab reservation。
        /// </summary>
        private static bool IsInstant(in GasGameplayEffectDefinitionBlob definition)
        {
            return definition.Lifetime == GasEffectLifetimePolicy.Instant ||
                   definition.Lifetime == GasEffectLifetimePolicy.InstantExecution;
        }

        /// <summary>
        /// 统计同一 target 在当前 canonical operation 前后的长期槽需求。
        /// </summary>
        private int CountTargetOperations(
            in GasEffectOperationRecord operation,
            int currentIndex,
            int operationCount)
        {
            var count = 0;
            for (var index = 0; index <= currentIndex && index < operationCount; index++)
            {
                var candidate = EffectOperations[index];
                if (!candidate.TargetAsc.Equals(operation.TargetAsc))
                    continue;
                var definition = Catalog.Value.GameplayEffects[candidate.DefinitionIndex];
                if (!IsInstant(in definition))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 通过 Ready registry 解析 target，供 admission 使用同一稳定 owner 规则。
        /// </summary>
        private bool TryResolveTarget(in OwnerAscHandle owner, out Entity asc)
        {
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State != GasAscRegistryState.Ready || !slot.OwnerAsc.Equals(owner))
                    continue;
                asc = slot.ResolveRuntimeEntity();
                if (!AscIdentities.HasComponent(asc) || !AscLifecycles.HasComponent(asc))
                    return false;
                var identity = AscIdentities[asc];
                var lifecycle = AscLifecycles[asc].State;
                return identity.SimulationEpoch == SimulationEpoch &&
                       identity.OwnerAsc.Equals(owner) &&
                        (lifecycle == GasAscLifecycleState.Ready ||
                         lifecycle == GasAscLifecycleState.Alive ||
                         lifecycle == GasAscLifecycleState.Terminal ||
                         lifecycle == GasAscLifecycleState.Dead);
            }
            asc = Entity.Null;
            return false;
        }

        /// <summary>
        /// 完整验证 OwnerWave 将访问的 slab/free-list，并拒绝本 Tick 无法回收的 generation 终态。
        /// </summary>
        private bool ValidateOwnerSlabs(Entity asc, in AscSlabHeads heads, ulong candidateTick)
        {
            var grants = new GasGrantedAbilitySlabStorage { Buffer = Grants[asc] };
            var activations = new GasAbilityActivationSlabStorage { Buffer = Activations[asc] };
            var continuations = new GasAbilityContinuationSlabStorage { Buffer = Continuations[asc] };
            var subscriptions = new GasAbilitySubscriptionSlabStorage { Buffer = Subscriptions[asc] };
            var cooldowns = new GasCooldownGateSlabStorage { Buffer = Cooldowns[asc] };
            var commands = new GasPendingCommandSlabStorage { Buffer = PendingCommands[asc] };
            return IsValidSlab(in heads.GrantedAbility, ref grants, Profile.MaxGrantedAbilityCount) &&
                   IsValidSlab(in heads.AbilityActivation, ref activations,
                       Profile.MaxAbilityActivationCount) &&
                   IsValidSlab(in heads.AbilityContinuation, ref continuations,
                       Profile.MaxAbilityContinuationCount) &&
                   IsValidSlab(in heads.AbilitySubscription, ref subscriptions,
                       Profile.MaxAbilitySubscriptionCount) &&
                   IsValidSlab(in heads.CooldownGate, ref cooldowns, Profile.MaxCooldownGateCount) &&
                   IsValidSlab(in heads.PendingCommand, ref commands, Profile.MaxPendingCommandCount) &&
                   CanRecycleExistingTombstones(ref grants) &&
                   CanRecycleExistingTombstones(ref activations) &&
                   CanRecycleExistingTombstones(ref continuations) &&
                   CanRecycleExistingTombstones(ref subscriptions) &&
                   CanRecycleExistingTombstones(ref commands) &&
                   CanReleaseDueCooldowns(Cooldowns[asc], candidateTick);
        }

        /// <summary>
        /// 验证 grant→activation→continuation 与 observed subscription 的完整 typed-handle 关系和 child count。
        /// </summary>
        private bool ValidateAbilityRelations(in OwnerAscHandle owner, Entity asc)
        {
            var grants = Grants[asc];
            var activations = Activations[asc];
            var continuations = Continuations[asc];
            var subscriptions = Subscriptions[asc];
            return ValidateGrantRelations(in owner, grants, activations) &&
                   ValidateActivationRelations(in owner, grants, activations, continuations) &&
                   ValidateContinuationRelations(in owner, activations, continuations) &&
                   ValidateSubscriptionRelations(in owner, subscriptions);
        }

        /// <summary>
        /// 每个 live grant 的稳定身份与 ChildActivationCount 必须等于实际 live child 数。
        /// </summary>
        internal static bool ValidateGrantRelations(
            in OwnerAscHandle owner,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations)
        {
            for (var grantIndex = 0; grantIndex < grants.Length; grantIndex++)
            {
                var grant = grants[grantIndex];
                if (grant.Header.StorageState != GasSlabSlotState.Live)
                    continue;
                if (!grant.Handle.IsValid || !grant.Handle.OwnerAsc.Equals(owner) ||
                    grant.Handle.SlotIndex != grantIndex ||
                    grant.Handle.SlotGeneration != grant.Header.Generation ||
                    grant.ChildActivationCount < 0)
                    return false;
                var childCount = 0;
                for (var index = 0; index < activations.Length; index++)
                {
                    var activation = activations[index];
                    if (activation.Header.StorageState == GasSlabSlotState.Live &&
                        activation.GrantedAbility.Equals(grant.Handle))
                        childCount++;
                }
                if (childCount != grant.ChildActivationCount)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 每个 live Activation 必须命中 live grant，且 continuation count 与实际 child 数一致。
        /// </summary>
        internal static bool ValidateActivationRelations(
            in OwnerAscHandle owner,
            DynamicBuffer<GrantedAbilitySlot> grants,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations)
        {
            for (var activationIndex = 0; activationIndex < activations.Length; activationIndex++)
            {
                var activation = activations[activationIndex];
                if (activation.Header.StorageState != GasSlabSlotState.Live)
                    continue;
                if (!IsLiveActivationShape(
                        in owner, activationIndex, in activation, grants) ||
                    activation.ContinuationCount < 0)
                    return false;
                var childCount = 0;
                for (var index = 0; index < continuations.Length; index++)
                {
                    var continuation = continuations[index];
                    if (continuation.Header.StorageState == GasSlabSlotState.Live &&
                        continuation.Activation.Equals(activation.Handle))
                        childCount++;
                }
                if (childCount != activation.ContinuationCount)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 复验 live Activation 自身句柄、阶段及其 live grant 反向引用。
        /// </summary>
        private static bool IsLiveActivationShape(
            in OwnerAscHandle owner,
            int activationIndex,
            in AbilityActivationSlot activation,
            DynamicBuffer<GrantedAbilitySlot> grants)
        {
            if (!activation.Handle.IsValid || !activation.Handle.OwnerAsc.Equals(owner) ||
                activation.Handle.SlotIndex != activationIndex ||
                activation.Handle.SlotGeneration != activation.Header.Generation ||
                activation.Phase < GasAbilityActivationPhase.RunningUncommitted ||
                activation.Phase > GasAbilityActivationPhase.Ending)
                return false;
            var handle = activation.GrantedAbility;
            if (!handle.IsValid || handle.SlotIndex < 0 || handle.SlotIndex >= grants.Length)
                return false;
            var grant = grants[handle.SlotIndex];
            return grant.Header.StorageState == GasSlabSlotState.Live &&
                   grant.Header.Generation == handle.SlotGeneration && grant.Handle.Equals(handle);
        }

        /// <summary>
        /// 每个 live Continuation 必须命中本 owner live Activation，并满足冻结 wait 形状。
        /// </summary>
        internal static bool ValidateContinuationRelations(
            in OwnerAscHandle owner,
            DynamicBuffer<AbilityActivationSlot> activations,
            DynamicBuffer<AbilityContinuationSlot> continuations)
        {
            for (var index = 0; index < continuations.Length; index++)
            {
                var slot = continuations[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live)
                    continue;
                if (!slot.Handle.IsValid || !slot.Handle.OwnerAsc.Equals(owner) ||
                    slot.Handle.SlotIndex != index ||
                    slot.Handle.SlotGeneration != slot.Header.Generation ||
                    slot.Handle.SimulationEpoch != slot.Activation.SimulationEpoch ||
                    slot.RegistrationGeneration == 0 ||
                    slot.WaitSemantic < GasAbilityWaitSemantic.Level ||
                    slot.WaitSemantic > GasAbilityWaitSemantic.Timer ||
                    slot.WaitPolicy < GasAbilityWaitPolicy.OneShot ||
                    slot.WaitPolicy > GasAbilityWaitPolicy.Persistent ||
                    slot.WaitState < GasAbilityWaitState.PendingRegistration ||
                    slot.WaitState > GasAbilityWaitState.Ending ||
                    !TryResolveLiveActivation(slot.Activation, activations))
                    return false;
                if (slot.WaitSemantic == GasAbilityWaitSemantic.Timer)
                {
                    if (slot.WaitPolicy != GasAbilityWaitPolicy.OneShot || slot.ObservedAsc.IsValid ||
                        !slot.Subscription.Equals(default) ||
                        slot.WaitState == GasAbilityWaitState.PendingRegistration)
                        return false;
                }
                else if (!IsCrossAscContinuationShape(in slot))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 跨 ASC Continuation 的 Subscription 必须与握手阶段一致，禁止半初始化或异主句柄。
        /// </summary>
        private static bool IsCrossAscContinuationShape(in AbilityContinuationSlot slot)
        {
            if (!slot.ObservedAsc.IsValid)
                return false;
            if (slot.WaitState == GasAbilityWaitState.PendingRegistration)
                return slot.Subscription.Equals(default);
            if (slot.Subscription.Equals(default))
                return slot.WaitState == GasAbilityWaitState.Completed ||
                       slot.WaitState == GasAbilityWaitState.Ending;
            return slot.Subscription.IsValid &&
                   slot.Subscription.OwnerAsc.Equals(slot.ObservedAsc) &&
                   slot.Subscription.SimulationEpoch == slot.Activation.SimulationEpoch;
        }

        /// <summary>
        /// 解析 Continuation 引用的 owner-local live Activation，不接受仅索引命中。
        /// </summary>
        private static bool TryResolveLiveActivation(
            in AbilityActivationHandle handle,
            DynamicBuffer<AbilityActivationSlot> activations)
        {
            if (!handle.IsValid || handle.SlotIndex < 0 || handle.SlotIndex >= activations.Length)
                return false;
            var activation = activations[handle.SlotIndex];
            return activation.Header.StorageState == GasSlabSlotState.Live &&
                   activation.Header.Generation == handle.SlotGeneration &&
                   activation.Handle.Equals(handle);
        }

        /// <summary>
        /// observed ASC 的 live Subscription 必须保存完整双端句柄、协议 generation 与 typed observed handle。
        /// </summary>
        internal static bool ValidateSubscriptionRelations(
            in OwnerAscHandle owner,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions)
        {
            for (var index = 0; index < subscriptions.Length; index++)
            {
                var slot = subscriptions[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live)
                    continue;
                if (!slot.Handle.IsValid || !slot.Handle.OwnerAsc.Equals(owner) ||
                    slot.Handle.SlotIndex != index ||
                    slot.Handle.SlotGeneration != slot.Header.Generation ||
                    !slot.ObservedAsc.Equals(owner) || !slot.SubscriberAsc.IsValid ||
                    !slot.Activation.IsValid || !slot.Continuation.IsValid ||
                    !slot.Activation.OwnerAsc.Equals(slot.SubscriberAsc) ||
                    !slot.Continuation.OwnerAsc.Equals(slot.SubscriberAsc) ||
                    slot.Handle.SimulationEpoch != slot.Activation.SimulationEpoch ||
                    slot.Activation.SimulationEpoch != slot.Continuation.SimulationEpoch ||
                    slot.RegistrationGeneration == 0 || !IsLiveSubscriptionProtocolShape(in slot))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 复验 live Subscription 的枚举闭集与 HandleLifecycle typed observed handle 形状。
        /// </summary>
        private static bool IsLiveSubscriptionProtocolShape(in AbilitySubscriptionSlot slot)
        {
            if (slot.WaitSemantic < GasAbilityWaitSemantic.Level ||
                slot.WaitSemantic > GasAbilityWaitSemantic.HandleLifecycle ||
                slot.WaitPolicy < GasAbilityWaitPolicy.OneShot ||
                slot.WaitPolicy > GasAbilityWaitPolicy.Persistent ||
                slot.State < GasAbilitySubscriptionState.PendingRegistration ||
                slot.State > GasAbilitySubscriptionState.Completed)
                return false;
            if (slot.WaitSemantic == GasAbilityWaitSemantic.HandleLifecycle)
            {
                return slot.ObservedHandle.IsValid &&
                       slot.ObservedHandle.OwnerAsc.Equals(slot.ObservedAsc) &&
                       slot.ObservedHandle.SimulationEpoch == slot.Activation.SimulationEpoch;
            }
            return slot.ObservedHandle.Equals(default);
        }

        /// <summary>
        /// 委托唯一 allocator 对 slab 的 high-water、header 与完整 free-list 做只读证明。
        /// </summary>
        private static bool IsValidSlab<TStorage>(
            in GasSlabHead head,
            ref TStorage storage,
            int hardCapacity)
            where TStorage : struct, IGasSlabHeaderStorage
        {
            return GasNonCompactingSlabAllocator.Validate(
                in head, ref storage, hardCapacity) == GasSlabStorageFailure.None;
        }

        /// <summary>
        /// pre-command maintenance 必须能回收每个既有 tombstone，generation 耗尽直接 admission fault。
        /// </summary>
        private static bool CanRecycleExistingTombstones<TStorage>(ref TStorage storage)
            where TStorage : struct, IGasSlabHeaderStorage
        {
            for (var index = 0; index < storage.Count; index++)
            {
                var header = storage.ReadHeader(index);
                if (header.StorageState == GasSlabSlotState.Tombstone &&
                    header.Generation == uint.MaxValue)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// due cooldown 必须可在 command 前完成 tombstone/recycle，且不接受遗留 tombstone。
        /// </summary>
        private static bool CanReleaseDueCooldowns(
            DynamicBuffer<CooldownGateSlot> cooldowns,
            ulong candidateTick)
        {
            for (var index = 0; index < cooldowns.Length; index++)
            {
                var slot = cooldowns[index];
                if (slot.Header.StorageState == GasSlabSlotState.Tombstone)
                    return false;
                if (slot.Header.StorageState == GasSlabSlotState.Live &&
                    slot.State == GasSlotBusinessState.Active && slot.EndTick <= candidateTick &&
                    slot.Header.Generation == uint.MaxValue)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 统计当前 observed owner 处理 due unsubscribe 时必须新建的 cancellation fence。
        /// </summary>
        private static int CountCancellationFenceDemand(
            in OwnerAscHandle owner,
            DynamicBuffer<PendingCommand> commands,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            ulong candidateTick)
        {
            var demand = 0;
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Header.StorageState == GasSlabSlotState.Live &&
                    command.State == GasSlotBusinessState.Pending &&
                    command.CommandKind == (int)GasAbilityPendingCommandKind.WaitUnsubscribe &&
                    GasAbilityWaitSlabTransaction.RequiresCancellationFence(
                        in owner, in command, candidateTick, subscriptions))
                    demand++;
            }
            return demand;
        }

        /// <summary>
        /// 统计一名 owner 在 durable destination 与同 Tick owner-gone 回退两种分支下的 route 写入上界。
        /// </summary>
        private int CountPendingRouteDemand(in OwnerAscHandle owner, int routeCount)
        {
            long demand = 0;
            for (var index = 0; index < routeCount; index++)
            {
                var route = AbilityRoutes[index];
                if (route.Used == 0)
                    continue;
                if (HasReadyOwner(route.DestinationAsc) &&
                    route.DestinationAsc.Equals(owner))
                    demand++;
                if (GasAbilityWaitSlabTransaction.SupportsObservedOwnerGoneResponse(
                        route.Command.CommandKind) &&
                    route.Command.SourceAsc.Equals(owner) &&
                    !route.Command.SourceAsc.Equals(route.DestinationAsc))
                    demand++;
            }
            return demand > int.MaxValue ? int.MaxValue : (int)demand;
        }

        /// <summary>
        /// 统计 sample 后需要新建 observed Subscription 的 registration Ack 模板。
        /// </summary>
        private int CountRegistrationAllocationDemand(
            in OwnerAscHandle observedOwner,
            int routeCount)
        {
            var demand = 0;
            for (var index = 0; index < routeCount; index++)
            {
                var route = AbilityRoutes[index];
                var command = route.Command;
                if (route.Used != 0 && route.RequiresOwnerApply != 0 &&
                    route.OriginCommandKind ==
                    (int)GasAbilityPendingCommandKind.WaitRegistration &&
                    command.CommandKind ==
                    (int)GasAbilityPendingCommandKind.WaitRegistrationAck &&
                    command.SourceAsc.Equals(observedOwner) &&
                    !command.Subscription.IsValid)
                    demand++;
            }
            return demand;
        }

        /// <summary>
        /// 判断稳定 owner 是否仍由 Session registry 发布为 Ready。
        /// </summary>
        private bool HasReadyOwner(in OwnerAscHandle owner)
        {
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                if (registry[index].State == GasAscRegistryState.Ready &&
                    registry[index].OwnerAsc.Equals(owner))
                {
                    var asc = registry[index].ResolveRuntimeEntity();
                    return IsReadyOwner(owner, asc);
                }
            }
            return false;
        }

        /// <summary>
        /// 把 registry 发布与 ASC lifecycle 共同解释为可参与当前 gameplay Tick 的 owner。
        /// </summary>
        private bool IsReadyOwner(in OwnerAscHandle owner, Entity asc)
        {
            if (!AscIdentities.HasComponent(asc) || !AscLifecycles.HasComponent(asc))
                return false;
            var identity = AscIdentities[asc];
            var state = AscLifecycles[asc].State;
            return identity.SimulationEpoch == SimulationEpoch && identity.OwnerAsc.Equals(owner) &&
                   (state == GasAscLifecycleState.Ready || state == GasAscLifecycleState.Alive);
        }

        /// <summary>
        /// 统计一个 owner 的 accepted Activation 与 Cooldown 槽需求。
        /// </summary>
        private void CountOwnerDemands(
            in OwnerAscHandle owner,
            int planCount,
            out int activationDemand,
            out int cooldownDemand)
        {
            activationDemand = 0;
            cooldownDemand = 0;
            for (var index = 0; index < planCount; index++)
            {
                var plan = OwnerPlans[index];
                if (plan.BusinessAccepted == 0 || !plan.OwnerAsc.Equals(owner))
                    continue;
                activationDemand += plan.RequiresActivationSlot;
                cooldownDemand += plan.RequiresCooldownSlot;
            }
        }

        /// <summary>
        /// 验证 slab metadata、物理 capacity、profile hard limit 与 Tick 内可回收槽共同覆盖需求。
        /// </summary>
        private static bool CanReserve(
            in GasSlabHead head,
            int length,
            int physicalCapacity,
            int hardCapacity,
            int demand,
            int additionalRecyclable)
        {
            if (demand < 0 || additionalRecyclable < 0 || hardCapacity < 0 ||
                head.HighWater != length || head.HighWater > hardCapacity ||
                head.HighWater > physicalCapacity || head.FreeCount < 0)
                return false;
            var appendCapacity = System.Math.Min(physicalCapacity, hardCapacity) - head.HighWater;
            return (long)head.FreeCount + additionalRecyclable + appendCapacity >= demand;
        }

        /// <summary>
        /// 统计本 Tick due 且 generation 可安全递增的 live cooldown gates。
        /// </summary>
        private static int CountRecyclableDueCooldowns(
            DynamicBuffer<CooldownGateSlot> cooldowns,
            ulong candidateTick)
        {
            var count = 0;
            for (var index = 0; index < cooldowns.Length; index++)
            {
                var slot = cooldowns[index];
                if (slot.Header.StorageState == GasSlabSlotState.Live &&
                    slot.Header.Generation < uint.MaxValue &&
                    slot.State == GasSlotBusinessState.Active && slot.EndTick <= candidateTick)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 统计 pre-command maintenance 可回收且 generation 尚未耗尽的 Activation tombstones。
        /// </summary>
        private static int CountRecyclableActivationTombstones(
            DynamicBuffer<AbilityActivationSlot> activations)
        {
            var count = 0;
            for (var index = 0; index < activations.Length; index++)
            {
                var header = activations[index].Header;
                if (header.StorageState == GasSlabSlotState.Tombstone &&
                    header.Generation < uint.MaxValue)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 统计 pre-command 会递增 generation 并接入 free-list 的 tombstone 数。
        /// </summary>
        private static int CountRecyclableSubscriptionTombstones(
            DynamicBuffer<AbilitySubscriptionSlot> slots)
        {
            var count = 0;
            for (var index = 0; index < slots.Length; index++)
            {
                var header = slots[index].Header;
                if (header.StorageState == GasSlabSlotState.Tombstone &&
                    header.Generation < uint.MaxValue)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 统计 pre-command 会回收的 PendingCommand tombstone 数。
        /// </summary>
        private static int CountRecyclablePendingTombstones(
            DynamicBuffer<PendingCommand> slots)
        {
            var count = 0;
            for (var index = 0; index < slots.Length; index++)
            {
                var header = slots[index].Header;
                if (header.StorageState == GasSlabSlotState.Tombstone &&
                    header.Generation < uint.MaxValue)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 统计 target ActiveEffect slab 在本 Tick 可递增 generation 的 tombstone 数。
        /// </summary>
        private static int CountRecyclableActiveTombstones(
            DynamicBuffer<ActiveEffectSlot> slots)
        {
            var count = 0;
            for (var index = 0; index < slots.Length; index++)
            {
                var header = slots[index].Header;
                if (header.StorageState == GasSlabSlotState.Tombstone &&
                    header.Generation < uint.MaxValue)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 证明 accepted cost mutation 的 Attribute revision 与索引不会在 OwnerWave 失败。
        /// </summary>
        private bool CanApplyOwnerCosts(
            in OwnerAscHandle owner,
            int planCount,
            DynamicBuffer<AttributeValueSlot> attributes)
        {
            for (var attributeIndex = 0; attributeIndex < attributes.Length; attributeIndex++)
            {
                var mutationCount = 0UL;
                for (var planIndex = 0; planIndex < planCount; planIndex++)
                {
                    var plan = OwnerPlans[planIndex];
                    if (plan.BusinessAccepted != 0 &&
                        plan.CommandKind == GasBoundaryCommandKind.Commit &&
                        plan.OwnerAsc.Equals(owner) &&
                        plan.CostAttributeLayoutIndex == attributeIndex)
                        mutationCount++;
                }
                if (mutationCount > uint.MaxValue - (ulong)attributes[attributeIndex].Revision)
                    return false;
            }
            for (var index = 0; index < planCount; index++)
            {
                var plan = OwnerPlans[index];
                if (plan.BusinessAccepted != 0 && plan.CommandKind == GasBoundaryCommandKind.Commit &&
                    plan.OwnerAsc.Equals(owner) && plan.CostAttributeLayoutIndex >= attributes.Length)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 证明每个 accepted cooldown 的 EndTick 加法不会在 OwnerWave 发生 ulong 回绕。
        /// </summary>
        private bool CanApplyOwnerCooldownTicks(
            in OwnerAscHandle owner,
            int planCount,
            ulong candidateTick)
        {
            for (var index = 0; index < planCount; index++)
            {
                var plan = OwnerPlans[index];
                if (plan.BusinessAccepted == 0 || plan.CommandKind != GasBoundaryCommandKind.Commit ||
                    !plan.OwnerAsc.Equals(owner) || plan.RequiresCooldownSlot == 0)
                    continue;
                if (plan.CooldownDurationTicks <= 0 ||
                    (ulong)plan.CooldownDurationTicks > ulong.MaxValue - candidateTick)
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 仅在 admission 失败时写固定 Session fault 控制证据，不写 gameplay fact/outbox 或结构 intent。
    /// </summary>
    internal struct GasFaultLatchJob : IJob
    {
        private const ulong HashOffsetBasis = 14695981039346656037UL;
        private const ulong HashPrime = 1099511628211UL;

        public Entity Session;
        public ulong SimulationEpoch;
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasTickExecutionState> Execution;
        public ComponentLookup<SessionFaultLatch> FaultLatches;
        public ComponentLookup<GasSessionLifecycle> Lifecycles;
        public byte IncludePostAdmissionFailure;

        /// <summary>
        /// 成功时只记录 lane；失败时锁存 deterministic FaultId、sealed subset 与 Faulted lifecycle。
        /// </summary>
        public void Execute()
        {
            var execution = Execution[0];
            if (execution.GameplayEnabled == 0)
                return;
            execution.LaneMask |= GasTickLaneMask.FaultLatch;
            var admission = Admission[0];
            var reason = admission.Succeeded == 0
                ? admission.FailureReason
                : IncludePostAdmissionFailure != 0
                    ? execution.PostAdmissionFailure
                    : GasTickAdmissionFailureReason.None;
            if (reason != GasTickAdmissionFailureReason.None)
            {
                var faultId = CreateFaultId(in execution, reason);
                FaultLatches[Session] = new SessionFaultLatch
                {
                    FaultId = faultId,
                    FaultEpoch = SimulationEpoch,
                    FaultTick = execution.CandidateTick,
                    ReasonCode = (int)reason,
                    Detected = 1,
                    SealedFirstRequestSequence = execution.SealedFirstRequestSequence,
                    SealedLastRequestSequence = execution.SealedLastRequestSequence,
                    SealedRequestCount = execution.SealedCommandCount,
                    SealedRequestHash = execution.SealedRequestHash,
                };
                var lifecycle = Lifecycles[Session];
                lifecycle.State = GasSessionLifecycleState.Faulted;
                Lifecycles[Session] = lifecycle;
            }
            Execution[0] = execution;
        }

        /// <summary>
        /// 由 Epoch、candidate tick、reason 与 sealed hash 构造非零平台无关 FNV-1a FaultId。
        /// </summary>
        private ulong CreateFaultId(
            in GasTickExecutionState execution,
            GasTickAdmissionFailureReason reason)
        {
            var hash = HashOffsetBasis;
            Add(ref hash, SimulationEpoch);
            Add(ref hash, execution.CandidateTick);
            Add(ref hash, unchecked((ulong)(int)reason));
            Add(ref hash, execution.SealedRequestHash);
            return hash == 0 ? 1UL : hash;
        }

        /// <summary>
        /// 按固定小端字节序把一个 ulong 加入 FaultId FNV-1a。
        /// </summary>
        private static void Add(ref ulong hash, ulong value)
        {
            for (var shift = 0; shift < 64; shift += 8)
            {
                hash ^= (byte)(value >> shift);
                hash = unchecked(hash * HashPrime);
            }
        }
    }

    /// <summary>
    /// 执行 admitted owner-local no-fail CommitPlan，并先提交 due cooldown maintenance。
    /// </summary>
    internal struct GasAscOwnerCommandWaveJob : IJob
    {
        public Entity Session;
        public ulong SimulationEpoch;
        [ReadOnly] public GasScaleProfile Profile;
        [ReadOnly] public BlobAssetReference<GasDefinitionCatalogBlob> Catalog;
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasOwnerPlanRecord> OwnerPlans;
        public NativeArray<GasAbilityRouteRecord> AbilityRoutes;
        public NativeArray<GasTickExecutionState> Execution;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public ComponentLookup<AscLifecycle> AscLifecycles;
        public ComponentLookup<SimulationTickState> Ticks;
        public ComponentLookup<AscSlabHeads> SlabHeads;
        public BufferLookup<GrantedAbilitySlot> Grants;
        public BufferLookup<AbilityActivationSlot> Activations;
        public BufferLookup<AbilityContinuationSlot> Continuations;
        public BufferLookup<AbilitySubscriptionSlot> Subscriptions;
        [ReadOnly] public BufferLookup<ActiveEffectSlot> ActiveEffects;
        public BufferLookup<CooldownGateSlot> Cooldowns;
        public BufferLookup<PendingCommand> PendingCommands;
        public BufferLookup<AttributeValueSlot> Attributes;
        public BufferLookup<AttributeDirtyWord> AttributeDirtyWords;
        public BufferLookup<TagCountSlot> TagCounts;
        public BufferLookup<TagPresenceWord> TagPresence;

        /// <summary>
        /// admission 成功后先释放 due gate，再按 canonical plan 顺序提交同 owner read-your-writes。
        /// </summary>
        public void Execute()
        {
            if (!GasTickJobUtility.EnterDownstream(Admission, Execution, GasTickLaneMask.OwnerWave))
                return;
            ref var catalog = ref Catalog.Value;
            RunPreCommandMaintenance();
            CleanupOrphanedSubscriptions();
            EvaluateTimers();
            ProcessDueWaitControlMessages();
            CleanupOrphanedSubscriptions();
            ReleaseDueCooldowns(ref catalog);
            var execution = Execution[0];
            for (var index = 0; index < execution.OwnerPlanCount; index++)
            {
                var plan = OwnerPlans[index];
                if (plan.BusinessAccepted == 0 || !TryResolveOwner(plan.OwnerAsc, out var asc))
                    continue;
                var heads = SlabHeads[asc];
                var applied = GasAbilityOwnerTransaction.ApplyPlan(
                    ref plan,
                    SimulationEpoch,
                    execution.CandidateTick,
                    in Profile,
                    ref catalog,
                    Grants[asc],
                    Activations[asc],
                    Cooldowns[asc],
                    Attributes[asc],
                    AttributeDirtyWords[asc],
                    TagCounts[asc],
                    TagPresence[asc],
                    ref heads);
                SlabHeads[asc] = heads;
                OwnerPlans[index] = plan;
                if (!applied)
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                    Execution[0] = execution;
                    return;
                }
            }
            ApplyPlannedWaitCancellations();
            FinalizeLocalEndingContinuations();
            var tick = Ticks[Session];
            tick.NextStableSequence = execution.NextStableSequenceAfterPlan;
            Ticks[Session] = tick;
        }

        /// <summary>
        /// observed writer 在 command 前清理 subscriber 已消失的 live Subscription，避免孤儿订阅常驻。
        /// </summary>
        private void CleanupOrphanedSubscriptions()
        {
            var registry = Registries[Session];
            for (var registryIndex = 0; registryIndex < registry.Length; registryIndex++)
            {
                var owner = registry[registryIndex];
                if (owner.State != GasAscRegistryState.Ready)
                    continue;
                var asc = owner.ResolveRuntimeEntity();
                if (!IsReadyOwner(owner.OwnerAsc, asc))
                    continue;
                var subscriptions = Subscriptions[asc];
                var heads = SlabHeads[asc];
                for (var index = 0; index < subscriptions.Length; index++)
                {
                    var slot = subscriptions[index];
                    if (slot.Header.StorageState != GasSlabSlotState.Live ||
                        (slot.State != GasAbilitySubscriptionState.PendingRegistration &&
                         slot.State != GasAbilitySubscriptionState.Registered &&
                         slot.State != GasAbilitySubscriptionState.Completed &&
                         slot.State != GasAbilitySubscriptionState.Ending) ||
                        TryResolveOwner(slot.SubscriberAsc, out _))
                        continue;
                    GasAbilityWaitSlabTransaction.TryTombstoneOrphanedSubscription(
                        in owner.OwnerAsc, in slot.Handle, subscriptions,
                        ref heads.AbilitySubscription);
                }
                SlabHeads[asc] = heads;
            }
        }

        /// <summary>
        /// 在 current commands 前只按冻结 DueTick 驱动 owner-local Timer completion。
        /// </summary>
        private void EvaluateTimers()
        {
            var execution = Execution[0];
            var registry = Registries[Session];
            for (var registryIndex = 0; registryIndex < registry.Length; registryIndex++)
            {
                var owner = registry[registryIndex];
                if (owner.State != GasAscRegistryState.Ready)
                    continue;
                var asc = owner.ResolveRuntimeEntity();
                if (!IsReadyOwner(owner.OwnerAsc, asc))
                    continue;
                var continuations = Continuations[asc];
                for (var index = 0; index < continuations.Length; index++)
                {
                    var slot = continuations[index];
                    if (slot.Header.StorageState == GasSlabSlotState.Live &&
                        slot.WaitSemantic == GasAbilityWaitSemantic.Timer)
                        GasAbilityWaitSlabTransaction.EvaluateTimer(
                            in slot.Handle, execution.CandidateTick, continuations);
                }
            }
        }

        /// <summary>
        /// 按内部 command 全序消费 due registration/signal/handshake，并把 completion 提升为 durable resume。
        /// </summary>
        private void ProcessDueWaitControlMessages()
        {
            var execution = Execution[0];
            var registry = Registries[Session];
            for (var registryIndex = 0; registryIndex < registry.Length; registryIndex++)
            {
                var owner = registry[registryIndex];
                if (owner.State != GasAscRegistryState.Ready)
                    continue;
                var asc = owner.ResolveRuntimeEntity();
                if (!IsReadyOwner(owner.OwnerAsc, asc))
                    continue;
                var heads = SlabHeads[asc];
                var commands = PendingCommands[asc];
                for (var ordinal = 0; ordinal < commands.Length; ordinal++)
                {
                    var commandIndex = FindNextWaitControlCommand(
                        owner.OwnerAsc, commands, execution.CandidateTick);
                    if (commandIndex < 0)
                        break;
                    ApplyWaitControlCommand(owner.OwnerAsc, asc, commandIndex, commands, ref heads);
                }
                SlabHeads[asc] = heads;
            }
        }

        /// <summary>
        /// 从 non-compacting command slab 选择尚未处理的最小正式 command key。
        /// </summary>
        private static int FindNextWaitControlCommand(
            in OwnerAscHandle owner,
            DynamicBuffer<PendingCommand> commands,
            ulong candidateTick)
        {
            var selected = -1;
            for (var index = 0; index < commands.Length; index++)
            {
                var candidate = commands[index];
                if (candidate.Header.StorageState != GasSlabSlotState.Live ||
                    candidate.State != GasSlotBusinessState.Pending ||
                    candidate.AvailableTick != candidateTick ||
                    !candidate.TargetAsc.Equals(owner) ||
                    !IsWaitControlKind(candidate.CommandKind))
                    continue;
                var selectedCommand = selected >= 0 ? commands[selected] : default;
                if (selected < 0 || CompareWaitControl(in candidate, in selectedCommand) < 0)
                    selected = index;
            }
            return selected;
        }

        /// <summary>
        /// 应用一条 due wait control，并将无论成功或 stale 的消息终结为 tombstone。
        /// </summary>
        private void ApplyWaitControlCommand(
            in OwnerAscHandle owner,
            Entity asc,
            int commandIndex,
            DynamicBuffer<PendingCommand> commands,
            ref AscSlabHeads heads)
        {
            var command = commands[commandIndex];
            if (IsObservedWaitInput(command.CommandKind) &&
                !CanDeliverObservedWaitInput(asc, in command))
            {
                var noResponse = default(PendingCommand);
                CompleteControlResponseRoute(
                    in command,
                    GasAbilityWaitProtocolStatus.NoOpStale,
                    in noResponse);
                TombstonePendingCommand(commandIndex, commands, ref heads.PendingCommand);
                return;
            }
            if (command.CommandKind == (int)GasAbilityPendingCommandKind.WaitRegistration)
            {
                ApplyRegistrationCommand(
                    in owner, asc, commandIndex, in command, commands, ref heads);
                return;
            }
            if (command.CommandKind == (int)GasAbilityPendingCommandKind.WaitSignal)
            {
                ApplySignalCommand(
                    in owner, asc, commandIndex, in command, commands, ref heads);
                return;
            }
            if (command.CommandKind == (int)GasAbilityPendingCommandKind.WaitUnsubscribe)
            {
                var applied = GasAbilityWaitSlabTransaction.UnsubscribeObserved(
                    in owner, in command, command.AvailableTick,
                    Profile.MaxAbilitySubscriptionCount, Subscriptions[asc],
                    ref heads.AbilitySubscription, out var response);
                CompleteControlResponseRoute(
                    in command,
                    applied ? GasAbilityWaitProtocolStatus.Ending :
                        GasAbilityWaitProtocolStatus.InvalidRequest,
                    in response);
            }
            else if (command.CommandKind == (int)GasAbilityPendingCommandKind.WaitUnsubscribeAck)
            {
                GasAbilityWaitSlabTransaction.ApplyCancelAckOwner(
                    in owner, in command, Activations[asc], Continuations[asc],
                    ref heads.AbilityContinuation);
            }
            else
            {
                var applied = GasAbilityWaitSlabTransaction.ApplyOwnerResponse(
                    in owner, in command, Activations[asc], Continuations[asc]);
                if (applied && command.CommandKind ==
                    (int)GasAbilityPendingCommandKind.WaitCompletion &&
                    !IsEndingActivation(command.Activation, Activations[asc]) &&
                    GasAbilityWaitSlabTransaction.TryMarkOwnerResponseRunnable(
                        in owner, commandIndex, command.AvailableTick, commands))
                    return;
            }
            TombstonePendingCommand(commandIndex, commands, ref heads.PendingCommand);
        }

        /// <summary>
        /// 标记只允许在仍存活 subscriber owner 上执行的 observed 输入类型。
        /// </summary>
        private static bool IsObservedWaitInput(int commandKind)
        {
            return commandKind == (int)GasAbilityPendingCommandKind.WaitRegistration ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitSignal ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitUnsubscribe;
        }

        /// <summary>
        /// observed 输入只有在 source 或 Subscription subscriber 仍可写时才执行。
        /// </summary>
        private bool CanDeliverObservedWaitInput(
            Entity observedAsc,
            in PendingCommand command)
        {
            if (command.CommandKind != (int)GasAbilityPendingCommandKind.WaitSignal)
                return TryResolveOwner(command.SourceAsc, out _);
            if (!TryResolveSubscription(
                    command.Subscription,
                    Subscriptions[observedAsc],
                    out var subscription))
                return false;
            return TryResolveOwner(subscription.SubscriberAsc, out _);
        }

        /// <summary>
        /// 解析 observed ASC 上的 live Subscription，复验 generation 与 typed handle。
        /// </summary>
        private static bool TryResolveSubscription(
            in AbilitySubscriptionHandle handle,
            DynamicBuffer<AbilitySubscriptionSlot> subscriptions,
            out AbilitySubscriptionSlot subscription)
        {
            subscription = default;
            if (!handle.IsValid || handle.SlotIndex < 0 || handle.SlotIndex >= subscriptions.Length)
                return false;
            subscription = subscriptions[handle.SlotIndex];
            return subscription.Header.StorageState == GasSlabSlotState.Live &&
                   subscription.Header.Generation == handle.SlotGeneration &&
                   subscription.Handle.Equals(handle);
        }

        /// <summary>
        /// observed writer 以当前 authority sample 注册，并用实际 Subscription handle 完成预规划 route。
        /// </summary>
        private void ApplyRegistrationCommand(
            in OwnerAscHandle owner,
            Entity asc,
            int commandIndex,
            in PendingCommand command,
            DynamicBuffer<PendingCommand> commands,
            ref AscSlabHeads heads)
        {
            var routeIndex = FindControlResponseRoute(in command);
            var canSample = GasAbilityWaitObservationUtility.TrySampleRegistration(
                in owner, in command, TagCounts[asc], Grants[asc], Activations[asc],
                Continuations[asc], Subscriptions[asc], Cooldowns[asc], ActiveEffects[asc],
                out var sample);
            var status = GasAbilityWaitProtocolStatus.InvalidRequest;
            var response = default(PendingCommand);
            if (canSample && routeIndex >= 0)
            {
                var registrationSequence = AbilityRoutes[routeIndex].Command.CommandSequence;
                status = GasAbilityWaitSlabTransaction.SampleAndRegisterObserved(
                    in owner, in command, in sample, command.AvailableTick, registrationSequence,
                    Profile.MaxAbilitySubscriptionCount, Subscriptions[asc],
                    ref heads.AbilitySubscription, out response);
            }
            CompleteControlResponseRoute(in command, status, in response);
            TombstonePendingCommand(commandIndex, commands, ref heads.PendingCommand);
        }

        /// <summary>
        /// observed writer 按冻结 signal 生成唯一 wake；one-shot 在 route 冻结后立即 tombstone Subscription。
        /// </summary>
        private void ApplySignalCommand(
            in OwnerAscHandle owner,
            Entity asc,
            int commandIndex,
            in PendingCommand command,
            DynamicBuffer<PendingCommand> commands,
            ref AscSlabHeads heads)
        {
            var status = GasAbilityWaitSlabTransaction.WakeObserved(
                in owner, in command, command.AvailableTick, Subscriptions[asc], out var response);
            CompleteControlResponseRoute(in command, status, in response);
            if (status == GasAbilityWaitProtocolStatus.Completed)
            {
                GasAbilityWaitSlabTransaction.FinalizeObservedCompletion(
                    in command.Subscription, Subscriptions[asc], ref heads.AbilitySubscription);
            }
            TombstonePendingCommand(commandIndex, commands, ref heads.PendingCommand);
        }

        /// <summary>
        /// 以 origin 稳定序列定位 admission 前预排的唯一 observed response route。
        /// </summary>
        private int FindControlResponseRoute(in PendingCommand origin)
        {
            var execution = Execution[0];
            for (var index = 0; index < execution.AbilityRouteCount; index++)
            {
                var route = AbilityRoutes[index];
                if (route.Used != 0 && route.RequiresOwnerApply != 0 &&
                    route.OriginCommandSequence == origin.CommandSequence &&
                    route.OriginCommandKind == origin.CommandKind)
                    return index;
            }
            return -1;
        }

        /// <summary>
        /// 成功时以实际 response 替换模板并保留 route 序列；no-op/stale 则撤销预留 route。
        /// </summary>
        private void CompleteControlResponseRoute(
            in PendingCommand origin,
            GasAbilityWaitProtocolStatus status,
            in PendingCommand actualResponse)
        {
            var routeIndex = FindControlResponseRoute(in origin);
            if (routeIndex < 0)
                return;
            var route = AbilityRoutes[routeIndex];
            if (!HasDurableResponse(status))
            {
                route.Used = 0;
                route.RequiresOwnerApply = 0;
                AbilityRoutes[routeIndex] = route;
                return;
            }
            if (!TryResolveOwner(actualResponse.TargetAsc, out _))
            {
                route.Used = 0;
                route.RequiresOwnerApply = 0;
                AbilityRoutes[routeIndex] = route;
                return;
            }
            var response = actualResponse;
            response.CommandSequence = route.Command.CommandSequence;
            response.RegistrationSequence = route.Command.RegistrationSequence != 0
                ? route.Command.RegistrationSequence
                : route.Command.CommandSequence;
            response.RecipientKindPriority = route.Command.RecipientKindPriority;
            response.MatchedTagDepth = route.Command.MatchedTagDepth;
            route.Command = response;
            route.RequiresOwnerApply = 0;
            AbilityRoutes[routeIndex] = route;
        }

        /// <summary>
        /// 判断 observed transaction 是否产生必须持久投递的 Ack、completion 或 persistent wake。
        /// </summary>
        private static bool HasDurableResponse(GasAbilityWaitProtocolStatus status)
        {
            return status == GasAbilityWaitProtocolStatus.Registered ||
                   status == GasAbilityWaitProtocolStatus.Completed ||
                   status == GasAbilityWaitProtocolStatus.PersistentWake ||
                   status == GasAbilityWaitProtocolStatus.Ending;
        }

        /// <summary>
        /// 把 sample、signal 与四类握手消息全部纳入同一正式 owner command order。
        /// </summary>
        private static bool IsWaitControlKind(int commandKind)
        {
            return commandKind == (int)GasAbilityPendingCommandKind.WaitRegistrationAck ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitCompletion ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitUnsubscribe ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitUnsubscribeAck ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitRegistration ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitSignal;
        }

        /// <summary>
        /// 比较同 destination 的冻结 schema command key，不依赖 enum 或 Job phase 顺序。
        /// </summary>
        private static int CompareWaitControl(in PendingCommand left, in PendingCommand right)
        {
            return GasAbilityPendingCommandOrder.Compare(in left, in right);
        }

        /// <summary>
        /// 终结已消费 pending command；admission 已证明 allocator metadata 与容量。
        /// </summary>
        private static void TombstonePendingCommand(
            int commandIndex,
            DynamicBuffer<PendingCommand> commands,
            ref GasSlabHead head)
        {
            var storage = new GasPendingCommandSlabStorage { Buffer = commands };
            if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                    in head, ref storage, commandIndex) != GasSlabStorageFailure.None)
                return;
            var command = commands[commandIndex];
            command.State = GasSlotBusinessState.Terminal;
            commands[commandIndex] = command;
        }

        /// <summary>
        /// 将 admission 前冻结的 unsubscribe 与 owner Continuation generation 一次提交。
        /// </summary>
        private void ApplyPlannedWaitCancellations()
        {
            var execution = Execution[0];
            for (var index = 0; index < execution.AbilityRouteCount; index++)
            {
                var route = AbilityRoutes[index];
                if (route.Used == 0 ||
                    route.Command.CommandKind != (int)GasAbilityPendingCommandKind.WaitUnsubscribe ||
                    !TryResolveOwner(route.Command.SourceAsc, out var asc))
                    continue;
                if (GasAbilityWaitSlabTransaction.ApplyPlannedOwnerCancellation(
                        in route.Command, Activations[asc], Continuations[asc]))
                    continue;
                route.Used = 0;
                AbilityRoutes[index] = route;
            }
        }

        /// <summary>
        /// Ending Activation 的 Timer 与已完成 one-shot 无远端交接，当前 Tick 可本地 tombstone。
        /// </summary>
        private void FinalizeLocalEndingContinuations()
        {
            var registry = Registries[Session];
            for (var registryIndex = 0; registryIndex < registry.Length; registryIndex++)
            {
                var owner = registry[registryIndex];
                if (owner.State != GasAscRegistryState.Ready)
                    continue;
                var asc = owner.ResolveRuntimeEntity();
                if (!IsReadyOwner(owner.OwnerAsc, asc))
                    continue;
                var heads = SlabHeads[asc];
                FinalizeLocalEndingContinuations(asc, ref heads.AbilityContinuation);
                SlabHeads[asc] = heads;
            }
        }

        /// <summary>
        /// 对单 owner 扫描 bounded Continuation slab，并只处理无需 unsubscribe Ack 的终态。
        /// </summary>
        private void FinalizeLocalEndingContinuations(Entity asc, ref GasSlabHead continuationHead)
        {
            var activations = Activations[asc];
            var continuations = Continuations[asc];
            for (var index = 0; index < continuations.Length; index++)
            {
                var slot = continuations[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live ||
                    !IsEndingActivation(slot.Activation, activations))
                    continue;
                if (slot.WaitSemantic == GasAbilityWaitSemantic.Timer)
                {
                    GasAbilityWaitSlabTransaction.TryCancelLocalTimerWait(
                        in slot.Handle, activations, continuations, ref continuationHead);
                }
                else if (slot.WaitState == GasAbilityWaitState.Completed)
                {
                    GasAbilityWaitSlabTransaction.TryConsumeCompletedOwnerWait(
                        in slot.Handle, activations, continuations, ref continuationHead);
                }
            }
        }

        /// <summary>
        /// 判断 Continuation 的完整 Activation handle 是否仍命中 Ending live 槽。
        /// </summary>
        private static bool IsEndingActivation(
            in AbilityActivationHandle handle,
            DynamicBuffer<AbilityActivationSlot> activations)
        {
            if (!handle.IsValid || handle.SlotIndex < 0 || handle.SlotIndex >= activations.Length)
                return false;
            var activation = activations[handle.SlotIndex];
            return activation.Header.StorageState == GasSlabSlotState.Live &&
                   activation.Handle.Equals(handle) && activation.Phase == GasAbilityActivationPhase.Ending;
        }

        /// <summary>
        /// 回收前一 Tick tombstone，并在 Boundary command 前应用 due grant removal。
        /// </summary>
        private void RunPreCommandMaintenance()
        {
            var execution = Execution[0];
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State != GasAscRegistryState.Ready)
                    continue;
                var asc = slot.ResolveRuntimeEntity();
                if (!IsReadyOwner(slot.OwnerAsc, asc))
                    continue;
                var heads = SlabHeads[asc];
                var battle = slot.BattleInstance;
                GasAbilityLifecycleMaintenance.RunPreCommand(
                    execution.CandidateTick,
                    in battle,
                    Grants[asc],
                    Activations[asc],
                    Continuations[asc],
                    Subscriptions[asc],
                    PendingCommands[asc],
                    ref heads);
                SlabHeads[asc] = heads;
            }
        }

        /// <summary>
        /// 对每个 Ready ASC 执行一次 tick-start cooldown release，任何 command 都不能绕过。
        /// </summary>
        private void ReleaseDueCooldowns(ref GasDefinitionCatalogBlob catalog)
        {
            var execution = Execution[0];
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State != GasAscRegistryState.Ready)
                    continue;
                var asc = slot.ResolveRuntimeEntity();
                if (!IsReadyOwner(slot.OwnerAsc, asc))
                    continue;
                var heads = SlabHeads[asc];
                GasAbilityOwnerTransaction.ReleaseDueCooldowns(
                    execution.CandidateTick,
                    ref catalog,
                    Cooldowns[asc],
                    TagCounts[asc],
                    TagPresence[asc],
                    ref heads.CooldownGate);
                SlabHeads[asc] = heads;
            }
        }

        /// <summary>
        /// 通过唯一 registry 将稳定 owner identity 解析为 Runtime Entity。
        /// </summary>
        private bool TryResolveOwner(in OwnerAscHandle owner, out Entity asc)
        {
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State == GasAscRegistryState.Ready && slot.OwnerAsc.Equals(owner))
                {
                    asc = slot.ResolveRuntimeEntity();
                    return IsReadyOwner(owner, asc);
                }
            }
            asc = Entity.Null;
            return false;
        }

        /// <summary>
        /// 只有 registry 指向且 lifecycle 仍为 Ready/Alive 的 ASC 才可进入 OwnerWave。
        /// </summary>
        private bool IsReadyOwner(in OwnerAscHandle owner, Entity asc)
        {
            if (!AscIdentities.HasComponent(asc) || !AscLifecycles.HasComponent(asc))
                return false;
            var identity = AscIdentities[asc];
            var state = AscLifecycles[asc].State;
            return identity.SimulationEpoch == SimulationEpoch && identity.OwnerAsc.Equals(owner) &&
                   (state == GasAscLifecycleState.Ready || state == GasAscLifecycleState.Alive);
        }
    }

    /// <summary>
    /// 只为成功 Commit 的正式身份密封 source-bound spec，后续 target lane 只读取该不可变投影。
    /// </summary>
    internal struct GasSourceSpecProjectionJob : IJob
    {
        public Entity Session;
        [ReadOnly] public BlobAssetReference<GasDefinitionCatalogBlob> Catalog;
        public ulong SimulationEpoch;
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public NativeArray<GasEffectOperationRecord> EffectOperations;
        [ReadOnly] public NativeArray<GasTargetResolveRejectionRecord> TargetResolveRejections;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public BufferLookup<AttributeValueSlot> Attributes;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        public NativeArray<GasSourceSpecRecord> SourceSpecs;
        public NativeArray<float> CaptureValues;
        public NativeArray<float> ValueViews;
        public int CaptureStride;
        public int ValueViewStride;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 将 admission 后的 effect operation 密封为 source-bound spec，并拒绝不完整身份。
        /// </summary>
        public void Execute()
        {
            if (!GasTickJobUtility.EnterDownstream(
                    Admission, Execution, GasTickLaneMask.SourceProjection))
                return;

            var execution = Execution[0];
            execution.SourceSpecCount = 0;
            for (var index = 0; index < execution.EffectOperationCount; index++)
            {
                var operation = EffectOperations[index];
                if (operation.ApplicationId == 0 ||
                    !operation.SourceAsc.IsValid || !operation.TargetAsc.IsValid ||
                    operation.DefinitionIndex < 0)
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                    break;
                }
                if (execution.SourceSpecCount >= SourceSpecs.Length)
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                    break;
                }
                var specOrdinal = execution.SourceSpecCount;
                if (!TryProjectInputs(
                        in operation,
                        specOrdinal,
                        out var captureStart,
                        out var captureCount,
                        out var valueViewStart,
                        out var valueViewCount))
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                    break;
                }
                SourceSpecs[execution.SourceSpecCount++] = new GasSourceSpecRecord
                {
                    OperationOrdinal = index,
                    DefinitionIndex = operation.DefinitionIndex,
                    SourceAsc = operation.SourceAsc,
                    TargetAsc = operation.TargetAsc,
                    ApplicationId = operation.ApplicationId,
                    StartTick = operation.StartTick,
                    TargetIsAlive = operation.TargetIsAlive,
                    CausalityId = operation.CausalityId,
                    Scope = GasBoundaryFactScope.Asc,
                    CaptureStart = captureStart,
                    CaptureCount = captureCount,
                    ValueViewStart = valueViewStart,
                    ValueViewCount = valueViewCount,
                    TargetAvatarStableId = operation.TargetAvatarStableId,
                    TargetAvatarBindingGeneration = operation.TargetAvatarBindingGeneration,
                    SpatialSnapshot = operation.SpatialSnapshot,
                };
            }
            if (execution.PostAdmissionFailure == GasTickAdmissionFailureReason.None)
            {
                for (var index = 0; index < execution.TargetResolveRejectionCount; index++)
                {
                    if (execution.SourceSpecCount >= SourceSpecs.Length)
                    {
                        execution.PostAdmissionFailure =
                            GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                        break;
                    }
                    var rejection = TargetResolveRejections[index];
                    if (rejection.ApplicationId == 0 ||
                        rejection.CausalityId == 0 ||
                        rejection.Outcome == GasGameplayEffectApplicationOutcome.None)
                    {
                        execution.PostAdmissionFailure =
                            GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                        break;
                    }
                    var operationOrdinal = execution.SourceSpecCount;
                    var captureStart = 0;
                    var captureCount = 0;
                    var valueViewStart = 0;
                    var valueViewCount = 0;
                    SourceSpecs[operationOrdinal] = new GasSourceSpecRecord
                    {
                        OperationOrdinal = operationOrdinal,
                        DefinitionIndex = rejection.DefinitionIndex,
                        SourceAsc = rejection.SourceAsc,
                        TargetAsc = rejection.TargetAsc,
                        ApplicationId = rejection.ApplicationId,
                        StartTick = execution.CandidateTick,
                        TargetIsAlive = 0,
                        CausalityId = rejection.CausalityId,
                        Scope = rejection.Scope,
                        CaptureStart = captureStart,
                        CaptureCount = captureCount,
                        ValueViewStart = valueViewStart,
                        ValueViewCount = valueViewCount,
                        IsTargetResolveRejection = 1,
                        RejectionOutcome = rejection.Outcome,
                        RejectionFailure = rejection.Failure,
                    };
                    execution.SourceSpecCount++;
                }
            }
            Execution[0] = execution;
        }

        /// <summary>
        /// 为一个 source-bound operation 分配输入行并冻结 Source Snapshot；Target Snapshot 延迟到 target writer。
        /// </summary>
        private bool TryProjectInputs(
            in GasEffectOperationRecord operation,
            int specOrdinal,
            out int captureStart,
            out int captureCount,
            out int valueViewStart,
            out int valueViewCount)
        {
            captureStart = 0;
            captureCount = 0;
            valueViewStart = 0;
            valueViewCount = 0;
            if (!Catalog.IsCreated || CaptureStride <= 0 || ValueViewStride <= 0 ||
                operation.DefinitionIndex < 0 ||
                operation.DefinitionIndex >= Catalog.Value.GameplayEffects.Length ||
                specOrdinal < 0)
                return false;
            var definition = Catalog.Value.GameplayEffects[operation.DefinitionIndex];
            captureCount = definition.CaptureRange.Count;
            valueViewCount = definition.ValueViewRange.Count;
            if (!IsRangeValid(in definition.CaptureRange, Catalog.Value.CaptureDescriptors.Length) ||
                !IsRangeValid(in definition.ValueViewRange, Catalog.Value.ValueViews.Length))
                return false;
            if (specOrdinal > int.MaxValue / CaptureStride ||
                specOrdinal > int.MaxValue / ValueViewStride)
                return false;
            captureStart = specOrdinal * CaptureStride;
            valueViewStart = specOrdinal * ValueViewStride;
            if (captureCount > CaptureStride || valueViewCount > ValueViewStride ||
                captureStart < 0 || valueViewStart < 0 ||
                captureStart > CaptureValues.Length - captureCount ||
                valueViewStart > ValueViews.Length - valueViewCount)
                return false;
            for (var offset = 0; offset < captureCount; offset++)
            {
                var descriptor = Catalog.Value.CaptureDescriptors[
                    definition.CaptureRange.Start + offset];
                if (descriptor.Owner != GasCaptureOwner.Source)
                    continue;
                if (descriptor.Binding != GasCaptureBinding.Snapshot ||
                    descriptor.Phase != GasCapturePhase.SourceSpecProjection ||
                    !TryResolveAttributeValue(
                        in operation.SourceAsc,
                        descriptor.AttributeLayoutIndex,
                        descriptor.ValueView,
                        out var value))
                    return false;
                CaptureValues[captureStart + offset] = value;
            }
            // 所有 descriptor 行都必须是有限值；Target-owned snapshot 将在 TargetWave 重新填充。
            for (var offset = 0; offset < captureCount; offset++)
            {
                if (Catalog.Value.CaptureDescriptors[definition.CaptureRange.Start + offset].Owner ==
                    GasCaptureOwner.Source && !IsFinite(CaptureValues[captureStart + offset]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 通过 Session Ready registry 读取 source ASC 的声明式 Attribute view。
        /// </summary>
        private bool TryResolveAttributeValue(
            in OwnerAscHandle owner,
            int attributeIndex,
            GasAttributeValueView view,
            out float value)
        {
            value = 0f;
            if (!owner.IsValid || attributeIndex < 0 || Session == Entity.Null)
                return false;
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State != GasAscRegistryState.Ready || !slot.OwnerAsc.Equals(owner))
                    continue;
                var asc = slot.ResolveRuntimeEntity();
                if (!AscIdentities.HasComponent(asc) || !Attributes.HasBuffer(asc) ||
                    AscIdentities[asc].SimulationEpoch != SimulationEpoch ||
                    !AscIdentities[asc].OwnerAsc.Equals(owner))
                    return false;
                var values = Attributes[asc];
                if (attributeIndex >= values.Length ||
                    attributeIndex >= Catalog.Value.AttributeLayout.Entries.Length)
                    return false;
                var entry = Catalog.Value.AttributeLayout.Entries[attributeIndex];
                var attribute = values[attributeIndex];
                switch (view)
                {
                    case GasAttributeValueView.Base:
                        value = attribute.Base;
                        return IsFinite(value);
                    case GasAttributeValueView.Current:
                    case GasAttributeValueView.Final:
                        value = attribute.Current;
                        return IsFinite(value);
                    case GasAttributeValueView.DefinitionMaxValue:
                        value = entry.MaximumValue;
                        return IsFinite(value);
                    default:
                        return false;
                }
            }
            return false;
        }

        /// <summary>
        /// 使用减法验证 descriptor range，防止损坏 Catalog 的整数回绕。
        /// </summary>
        private static bool IsRangeValid(in GasCatalogRange range, int length)
        {
            return range.Start >= 0 && range.Count >= 0 && range.Start <= length &&
                   range.Count <= length - range.Start;
        }

        /// <summary>
        /// 拒绝 NaN/Infinity 进入 source snapshot。
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// 按稳定 target identity 对 source-bound spec 建立 canonical ranges，保证每个 target 单写。
    /// </summary>
    internal struct GasGroupWorkByTargetJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasSourceSpecRecord> SourceSpecs;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 使用稳定 target、application 与 operation ordinal 做有界插入排序，不依赖 Job 完成顺序。
        /// </summary>
        public void Execute()
        {
            if (!GasTickJobUtility.EnterDownstream(
                    Admission, Execution, GasTickLaneMask.GroupTarget))
                return;

            var execution = Execution[0];
            for (var index = 1; index < execution.SourceSpecCount; index++)
            {
                var value = SourceSpecs[index];
                var position = index - 1;
                while (position >= 0)
                {
                    var previous = SourceSpecs[position];
                    if (Compare(in previous, in value) <= 0)
                        break;
                    SourceSpecs[position + 1] = SourceSpecs[position];
                    position--;
                }
                SourceSpecs[position + 1] = value;
            }
        }

        /// <summary>
        /// 按 target identity、application identity 与原始 operation ordinal 比较 source spec。
        /// </summary>
        private static int Compare(in GasSourceSpecRecord left, in GasSourceSpecRecord right)
        {
            var comparison = left.TargetAsc.AscStableId.CompareTo(right.TargetAsc.AscStableId);
            if (comparison != 0)
                return comparison;
            comparison = left.TargetAsc.AscGeneration.CompareTo(right.TargetAsc.AscGeneration);
            if (comparison != 0)
                return comparison;
            comparison = left.ApplicationId.CompareTo(right.ApplicationId);
            return comparison != 0
                ? comparison
                : left.OperationOrdinal.CompareTo(right.OperationOrdinal);
        }
    }

    /// <summary>
    /// 执行 target-local 单写 application transaction，并把结果仅写入 tick-local shadow。
    /// </summary>
    internal struct GasAscTargetPrepareJob : IJob
    {
        public ulong SimulationEpoch;
        [ReadOnly] public GasScaleProfile Profile;
        [ReadOnly] public BlobAssetReference<GasDefinitionCatalogBlob> Catalog;
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public NativeArray<GasSourceSpecRecord> SourceSpecs;
        public NativeArray<float> CaptureValues;
        public NativeArray<float> ValueViews;
        public NativeArray<float> PeriodCaptureValues;
        public NativeArray<float> PeriodValueViews;
        public int CaptureStride;
        public int ValueViewStride;
        public NativeArray<float> EvaluatorStack;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public ComponentLookup<AscLifecycle> AscLifecycles;
        [ReadOnly] public ComponentLookup<AscSlabHeads> SlabHeads;
        [ReadOnly] public ComponentLookup<GasPayloadRangeAllocatorState> PayloadStates;
        [ReadOnly] public BufferLookup<ActiveEffectSlot> ActiveEffects;
        [ReadOnly] public ComponentLookup<GasActorBinding> ActorBindings;
        [ReadOnly] public BufferLookup<AttributeValueSlot> Attributes;
        [ReadOnly] public BufferLookup<AttributeDirtyWord> AttributeDirtyWords;
        [ReadOnly] public BufferLookup<TagCountSlot> TagCounts;
        [ReadOnly] public BufferLookup<TagPresenceWord> TagPresenceWords;
        [ReadOnly] public BufferLookup<GasPayloadRangeRecord> PayloadRanges;
        [ReadOnly] public BufferLookup<GasPayloadValueSlot> PayloadValues;
        [ReadOnly] public ComponentLookup<GasTargetPrepareFaultInjection> FaultInjections;
        public EntityCommandBuffer ShadowCommands;
        public NativeArray<GasTargetShadowState> TargetShadows;
        public NativeArray<ActiveEffectSlot> ShadowActiveEffects;
        public NativeArray<AttributeValueSlot> ShadowAttributes;
        public NativeArray<AttributeDirtyWord> ShadowAttributeDirtyWords;
        public NativeArray<TagCountSlot> ShadowTagCounts;
        public NativeArray<TagPresenceWord> ShadowTagPresenceWords;
        public NativeArray<GasPayloadRangeRecord> ShadowPayloadRanges;
        public NativeArray<GasPayloadValueSlot> ShadowPayloadValues;
        public int ActiveEffectStride;
        public int AttributeStride;
        public int AttributeDirtyWordStride;
        public int TagStride;
        public int TagPresenceWordStride;
        public int PayloadRangeStride;
        public int PayloadValueStride;
        public NativeArray<GasApplicationOutcomeRecord> ApplicationOutcomes;
        public NativeArray<GasAttributeMutationRecord> AttributeMutations;
        public NativeArray<GasAttributeMutationOutcomeRecord> MutationOutcomes;
        public NativeArray<GasTickExecutionState> Execution;

        private struct PreparedCaptureRange
        {
            public PayloadRangeHandle Handle;
            public byte Allocated;
        }

        /// <summary>
        /// 把一个 target 的组件副本与临时 DynamicBuffer 聚合为现有 transaction 的唯一可写上下文。
        /// </summary>
        private struct PreparedTargetState
        {
            public int ShadowIndex;
            public Entity Target;
            public OwnerAscHandle OwnerAsc;
            public AscLifecycle Lifecycle;
            public AscSlabHeads SlabHeads;
            public GasPayloadRangeAllocatorState PayloadState;
            public DynamicBuffer<ActiveEffectSlot> ActiveEffects;
            public DynamicBuffer<AttributeValueSlot> Attributes;
            public DynamicBuffer<AttributeDirtyWord> AttributeDirtyWords;
            public DynamicBuffer<TagCountSlot> TagCounts;
            public DynamicBuffer<TagPresenceWord> TagPresenceWords;
            public DynamicBuffer<GasPayloadRangeRecord> PayloadRanges;
            public DynamicBuffer<GasPayloadValueSlot> PayloadValues;
        }

        /// <summary>
        /// admission 成功后按 scratch canonical 顺序准备 target-owned effect transactions。
        /// </summary>
        public void Execute()
        {
            if (!GasTickJobUtility.EnterDownstream(Admission, Execution, GasTickLaneMask.TargetWave))
                return;
            var execution = Execution[0];
            execution.ApplicationOutcomeCount = 0;
            execution.AttributeMutationCount = 0;
            ref var catalog = ref Catalog.Value;
            // 冻结顺序：先处理已有 ActiveEffect 的 maintenance/period/expiry，再提交本 Tick 新 application。
            if (execution.PostAdmissionFailure == GasTickAdmissionFailureReason.None &&
                !ProcessPeriodEffects(ref execution, ref catalog))
            {
                execution.PostAdmissionFailure =
                    GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
            }
            if (execution.PostAdmissionFailure == GasTickAdmissionFailureReason.None &&
                !ProcessApplications(ref execution, ref catalog))
            {
                execution.PostAdmissionFailure =
                    GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
            }
            Execution[0] = execution;
        }

        /// <summary>
        /// 按已排序 target range 复用同一个 shadow 上下文，保持同 target application read-your-writes。
        /// </summary>
        private bool ProcessApplications(
            ref GasTickExecutionState execution,
            ref GasDefinitionCatalogBlob catalog)
        {
            var index = 0;
            var preparedApplicationCount = 0;
            while (index < execution.SourceSpecCount)
            {
                var spec = SourceSpecs[index];
                if (spec.IsTargetResolveRejection != 0)
                {
                    if (!StoreTargetResolveRejection(ref execution, in spec))
                        return false;
                    index++;
                    continue;
                }
                if (!TryFindShadowIndex(in spec.TargetAsc, out var shadowIndex))
                {
                    StoreStaleTargetOutcome(ref execution, in spec);
                    index++;
                    continue;
                }
                if (!TryCreatePreparedStateFromShadow(shadowIndex, out var state))
                    return false;
                do
                {
                    spec = SourceSpecs[index];
                    if (!spec.TargetAsc.Equals(state.OwnerAsc) ||
                        spec.IsTargetResolveRejection != 0)
                        break;
                    if (!ApplyApplication(ref state, in spec, ref execution, ref catalog))
                        return false;
                    preparedApplicationCount++;
                    index++;
                }
                while (index < execution.SourceSpecCount);
                if (!TryStorePreparedState(ref state))
                    return false;
                if (ShouldInjectPrepareFailure(preparedApplicationCount))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 仅在 Session 显式安装 conformance 组件时，于完整 shadow 落盘后触发确定性 fatal。
        /// </summary>
        private bool ShouldInjectPrepareFailure(int preparedApplicationCount)
        {
            if (!FaultInjections.HasComponent(Session))
                return false;
            var threshold = FaultInjections[Session].FailAfterPreparedApplicationCount;
            return threshold > 0 && preparedApplicationCount >= threshold;
        }

        /// <summary>
        /// 将一条 TargetResolve typed rejection 写入 outcome scratch，不创建任何 target shadow。
        /// </summary>
        private bool StoreTargetResolveRejection(
            ref GasTickExecutionState execution,
            in GasSourceSpecRecord spec)
        {
            if (spec.ApplicationId == 0 || spec.CausalityId == 0 ||
                spec.RejectionOutcome == GasGameplayEffectApplicationOutcome.None)
                return false;
            var outcome = new GasGameplayEffectApplicationResult
            {
                Outcome = spec.RejectionOutcome,
                Failure = spec.RejectionFailure,
                ApplicationId = spec.ApplicationId,
                CausalityId = spec.CausalityId,
            };
            StoreOutcome(ref execution, in spec, in outcome);
            return execution.PostAdmissionFailure == GasTickAdmissionFailureReason.None;
        }

        /// <summary>
        /// 将准备期无法解析的 target 固化为 typed stale outcome，而不是提升为 Session fatal。
        /// </summary>
        private void StoreStaleTargetOutcome(
            ref GasTickExecutionState execution,
            in GasSourceSpecRecord spec)
        {
            var outcome = new GasGameplayEffectApplicationResult
            {
                Outcome = GasGameplayEffectApplicationOutcome.RejectedStaleBinding,
                Failure = GasGameplayEffectTransactionFailure.InvalidIdentity,
                ApplicationId = spec.ApplicationId,
                CausalityId = spec.CausalityId,
            };
            StoreOutcome(ref execution, in spec, in outcome);
        }

        /// <summary>
        /// 在一个 target shadow 上执行单条 application，并只更新 PreparedTargetState 与 scratch outcome。
        /// </summary>
        private bool ApplyApplication(
            ref PreparedTargetState state,
            in GasSourceSpecRecord spec,
            ref GasTickExecutionState execution,
            ref GasDefinitionCatalogBlob catalog)
        {
            if (execution.ApplicationOutcomeCount >= ApplicationOutcomes.Length ||
                spec.DefinitionIndex < 0 || spec.DefinitionIndex >= catalog.GameplayEffects.Length)
                return false;
            var outcome = default(GasGameplayEffectApplicationResult);
            var targetWasAlive = state.Lifecycle.State == GasAscLifecycleState.Ready ||
                                 state.Lifecycle.State == GasAscLifecycleState.Alive;
            var definition = catalog.GameplayEffects[spec.DefinitionIndex];
            if (!ValidateTargetContext(state.Target, in spec, in definition, ref catalog))
            {
                StoreStaleTargetOutcome(ref execution, in spec);
                return execution.PostAdmissionFailure == GasTickAdmissionFailureReason.None;
            }
            if (!TryPrepareSpecInputs(in spec, ref state, ref catalog,
                    out var captures, out var valueViews))
            {
                outcome.Outcome = GasGameplayEffectApplicationOutcome.RejectedDefinition;
                outcome.Failure = GasGameplayEffectTransactionFailure.InvalidDefinition;
                outcome.ApplicationId = spec.ApplicationId;
                outcome.CausalityId = spec.CausalityId;
                StoreOutcome(ref execution, in spec, in outcome);
                return execution.PostAdmissionFailure == GasTickAdmissionFailureReason.None;
            }
            var oldCaptureRange = FindExistingCaptureRange(ref state, in spec, in definition);
            if (!TryAllocateCaptureRange(ref state, in spec, in definition, in captures,
                    out var preparedCapture))
            {
                outcome.Outcome = GasGameplayEffectApplicationOutcome.InfrastructureFault;
                outcome.Failure = GasGameplayEffectTransactionFailure.PayloadCapacity;
                outcome.ApplicationId = spec.ApplicationId;
                outcome.CausalityId = spec.CausalityId;
                StoreOutcome(ref execution, in spec, in outcome);
                return false;
            }
            var mutationStart = execution.AttributeMutationCount;
            var request = new GasGameplayEffectApplicationRequest
            {
                SimulationEpoch = SimulationEpoch,
                SourceAsc = spec.SourceAsc,
                TargetAsc = spec.TargetAsc,
                DefinitionIndex = spec.DefinitionIndex,
                ApplicationId = spec.ApplicationId,
                StartTick = spec.StartTick,
                CausalityId = spec.CausalityId,
                TargetIsAlive = targetWasAlive ? (byte)1 : (byte)0,
                CaptureValueCount = captures.IsCreated ? captures.Length : 0,
                ValueViewCount = valueViews.IsCreated ? valueViews.Length : 0,
                TargetAvatarStableId = spec.TargetAvatarStableId,
                TargetAvatarBindingGeneration = spec.TargetAvatarBindingGeneration,
                SpatialSnapshot = spec.SpatialSnapshot,
            };
            var applied = GasGameplayEffectTransaction.TryApply(
                ref catalog,
                in request,
                state.ActiveEffects,
                state.Attributes,
                state.AttributeDirtyWords,
                state.TagCounts,
                state.TagPresenceWords,
                ref state.SlabHeads.ActiveEffect,
                Profile.MaxActiveEffectCount,
                captures,
                valueViews,
                EvaluatorStack,
                AttributeMutations,
                mutationStart,
                out outcome);
            if (applied && outcome.MutationCount > 0)
            {
                if (!CopyMutationOutcomes(
                        ref execution, in spec, mutationStart, outcome.MutationCount,
                        targetWasAlive, spec.CausalityId, out var deathTransitionId,
                        out var deathOverkill, out var deathAttributeLayoutIndex,
                        out var deathContributorId))
                    return false;
                if (deathTransitionId != 0 && targetWasAlive)
                {
                    state.Lifecycle.State = GasAscLifecycleState.Dead;
                    state.Lifecycle.DeathTick = execution.CandidateTick;
                    state.Lifecycle.DeathTransitionId = deathTransitionId;
                    state.Lifecycle.DeathApplicationId = spec.ApplicationId;
                    state.Lifecycle.DeathSourceAsc = spec.SourceAsc;
                    state.Lifecycle.DeathOverkill = deathOverkill;
                    outcome.DeathCrossed = 1;
                    outcome.DeathTransitionId = deathTransitionId;
                    outcome.DeathOverkill = deathOverkill;
                    outcome.DeathAttributeLayoutIndex = deathAttributeLayoutIndex;
                    outcome.DeathContributorId = deathContributorId;
                }
            }
            if (!applied)
                RecyclePreparedCapture(ref state, in preparedCapture);
            else if (!CommitCaptureRange(ref state, in outcome, in preparedCapture, in oldCaptureRange))
                return false;
            StoreOutcome(ref execution, in spec, in outcome);
            return execution.PostAdmissionFailure == GasTickAdmissionFailureReason.None &&
                   (applied || outcome.Outcome != GasGameplayEffectApplicationOutcome.InfrastructureFault);
        }

        /// <summary>
        /// 按稳定 owner 在已准备的 registry-indexed shadow 表中查找唯一条目。
        /// </summary>
        private bool TryFindShadowIndex(in OwnerAscHandle owner, out int shadowIndex)
        {
            for (var index = 0; index < TargetShadows.Length; index++)
            {
                var shadow = TargetShadows[index];
                if (shadow.Prepared != 0 && shadow.OwnerAsc.Equals(owner))
                {
                    shadowIndex = index;
                    return true;
                }
            }
            shadowIndex = -1;
            return false;
        }

        /// <summary>
        /// 从 durable authority 创建临时 DynamicBuffer 副本，TargetPrepare 只允许写这些副本。
        /// </summary>
        private bool TryCreatePreparedStateFromDurable(
            int shadowIndex,
            Entity target,
            in OwnerAscHandle owner,
            out PreparedTargetState state)
        {
            state = default;
            if (shadowIndex < 0 || shadowIndex >= TargetShadows.Length ||
                !AscLifecycles.HasComponent(target) || !SlabHeads.HasComponent(target) ||
                !PayloadStates.HasComponent(target) || !ActiveEffects.HasBuffer(target) ||
                !Attributes.HasBuffer(target) || !AttributeDirtyWords.HasBuffer(target) ||
                !TagCounts.HasBuffer(target) || !TagPresenceWords.HasBuffer(target) ||
                !PayloadRanges.HasBuffer(target) || !PayloadValues.HasBuffer(target) ||
                ActiveEffects[target].Length > ActiveEffectStride ||
                Attributes[target].Length > AttributeStride ||
                AttributeDirtyWords[target].Length > AttributeDirtyWordStride ||
                TagCounts[target].Length > TagStride ||
                TagPresenceWords[target].Length > TagPresenceWordStride ||
                PayloadRanges[target].Length > PayloadRangeStride ||
                PayloadValues[target].Length > PayloadValueStride)
                return false;
            state = new PreparedTargetState
            {
                ShadowIndex = shadowIndex,
                Target = target,
                OwnerAsc = owner,
                Lifecycle = AscLifecycles[target],
                SlabHeads = SlabHeads[target],
                PayloadState = PayloadStates[target],
                ActiveEffects = CreateTemporaryBuffer(target, ActiveEffects[target], ActiveEffectStride),
                Attributes = CreateTemporaryBuffer(target, Attributes[target], AttributeStride),
                AttributeDirtyWords = CreateTemporaryBuffer(
                    target, AttributeDirtyWords[target], AttributeDirtyWordStride),
                TagCounts = CreateTemporaryBuffer(target, TagCounts[target], TagStride),
                TagPresenceWords = CreateTemporaryBuffer(
                    target, TagPresenceWords[target], TagPresenceWordStride),
                PayloadRanges = CreateTemporaryBuffer(target, PayloadRanges[target], PayloadRangeStride),
                PayloadValues = CreateTemporaryBuffer(target, PayloadValues[target], PayloadValueStride),
            };
            return true;
        }

        /// <summary>
        /// 从前一 prepare 子阶段保存的定长 slice 重建临时 DynamicBuffer，维持 period 到 application 的读己之写。
        /// </summary>
        private bool TryCreatePreparedStateFromShadow(
            int shadowIndex,
            out PreparedTargetState state)
        {
            state = default;
            if (shadowIndex < 0 || shadowIndex >= TargetShadows.Length)
                return false;
            var shadow = TargetShadows[shadowIndex];
            if (shadow.Prepared == 0 ||
                !HasShadowRange(shadowIndex, ActiveEffectStride, shadow.ActiveEffectCount,
                    ShadowActiveEffects.Length) ||
                !HasShadowRange(shadowIndex, AttributeStride, shadow.AttributeCount,
                    ShadowAttributes.Length) ||
                !HasShadowRange(shadowIndex, AttributeDirtyWordStride, shadow.AttributeDirtyWordCount,
                    ShadowAttributeDirtyWords.Length) ||
                !HasShadowRange(shadowIndex, TagStride, shadow.TagCount, ShadowTagCounts.Length) ||
                !HasShadowRange(shadowIndex, TagPresenceWordStride, shadow.TagPresenceWordCount,
                    ShadowTagPresenceWords.Length) ||
                !HasShadowRange(shadowIndex, PayloadRangeStride, shadow.PayloadRangeCount,
                    ShadowPayloadRanges.Length) ||
                !HasShadowRange(shadowIndex, PayloadValueStride, shadow.PayloadValueCount,
                    ShadowPayloadValues.Length))
                return false;
            state = new PreparedTargetState
            {
                ShadowIndex = shadowIndex,
                Target = shadow.Target,
                OwnerAsc = shadow.OwnerAsc,
                Lifecycle = shadow.Lifecycle,
                SlabHeads = shadow.SlabHeads,
                PayloadState = shadow.PayloadState,
                ActiveEffects = CreateTemporaryBuffer(
                    shadow.Target, ShadowActiveEffects, shadowIndex * ActiveEffectStride,
                    shadow.ActiveEffectCount, ActiveEffectStride),
                Attributes = CreateTemporaryBuffer(
                    shadow.Target, ShadowAttributes, shadowIndex * AttributeStride,
                    shadow.AttributeCount, AttributeStride),
                AttributeDirtyWords = CreateTemporaryBuffer(
                    shadow.Target, ShadowAttributeDirtyWords,
                    shadowIndex * AttributeDirtyWordStride, shadow.AttributeDirtyWordCount,
                    AttributeDirtyWordStride),
                TagCounts = CreateTemporaryBuffer(
                    shadow.Target, ShadowTagCounts, shadowIndex * TagStride,
                    shadow.TagCount, TagStride),
                TagPresenceWords = CreateTemporaryBuffer(
                    shadow.Target, ShadowTagPresenceWords,
                    shadowIndex * TagPresenceWordStride, shadow.TagPresenceWordCount,
                    TagPresenceWordStride),
                PayloadRanges = CreateTemporaryBuffer(
                    shadow.Target, ShadowPayloadRanges, shadowIndex * PayloadRangeStride,
                    shadow.PayloadRangeCount, PayloadRangeStride),
                PayloadValues = CreateTemporaryBuffer(
                    shadow.Target, ShadowPayloadValues, shadowIndex * PayloadValueStride,
                    shadow.PayloadValueCount, PayloadValueStride),
            };
            return true;
        }

        /// <summary>
        /// 将一个 target 的临时 transaction 结果复制回定长 scratch，不触碰任何 ECS authority。
        /// </summary>
        private bool TryStorePreparedState(ref PreparedTargetState state)
        {
            if (!TryCopyToShadow(state.ActiveEffects, state.ShadowIndex, ActiveEffectStride,
                    ShadowActiveEffects) ||
                !TryCopyToShadow(state.Attributes, state.ShadowIndex, AttributeStride,
                    ShadowAttributes) ||
                !TryCopyToShadow(state.AttributeDirtyWords, state.ShadowIndex,
                    AttributeDirtyWordStride, ShadowAttributeDirtyWords) ||
                !TryCopyToShadow(state.TagCounts, state.ShadowIndex, TagStride,
                    ShadowTagCounts) ||
                !TryCopyToShadow(state.TagPresenceWords, state.ShadowIndex,
                    TagPresenceWordStride, ShadowTagPresenceWords) ||
                !TryCopyToShadow(state.PayloadRanges, state.ShadowIndex, PayloadRangeStride,
                    ShadowPayloadRanges) ||
                !TryCopyToShadow(state.PayloadValues, state.ShadowIndex, PayloadValueStride,
                    ShadowPayloadValues))
                return false;
            TargetShadows[state.ShadowIndex] = new GasTargetShadowState
            {
                Target = state.Target,
                OwnerAsc = state.OwnerAsc,
                Lifecycle = state.Lifecycle,
                SlabHeads = state.SlabHeads,
                PayloadState = state.PayloadState,
                ActiveEffectCount = state.ActiveEffects.Length,
                AttributeCount = state.Attributes.Length,
                AttributeDirtyWordCount = state.AttributeDirtyWords.Length,
                TagCount = state.TagCounts.Length,
                TagPresenceWordCount = state.TagPresenceWords.Length,
                PayloadRangeCount = state.PayloadRanges.Length,
                PayloadValueCount = state.PayloadValues.Length,
                Prepared = 1,
            };
            return true;
        }

        /// <summary>
        /// 复制 durable DynamicBuffer 到 ECB-owned 临时 Buffer，并按 profile 上界预留容量。
        /// </summary>
        private DynamicBuffer<T> CreateTemporaryBuffer<T>(
            Entity target,
            DynamicBuffer<T> source,
            int capacity)
            where T : unmanaged, IBufferElementData
        {
            var destination = ShadowCommands.SetBuffer<T>(target);
            destination.EnsureCapacity(capacity);
            for (var index = 0; index < source.Length; index++)
                destination.Add(source[index]);
            return destination;
        }

        /// <summary>
        /// 复制定长 scratch slice 到 ECB-owned 临时 Buffer，并按 profile 上界预留容量。
        /// </summary>
        private DynamicBuffer<T> CreateTemporaryBuffer<T>(
            Entity target,
            NativeArray<T> source,
            int start,
            int count,
            int capacity)
            where T : unmanaged, IBufferElementData
        {
            var destination = ShadowCommands.SetBuffer<T>(target);
            destination.EnsureCapacity(capacity);
            for (var offset = 0; offset < count; offset++)
                destination.Add(source[start + offset]);
            return destination;
        }

        /// <summary>
        /// 将临时 DynamicBuffer 复制到 registry-indexed 定长 slice，并拒绝任何隐式越界扩容。
        /// </summary>
        private static bool TryCopyToShadow<T>(
            DynamicBuffer<T> source,
            int shadowIndex,
            int stride,
            NativeArray<T> destination)
            where T : unmanaged, IBufferElementData
        {
            if (!HasShadowRange(shadowIndex, stride, source.Length, destination.Length))
                return false;
            var start = shadowIndex * stride;
            for (var offset = 0; offset < source.Length; offset++)
                destination[start + offset] = source[offset];
            return true;
        }

        /// <summary>
        /// 以 long 验证一个 registry-indexed slice，避免 stride 乘法回绕。
        /// </summary>
        private static bool HasShadowRange(int index, int stride, int count, int length)
        {
            if (index < 0 || stride < 0 || count < 0 || count > stride)
                return false;
            var start = (long)index * stride;
            return start >= 0 && start <= length && count <= length - start;
        }

        /// <summary>
        /// 在 target application 线性化点填充 Target Snapshot 与 ValueView，并验证 source 输入未被篡改。
        /// </summary>
        private bool TryPrepareSpecInputs(
            in GasSourceSpecRecord spec,
            ref PreparedTargetState state,
            ref GasDefinitionCatalogBlob catalog,
            out NativeArray<float> captures,
            out NativeArray<float> valueViews)
        {
            captures = default;
            valueViews = default;
            if (spec.DefinitionIndex < 0 || spec.DefinitionIndex >= catalog.GameplayEffects.Length ||
                spec.CaptureCount < 0 || spec.ValueViewCount < 0 ||
                spec.CaptureCount > CaptureStride || spec.ValueViewCount > ValueViewStride ||
                spec.CaptureStart < 0 || spec.ValueViewStart < 0 ||
                spec.CaptureStart > CaptureValues.Length - spec.CaptureCount ||
                spec.ValueViewStart > ValueViews.Length - spec.ValueViewCount)
                return false;
            var definition = catalog.GameplayEffects[spec.DefinitionIndex];
            if (spec.CaptureCount != definition.CaptureRange.Count ||
                spec.ValueViewCount != definition.ValueViewRange.Count ||
                !IsRangeValid(in definition.CaptureRange, catalog.CaptureDescriptors.Length) ||
                !IsRangeValid(in definition.ValueViewRange, catalog.ValueViews.Length))
                return false;
            if (spec.CaptureCount > 0)
                captures = CaptureValues.GetSubArray(spec.CaptureStart, spec.CaptureCount);
            if (spec.ValueViewCount > 0)
                valueViews = ValueViews.GetSubArray(spec.ValueViewStart, spec.ValueViewCount);

            for (var offset = 0; offset < spec.CaptureCount; offset++)
            {
                var descriptor = catalog.CaptureDescriptors[definition.CaptureRange.Start + offset];
                if (descriptor.Owner != GasCaptureOwner.Target)
                    continue;
                if (descriptor.Binding != GasCaptureBinding.Snapshot ||
                    descriptor.Phase != GasCapturePhase.TargetApplication ||
                    !TryReadAttributeValue(
                        state.Attributes,
                        descriptor.AttributeLayoutIndex,
                        descriptor.ValueView,
                        ref catalog,
                        out var value))
                    return false;
                captures[offset] = value;
            }
            for (var offset = 0; offset < spec.ValueViewCount; offset++)
            {
                var descriptor = catalog.ValueViews[definition.ValueViewRange.Start + offset];
                if (!TryReadAttributeValue(
                        state.Attributes,
                        descriptor.AttributeLayoutIndex,
                        descriptor.ValueView,
                        ref catalog,
                        out var value))
                    return false;
                valueViews[offset] = value;
            }
            return true;
        }

        /// <summary>
        /// 从目标 ASC 的唯一 Attribute buffer 读取声明式 Base/Current/DefinitionMaxValue 视图。
        /// </summary>
        private bool TryReadAttributeValue(
            DynamicBuffer<AttributeValueSlot> attributes,
            int attributeIndex,
            GasAttributeValueView view,
            ref GasDefinitionCatalogBlob catalog,
            out float value)
        {
            value = 0f;
            if (attributeIndex < 0 || attributeIndex >= attributes.Length ||
                attributeIndex >= catalog.AttributeLayout.Entries.Length)
                return false;
            var attribute = attributes[attributeIndex];
            switch (view)
            {
                case GasAttributeValueView.Base:
                    value = attribute.Base;
                    break;
                case GasAttributeValueView.Current:
                case GasAttributeValueView.Final:
                    value = attribute.Current;
                    break;
                case GasAttributeValueView.DefinitionMaxValue:
                    value = catalog.AttributeLayout.Entries[attributeIndex].MaximumValue;
                    break;
                default:
                    return false;
            }
            return IsFinite(value);
        }

        /// <summary>
        /// 使用减法验证 Catalog range，防止损坏 Definition 造成整数回绕。
        /// </summary>
        private static bool IsRangeValid(in GasCatalogRange range, int length)
        {
            return range.Start >= 0 && range.Count >= 0 && range.Start <= length &&
                   range.Count <= length - range.Start;
        }

        /// <summary>
        /// 拒绝非有限 capture/value 输入污染 Attribute authority。
        /// </summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// 按 registry owner 与稳定 slot index 执行全部 due period body，再一次提交 lifecycle claim/expiry。
        /// </summary>
        private bool ProcessPeriodEffects(
            ref GasTickExecutionState execution,
            ref GasDefinitionCatalogBlob catalog)
        {
            var registry = FindRegistry();
            for (var registryIndex = 0; registryIndex < registry.Length; registryIndex++)
            {
                var owner = registry[registryIndex];
                if (owner.State != GasAscRegistryState.Ready)
                    continue;
                if (!TryResolveOwner(owner.OwnerAsc, out var target) ||
                    !TryCreatePreparedStateFromDurable(
                        registryIndex, target, in owner.OwnerAsc, out var state))
                    return false;
                if (!ProcessPeriodEffectsForTarget(
                        ref state,
                        ref execution,
                        ref catalog) ||
                    !TryStorePreparedState(ref state))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 在单 target writer 内执行 period modifiers、记录 provenance，并推进 ActiveEffect due/expiry 状态。
        /// </summary>
        private bool ProcessPeriodEffectsForTarget(
            ref PreparedTargetState state,
            ref GasTickExecutionState execution,
            ref GasDefinitionCatalogBlob catalog)
        {
            var activeEffects = state.ActiveEffects;
            for (var slotIndex = 0; slotIndex < activeEffects.Length; slotIndex++)
            {
                var slot = activeEffects[slotIndex];
                if (slot.Header.StorageState != GasSlabSlotState.Live ||
                    slot.State != GasSlotBusinessState.Active)
                    continue;
                if (!TryExecutePeriodSlot(
                        ref state,
                        slotIndex,
                        in slot,
                        ref execution,
                        ref catalog))
                    return false;
            }

            if (!GasGameplayEffectLifecycleUtility.TryProcessDue(
                    ref catalog,
                    SimulationEpoch,
                    execution.CandidateTick,
                    activeEffects,
                    state.TagCounts,
                    ref state.SlabHeads.ActiveEffect,
                    Profile.MaxActiveEffectCount,
                    out _))
                return false;
            if (!RecycleExpiredCaptureRanges(ref state))
                return false;
            var recycled = 0;
            if (!GasGameplayEffectLifecycleUtility.TryRecycleTombstones(
                    activeEffects,
                    ref state.SlabHeads.ActiveEffect,
                    Profile.MaxActiveEffectCount,
                    out recycled,
                    out _))
                return false;
            return true;
        }

        /// <summary>
        /// 在 ActiveEffect tombstone 交接前回收其 target-owned capture range，避免持久 payload slab 泄漏。
        /// </summary>
        private bool RecycleExpiredCaptureRanges(
            ref PreparedTargetState state)
        {
            var activeEffects = state.ActiveEffects;
            var hasCaptureTombstone = false;
            for (var index = 0; index < activeEffects.Length; index++)
            {
                var slot = activeEffects[index];
                if (slot.Header.StorageState != GasSlabSlotState.Tombstone)
                    continue;
                if (slot.CaptureRange == default(PayloadRangeHandle))
                    continue;
                hasCaptureTombstone = true;
                if (!slot.CaptureRange.IsValid)
                    return false;
            }
            if (!hasCaptureTombstone)
                return true;
            var storage = new GasPayloadRangeDynamicBufferStorage(state.PayloadRanges);
            for (var index = 0; index < activeEffects.Length; index++)
            {
                var slot = activeEffects[index];
                if (slot.Header.StorageState != GasSlabSlotState.Tombstone ||
                    !slot.CaptureRange.IsValid)
                    continue;
                if (!RecycleRange(
                        ref state.PayloadState,
                        ref storage,
                        in slot.CaptureRange,
                        PayloadKind.Capture))
                    return false;
                slot.CaptureRange = default;
                activeEffects[index] = slot;
            }
            return true;
        }

        /// <summary>
        /// 在 target writer 再次校验冻结 Avatar/Spatial context，防止中间 lane 隐式丢弃边界字段。
        /// </summary>
        private bool ValidateTargetContext(
            Entity target,
            in GasSourceSpecRecord spec,
            in GasGameplayEffectDefinitionBlob definition,
            ref GasDefinitionCatalogBlob catalog)
        {
            var policy = definition.TargetPolicy;
            var hasAvatar = spec.TargetAvatarStableId != 0 ||
                            spec.TargetAvatarBindingGeneration != 0;
            if (policy.Avatar == GasAvatarTargetPolicy.RequireSameAvatar)
            {
                if (!hasAvatar || spec.TargetAvatarStableId == 0 ||
                    spec.TargetAvatarBindingGeneration == 0 ||
                    !ActorBindings.HasComponent(target))
                    return false;
                var binding = ActorBindings[target];
                if (binding.AvatarActorStableId != spec.TargetAvatarStableId ||
                    binding.BindingGeneration != spec.TargetAvatarBindingGeneration)
                    return false;
            }
            else if (policy.Avatar == GasAvatarTargetPolicy.FollowAsc)
            {
                if (hasAvatar)
                    return false;
            }
            else
                return false;

            var hasSpatial = !spec.SpatialSnapshot.Equals(GasBoundarySpatialSnapshot.None);
            if (policy.Spatial == GasSpatialTargetPolicy.None)
                return !hasSpatial;
            if (policy.Spatial == GasSpatialTargetPolicy.FrozenSpatial)
                return spec.SpatialSnapshot.IsValid &&
                       HasRequiredSpatialVariant(
                           ref catalog,
                           definition.TargetDataRange,
                           spec.SpatialSnapshot.Variant);
            return false;
        }

        /// <summary>
        /// 验证 target writer 使用的 FrozenSpatial variant 与 Definition 唯一 Required descriptor 一致。
        /// </summary>
        private static bool HasRequiredSpatialVariant(
            ref GasDefinitionCatalogBlob catalog,
            GasCatalogRange range,
            GasTargetDataVariant variant)
        {
            var count = 0;
            for (var offset = 0; offset < range.Count; offset++)
            {
                var descriptor = catalog.TargetDataDescriptors[range.Start + offset];
                if (descriptor.Variant < GasTargetDataVariant.FrozenSpatialPoint ||
                    descriptor.Variant > GasTargetDataVariant.FrozenSpatialShape)
                    continue;
                count++;
                if (descriptor.Variant != variant || descriptor.Required == 0)
                    return false;
            }
            return count == 1;
        }

        /// <summary>
        /// 执行一个已通过 admission 的 period claim；skip policy 仍产生唯一 PeriodTick outcome。
        /// </summary>
        private bool TryExecutePeriodSlot(
            ref PreparedTargetState state,
            int slotIndex,
            in ActiveEffectSlot slot,
            ref GasTickExecutionState execution,
            ref GasDefinitionCatalogBlob catalog)
        {
            if (slot.DefinitionIndex < 0 || slot.DefinitionIndex >= catalog.GameplayEffects.Length)
                return false;
            var definition = catalog.GameplayEffects[slot.DefinitionIndex];
            if (!GasGameplayEffectLifecycleUtility.TryGetPeriodDecision(
                    ref catalog,
                    in definition,
                    in slot,
                    state.TagCounts,
                    execution.CandidateTick,
                    out var claim,
                    out var execute,
                    out var periodOrdinal,
                    out _))
                return false;
            if (claim == 0)
                return true;
            if (slot.ApplicationId == 0 || execution.ApplicationOutcomeCount >= ApplicationOutcomes.Length)
                return false;

            var causalityId = ComposePeriodCausalityId(
                in slot.Handle,
                slot.ApplicationId,
                periodOrdinal,
                execution.CandidateTick);
            var outcome = CreateSkippedPeriodOutcome(
                in slot,
                causalityId,
                periodOrdinal,
                execution.AttributeMutationCount);
            if (execute != 0 && !ApplyPeriodBody(
                    ref state,
                    in slot,
                    periodOrdinal,
                    causalityId,
                    ref execution,
                    ref catalog,
                    ref outcome))
                return false;
            StorePeriodOutcome(
                ref execution,
                slotIndex,
                in slot,
                in outcome,
                periodOrdinal);
            return execution.PostAdmissionFailure == GasTickAdmissionFailureReason.None;
        }

        /// <summary>
        /// 调用 target transaction 应用 period modifiers，并把 Health crossing 提升为 ASC death provenance。
        /// </summary>
        private bool ApplyPeriodBody(
            ref PreparedTargetState state,
            in ActiveEffectSlot slot,
            uint periodOrdinal,
            ulong causalityId,
            ref GasTickExecutionState execution,
            ref GasDefinitionCatalogBlob catalog,
            ref GasGameplayEffectApplicationResult outcome)
        {
            var targetWasAlive = state.Lifecycle.State == GasAscLifecycleState.Ready ||
                                 state.Lifecycle.State == GasAscLifecycleState.Alive;
            if (!TryPreparePeriodInputs(
                    ref state,
                    in slot,
                    ref catalog,
                    out var captures,
                    out var valueViews))
                return false;
            var mutationStart = execution.AttributeMutationCount;
            var request = new GasGameplayEffectApplicationRequest
            {
                SimulationEpoch = SimulationEpoch,
                SourceAsc = slot.SourceAsc,
                TargetAsc = state.OwnerAsc,
                DefinitionIndex = slot.DefinitionIndex,
                ApplicationId = slot.ApplicationId,
                StartTick = execution.CandidateTick,
                CausalityId = causalityId,
                IsPeriodTick = 1,
                PeriodExecutionOrdinal = periodOrdinal,
                TargetIsAlive = targetWasAlive ? (byte)1 : (byte)0,
                CaptureValueCount = captures.IsCreated ? captures.Length : 0,
                ValueViewCount = valueViews.IsCreated ? valueViews.Length : 0,
                TargetAvatarStableId = slot.TargetAvatarStableId,
                TargetAvatarBindingGeneration = slot.TargetAvatarBindingGeneration,
                SpatialSnapshot = slot.SpatialSnapshot,
            };
            if (!GasGameplayEffectTransaction.TryApplyPeriodModifiers(
                    ref catalog,
                    in request,
                    in slot,
                    state.ActiveEffects,
                    state.Attributes,
                    state.AttributeDirtyWords,
                    state.TagCounts,
                    state.TagPresenceWords,
                    Profile.MaxActiveEffectCount,
                    captures,
                    valueViews,
                    EvaluatorStack,
                    AttributeMutations,
                    mutationStart,
                    out outcome))
                return false;
            return CopyPeriodMutationOutcomes(
                ref state,
                in slot,
                periodOrdinal,
                mutationStart,
                targetWasAlive,
                ref execution,
                ref catalog,
                ref outcome);
        }

        /// <summary>
        /// 从 ActiveEffect 持久 Capture range 恢复 source snapshot，并在 period 线性化点采样 target views。
        /// </summary>
        private bool TryPreparePeriodInputs(
            ref PreparedTargetState state,
            in ActiveEffectSlot slot,
            ref GasDefinitionCatalogBlob catalog,
            out NativeArray<float> captures,
            out NativeArray<float> valueViews)
        {
            captures = default;
            valueViews = default;
            if (slot.DefinitionIndex < 0 || slot.DefinitionIndex >= catalog.GameplayEffects.Length)
                return false;
            var definition = catalog.GameplayEffects[slot.DefinitionIndex];
            if (!IsRangeValid(in definition.CaptureRange, catalog.CaptureDescriptors.Length) ||
                !IsRangeValid(in definition.ValueViewRange, catalog.ValueViews.Length) ||
                definition.CaptureRange.Count > PeriodCaptureValues.Length ||
                definition.ValueViewRange.Count > PeriodValueViews.Length)
                return false;
            if (definition.CaptureRange.Count > 0)
            {
                if (!slot.CaptureRange.IsValid)
                    return false;
                var storage = new GasPayloadRangeDynamicBufferStorage(state.PayloadRanges);
                if (GasPayloadRangeAllocator.Validate(
                        in slot.CaptureRange,
                        in state.PayloadState,
                        ref storage,
                        PayloadKind.Capture) != PayloadRangeValidationFailure.None)
                    return false;
                var values = state.PayloadValues;
                if (slot.CaptureRange.Offset > values.Length ||
                    slot.CaptureRange.Length > values.Length - slot.CaptureRange.Offset ||
                    slot.CaptureRange.Length < definition.CaptureRange.Count)
                    return false;
                captures = PeriodCaptureValues.GetSubArray(0, definition.CaptureRange.Count);
                for (var index = 0; index < captures.Length; index++)
                {
                    var bits = values[slot.CaptureRange.Offset + index].ValueBits;
                    captures[index] = math.asfloat((uint)bits);
                    if (!IsFinite(captures[index]))
                        return false;
                }
            }
            if (definition.ValueViewRange.Count > 0)
            {
                valueViews = PeriodValueViews.GetSubArray(0, definition.ValueViewRange.Count);
                for (var index = 0; index < valueViews.Length; index++)
                {
                    var descriptor = catalog.ValueViews[definition.ValueViewRange.Start + index];
                    if (!TryReadAttributeValue(
                            state.Attributes,
                            descriptor.AttributeLayoutIndex,
                            descriptor.ValueView,
                            ref catalog,
                            out var value))
                        return false;
                    valueViews[index] = value;
                }
            }
            return true;
        }

        /// <summary>
        /// 查找本 target 上按冻结 stack key 可合并的旧 capture range。
        /// </summary>
        private PayloadRangeHandle FindExistingCaptureRange(
            ref PreparedTargetState state,
            in GasSourceSpecRecord spec,
            in GasGameplayEffectDefinitionBlob definition)
        {
            if (definition.StackPolicy == GasStackPolicy.None)
                return default;
            var effects = state.ActiveEffects;
            for (var index = 0; index < effects.Length; index++)
            {
                var slot = effects[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live ||
                    slot.State != GasSlotBusinessState.Active ||
                    slot.DefinitionIndex != spec.DefinitionIndex)
                    continue;
                if ((definition.StackKey & GasStackKeyFields.SourceAsc) != 0 &&
                    !slot.SourceAsc.Equals(spec.SourceAsc))
                    continue;
                return slot.CaptureRange;
            }
            return default;
        }

        /// <summary>
        /// 在 target transaction 前分配并写入本次成功 application 的 source/target capture snapshot。
        /// </summary>
        private bool TryAllocateCaptureRange(
            ref PreparedTargetState state,
            in GasSourceSpecRecord spec,
            in GasGameplayEffectDefinitionBlob definition,
            in NativeArray<float> captures,
            out PreparedCaptureRange prepared)
        {
            prepared = default;
            if (definition.CaptureRange.Count == 0 ||
                definition.Lifetime == GasEffectLifetimePolicy.Instant ||
                definition.Lifetime == GasEffectLifetimePolicy.InstantExecution)
                return true;
            if (!captures.IsCreated || captures.Length != definition.CaptureRange.Count)
                return false;
            var storage = new GasPayloadRangeDynamicBufferStorage(state.PayloadRanges);
            var failure = GasPayloadRangeAllocator.TryAllocate(
                ref state.PayloadState,
                ref storage,
                PayloadKind.Capture,
                captures.Length,
                Profile.MaxPayloadRangeRecordCount,
                Profile.MaxPayloadValueCount,
                out var allocation);
            if (failure != GasPayloadRangeStorageFailure.None)
                return false;
            var values = state.PayloadValues;
            var valueHighWater = state.PayloadState.ValueHighWater;
            var expectedPreviousLength = allocation.Reused
                ? valueHighWater
                : allocation.Handle.Offset;
            var appendedRangeEnd = (long)allocation.Handle.Offset + allocation.Handle.Length;
            if (valueHighWater < 0 || valueHighWater > values.Capacity ||
                values.Length != expectedPreviousLength ||
                (!allocation.Reused && appendedRangeEnd != valueHighWater))
            {
                RecycleRange(
                    ref state.PayloadState,
                    ref storage,
                    in allocation.Handle,
                    PayloadKind.Capture);
                return false;
            }
            // 新增 range 只推进已预留 buffer 的逻辑长度，exact-size reuse 保持既有 high-water。
            if (!allocation.Reused)
                values.ResizeUninitialized(valueHighWater);
            if (allocation.Handle.Offset < 0 ||
                allocation.Handle.Offset > values.Length ||
                allocation.Handle.Length > values.Length - allocation.Handle.Offset)
            {
                RecycleRange(
                    ref state.PayloadState,
                    ref storage,
                    in allocation.Handle,
                    PayloadKind.Capture);
                return false;
            }
            for (var index = 0; index < captures.Length; index++)
                values[allocation.Handle.Offset + index] = new GasPayloadValueSlot
                {
                    ValueBits = math.asuint(captures[index]),
                };
            prepared.Handle = allocation.Handle;
            prepared.Allocated = 1;
            return true;
        }

        /// <summary>
        /// 将新 capture range 绑定到成功创建/合并的 ActiveEffect，并回收被替换的旧代际。
        /// </summary>
        private bool CommitCaptureRange(
            ref PreparedTargetState state,
            in GasGameplayEffectApplicationResult outcome,
            in PreparedCaptureRange prepared,
            in PayloadRangeHandle oldRange)
        {
            if (prepared.Allocated == 0)
                return true;
            if (!outcome.ActiveEffect.IsValid)
                return false;
            var effects = state.ActiveEffects;
            for (var index = 0; index < effects.Length; index++)
            {
                var slot = effects[index];
                if (!slot.Handle.Equals(outcome.ActiveEffect))
                    continue;
                slot.CaptureRange = prepared.Handle;
                effects[index] = slot;
                if (oldRange.IsValid && oldRange != prepared.Handle)
                {
                    var storage = new GasPayloadRangeDynamicBufferStorage(state.PayloadRanges);
                    if (!RecycleRange(
                            ref state.PayloadState,
                            ref storage,
                            in oldRange,
                            PayloadKind.Capture))
                        return false;
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 事务拒绝或后置校验失败时回收本 Tick 预分配的 capture range。
        /// </summary>
        private void RecyclePreparedCapture(
            ref PreparedTargetState state,
            in PreparedCaptureRange prepared)
        {
            if (prepared.Allocated == 0)
                return;
            var storage = new GasPayloadRangeDynamicBufferStorage(state.PayloadRanges);
            RecycleRange(
                ref state.PayloadState,
                ref storage,
                in prepared.Handle,
                PayloadKind.Capture);
        }

        /// <summary>
        /// 将 live payload range 转为 tombstone 再进入 free list，保持 generation 单调。
        /// </summary>
        private static bool RecycleRange(
            ref GasPayloadRangeAllocatorState state,
            ref GasPayloadRangeDynamicBufferStorage storage,
            in PayloadRangeHandle handle,
            PayloadKind kind)
        {
            return GasPayloadRangeAllocator.TryMarkTombstone(
                       in state,
                       ref storage,
                       in handle,
                       kind) == GasPayloadRangeStorageFailure.None &&
                   GasPayloadRangeAllocator.TryRecycleTombstone(
                       ref state,
                       ref storage,
                       in handle,
                       kind) == GasPayloadRangeStorageFailure.None;
        }

        /// <summary>
        /// 创建 inhibited skip 或零 modifier period 的成功 outcome，claim 本身仍有可审计事实。
        /// </summary>
        private static GasGameplayEffectApplicationResult CreateSkippedPeriodOutcome(
            in ActiveEffectSlot slot,
            ulong causalityId,
            uint periodOrdinal,
            int mutationStart)
        {
            return new GasGameplayEffectApplicationResult
            {
                Outcome = GasGameplayEffectApplicationOutcome.PeriodTickExecuted,
                ActiveEffect = slot.Handle,
                ApplicationId = slot.ApplicationId,
                MutationStart = mutationStart,
                CausalityId = causalityId,
                IsPeriodTick = 1,
                PeriodExecutionOrdinal = periodOrdinal,
            };
        }

        /// <summary>
        /// 将每条 target application 的成功或拒绝结果写入定长 scratch，禁止结果被静默丢弃。
        /// </summary>
        private void StoreOutcome(
            ref GasTickExecutionState execution,
            in GasSourceSpecRecord spec,
            in GasGameplayEffectApplicationResult outcome)
        {
            if (execution.ApplicationOutcomeCount >= ApplicationOutcomes.Length)
            {
                execution.PostAdmissionFailure =
                    GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                return;
            }
            ApplicationOutcomes[execution.ApplicationOutcomeCount++] = new GasApplicationOutcomeRecord
            {
                OperationOrdinal = spec.OperationOrdinal,
                DefinitionId = ResolveDefinitionId(spec.DefinitionIndex),
                Outcome = outcome.Outcome,
                Failure = outcome.Failure,
                SourceAsc = spec.SourceAsc,
                TargetAsc = spec.TargetAsc,
                ActiveEffect = outcome.ActiveEffect,
                ApplicationId = outcome.ApplicationId,
                AppliedModifierCount = outcome.AppliedModifierCount,
                AttributeMutationStart = outcome.MutationStart,
                AttributeMutationCount = outcome.MutationCount,
                DeathCrossed = outcome.DeathCrossed,
                DeathTransitionId = outcome.DeathTransitionId,
                DeathOverkill = outcome.DeathOverkill,
                DeathAttributeLayoutIndex = outcome.DeathAttributeLayoutIndex,
                DeathContributorId = outcome.DeathContributorId,
                CausalityId = outcome.CausalityId,
                Scope = spec.Scope,
            };
        }

        /// <summary>
        /// 将 period execution outcome 写入同一稳定化输入，并保留 ActiveEffect 与 ordinal 身份。
        /// </summary>
        private void StorePeriodOutcome(
            ref GasTickExecutionState execution,
            int slotIndex,
            in ActiveEffectSlot slot,
            in GasGameplayEffectApplicationResult outcome,
            uint periodOrdinal)
        {
            if (execution.ApplicationOutcomeCount >= ApplicationOutcomes.Length)
            {
                execution.PostAdmissionFailure =
                    GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                return;
            }
            ApplicationOutcomes[execution.ApplicationOutcomeCount++] = new GasApplicationOutcomeRecord
            {
                OperationOrdinal = slotIndex,
                DefinitionId = ResolveDefinitionId(slot.DefinitionIndex),
                Outcome = GasGameplayEffectApplicationOutcome.PeriodTickExecuted,
                Failure = outcome.Failure,
                SourceAsc = slot.SourceAsc,
                TargetAsc = slot.Handle.OwnerAsc,
                ActiveEffect = slot.Handle,
                ApplicationId = slot.ApplicationId,
                AppliedModifierCount = outcome.AppliedModifierCount,
                AttributeMutationStart = outcome.MutationStart,
                AttributeMutationCount = outcome.MutationCount,
                DeathCrossed = outcome.DeathCrossed,
                DeathTransitionId = outcome.DeathTransitionId,
                DeathOverkill = outcome.DeathOverkill,
                DeathAttributeLayoutIndex = outcome.DeathAttributeLayoutIndex,
                DeathContributorId = outcome.DeathContributorId,
                CausalityId = outcome.CausalityId,
                Scope = GasBoundaryFactScope.Asc,
                IsPeriodTick = 1,
                PeriodExecutionOrdinal = periodOrdinal,
            };
        }

        /// <summary>
        /// 复制 period body mutation provenance，并在首次 Health crossing 时提交 ASC death 状态。
        /// </summary>
        private bool CopyPeriodMutationOutcomes(
            ref PreparedTargetState state,
            in ActiveEffectSlot slot,
            uint periodOrdinal,
            int mutationStart,
            bool targetWasAlive,
            ref GasTickExecutionState execution,
            ref GasDefinitionCatalogBlob catalog,
            ref GasGameplayEffectApplicationResult result)
        {
            var mutationCount = result.MutationCount;
            if (mutationStart < 0 || mutationCount < 0 ||
                mutationStart > AttributeMutations.Length ||
                mutationCount > AttributeMutations.Length - mutationStart ||
                execution.AttributeMutationCount != mutationStart ||
                mutationCount > MutationOutcomes.Length - execution.AttributeMutationCount)
                return false;
            for (var offset = 0; offset < mutationCount; offset++)
            {
                var mutation = AttributeMutations[mutationStart + offset];
                var mutationOutcome = new GasAttributeMutationOutcomeRecord
                {
                    OperationOrdinal = slot.Handle.SlotIndex,
                    ModifierOrdinal = offset,
                    DefinitionId = ResolveDefinitionId(slot.DefinitionIndex),
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = slot.Handle.OwnerAsc,
                    ApplicationId = slot.ApplicationId,
                    CausalityId = result.CausalityId,
                    ContributorId = ComposePeriodContributorId(
                        slot.ApplicationId, periodOrdinal, offset),
                    IsPeriodTick = 1,
                    PeriodExecutionOrdinal = periodOrdinal,
                    Mutation = mutation,
                };
                if (IsFirstHealthCrossing(
                        in mutation,
                        targetWasAlive,
                        result.DeathTransitionId))
                    CapturePeriodDeath(
                        ref state,
                        in slot,
                        in mutationOutcome,
                        ref mutationOutcome,
                        ref result,
                        ref catalog);
                MutationOutcomes[execution.AttributeMutationCount++] = mutationOutcome;
            }
            return true;
        }

        /// <summary>
        /// 判断当前 mutation 是否是本 period body 的首个存活到死亡 Health crossing。
        /// </summary>
        private static bool IsFirstHealthCrossing(
            in GasAttributeMutationRecord mutation,
            bool targetWasAlive,
            ulong existingDeathTransitionId)
        {
            return targetWasAlive && existingDeathTransitionId == 0 &&
                   mutation.PreviousCurrent > 0f && mutation.UnclampedCurrent <= 0f;
        }

        /// <summary>
        /// 将 period mutation 的 Health crossing 写入 mutation、outcome 与 ASC lifecycle provenance。
        /// </summary>
        private void CapturePeriodDeath(
            ref PreparedTargetState state,
            in ActiveEffectSlot slot,
            in GasAttributeMutationOutcomeRecord source,
            ref GasAttributeMutationOutcomeRecord mutationOutcome,
            ref GasGameplayEffectApplicationResult result,
            ref GasDefinitionCatalogBlob catalog)
        {
            var mutation = source.Mutation;
            if (mutation.AttributeLayoutIndex < 0 ||
                mutation.AttributeLayoutIndex >= catalog.AttributeLayout.Entries.Length ||
                catalog.AttributeLayout.Entries[mutation.AttributeLayoutIndex].DomainRole !=
                GasAttributeDomainRole.Health)
                return;
            var transitionId = ComposeDeathTransitionId(
                in slot.Handle.OwnerAsc,
                slot.ApplicationId,
                result.CausalityId);
            var overkill = mutation.UnclampedCurrent < 0f ? -mutation.UnclampedCurrent : 0f;
            mutationOutcome.DeathTransitionId = transitionId;
            mutationOutcome.DeathOverkill = overkill;
            mutationOutcome.DeathCrossed = 1;
            result.DeathCrossed = 1;
            result.DeathTransitionId = transitionId;
            result.DeathOverkill = overkill;
            result.DeathAttributeLayoutIndex = mutation.AttributeLayoutIndex;
            result.DeathContributorId = source.ContributorId;
            state.Lifecycle.State = GasAscLifecycleState.Dead;
            state.Lifecycle.DeathTick = Execution[0].CandidateTick;
            state.Lifecycle.DeathTransitionId = transitionId;
            state.Lifecycle.DeathApplicationId = slot.ApplicationId;
            state.Lifecycle.DeathSourceAsc = slot.SourceAsc;
            state.Lifecycle.DeathOverkill = overkill;
        }

        /// <summary>
        /// 复制本次 application 的 mutation provenance，并锁存第一个 Health death crossing。
        /// </summary>
        private bool CopyMutationOutcomes(
            ref GasTickExecutionState execution,
            in GasSourceSpecRecord spec,
            int mutationStart,
            int mutationCount,
            bool targetWasAlive,
            ulong causalityId,
            out ulong deathTransitionId,
            out float deathOverkill,
            out int deathAttributeLayoutIndex,
            out ulong deathContributorId)
        {
            deathTransitionId = 0;
            deathOverkill = 0f;
            deathAttributeLayoutIndex = -1;
            deathContributorId = 0;
            if (mutationStart < 0 || mutationCount < 0 ||
                mutationStart > AttributeMutations.Length ||
                mutationCount > AttributeMutations.Length - mutationStart ||
                execution.AttributeMutationCount != mutationStart ||
                mutationCount > MutationOutcomes.Length - execution.AttributeMutationCount)
                return false;

            ref var catalog = ref Catalog.Value;
            for (var offset = 0; offset < mutationCount; offset++)
            {
                var mutation = AttributeMutations[mutationStart + offset];
                var outcome = new GasAttributeMutationOutcomeRecord
                {
                    OperationOrdinal = spec.OperationOrdinal,
                    ModifierOrdinal = offset,
                    DefinitionId = ResolveDefinitionId(spec.DefinitionIndex),
                    SourceAsc = spec.SourceAsc,
                    TargetAsc = spec.TargetAsc,
                    ApplicationId = spec.ApplicationId,
                    CausalityId = causalityId,
                    ContributorId = ComposeContributorId(spec.ApplicationId, offset),
                    Mutation = mutation,
                };
                if (mutation.AttributeLayoutIndex >= 0 &&
                    mutation.AttributeLayoutIndex < catalog.AttributeLayout.Entries.Length &&
                    catalog.AttributeLayout.Entries[mutation.AttributeLayoutIndex].DomainRole ==
                    GasAttributeDomainRole.Health &&
                    targetWasAlive &&
                    mutation.PreviousCurrent > 0f && mutation.UnclampedCurrent <= 0f &&
                    deathTransitionId == 0)
                {
                    deathTransitionId = ComposeDeathTransitionId(
                        in spec.TargetAsc,
                        spec.ApplicationId,
                        causalityId);
                    deathOverkill = mutation.UnclampedCurrent < 0f
                        ? -mutation.UnclampedCurrent
                        : 0f;
                    deathAttributeLayoutIndex = mutation.AttributeLayoutIndex;
                    deathContributorId = outcome.ContributorId;
                    outcome.DeathTransitionId = deathTransitionId;
                    outcome.DeathOverkill = deathOverkill;
                    outcome.DeathCrossed = 1;
                }
                MutationOutcomes[execution.AttributeMutationCount++] = outcome;
            }
            return true;
        }

        /// <summary>
        /// 从已冻结 definition index 读取稳定 DefinitionId；非法索引返回零并由上游错误事实保留。
        /// </summary>
        private int ResolveDefinitionId(int definitionIndex)
        {
            return definitionIndex >= 0 && definitionIndex < Catalog.Value.GameplayEffects.Length
                ? Catalog.Value.GameplayEffects[definitionIndex].DefinitionId
                : 0;
        }

        /// <summary>
        /// 为 modifier 生成稳定 contributor 身份，避免把 modifier ordinal 混入外部表现层推导。
        /// </summary>
        private static ulong ComposeContributorId(ulong applicationId, int modifierOrdinal)
        {
            var value = applicationId ^ (0x9E3779B97F4A7C15UL * (ulong)(modifierOrdinal + 1));
            return value == 0 ? 1UL : value;
        }

        /// <summary>
        /// 由 ActiveEffect handle、application、period ordinal 与 tick 构造唯一 period causality。
        /// </summary>
        private static ulong ComposePeriodCausalityId(
            in ActiveEffectHandle handle,
            ulong applicationId,
            uint periodOrdinal,
            ulong candidateTick)
        {
            var value = handle.SimulationEpoch ^
                        handle.OwnerAsc.AscStableId ^
                        ((ulong)handle.OwnerAsc.AscGeneration << 32) ^
                        ((ulong)(uint)handle.SlotIndex << 16) ^
                        handle.SlotGeneration ^
                        applicationId ^
                        ((ulong)periodOrdinal * 0x9E3779B97F4A7C15UL) ^
                        (candidateTick * 0xD6E8FEB86659FD93UL);
            return value == 0 ? 1UL : value;
        }

        /// <summary>
        /// 由 application 与 period ordinal 构造 modifier contributor 身份，避免跨周期重复。
        /// </summary>
        private static ulong ComposePeriodContributorId(
            ulong applicationId,
            uint periodOrdinal,
            int modifierOrdinal)
        {
            var value = applicationId ^
                        ((ulong)periodOrdinal * 0x9E3779B97F4A7C15UL) ^
                        ((ulong)(modifierOrdinal + 1) * 0xD6E8FEB86659FD93UL);
            return value == 0 ? 1UL : value;
        }

        /// <summary>
        /// 由 target、causality 与 application 生成独立 DeathTransition 身份。
        /// </summary>
        private static ulong ComposeDeathTransitionId(
            in OwnerAscHandle target,
            ulong applicationId,
            ulong causalityId)
        {
            var value = target.AscStableId ^
                        ((ulong)target.AscGeneration << 32) ^
                        applicationId ^
                        (causalityId * 0xD6E8FEB86659FD93UL);
            return value == 0 ? 1UL : value;
        }

        /// <summary>
        /// 通过 Ready registry 解析 target owner，并复验 identity、lifecycle 与必要 buffer。
        /// </summary>
        private bool TryResolveOwner(in OwnerAscHandle owner, out Entity asc)
        {
            var registry = FindRegistry();
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State != GasAscRegistryState.Ready || !slot.OwnerAsc.Equals(owner))
                    continue;
                asc = slot.ResolveRuntimeEntity();
                if (!AscIdentities.HasComponent(asc) || !AscLifecycles.HasComponent(asc) ||
                    !ActiveEffects.HasBuffer(asc) || !Attributes.HasBuffer(asc) ||
                    !AttributeDirtyWords.HasBuffer(asc) || !TagCounts.HasBuffer(asc) ||
                    !TagPresenceWords.HasBuffer(asc) || !SlabHeads.HasComponent(asc))
                    return false;
                var identity = AscIdentities[asc];
                var lifecycle = AscLifecycles[asc].State;
                return identity.SimulationEpoch == SimulationEpoch && identity.OwnerAsc.Equals(owner) &&
                        (lifecycle == GasAscLifecycleState.Ready ||
                         lifecycle == GasAscLifecycleState.Alive ||
                         lifecycle == GasAscLifecycleState.Terminal ||
                         lifecycle == GasAscLifecycleState.Dead);
            }
            asc = Entity.Null;
            return false;
        }

        /// <summary>
        /// 读取唯一 registry；Job 由 Schedule 注入 Session Entity，避免静态 World 状态。
        /// </summary>
        private DynamicBuffer<AscRegistrySlot> FindRegistry()
        {
            return Registries[Session];
        }

        /// <summary>
        /// 保存本 Job 绑定的 Session Entity。
        /// </summary>
        public Entity Session;
    }

    /// <summary>
    /// 将 sealed command、OwnerPlan 与唯一 application outcome 投影为公开 RequestTerminal。
    /// </summary>
    internal static class GasRequestTerminalProjectionUtility
    {
        /// <summary>
        /// 为 canonical sealed ordinal 创建唯一 typed terminal，任何不闭合组合都返回 invariant failure。
        /// </summary>
        internal static bool TryCreate(
            int ordinal,
            in GasSealedCommand sealedCommand,
            in GasOwnerPlanRecord plan,
            NativeArray<GasApplicationOutcomeRecord> outcomes,
            int outcomeCount,
            out GasRequestTerminalIntent intent)
        {
            intent = default;
            var command = sealedCommand.Command;
            var requestKey = new GasRequestKey(
                command.SimulationEpoch, command.RequestId, command.RequestSequence);
            if (ordinal < 0 || plan.SealedCommandOrdinal != ordinal ||
                plan.CommandKind != command.CommandKind ||
                plan.SourceSequence != command.SourceSequence ||
                command.State != GasBoundaryCommandState.Sealed || !requestKey.IsValid)
                return false;

            GasRequestTerminal terminal;
            if (command.CommandKind == GasBoundaryCommandKind.ApplyEffect)
            {
                if (!TryCreateEffect(
                        in requestKey, in plan, outcomes, outcomeCount, out terminal))
                    return false;
            }
            else if (!TryCreateAbility(in requestKey, in plan, out terminal))
            {
                return false;
            }
            if (!terminal.IsWellFormed())
                return false;
            intent = GasRequestTerminalIntent.Create(in terminal);
            return true;
        }

        /// <summary>
        /// 投影 Activate/Commit 的成功或业务拒绝；只有 Activate 成功返回 continuation handle。
        /// </summary>
        private static bool TryCreateAbility(
            in GasRequestKey requestKey,
            in GasOwnerPlanRecord plan,
            out GasRequestTerminal terminal)
        {
            terminal = default;
            if (plan.CommandKind != GasBoundaryCommandKind.Activate &&
                plan.CommandKind != GasBoundaryCommandKind.Commit)
                return false;
            var expectedSuccess = plan.CommandKind == GasBoundaryCommandKind.Activate
                ? GasAbilityCommandResult.Activated
                : GasAbilityCommandResult.Committed;
            if (plan.BusinessAccepted != 0)
            {
                if (plan.Result != expectedSuccess || plan.StableSequence == 0 ||
                    (plan.CommandKind == GasBoundaryCommandKind.Activate &&
                     !plan.Activation.IsValid))
                    return false;
                var activation = plan.CommandKind == GasBoundaryCommandKind.Activate
                    ? plan.Activation
                    : default;
                terminal = GasRequestTerminal.ForAbility(
                    in requestKey, plan.CommandKind, GasRequestTerminalStatus.Succeeded,
                    plan.Result, in activation);
                return true;
            }
            if (plan.StableSequence != 0 || plan.Result == GasAbilityCommandResult.None ||
                plan.Result == GasAbilityCommandResult.Activated ||
                plan.Result == GasAbilityCommandResult.Committed)
                return false;
            terminal = GasRequestTerminal.ForAbility(
                in requestKey, plan.CommandKind, GasRequestTerminalStatus.Rejected,
                plan.Result, default);
            return true;
        }

        /// <summary>
        /// 投影 direct ApplyEffect；source/definition 前置拒绝不伪造 ApplicationId。
        /// </summary>
        private static bool TryCreateEffect(
            in GasRequestKey requestKey,
            in GasOwnerPlanRecord plan,
            NativeArray<GasApplicationOutcomeRecord> outcomes,
            int outcomeCount,
            out GasRequestTerminal terminal)
        {
            terminal = default;
            if (plan.CommandKind != GasBoundaryCommandKind.ApplyEffect)
                return false;
            if (plan.BusinessAccepted == 0)
            {
                var rejection = plan.Result == GasAbilityCommandResult.OwnerNotReady
                    ? GasEffectRequestResult.RejectedSourceUnavailable
                    : plan.Result == GasAbilityCommandResult.DefinitionInvalid
                        ? GasEffectRequestResult.RejectedDefinition
                        : GasEffectRequestResult.None;
                if (rejection == GasEffectRequestResult.None || plan.StableSequence != 0)
                    return false;
                terminal = GasRequestTerminal.ForEffect(
                    in requestKey, GasRequestTerminalStatus.Rejected, rejection, 0, default);
                return true;
            }
            if (plan.Result != GasAbilityCommandResult.None || plan.StableSequence == 0 ||
                !TryComposeApplicationId(plan.StableSequence, out var applicationId) ||
                !TryFindApplicationOutcome(
                    plan.StableSequence, applicationId, outcomes, outcomeCount, out var outcome) ||
                !TryMapEffectOutcome(
                    in outcome, out var status, out var result, out var activeEffect))
                return false;
            terminal = GasRequestTerminal.ForEffect(
                in requestKey, status, result, applicationId, in activeEffect);
            return true;
        }

        /// <summary>
        /// 以 Commit sequence 与 node-zero ordinal 复算 direct application identity。
        /// </summary>
        private static bool TryComposeApplicationId(ulong stableSequence, out ulong applicationId)
        {
            applicationId = 0;
            if (stableSequence == 0 || stableSequence > (ulong.MaxValue >> 16))
                return false;
            applicationId = stableSequence << 16 | 1UL;
            return applicationId != 0;
        }

        /// <summary>
        /// 查找唯一非 period outcome；重复或缺失都属于 post-admission invariant fault。
        /// </summary>
        private static bool TryFindApplicationOutcome(
            ulong causalityId,
            ulong applicationId,
            NativeArray<GasApplicationOutcomeRecord> outcomes,
            int outcomeCount,
            out GasApplicationOutcomeRecord outcome)
        {
            outcome = default;
            if (outcomeCount < 0 || outcomeCount > outcomes.Length)
                return false;
            var found = false;
            for (var index = 0; index < outcomeCount; index++)
            {
                var candidate = outcomes[index];
                if (candidate.IsPeriodTick != 0 || candidate.CausalityId != causalityId ||
                    candidate.ApplicationId != applicationId)
                    continue;
                if (found)
                    return false;
                outcome = candidate;
                found = true;
            }
            return found;
        }

        /// <summary>
        /// 把 target transaction 闭世界结果映射为公开 effect result，基础设施结果不可降级。
        /// </summary>
        private static bool TryMapEffectOutcome(
            in GasApplicationOutcomeRecord outcome,
            out GasRequestTerminalStatus status,
            out GasEffectRequestResult result,
            out ActiveEffectHandle activeEffect)
        {
            status = GasRequestTerminalStatus.Rejected;
            result = GasEffectRequestResult.None;
            activeEffect = default;
            switch (outcome.Outcome)
            {
                case GasGameplayEffectApplicationOutcome.AppliedInstant:
                    if (outcome.ActiveEffect.IsValid)
                        return false;
                    status = GasRequestTerminalStatus.Succeeded;
                    result = GasEffectRequestResult.AppliedInstant;
                    break;
                case GasGameplayEffectApplicationOutcome.CreatedActive:
                    if (!HasValidActiveEffect(in outcome))
                        return false;
                    status = GasRequestTerminalStatus.Succeeded;
                    result = GasEffectRequestResult.CreatedActive;
                    activeEffect = outcome.ActiveEffect;
                    break;
                case GasGameplayEffectApplicationOutcome.MergedStack:
                    if (!HasValidActiveEffect(in outcome))
                        return false;
                    status = GasRequestTerminalStatus.Succeeded;
                    result = GasEffectRequestResult.MergedStack;
                    activeEffect = outcome.ActiveEffect;
                    break;
                case GasGameplayEffectApplicationOutcome.RejectedRequirement:
                    if (outcome.ActiveEffect.IsValid)
                        return false;
                    result = GasEffectRequestResult.RejectedRequirement;
                    break;
                case GasGameplayEffectApplicationOutcome.RejectedImmunity:
                    if (outcome.ActiveEffect.IsValid)
                        return false;
                    result = GasEffectRequestResult.RejectedImmunity;
                    break;
                case GasGameplayEffectApplicationOutcome.RejectedTargetLife:
                    if (outcome.ActiveEffect.IsValid)
                        return false;
                    result = GasEffectRequestResult.RejectedTargetLife;
                    break;
                case GasGameplayEffectApplicationOutcome.RejectedStackPolicy:
                    if (outcome.ActiveEffect.IsValid)
                        return false;
                    result = GasEffectRequestResult.RejectedStackPolicy;
                    break;
                case GasGameplayEffectApplicationOutcome.RejectedStaleBinding:
                    if (outcome.ActiveEffect.IsValid)
                        return false;
                    result = GasEffectRequestResult.RejectedStaleBinding;
                    break;
                default:
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 验证长期 effect handle 归属于该 application outcome 的稳定 target owner。
        /// </summary>
        private static bool HasValidActiveEffect(in GasApplicationOutcomeRecord outcome)
        {
            return outcome.ActiveEffect.IsValid &&
                   outcome.ActiveEffect.OwnerAsc.Equals(outcome.TargetAsc);
        }
    }

    /// <summary>
    /// 在 final reduce 前为每条 sealed command 准备一个固定 RequestTerminal intent。
    /// </summary>
    internal struct GasRequestTerminalPrepareJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public NativeArray<GasSealedCommand> SealedCommands;
        [ReadOnly] public NativeArray<GasOwnerPlanRecord> OwnerPlans;
        [ReadOnly] public NativeArray<GasApplicationOutcomeRecord> ApplicationOutcomes;
        public NativeArray<GasRequestTerminalIntent> Intents;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 按 canonical ordinal 一次准备全部终态，任何缺失、重复或不可映射结果统一提升 fatal。
        /// </summary>
        public void Execute()
        {
            var execution = Execution[0];
            if (Admission[0].Succeeded == 0 ||
                execution.PostAdmissionFailure != GasTickAdmissionFailureReason.None)
                return;
            var count = execution.StoredSealedCommandCount;
            if (count < 0 || count != execution.OwnerPlanCount ||
                count > SealedCommands.Length || count > OwnerPlans.Length ||
                count > Intents.Length)
            {
                Fail(ref execution);
                return;
            }
            for (var index = 0; index < count; index++)
            {
                var sealedCommand = SealedCommands[index];
                var ownerPlan = OwnerPlans[index];
                if (HasDuplicateRequestKey(index) ||
                    !GasRequestTerminalProjectionUtility.TryCreate(
                        index, in sealedCommand, in ownerPlan,
                        ApplicationOutcomes, execution.ApplicationOutcomeCount, out var intent))
                {
                    Fail(ref execution);
                    return;
                }
                Intents[index] = intent;
            }
        }

        /// <summary>
        /// 拒绝同一 Tick 内两个 sealed command 争用同一个永久 RequestKey。
        /// </summary>
        private bool HasDuplicateRequestKey(int ordinal)
        {
            var command = SealedCommands[ordinal].Command;
            for (var index = 0; index < ordinal; index++)
            {
                var prior = SealedCommands[index].Command;
                if (prior.SimulationEpoch == command.SimulationEpoch &&
                    prior.RequestId == command.RequestId &&
                    prior.RequestSequence == command.RequestSequence)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 写入唯一 post-admission 首因，后续 intent 统一 no-op。
        /// </summary>
        private void Fail(ref GasTickExecutionState execution)
        {
            execution.PostAdmissionFailure =
                GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
            Execution[0] = execution;
        }
    }

    /// <summary>
    /// 在全部 post-owner prepare 完成后生成 Session 唯一发布裁决，fatal 时所有 intent 均不可发布。
    /// </summary>
    internal struct GasSessionFinalPublishFaultReduceJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public NativeArray<GasTickExecutionState> Execution;
        public NativeArray<GasFinalPublishDecision> Decision;

        /// <summary>
        /// 将 admission 与 prepare 首因收敛为单一成功或失败裁决。
        /// </summary>
        public void Execute()
        {
            var admission = Admission[0];
            var execution = Execution[0];
            var failure = admission.Succeeded == 0
                ? admission.FailureReason
                : execution.PostAdmissionFailure;
            Decision[0] = new GasFinalPublishDecision
            {
                FailureReason = failure,
                Succeeded = failure == GasTickAdmissionFailureReason.None ? (byte)1 : (byte)0,
            };
        }
    }

    /// <summary>
    /// 在 SessionFaultReduce 前完成全部 target destination 复验，失败只写 post-admission fatal。
    /// </summary>
    internal struct GasTargetPublishPreflightJob : IJob
    {
        public Entity Session;
        public ulong SimulationEpoch;
        [ReadOnly] public GasScaleProfile Profile;
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public NativeArray<GasOwnerPlanRecord> OwnerPlans;
        [ReadOnly] public NativeArray<GasTargetShadowState> TargetShadows;
        [ReadOnly] public NativeArray<ActiveEffectSlot> ShadowActiveEffects;
        [ReadOnly] public NativeArray<AttributeValueSlot> ShadowAttributes;
        [ReadOnly] public NativeArray<AttributeDirtyWord> ShadowAttributeDirtyWords;
        [ReadOnly] public NativeArray<TagCountSlot> ShadowTagCounts;
        [ReadOnly] public NativeArray<TagPresenceWord> ShadowTagPresenceWords;
        [ReadOnly] public NativeArray<GasPayloadRangeRecord> ShadowPayloadRanges;
        [ReadOnly] public NativeArray<GasPayloadValueSlot> ShadowPayloadValues;
        public int ActiveEffectStride;
        public int AttributeStride;
        public int AttributeDirtyWordStride;
        public int TagStride;
        public int TagPresenceWordStride;
        public int PayloadRangeStride;
        public int PayloadValueStride;
        [ReadOnly] public ComponentLookup<AscLifecycle> AscLifecycles;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public ComponentLookup<AscSlabHeads> SlabHeads;
        [ReadOnly] public ComponentLookup<GasPayloadRangeAllocatorState> PayloadStates;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public BufferLookup<GrantedAbilitySlot> Grants;
        [ReadOnly] public BufferLookup<AbilityActivationSlot> Activations;
        [ReadOnly] public BufferLookup<AbilityContinuationSlot> Continuations;
        [ReadOnly] public BufferLookup<AbilitySubscriptionSlot> Subscriptions;
        [ReadOnly] public BufferLookup<ActiveEffectSlot> ActiveEffects;
        [ReadOnly] public BufferLookup<AttributeValueSlot> Attributes;
        [ReadOnly] public BufferLookup<AttributeDirtyWord> AttributeDirtyWords;
        [ReadOnly] public BufferLookup<TagCountSlot> TagCounts;
        [ReadOnly] public BufferLookup<TagPresenceWord> TagPresenceWords;
        [ReadOnly] public BufferLookup<GasPayloadRangeRecord> PayloadRanges;
        [ReadOnly] public BufferLookup<GasPayloadValueSlot> PayloadValues;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 证明后续发布不会分配或失败；任何异常在归约前提升为 Session fatal。
        /// </summary>
        public void Execute()
        {
            var execution = Execution[0];
            if (execution.GameplayEnabled == 0 || Admission[0].Succeeded == 0 ||
                execution.PostAdmissionFailure != GasTickAdmissionFailureReason.None)
                return;
            for (var index = 0; index < TargetShadows.Length; index++)
            {
                var shadow = TargetShadows[index];
                if (shadow.Prepared == 0 || CanPublish(index, in shadow))
                    continue;
                Fail(ref execution);
                return;
            }
            if (!CanPublishAbilityLifecycles(in execution))
                Fail(ref execution);
        }

        /// <summary>
        /// 验证目标组件、Buffer、逻辑长度、shadow range 与预安装物理容量全部可用。
        /// </summary>
        private bool CanPublish(int index, in GasTargetShadowState shadow)
        {
            var target = shadow.Target;
            return target != Entity.Null &&
                   AscLifecycles.HasComponent(target) && SlabHeads.HasComponent(target) &&
                   PayloadStates.HasComponent(target) && ActiveEffects.HasBuffer(target) &&
                   Attributes.HasBuffer(target) && AttributeDirtyWords.HasBuffer(target) &&
                   TagCounts.HasBuffer(target) && TagPresenceWords.HasBuffer(target) &&
                   PayloadRanges.HasBuffer(target) && PayloadValues.HasBuffer(target) &&
                   HasShadowRange(index, ActiveEffectStride, shadow.ActiveEffectCount,
                       ShadowActiveEffects.Length) &&
                   HasShadowRange(index, AttributeStride, shadow.AttributeCount,
                       ShadowAttributes.Length) &&
                   HasShadowRange(index, AttributeDirtyWordStride, shadow.AttributeDirtyWordCount,
                       ShadowAttributeDirtyWords.Length) &&
                   HasShadowRange(index, TagStride, shadow.TagCount, ShadowTagCounts.Length) &&
                   HasShadowRange(index, TagPresenceWordStride, shadow.TagPresenceWordCount,
                       ShadowTagPresenceWords.Length) &&
                   HasShadowRange(index, PayloadRangeStride, shadow.PayloadRangeCount,
                       ShadowPayloadRanges.Length) &&
                   HasShadowRange(index, PayloadValueStride, shadow.PayloadValueCount,
                       ShadowPayloadValues.Length) &&
                   ActiveEffects[target].Capacity >= shadow.ActiveEffectCount &&
                   Attributes[target].Capacity >= shadow.AttributeCount &&
                   AttributeDirtyWords[target].Capacity >= shadow.AttributeDirtyWordCount &&
                   TagCounts[target].Capacity >= shadow.TagCount &&
                   TagPresenceWords[target].Capacity >= shadow.TagPresenceWordCount &&
                   PayloadRanges[target].Capacity >= shadow.PayloadRangeCount &&
                   PayloadValues[target].Capacity >= shadow.PayloadValueCount;
        }

        /// <summary>
        /// 复验 OwnerWave 后的 ability authority，并证明 projected owner lifecycle 可一次 no-fail 收口。
        /// </summary>
        private bool CanPublishAbilityLifecycles(in GasTickExecutionState execution)
        {
            if (!Registries.HasBuffer(Session) || execution.OwnerPlanCount < 0 ||
                execution.OwnerPlanCount > OwnerPlans.Length)
                return false;
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State != GasAscRegistryState.Ready)
                    continue;
                if (!TryResolveLifecycleOwner(
                        index, in slot, out var asc, out var projectedLifecycle) ||
                    !ValidateAbilityRelations(in slot.OwnerAsc, asc) ||
                    !ValidateLifecycleSlabs(asc))
                    return false;
                if (IsOwnerTerminal(projectedLifecycle))
                {
                    if (!CanFinalizeTerminalOwner(in slot.OwnerAsc, asc))
                        return false;
                    continue;
                }
                if (!CanFinalizeSurvivingOwner(
                        in slot.OwnerAsc, asc, in execution))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 解析 registry owner 与其同 ordinal target shadow，拒绝实体、身份或 buffer 半安装。
        /// </summary>
        private bool TryResolveLifecycleOwner(
            int registryIndex,
            in AscRegistrySlot slot,
            out Entity asc,
            out GasAscLifecycleState projectedLifecycle)
        {
            asc = slot.ResolveRuntimeEntity();
            projectedLifecycle = default;
            if (registryIndex < 0 || registryIndex >= TargetShadows.Length ||
                !AscIdentities.HasComponent(asc) || !AscLifecycles.HasComponent(asc) ||
                !SlabHeads.HasComponent(asc) || !Grants.HasBuffer(asc) ||
                !Activations.HasBuffer(asc) || !Continuations.HasBuffer(asc) ||
                !Subscriptions.HasBuffer(asc))
                return false;
            var identity = AscIdentities[asc];
            var shadow = TargetShadows[registryIndex];
            if (identity.SimulationEpoch != SimulationEpoch ||
                !identity.OwnerAsc.Equals(slot.OwnerAsc) || shadow.Prepared == 0 ||
                shadow.Target != asc || !shadow.OwnerAsc.Equals(slot.OwnerAsc))
                return false;
            projectedLifecycle = shadow.Lifecycle.State;
            return projectedLifecycle == GasAscLifecycleState.Ready ||
                   projectedLifecycle == GasAscLifecycleState.Alive ||
                   projectedLifecycle == GasAscLifecycleState.Terminal ||
                   projectedLifecycle == GasAscLifecycleState.Dead;
        }

        /// <summary>
        /// 复用 admission 的 typed-handle 关系验证，避免 lifecycle 阶段形成第二套关系事实源。
        /// </summary>
        private bool ValidateAbilityRelations(in OwnerAscHandle owner, Entity asc)
        {
            var grants = Grants[asc];
            var activations = Activations[asc];
            var continuations = Continuations[asc];
            var subscriptions = Subscriptions[asc];
            return GasWholeTickInfrastructureAdmissionJob.ValidateGrantRelations(
                       in owner, grants, activations) &&
                   GasWholeTickInfrastructureAdmissionJob.ValidateActivationRelations(
                       in owner, grants, activations, continuations) &&
                   GasWholeTickInfrastructureAdmissionJob.ValidateContinuationRelations(
                       in owner, activations, continuations) &&
                   GasWholeTickInfrastructureAdmissionJob.ValidateSubscriptionRelations(
                       in owner, subscriptions);
        }

        /// <summary>
        /// 对 lifecycle publish 会改写的 grant/activation slab 再做一次 OwnerWave 后 allocator 证明。
        /// </summary>
        private bool ValidateLifecycleSlabs(Entity asc)
        {
            var heads = SlabHeads[asc];
            var grants = new GasGrantedAbilitySlabStorage { Buffer = Grants[asc] };
            var activations = new GasAbilityActivationSlabStorage { Buffer = Activations[asc] };
            return GasNonCompactingSlabAllocator.Validate(
                       in heads.GrantedAbility, ref grants, Profile.MaxGrantedAbilityCount) ==
                   GasSlabStorageFailure.None &&
                   GasNonCompactingSlabAllocator.Validate(
                       in heads.AbilityActivation, ref activations,
                       Profile.MaxAbilityActivationCount) == GasSlabStorageFailure.None;
        }

        /// <summary>
        /// 证明 Dead/Terminal owner 不含 wait，并满足 OwnerTerminal helper 的闭世界形状。
        /// </summary>
        private bool CanFinalizeTerminalOwner(in OwnerAscHandle owner, Entity asc)
        {
            var continuations = Continuations[asc];
            var subscriptions = Subscriptions[asc];
            for (var index = 0; index < continuations.Length; index++)
            {
                if (continuations[index].Header.StorageState == GasSlabSlotState.Live)
                    return false;
            }
            for (var index = 0; index < subscriptions.Length; index++)
            {
                if (subscriptions[index].Header.StorageState == GasSlabSlotState.Live)
                    return false;
            }
            var activations = Activations[asc];
            for (var index = 0; index < activations.Length; index++)
            {
                var activation = activations[index];
                if (activation.Header.StorageState != GasSlabSlotState.Live)
                    continue;
                if (!activation.Handle.OwnerAsc.Equals(owner) ||
                    (activation.Phase != GasAbilityActivationPhase.RunningUncommitted &&
                     activation.Phase != GasAbilityActivationPhase.Committed) ||
                    activation.EndReason != GasAbilityEndReason.None ||
                    activation.WasCancelled != 0 || activation.ContinuationCount != 0 ||
                    (activation.Phase == GasAbilityActivationPhase.Committed) !=
                    (activation.CommitSequence != 0))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 证明 surviving owner 的每条 accepted Commit 精确命中唯一 Committed one-shot Activation。
        /// </summary>
        private bool CanFinalizeSurvivingOwner(
            in OwnerAscHandle owner,
            Entity asc,
            in GasTickExecutionState execution)
        {
            var activations = Activations[asc];
            for (var index = 0; index < execution.OwnerPlanCount; index++)
            {
                var plan = OwnerPlans[index];
                if (plan.BusinessAccepted == 0 ||
                    plan.CommandKind != GasBoundaryCommandKind.Commit ||
                    !plan.OwnerAsc.Equals(owner))
                    continue;
                if (!CanBeginNormalEnd(in plan, activations) ||
                    HasPriorCommittedPlan(in plan.Activation, index))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 镜像 one-shot normal-end helper 的只读前置条件，publish 阶段不得再发现业务拒绝。
        /// </summary>
        private static bool CanBeginNormalEnd(
            in GasOwnerPlanRecord plan,
            DynamicBuffer<AbilityActivationSlot> activations)
        {
            if (plan.Result != GasAbilityCommandResult.Committed ||
                plan.ProducesCommittedWork == 0 || plan.StableSequence == 0 ||
                !plan.Activation.IsValid || !plan.GrantedAbility.IsValid ||
                !plan.Activation.OwnerAsc.Equals(plan.OwnerAsc) ||
                plan.Activation.SlotIndex < 0 || plan.Activation.SlotIndex >= activations.Length)
                return false;
            var activation = activations[plan.Activation.SlotIndex];
            return activation.Header.StorageState == GasSlabSlotState.Live &&
                   activation.Handle.Equals(plan.Activation) &&
                   activation.GrantedAbility.Equals(plan.GrantedAbility) &&
                   activation.Phase == GasAbilityActivationPhase.Committed &&
                   activation.CommitSequence == plan.StableSequence &&
                   activation.EndReason == GasAbilityEndReason.None &&
                   activation.WasCancelled == 0 && activation.ContinuationCount == 0;
        }

        /// <summary>
        /// 拒绝同一 Activation 在一个 canonical Tick 内被两个 CommitPlan 重复 normal-end。
        /// </summary>
        private bool HasPriorCommittedPlan(in AbilityActivationHandle activation, int ordinal)
        {
            for (var index = 0; index < ordinal; index++)
            {
                var prior = OwnerPlans[index];
                if (prior.BusinessAccepted != 0 &&
                    prior.CommandKind == GasBoundaryCommandKind.Commit &&
                    prior.Activation.Equals(activation))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断 target shadow 是否要求 owner terminal 优先于 normal end。
        /// </summary>
        private static bool IsOwnerTerminal(GasAscLifecycleState lifecycle)
        {
            return lifecycle == GasAscLifecycleState.Terminal ||
                   lifecycle == GasAscLifecycleState.Dead;
        }

        /// <summary>
        /// 写入唯一 post-admission invariant 首因。
        /// </summary>
        private void Fail(ref GasTickExecutionState execution)
        {
            execution.PostAdmissionFailure =
                GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
            Execution[0] = execution;
        }

        /// <summary>
        /// 以 long 验证 stride range，拒绝负长度与整数乘加回绕。
        /// </summary>
        private static bool HasShadowRange(int index, int stride, int count, int length)
        {
            if (index < 0 || stride < 0 || count < 0 || count > stride)
                return false;
            var start = (long)index * stride;
            return start >= 0 && start <= length && count <= length - start;
        }
    }

    /// <summary>
    /// 在 SessionFaultReduce 成功后一次发布全部 target shadow；该阶段按契约不得再失败。
    /// </summary>
    internal struct GasAscTargetPublishJob : IJob
    {
        [ReadOnly] public NativeArray<GasFinalPublishDecision> Decision;
        [ReadOnly] public NativeArray<GasTargetShadowState> TargetShadows;
        [ReadOnly] public NativeArray<ActiveEffectSlot> ShadowActiveEffects;
        [ReadOnly] public NativeArray<AttributeValueSlot> ShadowAttributes;
        [ReadOnly] public NativeArray<AttributeDirtyWord> ShadowAttributeDirtyWords;
        [ReadOnly] public NativeArray<TagCountSlot> ShadowTagCounts;
        [ReadOnly] public NativeArray<TagPresenceWord> ShadowTagPresenceWords;
        [ReadOnly] public NativeArray<GasPayloadRangeRecord> ShadowPayloadRanges;
        [ReadOnly] public NativeArray<GasPayloadValueSlot> ShadowPayloadValues;
        public int ActiveEffectStride;
        public int AttributeStride;
        public int AttributeDirtyWordStride;
        public int TagStride;
        public int TagPresenceWordStride;
        public int PayloadRangeStride;
        public int PayloadValueStride;
        public ComponentLookup<AscLifecycle> AscLifecycles;
        public ComponentLookup<AscSlabHeads> SlabHeads;
        public ComponentLookup<GasPayloadRangeAllocatorState> PayloadStates;
        public BufferLookup<ActiveEffectSlot> ActiveEffects;
        public BufferLookup<AttributeValueSlot> Attributes;
        public BufferLookup<AttributeDirtyWord> AttributeDirtyWords;
        public BufferLookup<TagCountSlot> TagCounts;
        public BufferLookup<TagPresenceWord> TagPresenceWords;
        public BufferLookup<GasPayloadRangeRecord> PayloadRanges;
        public BufferLookup<GasPayloadValueSlot> PayloadValues;

        /// <summary>
        /// 按 registry ordinal 发布已经通过全量 preflight 的完整 authority。
        /// </summary>
        public void Execute()
        {
            if (Decision[0].Succeeded == 0)
                return;
            for (var index = 0; index < TargetShadows.Length; index++)
            {
                var shadow = TargetShadows[index];
                if (shadow.Prepared == 0)
                    continue;
                Publish(index, in shadow);
            }
        }

        /// <summary>
        /// 覆盖目标组件与 Buffer；本方法只在全量 CanPublish 成功后调用，禁止再产生失败分支。
        /// </summary>
        private void Publish(int index, in GasTargetShadowState shadow)
        {
            var target = shadow.Target;
            AscLifecycles[target] = shadow.Lifecycle;
            SlabHeads[target] = shadow.SlabHeads;
            PayloadStates[target] = shadow.PayloadState;
            CopyToBuffer(ShadowActiveEffects, index * ActiveEffectStride,
                shadow.ActiveEffectCount, ActiveEffects[target]);
            CopyToBuffer(ShadowAttributes, index * AttributeStride,
                shadow.AttributeCount, Attributes[target]);
            CopyToBuffer(ShadowAttributeDirtyWords, index * AttributeDirtyWordStride,
                shadow.AttributeDirtyWordCount, AttributeDirtyWords[target]);
            CopyToBuffer(ShadowTagCounts, index * TagStride,
                shadow.TagCount, TagCounts[target]);
            CopyToBuffer(ShadowTagPresenceWords, index * TagPresenceWordStride,
                shadow.TagPresenceWordCount, TagPresenceWords[target]);
            CopyToBuffer(ShadowPayloadRanges, index * PayloadRangeStride,
                shadow.PayloadRangeCount, PayloadRanges[target]);
            CopyToBuffer(ShadowPayloadValues, index * PayloadValueStride,
                shadow.PayloadValueCount, PayloadValues[target]);
        }

        /// <summary>
        /// 将定长 shadow slice 覆盖到已通过容量复验的 DynamicBuffer。
        /// </summary>
        private static void CopyToBuffer<T>(
            NativeArray<T> source,
            int start,
            int count,
            DynamicBuffer<T> destination)
            where T : unmanaged, IBufferElementData
        {
            destination.ResizeUninitialized(count);
            for (var offset = 0; offset < count; offset++)
                destination[offset] = source[start + offset];
        }
    }

    /// <summary>
    /// 在 target lifecycle 发布后收口 Ability；OwnerTerminal 始终优先于同 Tick normal End。
    /// </summary>
    internal struct GasAbilityLifecyclePublishJob : IJob
    {
        public Entity Session;
        public ulong SimulationEpoch;
        public NativeArray<GasFinalPublishDecision> Decision;
        [ReadOnly] public NativeArray<GasOwnerPlanRecord> OwnerPlans;
        public NativeArray<GasTickExecutionState> Execution;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public ComponentLookup<AscLifecycle> AscLifecycles;
        public ComponentLookup<AscSlabHeads> SlabHeads;
        public BufferLookup<GrantedAbilitySlot> Grants;
        public BufferLookup<AbilityActivationSlot> Activations;
        [ReadOnly] public BufferLookup<AbilityContinuationSlot> Continuations;
        [ReadOnly] public BufferLookup<AbilitySubscriptionSlot> Subscriptions;

        /// <summary>
        /// 先终结 Dead/Terminal owner，再 normal-end surviving Commit，最后统一执行 post-command 收尾。
        /// </summary>
        public void Execute()
        {
            if (Decision[0].Succeeded == 0)
                return;
            var execution = Execution[0];
            if (!FinalizeTerminalOwners() ||
                !BeginNormalEnds(in execution) ||
                !FinalizeSurvivingOwners())
            {
                Fail(ref execution);
            }
        }

        /// <summary>
        /// 对 projected Dead/Terminal owner 一次完成 OwnerTerminal 与 grant child 精确释放。
        /// </summary>
        private bool FinalizeTerminalOwners()
        {
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State != GasAscRegistryState.Ready)
                    continue;
                if (!TryResolveOwner(in slot, out var asc, out var lifecycle))
                    return false;
                if (!IsOwnerTerminal(lifecycle))
                    continue;
                var heads = SlabHeads[asc];
                if (!GasAbilityLifecycleMaintenance.TryFinalizeOneShotOwnerTerminal(
                        in slot.OwnerAsc, Grants[asc], Activations[asc], Continuations[asc],
                        Subscriptions[asc], ref heads, out _))
                    return false;
                SlabHeads[asc] = heads;
            }
            return true;
        }

        /// <summary>
        /// 对仍存活 owner 的 accepted one-shot Commit 建立 Completed Ending 屏障。
        /// </summary>
        private bool BeginNormalEnds(in GasTickExecutionState execution)
        {
            if (execution.OwnerPlanCount < 0 || execution.OwnerPlanCount > OwnerPlans.Length)
                return false;
            for (var index = 0; index < execution.OwnerPlanCount; index++)
            {
                var plan = OwnerPlans[index];
                if (plan.BusinessAccepted == 0 ||
                    plan.CommandKind != GasBoundaryCommandKind.Commit)
                    continue;
                if (!TryResolveOwner(
                        in plan.OwnerAsc, out var asc, out var lifecycle))
                    return false;
                if (IsOwnerTerminal(lifecycle))
                    continue;
                if (!GasAbilityLifecycleMaintenance.TryBeginCommittedOneShotNormalEnd(
                        in plan, Activations[asc]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 在全部 normal-end 屏障建立后完成 surviving owner 的 Ending/tombstone 收尾。
        /// </summary>
        private bool FinalizeSurvivingOwners()
        {
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State != GasAscRegistryState.Ready)
                    continue;
                if (!TryResolveOwner(in slot, out var asc, out var lifecycle))
                    return false;
                if (IsOwnerTerminal(lifecycle))
                    continue;
                var heads = SlabHeads[asc];
                if (!GasAbilityLifecycleMaintenance.RunPostCommand(
                        Grants[asc], Activations[asc], Continuations[asc], ref heads))
                    return false;
                SlabHeads[asc] = heads;
            }
            return true;
        }

        /// <summary>
        /// 通过 registry slot 解析 owner authority 与 target-publish 后 lifecycle。
        /// </summary>
        private bool TryResolveOwner(
            in AscRegistrySlot slot,
            out Entity asc,
            out GasAscLifecycleState lifecycle)
        {
            asc = slot.ResolveRuntimeEntity();
            return TryResolveEntity(in slot.OwnerAsc, asc, out lifecycle);
        }

        /// <summary>
        /// 通过稳定 owner 在 registry 中解析唯一 authority entity。
        /// </summary>
        private bool TryResolveOwner(
            in OwnerAscHandle owner,
            out Entity asc,
            out GasAscLifecycleState lifecycle)
        {
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State == GasAscRegistryState.Ready && slot.OwnerAsc.Equals(owner))
                {
                    asc = slot.ResolveRuntimeEntity();
                    return TryResolveEntity(in owner, asc, out lifecycle);
                }
            }
            asc = Entity.Null;
            lifecycle = default;
            return false;
        }

        /// <summary>
        /// 复验 lifecycle publish 所需组件、buffer 与完整 owner identity。
        /// </summary>
        private bool TryResolveEntity(
            in OwnerAscHandle owner,
            Entity asc,
            out GasAscLifecycleState lifecycle)
        {
            lifecycle = default;
            if (!AscIdentities.HasComponent(asc) || !AscLifecycles.HasComponent(asc) ||
                !SlabHeads.HasComponent(asc) || !Grants.HasBuffer(asc) ||
                !Activations.HasBuffer(asc) || !Continuations.HasBuffer(asc) ||
                !Subscriptions.HasBuffer(asc))
                return false;
            var identity = AscIdentities[asc];
            lifecycle = AscLifecycles[asc].State;
            return identity.SimulationEpoch == SimulationEpoch && identity.OwnerAsc.Equals(owner) &&
                   (lifecycle == GasAscLifecycleState.Ready ||
                    lifecycle == GasAscLifecycleState.Alive ||
                    lifecycle == GasAscLifecycleState.Terminal ||
                    lifecycle == GasAscLifecycleState.Dead);
        }

        /// <summary>
        /// 判断 target publish 后 owner 是否必须走 OwnerTerminal 优先路径。
        /// </summary>
        private static bool IsOwnerTerminal(GasAscLifecycleState lifecycle)
        {
            return lifecycle == GasAscLifecycleState.Terminal ||
                   lifecycle == GasAscLifecycleState.Dead;
        }

        /// <summary>
        /// 把理论不可达的 lifecycle 失败提升为第二次 final reduce 可见的唯一 fatal。
        /// </summary>
        private void Fail(ref GasTickExecutionState execution)
        {
            execution.PostAdmissionFailure =
                GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
            Execution[0] = execution;
            Decision[0] = new GasFinalPublishDecision
            {
                FailureReason = GasTickAdmissionFailureReason.PostAdmissionInvariantViolation,
                Succeeded = 0,
            };
        }
    }

    /// <summary>
    /// 把 target writer 的 application outcome 收敛为 target-owned stable CoreFact 分区。
    /// </summary>
    internal struct GasTargetLocalStabilizationDeathJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public NativeArray<GasApplicationOutcomeRecord> ApplicationOutcomes;
        [ReadOnly] public NativeArray<GasAttributeMutationOutcomeRecord> MutationOutcomes;
        [ReadOnly] public BlobAssetReference<GasDefinitionCatalogBlob> Catalog;
        public NativeArray<GasCoreFactRecord> CoreFacts;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 对每条 application 固化 typed outcome；结果在 merge 前不直接投影到 Boundary。
        /// </summary>
        public void Execute()
        {
            if (!GasTickJobUtility.EnterDownstream(
                    Admission, Execution, GasTickLaneMask.StabilizeDeath))
                return;

            var execution = Execution[0];
            execution.CoreFactCount = 0;
            execution.DeathFactCount = 0;
            if ((execution.AttributeMutationCount > 0 || execution.ApplicationOutcomeCount > 0) &&
                !Catalog.IsCreated)
            {
                execution.PostAdmissionFailure =
                    GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                Execution[0] = execution;
                return;
            }
            if (execution.AttributeMutationCount == 0 && execution.ApplicationOutcomeCount == 0)
            {
                Execution[0] = execution;
                return;
            }
            ref var catalog = ref Catalog.Value;
            for (var index = 0; index < execution.AttributeMutationCount; index++)
            {
                if (execution.CoreFactCount >= CoreFacts.Length)
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                    break;
                }
                var mutation = MutationOutcomes[index];
                var mutationPhase = mutation.IsPeriodTick != 0 ? (ushort)6 : (ushort)1;
                CoreFacts[execution.CoreFactCount++] = new GasCoreFactRecord
                {
                    Scope = GasBoundaryFactScope.Asc,
                    Plane = GasBoundaryFactPlane.Gameplay,
                    Kind = GasBoundaryFactKind.AttributeChanged,
                    SourceAsc = mutation.SourceAsc,
                    TargetAsc = mutation.TargetAsc,
                    SimulationTick = execution.CandidateTick,
                    SemanticPhaseOrdinal = mutationPhase,
                    WorkClassOrdinal = 1,
                    ParentCausalityId = mutation.CausalityId,
                    SemanticId = mutation.ApplicationId,
                    Payload = CreateMutationPayload(in mutation, ref catalog),
                    OperationOrdinal = mutation.OperationOrdinal,
                    FactOrdinal = mutation.IsPeriodTick != 0
                        ? ComposePeriodFactOrdinal(
                            mutation.PeriodExecutionOrdinal,
                            mutation.ModifierOrdinal)
                        : mutation.ModifierOrdinal,
                };
            }
            for (var index = 0; index < execution.ApplicationOutcomeCount; index++)
            {
                if (execution.CoreFactCount >= CoreFacts.Length)
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                    break;
                }
                var outcome = ApplicationOutcomes[index];
                if (outcome.IsPeriodTick != 0)
                {
                    CoreFacts[execution.CoreFactCount++] = new GasCoreFactRecord
                    {
                        Scope = outcome.Scope,
                        Plane = GasBoundaryFactPlane.Gameplay,
                        Kind = GasBoundaryFactKind.PeriodTick,
                        SourceAsc = outcome.SourceAsc,
                        TargetAsc = outcome.TargetAsc,
                        SimulationTick = execution.CandidateTick,
                        SemanticPhaseOrdinal = 7,
                        WorkClassOrdinal = 1,
                        ParentCausalityId = outcome.CausalityId,
                        SemanticId = outcome.ApplicationId,
                        Payload = CreatePeriodPayload(in outcome, in MutationOutcomes),
                        OperationOrdinal = outcome.OperationOrdinal,
                        FactOrdinal = ComposePeriodFactOrdinal(
                            outcome.PeriodExecutionOrdinal,
                            int.MaxValue),
                    };
                }
                else
                {
                    CoreFacts[execution.CoreFactCount++] = new GasCoreFactRecord
                    {
                        Scope = outcome.Scope,
                        Plane = GasBoundaryFactPlane.Gameplay,
                        Kind = GasBoundaryFactKind.EffectLifecycle,
                        SourceAsc = outcome.SourceAsc,
                        TargetAsc = outcome.TargetAsc,
                        SimulationTick = execution.CandidateTick,
                        SemanticPhaseOrdinal = 2,
                        WorkClassOrdinal = 1,
                        ParentCausalityId = outcome.CausalityId,
                        SemanticId = outcome.ApplicationId,
                        Payload = CreateOutcomePayload(in outcome),
                        OperationOrdinal = outcome.OperationOrdinal,
                        FactOrdinal = int.MaxValue,
                    };

                    AppendDefinitionFacts(
                        ref execution,
                        in outcome,
                        ref catalog);
                }
            }
            for (var index = 0; index < execution.ApplicationOutcomeCount; index++)
            {
                var outcome = ApplicationOutcomes[index];
                if (outcome.DeathCrossed == 0)
                    continue;
                if (execution.CoreFactCount >= CoreFacts.Length)
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                    break;
                }
                CoreFacts[execution.CoreFactCount++] = new GasCoreFactRecord
                {
                    Scope = GasBoundaryFactScope.Asc,
                    Plane = GasBoundaryFactPlane.Gameplay,
                    Kind = GasBoundaryFactKind.Death,
                    SourceAsc = outcome.SourceAsc,
                    TargetAsc = outcome.TargetAsc,
                    SimulationTick = execution.CandidateTick,
                    SemanticPhaseOrdinal = outcome.IsPeriodTick != 0 ? (ushort)8 : (ushort)3,
                    WorkClassOrdinal = 1,
                    ParentCausalityId = outcome.CausalityId,
                    SemanticId = outcome.DeathTransitionId,
                    Payload = CreateDeathPayload(in outcome, ref catalog),
                    OperationOrdinal = outcome.OperationOrdinal,
                    FactOrdinal = outcome.IsPeriodTick != 0
                        ? ComposePeriodFactOrdinal(
                            outcome.PeriodExecutionOrdinal,
                            0)
                        : 0,
                };
                execution.DeathFactCount++;
            }
            Execution[0] = execution;
        }

        /// <summary>
        /// 为声明了执行或 Cue 语义的 Definition 追加独立事实，避免消费者从普通 outcome 猜测阶段。
        /// </summary>
        private void AppendDefinitionFacts(
            ref GasTickExecutionState execution,
            in GasApplicationOutcomeRecord outcome,
            ref GasDefinitionCatalogBlob catalog)
        {
            if (outcome.Outcome != GasGameplayEffectApplicationOutcome.AppliedInstant &&
                outcome.Outcome != GasGameplayEffectApplicationOutcome.CreatedActive &&
                outcome.Outcome != GasGameplayEffectApplicationOutcome.MergedStack)
                return;
            if (outcome.DefinitionId == 0 || outcome.OperationOrdinal < 0)
                return;
            var definitionIndex = FindDefinitionIndex(ref catalog, outcome.DefinitionId);
            if (definitionIndex < 0)
                return;
            var definition = catalog.GameplayEffects[definitionIndex];
            if (definition.Lifetime == GasEffectLifetimePolicy.InstantExecution)
            {
                if (!TryAppendDefinitionFact(
                        ref execution,
                        in outcome,
                        GasBoundaryFactKind.ExecutionCalculation,
                        4,
                        1))
                    return;
                TryAppendDefinitionFact(
                    ref execution,
                    in outcome,
                    GasBoundaryFactKind.Cue,
                    5,
                    1);
            }
            else if (definition.CueRange.Count > 0)
            {
                TryAppendDefinitionFact(
                    ref execution,
                    in outcome,
                    GasBoundaryFactKind.Cue,
                    4,
                    1);
            }
        }

        /// <summary>
        /// 将执行/Cue marker 以稳定 phase 写入 scratch；容量不足显式提升 post-admission fault。
        /// </summary>
        private bool TryAppendDefinitionFact(
            ref GasTickExecutionState execution,
            in GasApplicationOutcomeRecord outcome,
            GasBoundaryFactKind kind,
            ushort phase,
            ushort workClass)
        {
            if (execution.CoreFactCount >= CoreFacts.Length)
            {
                execution.PostAdmissionFailure =
                    GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                return false;
            }
            CoreFacts[execution.CoreFactCount++] = new GasCoreFactRecord
            {
                Scope = outcome.Scope,
                Plane = GasBoundaryFactPlane.Gameplay,
                Kind = kind,
                SourceAsc = outcome.SourceAsc,
                TargetAsc = outcome.TargetAsc,
                SimulationTick = execution.CandidateTick,
                SemanticPhaseOrdinal = phase,
                WorkClassOrdinal = workClass,
                ParentCausalityId = outcome.CausalityId,
                SemanticId = outcome.ApplicationId,
                Payload = new BoundaryFactPayload
                {
                    SchemaVersion = 1,
                    Kind = GasBoundaryPayloadKind.IntegerPair,
                    Integer0 = (long)kind,
                    Integer1 = outcome.ApplicationId > long.MaxValue
                        ? long.MaxValue
                        : (long)outcome.ApplicationId,
                    Integer2 = outcome.DefinitionId,
                    StableId0 = outcome.ApplicationId,
                    StableId1 = outcome.CausalityId,
                },
                OperationOrdinal = outcome.OperationOrdinal,
                FactOrdinal = phase,
            };
            return true;
        }

        /// <summary>
        /// 按稳定 DefinitionId 查找 Catalog dense index。
        /// </summary>
        private static int FindDefinitionIndex(
            ref GasDefinitionCatalogBlob catalog,
            int definitionId)
        {
            for (var index = 0; index < catalog.GameplayEffects.Length; index++)
                if (catalog.GameplayEffects[index].DefinitionId == definitionId)
                    return index;
            return -1;
        }

        /// <summary>
        /// 将 outcome、failure、modifier count 与 ActiveEffect slot 身份编码为闭世界 payload。
        /// </summary>
        private static BoundaryFactPayload CreateOutcomePayload(
            in GasApplicationOutcomeRecord outcome)
        {
            return new BoundaryFactPayload
            {
                SchemaVersion = 1,
                Kind = GasBoundaryPayloadKind.IntegerPair,
                Integer0 = (long)outcome.Outcome,
                Integer1 = ((long)(byte)outcome.Failure << 32) |
                           (uint)outcome.AppliedModifierCount,
                Integer2 = outcome.DefinitionId,
                StableId0 = outcome.ApplicationId,
                Scalar0 = outcome.DeathOverkill,
                StableId1 = outcome.DeathTransitionId,
                StableId2 = outcome.ActiveEffect.OwnerAsc.AscStableId,
                Generation0 = outcome.ActiveEffect.SlotGeneration,
                Generation1 = outcome.ActiveEffect.OwnerAsc.AscGeneration,
                Generation2 = outcome.ActiveEffect.OwnerAsc.AscGeneration,
            };
        }

        /// <summary>
        /// 将 period execution identity 与 due tick 编码为自包含 PeriodTick payload。
        /// </summary>
        private static BoundaryFactPayload CreatePeriodPayload(
            in GasApplicationOutcomeRecord outcome,
            in NativeArray<GasAttributeMutationOutcomeRecord> mutationOutcomes)
        {
            var totalCurrentDelta = 0f;
            var totalBaseDelta = 0f;
            if (outcome.AttributeMutationStart >= 0 &&
                outcome.AttributeMutationCount > 0 &&
                outcome.AttributeMutationStart <= mutationOutcomes.Length - outcome.AttributeMutationCount)
            {
                for (var index = 0; index < outcome.AttributeMutationCount; index++)
                {
                    var mutation = mutationOutcomes[outcome.AttributeMutationStart + index].Mutation;
                    totalCurrentDelta += mutation.AppliedCurrent - mutation.PreviousCurrent;
                    totalBaseDelta += mutation.AppliedBase - mutation.PreviousBase;
                }
            }
            return new BoundaryFactPayload
            {
                SchemaVersion = 1,
                Kind = GasBoundaryPayloadKind.IntegerPair,
                Integer0 = (long)outcome.PeriodExecutionOrdinal,
                Integer1 = outcome.DefinitionId,
                Integer2 = (long)outcome.Outcome,
                StableId0 = outcome.ApplicationId,
                StableId1 = outcome.CausalityId,
                StableId2 = outcome.ActiveEffect.OwnerAsc.AscStableId,
                Generation0 = outcome.ActiveEffect.SlotGeneration,
                Generation1 = outcome.ActiveEffect.OwnerAsc.AscGeneration,
                Scalar0 = totalBaseDelta,
                Scalar9 = totalCurrentDelta,
            };
        }

        /// <summary>
        /// 将 period ordinal 与 modifier ordinal 合成为稳定 int fact ordinal。
        /// </summary>
        private static int ComposePeriodFactOrdinal(uint periodOrdinal, int modifierOrdinal)
        {
            var ordinal = (long)periodOrdinal * 1024L + modifierOrdinal;
            return ordinal > int.MaxValue ? int.MaxValue : (int)ordinal;
        }

        /// <summary>
        /// 将 modifier 的 requested、unclamped、applied 与 revision 快照编码为 AttributeDelta payload。
        /// </summary>
        private static BoundaryFactPayload CreateMutationPayload(
            in GasAttributeMutationOutcomeRecord outcome,
            ref GasDefinitionCatalogBlob catalog)
        {
            var mutation = outcome.Mutation;
            var death = outcome.DeathTransitionId;
            var attributeId = mutation.AttributeLayoutIndex >= 0 &&
                              mutation.AttributeLayoutIndex < catalog.AttributeLayout.Entries.Length
                ? catalog.AttributeLayout.Entries[mutation.AttributeLayoutIndex].AttributeId
                : 0;
            return new BoundaryFactPayload
            {
                SchemaVersion = 1,
                Kind = GasBoundaryPayloadKind.AttributeDelta,
                Integer0 = attributeId,
                Integer1 = ((long)(uint)mutation.AttributeLayoutIndex << 32) | mutation.Revision,
                Integer2 = outcome.DefinitionId,
                Scalar0 = mutation.RequestedBaseDelta,
                Scalar1 = mutation.RequestedCurrentDelta,
                Scalar2 = mutation.PreviousBase,
                Scalar3 = mutation.PreviousCurrent,
                Scalar4 = mutation.UnclampedBase,
                Scalar5 = mutation.UnclampedCurrent,
                Scalar6 = mutation.AppliedBase,
                Scalar7 = mutation.AppliedCurrent,
                Scalar8 = mutation.AppliedBase - mutation.PreviousBase,
                Scalar9 = mutation.AppliedCurrent - mutation.PreviousCurrent,
                StableId0 = outcome.ApplicationId,
                StableId1 = death,
                StableId2 = outcome.ContributorId,
                Generation0 = outcome.DeathCrossed,
                Generation1 = mutation.PreviousRevision,
            };
        }

        /// <summary>
        /// 将首个 Health crossing 的 transition、killer contributor 与 overkill 固化为独立 Death fact。
        /// </summary>
        private static BoundaryFactPayload CreateDeathPayload(
            in GasApplicationOutcomeRecord outcome,
            ref GasDefinitionCatalogBlob catalog)
        {
            var attributeId = outcome.DeathAttributeLayoutIndex >= 0 &&
                              outcome.DeathAttributeLayoutIndex < catalog.AttributeLayout.Entries.Length
                ? catalog.AttributeLayout.Entries[outcome.DeathAttributeLayoutIndex].AttributeId
                : 0;
            return new BoundaryFactPayload
            {
                SchemaVersion = 1,
                Kind = GasBoundaryPayloadKind.Death,
                Integer0 = attributeId,
                Integer1 = outcome.DeathAttributeLayoutIndex,
                Integer2 = outcome.DefinitionId,
                Scalar0 = outcome.DeathOverkill,
                StableId0 = outcome.DeathTransitionId,
                StableId1 = outcome.ApplicationId,
                StableId2 = outcome.DeathContributorId,
                Generation0 = 1,
            };
        }
    }

    /// <summary>
    /// 使用稳定 semantic key 合并 CoreFact，并将终局变更冻结为不写 authority 的发布 intent。
    /// </summary>
    internal struct GasStableFactMergeTerminalPrepareJob : IJob
    {
        public Entity Session;
        public ulong SimulationEpoch;
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public ComponentLookup<AscBattleMembership> Memberships;
        [ReadOnly] public NativeArray<GasTargetShadowState> TargetShadows;
        [ReadOnly] public ComponentLookup<GasSessionLifecycle> SessionLifecycles;
        [ReadOnly] public BufferLookup<BattleInstanceSlot> Battles;
        [ReadOnly] public ComponentLookup<GasFinalPublishFaultInjection> FaultInjections;
        public NativeArray<GasTerminalBattlePublishIntent> BattleIntents;
        public NativeArray<GasSessionLifecyclePublishIntent> SessionLifecycleIntent;
        public NativeArray<GasCoreFactRecord> CoreFacts;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 对已稳定的 CoreFact 做 bounded insertion sort，形成 BoundaryProject 唯一输入序列。
        /// </summary>
        public void Execute()
        {
            if (!GasTickJobUtility.EnterDownstream(
                    Admission, Execution, GasTickLaneMask.FactMergeTerminal))
                return;

            var execution = Execution[0];
            ClearIntents();
            for (var index = 1; index < execution.CoreFactCount; index++)
            {
                var value = CoreFacts[index];
                var position = index - 1;
                while (position >= 0)
                {
                    var previous = CoreFacts[position];
                    if (Compare(in previous, in value) <= 0)
                        break;
                    CoreFacts[position + 1] = CoreFacts[position];
                    position--;
                }
                CoreFacts[position + 1] = value;
            }

            if (!TryResolveTerminalOutcomes(ref execution))
            {
                Execution[0] = execution;
                return;
            }
            for (var index = 1; index < execution.CoreFactCount; index++)
            {
                var value = CoreFacts[index];
                var position = index - 1;
                while (position >= 0)
                {
                    var previous = CoreFacts[position];
                    if (Compare(in previous, in value) <= 0)
                        break;
                    CoreFacts[position + 1] = CoreFacts[position];
                    position--;
                }
                CoreFacts[position + 1] = value;
            }
            Execution[0] = execution;
        }

        /// <summary>
        /// 清空当前 Tick 的 terminal publish intent，保证直接 Job 测试重用 scratch 时不继承旧值。
        /// </summary>
        private void ClearIntents()
        {
            for (var index = 0; index < BattleIntents.Length; index++)
                BattleIntents[index] = default;
            if (SessionLifecycleIntent.Length > 0)
                SessionLifecycleIntent[0] = default;
        }

        /// <summary>
        /// 先完成所有战局的终局预检，再冻结 Battle/Session intent 与唯一 BattleOutcome。
        /// </summary>
        private bool TryResolveTerminalOutcomes(ref GasTickExecutionState execution)
        {
            if (!Battles.HasBuffer(Session))
                return true;

            var battles = Battles[Session];
            if (battles.Length > BattleIntents.Length || SessionLifecycleIntent.Length < 1)
            {
                execution.PostAdmissionFailure =
                    GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                return false;
            }
            var outcomeCount = CountTerminalOutcomes(in battles, out var invalid);
            if (invalid || outcomeCount < 0 || execution.CoreFactCount > CoreFacts.Length - outcomeCount)
            {
                execution.PostAdmissionFailure = GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                return false;
            }

            for (var index = 0; index < battles.Length; index++)
            {
                var battle = battles[index];
                if (!TryReadTerminalStats(in battle, out var stats, out var isTerminal, out invalid))
                {
                    if (invalid)
                    {
                        execution.PostAdmissionFailure =
                            GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                        return false;
                    }
                    continue;
                }
                if (!isTerminal)
                    continue;

                battle.State = GasBattleInstanceState.Terminal;
                battle.IngressClosed = 1;
                battle.OutcomeCode = stats.OutcomeCode;
                battle.State = GasBattleInstanceState.OutcomeFrozen;
                BattleIntents[index] = new GasTerminalBattlePublishIntent
                {
                    BattleIndex = index,
                    Value = battle,
                    Used = 1,
                };
                CoreFacts[execution.CoreFactCount++] = CreateOutcomeFact(
                    in battle, in stats, execution.CandidateTick, index);
            }
            PrepareSessionLifecycleIfAllBattlesFrozen(in battles);
            if (ShouldInjectFailure(outcomeCount))
            {
                execution.PostAdmissionFailure =
                    GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                return false;
            }
            return true;
        }

        /// <summary>
        /// 仅当当前 Session 的全部活动战局投影后已冻结终局时准备 Session Terminalizing intent。
        /// </summary>
        private void PrepareSessionLifecycleIfAllBattlesFrozen(
            in DynamicBuffer<BattleInstanceSlot> battles)
        {
            if (!SessionLifecycles.HasComponent(Session))
                return;

            var lifecycle = SessionLifecycles[Session];
            if (lifecycle.State != GasSessionLifecycleState.Ready &&
                lifecycle.State != GasSessionLifecycleState.Running)
                return;

            var hasActiveBattle = false;
            for (var index = 0; index < battles.Length; index++)
            {
                var battle = BattleIntents[index].Used != 0
                    ? BattleIntents[index].Value
                    : battles[index];
                if (battle.Header.StorageState != GasSlabSlotState.Live ||
                    battle.State == GasBattleInstanceState.Free ||
                    battle.State == GasBattleInstanceState.Tombstone)
                    continue;

                hasActiveBattle = true;
                if (battle.State != GasBattleInstanceState.OutcomeFrozen)
                    return;
            }

            if (!hasActiveBattle)
                return;
            lifecycle.State = GasSessionLifecycleState.Terminalizing;
            SessionLifecycleIntent[0] = new GasSessionLifecyclePublishIntent
            {
                Value = lifecycle,
                Used = 1,
            };
        }

        /// <summary>
        /// 在 terminal intent 已完整冻结后触发确定性测试故障。
        /// </summary>
        private bool ShouldInjectFailure(int preparedIntentCount)
        {
            if (!FaultInjections.HasComponent(Session))
                return false;
            var injection = FaultInjections[Session];
            return injection.Stage == GasFinalPublishPrepareStage.Terminal &&
                   injection.FailAfterPreparedIntentCount > 0 &&
                   preparedIntentCount >= injection.FailAfterPreparedIntentCount;
        }

        /// <summary>
        /// 统计需要产出终局事实的战局数量，并在 Ready registry 结构损坏时返回显式 invalid。
        /// </summary>
        private int CountTerminalOutcomes(
            in DynamicBuffer<BattleInstanceSlot> battles,
            out bool invalid)
        {
            invalid = false;
            var count = 0;
            for (var index = 0; index < battles.Length; index++)
            {
                var battle = battles[index];
                if (!TryReadTerminalStats(in battle, out _, out var isTerminal, out var battleInvalid))
                {
                    invalid |= battleInvalid;
                    continue;
                }
                if (isTerminal)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// 按 BattleInstance 的 Ready registry 成员独立统计阵营与 ASC lifecycle。
        /// </summary>
        private bool TryReadTerminalStats(
            in BattleInstanceSlot battle,
            out GasBattleOutcomeStats stats,
            out bool isTerminal,
            out bool invalid)
        {
            stats = default;
            isTerminal = false;
            invalid = false;
            if (battle.Header.StorageState != GasSlabSlotState.Live ||
                (battle.State != GasBattleInstanceState.Ready &&
                 battle.State != GasBattleInstanceState.Running) ||
                battle.IngressClosed != 0)
                return true;
            if (!battle.Handle.IsValid || battle.Handle.SimulationEpoch != SimulationEpoch ||
                battle.BattleInstanceId != battle.Handle.BattleStableId ||
                battle.Header.Generation != battle.Handle.BattleGeneration ||
                battle.MemberStart < 0 || battle.MemberCount <= 0 ||
                !Registries.HasBuffer(Session))
            {
                invalid = true;
                return false;
            }

            var registry = Registries[Session];
            if (battle.MemberStart > registry.Length ||
                battle.MemberCount > registry.Length - battle.MemberStart)
            {
                invalid = true;
                return false;
            }

            var readyCount = 0;
            var factionCount = 0;
            var aliveFactionCount = 0;
            var aliveMemberCount = 0;
            var winnerSideId = 0;
            var winnerTeamId = 0;
            for (var offset = 0; offset < battle.MemberCount; offset++)
            {
                if (!TryReadMember(in battle, in registry, offset, out var asc,
                        out var membership, out var alive))
                {
                    invalid = true;
                    return false;
                }

                readyCount++;
                if (alive)
                {
                    aliveMemberCount++;
                    winnerSideId = membership.SideId;
                    winnerTeamId = membership.TeamId;
                }
                if (!HasPriorFaction(in registry, battle.MemberStart, offset, in membership))
                    factionCount++;
                if (alive && !HasPriorAliveFaction(
                        in registry, battle.MemberStart, offset, in membership))
                    aliveFactionCount++;
            }

            if (battle.ReadyMemberCount != 0 && readyCount != battle.ReadyMemberCount)
            {
                invalid = true;
                return false;
            }
            stats = new GasBattleOutcomeStats
            {
                OutcomeCode = aliveFactionCount == 1
                    ? GasBattleOutcomeCode.Winner
                    : GasBattleOutcomeCode.Draw,
                WinnerSideId = aliveFactionCount == 1 ? winnerSideId : 0,
                WinnerTeamId = aliveFactionCount == 1 ? winnerTeamId : 0,
                FactionCount = factionCount,
                AliveFactionCount = aliveFactionCount,
                AliveMemberCount = aliveMemberCount,
            };
            isTerminal = factionCount > 1 && aliveFactionCount <= 1;
            return true;
        }

        /// <summary>
        /// 校验一个 Ready registry 成员的 stable identity、membership 与 lifecycle。
        /// </summary>
        private bool TryReadMember(
            in BattleInstanceSlot battle,
            in DynamicBuffer<AscRegistrySlot> registry,
            int offset,
            out Entity asc,
            out AscBattleMembership membership,
            out bool alive)
        {
            var registryIndex = battle.MemberStart + offset;
            var slot = registry[registryIndex];
            if (slot.State != GasAscRegistryState.Ready || slot.BattleInstance != battle.Handle)
            {
                asc = Entity.Null;
                membership = default;
                alive = false;
                return false;
            }
            asc = slot.ResolveRuntimeEntity();
            if (!AscIdentities.HasComponent(asc) || !Memberships.HasComponent(asc) ||
                registryIndex < 0 || registryIndex >= TargetShadows.Length)
            {
                membership = default;
                alive = false;
                return false;
            }
            var identity = AscIdentities[asc];
            membership = Memberships[asc];
            var shadow = TargetShadows[registryIndex];
            if (identity.SimulationEpoch != SimulationEpoch ||
                !identity.OwnerAsc.Equals(slot.OwnerAsc) ||
                membership.BattleInstance != battle.Handle ||
                shadow.Prepared == 0 || shadow.Target != asc ||
                !shadow.OwnerAsc.Equals(slot.OwnerAsc))
            {
                alive = false;
                return false;
            }
            alive = IsAlive(shadow.Lifecycle.State);
            return true;
        }

        /// <summary>
        /// 判断当前成员的 SideId/TeamId 是否已在前序 Ready registry 成员中出现。
        /// </summary>
        private bool HasPriorFaction(
            in DynamicBuffer<AscRegistrySlot> registry,
            int memberStart,
            int offset,
            in AscBattleMembership membership)
        {
            for (var prior = 0; prior < offset; prior++)
            {
                var asc = registry[memberStart + prior].ResolveRuntimeEntity();
                if (!Memberships.HasComponent(asc))
                    continue;
                var priorMembership = Memberships[asc];
                if (priorMembership.SideId == membership.SideId &&
                    priorMembership.TeamId == membership.TeamId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 判断前序成员中是否已有同阵营且仍存活的 ASC。
        /// </summary>
        private bool HasPriorAliveFaction(
            in DynamicBuffer<AscRegistrySlot> registry,
            int memberStart,
            int offset,
            in AscBattleMembership membership)
        {
            for (var prior = 0; prior < offset; prior++)
            {
                var registryIndex = memberStart + prior;
                var asc = registry[registryIndex].ResolveRuntimeEntity();
                if (!Memberships.HasComponent(asc) ||
                    registryIndex < 0 || registryIndex >= TargetShadows.Length)
                    continue;
                var shadow = TargetShadows[registryIndex];
                if (shadow.Prepared == 0 || shadow.Target != asc ||
                    !shadow.OwnerAsc.Equals(registry[registryIndex].OwnerAsc) ||
                    !IsAlive(shadow.Lifecycle.State))
                    continue;
                var priorMembership = Memberships[asc];
                if (priorMembership.SideId == membership.SideId &&
                    priorMembership.TeamId == membership.TeamId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 仅把 Ready/Alive lifecycle 视为仍有资格获胜的业务成员。
        /// </summary>
        private static bool IsAlive(GasAscLifecycleState state)
        {
            return state == GasAscLifecycleState.Ready || state == GasAscLifecycleState.Alive;
        }

        /// <summary>
        /// 创建带显式 BattleInstance handle 的唯一 BattleOutcome CoreFact。
        /// </summary>
        private static GasCoreFactRecord CreateOutcomeFact(
            in BattleInstanceSlot battle,
            in GasBattleOutcomeStats stats,
            ulong simulationTick,
            int battleOrdinal)
        {
            return new GasCoreFactRecord
            {
                Scope = GasBoundaryFactScope.BattleInstance,
                Plane = GasBoundaryFactPlane.Gameplay,
                Kind = GasBoundaryFactKind.BattleOutcome,
                BattleInstance = battle.Handle,
                SimulationTick = simulationTick,
                SemanticPhaseOrdinal = 9,
                WorkClassOrdinal = 1,
                SemanticId = battle.BattleInstanceId,
                Payload = new BoundaryFactPayload
                {
                    SchemaVersion = 1,
                    Kind = GasBoundaryPayloadKind.IntegerPair,
                    Integer0 = stats.OutcomeCode,
                    Integer1 = stats.WinnerSideId,
                    Integer2 = stats.WinnerTeamId,
                    StableId0 = battle.Handle.BattleStableId,
                    StableId1 = (ulong)stats.AliveMemberCount,
                    StableId2 = (ulong)stats.AliveFactionCount,
                    Generation0 = battle.Handle.BattleGeneration,
                    Generation1 = (uint)stats.FactionCount,
                },
                OperationOrdinal = battleOrdinal,
                FactOrdinal = 0,
            };
        }

        /// <summary>
        /// 保存一个 BattleInstance 的 faction/liveness 统计结果。
        /// </summary>
        private struct GasBattleOutcomeStats
        {
            public int OutcomeCode;
            public int WinnerSideId;
            public int WinnerTeamId;
            public int FactionCount;
            public int AliveFactionCount;
            public int AliveMemberCount;
        }

        /// <summary>
        /// 比较 scope、target、semantic phase、work class 与 semantic id 的完整稳定键。
        /// </summary>
        private static int Compare(in GasCoreFactRecord left, in GasCoreFactRecord right)
        {
            var comparison = left.Scope.CompareTo(right.Scope);
            if (comparison != 0)
                return comparison;
            comparison = left.Plane.CompareTo(right.Plane);
            if (comparison != 0)
                return comparison;
            comparison = left.BattleInstance.SimulationEpoch.CompareTo(
                right.BattleInstance.SimulationEpoch);
            if (comparison != 0)
                return comparison;
            comparison = left.BattleInstance.BattleStableId.CompareTo(
                right.BattleInstance.BattleStableId);
            if (comparison != 0)
                return comparison;
            comparison = left.BattleInstance.BattleGeneration.CompareTo(
                right.BattleInstance.BattleGeneration);
            if (comparison != 0)
                return comparison;
            comparison = left.TargetAsc.AscStableId.CompareTo(right.TargetAsc.AscStableId);
            if (comparison != 0)
                return comparison;
            comparison = left.TargetAsc.AscGeneration.CompareTo(right.TargetAsc.AscGeneration);
            if (comparison != 0)
                return comparison;
            comparison = left.SimulationTick.CompareTo(right.SimulationTick);
            if (comparison != 0)
                return comparison;
            comparison = left.SemanticPhaseOrdinal.CompareTo(right.SemanticPhaseOrdinal);
            if (comparison != 0)
                return comparison;
            comparison = left.WorkClassOrdinal.CompareTo(right.WorkClassOrdinal);
            if (comparison != 0)
                return comparison;
            comparison = left.SemanticId.CompareTo(right.SemanticId);
            if (comparison != 0)
                return comparison;
            comparison = left.OperationOrdinal.CompareTo(right.OperationOrdinal);
            return comparison != 0 ? comparison : left.FactOrdinal.CompareTo(right.FactOrdinal);
        }
    }

    /// <summary>
    /// 在唯一 final-publish token 成功后覆盖 TerminalPrepare 冻结的 Battle/Session authority。
    /// </summary>
    internal struct GasTerminalPublishJob : IJob
    {
        public Entity Session;
        [ReadOnly] public NativeArray<GasFinalPublishDecision> Decision;
        [ReadOnly] public NativeArray<GasTerminalBattlePublishIntent> BattleIntents;
        [ReadOnly] public NativeArray<GasSessionLifecyclePublishIntent> SessionLifecycleIntent;
        public BufferLookup<BattleInstanceSlot> Battles;
        public ComponentLookup<GasSessionLifecycle> SessionLifecycles;

        /// <summary>
        /// 只执行已冻结的索引覆盖，禁止重新解析终局或产生失败分支。
        /// </summary>
        public void Execute()
        {
            if (Decision[0].Succeeded == 0)
                return;
            for (var index = 0; index < BattleIntents.Length; index++)
            {
                var intent = BattleIntents[index];
                if (intent.Used != 0)
                    PublishBattle(in intent);
            }
            var sessionIntent = SessionLifecycleIntent[0];
            if (sessionIntent.Used != 0)
                SessionLifecycles[Session] = sessionIntent.Value;
        }

        /// <summary>
        /// 对冻结 Battle 索引执行单次覆盖。
        /// </summary>
        private void PublishBattle(in GasTerminalBattlePublishIntent intent)
        {
            var battles = Battles[Session];
            battles[intent.BattleIndex] = intent.Value;
        }
    }

    /// <summary>
    /// 按 projected lifecycle 模拟 T+1 route、destination allocation 与 source consume，全部变更只写稀疏 intent。
    /// </summary>
    internal struct GasNextTickRoutePrepareJob : IJob
    {
        public Entity Session;
        public ulong SimulationEpoch;
        [ReadOnly] public GasScaleProfile Profile;
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public NativeArray<GasTargetShadowState> TargetShadows;
        [ReadOnly] public ComponentLookup<AscSlabHeads> SlabHeads;
        [ReadOnly] public BufferLookup<PendingCommand> PendingCommands;
        [ReadOnly] public ComponentLookup<GasFinalPublishFaultInjection> FaultInjections;
        public NativeArray<GasPendingCommandSlotPublishIntent> SlotIntents;
        public NativeArray<GasPendingCommandHeadPublishIntent> HeadIntents;
        public NativeArray<GasAbilityRouteRecord> AbilityRoutes;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 按 canonical route 顺序冻结完整写集，任一 allocation/consume 异常均显式提升为 final-publish fatal。
        /// </summary>
        public void Execute()
        {
            if (!GasTickJobUtility.EnterDownstream(
                    Admission, Execution, GasTickLaneMask.RouteNextTick))
                return;
            ClearIntents();
            var execution = Execution[0];
            for (var index = 0; index < execution.AbilityRouteCount; index++)
            {
                var route = AbilityRoutes[index];
                if (route.Used == 0 || route.RequiresOwnerApply != 0)
                    continue;
                if (!TryPrepareRoute(in route, execution.CandidateTick))
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                    Execution[0] = execution;
                    return;
                }
                route.Used = 0;
                AbilityRoutes[index] = route;
            }
            if (ShouldInjectFailure(CountPreparedSlotIntents()))
                execution.PostAdmissionFailure =
                    GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
            Execution[0] = execution;
        }

        /// <summary>
        /// 清空当前 Tick route overlay，保证直接 Job 测试不继承旧 intent。
        /// </summary>
        private void ClearIntents()
        {
            for (var index = 0; index < SlotIntents.Length; index++)
                SlotIntents[index] = default;
            for (var index = 0; index < HeadIntents.Length; index++)
                HeadIntents[index] = default;
        }

        /// <summary>
        /// 为单条 route 冻结可选 owner-gone 回执、destination 分配与 source tombstone。
        /// </summary>
        private bool TryPrepareRoute(in GasAbilityRouteRecord route, ulong candidateTick)
        {
            var command = route.Command;
            var destination = route.DestinationAsc;
            var hasDestination = TryResolveRegisteredOwner(
                in destination, out var destinationAsc, out var destinationIndex,
                out var destinationState);
            var shouldEnqueue = hasDestination && IsDeliverable(destinationState);
            if (!shouldEnqueue &&
                GasAbilityWaitSlabTransaction.SupportsObservedOwnerGoneResponse(
                    command.CommandKind))
            {
                if (!GasAbilityWaitSlabTransaction.TryCreateObservedOwnerGoneResponse(
                        in command, candidateTick, out var ownerGoneResponse))
                    return false;
                command = ownerGoneResponse;
                destination = command.TargetAsc;
                hasDestination = TryResolveRegisteredOwner(
                    in destination, out destinationAsc, out destinationIndex,
                    out destinationState);
                shouldEnqueue = hasDestination && IsDeliverable(destinationState);
            }

            if (shouldEnqueue)
            {
                if (!TryHasEquivalentPendingCommand(
                        destinationAsc, destinationIndex, in command, out var duplicate))
                    return false;
                if (!duplicate && !TryEnqueue(
                        in destination, destinationAsc, destinationIndex, in command))
                    return false;
            }
            return TryConsumeForwardedCommand(in route);
        }

        /// <summary>
        /// 判断命令是否为 owner 可消费的 observed-owner-gone 终态回执。
        /// </summary>
        private static bool IsObservedOwnerGoneCompletion(in PendingCommand command)
        {
            return command.CommandKind ==
                       (int)GasAbilityPendingCommandKind.WaitCompletion &&
                   command.CompletionReason == GasAbilityWaitCompletionReason.ObservedOwnerGone;
        }

        /// <summary>
        /// 在 durable + 当前 overlay 上检测等价 owner-gone completion，避免重复交付。
        /// </summary>
        private bool TryHasEquivalentPendingCommand(
            Entity asc,
            int registryIndex,
            in PendingCommand candidate,
            out bool duplicate)
        {
            duplicate = false;
            if (!TryCreateStorage(asc, registryIndex, out var storage))
                return false;
            for (var index = 0; index < storage.Count; index++)
            {
                var command = storage.Read(index);
                if (storage.Failed != 0)
                    return false;
                if (command.Header.StorageState == GasSlabSlotState.Live &&
                    (command.State == GasSlotBusinessState.Pending ||
                     command.State == GasSlotBusinessState.Active) &&
                    IsObservedOwnerGoneCompletion(in command) &&
                    command.SourceAsc.Equals(candidate.SourceAsc) &&
                    command.TargetAsc.Equals(candidate.TargetAsc) &&
                    command.Activation.Equals(candidate.Activation) &&
                    command.Continuation.Equals(candidate.Continuation) &&
                    command.RegistrationGeneration == candidate.RegistrationGeneration &&
                    command.WaitSemantic == candidate.WaitSemantic &&
                    command.WaitPolicy == candidate.WaitPolicy)
                {
                    duplicate = true;
                    return true;
                }
            }
            return true;
        }

        /// <summary>
        /// 在稀疏 overlay 上执行一次 slab allocation，冻结完整 slot 和 PendingCommand head。
        /// </summary>
        private bool TryEnqueue(
            in OwnerAscHandle destination,
            Entity asc,
            int registryIndex,
            in PendingCommand frozen)
        {
            if (!TryGetPendingHead(asc, registryIndex, out var head) ||
                !TryCreateStorage(asc, registryIndex, out var storage))
                return false;
            if (GasNonCompactingSlabAllocator.TryAllocate(
                    ref head,
                    ref storage,
                    Profile.MaxPendingCommandCount,
                    out var allocation) != GasSlabStorageFailure.None ||
                storage.Failed != 0)
                return false;
            var command = frozen;
            command.Header = allocation.LiveHeader;
            command.TargetAsc = destination;
            command.State = GasSlotBusinessState.Pending;
            storage.Write(allocation.SlotIndex, in command);
            return storage.Failed == 0 && TryStorePendingHead(asc, registryIndex, in head);
        }

        /// <summary>
        /// 原始跨 ASC wait 转发时必须在 overlay 上精确消费 source 命令；不匹配禁止静默成功。
        /// </summary>
        private bool TryConsumeForwardedCommand(in GasAbilityRouteRecord route)
        {
            if (route.RequiresSourceConsume == 0)
                return true;
            if (route.OriginCommandSequence == 0 ||
                !IsForwardedWaitKind(route.OriginCommandKind))
                return false;
            if (!TryResolveRegisteredOwner(
                    in route.Command.SourceAsc, out var sourceAsc, out var sourceIndex, out _) ||
                !TryGetPendingHead(sourceAsc, sourceIndex, out var head) ||
                !TryCreateStorage(sourceAsc, sourceIndex, out var storage))
                return false;
            for (var index = 0; index < storage.Count; index++)
            {
                var command = storage.Read(index);
                if (storage.Failed != 0)
                    return false;
                if (command.Header.StorageState != GasSlabSlotState.Live ||
                    command.State != GasSlotBusinessState.Pending ||
                    command.CommandKind != route.OriginCommandKind ||
                    command.CommandSequence != route.OriginCommandSequence ||
                    !command.TargetAsc.Equals(route.DestinationAsc) ||
                    !command.Activation.Equals(route.Command.Activation) ||
                    !command.Continuation.Equals(route.Command.Continuation) ||
                    command.RegistrationGeneration != route.Command.RegistrationGeneration)
                    continue;
                if (GasNonCompactingSlabAllocator.TryMarkTombstone(
                        in head, ref storage, index) != GasSlabStorageFailure.None ||
                    storage.Failed != 0)
                    return false;
                command = storage.Read(index);
                if (storage.Failed != 0)
                    return false;
                command.State = GasSlotBusinessState.Terminal;
                storage.Write(index, in command);
                return storage.Failed == 0;
            }
            return false;
        }

        /// <summary>
        /// 只把 registration、signal、unsubscribe 原命令视为跨 ASC 可转发输入。
        /// </summary>
        private static bool IsForwardedWaitKind(int commandKind)
        {
            return commandKind == (int)GasAbilityPendingCommandKind.WaitRegistration ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitSignal ||
                   commandKind == (int)GasAbilityPendingCommandKind.WaitUnsubscribe;
        }

        /// <summary>
        /// 通过 Ready registry 与同 ordinal TargetShadow 解析 owner，禁止缺 shadow 时回退 durable lifecycle。
        /// </summary>
        private bool TryResolveRegisteredOwner(
            in OwnerAscHandle owner,
            out Entity asc,
            out int registryIndex,
            out GasAscLifecycleState state)
        {
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State != GasAscRegistryState.Ready || !slot.OwnerAsc.Equals(owner))
                    continue;
                asc = slot.ResolveRuntimeEntity();
                registryIndex = index;
                if (index >= TargetShadows.Length || index >= HeadIntents.Length ||
                    !AscIdentities.HasComponent(asc) || !SlabHeads.HasComponent(asc) ||
                    !PendingCommands.HasBuffer(asc))
                    break;
                var identity = AscIdentities[asc];
                var shadow = TargetShadows[index];
                if (identity.SimulationEpoch == SimulationEpoch &&
                    identity.OwnerAsc.Equals(owner) && shadow.Prepared != 0 &&
                    shadow.Target == asc && shadow.OwnerAsc.Equals(owner))
                {
                    state = shadow.Lifecycle.State;
                    return state == GasAscLifecycleState.Ready ||
                           state == GasAscLifecycleState.Alive ||
                           state == GasAscLifecycleState.Terminal ||
                           state == GasAscLifecycleState.Dead;
                }
                break;
            }
            asc = Entity.Null;
            registryIndex = -1;
            state = default;
            return false;
        }

        /// <summary>
        /// 只允许 projected Ready/Alive owner 接收下一 Tick 命令。
        /// </summary>
        private static bool IsDeliverable(GasAscLifecycleState state)
        {
            return state == GasAscLifecycleState.Ready || state == GasAscLifecycleState.Alive;
        }

        /// <summary>
        /// 读取当前 overlay 的 PendingCommand head，首次读取同时复验 durable slab 长度。
        /// </summary>
        private bool TryGetPendingHead(Entity asc, int registryIndex, out GasSlabHead head)
        {
            head = default;
            if (registryIndex < 0 || registryIndex >= HeadIntents.Length)
                return false;
            var intent = HeadIntents[registryIndex];
            if (intent.Used != 0)
            {
                if (intent.Asc != asc)
                    return false;
                head = intent.Value;
                return true;
            }
            head = SlabHeads[asc].PendingCommand;
            return PendingCommands[asc].Length == head.HighWater;
        }

        /// <summary>
        /// 冻结单 ASC 最终 PendingCommand head，并证明 publisher Resize 不会扩容。
        /// </summary>
        private bool TryStorePendingHead(Entity asc, int registryIndex, in GasSlabHead head)
        {
            if (registryIndex < 0 || registryIndex >= HeadIntents.Length ||
                PendingCommands[asc].Capacity < head.HighWater)
                return false;
            HeadIntents[registryIndex] = new GasPendingCommandHeadPublishIntent
            {
                Asc = asc,
                Value = head,
                Used = 1,
            };
            return true;
        }

        /// <summary>
        /// 创建一个以 durable buffer 为基线、以稀疏 slot intent 为覆盖层的 slab storage。
        /// </summary>
        private bool TryCreateStorage(
            Entity asc,
            int registryIndex,
            out PendingCommandOverlayStorage storage)
        {
            storage = default;
            if (!TryGetPendingHead(asc, registryIndex, out var head))
                return false;
            storage = new PendingCommandOverlayStorage
            {
                Asc = asc,
                Durable = PendingCommands[asc],
                Intents = SlotIntents,
                LogicalCount = head.HighWater,
            };
            return true;
        }

        /// <summary>
        /// 统计已冻结 slot intent，供确定性 fault injection 选择断点。
        /// </summary>
        private int CountPreparedSlotIntents()
        {
            var count = 0;
            for (var index = 0; index < SlotIntents.Length; index++)
                count += SlotIntents[index].Used != 0 ? 1 : 0;
            return count;
        }

        /// <summary>
        /// 在 route intent 已完整冻结后触发确定性测试故障。
        /// </summary>
        private bool ShouldInjectFailure(int preparedIntentCount)
        {
            if (!FaultInjections.HasComponent(Session))
                return false;
            var injection = FaultInjections[Session];
            return injection.Stage == GasFinalPublishPrepareStage.Route &&
                   injection.FailAfterPreparedIntentCount > 0 &&
                   preparedIntentCount >= injection.FailAfterPreparedIntentCount;
        }

        /// <summary>
        /// 以稀疏 intent 实现 allocator 所需的槽头存储，不触碰 durable PendingCommand。
        /// </summary>
        private struct PendingCommandOverlayStorage : IGasSlabHeaderStorage
        {
            public Entity Asc;
            public DynamicBuffer<PendingCommand> Durable;
            public NativeArray<GasPendingCommandSlotPublishIntent> Intents;
            public int LogicalCount;
            public byte Failed;
            public int Count => LogicalCount;

            /// <summary>
            /// 优先读取当前 ASC/slot 的 intent，未覆盖时读 durable 基线。
            /// </summary>
            public PendingCommand Read(int slotIndex)
            {
                for (var index = 0; index < Intents.Length; index++)
                {
                    var intent = Intents[index];
                    if (intent.Used != 0 && intent.Asc == Asc && intent.SlotIndex == slotIndex)
                        return intent.Value;
                }
                if (slotIndex >= 0 && slotIndex < Durable.Length)
                    return Durable[slotIndex];
                Failed = 1;
                return default;
            }

            /// <summary>
            /// 覆盖已有 intent 或占用一个新的稀疏槽位。
            /// </summary>
            public void Write(int slotIndex, in PendingCommand value)
            {
                var freeIndex = -1;
                for (var index = 0; index < Intents.Length; index++)
                {
                    var intent = Intents[index];
                    if (intent.Used != 0 && intent.Asc == Asc && intent.SlotIndex == slotIndex)
                    {
                        intent.Value = value;
                        Intents[index] = intent;
                        return;
                    }
                    if (intent.Used == 0 && freeIndex < 0)
                        freeIndex = index;
                }
                if (freeIndex < 0)
                {
                    Failed = 1;
                    return;
                }
                Intents[freeIndex] = new GasPendingCommandSlotPublishIntent
                {
                    Asc = Asc,
                    SlotIndex = slotIndex,
                    Value = value,
                    Used = 1,
                };
            }

            /// <summary>
            /// 读取指定稳定索引的槽头。
            /// </summary>
            public GasSlabSlotHeader ReadHeader(int slotIndex)
            {
                return Read(slotIndex).Header;
            }

            /// <summary>
            /// 只更新指定槽的物理头。
            /// </summary>
            public void WriteHeader(int slotIndex, in GasSlabSlotHeader header)
            {
                var slot = Read(slotIndex);
                if (Failed != 0)
                    return;
                slot.Header = header;
                Write(slotIndex, in slot);
            }

            /// <summary>
            /// 在 overlay high-water 尾部追加一个新槽头。
            /// </summary>
            public void AppendHeader(in GasSlabSlotHeader header)
            {
                var slot = new PendingCommand { Header = header };
                Write(LogicalCount, in slot);
                if (Failed == 0)
                    LogicalCount++;
            }
        }
    }

    /// <summary>
    /// 在唯一 final-publish token 成功后发布 RoutePrepare 冻结的 PendingCommand 槽与 head。
    /// </summary>
    internal struct GasNextTickRoutePublishJob : IJob
    {
        [ReadOnly] public NativeArray<GasFinalPublishDecision> Decision;
        [ReadOnly] public NativeArray<GasPendingCommandSlotPublishIntent> SlotIntents;
        [ReadOnly] public NativeArray<GasPendingCommandHeadPublishIntent> HeadIntents;
        public ComponentLookup<AscSlabHeads> SlabHeads;
        public BufferLookup<PendingCommand> PendingCommands;

        /// <summary>
        /// 只执行已冻结的 resize/槽覆盖/head 覆盖，禁止重新调用 allocator 或 owner resolve。
        /// </summary>
        public void Execute()
        {
            if (Decision[0].Succeeded == 0)
                return;
            for (var index = 0; index < HeadIntents.Length; index++)
            {
                var intent = HeadIntents[index];
                if (intent.Used == 0)
                    continue;
                var commands = PendingCommands[intent.Asc];
                commands.ResizeUninitialized(intent.Value.HighWater);
            }
            for (var index = 0; index < SlotIntents.Length; index++)
            {
                var intent = SlotIntents[index];
                if (intent.Used == 0)
                    continue;
                var commands = PendingCommands[intent.Asc];
                commands[intent.SlotIndex] = intent.Value;
            }
            for (var index = 0; index < HeadIntents.Length; index++)
            {
                var intent = HeadIntents[index];
                if (intent.Used == 0)
                    continue;
                var heads = SlabHeads[intent.Asc];
                heads.PendingCommand = intent.Value;
                SlabHeads[intent.Asc] = heads;
            }
        }
    }

    /// <summary>
    /// 把稳定 CoreFact 投影为冻结 owner/EventId/DrainState 的 Boundary publish intent。
    /// </summary>
    internal struct GasBoundaryProjectPrepareJob : IJob
    {
        public Entity Session;
        public ulong SimulationEpoch;
        [ReadOnly] public NativeArray<GasCoreFactRecord> CoreFacts;
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public NativeArray<GasTargetShadowState> TargetShadows;
        [ReadOnly] public ComponentLookup<AscBattleMembership> Memberships;
        [ReadOnly] public BufferLookup<BattleInstanceSlot> Battles;
        [ReadOnly] public ComponentLookup<BoundaryDrainState> Drains;
        [ReadOnly] public BufferLookup<BoundaryFactBuffer> BoundaryFacts;
        [ReadOnly] public ComponentLookup<GasFinalPublishFaultInjection> FaultInjections;
        public NativeArray<GasBoundaryFactPublishIntent> BoundaryIntents;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 按稳定序列纯计算每条 Boundary fact 与 state-after，任何异常均不写 authority。
        /// </summary>
        public void Execute()
        {
            if (!GasTickJobUtility.EnterDownstream(
                    Admission, Execution, GasTickLaneMask.BoundaryProject))
                return;

            var execution = Execution[0];
            execution.BoundaryFactCount = 0;
            ClearIntents();
            if (!TryPrepareIntents(in execution, out var preparedCount))
            {
                execution.PostAdmissionFailure =
                    GasTickAdmissionFailureReason.BoundaryProjectionFailure;
                Execution[0] = execution;
                return;
            }
            if (ShouldInjectFailure(preparedCount))
            {
                execution.PostAdmissionFailure =
                    GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
            }
            Execution[0] = execution;
        }

        /// <summary>
        /// 清空当前 Tick boundary intent，保证直接 Job 测试不继承旧值。
        /// </summary>
        private void ClearIntents()
        {
            for (var index = 0; index < BoundaryIntents.Length; index++)
                BoundaryIntents[index] = default;
        }

        /// <summary>
        /// 校验 canonical key 并顺序冻结每条 fact 的物理 owner、EventId 与 state-after。
        /// </summary>
        private bool TryPrepareIntents(
            in GasTickExecutionState execution,
            out int preparedCount)
        {
            preparedCount = 0;
            if (execution.CoreFactCount < 0 || execution.CoreFactCount > CoreFacts.Length ||
                execution.CoreFactCount > BoundaryIntents.Length)
                return false;
            for (var index = 0; index < execution.CoreFactCount; index++)
            {
                if (index > 0)
                {
                    var previous = CoreFacts[index - 1];
                    var current = CoreFacts[index];
                    if (CompareCanonicalKey(in previous, in current) >= 0)
                        return false;
                }
                var coreFact = CoreFacts[index];
                if (!TryResolveProjectionTarget(
                        in coreFact, out var target, out var membership) ||
                    !Drains.HasComponent(target) || !BoundaryFacts.HasBuffer(target) ||
                    !TryGetProjectedDrainState(target, index, out var projected, out var priorCount))
                    return false;
                var outbox = BoundaryFacts[target];
                if (priorCount < 0 || outbox.Length > int.MaxValue - priorCount - 1 ||
                    outbox.Capacity < outbox.Length + priorCount + 1 ||
                    !TryBuildBoundaryFact(
                        in coreFact, in projected, in membership, out var template) ||
                    !GasBoundaryDrainProtocol.TryPrepareFactAppend(
                        in projected,
                        in template,
                        out var preparedFact,
                        out var stateAfter,
                        out _))
                    return false;
                BoundaryIntents[index] = new GasBoundaryFactPublishIntent
                {
                    Owner = target,
                    Fact = preparedFact,
                    StateAfter = stateAfter,
                    Used = 1,
                };
                preparedCount++;
            }
            return true;
        }

        /// <summary>
        /// 读取同 owner 最后一条 intent 的 state-after，未出现时使用 durable 基线。
        /// </summary>
        private bool TryGetProjectedDrainState(
            Entity target,
            int currentIndex,
            out BoundaryDrainState state,
            out int priorCount)
        {
            state = Drains[target];
            priorCount = 0;
            for (var index = 0; index < currentIndex; index++)
            {
                var intent = BoundaryIntents[index];
                if (intent.Used == 0 || intent.Owner != target)
                    continue;
                state = intent.StateAfter;
                priorCount++;
            }
            return true;
        }

        /// <summary>
        /// 在 boundary intent 已完整冻结后触发确定性测试故障。
        /// </summary>
        private bool ShouldInjectFailure(int preparedIntentCount)
        {
            if (!FaultInjections.HasComponent(Session))
                return false;
            var injection = FaultInjections[Session];
            return injection.Stage == GasFinalPublishPrepareStage.Boundary &&
                   injection.FailAfterPreparedIntentCount > 0 &&
                   preparedIntentCount >= injection.FailAfterPreparedIntentCount;
        }

        /// <summary>
        /// 将 CoreFact 转为带 scope identity 与 membership 快照的 Boundary fact。
        /// </summary>
        private static bool TryBuildBoundaryFact(
            in GasCoreFactRecord coreFact,
            in BoundaryDrainState state,
            in AscBattleMembership membership,
            out BoundaryFactBuffer fact)
        {
            var scopeStableId = state.OwnerStableId;
            var scopeGeneration = state.OwnerGeneration;
            var battleInstanceId = 0UL;
            var battleInstanceGeneration = 0U;
            if (coreFact.Scope == GasBoundaryFactScope.Asc)
            {
                battleInstanceId = membership.BattleInstance.BattleStableId;
                battleInstanceGeneration = membership.BattleInstance.BattleGeneration;
            }
            else if (coreFact.Scope == GasBoundaryFactScope.BattleInstance)
            {
                var battle = coreFact.BattleInstance.IsValid
                    ? coreFact.BattleInstance
                    : membership.BattleInstance;
                if (!battle.IsValid || battle.SimulationEpoch != state.SimulationEpoch)
                {
                    fact = default;
                    return false;
                }

                scopeStableId = battle.BattleStableId;
                scopeGeneration = battle.BattleGeneration;
                battleInstanceId = battle.BattleStableId;
                battleInstanceGeneration = battle.BattleGeneration;
            }

            fact = new BoundaryFactBuffer
            {
                Scope = coreFact.Scope,
                Plane = coreFact.Plane,
                ScopeStableId = scopeStableId,
                ScopeGeneration = scopeGeneration,
                BattleInstanceId = battleInstanceId,
                BattleInstanceGeneration = battleInstanceGeneration,
                OwnerScenarioUnitId = coreFact.Scope == GasBoundaryFactScope.Asc
                    ? membership.ScenarioUnitId
                    : 0,
                SourceAsc = coreFact.SourceAsc,
                TargetAsc = coreFact.TargetAsc,
                SimulationTick = coreFact.SimulationTick,
                SemanticPhaseOrdinal = coreFact.SemanticPhaseOrdinal,
                WorkClassOrdinal = coreFact.WorkClassOrdinal,
                ParentCausalityId = coreFact.ParentCausalityId,
                SemanticId = coreFact.SemanticId,
                Kind = coreFact.Kind,
                Payload = coreFact.Payload,
            };
            return coreFact.Scope == GasBoundaryFactScope.Asc ||
                   coreFact.Scope == GasBoundaryFactScope.BattleInstance ||
                   coreFact.Scope == GasBoundaryFactScope.Session;
        }

        /// <summary>
        /// 按逻辑 scope 解析唯一物理 owner，并为 BattleInstance fact 校验显式战局身份。
        /// </summary>
        private bool TryResolveProjectionTarget(
            in GasCoreFactRecord coreFact,
            out Entity target,
            out AscBattleMembership membership)
        {
            if (coreFact.Scope == GasBoundaryFactScope.Session)
            {
                target = Session;
                membership = default;
                return true;
            }
            if (coreFact.Scope == GasBoundaryFactScope.Asc)
                return TryResolveOwner(coreFact.TargetAsc, out target, out membership);

            if (coreFact.Scope == GasBoundaryFactScope.BattleInstance &&
                TryResolveBattleInstance(in coreFact, out _, out membership))
            {
                target = Session;
                return true;
            }

            target = Entity.Null;
            membership = default;
            return false;
        }

        /// <summary>
        /// 校验 BattleInstance handle 属于当前 Session；若 fact 带 target 则同时复验其 membership。
        /// </summary>
        private bool TryResolveBattleInstance(
            in GasCoreFactRecord coreFact,
            out BattleInstanceHandle battle,
            out AscBattleMembership membership)
        {
            battle = coreFact.BattleInstance;
            membership = default;
            if (!battle.IsValid && coreFact.TargetAsc.IsValid)
            {
                if (!TryResolveOwner(coreFact.TargetAsc, out _, out membership))
                    return false;
                battle = membership.BattleInstance;
            }

            if (!battle.IsValid || battle.SimulationEpoch != SimulationEpoch ||
                !Battles.HasBuffer(Session))
                return false;

            if (coreFact.TargetAsc.IsValid)
            {
                if (!TryResolveOwner(coreFact.TargetAsc, out _, out var targetMembership) ||
                    targetMembership.BattleInstance != battle)
                    return false;
                membership = targetMembership;
            }

            var battles = Battles[Session];
            for (var index = 0; index < battles.Length; index++)
            {
                var slot = battles[index];
                if (slot.Header.StorageState != GasSlabSlotState.Live ||
                    slot.Header.Generation != battle.BattleGeneration ||
                    slot.Handle != battle ||
                    slot.BattleInstanceId != battle.BattleStableId ||
                    slot.Handle.BattleGeneration != battle.BattleGeneration)
                    continue;
                return slot.State != GasBattleInstanceState.Free &&
                       slot.State != GasBattleInstanceState.SpawnPending &&
                       slot.State != GasBattleInstanceState.Tombstone;
            }

            return false;
        }

        /// <summary>
        /// 比较 StableFactMerge 使用的完整 canonical key，拒绝重复或逆序输入。
        /// </summary>
        private static int CompareCanonicalKey(
            in GasCoreFactRecord left,
            in GasCoreFactRecord right)
        {
            var comparison = left.Scope.CompareTo(right.Scope);
            if (comparison != 0)
                return comparison;
            comparison = left.Plane.CompareTo(right.Plane);
            if (comparison != 0)
                return comparison;
            comparison = left.BattleInstance.SimulationEpoch.CompareTo(
                right.BattleInstance.SimulationEpoch);
            if (comparison != 0)
                return comparison;
            comparison = left.BattleInstance.BattleStableId.CompareTo(
                right.BattleInstance.BattleStableId);
            if (comparison != 0)
                return comparison;
            comparison = left.BattleInstance.BattleGeneration.CompareTo(
                right.BattleInstance.BattleGeneration);
            if (comparison != 0)
                return comparison;
            comparison = left.TargetAsc.AscStableId.CompareTo(right.TargetAsc.AscStableId);
            if (comparison != 0)
                return comparison;
            comparison = left.TargetAsc.AscGeneration.CompareTo(right.TargetAsc.AscGeneration);
            if (comparison != 0)
                return comparison;
            comparison = left.SimulationTick.CompareTo(right.SimulationTick);
            if (comparison != 0)
                return comparison;
            comparison = left.SemanticPhaseOrdinal.CompareTo(right.SemanticPhaseOrdinal);
            if (comparison != 0)
                return comparison;
            comparison = left.WorkClassOrdinal.CompareTo(right.WorkClassOrdinal);
            if (comparison != 0)
                return comparison;
            comparison = left.SemanticId.CompareTo(right.SemanticId);
            if (comparison != 0)
                return comparison;
            comparison = left.OperationOrdinal.CompareTo(right.OperationOrdinal);
            return comparison != 0
                ? comparison
                : left.FactOrdinal.CompareTo(right.FactOrdinal);
        }

        /// <summary>
        /// 通过 Session registry 与同 ordinal TargetShadow 解析 target，缺 shadow 时禁止回退 durable lifecycle。
        /// </summary>
        private bool TryResolveOwner(
            in OwnerAscHandle owner,
            out Entity asc,
            out AscBattleMembership membership)
        {
            if (!Registries.HasBuffer(Session))
            {
                asc = Entity.Null;
                membership = default;
                return false;
            }
            var registry = Registries[Session];
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                if (slot.State != GasAscRegistryState.Ready || !slot.OwnerAsc.Equals(owner))
                    continue;
                asc = slot.ResolveRuntimeEntity();
                if (index >= TargetShadows.Length || !AscIdentities.HasComponent(asc) ||
                    !Memberships.HasComponent(asc))
                    break;
                var identity = AscIdentities[asc];
                var shadow = TargetShadows[index];
                membership = Memberships[asc];
                if (identity.SimulationEpoch == SimulationEpoch &&
                    identity.OwnerAsc.Equals(owner) &&
                    shadow.Prepared != 0 && shadow.Target == asc &&
                    shadow.OwnerAsc.Equals(owner) &&
                    (shadow.Lifecycle.State == GasAscLifecycleState.Ready ||
                     shadow.Lifecycle.State == GasAscLifecycleState.Alive ||
                     shadow.Lifecycle.State == GasAscLifecycleState.Terminal ||
                     shadow.Lifecycle.State == GasAscLifecycleState.Dead))
                    return true;
                break;
            }
            asc = Entity.Null;
            membership = default;
            return false;
        }
    }

    /// <summary>
    /// 在第二次 final reduce 成功后把已准备的 RequestTerminal intent 追加到 Session durable buffer。
    /// </summary>
    internal struct GasRequestTerminalPublishJob : IJob
    {
        public Entity Session;
        [ReadOnly] public NativeArray<GasFinalPublishDecision> Decision;
        [ReadOnly] public NativeArray<GasRequestTerminalIntent> Intents;
        [ReadOnly] public NativeArray<GasTickExecutionState> Execution;
        public BufferLookup<GasRequestTerminalIntent> TerminalBuffers;

        /// <summary>
        /// 按 canonical sealed ordinal 无条件追加；容量与所有终态组合已在 admission/prepare 证明。
        /// </summary>
        public void Execute()
        {
            if (Decision[0].Succeeded == 0)
                return;
            var terminalBuffer = TerminalBuffers[Session];
            var count = Execution[0].StoredSealedCommandCount;
            for (var index = 0; index < count; index++)
                terminalBuffer.Add(Intents[index]);
        }
    }

    /// <summary>
    /// 在唯一 final-publish token 成功后追加已冻结 Boundary fact 并覆盖对应 state-after。
    /// </summary>
    internal struct GasBoundaryPublishJob : IJob
    {
        [ReadOnly] public NativeArray<GasFinalPublishDecision> Decision;
        [ReadOnly] public NativeArray<GasBoundaryFactPublishIntent> BoundaryIntents;
        public ComponentLookup<BoundaryDrainState> Drains;
        public BufferLookup<BoundaryFactBuffer> BoundaryFacts;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 只执行已冻结的 Add 与 state 覆盖，禁止重新 owner resolve、protocol 校验或 rollback。
        /// </summary>
        public void Execute()
        {
            if (Decision[0].Succeeded == 0)
                return;
            var publishedCount = 0;
            for (var index = 0; index < BoundaryIntents.Length; index++)
            {
                var intent = BoundaryIntents[index];
                if (intent.Used == 0)
                    continue;
                var outbox = BoundaryFacts[intent.Owner];
                outbox.Add(intent.Fact);
                Drains[intent.Owner] = intent.StateAfter;
                publishedCount++;
            }
            var execution = Execution[0];
            execution.BoundaryFactCount = publishedCount;
            Execution[0] = execution;
        }
    }

    /// <summary>
    /// 记录 admitted 结构 intent 到标准 EndFixed；阶段 C 没有合法业务 intent，仍固定预排该节点。
    /// </summary>
    internal struct GasRecordEndFixedJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 无条件进入 lane，admission 失败时立即 no-op。
        /// </summary>
        public void Execute()
        {
            GasTickJobUtility.EnterDownstream(Admission, Execution, GasTickLaneMask.RecordEndFixed);
        }
    }

    /// <summary>
    /// 仅在完整 DAG admission 成功后推进 Tick、消费 sealed inbox 并发布非权威诊断。
    /// </summary>
    internal struct GasTickFinalizeJob : IJob
    {
        public Entity Session;
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasTickExecutionState> Execution;
        public ComponentLookup<SimulationTickState> Ticks;
        public ComponentLookup<GasSessionLifecycle> Lifecycles;
        public ComponentLookup<GasTickDiagnostics> Diagnostics;
        public BufferLookup<BoundaryCommandInbox> Inboxes;

        /// <summary>
        /// 失败时不推进任何 gameplay authority；成功时原子提交 candidate tick 与 inbox consume 控制态。
        /// </summary>
        public void Execute()
        {
            var execution = Execution[0];
            if (execution.GameplayEnabled == 0)
                return;
            execution.LaneMask |= GasTickLaneMask.TickFinalize;
            var admission = Admission[0];
            if (admission.Succeeded != 0 &&
                execution.PostAdmissionFailure == GasTickAdmissionFailureReason.None)
            {
                var tick = Ticks[Session];
                tick.CurrentTick = admission.CandidateTick;
                Ticks[Session] = tick;
                PromoteRunningLifecycle();
                ConsumeSealedCommands(admission.CandidateTick);
            }
            PublishDiagnostics(in execution, in admission);
            Execution[0] = execution;
        }

        /// <summary>
        /// 首个成功 gameplay Tick 把 Ready 提升为 Running，其余 lifecycle 不在本节点解释。
        /// </summary>
        private void PromoteRunningLifecycle()
        {
            var lifecycle = Lifecycles[Session];
            if (lifecycle.State == GasSessionLifecycleState.Ready)
                lifecycle.State = GasSessionLifecycleState.Running;
            Lifecycles[Session] = lifecycle;
        }

        /// <summary>
        /// 只消费本 candidate tick 由 Gather seal 的条目，future tail 保持 Pending。
        /// </summary>
        private void ConsumeSealedCommands(ulong candidateTick)
        {
            var inbox = Inboxes[Session];
            for (var index = 0; index < inbox.Length; index++)
            {
                var command = inbox[index];
                if (command.State != GasBoundaryCommandState.Sealed || command.SealedTick != candidateTick)
                    continue;
                command.State = GasBoundaryCommandState.Consumed;
                inbox[index] = command;
            }
        }

        /// <summary>
        /// 发布 lane/admission 诊断供测试与 profiler 读取，但它不参与下一 Tick gameplay 判定。
        /// </summary>
        private void PublishDiagnostics(
            in GasTickExecutionState execution,
            in GasAdmissionResult admission)
        {
            Diagnostics[Session] = new GasTickDiagnostics
            {
                CandidateTick = execution.CandidateTick,
                ExecutedLaneMask = execution.LaneMask,
                AdmissionReasonCode = (int)admission.FailureReason,
                SealedCommandCount = execution.SealedCommandCount,
                SourceSpecCount = execution.SourceSpecCount,
                ApplicationOutcomeCount = execution.ApplicationOutcomeCount,
                AttributeMutationCount = execution.AttributeMutationCount,
                DeathFactCount = execution.DeathFactCount,
                CoreFactCount = execution.CoreFactCount,
                BoundaryFactCount = execution.BoundaryFactCount,
                AdmissionSucceeded = admission.Succeeded,
            };
        }
    }

    /// <summary>
    /// 集中实现 admission 后 lane 的统一入口协议，所有调用者都必须先记录 trace 再判断 no-op。
    /// </summary>
    internal static class GasTickJobUtility
    {
        /// <summary>
        /// 记录 lane 已执行入口；admission 失败时调用者不得继续任何 gameplay 写入。
        /// </summary>
        internal static bool EnterDownstream(
            NativeArray<GasAdmissionResult> admission,
            NativeArray<GasTickExecutionState> execution,
            ulong laneMask)
        {
            var state = execution[0];
            if (state.GameplayEnabled == 0)
                return false;
            state.LaneMask |= laneMask;
            execution[0] = state;
            return admission[0].Succeeded != 0 &&
                   state.PostAdmissionFailure == GasTickAdmissionFailureReason.None;
        }
    }
}
