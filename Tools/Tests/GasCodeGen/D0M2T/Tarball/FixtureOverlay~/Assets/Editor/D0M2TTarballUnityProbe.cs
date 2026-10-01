using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace GAS.Tests.D0M2T.Tarball
{
    /// <summary>
    /// 在一次性 Unity 工程内采集 immutable tarball 的真实解析根、marker 与三程序集编译图。
    /// </summary>
    public static class D0M2TTarballUnityProbe
    {
        private const string OutputArgument = "-d0m2tOutput";
        private const string ExpectedGenerationArgument = "-d0m2tExpectedGeneration";
        private const string ExpectedArchiveShaArgument = "-d0m2tExpectedArchiveSha";
        private const string PackageName = "com.exhard.exgas.d0m2f-tarball";
        private const string RuntimeAssemblyName = "com.exhard.exgas.generated.runtime";
        private const string EditorAssemblyName = "com.exhard.exgas.generated.editor";
        private const string AutoChessAssemblyName = "com.exhard.exgas.autochessdemo";
        private const string RuntimeMarkerType =
            "D0M2F.Tarball.Generated.Runtime.GenerationMarker, com.exhard.exgas.generated.runtime";
        private const string EditorMarkerType =
            "D0M2F.Tarball.Generated.Editor.GenerationMarker, com.exhard.exgas.generated.editor";
        private const string AutoChessMarkerType =
            "D0M2F.Tarball.Generated.AutoChess.GenerationMarker, com.exhard.exgas.autochessdemo";

        /// <summary>
        /// 读取命令行、持久化一次 typed 原始观察，并以退出码显式返回验证结果。
        /// </summary>
        public static void Run()
        {
            string outputPath = GetRequiredArgument(OutputArgument);
            try
            {
                string generation = GetRequiredArgument(ExpectedGenerationArgument);
                string archiveSha = GetRequiredArgument(ExpectedArchiveShaArgument);
                TarballProbeResult result = CollectResult(generation, archiveSha);
                WriteResult(outputPath, result);
                EditorApplication.Exit(result.Passed ? 0 : 41);
            }
            catch (Exception exception)
            {
                WriteResult(outputPath, TarballProbeResult.CreateFailure(exception.ToString()));
                EditorApplication.Exit(42);
            }
        }

        /// <summary>
        /// 汇总 package 与三程序集证据，并要求它们全部绑定期望 generation 和 token。
        /// </summary>
        private static TarballProbeResult CollectResult(string expectedGeneration, string expectedArchiveSha)
        {
            UnityEditor.PackageManager.PackageInfo package = FindPackage();
            string resolvedPath = Path.GetFullPath(package.resolvedPath);
            AssertPackageCachePath(resolvedPath);
            AssemblyProbe[] assemblies = new[]
            {
                CollectAssembly(RuntimeAssemblyName, RuntimeMarkerType, resolvedPath),
                CollectAssembly(EditorAssemblyName, EditorMarkerType, resolvedPath),
                CollectAssembly(AutoChessAssemblyName, AutoChessMarkerType, resolvedPath)
            };
            bool passed = TestExpectedGeneration(assemblies, expectedGeneration);
            return new TarballProbeResult
            {
                Passed = passed,
                Detail = passed ? "三程序集消费同一预期 tarball。" : "程序集 generation 或 token 发生分裂。",
                UnityVersion = Application.unityVersion,
                ExpectedGeneration = expectedGeneration,
                ExpectedArchiveSha256 = expectedArchiveSha,
                PackageName = package.name,
                PackageVersion = package.version,
                PackageSource = package.source.ToString(),
                PackageId = package.packageId,
                ResolvedPackagePath = resolvedPath,
                Assemblies = assemblies,
                CompileGraphSha256 = ComputeCompileGraphHash(assemblies)
            };
        }

        /// <summary>
        /// 在 Unity 已注册 package 集合中精确查找唯一测试 tarball。
        /// </summary>
        private static UnityEditor.PackageManager.PackageInfo FindPackage()
        {
            UnityEditor.PackageManager.PackageInfo match = null;
            foreach (UnityEditor.PackageManager.PackageInfo package in
                     UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
            {
                if (package != null && string.Equals(package.name, PackageName, StringComparison.Ordinal))
                {
                    if (match != null)
                    {
                        throw new InvalidOperationException("Tarball package registration is not unique.");
                    }

                    match = package;
                }
            }

            return match ?? throw new InvalidOperationException("Tarball package was not registered: " + PackageName);
        }

        /// <summary>
        /// 拒绝 Unity 把本地 tarball 解析到 fixture PackageCache 之外。
        /// </summary>
        private static void AssertPackageCachePath(string resolvedPackagePath)
        {
            DirectoryInfo project = Directory.GetParent(Application.dataPath);
            if (project == null)
            {
                throw new InvalidOperationException("Unity project root cannot be resolved.");
            }

            string cacheRoot = Path.GetFullPath(Path.Combine(project.FullName, "Library", "PackageCache"))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!resolvedPackagePath.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Resolved package escaped PackageCache: " + resolvedPackagePath);
            }
        }

        /// <summary>
        /// 采集单个目标程序集的 marker、源文件、引用、define 与 Roslyn 输入。
        /// </summary>
        private static AssemblyProbe CollectAssembly(
            string assemblyName,
            string markerTypeName,
            string resolvedPackagePath)
        {
            UnityEditor.Compilation.Assembly assembly = FindAssembly(assemblyName);
            Type markerType = Type.GetType(markerTypeName, true);
            string[] sourceFiles = NormalizePaths(assembly.sourceFiles);
            AssertSourcesBelongToPackage(sourceFiles, resolvedPackagePath, assemblyName);
            return new AssemblyProbe
            {
                Name = assembly.name,
                Generation = ReadMarker(markerType, "GenerationId"),
                GenerationToken = ReadMarker(markerType, "GenerationToken"),
                OutputPath = NormalizePath(assembly.outputPath),
                SourceFiles = sourceFiles,
                CompiledAssemblyReferences = NormalizePaths(assembly.compiledAssemblyReferences),
                Defines = SortCopy(assembly.defines),
                RoslynAdditionalFilePaths = NormalizePaths(assembly.compilerOptions.RoslynAdditionalFilePaths)
            };
        }

        /// <summary>
        /// 在当前 Editor 编译图中精确查找目标程序集，缺失时 fail closed。
        /// </summary>
        private static UnityEditor.Compilation.Assembly FindAssembly(string assemblyName)
        {
            foreach (UnityEditor.Compilation.Assembly assembly in
                     CompilationPipeline.GetAssemblies(AssembliesType.Editor))
            {
                if (string.Equals(assembly.name, assemblyName, StringComparison.Ordinal))
                {
                    return assembly;
                }
            }

            throw new InvalidOperationException("Compilation assembly was not found: " + assemblyName);
        }

        /// <summary>
        /// 读取 generated marker 的 public string 常量，拒绝缺失或空值。
        /// </summary>
        private static string ReadMarker(Type markerType, string fieldName)
        {
            if (markerType == null)
            {
                throw new InvalidOperationException("Generation marker type was not loaded.");
            }

            System.Reflection.FieldInfo field = markerType.GetField(fieldName);
            string value = field == null ? string.Empty : field.GetValue(null) as string;
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("Generation marker field is missing: " + fieldName);
            }

            return value;
        }

        /// <summary>
        /// 确认程序集至少一个源文件存在，且全部来自同一解析 package 根。
        /// </summary>
        private static void AssertSourcesBelongToPackage(
            string[] sourceFiles,
            string resolvedPackagePath,
            string assemblyName)
        {
            string prefix = resolvedPackagePath
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (sourceFiles.Length == 0)
            {
                throw new InvalidOperationException("Assembly has no source files: " + assemblyName);
            }

            foreach (string sourceFile in sourceFiles)
            {
                if (!sourceFile.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(sourceFile))
                {
                    throw new InvalidOperationException("Assembly source escaped resolved package: " + sourceFile);
                }
            }
        }

        /// <summary>
        /// 要求三程序集 generation 相同、token 相同，并精确命中期望 generation。
        /// </summary>
        private static bool TestExpectedGeneration(AssemblyProbe[] assemblies, string expectedGeneration)
        {
            if (assemblies.Length != 3)
            {
                return false;
            }

            string expectedToken = assemblies[0].GenerationToken;
            foreach (AssemblyProbe assembly in assemblies)
            {
                if (!string.Equals(assembly.Generation, expectedGeneration, StringComparison.Ordinal)
                    || !string.Equals(assembly.GenerationToken, expectedToken, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return !string.IsNullOrWhiteSpace(expectedToken);
        }

        /// <summary>
        /// 将 Unity 返回路径规范为 fixture 内绝对路径，便于外部 harness 复核边界。
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

            DirectoryInfo project = Directory.GetParent(Application.dataPath);
            if (project == null)
            {
                throw new InvalidOperationException("Unity project root cannot be resolved.");
            }

            return Path.GetFullPath(Path.Combine(project.FullName, path));
        }

        /// <summary>
        /// 规范化并排序路径数组，使证据不依赖 Unity 枚举顺序。
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
        /// 对规范化后的三程序集编译图计算稳定 SHA-256，供 A/B 漂移复核。
        /// </summary>
        private static string ComputeCompileGraphHash(AssemblyProbe[] assemblies)
        {
            StringBuilder builder = new StringBuilder();
            foreach (AssemblyProbe assembly in assemblies)
            {
                AppendGraphValues(builder, assembly.Name, new[] { assembly.Generation, assembly.GenerationToken });
                AppendGraphValues(builder, "source", assembly.SourceFiles);
                AppendGraphValues(builder, "reference", assembly.CompiledAssemblyReferences);
                AppendGraphValues(builder, "define", assembly.Defines);
                AppendGraphValues(builder, "additional", assembly.RoslynAdditionalFilePaths);
            }

            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(new UTF8Encoding(false).GetBytes(builder.ToString()));
                return ToLowerHex(digest);
            }
        }

        /// <summary>
        /// 以长度前缀追加编译图字段，避免字符串拼接产生边界歧义。
        /// </summary>
        private static void AppendGraphValues(StringBuilder builder, string label, string[] values)
        {
            builder.Append(label.Length).Append(':').Append(label).Append('\n');
            foreach (string value in values ?? Array.Empty<string>())
            {
                string safeValue = value ?? string.Empty;
                builder.Append(safeValue.Length).Append(':').Append(safeValue).Append('\n');
            }
        }

        /// <summary>
        /// 将哈希字节转换为兼容 C# 9 的小写十六进制文本。
        /// </summary>
        private static string ToLowerHex(byte[] bytes)
        {
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }

        /// <summary>
        /// 读取紧随指定名称后的非空命令行参数，缺失时显式失败。
        /// </summary>
        private static string GetRequiredArgument(string argumentName)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argumentName, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(arguments[index + 1]))
                {
                    return arguments[index + 1];
                }
            }

            throw new ArgumentException("Required command-line argument is missing: " + argumentName);
        }

        /// <summary>
        /// 以 UTF-8 无 BOM、CreateNew 和原子 rename 写出唯一原始观察 JSON。
        /// </summary>
        private static void WriteResult(string outputPath, TarballProbeResult result)
        {
            string absolutePath = Path.GetFullPath(outputPath);
            string parent = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrWhiteSpace(parent) || File.Exists(absolutePath))
            {
                throw new InvalidOperationException("Probe output must be a fresh file with a parent directory.");
            }

            Directory.CreateDirectory(parent);
            string temporaryPath = absolutePath + ".tmp-" + Guid.NewGuid().ToString("N");
            string json = JsonUtility.ToJson(result, true).Replace("\r\n", "\n") + "\n";
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            File.Move(temporaryPath, absolutePath);
        }
    }

    /// <summary>
    /// 表示 Unity 对一次 immutable tarball 消费的 typed 原始观察。
    /// </summary>
    [Serializable]
    internal sealed class TarballProbeResult
    {
        public bool Passed;
        public string Detail;
        public string UnityVersion;
        public string ExpectedGeneration;
        public string ExpectedArchiveSha256;
        public string PackageName;
        public string PackageVersion;
        public string PackageSource;
        public string PackageId;
        public string ResolvedPackagePath;
        public AssemblyProbe[] Assemblies;
        public string CompileGraphSha256;

        /// <summary>
        /// 将入口异常转换为可落盘的显式失败结果。
        /// </summary>
        public static TarballProbeResult CreateFailure(string detail)
        {
            return new TarballProbeResult
            {
                Passed = false,
                Detail = detail,
                UnityVersion = Application.unityVersion,
                Assemblies = Array.Empty<AssemblyProbe>()
            };
        }
    }

    /// <summary>
    /// 表示一个冻结程序集的 generation 与 CompilationPipeline 输入快照。
    /// </summary>
    [Serializable]
    internal sealed class AssemblyProbe
    {
        public string Name;
        public string Generation;
        public string GenerationToken;
        public string OutputPath;
        public string[] SourceFiles;
        public string[] CompiledAssemblyReferences;
        public string[] Defines;
        public string[] RoslynAdditionalFilePaths;
    }
}
