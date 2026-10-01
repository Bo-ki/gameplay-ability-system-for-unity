using System;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 冻结 Runtime v1 Catalog 的 schema 版本，安装端必须精确匹配且不得回退旧 schema。
    /// </summary>
    public static class GasDefinitionCatalogSchema
    {
        public const int Version = 1;
        public const ulong Hash = 0x4753415356310001UL;
    }

    /// <summary>
    /// 描述 Catalog 内连续数组的一段半开区间。
    /// </summary>
    public struct GasCatalogRange
    {
        public int Start;
        public int Count;
    }

    /// <summary>
    /// 把稳定 Definition ID 映射到同类 Definition 数组索引。
    /// </summary>
    public struct GasDefinitionIndexEntry
    {
        public int DefinitionId;
        public int DefinitionIndex;
    }

    /// <summary>
    /// 声明 Effect requirement 的独立语义阶段，避免跨阶段 range 被压平。
    /// </summary>
    public enum GasRequirementPhase : byte
    {
        Application = 1,
        Ongoing = 2,
        Removal = 3,
        Immunity = 4,
        AbilityActivation = 5,
    }

    /// <summary>
    /// 声明 Tag requirement 的集合匹配方式。
    /// </summary>
    public enum GasTagRequirementMatch : byte
    {
        All = 1,
        Any = 2,
        None = 3,
    }

    /// <summary>
    /// 保存一个预解析 Tag requirement program 及其阶段归属。
    /// </summary>
    public struct GasRequirementBlob
    {
        public int RequirementId;
        public GasRequirementPhase Phase;
        public GasTagRequirementMatch Match;
        public GasCatalogRange TagIndexRange;
    }

    /// <summary>
    /// 区分 capture 从 source 还是 target ASC 读取。
    /// </summary>
    public enum GasCaptureOwner : byte
    {
        Source = 1,
        Target = 2,
    }

    /// <summary>
    /// 区分 capture 在 Spec 中冻结还是绑定 live revision。
    /// </summary>
    public enum GasCaptureBinding : byte
    {
        Snapshot = 1,
        Live = 2,
    }

    /// <summary>
    /// 冻结 capture 的合法解析时点，不允许 Runtime 任意时刻随机读取。
    /// </summary>
    public enum GasCapturePhase : byte
    {
        OwnerPlanBuild = 1,
        SourceSpecProjection = 2,
        TargetApplication = 3,
        TargetStabilization = 4,
        CrossAscMaintenance = 5,
    }

    /// <summary>
    /// 描述 Live capture 是否局限同 ASC 或跨 ASC 延迟传播。
    /// </summary>
    public enum GasLiveCaptureScope : byte
    {
        None = 0,
        SameAsc = 1,
        CrossAsc = 2,
    }

    /// <summary>
    /// 冻结 capture owner 消失后的显式处理策略。
    /// </summary>
    public enum GasCaptureGonePolicy : byte
    {
        RejectApplication = 1,
        FreezeLastValue = 2,
        RemoveConsumer = 3,
    }

    /// <summary>
    /// 声明闭世界 evaluator 允许读取的属性值视图。
    /// </summary>
    public enum GasAttributeValueView : byte
    {
        Base = 1,
        Current = 2,
        DefinitionMaxValue = 3,
        Final = 4,
        Bonus = 5,
        Contribution = 6,
        ModifierList = 7,
    }

    /// <summary>
    /// 汇总 evaluator 必须由 Catalog 提供的 ValueView 契约。
    /// </summary>
    [Flags]
    public enum GasAttributeValueViewMask : ushort
    {
        None = 0,
        Base = 1 << 0,
        Current = 1 << 1,
        DefinitionMaxValue = 1 << 2,
        Final = 1 << 3,
        Bonus = 1 << 4,
        Contribution = 1 << 5,
        ModifierList = 1 << 6,
    }

    /// <summary>
    /// 保存 Source/Target × Snapshot/Live capture 的完整生成期描述。
    /// </summary>
    public struct GasCaptureDescriptorBlob
    {
        public int CaptureOrdinal;
        public GasCaptureOwner Owner;
        public GasCaptureBinding Binding;
        public GasCapturePhase Phase;
        public GasLiveCaptureScope LiveScope;
        public GasCaptureGonePolicy GonePolicy;
        public GasAttributeValueView ValueView;
        public int AttributeLayoutIndex;
        public int ConsumerNodeOrdinal;
        public int ConsumerFieldOrdinal;
    }

    /// <summary>
    /// 把 evaluator 的每个显式 ValueView 绑定到已解析 AttributeLayout index。
    /// </summary>
    public struct GasValueViewDescriptorBlob
    {
        public int AttributeLayoutIndex;
        public GasAttributeValueView ValueView;
    }

    /// <summary>
    /// 冻结逻辑目标解析规则，Self 必须显式声明。
    /// </summary>
    public enum GasLogicalTargetPolicy : byte
    {
        Self = 1,
        FrozenAsc = 2,
        ResolveAtCommit = 3,
    }

    /// <summary>
    /// 冻结目标 Avatar 是否跟随 ASC 或要求原 binding generation。
    /// </summary>
    public enum GasAvatarTargetPolicy : byte
    {
        FollowAsc = 1,
        RequireSameAvatar = 2,
    }

    /// <summary>
    /// 冻结空间目标使用快照还是在批准阶段重新采样。
    /// </summary>
    public enum GasSpatialTargetPolicy : byte
    {
        None = 1,
        FrozenSpatial = 2,
        ResampleAtApplication = 3,
    }

    /// <summary>
    /// 冻结 target application 对生命状态的准入规则。
    /// </summary>
    public enum GasTargetLifePolicy : byte
    {
        AliveOnly = 1,
        RequireDead = 2,
        AnyLifeState = 3,
    }

    /// <summary>
    /// 将逻辑 ASC、Avatar、空间与生命策略正交保存。
    /// </summary>
    public struct GasTargetPolicyBlob
    {
        public GasLogicalTargetPolicy LogicalTarget;
        public GasAvatarTargetPolicy Avatar;
        public GasSpatialTargetPolicy Spatial;
        public GasTargetLifePolicy Life;
    }

    /// <summary>
    /// 冻结 GameplayEffect 是否创建长期 ActiveEffect slot。
    /// </summary>
    public enum GasEffectLifetimePolicy : byte
    {
        Instant = 1,
        InstantExecution = 2,
        Duration = 3,
        Infinite = 4,
    }

    /// <summary>
    /// 声明 modifier 对 Attribute Base 的运算类型。
    /// </summary>
    public enum GasModifierOperation : byte
    {
        Add = 1,
        Multiply = 2,
        Divide = 3,
        Override = 4,
    }

    /// <summary>
    /// 定义 immutable postfix evaluator program 的通用指令集。
    /// </summary>
    public enum GasEvaluatorOpcode : byte
    {
        PushConstant = 1,
        PushCapture = 2,
        PushValueView = 3,
        PushStackCount = 4,
        Add = 5,
        Subtract = 6,
        Multiply = 7,
        Divide = 8,
        Negate = 9,
        Maximum = 10,
        Minimum = 11,
        Clamp = 12,
        CompareLess = 13,
        CompareLessOrEqual = 14,
        CompareEqual = 15,
        Select = 16,
    }

    /// <summary>
    /// 保存一条无业务命名、可由 generated pure glue 解释的 evaluator 指令。
    /// </summary>
    public struct GasEvaluatorInstructionBlob
    {
        public GasEvaluatorOpcode Opcode;
        public int OperandIndex;
        public float ConstantValue;
    }

    /// <summary>
    /// 保存一个 modifier 的预解析属性、运算和 capture range。
    /// </summary>
    public struct GasModifierDefinitionBlob
    {
        public int AttributeLayoutIndex;
        public GasModifierOperation Operation;
        public GasCatalogRange EvaluatorProgramRange;
        public GasCatalogRange CaptureRange;
    }

    /// <summary>
    /// 声明 Effect Spec 接收的 SetByCaller key 与必填性。
    /// </summary>
    public struct GasSetByCallerDescriptorBlob
    {
        public int KeyId;
        public int FieldOrdinal;
        public byte Required;
    }

    /// <summary>
    /// 声明 Effect Spec 可承载的封闭 TargetData 变体。
    /// </summary>
    public enum GasTargetDataVariant : byte
    {
        StableAsc = 1,
        FrozenSpatialPoint = 2,
        FrozenSpatialHit = 3,
        FrozenSpatialShape = 4,
    }

    /// <summary>
    /// 保存 TargetData tagged-union 变体的稳定 field ordinal 与必填性。
    /// </summary>
    public struct GasTargetDataDescriptorBlob
    {
        public GasTargetDataVariant Variant;
        public int FieldOrdinal;
        public byte Required;
    }

    /// <summary>
    /// 声明 EffectContext 中允许进入 Spec 的封闭字段种类。
    /// </summary>
    public enum GasEffectContextFieldKind : byte
    {
        CausalityId = 1,
        ParentContextId = 2,
        AbilityActivationId = 3,
        SourceAvatarBindingGeneration = 4,
        TargetAvatarBindingGeneration = 5,
        OriginSnapshot = 6,
        HitSnapshot = 7,
        Level = 8,
    }

    /// <summary>
    /// 保存 EffectContext 字段的稳定 ordinal 与必填性。
    /// </summary>
    public struct GasEffectContextFieldDescriptorBlob
    {
        public GasEffectContextFieldKind Field;
        public int FieldOrdinal;
        public byte Required;
    }

    /// <summary>
    /// 保存 DirectEffectProgram 的静态节点与生成期展开上限。
    /// </summary>
    public struct GasDirectEffectProgramNodeBlob
    {
        public int NodeOrdinal;
        public int EffectDefinitionId;
        public int MaximumTargetCount;
        public int MaximumOutputCount;
    }

    /// <summary>
    /// 标识 Cue 定义允许投影的四阶段 request。
    /// </summary>
    [Flags]
    public enum GasCuePhaseFlags : byte
    {
        None = 0,
        OnActive = 1 << 0,
        WhileActive = 1 << 1,
        Executed = 1 << 2,
        Removed = 1 << 3,
    }

    /// <summary>
    /// 保存 Definition 对 Cue code 的稳定 ordinal 与阶段契约。
    /// </summary>
    public struct GasCueReferenceBlob
    {
        public int CueDefinitionId;
        public int CueDefinitionOrdinal;
        public GasCuePhaseFlags Phases;
    }

    /// <summary>
    /// 冻结 stack key 参与维度，避免 Runtime 隐式选择聚合身份。
    /// </summary>
    [Flags]
    public enum GasStackKeyFields : byte
    {
        None = 0,
        Definition = 1 << 0,
        TargetAsc = 1 << 1,
        SourceAsc = 1 << 2,
        StackingId = 1 << 3,
    }

    /// <summary>
    /// 冻结 ActiveEffect stack 聚合策略。
    /// </summary>
    public enum GasStackPolicy : byte
    {
        None = 0,
        AggregateBySource = 1,
        AggregateByTarget = 2,
    }

    /// <summary>
    /// 冻结 reapply 后 active payload 的替换策略。
    /// </summary>
    public enum GasStackPayloadPolicy : byte
    {
        None = 0,
        ReplaceLatestPayloadAndProvenance = 1,
    }

    /// <summary>
    /// 冻结达到 stack limit 后的新 application 是拒绝还是成功但保持上限。
    /// </summary>
    public enum GasStackLimitApplicationPolicy : byte
    {
        None = 0,
        RejectAtLimit = 1,
        AcceptAndKeepLimit = 2,
    }

    /// <summary>
    /// 冻结成功 application 是否刷新 duration。
    /// </summary>
    public enum GasDurationRefreshPolicy : byte
    {
        Never = 0,
        OnSuccessfulApplication = 1,
    }

    /// <summary>
    /// 冻结成功 application 是否重置 period due tick。
    /// </summary>
    public enum GasPeriodResetPolicy : byte
    {
        Never = 0,
        OnSuccessfulApplication = 1,
    }

    /// <summary>
    /// 冻结 effect 到期时的 stack 处理。
    /// </summary>
    public enum GasExpiryPolicy : byte
    {
        Remove = 1,
        RemoveOneStackAndRefreshDuration = 2,
    }

    /// <summary>
    /// 冻结 expiry 刷新后 period 的处理。
    /// </summary>
    public enum GasExpiryPeriodPolicy : byte
    {
        Stop = 1,
        Reset = 2,
    }

    /// <summary>
    /// 冻结 DueTick 与 EndTick 相等时的语义顺序。
    /// </summary>
    public enum GasExpirySameTickPolicy : byte
    {
        ExpiryBeforePeriodDue = 1,
        PeriodDueBeforeExpiry = 2,
    }

    /// <summary>
    /// 冻结 inhibition 是否暂停 duration。
    /// </summary>
    public enum GasInhibitTimePolicy : byte
    {
        PauseDuration = 1,
        DurationContinues = 2,
    }

    /// <summary>
    /// 冻结 ActiveEffect inhibited 时法定 period due 的执行行为。
    /// </summary>
    public enum GasInhibitedPeriodPolicy : byte
    {
        None = 0,
        PauseSchedule = 1,
        SkipExecution = 2,
        ContinueExecution = 3,
    }

    /// <summary>
    /// 冻结恢复后遗漏 period 的追赶策略。
    /// </summary>
    public enum GasMissedPeriodPolicy : byte
    {
        SkipNoCatchUp = 1,
        ExecuteOnce = 2,
        CatchUpBounded = 3,
    }

    /// <summary>
    /// 保存每个 Definition 的静态展开和 scratch 容量上限。
    /// </summary>
    public struct GasDefinitionMaxima
    {
        public int MaximumTargetCount;
        public int MaximumPlannedApplicationCount;
        public int MaximumRequirementCount;
        public int MaximumCaptureDescriptorCount;
        public int MaximumModifierCount;
        public int MaximumDirectProgramNodeCount;
        public int MaximumDirectProgramOutputCount;
        public int MaximumCueCount;
        public int MaximumValueViewCount;
        public int MaximumEvaluatorInstructionCount;
        public int MaximumSetByCallerCount;
        public int MaximumTargetDataCount;
        public int MaximumEffectContextFieldCount;
    }

    /// <summary>
    /// 冻结 Ability Commit 对 owner ASC 同一属性权威执行的单一 cost 变更，禁止投影为普通 self GE。
    /// </summary>
    public struct GasCostMutationContractBlob
    {
        public byte Enabled;
        public int AttributeLayoutIndex;
        public float BaseDelta;
        public float CurrentDelta;
    }

    /// <summary>
    /// 冻结 Ability Commit 在 owner ASC 创建的单一 cooldown gate 及其可选 owned Tag。
    /// </summary>
    public struct GasCooldownGateContractBlob
    {
        public byte Enabled;
        public int GateKey;
        public int DurationTicks;
        public int OwnedTagIndex;
    }

    /// <summary>
    /// 保存一个 Ability 的不可变 owner commit、target、DirectEffectProgram 与容量契约。
    /// </summary>
    public struct GasAbilityDefinitionBlob
    {
        public int DefinitionId;
        public int Level;
        public int MaxConcurrentActivations;
        public GasCatalogRange ActivationRequirementRange;
        public GasCostMutationContractBlob CostMutationContract;
        public GasCooldownGateContractBlob CooldownGateContract;
        public GasTargetPolicyBlob TargetPolicy;
        public GasCatalogRange DirectEffectProgramRange;
        public GasCatalogRange CueRange;
        public GasDefinitionMaxima Maxima;
    }

    /// <summary>
    /// 保存一个 GameplayEffect 的 phase-aware、capture、policy 与纯 evaluator 契约。
    /// </summary>
    public struct GasGameplayEffectDefinitionBlob
    {
        public int DefinitionId;
        public GasEffectLifetimePolicy Lifetime;
        public GasTargetPolicyBlob TargetPolicy;
        public GasCatalogRange ApplicationRequirementRange;
        public GasCatalogRange OngoingRequirementRange;
        public GasCatalogRange RemovalRequirementRange;
        public GasCatalogRange ImmunityRequirementRange;
        public GasCatalogRange CaptureRange;
        public GasCatalogRange ModifierRange;
        public GasCatalogRange DirectEffectProgramRange;
        public GasCatalogRange CueRange;
        public GasCatalogRange ValueViewRange;
        public GasCatalogRange EvaluatorProgramRange;
        public GasCatalogRange SetByCallerRange;
        public GasCatalogRange TargetDataRange;
        public GasCatalogRange EffectContextFieldRange;
        public GasAttributeValueViewMask RequiredValueViews;
        public int DurationTicks;
        public int PeriodTicks;
        public int StackLimit;
        public GasStackKeyFields StackKey;
        public GasStackPolicy StackPolicy;
        public GasStackPayloadPolicy StackPayloadPolicy;
        public GasStackLimitApplicationPolicy StackLimitApplicationPolicy;
        public GasDurationRefreshPolicy DurationRefreshPolicy;
        public GasPeriodResetPolicy PeriodResetPolicy;
        public GasExpiryPolicy ExpiryPolicy;
        public GasExpiryPeriodPolicy ExpiryPeriodPolicy;
        public GasExpirySameTickPolicy ExpirySameTickPolicy;
        public GasInhibitTimePolicy InhibitTimePolicy;
        public GasInhibitedPeriodPolicy InhibitedPeriodPolicy;
        public GasMissedPeriodPolicy MissedPeriodPolicy;
        public byte ExecuteOnApplication;
        public GasDefinitionMaxima Maxima;
    }

    /// <summary>
    /// 标识 Attribute 在 Runtime 权威事务中的闭集领域职责；默认 None 不触发隐式业务规则。
    /// </summary>
    public enum GasAttributeDomainRole : byte
    {
        None = 0,
        Health = 1,
    }

    /// <summary>
    /// 保存 dense AttributeLayout 中一个稳定属性的 clamp、领域职责与默认值元数据。
    /// </summary>
    public struct GasAttributeLayoutEntryBlob
    {
        public int AttributeId;
        public int LayoutIndex;
        public GasAttributeDomainRole DomainRole;
        public float DefaultValue;
        public float MinimumValue;
        public float MaximumValue;
        public byte ClampMinimum;
        public byte ClampMaximum;
    }

    /// <summary>
    /// 保存 Session immutable AttributeLayout 及其独立 hash。
    /// </summary>
    public struct GasAttributeLayoutBlob
    {
        public ulong LayoutHash;
        public BlobArray<GasAttributeLayoutEntryBlob> Entries;
    }

    /// <summary>
    /// 保存 dense TagCatalog 中一个 Tag 及其 ancestor chain range。
    /// </summary>
    public struct GasTagCatalogEntryBlob
    {
        public int TagId;
        public int TagIndex;
        public GasCatalogRange AncestorIndexRange;
    }

    /// <summary>
    /// 保存 Session immutable TagCatalog、ancestor chain 与独立 hash。
    /// </summary>
    public struct GasTagCatalogBlob
    {
        public ulong CatalogHash;
        public BlobArray<GasTagCatalogEntryBlob> Entries;
        public BlobArray<int> AncestorIndices;
    }

    /// <summary>
    /// Runtime v1 唯一 immutable Definition Catalog Blob 根。
    /// </summary>
    public struct GasDefinitionCatalogBlob
    {
        public int SchemaVersion;
        public ulong SchemaHash;
        public ulong ContentHash;
        public GasAttributeLayoutBlob AttributeLayout;
        public GasTagCatalogBlob TagCatalog;
        public BlobArray<GasDefinitionIndexEntry> AbilityIndex;
        public BlobArray<GasAbilityDefinitionBlob> Abilities;
        public BlobArray<GasDefinitionIndexEntry> GameplayEffectIndex;
        public BlobArray<GasGameplayEffectDefinitionBlob> GameplayEffects;
        public BlobArray<GasRequirementBlob> Requirements;
        public BlobArray<int> RequirementTagIndices;
        public BlobArray<GasCaptureDescriptorBlob> CaptureDescriptors;
        public BlobArray<GasModifierDefinitionBlob> Modifiers;
        public BlobArray<GasDirectEffectProgramNodeBlob> DirectEffectProgramNodes;
        public BlobArray<GasCueReferenceBlob> CueReferences;
        public BlobArray<GasValueViewDescriptorBlob> ValueViews;
        public BlobArray<GasEvaluatorInstructionBlob> EvaluatorInstructions;
        public BlobArray<GasSetByCallerDescriptorBlob> SetByCallerDescriptors;
        public BlobArray<GasTargetDataDescriptorBlob> TargetDataDescriptors;
        public BlobArray<GasEffectContextFieldDescriptorBlob> EffectContextFieldDescriptors;
    }
}
