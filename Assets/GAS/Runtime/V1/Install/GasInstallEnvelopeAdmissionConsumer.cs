using System;
using System.Globalization;

namespace GAS.Runtime
{
    /// <summary>
    /// 纯值消费 Runtime install-envelope 投影并在 authority 写入前 fail closed；expectation 分型不认证其外部来源。
    /// </summary>
    internal static class GasInstallEnvelopeAdmissionConsumer
    {
        /// <summary>
        /// 按 C1/Z 从构建期独立来源提供的 expectation 校验 candidate；禁止从 candidate 克隆且当前始终不可安装。
        /// </summary>
        public static GasInstallEnvelopeAdmissionResult Evaluate(
            in GasInstallEnvelopeAdmissionExpectation trustedExpectation,
            in GasInstallEnvelopeAdmissionCandidate candidate)
        {
            var missingExpectationField = FindMissingExpectationField(in trustedExpectation);
            if (missingExpectationField != GasInstallAdmissionField.None)
            {
                return Reject(
                    GasInstallAdmissionReason.TrustedExpectationUnavailable,
                    missingExpectationField,
                    "trusted-value",
                    string.Empty,
                    in trustedExpectation,
                    in candidate,
                    GasInstallEnvelopeAdmissionRuleIds.TrustedExpectation,
                    candidate.Provenance);
            }
            if (!TryValidateTrustedExpectationBinding(
                    in trustedExpectation,
                    in candidate,
                    out var result))
                return result;
            if (!TryValidateContract(in trustedExpectation, in candidate, out result))
                return result;
            if (!TryValidateIdentity(in trustedExpectation, in candidate, out result))
                return result;
            if (!TryValidateProofs(in trustedExpectation, in candidate, out result))
                return result;
            if (!TryValidateBinding(in trustedExpectation, in candidate, out result))
                return result;
            return RejectEligibilityOrSealed(in trustedExpectation, in candidate);
        }

        /// <summary>
        /// 返回 trusted expectation 中按固定优先级遇到的首个缺失字段。
        /// </summary>
        private static GasInstallAdmissionField FindMissingExpectationField(
            in GasInstallEnvelopeAdmissionExpectation expectation)
        {
            if (expectation.ContractVersion <= 0)
                return GasInstallAdmissionField.ContractVersion;
            if (IsMissing(expectation.Algorithm))
                return GasInstallAdmissionField.Algorithm;
            if (IsMissing(expectation.Domain))
                return GasInstallAdmissionField.Domain;
            if (IsMissing(expectation.Identity.SchemaHash))
                return GasInstallAdmissionField.SchemaHash;
            if (IsMissing(expectation.Identity.ContentHash))
                return GasInstallAdmissionField.ContentHash;
            if (IsMissing(expectation.Identity.LayoutHash))
                return GasInstallAdmissionField.LayoutHash;
            if (IsMissing(expectation.Identity.ArtifactManifestHash))
                return GasInstallAdmissionField.ArtifactManifestHash;
            if (!IsTrustedProofAvailable(in expectation.TypedContractProof))
                return GasInstallAdmissionField.TypedContractProof;
            if (!IsTrustedProofAvailable(in expectation.LayoutProof))
                return GasInstallAdmissionField.LayoutProof;
            if (!IsTrustedProofAvailable(in expectation.CapacityProof))
                return GasInstallAdmissionField.CapacityProof;
            return IsMissing(expectation.ExpectedEnvelopeBinding)
                ? GasInstallAdmissionField.EnvelopeBinding
                : GasInstallAdmissionField.None;
        }

        /// <summary>
        /// 验证 frozen expectation binding 的格式与字段一致性，但不把这种一致性视为外部来源认证。
        /// </summary>
        private static bool TryValidateTrustedExpectationBinding(
            in GasInstallEnvelopeAdmissionExpectation expectation,
            in GasInstallEnvelopeAdmissionCandidate candidate,
            out GasInstallEnvelopeAdmissionResult result)
        {
            if (!GasInstallEnvelopeBindingCodec.IsCanonicalBinding(
                    expectation.ExpectedEnvelopeBinding))
            {
                result = Reject(
                    GasInstallAdmissionReason.TrustedExpectationUnavailable,
                    GasInstallAdmissionField.EnvelopeBinding,
                    "64-lowercase-hex",
                    expectation.ExpectedEnvelopeBinding,
                    in expectation,
                    in candidate,
                    GasInstallEnvelopeAdmissionRuleIds.TrustedExpectation,
                    string.Empty);
                return false;
            }

            if (!GasInstallEnvelopeBindingCodec.TryCompute(in expectation, out var recomputed) ||
                !EqualsOrdinal(expectation.ExpectedEnvelopeBinding, recomputed))
            {
                result = Reject(
                    GasInstallAdmissionReason.TrustedExpectationUnavailable,
                    GasInstallAdmissionField.EnvelopeBinding,
                    expectation.ExpectedEnvelopeBinding,
                    recomputed,
                    in expectation,
                    in candidate,
                    GasInstallEnvelopeAdmissionRuleIds.TrustedExpectation,
                    string.Empty);
                return false;
            }

            result = default;
            return true;
        }

