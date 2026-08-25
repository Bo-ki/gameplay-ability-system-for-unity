using Unity.Collections;

namespace GAS.Runtime
{
    /// <summary>
    /// 固定 Runtime v1 gameplay DAG 的具名 lane bit，诊断顺序不得参与业务 canonical key。
    /// </summary>
    internal static class GasTickLaneMask
    {
        internal const ulong Gather = 1UL << 0;
        internal const ulong Provision = 1UL << 1;
        internal const ulong OwnerPlan = 1UL << 2;
        internal const ulong TargetResolve = 1UL << 3;
        internal const ulong Admission = 1UL << 4;
        internal const ulong FaultLatch = 1UL << 5;
        internal const ulong OwnerWave = 1UL << 6;
        internal const ulong SourceProjection = 1UL << 7;
        internal const ulong GroupTarget = 1UL << 8;
        internal const ulong TargetWave = 1UL << 9;
        internal const ulong StabilizeDeath = 1UL << 10;
        internal const ulong FactMergeTerminal = 1UL << 11;
        internal const ulong RouteNextTick = 1UL << 12;
        internal const ulong BoundaryProject = 1UL << 13;
        internal const ulong RecordEndFixed = 1UL << 14;
        internal const ulong TickFinalize = 1UL << 15;
        internal const ulong AllGameplay = (1UL << 16) - 1;
    }

    /// <summary>
    /// 定义整 Tick 基础设施准入的稳定失败原因，业务拒绝不得映射到本枚举。
    /// </summary>
    internal enum GasTickAdmissionFailureReason : int
    {
        None = 0,
        TickOverflow = 1001,
        InboxStateInvalid = 1002,
        BoundaryCommandLimit = 1003,
        CanonicalKeyCollision = 1004,
        EnvelopeArithmeticOverflow = 1005,
        OwnerPlanLimit = 1006,
        ResolvedTargetLimit = 1007,
        EffectOperationLimit = 1008,
        OwnerReservationLimit = 1009,
        TargetReservationLimit = 1010,
        CoreFactLimit = 1011,
        NextTickRouteLimit = 1012,
        StructuralIntentLimit = 1013,
        DurableCapacityUnavailable = 1014,
        IngressPayloadRangeInvalid = 1015,
        IngressPhysicalCapacityUnavailable = 1016,
    }

    /// <summary>
    /// 保存 Gather 后已 seal 的持久 inbox 条目与原始索引，RequestSequence 不参与业务排序。
    /// </summary>
    internal struct GasSealedCommand
    {
        public int InboxIndex;
        public BoundaryCommandInbox Command;
    }

    /// <summary>
    /// 按 schema-hashed 业务字段比较 owner command，显式排除 RequestId 与 RequestSequence。
    /// </summary>
    internal struct GasSealedCommandComparer : System.Collections.Generic.IComparer<GasSealedCommand>
    {
        /// <summary>
        /// 依次比较 Epoch、deliver tick、owner、语义 ordinal、source sequence 与 command kind。
        /// </summary>
        public int Compare(GasSealedCommand left, GasSealedCommand right)
        {
            var comparison = left.Command.SimulationEpoch.CompareTo(right.Command.SimulationEpoch);
            if (comparison != 0)
                return comparison;
            comparison = left.Command.AvailableTick.CompareTo(right.Command.AvailableTick);
            if (comparison != 0)
                return comparison;
            comparison = CompareOwner(in left.Command.SourceAsc, in right.Command.SourceAsc);
            if (comparison != 0)
                return comparison;
            comparison = left.Command.SemanticPhaseOrdinal.CompareTo(right.Command.SemanticPhaseOrdinal);
            if (comparison != 0)
                return comparison;
            comparison = left.Command.WorkClassOrdinal.CompareTo(right.Command.WorkClassOrdinal);
            if (comparison != 0)
                return comparison;
            comparison = left.Command.SourceSequence.CompareTo(right.Command.SourceSequence);
            return comparison != 0
                ? comparison
                : left.Command.CommandKind.CompareTo(right.Command.CommandKind);
        }

        /// <summary>
        /// 比较完整 ASC 稳定 owner 身份，禁止退回 Entity 或 registry ordinal。
        /// </summary>
        private static int CompareOwner(in OwnerAscHandle left, in OwnerAscHandle right)
        {
            var comparison = left.AscStableId.CompareTo(right.AscStableId);
            return comparison != 0 ? comparison : left.AscGeneration.CompareTo(right.AscGeneration);
        }
    }

    /// <summary>
    /// 保存 Gather 与后续 lane 共享的单 Tick 控制状态，生命周期仅限当前 Kernel DAG。
    /// </summary>
    internal struct GasTickExecutionState
    {
        public ulong CandidateTick;
        public ulong LaneMask;
        public ulong SealedFirstRequestSequence;
        public ulong SealedLastRequestSequence;
        public ulong SealedRequestHash;
        public int SealedCommandCount;
        public int StoredSealedCommandCount;
        public int OwnerPlanCount;
        public int ResolvedTargetCount;
        public int EffectOperationCount;
        public GasTickAdmissionFailureReason PreAdmissionFailure;
        public byte GameplayEnabled;
    }

    /// <summary>
    /// 冻结 Plan/Expand 在写 variable scratch 前由 sealed count 与 Catalog maxima 证明的容量 envelope。
    /// </summary>
    internal struct PlanExpandScratchEnvelopeToken
    {
        public ulong CandidateTick;
        public int MaximumOwnerPlanCount;
        public int MaximumResolvedTargetCount;
        public int MaximumEffectOperationCount;
        public GasTickAdmissionFailureReason FailureReason;
    }

