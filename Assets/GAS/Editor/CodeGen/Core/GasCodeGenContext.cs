using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace GAS.Editor
{
#if !GAS_CODEGEN_BOOTSTRAP_ONLY
    /// <summary>
    /// 提供离线与 Unity Editor 共用的 CodeGen 命令路由，并默认执行完整同代发布。
    /// </summary>
    public static class GasCodeGenCli
    {
        public static int Run(string[] args)
        {
            try
            {
                var projectRoot = GasCodeGenEnvironment.ResolveProjectRootArgument(args);
                GasCodeGenEnvironment.UseOfflineProjectRoot(projectRoot);

#if UNITY_EDITOR
                if (!GasCodeGenEnvironment.IsOffline && !GasCodeGenProcessGate.RunDefault())
                    return 2;
#endif

                var mode = ResolveMode(args);
                if (string.Equals(mode, "sourcegen-all", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mode, "sourcegen", StringComparison.OrdinalIgnoreCase))
                    return GasCodeGenPipeline.TryRunAll(refreshAssetDatabase: false) ? 0 : 3;

                if (string.Equals(mode, "autochess", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mode, "sourcegen-autochess", StringComparison.OrdinalIgnoreCase))
                {
                    return GasCodeGenPipeline.TryRunAutoChessDemo(refreshAssetDatabase: false) ? 0 : 3;
                }

                if (string.Equals(mode, "core", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mode, "sourcegen-core", StringComparison.OrdinalIgnoreCase))
                {
                    return GasCodeGenPipeline.TryRunCore(refreshAssetDatabase: false) ? 0 : 3;
                }

                GasCodeGenEnvironment.LogError($"Unknown codegen mode: {mode}");
                return 2;
            }
            catch (Exception ex)
            {
                GasCodeGenEnvironment.LogException(ex);
                return 1;
            }
        }

        private static string ResolveMode(string[] args)
        {
            var explicitMode = string.Empty;
            if (args != null)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    if (arg == "--mode" && i + 1 < args.Length)
                    {
                        explicitMode = args[i + 1];
                        break;
                    }

                    const string prefix = "--mode=";
                    if (arg != null && arg.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        explicitMode = arg.Substring(prefix.Length);
                        break;
                    }
                }
            }

            return string.IsNullOrWhiteSpace(explicitMode)
                ? "sourcegen"
                : explicitMode;
        }
    }
#endif

    internal static class GasCodeGenEnvironment
    {
        private static string s_projectRootOverride;
        private static GasCodeGenSettings s_activeSettings;

        public static bool IsOffline => !string.IsNullOrWhiteSpace(s_projectRootOverride);

        public static GasCodeGenSettings ActiveSettings => s_activeSettings;

        public static void UseOfflineProjectRoot(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("Project root is required.", nameof(projectRoot));

            s_projectRootOverride = Path.GetFullPath(projectRoot);
        }

        public static string ProjectRoot
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(s_projectRootOverride))
                    return s_projectRootOverride;

#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
#else
                return Directory.GetCurrentDirectory();
