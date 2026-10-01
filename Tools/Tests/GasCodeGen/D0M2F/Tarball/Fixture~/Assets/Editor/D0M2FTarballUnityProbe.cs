using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace GAS.Tests.D0M2F.Tarball
{
    /// <summary>
    /// 在 disposable Unity 工程内采集 tarball 实际解析根、三程序集 generation 与 selector 冲突证据。
    /// </summary>
    public static class D0M2FTarballUnityProbe
    {
        private const string OutputArgument = "-d0m2fTarballOutput";
        private const string OperationArgument = "-d0m2fTarballOperation";
        private const string ExpectedGenerationArgument = "-d0m2fExpectedGeneration";
        private const string ExpectedArchiveShaArgument = "-d0m2fExpectedArchiveSha";
        private const string ModeArgument = "-d0m2fMode";
        private const string VerifyOperation = "Verify";
        private const string SignalAndWaitForKillOperation = "SignalAndWaitForKill";
        private const string PackageName = "com.exhard.exgas.d0m2f-tarball";
        private const string RuntimeAssemblyName = "com.exhard.exgas.generated.runtime";
        private const string EditorAssemblyName = "com.exhard.exgas.generated.editor";
        private const string AutoChessAssemblyName = "com.exhard.exgas.autochessdemo";
        private const string RuntimeMarkerPath = "Runtime/GenerationMarker.cs";
        private const string EditorMarkerPath = "Editor/GenerationMarker.cs";
        private const string AutoChessMarkerPath = "AutoChessGenerated/GenerationMarker.cs";
        private const string RuntimeMarkerType = "D0M2F.Tarball.Generated.Runtime.GenerationMarker, com.exhard.exgas.generated.runtime";
        private const string EditorMarkerType = "D0M2F.Tarball.Generated.Editor.GenerationMarker, com.exhard.exgas.generated.editor";
        private const string AutoChessMarkerType = "D0M2F.Tarball.Generated.AutoChess.GenerationMarker, com.exhard.exgas.autochessdemo";

        /// <summary>
        /// 解析命令行并写入一次原子 JSON 观察；强杀操作写完证据后等待外部 harness 终止进程。
        /// </summary>
        public static void Run()
        {
            string outputPath = GetRequiredArgument(OutputArgument);
            string operation = GetRequiredArgument(OperationArgument);
            try
            {
                ProbeResult result = CollectResult(operation);
                WriteResult(outputPath, result);
                if (operation == SignalAndWaitForKillOperation)
                {
                    WaitForExternalKill();
                    return;
                }

                EditorApplication.Exit(result.Passed ? 0 : 31);
            }
            catch (Exception exception)
            {
                WriteResult(outputPath, ProbeResult.CreateFailure(operation, exception.ToString()));
                EditorApplication.Exit(32);
            }
        }

        /// <summary>
        /// 采集当前 package、marker、程序集源文件与 audit ref，并执行同代校验。
        /// </summary>
        private static ProbeResult CollectResult(string operation)
        {
            if (operation != VerifyOperation && operation != SignalAndWaitForKillOperation)
            {
                throw new InvalidOperationException("Unsupported tarball probe operation: " + operation);
            }

            string expectedGeneration = GetRequiredArgument(ExpectedGenerationArgument);
            PackageEvidence package = CollectPackageEvidence();
            string runtimeGeneration = ReadMarkerField(RuntimeMarkerType, "GenerationId");
            string editorGeneration = ReadMarkerField(EditorMarkerType, "GenerationId");
            string autoChessGeneration = ReadMarkerField(AutoChessMarkerType, "GenerationId");
            string runtimeToken = ReadMarkerField(RuntimeMarkerType, "GenerationToken");
            string editorToken = ReadMarkerField(EditorMarkerType, "GenerationToken");
            string autoChessToken = ReadMarkerField(AutoChessMarkerType, "GenerationToken");
            bool sameGeneration = runtimeGeneration == expectedGeneration
                && editorGeneration == expectedGeneration
                && autoChessGeneration == expectedGeneration;
            bool sameToken = runtimeToken == editorToken && editorToken == autoChessToken;

            return CreateResult(
                operation,
                expectedGeneration,
                package,
                runtimeGeneration,
                editorGeneration,
                autoChessGeneration,
                runtimeToken,
                editorToken,
                autoChessToken,
                sameGeneration && sameToken);
        }

        /// <summary>
        /// 构造可由外部 PowerShell 独立复核的 typed Unity 观察结果。
        /// </summary>
        private static ProbeResult CreateResult(
            string operation,
            string expectedGeneration,
            PackageEvidence package,
            string runtimeGeneration,
            string editorGeneration,
            string autoChessGeneration,
            string runtimeToken,
            string editorToken,
            string autoChessToken,
            bool passed)
        {
            return new ProbeResult
            {
                Operation = operation,
                Mode = GetRequiredArgument(ModeArgument),
                Passed = passed,
                Detail = passed ? "三个程序集消费同一 tarball generation。" : "至少一个程序集或 token 未绑定预期 tarball generation。",
                UnityVersion = Application.unityVersion,
                ExpectedGeneration = expectedGeneration,
                ExpectedArchiveSha256 = GetRequiredArgument(ExpectedArchiveShaArgument),
                ActiveGenerationRefGeneration = ReadActiveGenerationRef(),
                PackageName = package.Name,
                PackageVersion = package.Version,
                PackageSource = package.Source,
                PackageId = package.PackageId,
                ResolvedPackagePath = package.ResolvedPath,
                RuntimeGeneration = runtimeGeneration,
                EditorGeneration = editorGeneration,
                AutoChessGeneration = autoChessGeneration,
                RuntimeToken = runtimeToken,
                EditorToken = editorToken,
                AutoChessToken = autoChessToken,
                RuntimeSourceFiles = package.RuntimeSourceFiles,
                EditorSourceFiles = package.EditorSourceFiles,
                AutoChessSourceFiles = package.AutoChessSourceFiles
            };
        }

        /// <summary>
        /// 从 PackageManager 与 CompilationPipeline 收集 package 身份和三个程序集的真实源文件。
        /// </summary>
        private static PackageEvidence CollectPackageEvidence()
        {
            UnityEditor.PackageManager.PackageInfo package = FindRegisteredPackage();
            string resolvedPath = Path.GetFullPath(package.resolvedPath);
            if (!Directory.Exists(resolvedPath))
            {
                throw new DirectoryNotFoundException("Resolved tarball package path does not exist: " + resolvedPath);
            }

            string[] runtimeSources = GetAssemblySourceFiles(RuntimeAssemblyName, resolvedPath);
            string[] editorSources = GetAssemblySourceFiles(EditorAssemblyName, resolvedPath);
            string[] autoChessSources = GetAssemblySourceFiles(AutoChessAssemblyName, resolvedPath);
            EnsureMarkerSource(resolvedPath, RuntimeMarkerPath, runtimeSources);
            EnsureMarkerSource(resolvedPath, EditorMarkerPath, editorSources);
            EnsureMarkerSource(resolvedPath, AutoChessMarkerPath, autoChessSources);
            return new PackageEvidence
            {
                Name = package.name,
                Version = package.version,
                Source = package.source.ToString(),
                PackageId = package.packageId,
                ResolvedPath = resolvedPath,
                RuntimeSourceFiles = runtimeSources,
                EditorSourceFiles = editorSources,
                AutoChessSourceFiles = autoChessSources
            };
        }

        /// <summary>
        /// 查找本夹具唯一注册的 D0-M2F tarball package。
        /// </summary>
        private static UnityEditor.PackageManager.PackageInfo FindRegisteredPackage()
        {
            UnityEditor.PackageManager.PackageInfo[] packages =
                UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
            foreach (UnityEditor.PackageManager.PackageInfo package in packages)
            {
                if (package != null && string.Equals(package.name, PackageName, StringComparison.Ordinal))
                {
                    return package;
                }
            }

            throw new InvalidOperationException("Registered tarball package was not found: " + PackageName);
        }

        /// <summary>
        /// 读取目标程序集全部源文件并转换为可验证的绝对物理路径。
        /// </summary>
        private static string[] GetAssemblySourceFiles(string assemblyName, string resolvedPackagePath)
        {
            UnityEditor.Compilation.Assembly[] assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            foreach (UnityEditor.Compilation.Assembly assembly in assemblies)
            {
                if (!string.Equals(assembly.name, assemblyName, StringComparison.Ordinal))
                {
                    continue;
                }

                string[] sourceFiles = assembly.sourceFiles ?? Array.Empty<string>();
                string[] normalizedFiles = new string[sourceFiles.Length];
                for (int index = 0; index < sourceFiles.Length; index++)
                {
                    normalizedFiles[index] = NormalizeSourcePath(sourceFiles[index], resolvedPackagePath);
                }

                return normalizedFiles;
            }

            throw new InvalidOperationException("Compilation assembly was not found: " + assemblyName);
        }

        /// <summary>
        /// 将 Unity 虚拟 Packages 路径映射到 PackageInfo 报告的解析根。
        /// </summary>
        private static string NormalizeSourcePath(string sourcePath, string resolvedPackagePath)
        {
            string normalized = sourcePath.Replace('\\', '/');
            string packagePrefix = "Packages/" + PackageName + "/";
            if (normalized.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                string relativePath = normalized.Substring(packagePrefix.Length)
                    .Replace('/', Path.DirectorySeparatorChar);
                return Path.GetFullPath(Path.Combine(resolvedPackagePath, relativePath));
            }

            if (Path.IsPathRooted(sourcePath))
            {
                return Path.GetFullPath(sourcePath);
            }

            DirectoryInfo project = Directory.GetParent(Application.dataPath);
            if (project == null)
            {
                throw new InvalidOperationException("Unity project root cannot be resolved.");
            }

            return Path.GetFullPath(Path.Combine(project.FullName, sourcePath));
        }

        /// <summary>
        /// 确认 marker 位于解析根内并由目标程序集实际编译。
        /// </summary>
        private static void EnsureMarkerSource(string resolvedPackagePath, string relativePath, string[] sourceFiles)
        {
            string markerPath = Path.GetFullPath(Path.Combine(
                resolvedPackagePath,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = resolvedPackagePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!markerPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(markerPath))
            {
                throw new InvalidOperationException("Marker escaped or is missing from resolved package: " + markerPath);
            }

            foreach (string sourceFile in sourceFiles)
            {
                if (string.Equals(sourceFile, markerPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            throw new InvalidOperationException("Marker was not compiled from resolved package: " + markerPath);
        }

        /// <summary>
        /// 通过反射读取 generated marker 的常量字段并拒绝缺失或非字符串值。
        /// </summary>
        private static string ReadMarkerField(string assemblyQualifiedType, string fieldName)
        {
            Type markerType = Type.GetType(assemblyQualifiedType, true);
            if (markerType == null)
            {
                throw new InvalidOperationException("Generation marker type was not loaded: " + assemblyQualifiedType);
            }

            System.Reflection.FieldInfo field = markerType.GetField(fieldName);
            if (field == null || field.FieldType != typeof(string))
            {
                throw new InvalidOperationException("Generation marker field is missing: " + fieldName);
            }

            string value = field.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Generation marker field is empty: " + fieldName);
            }

            return value;
        }

        /// <summary>
        /// 读取 fixture 内可选的 ActiveGenerationRef audit record；缺失时返回空字符串。
        /// </summary>
        private static string ReadActiveGenerationRef()
        {
            DirectoryInfo project = Directory.GetParent(Application.dataPath);
            if (project == null)
            {
                throw new InvalidOperationException("Unity project root cannot be resolved.");
            }

            string path = Path.Combine(project.FullName, "ProjectSettings", "GasCodeGen", "ActiveGenerationRef.json");
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            ActiveGenerationRefRecord record = JsonUtility.FromJson<ActiveGenerationRefRecord>(File.ReadAllText(path));
            return record == null || string.IsNullOrWhiteSpace(record.GenerationId)
                ? string.Empty
                : record.GenerationId;
        }

        /// <summary>
        /// 从命令行读取必需参数并拒绝缺失值。
        /// </summary>
        private static string GetRequiredArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.Ordinal))
                {
                    string value = arguments[index + 1];
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            throw new InvalidOperationException("Required command-line argument is missing: " + name);
        }

        /// <summary>
        /// 以 UTF-8 无 BOM 临时文件加 rename 写出唯一观察 JSON。
        /// </summary>
        private static void WriteResult(string outputPath, ProbeResult result)
        {
            string absolutePath = Path.GetFullPath(outputPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Probe output directory is missing.");
            }

            Directory.CreateDirectory(directory);
            if (File.Exists(absolutePath))
            {
                throw new IOException("Probe output already exists: " + absolutePath);
            }

            string temporaryPath = absolutePath + ".tmp-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(result, true) + "\n", new UTF8Encoding(false));
            File.Move(temporaryPath, absolutePath);
        }

        /// <summary>
        /// 保持 Editor 主进程存活，直到外部 harness 执行有界强杀。
        /// </summary>
        private static void WaitForExternalKill()
        {
            while (true)
            {
                Thread.Sleep(1000);
            }
        }
    }

    /// <summary>
    /// 表示 Unity 对一次 tarball 消费的 typed 观察结果。
    /// </summary>
    [Serializable]
    internal sealed class ProbeResult
    {
        public string Operation;
        public string Mode;
        public bool Passed;
        public string Detail;
        public string UnityVersion;
        public string ExpectedGeneration;
        public string ExpectedArchiveSha256;
        public string ActiveGenerationRefGeneration;
        public string PackageName;
        public string PackageVersion;
        public string PackageSource;
        public string PackageId;
        public string ResolvedPackagePath;
        public string RuntimeGeneration;
        public string EditorGeneration;
        public string AutoChessGeneration;
        public string RuntimeToken;
        public string EditorToken;
        public string AutoChessToken;
        public string[] RuntimeSourceFiles;
        public string[] EditorSourceFiles;
        public string[] AutoChessSourceFiles;

        /// <summary>
        /// 将未捕获异常转换为可落盘的失败结果。
        /// </summary>
        public static ProbeResult CreateFailure(string operation, string detail)
        {
            return new ProbeResult
            {
                Operation = operation,
                Passed = false,
                Detail = detail,
                UnityVersion = Application.unityVersion,
                RuntimeSourceFiles = Array.Empty<string>(),
                EditorSourceFiles = Array.Empty<string>(),
                AutoChessSourceFiles = Array.Empty<string>()
            };
        }
    }

    /// <summary>
    /// 表示 package 解析身份与三个程序集源文件集合。
    /// </summary>
    internal sealed class PackageEvidence
    {
        public string Name;
        public string Version;
        public string Source;
        public string PackageId;
        public string ResolvedPath;
        public string[] RuntimeSourceFiles;
        public string[] EditorSourceFiles;
        public string[] AutoChessSourceFiles;
    }

    /// <summary>
    /// 表示仅用于 SelectorConflict 的 audit ref 最小记录。
    /// </summary>
    [Serializable]
    internal sealed class ActiveGenerationRefRecord
    {
        public string GenerationId;
        public string ArchiveSha256;
        public string Role;
    }
}