        /// <summary>
        /// 按固定顺序验证 candidate contract version、algorithm 与 domain。
        /// </summary>
        private static bool TryValidateContract(
            in GasInstallEnvelopeAdmissionExpectation expectation,
            in GasInstallEnvelopeAdmissionCandidate candidate,
            out GasInstallEnvelopeAdmissionResult result)
        {
            if (!TryValidateVersion(in expectation, in candidate, out result))
                return false;
            if (!TryValidateRequiredValue(
                    GasInstallAdmissionField.Algorithm,
                    expectation.Algorithm,
                    candidate.Envelope.Algorithm,
                    GasInstallAdmissionReason.ContractFieldMissing,
                    GasInstallAdmissionReason.ContractFieldMismatch,
                    GasInstallEnvelopeAdmissionRuleIds.Contract,
                    in expectation,
                    in candidate,
                    out result))
                return false;
            return TryValidateRequiredValue(
                GasInstallAdmissionField.Domain,
                expectation.Domain,
                candidate.Envelope.Domain,
                GasInstallAdmissionReason.ContractFieldMissing,
                GasInstallAdmissionReason.ContractFieldMismatch,
                GasInstallEnvelopeAdmissionRuleIds.Contract,
                in expectation,
                in candidate,
                out result);
        }

        /// <summary>
        /// 验证 frozen eligibility，并在 C0 全部匹配时仍返回 Z 尚未开放的 sealed 结果。
        /// </summary>
        private static GasInstallEnvelopeAdmissionResult RejectEligibilityOrSealed(
            in GasInstallEnvelopeAdmissionExpectation expectation,
            in GasInstallEnvelopeAdmissionCandidate candidate)
        {
            var actual = candidate.Envelope.FullSemanticEligibility;
            if (actual != expectation.ExpectedFullSemanticEligibility)
            {
                return Reject(
                    GasInstallAdmissionReason.EnvelopeTampered,
                    GasInstallAdmissionField.FullSemanticEligibility,
                    FormatBoolean(expectation.ExpectedFullSemanticEligibility),
                    FormatBoolean(actual),
                    in expectation,
                    in candidate,
                    GasInstallEnvelopeAdmissionRuleIds.Integrity,
                    candidate.Provenance);
            }
            if (!actual)
            {
                return Reject(
                    GasInstallAdmissionReason.FullSemanticEligibilityRequired,
                    GasInstallAdmissionField.FullSemanticEligibility,
                    "true",
                    "false",
                    in expectation,
                    in candidate,
                    GasInstallEnvelopeAdmissionRuleIds.Eligibility,
                    candidate.Provenance);
            }

            // C0 刻意不产生 success token，避免 D0/A/B/Z 闭合前形成可安装旁路。
            return Reject(
                GasInstallAdmissionReason.AcceptPathUnavailable,
                GasInstallAdmissionField.AcceptPath,
                "sealed-by-Z",
                "unavailable",
                in expectation,
                in candidate,
                GasInstallEnvelopeAdmissionRuleIds.AcceptPath,
                candidate.Provenance);
        }

        /// <summary>
        /// 验证 candidate contract version 存在且精确匹配。
        /// </summary>
        private static bool TryValidateVersion(
            in GasInstallEnvelopeAdmissionExpectation expectation,
            in GasInstallEnvelopeAdmissionCandidate candidate,
            out GasInstallEnvelopeAdmissionResult result)
        {
            var actualVersion = candidate.Envelope.ContractVersion;
            if (actualVersion <= 0)
            {
                result = Reject(
                    GasInstallAdmissionReason.ContractFieldMissing,
                    GasInstallAdmissionField.ContractVersion,
                    FormatVersion(expectation.ContractVersion),
                    string.Empty,
                    in expectation,
                    in candidate,
                    GasInstallEnvelopeAdmissionRuleIds.Contract,
                    candidate.Provenance);
                return false;
            }

            if (actualVersion != expectation.ContractVersion)
            {
                result = Reject(
                    GasInstallAdmissionReason.ContractFieldMismatch,
                    GasInstallAdmissionField.ContractVersion,
                    FormatVersion(expectation.ContractVersion),
                    FormatVersion(actualVersion),
                    in expectation,
                    in candidate,
                    GasInstallEnvelopeAdmissionRuleIds.Contract,
                    candidate.Provenance);
                return false;
            }

            result = default;
            return true;
        }

