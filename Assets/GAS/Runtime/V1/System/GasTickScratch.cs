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
        WaitRouteOverdue = 1017,
        BoundaryProjectionFailure = 1018,
        PostAdmissionInvariantViolation = 1019,
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
        public int SourceSpecCount;
        public int ApplicationOutcomeCount;
        public int CoreFactCount;
        public int BoundaryFactCount;
        public GasTickAdmissionFailureReason PostAdmissionFailure;
        public int AbilityRouteCount;
        public ulong FirstAbilityRouteStableSequence;
        public ulong NextStableSequenceAfterPlan;
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
    /// 保存 OwnerPlanBuild 已全量验证的 Ability owner-local 事务，失败计划保持零资源需求。
    /// </summary>
    internal struct GasOwnerPlanRecord
    {
        public int SealedCommandOrdinal;
        public OwnerAscHandle OwnerAsc;
        public ulong SourceSequence;
        public ulong StableSequence;
        public StableHandleDiagnosticCarrier SubjectHandle;
        public BoundaryTargetRef Target;
        public byte HasTarget;
        public GrantedAbilityHandle GrantedAbility;
        public AbilityActivationHandle Activation;
        public GasBoundaryCommandKind CommandKind;
        public GasAbilityCommandResult Result;
        public int DefinitionIndex;
        public int CostAttributeLayoutIndex;
        public float CostBaseDelta;
        public float CostCurrentDelta;
        public int CooldownGateKey;
        public int CooldownDurationTicks;
        public int CooldownOwnedTagIndex;
        public GasAbilityEndReason EndReason;
        public byte WasCancelled;
        public byte RequiresActivationSlot;
        public byte RequiresCooldownSlot;
        public byte ProducesCommittedWork;
        public byte BusinessAccepted;
    }

    /// <summary>
    /// 保存 owner/observed ASC writer 产生且只能在 T+1 投递的 Ability 内部消息。
    /// </summary>
    internal struct GasAbilityRouteRecord
    {
        public OwnerAscHandle DestinationAsc;
        public PendingCommand Command;
        public ulong OriginCommandSequence;
        public int OriginCommandKind;
        public byte RequiresOwnerApply;
        public byte Used;
    }

    /// <summary>
    /// 按 destination、deliver tick 与完整 wait identity 排定跨 ASC 内部消息的唯一全序。
    /// </summary>
    internal struct GasAbilityRouteComparer : System.Collections.Generic.IComparer<GasAbilityRouteRecord>
    {
        /// <summary>
        /// Used 记录优先，再比较 destination、因果来源与冻结 recipient 语义键。
        /// </summary>
        public int Compare(GasAbilityRouteRecord left, GasAbilityRouteRecord right)
        {
            var comparison = right.Used.CompareTo(left.Used);
            if (comparison != 0)
                return comparison;
            comparison = CompareOwner(in left.DestinationAsc, in right.DestinationAsc);
            if (comparison != 0)
                return comparison;
            comparison = left.Command.AvailableTick.CompareTo(right.Command.AvailableTick);
            if (comparison != 0)
                return comparison;
            comparison = CompareOwner(in left.Command.SourceAsc, in right.Command.SourceAsc);
            if (comparison != 0)
                return comparison;
            comparison = left.OriginCommandSequence.CompareTo(right.OriginCommandSequence);
            if (comparison != 0)
                return comparison;
            comparison = left.Command.RecipientKindPriority.CompareTo(
                right.Command.RecipientKindPriority);
            if (comparison != 0)
                return comparison;
            comparison = right.Command.MatchedTagDepth.CompareTo(left.Command.MatchedTagDepth);
            if (comparison != 0)
                return comparison;
            comparison = CompareContinuation(
                in left.Command.Continuation, in right.Command.Continuation);
            if (comparison != 0)
                return comparison;
            comparison = left.Command.RegistrationSequence.CompareTo(
                right.Command.RegistrationSequence);
            if (comparison != 0)
                return comparison;
            comparison = left.Command.WakeOrdinal.CompareTo(right.Command.WakeOrdinal);
            if (comparison != 0)
                return comparison;
            comparison = CompareActivation(in left.Command.Activation, in right.Command.Activation);
            if (comparison != 0)
                return comparison;
            comparison = left.Command.RegistrationGeneration.CompareTo(
                right.Command.RegistrationGeneration);
            return comparison != 0 ? comparison :
                GasAbilityPendingCommandOrder.GetSemanticPriority(left.Command.CommandKind).CompareTo(
                    GasAbilityPendingCommandOrder.GetSemanticPriority(right.Command.CommandKind));
        }

        /// <summary>
        /// 比较完整 ASC owner 身份。
        /// </summary>
        private static int CompareOwner(in OwnerAscHandle left, in OwnerAscHandle right)
        {
            var comparison = left.AscStableId.CompareTo(right.AscStableId);
            return comparison != 0 ? comparison : left.AscGeneration.CompareTo(right.AscGeneration);
        }

        /// <summary>
        /// 比较 Activation 的 owner、slot 与 generation 身份。
        /// </summary>
        private static int CompareActivation(
            in AbilityActivationHandle left,
            in AbilityActivationHandle right)
        {
            var comparison = CompareOwner(in left.OwnerAsc, in right.OwnerAsc);
            if (comparison != 0)
                return comparison;
            comparison = left.SlotIndex.CompareTo(right.SlotIndex);
            return comparison != 0
                ? comparison
                : left.SlotGeneration.CompareTo(right.SlotGeneration);
        }

        /// <summary>
        /// 比较 Continuation 的稳定 slot 与 generation 身份。
        /// </summary>
        private static int CompareContinuation(
            in AbilityContinuationHandle left,
            in AbilityContinuationHandle right)
        {
            var comparison = left.SlotIndex.CompareTo(right.SlotIndex);
            return comparison != 0
                ? comparison
                : left.SlotGeneration.CompareTo(right.SlotGeneration);
        }
    }

    /// <summary>
    /// 保存 admission 前已解析的稳定目标记录，禁止携带 raw Entity 到后续 lane。
    /// </summary>
    internal struct GasResolvedTargetRecord
    {
        public int OwnerPlanOrdinal;
        public OwnerAscHandle TargetAsc;
        public int TargetOrdinal;
        public int DefinitionIndex;
        public ulong ApplicationId;
        public byte TargetIsAlive;
    }

    /// <summary>
    /// 保存 admission 前闭合展开的 Effect 操作计数载体，正式身份只能在成功 Owner Commit 后产生。
    /// </summary>
    internal struct GasEffectOperationRecord
    {
        public int OwnerPlanOrdinal;
        public int TargetOrdinal;
        public int ProgramNodeOrdinal;
        public int DefinitionIndex;
        public OwnerAscHandle SourceAsc;
        public OwnerAscHandle TargetAsc;
        public ulong ApplicationId;
        public ulong StartTick;
        public byte TargetIsAlive;
    }

    /// <summary>
    /// 保存 SourceSpecProjection 密封后的 source-bound effect spec，后续 lane 不再直接消费未投影 operation。
    /// </summary>
    internal struct GasSourceSpecRecord
    {
        public int OperationOrdinal;
        public int DefinitionIndex;
        public OwnerAscHandle SourceAsc;
        public OwnerAscHandle TargetAsc;
        public ulong ApplicationId;
        public ulong StartTick;
        public byte TargetIsAlive;
    }

    /// <summary>
    /// 保存 target writer 线性化后的 typed application outcome，拒绝与成功都必须进入后续 fact 链。
    /// </summary>
    internal struct GasApplicationOutcomeRecord
    {
        public int OperationOrdinal;
        public GasGameplayEffectApplicationOutcome Outcome;
        public GasGameplayEffectTransactionFailure Failure;
        public OwnerAscHandle SourceAsc;
        public OwnerAscHandle TargetAsc;
        public ActiveEffectHandle ActiveEffect;
        public ulong ApplicationId;
        public int AppliedModifierCount;
        public byte DeathCrossed;
    }

    /// <summary>
    /// 保存 StableFactMerge 前的单条 Core reaction record，字段已足够独立投影到 scoped Boundary outbox。
    /// </summary>
    internal struct GasCoreFactRecord
    {
        public GasBoundaryFactScope Scope;
        public GasBoundaryFactPlane Plane;
        public GasBoundaryFactKind Kind;
        public OwnerAscHandle SourceAsc;
        public OwnerAscHandle TargetAsc;
        public ulong SimulationTick;
        public ushort SemanticPhaseOrdinal;
        public ushort WorkClassOrdinal;
        public ulong ParentCausalityId;
        public ulong SemanticId;
        public BoundaryFactPayload Payload;
        public int OperationOrdinal;
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
        public NativeArray<GasSourceSpecRecord> SourceSpecs;
        public NativeArray<GasApplicationOutcomeRecord> ApplicationOutcomes;
        public NativeArray<GasCoreFactRecord> CoreFacts;
        public NativeArray<GasAbilityRouteRecord> AbilityRoutes;
        public NativeArray<GasOwnerResourceDemand> OwnerDemands;
        public NativeArray<GasTargetResourceDemand> TargetDemands;
        public NativeArray<float> EvaluatorStack;

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
                SourceSpecs = CreateArray<GasSourceSpecRecord>(ClampLength(profile.MaxEffectOperationCount), allocator),
                ApplicationOutcomes = CreateArray<GasApplicationOutcomeRecord>(
                    ClampLength(profile.MaxEffectOperationCount), allocator),
                CoreFacts = CreateArray<GasCoreFactRecord>(ClampLength(profile.MaxCoreFactCount), allocator),
                AbilityRoutes = CreateArray<GasAbilityRouteRecord>(ClampLength(profile.MaxNextTickRouteCount), allocator),
                OwnerDemands = CreateArray<GasOwnerResourceDemand>(ClampLength(profile.MaxOwnerReservationCount), allocator),
                TargetDemands = CreateArray<GasTargetResourceDemand>(ClampLength(profile.MaxTargetReservationCount), allocator),
                EvaluatorStack = CreateArray<float>(
                    ClampLength(profile.MaxEffectOperationCount), allocator),
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
