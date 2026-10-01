using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace GAS.Tests.D0M2S.SourceGenerator
{
    /// <summary>
    /// 为 D0-M2S 隔离工程生成三程序集同代 marker，并从 tracked phase-stamp 源码恢复本次编译身份与强杀控制。
    /// </summary>
    [Generator]
    public sealed class D0M2SCanaryGenerator : ISourceGenerator
    {
        private const string SelectorFileName = "Generation.D0M2SCanaryGenerator.additionalfile";
        private const string RuntimeAssembly = "com.exhard.exgas.generated.runtime";
        private const string EditorAssembly = "com.exhard.exgas.generated.editor";
        private const string AutoChessAssembly = "com.exhard.exgas.autochessdemo";
        private const string TrackedPhaseStampToken = "D0M2S_TRACKED_PHASE_STAMP";
        private const string NoControlMode = "None";
        private const string BlockBeforeAddSourceMode = "BlockBeforeAddSource";
        private static readonly Regex TrackedPhaseStampPattern = new Regex(
            @"^// D0M2S_TRACKED_PHASE_STAMP\|RunId=(?<RunId>D0M2S-[0-9]{8}T[0-9]{6}Z-[0-9a-f]{12})"
            + @"\|CaseId=(?<CaseId>SG-0[1-8])\|InvocationId=(?<InvocationId>[0-9a-f]{32})"
            + @"\|GeneratorSha256=(?<GeneratorSha256>[0-9a-f]{64})"
            + @"\|PhaseNonce=(?<PhaseNonce>phase-[0-9a-f]{32})"
            + @"\|ControlMode=(?<ControlMode>None|BlockBeforeAddSource)"
            + @"\|SignalPathBase64=(?<SignalPathBase64>[A-Za-z0-9+/]*={0,2})$",
            RegexOptions.CultureInvariant);
        private static readonly DiagnosticDescriptor MissingSelector = CreateDiagnostic(
            "D0M2SSG001",
            "D0-M2S semantic selector missing");
        private static readonly DiagnosticDescriptor DuplicateSelector = CreateDiagnostic(
            "D0M2SSG002",
            "D0-M2S semantic selector duplicated");
        private static readonly DiagnosticDescriptor InvalidSelector = CreateDiagnostic(
            "D0M2SSG003",
            "D0-M2S semantic selector bytes invalid");
        private static readonly DiagnosticDescriptor ControlFailure = CreateDiagnostic(
            "D0M2SSG004",
            "D0-M2S generator control invalid");

        /// <summary>
        /// 本 generator 不注册语法接收器；generation 语义只读取精确命名的 additional-file。
        /// </summary>
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        /// <summary>
        /// 验证唯一 selector 的 canonical bytes，在可选强杀 checkpoint 后为目标程序集生成身份 marker。
        /// </summary>
        public void Execute(GeneratorExecutionContext context)
        {
            string markerNamespace = GetMarkerNamespace(context.Compilation.AssemblyName);
            if (markerNamespace.Length == 0)
            {
                return;
            }

            AdditionalText selector = FindUniqueSelector(context);
            if (selector == null)
            {
                return;
            }

            SourceText selectorSnapshot;
            byte[] selectorBytes;
            try
            {
                selectorSnapshot = selector.GetText(context.CancellationToken);
                selectorBytes = File.ReadAllBytes(selector.Path);
            }
            catch (Exception exception)
            {
                ReportError(context, InvalidSelector,
                    "Semantic selector raw bytes could not be read: " + exception.GetType().Name);
                return;
            }

            string generation = GetCanonicalGeneration(selectorBytes);
            if (generation.Length == 0)
            {
                ReportError(context, InvalidSelector,
                    "Semantic selector must contain exact UTF-8 canonical bytes generation-a\\n or generation-b\\n.");
                return;
            }
            if (selectorSnapshot == null || !string.Equals(
                selectorSnapshot.ToString(),
                generation + "\n",
                StringComparison.Ordinal))
            {
                ReportError(context, InvalidSelector,
                    "Semantic selector raw bytes do not match the Roslyn AdditionalText snapshot.");
                return;
            }

            string selectorSha256 = GetBytesSha256(selectorBytes);
            string runId;
            string caseId;
            string invocationId;
            string generatorSha256;
            string phaseNonce;
            string controlMode;
            string signalPath;
            if (!TryGetInvocationIdentity(
                context,
                selector.Path,
                out runId,
                out caseId,
                out invocationId,
                out generatorSha256,
                out phaseNonce,
                out controlMode,
                out signalPath))
            {
                return;
            }
            if (!TryEnterCompileWindowControl(
                context,
                generation,
                selectorSha256,
                runId,
                caseId,
                invocationId,
                generatorSha256,
                phaseNonce,
                controlMode,
                signalPath))
            {
                return;
            }

            string assemblyName = context.Compilation.AssemblyName ?? string.Empty;
            string source = BuildMarkerSource(
                markerNamespace,
                assemblyName,
                generation,
                selectorSha256,
                runId,
                caseId,
                invocationId,
                generatorSha256,
                phaseNonce);
            context.AddSource("D0M2S.GenerationMarker.g.cs", SourceText.From(source, Encoding.UTF8));
        }

        /// <summary>
        /// 将冻结 assembly graph 映射到稳定 marker namespace，其他程序集不生成输出。
        /// </summary>
        private static string GetMarkerNamespace(string assemblyName)
        {
            if (assemblyName == RuntimeAssembly)
            {
                return "D0M2S.Generated.Runtime";
            }

            if (assemblyName == EditorAssembly)
            {
                return "D0M2S.Generated.Editor";
            }

            return assemblyName == AutoChessAssembly ? "D0M2S.Generated.AutoChess" : string.Empty;
        }

        /// <summary>
        /// 查找唯一、精确命名的 selector，并为缺失或重复输入报告不同的稳定诊断。
        /// </summary>
        private static AdditionalText FindUniqueSelector(GeneratorExecutionContext context)
        {
            AdditionalText selected = null;
            foreach (AdditionalText candidate in context.AdditionalFiles)
            {
                if (!string.Equals(Path.GetFileName(candidate.Path), SelectorFileName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (selected != null)
                {
                    ReportError(context, DuplicateSelector,
                        "More than one exact-name semantic selector reached the same compilation.");
                    return null;
                }

                selected = candidate;
            }

            if (selected == null)
            {
                ReportError(context, MissingSelector,
                    "The exact-name semantic selector did not reach this compilation.");
            }

            return selected;
        }

        /// <summary>
        /// 仅接受两种无 BOM、LF 结尾的 canonical 文本，禁止 Trim 或空白宽松解析。
        /// </summary>
        private static string GetCanonicalGeneration(byte[] selectorBytes)
        {
            if (HasExactUtf8Bytes(selectorBytes, "generation-a\n"))
            {
                return "generation-a";
            }

            return HasExactUtf8Bytes(selectorBytes, "generation-b\n")
                ? "generation-b"
                : string.Empty;
        }

        /// <summary>
        /// 逐 byte 比较 UTF-8 无 BOM canonical 值，拒绝 BOM、UTF-16、CRLF 或多余空白。
        /// </summary>
        private static bool HasExactUtf8Bytes(byte[] actual, string canonicalText)
        {
            byte[] expected = new UTF8Encoding(false).GetBytes(canonicalText);
            if (actual.Length != expected.Length)
            {
                return false;
            }

            for (int index = 0; index < actual.Length; index++)
            {
                if (actual[index] != expected[index])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 将当前目标程序集映射到其唯一 tracked phase-stamp 工程相对路径。
        /// </summary>
        private static string GetPhaseStampRelativePath(string assemblyName)
        {
            if (assemblyName == RuntimeAssembly)
            {
                return "Assets/RuntimeGenerated/D0M2SRuntimePhaseStamp.cs";
            }

            if (assemblyName == EditorAssembly)
            {
                return "Assets/EditorGenerated/D0M2SEditorPhaseStamp.cs";
            }

            return assemblyName == AutoChessAssembly
                ? "Assets/AutoChessHost/D0M2SAutoChessPhaseStamp.cs"
                : string.Empty;
        }

        /// <summary>
        /// 只在当前程序集映射的精确 phase-stamp SyntaxTree 中查找唯一 tracked 注释行。
        /// </summary>
        private static bool TryFindTrackedPhaseStampLine(
            GeneratorExecutionContext context,
            string selectorPath,
            out string trackedLine)
        {
            trackedLine = null;
            string assemblyName = context.Compilation.AssemblyName ?? string.Empty;
            string expectedPath = GetPhaseStampRelativePath(assemblyName);
            string expectedAbsolutePath;
            try
            {
                string selectorAbsolutePath = Path.GetFullPath(selectorPath);
                DirectoryInfo selectorDirectory = Directory.GetParent(selectorAbsolutePath);
                DirectoryInfo assetsDirectory = selectorDirectory == null ? null : selectorDirectory.Parent;
                DirectoryInfo projectDirectory = assetsDirectory == null ? null : assetsDirectory.Parent;
                if (selectorDirectory == null || assetsDirectory == null || projectDirectory == null ||
                    !string.Equals(selectorDirectory.Name, "Selector", StringComparison.Ordinal) ||
                    !string.Equals(assetsDirectory.Name, "Assets", StringComparison.Ordinal))
                {
                    ReportError(context, ControlFailure, "Semantic selector path is outside the canonical fixture layout.");
                    return false;
                }

                expectedAbsolutePath = Path.GetFullPath(
                    Path.Combine(projectDirectory.FullName, expectedPath.Replace('/', Path.DirectorySeparatorChar)));
            }
            catch (Exception)
            {
                ReportError(context, ControlFailure, "Tracked phase-stamp path could not be resolved.");
                return false;
            }

            int matchedTreeCount = 0;
            foreach (SyntaxTree syntaxTree in context.Compilation.SyntaxTrees)
            {
                string syntaxPath = syntaxTree.FilePath ?? string.Empty;
                bool expectedTree = Path.IsPathRooted(syntaxPath)
                    ? string.Equals(Path.GetFullPath(syntaxPath), expectedAbsolutePath, StringComparison.Ordinal)
                    : string.Equals(syntaxPath.Replace('\\', '/'), expectedPath, StringComparison.Ordinal);
                if (!expectedTree)
                {
                    continue;
                }

                matchedTreeCount++;
                SourceText sourceText = syntaxTree.GetText(context.CancellationToken);
                foreach (TextLine line in sourceText.Lines)
                {
                    string candidate = line.ToString();
                    if (candidate.IndexOf(TrackedPhaseStampToken, StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    if (trackedLine != null)
                    {
                        ReportError(context, ControlFailure, "Tracked phase-stamp identity is duplicated.");
                        return false;
                    }

                    trackedLine = candidate;
                }
            }

            if (matchedTreeCount != 1 || trackedLine == null)
            {
                ReportError(context, ControlFailure, "Tracked phase-stamp identity is missing or duplicated.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 从当前 Compilation.SyntaxTrees 唯一 canonical phase-stamp 注释读取完整 invocation 与控制身份。
        /// </summary>
        private static bool TryGetInvocationIdentity(
            GeneratorExecutionContext context,
            string selectorPath,
            out string runId,
            out string caseId,
            out string invocationId,
            out string generatorSha256,
            out string phaseNonce,
            out string controlMode,
            out string signalPath)
        {
            runId = string.Empty;
            caseId = string.Empty;
            invocationId = string.Empty;
            generatorSha256 = string.Empty;
            phaseNonce = string.Empty;
            controlMode = string.Empty;
            signalPath = string.Empty;
            string trackedLine;
            if (!TryFindTrackedPhaseStampLine(context, selectorPath, out trackedLine))
            {
                return false;
            }
            Match match = TrackedPhaseStampPattern.Match(trackedLine);
            if (!match.Success)
            {
                ReportError(context, ControlFailure, "Tracked phase-stamp identity is not canonical.");
                return false;
            }
            runId = match.Groups["RunId"].Value;
            caseId = match.Groups["CaseId"].Value;
            invocationId = match.Groups["InvocationId"].Value;
            generatorSha256 = match.Groups["GeneratorSha256"].Value;
            phaseNonce = match.Groups["PhaseNonce"].Value;
            controlMode = match.Groups["ControlMode"].Value;
            string signalPathBase64 = match.Groups["SignalPathBase64"].Value;
            bool linkedNonce = string.Equals(phaseNonce, "phase-" + invocationId, StringComparison.Ordinal);
            bool decodedSignal = TryDecodeCanonicalSignalPath(signalPathBase64, out signalPath);
            bool validControl = controlMode == NoControlMode
                ? signalPathBase64.Length == 0 && signalPath.Length == 0
                : signalPathBase64.Length > 0 && signalPath.Length > 0;
            if (!linkedNonce || !decodedSignal || !validControl)
            {
                ReportError(context, ControlFailure, "Tracked phase-stamp tuple is internally inconsistent.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 将 canonical Base64 严格解码为无 BOM UTF-8 绝对规范路径，空值仅供 None control 使用。
        /// </summary>
        private static bool TryDecodeCanonicalSignalPath(string base64, out string signalPath)
        {
            signalPath = string.Empty;
            if (base64.Length == 0)
            {
                return true;
            }

            try
            {
                byte[] bytes = Convert.FromBase64String(base64);
                if (!string.Equals(Convert.ToBase64String(bytes), base64, StringComparison.Ordinal))
                {
                    return false;
                }

                signalPath = new UTF8Encoding(false, true).GetString(bytes);
                return signalPath.Length > 0 &&
                    Path.IsPathRooted(signalPath) &&
                    string.Equals(Path.GetFullPath(signalPath), signalPath, StringComparison.Ordinal);
            }
            catch (Exception)
            {
                signalPath = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// 仅在本次 Unity 子进程显式授权时为 Runtime 编译写出持久化 checkpoint 并阻塞在 AddSource 前。
        /// </summary>
        private static bool TryEnterCompileWindowControl(
            GeneratorExecutionContext context,
            string generation,
            string selectorSha256,
            string runId,
            string caseId,
            string invocationId,
            string generatorSha256,
            string phaseNonce,
            string controlMode,
            string signalPath)
        {
            if (controlMode == NoControlMode)
            {
                return true;
            }

            if (!string.Equals(controlMode, BlockBeforeAddSourceMode, StringComparison.Ordinal))
            {
                ReportError(context, ControlFailure, "Unknown generator control mode.");
                return false;
            }

            string assemblyName = context.Compilation.AssemblyName ?? string.Empty;
            if (!string.Equals(assemblyName, RuntimeAssembly, StringComparison.Ordinal))
            {
                return true;
            }

            if (!Path.IsPathRooted(signalPath) || runId.Length == 0 || generation != "generation-b")
            {
                ReportError(context, ControlFailure, "Compile-window control identity is incomplete.");
                return false;
            }

            WriteCompileWindowSignal(
                signalPath,
                runId,
                caseId,
                invocationId,
                Process.GetCurrentProcess().Id,
                assemblyName,
                selectorSha256,
                generatorSha256,
                phaseNonce);
            while (true)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(100);
            }
        }

        /// <summary>
        /// 以 CreateNew 和 flush-to-disk 写出强杀信号，避免复用旧 run checkpoint。
        /// </summary>
        private static void WriteCompileWindowSignal(
            string signalPath,
            string runId,
            string caseId,
            string invocationId,
            int compilerProcessId,
            string assemblyName,
            string selectorSha256,
            string generatorSha256,
            string phaseNonce)
        {
            string parent = Path.GetDirectoryName(signalPath);
            if (string.IsNullOrWhiteSpace(parent) || File.Exists(signalPath) || Directory.Exists(signalPath))
            {
                throw new InvalidOperationException("Compile-window signal path must be a fresh file.");
            }

            Directory.CreateDirectory(parent);
            string content = "{\"Schema\":\"D0M2S-GeneratorBeforeAddSource-v1\","
                + "\"RunId\":\"" + runId + "\","
                + "\"CaseId\":\"" + caseId + "\","
                + "\"InvocationId\":\"" + invocationId + "\","
                + "\"CompilerProcessId\":" + compilerProcessId + ","
                + "\"Assembly\":\"" + assemblyName + "\","
                + "\"SelectorSha256\":\"" + selectorSha256 + "\","
                + "\"GeneratorSha256\":\"" + generatorSha256 + "\","
                + "\"PhaseNonce\":\"" + phaseNonce + "\"}\n";
            byte[] bytes = new UTF8Encoding(false).GetBytes(content);
            using (FileStream stream = new FileStream(
                signalPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        /// <summary>
        /// 计算 selector 文件 raw bytes 的 SHA-256。
        /// </summary>
        private static string GetBytesSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(digest.Length * 2);
                foreach (byte item in digest)
                {
                    builder.Append(item.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// 构造 C# 9 兼容 marker，并把 generation、程序集及 exact selector SHA 固化到输出。
        /// </summary>
        private static string BuildMarkerSource(
            string markerNamespace,
            string assemblyName,
            string generation,
            string selectorSha256,
            string runId,
            string caseId,
            string invocationId,
            string generatorSha256,
            string phaseNonce)
        {
            return "namespace " + markerNamespace + "\n"
                + "{\n"
                + "    /// <summary>记录本程序集由 D0-M2S 唯一 canonical selector 生成的身份。</summary>\n"
                + "    public static class GenerationMarker\n"
                + "    {\n"
                + "        public const string GenerationId = \"" + generation + "\";\n"
                + "        public const string DeclaredAssembly = \"" + assemblyName + "\";\n"
                + "        public const string SelectorFileName = \"" + SelectorFileName + "\";\n"
                + "        public const string SelectorSha256 = \"" + selectorSha256 + "\";\n"
                + "        public const string RunId = \"" + runId + "\";\n"
                + "        public const string CaseId = \"" + caseId + "\";\n"
                + "        public const string InvocationId = \"" + invocationId + "\";\n"
                + "        public const string GeneratorSha256 = \"" + generatorSha256 + "\";\n"
                + "        public const string PhaseNonce = \"" + phaseNonce + "\";\n"
                + "    }\n"
                + "}\n";
        }

        /// <summary>
        /// 创建稳定的错误诊断描述符，使 harness 能区分缺失、重复、非法 bytes 与控制故障。
        /// </summary>
        private static DiagnosticDescriptor CreateDiagnostic(string id, string title)
        {
            return new DiagnosticDescriptor(
                id,
                title,
                "{0}",
                "D0M2S",
                DiagnosticSeverity.Error,
                true);
        }

        /// <summary>
        /// 将精确合同错误写入 Roslyn 编译诊断，禁止静默使用陈旧生成输出。
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
