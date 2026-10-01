using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace GAS.Editor
{
    /// <summary>
    /// 记录一次代码生成的输入身份、产物合约与逐文件字节身份，作为发布校验的外部事实源。
    /// </summary>
    public sealed class GasCodeGenManifest
    {
        private const string ManifestFileName = "GasCodeGen.manifest.json";
        private const int CurrentManifestVersion = 3;
        private const string CurrentGeneratorVersion = "EX-GAS-CodeGen-v3";
        private const string ArtifactManifestHashDomain = "EX-GAS-ArtifactManifest-v3";
        private static readonly HashSet<string> KnownArtifactKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "AssemblyDefinition",
            "NormalizedDefinitionRow",
            "DefinitionId",
            "BlobSchema",
            "BlobBuilder",
            "DefinitionCatalog",
            "StaticLookup",
            "RuntimePureGlue",
            "PureRuntimeGlue",
            "BakerGlue",
            "ValidationArtifact",
            "RuntimeDemoConfig",
            "MigrationProofOnly",
        };
        private static readonly HashSet<string> s_allowedLayers = new HashSet<string>(StringComparer.Ordinal)
        {
            "Runtime",
            "Baking",
            "Editor",
            "Editor/CI",
        };
        private readonly string _projectRoot;
        private readonly string _outputRoot;
        private readonly string _publishedOutputRoot;
        private readonly string _inputHash;
        private readonly List<Entry> _entries = new List<Entry>();
        private string _artifactManifestHash = string.Empty;
        private bool _hasSavedFrozenIdentities;

        /// <summary>
        /// 创建 manifest；outputRoot 是当前物理生成目录，publishedOutputRoot 是条目最终发布路径。
        /// </summary>
        public GasCodeGenManifest(
            string projectRoot,
            string outputRoot,
            string inputHash,
            string publishedOutputRoot = null)
        {
            _projectRoot = Normalize(projectRoot);
            _outputRoot = Normalize(outputRoot);
            _publishedOutputRoot = Normalize(string.IsNullOrWhiteSpace(publishedOutputRoot)
                ? outputRoot
                : publishedOutputRoot);
            _inputHash = inputHash ?? string.Empty;
            EnsureUnderProjectRoot(_outputRoot);
            EnsureUnderProjectRoot(_publishedOutputRoot);
        }

        public IReadOnlyList<Entry> Entries => _entries;

        public string ManifestPath => Path.Combine(_outputRoot, ManifestFileName);

        public int ManifestVersion => CurrentManifestVersion;

        public string GeneratorVersion => CurrentGeneratorVersion;

        public string ArtifactManifestHash => _artifactManifestHash;

        /// <summary>
        /// 将一个已登记条目的最终发布路径解析回本次物理 candidate 文件。
        /// </summary>
        public string ResolvePhysicalPath(Entry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            return Normalize(ProjectRelativeToFullPath(entry.ProjectRelativePath));
        }

        /// <summary>
        /// 将当前物理输出根内的文件映射为最终发布用项目相对路径。
        /// </summary>
        public string GetPublishedProjectRelativePath(string physicalPath)
        {
            var normalizedPath = Normalize(physicalPath);
            EnsureUnderOutputRoot(normalizedPath);
            return ToPublishedProjectRelativePath(normalizedPath);
        }

        public void AddGeneratedFile(
            string phaseName,
            string fullPath,
            string layer,
            bool runtimeVisible,
            string artifactCategory = null,
            string artifactOwner = null,
            bool mayAllocate = false,
            bool mayOwnLifecycle = false,
            bool mayOwnStructuralChange = false,
            bool mayOwnNativeContainer = false)
        {
            ValidateEntryContract(phaseName, layer, runtimeVisible, artifactCategory);
            _hasSavedFrozenIdentities = false;
            var normalizedPath = Normalize(fullPath);
            EnsureUnderOutputRoot(normalizedPath);
            var projectRelativePath = ToPublishedProjectRelativePath(normalizedPath);
            var resolvedCategory = string.IsNullOrWhiteSpace(artifactCategory)
                ? "Unclassified"
                : artifactCategory.Trim();
            var resolvedOwner = string.IsNullOrWhiteSpace(artifactOwner)
                ? "Unassigned"
                : artifactOwner.Trim();

            for (var i = 0; i < _entries.Count; i++)
            {
                if (!string.Equals(
                        _entries[i].ProjectRelativePath,
                        projectRelativePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _entries[i].PhaseName = phaseName;
                _entries[i].FileName = Path.GetFileName(normalizedPath);
                _entries[i].Layer = layer;
                _entries[i].RuntimeVisible = runtimeVisible;
                ApplyArtifactContract(
                    _entries[i],
                    resolvedCategory,
                    resolvedOwner,
                    mayAllocate,
                    mayOwnLifecycle,
                    mayOwnStructuralChange,
                    mayOwnNativeContainer);
                _entries[i].VersionControlled = IsVersionControlledPath(projectRelativePath);
                return;
            }

            _entries.Add(new Entry
            {
                PhaseName = phaseName,
                FileName = Path.GetFileName(normalizedPath),
                ProjectRelativePath = projectRelativePath,
                Layer = layer,
                RuntimeVisible = runtimeVisible,
                VersionControlled = IsVersionControlledPath(projectRelativePath),
            });

            ApplyArtifactContract(
                _entries[_entries.Count - 1],
                resolvedCategory,
                resolvedOwner,
                mayAllocate,
                mayOwnLifecycle,
                mayOwnStructuralChange,
                mayOwnNativeContainer);
        }

        /// <summary>
        /// 校验 Runtime-visible 产物的分类与 owner 声明，阻止未分类或越权产物进入 manifest。
        /// </summary>
        public IReadOnlyList<string> CollectContractErrors()
        {
            var errors = new List<string>();
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (string.IsNullOrWhiteSpace(entry.GeneratedArtifactKind)
                    || string.Equals(entry.GeneratedArtifactKind, "Unclassified", StringComparison.Ordinal)
                    || !KnownArtifactKinds.Contains(entry.GeneratedArtifactKind))
                {
                    errors.Add($"{entry.ProjectRelativePath}: artifact kind 无效或未声明（{entry.GeneratedArtifactKind}）。");
                }

                if (string.IsNullOrWhiteSpace(entry.GeneratedArtifactOwner)
                    || string.Equals(entry.GeneratedArtifactOwner, "Unassigned", StringComparison.Ordinal))
                {
                    errors.Add($"{entry.ProjectRelativePath}: artifact 未声明 GeneratedArtifactOwner。");
                }

                if (!string.Equals(entry.ArtifactCategory, entry.GeneratedArtifactKind, StringComparison.Ordinal))
                {
                    errors.Add($"{entry.ProjectRelativePath}: ArtifactCategory 与 GeneratedArtifactKind 不一致。");
                }

                if (entry.RuntimeVisible
                    && entry.IsRuntimePureGlue
                    && (entry.MayAllocate
                        || entry.MayOwnLifecycle
                        || entry.MayOwnStructuralChange
                        || entry.MayOwnNativeContainer))
                {
                    errors.Add($"{entry.ProjectRelativePath}: RuntimePureGlue 不得拥有 allocator/lifecycle/structural/native-container。");
                }

                if (entry.RuntimeVisible
                    && (string.Equals(entry.GeneratedArtifactKind, "BlobBuilder", StringComparison.Ordinal)
                        || string.Equals(entry.GeneratedArtifactKind, "BakerGlue", StringComparison.Ordinal)
                        || string.Equals(entry.GeneratedArtifactKind, "ValidationArtifact", StringComparison.Ordinal)
                        || string.Equals(entry.GeneratedArtifactKind, "NormalizedDefinitionRow", StringComparison.Ordinal)))
                {
                    errors.Add($"{entry.ProjectRelativePath}: {entry.GeneratedArtifactKind} 不得标记为 Runtime-visible。");
                }

                if (!entry.RuntimeVisible
                    && (string.Equals(entry.GeneratedArtifactKind, "DefinitionId", StringComparison.Ordinal)
                        || string.Equals(entry.GeneratedArtifactKind, "BlobSchema", StringComparison.Ordinal)
                        || string.Equals(entry.GeneratedArtifactKind, "DefinitionCatalog", StringComparison.Ordinal)
                        || string.Equals(entry.GeneratedArtifactKind, "StaticLookup", StringComparison.Ordinal)
                        || entry.IsRuntimePureGlue))
                {
                    errors.Add($"{entry.ProjectRelativePath}: {entry.GeneratedArtifactKind} 必须标记为 Runtime-visible。");
                }
            }

            return errors;
        }

        /// <summary>
        /// 在保存前执行 manifest 合约 gate，并把所有违规聚合为可读错误。
        /// </summary>
        public void EnsureContractValid()
        {
            var errors = CollectContractErrors();
            if (errors.Count == 0)
                return;

            throw new InvalidOperationException(
                "Generated artifact manifest contract failed:\n" + string.Join("\n", errors));
        }

        public int DeleteOrphanedFiles()
        {
            var manifestPath = ManifestPath;
            if (!File.Exists(manifestPath))
                return 0;

            var oldManifest = JsonConvert.DeserializeObject<Data>(File.ReadAllText(manifestPath));
            if (oldManifest == null || oldManifest.Entries == null)
                return 0;

            var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in _entries)
                current.Add(Normalize(ProjectRelativeToFullPath(entry.ProjectRelativePath)));

            var deleted = 0;
            foreach (var oldEntry in oldManifest.Entries)
            {
                if (oldEntry == null || string.IsNullOrWhiteSpace(oldEntry.ProjectRelativePath))
                    continue;

                var oldPath = Normalize(ProjectRelativeToFullPath(oldEntry.ProjectRelativePath));
                if (current.Contains(oldPath))
                    continue;

                if (!IsUnderOutputRoot(oldPath))
                    continue;

                if (!CanDeleteOrphanedGeneratedFile(oldPath))
                    continue;

                if (!File.Exists(oldPath))
                    continue;

                File.Delete(oldPath);
                var metaPath = oldPath + ".meta";
                if (File.Exists(metaPath))
                    File.Delete(metaPath);
                deleted++;
            }

            return deleted;
        }

        private static bool CanDeleteOrphanedGeneratedFile(string path)
        {
            var fileName = Path.GetFileName(path);
            return fileName.EndsWith(".gen.cs", StringComparison.OrdinalIgnoreCase)
                   || fileName.Equals("GasCodeGenValidationReport.md", StringComparison.OrdinalIgnoreCase)
                   || (fileName.StartsWith("com.exhard.exgas.generated.", StringComparison.OrdinalIgnoreCase)
                       && fileName.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 保存前冻结全部 artifact bytes 的 SHA-256，并由排序后的完整条目计算总 manifest 身份。
        /// </summary>
        public void Save()
        {
            if (!Directory.Exists(_outputRoot))
                Directory.CreateDirectory(_outputRoot);

            EnsureContractValid();
            RefreshArtifactIdentities();
            var data = new Data
            {
                ManifestVersion = CurrentManifestVersion,
                GeneratorVersion = CurrentGeneratorVersion,
                OutputRoot = ToProjectRelativePath(_publishedOutputRoot),
                InputHash = _inputHash,
                ArtifactManifestHash = _artifactManifestHash,
                Entries = _entries.ToArray(),
            };

            File.WriteAllText(ManifestPath, JsonConvert.SerializeObject(data, Formatting.Indented));
            _hasSavedFrozenIdentities = true;
        }

        /// <summary>
        /// 从当前磁盘 bytes 重算每个 manifest 条目及其排序聚合身份，缺失产物立即失败。
        /// </summary>
        public void RefreshArtifactIdentities()
        {
            _hasSavedFrozenIdentities = false;
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var path = Normalize(ProjectRelativeToFullPath(entry.ProjectRelativePath));
                EnsureUnderOutputRoot(path);
                if (!File.Exists(path))
                    throw new FileNotFoundException($"Generated artifact is missing: {entry.ProjectRelativePath}", path);

                var bytes = File.ReadAllBytes(path);
                entry.ByteLength = bytes.LongLength;
                entry.ContentSha256 = ComputeSha256(bytes);
                var metaPath = path + ".meta";
                if (File.Exists(metaPath))
                {
                    var metaBytes = File.ReadAllBytes(metaPath);
                    entry.MetaByteLength = metaBytes.LongLength;
                    entry.MetaContentSha256 = ComputeSha256(metaBytes);
                }
                else
                {
                    entry.MetaByteLength = 0;
                    entry.MetaContentSha256 = string.Empty;
                }
            }

            _artifactManifestHash = ComputeArtifactManifestHash();
        }

        /// <summary>
        /// 在 Save 后重新核对全部冻结 artifact/.meta bytes 与聚合身份，阻断编译期间的产物 TOCTOU 漂移。
        /// </summary>
        public void EnsureFrozenArtifactIdentitiesUnchanged()
        {
            if (!_hasSavedFrozenIdentities || string.IsNullOrWhiteSpace(_artifactManifestHash))
                throw new InvalidOperationException("Manifest must be saved before verifying frozen artifact identities.");

            for (var index = 0; index < _entries.Count; index++)
                EnsureFrozenEntryIdentityUnchanged(_entries[index]);

            var computedManifestHash = ComputeArtifactManifestHash();
            if (!string.Equals(_artifactManifestHash, computedManifestHash, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Artifact manifest aggregate identity changed after it was frozen.");
            }
        }

        /// <summary>
        /// 核对一个条目的 artifact 与可选 .meta 文件仍精确匹配 Save 时冻结的长度和 SHA-256。
        /// </summary>
        private void EnsureFrozenEntryIdentityUnchanged(Entry entry)
        {
            var path = Normalize(ProjectRelativeToFullPath(entry.ProjectRelativePath));
            EnsureUnderOutputRoot(path);
            if (!File.Exists(path))
                throw new FileNotFoundException($"Frozen artifact is missing: {entry.ProjectRelativePath}", path);

            EnsureFrozenFileIdentityUnchanged(
                path,
                entry.ByteLength,
                entry.ContentSha256,
                entry.ProjectRelativePath);

            var metaPath = path + ".meta";
            if (!File.Exists(metaPath))
            {
                if (entry.MetaByteLength != 0 || !string.IsNullOrEmpty(entry.MetaContentSha256))
                    throw new FileNotFoundException($"Frozen artifact meta is missing: {entry.ProjectRelativePath}.meta", metaPath);
                return;
            }

            EnsureFrozenFileIdentityUnchanged(
                metaPath,
                entry.MetaByteLength,
                entry.MetaContentSha256,
                entry.ProjectRelativePath + ".meta");
        }

        /// <summary>
        /// 重算单个冻结文件的长度和 SHA-256，并在任一身份变化时显式失败。
        /// </summary>
        private static void EnsureFrozenFileIdentityUnchanged(
            string path,
            long expectedLength,
            string expectedSha256,
            string displayPath)
        {
            var bytes = File.ReadAllBytes(path);
            var actualSha256 = ComputeSha256(bytes);
            if (bytes.LongLength == expectedLength
                && string.Equals(actualSha256, expectedSha256, StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidDataException(
                $"Frozen artifact bytes changed: {displayPath}. "
                + $"ExpectedLength={expectedLength}, ActualLength={bytes.LongLength}, "
                + $"ExpectedSha256={expectedSha256}, ActualSha256={actualSha256}.");
        }

        /// <summary>
        /// 仅按 canonical path、artifact kind/owner 与 artifact/.meta bytes 计算 v3 聚合身份。
        /// </summary>
        private string ComputeArtifactManifestHash()
        {
            var entries = new List<Entry>(_entries);
            entries.Sort((left, right) => string.CompareOrdinal(
                CanonicalizeArtifactPath(left.ProjectRelativePath),
                CanonicalizeArtifactPath(right.ProjectRelativePath)));
            var builder = new StringBuilder();
            AppendHashField(builder, ArtifactManifestHashDomain);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                AppendHashField(builder, CanonicalizeArtifactPath(entry.ProjectRelativePath));
                AppendHashField(builder, entry.GeneratedArtifactKind);
                AppendHashField(builder, entry.GeneratedArtifactOwner);
                AppendHashField(builder, entry.ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
                AppendHashField(builder, entry.ContentSha256);
                AppendHashField(builder, entry.MetaByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture));
                AppendHashField(builder, entry.MetaContentSha256);
            }

            return ComputeSha256(Encoding.UTF8.GetBytes(builder.ToString()));
        }

        /// <summary>
        /// 将项目相对 artifact 路径统一为正斜杠表示，保证跨平台排序与哈希输入一致。
        /// </summary>
        private static string CanonicalizeArtifactPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        /// <summary>
        /// 使用长度前缀写入哈希字段，避免不同字段组合产生文本拼接歧义。
        /// </summary>
        private static void AppendHashField(StringBuilder builder, string value)
        {
            var resolved = value ?? string.Empty;
            builder.Append(resolved.Length)
                .Append(':')
                .Append(resolved)
                .Append('|');
        }

        /// <summary>
        /// 返回小写十六进制 SHA-256，供条目和聚合 manifest 共用同一编码。
        /// </summary>
        private static string ComputeSha256(byte[] bytes)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(bytes);
            var builder = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                builder.Append(hash[i].ToString("x2"));
            return builder.ToString();
        }

        private string ProjectRelativeToFullPath(string projectRelativePath)
        {
            var publishedPath = Normalize(Path.Combine(
                _projectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsUnderRoot(publishedPath, _publishedOutputRoot))
                throw new InvalidOperationException($"Manifest entry escapes published output root: {projectRelativePath}");

            var relative = publishedPath.Substring(_publishedOutputRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.Combine(_outputRoot, relative);
        }

        /// <summary>
        /// 将候选物理路径映射成最终 active 项目相对路径，避免 manifest 暴露 Temp 事务目录。
        /// </summary>
        private string ToPublishedProjectRelativePath(string physicalPath)
        {
            var relative = physicalPath.Substring(_outputRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return ToProjectRelativePath(Path.Combine(_publishedOutputRoot, relative));
        }

        private string ToProjectRelativePath(string fullPath)
        {
            var relative = fullPath.Substring(_projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        private void EnsureUnderProjectRoot(string fullPath)
        {
            if (!IsUnderProjectRoot(fullPath))
                throw new InvalidOperationException($"Generated path escapes project root: {fullPath}");
        }

        // 确认生成文件同时位于项目根与当前 manifest 输出根下。
        private void EnsureUnderOutputRoot(string fullPath)
        {
            EnsureUnderProjectRoot(fullPath);
            if (!IsUnderOutputRoot(fullPath))
                throw new InvalidOperationException($"Generated path escapes manifest output root: {fullPath}");
        }

        // 校验 manifest 条目元数据，防止未分类或错误层级的 artifact 进入事实源。
        private void ValidateEntryContract(
            string phaseName,
            string layer,
            bool runtimeVisible,
            string artifactCategory)
        {
            if (string.IsNullOrWhiteSpace(phaseName))
                throw new ArgumentException("Manifest phase name is required.", nameof(phaseName));
            if (!s_allowedLayers.Contains(layer ?? string.Empty))
                throw new ArgumentException($"Unsupported manifest layer: {layer}", nameof(layer));

            var category = artifactCategory == null ? string.Empty : artifactCategory.Trim();
            if (category.Length > 0 && !KnownArtifactKinds.Contains(category))
                throw new ArgumentException($"Unsupported manifest artifact category: {category}", nameof(artifactCategory));
            if (runtimeVisible && !string.Equals(layer, "Runtime", StringComparison.Ordinal))
                throw new InvalidOperationException("Runtime-visible artifacts must use the Runtime layer.");
        }

        private bool IsUnderProjectRoot(string fullPath)
        {
            return IsUnderRoot(fullPath, _projectRoot);
        }

        // 判断路径是否位于输出根目录本身或其子目录，避免同名前缀误匹配。
        private bool IsUnderOutputRoot(string fullPath)
        {
            return IsUnderRoot(fullPath, _outputRoot);
        }

        /// <summary>
        /// 判断规范化路径是否位于指定根下，拒绝同名前缀目录越界。
        /// </summary>
        private static bool IsUnderRoot(string fullPath, string root)
        {
            return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
                   || fullPath.StartsWith(
                       root + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase)
                   || fullPath.StartsWith(
                       root + Path.AltDirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 按最终发布路径判断 artifact 是否应进入版本控制，而不是按 Temp candidate 路径误判。
        /// </summary>
        private static bool IsVersionControlledPath(string projectRelativePath)
        {
            return projectRelativePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                   || projectRelativePath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)
                   || projectRelativePath.StartsWith("ProjectSettings/", StringComparison.OrdinalIgnoreCase);
        }

        // 规范化绝对路径并保留盘符根路径的尾部分隔符。
        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required.", nameof(path));

            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
                return fullPath;

            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// 将 artifact 分类与职责边界一次性写入 manifest，保持旧 ArtifactCategory 字段可读。
        /// </summary>
        private static void ApplyArtifactContract(
            Entry entry,
            string category,
            string owner,
            bool mayAllocate,
            bool mayOwnLifecycle,
            bool mayOwnStructuralChange,
            bool mayOwnNativeContainer)
        {
            entry.ArtifactCategory = category;
            entry.GeneratedArtifactKind = category;
            entry.GeneratedArtifactOwner = owner;
            entry.MayAllocate = mayAllocate;
            entry.MayOwnLifecycle = mayOwnLifecycle;
            entry.MayOwnStructuralChange = mayOwnStructuralChange;
            entry.MayOwnNativeContainer = mayOwnNativeContainer;
        }

        /// <summary>
        /// 定义 manifest JSON 根结构并保持旧读取路径可向前失败。
        /// </summary>
        [Serializable]
        private sealed class Data
        {
            public int ManifestVersion;
            public string GeneratorVersion;
            public string OutputRoot;
            public string InputHash;
            public string ArtifactManifestHash;
            public Entry[] Entries;
        }

        /// <summary>
        /// 描述一个生成产物的职责边界、来源与精确磁盘字节身份。
        /// </summary>
        [Serializable]
        public sealed class Entry
        {
            public string PhaseName;
            public string FileName;
            public string ProjectRelativePath;
            public string Layer;
            public bool RuntimeVisible;
            public string ArtifactCategory;
            public string GeneratedArtifactKind;
            public string GeneratedArtifactOwner;
            public bool MayAllocate;
            public bool MayOwnLifecycle;
            public bool MayOwnStructuralChange;
            public bool MayOwnNativeContainer;
            public bool VersionControlled;
            public long ByteLength;
            public string ContentSha256;
            public long MetaByteLength;
            public string MetaContentSha256;

            [JsonIgnore]
            public bool IsRuntimePureGlue =>
                RuntimeVisible
                && (string.Equals(GeneratedArtifactKind, "RuntimePureGlue", StringComparison.Ordinal)
                    || string.Equals(GeneratedArtifactKind, "PureRuntimeGlue", StringComparison.Ordinal))
                && !MayAllocate
                && !MayOwnLifecycle
                && !MayOwnStructuralChange
                && !MayOwnNativeContainer;

            [JsonIgnore]
            public bool IsRuntimeBoundaryArtifact =>
                RuntimeVisible
                && ((string.Equals(GeneratedArtifactKind, "RuntimePureGlue", StringComparison.Ordinal)
                     || string.Equals(GeneratedArtifactKind, "PureRuntimeGlue", StringComparison.Ordinal))
                    || string.Equals(GeneratedArtifactKind, "AssemblyDefinition", StringComparison.Ordinal));
        }
    }
}
