using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace GAS.Editor
{
    public sealed class GasCodeGenManifest
    {
        private const string ManifestFileName = "GasCodeGen.manifest.json";
        private const int CurrentManifestVersion = 1;
        private const string CurrentGeneratorVersion = "EX-GAS-CodeGen-v1";
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
        private readonly string _projectRoot;
        private readonly string _outputRoot;
        private readonly string _inputHash;
        private readonly List<Entry> _entries = new List<Entry>();

        public GasCodeGenManifest(string projectRoot, string outputRoot, string inputHash)
        {
            _projectRoot = Normalize(projectRoot);
            _outputRoot = Normalize(outputRoot);
            _inputHash = inputHash ?? string.Empty;
            EnsureUnderProjectRoot(_outputRoot);
        }

        public IReadOnlyList<Entry> Entries => _entries;

        public string ManifestPath => Path.Combine(_outputRoot, ManifestFileName);

        public int ManifestVersion => CurrentManifestVersion;

        public string GeneratorVersion => CurrentGeneratorVersion;

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
            var normalizedPath = Normalize(fullPath);
            EnsureUnderProjectRoot(normalizedPath);
            var projectRelativePath = ToProjectRelativePath(normalizedPath);
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
                _entries[i].VersionControlled = IsVersionControlledPath(normalizedPath);
                return;
            }

            _entries.Add(new Entry
            {
                PhaseName = phaseName,
                FileName = Path.GetFileName(normalizedPath),
                ProjectRelativePath = projectRelativePath,
                Layer = layer,
                RuntimeVisible = runtimeVisible,
                VersionControlled = IsVersionControlledPath(normalizedPath),
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
                var oldPath = Normalize(ProjectRelativeToFullPath(oldEntry.ProjectRelativePath));
                if (current.Contains(oldPath))
                    continue;

                if (!oldPath.StartsWith(_outputRoot, StringComparison.OrdinalIgnoreCase))
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

        public void Save()
        {
            if (!Directory.Exists(_outputRoot))
                Directory.CreateDirectory(_outputRoot);

            var data = new Data
            {
                ManifestVersion = CurrentManifestVersion,
                GeneratorVersion = CurrentGeneratorVersion,
                OutputRoot = ToProjectRelativePath(_outputRoot),
                InputHash = _inputHash,
                Entries = _entries.ToArray(),
            };

            File.WriteAllText(ManifestPath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }

        private string ProjectRelativeToFullPath(string projectRelativePath)
        {
            return Path.Combine(_projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
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

        private bool IsUnderProjectRoot(string fullPath)
        {
            return string.Equals(fullPath, _projectRoot, StringComparison.OrdinalIgnoreCase)
                   || fullPath.StartsWith(
                       _projectRoot + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase)
                   || fullPath.StartsWith(
                       _projectRoot + Path.AltDirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }

        private bool IsVersionControlledPath(string fullPath)
        {
            var relative = ToProjectRelativePath(fullPath);
            return relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                   || relative.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase)
                   || relative.StartsWith("ProjectSettings/", StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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

        [Serializable]
        private sealed class Data
        {
            public int ManifestVersion;
            public string GeneratorVersion;
            public string OutputRoot;
            public string InputHash;
            public Entry[] Entries;
        }

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
