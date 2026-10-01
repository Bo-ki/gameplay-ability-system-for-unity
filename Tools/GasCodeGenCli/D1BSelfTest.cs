using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using GAS.Editor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GasCodeGenCliHost
{
    /// <summary>
    /// 在纯 .NET 进程中验证 D1-B descriptor/mapping/route snapshot 与 selector 六态事务。
    /// </summary>
    internal static class D1BSelfTest
    {
        private static readonly UTF8Encoding s_utf8 = new UTF8Encoding(false, true);
        private static readonly string[] s_artifactPaths =
        {
            "Assets/AutoChessDemo/Generated/AutoChessGeneratedConfig.gen.cs",
            "Assets/GAS/Generated/CodeGen/Editor/LubanNormalizedRows.gen.cs",
            "Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md",
            "Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs",
            "Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs",
            "Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs",
        };
        private static readonly string[] s_scaffoldPaths =
        {
            "Assets/AutoChessDemo/Generated/GasCodeGenSourceGeneratorRequired.cs",
            "Assets/AutoChessDemo/Generated/GasCodeGenSourceGeneratorRequired.cs.meta",
            "Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef",
            "Assets/AutoChessDemo/com.exhard.exgas.autochessdemo.asmdef.meta",
            "Assets/GAS/CodeGen/Analyzers/GasCodeGenSourceGenerator.dll.meta",
            "Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile.meta",
            "Assets/GAS/Generated/CodeGen/Editor/GasCodeGenSourceGeneratorRequired.cs",
            "Assets/GAS/Generated/CodeGen/Editor/GasCodeGenSourceGeneratorRequired.cs.meta",
            "Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef",
            "Assets/GAS/Generated/CodeGen/Editor/com.exhard.exgas.generated.editor.asmdef.meta",
            "Assets/GAS/Generated/CodeGen/Runtime/GasCodeGenSourceGeneratorRequired.cs",
            "Assets/GAS/Generated/CodeGen/Runtime/GasCodeGenSourceGeneratorRequired.cs.meta",
            "Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef",
            "Assets/GAS/Generated/CodeGen/Runtime/com.exhard.exgas.generated.runtime.asmdef.meta",
        };
        private static readonly string[] s_scenarioRequestProperties =
        {
            "Schema", "RunId", "CaseId", "InvocationId", "Scenario", "ProjectRoot",
            "SelectorRelativePath", "IntentRelativePath", "AuditRelativePath",
            "InitialSelector", "Target", "SelectorABytesBase64", "SelectorBBytesBase64",
            "CompetitionSelectorBytesBase64", "FaultPoint", "SeedState",
            "CompetitionTarget", "RecoveryCount",
        };

        /// <summary>
        /// 执行必要的 descriptor/meta/mapping/route 与六态 transaction 非 Unity 测试。
        /// </summary>
        internal static int RunContractSuite()
        {
            var suiteRoot = Path.Combine(
                Path.GetTempPath(),
                "gcd1b-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(suiteRoot);
            var passed = 0;
            try
            {
                using (var baseline = BuildPackage(Path.Combine(suiteRoot, "baseline"), "baseline"))
                {
                    Assert(baseline.Candidate.SourceMappings.Count == 5, "baseline mapping count");
                    passed++;
                    ValidateWithProductionGeneratorCodec(baseline.Candidate.SelectorBytes);
                    passed++;
                    passed += RunMappingNegativeTests(baseline);
                    passed += RunValidationReportSelfHashNegativeTests(baseline);
                }
                passed += RunMetaNegativeTests(suiteRoot);
                passed += RunRequiredSetNegativeTests(suiteRoot);
                passed += RunRouteSnapshotNegativeTest(suiteRoot);
                passed += RunRouteHardLinkNegativeTests(suiteRoot);
                passed += RunCorruptBundleSelectorNegativeTest(suiteRoot);
                passed += RunIntentJsonRecoveryNegativeTests(suiteRoot);
                passed += RunSelectorDirectoryRecoveryNegativeTest(suiteRoot);
                passed += RunTransactionTests(suiteRoot);
                Console.WriteLine("D1B contract tests passed. Passed=" + passed.ToString(CultureInfo.InvariantCulture));
                return 0;
            }
            finally
            {
                DeleteOwnedSuiteRoot(suiteRoot);
            }
        }

        /// <summary>
        /// 执行 E1 单 case request，并输出可由外部 oracle 独立复核的 transaction response JSON。
        /// </summary>
        internal static int RunScenario(string projectRoot, string requestPath, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(requestPath) || !File.Exists(requestPath))
                throw new FileNotFoundException("D1-B scenario request is missing.", requestPath);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("D1-B scenario output path is required.", nameof(outputPath));
            var requestText = File.ReadAllText(requestPath, s_utf8);
            ValidateScenarioRequestProperties(JObject.Parse(
                requestText,
                new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                }));
            var request = JsonConvert.DeserializeObject<ScenarioRequest>(requestText);
            if (request == null)
                throw new InvalidDataException("D1-B scenario request is empty.");
            ValidateScenarioRequest(request);
            var response = ExecuteScenario(Path.GetFullPath(projectRoot), request);
            var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);
            var json = JsonConvert.SerializeObject(response, Formatting.Indented)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n') + "\n";
            WriteFreshScenarioResponse(outputPath, s_utf8.GetBytes(json));
            return response.AdapterExitCode;
        }

        /// <summary>
        /// 以 CreateNew/WriteThrough/Flush(true) 持久化一次 fresh response，禁止覆盖既有 evidence。
        /// </summary>
        private static void WriteFreshScenarioResponse(string path, byte[] bytes)
        {
            using (var stream = new FileStream(
                       path,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.Read,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (!ByteArraysEqual(File.ReadAllBytes(path), bytes))
                throw new IOException("D1 E1 response durable write verification failed: " + path);
        }

        /// <summary>
        /// 从现有 exact route 用 production package writer 导出两个完整且不同的 E1 selector fixtures。
        /// </summary>
        internal static int ExportScenarioSelectors(
            string projectRoot,
            string selectorAOutput,
            string selectorBOutput)
        {
            if (string.IsNullOrWhiteSpace(selectorAOutput)
                || string.IsNullOrWhiteSpace(selectorBOutput))
                throw new ArgumentException("Both selector fixture output paths are required.");
            var selectorAPath = Path.GetFullPath(selectorAOutput);
            var selectorBPath = Path.GetFullPath(selectorBOutput);
            if (string.Equals(selectorAPath, selectorBPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Selector A/B output paths must be different.");
            byte[] selectorA;
            byte[] selectorB;
            using (var package = BuildPackage(projectRoot, "A", prepareRoute: false))
                selectorA = (byte[])package.Candidate.SelectorBytes.Clone();
            using (var package = BuildPackage(projectRoot, "B", prepareRoute: false))
                selectorB = (byte[])package.Candidate.SelectorBytes.Clone();
            if (ByteArraysEqual(selectorA, selectorB))
                throw new InvalidDataException("Production writer emitted identical selector A/B bytes.");
            ValidateWithProductionGeneratorCodec(selectorA);
            ValidateWithProductionGeneratorCodec(selectorB);
            WriteFreshSelectorFixture(selectorAPath, selectorA);
            WriteFreshSelectorFixture(selectorBPath, selectorB);
            Console.WriteLine(
                "D1B selector fixtures exported. A=" + HashBytes(selectorA)
                + ", B=" + HashBytes(selectorB));
            return 0;
        }

        /// <summary>
        /// 以 CreateNew/WriteThrough/Flush(true) 写一个 fresh selector fixture 并重读验证。
        /// </summary>
        private static void WriteFreshSelectorFixture(string path, byte[] bytes)
        {
            var parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent))
                throw new InvalidOperationException("Selector fixture output parent is missing.");
            Directory.CreateDirectory(parent);
            using (var stream = new FileStream(
                       path,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.Read,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (!ByteArraysEqual(File.ReadAllBytes(path), bytes))
                throw new IOException("Selector fixture durable write verification failed: " + path);
        }

        /// <summary>
        /// 对 missing/extra/target/hint/category/length/hash 七类 mapping 漂移逐一断言拒绝。
        /// </summary>
        private static int RunMappingNegativeTests(BuiltPackage package)
        {
            var baseline = package.Candidate.SourceMappings.ToArray();
            var passed = 0;
            ExpectFailure(() => GasCodeGenPackageDescriptor.ValidateSourceBijectionForTests(
                package.Candidate, baseline.Take(4).ToArray()));
            passed++;
            var extra = baseline.Concat(new[] { baseline[0] }).ToArray();
            ExpectFailure(() => GasCodeGenPackageDescriptor.ValidateSourceBijectionForTests(package.Candidate, extra));
            passed++;
            passed += ExpectMappingMutation(package, baseline, 0, targetAssembly: baseline[0].TargetAssembly + ".drift");
            passed += ExpectMappingMutation(package, baseline, 0, hintName: "Drift.gen.cs");
            passed += ExpectMappingMutation(package, baseline, 0, category: (byte)(baseline[0].Category == 1 ? 2 : 1));
            passed += ExpectMappingMutation(package, baseline, 0, length: baseline[0].SourceByteLength + 1);
            passed += ExpectMappingMutation(package, baseline, 0, sha256: new string('0', 64));
            return passed;
        }

        /// <summary>
        /// 在 validation report 分别嵌入 manifest/selector 的 raw 与 mixed-case hex，要求六项扫描全部拒绝。
        /// </summary>
        private static int RunValidationReportSelfHashNegativeTests(BuiltPackage package)
        {
            var probes = new[]
            {
                CreateForbiddenHashProbe(package.Candidate.ArtifactManifestHash, raw: true),
                CreateForbiddenHashProbe(package.Candidate.ArtifactManifestHash, raw: false),
                CreateForbiddenHashProbe(package.Candidate.SelectorSha256, raw: true),
                CreateForbiddenHashProbe(package.Candidate.SelectorSha256, raw: false),
            };
            var passed = 0;
            for (var index = 0; index < probes.Length; index++)
            {
                var artifacts = CloneArtifactsWithValidationReport(package.Candidate.Artifacts, probes[index]);
                ExpectFailure(() => GasCodeGenPackageDescriptor.ValidateArtifactAggregateHashesForTests(
                    artifacts,
                    package.Candidate.ArtifactManifestHash,
                    package.Candidate.SelectorSha256));
                passed++;
            }
            return passed;
        }

        /// <summary>
        /// 复制六项 artifact，仅替换 validation report bytes，避免测试篡改 baseline candidate 快照。
        /// </summary>
        private static GasCodeGenPackageDescriptor.ArtifactEntryData[] CloneArtifactsWithValidationReport(
            IReadOnlyList<GasCodeGenPackageDescriptor.ArtifactEntryData> artifacts,
            byte[] validationReportBytes)
        {
            var clones = new GasCodeGenPackageDescriptor.ArtifactEntryData[artifacts.Count];
            for (var index = 0; index < artifacts.Count; index++)
            {
                var source = artifacts[index];
                clones[index] = new GasCodeGenPackageDescriptor.ArtifactEntryData
                {
                    CanonicalPath = source.CanonicalPath,
                    GeneratedArtifactKind = source.GeneratedArtifactKind,
                    GeneratedArtifactOwner = source.GeneratedArtifactOwner,
                    RuntimeVisible = source.RuntimeVisible,
                    ByteLength = source.ByteLength,
                    ContentSha256 = source.ContentSha256,
                    MetaByteLength = source.MetaByteLength,
                    MetaContentSha256 = source.MetaContentSha256,
                    SourceComponent = source.SourceComponent,
                    PhysicalPath = source.PhysicalPath,
                    Bytes = string.Equals(source.CanonicalPath, s_artifactPaths[2], StringComparison.Ordinal)
                        ? validationReportBytes
                        : source.Bytes,
                };
            }
            return clones;
        }

        /// <summary>
        /// 构造 raw digest 或交错大小写 hex probe，覆盖两种冻结禁止表示。
        /// </summary>
        private static byte[] CreateForbiddenHashProbe(string hash, bool raw)
        {
            if (raw)
                return GasCodeGenPackageDescriptor.HexToBytes(hash);
            var characters = hash.ToCharArray();
            var uppercase = true;
            for (var index = 0; index < characters.Length; index++)
            {
                if (characters[index] < 'a' || characters[index] > 'f')
                    continue;
                characters[index] = uppercase
                    ? char.ToUpperInvariant(characters[index])
                    : characters[index];
                uppercase = !uppercase;
            }
            return Encoding.ASCII.GetBytes(new string(characters));
        }

        /// <summary>
        /// 通过反射调用 D1-A production decoder，证明 Editor 独立 writer 与 generator reader byte-compatible。
        /// </summary>
        private static void ValidateWithProductionGeneratorCodec(byte[] selectorBytes)
        {
            var assemblyPath = Path.GetFullPath(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Tools/GasCodeGenSourceGenerator/bin/Release/netstandard2.0/GasCodeGenSourceGenerator.dll"));
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException("Production generator DLL is missing for cross-codec validation.", assemblyPath);
            var assembly = Assembly.LoadFrom(assemblyPath);
            var codecType = assembly.GetType("Gas.CodeGen.SourceGenerator.GasSourceBundleCodec", throwOnError: true);
            var decode = codecType.GetMethod(
                "DecodeSelector",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(byte[]) },
                modifiers: null);
            if (decode == null)
                throw new MissingMethodException(codecType.FullName, "DecodeSelector(byte[])");
            try
            {
                decode.Invoke(null, new object[] { selectorBytes });
            }
            catch (TargetInvocationException exception)
            {
                throw new InvalidDataException(
                    "Production generator rejected the D1-B selector writer output.",
                    exception.InnerException ?? exception);
            }
        }

        /// <summary>
        /// 复制一个 mapping 并替换单字段，要求 production bijection validator 显式失败。
        /// </summary>
        private static int ExpectMappingMutation(
            BuiltPackage package,
            GasCodeGenSourceMapping[] baseline,
            int index,
            string targetAssembly = null,
            string hintName = null,
            byte? category = null,
            int? length = null,
            string sha256 = null)
        {
            var mutated = (GasCodeGenSourceMapping[])baseline.Clone();
            var original = baseline[index];
            mutated[index] = new GasCodeGenSourceMapping(
                original.CanonicalPath,
                targetAssembly ?? original.TargetAssembly,
                hintName ?? original.HintName,
                category ?? original.Category,
                length ?? original.SourceByteLength,
                sha256 ?? original.SourceSha256,
                original.SourceBytes);
            ExpectFailure(() => GasCodeGenPackageDescriptor.ValidateSourceBijectionForTests(package.Candidate, mutated));
            return 1;
        }

        /// <summary>
        /// 对六个 required managed meta 分别覆盖 missing、length drift 与 same-length SHA drift。
        /// </summary>
        private static int RunMetaNegativeTests(string suiteRoot)
        {
            var passed = 0;
            for (var artifactIndex = 0; artifactIndex < s_artifactPaths.Length; artifactIndex++)
            {
                foreach (var mutation in new[] { MetaMutation.Missing, MetaMutation.Length, MetaMutation.Sha })
                {
                    var caseRoot = Path.Combine(
                        suiteRoot,
                        "meta-" + artifactIndex.ToString(CultureInfo.InvariantCulture) + "-" + mutation);
                    ExpectFailure(() =>
                    {
                        using (BuildPackage(caseRoot, "meta", mutation, artifactIndex))
                        {
                        }
                    });
                    passed++;
                }
            }
            return passed;
        }

        /// <summary>
        /// 覆盖 required-set 缩窄和扩张，确保完整 manifest 不能偷偷改变六项合同。
        /// </summary>
        private static int RunRequiredSetNegativeTests(string suiteRoot)
        {
            ExpectFailure(() =>
            {
                using (BuildPackage(Path.Combine(suiteRoot, "required-shrink"), "shrink", setMutation: -1))
                {
                }
            });
            ExpectFailure(() =>
            {
                using (BuildPackage(Path.Combine(suiteRoot, "required-expand"), "expand", setMutation: 1))
                {
                }
            });
            return 2;
        }

        /// <summary>
        /// 捕获 route 后修改一个 scaffold byte，要求所有后续 seal 都拒绝 snapshot drift。
        /// </summary>
        private static int RunRouteSnapshotNegativeTest(string suiteRoot)
        {
            var root = Path.Combine(suiteRoot, "route-drift");
            using (var package = BuildPackage(root, "route"))
            {
                var path = Resolve(root, s_scaffoldPaths[0]);
                File.AppendAllText(path, "drift", s_utf8);
                ExpectFailure(package.Route.EnsureUnchanged);
            }
            return 1;
        }

        /// <summary>
        /// 在 Windows 对 analyzer 与 14 scaffold 逐项建立 hardlink，要求 route capture 全部拒绝 link count 非一。
        /// </summary>
        private static int RunRouteHardLinkNegativeTests(string suiteRoot)
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Console.WriteLine("D1B route hardlink negatives skipped on non-Windows; residual P2 recorded.");
                return 0;
            }
            var routePaths = new List<string> { GasCodeGenCandidateCompileGate.AnalyzerRelativePath };
            routePaths.AddRange(s_scaffoldPaths);
            var passed = 0;
            for (var index = 0; index < routePaths.Count; index++)
            {
                var root = Path.Combine(suiteRoot, "route-hardlink-" + index.ToString(CultureInfo.InvariantCulture));
                PrepareRoute(root);
                var sourcePath = Resolve(root, routePaths[index]);
                var linkPath = Path.Combine(root, "hardlink-probe-" + index.ToString(CultureInfo.InvariantCulture));
                CreateWindowsHardLink(linkPath, sourcePath);
                ExpectFailure(() =>
                {
                    using (BuildPackage(root, "hardlink", prepareRoute: false))
                    {
                    }
                });
                passed++;
            }
            return passed;
        }

        /// <summary>
        /// 建立 Windows hardlink；系统拒绝时输出原始 Win32 error，避免把未执行误记为通过。
        /// </summary>
        private static void CreateWindowsHardLink(string linkPath, string sourcePath)
        {
            if (!CreateHardLink(linkPath, sourcePath, IntPtr.Zero))
            {
                throw new IOException(
                    "Cannot create route hardlink fixture. Win32Error="
                    + Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>
        /// 调用 Win32 CreateHardLinkW 构造 route link-count 负例。
        /// </summary>
        [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateHardLink(
            string fileName,
            string existingFileName,
            IntPtr securityAttributes);

        /// <summary>
        /// 写入外壳 canonical 但 bundle version 损坏的 selector，要求 Store 在生成 intent 前 fail closed。
        /// </summary>
        private static int RunCorruptBundleSelectorNegativeTest(string suiteRoot)
        {
            var root = Path.Combine(suiteRoot, "selector-corrupt-bundle");
            using (var package = BuildPackage(root, "corrupt-bundle"))
            using (var store = GasCodeGenGenerationStore.Open(root))
            {
                var record = store.StageGeneration(package.Descriptor);
                var corruptSelector = CorruptBundleVersion(package.Candidate.SelectorBytes);
                GasCodeGenPackageDescriptor.ValidateSelectorBytes(package.Candidate.SelectorBytes);
                ExpectFailure(() => GasCodeGenPackageDescriptor.ValidateSelectorBytes(corruptSelector));
                var selectorPath = Resolve(root, GasCodeGenCandidateCompileGate.SelectorRelativePath);
                File.WriteAllBytes(selectorPath, corruptSelector);
                ExpectFailure(() => store.Publish(record));
                Assert(ByteArraysEqual(File.ReadAllBytes(selectorPath), corruptSelector), "corrupt selector unchanged");
                Assert(
                    !File.Exists(Resolve(root, "ProjectSettings/GasCodeGen/PublishIntent.json")),
                    "corrupt selector cannot create publish intent");
            }
            return 1;
        }

        /// <summary>
        /// 保持 selector ASCII/Base64 canonical，仅将 bundle little-endian version 改为不支持值。
        /// </summary>
        private static byte[] CorruptBundleVersion(byte[] selectorBytes)
        {
            var text = s_utf8.GetString(selectorBytes);
            var body = text.Substring(
                GasCodeGenPackageDescriptor.SelectorMagic.Length,
                text.Length - GasCodeGenPackageDescriptor.SelectorMagic.Length - 1);
            var bundle = Convert.FromBase64String(body);
            bundle[0] ^= 1;
            return Encoding.ASCII.GetBytes(
                GasCodeGenPackageDescriptor.SelectorMagic + Convert.ToBase64String(bundle) + "\n");
        }

        /// <summary>
        /// 以真实 durable NotStarted intent 覆盖 unknown member 与非 canonical whitespace 两类 recovery 拒绝。
        /// </summary>
        private static int RunIntentJsonRecoveryNegativeTests(string suiteRoot)
        {
            var passed = 0;
            passed += RunIntentJsonRecoveryNegativeCase(
                Path.Combine(suiteRoot, "intent-unknown-member"),
                addUnknownMember: true);
            passed += RunIntentJsonRecoveryNegativeCase(
                Path.Combine(suiteRoot, "intent-noncanonical-bytes"),
                addUnknownMember: false);
            return passed;
        }

        /// <summary>
        /// 建立 production intent 后注入指定 JSON 漂移，要求 recovery 保留 fixed evidence 并且不产生 Tx archive。
        /// </summary>
        private static int RunIntentJsonRecoveryNegativeCase(string root, bool addUnknownMember)
        {
            string intentPath;
            string transactionRoot;
            byte[] mutatedBytes;
            using (var package = BuildPackage(root, addUnknownMember ? "unknown-intent" : "wire-intent"))
            using (var store = GasCodeGenGenerationStore.OpenForFixture(
                       root,
                       GasCodeGenGenerationStoreFixture.Observe()))
            {
                var record = store.StageGeneration(package.Descriptor);
                store.SeedRecoveryIntentForFixture(
                    record,
                    GasCodeGenCommitAttemptState.NotStarted,
                    useMissingPreviousSnapshot: false);
                intentPath = store.PublishIntentPath;
                transactionRoot = Resolve(
                    root,
                    "ProjectSettings/GasCodeGen/Generations/" + record.GenerationId + "/Tx");
                var canonicalBytes = File.ReadAllBytes(intentPath);
                mutatedBytes = addUnknownMember
                    ? AddUnexpectedIntentMember(canonicalBytes)
                    : AddLeadingJsonWhitespace(canonicalBytes);
                RewriteFixtureBytesDurable(intentPath, mutatedBytes);
            }
            using (var recoveryStore = GasCodeGenGenerationStore.OpenForRecovery(root))
                ExpectFailure(() => recoveryStore.Recover());
            Assert(ByteArraysEqual(File.ReadAllBytes(intentPath), mutatedBytes), "invalid intent evidence retained");
            Assert(
                !Directory.Exists(transactionRoot)
                || Directory.GetFiles(transactionRoot, "*.json", SearchOption.TopDirectoryOnly).Length == 0,
                "invalid intent cannot create transaction archive");
            return 1;
        }

        /// <summary>
        /// 在不修改 IntentSha256 的前提下附加未知协议字段，复现默认 Json.NET 静默忽略风险。
        /// </summary>
        private static byte[] AddUnexpectedIntentMember(byte[] canonicalBytes)
        {
            var value = JObject.Parse(
                s_utf8.GetString(canonicalBytes),
                new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                });
            value.Add("UnexpectedProtocolField", "must-be-rejected");
            var json = JsonConvert.SerializeObject(value, Formatting.Indented)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n') + "\n";
            return s_utf8.GetBytes(json);
        }

        /// <summary>
        /// 只增加 JSON 语法允许的前导空白，证明 exact canonical byte round-trip 独立生效。
        /// </summary>
        private static byte[] AddLeadingJsonWhitespace(byte[] canonicalBytes)
        {
            var mutated = new byte[canonicalBytes.Length + 1];
            mutated[0] = (byte)' ';
            Buffer.BlockCopy(canonicalBytes, 0, mutated, 1, canonicalBytes.Length);
            return mutated;
        }

        /// <summary>
        /// 以 WriteThrough/Flush(true) 覆盖 fixture evidence，并重读验证精确 bytes。
        /// </summary>
        private static void RewriteFixtureBytesDurable(string path, byte[] bytes)
        {
            using (var stream = new FileStream(
                       path,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            Assert(ByteArraysEqual(File.ReadAllBytes(path), bytes), "fixture intent durable rewrite");
        }

        /// <summary>
        /// 以目录占用 exact selector path，要求 Missing recovery 将其视为 unknown 并保留 fixed intent。
        /// </summary>
        private static int RunSelectorDirectoryRecoveryNegativeTest(string suiteRoot)
        {
            var root = Path.Combine(suiteRoot, "selector-path-directory");
            string intentPath;
            string selectorPath;
            string transactionRoot;
            byte[] intentBytes;
            using (var package = BuildPackage(root, "selector-directory"))
            using (var store = GasCodeGenGenerationStore.OpenForFixture(
                       root,
                       GasCodeGenGenerationStoreFixture.Observe()))
            {
                var record = store.StageGeneration(package.Descriptor);
                store.SeedRecoveryIntentForFixture(
                    record,
                    GasCodeGenCommitAttemptState.NotStarted,
                    useMissingPreviousSnapshot: false);
                intentPath = store.PublishIntentPath;
                selectorPath = store.SelectorPath;
                transactionRoot = Resolve(
                    root,
                    "ProjectSettings/GasCodeGen/Generations/" + record.GenerationId + "/Tx");
                intentBytes = File.ReadAllBytes(intentPath);
                Directory.CreateDirectory(selectorPath);
            }
            using (var recoveryStore = GasCodeGenGenerationStore.OpenForRecovery(root))
                ExpectFailure(() => recoveryStore.Recover());
            Assert(Directory.Exists(selectorPath), "selector directory evidence retained");
            Assert(ByteArraysEqual(File.ReadAllBytes(intentPath), intentBytes), "selector directory intent retained");
            Assert(
                !Directory.Exists(transactionRoot)
                || Directory.GetFiles(transactionRoot, "*.json", SearchOption.TopDirectoryOnly).Length == 0,
                "selector directory cannot create transaction archive");
            return 1;
        }

        /// <summary>
        /// 覆盖 Committed、NoOp、NotStarted、Armed、CompetitionFailed 与 Indeterminate 六态恢复矩阵。
        /// </summary>
        private static int RunTransactionTests(string suiteRoot)
        {
            var passed = 0;
            passed += RunCommittedAndNoOp(Path.Combine(suiteRoot, "transaction-success"));
            passed += RunFaultRecoveryCase(
                Path.Combine(suiteRoot, "transaction-notstarted"),
                GasCodeGenPublishFaultPoint.AfterNotStartedIntent,
                GasCodeGenCommitAttemptState.NotStarted,
                recoverySucceeds: true);
            passed += RunFaultRecoveryCase(
                Path.Combine(suiteRoot, "transaction-armed"),
                GasCodeGenPublishFaultPoint.AfterArmedIntent,
                GasCodeGenCommitAttemptState.Armed,
                recoverySucceeds: true);
            passed += RunFaultRecoveryCase(
                Path.Combine(suiteRoot, "transaction-target-equal"),
                GasCodeGenPublishFaultPoint.AfterSelectorCommit,
                GasCodeGenCommitAttemptState.Armed,
                recoverySucceeds: false);
            passed += RunFaultRecoveryCase(
                Path.Combine(suiteRoot, "transaction-receipt"),
                GasCodeGenPublishFaultPoint.AfterCommittedReceipt,
                GasCodeGenCommitAttemptState.Committed,
                recoverySucceeds: true);
            passed += RunFaultRecoveryCase(
                Path.Combine(suiteRoot, "transaction-competition"),
                GasCodeGenPublishFaultPoint.ForceCompetitionFailed,
                GasCodeGenCommitAttemptState.CompetitionFailed,
                recoverySucceeds: false);
            passed += RunFaultRecoveryCase(
                Path.Combine(suiteRoot, "transaction-indeterminate"),
                GasCodeGenPublishFaultPoint.ForceIndeterminate,
                GasCodeGenCommitAttemptState.Indeterminate,
                recoverySucceeds: false);
            return passed;
        }

        /// <summary>
        /// 验证首次 Missing commit 和 same-target NoOp 均闭合，且 NoOp 不产生新 PromotionId/audit。
        /// </summary>
        private static int RunCommittedAndNoOp(string root)
        {
            using (var package = BuildPackage(root, "success"))
            using (var store = GasCodeGenGenerationStore.Open(root))
            {
                var record = store.StageGeneration(package.Descriptor);
                var committed = store.Publish(record);
                Assert(committed.State == GasCodeGenCommitAttemptState.Committed, "committed state");
                var auditBefore = store.VerifyActive();
                var noOp = store.Publish(record);
                Assert(noOp.IsNoOp && string.IsNullOrEmpty(noOp.PromotionId), "NoOp promotion identity");
                var auditAfter = store.VerifyActive();
                Assert(string.Equals(auditBefore.AuditSha256, auditAfter.AuditSha256, StringComparison.Ordinal), "NoOp audit unchanged");
            }
            return 2;
        }

        /// <summary>
        /// 在精确 fault point 中断并用新 Store handle 执行 recovery，验证预期状态与 fail-closed 结果。
        /// </summary>
        private static int RunFaultRecoveryCase(
            string root,
            GasCodeGenPublishFaultPoint faultPoint,
            GasCodeGenCommitAttemptState expectedState,
            bool recoverySucceeds)
        {
            using (var package = BuildPackage(root, faultPoint.ToString()))
            {
                using (var store = GasCodeGenGenerationStore.Open(root))
                {
                    var record = store.StageGeneration(package.Descriptor);
                    ExpectFailure(() => store.Publish(record, faultPoint));
                    var intent = store.VerifyPublishIntent();
                    Assert(intent.CommitAttemptState == expectedState, "fault state " + faultPoint);
                    if (expectedState == GasCodeGenCommitAttemptState.CompetitionFailed
                        || expectedState == GasCodeGenCommitAttemptState.Indeterminate)
                    {
                        Assert(
                            string.IsNullOrEmpty(intent.PromotionId),
                            "failed state cannot claim PromotionId " + faultPoint);
                    }
                }
                using (var recoveryStore = GasCodeGenGenerationStore.OpenForRecovery(root))
                {
                    if (recoverySucceeds)
                        recoveryStore.Recover();
                    else
                        ExpectFailure(() => recoveryStore.Recover());
                }
            }
            return 1;
        }

        /// <summary>
        /// 创建一个完整 fixture package；可在 manifest seal 后注入 meta 或 required-set 故障。
        /// </summary>
        private static BuiltPackage BuildPackage(
            string root,
            string tag,
            MetaMutation metaMutation = MetaMutation.None,
            int mutatedArtifactIndex = -1,
            int setMutation = 0,
            bool prepareRoute = true)
        {
            Directory.CreateDirectory(root);
            if (prepareRoute)
                PrepareRoute(root);
            var workspace = GasCodeGenCandidateWorkspace.Create(root);
            try
            {
                var manifests = WriteArtifactManifests(workspace, tag, setMutation);
                ApplyMetaMutation(workspace, metaMutation, mutatedArtifactIndex);
                var route = GasCodeGenCandidateCompileGate.CaptureProductionRoute(root);
                var authority = workspace.SealAuthority();
                var candidate = GasCodeGenPackageDescriptor.CreateSelectorCandidate(
                    workspace,
                    authority,
                    Hash("input-" + tag),
                    Hash("schema-" + tag),
                    Hash("content-" + tag),
                    Hash("layout-" + tag),
                    manifests.Core,
                    manifests.AutoChess,
                    route);
                var compilePlan = GasCodeGenCandidateCompileGate.ValidateFullPackage(
                    workspace, authority, candidate, route);
                var descriptor = GasCodeGenPackageDescriptor.Save(
                    workspace, authority, candidate, compilePlan, route);
                return new BuiltPackage(workspace, candidate, route, descriptor);
            }
            catch
            {
                workspace.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 写入六个 required artifacts/meta 并分别冻结 Core 与 AutoChess manifest v3。
        /// </summary>
        private static ManifestPair WriteArtifactManifests(
            GasCodeGenCandidateWorkspace workspace,
            string tag,
            int setMutation)
        {
            var core = new GasCodeGenManifest(
                workspace.ProjectRoot, workspace.CoreCandidateRoot, Hash("input-" + tag), workspace.CorePublishedRoot);
            var autoChess = new GasCodeGenManifest(
                workspace.ProjectRoot, workspace.AutoChessCandidateRoot, Hash("input-" + tag), workspace.AutoChessPublishedRoot);
            for (var index = 0; index < s_artifactPaths.Length; index++)
            {
                if (setMutation < 0 && index == 0)
                    continue;
                WriteAndRegisterArtifact(workspace, core, autoChess, s_artifactPaths[index], tag, index);
            }
            if (setMutation > 0)
                WriteExtraArtifact(workspace, core, tag);
            core.Save();
            autoChess.Save();
            return new ManifestPair(core, autoChess);
        }

        /// <summary>
        /// 写入一个 exact required artifact/meta，并按冻结 kind/owner/layer 登记到对应 manifest。
        /// </summary>
        private static void WriteAndRegisterArtifact(
            GasCodeGenCandidateWorkspace workspace,
            GasCodeGenManifest core,
            GasCodeGenManifest autoChess,
            string canonicalPath,
            string tag,
            int index)
        {
            var auto = canonicalPath.StartsWith("Assets/AutoChessDemo/", StringComparison.Ordinal);
            var publishedRoot = auto ? workspace.AutoChessPublishedRoot : workspace.CorePublishedRoot;
            var prefix = auto ? "Assets/AutoChessDemo/Generated/" : "Assets/GAS/Generated/CodeGen/";
            var relative = canonicalPath.Substring(prefix.Length);
            var physical = Path.Combine(auto ? workspace.AutoChessCandidateRoot : workspace.CoreCandidateRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(physical));
            var content = canonicalPath.EndsWith(".cs", StringComparison.Ordinal)
                ? "namespace Fixture { public static class T" + index + " { public const string Value = \"" + tag + "\"; } }\n"
                : "# fixture " + tag + "\n";
            File.WriteAllText(physical, content, s_utf8);
            File.WriteAllText(physical + ".meta", "fileFormatVersion: 2\nguid: " + Hash(canonicalPath).Substring(0, 32) + "\n", s_utf8);
            RegisterArtifact(auto ? autoChess : core, physical, canonicalPath, index, publishedRoot);
        }

        /// <summary>
        /// 按 exact required table 登记 artifact 职责。
        /// </summary>
        private static void RegisterArtifact(
            GasCodeGenManifest manifest,
            string physical,
            string canonicalPath,
            int index,
            string publishedRoot)
        {
            if (index == 0)
                manifest.AddGeneratedFile("AutoChess", physical, "Runtime", true, "RuntimeDemoConfig", "AutoChessDemo");
            else if (index == 1)
                manifest.AddGeneratedFile("Luban", physical, "Editor", false, "NormalizedDefinitionRow", "DefinitionCodeGen");
            else if (index == 2)
                manifest.AddGeneratedFile("Validation", physical, "Editor/CI", false, "ValidationArtifact", "EditorCi");
            else
                manifest.AddGeneratedFile("Runtime", physical, "Runtime", true, "RuntimePureGlue", "DefinitionCodeGen");
        }

        /// <summary>
        /// 写入第七个合法但不属于 required-set v2 的 artifact，用于扩张负例。
        /// </summary>
        private static void WriteExtraArtifact(
            GasCodeGenCandidateWorkspace workspace,
            GasCodeGenManifest manifest,
            string tag)
        {
            var path = Path.Combine(workspace.CoreCandidateRoot, "ExtraValidation.md");
            File.WriteAllText(path, "# extra " + tag + "\n", s_utf8);
            File.WriteAllText(path + ".meta", "fileFormatVersion: 2\nguid: " + Hash(path).Substring(0, 32) + "\n", s_utf8);
            manifest.AddGeneratedFile("Extra", path, "Editor/CI", false, "ValidationArtifact", "EditorCi");
        }

        /// <summary>
        /// 在 manifest Save 后注入 missing、length 或 same-length SHA meta 漂移。
        /// </summary>
        private static void ApplyMetaMutation(
            GasCodeGenCandidateWorkspace workspace,
            MetaMutation mutation,
            int artifactIndex)
        {
            if (mutation == MetaMutation.None)
                return;
            var physical = ResolveCandidateArtifact(workspace, s_artifactPaths[artifactIndex]);
            var metaPath = physical + ".meta";
            if (mutation == MetaMutation.Missing)
            {
                File.Delete(metaPath);
                return;
            }
            var bytes = File.ReadAllBytes(metaPath);
            if (mutation == MetaMutation.Length)
            {
                var expanded = new byte[bytes.Length + 1];
                Buffer.BlockCopy(bytes, 0, expanded, 0, bytes.Length);
                expanded[expanded.Length - 1] = (byte)'x';
                File.WriteAllBytes(metaPath, expanded);
                return;
            }
            bytes[0] ^= 1;
            File.WriteAllBytes(metaPath, bytes);
        }

        /// <summary>
        /// 将 required canonical path 映射到当前 fixture workspace 的物理 artifact。
        /// </summary>
        private static string ResolveCandidateArtifact(
            GasCodeGenCandidateWorkspace workspace,
            string canonicalPath)
        {
            const string autoPrefix = "Assets/AutoChessDemo/Generated/";
            const string corePrefix = "Assets/GAS/Generated/CodeGen/";
            return canonicalPath.StartsWith(autoPrefix, StringComparison.Ordinal)
                ? Path.Combine(workspace.AutoChessCandidateRoot, canonicalPath.Substring(autoPrefix.Length))
                : Path.Combine(workspace.CoreCandidateRoot, canonicalPath.Substring(corePrefix.Length));
        }

        /// <summary>
        /// 创建 exact analyzer 路径和 14 个 route scaffold 文件，bytes 在所有 fixture generation 间保持不变。
        /// </summary>
        private static void PrepareRoute(string root)
        {
            WriteFixtureFile(
                Resolve(root, GasCodeGenCandidateCompileGate.AnalyzerRelativePath),
                Encoding.ASCII.GetBytes("fixture-analyzer-v1"));
            for (var index = 0; index < s_scaffoldPaths.Length; index++)
                WriteFixtureFile(Resolve(root, s_scaffoldPaths[index]), Encoding.UTF8.GetBytes(s_scaffoldPaths[index] + "\n"));
        }

        /// <summary>
        /// 写入 fixture 普通文件并创建父目录。
        /// </summary>
        private static void WriteFixtureFile(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
        }

        /// <summary>
        /// 仅按 request 的 Scenario/SeedState/CompetitionTarget 执行事务，CaseId 只回绑身份。
        /// </summary>
        private static ScenarioResponse ExecuteScenario(string projectRoot, ScenarioRequest request)
        {
            Directory.CreateDirectory(projectRoot);
            var selectorA = DecodeRequestBytes(request.SelectorABytesBase64, "SelectorABytesBase64");
            var selectorB = DecodeRequestBytes(request.SelectorBBytesBase64, "SelectorBBytesBase64");
            var competition = DecodeRequestBytes(
                request.CompetitionSelectorBytesBase64,
                "CompetitionSelectorBytesBase64");
            ValidateCanonicalSelectorBytes(selectorA, "SelectorABytesBase64");
            ValidateCanonicalSelectorBytes(selectorB, "SelectorBBytesBase64");
            if (ByteArraysEqual(selectorA, selectorB))
                throw new InvalidDataException("Selector A and B must have different bytes.");
            var targetBytes = string.Equals(request.Target, "A", StringComparison.Ordinal)
                ? selectorA
                : selectorB;
            ValidateInitialSelector(projectRoot, request, selectorA, selectorB);
            using (var package = BuildPackage(
                       projectRoot,
                       request.Target,
                       prepareRoute: false))
            {
                var fixture = CreateScenarioFixture(request, selectorA, selectorB, targetBytes, competition);
                var execution = RunScenarioInvocation(
                    projectRoot,
                    request,
                    package,
                    fixture,
                    targetBytes);
                var beforeRecovery = CaptureTransactionSnapshot(projectRoot, "BeforeRecovery", -1);
                var recovery = RunScenarioRecoveries(projectRoot, request.RecoveryCount, beforeRecovery);
                return CreateScenarioResponse(
                    projectRoot,
                    request,
                    targetBytes,
                    fixture,
                    execution,
                    beforeRecovery,
                    recovery);
            }
        }

        /// <summary>
        /// 根据冻结场景选择唯一 interleave；不得从 CaseId、Name 或预期结果推断分支。
        /// </summary>
        private static GasCodeGenGenerationStoreFixture CreateScenarioFixture(
            ScenarioRequest request,
            byte[] selectorA,
            byte[] selectorB,
            byte[] targetBytes,
            byte[] competitionBytes)
        {
            if (string.Equals(request.Scenario, "PostClaimPreviousDrift", StringComparison.Ordinal))
                return GasCodeGenGenerationStoreFixture.ReplaceAfterClaim(competitionBytes);
            if (string.Equals(request.Scenario, "PresentMissingRecovery", StringComparison.Ordinal))
                return GasCodeGenGenerationStoreFixture.DeleteAfterClaim();
            if (string.Equals(request.Scenario, "CompetingCreate", StringComparison.Ordinal)
                || string.Equals(request.Scenario, "Recover", StringComparison.Ordinal)
                && !string.IsNullOrEmpty(request.CompetitionTarget))
            {
                var competing = string.Equals(request.CompetitionTarget, "A", StringComparison.Ordinal)
                    ? selectorA
                    : selectorB;
                return GasCodeGenGenerationStoreFixture.CompetingCreate(competing);
            }
            if (string.Equals(request.Scenario, "Recover", StringComparison.Ordinal)
                && string.Equals(request.FaultPoint, "ForceIndeterminate", StringComparison.Ordinal))
                return GasCodeGenGenerationStoreFixture.ReplaceAfterClaim(targetBytes);
            return GasCodeGenGenerationStoreFixture.Observe();
        }

        /// <summary>
        /// 使用 external selector fixture archive 后执行 production publish 或六态 seed。
        /// </summary>
        private static ScenarioExecution RunScenarioInvocation(
            string projectRoot,
            ScenarioRequest request,
            BuiltPackage package,
            GasCodeGenGenerationStoreFixture fixture,
            byte[] targetBytes)
        {
            using (var store = GasCodeGenGenerationStore.OpenForFixture(projectRoot, fixture))
            {
                var record = store.StageGenerationForFixture(package.Descriptor, targetBytes);
                if (string.Equals(request.Scenario, "RecoverState", StringComparison.Ordinal))
                    return SeedRecoveryState(store, record, request);
                if (string.Equals(request.Scenario, "Recover", StringComparison.Ordinal)
                    && string.Equals(request.InitialSelector, "Corrupt", StringComparison.Ordinal))
                {
                    store.SeedRecoveryIntentForFixture(
                        record,
                        GasCodeGenCommitAttemptState.Indeterminate,
                        useMissingPreviousSnapshot: true);
                    return new ScenarioExecution("Indeterminate", 0, string.Empty);
                }
                return PublishScenario(store, record, request);
            }
        }

        /// <summary>
        /// 通过 Store fixture seed API 真实构造 TR16-21 指定 durable intent state。
        /// </summary>
        private static ScenarioExecution SeedRecoveryState(
            GasCodeGenGenerationStore store,
            GasCodeGenGenerationStore.GenerationRecord record,
            ScenarioRequest request)
        {
            var state = ParseCommitState(request.SeedState);
            var intent = store.SeedRecoveryIntentForFixture(record, state, false);
            return new ScenarioExecution(
                intent.CommitAttemptState.ToString(),
                0,
                intent.PromotionId);
        }

        /// <summary>
        /// 执行一次 production publish；fixture 场景禁用枚举式 ForceCompetitionFailed 替代真实竞争写。
        /// </summary>
        private static ScenarioExecution PublishScenario(
            GasCodeGenGenerationStore store,
            GasCodeGenGenerationStore.GenerationRecord record,
            ScenarioRequest request)
        {
            var fault = IsInterleaveScenario(request.Scenario)
                ? GasCodeGenPublishFaultPoint.None
                : ParseFaultPoint(request.FaultPoint);
            try
            {
                var result = store.Publish(record, fault);
                return new ScenarioExecution(result.State.ToString(), 0, result.PromotionId);
            }
            catch (Exception exception)
            {
                var state = string.Empty;
                var promotionId = string.Empty;
                if (File.Exists(store.PublishIntentPath))
                {
                    var intent = store.VerifyPublishIntent();
                    state = intent.CommitAttemptState.ToString();
                    promotionId = intent.PromotionId;
                }
                return new ScenarioExecution(
                    state,
                    1,
                    promotionId,
                    "Publish:" + exception.GetType().Name);
            }
        }

        /// <summary>
        /// 判断场景是否由文件系统 interleave 驱动，避免退回枚举式伪竞争故障。
        /// </summary>
        private static bool IsInterleaveScenario(string scenario)
        {
            return string.Equals(scenario, "PostClaimPreviousDrift", StringComparison.Ordinal)
                   || string.Equals(scenario, "PresentMissingRecovery", StringComparison.Ordinal)
                   || string.Equals(scenario, "CompetingCreate", StringComparison.Ordinal)
                   || string.Equals(scenario, "Recover", StringComparison.Ordinal);
        }

        /// <summary>
        /// 逐次调用 public recovery 并保存每轮 selector/intent/audit/PromotionId typed snapshot。
        /// </summary>
        private static RecoveryExecution RunScenarioRecoveries(
            string projectRoot,
            int recoveryCount,
            TransactionSnapshot beforeRecovery)
        {
            var exitCodes = new List<int>();
            var observations = new List<string>();
            var snapshots = new List<TransactionSnapshot>();
            var previous = beforeRecovery;
            for (var index = 0; index < recoveryCount; index++)
            {
                var exitCode = RunSingleRecovery(projectRoot, previous, observations);
                var snapshot = CaptureTransactionSnapshot(projectRoot, "Recovery", index + 1);
                exitCodes.Add(exitCode);
                snapshots.Add(snapshot);
                previous = snapshot;
            }
            return new RecoveryExecution(exitCodes, observations, snapshots);
        }

        /// <summary>
        /// 执行一次 recovery；intent 已由前一轮关闭且磁盘身份不变时记录幂等 AlreadyClosed。
        /// </summary>
        private static int RunSingleRecovery(
            string projectRoot,
            TransactionSnapshot previous,
            ICollection<string> observations)
        {
            try
            {
                using (var store = GasCodeGenGenerationStore.OpenForRecovery(projectRoot))
                {
                    var result = store.Recover();
                    observations.Add("Recovery:" + result.Outcome);
                    return 0;
                }
            }
            catch (FileNotFoundException) when (previous.Intent.State == "Missing")
            {
                observations.Add("Recovery:AlreadyClosed");
                return 0;
            }
            catch (Exception exception)
            {
                observations.Add("RecoveryFailed:" + exception.GetType().Name);
                return 1;
            }
        }

        /// <summary>
        /// 从原始文件 bytes 生成 typed transaction snapshot，所有 SHA/Base64 均可由外部重算。
        /// </summary>
        private static TransactionSnapshot CaptureTransactionSnapshot(
            string projectRoot,
            string phase,
            int recoveryIndex)
        {
            var selector = CaptureFile(Resolve(projectRoot, GasCodeGenCandidateCompileGate.SelectorRelativePath));
            var intent = CaptureFile(Resolve(projectRoot, "ProjectSettings/GasCodeGen/PublishIntent.json"));
            var audit = CaptureFile(Resolve(projectRoot, "ProjectSettings/GasCodeGen/ActiveGenerationRef.json"));
            var identity = ReadTransactionIdentity(intent, audit);
            return new TransactionSnapshot
            {
                Phase = phase,
                RecoveryIndex = recoveryIndex,
                Selector = selector,
                Intent = intent,
                Audit = audit,
                ObservedState = identity.State,
                PromotionId = identity.PromotionId,
            };
        }

        /// <summary>
        /// 读取文件完整 bytes 身份；Missing 不携带伪 length/hash/Base64。
        /// </summary>
        private static RawFileSnapshot CaptureFile(string path)
        {
            if (!File.Exists(path))
                return RawFileSnapshot.Missing();
            var bytes = File.ReadAllBytes(path);
            return RawFileSnapshot.Present(bytes, HashBytes(bytes));
        }

        /// <summary>
        /// 仅从原始 intent/audit bytes 解出状态与 PromotionId，不把派生 response 当证据源。
        /// </summary>
        private static TransactionIdentity ReadTransactionIdentity(
            RawFileSnapshot intent,
            RawFileSnapshot audit)
        {
            if (intent.IsPresent)
            {
                var parsed = JsonConvert.DeserializeObject<GasCodeGenGenerationStore.PublishIntent>(
                    s_utf8.GetString(intent.Bytes));
                return new TransactionIdentity(
                    parsed == null ? string.Empty : parsed.CommitAttemptState.ToString(),
                    parsed == null ? string.Empty : parsed.PromotionId);
            }
            if (audit.IsPresent)
            {
                var parsed = JsonConvert.DeserializeObject<GasCodeGenGenerationStore.ActiveGenerationRef>(
                    s_utf8.GetString(audit.Bytes));
                return new TransactionIdentity(
                    parsed == null ? string.Empty : "Committed",
                    parsed == null ? string.Empty : parsed.PromotionId);
            }
            return new TransactionIdentity(string.Empty, string.Empty);
        }

        /// <summary>
        /// 汇总 E1 response；CreateNew/WriteThrough/Flush 只取 Store production writer 的实际回执。
        /// </summary>
        private static ScenarioResponse CreateScenarioResponse(
            string projectRoot,
            ScenarioRequest request,
            byte[] targetBytes,
            GasCodeGenGenerationStoreFixture fixture,
            ScenarioExecution execution,
            TransactionSnapshot beforeRecovery,
            RecoveryExecution recovery)
        {
            var final = CaptureTransactionSnapshot(projectRoot, "Final", request.RecoveryCount);
            var adapterExit = ResolveAdapterExitCode(request, execution.ObservedState);
            var promotionClaimed = final.Audit.IsPresent && !string.IsNullOrEmpty(final.PromotionId);
            return new ScenarioResponse
            {
                Schema = "EX-GAS-D1-E1-TransactionResponse-v1",
                RunId = request.RunId,
                CaseId = request.CaseId,
                InvocationId = request.InvocationId,
                Passed = true,
                AdapterExitCode = adapterExit,
                ObservedState = execution.ObservedState,
                FaultInvocationExitCode = execution.FaultInvocationExitCode,
                RecoveryExitCodes = recovery.ExitCodes.ToArray(),
                PromotionClaimed = promotionClaimed,
                IntentFullPreviousSnapshot = HasFullPreviousSnapshot(beforeRecovery.Intent),
                IntentCreatedWithCreateNewAndFlush = HasProductionClaimReceipt(fixture),
                RecoveryStable = IsRecoveryStable(recovery.Snapshots),
                Detail = BuildScenarioDetail(execution, recovery),
                TargetSelectorSha256 = HashBytes(targetBytes),
                TargetSelectorBytesBase64 = Convert.ToBase64String(targetBytes),
                IntentBeforeRecovery = beforeRecovery.Intent,
                BeforeRecovery = beforeRecovery,
                RecoverySnapshots = recovery.Snapshots.ToArray(),
                Final = final,
                ProductionClaimWrite = ProductionClaimWriteSnapshot.Create(fixture),
            };
        }

        /// <summary>
        /// 按 scenario/seed state 决定 adapter 进程结果，不读取 CaseId 或测试 Name。
        /// </summary>
        private static int ResolveAdapterExitCode(ScenarioRequest request, string observedState)
        {
            if (string.Equals(request.Scenario, "PostClaimPreviousDrift", StringComparison.Ordinal)
                || string.Equals(request.Scenario, "PresentMissingRecovery", StringComparison.Ordinal)
                || string.Equals(request.Scenario, "CompetingCreate", StringComparison.Ordinal)
                || string.Equals(request.Scenario, "Recover", StringComparison.Ordinal))
                return 1;
            if (string.Equals(request.Scenario, "RecoverState", StringComparison.Ordinal)
                && (string.Equals(observedState, "CompetitionFailed", StringComparison.Ordinal)
                    || string.Equals(observedState, "Indeterminate", StringComparison.Ordinal)))
                return 1;
            return 0;
        }

        /// <summary>
        /// 验证 intent 原始 bytes 携带 Missing 或 Present 的完整 previous snapshot。
        /// </summary>
        private static bool HasFullPreviousSnapshot(RawFileSnapshot snapshot)
        {
            if (!snapshot.IsPresent)
                return false;
            var intent = JsonConvert.DeserializeObject<GasCodeGenGenerationStore.PublishIntent>(
                s_utf8.GetString(snapshot.Bytes));
            if (intent == null)
                return false;
            if (string.Equals(intent.PreviousSelectorState, "Missing", StringComparison.Ordinal))
            {
                return string.IsNullOrEmpty(intent.PreviousSelectorBytesBase64)
                       && string.IsNullOrEmpty(intent.PreviousSelectorSha256);
            }
            var bytes = DecodeRequestBytes(
                intent.PreviousSelectorBytesBase64,
                "PreviousSelectorBytesBase64");
            return string.Equals(HashBytes(bytes), intent.PreviousSelectorSha256, StringComparison.Ordinal);
        }

        /// <summary>
        /// 要求 production durable writer 实际回执为 CreateNew、WriteThrough、Flush(true) 且重读一致。
        /// </summary>
        private static bool HasProductionClaimReceipt(GasCodeGenGenerationStoreFixture fixture)
        {
            return fixture.IntentClaimWriteObserved
                   && fixture.IntentClaimFileMode == FileMode.CreateNew
                   && (fixture.IntentClaimFileOptions & FileOptions.WriteThrough) != 0
                   && fixture.IntentClaimFlushToDisk
                   && fixture.IntentClaimBytesVerified;
        }

        /// <summary>
        /// 两轮及以上 recovery 必须从首轮结束后保持 selector/intent/audit/PromotionId 完全稳定。
        /// </summary>
        private static bool IsRecoveryStable(IReadOnlyList<TransactionSnapshot> snapshots)
        {
            if (snapshots.Count < 2)
                return true;
            var first = snapshots[0];
            for (var index = 1; index < snapshots.Count; index++)
            {
                if (!first.HasSameIdentity(snapshots[index]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 合并 publish/recovery 观察文本，保留明确失败类型但不夹带预期判定。
        /// </summary>
        private static string BuildScenarioDetail(
            ScenarioExecution execution,
            RecoveryExecution recovery)
        {
            var details = new List<string>();
            if (!string.IsNullOrEmpty(execution.Detail))
                details.Add(execution.Detail);
            details.AddRange(recovery.Observations);
            return details.Count == 0 ? "Scenario completed." : string.Join(";", details);
        }

        /// <summary>
        /// 解析冻结 fault point 名称；空值映射为 None，未知值直接拒绝。
        /// </summary>
        private static GasCodeGenPublishFaultPoint ParseFaultPoint(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return GasCodeGenPublishFaultPoint.None;
            if (!Enum.TryParse(value, ignoreCase: true, out GasCodeGenPublishFaultPoint result)
                || !Enum.IsDefined(typeof(GasCodeGenPublishFaultPoint), result))
                throw new InvalidDataException("Unknown D1-B fault point: " + value);
            return result;
        }

        /// <summary>
        /// 解析冻结六态名称；仅 RecoverState 可调用。
        /// </summary>
        private static GasCodeGenCommitAttemptState ParseCommitState(string value)
        {
            if (!Enum.TryParse(value, false, out GasCodeGenCommitAttemptState result)
                || !Enum.IsDefined(typeof(GasCodeGenCommitAttemptState), result))
                throw new InvalidDataException("Unknown D1-B seed state: " + value);
            return result;
        }

        /// <summary>
        /// 要求 request JSON 恰好包含冻结 ABI 的 18 个唯一属性，拒绝隐藏控制字段或缺项。
        /// </summary>
        private static void ValidateScenarioRequestProperties(JObject request)
        {
            var actual = request.Properties().Select(property => property.Name).ToArray();
            if (actual.Length != s_scenarioRequestProperties.Length
                || actual.Distinct(StringComparer.Ordinal).Count() != actual.Length)
                throw new InvalidDataException("D1 E1 request property count/uniqueness mismatch.");
            var expected = new HashSet<string>(s_scenarioRequestProperties, StringComparer.Ordinal);
            for (var index = 0; index < actual.Length; index++)
            {
                if (!expected.Contains(actual[index]))
                    throw new InvalidDataException("Unknown D1 E1 request property: " + actual[index]);
            }
        }

        /// <summary>
        /// 校验 E1 request exact schema、路径、selector 名称、场景与 seed/competition 组合。
        /// </summary>
        private static void ValidateScenarioRequest(ScenarioRequest request)
        {
            if (!string.Equals(request.Schema, "EX-GAS-D1-E1-TransactionRequest-v1", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(request.RunId)
                || string.IsNullOrWhiteSpace(request.CaseId)
                || string.IsNullOrWhiteSpace(request.InvocationId))
                throw new InvalidDataException("D1 E1 request schema/identity is invalid.");
            if (!string.Equals(request.InitialSelector, "Missing", StringComparison.Ordinal)
                && !string.Equals(request.InitialSelector, "A", StringComparison.Ordinal)
                && !string.Equals(request.InitialSelector, "B", StringComparison.Ordinal)
                && !string.Equals(request.InitialSelector, "Corrupt", StringComparison.Ordinal))
                throw new InvalidDataException("InitialSelector must be Missing, A, B or Corrupt.");
            if (!string.Equals(request.Target, "A", StringComparison.Ordinal)
                && !string.Equals(request.Target, "B", StringComparison.Ordinal))
                throw new InvalidDataException("Target must be A or B.");
            if (request.RecoveryCount < 0 || request.RecoveryCount > 16)
                throw new InvalidDataException("RecoveryCount is outside 0..16.");
            ValidateScenarioName(request.Scenario);
            ValidateScenarioCombination(request);
        }

        /// <summary>
        /// 拒绝未冻结的场景名，防止 adapter 静默回退到普通 publish。
        /// </summary>
        private static void ValidateScenarioName(string scenario)
        {
            var allowed = new[]
            {
                "Publish", "PublishAndRecover", "PostClaimPreviousDrift",
                "PresentMissingRecovery", "Recover", "CompetingCreate", "RecoverState",
            };
            if (!allowed.Contains(scenario, StringComparer.Ordinal))
                throw new InvalidDataException("Unknown D1 E1 scenario: " + scenario);
        }

        /// <summary>
        /// 校验 RecoverState 必带 SeedState、竞争场景必带 A/B CompetitionTarget。
        /// </summary>
        private static void ValidateScenarioCombination(ScenarioRequest request)
        {
            if (string.Equals(request.Scenario, "RecoverState", StringComparison.Ordinal))
                ParseCommitState(request.SeedState);
            else if (!string.IsNullOrEmpty(request.SeedState))
                throw new InvalidDataException("SeedState is only valid for RecoverState.");
            var needsCompetition = string.Equals(request.Scenario, "CompetingCreate", StringComparison.Ordinal)
                                   || string.Equals(request.Scenario, "Recover", StringComparison.Ordinal)
                                   && string.IsNullOrEmpty(request.FaultPoint)
                                   && !string.Equals(request.InitialSelector, "Corrupt", StringComparison.Ordinal);
            if (needsCompetition
                && !string.Equals(request.CompetitionTarget, "A", StringComparison.Ordinal)
                && !string.Equals(request.CompetitionTarget, "B", StringComparison.Ordinal))
                throw new InvalidDataException("CompetitionTarget must be A or B for a competition scenario.");
        }

        /// <summary>
        /// 校验 request ProjectRoot 与三个控制路径精确等于 production contract。
        /// </summary>
        private static void ValidateScenarioPaths(string projectRoot, ScenarioRequest request)
        {
            if (!string.Equals(
                    Path.GetFullPath(request.ProjectRoot),
                    Path.GetFullPath(projectRoot),
                    StringComparison.OrdinalIgnoreCase)
                || !string.Equals(
                    request.SelectorRelativePath,
                    GasCodeGenCandidateCompileGate.SelectorRelativePath,
                    StringComparison.Ordinal)
                || !string.Equals(
                    request.IntentRelativePath,
                    "ProjectSettings/GasCodeGen/PublishIntent.json",
                    StringComparison.Ordinal)
                || !string.Equals(
                    request.AuditRelativePath,
                    "ProjectSettings/GasCodeGen/ActiveGenerationRef.json",
                    StringComparison.Ordinal))
                throw new InvalidDataException("D1 E1 request path contract mismatch.");
        }

        /// <summary>
        /// 要求外层已布置的 selector 初态与 request 的 Missing/A/B/Corrupt 完全一致。
        /// </summary>
        private static void ValidateInitialSelector(
            string projectRoot,
            ScenarioRequest request,
            byte[] selectorA,
            byte[] selectorB)
        {
            ValidateScenarioPaths(projectRoot, request);
            var path = Resolve(projectRoot, GasCodeGenCandidateCompileGate.SelectorRelativePath);
            if (string.Equals(request.InitialSelector, "Missing", StringComparison.Ordinal))
            {
                if (File.Exists(path))
                    throw new InvalidDataException("Initial selector must be Missing.");
                return;
            }
            if (!File.Exists(path))
                throw new FileNotFoundException("Initial selector is missing.", path);
            var actual = File.ReadAllBytes(path);
            if (string.Equals(request.InitialSelector, "A", StringComparison.Ordinal)
                && !ByteArraysEqual(actual, selectorA)
                || string.Equals(request.InitialSelector, "B", StringComparison.Ordinal)
                && !ByteArraysEqual(actual, selectorB)
                || string.Equals(request.InitialSelector, "Corrupt", StringComparison.Ordinal)
                && IsCanonicalSelector(actual))
                throw new InvalidDataException("Initial selector bytes do not match request state.");
        }

        /// <summary>
        /// 解码 canonical Base64 request 字段并拒绝空值或非 canonical 表达。
        /// </summary>
        private static byte[] DecodeRequestBytes(string value, string fieldName)
        {
            if (string.IsNullOrEmpty(value))
                throw new InvalidDataException(fieldName + " is required.");
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(value);
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException(fieldName + " is not Base64.", exception);
            }
            if (!string.Equals(Convert.ToBase64String(bytes), value, StringComparison.Ordinal))
                throw new InvalidDataException(fieldName + " is not canonical Base64.");
            return bytes;
        }

        /// <summary>
        /// 校验 selector 为 exact magic、单 Base64 body 与终止 LF 的两行 envelope。
        /// </summary>
        private static void ValidateCanonicalSelectorBytes(byte[] bytes, string fieldName)
        {
            try
            {
                GasCodeGenPackageDescriptor.ValidateSelectorBytes(bytes);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException(fieldName + " is not a valid canonical selector bundle.", exception);
            }
        }

        /// <summary>
        /// 判断 bytes 是否通过 production canonical selector 与 GasSourceBundle-v1 strict reader。
        /// </summary>
        private static bool IsCanonicalSelector(byte[] bytes)
        {
            try
            {
                GasCodeGenPackageDescriptor.ValidateSelectorBytes(bytes);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 比较两个完整 byte arrays。
        /// </summary>
        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            return left != null && right != null && left.SequenceEqual(right);
        }

        /// <summary>
        /// 要求 action 抛出异常，否则测试失败。
        /// </summary>
        private static void ExpectFailure(Action action)
        {
            try
            {
                action();
            }
            catch
            {
                return;
            }
            throw new InvalidOperationException("Expected failure did not occur.");
        }

        /// <summary>
        /// 要求布尔断言成立。
        /// </summary>
        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("D1-B self-test assertion failed: " + message);
        }

        /// <summary>
        /// 计算 UTF-8 文本 SHA-256。
        /// </summary>
        private static string Hash(string value)
        {
            return HashBytes(Encoding.UTF8.GetBytes(value));
        }

        /// <summary>
        /// 计算 bytes lowercase SHA-256。
        /// </summary>
        private static string HashBytes(byte[] bytes)
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
        /// 解析 fixture project-relative path。
        /// </summary>
        private static string Resolve(string root, string relativePath)
        {
            return Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        /// <summary>
        /// 仅删除系统 Temp 下由本 suite 创建且名称匹配固定前缀的目录。
        /// </summary>
        private static void DeleteOwnedSuiteRoot(string root)
        {
            var tempRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var resolved = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!resolved.StartsWith(tempRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(resolved).StartsWith("gcd1b-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refused to delete a non-owned self-test root: " + resolved);
            if (Directory.Exists(resolved))
                Directory.Delete(resolved, recursive: true);
        }

        /// <summary>
        /// 保存两个 fixture component manifests。
        /// </summary>
        private sealed class ManifestPair
        {
            /// <summary>
            /// 创建 manifest pair。
            /// </summary>
            internal ManifestPair(GasCodeGenManifest core, GasCodeGenManifest autoChess)
            {
                Core = core;
                AutoChess = autoChess;
            }

            internal GasCodeGenManifest Core { get; }
            internal GasCodeGenManifest AutoChess { get; }
        }

        /// <summary>
        /// 持有一次 fixture workspace 及其 selector/route/descriptor snapshots。
        /// </summary>
        private sealed class BuiltPackage : IDisposable
        {
            /// <summary>
            /// 创建 fixture package owner。
            /// </summary>
            internal BuiltPackage(
                GasCodeGenCandidateWorkspace workspace,
                GasCodeGenSelectorCandidateSnapshot candidate,
                GasCodeGenRouteSnapshot route,
                GasCodeGenPackageDescriptorSnapshot descriptor)
            {
                Workspace = workspace;
                Candidate = candidate;
                Route = route;
                Descriptor = descriptor;
            }

            internal GasCodeGenCandidateWorkspace Workspace { get; }
            internal GasCodeGenSelectorCandidateSnapshot Candidate { get; }
            internal GasCodeGenRouteSnapshot Route { get; }
            internal GasCodeGenPackageDescriptorSnapshot Descriptor { get; }

            /// <summary>
            /// 清理 fixture candidate workspace，不删除 project transaction evidence。
            /// </summary>
            public void Dispose()
            {
                Workspace.Dispose();
            }
        }

        /// <summary>
        /// 指定 manifest Save 后对某个 required managed meta 注入的故障类型。
        /// </summary>
        private enum MetaMutation
        {
            None = 0,
            Missing = 1,
            Length = 2,
            Sha = 3,
        }

        /// <summary>
        /// 定义 E1 transaction adapter 的单 case request schema。
        /// </summary>
        private sealed class ScenarioRequest
        {
            public string Schema { get; set; }
            public string RunId { get; set; }
            public string CaseId { get; set; }
            public string InvocationId { get; set; }
            public string Scenario { get; set; }
            public string ProjectRoot { get; set; }
            public string SelectorRelativePath { get; set; }
            public string IntentRelativePath { get; set; }
            public string AuditRelativePath { get; set; }
            public string InitialSelector { get; set; }
            public string Target { get; set; }
            public string SelectorABytesBase64 { get; set; }
            public string SelectorBBytesBase64 { get; set; }
            public string CompetitionSelectorBytesBase64 { get; set; }
            public string FaultPoint { get; set; }
            public string SeedState { get; set; }
            public string CompetitionTarget { get; set; }
            public int RecoveryCount { get; set; }
        }

        /// <summary>
        /// 定义 E1 transaction adapter 的可复核 response schema。
        /// </summary>
        private sealed class ScenarioResponse
        {
            public string Schema { get; set; }
            public string RunId { get; set; }
            public string CaseId { get; set; }
            public string InvocationId { get; set; }
            public bool Passed { get; set; }
            public int AdapterExitCode { get; set; }
            public string ObservedState { get; set; }
            public int FaultInvocationExitCode { get; set; }
            public int[] RecoveryExitCodes { get; set; }
            public bool PromotionClaimed { get; set; }
            public bool IntentFullPreviousSnapshot { get; set; }
            public bool IntentCreatedWithCreateNewAndFlush { get; set; }
            public bool RecoveryStable { get; set; }
            public string Detail { get; set; }
            public string TargetSelectorSha256 { get; set; }
            public string TargetSelectorBytesBase64 { get; set; }
            public RawFileSnapshot IntentBeforeRecovery { get; set; }
            public TransactionSnapshot BeforeRecovery { get; set; }
            public TransactionSnapshot[] RecoverySnapshots { get; set; }
            public TransactionSnapshot Final { get; set; }
            public ProductionClaimWriteSnapshot ProductionClaimWrite { get; set; }
        }

        /// <summary>
        /// 保存一次 publish/seed 的逻辑状态与实际调用 exit，不包含测试期望。
        /// </summary>
        private sealed class ScenarioExecution
        {
            /// <summary>
            /// 创建 scenario invocation 结果。
            /// </summary>
            internal ScenarioExecution(
                string observedState,
                int faultInvocationExitCode,
                string promotionId,
                string detail = "")
            {
                ObservedState = observedState;
                FaultInvocationExitCode = faultInvocationExitCode;
                PromotionId = promotionId;
                Detail = detail;
            }

            internal string ObservedState { get; }
            internal int FaultInvocationExitCode { get; }
            internal string PromotionId { get; }
            internal string Detail { get; }
        }

        /// <summary>
        /// 保存全部 recovery exit、观察文本与每轮磁盘快照。
        /// </summary>
        private sealed class RecoveryExecution
        {
            /// <summary>
            /// 创建 recovery 序列结果。
            /// </summary>
            internal RecoveryExecution(
                IReadOnlyList<int> exitCodes,
                IReadOnlyList<string> observations,
                IReadOnlyList<TransactionSnapshot> snapshots)
            {
                ExitCodes = exitCodes;
                Observations = observations;
                Snapshots = snapshots;
            }

            internal IReadOnlyList<int> ExitCodes { get; }
            internal IReadOnlyList<string> Observations { get; }
            internal IReadOnlyList<TransactionSnapshot> Snapshots { get; }
        }

        /// <summary>
        /// 保存一个事务阶段的 selector/intent/audit 原始文件身份与解析出的状态标识。
        /// </summary>
        private sealed class TransactionSnapshot
        {
            public string Phase { get; set; }
            public int RecoveryIndex { get; set; }
            public RawFileSnapshot Selector { get; set; }
            public RawFileSnapshot Intent { get; set; }
            public RawFileSnapshot Audit { get; set; }
            public string ObservedState { get; set; }
            public string PromotionId { get; set; }

            /// <summary>
            /// 比较 selector/intent/audit SHA 与 PromotionId，证明重复 recovery 没有产生新身份。
            /// </summary>
            internal bool HasSameIdentity(TransactionSnapshot other)
            {
                return other != null
                       && Selector.HasSameIdentity(other.Selector)
                       && Intent.HasSameIdentity(other.Intent)
                       && Audit.HasSameIdentity(other.Audit)
                       && string.Equals(PromotionId, other.PromotionId, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// 保存一个文件的 Missing/Present、完整 byte length/SHA/Base64 快照。
        /// </summary>
        private sealed class RawFileSnapshot
        {
            public string State { get; private set; }
            public long Length { get; private set; }
            public string Sha256 { get; private set; }
            public string BytesBase64 { get; private set; }
            [JsonIgnore] internal bool IsPresent => string.Equals(State, "Present", StringComparison.Ordinal);
            [JsonIgnore] internal byte[] Bytes { get; private set; }

            /// <summary>
            /// 创建不携带 bytes/hash 的 Missing 快照。
            /// </summary>
            internal static RawFileSnapshot Missing()
            {
                return new RawFileSnapshot
                {
                    State = "Missing",
                    Length = 0,
                    Sha256 = string.Empty,
                    BytesBase64 = string.Empty,
                    Bytes = Array.Empty<byte>(),
                };
            }

            /// <summary>
            /// 创建携带完整 bytes/hash/Base64 的 Present 快照。
            /// </summary>
            internal static RawFileSnapshot Present(byte[] bytes, string sha256)
            {
                return new RawFileSnapshot
                {
                    State = "Present",
                    Length = bytes.LongLength,
                    Sha256 = sha256,
                    BytesBase64 = Convert.ToBase64String(bytes),
                    Bytes = (byte[])bytes.Clone(),
                };
            }

            /// <summary>
            /// 比较 state、length 与 SHA，避免在稳定性判断中比较派生文本。
            /// </summary>
            internal bool HasSameIdentity(RawFileSnapshot other)
            {
                return other != null
                       && string.Equals(State, other.State, StringComparison.Ordinal)
                       && Length == other.Length
                       && string.Equals(Sha256, other.Sha256, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// 保存从 intent 或 audit 原始 bytes 解析出的事务状态标识。
        /// </summary>
        private sealed class TransactionIdentity
        {
            /// <summary>
            /// 创建事务状态标识。
            /// </summary>
            internal TransactionIdentity(string state, string promotionId)
            {
                State = state;
                PromotionId = promotionId;
            }

            internal string State { get; }
            internal string PromotionId { get; }
        }

        /// <summary>
        /// 序列化 Store production durable writer 回执，作为外部复核的调用链观察。
        /// </summary>
        private sealed class ProductionClaimWriteSnapshot
        {
            public bool Observed { get; private set; }
            public string FileMode { get; private set; }
            public string FileOptions { get; private set; }
            public bool FlushToDisk { get; private set; }
            public bool BytesVerified { get; private set; }

            /// <summary>
            /// 从 Store fixture trace 创建 response，不允许调用方传入布尔常量冒充回执。
            /// </summary>
            internal static ProductionClaimWriteSnapshot Create(
                GasCodeGenGenerationStoreFixture fixture)
            {
                return new ProductionClaimWriteSnapshot
                {
                    Observed = fixture.IntentClaimWriteObserved,
                    FileMode = fixture.IntentClaimFileMode.ToString(),
                    FileOptions = fixture.IntentClaimFileOptions.ToString(),
                    FlushToDisk = fixture.IntentClaimFlushToDisk,
                    BytesVerified = fixture.IntentClaimBytesVerified,
                };
            }
        }
    }
}
