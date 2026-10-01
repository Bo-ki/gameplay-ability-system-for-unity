using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 标识 LayoutProof 固定的 Runtime v1 物理槽类型，名称不得被 authoring 别名替代。
    /// </summary>
    public enum GasLayoutSlotKind : byte
    {
        AttributeValue = 1,
        AttributeDirtyWord = 2,
        TagCount = 3,
        TagPresenceWord = 4,
    }

    /// <summary>
    /// 显式记录 LayoutProof 尚未取得的 canonical adapter 与目标 Player ABI 身份。
    /// </summary>
    [Flags]
    public enum GasLayoutProofCoverageGap : uint
    {
        None = 0,
        CanonicalGraphAdapterMissing = 1 << 0,
        TargetPlayerAbiIdentityMissing = 1 << 1,
    }

    /// <summary>
    /// 保存 stable AttributeId 到 dense AttributeLayoutIndex 的可追溯映射。
    /// </summary>
    public struct GasLayoutAttributeProofEntry
    {
        public int AttributeId;
        public int LayoutIndex;
        public int CanonicalOrdinal;
        public GasProofProvenance Provenance;
    }

    /// <summary>
    /// 保存 stable TagId 到 dense TagCatalogIndex 及完整 ancestor range 的可追溯映射。
    /// </summary>
    public struct GasLayoutTagProofEntry
    {
        public int TagId;
        public int TagIndex;
        public int CanonicalOrdinal;
        public int AncestorStart;
        public int AncestorCount;
        public GasProofProvenance Provenance;
    }

    /// <summary>
    /// 保存预解析 TagQueryProgram 的 phase、match 与 canonical tag-index range。
    /// </summary>
    public struct GasLayoutTagQueryProofEntry
    {
        public int RequirementId;
        public int CanonicalOrdinal;
        public GasRequirementPhase Phase;
        public GasTagRequirementMatch Match;
        public int TagIndexStart;
        public int TagIndexCount;
        public GasProofProvenance Provenance;
    }

    /// <summary>
    /// 保存一个 Blob range 的物理半开区间及其 backing array 上界。
    /// </summary>
    public struct GasLayoutRangeProofEntry
    {
        public GasCatalogRangeKind RangeKind;
        public string DefinitionKey;
        public int DefinitionId;
        public int CanonicalOrdinal;
        public int Start;
        public int Count;
        public int BackingLength;
        public GasProofProvenance Provenance;
    }

    /// <summary>
    /// 固定 Runtime v1 槽数量、ABI、revision owner 与派生缓存重建规则。
    /// </summary>
    public struct GasLayoutSlotProofEntry
    {
        public GasLayoutSlotKind SlotKind;
        public string RuntimeTypeName;
        public long ElementCount;
        public int ElementSizeBytes;
        public int AlignmentBytes;
        public long CheckedByteCount;
        public string RevisionContractId;
        public string RebuildContractId;
        public string RevisionOwner;
        public int WordBitCount;
        public string RebuildRule;
    }

    /// <summary>
    /// 保存不依赖 Pipeline 的独立 LayoutProof payload；成功只表示该 proof 自身闭合。
    /// </summary>
    public sealed class GasRuntimeV1LayoutProofPayload
    {
        public int ProofSchemaVersion;
        public string AlgorithmVersion;
        public int GraphSchemaVersion;
        public string GeneratorVersion;
        public string CanonicalGraphHash;
        public string AttributeLayoutHash;
        public string TagCatalogHash;

        /// <summary>
        /// 仅绑定排序、dense/range 与 slot 物理契约；graph identity 和 provenance 由 ProofHash 绑定。
        /// </summary>
        public string LayoutHash;
        public string ProofHash;
        public string AttributeSortRule;
        public string TagSortRule;
        public string BlobRangeSortRule;

        /// <summary>
        /// 固定当前 Layout proof 尚未闭合的覆盖面，直接进入 ProofHash。
        /// </summary>
        public GasLayoutProofCoverageGap CoverageGaps;

        /// <summary>
        /// 标明 slot SizeOf/AlignOf 仅来自当前编辑器宿主，不代表 Player/IL2CPP ABI。
        /// </summary>
        public GasProofConsumerStatus SlotAbiEvidenceStatus;
        public GasLayoutAttributeProofEntry[] Attributes = Array.Empty<GasLayoutAttributeProofEntry>();
        public GasLayoutTagProofEntry[] Tags = Array.Empty<GasLayoutTagProofEntry>();
        public int[] AncestorIndices = Array.Empty<int>();
        public GasLayoutTagQueryProofEntry[] TagQueryPrograms = Array.Empty<GasLayoutTagQueryProofEntry>();
        public int[] RequirementTagIndices = Array.Empty<int>();
        public GasLayoutRangeProofEntry[] BlobRanges = Array.Empty<GasLayoutRangeProofEntry>();
        public GasLayoutSlotProofEntry[] Slots = Array.Empty<GasLayoutSlotProofEntry>();
        public GasProofFailure[] Failures = Array.Empty<GasProofFailure>();

        /// <summary>
        /// 仅表示 builder 构建时无 Red；跨边界消费必须再调用 TryVerifyPayload，不得单独信任此值。
        /// </summary>
        public bool Succeeded =>
            !string.IsNullOrWhiteSpace(ProofHash) &&
            CoverageGaps == GasLayoutProofCoverageGap.None &&
            Failures != null &&
            Failures.Length == 0;
    }
}
