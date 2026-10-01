using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 区分生成期物理布局证明与容量证明，失败证据不得跨 proof kind 混用。
    /// </summary>
    public enum GasProofKind : byte
    {
        None = 0,
        Layout = 1,
        Capacity = 2,
    }

    /// <summary>
    /// 集中声明配置语义编译 Spec 已冻结的 RuleId，B 链不得自行扩展诊断域。
    /// </summary>
    public static class GasProofRuleIds
    {
        public const string InvalidDomainOrReference = "CFG1002";
        public const string UnsupportedContractField = "CFG1101";
        public const string MissingValueViewOrPhase = "CFG1102";
        public const string UnsupportedExecutionProjection = "CFG1104";
        public const string ProjectionUnbounded = "CFG1201";
        public const string DependencyOrCleanupUnbounded = "CFG1301";
        public const string CapacityProofMissing = "CFG1401";
        public const string NonCanonicalIdentity = "CFG1501";
    }

    /// <summary>
    /// 细分 proof Red 的机器原因；RuleId 保持 owner Spec 的稳定粗粒度身份。
    /// </summary>
    public enum GasProofFailureKind : ushort
    {
        None = 0,
        InputUnavailable = 1,
        ProvenanceMissing = 2,
        IdentityMissing = 3,
        StableIdInvalid = 4,
        CanonicalOrdinalInvalid = 5,
        DenseIndexInvalid = 6,
        RangeInvalid = 7,
        RangeArithmeticOverflow = 8,
        AncestorProgramInvalid = 9,
        RuntimeEncodingLimitExceeded = 10,
        CapacityDimensionMissing = 11,
        CapacityBoundInvalid = 12,
        ArithmeticOverflow = 13,
        ProfileInvalid = 14,
        ProfileCapacityExceeded = 15,
        RuntimeConsumerMissing = 16,
        UnsupportedRuntimeCombination = 17,
        ProjectionUnbounded = 18,
        DependencyOrCleanupUnbounded = 19,
        ProofHashMismatch = 20,
        LayoutProofFailed = 21,
        ConsumerMapIncomplete = 22,
        MemoryBudgetMissing = 23,
        MemoryBudgetExceeded = 24,
        MemoryCoverageIncomplete = 25,
        ProofBuildBudgetExceeded = 26,
        ProofCoverageIncomplete = 27,
    }

    /// <summary>
    /// 保存 canonical graph 字段的稳定 authoring 来源，绝对路径不参与身份。
    /// </summary>
    public readonly struct GasProofProvenance
    {
        public readonly int ProvenanceOrdinal;
        public readonly string WorkbookId;
        public readonly string TableId;
        public readonly string RowStableId;
        public readonly string FieldPath;
        public readonly string RawValue;
        public readonly string NormalizedValue;
        public readonly string RuleId;
        public readonly string RuleVersion;
        public readonly string GeneratorVersion;
        public readonly string[] RelatedDefinitionKeys;

        /// <summary>
        /// 构造一条不依赖本机路径且可进入 proof hash 的字段 provenance。
        /// </summary>
        public GasProofProvenance(
            int provenanceOrdinal,
            string workbookId,
            string tableId,
            string rowStableId,
            string fieldPath,
            string rawValue,
            string normalizedValue,
            string ruleId,
            string ruleVersion,
            string generatorVersion,
            string[] relatedDefinitionKeys)
        {
            ProvenanceOrdinal = provenanceOrdinal;
            WorkbookId = workbookId ?? string.Empty;
            TableId = tableId ?? string.Empty;
            RowStableId = rowStableId ?? string.Empty;
            FieldPath = fieldPath ?? string.Empty;
            RawValue = rawValue ?? string.Empty;
            NormalizedValue = normalizedValue ?? string.Empty;
            RuleId = ruleId ?? string.Empty;
            RuleVersion = ruleVersion ?? string.Empty;
            GeneratorVersion = generatorVersion ?? string.Empty;
            if (relatedDefinitionKeys == null)
                RelatedDefinitionKeys = Array.Empty<string>();
            else if (relatedDefinitionKeys.Length >
                     GasRuntimeV1ProofInventory.MaximumProofRelatedDefinitionCount)
                RelatedDefinitionKeys = null;
            else
                RelatedDefinitionKeys = (string[])relatedDefinitionKeys.Clone();
        }

        /// <summary>
        /// 判断该来源是否具有生成稳定诊断所需的最小身份字段。
        /// </summary>
        public bool IsComplete =>
            ProvenanceOrdinal >= 0 &&
            !string.IsNullOrWhiteSpace(WorkbookId) &&
            !string.IsNullOrWhiteSpace(TableId) &&
            !string.IsNullOrWhiteSpace(RowStableId) &&
            !string.IsNullOrWhiteSpace(FieldPath) &&
            !string.IsNullOrWhiteSpace(RuleId) &&
            !string.IsNullOrWhiteSpace(RuleVersion) &&
            !string.IsNullOrWhiteSpace(GeneratorVersion) &&
            RelatedDefinitionKeys != null;
    }

    /// <summary>
    /// 保存一次 checked proof 推导，操作数、单位和来源可独立重演。
    /// </summary>
    public struct GasProofDerivationEntry
    {
        public string DerivationId;
        public string DimensionId;
        public string Expression;
        public string Operator;
        public string LeftOperandName;
        public long LeftOperandValue;
        public string RightOperandName;
        public long RightOperandValue;
        public long Result;
        public string Unit;
        public int DefinitionId;
        public string DefinitionKey;
        public int ProfileId;
        public int CanonicalOrdinal;
        public GasProofProvenance Provenance;
    }

    /// <summary>
    /// 保存稳定 RuleId、精确算术操作数与 authoring 来源，禁止只返回布尔失败。
    /// </summary>
    public struct GasProofFailure
    {
        public GasProofKind ProofKind;
        public GasProofFailureKind FailureKind;
        public string RuleId;
        public string DimensionId;
        public string DerivationId;
        public string Operator;
        public string LeftOperandName;
        public long LeftOperandValue;
        public string RightOperandName;
        public long RightOperandValue;
        public long ExpectedMaximum;
        public long ActualValue;
        public string ExpectedText;
        public string ActualText;
        public string Unit;
        public int DefinitionId;
        public string DefinitionKey;
        public int ProfileId;
        public int CanonicalOrdinal;
        public string FirstConsumer;
        public GasProofProvenance Provenance;

        /// <summary>
        /// 判断该值是否代表一个真实 Red，而不是默认成功值。
        /// </summary>
        public bool IsFailure => FailureKind != GasProofFailureKind.None;
    }

    /// <summary>
    /// 标识一个 proof 字段在 Runtime v1 中是真消费、仅预留还是完全缺失。
    /// </summary>
    public enum GasProofConsumerStatus : byte
    {
        Missing = 0,
        ConsumedBeforeAuthorityWrite = 1,
        RuntimeEquivalentCheck = 2,
        PreallocatedOnly = 3,
        DeclaredOnly = 4,
        EvidenceOnly = 5,
    }

    /// <summary>
    /// 把 proof 维度映射到当前 Runtime v1 的第一消费者和失败时点。
    /// </summary>
    public struct GasProofConsumerEntry
    {
        public string DimensionId;
        public string ProfileField;
        public string FirstConsumer;
        public string ConsumptionMoment;
        public GasProofConsumerStatus Status;
    }

    /// <summary>
    /// 定义一行可参数化执行的 Red 契约及其必需证据。
    /// </summary>
    public readonly struct GasProofRedMatrixEntry
    {
        public readonly string CaseId;
        public readonly GasProofKind ProofKind;
        public readonly GasProofFailureKind FailureKind;
        public readonly string RuleId;
        public readonly string Trigger;
        public readonly string RequiredEvidence;

        /// <summary>
        /// 构造一行稳定 Red 契约，CaseId 用于测试与报告对账。
        /// </summary>
        public GasProofRedMatrixEntry(
            string caseId,
            GasProofKind proofKind,
            GasProofFailureKind failureKind,
            string ruleId,
            string trigger,
            string requiredEvidence)
        {
            CaseId = caseId ?? string.Empty;
            ProofKind = proofKind;
            FailureKind = failureKind;
            RuleId = ruleId ?? string.Empty;
            Trigger = trigger ?? string.Empty;
            RequiredEvidence = requiredEvidence ?? string.Empty;
        }
    }
}
