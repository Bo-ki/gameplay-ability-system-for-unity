using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Gas.CodeGen.SourceGenerator
{
    /// <summary>
    /// 验证唯一 production selector 与 immutable route identity，并把冻结 C# bytes 只分发到其目标程序集。
    /// </summary>
    [Generator]
    public sealed class GasCodeGenSourceGenerator : ISourceGenerator
    {
        public const string MissingSelectorDiagnosticId = "GASGEN001";
        public const string DuplicateSelectorDiagnosticId = "GASGEN002";
        public const string InvalidSelectorDiagnosticId = "GASGEN003";
        public const string AnalyzerIdentityDiagnosticId = "GASGEN004";
        public const string ScaffoldIdentityDiagnosticId = "GASGEN005";
        public const string UnexpectedFailureDiagnosticId = "GASGEN006";
        public const string LegacyActiveSourceDiagnosticId = "GASGEN007";

        private static readonly DiagnosticDescriptor MissingSelector = CreateDiagnostic(
            MissingSelectorDiagnosticId, "GAS production selector is missing");
        private static readonly DiagnosticDescriptor DuplicateSelector = CreateDiagnostic(
            DuplicateSelectorDiagnosticId, "GAS production selector is duplicated");
        private static readonly DiagnosticDescriptor InvalidSelector = CreateDiagnostic(
            InvalidSelectorDiagnosticId, "GAS production selector is invalid");
        private static readonly DiagnosticDescriptor AnalyzerIdentity = CreateDiagnostic(
            AnalyzerIdentityDiagnosticId, "GAS analyzer identity is invalid");
        private static readonly DiagnosticDescriptor ScaffoldIdentity = CreateDiagnostic(
            ScaffoldIdentityDiagnosticId, "GAS route scaffold identity is invalid");
        private static readonly DiagnosticDescriptor UnexpectedFailure = CreateDiagnostic(
            UnexpectedFailureDiagnosticId, "GAS source generator failed closed");
        private static readonly DiagnosticDescriptor LegacyActiveSource = CreateDiagnostic(
            LegacyActiveSourceDiagnosticId, "Legacy active GAS generated source is forbidden");

        /// <summary>
        /// 本 generator 不注册语法接收器；唯一 generation authority 只来自 canonical additional-file。
        /// </summary>
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        /// <summary>
        /// 对目标程序集完成 selector、analyzer、scaffold 全校验后，才一次性提交 required source 与 marker。
        /// </summary>
        public void Execute(GeneratorExecutionContext context)
        {
            string assemblyName = context.Compilation.AssemblyName ?? string.Empty;
            if (!GasCodeGenContract.IsTargetAssembly(assemblyName))
            {
                return;
            }

            try
            {
                AdditionalText selector = FindUniqueSelector(context);
                if (selector == null)
                {
                    return;
                }

                byte[] selectorBytes;
                if (!TryReadExactSelectorSnapshot(context, selector, out selectorBytes))
                {
                    return;
                }

                GasSourceBundle bundle;
                if (!TryDecodeSelector(context, selectorBytes, out bundle))
                {
                    return;
                }

                string projectRoot;
                if (!TryValidateRouteIdentity(context, selector.Path, bundle, out projectRoot)
                    || !TryRejectLegacyActiveSources(context, projectRoot)
                    || !TryRevalidateSelectorSnapshot(context, selector.Path, selectorBytes))
                {
                    return;
                }

                AddValidatedSources(context, assemblyName, selectorBytes, bundle);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                ReportError(context, UnexpectedFailure,
                    "Unexpected fail-closed error: " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        /// <summary>
        /// 查找唯一大小写精确的 selector filename，并把 case collision 视为重复或非法输入。
        /// </summary>
        private static AdditionalText FindUniqueSelector(GeneratorExecutionContext context)
        {
            AdditionalText selected = null;
            int insensitiveMatches = 0;
            foreach (AdditionalText candidate in context.AdditionalFiles)
            {
                string fileName = Path.GetFileName(candidate.Path);
                if (!string.Equals(fileName, GasCodeGenContract.SelectorFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                insensitiveMatches++;
                if (string.Equals(fileName, GasCodeGenContract.SelectorFileName, StringComparison.Ordinal))
                {
                    selected = candidate;
                }
            }

            if (insensitiveMatches == 0)
            {
                ReportError(context, MissingSelector, "The exact production selector did not reach this target compilation.");
                return null;
            }

            if (insensitiveMatches != 1)
            {
                ReportError(context, DuplicateSelector, "More than one case-insensitive production selector reached this compilation.");
                return null;
            }

            if (selected == null)
            {
                ReportError(context, InvalidSelector, "The production selector filename casing is not canonical.");
            }

            return selected;
        }

        /// <summary>
        /// 同时读取文件 raw bytes 与 Roslyn snapshot，并要求 UTF-8 no-BOM bytes 逐 byte 相同。
        /// </summary>
        private static bool TryReadExactSelectorSnapshot(
            GeneratorExecutionContext context,
            AdditionalText selector,
            out byte[] selectorBytes)
        {
            selectorBytes = null;
            try
            {
                GasRouteScaffoldIdentity.ValidateSelectorFile(selector.Path);
                SourceText snapshot = selector.GetText(context.CancellationToken);
                selectorBytes = File.ReadAllBytes(selector.Path);
                if (snapshot == null)
                {
                    ReportError(context, InvalidSelector, "Roslyn returned no selector SourceText snapshot.");
                    return false;
                }

                byte[] snapshotBytes = new UTF8Encoding(false, true).GetBytes(snapshot.ToString());
                if (!GasSourceBundleCodec.BytesEqual(selectorBytes, snapshotBytes))
                {
                    ReportError(context, InvalidSelector,
                        "Selector raw bytes do not match AdditionalText.GetText().ToString().");
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                ReportError(context, InvalidSelector,
                    "Selector snapshot could not be read: " + exception.GetType().Name + ": " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// 调用唯一严格 codec，并把任何 wire/contract 偏差映射为稳定 error diagnostic。
        /// </summary>
        private static bool TryDecodeSelector(
            GeneratorExecutionContext context,
            byte[] selectorBytes,
            out GasSourceBundle bundle)
        {
            bundle = null;
            try
            {
                bundle = GasSourceBundleCodec.DecodeSelector(selectorBytes);
                return true;
            }
            catch (Exception exception)
            {
                ReportError(context, InvalidSelector,
                    "Selector bundle validation failed: " + exception.GetType().Name + ": " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// 从 selector suffix 反算工程根，并在 AddSource 前分别重算 analyzer 与 14 项 scaffold hash。
        /// </summary>
        private static bool TryValidateRouteIdentity(
            GeneratorExecutionContext context,
            string selectorPath,
            GasSourceBundle bundle,
            out string projectRoot)
        {
            projectRoot = string.Empty;
            try
            {
                projectRoot = GasRouteScaffoldIdentity.ResolveProjectRoot(selectorPath);
            }
            catch (Exception exception)
            {
                ReportError(context, InvalidSelector,
                    "Selector path is outside the frozen production layout: " + exception.Message);
                return false;
            }

            if (!TryValidateAnalyzer(context, projectRoot, bundle)
                || !TryValidateScaffold(context, projectRoot, bundle))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 拒绝五项 required C# canonical path 的物理残留或 stale SyntaxTree，避免 legacy source 成为第二 active authority。
        /// </summary>
        private static bool TryRejectLegacyActiveSources(
            GeneratorExecutionContext context,
            string projectRoot)
        {
            foreach (RequiredArtifactDefinition item in GasCodeGenContract.GetRequiredArtifacts())
            {
                if (!item.IsSourceArtifact)
                {
                    continue;
                }

                string expectedPath = Path.GetFullPath(Path.Combine(
                    projectRoot,
                    item.CanonicalPath.Replace('/', Path.DirectorySeparatorChar)));
                if (HasCaseInsensitivePhysicalFile(expectedPath)
                    || CompilationContainsPath(context, expectedPath))
                {
                    ReportError(context, LegacyActiveSource,
                        "Legacy active generated source is present: " + item.CanonicalPath);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 检查 canonical parent 下是否存在同名或仅大小写不同的 legacy source 文件。
        /// </summary>
        private static bool HasCaseInsensitivePhysicalFile(string expectedPath)
        {
            string parent = Path.GetDirectoryName(expectedPath);
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
            {
                return false;
            }

            string expectedName = Path.GetFileName(expectedPath);
            foreach (string file in Directory.EnumerateFiles(parent))
            {
                if (string.Equals(Path.GetFileName(file), expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查 compilation 是否仍消费已从磁盘删除但被 stale graph 保留的 legacy source path。
        /// </summary>
        private static bool CompilationContainsPath(
            GeneratorExecutionContext context,
            string expectedPath)
        {
            foreach (SyntaxTree syntaxTree in context.Compilation.SyntaxTrees)
            {
                string candidatePath = syntaxTree.FilePath ?? string.Empty;
                if (!Path.IsPathRooted(candidatePath))
                {
                    continue;
                }

                string absoluteCandidate = Path.GetFullPath(candidatePath);
                if (string.Equals(absoluteCandidate, expectedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 在全部文件身份检查后再次读取 selector，拒绝验证窗口内发生的 concurrent promotion 漂移。
        /// </summary>
        private static bool TryRevalidateSelectorSnapshot(
            GeneratorExecutionContext context,
            string selectorPath,
            byte[] originalBytes)
        {
            try
            {
                GasRouteScaffoldIdentity.ValidateSelectorFile(selectorPath);
                byte[] currentBytes = File.ReadAllBytes(selectorPath);
                if (!GasSourceBundleCodec.BytesEqual(currentBytes, originalBytes))
                {
                    ReportError(context, InvalidSelector, "Selector changed during generator validation.");
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                ReportError(context, InvalidSelector,
                    "Selector could not be revalidated: " + exception.GetType().Name + ": " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// 重算 production analyzer DLL hash 并拒绝缺失或 bytes 漂移。
        /// </summary>
        private static bool TryValidateAnalyzer(
            GeneratorExecutionContext context,
            string projectRoot,
            GasSourceBundle bundle)
        {
            try
            {
                byte[] actual = GasRouteScaffoldIdentity.ComputeAnalyzerSha256(projectRoot);
                if (!GasSourceBundleCodec.BytesEqual(actual, bundle.AnalyzerSha256))
                {
                    ReportError(context, AnalyzerIdentity, "AnalyzerSha256 does not match production analyzer bytes.");
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                ReportError(context, AnalyzerIdentity,
                    "Analyzer identity could not be closed: " + exception.GetType().Name + ": " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// 重算精确 14 项 route scaffold hash 并拒绝路径、链接、缺项或 bytes 漂移。
        /// </summary>
        private static bool TryValidateScaffold(
            GeneratorExecutionContext context,
            string projectRoot,
            GasSourceBundle bundle)
        {
            try
            {
                byte[] actual = GasRouteScaffoldIdentity.ComputeRouteScaffoldSha256(projectRoot);
                if (!GasSourceBundleCodec.BytesEqual(actual, bundle.RouteScaffoldSha256))
                {
                    ReportError(context, ScaffoldIdentity, "RouteScaffoldSha256 does not match the frozen file set.");
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                ReportError(context, ScaffoldIdentity,
                    "Route scaffold identity could not be closed: " + exception.GetType().Name + ": " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// 在全部身份门通过后仅添加本程序集 entries，并额外生成固定 ABI marker。
        /// </summary>
        private static void AddValidatedSources(
            GeneratorExecutionContext context,
            string assemblyName,
            byte[] selectorBytes,
            GasSourceBundle bundle)
        {
            List<GasSourceEntry> selectedEntries = new List<GasSourceEntry>();
            foreach (GasSourceEntry entry in bundle.Entries)
            {
                if (string.Equals(entry.TargetAssembly, assemblyName, StringComparison.Ordinal))
                {
                    selectedEntries.Add(entry);
                }
            }

            if (selectedEntries.Count == 0)
            {
                throw new InvalidDataException("Target assembly has no required source entry.");
            }

            string selectorHash = GasSourceBundleCodec.ComputeSelectorSha256Hex(selectorBytes);
            string markerSource = BuildMarkerSource(assemblyName, selectorHash, bundle);
            foreach (GasSourceEntry entry in selectedEntries)
            {
                context.AddSource(entry.HintName, SourceText.From(entry.GetSourceText(), new UTF8Encoding(false)));
            }

            context.AddSource(
                GasCodeGenContract.MarkerHintName,
                SourceText.From(markerSource, new UTF8Encoding(false)));
        }

        /// <summary>
        /// 构造 target-local internal marker，其四个常量只描述已验证 selector 与 manifest/source 身份。
        /// </summary>
        private static string BuildMarkerSource(string assemblyName, string selectorHash, GasSourceBundle bundle)
        {
            return "namespace GAS.Generated.CodeGen\n"
                + "{\n"
                + "    /// <summary>记录本程序集由唯一 production selector 生成的物理安装身份。</summary>\n"
                + "    internal static class GasCodeGenSourceGeneratorMarker\n"
                + "    {\n"
                + "        internal const string TargetAssembly = \"" + assemblyName + "\";\n"
                + "        internal const string SelectorSha256 = \"" + selectorHash + "\";\n"
                + "        internal const string ArtifactManifestHash = \""
                + GasSourceBundleCodec.ToLowerHex(bundle.ArtifactManifestHash) + "\";\n"
                + "        internal const string SourceArtifactInventoryHash = \""
                + GasSourceBundleCodec.ToLowerHex(bundle.SourceArtifactInventoryHash) + "\";\n"
                + "    }\n"
                + "}\n";
        }

        /// <summary>
        /// 创建稳定 error diagnostic；SourceGenerator 不产生 warning 或宽松 fallback。
        /// </summary>
        private static DiagnosticDescriptor CreateDiagnostic(string id, string title)
        {
            return new DiagnosticDescriptor(
                id,
                title,
                "{0}",
                "GasCodeGen",
                DiagnosticSeverity.Error,
                true);
        }

        /// <summary>
        /// 把合同失败写入 compilation diagnostic，使 stale/legacy output 无法静默取得资格。
        /// </summary>
        private static void ReportError(
            GeneratorExecutionContext context,
            DiagnosticDescriptor descriptor,
            string message)
        {
            context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None, message));
        }
    }
}
