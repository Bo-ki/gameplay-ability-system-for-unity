using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Gas.CodeGen.SourceGenerator.Tests
{
    /// <summary>
    /// 以 Roslyn 4.3.1 GeneratorDriver 验证三目标分发、marker ABI 与 AddSource 前物理身份门。
    /// </summary>
    internal static class GeneratorTests
    {
        /// <summary>
        /// 返回 D1-A generator 固定 case 集。
        /// </summary>
        internal static IReadOnlyList<TestCase> GetCases()
        {
            return new[]
            {
                new TestCase("GEN-01 runtime exact distribution and marker", RuntimeDistribution),
                new TestCase("GEN-02 editor exact distribution and marker", EditorDistribution),
                new TestCase("GEN-03 AutoChess exact distribution and marker", AutoChessDistribution),
                new TestCase("GEN-04 non-target assembly no-op", NonTargetNoOp),
                new TestCase("GEN-05 missing selector fails before AddSource", MissingSelectorFails),
                new TestCase("GEN-06 duplicate selector fails before AddSource", DuplicateSelectorFails),
                new TestCase("GEN-07 raw and AdditionalText mismatch fails", SnapshotMismatchFails),
                new TestCase("GEN-08 analyzer drift fails before AddSource", AnalyzerDriftFails),
                new TestCase("GEN-09 scaffold drift fails before AddSource", ScaffoldDriftFails),
                new TestCase("GEN-10 corrupt selector fails before AddSource", CorruptSelectorFails),
                new TestCase("GEN-11 legacy active source fails before AddSource", LegacyActiveSourceFails),
            };
        }

        /// <summary>
        /// 验证 runtime compilation 仅得到三项 runtime source 与固定 marker。
        /// </summary>
        private static void RuntimeDistribution()
        {
            AssertSuccessfulDistribution(
                GasCodeGenContract.RuntimeAssembly,
                new[]
                {
                    "RuntimeAbilityActivation.gen.cs",
                    "RuntimeActiveEffect.gen.cs",
                    "RuntimeEffectInstant.gen.cs",
                    GasCodeGenContract.MarkerHintName,
                });
        }

        /// <summary>
        /// 验证 editor compilation 仅得到 Luban source 与固定 marker。
        /// </summary>
        private static void EditorDistribution()
        {
            AssertSuccessfulDistribution(
                GasCodeGenContract.EditorAssembly,
                new[] { "LubanNormalizedRows.gen.cs", GasCodeGenContract.MarkerHintName });
        }

        /// <summary>
        /// 验证 AutoChess compilation 保持既有 assembly owner 且只得到自身 source 与 marker。
        /// </summary>
        private static void AutoChessDistribution()
        {
            AssertSuccessfulDistribution(
                GasCodeGenContract.AutoChessAssembly,
                new[] { "AutoChessGeneratedConfig.gen.cs", GasCodeGenContract.MarkerHintName });
        }

        /// <summary>
        /// 验证非目标 assembly 即使没有 selector 也不产生输出或诊断。
        /// </summary>
        private static void NonTargetNoOp()
        {
            GeneratorOutcome outcome = RunGenerator("unrelated.assembly", Array.Empty<AdditionalText>());
            TestAssert.Equal(0, outcome.GeneratedSources.Count, "Non-target assembly received generated sources.");
            TestAssert.Equal(0, outcome.GeneratorErrors.Count, "Non-target assembly received generator errors.");
        }

        /// <summary>
        /// 验证目标 assembly 缺 selector 时报告 GASGEN001 且零 AddSource。
        /// </summary>
        private static void MissingSelectorFails()
        {
            GeneratorOutcome outcome = RunGenerator(GasCodeGenContract.RuntimeAssembly, Array.Empty<AdditionalText>());
            AssertFailure(outcome, GasCodeGenSourceGenerator.MissingSelectorDiagnosticId);
        }

        /// <summary>
        /// 验证同一 compilation 出现两个 exact selector 时报告 GASGEN002 且零 AddSource。
        /// </summary>
        private static void DuplicateSelectorFails()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                fixture.WriteSelector();
                AdditionalText first = new DiskAdditionalText(fixture.SelectorPath);
                AdditionalText second = new DiskAdditionalText(fixture.SelectorPath);
                GeneratorOutcome outcome = RunGenerator(
                    GasCodeGenContract.RuntimeAssembly,
                    new[] { first, second });
                AssertFailure(outcome, GasCodeGenSourceGenerator.DuplicateSelectorDiagnosticId);
            }
        }

        /// <summary>
        /// 验证 raw bytes 与 Roslyn SourceText 不一致时报告 GASGEN003 且零 AddSource。
        /// </summary>
        private static void SnapshotMismatchFails()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                byte[] selector = fixture.WriteSelector();
                string mismatchedText = Encoding.UTF8.GetString(selector) + " ";
                AdditionalText additionalText = new DiskAdditionalText(fixture.SelectorPath, mismatchedText);
                GeneratorOutcome outcome = RunGenerator(
                    GasCodeGenContract.RuntimeAssembly,
                    new[] { additionalText });
                AssertFailure(outcome, GasCodeGenSourceGenerator.InvalidSelectorDiagnosticId);
            }
        }

        /// <summary>
        /// 验证 selector 封存后 analyzer DLL 漂移会报告 GASGEN004 且零 AddSource。
        /// </summary>
        private static void AnalyzerDriftFails()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                fixture.WriteSelector();
                File.AppendAllText(
                    fixture.GetProjectPath(GasCodeGenContract.AnalyzerRelativePath),
                    "drift",
                    Encoding.UTF8);
                GeneratorOutcome outcome = RunFixtureGenerator(fixture, GasCodeGenContract.RuntimeAssembly);
                AssertFailure(outcome, GasCodeGenSourceGenerator.AnalyzerIdentityDiagnosticId);
            }
        }

        /// <summary>
        /// 验证 selector 封存后任一 scaffold bytes 漂移会报告 GASGEN005 且零 AddSource。
        /// </summary>
        private static void ScaffoldDriftFails()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                fixture.WriteSelector();
                string path = fixture.GetProjectPath(GasCodeGenContract.GetRouteScaffoldPaths()[0]);
                File.AppendAllText(path, "drift", Encoding.UTF8);
                GeneratorOutcome outcome = RunFixtureGenerator(fixture, GasCodeGenContract.RuntimeAssembly);
                AssertFailure(outcome, GasCodeGenSourceGenerator.ScaffoldIdentityDiagnosticId);
            }
        }

        /// <summary>
        /// 验证 corrupt canonical selector 报告 GASGEN003 且零 AddSource。
        /// </summary>
        private static void CorruptSelectorFails()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                fixture.WriteSelector();
                File.WriteAllText(fixture.SelectorPath, "EX-GAS-SourceSelector-v1\nnot-base64\n", new UTF8Encoding(false));
                GeneratorOutcome outcome = RunFixtureGenerator(fixture, GasCodeGenContract.RuntimeAssembly);
                AssertFailure(outcome, GasCodeGenSourceGenerator.InvalidSelectorDiagnosticId);
            }
        }

        /// <summary>
        /// 验证任一 required `.gen.cs` 物理残留报告 GASGEN007 且零 AddSource。
        /// </summary>
        private static void LegacyActiveSourceFails()
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                fixture.WriteSelector();
                RequiredArtifactDefinition legacy = GasCodeGenContract.GetRequiredArtifacts()
                    .First(item => item.IsSourceArtifact);
                string legacyPath = fixture.GetProjectPath(legacy.CanonicalPath);
                Directory.CreateDirectory(Path.GetDirectoryName(legacyPath));
                File.WriteAllBytes(legacyPath, D1CodecFixture.CreateSource(legacy.HintName));
                GeneratorOutcome outcome = RunFixtureGenerator(fixture, GasCodeGenContract.RuntimeAssembly);
                AssertFailure(outcome, GasCodeGenSourceGenerator.LegacyActiveSourceDiagnosticId);
            }
        }

        /// <summary>
        /// 对指定目标 assembly 断言 exact hints、零 generator error 与 marker 四常量 ABI。
        /// </summary>
        private static void AssertSuccessfulDistribution(string assemblyName, IReadOnlyList<string> expectedHints)
        {
            using (D1CodecFixture fixture = new D1CodecFixture())
            {
                GasSourceBundle bundle = fixture.CreateBundle();
                byte[] selector = fixture.WriteSelector(bundle);
                GeneratorOutcome outcome = RunFixtureGenerator(fixture, assemblyName);
                TestAssert.Equal(0, outcome.GeneratorErrors.Count, "Generator reported an unexpected error.");
                TestAssert.Equal(0, outcome.CompilationErrors.Count, "Generated output did not compile cleanly.");
                string[] actualHints = outcome.GeneratedSources.Keys.OrderBy(item => item, StringComparer.Ordinal).ToArray();
                string[] sortedExpected = expectedHints.OrderBy(item => item, StringComparer.Ordinal).ToArray();
                TestAssert.Equal(string.Join("|", sortedExpected), string.Join("|", actualHints),
                    "Generated hint distribution drifted.");
                string marker = outcome.GeneratedSources[GasCodeGenContract.MarkerHintName];
                AssertMarker(marker, assemblyName, selector, bundle);
            }
        }

        /// <summary>
        /// 断言 marker 含精确 namespace/type 与四个 internal const string 身份。
        /// </summary>
        private static void AssertMarker(
            string marker,
            string assemblyName,
            byte[] selector,
            GasSourceBundle bundle)
        {
            TestAssert.True(marker.Contains("namespace GAS.Generated.CodeGen"), "Marker namespace drifted.");
            TestAssert.True(marker.Contains("internal static class GasCodeGenSourceGeneratorMarker"), "Marker type drifted.");
            TestAssert.True(marker.Contains("TargetAssembly = \"" + assemblyName + "\""), "Marker assembly drifted.");
            TestAssert.True(marker.Contains(
                "SelectorSha256 = \"" + GasSourceBundleCodec.ComputeSelectorSha256Hex(selector) + "\""),
                "Marker selector hash drifted.");
            TestAssert.True(marker.Contains(
                "ArtifactManifestHash = \"" + GasSourceBundleCodec.ToLowerHex(bundle.ArtifactManifestHash) + "\""),
                "Marker manifest hash drifted.");
            TestAssert.True(marker.Contains(
                "SourceArtifactInventoryHash = \""
                + GasSourceBundleCodec.ToLowerHex(bundle.SourceArtifactInventoryHash) + "\""),
                "Marker inventory hash drifted.");
        }

        /// <summary>
        /// 在 fixture canonical selector 上运行 generator。
        /// </summary>
        private static GeneratorOutcome RunFixtureGenerator(D1CodecFixture fixture, string assemblyName)
        {
            return RunGenerator(assemblyName, new[] { new DiskAdditionalText(fixture.SelectorPath) });
        }

        /// <summary>
        /// 用 Roslyn 4.3.1 driver 执行单个 compilation，并收集生成结果和 error diagnostics。
        /// </summary>
        private static GeneratorOutcome RunGenerator(string assemblyName, IReadOnlyList<AdditionalText> additionalTexts)
        {
            CSharpParseOptions parseOptions = new CSharpParseOptions(LanguageVersion.CSharp9);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(
                "namespace Host { internal static class Anchor { } }",
                parseOptions,
                path: "Assets/Host/Anchor.cs");
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                ImmutableArray.Create<ISourceGenerator>(new GasCodeGenSourceGenerator()),
                additionalTexts.ToImmutableArray(),
                parseOptions,
                null);
            Compilation outputCompilation;
            ImmutableArray<Diagnostic> driverDiagnostics;
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out outputCompilation, out driverDiagnostics);
            return GeneratorOutcome.From(driver.GetRunResult(), driverDiagnostics, outputCompilation);
        }

        /// <summary>
        /// 断言指定 diagnostic ID 存在且失败路径没有任何 AddSource 投影。
        /// </summary>
        private static void AssertFailure(GeneratorOutcome outcome, string expectedDiagnosticId)
        {
            TestAssert.True(outcome.GeneratorErrors.Contains(expectedDiagnosticId),
                "Expected generator diagnostic was not reported: " + expectedDiagnosticId);
            TestAssert.Equal(0, outcome.GeneratedSources.Count, "Failure path reached AddSource.");
        }
    }

    /// <summary>
    /// 从磁盘读取 selector raw text，并可注入与 raw bytes 不一致的 Roslyn snapshot。
    /// </summary>
    internal sealed class DiskAdditionalText : AdditionalText
    {
        private readonly string overrideText;

        /// <summary>
        /// 创建指向指定 canonical selector path 的 AdditionalText。
        /// </summary>
        internal DiskAdditionalText(string path, string overrideText = null)
        {
            Path = path;
            this.overrideText = overrideText;
        }

        public override string Path { get; }

        /// <summary>
        /// 返回当前磁盘文本或测试显式注入的 snapshot。
        /// </summary>
        public override SourceText GetText(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string text = overrideText ?? File.ReadAllText(Path, new UTF8Encoding(false, true));
            return SourceText.From(text, new UTF8Encoding(false));
        }
    }

    /// <summary>
    /// 汇总一次 GeneratorDriver 执行的 hints、generator errors 与 output compilation errors。
    /// </summary>
    internal sealed class GeneratorOutcome
    {
        /// <summary>
        /// 创建不可变测试结果。
        /// </summary>
        private GeneratorOutcome(
            IReadOnlyDictionary<string, string> generatedSources,
            IReadOnlyList<string> generatorErrors,
            IReadOnlyList<string> compilationErrors)
        {
            GeneratedSources = generatedSources;
            GeneratorErrors = generatorErrors;
            CompilationErrors = compilationErrors;
        }

        internal IReadOnlyDictionary<string, string> GeneratedSources { get; private set; }
        internal IReadOnlyList<string> GeneratorErrors { get; private set; }
        internal IReadOnlyList<string> CompilationErrors { get; private set; }

        /// <summary>
        /// 从 Roslyn run result 收集单 generator 的生成文本和去重 error IDs。
        /// </summary>
        internal static GeneratorOutcome From(
            GeneratorDriverRunResult runResult,
            ImmutableArray<Diagnostic> driverDiagnostics,
            Compilation outputCompilation)
        {
            Dictionary<string, string> sources = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (GeneratedSourceResult generated in runResult.Results[0].GeneratedSources)
            {
                sources.Add(generated.HintName, generated.SourceText.ToString());
            }

            string[] generatorErrors = driverDiagnostics.Concat(runResult.Diagnostics)
                .Where(item => item.Severity == DiagnosticSeverity.Error)
                .Select(item => item.Id)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            string[] compilationErrors = outputCompilation.GetDiagnostics()
                .Where(item => item.Severity == DiagnosticSeverity.Error)
                .Select(item => item.Id)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            return new GeneratorOutcome(sources, generatorErrors, compilationErrors);
        }
    }
}
