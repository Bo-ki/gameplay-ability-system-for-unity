using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace GAS.Runtime
{
    /// <summary>
    /// 以版本化、目的域隔离且长度定界的 canonical 编码派生 Runtime install-envelope SHA-256 binding；C1 仍须按 D0 在解码前限制字段大小。
    /// </summary>
    internal static class GasInstallEnvelopeBindingCodec
    {
        internal const int CanonicalFormatVersion = 1;
        internal const string HashDomain = "EX-GAS/Runtime/V1/InstallEnvelopeBinding";

        private const int Sha256HexLength = 64;
        private static readonly UTF8Encoding CanonicalUtf8 = new UTF8Encoding(false, true);

        /// <summary>
        /// 固定每个 canonical 字段的稳定 ordinal，避免字段重排或跨 proof 交换保持同一 binding。
        /// </summary>
        private enum FieldOrdinal : int
        {
            BindingDomain = 1,
            CodecVersion = 2,
            ContractVersion = 10,
            Algorithm = 11,
            ContractDomain = 12,
            SchemaHash = 20,
            ContentHash = 21,
            LayoutHash = 22,
            ArtifactManifestHash = 23,
            TypedProof = 30,
            LayoutProof = 40,
            CapacityProof = 50,
            FullSemanticEligibility = 60,
        }

        /// <summary>
        /// 从 candidate 的完整 Runtime 投影重算 binding，非法 Unicode 或哈希提供器失败时 fail closed。
        /// </summary>
        internal static bool TryCompute(
            in GasInstallEnvelopeProjection envelope,
            out string binding)
        {
            return TryCompute(
                envelope.ContractVersion,
                envelope.Algorithm,
                envelope.Domain,
                in envelope.Identity,
                in envelope.TypedContractProof,
                in envelope.LayoutProof,
                in envelope.CapacityProof,
                envelope.FullSemanticEligibility,
                out binding);
        }

        /// <summary>
        /// 从 trusted expectation 的全部冻结字段重算 binding，仅校验内部一致性而不认证其外部来源。
        /// </summary>
        internal static bool TryCompute(
            in GasInstallEnvelopeAdmissionExpectation expectation,
            out string binding)
        {
            return TryCompute(
                expectation.ContractVersion,
                expectation.Algorithm,
                expectation.Domain,
                in expectation.Identity,
                in expectation.TypedContractProof,
                in expectation.LayoutProof,
                in expectation.CapacityProof,
                expectation.ExpectedFullSemanticEligibility,
                out binding);
        }

        /// <summary>
        /// 判断 binding 是否为 SHA-256 codec 唯一接受的 64 字符小写十六进制表示。
        /// </summary>
        internal static bool IsCanonicalBinding(string binding)
        {
            if (binding == null || binding.Length != Sha256HexLength)
                return false;

            for (var index = 0; index < binding.Length; index++)
            {
                var value = binding[index];
                if (!((value >= '0' && value <= '9') || (value >= 'a' && value <= 'f')))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 按固定字段顺序编码完整 binding material 并计算小写 SHA-256。
        /// </summary>
        private static bool TryCompute(
            int contractVersion,
            string algorithm,
            string contractDomain,
            in GasInstallEnvelopeIdentity identity,
            in GasInstallProofCommitment typedContractProof,
            in GasInstallProofCommitment layoutProof,
            in GasInstallProofCommitment capacityProof,
            bool fullSemanticEligibility,
            out string binding)
        {
            binding = string.Empty;
            try
            {
                var canonicalBytes = new List<byte>(512);
                AppendString(canonicalBytes, FieldOrdinal.BindingDomain, HashDomain);
                AppendInt32(canonicalBytes, FieldOrdinal.CodecVersion, CanonicalFormatVersion);
                AppendInt32(canonicalBytes, FieldOrdinal.ContractVersion, contractVersion);
                AppendString(canonicalBytes, FieldOrdinal.Algorithm, algorithm);
                AppendString(canonicalBytes, FieldOrdinal.ContractDomain, contractDomain);
                AppendIdentity(canonicalBytes, in identity);
                AppendProof(canonicalBytes, FieldOrdinal.TypedProof, in typedContractProof);
                AppendProof(canonicalBytes, FieldOrdinal.LayoutProof, in layoutProof);
                AppendProof(canonicalBytes, FieldOrdinal.CapacityProof, in capacityProof);
                AppendBoolean(
                    canonicalBytes,
                    FieldOrdinal.FullSemanticEligibility,
                    fullSemanticEligibility);

                using (var sha256 = SHA256.Create())
                {
                    if (sha256 == null)
                        return false;
                    binding = ToLowerHex(sha256.ComputeHash(canonicalBytes.ToArray()));
                    return true;
                }
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        /// <summary>
        /// 按稳定 ordinal 写入四元 install identity 的全部字段。
        /// </summary>
        private static void AppendIdentity(
            List<byte> bytes,
            in GasInstallEnvelopeIdentity identity)
        {
            AppendString(bytes, FieldOrdinal.SchemaHash, identity.SchemaHash);
            AppendString(bytes, FieldOrdinal.ContentHash, identity.ContentHash);
            AppendString(bytes, FieldOrdinal.LayoutHash, identity.LayoutHash);
            AppendString(bytes, FieldOrdinal.ArtifactManifestHash, identity.ArtifactManifestHash);
        }

        /// <summary>
        /// 写入一个 proof 的版本、输入身份、hash、RuleId、provenance 与验证位。
        /// </summary>
        private static void AppendProof(
            List<byte> bytes,
            FieldOrdinal proofOrdinal,
            in GasInstallProofCommitment proof)
        {
            var ordinal = (int)proofOrdinal;
            AppendInt32(bytes, ordinal, proof.Version);
            AppendString(bytes, ordinal + 1, proof.InputIdentity);
            AppendString(bytes, ordinal + 2, proof.ProofHash);
            AppendString(bytes, ordinal + 3, proof.RuleId);
            AppendString(bytes, ordinal + 4, proof.Provenance);
            AppendBoolean(bytes, ordinal + 5, proof.IsVerified);
        }

        /// <summary>
        /// 以字段 ordinal、四字节长度和固定小端 payload 编码一个 Int32。
        /// </summary>
        private static void AppendInt32(
            List<byte> bytes,
            FieldOrdinal ordinal,
            int value)
        {
            AppendInt32(bytes, (int)ordinal, value);
        }

        /// <summary>
        /// 以整数 ordinal 编码 proof 内的一个 Int32 字段。
        /// </summary>
        private static void AppendInt32(List<byte> bytes, int ordinal, int value)
        {
            AppendFieldHeader(bytes, ordinal, sizeof(int));
            AppendRawInt32(bytes, value);
        }

        /// <summary>
        /// 以字段 ordinal 与严格 UTF-8 字节长度编码字符串，null 使用 -1 长度。
        /// </summary>
        private static void AppendString(
            List<byte> bytes,
            FieldOrdinal ordinal,
            string value)
        {
            AppendString(bytes, (int)ordinal, value);
        }

        /// <summary>
        /// 以整数 ordinal 编码 proof 内的一个字符串字段。
        /// </summary>
        private static void AppendString(List<byte> bytes, int ordinal, string value)
        {
            if (value == null)
            {
                AppendFieldHeader(bytes, ordinal, -1);
                return;
            }

            var encoded = CanonicalUtf8.GetBytes(value);
            AppendFieldHeader(bytes, ordinal, encoded.Length);
            bytes.AddRange(encoded);
        }

        /// <summary>
        /// 以字段 ordinal 与单字节 0/1 编码布尔值。
        /// </summary>
        private static void AppendBoolean(
            List<byte> bytes,
            FieldOrdinal ordinal,
            bool value)
        {
            AppendBoolean(bytes, (int)ordinal, value);
        }

        /// <summary>
        /// 以整数 ordinal 编码 proof 内的一个布尔字段。
        /// </summary>
        private static void AppendBoolean(List<byte> bytes, int ordinal, bool value)
        {
            AppendFieldHeader(bytes, ordinal, 1);
            bytes.Add(value ? (byte)1 : (byte)0);
        }

        /// <summary>
        /// 写入固定小端字段 ordinal 与 payload 长度，消除串联边界歧义。
        /// </summary>
        private static void AppendFieldHeader(List<byte> bytes, int ordinal, int payloadLength)
        {
            AppendRawInt32(bytes, ordinal);
            AppendRawInt32(bytes, payloadLength);
        }

        /// <summary>
        /// 以平台无关的固定小端顺序写入 Int32 原始位。
        /// </summary>
        private static void AppendRawInt32(List<byte> bytes, int value)
        {
            var raw = unchecked((uint)value);
            bytes.Add((byte)raw);
            bytes.Add((byte)(raw >> 8));
            bytes.Add((byte)(raw >> 16));
            bytes.Add((byte)(raw >> 24));
        }

        /// <summary>
        /// 将 SHA-256 字节转换为文化区无关的小写十六进制文本。
        /// </summary>
        private static string ToLowerHex(byte[] bytes)
        {
            const string hexDigits = "0123456789abcdef";
            var characters = new char[bytes.Length * 2];
            for (var index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = hexDigits[bytes[index] >> 4];
                characters[index * 2 + 1] = hexDigits[bytes[index] & 0x0f];
            }

            return new string(characters);
        }
    }
}