        /// <summary>
        /// 验证四元 install identity 均独立存在且匹配。
        /// </summary>
        private static bool TryValidateIdentity(
            in GasInstallEnvelopeAdmissionExpectation expectation,
            in GasInstallEnvelopeAdmissionCandidate candidate,
            out GasInstallEnvelopeAdmissionResult result)
        {
            var actual = candidate.Envelope.Identity;
            if (!TryValidateRequiredValue(
                    GasInstallAdmissionField.SchemaHash,
                    expectation.Identity.SchemaHash,
                    actual.SchemaHash,
                    GasInstallAdmissionReason.InstallIdentityMissing,
                    GasInstallAdmissionReason.InstallIdentityMismatch,
                    GasInstallEnvelopeAdmissionRuleIds.Identity,
                    in expectation,
                    in candidate,
                    out result))
                return false;
            if (!TryValidateRequiredValue(
                    GasInstallAdmissionField.ContentHash,
                    expectation.Identity.ContentHash,
                    actual.ContentHash,
                    GasInstallAdmissionReason.InstallIdentityMissing,
                    GasInstallAdmissionReason.InstallIdentityMismatch,
                    GasInstallEnvelopeAdmissionRuleIds.Identity,
                    in expectation,
                    in candidate,
                    out result))
                return false;
            if (!TryValidateRequiredValue(
                    GasInstallAdmissionField.LayoutHash,
                    expectation.Identity.LayoutHash,
                    actual.LayoutHash,
                    GasInstallAdmissionReason.InstallIdentityMissing,
                    GasInstallAdmissionReason.InstallIdentityMismatch,
                    GasInstallEnvelopeAdmissionRuleIds.Identity,
                    in expectation,
                    in candidate,
                    out result))
                return false;
            return TryValidateRequiredValue(
                GasInstallAdmissionField.ArtifactManifestHash,
                expectation.Identity.ArtifactManifestHash,
                actual.ArtifactManifestHash,
                GasInstallAdmissionReason.InstallIdentityMissing,
                GasInstallAdmissionReason.InstallIdentityMismatch,
                GasInstallEnvelopeAdmissionRuleIds.Identity,
                in expectation,
                in candidate,
                out result);
        }

        /// <summary>
        /// 验证 TypedContract、Layout 与 Capacity 三类 proof 均完整且匹配。
        /// </summary>
        private static bool TryValidateProofs(
            in GasInstallEnvelopeAdmissionExpectation expectation,
            in GasInstallEnvelopeAdmissionCandidate candidate,
            out GasInstallEnvelopeAdmissionResult result)
        {
            var actual = candidate.Envelope;
            if (!TryValidateProof(
                    GasInstallAdmissionField.TypedContractProof,
                    in expectation.TypedContractProof,
                    in actual.TypedContractProof,
                    in expectation,
                    in candidate,
                    out result))
                return false;
            if (!TryValidateProof(
                    GasInstallAdmissionField.LayoutProof,
                    in expectation.LayoutProof,
                    in actual.LayoutProof,
                    in expectation,
                    in candidate,
                    out result))
                return false;
            return TryValidateProof(
                GasInstallAdmissionField.CapacityProof,
                in expectation.CapacityProof,
                in actual.CapacityProof,
                in expectation,
                in candidate,
                out result);
        }

        /// <summary>
        /// 验证一个 proof 的存在、生成期状态、版本、输入身份与 hash 承诺。
        /// </summary>
        private static bool TryValidateProof(
            GasInstallAdmissionField field,
            in GasInstallProofCommitment expectedProof,
            in GasInstallProofCommitment actualProof,
            in GasInstallEnvelopeAdmissionExpectation expectation,
            in GasInstallEnvelopeAdmissionCandidate candidate,
            out GasInstallEnvelopeAdmissionResult result)
        {
            if (TryGetMissingProofValue(
                    in expectedProof,
                    in actualProof,
                    out var missingExpectedValue,
                    out var missingActualValue))
            {
                result = RejectProof(
                    GasInstallAdmissionReason.ProofMissing,
                    field,
                    missingExpectedValue,
                    missingActualValue,
                    in expectedProof,
                    in expectation,
                    in candidate);
                return false;
            }
            if (!actualProof.IsVerified)
            {
                result = RejectProof(
                    GasInstallAdmissionReason.ProofRejected,
                    field,
                    "verified",
                    "rejected",
                    in expectedProof,
                    in expectation,
                    in candidate);
                return false;
            }

            if (TryGetProofMismatch(
                    in expectedProof,
                    in actualProof,
                    out var expectedValue,
                    out var actualValue))
            {
                result = RejectProof(
                    GasInstallAdmissionReason.ProofMismatch,
                    field,
                    expectedValue,
                    actualValue,
                    in expectedProof,
                    in expectation,
                    in candidate);
                return false;
            }

            result = default;
            return true;
        }

