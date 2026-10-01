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
            var path = Path.GetFullPath(Path.Combine(context.OutputDir, relativePath));
            if (!IsUnderOutputRoot(context.OutputDir, path))
            {
                throw new InvalidOperationException(
                    $"Generated path escapes current output root: {path}");
            }

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
            manifest.AddGeneratedFile(
                phaseName,
                path,
                "Editor",
                false,
                "ValidationArtifact",
                "EditorCi");
        }

        /// <summary>
        /// 登记生成的 asmdef，并明确它只负责程序集边界而不拥有运行时状态。
        /// </summary>
        protected static void AddAssemblyDefinitionManifest(
            GasCodeGenManifest manifest,
            string phaseName,
            string path,
            string layer,
            bool runtimeVisible)
        {
            manifest.AddGeneratedFile(
                phaseName,
                path,
                layer,
                runtimeVisible,
                "AssemblyDefinition",
                "DefinitionCodeGen");
        }

        /// <summary>
        /// 登记仍由演示模块消费的纯托管运行时配置，不允许把 ECS 执行实现写入生成物。
        /// </summary>
        protected static void AddRuntimeManifest(
            GasCodeGenManifest manifest,
            string phaseName,
            string path)
        {
            manifest.AddGeneratedFile(
                phaseName,
                path,
                "Runtime",
                true,
                "RuntimeDemoConfig",
                "AutoChessDemo");
        }

        /// <summary>
        /// 登记不分配、不拥有生命周期或结构变化的纯 Runtime 胶水产物。
        /// </summary>
        protected static void AddRuntimePureGlueManifest(
            GasCodeGenManifest manifest,
            string phaseName,
            string path)
        {
            manifest.AddGeneratedFile(
                phaseName,
                path,
                "Runtime",
                true,
                "RuntimePureGlue",
                "DefinitionCodeGen");
        }

        /// <summary>
        /// 判断生成路径是否仍位于本次 active/candidate 输出根内，避免 phase 穿越写入其它代际。
        /// </summary>
        private static bool IsUnderOutputRoot(string outputRoot, string path)
        {
            var normalizedRoot = Path.GetFullPath(outputRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(path, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith(
                       normalizedRoot + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith(
                       normalizedRoot + Path.AltDirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
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
        private const string GeneratedRuntimeAssembly = "com.exhard.exgas.generated.runtime";
        private const string GeneratedEditorAssembly = "com.exhard.exgas.generated.editor";
        private const string EditorAssembly = "com.exhard.exgas.editor";
        private const string RuntimeAssembly = "com.exhard.exgas.runtime";

        public override string PhaseName => "AssemblyDefinition";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[]
        {
            "Runtime/com.exhard.exgas.generated.runtime.asmdef",
            "Editor/com.exhard.exgas.generated.editor.asmdef",
        };

        public override bool RequiresRows => false;

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var runtimePath = GetOutputPath(context, OutputFileNames[0]);
            var editorPath = GetOutputPath(context, OutputFileNames[1]);
            WriteRuntimeAssemblyDefinition(runtimePath);
            WriteEditorAssemblyDefinition(context, editorPath);
            AddAssemblyDefinitionManifest(
                manifest,
                PhaseName,
                runtimePath,
                "Runtime",
                true);
            AddAssemblyDefinitionManifest(
                manifest,
                PhaseName,
                editorPath,
                "Editor",
                false);
        }

        /// <summary>
        /// 写入只依赖 GAS Runtime 与 DOTS 基础包的 Runtime generated asmdef。
        /// </summary>
        private static void WriteRuntimeAssemblyDefinition(string path)
        {
            var references = new[]
            {
                RuntimeAssembly,
                "Unity.Collections",
                "Unity.Entities",
                "Unity.Burst",
            };

            WriteAssemblyDefinition(
                path,
                GeneratedRuntimeAssembly,
                references,
                Array.Empty<string>());
        }

        private static void WriteEditorAssemblyDefinition(GasCodeGenContext context, string path)
        {
            var references = BuildEditorReferences(context);
            WriteAssemblyDefinition(
                path,
                GeneratedEditorAssembly,
                references,
                new[] { "Editor" });
        }

        /// <summary>
        /// 以统一字段顺序输出 asmdef，保证离线生成与 Unity 导入结果一致。
        /// </summary>
        private static void WriteAssemblyDefinition(
            string path,
            string assemblyName,
            IReadOnlyList<string> references,
            IReadOnlyList<string> includePlatforms)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"\"name\": \"{EscapeJson(assemblyName)}\",");
            writer.WriteLine("\"rootNamespace\": \"\",");
            WriteStringArray(writer, "references", references, trailingComma: true);
            WriteStringArray(writer, "includePlatforms", includePlatforms, trailingComma: true);
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
                GeneratedRuntimeAssembly,
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
                    || assembly == GeneratedRuntimeAssembly
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

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
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
                writer.WriteLine($"\"{EscapeJson(values[i])}\"{suffix}");
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
            "CreateSystem(",
            "RegisterSystem(",
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
            "ComponentLookup<",
            "BufferLookup<",
            "NativeList<",
            "NativeStream",
            "Dictionary<",
            "cfg.",
            "SimpleJSON",
            "DefinitionRow",
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
            var manifestContractErrors = manifest.CollectContractErrors();
            var missingArtifacts = CollectMissingRequiredArtifacts(context, manifest);

            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteReport(
                context,
                manifest,
                legacyArtifacts,
                lifecycleArtifacts,
                worldOwnerArtifacts,
                manifestContractErrors,
                missingArtifacts,
                writer);

            if (legacyArtifacts > 0
                || lifecycleArtifacts > 0
                || worldOwnerArtifacts > 0
                || manifestContractErrors.Count > 0
                || missingArtifacts.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Runtime v1 generated artifact gate failed: legacy={legacyArtifacts}, "
                    + $"lifecycle={lifecycleArtifacts}, worldOwner={worldOwnerArtifacts}, "
                    + $"manifest={manifestContractErrors.Count}, missing={missingArtifacts.Count}.");
            }
        }

        private static void WriteReport(
            GasCodeGenContext context,
            GasCodeGenManifest manifest,
            int legacyArtifacts,
            int lifecycleArtifacts,
            int worldOwnerArtifacts,
            IReadOnlyList<string> manifestContractErrors,
            IReadOnlyList<string> missingArtifacts,
            IndentedWriter writer)
        {
            writer.WriteLine("# GAS CodeGen Validation Report");
            writer.WriteLine(string.Empty);
            writer.WriteLine($"ManifestVersion: `{manifest.ManifestVersion}`");
            writer.WriteLine($"GeneratorVersion: `{manifest.GeneratorVersion}`");
            writer.WriteLine($"InputHash: `{context.InputHash}`");
            writer.WriteLine($"RowCount: `{context.Rows.Count}`");
            writer.WriteLine("OrphanGate: `Clean`");
            writer.WriteLine($"LegacyRuntimeImplementationArtifacts: `{legacyArtifacts}`");
            writer.WriteLine($"RuntimePureGlueArtifacts: `{CountRuntimePureGlue(manifest)}`");
            writer.WriteLine($"RuntimeLifecycleSystemArtifacts: `{lifecycleArtifacts}`");
            writer.WriteLine($"RuntimeWorldOwnerArtifacts: `{worldOwnerArtifacts}`");
            writer.WriteLine($"ManifestContractErrors: `{manifestContractErrors.Count}`");
            writer.WriteLine($"MissingRequiredArtifacts: `{missingArtifacts.Count}`");
            writer.WriteLine(string.Empty);
            writer.WriteLine("## Generation Contract");
            writer.WriteLine(string.Empty);
            writer.WriteLine("- Runtime v1 gameplay owner、Tick DAG、ECB playback 与 Boundary drain 均由手写 Runtime 持有。");
            writer.WriteLine("- CodeGen 只生成 Luban normalized rows、三项 Runtime pure glue marker 与 validation report；stable asmdef 属于 route scaffold。");
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
            writer.WriteLine("| Phase | File | Layer | RuntimeVisible | ArtifactCategory | GeneratedArtifactOwner | MayAllocate | MayOwnLifecycle | MayOwnStructuralChange | MayOwnNativeContainer |");
            writer.WriteLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (var entry in manifest.Entries)
            {
                writer.WriteLine(
                    $"| `{entry.PhaseName}` | `{entry.ProjectRelativePath}` | `{entry.Layer}` | `{entry.RuntimeVisible}` | `{entry.ArtifactCategory}` | `{entry.GeneratedArtifactOwner}` | `{entry.MayAllocate}` | `{entry.MayOwnLifecycle}` | `{entry.MayOwnStructuralChange}` | `{entry.MayOwnNativeContainer}` |");
            }

            if (manifestContractErrors.Count > 0)
            {
                writer.WriteLine(string.Empty);
                writer.WriteLine("## Manifest Contract Errors");
                writer.WriteLine(string.Empty);
                foreach (var error in manifestContractErrors)
                    writer.WriteLine($"- `{error}`");
            }

            if (missingArtifacts.Count > 0)
            {
                writer.WriteLine(string.Empty);
                writer.WriteLine("## Missing Required Artifacts");
                writer.WriteLine(string.Empty);
                foreach (var missing in missingArtifacts)
                    writer.WriteLine($"- `{missing}`");
            }
        }

        private static int CountRuntimePureGlue(GasCodeGenManifest manifest)
        {
            return manifest.Entries.Count(entry =>
                entry.IsRuntimePureGlue);
        }

        private static int CountLegacyRuntimeImplementationArtifacts(
            GasCodeGenContext context,
            GasCodeGenManifest manifest)
        {
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in manifest.Entries)
            {
                if (!entry.RuntimeVisible
                    || !string.Equals(entry.Layer, "Runtime", StringComparison.Ordinal))
                    continue;

                allowed.Add(manifest.ResolvePhysicalPath(entry));
            }

            return EnumerateGeneratedRuntimeArtifacts(context)
                .Count(path => !allowed.Contains(Path.GetFullPath(path)));
        }

        /// <summary>
        /// 检查核心生成链必须存在且登记三项 Runtime pure glue；stable asmdef 不再属于 generation manifest。
        /// </summary>
        private static IReadOnlyList<string> CollectMissingRequiredArtifacts(
            GasCodeGenContext context,
            GasCodeGenManifest manifest)
        {
            var errors = new List<string>();
            var requiredFiles = new[]
            {
                "RuntimeAbilityActivation.gen.cs",
                "RuntimeActiveEffect.gen.cs",
                "RuntimeEffectInstant.gen.cs",
            };
            for (var index = 0; index < requiredFiles.Length; index++)
            {
                var path = Path.GetFullPath(Path.Combine(context.OutputDir, "Runtime", requiredFiles[index]));
                if (!File.Exists(path))
                {
                    errors.Add("Runtime/" + requiredFiles[index] + " 文件不存在。");
                    continue;
                }
                var registered = manifest.Entries.Any(entry =>
                    string.Equals(
                        entry.ProjectRelativePath,
                        manifest.GetPublishedProjectRelativePath(path),
                        StringComparison.Ordinal)
                    && entry.RuntimeVisible
                    && string.Equals(entry.GeneratedArtifactKind, "RuntimePureGlue", StringComparison.Ordinal));
                if (!registered)
                    errors.Add("Runtime/" + requiredFiles[index] + " 未以 RuntimePureGlue 登记。");
            }
            return errors;
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

    }
}
