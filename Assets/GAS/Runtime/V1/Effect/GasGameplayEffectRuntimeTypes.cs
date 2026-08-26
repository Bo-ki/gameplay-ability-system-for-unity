using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 区分 GameplayEffect application 在 target writer 中的完整业务结果。
    /// </summary>
    internal enum GasGameplayEffectApplicationOutcome : byte
    {
        None,
        AppliedInstant,
        CreatedActive,
        MergedStack,
        RejectedRequirement,
        RejectedImmunity,
        RejectedTargetLife,
        RejectedStackPolicy,
        RejectedStaleBinding,
        RejectedDefinition,
        InfrastructureFault,
        PeriodTickExecuted,
    }

    /// <summary>
    /// 标识 target transaction 失败的基础设施或业务原因，供 admission/diagnostics 分层处理。
    /// </summary>
    internal enum GasGameplayEffectTransactionFailure : byte
    {
        None,
        InvalidIdentity,
        InvalidBufferShape,
        InvalidDefinition,
        InvalidTargetLife,
        RequirementRejected,
        ImmunityRejected,
        StackRejected,
        ActiveEffectCapacity,
        PayloadCapacity,
        EvaluatorFailure,
        AttributeMutationFailure,
        TagMutationFailure,
    }

    /// <summary>
    /// 保存一条已在 Target Resolve 阶段冻结的 Effect application intent。
    /// </summary>
    internal struct GasGameplayEffectApplicationRequest
    {
        public ulong SimulationEpoch;
        public OwnerAscHandle SourceAsc;
        public OwnerAscHandle TargetAsc;
        public int DefinitionIndex;
        public ulong ApplicationId;
        public ulong StartTick;
        public ulong CausalityId;
        /// <summary>
        /// 标记该 request 是否由 ActiveEffect period due 产生，而非新的 application。
        /// </summary>
        public byte IsPeriodTick;
        /// <summary>
        /// 保存 request 对应的 period execution ordinal，和 ApplicationId 共同构成执行身份。
        /// </summary>
        public uint PeriodExecutionOrdinal;
        public byte TargetIsAlive;
        public int PayloadLength;
        public int CaptureValueCount;
        public int ValueViewCount;
    }

    /// <summary>
    /// 返回 application outcome、生成句柄与可审计的变更摘要。
    /// </summary>
    internal struct GasGameplayEffectApplicationResult
    {
        public GasGameplayEffectApplicationOutcome Outcome;
        public GasGameplayEffectTransactionFailure Failure;
        public ActiveEffectHandle ActiveEffect;
        public ulong ApplicationId;
        public int AppliedModifierCount;
        public int MutationStart;
        public int MutationCount;
        public byte DeathCrossed;
        public ulong DeathTransitionId;
        public float DeathOverkill;
        public int DeathAttributeLayoutIndex;
        public ulong DeathContributorId;
        public ulong CausalityId;
        /// <summary>
        /// 标记该 outcome 是否由 ActiveEffect period due 产生，而非新的 application。
        /// </summary>
        public byte IsPeriodTick;
        /// <summary>
        /// 保存 period execution 的单调 ordinal，和 ApplicationId 共同构成执行身份。
        /// </summary>
        public uint PeriodExecutionOrdinal;
    }

    /// <summary>
    /// 保存 effect transaction 需要的 ASC-local authority buffers，减少 Job 入口参数的错配风险。
    /// </summary>
    internal struct GasGameplayEffectTargetBuffers
    {
        public DynamicBuffer<ActiveEffectSlot> ActiveEffects;
        public DynamicBuffer<AttributeValueSlot> Attributes;
        public DynamicBuffer<AttributeDirtyWord> AttributeDirtyWords;
        public DynamicBuffer<TagCountSlot> TagCounts;
        public DynamicBuffer<TagPresenceWord> TagPresenceWords;
        public DynamicBuffer<GasPayloadRangeRecord> PayloadRanges;
        public DynamicBuffer<GasPayloadValueSlot> PayloadValues;
    }
}