    /// <summary>
    /// 保存 OwnerPlanBuild 的阶段 C 计划载体；业务跃迁在阶段 D 才填充，当前仍保持零权威写。
    /// </summary>
    internal struct GasOwnerPlanRecord
    {
        public int SealedCommandOrdinal;
        public OwnerAscHandle OwnerAsc;
        public ulong SourceSequence;
        public int CommandKind;
        public byte BusinessAccepted;
    }

    /// <summary>
    /// 保存 admission 前已解析的稳定目标记录，禁止携带 raw Entity 到后续 lane。
    /// </summary>
    internal struct GasResolvedTargetRecord
    {
        public int OwnerPlanOrdinal;
        public OwnerAscHandle TargetAsc;
        public int TargetOrdinal;
    }

    /// <summary>
    /// 保存 admission 前闭合展开的 Effect 操作计数载体，正式身份只能在成功 Owner Commit 后产生。
    /// </summary>
    internal struct GasEffectOperationRecord
    {
        public int OwnerPlanOrdinal;
        public int TargetOrdinal;
        public int ProgramNodeOrdinal;
    }

    /// <summary>
    /// 汇总单 owner 在 admission 中必须预留的全部长期资源需求。
    /// </summary>
    internal struct GasOwnerResourceDemand
    {
        public int AbilityActivationCount;
        public int ContinuationCount;
        public int SubscriptionCount;
        public int CooldownGateCount;
        public int OwnedContributionCount;
        public int EmittedApplicationRefCount;
        public int PendingCommandCount;
        public int BoundaryFactCount;
    }

    /// <summary>
    /// 汇总单 target 在 admission 中必须预留的 Effect、payload、聚合与事实资源需求。
    /// </summary>
    internal struct GasTargetResourceDemand
    {
        public int ActiveEffectCount;
        public int PayloadRangeCount;
        public int PayloadValueCount;
        public int AggregatorCount;
        public int LiveDependencyCount;
        public int LiveDependencyRouteCount;
        public int BoundaryFactCount;
    }

    /// <summary>
    /// 保存 WholeTick admission 的唯一结果；下游所有 Job 无条件预排并先读取此值。
    /// </summary>
    internal struct GasAdmissionResult
    {
        public ulong CandidateTick;
        public GasTickAdmissionFailureReason FailureReason;
        public byte Succeeded;
    }

    /// <summary>
    /// 由 Kernel 在 WorldUpdateAllocator 上一次创建完整 DAG scratch，任何字段都不得跨 Tick 保存。
    /// </summary>
    internal struct GasTickScratch
    {
        public NativeArray<GasTickExecutionState> Execution;
        public NativeArray<PlanExpandScratchEnvelopeToken> Envelope;
        public NativeArray<GasAdmissionResult> Admission;
        public NativeArray<GasSealedCommand> SealedCommands;
        public NativeArray<GasOwnerPlanRecord> OwnerPlans;
        public NativeArray<GasResolvedTargetRecord> ResolvedTargets;
        public NativeArray<GasEffectOperationRecord> EffectOperations;
        public NativeArray<GasOwnerResourceDemand> OwnerDemands;
        public NativeArray<GasTargetResourceDemand> TargetDemands;

        /// <summary>
        /// 按 ScaleProfile 逻辑上限创建全部定长容器，Job 只能在界内写入而不得扩容。
        /// </summary>
        public static GasTickScratch Create(
            in GasScaleProfile profile,
            AllocatorManager.AllocatorHandle allocator)
        {
            return new GasTickScratch
            {
                Execution = CreateArray<GasTickExecutionState>(1, allocator),
                Envelope = CreateArray<PlanExpandScratchEnvelopeToken>(1, allocator),
                Admission = CreateArray<GasAdmissionResult>(1, allocator),
                SealedCommands = CreateArray<GasSealedCommand>(ClampLength(profile.MaxBoundaryCommandCount), allocator),
                OwnerPlans = CreateArray<GasOwnerPlanRecord>(ClampLength(profile.MaxOwnerPlanCount), allocator),
                ResolvedTargets = CreateArray<GasResolvedTargetRecord>(ClampLength(profile.MaxResolvedTargetCount), allocator),
                EffectOperations = CreateArray<GasEffectOperationRecord>(ClampLength(profile.MaxEffectOperationCount), allocator),
                OwnerDemands = CreateArray<GasOwnerResourceDemand>(ClampLength(profile.MaxOwnerReservationCount), allocator),
                TargetDemands = CreateArray<GasTargetResourceDemand>(ClampLength(profile.MaxTargetReservationCount), allocator),
            };
        }

        /// <summary>
        /// 让待由 SpawnFinalize 锁存的非法负容量只得到空 scratch，避免在验证 Job 前主线程抛错。
        /// </summary>
        private static int ClampLength(int length)
        {
            return length < 0 ? 0 : length;
        }

        /// <summary>
        /// 创建清零的定长 NativeArray，使零容量 profile 仍得到合法空容器。
        /// </summary>
        private static NativeArray<T> CreateArray<T>(
            int length,
            AllocatorManager.AllocatorHandle allocator)
            where T : unmanaged
        {
            return CollectionHelper.CreateNativeArray<T>(
                length,
                allocator,
                NativeArrayOptions.ClearMemory);
        }
    }
}
