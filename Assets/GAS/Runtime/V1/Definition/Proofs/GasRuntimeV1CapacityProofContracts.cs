using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 枚举 CapacityProof Spec 要求逐 Definition 固定的全部语义上界维度。
    /// </summary>
    public enum GasCapacityDimension : byte
    {
        ExpansionTargets = 1,
        ExpansionProgramNodes = 2,
        ExpansionProgramEdges = 3,
        ExpansionApplications = 4,
        ExpansionDynamicNextTickWork = 5,
        TouchedAttributes = 6,
        TouchedTags = 7,
        TouchedActiveEffects = 8,
        TouchedGrants = 9,
        TouchedActivations = 10,
        TouchedContinuations = 11,
        TouchedSubscriptions = 12,
        TargetOverlayEntries = 13,
        TargetOverlayBytes = 14,
        TargetReadYourWritesIndexBytes = 15,
        TargetPublishDeltaEntries = 16,
        TargetPublishDeltaBytes = 17,
        StabilizationTransitions = 18,
        StabilizationRounds = 19,
        StabilizationWorkUnits = 20,
        StabilizationSignedDependencyEdges = 21,
        StabilizationStateHashes = 22,
        ProjectionPayloadBytes = 23,
        ProjectionSnapshotContributors = 24,
        ProjectionLiveDirtyFanOut = 25,
        ProjectionCoalescedUpdates = 26,
        CleanupGrantChildren = 27,
        CleanupRightRemovals = 28,
        CleanupEmittedRefRetention = 29,
        CleanupEmittedRefHighWater = 30,
        CleanupContinuations = 31,
        CleanupSubscriptions = 32,
        ObservationFacts = 33,
        ObservationCueIntents = 34,
        ObservationBoundaryRecords = 35,
        ObservationEcbIntents = 36,
        RequirementDescriptors = 37,
        CaptureDescriptors = 38,
        Modifiers = 39,
        DirectProgramOutputs = 40,
        DirectProgramMaximumNodeFanOut = 41,
        ValueViews = 42,
        EvaluatorInstructions = 43,
        SetByCallerFields = 44,
        TargetDataFields = 45,
        EffectContextFields = 46,
        ApplicationRequirementDescriptors = 47,
        OngoingRequirementDescriptors = 48,
        RemovalRequirementDescriptors = 49,
        ImmunityRequirementDescriptors = 50,
        LiveCaptureDescriptors = 51,
        UnsupportedValueViewDescriptors = 52,
        CueMaximumOrdinal = 53,
        DirectProgramNestedDependencies = 54,
        DynamicDependencyBackEdges = 55,
    }

    /// <summary>
    /// 以 A 链提供的 opaque canonical key 区分 domain/kind/复合 ID，同时保留 Runtime v1 整数 ID。
    /// </summary>
    public readonly struct GasProofDefinitionIdentity
    {
        public readonly string CanonicalKey;
        public readonly int RuntimeDefinitionId;

        /// <summary>
        /// 构造一个不依赖 A 链 DTO 类型的最小 Definition identity 投影。
        /// </summary>
        public GasProofDefinitionIdentity(string canonicalKey, int runtimeDefinitionId)
        {
            CanonicalKey = canonicalKey ?? string.Empty;
            RuntimeDefinitionId = runtimeDefinitionId;
        }

        /// <summary>
        /// 判断 canonical key 与 Runtime ID 是否都可用于证据定位。
        /// </summary>
        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(CanonicalKey) && RuntimeDefinitionId > 0;
    }

    /// <summary>
    /// 将合法 Definition identity 与其 canonical ordinal、字段 provenance 一并固化到 payload。
    /// </summary>
    public struct GasCapacityDefinitionIdentityProofEntry
    {
        public string DefinitionKey;
        public int DefinitionId;
        public int DefinitionOrdinal;
        public GasProofProvenance Provenance;
    }

    /// <summary>
    /// 显式记录当前 CapacityProof 尚未覆盖的模型、provenance 关联与 Runtime authority 缺口。
    /// </summary>
    [Flags]
    public enum GasCapacityProofCoverageGap : uint
    {
        None = 0,
        CanonicalGraphAdapterMissing = 1 << 0,
        SemanticDimensionModelIncomplete = 1 << 1,
        RuntimeRouteFormulaIncomplete = 1 << 2,
        RuntimeFactFormulaIncomplete = 1 << 3,
        AuthorityConsumerMissing = 1 << 4,
        ProvenanceBindingIncomplete = 1 << 5,
    }

    /// <summary>
    /// 保存一个 Definition 的单项 canonical 上界与字段 provenance。
    /// </summary>
    public struct GasCapacityBoundProofEntry
    {
        public string DefinitionKey;
        public int DefinitionId;
        public int DefinitionOrdinal;
        public GasCapacityDimension Dimension;
        public long Maximum;
        public GasProofProvenance Provenance;
    }

    /// <summary>
    /// 保存当前 Runtime v1 物理容量与生成期推导需求的逐项对账。
    /// </summary>
    public struct GasRuntimeCapacityLimitEntry
    {
        public string LimitId;
        public string ProfileField;
        public string Formula;
        public long Required;
        public long Available;
        public string Unit;
        public GasProofConsumerStatus ConsumerStatus;
        public string FirstConsumer;
    }

    /// <summary>
    /// 标识内存证据对应真实定长分配、持久 Buffer 预留或仅计算汇总。
    /// </summary>
    public enum GasCapacityMemoryKind : byte
    {
        ScratchNativeArray = 1,
        DurableFixedLength = 2,
        DurableBufferCapacity = 3,
        Summary = 4,
    }

    /// <summary>
    /// 保存目标所需 checked 元素包络、当前宿主 ABI 与 payload 字节，超 int 时不冒充 Runtime 实际分配。
    /// </summary>
    public struct GasCapacityMemoryEvidence
    {
        public string EvidenceId;
        public GasCapacityMemoryKind MemoryKind;
        public string Owner;
        public string ElementTypeName;
        public string Formula;
        public long ElementCount;
        public int ElementSizeBytes;
        public int AlignmentBytes;
        public long CheckedByteCount;
        public string AbiFingerprint;
    }

    /// <summary>
    /// 汇总目标规模的已盘点元素 payload；不包含 allocator、container、chunk 与额外临时峰值。
    /// </summary>
    public struct GasCapacityMemorySummary
    {
        public long OneTickScratchBytes;
        public long EffectiveTickBatchScratchBytes;
        public long DeclaredHardCapTickBatchScratchBytes;
        public long DurableSessionBytes;
        public long DurableBytesPerAsc;
        public long DurableAllAscBytes;
        public long EffectiveScopedPayloadBytes;
        public long DeclaredHardCapScopedPayloadBytes;
        public long DeclaredMemoryBudgetBytes;
        public string LayoutAbiHash;
    }

    /// <summary>
    /// 保存不依赖发布控制面的独立 Runtime v1 CapacityProof payload 与 Red 证据。
    /// </summary>
    public sealed class GasRuntimeV1CapacityProofPayload
    {
        public int ProofSchemaVersion;
        public string AlgorithmVersion;
        public int GraphSchemaVersion;
        public string GeneratorVersion;
        public string CanonicalGraphHash;
        public string ContractMatrixHash;
        public string TargetScaleId;
        public string LayoutHash;
        public string LayoutAbiHash;
        public string ProofHash;
        public GasScaleProfile Profile;
        public GasProofProvenance ScaleProfileProvenance;

        /// <summary>
        /// 保存独立于 failure 文本的一等内存预算来源，并直接进入 ProofHash。
        /// </summary>
        public GasProofProvenance MemoryBudgetProvenance;

        /// <summary>
        /// 固定当前模型仍缺失的覆盖面；非 None 时 payload 必须保持 Incomplete/Red。
        /// </summary>
        public GasCapacityProofCoverageGap CoverageGaps;

        /// <summary>
        /// 保存每个合法 Definition identity 及其来源，不依赖 bounds 间接推断。
        /// </summary>
        public GasCapacityDefinitionIdentityProofEntry[] DefinitionIdentities =
            Array.Empty<GasCapacityDefinitionIdentityProofEntry>();
        public GasCapacityBoundProofEntry[] Bounds = Array.Empty<GasCapacityBoundProofEntry>();
        public GasProofDerivationEntry[] Derivations = Array.Empty<GasProofDerivationEntry>();
        public GasRuntimeCapacityLimitEntry[] RuntimeLimits = Array.Empty<GasRuntimeCapacityLimitEntry>();
        public GasProofConsumerEntry[] ConsumerMap = Array.Empty<GasProofConsumerEntry>();
        public GasCapacityMemoryEvidence[] MemoryEvidence = Array.Empty<GasCapacityMemoryEvidence>();
        public GasCapacityMemorySummary MemorySummary;
        public GasProofFailure[] Failures = Array.Empty<GasProofFailure>();

        /// <summary>
        /// 仅表示 builder 构建时全部门禁闭合；跨边界消费必须再调用 TryVerifyPayload。
        /// </summary>
        public bool Succeeded =>
            !string.IsNullOrWhiteSpace(ProofHash) &&
            CoverageGaps == GasCapacityProofCoverageGap.None &&
            Failures != null &&
            Failures.Length == 0;
    }
}
