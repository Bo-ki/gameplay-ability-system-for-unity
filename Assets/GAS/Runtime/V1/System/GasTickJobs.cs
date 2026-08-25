using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    /// <summary>
    /// 在任何 Session cardinality 分支前占据 cleanup maintenance 依赖位置；阶段 F 将填充 drain receipt 清理。
    /// </summary>
    internal struct GasCleanupAcceptedPrepassJob : IJob
    {
        /// <summary>
        /// 保持所有 Kernel update 都经过同一 maintenance 前置依赖，当前阶段没有 Accepted receipt 可变更。
        /// </summary>
        public void Execute()
        {
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
        [ReadOnly] public NativeArray<GasSealedCommand> SealedCommands;
        [ReadOnly] public NativeArray<PlanExpandScratchEnvelopeToken> Envelope;
        public NativeArray<GasOwnerPlanRecord> OwnerPlans;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 有效 envelope 下为每条 sealed command 写一个定长计划，业务接受留给阶段 D。
        /// </summary>
        public void Execute()
        {
            var execution = Execution[0];
            if (execution.GameplayEnabled == 0)
                return;
            execution.LaneMask |= GasTickLaneMask.OwnerPlan;
            if (Envelope[0].FailureReason == GasTickAdmissionFailureReason.None)
            {
                for (var index = 0; index < execution.StoredSealedCommandCount; index++)
                {
                    var command = SealedCommands[index].Command;
                    OwnerPlans[index] = new GasOwnerPlanRecord
                    {
                        SealedCommandOrdinal = index,
                        OwnerAsc = command.SourceAsc,
                        SourceSequence = command.SourceSequence,
                        CommandKind = (int)command.CommandKind,
                    };
                }
                execution.OwnerPlanCount = execution.StoredSealedCommandCount;
            }
            Execution[0] = execution;
        }
    }

    /// <summary>
    /// 保留 bounded target expansion 的具名 DAG 节点；阶段 C 不伪造尚未 Commit 的 target work。
    /// </summary>
    internal struct GasTargetResolveExpandJob : IJob
    {
        [ReadOnly] public NativeArray<PlanExpandScratchEnvelopeToken> Envelope;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 记录 lane 已进入；有效 envelope 下保持零目标，阶段 D/E 将只在预留数组内展开。
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
            }
            Execution[0] = execution;
        }
    }

    /// <summary>
    /// 在任何 gameplay authority writer 前统一裁决完整 Tick 的逻辑预算与 reservation contract。
    /// </summary>
    internal struct GasWholeTickInfrastructureAdmissionJob : IJob
    {
        [ReadOnly] public GasScaleProfile Profile;
        [ReadOnly] public NativeArray<PlanExpandScratchEnvelopeToken> Envelope;
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
            var failure = Envelope[0].FailureReason;
            if (failure == GasTickAdmissionFailureReason.None)
                failure = ValidateActualCounts(in execution);
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
            if (execution.OwnerPlanCount > Profile.MaxOwnerPlanCount)
                return GasTickAdmissionFailureReason.OwnerPlanLimit;
            if (execution.ResolvedTargetCount > Profile.MaxResolvedTargetCount)
                return GasTickAdmissionFailureReason.ResolvedTargetLimit;
            if (execution.EffectOperationCount > Profile.MaxEffectOperationCount)
                return GasTickAdmissionFailureReason.EffectOperationLimit;
            if (Profile.MaxOwnerReservationCount < 0)
                return GasTickAdmissionFailureReason.OwnerReservationLimit;
            if (Profile.MaxTargetReservationCount < 0)
                return GasTickAdmissionFailureReason.TargetReservationLimit;
            if (Profile.MaxCoreFactCount < 0)
                return GasTickAdmissionFailureReason.CoreFactLimit;
            if (Profile.MaxNextTickRouteCount < 0)
                return GasTickAdmissionFailureReason.NextTickRouteLimit;
            return Profile.MaxStructuralIntentCount < 0
                ? GasTickAdmissionFailureReason.StructuralIntentLimit
                : GasTickAdmissionFailureReason.None;
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
            if (admission.Succeeded == 0)
            {
                var faultId = CreateFaultId(in execution, admission.FailureReason);
                FaultLatches[Session] = new SessionFaultLatch
                {
                    FaultId = faultId,
                    FaultEpoch = SimulationEpoch,
                    FaultTick = execution.CandidateTick,
                    ReasonCode = (int)admission.FailureReason,
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
    /// 执行 admitted owner-local no-fail CommitPlan；阶段 C 仅固定预排/no-op contract。
    /// </summary>
    internal struct GasAscOwnerCommandWaveJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 无条件进入 lane，admission 失败时立即 no-op。
        /// </summary>
        public void Execute()
        {
            GasTickJobUtility.EnterDownstream(Admission, Execution, GasTickLaneMask.OwnerWave);
        }
    }

    /// <summary>
    /// 只为成功 Commit 的正式身份密封 source-bound spec；阶段 C 固定预排/no-op contract。
    /// </summary>
    internal struct GasSourceSpecProjectionJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 无条件进入 lane，admission 失败时立即 no-op。
        /// </summary>
        public void Execute()
        {
            GasTickJobUtility.EnterDownstream(Admission, Execution, GasTickLaneMask.SourceProjection);
        }
    }

    /// <summary>
    /// 按稳定 target identity 建 canonical ranges；阶段 C 固定预排/no-op contract。
    /// </summary>
    internal struct GasGroupWorkByTargetJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 无条件进入 lane，admission 失败时立即 no-op。
        /// </summary>
        public void Execute()
        {
            GasTickJobUtility.EnterDownstream(Admission, Execution, GasTickLaneMask.GroupTarget);
        }
    }

    /// <summary>
    /// 执行 target-local 单写 application transaction；阶段 C 固定预排/no-op contract。
    /// </summary>
    internal struct GasAscTargetStateWaveJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 无条件进入 lane，admission 失败时立即 no-op。
        /// </summary>
        public void Execute()
        {
            GasTickJobUtility.EnterDownstream(Admission, Execution, GasTickLaneMask.TargetWave);
        }
    }

    /// <summary>
    /// 求 target-local stable state 与首次 death crossing；阶段 C 固定预排/no-op contract。
    /// </summary>
    internal struct GasTargetLocalStabilizationDeathJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 无条件进入 lane，admission 失败时立即 no-op。
        /// </summary>
        public void Execute()
        {
            GasTickJobUtility.EnterDownstream(Admission, Execution, GasTickLaneMask.StabilizeDeath);
        }
    }

    /// <summary>
    /// 稳定合并 Core Fact 并按 BattleInstance 唯一裁决终局；阶段 C 固定预排/no-op contract。
    /// </summary>
    internal struct GasStableFactMergeTerminalResolveJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 无条件进入 lane，admission 失败时立即 no-op。
        /// </summary>
        public void Execute()
        {
            GasTickJobUtility.EnterDownstream(Admission, Execution, GasTickLaneMask.FactMergeTerminal);
        }
    }

    /// <summary>
    /// 按 destination ASC 生成 T+1 PendingCommand；阶段 C 固定预排/no-op contract。
    /// </summary>
    internal struct GasGroupNextTickRouteByDestinationJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 无条件进入 lane，admission 失败时立即 no-op。
        /// </summary>
        public void Execute()
        {
            GasTickJobUtility.EnterDownstream(Admission, Execution, GasTickLaneMask.RouteNextTick);
        }
    }

    /// <summary>
    /// 把稳定事实投影到唯一 scoped cleanup outbox；阶段 C 固定预排/no-op contract。
    /// </summary>
    internal struct GasBoundaryProjectJob : IJob
    {
        [ReadOnly] public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasTickExecutionState> Execution;

        /// <summary>
        /// 无条件进入 lane，admission 失败时立即 no-op。
        /// </summary>
        public void Execute()
        {
            GasTickJobUtility.EnterDownstream(Admission, Execution, GasTickLaneMask.BoundaryProject);
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
            if (admission.Succeeded != 0)
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
            return admission[0].Succeeded != 0;
        }
    }
}
