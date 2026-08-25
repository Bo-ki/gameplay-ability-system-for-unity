using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

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
        public NativeArray<GasResolvedTargetRecord> ResolvedTargets;
        public NativeArray<GasEffectOperationRecord> EffectOperations;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 只接受显式 ASC target；selector/rule 暂无生成规则时以 typed no-op 留在 inbox 审计中。
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
                for (var index = 0; index < execution.StoredSealedCommandCount; index++)
                {
                    var command = SealedCommands[index].Command;
                    if (command.CommandKind != GasBoundaryCommandKind.ApplyEffect ||
                        index >= execution.OwnerPlanCount ||
                        OwnerPlans[index].BusinessAccepted == 0 ||
                        OwnerPlans[index].StableSequence == 0 ||
                        !GasDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                            ref catalog, command.DefinitionId, out var definitionIndex))
                        continue;
                    var definition = catalog.GameplayEffects[definitionIndex];
                    if (!TryResolveTarget(
                            in command.Target,
                            definition.TargetPolicy.Life,
                            out var targetAsc,
                            out var targetAlive))
                        continue;
                    var sourceAsc = command.HasSource != 0 ? command.SourceAsc : targetAsc;
                    if (!TryComposeApplicationId(
                            OwnerPlans[index].StableSequence,
                            0,
                            out var applicationId))
                    {
                        execution.PreAdmissionFailure =
                            GasTickAdmissionFailureReason.EnvelopeArithmeticOverflow;
                        break;
                    }
                    if (!TryAddOperation(
                            ref execution,
                            index,
                            0,
                            definitionIndex,
                            sourceAsc,
                            targetAsc,
                            applicationId,
                            execution.CandidateTick,
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
                        if (!GasDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                                ref catalog, node.EffectDefinitionId, out var definitionIndex))
                            continue;
                        var target = ResolveAbilityTarget(in plan, in ability);
                        if (!TryResolveTarget(
                                in target,
                                catalog.GameplayEffects[definitionIndex].TargetPolicy.Life,
                                out var targetAsc,
                                out var targetAlive))
                            continue;
                        if (!TryComposeApplicationId(
                                plan.StableSequence,
                                node.NodeOrdinal,
                                out var applicationId) ||
                            !TryAddOperation(
                                ref execution,
                                index,
                                node.NodeOrdinal,
                                definitionIndex,
                                plan.OwnerAsc,
                                targetAsc,
                                applicationId,
                                execution.CandidateTick,
                                targetAlive))
                            break;
                    }
                }
            }
            Execution[0] = execution;
        }

        /// <summary>
        /// 通过 Ready registry 解析一个稳定 ASC target，并冻结当前 target life snapshot。
        /// </summary>
        private BoundaryTargetRef ResolveAbilityTarget(
            in GasOwnerPlanRecord plan,
            in GasAbilityDefinitionBlob ability)
        {
            if (ability.TargetPolicy.LogicalTarget == GasLogicalTargetPolicy.Self)
            {
                var registry = Registries[Session];
                for (var index = 0; index < registry.Length; index++)
                {
                    var slot = registry[index];
                    if (!slot.OwnerAsc.Equals(plan.OwnerAsc))
                        continue;
                    var battle = slot.BattleInstance;
                        return BoundaryTargetRef.ForAsc(
                            in battle,
                            in plan.OwnerAsc);
                }
            }
            return plan.HasTarget != 0 ? plan.Target : BoundaryTargetRef.None;
        }

        /// <summary>
        /// 通过 Ready registry 解析一个稳定 target，并冻结生命策略看到的 snapshot。
        /// </summary>
        private bool TryResolveTarget(
            in BoundaryTargetRef target,
            GasTargetLifePolicy lifePolicy,
            out OwnerAscHandle owner,
            out byte targetAlive)
        {
            owner = default;
            targetAlive = 0;
            if (target.Kind != GasBoundaryTargetKind.Asc || !target.TargetAsc.IsValid)
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
                var identity = AscIdentities[asc];
                var lifecycle = AscLifecycles[asc].State;
                if (!identity.OwnerAsc.Equals(target.TargetAsc) ||
                    identity.SimulationEpoch != target.SimulationEpoch)
                    return false;
                var alive = lifecycle == GasAscLifecycleState.Ready ||
                            lifecycle == GasAscLifecycleState.Alive;
                if (!alive && lifecycle != GasAscLifecycleState.Terminal)
                    return false;
                if (lifePolicy == GasTargetLifePolicy.AliveOnly && !alive)
                    return false;
                owner = target.TargetAsc;
                targetAlive = alive ? (byte)1 : (byte)0;
                return true;
            }
            return false;
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
            ulong applicationId,
            ulong startTick,
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
        [ReadOnly] public NativeArray<GasAbilityRouteRecord> AbilityRoutes;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public ComponentLookup<AscLifecycle> AscLifecycles;
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
        [ReadOnly] public ComponentLookup<BoundaryDrainState> BoundaryDrains;
        [ReadOnly] public BufferLookup<BoundaryFactBuffer> BoundaryFacts;
        [ReadOnly] public NativeArray<GasSourceSpecRecord> SourceSpecs;
        [ReadOnly] public NativeArray<GasApplicationOutcomeRecord> ApplicationOutcomes;
        [ReadOnly] public NativeArray<GasCoreFactRecord> CoreFacts;
        [ReadOnly] public NativeArray<float> EvaluatorStack;
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
                failure = ValidateEffectOperationIdentities(in execution);
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
            if (!HasActualCapacity(EffectOperations, execution.EffectOperationCount))
                return GasTickAdmissionFailureReason.EffectOperationLimit;
            if (!HasActualCapacity(ResolvedTargets, execution.ResolvedTargetCount))
                return GasTickAdmissionFailureReason.ResolvedTargetLimit;
            if (!HasActualCapacity(SourceSpecs, execution.EffectOperationCount) ||
                !HasActualCapacity(ApplicationOutcomes, execution.EffectOperationCount))
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
                if (!IsReadyOwner(ownerSlot.OwnerAsc, asc))
                    continue;
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
        /// 按 target 汇总本 Tick CoreFact demand，并证明 ASC cleanup outbox 可在 BoundaryProject 中无扩容追加。
        /// </summary>
        private GasTickAdmissionFailureReason ValidateBoundaryReservations(
            in GasTickExecutionState execution)
        {
            if (execution.EffectOperationCount == 0)
                return GasTickAdmissionFailureReason.None;
            if (Profile.MaxAscBoundaryFactCount < 0 || Profile.MaxSessionBoundaryFactCount < 0)
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
            return GasTickAdmissionFailureReason.None;
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
            var demand = 0;
            for (var index = 0; index < operationCount; index++)
            {
                if (EffectOperations[index].TargetAsc.Equals(target))
                    demand++;
            }
            return demand;
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
                        lifecycle == GasAscLifecycleState.Terminal);
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
        private static bool ValidateGrantRelations(
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
        private static bool ValidateActivationRelations(
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
        private static bool ValidateContinuationRelations(
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
        private static bool ValidateSubscriptionRelations(
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
        /// 统计一名 owner 作为真实或 owner-gone 回退 destination 将接收的 route 数。
        /// </summary>
        private int CountPendingRouteDemand(in OwnerAscHandle owner, int routeCount)
        {
            var demand = 0;
            for (var index = 0; index < routeCount; index++)
            {
                var route = AbilityRoutes[index];
                if (route.Used != 0 && ResolveEffectiveDestination(in route).Equals(owner))
                    demand++;
            }
            return demand;
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
        /// observed destination 缺失时把 unsubscribe/registration 终态消息回送 source owner。
        /// </summary>
        private OwnerAscHandle ResolveEffectiveDestination(in GasAbilityRouteRecord route)
        {
            if (HasReadyOwner(route.DestinationAsc))
                return route.DestinationAsc;
            var kind = (GasAbilityPendingCommandKind)route.Command.CommandKind;
            return kind == GasAbilityPendingCommandKind.WaitUnsubscribe ||
                   kind == GasAbilityPendingCommandKind.WaitRegistration
                ? route.Command.SourceAsc
                : route.DestinationAsc;
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
                GasAbilityOwnerTransaction.ApplyPlan(
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
            }
            ApplyPlannedWaitCancellations();
            FinalizeLocalEndingContinuations();
            RunPostCommandMaintenance();
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
        /// 在全部 canonical plans 后收尾无 Continuation 的 Ending Activation 与 grant removal。
        /// </summary>
        private void RunPostCommandMaintenance()
        {
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
                GasAbilityLifecycleMaintenance.RunPostCommand(
                    Grants[asc], Activations[asc], Continuations[asc], ref heads);
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
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public NativeArray<GasEffectOperationRecord> EffectOperations;
        public NativeArray<GasSourceSpecRecord> SourceSpecs;
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
                SourceSpecs[execution.SourceSpecCount++] = new GasSourceSpecRecord
                {
                    OperationOrdinal = index,
                    DefinitionIndex = operation.DefinitionIndex,
                    SourceAsc = operation.SourceAsc,
                    TargetAsc = operation.TargetAsc,
                    ApplicationId = operation.ApplicationId,
                    StartTick = operation.StartTick,
                    TargetIsAlive = operation.TargetIsAlive,
                };
            }
            Execution[0] = execution;
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
    /// 执行 target-local 单写 application transaction，并把结果留在 target-owned slabs 中。
    /// </summary>
    internal struct GasAscTargetStateWaveJob : IJob
    {
        public ulong SimulationEpoch;
        [ReadOnly] public GasScaleProfile Profile;
        [ReadOnly] public BlobAssetReference<GasDefinitionCatalogBlob> Catalog;
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public NativeArray<GasSourceSpecRecord> SourceSpecs;
        public NativeArray<float> EvaluatorStack;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public ComponentLookup<AscLifecycle> AscLifecycles;
        public ComponentLookup<AscSlabHeads> SlabHeads;
        public ComponentLookup<GasPayloadRangeAllocatorState> PayloadStates;
        public BufferLookup<ActiveEffectSlot> ActiveEffects;
        public BufferLookup<AttributeValueSlot> Attributes;
        public BufferLookup<AttributeDirtyWord> AttributeDirtyWords;
        public BufferLookup<TagCountSlot> TagCounts;
        public BufferLookup<TagPresenceWord> TagPresenceWords;
        public BufferLookup<GasPayloadRangeRecord> PayloadRanges;
        public BufferLookup<GasPayloadValueSlot> PayloadValues;
        public NativeArray<GasApplicationOutcomeRecord> ApplicationOutcomes;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// admission 成功后按 scratch canonical 顺序执行 target-owned effect transactions。
        /// </summary>
        public void Execute()
        {
            if (!GasTickJobUtility.EnterDownstream(Admission, Execution, GasTickLaneMask.TargetWave))
                return;
            var execution = Execution[0];
            execution.ApplicationOutcomeCount = 0;
            ref var catalog = ref Catalog.Value;
            for (var index = 0; index < execution.SourceSpecCount; index++)
            {
                if (execution.ApplicationOutcomeCount >= ApplicationOutcomes.Length)
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                    break;
                }
                var spec = SourceSpecs[index];
                var outcome = default(GasGameplayEffectApplicationResult);
                if (!TryResolveOwner(spec.TargetAsc, out var target))
                {
                    outcome.Outcome = GasGameplayEffectApplicationOutcome.RejectedStaleBinding;
                    outcome.Failure = GasGameplayEffectTransactionFailure.InvalidIdentity;
                    outcome.ApplicationId = spec.ApplicationId;
                    StoreOutcome(ref execution, in spec, in outcome);
                    continue;
                }
                var heads = SlabHeads[target];
                var request = new GasGameplayEffectApplicationRequest
                {
                    SimulationEpoch = SimulationEpoch,
                    SourceAsc = spec.SourceAsc,
                    TargetAsc = spec.TargetAsc,
                    DefinitionIndex = spec.DefinitionIndex,
                    ApplicationId = spec.ApplicationId,
                    StartTick = spec.StartTick,
                    TargetIsAlive = spec.TargetIsAlive,
                    CaptureValueCount = 0,
                    ValueViewCount = 0,
                };
                var applied = GasGameplayEffectTransaction.TryApply(
                    ref catalog,
                    in request,
                    ActiveEffects[target],
                    Attributes[target],
                    AttributeDirtyWords[target],
                    TagCounts[target],
                    TagPresenceWords[target],
                    ref heads.ActiveEffect,
                    Profile.MaxActiveEffectCount,
                    default(NativeArray<float>),
                    default(NativeArray<float>),
                    EvaluatorStack,
                    out outcome);
                StoreOutcome(ref execution, in spec, in outcome);
                SlabHeads[target] = heads;
                if (!applied &&
                    outcome.Outcome == GasGameplayEffectApplicationOutcome.InfrastructureFault)
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                    break;
                }
            }
            Execution[0] = execution;
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
                Outcome = outcome.Outcome,
                Failure = outcome.Failure,
                SourceAsc = spec.SourceAsc,
                TargetAsc = spec.TargetAsc,
                ActiveEffect = outcome.ActiveEffect,
                ApplicationId = outcome.ApplicationId,
                AppliedModifierCount = outcome.AppliedModifierCount,
                DeathCrossed = outcome.DeathCrossed,
            };
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
                        lifecycle == GasAscLifecycleState.Terminal);
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
    /// 把 target writer 的 application outcome 收敛为 target-owned stable CoreFact 分区。
    /// </summary>
    internal struct GasTargetLocalStabilizationDeathJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public NativeArray<GasApplicationOutcomeRecord> ApplicationOutcomes;
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
            for (var index = 0; index < execution.ApplicationOutcomeCount; index++)
            {
                if (execution.CoreFactCount >= CoreFacts.Length)
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.PostAdmissionInvariantViolation;
                    break;
                }
                var outcome = ApplicationOutcomes[index];
                CoreFacts[execution.CoreFactCount++] = new GasCoreFactRecord
                {
                    Scope = GasBoundaryFactScope.Asc,
                    Plane = GasBoundaryFactPlane.Gameplay,
                    Kind = GasBoundaryFactKind.EffectLifecycle,
                    SourceAsc = outcome.SourceAsc,
                    TargetAsc = outcome.TargetAsc,
                    SimulationTick = execution.CandidateTick,
                    SemanticPhaseOrdinal = 1,
                    WorkClassOrdinal = 1,
                    ParentCausalityId = outcome.ApplicationId,
                    SemanticId = outcome.ApplicationId,
                    Payload = CreateOutcomePayload(in outcome),
                    OperationOrdinal = outcome.OperationOrdinal,
                };
            }
            Execution[0] = execution;
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
                StableId0 = outcome.ApplicationId,
                StableId1 = outcome.ActiveEffect.OwnerAsc.AscStableId,
                Generation0 = outcome.ActiveEffect.SlotGeneration,
                Generation1 = outcome.ActiveEffect.OwnerAsc.AscGeneration,
            };
        }
    }

    /// <summary>
    /// 使用稳定 semantic key 合并各 target 分区 CoreFact，禁止以 Job 完成顺序决定事实顺序。
    /// </summary>
    internal struct GasStableFactMergeTerminalResolveJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
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
        }

        /// <summary>
        /// 比较 scope、target、semantic phase、work class 与 semantic id 的完整稳定键。
        /// </summary>
        private static int Compare(in GasCoreFactRecord left, in GasCoreFactRecord right)
        {
            var comparison = left.Scope.CompareTo(right.Scope);
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
            return comparison != 0
                ? comparison
                : left.OperationOrdinal.CompareTo(right.OperationOrdinal);
        }
    }

    /// <summary>
    /// 按 destination ASC 把 admission 前冻结的 T+1 route 写入 owner-local PendingCommand slab。
    /// </summary>
    internal struct GasGroupNextTickRouteByDestinationJob : IJob
    {
        public Entity Session;
        public ulong SimulationEpoch;
        [ReadOnly] public GasScaleProfile Profile;
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public ComponentLookup<AscLifecycle> AscLifecycles;
        public ComponentLookup<AscSlabHeads> SlabHeads;
        public BufferLookup<PendingCommand> PendingCommands;
        public NativeArray<GasAbilityRouteRecord> AbilityRoutes;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// admission 成功后按已排序 route 顺序提交 destination 单写；owner-gone 转为类型化回执。
        /// </summary>
        public void Execute()
        {
            if (!GasTickJobUtility.EnterDownstream(
                    Admission, Execution, GasTickLaneMask.RouteNextTick))
                return;
            var execution = Execution[0];
            for (var index = 0; index < execution.AbilityRouteCount; index++)
            {
                var route = AbilityRoutes[index];
                if (route.Used == 0 || route.RequiresOwnerApply != 0)
                    continue;
                var outbound = route.Command;
                var command = outbound;
                var destination = route.DestinationAsc;
                if (!TryResolveOwner(destination, out var asc))
                {
                    if (!GasAbilityWaitSlabTransaction.TryCreateObservedOwnerGoneResponse(
                            in command, execution.CandidateTick, out var ownerGoneResponse) ||
                        !TryResolveOwner(ownerGoneResponse.TargetAsc, out asc))
                    {
                        ConsumeForwardedCommand(in route);
                        route.Used = 0;
                        AbilityRoutes[index] = route;
                        continue;
                    }
                    command = ownerGoneResponse;
                    destination = command.TargetAsc;
                }
                if (IsObservedOwnerGoneCompletion(in command) &&
                    HasEquivalentPendingCommand(asc, in command))
                {
                    ConsumeForwardedCommand(in route);
                    route.Used = 0;
                    AbilityRoutes[index] = route;
                    continue;
                }
                if (!Enqueue(destination, asc, in command))
                    continue;
                ConsumeForwardedCommand(in route);
                route.Used = 0;
                AbilityRoutes[index] = route;
            }
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
        /// 避免重复 owner-gone route 在目标 Pending slab 形成多条同一终态 completion。
        /// </summary>
        private bool HasEquivalentPendingCommand(
            Entity asc,
            in PendingCommand candidate)
        {
            var commands = PendingCommands[asc];
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
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
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 使用 admission 已证明的 free-list/capacity，把完整冻结消息写入 destination slab。
        /// </summary>
        private bool Enqueue(
            in OwnerAscHandle destination,
            Entity asc,
            in PendingCommand frozen)
        {
            var heads = SlabHeads[asc];
            var commands = PendingCommands[asc];
            var storage = new GasPendingCommandSlabStorage { Buffer = commands };
            if (GasNonCompactingSlabAllocator.TryAllocate(
                    ref heads.PendingCommand,
                    ref storage,
                    Profile.MaxPendingCommandCount,
                    out var allocation) != GasSlabStorageFailure.None)
                return false;
            var command = frozen;
            command.Header = allocation.LiveHeader;
            command.TargetAsc = destination;
            command.State = GasSlotBusinessState.Pending;
            commands[allocation.SlotIndex] = command;
            SlabHeads[asc] = heads;
            return true;
        }

        /// <summary>
        /// 原始跨 ASC wait 已成功转发后消费 source slab 命令；由取消计划生成的 route 无 source 命令则 no-op。
        /// </summary>
        private void ConsumeForwardedCommand(in GasAbilityRouteRecord route)
        {
            if (route.OriginCommandSequence == 0 ||
                !IsForwardedWaitKind(route.OriginCommandKind))
                return;
            if (!TryResolveOwner(route.Command.SourceAsc, out var sourceAsc))
                return;
            var commands = PendingCommands[sourceAsc];
            var heads = SlabHeads[sourceAsc];
            var storage = new GasPendingCommandSlabStorage { Buffer = commands };
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
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
                        in heads.PendingCommand, ref storage, index) != GasSlabStorageFailure.None)
                    return;
                command = commands[index];
                command.State = GasSlotBusinessState.Terminal;
                commands[index] = command;
                SlabHeads[sourceAsc] = heads;
                return;
            }
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
        /// 通过 Session Ready registry 解析内部 destination Entity。
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
                    if (!AscIdentities.HasComponent(asc) || !AscLifecycles.HasComponent(asc))
                        return false;
                    var identity = AscIdentities[asc];
                    var state = AscLifecycles[asc].State;
                    return identity.SimulationEpoch == SimulationEpoch &&
                           identity.OwnerAsc.Equals(owner) &&
                           (state == GasAscLifecycleState.Ready ||
                            state == GasAscLifecycleState.Alive) &&
                           SlabHeads.HasComponent(asc) && PendingCommands.HasBuffer(asc);
                }
            }
            asc = Entity.Null;
            return false;
        }
    }

    /// <summary>
    /// 把稳定 CoreFact 按唯一物理 owner 投影到 ASC scoped cleanup outbox。
    /// </summary>
    internal struct GasBoundaryProjectJob : IJob
    {
        public Entity Session;
        public ulong SimulationEpoch;
        [ReadOnly] public NativeArray<GasCoreFactRecord> CoreFacts;
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        [ReadOnly] public BufferLookup<AscRegistrySlot> Registries;
        [ReadOnly] public ComponentLookup<GasAscIdentity> AscIdentities;
        [ReadOnly] public ComponentLookup<AscLifecycle> AscLifecycles;
        [ReadOnly] public ComponentLookup<AscBattleMembership> Memberships;
        public ComponentLookup<BoundaryDrainState> Drains;
        public BufferLookup<BoundaryFactBuffer> BoundaryFacts;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 通过稳定 target identity 找到唯一 ASC outbox，并按 owner sequence 追加自包含事实。
        /// </summary>
        public void Execute()
        {
            if (!GasTickJobUtility.EnterDownstream(
                    Admission, Execution, GasTickLaneMask.BoundaryProject))
                return;

            var execution = Execution[0];
            execution.BoundaryFactCount = 0;
            if (!Registries.HasBuffer(Session))
            {
                execution.PostAdmissionFailure =
                    GasTickAdmissionFailureReason.BoundaryProjectionFailure;
                Execution[0] = execution;
                return;
            }
            for (var index = 0; index < execution.CoreFactCount; index++)
            {
                var coreFact = CoreFacts[index];
                if (coreFact.Scope != GasBoundaryFactScope.Asc ||
                    !TryResolveOwner(coreFact.TargetAsc, out var target, out var membership))
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.BoundaryProjectionFailure;
                    break;
                }
                if (!Drains.HasComponent(target) || !BoundaryFacts.HasBuffer(target))
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.BoundaryProjectionFailure;
                    break;
                }

                var state = Drains[target];
                var fact = new BoundaryFactBuffer
                {
                    Scope = coreFact.Scope,
                    Plane = coreFact.Plane,
                    ScopeStableId = state.OwnerStableId,
                    ScopeGeneration = state.OwnerGeneration,
                    BattleInstanceId = membership.BattleInstance.BattleStableId,
                    BattleInstanceGeneration = membership.BattleInstance.BattleGeneration,
                    OwnerScenarioUnitId = membership.ScenarioUnitId,
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
                if (!GasBoundaryDrainProtocol.TryAppendFactWithSequence(
                        ref state,
                        BoundaryFacts[target],
                        in fact,
                        out _,
                        out _))
                {
                    execution.PostAdmissionFailure =
                        GasTickAdmissionFailureReason.BoundaryProjectionFailure;
                    break;
                }
                Drains[target] = state;
                execution.BoundaryFactCount++;
            }
            Execution[0] = execution;
        }

        /// <summary>
        /// 通过 Session registry 解析 target Entity，并复验 Epoch、identity、lifecycle 与 membership。
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
                if (!AscIdentities.HasComponent(asc) || !AscLifecycles.HasComponent(asc) ||
                    !Memberships.HasComponent(asc))
                    break;
                var identity = AscIdentities[asc];
                var lifecycle = AscLifecycles[asc].State;
                membership = Memberships[asc];
                if (identity.SimulationEpoch == SimulationEpoch &&
                    identity.OwnerAsc.Equals(owner) &&
                    (lifecycle == GasAscLifecycleState.Ready ||
                     lifecycle == GasAscLifecycleState.Alive ||
                     lifecycle == GasAscLifecycleState.Terminal))
                    return true;
                break;
            }
            asc = Entity.Null;
            membership = default;
            return false;
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
