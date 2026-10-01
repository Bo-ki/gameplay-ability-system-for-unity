namespace GAS.Runtime
{
    /// <summary>
    /// 标识 install-envelope admission 中可稳定诊断的字段；具体文件 schema 由 D0/C1 冻结。
    /// </summary>
    internal enum GasInstallAdmissionField : byte
    {
        None = 0,
        ContractVersion = 1,
        Algorithm = 2,
        Domain = 3,
        SchemaHash = 4,
        ContentHash = 5,
        LayoutHash = 6,
        ArtifactManifestHash = 7,
        TypedContractProof = 8,
        LayoutProof = 9,
        CapacityProof = 10,
        EnvelopeBinding = 11,
        FullSemanticEligibility = 12,
        AcceptPath = 13,
    }

    /// <summary>
    /// 表示 Runtime install-envelope consumer 的稳定拒绝原因；默认值保持 fail-closed。
    /// </summary>
    internal enum GasInstallAdmissionReason : byte
    {
        Unspecified = 0,
        TrustedExpectationUnavailable = 1,
        ContractFieldMissing = 2,
        ContractFieldMismatch = 3,
        InstallIdentityMissing = 4,
        InstallIdentityMismatch = 5,
        ProofMissing = 6,
        ProofRejected = 7,
        ProofMismatch = 8,
        EnvelopeBindingMissing = 9,
        EnvelopeTampered = 10,
        FullSemanticEligibilityRequired = 11,
        AcceptPathUnavailable = 12,
    }

    /// <summary>
    /// 集中维护 C0 admission 的稳定 RuleId，避免诊断文本成为协议身份。
    /// </summary>
    internal static class GasInstallEnvelopeAdmissionRuleIds
    {
        public const string TrustedExpectation = "GAS.RUNTIME.INSTALL.EXPECTATION";
        public const string Contract = "GAS.RUNTIME.INSTALL.CONTRACT";
        public const string Identity = "GAS.RUNTIME.INSTALL.IDENTITY";
        public const string Proof = "GAS.RUNTIME.INSTALL.PROOF";
        public const string Integrity = "GAS.RUNTIME.INSTALL.INTEGRITY";
        public const string Eligibility = "GAS.RUNTIME.INSTALL.ELIGIBILITY";
        public const string AcceptPath = "GAS.RUNTIME.INSTALL.ACCEPT_UNAVAILABLE";
    }

    /// <summary>
    /// 保存 Runtime install 必须精确匹配的四元身份；SourceInputHash 不属于该身份。
    /// </summary>
    internal readonly struct GasInstallEnvelopeIdentity
    {
        public readonly string SchemaHash;
        public readonly string ContentHash;
        public readonly string LayoutHash;
        public readonly string ArtifactManifestHash;

        /// <summary>
        /// 使用四个互不替代的稳定 hash 构造 install identity。
        /// </summary>
        public GasInstallEnvelopeIdentity(
            string schemaHash,
            string contentHash,
            string layoutHash,
            string artifactManifestHash)
        {
            SchemaHash = schemaHash;
            ContentHash = contentHash;
            LayoutHash = layoutHash;
            ArtifactManifestHash = artifactManifestHash;
        }
    }

    /// <summary>
    /// 保存一个 proof 的运行时承诺；Player 只比较已构建值，不重算生成期 proof。
    /// </summary>
    internal readonly struct GasInstallProofCommitment
    {
        public readonly int Version;
        public readonly string InputIdentity;
        public readonly string ProofHash;
        public readonly string RuleId;
        public readonly string Provenance;
        public readonly bool IsVerified;

        /// <summary>
        /// 使用版本、输入身份、proof hash 与诊断来源构造 proof 承诺。
        /// </summary>
        public GasInstallProofCommitment(
            int version,
            string inputIdentity,
            string proofHash,
            string ruleId,
            string provenance,
            bool isVerified)
        {
            Version = version;
            InputIdentity = inputIdentity;
            ProofHash = proofHash;
            RuleId = ruleId;
            Provenance = provenance;
            IsVerified = isVerified;
        }
    }

    /// <summary>
    /// 表示具体 envelope 经 C1 投影后的 Runtime-safe candidate 内容，不冻结 JSON 或文件 schema。
    /// </summary>
    internal readonly struct GasInstallEnvelopeProjection
    {
        public readonly int ContractVersion;
        public readonly string Algorithm;
        public readonly string Domain;
        public readonly GasInstallEnvelopeIdentity Identity;
        public readonly GasInstallProofCommitment TypedContractProof;
        public readonly GasInstallProofCommitment LayoutProof;
        public readonly GasInstallProofCommitment CapacityProof;
        public readonly bool FullSemanticEligibility;

        /// <summary>
        /// 从投影全部 binding material 即时派生 candidate binding，不允许调用方独立注入。
        /// </summary>
        public string EnvelopeBinding
        {
            get
            {
                return GasInstallEnvelopeBindingCodec.TryCompute(in this, out var binding)
                    ? binding
                    : string.Empty;
            }
        }

        /// <summary>
        /// 使用契约头、四元身份、三类 proof 与 eligibility 构造唯一可派生 binding 的 Runtime 投影。
        /// </summary>
        public GasInstallEnvelopeProjection(
            int contractVersion,
            string algorithm,
            string domain,
            in GasInstallEnvelopeIdentity identity,
            in GasInstallProofCommitment typedContractProof,
            in GasInstallProofCommitment layoutProof,
            in GasInstallProofCommitment capacityProof,
            bool fullSemanticEligibility)
        {
            ContractVersion = contractVersion;
            Algorithm = algorithm;
            Domain = domain;
            Identity = identity;
            TypedContractProof = typedContractProof;
            LayoutProof = layoutProof;
            CapacityProof = capacityProof;
            FullSemanticEligibility = fullSemanticEligibility;
        }
    }

    /// <summary>
    /// 表示由 Player 构建结果独立携带的 trusted expectation；角色分型不认证来源，C1/Z 禁止从 candidate 克隆。
    /// </summary>
    internal readonly struct GasInstallEnvelopeAdmissionExpectation
    {
        public readonly int ContractVersion;
        public readonly string Algorithm;
        public readonly string Domain;
        public readonly GasInstallEnvelopeIdentity Identity;
        public readonly GasInstallProofCommitment TypedContractProof;
        public readonly GasInstallProofCommitment LayoutProof;
        public readonly GasInstallProofCommitment CapacityProof;
        public readonly string ExpectedEnvelopeBinding;
        public readonly bool ExpectedFullSemanticEligibility;

        /// <summary>
        /// 使用构建期独立固化的契约、身份、proof、binding 与 eligibility 构造 expectation；不提供运行时认证。
        /// </summary>
        public GasInstallEnvelopeAdmissionExpectation(
            int contractVersion,
            string algorithm,
            string domain,
            in GasInstallEnvelopeIdentity identity,
            in GasInstallProofCommitment typedContractProof,
            in GasInstallProofCommitment layoutProof,
            in GasInstallProofCommitment capacityProof,
            string expectedEnvelopeBinding,
            bool expectedFullSemanticEligibility)
        {
            ContractVersion = contractVersion;
            Algorithm = algorithm;
            Domain = domain;
            Identity = identity;
            TypedContractProof = typedContractProof;
            LayoutProof = layoutProof;
            CapacityProof = capacityProof;
            ExpectedEnvelopeBinding = expectedEnvelopeBinding;
            ExpectedFullSemanticEligibility = expectedFullSemanticEligibility;
        }
    }

    /// <summary>
    /// 包装待验证 envelope 投影及外层诊断来源；外层 provenance 不参与 binding，proof provenance 已在投影内受绑定。
    /// </summary>
    internal readonly struct GasInstallEnvelopeAdmissionCandidate
    {
        public readonly GasInstallEnvelopeProjection Envelope;
        public readonly string Provenance;

        /// <summary>
        /// 使用包含 eligibility 的 envelope 内存投影与诊断来源构造 candidate。
        /// </summary>
        public GasInstallEnvelopeAdmissionCandidate(
            in GasInstallEnvelopeProjection envelope,
            string provenance)
        {
            Envelope = envelope;
            Provenance = provenance;
        }
    }

    /// <summary>
    /// 保存 C0 的稳定拒绝证据；Z 接入 accept path 前 CanInstall 永远为 false。
    /// </summary>
    internal readonly struct GasInstallEnvelopeAdmissionResult
    {
        public readonly GasInstallAdmissionReason Reason;
        public readonly GasInstallAdmissionField Field;
        public readonly string ExpectedValue;
        public readonly string ActualValue;
        public readonly GasInstallEnvelopeIdentity ExpectedIdentity;
        public readonly GasInstallEnvelopeIdentity ActualIdentity;
        public readonly string RuleId;
        public readonly string Provenance;

        /// <summary>
        /// C0 仅交付 negative path，因此任何结果都不能授权 authority 写入。
        /// </summary>
        public bool CanInstall => false;

        /// <summary>
        /// 使用稳定 reason、精确字段和值证据构造 fail-closed 结果。
        /// </summary>
        internal GasInstallEnvelopeAdmissionResult(
            GasInstallAdmissionReason reason,
            GasInstallAdmissionField field,
            string expectedValue,
            string actualValue,
            in GasInstallEnvelopeIdentity expectedIdentity,
            in GasInstallEnvelopeIdentity actualIdentity,
            string ruleId,
            string provenance)
        {
            Reason = reason;
            Field = field;
            ExpectedValue = expectedValue;
            ActualValue = actualValue;
            ExpectedIdentity = expectedIdentity;
            ActualIdentity = actualIdentity;
            RuleId = ruleId;
            Provenance = provenance;
        }
    }
}
