using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace GAS.Editor
{
    /// <summary>
    /// 以 EX-GAS-InstallEnvelope-v2 唯一 binary wire 封存 Runtime-safe 安装身份投影。
    /// </summary>
    internal static class GasCodeGenInstallEnvelope
    {
        internal const string FileName = "GasInstallEnvelope.bin";
        internal const string EncodingDomain = "EX-GAS-InstallEnvelope-v2";
        private const uint EnvelopeVersion = 2;
        private const uint DescriptorVersion = 3;
        private const string ArtifactIdentityAlgorithm = "SHA-256";

        /// <summary>
        /// 按 ADR-0001 固定字段顺序编码 envelope；proof 未闭合字段写零长度。
        /// </summary>
        internal static byte[] Encode(GasCodeGenInstallEnvelopeInput input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            input.Validate();
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(EnvelopeVersion);
                WriteText(writer, EncodingDomain);
                writer.Write(DescriptorVersion);
                WriteText(writer, input.DescriptorSha256);
                WriteText(writer, GasCodeGenSemanticIdentity.Algorithm);
                WriteText(writer, ArtifactIdentityAlgorithm);
                WriteText(writer, GasCodeGenPackageDescriptor.ArtifactManifestHashDomain);
                WriteText(writer, GasCodeGenPackageDescriptor.RequiredArtifactSetId);
                WriteText(writer, GasCodeGenPackageDescriptor.RequiredArtifactSetContractHash);
                WriteText(writer, input.SourceInputHash);
                WriteText(writer, input.SchemaHash);
                WriteText(writer, input.ContentHash);
                WriteText(writer, input.LayoutHash);
                WriteText(writer, input.ArtifactManifestHash);
                WriteText(writer, input.SourceArtifactInventoryHash);
                WriteText(writer, input.SelectorSha256);
                WriteText(writer, input.AnalyzerSha256);
                WriteText(writer, input.RouteScaffoldSha256);
                WriteText(writer, input.CandidateCompilePlanSha256);
                WriteText(writer, string.Empty);
                WriteText(writer, string.Empty);
                WriteText(writer, string.Empty);
                writer.Write((byte)0);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 写入 uint32 byte-length 前缀的严格 UTF-8 字段。
        /// </summary>
        private static void WriteText(BinaryWriter writer, string value)
        {
            var bytes = new UTF8Encoding(false, true).GetBytes(value ?? string.Empty);
            writer.Write((uint)bytes.Length);
            writer.Write(bytes);
        }
    }

    /// <summary>
    /// 收集 envelope v2 所需 descriptor、四元身份与 route provenance 字段。
    /// </summary>
    internal sealed class GasCodeGenInstallEnvelopeInput
    {
        /// <summary>
        /// 创建 eligibility=false 且 proof fields 为空的 D1 envelope 输入。
        /// </summary>
        internal GasCodeGenInstallEnvelopeInput(
            string descriptorSha256,
            string sourceInputHash,
            string schemaHash,
            string contentHash,
            string layoutHash,
            string artifactManifestHash,
            string sourceArtifactInventoryHash,
            string selectorSha256,
            string analyzerSha256,
            string routeScaffoldSha256,
            string candidateCompilePlanSha256)
        {
            DescriptorSha256 = descriptorSha256;
            SourceInputHash = sourceInputHash;
            SchemaHash = schemaHash;
            ContentHash = contentHash;
            LayoutHash = layoutHash;
            ArtifactManifestHash = artifactManifestHash;
            SourceArtifactInventoryHash = sourceArtifactInventoryHash;
            SelectorSha256 = selectorSha256;
            AnalyzerSha256 = analyzerSha256;
            RouteScaffoldSha256 = routeScaffoldSha256;
            CandidateCompilePlanSha256 = candidateCompilePlanSha256;
        }

        internal string DescriptorSha256 { get; }
        internal string SourceInputHash { get; }
        internal string SchemaHash { get; }
        internal string ContentHash { get; }
        internal string LayoutHash { get; }
        internal string ArtifactManifestHash { get; }
        internal string SourceArtifactInventoryHash { get; }
        internal string SelectorSha256 { get; }
        internal string AnalyzerSha256 { get; }
        internal string RouteScaffoldSha256 { get; }
        internal string CandidateCompilePlanSha256 { get; }

        /// <summary>
        /// 要求所有已闭合 identity 使用 canonical lowercase SHA-256。
        /// </summary>
        internal void Validate()
        {
            ValidateHash(DescriptorSha256, nameof(DescriptorSha256));
            ValidateHash(SourceInputHash, nameof(SourceInputHash));
            ValidateHash(SchemaHash, nameof(SchemaHash));
            ValidateHash(ContentHash, nameof(ContentHash));
            ValidateHash(LayoutHash, nameof(LayoutHash));
            ValidateHash(ArtifactManifestHash, nameof(ArtifactManifestHash));
            ValidateHash(SourceArtifactInventoryHash, nameof(SourceArtifactInventoryHash));
            ValidateHash(SelectorSha256, nameof(SelectorSha256));
            ValidateHash(AnalyzerSha256, nameof(AnalyzerSha256));
            ValidateHash(RouteScaffoldSha256, nameof(RouteScaffoldSha256));
            ValidateHash(CandidateCompilePlanSha256, nameof(CandidateCompilePlanSha256));
        }

        /// <summary>
        /// 校验 SHA 字段恰为 64 个 lowercase hexadecimal ASCII 字符。
        /// </summary>
        private static void ValidateHash(string value, string fieldName)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64)
                throw new InvalidDataException(fieldName + " must be a 64-character SHA-256.");
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!(character >= '0' && character <= '9')
                    && !(character >= 'a' && character <= 'f'))
                {
                    throw new InvalidDataException(
                        fieldName + " must be lowercase hexadecimal at index "
                        + index.ToString(CultureInfo.InvariantCulture) + ".");
                }
            }
        }
    }
}
