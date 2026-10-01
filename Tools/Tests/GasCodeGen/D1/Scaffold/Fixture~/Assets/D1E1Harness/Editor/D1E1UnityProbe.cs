using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace GAS.Tests.D1.E1
{
    /// <summary>
    /// 从 disposable production-path 工程导出三程序集 marker、loaded Location/MVID 与编译输入的 fresh raw 证据。
    /// </summary>
    public static class D1E1UnityProbe
    {
        private const string OutputArgument = "-d1e1Output";
        private const string SignalArgument = "-d1e1Signal";
        private const string RunIdArgument = "-d1e1RunId";
        private const string CaseIdArgument = "-d1e1CaseId";
        private const string InvocationIdArgument = "-d1e1InvocationId";
        private const string ExpectedSelectorShaArgument = "-d1e1ExpectedSelectorSha256";
        private const string MarkerTypeName =
            "GAS.Generated.CodeGen.GasCodeGenSourceGeneratorMarker";
        private const string SelectorRelativePath =
            "Assets/GAS/CodeGen/Selector/Generation.GasCodeGenSourceGenerator.additionalfile";

        private static readonly string[] TargetAssemblies =
        {
            "com.exhard.exgas.generated.runtime",
            "com.exhard.exgas.generated.editor",
            "com.exhard.exgas.autochessdemo"
        };

        /// <summary>采集只读事实，以 CreateNew+Flush(true) 写 raw，并用进程退出码返回判定。</summary>
        public static void Run()
        {
            string outputPath = GetRequiredArgument(OutputArgument);
            D1E1UnityProbeResult result;
            try
            {
                result = CollectResult(
                    GetRequiredArgument(RunIdArgument),
                    GetRequiredArgument(CaseIdArgument),
                    GetRequiredArgument(InvocationIdArgument),
                    GetRequiredArgument(ExpectedSelectorShaArgument));
            }
            catch (Exception exception)
            {
                result = D1E1UnityProbeResult.CreateFailure(
                    GetOptionalArgument(RunIdArgument),
                    GetOptionalArgument(CaseIdArgument),
                    GetOptionalArgument(InvocationIdArgument),
                    exception.ToString());
            }

            try
            {
                WriteFreshResult(outputPath, result);
                WriteFreshSignal(
                    GetRequiredArgument(SignalArgument),
                    result.RunId,
                    result.CaseId,
                    result.InvocationId);
                EditorApplication.Exit(result.Passed ? 0 : 31);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(32);
            }
        }

        /// <summary>收集三程序集并要求它们共同绑定唯一 selector 和 manifest/inventory 身份。</summary>
        private static D1E1UnityProbeResult CollectResult(
            string runId,
            string caseId,
            string invocationId,
            string expectedSelectorSha256)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string exactSelector = NormalizePath(Path.Combine(projectRoot, SelectorRelativePath));
            D1E1AssemblyProbe[] assemblies = new D1E1AssemblyProbe[TargetAssemblies.Length];
            for (int index = 0; index < TargetAssemblies.Length; index++)
            {
                assemblies[index] = CollectAssembly(TargetAssemblies[index], exactSelector);
            }

            bool passed = true;
            string artifactManifestHash = assemblies[0].ArtifactManifestHash;
            string inventoryHash = assemblies[0].SourceArtifactInventoryHash;
            foreach (D1E1AssemblyProbe assembly in assemblies)
            {
                passed &= string.Equals(
                    assembly.TargetAssembly, assembly.Name, StringComparison.Ordinal);
                passed &= string.Equals(
                    assembly.SelectorSha256, expectedSelectorSha256, StringComparison.Ordinal);
                passed &= string.Equals(
                    assembly.ArtifactManifestHash, artifactManifestHash, StringComparison.Ordinal);
                passed &= string.Equals(
                    assembly.SourceArtifactInventoryHash, inventoryHash, StringComparison.Ordinal);
                passed &= assembly.RoslynAdditionalFilePaths.Length == 1;
                passed &= assembly.RoslynAdditionalFilePaths.Length == 1 && string.Equals(
                    assembly.RoslynAdditionalFilePaths[0], exactSelector,
                    StringComparison.OrdinalIgnoreCase);
                passed &= !string.IsNullOrWhiteSpace(assembly.LoadedLocation);
                passed &= !string.IsNullOrWhiteSpace(assembly.LoadedMvid);
            }

            return new D1E1UnityProbeResult
            {
                Schema = "EX-GAS-D1-E1-UnityRaw-v1",
                RunId = runId,
                CaseId = caseId,
                InvocationId = invocationId,
                Passed = passed,
                ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id,
                ProcessStartUtc = System.Diagnostics.Process.GetCurrentProcess()
                    .StartTime.ToUniversalTime().ToString("O"),
                UnityVersion = Application.unityVersion,
                ProjectRoot = NormalizePath(projectRoot),
                ExpectedSelectorSha256 = expectedSelectorSha256,
                Assemblies = assemblies,
                Detail = passed
                    ? "三程序集共同绑定 fresh selector 与 marker。"
                    : "至少一个程序集未绑定 exact selector、marker 或 loaded identity。"
            };
        }

        /// <summary>读取 CompilationPipeline 与同名 loaded assembly，拒绝跨程序集同名 marker。</summary>
        private static D1E1AssemblyProbe CollectAssembly(
            string assemblyName,
            string exactSelectorPath)
        {
            UnityEditor.Compilation.Assembly compilationAssembly =
                FindCompilationAssembly(assemblyName);
            System.Reflection.Assembly loadedAssembly = FindLoadedAssembly(assemblyName);
            Type markerType = loadedAssembly.GetType(MarkerTypeName, false, false);
            if (markerType == null)
            {
                throw new InvalidOperationException(
                    "Generated marker is missing from assembly: " + assemblyName);
            }

            string[] additionalFiles = NormalizePaths(
                compilationAssembly.compilerOptions.RoslynAdditionalFilePaths);
            return new D1E1AssemblyProbe
            {
                Name = assemblyName,
                TargetAssembly = ReadConstant(markerType, "TargetAssembly"),
                SelectorSha256 = ReadConstant(markerType, "SelectorSha256"),
                ArtifactManifestHash = ReadConstant(markerType, "ArtifactManifestHash"),
                SourceArtifactInventoryHash =
                    ReadConstant(markerType, "SourceArtifactInventoryHash"),
                MarkerAssemblyName = markerType.Assembly.GetName().Name,
                LoadedLocation = NormalizePath(loadedAssembly.Location),
                LoadedMvid = loadedAssembly.ManifestModule.ModuleVersionId.ToString("D"),
                CompilationOutputPath = NormalizePath(compilationAssembly.outputPath),
                RoslynAdditionalFilePaths = additionalFiles,
                ExactSelectorPath = exactSelectorPath,
                SourceFiles = NormalizePaths(compilationAssembly.sourceFiles),
                CompiledAssemblyReferences =
                    NormalizePaths(compilationAssembly.compiledAssemblyReferences)
            };
        }

        /// <summary>按 ordinal 名称查找 Unity 当前 Editor 编译图中的唯一目标程序集。</summary>
        private static UnityEditor.Compilation.Assembly FindCompilationAssembly(string assemblyName)
        {
            UnityEditor.Compilation.Assembly match = null;
            foreach (UnityEditor.Compilation.Assembly assembly in
                     CompilationPipeline.GetAssemblies(AssembliesType.Editor))
            {
                if (!string.Equals(assembly.name, assemblyName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (match != null)
                {
                    throw new InvalidOperationException(
                        "Duplicate compilation assembly: " + assemblyName);
                }

                match = assembly;
            }

            return match ?? throw new InvalidOperationException(
                "Compilation assembly is missing: " + assemblyName);
        }

        /// <summary>按 AssemblyName 精确查找唯一 loaded assembly，以便绑定 Location 与 MVID。</summary>
        private static System.Reflection.Assembly FindLoadedAssembly(string assemblyName)
        {
            System.Reflection.Assembly match = null;
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!string.Equals(
                        assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (match != null)
                {
                    throw new InvalidOperationException("Duplicate loaded assembly: " + assemblyName);
                }

                match = assembly;
            }

            return match ?? throw new InvalidOperationException(
                "Loaded assembly is missing: " + assemblyName);
        }

        /// <summary>读取 marker 的 const string 原始常量，拒绝字段缺失、非 literal 或空值。</summary>
        private static string ReadConstant(Type markerType, string fieldName)
        {
            FieldInfo field = markerType.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null || !field.IsLiteral || field.FieldType != typeof(string))
            {
                throw new InvalidOperationException("Marker const is missing: " + fieldName);
            }

            string value = field.GetRawConstantValue() as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Marker const is empty: " + fieldName);
            }

            return value;
        }

        /// <summary>把 Unity 相对路径规范化为 disposable project 内的绝对路径。</summary>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string absolute = Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName, path));
            return absolute.Replace('\\', '/');
        }

        /// <summary>规范化并按 ordinal-ignore-case 排序 Unity 路径数组。</summary>
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

        /// <summary>读取必需命令行参数，确保每次 invocation 的 raw 绑定完整。</summary>
        private static string GetRequiredArgument(string name)
        {
            string value = GetOptionalArgument(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Required argument is missing: " + name);
            }

            return value;
        }

        /// <summary>读取可选命令行参数，供异常 raw 保留已有 invocation 身份。</summary>
        private static string GetOptionalArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (arguments[index] == name)
                {
                    return arguments[index + 1] ?? string.Empty;
                }
            }

            return string.Empty;
        }

        /// <summary>以 UTF-8 无 BOM、CreateNew 和 Flush(true) 写出本 invocation 唯一 raw。</summary>
        private static void WriteFreshResult(string outputPath, D1E1UnityProbeResult result)
        {
            string parent = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(parent) || File.Exists(outputPath) || Directory.Exists(outputPath))
            {
                throw new InvalidOperationException("Raw output path must be a fresh file.");
            }

            Directory.CreateDirectory(parent);
            byte[] bytes = new UTF8Encoding(false).GetBytes(
                JsonUtility.ToJson(result, true).Replace("\r\n", "\n") + "\n");
            using (FileStream stream = new FileStream(
                outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        /// <summary>在 raw 已 durable 后写 fresh 绑定信号，禁止复用旧 invocation 可见性。</summary>
        private static void WriteFreshSignal(
            string signalPath,
            string runId,
            string caseId,
            string invocationId)
        {
            string parent = Path.GetDirectoryName(signalPath);
            if (string.IsNullOrWhiteSpace(parent) || File.Exists(signalPath) ||
                Directory.Exists(signalPath))
            {
                throw new InvalidOperationException("Signal path must be a fresh file.");
            }

            Directory.CreateDirectory(parent);
            string value = "EX-GAS-D1-E1-Signal-v1\n" + runId + "\n" +
                           caseId + "\n" + invocationId + "\n";
            byte[] bytes = new UTF8Encoding(false).GetBytes(value);
            using (FileStream stream = new FileStream(
                signalPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }
    }

    /// <summary>单次 direct-Unity invocation 的 fresh raw 顶层记录。</summary>
    [Serializable]
    internal sealed class D1E1UnityProbeResult
    {
        public string Schema;
        public string RunId;
        public string CaseId;
        public string InvocationId;
        public bool Passed;
        public int ProcessId;
        public string ProcessStartUtc;
        public string UnityVersion;
        public string ProjectRoot;
        public string ExpectedSelectorSha256;
        public D1E1AssemblyProbe[] Assemblies;
        public string Detail;

        /// <summary>构造仍绑定现有 run/case/invocation 的显式失败 raw。</summary>
        public static D1E1UnityProbeResult CreateFailure(
            string runId,
            string caseId,
            string invocationId,
            string detail)
        {
            return new D1E1UnityProbeResult
            {
                Schema = "EX-GAS-D1-E1-UnityRaw-v1",
                RunId = runId,
                CaseId = caseId,
                InvocationId = invocationId,
                Passed = false,
                Assemblies = Array.Empty<D1E1AssemblyProbe>(),
                Detail = detail
            };
        }
    }

    /// <summary>单个目标程序集的 marker、编译图与 loaded Location/MVID 快照。</summary>
    [Serializable]
    internal sealed class D1E1AssemblyProbe
    {
        public string Name;
        public string TargetAssembly;
        public string SelectorSha256;
        public string ArtifactManifestHash;
        public string SourceArtifactInventoryHash;
        public string MarkerAssemblyName;
        public string LoadedLocation;
        public string LoadedMvid;
        public string CompilationOutputPath;
        public string[] RoslynAdditionalFilePaths;
        public string ExactSelectorPath;
        public string[] SourceFiles;
        public string[] CompiledAssemblyReferences;
    }
}
