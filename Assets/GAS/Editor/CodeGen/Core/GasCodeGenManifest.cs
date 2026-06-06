using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace GAS.Editor
{
    public sealed class GasCodeGenManifest
    {
        private const string ManifestFileName = "GasCodeGen.manifest.json";
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

        public void AddGeneratedFile(string phaseName, string fullPath, string layer, bool runtimeVisible)
        {
            var normalizedPath = Normalize(fullPath);
            EnsureUnderProjectRoot(normalizedPath);

            _entries.Add(new Entry
            {
                PhaseName = phaseName,
                FileName = Path.GetFileName(normalizedPath),
                ProjectRelativePath = ToProjectRelativePath(normalizedPath),
                Layer = layer,
                RuntimeVisible = runtimeVisible,
                VersionControlled = IsVersionControlledPath(normalizedPath),
            });
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

        public void Save()
        {
            if (!Directory.Exists(_outputRoot))
                Directory.CreateDirectory(_outputRoot);

            var data = new Data
            {
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

        [Serializable]
        private sealed class Data
        {
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
            public bool VersionControlled;
        }
    }
}
