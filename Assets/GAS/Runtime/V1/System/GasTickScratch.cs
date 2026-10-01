using Unity.Collections;
using Unity.Entities;

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
    /// 定义 BattleOutcome 的稳定结果码；胜负阵营通过事实 payload 的 SideId/TeamId 携带。
    /// </summary>
    internal static class GasBattleOutcomeCode
    {
        internal const int Draw = 0;
        internal const int Winner = 1;
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
        public int TargetResolveRejectionCount;
        public int AttributeMutationDemand;
        public int AttributeMutationCount;
        public int PeriodTickDemand;
        public int PeriodMutationDemand;
        public int DeathFactCount;
        public int CoreFactCount;
        public int BoundaryFactCount;
        /// <summary>
        /// 保存 admission 已为当前 Tick 预留的 BattleOutcome 上界。
        /// </summary>
        public int TerminalOutcomeDemand;
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
        public byte RequiresSourceConsume;
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
        public ulong CausalityId;
        public ulong TargetAvatarStableId;
        public uint TargetAvatarBindingGeneration;
        public GasBoundarySpatialSnapshot SpatialSnapshot;
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
        public ulong CausalityId;
        public ulong TargetAvatarStableId;
        public uint TargetAvatarBindingGeneration;
        public GasBoundarySpatialSnapshot SpatialSnapshot;
    }

    /// <summary>
    /// 保存 TargetResolve 无法建立 target binding 时的 typed rejection；不把无效目标写入 effect operation。
    /// </summary>
    internal struct GasTargetResolveRejectionRecord
    {
        public int OwnerPlanOrdinal;
        public int ProgramNodeOrdinal;
        public int DefinitionIndex;
        public OwnerAscHandle SourceAsc;
        public OwnerAscHandle TargetAsc;
        public ulong ApplicationId;
        public ulong CausalityId;
        public GasBoundaryFactScope Scope;
        public GasGameplayEffectApplicationOutcome Outcome;
        public GasGameplayEffectTransactionFailure Failure;
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
        public ulong CausalityId;
        public GasBoundaryFactScope Scope;
        /// <summary>
        /// 指向 Tick-local capture 投影行；Source Snapshot 与 Target Snapshot 在同一行内按 descriptor ordinal 对齐。
        /// </summary>
        public int CaptureStart;
        public int CaptureCount;
        /// <summary>
        /// 指向 Tick-local ValueView 投影行，Evaluator 只读取这段冻结数值。
        /// </summary>
        public int ValueViewStart;
        public int ValueViewCount;
        public ulong TargetAvatarStableId;
        public uint TargetAvatarBindingGeneration;
        public GasBoundarySpatialSnapshot SpatialSnapshot;
        public byte IsTargetResolveRejection;
        public GasGameplayEffectApplicationOutcome RejectionOutcome;
        public GasGameplayEffectTransactionFailure RejectionFailure;
    }

    /// <summary>
    /// 保存 target writer 线性化后的 typed application outcome，拒绝与成功都必须进入后续 fact 链。
    /// </summary>
    internal struct GasApplicationOutcomeRecord
    {
        public int OperationOrdinal;
        public int DefinitionId;
        public GasGameplayEffectApplicationOutcome Outcome;
        public GasGameplayEffectTransactionFailure Failure;
        public OwnerAscHandle SourceAsc;
        public OwnerAscHandle TargetAsc;
        public ActiveEffectHandle ActiveEffect;
        public ulong ApplicationId;
        public int AppliedModifierCount;
        public int AttributeMutationStart;
        public int AttributeMutationCount;
        public byte DeathCrossed;
        public ulong DeathTransitionId;
        public float DeathOverkill;
        public int DeathAttributeLayoutIndex;
        public ulong DeathContributorId;
        public ulong CausalityId;
        public GasBoundaryFactScope Scope;
        /// <summary>
        /// 标记该 outcome 是否为跨 Tick ActiveEffect period execution。
        /// </summary>
        public byte IsPeriodTick;
        /// <summary>
        /// 保存 period execution ordinal，和 ApplicationId 共同构成稳定执行身份。
        /// </summary>
        public uint PeriodExecutionOrdinal;
    }

    /// <summary>
    /// 保存一条带 application provenance 的 Attribute mutation，供 CoreFact 与 death crossing 共同消费。
    /// </summary>
    internal struct GasAttributeMutationOutcomeRecord
    {
        public int OperationOrdinal;
        public int ModifierOrdinal;
        public int DefinitionId;
        public OwnerAscHandle SourceAsc;
        public OwnerAscHandle TargetAsc;
        public ulong ApplicationId;
        public ulong CausalityId;
        public ulong ContributorId;
        /// <summary>
        /// 标记该 mutation 是否由 period body 产生。
        /// </summary>
        public byte IsPeriodTick;
        /// <summary>
        /// 保存 period body 的 execution ordinal。
        /// </summary>
        public uint PeriodExecutionOrdinal;
        public GasAttributeMutationRecord Mutation;
        public ulong DeathTransitionId;
        public float DeathOverkill;
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
        /// <summary>
        /// 保存 BattleInstance scope 的显式身份；Battle/Session fact 共用 Session 物理 outbox，但不能丢失逻辑战局代际。
        /// </summary>
        public BattleInstanceHandle BattleInstance;
        public ulong SimulationTick;
        public ushort SemanticPhaseOrdinal;
        public ushort WorkClassOrdinal;
        public ulong ParentCausalityId;
        public ulong SemanticId;
        public BoundaryFactPayload Payload;
        public int OperationOrdinal;
        public int FactOrdinal;
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
    /// 保存单个 Ready ASC 在 TargetPrepare 阶段生成的完整 tick-local authority 快照。
    /// </summary>
    internal struct GasTargetShadowState
    {
        public Entity Target;
        public OwnerAscHandle OwnerAsc;
        public AscLifecycle Lifecycle;
        public AscSlabHeads SlabHeads;
        public GasPayloadRangeAllocatorState PayloadState;
        public int ActiveEffectCount;
        public int AttributeCount;
        public int AttributeDirtyWordCount;
        public int TagCount;
        public int TagPresenceWordCount;
        public int PayloadRangeCount;
        public int PayloadValueCount;
        public byte Prepared;
    }

    /// <summary>
    /// 保存 FinalPublishFaultReduce 对整 Tick Target/Terminal/Route/Boundary 的唯一发布裁决。
    /// </summary>
    internal struct GasFinalPublishDecision
    {
        public GasTickAdmissionFailureReason FailureReason;
        public byte Succeeded;
    }

    /// <summary>
    /// 冻结 TerminalPrepare 对单个 BattleInstance 的最终业务镜像，发布阶段只按索引覆盖。
    /// </summary>
    internal struct GasTerminalBattlePublishIntent
    {
        public int BattleIndex;
        public BattleInstanceSlot Value;
        public byte Used;
    }

    /// <summary>
    /// 冻结 TerminalPrepare 可能产生的 Session lifecycle 推进，避免 prepare 写入 authority。
    /// </summary>
    internal struct GasSessionLifecyclePublishIntent
    {
        public GasSessionLifecycle Value;
        public byte Used;
    }

    /// <summary>
    /// 冻结 RoutePrepare 对 owner-local PendingCommand 稳定槽的最终覆盖。
    /// </summary>
    internal struct GasPendingCommandSlotPublishIntent
    {
        public Entity Asc;
        public int SlotIndex;
        public PendingCommand Value;
        public byte Used;
    }

    /// <summary>
    /// 冻结 RoutePrepare 对单 ASC PendingCommand slab head 的最终值。
    /// </summary>
    internal struct GasPendingCommandHeadPublishIntent
    {
        public Entity Asc;
        public GasSlabHead Value;
        public byte Used;
    }

    /// <summary>
    /// 冻结 BoundaryPrepare 的物理 owner、完整 fact 与 append 后 drain state。
    /// </summary>
    internal struct GasBoundaryFactPublishIntent
    {
        public Entity Owner;
        public BoundaryFactBuffer Fact;
        public BoundaryDrainState StateAfter;
        public byte Used;
    }

    /// <summary>
    /// 指定 final publish conformance fault 应在哪个 prepare 阶段收束后触发。
    /// </summary>
    internal enum GasFinalPublishPrepareStage : byte
    {
        None,
        Terminal,
        Route,
        Boundary,
    }

    /// <summary>
    /// 为 post-owner 原子发布测试提供显式 prepare 后故障点；正常 Session 不安装该组件。
    /// </summary>
    internal struct GasFinalPublishFaultInjection : IComponentData
    {
        public GasFinalPublishPrepareStage Stage;
        public int FailAfterPreparedIntentCount;
    }

    /// <summary>
    /// 为确定性 conformance fault 测试提供显式 Prepare 失败点；正常 Session 不安装该组件。
    /// </summary>
    internal struct GasTargetPrepareFaultInjection : IComponentData
    {
        public int FailAfterPreparedApplicationCount;
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
        public NativeArray<GasTargetResolveRejectionRecord> TargetResolveRejections;
        public NativeArray<GasSourceSpecRecord> SourceSpecs;
        public NativeArray<GasApplicationOutcomeRecord> ApplicationOutcomes;
        public NativeArray<GasAttributeMutationRecord> AttributeMutations;
        public NativeArray<GasAttributeMutationOutcomeRecord> MutationOutcomes;
        public NativeArray<GasCoreFactRecord> CoreFacts;
        public NativeArray<GasAbilityRouteRecord> AbilityRoutes;
        public NativeArray<GasOwnerResourceDemand> OwnerDemands;
        public NativeArray<GasTargetResourceDemand> TargetDemands;
        public NativeArray<float> EvaluatorStack;
        public NativeArray<GasTargetShadowState> TargetShadows;
        public NativeArray<GasFinalPublishDecision> FinalPublishDecision;
        public NativeArray<GasTerminalBattlePublishIntent> TerminalBattleIntents;
        public NativeArray<GasSessionLifecyclePublishIntent> SessionLifecycleIntent;
        public NativeArray<GasPendingCommandSlotPublishIntent> PendingCommandSlotIntents;
        public NativeArray<GasPendingCommandHeadPublishIntent> PendingCommandHeadIntents;
        public NativeArray<GasBoundaryFactPublishIntent> BoundaryFactIntents;
        public NativeArray<GasRequestTerminalIntent> RequestTerminalIntents;
        public NativeArray<ActiveEffectSlot> TargetActiveEffects;
        public NativeArray<AttributeValueSlot> TargetAttributes;
        public NativeArray<AttributeDirtyWord> TargetAttributeDirtyWords;
        public NativeArray<TagCountSlot> TargetTagCounts;
        public NativeArray<TagPresenceWord> TargetTagPresenceWords;
        public NativeArray<GasPayloadRangeRecord> TargetPayloadRanges;
        public NativeArray<GasPayloadValueSlot> TargetPayloadValues;
        public int TargetActiveEffectStride;
        public int TargetAttributeStride;
        public int TargetAttributeDirtyWordStride;
        public int TargetTagStride;
        public int TargetTagPresenceWordStride;
        public int TargetPayloadRangeStride;
        public int TargetPayloadValueStride;
        /// <summary>
        /// 保存每条 source-bound spec 的定长 capture 投影行；行起点由 GasSourceSpecRecord 冻结。
        /// </summary>
        public NativeArray<float> CaptureValues;
        /// <summary>
        /// 保存每条 target application 的定长 ValueView 投影行；禁止 evaluator 直接读取 ECS。
        /// </summary>
        public NativeArray<float> ValueViews;
        /// <summary>
        /// 为既有 ActiveEffect period execution 保留的独立输入行，避免覆盖新 application snapshot。
        /// </summary>
        public NativeArray<float> PeriodCaptureValues;
        public NativeArray<float> PeriodValueViews;
        public int CaptureStride;
        public int ValueViewStride;

        /// <summary>
        /// 按 ScaleProfile 逻辑上限创建全部定长容器，Job 只能在界内写入而不得扩容。
        /// </summary>
        public static GasTickScratch Create(
            in GasScaleProfile profile,
            AllocatorManager.AllocatorHandle allocator)
        {
            return Create(in profile, default, allocator);
        }

        /// <summary>
        /// 按 ScaleProfile 与 immutable Catalog 的生成期 descriptor maxima 创建 capture/value 输入平面。
        /// </summary>
        public static GasTickScratch Create(
            in GasScaleProfile profile,
            BlobAssetReference<GasDefinitionCatalogBlob> catalog,
            AllocatorManager.AllocatorHandle allocator)
        {
            var captureStride = 1;
            var valueViewStride = 1;
            var evaluatorLength = 1;
            var attributeCount = 0;
            var tagCount = 0;
            if (catalog.IsCreated)
            {
                ref var root = ref catalog.Value;
                attributeCount = root.AttributeLayout.Entries.Length;
                tagCount = root.TagCatalog.Entries.Length;
                for (var index = 0; index < root.GameplayEffects.Length; index++)
                {
                    var definition = root.GameplayEffects[index];
                    captureStride = Max(captureStride, definition.CaptureRange.Count);
                    valueViewStride = Max(valueViewStride, definition.ValueViewRange.Count);
                    evaluatorLength = Max(evaluatorLength, definition.EvaluatorProgramRange.Count);
                }
            }
            var rowCount = Max(1, profile.MaxEffectOperationCount);
            var captureLength = Product(rowCount, captureStride);
            var valueViewLength = Product(rowCount, valueViewStride);
            var targetCount = ClampLength(profile.MaxAscRegistryCount);
            var activeEffectStride = ClampLength(profile.MaxActiveEffectCount);
            var attributeDirtyWordStride = WordCount(attributeCount);
            var tagPresenceWordStride = WordCount(tagCount);
            var payloadRangeStride = ClampLength(profile.MaxPayloadRangeRecordCount);
            var payloadValueStride = ClampLength(profile.MaxPayloadValueCount);
            return new GasTickScratch
            {
                Execution = CreateArray<GasTickExecutionState>(1, allocator),
                Envelope = CreateArray<PlanExpandScratchEnvelopeToken>(1, allocator),
                Admission = CreateArray<GasAdmissionResult>(1, allocator),
                SealedCommands = CreateArray<GasSealedCommand>(ClampLength(profile.MaxBoundaryCommandCount), allocator),
                OwnerPlans = CreateArray<GasOwnerPlanRecord>(ClampLength(profile.MaxOwnerPlanCount), allocator),
                ResolvedTargets = CreateArray<GasResolvedTargetRecord>(ClampLength(profile.MaxResolvedTargetCount), allocator),
                EffectOperations = CreateArray<GasEffectOperationRecord>(ClampLength(profile.MaxEffectOperationCount), allocator),
                TargetResolveRejections = CreateArray<GasTargetResolveRejectionRecord>(
                    ClampLength(profile.MaxEffectOperationCount), allocator),
                SourceSpecs = CreateArray<GasSourceSpecRecord>(ClampLength(profile.MaxEffectOperationCount), allocator),
                ApplicationOutcomes = CreateArray<GasApplicationOutcomeRecord>(
                    ClampLength(MaxLength(profile.MaxEffectOperationCount, profile.MaxCoreFactCount)), allocator),
                AttributeMutations = CreateArray<GasAttributeMutationRecord>(
                    ClampLength(profile.MaxCoreFactCount), allocator),
                MutationOutcomes = CreateArray<GasAttributeMutationOutcomeRecord>(
                    ClampLength(profile.MaxCoreFactCount), allocator),
                CoreFacts = CreateArray<GasCoreFactRecord>(ClampLength(profile.MaxCoreFactCount), allocator),
                AbilityRoutes = CreateArray<GasAbilityRouteRecord>(ClampLength(profile.MaxNextTickRouteCount), allocator),
                OwnerDemands = CreateArray<GasOwnerResourceDemand>(ClampLength(profile.MaxOwnerReservationCount), allocator),
                TargetDemands = CreateArray<GasTargetResourceDemand>(ClampLength(profile.MaxTargetReservationCount), allocator),
                EvaluatorStack = CreateArray<float>(
                    Max(1, Max(ClampLength(profile.MaxEffectOperationCount), evaluatorLength)), allocator),
                TargetShadows = CreateArray<GasTargetShadowState>(targetCount, allocator),
                FinalPublishDecision = CreateArray<GasFinalPublishDecision>(1, allocator),
                TerminalBattleIntents = CreateArray<GasTerminalBattlePublishIntent>(
                    ClampLength(profile.MaxBattleInstanceCount), allocator),
                SessionLifecycleIntent = CreateArray<GasSessionLifecyclePublishIntent>(1, allocator),
                PendingCommandSlotIntents = CreateArray<GasPendingCommandSlotPublishIntent>(
                    Product(ClampLength(profile.MaxNextTickRouteCount), 2), allocator),
                PendingCommandHeadIntents = CreateArray<GasPendingCommandHeadPublishIntent>(
                    targetCount, allocator),
                BoundaryFactIntents = CreateArray<GasBoundaryFactPublishIntent>(
                    ClampLength(profile.MaxCoreFactCount), allocator),
                RequestTerminalIntents = CreateArray<GasRequestTerminalIntent>(
                    ClampLength(profile.MaxBoundaryCommandCount), allocator),
                TargetActiveEffects = CreateArray<ActiveEffectSlot>(
                    Product(targetCount, activeEffectStride), allocator),
                TargetAttributes = CreateArray<AttributeValueSlot>(
                    Product(targetCount, attributeCount), allocator),
                TargetAttributeDirtyWords = CreateArray<AttributeDirtyWord>(
                    Product(targetCount, attributeDirtyWordStride), allocator),
                TargetTagCounts = CreateArray<TagCountSlot>(
                    Product(targetCount, tagCount), allocator),
                TargetTagPresenceWords = CreateArray<TagPresenceWord>(
                    Product(targetCount, tagPresenceWordStride), allocator),
                TargetPayloadRanges = CreateArray<GasPayloadRangeRecord>(
                    Product(targetCount, payloadRangeStride), allocator),
                TargetPayloadValues = CreateArray<GasPayloadValueSlot>(
                    Product(targetCount, payloadValueStride), allocator),
                TargetActiveEffectStride = activeEffectStride,
                TargetAttributeStride = attributeCount,
                TargetAttributeDirtyWordStride = attributeDirtyWordStride,
                TargetTagStride = tagCount,
                TargetTagPresenceWordStride = tagPresenceWordStride,
                TargetPayloadRangeStride = payloadRangeStride,
                TargetPayloadValueStride = payloadValueStride,
                CaptureValues = CreateArray<float>(captureLength, allocator),
                ValueViews = CreateArray<float>(valueViewLength, allocator),
                PeriodCaptureValues = CreateArray<float>(captureStride, allocator),
                PeriodValueViews = CreateArray<float>(valueViewStride, allocator),
                CaptureStride = captureStride,
                ValueViewStride = valueViewStride,
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
        /// 为普通 application 与跨帧 period outcome 取统一 scratch 上界。
        /// </summary>
        private static int MaxLength(int left, int right)
        {
            return left > right ? left : right;
        }

        /// <summary>
        /// 返回两个非负容量中的较大值，并为损坏 profile 提供最小合法行宽。
        /// </summary>
        private static int Max(int left, int right)
        {
            return left > right ? left : right;
        }

        /// <summary>
        /// 计算定长输入平面大小；溢出时返回零让 admission 明确拒绝，而非主线程隐式扩容。
        /// </summary>
        private static int Product(int left, int right)
        {
            if (left <= 0 || right <= 0 || left > int.MaxValue / right)
                return 0;
            return left * right;
        }

        /// <summary>
        /// 将 dense 元素数转换为 64 位 presence/dirty word 数量。
        /// </summary>
        private static int WordCount(int elementCount)
        {
            return elementCount <= 0 ? 0 : ((elementCount - 1) / 64) + 1;
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
