using GAS.Runtime;
using NUnit.Framework;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Runtime v1 install-envelope C0 consumer 的确定性拒绝与 test-local 零写；不替代 C1/Z 的 bounded decode、trust anchor、Player/IL2CPP 或真实 authority 验证。
    /// </summary>
    [TestFixture]
    public sealed class RuntimeV1InstallEnvelopeAdmissionTests
    {
        private const int ContractVersion = 7;
        private const string ContractVersionText = "7";
        private const string Algorithm = "test-sha256";
        private const string Domain = "test.gas.install-envelope";

        /// <summary>
        /// 独立来源为未调用 production codec 的 PowerShell fixture；固定 hash domain 为
        /// "EX-GAS/Runtime/V1/InstallEnvelopeBinding"，codec version 为 1。
        /// Canonical bytes 依次为 fixed hash domain(1)、codec version(2)、contract(10-12)、
        /// 四元 identity(20-23)、Typed/Layout/Capacity proof(30-35/40-45/50-55) 与 eligibility(60)。
        /// 每字段编码 int32-le ordinal、int32-le payload length 与严格 UTF-8/固定数值 payload，最后取小写 SHA-256。
        /// </summary>
        private const string IneligibleEnvelopeBinding =
            "62d3eb407bd292287b513bc0820ad0ef815a36e3ea61e3ee7abc39e5b4f9d01b";
        private const string EligibleEnvelopeBinding =
            "b832d3916008c1d167d269093eeaec87b9823c535de69d32d02348859d08b2df";
        private const string CandidateProvenance = "test://install-envelope/candidate";
        private const string TamperedValue = "tampered-value";

        /// <summary>
        /// 标识测试要缺失或篡改的 contract header 字段。
        /// </summary>
        public enum ContractField : byte
        {
            Version = 1,
            Algorithm = 2,
            Domain = 3,
        }

        /// <summary>
        /// 标识四元 install identity 中被独立缺失或篡改的字段。
        /// </summary>
        public enum IdentityField : byte
        {
            Schema = 1,
            Content = 2,
            Layout = 3,
            ArtifactManifest = 4,
        }

        /// <summary>
        /// 标识 C0 必须逐项消费的三类 proof。
        /// </summary>
        public enum ProofKind : byte
        {
            TypedContract = 1,
            Layout = 2,
            Capacity = 3,
        }

        /// <summary>
        /// 验证缺少独立 trusted expectation 时 candidate 不能自证并触达 authority。
        /// </summary>
        [Test]
        public void 默认Expectation_拒绝Candidate自证且Authority零写()
        {
            var candidate = CreateCandidate(true);

            AssertRejected(
                default,
                in candidate,
                GasInstallAdmissionReason.TrustedExpectationUnavailable,
                GasInstallAdmissionField.ContractVersion,
                GasInstallEnvelopeAdmissionRuleIds.TrustedExpectation);
        }

        /// <summary>
        /// 验证默认 candidate 以首个缺失 contract version 拒绝且 authority 零写。
        /// </summary>
        [Test]
        public void 默认Candidate_按固定首因拒绝且Authority零写()
        {
            var expectation = CreateTrustedExpectation();
            var candidate = default(GasInstallEnvelopeAdmissionCandidate);

            AssertRejected(
                in expectation,
                in candidate,
                GasInstallAdmissionReason.ContractFieldMissing,
                GasInstallAdmissionField.ContractVersion,
                GasInstallEnvelopeAdmissionRuleIds.Contract,
                ContractVersionText,
                string.Empty);
        }

        /// <summary>
        /// 验证 version、algorithm、domain 任一缺失或错配都会独立拒绝。
        /// </summary>
        [TestCase(ContractField.Version, true)]
        [TestCase(ContractField.Version, false)]
        [TestCase(ContractField.Algorithm, true)]
        [TestCase(ContractField.Algorithm, false)]
        [TestCase(ContractField.Domain, true)]
        [TestCase(ContractField.Domain, false)]
        public void Contract字段_缺失或错配时Authority零写(ContractField field, bool missing)
        {
            var expectation = CreateTrustedExpectation();
            var candidate = MutateContract(CreateCandidate(true), field, missing);

            AssertBindingChangedFromEligibleGolden(in candidate);
            AssertRejected(
                in expectation,
                in candidate,
                missing
                    ? GasInstallAdmissionReason.ContractFieldMissing
                    : GasInstallAdmissionReason.ContractFieldMismatch,
                ToAdmissionField(field),
                GasInstallEnvelopeAdmissionRuleIds.Contract,
                GetExpectedContractValue(field),
                missing ? string.Empty : GetActualContractValue(in candidate, field));
        }

        /// <summary>
        /// 验证四元 identity 任一字段缺失或篡改都不能由其它 identity 或 provenance 补救。
        /// </summary>
        [TestCase(IdentityField.Schema, true)]
        [TestCase(IdentityField.Schema, false)]
        [TestCase(IdentityField.Content, true)]
        [TestCase(IdentityField.Content, false)]
        [TestCase(IdentityField.Layout, true)]
        [TestCase(IdentityField.Layout, false)]
        [TestCase(IdentityField.ArtifactManifest, true)]
        [TestCase(IdentityField.ArtifactManifest, false)]
        public void 四元Identity_单字段缺失或篡改时Authority零写(IdentityField field, bool missing)
        {
            var expectation = CreateTrustedExpectation();
            var candidate = MutateIdentity(CreateCandidate(true), field, missing);

            AssertBindingChangedFromEligibleGolden(in candidate);
            AssertRejected(
                in expectation,
                in candidate,
                missing
                    ? GasInstallAdmissionReason.InstallIdentityMissing
                    : GasInstallAdmissionReason.InstallIdentityMismatch,
                ToAdmissionField(field),
                GasInstallEnvelopeAdmissionRuleIds.Identity,
                GetIdentityValue(in expectation.Identity, field),
                missing ? string.Empty : TamperedValue,
                CandidateProvenance);
        }

        /// <summary>
        /// 验证三类 proof 任一缺失或 hash 篡改都会携各自 RuleId/provenance 拒绝。
        /// </summary>
        [TestCase(ProofKind.TypedContract, true)]
        [TestCase(ProofKind.TypedContract, false)]
        [TestCase(ProofKind.Layout, true)]
        [TestCase(ProofKind.Layout, false)]
        [TestCase(ProofKind.Capacity, true)]
        [TestCase(ProofKind.Capacity, false)]
        public void 三类Proof_缺失或篡改时Authority零写(ProofKind kind, bool missing)
        {
            var expectation = CreateTrustedExpectation();
            var candidate = MutateProof(CreateCandidate(true), kind, missing);
            var expectedProof = GetExpectedProof(in expectation, kind);

            AssertBindingChangedFromEligibleGolden(in candidate);
            AssertRejected(
                in expectation,
                in candidate,
                missing ? GasInstallAdmissionReason.ProofMissing : GasInstallAdmissionReason.ProofMismatch,
                ToAdmissionField(kind),
                expectedProof.RuleId,
                missing ? expectedProof.Version.ToString() : expectedProof.ProofHash,
                missing ? string.Empty : TamperedValue,
                expectedProof.Provenance);
        }

        /// <summary>
        /// 验证 proof hash 未变化时，版本或输入身份绑定篡改仍拒绝。
        /// </summary>
        [TestCase(ProofKind.TypedContract, true)]
        [TestCase(ProofKind.TypedContract, false)]
        [TestCase(ProofKind.Layout, true)]
        [TestCase(ProofKind.Layout, false)]
        [TestCase(ProofKind.Capacity, true)]
        [TestCase(ProofKind.Capacity, false)]
        public void Proof版本或输入身份篡改_拒绝且Authority零写(
            ProofKind kind,
            bool versionMismatch)
        {
            var expectation = CreateTrustedExpectation();
            var candidate = CreateCandidate(true);
            var originalProof = GetProof(in candidate.Envelope, kind);
            var expectedProof = GetExpectedProof(in expectation, kind);
            var actualProof = new GasInstallProofCommitment(
                versionMismatch ? originalProof.Version + 1 : originalProof.Version,
                versionMismatch ? originalProof.InputIdentity : TamperedValue,
                originalProof.ProofHash,
                originalProof.RuleId,
                originalProof.Provenance,
                true);
            candidate = WithProof(candidate, kind, in actualProof);

            AssertBindingChangedFromEligibleGolden(in candidate);
            AssertRejected(
                in expectation,
                in candidate,
                GasInstallAdmissionReason.ProofMismatch,
                ToAdmissionField(kind),
                expectedProof.RuleId,
                versionMismatch ? expectedProof.Version.ToString() : expectedProof.InputIdentity,
                versionMismatch ? actualProof.Version.ToString() : TamperedValue,
                expectedProof.Provenance);
        }

        /// <summary>
        /// 验证 proof provenance 的非空篡改参与承诺比较且诊断只回显 trusted provenance。
        /// </summary>
        [TestCase(ProofKind.TypedContract)]
        [TestCase(ProofKind.Layout)]
        [TestCase(ProofKind.Capacity)]
        public void ProofProvenance篡改_拒绝且Authority零写(ProofKind kind)
        {
            var expectation = CreateTrustedExpectation();
            var candidate = CreateCandidate(true);
            var originalProof = GetProof(in candidate.Envelope, kind);
            var expectedProof = GetExpectedProof(in expectation, kind);
            var actualProof = new GasInstallProofCommitment(
                originalProof.Version,
                originalProof.InputIdentity,
                originalProof.ProofHash,
                originalProof.RuleId,
                TamperedValue,
                true);
            candidate = WithProof(candidate, kind, in actualProof);

            AssertBindingChangedFromEligibleGolden(in candidate);
            AssertRejected(
                in expectation,
                in candidate,
                GasInstallAdmissionReason.ProofMismatch,
                ToAdmissionField(kind),
                expectedProof.RuleId,
                expectedProof.Provenance,
                TamperedValue,
                expectedProof.Provenance);
        }

        /// <summary>
        /// 验证 proof RuleId 仅作非空单点篡改时 binding 改变且拒绝诊断仍取 trusted RuleId。
        /// </summary>
        [TestCase(ProofKind.TypedContract)]
        [TestCase(ProofKind.Layout)]
        [TestCase(ProofKind.Capacity)]
        public void ProofRuleId非空单点篡改_拒绝且Authority零写(ProofKind kind)
        {
            var expectation = CreateTrustedExpectation();
            var candidate = CreateCandidate(true);
            var originalProof = GetProof(in candidate.Envelope, kind);
            var expectedProof = GetExpectedProof(in expectation, kind);
            var actualProof = new GasInstallProofCommitment(
                originalProof.Version,
                originalProof.InputIdentity,
                originalProof.ProofHash,
                TamperedValue,
                originalProof.Provenance,
                originalProof.IsVerified);
            candidate = WithProof(candidate, kind, in actualProof);

            AssertBindingChangedFromEligibleGolden(in candidate);
            AssertRejected(
                in expectation,
                in candidate,
                GasInstallAdmissionReason.ProofMismatch,
                ToAdmissionField(kind),
                expectedProof.RuleId,
                expectedProof.RuleId,
                TamperedValue,
                expectedProof.Provenance);
        }

        /// <summary>
        /// 验证相同 proof payload 集合跨 slot 完整交换后仍因 slot ordinal 进入不同 binding 并拒绝。
        /// </summary>
        [TestCase(ProofKind.TypedContract, ProofKind.Layout)]
        [TestCase(ProofKind.TypedContract, ProofKind.Capacity)]
        [TestCase(ProofKind.Layout, ProofKind.Capacity)]
        public void ProofPayload跨Slot完整交换_拒绝且Authority零写(
            ProofKind left,
            ProofKind right)
        {
            var expectation = CreateTrustedExpectation();
            var candidate = SwapProofSlots(CreateCandidate(true), left, right);
            var firstMismatch = (byte)left < (byte)right ? left : right;
            var expectedProof = GetExpectedProof(in expectation, firstMismatch);
            var actualProof = GetProof(in candidate.Envelope, firstMismatch);

            AssertBindingChangedFromEligibleGolden(in candidate);
            AssertRejected(
                in expectation,
                in candidate,
                GasInstallAdmissionReason.ProofMismatch,
                ToAdmissionField(firstMismatch),
                expectedProof.RuleId,
                expectedProof.Version.ToString(),
                actualProof.Version.ToString(),
                expectedProof.Provenance);
        }

        /// <summary>
        /// 验证 proof 的 RuleId 或 provenance 单独缺失时诊断精确指向缺失承诺值。
        /// </summary>
        [TestCase(ProofKind.TypedContract, true)]
        [TestCase(ProofKind.TypedContract, false)]
        [TestCase(ProofKind.Layout, true)]
        [TestCase(ProofKind.Layout, false)]
        [TestCase(ProofKind.Capacity, true)]
        [TestCase(ProofKind.Capacity, false)]
        public void Proof诊断字段单独缺失_精确拒绝且Authority零写(
            ProofKind kind,
            bool missingRuleId)
        {
            var expectation = CreateTrustedExpectation();
            var candidate = CreateCandidate(true);
            var originalProof = GetProof(in candidate.Envelope, kind);
            var expectedProof = GetExpectedProof(in expectation, kind);
            var actualProof = new GasInstallProofCommitment(
                originalProof.Version,
                originalProof.InputIdentity,
                originalProof.ProofHash,
                missingRuleId ? string.Empty : originalProof.RuleId,
                missingRuleId ? originalProof.Provenance : string.Empty,
                true);
            candidate = WithProof(candidate, kind, in actualProof);

            AssertBindingChangedFromEligibleGolden(in candidate);
            AssertRejected(
                in expectation,
                in candidate,
                GasInstallAdmissionReason.ProofMissing,
                ToAdmissionField(kind),
                expectedProof.RuleId,
                missingRuleId ? expectedProof.RuleId : expectedProof.Provenance,
                string.Empty,
                expectedProof.Provenance);
        }

        /// <summary>
        /// 验证承诺字段齐全但未通过生成期验证的 proof 仍明确拒绝。
        /// </summary>
        [TestCase(ProofKind.TypedContract)]
        [TestCase(ProofKind.Layout)]
        [TestCase(ProofKind.Capacity)]
        public void Proof未验证_拒绝且Authority零写(ProofKind kind)
        {
            var expectation = CreateTrustedExpectation();
            var candidate = CreateCandidate(true);
            var originalProof = GetProof(in candidate.Envelope, kind);
            var expectedProof = GetExpectedProof(in expectation, kind);
            var rejectedProof = CloneProof(in originalProof, originalProof.ProofHash, false);
            candidate = WithProof(candidate, kind, in rejectedProof);

            AssertBindingChangedFromEligibleGolden(in candidate);
            AssertRejected(
                in expectation,
                in candidate,
                GasInstallAdmissionReason.ProofRejected,
                ToAdmissionField(kind),
                expectedProof.RuleId,
                "verified",
                "rejected",
                expectedProof.Provenance);
        }

        /// <summary>
        /// 验证 production codec 对 false/true eligibility 命中独立冻结的 golden binding。
        /// </summary>
        [TestCase(false, IneligibleEnvelopeBinding)]
        [TestCase(true, EligibleEnvelopeBinding)]
        public void CandidateBinding_命中独立GoldenVector(bool eligibility, string expectedBinding)
        {
            var candidate = CreateCandidate(eligibility);

            Assert.That(candidate.Envelope.EnvelopeBinding, Is.EqualTo(expectedBinding));
            Assert.That(
                GasInstallEnvelopeBindingCodec.IsCanonicalBinding(candidate.Envelope.EnvelopeBinding),
                Is.True);
        }

        /// <summary>
        /// 验证 expected eligibility 与 frozen binding 不一致时 trust anchor 无法自洽。
        /// </summary>
        [Test]
        public void TrustedEligibility与FrozenBinding不一致_拒绝且Authority零写()
        {
            var expectation = CreateTrustedExpectation(true, IneligibleEnvelopeBinding);
            var candidate = CreateCandidate(true);

            AssertRejected(
                in expectation,
                in candidate,
                GasInstallAdmissionReason.TrustedExpectationUnavailable,
                GasInstallAdmissionField.EnvelopeBinding,
                GasInstallEnvelopeAdmissionRuleIds.TrustedExpectation,
                IneligibleEnvelopeBinding,
                EligibleEnvelopeBinding,
                string.Empty);
        }

        /// <summary>
        /// 验证长度定界字段编码不会把不同 algorithm/domain 分段折叠为同一 binding。
        /// </summary>
        [Test]
        public void Canonical字段边界_不同分段产生不同Binding()
        {
            var source = CreateProjection(true);
            var left = RebuildProjection(
                in source,
                source.ContractVersion,
                "ab",
                "c",
                in source.Identity);
            var right = RebuildProjection(
                in source,
                source.ContractVersion,
                "a",
                "bc",
                in source.Identity);

            Assert.That(left.EnvelopeBinding, Is.Not.EqualTo(right.EnvelopeBinding));
        }

        /// <summary>
        /// 验证当前 FullSemanticEligibility=false 即使其它字段匹配也不可安装。
        /// </summary>
        [Test]
        public void FullSemanticEligibilityFalse_明确拒绝且Authority零写()
        {
            var expectation = CreateTrustedExpectation(false);
            var candidate = CreateCandidate(false);

            AssertRejected(
                in expectation,
                in candidate,
                GasInstallAdmissionReason.FullSemanticEligibilityRequired,
                GasInstallAdmissionField.FullSemanticEligibility,
                GasInstallEnvelopeAdmissionRuleIds.Eligibility,
                "true",
                "false",
                CandidateProvenance);
        }

        /// <summary>
        /// 验证 current false envelope 仅翻转 eligibility 会派生新 binding 并在 authority 前拒绝。
        /// </summary>
        [Test]
        public void FullSemanticEligibilityFalseToTrue_旧Binding不可复用且Authority零写()
        {
            var expectation = CreateTrustedExpectation(false);
            var baseline = CreateCandidate(false);
            var candidate = WithEligibility(baseline, true);

            Assert.That(baseline.Envelope.EnvelopeBinding, Is.EqualTo(IneligibleEnvelopeBinding));
            Assert.That(candidate.Envelope.EnvelopeBinding, Is.EqualTo(EligibleEnvelopeBinding));
            Assert.That(candidate.Envelope.EnvelopeBinding,
                Is.Not.EqualTo(baseline.Envelope.EnvelopeBinding));
            AssertRejected(
                in expectation,
                in candidate,
                GasInstallAdmissionReason.EnvelopeTampered,
                GasInstallAdmissionField.EnvelopeBinding,
                GasInstallEnvelopeAdmissionRuleIds.Integrity,
                IneligibleEnvelopeBinding,
                EligibleEnvelopeBinding,
                CandidateProvenance);
        }

        /// <summary>
        /// 验证全部 C0 字段匹配且 eligibility=true 时仍不提前开放 Z 的 accept path。
        /// </summary>
        [Test]
        public void 全部字段匹配_AcceptPath仍不可达且Authority零写()
        {
            var expectation = CreateTrustedExpectation();
            var candidate = CreateCandidate(true);

            AssertRejected(
                in expectation,
                in candidate,
                GasInstallAdmissionReason.AcceptPathUnavailable,
                GasInstallAdmissionField.AcceptPath,
                GasInstallEnvelopeAdmissionRuleIds.AcceptPath,
                "sealed-by-Z",
                "unavailable",
                CandidateProvenance);
        }

        /// <summary>
        /// 从 hard-coded golden vector 构造独立 trusted expectation，不调用 candidate codec 或工厂。
        /// </summary>
        private static GasInstallEnvelopeAdmissionExpectation CreateTrustedExpectation(
            bool expectedEligibility = true)
        {
            return CreateTrustedExpectation(
                expectedEligibility,
                expectedEligibility ? EligibleEnvelopeBinding : IneligibleEnvelopeBinding);
        }

        /// <summary>
        /// 从显式 frozen binding 构造独立 trusted expectation，供 trust-anchor 负例复用。
        /// </summary>
        private static GasInstallEnvelopeAdmissionExpectation CreateTrustedExpectation(
            bool expectedEligibility,
            string expectedBinding)
        {
            var identity = new GasInstallEnvelopeIdentity(
                "schema-identity",
                "content-identity",
                "layout-identity",
                "artifact-manifest-identity");
            var typed = new GasInstallProofCommitment(
                1,
                "input-identity-TypedContract",
                "proof-hash-TypedContract",
                "TEST.PROOF.TypedContract",
                "test://proof/TypedContract",
                true);
            var layout = new GasInstallProofCommitment(
                2,
                "input-identity-Layout",
                "proof-hash-Layout",
                "TEST.PROOF.Layout",
                "test://proof/Layout",
                true);
            var capacity = new GasInstallProofCommitment(
                3,
                "input-identity-Capacity",
                "proof-hash-Capacity",
                "TEST.PROOF.Capacity",
                "test://proof/Capacity",
                true);
            return new GasInstallEnvelopeAdmissionExpectation(
                ContractVersion,
                Algorithm,
                Domain,
                in identity,
                in typed,
                in layout,
                in capacity,
                expectedBinding,
                expectedEligibility);
        }

        /// <summary>
        /// 构造值与 expectation 匹配、但对象来源独立的 Runtime envelope 投影。
        /// </summary>
        private static GasInstallEnvelopeProjection CreateProjection(bool eligibility)
        {
            var identity = new GasInstallEnvelopeIdentity(
                "schema-identity",
                "content-identity",
                "layout-identity",
                "artifact-manifest-identity");
            var typed = CreateCandidateProof(ProofKind.TypedContract, true);
            var layout = CreateCandidateProof(ProofKind.Layout, true);
            var capacity = CreateCandidateProof(ProofKind.Capacity, true);
            return new GasInstallEnvelopeProjection(
                ContractVersion,
                Algorithm,
                Domain,
                in identity,
                in typed,
                in layout,
                in capacity,
                eligibility);
        }

        /// <summary>
        /// 构造与 trusted expectation 值相同但来源独立的 candidate。
        /// </summary>
        private static GasInstallEnvelopeAdmissionCandidate CreateCandidate(bool eligibility)
        {
            var envelope = CreateProjection(eligibility);
            return new GasInstallEnvelopeAdmissionCandidate(in envelope, CandidateProvenance);
        }

        /// <summary>
        /// 构造指定种类且具有独立 input、RuleId 与 provenance 的 proof。
        /// </summary>
        private static GasInstallProofCommitment CreateCandidateProof(ProofKind kind, bool verified)
        {
            var name = kind.ToString();
            return new GasInstallProofCommitment(
                (int)kind,
                "input-identity-" + name,
                "proof-hash-" + name,
                "TEST.PROOF." + name,
                "test://proof/" + name,
                verified);
        }

        /// <summary>
        /// 创建仅 contract header 指定字段缺失或错配的 candidate。
        /// </summary>
        private static GasInstallEnvelopeAdmissionCandidate MutateContract(
            GasInstallEnvelopeAdmissionCandidate candidate,
            ContractField field,
            bool missing)
        {
            var source = candidate.Envelope;
            var version = field == ContractField.Version
                ? (missing ? 0 : ContractVersion + 1)
                : source.ContractVersion;
            var algorithm = field == ContractField.Algorithm
                ? (missing ? string.Empty : TamperedValue)
                : source.Algorithm;
            var domain = field == ContractField.Domain
                ? (missing ? string.Empty : TamperedValue)
                : source.Domain;
            var envelope = RebuildProjection(in source, version, algorithm, domain,
                in source.Identity);
            return RebuildCandidate(in candidate, in envelope);
        }

        /// <summary>
        /// 创建仅四元 identity 指定字段缺失或篡改的 candidate。
        /// </summary>
        private static GasInstallEnvelopeAdmissionCandidate MutateIdentity(
            GasInstallEnvelopeAdmissionCandidate candidate,
            IdentityField field,
            bool missing)
        {
            var source = candidate.Envelope;
            var replacement = missing ? string.Empty : TamperedValue;
            var identity = new GasInstallEnvelopeIdentity(
                field == IdentityField.Schema ? replacement : source.Identity.SchemaHash,
                field == IdentityField.Content ? replacement : source.Identity.ContentHash,
                field == IdentityField.Layout ? replacement : source.Identity.LayoutHash,
                field == IdentityField.ArtifactManifest ? replacement : source.Identity.ArtifactManifestHash);
            var envelope = RebuildProjection(in source, source.ContractVersion, source.Algorithm, source.Domain,
                in identity);
            return RebuildCandidate(in candidate, in envelope);
        }

        /// <summary>
        /// 创建仅指定 proof 缺失或 proof hash 篡改的 candidate。
        /// </summary>
        private static GasInstallEnvelopeAdmissionCandidate MutateProof(
            GasInstallEnvelopeAdmissionCandidate candidate,
            ProofKind kind,
            bool missing)
        {
            var original = GetProof(in candidate.Envelope, kind);
            var proof = missing ? default : CloneProof(in original, TamperedValue, true);
            return WithProof(candidate, kind, in proof);
        }

        /// <summary>
        /// 使用新的 proof hash 与验证状态复制其余承诺字段。
        /// </summary>
        private static GasInstallProofCommitment CloneProof(
            in GasInstallProofCommitment proof,
            string proofHash,
            bool verified)
        {
            return new GasInstallProofCommitment(
                proof.Version,
                proof.InputIdentity,
                proofHash,
                proof.RuleId,
                proof.Provenance,
                verified);
        }

        /// <summary>
        /// 替换 candidate 中指定种类的 proof，同时保持其它字段不变。
        /// </summary>
        private static GasInstallEnvelopeAdmissionCandidate WithProof(
            GasInstallEnvelopeAdmissionCandidate candidate,
            ProofKind kind,
            in GasInstallProofCommitment proof)
        {
            var source = candidate.Envelope;
            var typed = kind == ProofKind.TypedContract ? proof : source.TypedContractProof;
            var layout = kind == ProofKind.Layout ? proof : source.LayoutProof;
            var capacity = kind == ProofKind.Capacity ? proof : source.CapacityProof;
            var envelope = new GasInstallEnvelopeProjection(
                source.ContractVersion,
                source.Algorithm,
                source.Domain,
                in source.Identity,
                in typed,
                in layout,
                in capacity,
                source.FullSemanticEligibility);
            return RebuildCandidate(in candidate, in envelope);
        }

        /// <summary>
        /// 交换 candidate 的两个完整 proof commitment，保持 payload 集合与其它字段不变。
        /// </summary>
        private static GasInstallEnvelopeAdmissionCandidate SwapProofSlots(
            GasInstallEnvelopeAdmissionCandidate candidate,
            ProofKind left,
            ProofKind right)
        {
            var leftProof = GetProof(in candidate.Envelope, left);
            var rightProof = GetProof(in candidate.Envelope, right);
            candidate = WithProof(candidate, left, in rightProof);
            return WithProof(candidate, right, in leftProof);
        }

        /// <summary>
        /// 仅改变 candidate eligibility 并由 production codec 自动派生新的 binding。
        /// </summary>
        private static GasInstallEnvelopeAdmissionCandidate WithEligibility(
            GasInstallEnvelopeAdmissionCandidate candidate,
            bool eligibility)
        {
            var source = candidate.Envelope;
            var envelope = new GasInstallEnvelopeProjection(
                source.ContractVersion,
                source.Algorithm,
                source.Domain,
                in source.Identity,
                in source.TypedContractProof,
                in source.LayoutProof,
                in source.CapacityProof,
                eligibility);
            return RebuildCandidate(in candidate, in envelope);
        }

        /// <summary>
        /// 使用指定 header 与 identity 重建投影并保留三类 proof、eligibility。
        /// </summary>
        private static GasInstallEnvelopeProjection RebuildProjection(
            in GasInstallEnvelopeProjection source,
            int version,
            string algorithm,
            string domain,
            in GasInstallEnvelopeIdentity identity)
        {
            return new GasInstallEnvelopeProjection(
                version,
                algorithm,
                domain,
                in identity,
                in source.TypedContractProof,
                in source.LayoutProof,
                in source.CapacityProof,
                source.FullSemanticEligibility);
        }

        /// <summary>
        /// 使用新投影重建 candidate 并保留 provenance。
        /// </summary>
        private static GasInstallEnvelopeAdmissionCandidate RebuildCandidate(
            in GasInstallEnvelopeAdmissionCandidate source,
            in GasInstallEnvelopeProjection envelope)
        {
            return new GasInstallEnvelopeAdmissionCandidate(in envelope, source.Provenance);
        }

        /// <summary>
        /// 从 trusted expectation 返回指定 proof 承诺。
        /// </summary>
        private static GasInstallProofCommitment GetExpectedProof(
            in GasInstallEnvelopeAdmissionExpectation expectation,
            ProofKind kind)
        {
            if (kind == ProofKind.TypedContract)
                return expectation.TypedContractProof;
            return kind == ProofKind.Layout ? expectation.LayoutProof : expectation.CapacityProof;
        }

        /// <summary>
        /// 从固定投影返回指定 proof 承诺。
        /// </summary>
        private static GasInstallProofCommitment GetProof(
            in GasInstallEnvelopeProjection envelope,
            ProofKind kind)
        {
            if (kind == ProofKind.TypedContract)
                return envelope.TypedContractProof;
            return kind == ProofKind.Layout ? envelope.LayoutProof : envelope.CapacityProof;
        }

        /// <summary>
        /// 将测试 contract 字段映射到稳定 admission 字段。
        /// </summary>
        private static GasInstallAdmissionField ToAdmissionField(ContractField field)
        {
            if (field == ContractField.Version)
                return GasInstallAdmissionField.ContractVersion;
            return field == ContractField.Algorithm
                ? GasInstallAdmissionField.Algorithm
                : GasInstallAdmissionField.Domain;
        }

        /// <summary>
        /// 将测试 identity 字段映射到稳定 admission 字段。
        /// </summary>
        private static GasInstallAdmissionField ToAdmissionField(IdentityField field)
        {
            if (field == IdentityField.Schema)
                return GasInstallAdmissionField.SchemaHash;
            if (field == IdentityField.Content)
                return GasInstallAdmissionField.ContentHash;
            return field == IdentityField.Layout
                ? GasInstallAdmissionField.LayoutHash
                : GasInstallAdmissionField.ArtifactManifestHash;
        }

        /// <summary>
        /// 将测试 proof 种类映射到稳定 admission 字段。
        /// </summary>
        private static GasInstallAdmissionField ToAdmissionField(ProofKind kind)
        {
            if (kind == ProofKind.TypedContract)
                return GasInstallAdmissionField.TypedContractProof;
            return kind == ProofKind.Layout
                ? GasInstallAdmissionField.LayoutProof
                : GasInstallAdmissionField.CapacityProof;
        }

        /// <summary>
        /// 返回指定 contract 字段的可信期望值。
        /// </summary>
        private static string GetExpectedContractValue(ContractField field)
        {
            if (field == ContractField.Version)
                return ContractVersionText;
            return field == ContractField.Algorithm ? Algorithm : Domain;
        }

        /// <summary>
        /// 返回 candidate 中指定 contract 字段的实际值。
        /// </summary>
        private static string GetActualContractValue(
            in GasInstallEnvelopeAdmissionCandidate candidate,
            ContractField field)
        {
            if (field == ContractField.Version)
                return candidate.Envelope.ContractVersion.ToString();
            return field == ContractField.Algorithm
                ? candidate.Envelope.Algorithm
                : candidate.Envelope.Domain;
        }

        /// <summary>
        /// 返回指定四元 identity 字段的值。
        /// </summary>
        private static string GetIdentityValue(
            in GasInstallEnvelopeIdentity identity,
            IdentityField field)
        {
            if (field == IdentityField.Schema)
                return identity.SchemaHash;
            if (field == IdentityField.Content)
                return identity.ContentHash;
            return field == IdentityField.Layout
                ? identity.LayoutHash
                : identity.ArtifactManifestHash;
        }

        /// <summary>
        /// 断言 candidate mutation 已进入 canonical binding 且不再复用 eligible golden。
        /// </summary>
        private static void AssertBindingChangedFromEligibleGolden(
            in GasInstallEnvelopeAdmissionCandidate candidate)
        {
            var actualBinding = candidate.Envelope.EnvelopeBinding;
            Assert.That(GasInstallEnvelopeBindingCodec.IsCanonicalBinding(actualBinding), Is.True);
            Assert.That(actualBinding, Is.Not.EqualTo(EligibleEnvelopeBinding));
        }

        /// <summary>
        /// 执行纯 admission，并仅在 CanInstall=true 时模拟 authority commit。
        /// </summary>
        private static GasInstallEnvelopeAdmissionResult EvaluateAtAuthorityBoundary(
            in GasInstallEnvelopeAdmissionExpectation expectation,
            in GasInstallEnvelopeAdmissionCandidate candidate,
            RecordingInstallAuthority authority)
        {
            var result = GasInstallEnvelopeAdmissionConsumer.Evaluate(in expectation, in candidate);
            if (result.CanInstall)
                authority.Write(in candidate);
            return result;
        }

        /// <summary>
        /// 断言拒绝证据稳定、完整 identity 被保留且 authority 状态零变化。
        /// </summary>
        private static void AssertRejected(
            in GasInstallEnvelopeAdmissionExpectation expectation,
            in GasInstallEnvelopeAdmissionCandidate candidate,
            GasInstallAdmissionReason reason,
            GasInstallAdmissionField field,
            string ruleId,
            string expectedValue = null,
            string actualValue = null,
            string provenance = null)
        {
            var authority = new RecordingInstallAuthority();
            var result = EvaluateAtAuthorityBoundary(in expectation, in candidate, authority);

            Assert.That(result.CanInstall, Is.False);
            Assert.That(result.Reason, Is.EqualTo(reason));
            Assert.That(result.Field, Is.EqualTo(field));
            Assert.That(result.RuleId, Is.EqualTo(ruleId));
            if (expectedValue != null)
                Assert.That(result.ExpectedValue, Is.EqualTo(expectedValue));
            if (actualValue != null)
                Assert.That(result.ActualValue, Is.EqualTo(actualValue));
            if (provenance != null)
                Assert.That(result.Provenance, Is.EqualTo(provenance));
            AssertIdentity(in result.ExpectedIdentity, in expectation.Identity);
            AssertIdentity(in result.ActualIdentity, in candidate.Envelope.Identity);
            Assert.That(authority.WriteCount, Is.Zero);
            Assert.That(authority.CatalogRevision, Is.EqualTo(RecordingInstallAuthority.InitialCatalogRevision));
        }

        /// <summary>
        /// 断言结果携带的四元 identity 与输入逐字段一致。
        /// </summary>
        private static void AssertIdentity(
            in GasInstallEnvelopeIdentity actual,
            in GasInstallEnvelopeIdentity expected)
        {
            Assert.That(actual.SchemaHash, Is.EqualTo(expected.SchemaHash));
            Assert.That(actual.ContentHash, Is.EqualTo(expected.ContentHash));
            Assert.That(actual.LayoutHash, Is.EqualTo(expected.LayoutHash));
            Assert.That(actual.ArtifactManifestHash, Is.EqualTo(expected.ArtifactManifestHash));
        }

        /// <summary>
        /// 模拟 Z 未来才可触达的提交边界，用计数与 sentinel 限定证明 consumer 信号不会触发 test-local 写入。
        /// </summary>
        private sealed class RecordingInstallAuthority
        {
            public const int InitialCatalogRevision = 73;

            public int WriteCount { get; private set; }
            public int CatalogRevision { get; private set; } = InitialCatalogRevision;

            /// <summary>
            /// 模拟一次 authority commit；C0 测试中该方法不得被调用。
            /// </summary>
            public void Write(in GasInstallEnvelopeAdmissionCandidate candidate)
            {
                WriteCount++;
                CatalogRevision++;
            }
        }
    }
}
