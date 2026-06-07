using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
#if UNITY_EDITOR
using UnityEngine;
#endif

namespace GAS.Editor
{
#if !GAS_CODEGEN_BOOTSTRAP_ONLY
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
                if (string.Equals(mode, "sourcegen-all", StringComparison.OrdinalIgnoreCase))
                    return GasCodeGenPipeline.TryRunAll(refreshAssetDatabase: false) ? 0 : 3;

                if (string.Equals(mode, "autochess", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mode, "sourcegen-autochess", StringComparison.OrdinalIgnoreCase))
                {
                    return GasCodeGenPipeline.TryRunAutoChessDemo(refreshAssetDatabase: false) ? 0 : 3;
                }

                if (string.Equals(mode, "sourcegen", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mode, "core", StringComparison.OrdinalIgnoreCase)
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

        public static GasCodeGenContext Create(bool forceRefresh = false)
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
            var outputDir = ResolveProjectPath(projectRoot, codeGenSettings.OutputPath);
            var rowTypes = GasRowScanner.Scan(forceRefresh);
            var rows = RowMetadataFactory.BuildAll(rowTypes, codeGenSettings);
            var inputHash = ComputeInputHash(rows);

            return new GasCodeGenContext(
                projectRoot,
                outputDir,
                codeGenSettings.RootNamespace,
                inputHash,
                codeGenSettings,
                rowTypes,
                rows);
        }

        private static string ResolveProjectPath(string projectRoot, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return projectRoot;

            return Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(projectRoot, path));
        }

        private static string ComputeInputHash(IReadOnlyList<RowMetadata> rows)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                builder.Append(row.RowType.AssemblyQualifiedName).Append('|')
                    .Append(row.DomainName).Append('|')
                    .Append(row.CodeFieldName).Append('|')
                    .Append(string.Join(",", row.BakerKeyFieldNames ?? Array.Empty<string>())).Append('|')
                    .Append(row.DefinitionKind).Append('|')
                    .Append(row.RowFactoryTypeName).Append('|')
                    .Append(row.RowFactoryMethodName).Append('|');

                var members = row.BlobMembers;
                for (var j = 0; j < members.Count; j++)
                {
                    builder.Append(members[j].Name).Append(':')
                        .Append(members[j].BlobTypeName).Append(':')
                        .Append(members[j].RowAccessor).Append(';');
                }

                var rowValues = row.RowValues ?? Array.Empty<RowValueSnapshot>();
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

                builder.AppendLine();
            }

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            var result = new StringBuilder(hash.Length * 2);
            for (var i = 0; i < hash.Length; i++)
                result.Append(hash[i].ToString("x2"));
            return result.ToString();
        }

        private static void AppendRowValueHash(StringBuilder builder, object row)
        {
            if (row == null)
            {
                builder.Append("<null>");
                return;
            }

            var rowType = row.GetType();
            foreach (var field in rowType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                builder.Append(field.Name).Append('=');
                AppendValue(builder, field.GetValue(row));
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

            builder.Append(value);
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