#endif
            }
        }

        public static void SetActiveSettings(GasCodeGenSettings settings)
        {
            s_activeSettings = settings;
        }

        public static string ResolveProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return ProjectRoot;

            return Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(ProjectRoot, path));
        }

        public static string ResolveProjectRootArgument(string[] args)
        {
            if (args != null)
            {
                for (var i = 0; i < args.Length; i++)
                {
                    var arg = args[i];
                    if (arg == "--projectRoot" && i + 1 < args.Length)
                        return args[i + 1];

                    const string prefix = "--projectRoot=";
                    if (arg != null && arg.StartsWith(prefix, StringComparison.Ordinal))
                        return arg.Substring(prefix.Length);
                }
            }

            return Directory.GetCurrentDirectory();
        }

        public static void RefreshAssetDatabase()
        {
            if (IsOffline)
                return;

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }

        public static void ClearProgressBar()
        {
            if (IsOffline)
                return;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.ClearProgressBar();
#endif
        }

        public static void DisplayProgressBar(string title, string info, float progress)
        {
            if (IsOffline)
                return;

#if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayProgressBar(title, info, progress);
#endif
        }

        public static void Log(string message)
        {
            if (IsOffline)
                Console.WriteLine(message);
#if UNITY_EDITOR
            else
                UnityEngine.Debug.Log(message);
#else
            else
                Console.WriteLine(message);
#endif
        }

        public static void LogError(string message)
        {
            if (IsOffline)
                Console.Error.WriteLine(message);
#if UNITY_EDITOR
            else
                UnityEngine.Debug.LogError(message);
#else
            else
                Console.Error.WriteLine(message);
#endif
        }

        public static void LogException(Exception ex)
        {
            if (IsOffline)
                WriteException(ex);
#if UNITY_EDITOR
            else
                UnityEngine.Debug.LogException(ex);
#else
            else
                WriteException(ex);
#endif
        }

        private static void WriteException(Exception ex)
        {
            var current = ex;
            var depth = 0;
            while (current != null)
            {
                Console.Error.WriteLine($"[{depth}] {current.GetType().FullName}: {SafeString(() => current.Message)}");
                var stackTrace = SafeString(() => current.StackTrace);
                if (!string.IsNullOrWhiteSpace(stackTrace))
                    Console.Error.WriteLine(stackTrace);

                current = current.InnerException;
                depth++;
            }
        }

        private static string SafeString(Func<string> read)
        {
            try
            {
                return read() ?? string.Empty;
            }
            catch (Exception nested)
            {
                return $"<failed to read exception text: {nested.GetType().FullName}>";
            }
        }
    }

    /// <summary>
    /// 冻结一次生成所使用的设置、typed rows、输入身份与物理输出目录。
    /// </summary>
    public sealed class GasCodeGenContext
    {
        private GasCodeGenContext(
            string projectRoot,
            string outputDir,
            string rootNamespace,
            string inputHash,
            GasCodeGenSettings settings,
            IReadOnlyList<Type> rowTypes,
            IReadOnlyList<RowMetadata> rows)
        {
            ProjectRoot = projectRoot;
            OutputDir = outputDir;
            RootNamespace = rootNamespace;
            InputHash = inputHash;
            Settings = settings;
            RowTypes = rowTypes;
            Rows = rows;
        }

        public string ProjectRoot { get; }

        public string OutputDir { get; }

        public string RootNamespace { get; }

        public string InputHash { get; }

        public GasCodeGenSettings Settings { get; }

        public IReadOnlyList<Type> RowTypes { get; }

        public IReadOnlyList<RowMetadata> Rows { get; }

        public int OrphansDeleted { get; internal set; }

        /// <summary>
        /// 以项目配置的 active 输出目录创建代码生成上下文。
        /// </summary>
        public static GasCodeGenContext Create(bool forceRefresh = false)
        {
            return Create(forceRefresh, null);
        }

        /// <summary>
        /// 以指定物理输出目录创建上下文，使 phase 可在独立 candidate 中执行。
        /// </summary>
        internal static GasCodeGenContext Create(bool forceRefresh, string outputDirOverride)
        {
            var codeGenSettings = GasCodeGenEnvironment.IsOffline
                ? GasCodeGenSettings.CreateDefault()
#if UNITY_EDITOR
                : GasCodeGenSettings.From(GASSettingAsset.LoadOrCreate());
#else
                : GasCodeGenSettings.CreateDefault();
#endif
            GasCodeGenEnvironment.SetActiveSettings(codeGenSettings);
            var projectRoot = GasCodeGenEnvironment.ProjectRoot;
            var outputDir = string.IsNullOrWhiteSpace(outputDirOverride)
                ? ResolveProjectPath(projectRoot, codeGenSettings.OutputPath)
                : Path.GetFullPath(outputDirOverride);
            var rowTypes = GasRowScanner.Scan(forceRefresh);
            var rows = RowMetadataFactory.BuildAll(rowTypes, codeGenSettings);
            var inputHash = ComputeInputHash(rows, projectRoot, codeGenSettings);

            return new GasCodeGenContext(
                projectRoot,
                outputDir,
                codeGenSettings.RootNamespace,
                inputHash,
                codeGenSettings,
                rowTypes,
                rows);
        }

        /// <summary>
        /// 复用同一输入快照创建另一物理输出视图，确保 core 与 demo 属于同一代 candidate。
        /// </summary>
        internal GasCodeGenContext WithOutputDir(string outputDir)
        {
            if (string.IsNullOrWhiteSpace(outputDir))
                throw new ArgumentException("Output directory is required.", nameof(outputDir));

            return new GasCodeGenContext(
                ProjectRoot,
                Path.GetFullPath(outputDir),
                RootNamespace,
                InputHash,
                Settings,
                RowTypes,
                Rows);
        }

        private static string ResolveProjectPath(string projectRoot, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return projectRoot;

            return Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(projectRoot, path));
        }

        /// <summary>
        /// 对 typed row 快照与全部 artifact-producing JSON 输入计算同一 SHA-256 身份。
        /// </summary>
        private static string ComputeInputHash(
            IReadOnlyList<RowMetadata> rows,
            string projectRoot,
            GasCodeGenSettings settings)
        {
            var builder = new StringBuilder();
            AppendInputHashField(builder, "OutputPath", settings.OutputPath);
            AppendInputHashField(builder, "RootNamespace", settings.RootNamespace);
            AppendInputHashField(
                builder,
                "RowTypePrefixesToStrip",
                string.Join(",", settings.RowTypePrefixesToStrip ?? Array.Empty<string>()));
            AppendInputHashField(builder, "ConfigProjectPath", settings.ConfigProjectPath);
            AppendInputHashField(builder, "LubanCodeOutputPath", settings.LubanCodeOutputPath);
            AppendInputHashField(builder, "LubanDataOutputPath", settings.LubanDataOutputPath);
            var canonicalRows = new List<RowMetadata>(rows ?? Array.Empty<RowMetadata>());
            canonicalRows.Sort(CompareRows);
            for (var i = 0; i < canonicalRows.Count; i++)
            {
                var row = canonicalRows[i];
                builder.Append(row.RowType.FullName ?? row.RowType.Name).Append('|')
                    .Append(row.DomainName).Append('|')
                    .Append(row.CodeFieldName).Append('|')
                    .Append(string.Join(",", row.BakerKeyFieldNames ?? Array.Empty<string>())).Append('|')
                    .Append(row.DefinitionKind).Append('|')
                    .Append(row.RowFactoryTypeName).Append('|')
                    .Append(row.RowFactoryMethodName).Append('|');

                var members = new List<BlobMemberInfo>(row.BlobMembers ?? Array.Empty<BlobMemberInfo>());
                members.Sort(CompareBlobMembers);
                for (var j = 0; j < members.Count; j++)
                {
                    builder.Append(members[j].Name).Append(':')
                        .Append(members[j].BlobTypeName).Append(':')
                        .Append(members[j].RowAccessor).Append(';');
                }

                var rowValues = new List<RowValueSnapshot>(row.RowValues ?? Array.Empty<RowValueSnapshot>());
                rowValues.Sort(CompareRowValues);
                builder.Append("|Rows=").Append(rowValues.Count).Append('|');
                for (var j = 0; j < rowValues.Count; j++)
                {
                    builder.Append(rowValues[j].Code).Append(':');
                    var bakerKeyValues = rowValues[j].BakerKeyValues ?? Array.Empty<int>();
                    for (var k = 0; k < bakerKeyValues.Count; k++)
                        builder.Append(bakerKeyValues[k]).Append(',');
                    builder.Append(':');
                    AppendRowValueHash(builder, rowValues[j].Row);
                    builder.Append('|');
                }

                builder.Append('\n');
            }

            AppendArtifactInputFiles(builder, projectRoot, settings);

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            var result = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                result.Append(hash[i].ToString("x2"));
            return result.ToString();
        }

        /// <summary>
        /// 以长度前缀写入一个设置字段，避免配置值中的分隔符造成 hash 拼接歧义。
        /// </summary>
        private static void AppendInputHashField(StringBuilder builder, string name, string value)
        {
            var resolvedName = name ?? string.Empty;
            var resolvedValue = value ?? string.Empty;
            builder.Append(resolvedName.Length).Append(':').Append(resolvedName)
                .Append('|')
                .Append(resolvedValue.Length).Append(':').Append(resolvedValue)
                .Append('\n');
        }

        /// <summary>
        /// 纳入 Luban JSON 与 AutoChess 场景 sidecar，防止相同 row hash 掩盖 artifact byte 变化。
        /// </summary>
        private static void AppendArtifactInputFiles(
            StringBuilder builder,
            string projectRoot,
            GasCodeGenSettings settings)
        {
            var paths = new List<string>();
            var lubanRoot = ResolveProjectPath(projectRoot, settings.LubanDataOutputPath);
            if (Directory.Exists(lubanRoot))
                paths.AddRange(Directory.GetFiles(lubanRoot, "*.json", SearchOption.AllDirectories));

            paths.Add(Path.Combine(
                ResolveProjectPath(projectRoot, settings.ConfigProjectPath),
                "Datas",
                "AutoChessDemo",
                "autochess.sourcegen.json"));
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths)
                AppendArtifactInputFile(builder, projectRoot, path);
        }

        /// <summary>
        /// 以项目相对路径、长度和原始 bytes SHA-256 写入一个输入文件身份。
        /// </summary>
        private static void AppendArtifactInputFile(
            StringBuilder builder,
            string projectRoot,
            string path)
        {
            var fullPath = Path.GetFullPath(path);
            var normalizedProjectRoot = Path.GetFullPath(projectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!IsUnderRoot(fullPath, normalizedProjectRoot))
                throw new InvalidDataException("Artifact-producing input escapes project root: " + fullPath);

            var relativePath = fullPath.Substring(normalizedProjectRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/');
            builder.Append("InputFile|").Append(relativePath).Append('|');
            if (!File.Exists(fullPath))
            {
                builder.Append("<missing>\n");
                return;
            }

            var bytes = File.ReadAllBytes(fullPath);
            using var sha = SHA256.Create();
            builder.Append(bytes.Length).Append('|')
                .Append(Convert.ToBase64String(sha.ComputeHash(bytes)))
                .Append('\n');
        }

        /// <summary>
        /// 判断绝对路径是否等于项目根或位于其真实目录边界下。
        /// </summary>
        private static bool IsUnderRoot(string path, string root)
        {
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith(
                       root + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith(
                       root + Path.AltDirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }

        // 按行类型全名稳定排序，避免程序集枚举顺序影响哈希。
        private static int CompareRows(RowMetadata left, RowMetadata right)
        {
            var leftName = left?.RowType?.FullName ?? left?.RowType?.Name ?? string.Empty;
            var rightName = right?.RowType?.FullName ?? right?.RowType?.Name ?? string.Empty;
            return string.CompareOrdinal(leftName, rightName);
        }

        // 按 schema 名称稳定排序 Blob 成员，避免反射返回顺序影响输入哈希。
        private static int CompareBlobMembers(BlobMemberInfo left, BlobMemberInfo right)
        {
            var result = string.CompareOrdinal(left?.Name, right?.Name);
            if (result != 0)
                return result;

            result = string.CompareOrdinal(left?.BlobTypeName, right?.BlobTypeName);
            if (result != 0)
                return result;

            return string.CompareOrdinal(left?.RowAccessor, right?.RowAccessor);
        }

        // 按业务键和规范化行值稳定排序，消除源数据行顺序漂移。
        private static int CompareRowValues(RowValueSnapshot left, RowValueSnapshot right)
        {
            var result = left.Code.CompareTo(right.Code);
            if (result != 0)
                return result;

            var leftKeys = left.BakerKeyValues ?? Array.Empty<int>();
            var rightKeys = right.BakerKeyValues ?? Array.Empty<int>();
            result = leftKeys.Count.CompareTo(rightKeys.Count);
            if (result != 0)
                return result;

            for (var i = 0; i < leftKeys.Count; i++)
            {
                result = leftKeys[i].CompareTo(rightKeys[i]);
                if (result != 0)
                    return result;
            }

            var leftText = BuildCanonicalRowValue(left.Row);
            var rightText = BuildCanonicalRowValue(right.Row);
            return string.CompareOrdinal(leftText, rightText);
        }

        // 将行值转成可比较的规范文本，用于消除源数据行顺序造成的哈希漂移。
        private static string BuildCanonicalRowValue(object row)
        {
            var builder = new StringBuilder();
            AppendRowValueHash(builder, row);
            return builder.ToString();
        }

        private static void AppendRowValueHash(StringBuilder builder, object row)
        {
            if (row == null)
            {
                builder.Append("<null>");
                return;
            }

            var rowType = row.GetType();
            var members = new List<MemberInfo>();
            members.AddRange(rowType.GetFields(BindingFlags.Public | BindingFlags.Instance));
            foreach (var property in rowType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.CanRead
                    && property.GetMethod != null
                    && property.GetIndexParameters().Length == 0)
                    members.Add(property);
            }

            members.Sort((left, right) =>
            {
                var result = string.CompareOrdinal(left.Name, right.Name);
                return result != 0
                    ? result
                    : string.CompareOrdinal(left.MemberType.ToString(), right.MemberType.ToString());
            });

            foreach (var member in members)
            {
                builder.Append(member.Name).Append(':').Append(member.MemberType).Append('=');
                var field = member as FieldInfo;
                AppendValue(builder, field != null
                    ? field.GetValue(row)
                    : ((PropertyInfo)member).GetValue(row, null));
                builder.Append(';');
            }
        }

        private static void AppendValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("<null>");
                return;
            }

            if (value is string text)
            {
                builder.Append('"').Append(text).Append('"');
                return;
            }

            if (value is IEnumerable enumerable && !(value is string))
            {
                builder.Append('[');
                foreach (var item in enumerable)
                {
                    AppendValue(builder, item);
                    builder.Append(',');
                }

                builder.Append(']');
                return;
            }

            var formattable = value as IFormattable;
            builder.Append(formattable != null
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value.ToString());
        }
    }

    public sealed class GasCodeGenSettings
    {
        private GasCodeGenSettings(
            string outputPath,
            string rootNamespace,
            IReadOnlyList<string> rowTypePrefixesToStrip,
            string configProjectPath,
            string lubanCodeOutputPath,
            string lubanDataOutputPath)
        {
            OutputPath = outputPath;
            RootNamespace = rootNamespace;
            RowTypePrefixesToStrip = rowTypePrefixesToStrip;
            ConfigProjectPath = configProjectPath;
            LubanCodeOutputPath = lubanCodeOutputPath;
            LubanDataOutputPath = lubanDataOutputPath;
        }

        public string OutputPath { get; }

        public string RootNamespace { get; }

        public IReadOnlyList<string> RowTypePrefixesToStrip { get; }

        public string ConfigProjectPath { get; }

        public string LubanCodeOutputPath { get; }

        public string LubanDataOutputPath { get; }

#if UNITY_EDITOR
        public static GasCodeGenSettings From(GASSettingAsset setting)
        {
            return new GasCodeGenSettings(
                setting.CodeGeneratePath,
                string.IsNullOrWhiteSpace(setting.CodeGenerateRootNamespace)
                    ? "GAS.Runtime.Generated"
                    : setting.CodeGenerateRootNamespace.Trim(),
                ParseCsv(setting.CodeGenerateRowTypePrefixesToStrip),
                setting.ConfigProjectPath,
                setting.TableClassCodeOutpuPath,
                setting.TableOutpuPath);
        }
#endif

        public static GasCodeGenSettings CreateDefault()
        {
            return new GasCodeGenSettings(
                "Assets/GAS/Generated/CodeGen",
                "GAS.Runtime.Generated",
                Array.Empty<string>(),
                "EX_GAS_Config/ProjectConfigTable/exgas_config",
                "Assets/DataGenerated/Luban/CSharp",
                "Assets/DataGenerated/Luban/Json/GAS");
        }

        private static IReadOnlyList<string> ParseCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();

            var result = new List<string>();
            var parts = value.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                var item = parts[i].Trim();
                if (item.Length == 0)
                    continue;

                if (!result.Contains(item))
                    result.Add(item);
            }

            result.Sort((left, right) => right.Length.CompareTo(left.Length));
            return result;
        }
    }
}