        /// <summary>
        /// 重算覆盖完整 candidate（含 eligibility）的 binding；物理 tree 与 artifact bytes 已由 Editor/CI 预先验证。
        /// </summary>
        private static bool TryValidateBinding(
            in GasInstallEnvelopeAdmissionExpectation expectation,
            in GasInstallEnvelopeAdmissionCandidate candidate,
            out GasInstallEnvelopeAdmissionResult result)
        {
            if (!GasInstallEnvelopeBindingCodec.TryCompute(
                    in candidate.Envelope,
                    out var actual))
            {
                result = Reject(
                    GasInstallAdmissionReason.EnvelopeBindingMissing,
                    GasInstallAdmissionField.EnvelopeBinding,
                    expectation.ExpectedEnvelopeBinding,
                    string.Empty,
                    in expectation,
                    in candidate,
                    GasInstallEnvelopeAdmissionRuleIds.Integrity,
                    candidate.Provenance);
                return false;
            }
            if (!EqualsOrdinal(expectation.ExpectedEnvelopeBinding, actual))
            {
                result = Reject(
                    GasInstallAdmissionReason.EnvelopeTampered,
                    GasInstallAdmissionField.EnvelopeBinding,
                    expectation.ExpectedEnvelopeBinding,
                    actual,
                    in expectation,
                    in candidate,
                    GasInstallEnvelopeAdmissionRuleIds.Integrity,
                    candidate.Provenance);
                return false;
            }

            result = default;
            return true;
        }

        /// <summary>
        /// 验证一个必需字符串存在且以 Ordinal 精确匹配。
        /// </summary>
        private static bool TryValidateRequiredValue(
            GasInstallAdmissionField field,
            string expected,
            string actual,
            GasInstallAdmissionReason missingReason,
            GasInstallAdmissionReason mismatchReason,
            string ruleId,
            in GasInstallEnvelopeAdmissionExpectation expectation,
            in GasInstallEnvelopeAdmissionCandidate candidate,
            out GasInstallEnvelopeAdmissionResult result)
        {
            if (IsMissing(actual) || !EqualsOrdinal(expected, actual))
            {
                result = Reject(
                    IsMissing(actual) ? missingReason : mismatchReason,
                    field,
                    expected,
                    actual,
                    in expectation,
                    in candidate,
                    ruleId,
                    candidate.Provenance);
                return false;
            }

            result = default;
            return true;
        }

        /// <summary>
        /// 返回 candidate proof 中按固定顺序遇到的首个缺失承诺值。
        /// </summary>
        private static bool TryGetMissingProofValue(
            in GasInstallProofCommitment expectedProof,
            in GasInstallProofCommitment actualProof,
            out string expectedValue,
            out string actualValue)
        {
            if (actualProof.Version <= 0)
                return SetMismatch(FormatVersion(expectedProof.Version), string.Empty,
                    out expectedValue, out actualValue);
            if (IsMissing(actualProof.InputIdentity))
                return SetMismatch(expectedProof.InputIdentity, actualProof.InputIdentity,
                    out expectedValue, out actualValue);
            if (IsMissing(actualProof.ProofHash))
                return SetMismatch(expectedProof.ProofHash, actualProof.ProofHash,
                    out expectedValue, out actualValue);
            if (IsMissing(actualProof.RuleId))
                return SetMismatch(expectedProof.RuleId, actualProof.RuleId,
                    out expectedValue, out actualValue);
            if (IsMissing(actualProof.Provenance))
                return SetMismatch(expectedProof.Provenance, actualProof.Provenance,
                    out expectedValue, out actualValue);

            expectedValue = string.Empty;
            actualValue = string.Empty;
            return false;
        }

