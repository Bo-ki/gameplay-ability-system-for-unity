using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GAS.Editor
{
    /// <summary>
    /// 提供 CodeGen phase 共用的文件输出与 manifest 登记能力；不生成 Runtime lifecycle 或 ECS owner。
    /// </summary>
    internal abstract class GasCodeGenPhaseBase : IGasCodeGenPhase
    {
        public abstract string PhaseName { get; }

        public abstract IReadOnlyList<string> OutputFileNames { get; }

        public virtual bool RequiresRows => true;

        public abstract void Execute(GasCodeGenContext context, GasCodeGenManifest manifest);

        protected static string GetOutputPath(GasCodeGenContext context, string relativePath)
        {
            var path = Path.Combine(context.OutputDir, relativePath);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            return path;
        }

        protected static void AddEditorManifest(
            GasCodeGenManifest manifest,
            string phaseName,
            string path)
        {
            manifest.AddGeneratedFile(phaseName, path, "Editor", false);
        }

        /// <summary>
        /// 登记仍由演示模块消费的纯托管运行时配置，不允许把 ECS 执行实现写入生成物。
        /// </summary>
        protected static void AddRuntimeManifest(
            GasCodeGenManifest manifest,
            string phaseName,
            string path)
        {
            manifest.AddGeneratedFile(phaseName, path, "Runtime", true, "RuntimePureGlue");
        }

        protected static void AddRuntimePureGlueManifest(
            GasCodeGenManifest manifest,
            string phaseName,
            string path)
        {
            manifest.AddGeneratedFile(phaseName, path, "Runtime", true, "RuntimePureGlue");
        }

        protected static void WriteHeader(IndentedWriter writer)
        {
            writer.WriteLine("///////////////////////////////////");
            writer.WriteLine("//// This is a generated file. ////");
            writer.WriteLine("////     Do not modify it.     ////");
            writer.WriteLine("///////////////////////////////////");
            writer.WriteLine(string.Empty);
        }
    }

    /// <summary>
    /// 生成仅供 Editor/CodeGen 使用的 asmdef，避免 Runtime 产物形成第二条定义执行链。
    /// </summary>
    internal sealed class AssemblyDefinitionPhase : GasCodeGenPhaseBase
    {
        private const string GeneratedEditorAssembly = "com.exhard.exgas.generated.editor";
        private const string EditorAssembly = "com.exhard.exgas.editor";
        private const string RuntimeAssembly = "com.exhard.exgas.runtime";

        public override string PhaseName => "AssemblyDefinition";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[]
        {
            "Editor/com.exhard.exgas.generated.editor.asmdef",
        };

        public override bool RequiresRows => false;

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var path = GetOutputPath(context, OutputFileNames[0]);
            WriteEditorAssemblyDefinition(context, path);
            AddEditorManifest(manifest, PhaseName, path);
        }

        private static void WriteEditorAssemblyDefinition(GasCodeGenContext context, string path)
        {
            var references = BuildEditorReferences(context);
            using var writer = new IndentedWriter(new StreamWriter(path));
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"\"name\": \"{GeneratedEditorAssembly}\",");
            writer.WriteLine("\"rootNamespace\": \"\",");
            WriteStringArray(writer, "references", references, trailingComma: true);
            writer.WriteLine("\"includePlatforms\": [\"Editor\"],");
            writer.WriteLine("\"excludePlatforms\": [],");
            writer.WriteLine("\"allowUnsafeCode\": false,");
            writer.WriteLine("\"overrideReferences\": false,");
            writer.WriteLine("\"precompiledReferences\": [],");
            writer.WriteLine("\"autoReferenced\": true,");
            writer.WriteLine("\"defineConstraints\": [],");
            writer.WriteLine("\"versionDefines\": [],");
            writer.WriteLine("\"noEngineReferences\": false");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static IReadOnlyList<string> BuildEditorReferences(GasCodeGenContext context)
        {
            var references = new SortedSet<string>(StringComparer.Ordinal)
            {
                EditorAssembly,
                RuntimeAssembly,
                "Unity.Collections",
                "Unity.Entities",
                "Unity.Entities.Hybrid",
            };

            foreach (var row in context.Rows)
            {
                var assembly = row.RowType.Assembly.GetName().Name;
                if (string.IsNullOrWhiteSpace(assembly)
                    || assembly == GeneratedEditorAssembly
                    || assembly == "Assembly-CSharp"
                    || assembly == "Assembly-CSharp-firstpass"
                    || assembly == "GasCodeGenCli")
                {
                    continue;
                }

                references.Add(assembly);
            }

            return references.ToArray();
        }

        private static void WriteStringArray(
            IndentedWriter writer,
            string name,
            IReadOnlyList<string> values,
            bool trailingComma)
        {
            writer.WriteLine($"\"{name}\": [");
            writer.Indent++;
            for (var i = 0; i < values.Count; i++)
            {
                var suffix = i == values.Count - 1 ? string.Empty : ",";
                writer.WriteLine($"\"{values[i]}\"{suffix}");
            }

            writer.Indent--;
            writer.WriteLine(trailingComma ? "]," : "]");
        }
    }

    /// <summary>
    /// 生成 Runtime v1 已由手写 owner 接管的 marker，防止旧生命周期代码生成回流。
    /// </summary>
    internal sealed class RuntimeLifecycleMigrationPhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "RuntimeLifecycleMigration";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[]
        {
            "Runtime/RuntimeAbilityActivation.gen.cs",
            "Runtime/RuntimeEffectInstant.gen.cs",
            "Runtime/RuntimeActiveEffect.gen.cs",
        };

        public override bool RequiresRows => false;

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            WriteMarker(context, GetOutputPath(context, OutputFileNames[0]), "GASGeneratedAbilityActivationRuntimeMarker");
            WriteMarker(context, GetOutputPath(context, OutputFileNames[1]), "GASGeneratedEffectInstantRuntimeMarker");
            WriteMarker(context, GetOutputPath(context, OutputFileNames[2]), "GASGeneratedActiveEffectRuntimeMarker");

            for (var i = 0; i < OutputFileNames.Count; i++)
            {
                AddRuntimePureGlueManifest(
                    manifest,
                    PhaseName,
                    GetOutputPath(context, OutputFileNames[i]));
            }
        }

        private static void WriteMarker(GasCodeGenContext context, string path, string typeName)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// Runtime v1 的生命周期 owner 已由手写 Tick DAG 接管。");
            writer.WriteLine("/// </summary>");
            writer.WriteLine($"public static class {typeName}");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public const bool HandwrittenRuntimeOwner = true;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }
    }

    /// <summary>
    /// 输出当前生成输入与产物清单，作为 Editor/CI 的静态对账证据。
    /// </summary>
    internal sealed class ValidationReportPhase : GasCodeGenPhaseBase
    {
        private static readonly string[] RuntimeLifecycleTokens =
        {
            "ISystem",
            "SystemBase",
            "IJob",
            "IJobChunk",
            "UpdateInGroup",
            "OnUpdate(",
            "SystemAPI.",
            "EntityCommandBuffer",
        };

        private static readonly string[] RuntimeWorldOwnerTokens =
        {
            "EntityManager",
            "GetOrCreateSystem",
            "WorldUpdateAllocator",
            "GasRuntimeWorldOwner",
            "EntityCommandBuffer",
            "CreateEntity(",
            "DefaultGameObjectInjectionWorld",
        };

        public override string PhaseName => "ValidationReport";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[]
        {
            "GasCodeGenValidationReport.md",
        };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var path = GetOutputPath(context, OutputFileNames[0]);
            AddEditorManifest(manifest, PhaseName, path);

            var legacyArtifacts = CountLegacyRuntimeImplementationArtifacts(context, manifest);
            var lifecycleArtifacts = CountRuntimeArtifactsContaining(context, RuntimeLifecycleTokens);
            var worldOwnerArtifacts = CountRuntimeArtifactsContaining(context, RuntimeWorldOwnerTokens);

            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteReport(
                context,
                manifest,
                legacyArtifacts,
                lifecycleArtifacts,
                worldOwnerArtifacts,
                writer);

            if (legacyArtifacts > 0 || lifecycleArtifacts > 0 || worldOwnerArtifacts > 0)
            {
                throw new InvalidOperationException(
                    $"Runtime v1 generated artifact gate failed: legacy={legacyArtifacts}, "
                    + $"lifecycle={lifecycleArtifacts}, worldOwner={worldOwnerArtifacts}.");
            }
        }

        private static void WriteReport(
            GasCodeGenContext context,
            GasCodeGenManifest manifest,
            int legacyArtifacts,
            int lifecycleArtifacts,
            int worldOwnerArtifacts,
            IndentedWriter writer)
        {
            writer.WriteLine("# GAS CodeGen Validation Report");
            writer.WriteLine(string.Empty);
            writer.WriteLine($"InputHash: `{context.InputHash}`");
            writer.WriteLine($"RowCount: `{context.Rows.Count}`");
            writer.WriteLine($"OrphansDeleted: `{context.OrphansDeleted}`");
            writer.WriteLine($"LegacyRuntimeImplementationArtifacts: `{legacyArtifacts}`");
            writer.WriteLine($"RuntimePureGlueArtifacts: `{CountRuntimePureGlue(manifest)}`");
            writer.WriteLine($"RuntimeLifecycleSystemArtifacts: `{lifecycleArtifacts}`");
            writer.WriteLine($"RuntimeWorldOwnerArtifacts: `{worldOwnerArtifacts}`");
            writer.WriteLine(string.Empty);
            writer.WriteLine("## Generation Contract");
            writer.WriteLine(string.Empty);
            writer.WriteLine("- Runtime v1 gameplay owner、Tick DAG、ECB playback 与 Boundary drain 均由手写 Runtime 持有。");
            writer.WriteLine("- CodeGen 只保留 Luban normalized rows、Editor asmdef、生命周期防回流 marker 与 validation report。");
            writer.WriteLine("- 旧 catalog、lookup、Baker、SystemGroup 与 Runtime lifecycle 产物不再生成。");
            writer.WriteLine(string.Empty);
            writer.WriteLine("## Rows");
            writer.WriteLine(string.Empty);
            writer.WriteLine("| Row | DefinitionKind | CodeField | SourceRows |");
            writer.WriteLine("| --- | --- | --- | ---: |");
            foreach (var row in context.Rows)
            {
                writer.WriteLine(
                    $"| `{row.RowType.FullName}` | `{row.DefinitionKind}` | `{row.CodeFieldName}` | `{row.RowValues?.Count ?? 0}` |");
            }

            writer.WriteLine(string.Empty);
            writer.WriteLine("## Manifest Entries");
            writer.WriteLine(string.Empty);
            writer.WriteLine("| Phase | File | Layer | RuntimeVisible | ArtifactCategory |");
            writer.WriteLine("| --- | --- | --- | --- | --- |");
            foreach (var entry in manifest.Entries)
            {
                writer.WriteLine(
                    $"| `{entry.PhaseName}` | `{entry.ProjectRelativePath}` | `{entry.Layer}` | `{entry.RuntimeVisible}` | `{entry.ArtifactCategory}` |");
            }
        }

        private static int CountRuntimePureGlue(GasCodeGenManifest manifest)
        {
            return manifest.Entries.Count(entry =>
                string.Equals(entry.ArtifactCategory, "RuntimePureGlue", StringComparison.Ordinal));
        }

        private static int CountLegacyRuntimeImplementationArtifacts(
            GasCodeGenContext context,
            GasCodeGenManifest manifest)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in manifest.Entries)
            {
                if (!entry.RuntimeVisible
                    || !string.Equals(entry.Layer, "Runtime", StringComparison.Ordinal)
                    || !string.Equals(entry.ArtifactCategory, "RuntimePureGlue", StringComparison.Ordinal))
                    continue;

                allowed.Add(ResolveProjectPath(context, entry.ProjectRelativePath));
            }

            return EnumerateGeneratedRuntimeArtifacts(context)
                .Count(path => !allowed.Contains(Path.GetFullPath(path)));
        }

        private static int CountRuntimeArtifactsContaining(
            GasCodeGenContext context,
            IReadOnlyList<string> tokens)
        {
            var count = 0;
            foreach (var path in EnumerateGeneratedRuntimeArtifacts(context))
            {
                if (FileContainsAny(path, tokens))
                    count++;
            }

            return count;
        }

        private static bool FileContainsAny(string path, IReadOnlyList<string> tokens)
        {
            foreach (var line in File.ReadLines(path))
            {
                for (var i = 0; i < tokens.Count; i++)
                {
                    if (line.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateGeneratedRuntimeArtifacts(GasCodeGenContext context)
        {
            var runtimeRoot = Path.Combine(context.OutputDir, "Runtime");
            if (!Directory.Exists(runtimeRoot))
                return Array.Empty<string>();

            return Directory.GetFiles(runtimeRoot, "*", SearchOption.AllDirectories)
                .Where(path =>
                    string.Equals(Path.GetExtension(path), ".cs", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetExtension(path), ".asmdef", StringComparison.OrdinalIgnoreCase));
        }

        private static string ResolveProjectPath(GasCodeGenContext context, string path)
        {
            return Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(context.ProjectRoot, path.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
