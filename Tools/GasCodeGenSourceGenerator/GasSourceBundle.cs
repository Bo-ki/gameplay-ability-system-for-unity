using System;
using System.Collections.Generic;
using System.Linq;

namespace Gas.CodeGen.SourceGenerator
{
    /// <summary>
    /// 保存已按 D0-M2R 冻结字段分离的 GasSourceBundle-v1 header 与五项 C# source subset。
    /// </summary>
    public sealed class GasSourceBundle
    {
        private readonly byte[] sourceInputHash;
        private readonly byte[] schemaHash;
        private readonly byte[] contentHash;
        private readonly byte[] layoutHash;
        private readonly byte[] artifactManifestHash;
        private readonly byte[] sourceArtifactInventoryHash;
        private readonly byte[] analyzerSha256;
        private readonly byte[] routeScaffoldSha256;
        private readonly GasSourceEntry[] entries;

        /// <summary>
        /// 创建 bundle model，并把 entries 固定为 target/hint ordinal 顺序后计算独立 source inventory hash。
        /// </summary>
        public GasSourceBundle(
            byte[] sourceInputHash,
            byte[] schemaHash,
            byte[] contentHash,
            byte[] layoutHash,
            byte[] artifactManifestHash,
            byte[] analyzerSha256,
            byte[] routeScaffoldSha256,
            IEnumerable<GasSourceEntry> entries)
        {
            this.sourceInputHash = CloneDigest(sourceInputHash, nameof(sourceInputHash));
            this.schemaHash = CloneDigest(schemaHash, nameof(schemaHash));
            this.contentHash = CloneDigest(contentHash, nameof(contentHash));
            this.layoutHash = CloneDigest(layoutHash, nameof(layoutHash));
            this.artifactManifestHash = CloneDigest(artifactManifestHash, nameof(artifactManifestHash));
            this.analyzerSha256 = CloneDigest(analyzerSha256, nameof(analyzerSha256));
            this.routeScaffoldSha256 = CloneDigest(routeScaffoldSha256, nameof(routeScaffoldSha256));
            this.entries = entries == null
                ? throw new ArgumentNullException(nameof(entries))
                : entries.OrderBy(item => item.TargetAssembly, StringComparer.Ordinal)
                    .ThenBy(item => item.HintName, StringComparer.Ordinal)
                    .ToArray();
            GasSourceBundleValidator.ValidateRequiredSources(this.entries);
            sourceArtifactInventoryHash = GasSourceBundleCodec.ComputeSourceArtifactInventoryHash(this.entries);
        }

        public byte[] SourceInputHash { get { return (byte[])sourceInputHash.Clone(); } }
        public byte[] SchemaHash { get { return (byte[])schemaHash.Clone(); } }
        public byte[] ContentHash { get { return (byte[])contentHash.Clone(); } }
        public byte[] LayoutHash { get { return (byte[])layoutHash.Clone(); } }
        public byte[] ArtifactManifestHash { get { return (byte[])artifactManifestHash.Clone(); } }
        public byte[] SourceArtifactInventoryHash { get { return (byte[])sourceArtifactInventoryHash.Clone(); } }
        public byte[] AnalyzerSha256 { get { return (byte[])analyzerSha256.Clone(); } }
        public byte[] RouteScaffoldSha256 { get { return (byte[])routeScaffoldSha256.Clone(); } }
        public IReadOnlyList<GasSourceEntry> Entries { get { return Array.AsReadOnly((GasSourceEntry[])entries.Clone()); } }
        public bool FullSemanticEligibility { get { return false; } }

        /// <summary>
        /// 复制并验证一个完整 32-byte digest，阻止引用方在 model 建立后篡改身份。
        /// </summary>
        private static byte[] CloneDigest(byte[] value, string parameterName)
        {
            if (value == null || value.Length != 32)
            {
                throw new ArgumentException("Digest must contain exactly 32 bytes.", parameterName);
            }

            return (byte[])value.Clone();
        }
    }

    /// <summary>
    /// 保存单个 bundle C# source entry，并从不可变 source snapshot 计算独立 SHA-256。
    /// </summary>
    public sealed class GasSourceEntry
    {
        private readonly byte[] sourceBytes;
        private readonly byte[] sourceSha256;

        /// <summary>
        /// 从目标程序集、hint、category 与精确 source bytes 创建不可变 entry。
        /// </summary>
        public GasSourceEntry(string targetAssembly, string hintName, byte artifactCategory, byte[] sourceBytes)
        {
            TargetAssembly = targetAssembly ?? throw new ArgumentNullException(nameof(targetAssembly));
            HintName = hintName ?? throw new ArgumentNullException(nameof(hintName));
            ArtifactCategory = artifactCategory;
            this.sourceBytes = sourceBytes == null
                ? throw new ArgumentNullException(nameof(sourceBytes))
                : (byte[])sourceBytes.Clone();
            sourceSha256 = GasHashing.ComputeSha256(this.sourceBytes);
        }

        public string TargetAssembly { get; private set; }
        public string HintName { get; private set; }
        public byte ArtifactCategory { get; private set; }
        public uint SourceByteLength { get { return checked((uint)sourceBytes.Length); } }
        public byte[] SourceSha256 { get { return (byte[])sourceSha256.Clone(); } }
        public byte[] SourceBytes { get { return (byte[])sourceBytes.Clone(); } }

        /// <summary>
        /// 将已严格验证的 UTF-8 source snapshot 转换为 Roslyn 输入文本。
        /// </summary>
        public string GetSourceText()
        {
            return new System.Text.UTF8Encoding(false, true).GetString(sourceBytes);
        }
    }
}