        /// <summary>
        /// 返回 proof 承诺中按固定顺序遇到的首个 expected/actual 错配。
        /// </summary>
        private static bool TryGetProofMismatch(
            in GasInstallProofCommitment expectedProof,
            in GasInstallProofCommitment actualProof,
            out string expectedValue,
            out string actualValue)
        {
            if (expectedProof.Version != actualProof.Version)
                return SetMismatch(FormatVersion(expectedProof.Version), FormatVersion(actualProof.Version),
                    out expectedValue, out actualValue);
            if (!EqualsOrdinal(expectedProof.InputIdentity, actualProof.InputIdentity))
                return SetMismatch(expectedProof.InputIdentity, actualProof.InputIdentity,
                    out expectedValue, out actualValue);
            if (!EqualsOrdinal(expectedProof.ProofHash, actualProof.ProofHash))
                return SetMismatch(expectedProof.ProofHash, actualProof.ProofHash,
                    out expectedValue, out actualValue);
            if (!EqualsOrdinal(expectedProof.RuleId, actualProof.RuleId))
                return SetMismatch(expectedProof.RuleId, actualProof.RuleId,
                    out expectedValue, out actualValue);
            if (!EqualsOrdinal(expectedProof.Provenance, actualProof.Provenance))
                return SetMismatch(expectedProof.Provenance, actualProof.Provenance,
                    out expectedValue, out actualValue);

            expectedValue = string.Empty;
            actualValue = string.Empty;
            return false;
        }

        /// <summary>
        /// 构造 proof 拒绝结果并优先保留 trusted RuleId 与可用 provenance。
        /// </summary>
        private static GasInstallEnvelopeAdmissionResult RejectProof(
            GasInstallAdmissionReason reason,
            GasInstallAdmissionField field,
            string expectedValue,
            string actualValue,
            in GasInstallProofCommitment expectedProof,
            in GasInstallEnvelopeAdmissionExpectation expectation,
            in GasInstallEnvelopeAdmissionCandidate candidate)
        {
            return Reject(
                reason,
                field,
                expectedValue,
                actualValue,
                in expectation,
                in candidate,
                expectedProof.RuleId,
                expectedProof.Provenance);
        }

        /// <summary>
        /// 构造携带完整 expected/actual install identity 的统一拒绝结果。
        /// </summary>
        private static GasInstallEnvelopeAdmissionResult Reject(
            GasInstallAdmissionReason reason,
            GasInstallAdmissionField field,
            string expectedValue,
            string actualValue,
            in GasInstallEnvelopeAdmissionExpectation expectation,
            in GasInstallEnvelopeAdmissionCandidate candidate,
            string ruleId,
            string provenance)
        {
            return new GasInstallEnvelopeAdmissionResult(
                reason,
                field,
                expectedValue ?? string.Empty,
                actualValue ?? string.Empty,
                in expectation.Identity,
                in candidate.Envelope.Identity,
                ruleId ?? string.Empty,
                provenance ?? string.Empty);
        }

        /// <summary>
        /// 判断 trusted proof 是否具备完整且已验证的最小运行时承诺。
        /// </summary>
        private static bool IsTrustedProofAvailable(in GasInstallProofCommitment proof)
        {
            return proof.IsVerified && !IsProofMissing(in proof);
        }

        /// <summary>
        /// 判断 candidate proof 是否缺少任一必需承诺字段。
        /// </summary>
        private static bool IsProofMissing(in GasInstallProofCommitment proof)
        {
            return proof.Version <= 0
                || IsMissing(proof.InputIdentity)
                || IsMissing(proof.ProofHash)
                || IsMissing(proof.RuleId)
                || IsMissing(proof.Provenance);
        }

        /// <summary>
        /// 使用 Ordinal 比较协议身份，禁止文化区或大小写归一化改变语义。
        /// </summary>
        private static bool EqualsOrdinal(string expected, string actual)
        {
            return string.Equals(expected, actual, StringComparison.Ordinal);
        }

        /// <summary>
        /// 判断必需协议字符串是否为 null、空串或纯空白。
        /// </summary>
        private static bool IsMissing(string value)
        {
            return string.IsNullOrWhiteSpace(value);
        }

        /// <summary>
        /// 使用 invariant culture 格式化版本号，保持机器诊断稳定。
        /// </summary>
        private static string FormatVersion(int version)
        {
            return version.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 使用固定小写文本格式化 eligibility，保持机器诊断稳定。
        /// </summary>
        private static string FormatBoolean(bool value)
        {
            return value ? "true" : "false";
        }

        /// <summary>
        /// 设置 proof mismatch 的 expected/actual 值并返回 true 供短路使用。
        /// </summary>
        private static bool SetMismatch(
            string expected,
            string actual,
            out string expectedValue,
            out string actualValue)
        {
            expectedValue = expected ?? string.Empty;
            actualValue = actual ?? string.Empty;
            return true;
        }
    }
}
