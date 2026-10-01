using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace GAS.Tests.D0M2F.SourceGenerator
{
    /// <summary>
    /// 从隔离 Unity 工程导出三程序集生成 token 与 CompilationPipeline 真实输入，不参与 selector 切换。
    /// </summary>
    public static class D0M2FSourceGeneratorUnityProbe
    {
        private const string OutputArgument = "-d0m2fOutput";
        private const string ExpectedGenerationArgument = "-d0m2fExpectedGeneration";
        private const string HoldSignalArgument = "-d0m2fHoldSignal";
        private const string RuntimeAssembly = "com.exhard.exgas.generated.runtime";
        private const string EditorAssembly = "com.exhard.exgas.generated.editor";
        private const string AutoChessAssembly = "com.exhard.exgas.autochessdemo";

        /// <summary>
        /// 读取命令行、采集三程序集证据并以退出码显式返回探针结果。
        /// </summary>
        public static void Run()
        {
            string outputPath = GetRequiredArgument(OutputArgument);
            try
            {
                string expectedGeneration = GetRequiredArgument(ExpectedGenerationArgument);
                UnityProbeResult result = CollectResult(expectedGeneration);
                WriteResult(outputPath, result);
                Debug.Log("D0-M2F SourceGenerator probe completed. Passed=" + result.Passed);
                string holdSignal;
                if (TryGetArgument(HoldSignalArgument, out holdSignal))
                {
                    WriteHoldSignal(holdSignal);
                    Thread.Sleep(Timeout.Infinite);
                    return;
                }

                EditorApplication.Exit(result.Passed ? 0 : 1);
            }
            catch (Exception exception)
            {
                WriteResult(outputPath, UnityProbeResult.CreateFailure(exception.ToString()));
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// 收集 Runtime、Editor 与 AutoChess 的 marker 和编译输入，并要求它们绑定期望 generation。
        /// </summary>
        private static UnityProbeResult CollectResult(string expectedGeneration)
        {
            AssemblyProbe[] assemblies = new[]
            {
                CollectAssembly(RuntimeAssembly, "D0M2F.Generated.Runtime.GenerationMarker"),
                CollectAssembly(EditorAssembly, "D0M2F.Generated.Editor.GenerationMarker"),
                CollectAssembly(AutoChessAssembly, "D0M2F.Generated.AutoChess.GenerationMarker")
            };
            bool passed = true;
            foreach (AssemblyProbe assembly in assemblies)
            {
                passed &= assembly.Generation == expectedGeneration;
                passed &= assembly.DeclaredAssembly == assembly.Name;
            }

            return new UnityProbeResult
            {
                Passed = passed,
                ExpectedGeneration = expectedGeneration,
                ProjectRoot = Directory.GetParent(Application.dataPath).FullName,
                UnityVersion = Application.unityVersion,
                Assemblies = assemblies,
                Detail = passed
                    ? "三个冻结程序集均消费期望 generation。"
                    : "至少一个程序集未消费期望 generation。"
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
                OutputPath = NormalizePath(assembly.outputPath),
                SourceFiles = NormalizePaths(assembly.sourceFiles),
                CompiledAssemblyReferences = NormalizePaths(assembly.compiledAssemblyReferences),
                Defines = SortCopy(assembly.defines),
                RoslynAdditionalFilePaths = NormalizePaths(
                    assembly.compilerOptions.RoslynAdditionalFilePaths)
            };
        }

        /// <summary>
        /// 在 Unity 当前 Editor 编译图中精确查找目标程序集，缺失时 fail closed。
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
        /// 通过程序集限定名加载 Source Generator 输出类型，拒绝从其他程序集误取同名类型。
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
        /// 将 Unity 返回的相对路径映射为 fixture 内绝对路径，便于外部 harness 复核。
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
        /// 规范化并排序路径数组，使 JSON 证据不依赖 Unity 枚举顺序。
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
        /// 复制并排序普通字符串数组，避免修改 Unity 提供的原始集合。
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
            string value;
            if (TryGetArgument(argumentName, out value))
            {
                return value;
            }

            throw new ArgumentException("Required command-line argument is missing: " + argumentName);
        }

        /// <summary>
        /// 尝试读取紧随指定命令行名称后的非空值，供强杀 probe 判断可选 signal。
        /// </summary>
        private static bool TryGetArgument(string argumentName, out string value)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (arguments[index] == argumentName && !string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    value = arguments[index + 1];
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }

        /// <summary>
        /// 在 raw JSON 已持久化后创建 fresh signal，再等待 harness 强杀本进程。
        /// </summary>
        private static void WriteHoldSignal(string signalPath)
        {
            string parent = Path.GetDirectoryName(signalPath);
            if (string.IsNullOrWhiteSpace(parent) || File.Exists(signalPath) || Directory.Exists(signalPath))
            {
                throw new InvalidOperationException("Hold signal path must be a fresh file with a parent directory.");
            }

            Directory.CreateDirectory(parent);
            using (FileStream stream = new FileStream(
                signalPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes("ready-for-kill\n");
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
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
            File.WriteAllText(outputPath, json, new UTF8Encoding(false));
        }
    }

    /// <summary>
    /// Unity 侧单次 Source Generator 调用的可序列化结果。
    /// </summary>
    [Serializable]
    internal sealed class UnityProbeResult
    {
        public bool Passed;
        public string ExpectedGeneration;
        public string ProjectRoot;
        public string UnityVersion;
        public AssemblyProbe[] Assemblies;
        public string Detail;

        /// <summary>
        /// 为入口异常创建显式失败结果，禁止错误被日志吞没。
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
    /// 单个目标程序集的 generation、owner 与 CompilationPipeline 输入快照。
    /// </summary>
    [Serializable]
    internal sealed class AssemblyProbe
    {
        public string Name;
        public string Generation;
        public string DeclaredAssembly;
        public string SelectorFileName;
        public string OutputPath;
        public string[] SourceFiles;
        public string[] CompiledAssemblyReferences;
        public string[] Defines;
        public string[] RoslynAdditionalFilePaths;
    }
}
