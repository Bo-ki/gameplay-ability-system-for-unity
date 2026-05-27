using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GAS.Editor
{
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
            var setting = GASSettingAsset.LoadOrCreate();
            var codeGenSettings = GasCodeGenSettings.From(setting);
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
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
            string lubanCodeOutputPath,
            string lubanDataOutputPath)
        {
            OutputPath = outputPath;
            RootNamespace = rootNamespace;
            RowTypePrefixesToStrip = rowTypePrefixesToStrip;
            LubanCodeOutputPath = lubanCodeOutputPath;
            LubanDataOutputPath = lubanDataOutputPath;
        }

        public string OutputPath { get; }

        public string RootNamespace { get; }

        public IReadOnlyList<string> RowTypePrefixesToStrip { get; }

        public string LubanCodeOutputPath { get; }

        public string LubanDataOutputPath { get; }

        public static GasCodeGenSettings From(GASSettingAsset setting)
        {
            return new GasCodeGenSettings(
                setting.CodeGeneratePath,
                string.IsNullOrWhiteSpace(setting.CodeGenerateRootNamespace)
                    ? "GAS.Runtime.Generated"
                    : setting.CodeGenerateRootNamespace.Trim(),
                ParseCsv(setting.CodeGenerateRowTypePrefixesToStrip),
                setting.TableClassCodeOutpuPath,
                setting.TableOutpuPath);
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
