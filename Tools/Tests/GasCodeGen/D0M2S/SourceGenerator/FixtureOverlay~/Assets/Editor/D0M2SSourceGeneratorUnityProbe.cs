using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace GAS.Tests.D0M2S.SourceGenerator
{
    /// <summary>
    /// 从隔离 Unity 工程导出三程序集 marker、selector raw identity 与 CompilationPipeline 输入，不参与 selector 切换。
    /// </summary>
    public static class D0M2SSourceGeneratorUnityProbe
    {
        private const string OutputArgument = "-d0m2sOutput";
        private const string ExpectedGenerationArgument = "-d0m2sExpectedGeneration";
        private const string ExpectedSelectorShaArgument = "-d0m2sExpectedSelectorSha256";
        private const string RunIdArgument = "-d0m2sRunId";
        private const string CaseIdArgument = "-d0m2sCaseId";
        private const string InvocationIdArgument = "-d0m2sInvocationId";
        private const string ExpectedGeneratorShaArgument = "-d0m2sExpectedGeneratorSha256";
        private const string ExpectedPhaseNonceArgument = "-d0m2sExpectedPhaseNonce";
        private const string SelectorRelativePath =
            "Assets/Selector/Generation.D0M2SCanaryGenerator.additionalfile";
        private const string RuntimeAssembly = "com.exhard.exgas.generated.runtime";
        private const string EditorAssembly = "com.exhard.exgas.generated.editor";
        private const string AutoChessAssembly = "com.exhard.exgas.autochessdemo";

        /// <summary>
        /// 读取命令行、采集三程序集证据并以显式退出码返回结果。
        /// </summary>
        public static void Run()
        {
            string outputPath = GetRequiredArgument(OutputArgument);
            try
            {
                string expectedGeneration = GetRequiredArgument(ExpectedGenerationArgument);
                string expectedSelectorSha256 = GetRequiredArgument(ExpectedSelectorShaArgument);
                string runId = GetRequiredArgument(RunIdArgument);
                string caseId = GetRequiredArgument(CaseIdArgument);
                string invocationId = GetRequiredArgument(InvocationIdArgument);
                string expectedGeneratorSha256 = GetRequiredArgument(ExpectedGeneratorShaArgument);
                string expectedPhaseNonce = GetRequiredArgument(ExpectedPhaseNonceArgument);
                UnityProbeResult result = CollectResult(
                    expectedGeneration,
                    expectedSelectorSha256,
                    runId,
                    caseId,
                    invocationId,
                    expectedGeneratorSha256,
                    expectedPhaseNonce);
                WriteResult(outputPath, result);
                UnityEngine.Debug.Log("D0-M2S SourceGenerator probe completed. Passed=" + result.Passed);
                EditorApplication.Exit(result.Passed ? 0 : 1);
            }
            catch (Exception exception)
            {
                WriteResult(outputPath, UnityProbeResult.CreateFailure(exception.ToString()));
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// 收集 Runtime、Editor 与 AutoChess marker，并要求它们绑定同一 exact selector bytes。
        /// </summary>
        private static UnityProbeResult CollectResult(
            string expectedGeneration,
            string expectedSelectorSha256,
            string runId,
            string caseId,
            string invocationId,
            string expectedGeneratorSha256,
            string expectedPhaseNonce)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string selectorPath = Path.GetFullPath(Path.Combine(projectRoot, SelectorRelativePath));
            string observedSelectorSha256 = GetFileSha256(selectorPath);
            string generatorPath = Path.GetFullPath(Path.Combine(
                projectRoot,
                "Assets/Analyzers/D0M2SCanaryGenerator.dll"));
            string observedGeneratorSha256 = GetFileSha256(generatorPath);
            AssemblyProbe[] assemblies = new[]
            {
                CollectAssembly(RuntimeAssembly, "D0M2S.Generated.Runtime.GenerationMarker"),
                CollectAssembly(EditorAssembly, "D0M2S.Generated.Editor.GenerationMarker"),
                CollectAssembly(AutoChessAssembly, "D0M2S.Generated.AutoChess.GenerationMarker")
            };
            bool passed = string.Equals(
                observedSelectorSha256,
                expectedSelectorSha256,
                StringComparison.Ordinal) && string.Equals(
                observedGeneratorSha256,
                expectedGeneratorSha256,
                StringComparison.Ordinal);
            foreach (AssemblyProbe assembly in assemblies)
            {
                passed &= string.Equals(assembly.Generation, expectedGeneration, StringComparison.Ordinal);
                passed &= string.Equals(assembly.DeclaredAssembly, assembly.Name, StringComparison.Ordinal);
                passed &= string.Equals(
                    assembly.SelectorFileName,
                    Path.GetFileName(SelectorRelativePath),
                    StringComparison.Ordinal);
                passed &= string.Equals(
                    assembly.SelectorSha256,
                    expectedSelectorSha256,
                    StringComparison.Ordinal);
                passed &= string.Equals(assembly.RunId, runId, StringComparison.Ordinal);
                passed &= string.Equals(assembly.CaseId, caseId, StringComparison.Ordinal);
                passed &= string.Equals(assembly.InvocationId, invocationId, StringComparison.Ordinal);
                passed &= string.Equals(
                    assembly.GeneratorSha256,
                    expectedGeneratorSha256,
                    StringComparison.Ordinal);
                passed &= string.Equals(
                    assembly.PhaseNonce,
                    expectedPhaseNonce,
                    StringComparison.Ordinal);
            }

            return new UnityProbeResult
            {
                Passed = passed,
                ExpectedGeneration = expectedGeneration,
                ExpectedSelectorSha256 = expectedSelectorSha256,
                RunId = runId,
                CaseId = caseId,
                InvocationId = invocationId,
                ProcessId = Process.GetCurrentProcess().Id,
                ExpectedGeneratorSha256 = expectedGeneratorSha256,
                ObservedGeneratorPath = generatorPath,
                ObservedGeneratorSha256 = observedGeneratorSha256,
                ExpectedPhaseNonce = expectedPhaseNonce,
                ObservedSelectorPath = selectorPath,
                ObservedSelectorSha256 = observedSelectorSha256,
                ProjectRoot = projectRoot,
                UnityVersion = Application.unityVersion,
                Assemblies = assemblies,
                Detail = passed
                    ? "三个冻结程序集均消费同一 exact selector bytes。"
                    : "至少一个程序集或 selector raw identity 未绑定期望值。"
            };
        }

        /// <summary>
        /// 读取单个目标程序集的生成常量、源码成员、引用及 additional-file 输入。
        /// </summary>
        private static AssemblyProbe CollectAssembly(string assemblyName, string markerTypeName)
        {
            UnityEditor.Compilation.Assembly assembly = FindAssembly(assemblyName);
            Type markerType = FindType(markerTypeName, assemblyName);
            return new AssemblyProbe
            {
                Name = assembly.name,
                Generation = ReadConstant(markerType, "GenerationId"),
                DeclaredAssembly = ReadConstant(markerType, "DeclaredAssembly"),
                SelectorFileName = ReadConstant(markerType, "SelectorFileName"),
                SelectorSha256 = ReadConstant(markerType, "SelectorSha256"),
                RunId = ReadConstant(markerType, "RunId"),
                CaseId = ReadConstant(markerType, "CaseId"),
                InvocationId = ReadConstant(markerType, "InvocationId"),
                GeneratorSha256 = ReadConstant(markerType, "GeneratorSha256"),
                PhaseNonce = ReadConstant(markerType, "PhaseNonce"),
                LoadedAssemblyLocation = NormalizePath(markerType.Assembly.Location),
                LoadedAssemblyMvid = markerType.Assembly.ManifestModule.ModuleVersionId.ToString("D"),
                OutputPath = NormalizePath(assembly.outputPath),
                SourceFiles = NormalizePaths(assembly.sourceFiles),
                CompiledAssemblyReferences = NormalizePaths(assembly.compiledAssemblyReferences),
                Defines = SortCopy(assembly.defines),
                RoslynAdditionalFilePaths = NormalizePaths(
                    assembly.compilerOptions.RoslynAdditionalFilePaths)
            };
        }

        /// <summary>
        /// 在当前 Editor 编译图中精确查找目标程序集，缺失时 fail closed。
        /// </summary>
        private static UnityEditor.Compilation.Assembly FindAssembly(string assemblyName)
        {
            UnityEditor.Compilation.Assembly[] assemblies =
                CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            foreach (UnityEditor.Compilation.Assembly assembly in assemblies)
            {
                if (string.Equals(assembly.name, assemblyName, StringComparison.Ordinal))
                {
                    return assembly;
                }
            }

            throw new InvalidOperationException("Compilation assembly was not found: " + assemblyName);
        }

        /// <summary>
        /// 通过程序集限定名加载生成类型，拒绝从其他程序集误取同名 marker。
        /// </summary>
        private static Type FindType(string typeName, string assemblyName)
        {
            Type type = Type.GetType(typeName + ", " + assemblyName, false);
            if (type == null)
            {
                throw new InvalidOperationException("Generated marker type was not loaded: " + typeName);
            }

            return type;
        }

        /// <summary>
        /// 读取生成 marker 的 public const string，并拒绝缺失或空值。
        /// </summary>
        private static string ReadConstant(Type markerType, string fieldName)
        {
            FieldInfo field = markerType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            string value = field == null ? string.Empty : field.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Generated marker field is missing: " + fieldName);
            }

            return value;
        }

        /// <summary>
        /// 计算普通文件 raw bytes 的 SHA-256，并返回 lowercase hex。
        /// </summary>
        private static string GetFileSha256(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(stream);
                StringBuilder builder = new StringBuilder(digest.Length * 2);
                foreach (byte item in digest)
                {
                    builder.Append(item.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// 将 Unity 返回的相对路径映射为 fixture 内绝对路径。
        /// </summary>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        /// <summary>
        /// 规范化并排序路径数组，使 raw JSON 不依赖 Unity 枚举顺序。
        /// </summary>
        private static string[] NormalizePaths(string[] paths)
        {
            if (paths == null)
            {
                return Array.Empty<string>();
            }

            string[] normalized = new string[paths.Length];
            for (int index = 0; index < paths.Length; index++)
            {
                normalized[index] = NormalizePath(paths[index]);
            }

            Array.Sort(normalized, StringComparer.OrdinalIgnoreCase);
            return normalized;
        }

        /// <summary>
        /// 复制并排序普通字符串数组，避免修改 Unity 提供的集合。
        /// </summary>
        private static string[] SortCopy(string[] values)
        {
            if (values == null)
            {
                return Array.Empty<string>();
            }

            string[] copy = (string[])values.Clone();
            Array.Sort(copy, StringComparer.Ordinal);
            return copy;
        }

        /// <summary>
        /// 读取紧随指定命令行名称后的非空值，缺失时显式失败。
        /// </summary>
        private static string GetRequiredArgument(string argumentName)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (arguments[index] == argumentName &&
                    !string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    return arguments[index + 1];
                }
            }

            throw new ArgumentException("Required command-line argument is missing: " + argumentName);
        }

        /// <summary>
        /// 以 UTF-8 无 BOM、LF 和单文件 JSON 写出 Unity 侧原始证据。
        /// </summary>
        private static void WriteResult(string outputPath, UnityProbeResult result)
        {
            string parent = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(parent))
            {
                throw new InvalidOperationException("Probe output requires a parent directory.");
            }

            Directory.CreateDirectory(parent);
            string json = JsonUtility.ToJson(result, true).Replace("\r\n", "\n") + "\n";
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);
            using (FileStream stream = new FileStream(
                outputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }
    }

    /// <summary>
    /// Unity 侧单次 SourceGenerator 调用的可序列化 raw 结果。
    /// </summary>
    [Serializable]
    internal sealed class UnityProbeResult
    {
        public bool Passed;
        public string ExpectedGeneration;
        public string ExpectedSelectorSha256;
        public string RunId;
        public string CaseId;
        public string InvocationId;
        public int ProcessId;
        public string ExpectedGeneratorSha256;
        public string ObservedGeneratorPath;
        public string ObservedGeneratorSha256;
        public string ExpectedPhaseNonce;
        public string ObservedSelectorPath;
        public string ObservedSelectorSha256;
        public string ProjectRoot;
        public string UnityVersion;
        public AssemblyProbe[] Assemblies;
        public string Detail;

        /// <summary>
        /// 为入口异常创建显式失败 raw，禁止错误被日志吞没或伪装成功。
        /// </summary>
        public static UnityProbeResult CreateFailure(string detail)
        {
            return new UnityProbeResult
            {
                Passed = false,
                Assemblies = Array.Empty<AssemblyProbe>(),
                Detail = detail
            };
        }
    }

    /// <summary>
    /// 单个目标程序集的 generation、selector 与 CompilationPipeline 输入快照。
    /// </summary>
    [Serializable]
    internal sealed class AssemblyProbe
    {
        public string Name;
        public string Generation;
        public string DeclaredAssembly;
        public string SelectorFileName;
        public string SelectorSha256;
        public string RunId;
        public string CaseId;
        public string InvocationId;
        public string GeneratorSha256;
        public string PhaseNonce;
        public string LoadedAssemblyLocation;
        public string LoadedAssemblyMvid;
        public string OutputPath;
        public string[] SourceFiles;
        public string[] CompiledAssemblyReferences;
        public string[] Defines;
        public string[] RoslynAdditionalFilePaths;
    }
}
