using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace GAS.Editor
{
    /// <summary>
    /// 将现有 emitter 的六个受管 artifact 封存为 descriptor v3、单 selector bundle 与 source 双向映射。
    /// </summary>
    public static class GasCodeGenPackageDescriptor
    {
        internal const int DescriptorVersion = 3;
        internal const string DescriptorFileName = "GasPackageDescriptor.json";
        internal const string SelectorFileName = "Generation.GasCodeGenSourceGenerator.additionalfile";
        internal const string RequiredArtifactSetId = "EX-GAS-RuntimeV1-RequiredArtifacts-v2";
        internal const string RequiredArtifactSetContractHash =
            "63f69551708194359d76c3772fb68983a3632d8994d818d7ec3e8c931ac74565";
        internal const string ArtifactManifestHashDomain = "EX-GAS-ArtifactManifest-v3";
        internal const string SourceInventoryDomain = "EX-GAS-SourceArtifactInventory-v1\0";
        internal const string SourceBundleDomain = "EX-GAS-SourceBundle-v1";
        internal const string SelectorMagic = "EX-GAS-SourceSelector-v1\n";
        private const int RequiredManifestVersion = 3;
        private const string ArtifactIdentityAlgorithm = "SHA-256";
        private const string CanonicalIdentityEncoding = "LengthPrefixedFields-v1";
        private const string RequiredContractDomain = "EX-GAS-RequiredArtifactSetContract-v1\0";
        private const string CandidateArtifactManifestDomain = "EX-GAS-CandidateArtifactManifest-v1";
        private const string CoreComponent = "Core";
        private const string AutoChessComponent = "AutoChess";
        private const int MaximumSelectorByteLength = 100663296;
        private const int MaximumSourceByteLength = 16777216;
        private const long MaximumTotalSourceByteLength = 67108864;
        private static readonly UTF8Encoding s_strictUtf8 = new UTF8Encoding(false, true);
        private static readonly RequiredArtifactContract[] s_requiredArtifacts =
        {
            new RequiredArtifactContract(
                "Assets/AutoChessDemo/Generated/AutoChessGeneratedConfig.gen.cs",
                "RuntimeDemoConfig", "AutoChessDemo", true, AutoChessComponent,
                "com.exhard.exgas.autochessdemo", "AutoChessGeneratedConfig.gen.cs", 3),
            new RequiredArtifactContract(
                "Assets/GAS/Generated/CodeGen/Editor/LubanNormalizedRows.gen.cs",
                "NormalizedDefinitionRow", "DefinitionCodeGen", false, CoreComponent,
                "com.exhard.exgas.generated.editor", "LubanNormalizedRows.gen.cs", 2),
            new RequiredArtifactContract(
                "Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md",
                "ValidationArtifact", "EditorCi", false, CoreComponent,
                string.Empty, string.Empty, 0),
            new RequiredArtifactContract(
                "Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs",
                "RuntimePureGlue", "DefinitionCodeGen", true, CoreComponent,
                "com.exhard.exgas.generated.runtime", "RuntimeAbilityActivation.gen.cs", 1),
            new RequiredArtifactContract(
                "Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs",
                "RuntimePureGlue", "DefinitionCodeGen", true, CoreComponent,
                "com.exhard.exgas.generated.runtime", "RuntimeActiveEffect.gen.cs", 1),
            new RequiredArtifactContract(
                "Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs",
                "RuntimePureGlue", "DefinitionCodeGen", true, CoreComponent,
                "com.exhard.exgas.generated.runtime", "RuntimeEffectInstant.gen.cs", 1),
        };

        /// <summary>
        /// 从两个已冻结 manifest 形成完整 artifact manifest、source inventory 与唯一 selector candidate。
        /// </summary>
        internal static GasCodeGenSelectorCandidateSnapshot CreateSelectorCandidate(
            GasCodeGenCandidateWorkspace workspace,
            GasCodeGenCandidateWorkspace.CandidateAuthority authority,
            string sourceInputHash,
            string schemaHash,
            string contentHash,
            string layoutHash,
            GasCodeGenManifest coreManifest,
            GasCodeGenManifest autoChessManifest,
            GasCodeGenRouteSnapshot route)
        {
            ValidateCreateArguments(workspace, authority, coreManifest, autoChessManifest, route);
            var artifacts = CollectArtifactEntries(coreManifest, autoChessManifest);
            ValidateRequiredArtifactSet(artifacts);
            var artifactManifestHash = ComputeArtifactManifestHash(artifacts);
            EnsureNoArtifactAggregateHash(artifacts, artifactManifestHash);
            var artifactManifestBytes = SerializeCandidateArtifactManifest(artifacts, artifactManifestHash);
            var sourceMappings = CreateSourceMappings(artifacts);
            var sourceInventoryHash = ComputeSourceArtifactInventoryHash(sourceMappings);
            EnsureNoSourceIdentityHash(sourceMappings, sourceInventoryHash);

            var bundleBytes = EncodeBundle(
                NormalizeSha256(sourceInputHash, nameof(sourceInputHash)),
                NormalizeSha256(schemaHash, nameof(schemaHash)),
                NormalizeSha256(contentHash, nameof(contentHash)),
                NormalizeSha256(layoutHash, nameof(layoutHash)),
                artifactManifestHash,
                sourceInventoryHash,
                route.AnalyzerSha256,
                route.RouteScaffoldSha256,
                sourceMappings);
            var selectorBytes = EncodeSelector(bundleBytes);
            var selectorSha256 = ComputeSha256(selectorBytes);
            EnsureNoArtifactAggregateHash(artifacts, selectorSha256);
            ValidateSelectorBytes(selectorBytes);
            var selectorPath = Path.Combine(workspace.ControlRoot, SelectorFileName);
            WriteBytesDurable(selectorPath, selectorBytes);

            return new GasCodeGenSelectorCandidateSnapshot(
                workspace,
                authority,
                selectorPath,
                selectorBytes,
                selectorSha256,
                bundleBytes,
                NormalizeSha256(sourceInputHash, nameof(sourceInputHash)),
                NormalizeSha256(schemaHash, nameof(schemaHash)),
                NormalizeSha256(contentHash, nameof(contentHash)),
                NormalizeSha256(layoutHash, nameof(layoutHash)),
                artifactManifestHash,
                artifactManifestBytes,
                ComputeSha256(artifactManifestBytes),
                sourceInventoryHash,
                route.AnalyzerSha256,
                route.RouteScaffoldSha256,
                artifacts,
                sourceMappings);
        }

        /// <summary>
        /// 在 compile plan 封存后写 descriptor v3 和 envelope v2，并返回 Store 唯一接受的 sealed snapshot。
        /// </summary>
        internal static GasCodeGenPackageDescriptorSnapshot Save(
            GasCodeGenCandidateWorkspace workspace,
            GasCodeGenCandidateWorkspace.CandidateAuthority authority,
            GasCodeGenSelectorCandidateSnapshot candidate,
            GasCodeGenCandidateCompilePlanSnapshot compilePlan,
            GasCodeGenRouteSnapshot route)
        {
            ValidateSealArguments(workspace, authority, candidate, compilePlan, route);
            var descriptorData = CreateDescriptorData(candidate, compilePlan);
            var descriptorBytes = SerializeJson(descriptorData);
            var descriptorSha256 = ComputeSha256(descriptorBytes);
            var descriptorPath = Path.Combine(workspace.ControlRoot, DescriptorFileName);
            WriteBytesDurable(descriptorPath, descriptorBytes);

            var envelopeBytes = GasCodeGenInstallEnvelope.Encode(
                new GasCodeGenInstallEnvelopeInput(
                    descriptorSha256,
                    candidate.SourceInputHash,
                    candidate.SchemaHash,
                    candidate.ContentHash,
                    candidate.LayoutHash,
                    candidate.ArtifactManifestHash,
                    candidate.SourceArtifactInventoryHash,
                    candidate.SelectorSha256,
                    candidate.AnalyzerSha256,
                    candidate.RouteScaffoldSha256,
                    compilePlan.Sha256));
            var envelopePath = Path.Combine(workspace.ControlRoot, GasCodeGenInstallEnvelope.FileName);
            WriteBytesDurable(envelopePath, envelopeBytes);

            candidate.EnsureUnchanged();
            compilePlan.EnsureUnchanged();
            route.EnsureUnchanged();
            return new GasCodeGenPackageDescriptorSnapshot(
                workspace,
                authority,
                candidate,
                compilePlan,
                route,
                descriptorPath,
                descriptorBytes,
                descriptorSha256,
                envelopePath,
                envelopeBytes,
                ComputeSha256(envelopeBytes));
        }

        /// <summary>
        /// 让非 Unity D1-B contract tests 复用 production 双向 mapping validator，不新增发布权限。
        /// </summary>
        internal static void ValidateSourceBijectionForTests(
            GasCodeGenSelectorCandidateSnapshot candidate,
            IReadOnlyList<GasCodeGenSourceMapping> mappings)
        {
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            if (mappings == null)
                throw new ArgumentNullException(nameof(mappings));
            ValidateSourceBijection(candidate.Artifacts, mappings);
        }

        /// <summary>
        /// 让非 Unity contract tests 复用六项 artifact 聚合哈希禁令，不建立第二套测试判断。
        /// </summary>
        internal static void ValidateArtifactAggregateHashesForTests(
            IReadOnlyList<ArtifactEntryData> artifacts,
            params string[] hashes)
        {
            if (artifacts == null)
                throw new ArgumentNullException(nameof(artifacts));
            EnsureNoArtifactAggregateHash(artifacts, hashes);
        }

        /// <summary>
        /// 校验 candidate 创建参数、manifest 版本与所有冻结 bytes 未发生漂移。
        /// </summary>
        private static void ValidateCreateArguments(
            GasCodeGenCandidateWorkspace workspace,
            GasCodeGenCandidateWorkspace.CandidateAuthority authority,
            GasCodeGenManifest coreManifest,
            GasCodeGenManifest autoChessManifest,
            GasCodeGenRouteSnapshot route)
        {
            if (workspace == null)
                throw new ArgumentNullException(nameof(workspace));
            if (coreManifest == null)
                throw new ArgumentNullException(nameof(coreManifest));
            if (autoChessManifest == null)
                throw new ArgumentNullException(nameof(autoChessManifest));
            if (route == null)
                throw new ArgumentNullException(nameof(route));
            workspace.EnsureAuthority(authority);
            EnsureManifestVersion(coreManifest, nameof(coreManifest));
            EnsureManifestVersion(autoChessManifest, nameof(autoChessManifest));
            coreManifest.EnsureFrozenArtifactIdentitiesUnchanged();
            autoChessManifest.EnsureFrozenArtifactIdentitiesUnchanged();
            route.EnsureUnchanged();
            EnsureRequiredContractHash();
        }

        /// <summary>
        /// 校验 descriptor seal 的四个 snapshot 仍属于同一 workspace 和相同 route identity。
        /// </summary>
        private static void ValidateSealArguments(
            GasCodeGenCandidateWorkspace workspace,
            GasCodeGenCandidateWorkspace.CandidateAuthority authority,
            GasCodeGenSelectorCandidateSnapshot candidate,
            GasCodeGenCandidateCompilePlanSnapshot compilePlan,
            GasCodeGenRouteSnapshot route)
        {
            if (workspace == null)
                throw new ArgumentNullException(nameof(workspace));
            if (candidate == null)
                throw new ArgumentNullException(nameof(candidate));
            if (compilePlan == null)
                throw new ArgumentNullException(nameof(compilePlan));
            if (route == null)
                throw new ArgumentNullException(nameof(route));
            workspace.EnsureAuthority(authority);
            candidate.EnsureOwnedBy(workspace, authority);
            candidate.EnsureUnchanged();
            compilePlan.EnsureUnchanged();
            route.EnsureUnchanged();
            if (!string.Equals(candidate.SelectorSha256, compilePlan.SelectorSha256, StringComparison.Ordinal)
                || !string.Equals(candidate.AnalyzerSha256, compilePlan.AnalyzerSha256, StringComparison.Ordinal)
                || !string.Equals(candidate.RouteScaffoldSha256, compilePlan.RouteScaffoldSha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Descriptor snapshots do not describe one route.");
            }
        }

        /// <summary>
        /// 联合两个组件 manifest，拒绝 canonical path 冲突并保留每项实际物理路径。
        /// </summary>
        private static ArtifactEntryData[] CollectArtifactEntries(
            GasCodeGenManifest coreManifest,
            GasCodeGenManifest autoChessManifest)
        {
            var exactPaths = new Dictionary<string, ArtifactEntryData>(StringComparer.Ordinal);
            var foldedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddManifestEntries(exactPaths, foldedPaths, coreManifest, CoreComponent);
            AddManifestEntries(exactPaths, foldedPaths, autoChessManifest, AutoChessComponent);
            var artifacts = new List<ArtifactEntryData>(exactPaths.Values);
            artifacts.Sort((left, right) => string.CompareOrdinal(left.CanonicalPath, right.CanonicalPath));
            return artifacts.ToArray();
        }

        /// <summary>
        /// 将单个 manifest 的条目复制为不可变 descriptor artifact 条目。
        /// </summary>
        private static void AddManifestEntries(
            IDictionary<string, ArtifactEntryData> exactPaths,
            ISet<string> foldedPaths,
            GasCodeGenManifest manifest,
            string component)
        {
            for (var index = 0; index < manifest.Entries.Count; index++)
            {
                var artifact = CreateArtifactEntry(manifest, manifest.Entries[index], component);
                if (!foldedPaths.Add(artifact.CanonicalPath))
                    throw new InvalidDataException("Duplicate or case-colliding artifact path: " + artifact.CanonicalPath);
                exactPaths.Add(artifact.CanonicalPath, artifact);
            }
        }

        /// <summary>
        /// 重读 artifact 与 managed meta bytes，并要求它们匹配 manifest 冻结身份。
        /// </summary>
        private static ArtifactEntryData CreateArtifactEntry(
            GasCodeGenManifest manifest,
            GasCodeGenManifest.Entry entry,
            string component)
        {
            if (entry == null)
                throw new InvalidDataException("Manifest contains a null artifact entry.");
            var canonicalPath = CanonicalizeProjectRelativePath(entry.ProjectRelativePath);
            var physicalPath = Path.GetFullPath(manifest.ResolvePhysicalPath(entry));
            var artifactBytes = File.ReadAllBytes(physicalPath);
            var metaPath = physicalPath + ".meta";
            if (!File.Exists(metaPath))
                throw new FileNotFoundException("Required managed meta is missing: " + canonicalPath + ".meta", metaPath);
            var metaBytes = File.ReadAllBytes(metaPath);
            if (metaBytes.Length == 0)
                throw new InvalidDataException("Required managed meta is empty: " + canonicalPath + ".meta");
            EnsureFrozenIdentity(entry, artifactBytes, metaBytes, canonicalPath);
            return new ArtifactEntryData
            {
                CanonicalPath = canonicalPath,
                GeneratedArtifactKind = RequireCanonicalText(entry.GeneratedArtifactKind, "kind", canonicalPath),
                GeneratedArtifactOwner = RequireCanonicalText(entry.GeneratedArtifactOwner, "owner", canonicalPath),
                RuntimeVisible = entry.RuntimeVisible,
                ByteLength = artifactBytes.LongLength,
                ContentSha256 = ComputeSha256(artifactBytes),
                MetaByteLength = metaBytes.LongLength,
                MetaContentSha256 = ComputeSha256(metaBytes),
                SourceComponent = component,
                PhysicalPath = physicalPath,
                Bytes = artifactBytes,
            };
        }

        /// <summary>
        /// 比较实际 artifact/meta 长度与 SHA，分别阻断 length 和 content mismatch。
        /// </summary>
        private static void EnsureFrozenIdentity(
            GasCodeGenManifest.Entry entry,
            byte[] artifactBytes,
            byte[] metaBytes,
            string canonicalPath)
        {
            var artifactSha = ComputeSha256(artifactBytes);
            var metaSha = ComputeSha256(metaBytes);
            if (entry.ByteLength != artifactBytes.LongLength
                || !string.Equals(entry.ContentSha256, artifactSha, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Artifact length/SHA mismatch: " + canonicalPath);
            }
            if (entry.MetaByteLength != metaBytes.LongLength
                || !string.Equals(entry.MetaContentSha256, metaSha, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Managed meta length/SHA mismatch: " + canonicalPath + ".meta");
            }
        }

        /// <summary>
        /// 要求完整 manifest 与 v2 六项 contract 双向完全相等，并逐字段校验职责和 meta。
        /// </summary>
        private static void ValidateRequiredArtifactSet(ArtifactEntryData[] artifacts)
        {
            if (artifacts.Length != s_requiredArtifacts.Length)
            {
                throw new InvalidDataException(
                    "Required artifact set mismatch. Expected=" + s_requiredArtifacts.Length
                    + ", Actual=" + artifacts.Length + ".");
            }
            var actualByPath = new Dictionary<string, ArtifactEntryData>(StringComparer.Ordinal);
            for (var index = 0; index < artifacts.Length; index++)
                actualByPath.Add(artifacts[index].CanonicalPath, artifacts[index]);
            for (var index = 0; index < s_requiredArtifacts.Length; index++)
            {
                var required = s_requiredArtifacts[index];
                if (!actualByPath.TryGetValue(required.Path, out var actual))
                    throw new InvalidDataException("Required artifact is missing: " + required.Path);
                required.EnsureMatches(actual);
            }
        }

        /// <summary>
        /// 从五个 C# required item 构造按 target/hint ordinal 排序的 bundle mapping。
        /// </summary>
        private static GasCodeGenSourceMapping[] CreateSourceMappings(ArtifactEntryData[] artifacts)
        {
            var requiredByPath = new Dictionary<string, RequiredArtifactContract>(StringComparer.Ordinal);
            for (var index = 0; index < s_requiredArtifacts.Length; index++)
                requiredByPath.Add(s_requiredArtifacts[index].Path, s_requiredArtifacts[index]);
            var mappings = new List<GasCodeGenSourceMapping>();
            for (var index = 0; index < artifacts.Length; index++)
            {
                var artifact = artifacts[index];
                var required = requiredByPath[artifact.CanonicalPath];
                if (required.Category == 0)
                    continue;
                ValidateSourceBytes(artifact.Bytes, artifact.CanonicalPath);
                mappings.Add(new GasCodeGenSourceMapping(
                    artifact.CanonicalPath,
                    required.TargetAssembly,
                    required.HintName,
                    required.Category,
                    checked((int)artifact.ByteLength),
                    artifact.ContentSha256,
                    artifact.Bytes));
            }
            mappings.Sort(CompareMappings);
            ValidateSourceBijection(artifacts, mappings);
            return mappings.ToArray();
        }

        /// <summary>
        /// 比较 source mapping 的 target assembly 与 hint name，固定 selector wire 顺序。
        /// </summary>
        private static int CompareMappings(GasCodeGenSourceMapping left, GasCodeGenSourceMapping right)
        {
            var target = string.CompareOrdinal(left.TargetAssembly, right.TargetAssembly);
            return target != 0 ? target : string.CompareOrdinal(left.HintName, right.HintName);
        }

        /// <summary>
        /// 双向验证 required C# artifact 与 bundle entries 一一对应且 length/hash/target/hint/category 相等。
        /// </summary>
        private static void ValidateSourceBijection(
            IReadOnlyList<ArtifactEntryData> artifacts,
            IReadOnlyList<GasCodeGenSourceMapping> mappings)
        {
            if (mappings.Count != 5)
                throw new InvalidDataException("Source bundle must contain exactly five required C# entries.");
            var artifactsByPath = new Dictionary<string, ArtifactEntryData>(StringComparer.Ordinal);
            var requiredByPath = new Dictionary<string, RequiredArtifactContract>(StringComparer.Ordinal);
            for (var index = 0; index < artifacts.Count; index++)
                artifactsByPath.Add(artifacts[index].CanonicalPath, artifacts[index]);
            for (var index = 0; index < s_requiredArtifacts.Length; index++)
                requiredByPath.Add(s_requiredArtifacts[index].Path, s_requiredArtifacts[index]);
            var mappedPaths = new HashSet<string>(StringComparer.Ordinal);
            var mappedRoutes = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < mappings.Count; index++)
            {
                var mapping = mappings[index];
                if (!requiredByPath.TryGetValue(mapping.CanonicalPath, out var required)
                    || required.Category == 0
                    || !string.Equals(mapping.TargetAssembly, required.TargetAssembly, StringComparison.Ordinal)
                    || !string.Equals(mapping.HintName, required.HintName, StringComparison.Ordinal)
                    || mapping.Category != required.Category)
                {
                    throw new InvalidDataException("Source mapping route contract mismatch: " + mapping.CanonicalPath);
                }
                if (!mappedPaths.Add(mapping.CanonicalPath)
                    || !mappedRoutes.Add(mapping.TargetAssembly + "\0" + mapping.HintName)
                    || !artifactsByPath.TryGetValue(mapping.CanonicalPath, out var artifact)
                    || artifact.ByteLength != mapping.SourceByteLength
                    || !string.Equals(artifact.ContentSha256, mapping.SourceSha256, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Source mapping is not a bijection: " + mapping.CanonicalPath);
                }
            }
            for (var index = 0; index < s_requiredArtifacts.Length; index++)
            {
                if (s_requiredArtifacts[index].Category != 0
                    && !mappedPaths.Contains(s_requiredArtifacts[index].Path))
                {
                    throw new InvalidDataException("Required C# artifact is not mapped: " + s_requiredArtifacts[index].Path);
                }
            }
        }

        /// <summary>
        /// 校验 C# source 严格 UTF-8、no-BOM、LF-only、非空且不含 NUL。
        /// </summary>
        private static void ValidateSourceBytes(byte[] bytes, string canonicalPath)
        {
            if (bytes.Length == 0 || bytes.Length > MaximumSourceByteLength)
                throw new InvalidDataException("Source byte length is outside the allowed range: " + canonicalPath);
            if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
                throw new InvalidDataException("Source must be UTF-8 no-BOM: " + canonicalPath);
            for (var index = 0; index < bytes.Length; index++)
            {
                if (bytes[index] == 0 || bytes[index] == (byte)'\r')
                    throw new InvalidDataException("Source must be NUL-free and LF-only: " + canonicalPath);
            }
            s_strictUtf8.GetString(bytes);
        }

        /// <summary>
        /// 计算 `EX-GAS-SourceArtifactInventory-v1` 的 binary inventory hash。
        /// </summary>
        private static string ComputeSourceArtifactInventoryHash(IReadOnlyList<GasCodeGenSourceMapping> mappings)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Encoding.ASCII.GetBytes(SourceInventoryDomain));
                writer.Write((uint)mappings.Count);
                for (var index = 0; index < mappings.Count; index++)
                {
                    var mapping = mappings[index];
                    WriteUtf8(writer, mapping.TargetAssembly);
                    WriteUtf8(writer, mapping.HintName);
                    writer.Write(mapping.Category);
                    writer.Write((uint)mapping.SourceByteLength);
                    writer.Write(HexToBytes(mapping.SourceSha256));
                }
                writer.Flush();
                return ComputeSha256(stream.ToArray());
            }
        }

        /// <summary>
        /// 逐字段编码 GasSourceBundle-v1 header 与五个 exact source entries。
        /// </summary>
        private static byte[] EncodeBundle(
            string sourceInputHash,
            string schemaHash,
            string contentHash,
            string layoutHash,
            string artifactManifestHash,
            string sourceInventoryHash,
            string analyzerSha256,
            string routeScaffoldSha256,
            IReadOnlyList<GasCodeGenSourceMapping> mappings)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write((uint)1);
                WriteUtf8(writer, SourceBundleDomain);
                WriteRawHash(writer, sourceInputHash);
                WriteRawHash(writer, schemaHash);
                WriteRawHash(writer, contentHash);
                WriteRawHash(writer, layoutHash);
                WriteRawHash(writer, artifactManifestHash);
                WriteRawHash(writer, sourceInventoryHash);
                WriteUtf8(writer, RequiredArtifactSetId);
                WriteRawHash(writer, RequiredArtifactSetContractHash);
                WriteRawHash(writer, analyzerSha256);
                WriteRawHash(writer, routeScaffoldSha256);
                writer.Write((byte)0);
                writer.Write((uint)mappings.Count);
                for (var index = 0; index < mappings.Count; index++)
                    WriteSourceEntry(writer, mappings[index]);
                writer.Flush();
                return stream.ToArray();
            }
        }

        /// <summary>
        /// 写入一个 source entry 的 target、hint、category、length、raw hash 与 source bytes。
        /// </summary>
        private static void WriteSourceEntry(BinaryWriter writer, GasCodeGenSourceMapping mapping)
        {
            WriteUtf8(writer, mapping.TargetAssembly);
            WriteUtf8(writer, mapping.HintName);
            writer.Write(mapping.Category);
            writer.Write((uint)mapping.SourceByteLength);
            WriteRawHash(writer, mapping.SourceSha256);
            writer.Write(mapping.SourceBytes);
        }

        /// <summary>
        /// 将 bundle 编成唯一两行 canonical selector 文本。
        /// </summary>
        private static byte[] EncodeSelector(byte[] bundleBytes)
        {
            var text = SelectorMagic + Convert.ToBase64String(bundleBytes) + "\n";
            return new UTF8Encoding(false, true).GetBytes(text);
        }

        /// <summary>
        /// 严格读取 canonical selector 与完整 GasSourceBundle-v1，任何 wire 或语义偏差均 fail closed。
        /// </summary>
        internal static void ValidateSelectorBytes(byte[] selectorBytes)
        {
            var bundleBytes = DecodeCanonicalSelectorText(selectorBytes);
            ValidateBundleBytes(bundleBytes, ComputeSha256(selectorBytes));
        }

        /// <summary>
        /// 验证 selector exact ASCII 两行 envelope，并返回 canonical Base64 payload。
        /// </summary>
        private static byte[] DecodeCanonicalSelectorText(byte[] selectorBytes)
        {
            if (selectorBytes == null)
                throw new ArgumentNullException(nameof(selectorBytes));
            if (selectorBytes.Length == 0 || selectorBytes.Length > MaximumSelectorByteLength)
                throw new InvalidDataException("Selector byte length is outside the supported protocol bound.");
            string selectorText;
            try
            {
                selectorText = s_strictUtf8.GetString(selectorBytes);
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("Selector is not strict UTF-8.", exception);
            }
            if (!selectorText.StartsWith(SelectorMagic, StringComparison.Ordinal)
                || !selectorText.EndsWith("\n", StringComparison.Ordinal))
                throw new InvalidDataException("Selector magic or final LF is not canonical.");
            var base64 = selectorText.Substring(
                SelectorMagic.Length,
                selectorText.Length - SelectorMagic.Length - 1);
            ValidateCanonicalBase64(base64);
            var bundleBytes = Convert.FromBase64String(base64);
            if (!string.Equals(Convert.ToBase64String(bundleBytes), base64, StringComparison.Ordinal)
                || !GasCodeGenCandidateCompileGate.ByteArraysEqual(
                    selectorBytes,
                    Encoding.ASCII.GetBytes(SelectorMagic + base64 + "\n")))
            {
                throw new InvalidDataException("Selector bytes are not exact canonical ASCII Base64 text.");
            }
            return bundleBytes;
        }

        /// <summary>
        /// 验证 standard Base64 alphabet、长度与尾部 padding，不允许空白或 URL-safe 变体。
        /// </summary>
        private static void ValidateCanonicalBase64(string value)
        {
            if (value.Length == 0 || (value.Length & 3) != 0)
                throw new InvalidDataException("Selector Base64 length is not canonical.");
            var paddingStart = value.IndexOf('=');
            if (paddingStart >= 0 && value.Length - paddingStart > 2)
                throw new InvalidDataException("Selector Base64 padding is not canonical.");
            for (var index = 0; index < value.Length; index++)
            {
                var item = value[index];
                var data = item >= 'A' && item <= 'Z'
                           || item >= 'a' && item <= 'z'
                           || item >= '0' && item <= '9'
                           || item == '+' || item == '/';
                var padding = item == '=' && paddingStart >= 0 && index >= paddingStart;
                if (!data && !padding)
                    throw new InvalidDataException("Selector Base64 contains non-standard bytes.");
            }
        }

        /// <summary>
        /// 严格读取 bundle header、五项 source、inventory 与 exact EOF。
        /// </summary>
        private static void ValidateBundleBytes(byte[] bundleBytes, string selectorSha256)
        {
            using (var stream = new MemoryStream(bundleBytes, writable: false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                ValidateBundleHeaderPrefix(reader);
                for (var index = 0; index < 4; index++)
                    ReadRawHash(reader);
                var artifactManifestHash = ReadRawHash(reader);
                var declaredInventoryHash = ReadRawHash(reader);
                ValidateBundleHeaderSuffix(reader);
                ReadRawHash(reader);
                ReadRawHash(reader);
                if (reader.ReadByte() != 0)
                    throw new InvalidDataException("D1 FullSemanticEligibility must be zero.");
                var mappings = ReadSourceMappings(reader);
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Bundle contains trailing bytes after the final source entry.");
                var actualInventoryHash = ComputeSourceArtifactInventoryHash(mappings);
                if (!string.Equals(actualInventoryHash, declaredInventoryHash, StringComparison.Ordinal))
                    throw new InvalidDataException("SourceArtifactInventoryHash does not match source metadata.");
                EnsureNoSourceIdentityHash(mappings, actualInventoryHash);
                EnsureNoSourceAggregateHash(mappings, artifactManifestHash, selectorSha256);
            }
        }

        /// <summary>
        /// 验证 bundle version 与 encoding domain。
        /// </summary>
        private static void ValidateBundleHeaderPrefix(BinaryReader reader)
        {
            if (reader.ReadUInt32() != 1
                || !string.Equals(ReadStrictUtf8(reader, 64), SourceBundleDomain, StringComparison.Ordinal))
                throw new InvalidDataException("Bundle version or encoding domain is invalid.");
        }

        /// <summary>
        /// 验证 required-set ID 与冻结 contract hash。
        /// </summary>
        private static void ValidateBundleHeaderSuffix(BinaryReader reader)
        {
            EnsureRequiredContractHash();
            if (!string.Equals(ReadStrictUtf8(reader, 128), RequiredArtifactSetId, StringComparison.Ordinal)
                || !string.Equals(ReadRawHash(reader), RequiredArtifactSetContractHash, StringComparison.Ordinal))
                throw new InvalidDataException("Required artifact set contract is invalid.");
        }

        /// <summary>
        /// 读取严格有序的五项 source entry，并验证 route、长度、SHA 与总量上限。
        /// </summary>
        private static GasCodeGenSourceMapping[] ReadSourceMappings(BinaryReader reader)
        {
            var required = GetOrderedSourceContracts();
            var count = reader.ReadUInt32();
            if (count != (uint)required.Count)
                throw new InvalidDataException("Bundle must contain exactly five required source entries.");
            var mappings = new GasCodeGenSourceMapping[required.Count];
            long totalLength = 0;
            for (var index = 0; index < mappings.Length; index++)
            {
                var targetAssembly = ReadStrictUtf8(reader, 128);
                var hintName = ReadStrictUtf8(reader, 255);
                var category = reader.ReadByte();
                var sourceLength = reader.ReadUInt32();
                if (sourceLength < 1 || sourceLength > MaximumSourceByteLength)
                    throw new InvalidDataException("Source byte length is outside the frozen range.");
                totalLength += sourceLength;
                if (totalLength > MaximumTotalSourceByteLength)
                    throw new InvalidDataException("Total source byte length exceeds the frozen limit.");
                var declaredSha256 = ReadRawHash(reader);
                var sourceBytes = ReadExactBytes(reader, checked((int)sourceLength));
                mappings[index] = CreateValidatedSourceMapping(
                    required[index], targetAssembly, hintName, category, declaredSha256, sourceBytes);
            }
            return mappings;
        }

        /// <summary>
        /// 返回按 wire target/hint ordinal 排序的五项冻结 source contract。
        /// </summary>
        private static List<RequiredArtifactContract> GetOrderedSourceContracts()
        {
            var required = new List<RequiredArtifactContract>();
            for (var index = 0; index < s_requiredArtifacts.Length; index++)
            {
                if (s_requiredArtifacts[index].Category != 0)
                    required.Add(s_requiredArtifacts[index]);
            }
            required.Sort((left, right) =>
            {
                var targetOrder = string.CompareOrdinal(left.TargetAssembly, right.TargetAssembly);
                return targetOrder != 0 ? targetOrder : string.CompareOrdinal(left.HintName, right.HintName);
            });
            return required;
        }

        /// <summary>
        /// 建立一个已匹配 required route、source bytes 与声明 SHA 的不可变 mapping。
        /// </summary>
        private static GasCodeGenSourceMapping CreateValidatedSourceMapping(
            RequiredArtifactContract required,
            string targetAssembly,
            string hintName,
            byte category,
            string declaredSha256,
            byte[] sourceBytes)
        {
            if (!string.Equals(targetAssembly, required.TargetAssembly, StringComparison.Ordinal)
                || !string.Equals(hintName, required.HintName, StringComparison.Ordinal)
                || category != required.Category)
                throw new InvalidDataException("Bundle source route does not match the frozen required set.");
            ValidateSourceBytes(sourceBytes, required.Path);
            var actualSha256 = ComputeSha256(sourceBytes);
            if (!string.Equals(actualSha256, declaredSha256, StringComparison.Ordinal))
                throw new InvalidDataException("SourceSha256 does not match source bytes.");
            return new GasCodeGenSourceMapping(
                required.Path, targetAssembly, hintName, category,
                sourceBytes.Length, actualSha256, sourceBytes);
        }

        /// <summary>
        /// 读取带 uint32 byte length 的严格 UTF-8 字符串，并实施字段上限。
        /// </summary>
        private static string ReadStrictUtf8(BinaryReader reader, int maximumByteLength)
        {
            var length = reader.ReadUInt32();
            if (length > maximumByteLength)
                throw new InvalidDataException("Length-prefixed UTF-8 field exceeds its protocol bound.");
            try
            {
                return s_strictUtf8.GetString(ReadExactBytes(reader, checked((int)length)));
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("Length-prefixed field is not strict UTF-8.", exception);
            }
        }

        /// <summary>
        /// 读取一个 exact 32-byte digest 并转换为 canonical lowercase hex。
        /// </summary>
        private static string ReadRawHash(BinaryReader reader)
        {
            return BytesToLowerHex(ReadExactBytes(reader, 32));
        }

        /// <summary>
        /// 读取精确 byte 数；截断 payload 不允许由 BinaryReader 静默缩短。
        /// </summary>
        private static byte[] ReadExactBytes(BinaryReader reader, int count)
        {
            var bytes = reader.ReadBytes(count);
            if (bytes.Length != count)
                throw new EndOfStreamException("Bundle ended before the declared field was complete.");
            return bytes;
        }

        /// <summary>
        /// 将 raw digest 转换为 canonical lowercase hex。
        /// </summary>
        private static string BytesToLowerHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            for (var index = 0; index < bytes.Length; index++)
                builder.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        /// <summary>
        /// 对六项 artifact bytes 扫描 manifest/selector 聚合身份的 raw 与任意大小写 hex 表示。
        /// </summary>
        private static void EnsureNoArtifactAggregateHash(
            IReadOnlyList<ArtifactEntryData> artifacts,
            params string[] hashes)
        {
            for (var artifactIndex = 0; artifactIndex < artifacts.Count; artifactIndex++)
            {
                var artifact = artifacts[artifactIndex];
                for (var hashIndex = 0; hashIndex < hashes.Length; hashIndex++)
                    EnsureHashAbsent(artifact.Bytes, hashes[hashIndex], artifact.CanonicalPath);
            }
        }

        /// <summary>
        /// 对五项 C# source 继续扫描自身 SourceSha 与 source inventory 身份。
        /// </summary>
        private static void EnsureNoSourceIdentityHash(
            IReadOnlyList<GasCodeGenSourceMapping> mappings,
            string sourceInventoryHash)
        {
            for (var index = 0; index < mappings.Count; index++)
            {
                var mapping = mappings[index];
                EnsureHashAbsent(mapping.SourceBytes, mapping.SourceSha256, mapping.CanonicalPath);
                EnsureHashAbsent(mapping.SourceBytes, sourceInventoryHash, mapping.CanonicalPath);
            }
        }

        /// <summary>
        /// 供 strict reader 对 bundle 内五项 source 扫描 manifest/selector 聚合身份。
        /// </summary>
        private static void EnsureNoSourceAggregateHash(
            IReadOnlyList<GasCodeGenSourceMapping> mappings,
            params string[] hashes)
        {
            for (var mappingIndex = 0; mappingIndex < mappings.Count; mappingIndex++)
            {
                var mapping = mappings[mappingIndex];
                for (var hashIndex = 0; hashIndex < hashes.Length; hashIndex++)
                    EnsureHashAbsent(mapping.SourceBytes, hashes[hashIndex], mapping.CanonicalPath);
            }
        }

        /// <summary>
        /// 检查 artifact 中不出现指定 SHA 的 raw 或任意 ASCII 大小写 hex digest。
        /// </summary>
        private static void EnsureHashAbsent(byte[] artifactBytes, string hash, string canonicalPath)
        {
            var lowerHex = Encoding.ASCII.GetBytes(NormalizeSha256(hash, nameof(hash)));
            if (ContainsBytes(artifactBytes, HexToBytes(hash))
                || ContainsHexBytesIgnoreCase(artifactBytes, lowerHex))
                throw new InvalidDataException("Artifact embeds a forbidden identity hash: " + canonicalPath);
        }

        /// <summary>
        /// 在 byte array 中查找精确子序列。
        /// </summary>
        private static bool ContainsBytes(byte[] source, byte[] pattern)
        {
            if (pattern.Length == 0 || source.Length < pattern.Length)
                return false;
            for (var start = 0; start <= source.Length - pattern.Length; start++)
            {
                var matched = true;
                for (var index = 0; index < pattern.Length; index++)
                {
                    if (source[start + index] == pattern[index])
                        continue;
                    matched = false;
                    break;
                }
                if (matched)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 以 ASCII 大小写不敏感方式查找 64-character hex digest，包括 mixed-case 表示。
        /// </summary>
        private static bool ContainsHexBytesIgnoreCase(byte[] source, byte[] lowercaseHex)
        {
            if (source.Length < lowercaseHex.Length)
                return false;
            for (var start = 0; start <= source.Length - lowercaseHex.Length; start++)
            {
                var offset = 0;
                while (offset < lowercaseHex.Length
                       && ToLowerAscii(source[start + offset]) == lowercaseHex[offset])
                    offset++;
                if (offset == lowercaseHex.Length)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 仅将 ASCII A-F 规范为小写，避免 culture 参与 digest 比较。
        /// </summary>
        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'F'
                ? (byte)(value + ((byte)'a' - (byte)'A'))
                : value;
        }

        /// <summary>
        /// 按 v3 domain、path/kind/owner 与 artifact/meta 长度及哈希计算完整 manifest identity。
        /// </summary>
        private static string ComputeArtifactManifestHash(IReadOnlyList<ArtifactEntryData> artifacts)
        {
            var builder = new StringBuilder();
            AppendHashField(builder, ArtifactManifestHashDomain);
            for (var index = 0; index < artifacts.Count; index++)
            {
                var artifact = artifacts[index];
                AppendHashField(builder, artifact.CanonicalPath);
                AppendHashField(builder, artifact.GeneratedArtifactKind);
                AppendHashField(builder, artifact.GeneratedArtifactOwner);
                AppendHashField(builder, artifact.ByteLength.ToString(CultureInfo.InvariantCulture));
                AppendHashField(builder, artifact.ContentSha256);
                AppendHashField(builder, artifact.MetaByteLength.ToString(CultureInfo.InvariantCulture));
                AppendHashField(builder, artifact.MetaContentSha256);
            }
            return ComputeSha256(Encoding.UTF8.GetBytes(builder.ToString()));
        }

        /// <summary>
        /// 序列化 descriptor 内嵌的完整 candidate artifact manifest bytes。
        /// </summary>
        private static byte[] SerializeCandidateArtifactManifest(
            ArtifactEntryData[] artifacts,
            string artifactManifestHash)
        {
            var data = new CandidateArtifactManifestData
            {
                Domain = CandidateArtifactManifestDomain,
                ArtifactManifestHashDomain = ArtifactManifestHashDomain,
                ArtifactManifestHash = artifactManifestHash,
                ArtifactEntries = artifacts,
            };
            return SerializeJson(data);
        }

        /// <summary>
        /// 创建 descriptor v3 固定 JSON 模型，proof 未闭合字段保持空且 eligibility 固定 false。
        /// </summary>
        private static DescriptorData CreateDescriptorData(
            GasCodeGenSelectorCandidateSnapshot candidate,
            GasCodeGenCandidateCompilePlanSnapshot compilePlan)
        {
            return new DescriptorData
            {
                Version = DescriptorVersion,
                SemanticIdentityAlgorithm = GasCodeGenSemanticIdentity.Algorithm,
                ArtifactIdentityAlgorithm = ArtifactIdentityAlgorithm,
                CanonicalIdentityEncoding = CanonicalIdentityEncoding,
                ArtifactManifestHashDomain = ArtifactManifestHashDomain,
                RequiredArtifactSetId = RequiredArtifactSetId,
                RequiredArtifactSetContractHash = RequiredArtifactSetContractHash,
                SourceInputHash = candidate.SourceInputHash,
                SchemaHash = candidate.SchemaHash,
                ContentHash = candidate.ContentHash,
                LayoutHash = candidate.LayoutHash,
                ArtifactManifestHash = candidate.ArtifactManifestHash,
                CandidateArtifactManifestSha256 = candidate.CandidateArtifactManifestSha256,
                CandidateArtifactManifestBytesBase64 = Convert.ToBase64String(candidate.CandidateArtifactManifestBytes),
                SourceArtifactInventoryHash = candidate.SourceArtifactInventoryHash,
                SelectorSha256 = candidate.SelectorSha256,
                AnalyzerSha256 = candidate.AnalyzerSha256,
                RouteScaffoldSha256 = candidate.RouteScaffoldSha256,
                CandidateCompilePlanSha256 = compilePlan.Sha256,
                TypedContractHash = string.Empty,
                LayoutProofHash = string.Empty,
                CapacityProofHash = string.Empty,
                FullSemanticEligibility = false,
                ArtifactEntries = candidate.Artifacts,
                SourceMappings = candidate.SourceMappings,
            };
        }

        /// <summary>
        /// 重算 v2 required contract 并与冻结 hash 比较，阻断源码常量被局部修改。
        /// </summary>
        private static void EnsureRequiredContractHash()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(Encoding.ASCII.GetBytes(RequiredContractDomain));
                writer.Write((uint)s_requiredArtifacts.Length);
                for (var index = 0; index < s_requiredArtifacts.Length; index++)
                    s_requiredArtifacts[index].WriteContract(writer);
                writer.Flush();
                var actual = ComputeSha256(stream.ToArray());
                if (!string.Equals(actual, RequiredArtifactSetContractHash, StringComparison.Ordinal))
                    throw new InvalidDataException("Required artifact set contract hash drifted: " + actual);
            }
        }

        /// <summary>
        /// 将项目相对路径规范为正斜杠并拒绝绝对、空段和目录穿越。
        /// </summary>
        private static string CanonicalizeProjectRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || !string.Equals(path, path.Trim(), StringComparison.Ordinal))
                throw new InvalidDataException("Artifact canonical path is invalid: " + path);
            var canonical = path.Replace('\\', '/');
            if (Path.IsPathRooted(canonical) || canonical.IndexOf(':') >= 0)
                throw new InvalidDataException("Artifact path must be project-relative: " + path);
            var segments = canonical.Split('/');
            for (var index = 0; index < segments.Length; index++)
            {
                if (segments[index].Length == 0
                    || string.Equals(segments[index], ".", StringComparison.Ordinal)
                    || string.Equals(segments[index], "..", StringComparison.Ordinal))
                    throw new InvalidDataException("Artifact path is not canonical: " + path);
            }
            return canonical;
        }

        /// <summary>
        /// 要求职责文本非空且不存在隐式 trim 修正。
        /// </summary>
        private static string RequireCanonicalText(string value, string fieldName, string path)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
                throw new InvalidDataException(fieldName + " is invalid: " + path);
            return value;
        }

        /// <summary>
        /// 要求组件 manifest 使用当前 v3 artifact identity。
        /// </summary>
        private static void EnsureManifestVersion(GasCodeGenManifest manifest, string parameterName)
        {
            if (manifest.ManifestVersion != RequiredManifestVersion)
                throw new InvalidDataException(parameterName + " must use manifest version 3.");
        }

        /// <summary>
        /// 以固定 property order、UTF-8 no-BOM 与 LF 序列化 JSON bytes。
        /// </summary>
        private static byte[] SerializeJson(object data)
        {
            var json = JsonConvert.SerializeObject(data, Formatting.Indented)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n') + "\n";
            return new UTF8Encoding(false, true).GetBytes(json);
        }

        /// <summary>
        /// 使用 Create/WriteThrough/Flush(true) 写入候选控制文件并逐 byte 重读复核。
        /// </summary>
        private static void WriteBytesDurable(string path, byte[] bytes)
        {
            using (var stream = new FileStream(
                       path, FileMode.Create, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }
            if (!GasCodeGenCandidateCompileGate.ByteArraysEqual(File.ReadAllBytes(path), bytes))
                throw new IOException("Durable write verification failed: " + path);
        }

        /// <summary>
        /// 将一个字段写成 UTF-16 code-unit length 前缀的 v3 manifest 文本域。
        /// </summary>
        private static void AppendHashField(StringBuilder builder, string value)
        {
            var resolved = value ?? string.Empty;
            builder.Append(resolved.Length).Append(':').Append(resolved).Append('|');
        }

        /// <summary>
        /// 写入 uint32 UTF-8 byte-length 前缀字符串。
        /// </summary>
        internal static void WriteUtf8(BinaryWriter writer, string value)
        {
            var bytes = s_strictUtf8.GetBytes(value ?? string.Empty);
            writer.Write((uint)bytes.Length);
            writer.Write(bytes);
        }

        /// <summary>
        /// 将 SHA-256 hex 写成 32 个 raw bytes。
        /// </summary>
        private static void WriteRawHash(BinaryWriter writer, string hash)
        {
            writer.Write(HexToBytes(hash));
        }

        /// <summary>
        /// 规范化 64 位 SHA-256 hex 为小写表示。
        /// </summary>
        internal static string NormalizeSha256(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
                throw new InvalidDataException(fieldName + " must be a 64-character SHA-256 hex string.");
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                var valid = character >= '0' && character <= '9'
                            || character >= 'a' && character <= 'f'
                            || character >= 'A' && character <= 'F';
                if (!valid)
                    throw new InvalidDataException(fieldName + " contains a non-hex character.");
            }
            return value.ToLowerInvariant();
        }

        /// <summary>
        /// 将 64 位 SHA-256 hex 转为 32 个 raw bytes。
        /// </summary>
        internal static byte[] HexToBytes(string value)
        {
            var canonical = NormalizeSha256(value, nameof(value));
            var bytes = new byte[32];
            for (var index = 0; index < bytes.Length; index++)
                bytes[index] = byte.Parse(canonical.Substring(index * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return bytes;
        }

        /// <summary>
        /// 计算 bytes 的小写十六进制 SHA-256。
        /// </summary>
        internal static string ComputeSha256(byte[] bytes)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(bytes);
                var builder = new StringBuilder(hash.Length * 2);
                for (var index = 0; index < hash.Length; index++)
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        /// <summary>
        /// 定义 descriptor v3 的固定字段顺序与完整 route/source binding。
        /// </summary>
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class DescriptorData
        {
            [JsonProperty(Order = 1)] public int Version { get; set; }
            [JsonProperty(Order = 2)] public string SemanticIdentityAlgorithm { get; set; }
            [JsonProperty(Order = 3)] public string ArtifactIdentityAlgorithm { get; set; }
            [JsonProperty(Order = 4)] public string CanonicalIdentityEncoding { get; set; }
            [JsonProperty(Order = 5)] public string ArtifactManifestHashDomain { get; set; }
            [JsonProperty(Order = 6)] public string RequiredArtifactSetId { get; set; }
            [JsonProperty(Order = 7)] public string RequiredArtifactSetContractHash { get; set; }
            [JsonProperty(Order = 8)] public string SourceInputHash { get; set; }
            [JsonProperty(Order = 9)] public string SchemaHash { get; set; }
            [JsonProperty(Order = 10)] public string ContentHash { get; set; }
            [JsonProperty(Order = 11)] public string LayoutHash { get; set; }
            [JsonProperty(Order = 12)] public string ArtifactManifestHash { get; set; }
            [JsonProperty(Order = 13)] public string CandidateArtifactManifestSha256 { get; set; }
            [JsonProperty(Order = 14)] public string CandidateArtifactManifestBytesBase64 { get; set; }
            [JsonProperty(Order = 15)] public string SourceArtifactInventoryHash { get; set; }
            [JsonProperty(Order = 16)] public string SelectorSha256 { get; set; }
            [JsonProperty(Order = 17)] public string AnalyzerSha256 { get; set; }
            [JsonProperty(Order = 18)] public string RouteScaffoldSha256 { get; set; }
            [JsonProperty(Order = 19)] public string CandidateCompilePlanSha256 { get; set; }
            [JsonProperty(Order = 20)] public string TypedContractHash { get; set; }
            [JsonProperty(Order = 21)] public string LayoutProofHash { get; set; }
            [JsonProperty(Order = 22)] public string CapacityProofHash { get; set; }
            [JsonProperty(Order = 23)] public bool FullSemanticEligibility { get; set; }
            [JsonProperty(Order = 24)] public ArtifactEntryData[] ArtifactEntries { get; set; }
            [JsonProperty(Order = 25)] public IReadOnlyList<GasCodeGenSourceMapping> SourceMappings { get; set; }
        }

        /// <summary>
        /// 定义 descriptor 内嵌的完整 candidate artifact manifest bytes。
        /// </summary>
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class CandidateArtifactManifestData
        {
            [JsonProperty(Order = 1)] public string Domain { get; set; }
            [JsonProperty(Order = 2)] public string ArtifactManifestHashDomain { get; set; }
            [JsonProperty(Order = 3)] public string ArtifactManifestHash { get; set; }
            [JsonProperty(Order = 4)] public ArtifactEntryData[] ArtifactEntries { get; set; }
        }

        /// <summary>
        /// 描述完整 manifest 中一个 artifact 与 managed meta 的精确身份。
        /// </summary>
        [JsonObject(MemberSerialization.OptIn)]
        internal sealed class ArtifactEntryData
        {
            [JsonProperty(Order = 1)] public string CanonicalPath { get; set; }
            [JsonProperty(Order = 2)] public string GeneratedArtifactKind { get; set; }
            [JsonProperty(Order = 3)] public string GeneratedArtifactOwner { get; set; }
            [JsonProperty(Order = 4)] public bool RuntimeVisible { get; set; }
            [JsonProperty(Order = 5)] public long ByteLength { get; set; }
            [JsonProperty(Order = 6)] public string ContentSha256 { get; set; }
            [JsonProperty(Order = 7)] public long MetaByteLength { get; set; }
            [JsonProperty(Order = 8)] public string MetaContentSha256 { get; set; }
            [JsonProperty(Order = 9)] public string SourceComponent { get; set; }
            [JsonIgnore] public string PhysicalPath { get; set; }
            [JsonIgnore] public byte[] Bytes { get; set; }
        }

        /// <summary>
        /// 冻结一个 required artifact 的职责、source route 与 managed-meta 要求。
        /// </summary>
        private sealed class RequiredArtifactContract
        {
            /// <summary>
            /// 创建一项 exact v2 required contract。
            /// </summary>
            internal RequiredArtifactContract(
                string path,
                string kind,
                string owner,
                bool runtimeVisible,
                string component,
                string targetAssembly,
                string hintName,
                byte category)
            {
                Path = path;
                Kind = kind;
                Owner = owner;
                RuntimeVisible = runtimeVisible;
                Component = component;
                TargetAssembly = targetAssembly;
                HintName = hintName;
                Category = category;
            }

            internal string Path { get; }
            internal string Kind { get; }
            internal string Owner { get; }
            internal bool RuntimeVisible { get; }
            internal string Component { get; }
            internal string TargetAssembly { get; }
            internal string HintName { get; }
            internal byte Category { get; }

            /// <summary>
            /// 校验实际 artifact 与 required contract 的所有职责字段和 mandatory meta。
            /// </summary>
            internal void EnsureMatches(ArtifactEntryData actual)
            {
                if (!string.Equals(actual.CanonicalPath, Path, StringComparison.Ordinal)
                    || !string.Equals(actual.GeneratedArtifactKind, Kind, StringComparison.Ordinal)
                    || !string.Equals(actual.GeneratedArtifactOwner, Owner, StringComparison.Ordinal)
                    || actual.RuntimeVisible != RuntimeVisible
                    || !string.Equals(actual.SourceComponent, Component, StringComparison.Ordinal)
                    || actual.MetaByteLength <= 0
                    || string.IsNullOrEmpty(actual.MetaContentSha256))
                {
                    throw new InvalidDataException("Required artifact contract mismatch: " + Path);
                }
            }

            /// <summary>
            /// 按冻结字段顺序写入 required contract hash 输入。
            /// </summary>
            internal void WriteContract(BinaryWriter writer)
            {
                WriteUtf8(writer, Path);
                WriteUtf8(writer, Kind);
                WriteUtf8(writer, Owner);
                writer.Write(RuntimeVisible ? (byte)1 : (byte)0);
                WriteUtf8(writer, Component);
                WriteUtf8(writer, TargetAssembly);
                WriteUtf8(writer, HintName);
                writer.Write(Category);
                writer.Write((byte)1);
            }
        }
    }

    /// <summary>
    /// 描述 required C# artifact 到唯一 bundle entry 的双向映射与 exact source bytes。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class GasCodeGenSourceMapping
    {
        /// <summary>
        /// 创建不可变 source mapping；source bytes 仅供 bundle writer 使用，不进入 descriptor JSON。
        /// </summary>
        internal GasCodeGenSourceMapping(
            string canonicalPath,
            string targetAssembly,
            string hintName,
            byte category,
            int sourceByteLength,
            string sourceSha256,
            byte[] sourceBytes)
        {
            CanonicalPath = canonicalPath;
            TargetAssembly = targetAssembly;
            HintName = hintName;
            Category = category;
            SourceByteLength = sourceByteLength;
            SourceSha256 = sourceSha256;
            SourceBytes = (byte[])sourceBytes.Clone();
        }

        [JsonProperty(Order = 1)] public string CanonicalPath { get; }
        [JsonProperty(Order = 2)] public string TargetAssembly { get; }
        [JsonProperty(Order = 3)] public string HintName { get; }
        [JsonProperty(Order = 4)] public byte Category { get; }
        [JsonProperty(Order = 5)] public int SourceByteLength { get; }
        [JsonProperty(Order = 6)] public string SourceSha256 { get; }
        [JsonIgnore] internal byte[] SourceBytes { get; }
    }

    /// <summary>
    /// 保存已封包 selector、完整 artifact manifest 与五项 source mapping 的不可变候选快照。
    /// </summary>
    internal sealed class GasCodeGenSelectorCandidateSnapshot
    {
        private readonly GasCodeGenCandidateWorkspace _workspace;
        private readonly GasCodeGenCandidateWorkspace.CandidateAuthority _authority;

        /// <summary>
        /// 冻结 selector candidate 的全部身份与 source/artifact byte 引用。
        /// </summary>
        internal GasCodeGenSelectorCandidateSnapshot(
            GasCodeGenCandidateWorkspace workspace,
            GasCodeGenCandidateWorkspace.CandidateAuthority authority,
            string selectorPath,
            byte[] selectorBytes,
            string selectorSha256,
            byte[] bundleBytes,
            string sourceInputHash,
            string schemaHash,
            string contentHash,
            string layoutHash,
            string artifactManifestHash,
            byte[] candidateArtifactManifestBytes,
            string candidateArtifactManifestSha256,
            string sourceArtifactInventoryHash,
            string analyzerSha256,
            string routeScaffoldSha256,
            GasCodeGenPackageDescriptor.ArtifactEntryData[] artifacts,
            GasCodeGenSourceMapping[] sourceMappings)
        {
            _workspace = workspace;
            _authority = authority;
            SelectorPath = selectorPath;
            SelectorBytes = (byte[])selectorBytes.Clone();
            SelectorSha256 = selectorSha256;
            BundleBytes = (byte[])bundleBytes.Clone();
            SourceInputHash = sourceInputHash;
            SchemaHash = schemaHash;
            ContentHash = contentHash;
            LayoutHash = layoutHash;
            ArtifactManifestHash = artifactManifestHash;
            CandidateArtifactManifestBytes = (byte[])candidateArtifactManifestBytes.Clone();
            CandidateArtifactManifestSha256 = candidateArtifactManifestSha256;
            SourceArtifactInventoryHash = sourceArtifactInventoryHash;
            AnalyzerSha256 = analyzerSha256;
            RouteScaffoldSha256 = routeScaffoldSha256;
            Artifacts = artifacts;
            SourceMappings = sourceMappings;
            var artifactFiles = new List<string>(artifacts.Length);
            for (var index = 0; index < artifacts.Length; index++)
                artifactFiles.Add(artifacts[index].PhysicalPath);
            ArtifactFiles = artifactFiles;
        }

        internal string SelectorPath { get; }
        internal byte[] SelectorBytes { get; }
        internal string SelectorSha256 { get; }
        internal byte[] BundleBytes { get; }
        internal string SourceInputHash { get; }
        internal string SchemaHash { get; }
        internal string ContentHash { get; }
        internal string LayoutHash { get; }
        internal string ArtifactManifestHash { get; }
        internal byte[] CandidateArtifactManifestBytes { get; }
        internal string CandidateArtifactManifestSha256 { get; }
        internal string SourceArtifactInventoryHash { get; }
        internal string AnalyzerSha256 { get; }
        internal string RouteScaffoldSha256 { get; }
        internal GasCodeGenPackageDescriptor.ArtifactEntryData[] Artifacts { get; }
        internal IReadOnlyList<GasCodeGenSourceMapping> SourceMappings { get; }
        internal IReadOnlyList<string> ArtifactFiles { get; }

        /// <summary>
        /// 验证 candidate 仍由当前 workspace authority 持有。
        /// </summary>
        internal void EnsureOwnedBy(
            GasCodeGenCandidateWorkspace workspace,
            GasCodeGenCandidateWorkspace.CandidateAuthority authority)
        {
            if (!ReferenceEquals(_workspace, workspace) || !ReferenceEquals(_authority, authority))
                throw new InvalidOperationException("Selector candidate authority mismatch.");
            workspace.EnsureAuthority(authority);
        }

        /// <summary>
        /// 重读 selector、六个 artifact 与 managed meta，阻断 seal 后的 TOCTOU 漂移。
        /// </summary>
        internal void EnsureUnchanged()
        {
            if (!GasCodeGenCandidateCompileGate.ByteArraysEqual(File.ReadAllBytes(SelectorPath), SelectorBytes))
                throw new InvalidDataException("Selector candidate changed after it was sealed.");
            for (var index = 0; index < Artifacts.Length; index++)
            {
                var artifact = Artifacts[index];
                var bytes = File.ReadAllBytes(artifact.PhysicalPath);
                var metaBytes = File.ReadAllBytes(artifact.PhysicalPath + ".meta");
                if (bytes.LongLength != artifact.ByteLength
                    || !string.Equals(GasCodeGenPackageDescriptor.ComputeSha256(bytes), artifact.ContentSha256, StringComparison.Ordinal)
                    || metaBytes.LongLength != artifact.MetaByteLength
                    || !string.Equals(GasCodeGenPackageDescriptor.ComputeSha256(metaBytes), artifact.MetaContentSha256, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Candidate artifact changed after seal: " + artifact.CanonicalPath);
                }
            }
        }
    }

    /// <summary>
    /// 保存 descriptor/envelope/selector/route/compile-plan 的单次 byte snapshot，作为 Store 唯一 Stage token。
    /// </summary>
    internal sealed class GasCodeGenPackageDescriptorSnapshot
    {
        private readonly GasCodeGenCandidateWorkspace _workspace;
        private readonly GasCodeGenCandidateWorkspace.CandidateAuthority _authority;

        /// <summary>
        /// 创建已完成 descriptor v3 与 envelope v2 封存的 package snapshot。
        /// </summary>
        internal GasCodeGenPackageDescriptorSnapshot(
            GasCodeGenCandidateWorkspace workspace,
            GasCodeGenCandidateWorkspace.CandidateAuthority authority,
            GasCodeGenSelectorCandidateSnapshot candidate,
            GasCodeGenCandidateCompilePlanSnapshot compilePlan,
            GasCodeGenRouteSnapshot route,
            string descriptorPath,
            byte[] descriptorBytes,
            string descriptorSha256,
            string envelopePath,
            byte[] envelopeBytes,
            string installEnvelopeSha256)
        {
            _workspace = workspace;
            _authority = authority;
            Candidate = candidate;
            CompilePlan = compilePlan;
            Route = route;
            DescriptorPath = descriptorPath;
            DescriptorBytes = (byte[])descriptorBytes.Clone();
            DescriptorSha256 = descriptorSha256;
            InstallEnvelopePath = envelopePath;
            InstallEnvelopeBytes = (byte[])envelopeBytes.Clone();
            InstallEnvelopeSha256 = installEnvelopeSha256;
        }

        internal GasCodeGenSelectorCandidateSnapshot Candidate { get; }
        internal GasCodeGenCandidateCompilePlanSnapshot CompilePlan { get; }
        internal GasCodeGenRouteSnapshot Route { get; }
        internal string DescriptorPath { get; }
        internal byte[] DescriptorBytes { get; }
        internal string DescriptorSha256 { get; }
        internal string InstallEnvelopePath { get; }
        internal byte[] InstallEnvelopeBytes { get; }
        internal string InstallEnvelopeSha256 { get; }
        internal string SchemaHash => Candidate.SchemaHash;
        internal string ContentHash => Candidate.ContentHash;
        internal string LayoutHash => Candidate.LayoutHash;
        internal string ArtifactManifestHash => Candidate.ArtifactManifestHash;

        /// <summary>
        /// 复核所有 package snapshot bytes 与 authority，阻断 Stage 前 TOCTOU 漂移。
        /// </summary>
        internal void EnsureUnchanged()
        {
            _workspace.EnsureAuthority(_authority);
            Candidate.EnsureOwnedBy(_workspace, _authority);
            Candidate.EnsureUnchanged();
            CompilePlan.EnsureUnchanged();
            Route.EnsureUnchanged();
            if (!GasCodeGenCandidateCompileGate.ByteArraysEqual(File.ReadAllBytes(DescriptorPath), DescriptorBytes)
                || !GasCodeGenCandidateCompileGate.ByteArraysEqual(File.ReadAllBytes(InstallEnvelopePath), InstallEnvelopeBytes))
            {
                throw new InvalidDataException("Descriptor or install envelope changed after seal.");
            }
        }
    }
}
