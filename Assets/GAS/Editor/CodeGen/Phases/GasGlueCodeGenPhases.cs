using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GAS.Editor
{
    internal abstract class GasCodeGenPhaseBase : IGasCodeGenPhase
    {
        public abstract string PhaseName { get; }

        public abstract IReadOnlyList<string> OutputFileNames { get; }

        public virtual bool RequiresRows => true;

        public abstract void Execute(GasCodeGenContext context, GasCodeGenManifest manifest);

        protected static string GetOutputPath(GasCodeGenContext context, string fileName)
        {
            if (!Directory.Exists(context.OutputDir))
                Directory.CreateDirectory(context.OutputDir);

            var path = Path.Combine(context.OutputDir, fileName);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            return path;
        }

        protected static void AddRuntimeManifest(GasCodeGenManifest manifest, string phaseName, string path)
        {
            manifest.AddGeneratedFile(phaseName, path, "Runtime", true);
        }

        protected static void AddRuntimePureGlueManifest(GasCodeGenManifest manifest, string phaseName, string path)
        {
            manifest.AddGeneratedFile(phaseName, path, "Runtime", true, "RuntimePureGlue");
        }

        protected static void AddRuntimeLifecycleMigrationManifest(GasCodeGenManifest manifest, string phaseName, string path)
        {
            manifest.AddGeneratedFile(phaseName, path, "Runtime", true, "RuntimeLifecycleMigration");
        }

        protected static void AddBakingManifest(GasCodeGenManifest manifest, string phaseName, string path)
        {
            manifest.AddGeneratedFile(phaseName, path, "Baking", false);
        }

        protected static void AddEditorCiManifest(GasCodeGenManifest manifest, string phaseName, string path)
        {
            manifest.AddGeneratedFile(phaseName, path, "Editor/CI", false);
        }

        protected static void WriteHeader(IndentedWriter writer)
        {
            writer.WriteLine("///////////////////////////////////");
            writer.WriteLine("//// This is a generated file. ////");
            writer.WriteLine("////     Do not modify it.     ////");
            writer.WriteLine("///////////////////////////////////");
            writer.WriteLine("");
        }

        protected static string RowTypeName(RowMetadata row)
        {
            return (row.RowType.FullName ?? row.RowType.Name).Replace('+', '.');
        }

        protected static IReadOnlyList<RowMetadata> RuntimeVisibleRows(GasCodeGenContext context)
        {
            return context.Rows.Where(IsRuntimeVisibleRow).ToArray();
        }

        protected static bool IsRuntimeVisibleRow(RowMetadata row)
        {
            return !IsTimelineSourceRow(row);
        }

        protected static bool IsTimelineSourceRow(RowMetadata row)
        {
            return string.Equals(row.DomainName, "Timeline", StringComparison.Ordinal);
        }

        protected static string BlobMemberCountExpression(BlobMemberInfo member, string rowAccess)
        {
            return member.IsArray ? $"{rowAccess}.Length" : $"{rowAccess}.Count";
        }
    }

    internal sealed class AssemblyDefinitionPhase : GasCodeGenPhaseBase
    {
        private const string GeneratedRuntimeAssembly = "com.exhard.exgas.generated.runtime";
        private const string GeneratedEditorAssembly = "com.exhard.exgas.generated.editor";
        private const string RuntimeAssembly = "com.exhard.exgas.runtime";
        private const string EditorAssembly = "com.exhard.exgas.editor";

        public override string PhaseName => "AssemblyDefinition";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[]
        {
            "Runtime/com.exhard.exgas.generated.runtime.asmdef",
            "Editor/com.exhard.exgas.generated.editor.asmdef",
        };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var runtimePath = GetOutputPath(context, OutputFileNames[0]);
            var editorPath = GetOutputPath(context, OutputFileNames[1]);

            WriteAsmdef(
                runtimePath,
                GeneratedRuntimeAssembly,
                new[]
                {
                    RuntimeAssembly,
                    "Unity.Collections",
                    "Unity.Entities",
                    "Unity.Burst",
                },
                Array.Empty<string>());

            WriteAsmdef(
                editorPath,
                GeneratedEditorAssembly,
                BuildEditorReferences(context),
                new[] { "Editor" });

            AddRuntimeManifest(manifest, PhaseName, runtimePath);
            AddBakingManifest(manifest, PhaseName, editorPath);
        }

        private static IReadOnlyList<string> BuildEditorReferences(GasCodeGenContext context)
        {
            var references = new SortedSet<string>(StringComparer.Ordinal)
            {
                GeneratedRuntimeAssembly,
                RuntimeAssembly,
                EditorAssembly,
                "Unity.Collections",
                "Unity.Entities",
                "Unity.Entities.Hybrid",
            };

            foreach (var row in context.Rows)
            {
                var assemblyName = NormalizeSourceAssemblyReference(row.RowType.Assembly.GetName().Name);
                if (string.IsNullOrWhiteSpace(assemblyName))
                    continue;

                if (!IsReferenceableSourceAssembly(assemblyName))
                    continue;

                references.Add(assemblyName);
            }

            return references.ToArray();
        }

        private static string NormalizeSourceAssemblyReference(string assemblyName)
        {
            if (string.Equals(assemblyName, "GasCodeGenCli", StringComparison.Ordinal))
                return EditorAssembly;

            return assemblyName;
        }

        private static bool IsReferenceableSourceAssembly(string assemblyName)
        {
            return assemblyName != GeneratedEditorAssembly
                   && assemblyName != GeneratedRuntimeAssembly
                   && assemblyName != "Assembly-CSharp"
                   && assemblyName != "Assembly-CSharp-firstpass";
        }

        private static void WriteAsmdef(
            string path,
            string assemblyName,
            IReadOnlyList<string> references,
            IReadOnlyList<string> includePlatforms)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"\"name\": \"{EscapeJson(assemblyName)}\",");
            writer.WriteLine("\"rootNamespace\": \"\",");
            WriteStringArray(writer, "references", references, trailingComma: true);
            WriteStringArray(writer, "includePlatforms", includePlatforms, trailingComma: true);
            writer.WriteLine("\"excludePlatforms\": [],");
            writer.WriteLine("\"allowUnsafeCode\": false,");
            writer.WriteLine("\"overrideReferences\": false,");
            writer.WriteLine("\"precompiledReferences\": [],");
            writer.WriteLine("\"autoReferenced\": true,");
            writer.WriteLine("\"defineConstraints\": [],");
            writer.WriteLine("\"versionDefines\": [],");
            writer.WriteLine("\"noEngineReferences\": false");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteStringArray(
            IndentedWriter writer,
            string name,
            IReadOnlyList<string> values,
            bool trailingComma)
        {
            writer.WriteLine($"\"{name}\": [");
            writer.Indent++;
            for (var i = 0; i < values.Count; i++)
            {
                var suffix = i == values.Count - 1 ? string.Empty : ",";
                writer.WriteLine($"\"{EscapeJson(values[i])}\"{suffix}");
            }

            writer.Indent--;
            writer.WriteLine(trailingComma ? "]," : "]");
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }
    }

    internal sealed class DefinitionIndexPhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "DefinitionIndex";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[] { "Runtime/DefinitionIndex.gen.cs" };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var path = GetOutputPath(context, OutputFileNames[0]);
            var rows = RuntimeVisibleRows(context);
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using System.Collections.Generic;");
            writer.WriteLine("using GAS.Runtime;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("public readonly struct GASGeneratedDefinitionIndexEntry");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public readonly int Index;");
            writer.WriteLine("public readonly GASDefinitionKind DefinitionKind;");
            writer.WriteLine("public readonly int DomainHash;");
            writer.WriteLine("public readonly int CodeFieldHash;");
            writer.WriteLine("public readonly int BlobSchemaHash;");
            writer.WriteLine("public readonly int SourceRowCount;");
            writer.WriteLine("public readonly int BlobMemberCount;");
            writer.WriteLine("");
            writer.WriteLine("public GASGeneratedDefinitionIndexEntry(");
            writer.Indent++;
            writer.WriteLine("int index,");
            writer.WriteLine("GASDefinitionKind definitionKind,");
            writer.WriteLine("int domainHash,");
            writer.WriteLine("int codeFieldHash,");
            writer.WriteLine("int blobSchemaHash,");
            writer.WriteLine("int sourceRowCount,");
            writer.WriteLine("int blobMemberCount)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("Index = index;");
            writer.WriteLine("DefinitionKind = definitionKind;");
            writer.WriteLine("DomainHash = domainHash;");
            writer.WriteLine("CodeFieldHash = codeFieldHash;");
            writer.WriteLine("BlobSchemaHash = blobSchemaHash;");
            writer.WriteLine("SourceRowCount = sourceRowCount;");
            writer.WriteLine("BlobMemberCount = blobMemberCount;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine("");
            writer.WriteLine("public static class GASGeneratedDefinitionIndex");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"public const int Count = {rows.Count};");
            writer.WriteLine("");
            writer.WriteLine("public static bool TryGetAt(int index, out GASGeneratedDefinitionIndexEntry entry)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("switch (index)");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var row in rows.Select((value, index) => new { value, index }))
            {
                writer.WriteLine($"case {row.index}:");
                writer.Indent++;
                writer.WriteLine($"entry = {CreateEntryExpression(row.value, row.index)};");
                writer.WriteLine("return true;");
                writer.Indent--;
            }
            writer.WriteLine("default:");
            writer.Indent++;
            writer.WriteLine("entry = default;");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine("");
            writer.WriteLine("public static bool TryGetByDefinitionKind(GASDefinitionKind definitionKind, out GASGeneratedDefinitionIndexEntry entry)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("switch (definitionKind)");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var row in rows
                         .Select((value, index) => new { value, index })
                         .GroupBy(row => row.value.DefinitionKind)
                         .Select(group => group.First()))
            {
                writer.WriteLine($"case GASDefinitionKind.{row.value.DefinitionKind}:");
                writer.Indent++;
                writer.WriteLine($"entry = {CreateEntryExpression(row.value, row.index)};");
                writer.WriteLine("return true;");
                writer.Indent--;
            }
            writer.WriteLine("default:");
            writer.Indent++;
            writer.WriteLine("entry = default;");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");

            writer.Indent--;
            writer.WriteLine("}");

            AddRuntimeManifest(manifest, PhaseName, path);
        }

        private static string CreateEntryExpression(RowMetadata row, int index)
        {
            return "new GASGeneratedDefinitionIndexEntry("
                   + $"{index}, "
                   + $"GASDefinitionKind.{row.DefinitionKind}, "
                   + $"{StableHash(row.DomainName)}, "
                   + $"{StableHash(row.CodeFieldName)}, "
                   + $"{StableHash(row.BlobSchemaName)}, "
                   + $"{row.RowValues?.Count ?? 0}, "
                   + $"{row.BlobMembers.Count})";
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                var hash = (uint)2166136261;
                if (value != null)
                {
                    for (var i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619;
                    }
                }

                return (int)hash;
            }
        }
    }

    internal sealed class BlobSchemaPhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "BlobSchema";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[]
        {
            "Runtime/BlobSchemas.gen.cs",
            "Editor/BlobBuilders.gen.cs",
        };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var schemaPath = GetOutputPath(context, OutputFileNames[0]);
            var builderPath = GetOutputPath(context, OutputFileNames[1]);

            WriteBlobSchemas(context, schemaPath);
            WriteBlobBuilders(context, builderPath);

            AddRuntimeManifest(manifest, PhaseName, schemaPath);
            AddBakingManifest(manifest, PhaseName, builderPath);
        }

        private static void WriteBlobSchemas(GasCodeGenContext context, string path)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var row in RuntimeVisibleRows(context))
            {
                writer.WriteLine("");
                writer.WriteLine("/// <summary>");
                writer.WriteLine($"/// BlobAsset definition generated for {row.DomainName}.");
                writer.WriteLine("/// </summary>");
                writer.WriteLine($"public struct {row.BlobSchemaName}");
                writer.WriteLine("{");
                writer.Indent++;

                foreach (var member in row.BlobMembers)
                    writer.WriteLine($"public {member.BlobTypeName} {member.Name};");

                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteBlobBuilders(GasCodeGenContext context, string path)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using Unity.Collections;");
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine("#if UNITY_EDITOR");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static class GASGeneratedDefinitionBlobBuilder");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var row in RuntimeVisibleRows(context))
            {
                writer.WriteLine("");
                writer.WriteLine($"public static BlobAssetReference<{row.BlobSchemaName}> {row.BakerMethodName}(");
                writer.Indent++;
                writer.WriteLine($"{RowTypeName(row)} row,");
                writer.WriteLine("Allocator allocator = Allocator.Persistent)");
                writer.Indent--;
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var builder = new BlobBuilder(Allocator.Temp);");
                writer.WriteLine($"ref var root = ref builder.ConstructRoot<{row.BlobSchemaName}>();");
                writer.WriteLine("");

                foreach (var member in row.BlobMembers)
                {
                    var rowAccess = $"row.{member.RowAccessor}";
                    if (member.RequiresAllocate)
                    {
                        writer.WriteLine($"if ({rowAccess} != null)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine($"var count = {BlobMemberCountExpression(member, rowAccess)};");
                        writer.WriteLine($"var target = builder.Allocate(ref root.{member.Name}, count);");
                        writer.WriteLine($"for (var i = 0; i < count; i++) target[i] = {rowAccess}[i];");
                        writer.Indent--;
                        writer.WriteLine("}");
                    }
                    else if (member.IsBlobString)
                    {
                        writer.WriteLine($"builder.AllocateString(ref root.{member.Name}, {rowAccess} ?? string.Empty);");
                    }
                    else if (member.RequiresCast)
                    {
                        writer.WriteLine($"root.{member.Name} = (int){rowAccess};");
                    }
                    else
                    {
                        writer.WriteLine($"root.{member.Name} = {rowAccess};");
                    }
                }

                writer.WriteLine("");
                writer.WriteLine($"var blob = builder.CreateBlobAssetReference<{row.BlobSchemaName}>(allocator);");
                writer.WriteLine("builder.Dispose();");
                writer.WriteLine("return blob;");
                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("#endif");
        }
    }

    internal sealed class StaticLookupPhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "StaticLookup";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[]
        {
            "Runtime/StaticLookups.gen.cs",
            "Editor/StaticLookupBuilders.gen.cs",
        };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var runtimePath = GetOutputPath(context, OutputFileNames[0]);
            var builderPath = GetOutputPath(context, OutputFileNames[1]);

            WriteRuntimeLookups(context, runtimePath);
            WriteLookupBuilders(context, builderPath);

            AddRuntimeManifest(manifest, PhaseName, runtimePath);
            AddBakingManifest(manifest, PhaseName, builderPath);
        }

        private static void WriteRuntimeLookups(GasCodeGenContext context, string path)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using System;");
            writer.WriteLine("using Unity.Collections;");
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var row in RuntimeVisibleRows(context))
            {
                writer.WriteLine("");
                writer.WriteLine("/// <summary>");
                writer.WriteLine($"/// Burst-readable lookup for {row.BlobSchemaName}. Runtime owns only sorted codes and blob references.");
                writer.WriteLine("/// </summary>");
                writer.WriteLine($"public struct {row.LookupName} : IDisposable");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("private NativeArray<int> _sortedCodes;");
                writer.WriteLine($"private NativeArray<BlobAssetReference<{row.BlobSchemaName}>> _entries;");
                writer.WriteLine("private byte _ownsMemory;");
                writer.WriteLine("");
                writer.WriteLine("public bool IsCreated => _sortedCodes.IsCreated && _entries.IsCreated;");
                writer.WriteLine("public int Count => _sortedCodes.IsCreated ? _sortedCodes.Length : 0;");
                writer.WriteLine("public bool OwnsMemory => _ownsMemory != 0;");
                writer.WriteLine("");
                writer.WriteLine($"public {row.LookupName}(");
                writer.Indent++;
                writer.WriteLine("NativeArray<int> sortedCodes,");
                writer.WriteLine($"NativeArray<BlobAssetReference<{row.BlobSchemaName}>> entries,");
                writer.WriteLine("bool ownsMemory = true)");
                writer.Indent--;
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("if (sortedCodes.IsCreated != entries.IsCreated)");
                writer.Indent++;
                writer.WriteLine("throw new ArgumentException(\"Lookup code and entry arrays must have the same ownership state.\");");
                writer.Indent--;
                writer.WriteLine("if (sortedCodes.IsCreated && sortedCodes.Length != entries.Length)");
                writer.Indent++;
                writer.WriteLine("throw new ArgumentException(\"Lookup code and entry arrays must have the same length.\");");
                writer.Indent--;
                writer.WriteLine("_sortedCodes = sortedCodes;");
                writer.WriteLine("_entries = entries;");
                writer.WriteLine("_ownsMemory = ownsMemory ? (byte)1 : (byte)0;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");
                writer.WriteLine($"public bool TryGet(int code, out BlobAssetReference<{row.BlobSchemaName}> blob)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("blob = default;");
                writer.WriteLine("if (!IsCreated) return false;");
                writer.WriteLine("var lo = 0;");
                writer.WriteLine("var hi = _sortedCodes.Length - 1;");
                writer.WriteLine("while (lo <= hi)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var mid = lo + ((hi - lo) >> 1);");
                writer.WriteLine("var midCode = _sortedCodes[mid];");
                writer.WriteLine("if (midCode == code)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("blob = _entries[mid];");
                writer.WriteLine("return true;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("if (midCode < code) lo = mid + 1;");
                writer.WriteLine("else hi = mid - 1;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("return false;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");
                writer.WriteLine("public void Dispose()");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("if (!OwnsMemory)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("_sortedCodes = default;");
                writer.WriteLine("_entries = default;");
                writer.WriteLine("_ownsMemory = 0;");
                writer.WriteLine("return;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("if (_entries.IsCreated)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("for (var i = 0; i < _entries.Length; i++)");
                writer.Indent++;
                writer.WriteLine("if (_entries[i].IsCreated) _entries[i].Dispose();");
                writer.Indent--;
                writer.WriteLine("_entries.Dispose();");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("if (_sortedCodes.IsCreated) _sortedCodes.Dispose();");
                writer.WriteLine("_sortedCodes = default;");
                writer.WriteLine("_entries = default;");
                writer.WriteLine("_ownsMemory = 0;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteLookupBuilders(GasCodeGenContext context, string path)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("#if UNITY_EDITOR");
            writer.WriteLine("using System;");
            writer.WriteLine("using System.Collections.Generic;");
            writer.WriteLine("using Unity.Collections;");
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static class GASGeneratedDefinitionLookupBuilder");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var row in RuntimeVisibleRows(context))
            {
                writer.WriteLine("");
                writer.WriteLine($"public static {row.LookupName} Build{row.LookupName}FromRows(");
                writer.Indent++;
                writer.WriteLine($"IReadOnlyList<{RowTypeName(row)}> rows,");
                writer.WriteLine("Allocator allocator = Allocator.Persistent)");
                writer.Indent--;
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("if (rows == null) throw new ArgumentNullException(nameof(rows));");
                writer.WriteLine("var sorted = new (int Code, int Index)[rows.Count];");
                writer.WriteLine("for (var i = 0; i < rows.Count; i++)");
                writer.Indent++;
                writer.WriteLine($"sorted[i] = (rows[i].{row.CodeFieldName}, i);");
                writer.Indent--;
                writer.WriteLine("Array.Sort(sorted, (left, right) => left.Code.CompareTo(right.Code));");
                writer.WriteLine("var sortedCodes = new NativeArray<int>(rows.Count, allocator);");
                writer.WriteLine($"var entries = new NativeArray<BlobAssetReference<{row.BlobSchemaName}>>(rows.Count, allocator);");
                writer.WriteLine("for (var i = 0; i < sorted.Length; i++)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("sortedCodes[i] = sorted[i].Code;");
                writer.WriteLine($"entries[i] = GASGeneratedDefinitionBlobBuilder.{row.BakerMethodName}(rows[sorted[i].Index], allocator);");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine($"return new {row.LookupName}(sortedCodes, entries);");
                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("#endif");
        }
    }

    internal sealed class DefinitionCatalogPhase : GasCodeGenPhaseBase
    {
        private const int SchemaVersion = 1;

        public override string PhaseName => "DefinitionCatalog";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[]
        {
            "Runtime/DefinitionCatalog.gen.cs",
            "Editor/DefinitionCatalogBuilder.gen.cs",
        };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var runtimePath = GetOutputPath(context, OutputFileNames[0]);
            var builderPath = GetOutputPath(context, OutputFileNames[1]);
            var model = CatalogModel.Create(context);

            WriteRuntimeCatalog(context, model, runtimePath);
            WriteCatalogBuilder(context, model, builderPath);

            AddRuntimeManifest(manifest, PhaseName, runtimePath);
            AddBakingManifest(manifest, PhaseName, builderPath);
        }

        private static void WriteRuntimeCatalog(GasCodeGenContext context, CatalogModel model, string path)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using GAS.Runtime;");
            writer.WriteLine("using Unity.Collections;");
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static class GASGeneratedDefinitionCatalogInfo");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"public const int SchemaVersion = {SchemaVersion};");
            writer.WriteLine($"public const int AbilityCount = {model.Abilities.Count};");
            writer.WriteLine($"public const int GameplayEffectCount = {model.GameplayEffects.Count};");
            writer.WriteLine($"public const int ModifierCount = {model.Modifiers.Count};");
            writer.WriteLine($"public const int TagMaskCount = {model.TagMasks.Count};");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public static class GASGeneratedDefinitionCatalogLookup");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static bool IsCatalogCreated(BlobAssetReference<GASDefinitionCatalogBlob> catalog)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("return catalog.IsCreated && catalog.Value.SchemaVersion == GASGeneratedDefinitionCatalogInfo.SchemaVersion;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            WriteLookupMethod(writer, "Ability", "AbilityCodes", "Abilities", "GASCatalogAbilityDefinitionBlob", "abilityCode");
            writer.WriteLine("");
            WriteLookupMethod(writer, "GameplayEffect", "GameplayEffectCodes", "GameplayEffects", "GASCatalogGameplayEffectDefinitionBlob", "gameplayEffectCode");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            WriteCatalogBuilderClass(writer, model);
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteLookupMethod(
            IndentedWriter writer,
            string domain,
            string codesMember,
            string entriesMember,
            string entryTypeName,
            string codeParameterName)
        {
            writer.WriteLine($"public static bool TryGet{domain}Index(ref GASDefinitionCatalogBlob catalog, int {codeParameterName}, out int index)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("index = -1;");
            writer.WriteLine("var lo = 0;");
            writer.WriteLine($"var hi = catalog.{codesMember}.Length - 1;");
            writer.WriteLine("while (lo <= hi)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var mid = lo + ((hi - lo) >> 1);");
            writer.WriteLine($"var midCode = catalog.{codesMember}[mid];");
            writer.WriteLine($"if (midCode == {codeParameterName})");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("index = mid;");
            writer.WriteLine("return true;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine($"if (midCode < {codeParameterName}) lo = mid + 1;");
            writer.WriteLine("else hi = mid - 1;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine($"public static ref readonly {entryTypeName} Get{domain}(ref GASDefinitionCatalogBlob catalog, int index)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"return ref catalog.{entriesMember}[index];");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteCatalogBuilder(GasCodeGenContext context, CatalogModel model, string path)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("#if UNITY_EDITOR");
            writer.WriteLine("using GAS.Runtime;");
            writer.WriteLine("using Unity.Collections;");
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("using UnityEngine;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public sealed class GASGeneratedDefinitionCatalogAuthoring : MonoBehaviour");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public int Revision = GASGeneratedDefinitionCatalogInfo.SchemaVersion;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public sealed class GASGeneratedDefinitionCatalogBaker : Baker<GASGeneratedDefinitionCatalogAuthoring>");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public override void Bake(GASGeneratedDefinitionCatalogAuthoring authoring)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var entity = GetEntity(TransformUsageFlags.None);");
            writer.WriteLine("var catalog = GASGeneratedDefinitionCatalogBuilder.BuildCatalog();");
            writer.WriteLine("AddBlobAsset(ref catalog, out _);");
            writer.WriteLine("AddComponent(entity, new GASDefinitionCatalogComponent");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("Catalog = catalog,");
            writer.WriteLine("Revision = authoring.Revision,");
            writer.Indent--;
            writer.WriteLine("});");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("#endif");
        }

        private static void WriteCatalogBuilderClass(IndentedWriter writer, CatalogModel model)
        {
            writer.WriteLine("public static class GASGeneratedDefinitionCatalogBuilder");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static BlobAssetReference<GASDefinitionCatalogBlob> BuildCatalog(Allocator allocator = Allocator.Persistent)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var builder = new BlobBuilder(Allocator.Temp);");
            writer.WriteLine("ref var root = ref builder.ConstructRoot<GASDefinitionCatalogBlob>();");
            writer.WriteLine($"root.SchemaVersion = {SchemaVersion};");
            writer.WriteLine("");
            WriteIntArrayAllocation(writer, "AbilityCodes", model.Abilities.Select(item => item.AbilityCode).ToArray());
            WriteAbilityAllocation(writer, model.Abilities);
            WriteIntArrayAllocation(writer, "GameplayEffectCodes", model.GameplayEffects.Select(item => item.GameplayEffectCode).ToArray());
            WriteGameplayEffectAllocation(writer, model.GameplayEffects);
            WriteModifierAllocation(writer, model.Modifiers);
            WriteRequirementAllocation(writer, model.Requirements);
            WriteTagMaskAllocation(writer, model.TagMasks);
            WriteGrantedAbilityAllocation(writer);
            writer.WriteLine("");
            writer.WriteLine("var blob = builder.CreateBlobAssetReference<GASDefinitionCatalogBlob>(allocator);");
            writer.WriteLine("builder.Dispose();");
            writer.WriteLine("return blob;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteIntArrayAllocation(IndentedWriter writer, string memberName, IReadOnlyList<int> values)
        {
            writer.WriteLine($"var {ToCamel(memberName)} = builder.Allocate(ref root.{memberName}, {values.Count});");
            for (var i = 0; i < values.Count; i++)
                writer.WriteLine($"{ToCamel(memberName)}[{i}] = {values[i]};");
            writer.WriteLine("");
        }

        private static void WriteAbilityAllocation(IndentedWriter writer, IReadOnlyList<CatalogAbility> values)
        {
            writer.WriteLine($"var abilities = builder.Allocate(ref root.Abilities, {values.Count});");
            for (var i = 0; i < values.Count; i++)
            {
                var item = values[i];
                writer.WriteLine($"abilities[{i}] = new GASCatalogAbilityDefinitionBlob");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"AbilityCode = {item.AbilityCode},");
                writer.WriteLine($"Level = {item.Level},");
                writer.WriteLine($"PrimaryGameplayEffectCode = {item.PrimaryGameplayEffectCode},");
                writer.WriteLine($"SecondaryGameplayEffectCode = {item.SecondaryGameplayEffectCode},");
                writer.WriteLine($"CostGameplayEffectCode = {item.CostGameplayEffectCode},");
                writer.WriteLine($"CooldownGameplayEffectCode = {item.CooldownGameplayEffectCode},");
                writer.WriteLine($"CooldownFrames = {item.CooldownFrames},");
                writer.WriteLine($"ActivationOwnedTagMaskIndex = {item.ActivationOwnedTagMaskIndex},");
                writer.WriteLine($"RequirementStart = {item.RequirementStart},");
                writer.WriteLine($"RequirementCount = {item.RequirementCount},");
                writer.WriteLine($"TargetRuleCode = {item.TargetRuleCode},");
                writer.WriteLine($"TargetRuleParam0 = {item.TargetRuleParam0},");
                writer.WriteLine($"TargetRuleParam1 = {item.TargetRuleParam1},");
                writer.Indent--;
                writer.WriteLine("};");
            }
            writer.WriteLine("");
        }

        private static void WriteGameplayEffectAllocation(IndentedWriter writer, IReadOnlyList<CatalogGameplayEffect> values)
        {
            writer.WriteLine($"var gameplayEffects = builder.Allocate(ref root.GameplayEffects, {values.Count});");
            for (var i = 0; i < values.Count; i++)
            {
                var item = values[i];
                writer.WriteLine($"gameplayEffects[{i}] = new GASCatalogGameplayEffectDefinitionBlob");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"GameplayEffectCode = {item.GameplayEffectCode},");
                writer.WriteLine($"DurationFrames = {item.DurationFrames},");
                writer.WriteLine($"PeriodFrames = {item.PeriodFrames},");
                writer.WriteLine($"PeriodGameplayEffectCode = {item.PeriodGameplayEffectCode},");
                writer.WriteLine($"GrantedTagMaskIndex = {item.GrantedTagMaskIndex},");
                writer.WriteLine($"RemoveGameplayEffectTagMaskIndex = {item.RemoveGameplayEffectTagMaskIndex},");
                writer.WriteLine($"RemoveGameplayEffectTagQuery = {TagRequirementMaskLiteral(item.RemoveGameplayEffectTagQuery)},");
                writer.WriteLine($"GameplayCueCode = {item.GameplayCueCode},");
                writer.WriteLine($"DamageTypeCode = {item.DamageTypeCode},");
                writer.WriteLine($"ResistanceAttributeSetCode = {item.ResistanceAttributeSetCode},");
                writer.WriteLine($"ResistanceAttributeCode = {item.ResistanceAttributeCode},");
                writer.WriteLine($"ResistanceCap = {FloatLiteral(item.ResistanceCap)},");
                writer.WriteLine($"StackingCode = {item.StackingCode},");
                writer.WriteLine($"StackLimitCount = {item.StackLimitCount},");
                writer.WriteLine($"StackType = {item.StackType},");
                writer.WriteLine($"EffectDurationRefreshPolicy = {item.EffectDurationRefreshPolicy},");
                writer.WriteLine($"EffectPeriodResetPolicy = {item.EffectPeriodResetPolicy},");
                writer.WriteLine($"EffectExpirationPolicy = {item.EffectExpirationPolicy},");
                writer.WriteLine($"DenyOverflowApplication = (byte){(item.DenyOverflowApplication ? 1 : 0)},");
                writer.WriteLine($"ClearStackOnOverflow = (byte){(item.ClearStackOnOverflow ? 1 : 0)},");
                writer.WriteLine($"OverflowGameplayEffectCode = {item.OverflowGameplayEffectCode},");
                writer.WriteLine($"ModifierStart = {item.ModifierStart},");
                writer.WriteLine($"ModifierCount = {item.ModifierCount},");
                writer.WriteLine($"RequirementStart = {item.RequirementStart},");
                writer.WriteLine($"RequirementCount = {item.RequirementCount},");
                writer.WriteLine($"GrantedAbilityStart = {item.GrantedAbilityStart},");
                writer.WriteLine($"GrantedAbilityCount = {item.GrantedAbilityCount},");
                writer.Indent--;
                writer.WriteLine("};");
            }
            writer.WriteLine("");
        }

        private static void WriteModifierAllocation(IndentedWriter writer, IReadOnlyList<CatalogModifier> values)
        {
            writer.WriteLine($"var modifiers = builder.Allocate(ref root.Modifiers, {values.Count});");
            for (var i = 0; i < values.Count; i++)
            {
                var item = values[i];
                writer.WriteLine($"modifiers[{i}] = new GASCatalogModifierDefinitionBlob");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"GameplayEffectCode = {item.GameplayEffectCode},");
                writer.WriteLine($"ModifierIndex = {item.ModifierIndex},");
                writer.WriteLine($"AttributeSetCode = {item.AttributeSetCode},");
                writer.WriteLine($"AttributeCode = {item.AttributeCode},");
                writer.WriteLine($"Operation = (EModifierOp){item.Operation},");
                writer.WriteLine($"BaseMagnitude = {FloatLiteral(item.BaseMagnitude)},");
                writer.WriteLine($"MagnitudeSource = (EMagnitudeSource){item.MagnitudeSource},");
                writer.WriteLine($"MagnitudeKey = {item.MagnitudeKey},");
                writer.WriteLine($"CaptureAttributeSetCode = {item.CaptureAttributeSetCode},");
                writer.WriteLine($"CaptureAttributeCode = {item.CaptureAttributeCode},");
                writer.WriteLine($"CaptureTiming = (EAttributeCaptureTiming){item.CaptureTiming},");
                writer.WriteLine($"FallbackMagnitude = {FloatLiteral(item.FallbackMagnitude)},");
                writer.WriteLine($"Coefficient = {FloatLiteral(item.Coefficient)},");
                writer.WriteLine($"PreAdd = {FloatLiteral(item.PreAdd)},");
                writer.WriteLine($"PostAdd = {FloatLiteral(item.PostAdd)},");
                writer.Indent--;
                writer.WriteLine("};");
            }
            writer.WriteLine("");
        }

        private static void WriteRequirementAllocation(IndentedWriter writer, IReadOnlyList<DefinitionCatalogPhase.CatalogRequirement> values)
        {
            writer.WriteLine($"var requirements = builder.Allocate(ref root.Requirements, {values.Count});");
            for (var i = 0; i < values.Count; i++)
            {
                var item = values[i];
                writer.WriteLine($"requirements[{i}] = new GASCatalogRequirementDefinitionBlob");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"RequirementKind = {item.RequirementKind},");
                writer.WriteLine($"TagMaskIndex = {item.TagMaskIndex},");
                writer.WriteLine($"TagQuery = {TagRequirementMaskLiteral(item.TagQuery)},");
                writer.WriteLine($"AttributeSetCode = {item.AttributeSetCode},");
                writer.WriteLine($"AttributeCode = {item.AttributeCode},");
                writer.WriteLine($"CompareOp = {item.CompareOp},");
                writer.WriteLine($"CompareValue = {FloatLiteral(item.CompareValue)},");
                writer.Indent--;
                writer.WriteLine("};");
            }
            writer.WriteLine("");
        }

        private static void WriteTagMaskAllocation(IndentedWriter writer, IReadOnlyList<CatalogTagMask> values)
        {
            WriteIntArrayAllocation(writer, "TagMaskCodes", values.Select(item => item.TagCode).ToArray());
            writer.WriteLine($"var tagMasks = builder.Allocate(ref root.TagMasks, {values.Count});");
            for (var i = 0; i < values.Count; i++)
            {
                var item = values[i];
                writer.WriteLine($"tagMasks[{i}] = new GASCatalogTagMaskDefinitionBlob");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"TagCode = {item.TagCode},");
                writer.WriteLine($"Mask = {TagMaskLiteral(item.Mask)},");
                writer.Indent--;
                writer.WriteLine("};");
            }
            writer.WriteLine("");
        }

        private static void WriteGrantedAbilityAllocation(IndentedWriter writer)
        {
            writer.WriteLine("builder.Allocate(ref root.GrantedAbilities, 0);");
        }

        private static string ToCamel(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }

        private static string FloatLiteral(float value)
        {
            if (float.IsNaN(value)) return "float.NaN";
            if (float.IsPositiveInfinity(value)) return "float.PositiveInfinity";
            if (float.IsNegativeInfinity(value)) return "float.NegativeInfinity";
            return value.ToString("R", CultureInfo.InvariantCulture) + "f";
        }

        private static string TagRequirementMaskLiteral(GAS.Runtime.TagRequirementMask requirement)
        {
            return "new TagRequirementMask { " +
                   $"All = {TagMaskLiteral(requirement.All)}, " +
                   $"Any = {TagMaskLiteral(requirement.Any)}, " +
                   $"None = {TagMaskLiteral(requirement.None)} }}";
        }

        private static string TagMaskLiteral(GAS.Runtime.TagMaskComponent mask)
        {
            return "new TagMaskComponent { " +
                   $"Mask0 = {ULongLiteral(mask.Mask0)}, " +
                   $"Mask1 = {ULongLiteral(mask.Mask1)}, " +
                   $"Mask2 = {ULongLiteral(mask.Mask2)}, " +
                   $"Mask3 = {ULongLiteral(mask.Mask3)} }}";
        }

        private static string ULongLiteral(ulong value)
        {
            return value.ToString(CultureInfo.InvariantCulture) + "ul";
        }

        private sealed class CatalogModel
        {
            public List<CatalogAbility> Abilities { get; } = new List<CatalogAbility>();
            public List<CatalogGameplayEffect> GameplayEffects { get; } = new List<CatalogGameplayEffect>();
            public List<CatalogModifier> Modifiers { get; } = new List<CatalogModifier>();
            public List<DefinitionCatalogPhase.CatalogRequirement> Requirements { get; } = new List<DefinitionCatalogPhase.CatalogRequirement>();
            public List<CatalogTagMask> TagMasks { get; } = new List<CatalogTagMask>();

            public static CatalogModel Create(GasCodeGenContext context)
            {
                var model = new CatalogModel();
                var tagMaskIndices = new Dictionary<int, int>();
                var tagCatalog = BuildTagCatalog(context);
                var timelinesById = BuildTimelines(context);
                BuildAbilities(context, model, timelinesById, tagMaskIndices, tagCatalog);
                BuildGameplayEffects(context, model, tagMaskIndices, tagCatalog);
                BuildTagMasks(model, tagMaskIndices, tagCatalog);
                return model;
            }

            private static DefinitionCatalogPhase.CatalogTagCatalog BuildTagCatalog(GasCodeGenContext context)
            {
                var row = context.Rows.FirstOrDefault(item => item.DefinitionKind == GAS.Runtime.GASDefinitionKind.GameplayTag);
                var entries = new List<CatalogTagEntry>();
                if (row?.RowValues != null)
                {
                    foreach (var snapshot in row.RowValues)
                    {
                        var tagCode = GetInt(snapshot.Row, "GameplayTagCode");
                        if (tagCode <= 0)
                            continue;

                        entries.Add(new CatalogTagEntry
                        {
                            TagCode = tagCode,
                            ParentCodes = ToPositiveIntArray(GetArrayValue(snapshot.Row, "ParentCodes")),
                        });
                    }
                }

                entries.Sort((left, right) => left.TagCode.CompareTo(right.TagCode));
                return DefinitionCatalogPhase.CatalogTagCatalog.Create(entries);
            }

            private static Dictionary<int, CatalogTimeline> BuildTimelines(GasCodeGenContext context)
            {
                var row = context.Rows.FirstOrDefault(IsTimelineSourceRow);
                var result = new Dictionary<int, CatalogTimeline>();
                if (row?.RowValues == null)
                    return result;

                foreach (var snapshot in row.RowValues)
                {
                    var item = new CatalogTimeline
                    {
                        TimelineId = GetInt(snapshot.Row, "TimelineId"),
                        PrimaryGameplayEffectCode = GetInt(snapshot.Row, "GameplayEffectCode"),
                        SecondaryGameplayEffectCode = GetInt(snapshot.Row, "SecondaryGameplayEffectCode"),
                        TargetRuleCode = StableHash(GetString(snapshot.Row, "TargetCatcherName")),
                    };
                    if (item.TimelineId <= 0)
                        continue;

                    result[item.TimelineId] = item;
                }

                return result;
            }

            private static void BuildAbilities(
                GasCodeGenContext context,
                CatalogModel model,
                IReadOnlyDictionary<int, CatalogTimeline> timelinesById,
                Dictionary<int, int> tagMaskIndices,
                DefinitionCatalogPhase.CatalogTagCatalog tagCatalog)
            {
                var row = context.Rows.FirstOrDefault(item => item.DefinitionKind == GAS.Runtime.GASDefinitionKind.Ability);
                if (row?.RowValues == null)
                    return;

                foreach (var snapshot in row.RowValues)
                {
                    var timelineId = GetInt(snapshot.Row, "TimelineId");
                    timelinesById.TryGetValue(timelineId, out var timeline);
                    var activationOwnedTagCode = GetInt(snapshot.Row, "ActivationOwnedTagCode");

                    var ability = new CatalogAbility
                    {
                        AbilityCode = GetInt(snapshot.Row, "AbilityCode"),
                        Level = GetInt(snapshot.Row, "Level"),
                        PrimaryGameplayEffectCode = timeline.PrimaryGameplayEffectCode > 0
                            ? timeline.PrimaryGameplayEffectCode
                            : GetInt(snapshot.Row, "PrimaryGameplayEffectCode"),
                        SecondaryGameplayEffectCode = timeline.SecondaryGameplayEffectCode > 0
                            ? timeline.SecondaryGameplayEffectCode
                            : GetInt(snapshot.Row, "SecondaryGameplayEffectCode"),
                        CostGameplayEffectCode = GetInt(snapshot.Row, "CostGameplayEffectCode"),
                        CooldownGameplayEffectCode = GetInt(snapshot.Row, "CooldownGameplayEffectCode"),
                        CooldownFrames = GetInt(snapshot.Row, "CooldownFrames"),
                        ActivationOwnedTagMaskIndex = GetOrAddTagMaskIndex(tagMaskIndices, activationOwnedTagCode),
                        RequirementStart = 0,
                        RequirementCount = 0,
                        TargetRuleCode = timeline.TargetRuleCode != 0
                            ? timeline.TargetRuleCode
                            : GetInt(snapshot.Row, "TargetRuleCode"),
                        TargetRuleParam0 = 0,
                        TargetRuleParam1 = 0,
                    };

                    ability.RequirementStart = model.Requirements.Count;
                    AddTagRequirement(
                        model.Requirements,
                        GAS.Runtime.GASRequirementKind.RequiredTags,
                        BuildTagRequirementMask(snapshot.Row, "ActivationRequired", tagCatalog));
                    AddTagRequirement(
                        model.Requirements,
                        GAS.Runtime.GASRequirementKind.BlockedTags,
                        BuildTagRequirementMask(snapshot.Row, "ActivationBlocked", tagCatalog));
                    ability.RequirementCount = model.Requirements.Count - ability.RequirementStart;
                    model.Abilities.Add(ability);
                }

                model.Abilities.Sort((left, right) => left.AbilityCode.CompareTo(right.AbilityCode));
            }

            private static void BuildGameplayEffects(
                GasCodeGenContext context,
                CatalogModel model,
                Dictionary<int, int> tagMaskIndices,
                DefinitionCatalogPhase.CatalogTagCatalog tagCatalog)
            {
                var row = context.Rows.FirstOrDefault(item => item.DefinitionKind == GAS.Runtime.GASDefinitionKind.GameplayEffect);
                if (row?.RowValues == null)
                    return;

                foreach (var snapshot in row.RowValues)
                {
                    var gameplayEffectCode = GetInt(snapshot.Row, "GameplayEffectCode");
                    var ge = new CatalogGameplayEffect
                    {
                        GameplayEffectCode = gameplayEffectCode,
                        DurationFrames = GetInt(snapshot.Row, "DurationFrames"),
                        PeriodFrames = GetInt(snapshot.Row, "PeriodFrames"),
                        PeriodGameplayEffectCode = GetInt(snapshot.Row, "PeriodGameplayEffectCode"),
                        GrantedTagMaskIndex = GetOrAddTagMaskIndex(tagMaskIndices, GetInt(snapshot.Row, "GrantedTagCode")),
                        RemoveGameplayEffectTagMaskIndex = GetOrAddTagMaskIndex(tagMaskIndices, GetInt(snapshot.Row, "RemoveGameplayEffectTagCode")),
                        RemoveGameplayEffectTagQuery = BuildTagRequirementMask(snapshot.Row, "RemoveGameplayEffect", tagCatalog),
                        GameplayCueCode = GetInt(snapshot.Row, "GameplayCueCode"),
                        DamageTypeCode = GetInt(snapshot.Row, "DamageTypeCode"),
                        ResistanceAttributeSetCode = GetInt(snapshot.Row, "ResistanceAttributeSetCode"),
                        ResistanceAttributeCode = GetInt(snapshot.Row, "ResistanceAttributeCode"),
                        ResistanceCap = GetFloat(snapshot.Row, "ResistanceCap"),
                        StackingCode = GetInt(snapshot.Row, "StackingCode"),
                        StackLimitCount = GetInt(snapshot.Row, "StackLimitCount"),
                        StackType = GetEnumInt(snapshot.Row, "StackType"),
                        EffectDurationRefreshPolicy = GetEnumInt(snapshot.Row, "EffectDurationRefreshPolicy"),
                        EffectPeriodResetPolicy = GetEnumInt(snapshot.Row, "EffectPeriodResetPolicy"),
                        EffectExpirationPolicy = GetEnumInt(snapshot.Row, "EffectExpirationPolicy"),
                        DenyOverflowApplication = GetBool(snapshot.Row, "DenyOverflowApplication"),
                        ClearStackOnOverflow = GetBool(snapshot.Row, "ClearStackOnOverflow"),
                        OverflowGameplayEffectCode = GetInt(snapshot.Row, "OverflowGameplayEffectCode"),
                        GrantedAbilityStart = 0,
                        GrantedAbilityCount = 0,
                    };

                    ge.RequirementStart = model.Requirements.Count;
                    AddTagRequirement(
                        model.Requirements,
                        GAS.Runtime.GASRequirementKind.RequiredTags,
                        BuildTagRequirementMask(snapshot.Row, "ApplicationRequired", tagCatalog));
                    AddTagRequirement(
                        model.Requirements,
                        GAS.Runtime.GASRequirementKind.RequiredTags,
                        BuildTagRequirementMask(snapshot.Row, "OngoingRequired", tagCatalog));
                    AddTagRequirement(
                        model.Requirements,
                        GAS.Runtime.GASRequirementKind.BlockedTags,
                        BuildTagRequirementMask(snapshot.Row, "Immunity", tagCatalog));
                    ge.RequirementCount = model.Requirements.Count - ge.RequirementStart;

                    var modifierCount = GetModifierCount(snapshot.Row);
                    if (modifierCount > 0)
                    {
                        ge.ModifierStart = model.Modifiers.Count;
                        ge.ModifierCount = modifierCount;
                        for (var modifierIndex = 0; modifierIndex < modifierCount; modifierIndex++)
                        {
                            var attributeSetCode = GetIntAt(snapshot.Row, "ModifierAttributeSetCodes", modifierIndex, "ModifierAttributeSetCode");
                            var attributeCode = GetIntAt(snapshot.Row, "ModifierAttributeCodes", modifierIndex, "ModifierAttributeCode");
                            var magnitude = GetFloatAt(snapshot.Row, "ModifierMagnitudes", modifierIndex, "ModifierMagnitude");
                            model.Modifiers.Add(new CatalogModifier
                            {
                                GameplayEffectCode = gameplayEffectCode,
                                ModifierIndex = modifierIndex,
                                AttributeSetCode = attributeSetCode,
                                AttributeCode = attributeCode,
                                Operation = GetEnumIntAt(snapshot.Row, "ModifierOperations", modifierIndex, "ModifierOperation"),
                                BaseMagnitude = magnitude,
                                MagnitudeSource = GetEnumIntAt(snapshot.Row, "ModifierMagnitudeSources", modifierIndex, "ModifierMagnitudeSource"),
                                MagnitudeKey = GetIntAt(snapshot.Row, "ModifierMagnitudeKeys", modifierIndex, "ModifierMagnitudeKey"),
                                CaptureAttributeSetCode = attributeSetCode,
                                CaptureAttributeCode = attributeCode,
                                CaptureTiming = 0,
                                FallbackMagnitude = magnitude,
                                Coefficient = 1f,
                                PreAdd = 0f,
                                PostAdd = 0f,
                            });
                        }
                    }
                    else
                    {
                        ge.ModifierStart = model.Modifiers.Count;
                        ge.ModifierCount = 0;
                    }

                    model.GameplayEffects.Add(ge);
                }

                model.GameplayEffects.Sort((left, right) => left.GameplayEffectCode.CompareTo(right.GameplayEffectCode));
                RebuildModifierRanges(model);
            }

            private static void RebuildModifierRanges(CatalogModel model)
            {
                var sortedModifiers = new List<CatalogModifier>();
                for (var i = 0; i < model.GameplayEffects.Count; i++)
                {
                    var ge = model.GameplayEffects[i];
                    var sourceModifiers = model.Modifiers
                        .Where(modifier => modifier.GameplayEffectCode == ge.GameplayEffectCode)
                        .OrderBy(modifier => modifier.ModifierIndex)
                        .ToArray();

                    ge.ModifierStart = sortedModifiers.Count;
                    ge.ModifierCount = sourceModifiers.Length;
                    sortedModifiers.AddRange(sourceModifiers);
                    model.GameplayEffects[i] = ge;
                }

                model.Modifiers.Clear();
                model.Modifiers.AddRange(sortedModifiers);
            }

            private static void BuildTagMasks(
                CatalogModel model,
                Dictionary<int, int> tagMaskIndices,
                DefinitionCatalogPhase.CatalogTagCatalog tagCatalog)
            {
                foreach (var pair in tagMaskIndices.OrderBy(item => item.Value))
                {
                    model.TagMasks.Add(new CatalogTagMask
                    {
                        TagCode = pair.Key,
                        Mask = tagCatalog.BuildMask(new[] { pair.Key }),
                    });
                }
            }

            private static void AddTagRequirement(
                List<DefinitionCatalogPhase.CatalogRequirement> requirements,
                int requirementKind,
                GAS.Runtime.TagRequirementMask tagQuery)
            {
                if (tagQuery.IsEmpty)
                    return;

                requirements.Add(new DefinitionCatalogPhase.CatalogRequirement
                {
                    RequirementKind = requirementKind,
                    TagMaskIndex = -1,
                    TagQuery = tagQuery,
                    AttributeSetCode = 0,
                    AttributeCode = 0,
                    CompareOp = 0,
                    CompareValue = 0f,
                });
            }

            private static GAS.Runtime.TagRequirementMask BuildTagRequirementMask(
                object row,
                string memberPrefix,
                DefinitionCatalogPhase.CatalogTagCatalog tagCatalog)
            {
                return new GAS.Runtime.TagRequirementMask
                {
                    All = tagCatalog.BuildMask(GetIntArray(row, memberPrefix + "AllTagCodes")),
                    Any = tagCatalog.BuildMask(GetIntArray(row, memberPrefix + "AnyTagCodes")),
                    None = tagCatalog.BuildMask(GetIntArray(row, memberPrefix + "NoneTagCodes")),
                };
            }

            private static bool HasModifier(object row)
            {
                if (TryGetMemberValue(row, "HasModifier", out var hasModifier))
                    return Convert.ToBoolean(hasModifier);

                return GetInt(row, "ModifierAttributeSetCode") > 0
                       && GetInt(row, "ModifierAttributeCode") > 0;
            }

            private static int GetModifierCount(object row)
            {
                var attributeSetCodes = GetArrayValue(row, "ModifierAttributeSetCodes");
                var attributeCodes = GetArrayValue(row, "ModifierAttributeCodes");
                var operations = GetArrayValue(row, "ModifierOperations");
                var magnitudes = GetArrayValue(row, "ModifierMagnitudes");
                if (attributeSetCodes != null || attributeCodes != null || operations != null || magnitudes != null)
                {
                    var count = MinPositiveLength(attributeSetCodes, attributeCodes, operations, magnitudes);
                    if (count > 0)
                        return count;
                }

                return HasModifier(row) ? 1 : 0;
            }

            private static int MinPositiveLength(params Array[] arrays)
            {
                var result = int.MaxValue;
                var hasArray = false;
                for (var i = 0; i < arrays.Length; i++)
                {
                    var array = arrays[i];
                    if (array == null)
                        continue;

                    hasArray = true;
                    result = Math.Min(result, array.Length);
                }

                return hasArray && result != int.MaxValue ? result : 0;
            }

            private static int GetEnumIntAt(object row, string arrayMemberName, int index, string fallbackMemberName)
            {
                return GetIntAt(row, arrayMemberName, index, fallbackMemberName);
            }

            private static int GetIntAt(object row, string arrayMemberName, int index, string fallbackMemberName)
            {
                var array = GetArrayValue(row, arrayMemberName);
                if (array != null && (uint)index < (uint)array.Length)
                    return Convert.ToInt32(array.GetValue(index), CultureInfo.InvariantCulture);

                return GetInt(row, fallbackMemberName);
            }

            private static float GetFloatAt(object row, string arrayMemberName, int index, string fallbackMemberName)
            {
                var array = GetArrayValue(row, arrayMemberName);
                if (array != null && (uint)index < (uint)array.Length)
                    return Convert.ToSingle(array.GetValue(index), CultureInfo.InvariantCulture);

                return GetFloat(row, fallbackMemberName);
            }

            private static Array GetArrayValue(object row, string memberName)
            {
                if (!TryGetMemberValue(row, memberName, out var value) || value == null)
                    return null;

                return value as Array;
            }

            private static int[] GetIntArray(object row, string memberName)
            {
                return ToPositiveIntArray(GetArrayValue(row, memberName));
            }

            private static int[] ToPositiveIntArray(Array values)
            {
                if (values == null || values.Length == 0)
                    return Array.Empty<int>();

                var result = new List<int>(values.Length);
                for (var i = 0; i < values.Length; i++)
                {
                    var value = Convert.ToInt32(values.GetValue(i), CultureInfo.InvariantCulture);
                    if (value > 0)
                        result.Add(value);
                }

                return result.ToArray();
            }

            private static int GetOrAddTagMaskIndex(Dictionary<int, int> tagMaskIndices, int tagCode)
            {
                if (tagCode <= 0)
                    return -1;

                if (tagMaskIndices.TryGetValue(tagCode, out var index))
                    return index;

                index = tagMaskIndices.Count;
                tagMaskIndices.Add(tagCode, index);
                return index;
            }

            private static int GetInt(object row, string memberName)
            {
                if (!TryGetMemberValue(row, memberName, out var value) || value == null)
                    return 0;

                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            private static int GetEnumInt(object row, string memberName)
            {
                return GetInt(row, memberName);
            }

            private static float GetFloat(object row, string memberName)
            {
                if (!TryGetMemberValue(row, memberName, out var value) || value == null)
                    return 0f;

                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }

            private static bool GetBool(object row, string memberName)
            {
                if (!TryGetMemberValue(row, memberName, out var value) || value == null)
                    return false;

                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }

            private static string GetString(object row, string memberName)
            {
                return TryGetMemberValue(row, memberName, out var value) ? value as string ?? string.Empty : string.Empty;
            }

            private static bool TryGetMemberValue(object row, string memberName, out object value)
            {
                value = null;
                if (row == null)
                    return false;

                var rowType = row.GetType();
                var field = rowType.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    value = field.GetValue(row);
                    return true;
                }

                var property = rowType.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (property != null && property.CanRead)
                {
                    value = property.GetValue(row);
                    return true;
                }

                return false;
            }

            private static int StableHash(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return 0;

                unchecked
                {
                    var hash = (uint)2166136261;
                    for (var i = 0; i < value.Length; i++)
                    {
                        hash ^= value[i];
                        hash *= 16777619;
                    }

                    return (int)hash;
                }
            }
        }

        private struct CatalogAbility
        {
            public int AbilityCode;
            public int Level;
            public int PrimaryGameplayEffectCode;
            public int SecondaryGameplayEffectCode;
            public int CostGameplayEffectCode;
            public int CooldownGameplayEffectCode;
            public int CooldownFrames;
            public int ActivationOwnedTagMaskIndex;
            public int RequirementStart;
            public int RequirementCount;
            public int TargetRuleCode;
            public int TargetRuleParam0;
            public int TargetRuleParam1;
        }

        private struct CatalogGameplayEffect
        {
            public int GameplayEffectCode;
            public int DurationFrames;
            public int PeriodFrames;
            public int PeriodGameplayEffectCode;
            public int GrantedTagMaskIndex;
            public int RemoveGameplayEffectTagMaskIndex;
            public GAS.Runtime.TagRequirementMask RemoveGameplayEffectTagQuery;
            public int GameplayCueCode;
            public int DamageTypeCode;
            public int ResistanceAttributeSetCode;
            public int ResistanceAttributeCode;
            public float ResistanceCap;
            public int StackingCode;
            public int StackLimitCount;
            public int StackType;
            public int EffectDurationRefreshPolicy;
            public int EffectPeriodResetPolicy;
            public int EffectExpirationPolicy;
            public bool DenyOverflowApplication;
            public bool ClearStackOnOverflow;
            public int OverflowGameplayEffectCode;
            public int ModifierStart;
            public int ModifierCount;
            public int RequirementStart;
            public int RequirementCount;
            public int GrantedAbilityStart;
            public int GrantedAbilityCount;
        }

        private struct CatalogTimeline
        {
            public int TimelineId;
            public int PrimaryGameplayEffectCode;
            public int SecondaryGameplayEffectCode;
            public int TargetRuleCode;
        }

        private struct CatalogModifier
        {
            public int GameplayEffectCode;
            public int ModifierIndex;
            public int AttributeSetCode;
            public int AttributeCode;
            public int Operation;
            public float BaseMagnitude;
            public int MagnitudeSource;
            public int MagnitudeKey;
            public int CaptureAttributeSetCode;
            public int CaptureAttributeCode;
            public int CaptureTiming;
            public float FallbackMagnitude;
            public float Coefficient;
            public float PreAdd;
            public float PostAdd;
        }

        private struct CatalogRequirement
        {
            public int RequirementKind;
            public int TagMaskIndex;
            public GAS.Runtime.TagRequirementMask TagQuery;
            public int AttributeSetCode;
            public int AttributeCode;
            public int CompareOp;
            public float CompareValue;
        }

        private struct CatalogTagMask
        {
            public int TagCode;
            public GAS.Runtime.TagMaskComponent Mask;
        }

        private struct CatalogTagEntry
        {
            public int TagCode;
            public int[] ParentCodes;
        }

        private sealed class CatalogTagCatalog
        {
            private readonly Dictionary<int, int> _denseIndexByCode;
            private readonly Dictionary<int, int[]> _parentCodesByCode;

            private CatalogTagCatalog(
                Dictionary<int, int> denseIndexByCode,
                Dictionary<int, int[]> parentCodesByCode)
            {
                _denseIndexByCode = denseIndexByCode;
                _parentCodesByCode = parentCodesByCode;
            }

            public static CatalogTagCatalog Create(IReadOnlyList<CatalogTagEntry> entries)
            {
                var denseIndexByCode = new Dictionary<int, int>();
                var parentCodesByCode = new Dictionary<int, int[]>();
                if (entries != null)
                {
                    for (var i = 0; i < entries.Count && i < GAS.Runtime.TagMaskComponent.Capacity; i++)
                    {
                        denseIndexByCode[entries[i].TagCode] = i;
                        parentCodesByCode[entries[i].TagCode] = entries[i].ParentCodes ?? Array.Empty<int>();
                    }
                }

                return new CatalogTagCatalog(denseIndexByCode, parentCodesByCode);
            }

            public GAS.Runtime.TagMaskComponent BuildMask(IEnumerable<int> tagCodes)
            {
                var mask = new GAS.Runtime.TagMaskComponent();
                if (tagCodes == null)
                    return mask;

                foreach (var tagCode in tagCodes)
                    AddTagAndParents(ref mask, tagCode);

                return mask;
            }

            private void AddTagAndParents(ref GAS.Runtime.TagMaskComponent mask, int tagCode)
            {
                if (!_denseIndexByCode.TryGetValue(tagCode, out var denseIndex))
                    return;

                mask.AddTag(denseIndex);
                if (!_parentCodesByCode.TryGetValue(tagCode, out var parentCodes))
                    return;

                for (var i = 0; i < parentCodes.Length; i++)
                    AddTagAndParents(ref mask, parentCodes[i]);
            }
        }
    }

    internal sealed class RuntimeDefinitionGluePhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "RuntimeDefinitionGlue";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[]
        {
            "Runtime/RuntimeDefinitionGlue.gen.cs",
        };

        public override bool RequiresRows => false;

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var gluePath = GetOutputPath(context, OutputFileNames[0]);

            using var writer = new IndentedWriter(new StreamWriter(gluePath));
            WriteHeader(writer);
            writer.WriteLine("using GAS.Runtime;");
            writer.WriteLine("using Unity.Burst.Intrinsics;");
            writer.WriteLine("using Unity.Collections;");
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;
            WriteRuntimeDefinitionResolver(writer);
            writer.WriteLine("");
            WriteRequirementEvaluator(writer);
            writer.WriteLine("");
            WriteMagnitudeEvaluator(writer);
            writer.WriteLine("");
            WriteTargetRuleTable(writer);
            writer.Indent--;
            writer.WriteLine("}");

            AddRuntimePureGlueManifest(manifest, PhaseName, gluePath);
        }

        private static void WriteRuntimeDefinitionResolver(IndentedWriter writer)
        {
            writer.WriteLine("public static class GASGeneratedRuntimeDefinitionResolver");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static bool TryBuildAbilityActivationPlan(");
            writer.Indent++;
            writer.WriteLine("ref GASDefinitionCatalogBlob catalog,");
            writer.WriteLine("int abilityCode,");
            writer.WriteLine("Entity sourceAsc,");
            writer.WriteLine("Entity targetAsc,");
            writer.WriteLine("Entity sourceAbility,");
            writer.WriteLine("int frame,");
            writer.WriteLine("int requestedLevel,");
            writer.WriteLine("out AbilityActivationPlanRecord plan)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("plan = default;");
            writer.WriteLine("if (!GASGeneratedDefinitionCatalogLookup.TryGetAbilityIndex(ref catalog, abilityCode, out var abilityIndex))");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("plan.FailureReasonCode = GASFailureReasonCodes.AbilityNotFound;");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("ref readonly var ability = ref GASGeneratedDefinitionCatalogLookup.GetAbility(ref catalog, abilityIndex);");
            writer.WriteLine("plan = new AbilityActivationPlanRecord");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("Frame = frame,");
            writer.WriteLine("AbilityCode = ability.AbilityCode,");
            writer.WriteLine("AbilityDefinitionIndex = abilityIndex,");
            writer.WriteLine("Level = requestedLevel > 0 ? requestedLevel : ability.Level,");
            writer.WriteLine("SourceAsc = sourceAsc,");
            writer.WriteLine("TargetAsc = targetAsc,");
            writer.WriteLine("SourceAbility = sourceAbility,");
            writer.WriteLine("PrimaryGameplayEffectCode = ability.PrimaryGameplayEffectCode,");
            writer.WriteLine("SecondaryGameplayEffectCode = ability.SecondaryGameplayEffectCode,");
            writer.WriteLine("CostGameplayEffectCode = ability.CostGameplayEffectCode,");
            writer.WriteLine("CooldownGameplayEffectCode = ability.CooldownGameplayEffectCode,");
            writer.WriteLine("CooldownFrames = ability.CooldownFrames,");
            writer.WriteLine("TargetRuleCode = ability.TargetRuleCode,");
            writer.WriteLine("FailureReasonCode = GASFailureReasonCodes.None,");
            writer.Indent--;
            writer.WriteLine("};");
            writer.WriteLine("return true;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public static bool TryBuildGECommandSeed(");
            writer.Indent++;
            writer.WriteLine("ref GASDefinitionCatalogBlob catalog,");
            writer.WriteLine("in AbilityActivationPlanRecord plan,");
            writer.WriteLine("int seedKind,");
            writer.WriteLine("GEEffectCommandSource source,");
            writer.WriteLine("int gameplayEffectCode,");
            writer.WriteLine("int contextId,");
            writer.WriteLine("int parentContextId,");
            writer.WriteLine("out GECommandSeedRecord seed)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("seed = default;");
            writer.WriteLine("if (!plan.Succeeded)");
            writer.Indent++;
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("return TryBuildSeed(ref catalog, in plan, seedKind, source, gameplayEffectCode, contextId, parentContextId, out seed);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public static int AppendModifierRecords(");
            writer.Indent++;
            writer.WriteLine("ref GASDefinitionCatalogBlob catalog,");
            writer.WriteLine("int gameplayEffectDefinitionIndex,");
            writer.WriteLine("in MagnitudeEvalContext context,");
            writer.WriteLine("ref NativeList<ResolvedModifierRecord> modifiers)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if ((uint)gameplayEffectDefinitionIndex >= (uint)catalog.GameplayEffects.Length)");
            writer.Indent++;
            writer.WriteLine("return 0;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectDefinitionIndex);");
            writer.WriteLine("var appended = 0;");
            writer.WriteLine("for (var i = 0; i < gameplayEffect.ModifierCount; i++)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var modifierIndex = gameplayEffect.ModifierStart + i;");
            writer.WriteLine("if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var modifier = catalog.Modifiers[modifierIndex];");
            writer.WriteLine("if (!GASGeneratedMagnitudeEvaluator.TryResolveMagnitude(in modifier, in context, out var magnitude))");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("modifiers.Add(new ResolvedModifierRecord");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("GameplayEffectCode = gameplayEffect.GameplayEffectCode,");
            writer.WriteLine("ModifierIndex = modifier.ModifierIndex,");
            writer.WriteLine("AttributeSetCode = modifier.AttributeSetCode,");
            writer.WriteLine("AttributeCode = modifier.AttributeCode,");
            writer.WriteLine("Operation = modifier.Operation,");
            writer.WriteLine("Magnitude = magnitude,");
            writer.WriteLine("MagnitudeSource = modifier.MagnitudeSource,");
            writer.WriteLine("MagnitudeKey = modifier.MagnitudeKey,");
            writer.WriteLine("SourceAsc = context.SourceAsc,");
            writer.WriteLine("TargetAsc = context.TargetAsc,");
            writer.Indent--;
            writer.WriteLine("});");
            writer.WriteLine("appended++;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("return appended;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static bool TryBuildSeed(");
            writer.Indent++;
            writer.WriteLine("ref GASDefinitionCatalogBlob catalog,");
            writer.WriteLine("in AbilityActivationPlanRecord plan,");
            writer.WriteLine("int seedKind,");
            writer.WriteLine("GEEffectCommandSource source,");
            writer.WriteLine("int gameplayEffectCode,");
            writer.WriteLine("int contextId,");
            writer.WriteLine("int parentContextId,");
            writer.WriteLine("out GECommandSeedRecord seed)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("seed = default;");
            writer.WriteLine("if (gameplayEffectCode <= 0)");
            writer.Indent++;
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var failureReason = GASFailureReasonCodes.None;");
            writer.WriteLine("var durationFrameOverride = 0;");
            writer.WriteLine("var flags = GASGECommandSeedFlags.None;");
            writer.WriteLine("if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, gameplayEffectCode, out var gameplayEffectIndex))");
            writer.Indent++;
            writer.WriteLine("failureReason = GASFailureReasonCodes.GameplayEffectNotFound;");
            writer.Indent--;
            writer.WriteLine("else");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);");
            writer.WriteLine("durationFrameOverride = seedKind == GASGESeedKind.Cooldown && plan.CooldownFrames > 0");
            writer.Indent++;
            writer.WriteLine("? plan.CooldownFrames");
            writer.WriteLine(": gameplayEffect.DurationFrames;");
            writer.Indent--;
            writer.WriteLine("if (durationFrameOverride > 0");
            writer.Indent++;
            writer.WriteLine("|| gameplayEffect.PeriodFrames > 0");
            writer.WriteLine("|| gameplayEffect.StackLimitCount > 0");
            writer.WriteLine("|| gameplayEffect.GrantedTagMaskIndex >= 0");
            writer.WriteLine("|| !gameplayEffect.RemoveGameplayEffectTagQuery.IsEmpty");
            writer.WriteLine("|| gameplayEffect.GrantedAbilityCount > 0");
            writer.WriteLine("|| gameplayEffect.ModifierCount == 0)");
            writer.Indent--;
            writer.Indent++;
            writer.WriteLine("flags |= GASGECommandSeedFlags.ActiveMutation;");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("seed = new GECommandSeedRecord");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("Frame = plan.Frame,");
            writer.WriteLine("SeedKind = seedKind,");
            writer.WriteLine("Source = source,");
            writer.WriteLine("SourceAsc = plan.SourceAsc,");
            writer.WriteLine("TargetAsc = plan.TargetAsc,");
            writer.WriteLine("SourceAbility = plan.SourceAbility,");
            writer.WriteLine("GameplayEffectCode = gameplayEffectCode,");
            writer.WriteLine("GameplayEffectDefinitionIndex = gameplayEffectIndex,");
            writer.WriteLine("Level = plan.Level,");
            writer.WriteLine("ContextId = contextId,");
            writer.WriteLine("ParentContextId = parentContextId,");
            writer.WriteLine("DurationFrameOverride = durationFrameOverride,");
            writer.WriteLine("FailureReasonCode = failureReason,");
            writer.WriteLine("Flags = flags,");
            writer.Indent--;
            writer.WriteLine("};");
            writer.WriteLine("return failureReason == GASFailureReasonCodes.None;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        internal static void WriteRuntimeAbilityActivationSystem(GasCodeGenContext context, string path)
        {
            var source = RuntimeAbilityActivationSystemTemplate.Replace("__ROOT_NAMESPACE__", context.RootNamespace);
            File.WriteAllText(path, source);
        }

        private const string RuntimeAbilityActivationSystemTemplate = @"///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace __ROOT_NAMESPACE__
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateAfter(typeof(AbilityTryActivateSystem))]
    [UpdateBefore(typeof(AbilityCommitSystem))]
    public partial struct AbilityCatalogCommitSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<AbilityCommitRequestComponent>(),
                    ComponentType.ReadWrite<AbilityStateComponent>(),
                    ComponentType.ReadWrite<AbilityEndRequestComponent>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
            });
            state.RequireForUpdate(_query);
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || !EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            state.Dependency = new AbilityCatalogCommitJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                StateTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityStateComponent>(),
                CommitRequestTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityCommitRequestComponent>(),
                DestroyingLookup = SystemAPI.GetComponentLookup<ASCDestroyingComponent>(isReadOnly: true),
                TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(),
                FixedTagMaskLookup = SystemAPI.GetComponentLookup<TagFixedMaskComponent>(isReadOnly: true),
                TemporaryTagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(),
                AutoEndOnCommitLookup = SystemAPI.GetComponentLookup<AbilityAutoEndOnCommitComponent>(isReadOnly: true),
                GrantedByEffectLookup = SystemAPI.GetComponentLookup<AbilityGrantedByEffectComponent>(isReadOnly: true),
                CancelRequestLookup = SystemAPI.GetComponentLookup<AbilityCancelRequestComponent>(),
                EndRequestTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityEndRequestComponent>(),
                DestroyOnCleanupLookup = SystemAPI.GetComponentLookup<AbilityDestroyOnCleanupComponent>(isReadOnly: true),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(),
                SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: true),
                ActiveMutationCommandLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationCommandBuffer>(isReadOnly: false),
                ActiveMutationSetByCallerLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationSetByCallerValueBuffer>(isReadOnly: false),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(),
                Catalog = catalogComponent.Catalog,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
            }.Schedule(_query, state.Dependency);
        }

        [BurstCompile]
        private struct AbilityCatalogCommitJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<AbilityStateComponent> StateTypeHandle;
            public ComponentTypeHandle<AbilityCommitRequestComponent> CommitRequestTypeHandle;
            [ReadOnly] public ComponentLookup<ASCDestroyingComponent> DestroyingLookup;
            public ComponentLookup<TagMaskComponent> TagMaskLookup;
            [ReadOnly] public ComponentLookup<TagFixedMaskComponent> FixedTagMaskLookup;
            public BufferLookup<TagTemporarySourceBuffer> TemporaryTagSourceLookup;
            [ReadOnly] public ComponentLookup<AbilityAutoEndOnCommitComponent> AutoEndOnCommitLookup;
            [ReadOnly] public ComponentLookup<AbilityGrantedByEffectComponent> GrantedByEffectLookup;
            public ComponentLookup<AbilityCancelRequestComponent> CancelRequestLookup;
            public ComponentTypeHandle<AbilityEndRequestComponent> EndRequestTypeHandle;
            [ReadOnly] public ComponentLookup<AbilityDestroyOnCleanupComponent> DestroyOnCleanupLookup;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            [ReadOnly] public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;
            public BufferLookup<ActiveEffectMutationCommandBuffer> ActiveMutationCommandLookup;
            public BufferLookup<ActiveEffectMutationSetByCallerValueBuffer> ActiveMutationSetByCallerLookup;
            public BufferLookup<GameplayEventBuffer> FactLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public int Frame;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Catalog.IsCreated
                    || StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity))
                {
                    return;
                }

                var abilities = chunk.GetNativeArray(EntityTypeHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);
                var commitRequests = chunk.GetNativeArray(ref CommitRequestTypeHandle);
                var commitRequestMask = chunk.GetEnabledMask(ref CommitRequestTypeHandle);
                var endRequests = chunk.GetNativeArray(ref EndRequestTypeHandle);
                var endRequestMask = chunk.GetEnabledMask(ref EndRequestTypeHandle);
                ref var catalog = ref Catalog.Value;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    if (!commitRequestMask[entityIndex])
                        continue;

                    var ability = abilities[entityIndex];
                    var state = states[entityIndex];
                    var commitRequest = commitRequests[entityIndex];
                    ApplyAbilityActivationCommitRecord(
                        ability,
                        commitRequest.TargetAsc,
                        ref catalog,
                        ref state,
                        ref endRequests,
                        endRequestMask,
                        entityIndex);
                    states[entityIndex] = state;
                    commitRequestMask[entityIndex] = false;
                }
            }

            private bool ApplyAbilityActivationCommitRecord(
                Entity ability,
                Entity requestedTarget,
                ref GASDefinitionCatalogBlob catalog,
                ref AbilityStateComponent state,
                ref NativeArray<AbilityEndRequestComponent> endRequests,
                EnabledMask endRequestMask,
                int entityIndex)
            {
                var resolvedTarget = ResolveMainTarget(requestedTarget, state.Owner);

                var committed = TryCommitAbility(
                    ability,
                    state,
                    resolvedTarget,
                    ref catalog,
                    out var nextRuntime);

                if (!committed)
                    return false;

                if (AutoEndOnCommitLookup.HasComponent(ability)
                    && AutoEndOnCommitLookup.IsComponentEnabled(ability))
                {
                    if (CanCompleteAutoEndOnCommitDirectly(ability, endRequestMask, entityIndex))
                    {
                        CompleteAutoEndOnCommitDirectly(
                            ability,
                            nextRuntime.Owner,
                            HasActivationOwnedTags(ref catalog, state.Code),
                            ref nextRuntime);
                    }
                    else
                    {
                        RequestAbilityEnd(
                            ability,
                            EAbilityLifecycleReason.ActivationCompleted,
                            ref endRequests,
                            endRequestMask,
                            entityIndex,
                            sourceAbility: ability,
                            sourceAbilityCode: nextRuntime.Code);
                    }
                }

                state = nextRuntime;
                return true;
            }

            private bool CanCompleteAutoEndOnCommitDirectly(
                Entity ability,
                EnabledMask endRequestMask,
                int entityIndex)
            {
                if (GrantedByEffectLookup.HasComponent(ability)
                    || IsDestroyOnCleanupEnabled(ability))
                {
                    return false;
                }

                if (CancelRequestLookup.HasComponent(ability)
                    && CancelRequestLookup.IsComponentEnabled(ability))
                {
                    return false;
                }

                return !endRequestMask[entityIndex];
            }

            private void CompleteAutoEndOnCommitDirectly(
                Entity ability,
                Entity owner,
                bool removeActivationOwnedTags,
                ref AbilityStateComponent state)
            {
                if (removeActivationOwnedTags)
                    RemoveTagsFromSource(owner, ability);
                EnqueueAutoEndLifecycleEvent(
                    EGameplayEventType.AbilityEndRequested,
                    ability,
                    in state);
                EnqueueAutoEndLifecycleEvent(
                    EGameplayEventType.AbilityEnded,
                    ability,
                    in state);

                state.Phase = EAbilityPhase.Ready;
                state.Timer = 0f;
                state.RemainingFrame = 0;
            }

            private void EnqueueAutoEndLifecycleEvent(
                EGameplayEventType type,
                Entity ability,
                in AbilityStateComponent state)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = type,
                    Domain = EGameplayFactDomain.Ability,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = state.Owner,
                    TargetAsc = state.Owner,
                    SourceAbility = ability,
                    EventCode = state.Code,
                    ReasonCode = (int)EAbilityLifecycleReason.ActivationCompleted,
                    Value = state.Code,
                });
            }

            private bool TryCommitAbility(
                Entity ability,
                in AbilityStateComponent state,
                Entity target,
                ref GASDefinitionCatalogBlob catalog,
                out AbilityStateComponent nextRuntime)
            {
                nextRuntime = state;
                if (state.Phase == EAbilityPhase.Activating || state.Phase == EAbilityPhase.Active)
                    return false;

                if (!GASGeneratedRuntimeDefinitionResolver.TryBuildAbilityActivationPlan(
                    ref catalog,
                    state.Code,
                    state.Owner,
                    target,
                    ability,
                    Frame,
                    state.Level,
                    out var plan))
                    return false;

                ref readonly var abilityDefinition = ref GASGeneratedDefinitionCatalogLookup.GetAbility(ref catalog, plan.AbilityDefinitionIndex);
                if (abilityDefinition.RequirementCount > 0)
                {
                    var ownerTags = TagMaskLookup.HasComponent(state.Owner)
                        ? TagMaskLookup[state.Owner]
                        : default;
                    if (!GASGeneratedRequirementEvaluator.EvaluateAbilityRequirements(
                        ref catalog,
                        plan.AbilityDefinitionIndex,
                        in ownerTags,
                        out _))
                        return false;
                }

                ApplyActivationOwnedTags(ability, state.Owner, ref catalog, in abilityDefinition);

                AppendAbilityEffectCommand(ref catalog, in plan, GASGESeedKind.Cost, plan.CostGameplayEffectCode);
                AppendAbilityEffectCommand(ref catalog, in plan, GASGESeedKind.Cooldown, plan.CooldownGameplayEffectCode);
                AppendAbilityEffectCommand(ref catalog, in plan, GASGESeedKind.Primary, plan.PrimaryGameplayEffectCode);
                AppendAbilityEffectCommand(ref catalog, in plan, GASGESeedKind.Secondary, plan.SecondaryGameplayEffectCode);

                nextRuntime.Phase = EAbilityPhase.Active;
                nextRuntime.Timer = 0f;
                nextRuntime.RemainingFrame = -1;
                return true;
            }

            private void AppendAbilityEffectCommand(
                ref GASDefinitionCatalogBlob catalog,
                in AbilityActivationPlanRecord plan,
                int seedKind,
                int gameplayEffectCode)
            {
                if (!GASGeneratedRuntimeDefinitionResolver.TryBuildGECommandSeed(
                        ref catalog,
                        in plan,
                        seedKind,
                        GEEffectCommandSource.Ability,
                        gameplayEffectCode,
                        contextId: 0,
                        parentContextId: 0,
                        out var seed))
                {
                    return;
                }

                AppendEffectCommand(ToEffectCommand(in seed));
            }

            private void AppendEffectCommand(in GEEffectCommandBuffer command)
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity))
                {
                    return;
                }

                if (command.Kind == GEEffectCommandKind.ActiveMutation
                    && !CanAppendActiveMutationCommand(in command))
                {
                    return;
                }

                if (command.Kind == GEEffectCommandKind.Instant
                    && !CanAppendInstantCommand(in command))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                var targetAsc = ResolveTargetAsc(in command);
                var resolved = PrepareCommand(
                    ref stream,
                    command.Kind == GEEffectCommandKind.ActiveMutation
                        ? 0
                        : SetByCallerLookup[targetAsc].Length,
                    in command);
                if (resolved.Kind == GEEffectCommandKind.ActiveMutation)
                    AppendActiveMutationCommand(in resolved);
                else
                    AppendInstantCommand(in resolved);
                StreamLookup[StreamEntity] = stream;
            }

            private bool CanAppendInstantCommand(in GEEffectCommandBuffer command)
            {
                var targetAsc = ResolveTargetAsc(in command);
                return targetAsc != Entity.Null
                    && CommandLookup.HasBuffer(targetAsc)
                    && SetByCallerLookup.HasBuffer(targetAsc);
            }

            private bool CanAppendActiveMutationCommand(in GEEffectCommandBuffer command)
            {
                var targetAsc = ResolveTargetAsc(in command);
                return targetAsc != Entity.Null
                    && ActiveMutationCommandLookup.HasBuffer(targetAsc)
                    && ActiveMutationSetByCallerLookup.HasBuffer(targetAsc);
            }

            private void AppendActiveMutationCommand(in GEEffectCommandBuffer command)
            {
                var targetAsc = ResolveTargetAsc(in command);
                var ownerCommand = command;
                ownerCommand.SetByCallerStart = 0;
                ownerCommand.SetByCallerCount = 0;
                ActiveMutationCommandLookup[targetAsc].Add(new ActiveEffectMutationCommandBuffer
                {
                    Command = ownerCommand,
                });
            }

            private void AppendInstantCommand(in GEEffectCommandBuffer command)
            {
                var targetAsc = ResolveTargetAsc(in command);
                var ownerCommand = command;
                ownerCommand.SetByCallerStart = SetByCallerLookup[targetAsc].Length;
                ownerCommand.SetByCallerCount = 0;
                CommandLookup[targetAsc].Add(ownerCommand);
            }

            private static Entity ResolveTargetAsc(in GEEffectCommandBuffer command)
            {
                return command.TargetAsc != Entity.Null ? command.TargetAsc : command.SourceAsc;
            }

            private GEEffectCommandBuffer PrepareCommand(
                ref GEEffectCommandStreamComponent stream,
                int setByCallerStart,
                in GEEffectCommandBuffer command)
            {
                var resolved = command;
                if (resolved.Sequence <= 0)
                    resolved.Sequence = Allocate(ref stream.NextCommandSequence);
                if (resolved.Frame <= 0)
                    resolved.Frame = Frame;
                if (resolved.ContextId <= 0)
                    resolved.ContextId = Allocate(ref stream.NextContextId);
                if (resolved.TargetAsc == Entity.Null)
                    resolved.TargetAsc = resolved.SourceAsc;
                if (resolved.Instigator == Entity.Null)
                    resolved.Instigator = resolved.SourceAsc;
                if (resolved.Causer == Entity.Null)
                    resolved.Causer = resolved.SourceAbility;

                resolved.SetByCallerStart = setByCallerStart;
                resolved.SetByCallerCount = 0;
                return resolved;
            }

            private static GEEffectCommandBuffer ToEffectCommand(in GECommandSeedRecord seed)
            {
                var isActiveMutation = (seed.Flags & GASGECommandSeedFlags.ActiveMutation) != 0;
                return new GEEffectCommandBuffer
                {
                    Frame = seed.Frame,
                    Kind = isActiveMutation ? GEEffectCommandKind.ActiveMutation : GEEffectCommandKind.Instant,
                    Source = seed.Source,
                    SourceAsc = seed.SourceAsc,
                    TargetAsc = seed.TargetAsc,
                    SourceAbility = seed.SourceAbility,
                    SourceEffect = seed.SourceEffect,
                    Instigator = seed.SourceAsc,
                    Causer = seed.SourceAbility,
                    GameplayEffectCode = seed.GameplayEffectCode,
                    Level = seed.Level,
                    DurationFrameOverride = seed.DurationFrameOverride,
                    ContextId = seed.ContextId,
                    ParentContextId = seed.ParentContextId,
                    TargetDataKind = seed.TargetAsc == seed.SourceAsc ? ETargetDataKind.Self : ETargetDataKind.Entity,
                    Flags = seed.Flags,
                };
            }

            private static bool HasActivationOwnedTags(
                ref GASDefinitionCatalogBlob catalog,
                int abilityCode)
            {
                for (var i = 0; i < catalog.Abilities.Length; i++)
                {
                    var abilityDefinition = catalog.Abilities[i];
                    if (abilityDefinition.AbilityCode != abilityCode)
                        continue;

                    var tagMaskIndex = abilityDefinition.ActivationOwnedTagMaskIndex;
                    return tagMaskIndex >= 0
                           && tagMaskIndex < catalog.TagMasks.Length
                           && !catalog.TagMasks[tagMaskIndex].Mask.IsEmpty;
                }

                return false;
            }

            private void ApplyActivationOwnedTags(
                Entity ability,
                Entity owner,
                ref GASDefinitionCatalogBlob catalog,
                in GASCatalogAbilityDefinitionBlob abilityDefinition)
            {
                var tagMaskIndex = abilityDefinition.ActivationOwnedTagMaskIndex;
                if (tagMaskIndex < 0 || tagMaskIndex >= catalog.TagMasks.Length)
                    return;
                if (owner == Entity.Null || !TagMaskLookup.HasComponent(owner))
                    return;

                var ownedMask = catalog.TagMasks[tagMaskIndex].Mask;
                if (ownedMask.IsEmpty)
                    return;

                var ownerTags = TagMaskLookup[owner];
                ownerTags.Mask0 |= ownedMask.Mask0;
                ownerTags.Mask1 |= ownedMask.Mask1;
                ownerTags.Mask2 |= ownedMask.Mask2;
                ownerTags.Mask3 |= ownedMask.Mask3;
                TagMaskLookup[owner] = ownerTags;

                if (!TemporaryTagSourceLookup.HasBuffer(owner))
                    return;

                var tempSources = TemporaryTagSourceLookup[owner];
                for (var tagIndex = 0; tagIndex < TagMaskComponent.Capacity; tagIndex++)
                {
                    if (!ownedMask.HasTag(tagIndex))
                        continue;
                    tempSources.Add(new TagTemporarySourceBuffer
                    {
                        TagIndex = tagIndex,
                        Source = ability,
                    });
                }
            }

            private Entity ResolveMainTarget(Entity requestedTarget, Entity fallbackTarget)
            {
                if (IsAvailableAsc(requestedTarget))
                    return requestedTarget;

                return fallbackTarget;
            }

            private bool IsAvailableAsc(Entity asc)
            {
                return asc != Entity.Null
                    && DestroyingLookup.HasComponent(asc)
                    && !DestroyingLookup.IsComponentEnabled(asc);
            }

            private bool IsDestroyOnCleanupEnabled(Entity ability)
            {
                return DestroyOnCleanupLookup.HasComponent(ability)
                       && DestroyOnCleanupLookup.IsComponentEnabled(ability);
            }

            private void RequestAbilityEnd(
                Entity ability,
                EAbilityLifecycleReason reason,
                ref NativeArray<AbilityEndRequestComponent> endRequests,
                EnabledMask endRequestMask,
                int entityIndex,
                Entity sourceAbility = default,
                Entity sourceEffect = default,
                int sourceAbilityCode = 0)
            {
                if (endRequestMask[entityIndex])
                {
                    return;
                }

                endRequests[entityIndex] = new AbilityEndRequestComponent
                {
                    Reason = reason,
                    SourceAbility = sourceAbility,
                    SourceEffect = sourceEffect,
                    SourceAbilityCode = sourceAbilityCode,
                };
                endRequestMask[entityIndex] = true;
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.AbilityEndRequested,
                    Domain = EGameplayFactDomain.Ability,
                    Category = EGameplayFactCategory.Request,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAbility = ability,
                    SourceEffect = sourceEffect,
                    ReasonCode = (int)reason,
                    EventCode = sourceAbilityCode,
                    Value = sourceAbilityCode,
                });
            }

            private void RemoveTagsFromSource(Entity owner, Entity source)
            {
                if (owner == Entity.Null
                    || !TagMaskLookup.HasComponent(owner)
                    || !TemporaryTagSourceLookup.HasBuffer(owner))
                {
                    return;
                }

                var sources = TemporaryTagSourceLookup[owner];
                for (var i = sources.Length - 1; i >= 0; i--)
                {
                    if (sources[i].Source != source)
                        continue;

                    var tagIndex = sources[i].TagIndex;
                    sources.RemoveAt(i);
                    RemoveTagIndexFromEffectiveMaskIfUnreferenced(owner, tagIndex);
                }
            }

            private void RemoveTagIndexFromEffectiveMaskIfUnreferenced(Entity owner, int tagIndex)
            {
                if (!TagMaskLookup.HasComponent(owner))
                    return;

                if (FixedTagMaskLookup.HasComponent(owner)
                    && FixedTagMaskLookup[owner].Mask.HasTag(tagIndex))
                {
                    return;
                }

                if (HasAnyTemporarySourceForTag(owner, tagIndex))
                    return;

                var mask = TagMaskLookup[owner];
                mask.RemoveTag(tagIndex);
                TagMaskLookup[owner] = mask;
            }

            private bool HasAnyTemporarySourceForTag(Entity owner, int tagIndex)
            {
                if (!TemporaryTagSourceLookup.HasBuffer(owner))
                    return false;

                var temporaryTags = TemporaryTagSourceLookup[owner];
                for (var i = 0; i < temporaryTags.Length; i++)
                    if (temporaryTags[i].TagIndex == tagIndex)
                        return true;
                return false;
            }

            private void EnqueueGameplayEvent(GameplayEventBuffer evt)
            {
                if (StreamEntity == Entity.Null || !FactLookup.HasBuffer(StreamEntity))
                    return;

                evt.Frame = Frame;
                if (StreamLookup.HasComponent(StreamEntity))
                {
                    var stream = StreamLookup[StreamEntity];
                    evt.Sequence = Allocate(ref stream.NextFactSequence);
                    StreamLookup[StreamEntity] = stream;
                }
                else
                {
                    evt.Sequence = 0;
                }

                FactLookup[StreamEntity].Add(evt);
            }

            private static int Allocate(ref int next)
            {
                var value = next;
                next++;
                if (next <= 0)
                    next = 1;
                return value <= 0 ? Allocate(ref next) : value;
            }
        }
    }
}
";

        internal static void WriteRuntimeEffectInstantSystems(GasCodeGenContext context, string path)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using System.Collections.Generic;");
            writer.WriteLine("using GAS.Runtime;");
            writer.WriteLine("using Unity.Burst;");
            writer.WriteLine("using Unity.Burst.Intrinsics;");
            writer.WriteLine("using Unity.Collections;");
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("using Unity.Jobs;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;
            WriteGeneratedInstantSpecBuildSystem(writer);
            writer.WriteLine("");
            WriteGeneratedAttributeDeltaApplySystem(writer);
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteGeneratedInstantSpecBuildSystem(IndentedWriter writer)
        {
            writer.WriteLine("[UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]");
            writer.WriteLine("[UpdateAfter(typeof(GEEffectCommandCatalogNormalizeSystem))]");
            writer.WriteLine("public partial struct GEEffectSpecBuildSystem : ISystem");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("private EntityQuery _ownerInstantCommandQuery;");
            writer.WriteLine("");
            writer.WriteLine("public void OnCreate(ref SystemState state)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("_ownerInstantCommandQuery = state.GetEntityQuery(new EntityQueryDesc");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("All = new[]");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("ComponentType.ReadOnly<ASCIdentityComponent>(),");
            writer.WriteLine("ComponentType.ReadOnly<GEEffectCommandBuffer>(),");
            writer.WriteLine("ComponentType.ReadOnly<GESetByCallerValueBuffer>(),");
            writer.WriteLine("ComponentType.ReadOnly<GEEffectSpecBuffer>(),");
            writer.Indent--;
            writer.WriteLine("},");
            writer.Indent--;
            writer.WriteLine("});");
            writer.WriteLine("state.RequireForUpdate(_ownerInstantCommandQuery);");
            writer.WriteLine("state.RequireForUpdate<GEEffectCommandStreamComponent>();");
            writer.WriteLine("state.RequireForUpdate<GASDefinitionCatalogComponent>();");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public void OnUpdate(ref SystemState state)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();");
            writer.WriteLine("if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))");
            writer.Indent++;
            writer.WriteLine("return;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();");
            writer.WriteLine("");
            writer.WriteLine("var records = new NativeList<OwnerLocalInstantSpecCommandRecord>(1, Allocator.TempJob);");
            writer.WriteLine("var payloads = new NativeList<GESetByCallerValueBuffer>(1, Allocator.TempJob);");
            writer.WriteLine("var collectHandle = new CollectOwnerLocalInstantSpecCommandsJob");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("EntityType = SystemAPI.GetEntityTypeHandle(),");
            writer.WriteLine("CommandType = SystemAPI.GetBufferTypeHandle<GEEffectCommandBuffer>(isReadOnly: true),");
            writer.WriteLine("SetByCallerType = SystemAPI.GetBufferTypeHandle<GESetByCallerValueBuffer>(isReadOnly: true),");
            writer.WriteLine("Records = records,");
            writer.WriteLine("Payloads = payloads,");
            writer.Indent--;
            writer.WriteLine("}.Schedule(_ownerInstantCommandQuery, state.Dependency);");
            writer.WriteLine("");
            writer.WriteLine("var buildHandle = new BuildOwnerLocalInstantSpecsJob");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),");
            writer.WriteLine("SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(),");
            writer.WriteLine("SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(),");
            writer.WriteLine("EntityStorageInfoLookup = SystemAPI.GetEntityStorageInfoLookup(),");
            writer.WriteLine("DestroyingLookup = SystemAPI.GetComponentLookup<ASCDestroyingComponent>(isReadOnly: true),");
            writer.WriteLine("TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: true),");
            writer.WriteLine("Catalog = catalogComponent.Catalog,");
            writer.WriteLine("StreamEntity = streamEntity,");
            writer.WriteLine("Records = records,");
            writer.WriteLine("Payloads = payloads,");
            writer.Indent--;
            writer.WriteLine("}.Schedule(collectHandle);");
            writer.WriteLine("");
            writer.WriteLine("var disposeRecordsHandle = records.Dispose(buildHandle);");
            writer.WriteLine("state.Dependency = payloads.Dispose(disposeRecordsHandle);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private struct OwnerLocalInstantSpecCommandRecord");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public Entity Owner;");
            writer.WriteLine("public int LocalIndex;");
            writer.WriteLine("public int PayloadStart;");
            writer.WriteLine("public int PayloadCount;");
            writer.WriteLine("public GEEffectCommandBuffer Command;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("[BurstCompile]");
            writer.WriteLine("private struct CollectOwnerLocalInstantSpecCommandsJob : IJobChunk");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("[ReadOnly] public EntityTypeHandle EntityType;");
            writer.WriteLine("[ReadOnly] public BufferTypeHandle<GEEffectCommandBuffer> CommandType;");
            writer.WriteLine("[ReadOnly] public BufferTypeHandle<GESetByCallerValueBuffer> SetByCallerType;");
            writer.WriteLine("public NativeList<OwnerLocalInstantSpecCommandRecord> Records;");
            writer.WriteLine("public NativeList<GESetByCallerValueBuffer> Payloads;");
            writer.WriteLine("");
            writer.WriteLine("public void Execute(");
            writer.Indent++;
            writer.WriteLine("in ArchetypeChunk chunk,");
            writer.WriteLine("int unfilteredChunkIndex,");
            writer.WriteLine("bool useEnabledMask,");
            writer.WriteLine("in v128 chunkEnabledMask)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var entities = chunk.GetNativeArray(EntityType);");
            writer.WriteLine("var commands = chunk.GetBufferAccessor(ref CommandType);");
            writer.WriteLine("var setByCallerValues = chunk.GetBufferAccessor(ref SetByCallerType);");
            writer.WriteLine("var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);");
            writer.WriteLine("while (enumerator.NextEntityIndex(out var entityIndex))");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var owner = entities[entityIndex];");
            writer.WriteLine("var ownerCommands = commands[entityIndex];");
            writer.WriteLine("var ownerSetByCallerValues = setByCallerValues[entityIndex];");
            writer.WriteLine("for (var commandIndex = 0; commandIndex < ownerCommands.Length; commandIndex++)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var command = ownerCommands[commandIndex];");
            writer.WriteLine("if (command.Kind != GEEffectCommandKind.Instant");
            writer.Indent++;
            writer.WriteLine("|| command.Sequence <= 0)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("var payloadStart = Payloads.Length;");
            writer.WriteLine("var payloadCount = CopySetByCallerValues(ownerSetByCallerValues, in command, Payloads);");
            writer.WriteLine("Records.Add(new OwnerLocalInstantSpecCommandRecord");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("Owner = owner,");
            writer.WriteLine("LocalIndex = commandIndex,");
            writer.WriteLine("PayloadStart = payloadStart,");
            writer.WriteLine("PayloadCount = payloadCount,");
            writer.WriteLine("Command = command,");
            writer.Indent--;
            writer.WriteLine("});");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("[BurstCompile]");
            writer.WriteLine("private struct BuildOwnerLocalInstantSpecsJob : IJob");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;");
            writer.WriteLine("public BufferLookup<GEEffectSpecBuffer> SpecLookup;");
            writer.WriteLine("public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;");
            writer.WriteLine("[ReadOnly] public EntityStorageInfoLookup EntityStorageInfoLookup;");
            writer.WriteLine("[ReadOnly] public ComponentLookup<ASCDestroyingComponent> DestroyingLookup;");
            writer.WriteLine("[ReadOnly] public ComponentLookup<TagMaskComponent> TagMaskLookup;");
            writer.WriteLine("[ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;");
            writer.WriteLine("public Entity StreamEntity;");
            writer.WriteLine("public NativeList<OwnerLocalInstantSpecCommandRecord> Records;");
            writer.WriteLine("[ReadOnly] public NativeList<GESetByCallerValueBuffer> Payloads;");
            writer.WriteLine("");
            writer.WriteLine("public void Execute()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (!Catalog.IsCreated");
            writer.Indent++;
            writer.WriteLine("|| StreamEntity == Entity.Null");
            writer.WriteLine("|| !StreamLookup.HasComponent(StreamEntity))");
            writer.Indent--;
            writer.Indent++;
            writer.WriteLine("return;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("if (Records.Length == 0)");
            writer.Indent++;
            writer.WriteLine("return;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("Records.Sort(new OwnerLocalInstantSpecCommandRecordComparer());");
            writer.WriteLine("var stream = StreamLookup[StreamEntity];");
            writer.WriteLine("ref var catalog = ref Catalog.Value;");
            writer.WriteLine("var builtCount = 0;");
            writer.WriteLine("for (var i = 0; i < Records.Length; i++)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var record = Records[i];");
            writer.WriteLine("var command = record.Command;");
            writer.WriteLine("if (!CanBuildInstantSpec(ref catalog, in command, EntityStorageInfoLookup, DestroyingLookup, TagMaskLookup, out var gameplayEffectIndex))");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("if (!SpecLookup.HasBuffer(record.Owner)");
            writer.Indent++;
            writer.WriteLine("|| !SetByCallerLookup.HasBuffer(record.Owner))");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("var specs = SpecLookup[record.Owner];");
            writer.WriteLine("var setByCallerValues = SetByCallerLookup[record.Owner];");
            writer.WriteLine("var specSequence = Allocate(ref stream.NextSpecSequence);");
            writer.WriteLine("var setByCallerStart = setByCallerValues.Length;");
            writer.WriteLine("var setByCallerCount = CopySetByCallerValues(");
            writer.Indent++;
            writer.WriteLine("Payloads,");
            writer.WriteLine("record.PayloadStart,");
            writer.WriteLine("record.PayloadCount,");
            writer.WriteLine("command.Sequence,");
            writer.WriteLine("specSequence,");
            writer.WriteLine("setByCallerValues);");
            writer.Indent--;
            writer.WriteLine("specs.Add(new GEEffectSpecBuffer");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("Sequence = specSequence,");
            writer.WriteLine("SourceCommandSequence = command.Sequence,");
            writer.WriteLine("Frame = command.Frame,");
            writer.WriteLine("SourceAsc = command.SourceAsc,");
            writer.WriteLine("TargetAsc = command.TargetAsc,");
            writer.WriteLine("SourceAbility = command.SourceAbility,");
            writer.WriteLine("SourceEffect = command.SourceEffect,");
            writer.WriteLine("Instigator = command.Instigator,");
            writer.WriteLine("Causer = command.Causer,");
            writer.WriteLine("GameplayEffectCode = command.GameplayEffectCode,");
            writer.WriteLine("CueRequestOnApplyCode = GetCueRequestOnApply(ref catalog, gameplayEffectIndex),");
            writer.WriteLine("Level = command.Level,");
            writer.WriteLine("StackCount = 1,");
            writer.WriteLine("DurationFrameOverride = command.DurationFrameOverride,");
            writer.WriteLine("ContextId = command.ContextId,");
            writer.WriteLine("ParentContextId = command.ParentContextId,");
            writer.WriteLine("TargetDataKind = command.TargetDataKind,");
            writer.WriteLine("SetByCallerStart = setByCallerCount > 0 ? setByCallerStart : 0,");
            writer.WriteLine("SetByCallerCount = setByCallerCount,");
            writer.WriteLine("Flags = gameplayEffectIndex,");
            writer.Indent--;
            writer.WriteLine("});");
            writer.WriteLine("builtCount++;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("stream.OwnerLocalSpecCount += builtCount;");
            writer.WriteLine("StreamLookup[StreamEntity] = stream;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static bool CanBuildInstantSpec(");
            writer.Indent++;
            writer.WriteLine("ref GASDefinitionCatalogBlob catalog,");
            writer.WriteLine("in GEEffectCommandBuffer command,");
            writer.WriteLine("EntityStorageInfoLookup entityStorageInfoLookup,");
            writer.WriteLine("ComponentLookup<ASCDestroyingComponent> destroyingLookup,");
            writer.WriteLine("ComponentLookup<TagMaskComponent> tagMaskLookup,");
            writer.WriteLine("out int gameplayEffectIndex)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("gameplayEffectIndex = -1;");
            writer.WriteLine("if (command.GameplayEffectCode <= 0");
            writer.Indent++;
            writer.WriteLine("|| command.TargetAsc == Entity.Null");
            writer.WriteLine("|| IsUnavailableAsc(entityStorageInfoLookup, destroyingLookup, command.SourceAsc)");
            writer.WriteLine("|| IsUnavailableAsc(entityStorageInfoLookup, destroyingLookup, command.TargetAsc)");
            writer.WriteLine("|| !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, command.GameplayEffectCode, out gameplayEffectIndex))");
            writer.Indent--;
            writer.Indent++;
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);");
            writer.WriteLine("if (gameplayEffect.DurationFrames > 0");
            writer.Indent++;
            writer.WriteLine("|| gameplayEffect.PeriodFrames > 0");
            writer.WriteLine("|| gameplayEffect.StackLimitCount > 0");
            writer.WriteLine("|| gameplayEffect.GrantedTagMaskIndex >= 0");
            writer.WriteLine("|| !gameplayEffect.RemoveGameplayEffectTagQuery.IsEmpty");
            writer.WriteLine("|| gameplayEffect.GrantedAbilityCount > 0");
            writer.WriteLine("|| (gameplayEffect.ModifierCount <= 0");
            writer.WriteLine("    && gameplayEffect.GameplayCueCode <= 0))");
            writer.Indent--;
            writer.Indent++;
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("if (gameplayEffect.RequirementCount > 0)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var targetTags = tagMaskLookup.HasComponent(command.TargetAsc)");
            writer.Indent++;
            writer.WriteLine("? tagMaskLookup[command.TargetAsc]");
            writer.WriteLine(": default;");
            writer.Indent--;
            writer.WriteLine("if (!GASGeneratedRequirementEvaluator.EvaluateGameplayEffectRequirements(ref catalog, in gameplayEffect, in targetTags, out _))");
            writer.Indent++;
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("return true;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static int GetCueRequestOnApply(ref GASDefinitionCatalogBlob catalog, int gameplayEffectIndex)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if ((uint)gameplayEffectIndex >= (uint)catalog.GameplayEffects.Length)");
            writer.Indent++;
            writer.WriteLine("return 0;");
            writer.Indent--;
            writer.WriteLine("return catalog.GameplayEffects[gameplayEffectIndex].GameplayCueCode;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private struct OwnerLocalInstantSpecCommandRecordComparer : IComparer<OwnerLocalInstantSpecCommandRecord>");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public int Compare(OwnerLocalInstantSpecCommandRecord x, OwnerLocalInstantSpecCommandRecord y)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var result = x.Command.Sequence.CompareTo(y.Command.Sequence);");
            writer.WriteLine("if (result != 0)");
            writer.Indent++;
            writer.WriteLine("return result;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("result = CompareEntity(x.Owner, y.Owner);");
            writer.WriteLine("if (result != 0)");
            writer.Indent++;
            writer.WriteLine("return result;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("return x.LocalIndex.CompareTo(y.LocalIndex);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static int CopySetByCallerValues(");
            writer.Indent++;
            writer.WriteLine("DynamicBuffer<GESetByCallerValueBuffer> source,");
            writer.WriteLine("in GEEffectCommandBuffer command,");
            writer.WriteLine("NativeList<GESetByCallerValueBuffer> target)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (!source.IsCreated || command.SetByCallerCount <= 0)");
            writer.Indent++;
            writer.WriteLine("return 0;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var start = command.SetByCallerStart < 0 ? 0 : command.SetByCallerStart;");
            writer.WriteLine("if (start >= source.Length)");
            writer.Indent++;
            writer.WriteLine("return 0;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var count = command.SetByCallerCount;");
            writer.WriteLine("if (start + count > source.Length)");
            writer.Indent++;
            writer.WriteLine("count = source.Length - start;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var copied = 0;");
            writer.WriteLine("for (var i = 0; i < count; i++)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var value = source[start + i];");
            writer.WriteLine("if (value.CommandSequence != command.Sequence)");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("value.CommandSequence = command.Sequence;");
            writer.WriteLine("value.SpecSequence = 0;");
            writer.WriteLine("target.Add(value);");
            writer.WriteLine("copied++;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("return copied;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static int CopySetByCallerValues(");
            writer.Indent++;
            writer.WriteLine("NativeList<GESetByCallerValueBuffer> source,");
            writer.WriteLine("int start,");
            writer.WriteLine("int count,");
            writer.WriteLine("int commandSequence,");
            writer.WriteLine("int specSequence,");
            writer.WriteLine("DynamicBuffer<GESetByCallerValueBuffer> target)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (!source.IsCreated || count <= 0)");
            writer.Indent++;
            writer.WriteLine("return 0;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("if (start < 0)");
            writer.Indent++;
            writer.WriteLine("start = 0;");
            writer.Indent--;
            writer.WriteLine("if (start >= source.Length)");
            writer.Indent++;
            writer.WriteLine("return 0;");
            writer.Indent--;
            writer.WriteLine("if (start + count > source.Length)");
            writer.Indent++;
            writer.WriteLine("count = source.Length - start;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var copied = 0;");
            writer.WriteLine("for (var i = 0; i < count; i++)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var value = source[start + i];");
            writer.WriteLine("if (value.CommandSequence != commandSequence)");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("value.CommandSequence = commandSequence;");
            writer.WriteLine("value.SpecSequence = specSequence;");
            writer.WriteLine("target.Add(value);");
            writer.WriteLine("copied++;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("return copied;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static int CompareEntity(Entity left, Entity right)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var result = left.Index.CompareTo(right.Index);");
            writer.WriteLine("return result != 0 ? result : left.Version.CompareTo(right.Version);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            WriteGeneratedEffectSharedHelpers(writer);
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteGeneratedAttributeDeltaApplySystem(IndentedWriter writer)
        {
            writer.WriteLine("[UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]");
            writer.WriteLine("[UpdateAfter(typeof(GEExecutionCalculationOutputModifierSystem))]");
            writer.WriteLine("[UpdateAfter(typeof(GEEffectSpecBuildSystem))]");
            writer.WriteLine("[UpdateBefore(typeof(GASAttributeModifierDeltaApplySystem))]");
            writer.WriteLine("[UpdateBefore(typeof(GameplayFactProjectionSystem))]");
            writer.WriteLine("public partial struct GASAttributeSetReduceApplySystem : ISystem");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("private EntityQuery _ownerSpecQuery;");
            writer.WriteLine("");
            writer.WriteLine("public void OnCreate(ref SystemState state)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("_ownerSpecQuery = state.GetEntityQuery(new EntityQueryDesc");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("All = new[]");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("ComponentType.ReadOnly<ASCIdentityComponent>(),");
            writer.WriteLine("ComponentType.ReadOnly<ASCDestroyingComponent>(),");
            writer.WriteLine("ComponentType.ReadOnly<GEEffectSpecBuffer>(),");
            writer.WriteLine("ComponentType.ReadOnly<GESetByCallerValueBuffer>(),");
            writer.WriteLine("ComponentType.ReadWrite<AttributeValueBuffer>(),");
            writer.WriteLine("ComponentType.ReadWrite<OwnerLocalGameplayFactBuffer>(),");
            writer.Indent--;
            writer.WriteLine("},");
            writer.WriteLine("Options = EntityQueryOptions.IgnoreComponentEnabledState,");
            writer.Indent--;
            writer.WriteLine("});");
            writer.WriteLine("state.RequireForUpdate(_ownerSpecQuery);");
            writer.WriteLine("state.RequireForUpdate<GEEffectCommandStreamComponent>();");
            writer.WriteLine("state.RequireForUpdate<GASDefinitionCatalogComponent>();");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public void OnUpdate(ref SystemState state)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();");
            writer.WriteLine("if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))");
            writer.Indent++;
            writer.WriteLine("return;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();");
            writer.WriteLine("state.Dependency = new AttributeSetReduceApplyJob");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("EntityType = SystemAPI.GetEntityTypeHandle(),");
            writer.WriteLine("DestroyingType = SystemAPI.GetComponentTypeHandle<ASCDestroyingComponent>(isReadOnly: true),");
            writer.WriteLine("SpecType = SystemAPI.GetBufferTypeHandle<GEEffectSpecBuffer>(isReadOnly: true),");
            writer.WriteLine("SetByCallerType = SystemAPI.GetBufferTypeHandle<GESetByCallerValueBuffer>(isReadOnly: true),");
            writer.WriteLine("AttributeType = SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(),");
            writer.WriteLine("OwnerFactType = SystemAPI.GetBufferTypeHandle<OwnerLocalGameplayFactBuffer>(),");
            writer.WriteLine("StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),");
            writer.WriteLine("Catalog = catalogComponent.Catalog,");
            writer.WriteLine("StreamEntity = streamEntity,");
            writer.Indent--;
            writer.WriteLine("}.Schedule(_ownerSpecQuery, state.Dependency);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("[BurstCompile]");
            writer.WriteLine("private struct AttributeSetReduceApplyJob : IJobChunk");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("[ReadOnly] public EntityTypeHandle EntityType;");
            writer.WriteLine("[ReadOnly] public ComponentTypeHandle<ASCDestroyingComponent> DestroyingType;");
            writer.WriteLine("[ReadOnly] public BufferTypeHandle<GEEffectSpecBuffer> SpecType;");
            writer.WriteLine("[ReadOnly] public BufferTypeHandle<GESetByCallerValueBuffer> SetByCallerType;");
            writer.WriteLine("public BufferTypeHandle<AttributeValueBuffer> AttributeType;");
            writer.WriteLine("public BufferTypeHandle<OwnerLocalGameplayFactBuffer> OwnerFactType;");
            writer.WriteLine("public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;");
            writer.WriteLine("[ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;");
            writer.WriteLine("public Entity StreamEntity;");
            writer.WriteLine("");
            writer.WriteLine("public void Execute(");
            writer.Indent++;
            writer.WriteLine("in ArchetypeChunk chunk,");
            writer.WriteLine("int unfilteredChunkIndex,");
            writer.WriteLine("bool useEnabledMask,");
            writer.WriteLine("in v128 chunkEnabledMask)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (!Catalog.IsCreated");
            writer.Indent++;
            writer.WriteLine("|| StreamEntity == Entity.Null");
            writer.WriteLine("|| !StreamLookup.HasComponent(StreamEntity))");
            writer.Indent--;
            writer.Indent++;
            writer.WriteLine("return;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var stream = StreamLookup[StreamEntity];");
            writer.WriteLine("var owners = chunk.GetNativeArray(EntityType);");
            writer.WriteLine("var destroyingMask = chunk.GetEnabledMask(ref DestroyingType);");
            writer.WriteLine("var specBuffers = chunk.GetBufferAccessor(ref SpecType);");
            writer.WriteLine("var setByCallerBuffers = chunk.GetBufferAccessor(ref SetByCallerType);");
            writer.WriteLine("var attributeBuffers = chunk.GetBufferAccessor(ref AttributeType);");
            writer.WriteLine("var ownerFactBuffers = chunk.GetBufferAccessor(ref OwnerFactType);");
            writer.WriteLine("ref var catalog = ref Catalog.Value;");
            writer.WriteLine("var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);");
            writer.WriteLine("while (enumerator.NextEntityIndex(out var entityIndex))");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (destroyingMask[entityIndex])");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var owner = owners[entityIndex];");
            writer.WriteLine("var specs = specBuffers[entityIndex];");
            writer.WriteLine("if (specs.Length == 0)");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var setByCallerValues = setByCallerBuffers[entityIndex];");
            writer.WriteLine("var attributes = attributeBuffers[entityIndex];");
            writer.WriteLine("var facts = ownerFactBuffers[entityIndex];");
            writer.WriteLine("for (var i = 0; i < specs.Length; i++)");
            writer.Indent++;
            writer.WriteLine("ApplySpec(ref stream, ref catalog, owner, specs[i], setByCallerValues, attributes, facts);");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("StreamLookup[StreamEntity] = stream;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static void ApplySpec(");
            writer.Indent++;
            writer.WriteLine("ref GEEffectCommandStreamComponent stream,");
            writer.WriteLine("ref GASDefinitionCatalogBlob catalog,");
            writer.WriteLine("Entity owner,");
            writer.WriteLine("in GEEffectSpecBuffer spec,");
            writer.WriteLine("DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,");
            writer.WriteLine("DynamicBuffer<AttributeValueBuffer> attributes,");
            writer.WriteLine("DynamicBuffer<OwnerLocalGameplayFactBuffer> facts)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (spec.TargetAsc == Entity.Null");
            writer.Indent++;
            writer.WriteLine("|| CompareEntity(spec.TargetAsc, owner) != 0");
            writer.WriteLine("|| !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, spec.GameplayEffectCode, out var gameplayEffectIndex))");
            writer.Indent--;
            writer.Indent++;
            writer.WriteLine("return;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);");
            writer.WriteLine("for (var i = 0; i < gameplayEffect.ModifierCount; i++)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var modifierIndex = gameplayEffect.ModifierStart + i;");
            writer.WriteLine("if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var modifier = catalog.Modifiers[modifierIndex];");
            writer.WriteLine("var context = BuildMagnitudeContext(in spec, setByCallerValues, in modifier);");
            writer.WriteLine("if (!GASGeneratedMagnitudeEvaluator.TryResolveMagnitude(in modifier, in context, out var magnitude))");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var attrIndex = attributes.IndexOfAttribute(modifier.AttributeSetCode, modifier.AttributeCode);");
            writer.WriteLine("if (attrIndex < 0)");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var attribute = attributes[attrIndex];");
            writer.WriteLine("var oldValue = attribute.BaseValue;");
            writer.WriteLine("var oldCurrentValue = attribute.CurrentValue;");
            writer.WriteLine("var newValue = AttributeHelper.ApplyModifier(attribute.BaseValue, modifier.Operation, magnitude);");
            writer.WriteLine("attribute.CurrentValue = newValue;");
            writer.WriteLine("AttributeHelper.Clamp(ref attribute);");
            writer.WriteLine("newValue = attribute.CurrentValue;");
            writer.WriteLine("attribute.BaseValue = newValue;");
            writer.WriteLine("attribute.CurrentValue = newValue;");
            writer.WriteLine("if (newValue != oldValue)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("attribute.Dirty = true;");
            writer.WriteLine("if (oldCurrentValue != attribute.CurrentValue)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("attribute.PreviousCurrentValue = oldCurrentValue;");
            writer.WriteLine("attribute.CurrentValueChangePending = true;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("var deltaSequence = Allocate(ref stream.NextDeltaSequence);");
            writer.WriteLine("facts.Add(new OwnerLocalGameplayFactBuffer");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("Fact = new GameplayEventBuffer");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("Sequence = Allocate(ref stream.NextFactSequence),");
            writer.WriteLine("SourceCommandSequence = spec.SourceCommandSequence,");
            writer.WriteLine("SourceSpecSequence = spec.Sequence,");
            writer.WriteLine("SourceDeltaSequence = deltaSequence,");
            writer.WriteLine("Frame = spec.Frame,");
            writer.WriteLine("EventType = EGameplayEventType.AttributeBaseValueChanged,");
            writer.WriteLine("Domain = EGameplayFactDomain.Attribute,");
            writer.WriteLine("Category = EGameplayFactCategory.StateChange,");
            writer.WriteLine("Severity = EGameplayFactSeverity.Info,");
            writer.WriteLine("SourceAsc = spec.SourceAsc,");
            writer.WriteLine("TargetAsc = spec.TargetAsc,");
            writer.WriteLine("SourceAbility = spec.SourceAbility,");
            writer.WriteLine("SourceEffect = spec.SourceEffect,");
            writer.WriteLine("GameplayEffectCode = spec.GameplayEffectCode,");
            writer.WriteLine("ContextId = spec.ContextId,");
            writer.WriteLine("ParentContextId = spec.ParentContextId,");
            writer.WriteLine("AttrSetCode = modifier.AttributeSetCode,");
            writer.WriteLine("AttributeCode = modifier.AttributeCode,");
            writer.WriteLine("Value = magnitude,");
            writer.WriteLine("OldValue = oldValue,");
            writer.WriteLine("NewValue = newValue,");
            writer.Indent--;
            writer.WriteLine("},");
            writer.Indent--;
            writer.WriteLine("});");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("attributes[attrIndex] = attribute;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static MagnitudeEvalContext BuildMagnitudeContext(");
            writer.Indent++;
            writer.WriteLine("in GEEffectSpecBuffer spec,");
            writer.WriteLine("DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,");
            writer.WriteLine("in GASCatalogModifierDefinitionBlob modifier)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var context = new MagnitudeEvalContext");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("SourceAsc = spec.SourceAsc,");
            writer.WriteLine("TargetAsc = spec.TargetAsc,");
            writer.WriteLine("GameplayEffectCode = spec.GameplayEffectCode,");
            writer.WriteLine("Level = spec.Level,");
            writer.WriteLine("StackCount = spec.StackCount <= 0 ? 1 : spec.StackCount,");
            writer.WriteLine("SetByCallerKey = modifier.MagnitudeKey,");
            writer.Indent--;
            writer.WriteLine("};");
            writer.WriteLine("");
            writer.WriteLine("if (modifier.MagnitudeSource == EMagnitudeSource.SetByCaller");
            writer.Indent++;
            writer.WriteLine("&& TryFindSetByCallerValue(in spec, setByCallerValues, modifier.MagnitudeKey, out var setByCallerValue))");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("context.HasSetByCallerValue = 1;");
            writer.WriteLine("context.SetByCallerValue = setByCallerValue;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("return context;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static bool TryFindSetByCallerValue(");
            writer.Indent++;
            writer.WriteLine("in GEEffectSpecBuffer spec,");
            writer.WriteLine("DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,");
            writer.WriteLine("int key,");
            writer.WriteLine("out float value)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var start = spec.SetByCallerStart < 0 ? 0 : spec.SetByCallerStart;");
            writer.WriteLine("var end = start + spec.SetByCallerCount;");
            writer.WriteLine("if (end > setByCallerValues.Length)");
            writer.Indent++;
            writer.WriteLine("end = setByCallerValues.Length;");
            writer.Indent--;
            writer.WriteLine("for (var i = start; i < end; i++)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var setByCaller = setByCallerValues[i];");
            writer.WriteLine("if (setByCaller.Key != key || setByCaller.SpecSequence != spec.Sequence)");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("value = setByCaller.Value;");
            writer.WriteLine("return true;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("value = 0f;");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            WriteGeneratedEffectSharedHelpers(writer);
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteGeneratedEffectSharedHelpers(IndentedWriter writer)
        {
            writer.WriteLine("private static int CompareEntity(Entity left, Entity right)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var result = left.Index.CompareTo(right.Index);");
            writer.WriteLine("return result != 0 ? result : left.Version.CompareTo(right.Version);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static int Allocate(ref int next)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (next <= 0)");
            writer.Indent++;
            writer.WriteLine("next = 1;");
            writer.Indent--;
            writer.WriteLine("return next++;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static int ClampCursor(int cursor, int length)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (cursor < 0) return 0;");
            writer.WriteLine("if (cursor > length) return length;");
            writer.WriteLine("return cursor;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static bool IsUnavailableAsc(");
            writer.Indent++;
            writer.WriteLine("EntityStorageInfoLookup entityStorageInfoLookup,");
            writer.WriteLine("ComponentLookup<ASCDestroyingComponent> destroyingLookup,");
            writer.WriteLine("Entity asc)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("return asc == Entity.Null");
            writer.Indent++;
            writer.WriteLine("|| !entityStorageInfoLookup.Exists(asc)");
            writer.WriteLine("|| IsDestroyingAsc(destroyingLookup, asc);");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static bool IsDestroyingAsc(");
            writer.Indent++;
            writer.WriteLine("ComponentLookup<ASCDestroyingComponent> destroyingLookup,");
            writer.WriteLine("Entity asc)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("return asc != Entity.Null");
            writer.Indent++;
            writer.WriteLine("&& destroyingLookup.HasComponent(asc)");
            writer.WriteLine("&& destroyingLookup.IsComponentEnabled(asc);");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static void AssignSpecSequence(");
            writer.Indent++;
            writer.WriteLine("DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,");
            writer.WriteLine("int start,");
            writer.WriteLine("int count,");
            writer.WriteLine("int commandSequence,");
            writer.WriteLine("int specSequence)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (count <= 0)");
            writer.Indent++;
            writer.WriteLine("return;");
            writer.Indent--;
            writer.WriteLine("if (start < 0) start = 0;");
            writer.WriteLine("var end = start + count;");
            writer.WriteLine("if (end > setByCallerValues.Length)");
            writer.Indent++;
            writer.WriteLine("end = setByCallerValues.Length;");
            writer.Indent--;
            writer.WriteLine("for (var i = start; i < end; i++)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var value = setByCallerValues[i];");
            writer.WriteLine("if (value.CommandSequence != commandSequence)");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("value.SpecSequence = specSequence;");
            writer.WriteLine("setByCallerValues[i] = value;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        internal static void WriteRuntimeActiveEffectSystems(GasCodeGenContext context, string path)
        {
            var template = RuntimeActiveEffectSystemsTemplate.Replace("__ROOT_NAMESPACE__", context.RootNamespace);
            var namespaceStart = template.IndexOf("namespace ", StringComparison.Ordinal);
            var namespaceBodyStart = namespaceStart >= 0
                ? template.IndexOf('{', namespaceStart)
                : -1;
            var runtimeHelperStart = template.IndexOf(RuntimeActiveEffectHelperMarker, StringComparison.Ordinal);
            if (namespaceBodyStart < 0 || runtimeHelperStart < 0)
                throw new InvalidOperationException("Runtime active effect generated helper marker is missing.");

            File.WriteAllText(
                path,
                template.Substring(0, namespaceBodyStart + 1)
                + Environment.NewLine
                + template.Substring(runtimeHelperStart));
        }

        private const string RuntimeActiveEffectHelperMarker = "    public static class GASGeneratedActiveEffectRuntime";

        private const string RuntimeActiveEffectSystemsTemplate = @"///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using System;
using GAS.Runtime;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace __ROOT_NAMESPACE__
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASActiveEffectRemoveSystem))]
    [UpdateBefore(typeof(GEEffectSpecBuildSystem))]
    public partial struct GEEffectCommandCatalogNormalizeSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<GEEffectCommandBuffer>(),
                    ComponentType.ReadOnly<GESetByCallerValueBuffer>(),
                },
            });
            state.RequireForUpdate(_query);
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            state.Dependency = new GEEffectCommandCatalogNormalizeJob
            {
                CommandTypeHandle = SystemAPI.GetBufferTypeHandle<GEEffectCommandBuffer>(),
                SetByCallerTypeHandle = SystemAPI.GetBufferTypeHandle<GESetByCallerValueBuffer>(isReadOnly: true),
                ActiveMutationCommandLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationCommandBuffer>(isReadOnly: false),
                ActiveMutationSetByCallerLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationSetByCallerValueBuffer>(isReadOnly: false),
                Catalog = catalogComponent.Catalog,
            }.Schedule(_query, state.Dependency);
        }

        private struct GEEffectCommandCatalogNormalizeJob : IJobChunk
        {
            public BufferTypeHandle<GEEffectCommandBuffer> CommandTypeHandle;
            [ReadOnly] public BufferTypeHandle<GESetByCallerValueBuffer> SetByCallerTypeHandle;
            public BufferLookup<ActiveEffectMutationCommandBuffer> ActiveMutationCommandLookup;
            public BufferLookup<ActiveEffectMutationSetByCallerValueBuffer> ActiveMutationSetByCallerLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Catalog.IsCreated)
                    return;

                ref var catalog = ref Catalog.Value;
                var commandBuffers = chunk.GetBufferAccessor(ref CommandTypeHandle);
                var setByCallerBuffers = chunk.GetBufferAccessor(ref SetByCallerTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var bufferIndex))
                {
                    var commands = commandBuffers[bufferIndex];
                    var setByCallerValues = setByCallerBuffers[bufferIndex];
                    for (var i = 0; i < commands.Length; i++)
                    {
                        var command = commands[i];
                        if (GASGeneratedActiveEffectRuntime.TryNormalizeCommand(ref catalog, ref command))
                            commands[i] = command;
                        AppendActiveMutationCommand(in command, setByCallerValues);
                    }
                }
            }

            private void AppendActiveMutationCommand(
                in GEEffectCommandBuffer command,
                DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues)
            {
                if (command.Kind != GEEffectCommandKind.ActiveMutation
                    || command.TargetAsc == Entity.Null
                    || !ActiveMutationCommandLookup.HasBuffer(command.TargetAsc)
                    || !ActiveMutationSetByCallerLookup.HasBuffer(command.TargetAsc))
                {
                    return;
                }

                var ownerSetByCallerValues = ActiveMutationSetByCallerLookup[command.TargetAsc];
                var ownerCommand = CopySetByCallerValuesToOwner(in command, setByCallerValues, ownerSetByCallerValues);
                ActiveMutationCommandLookup[command.TargetAsc].Add(new ActiveEffectMutationCommandBuffer
                {
                    Command = ownerCommand,
                });
            }

            private static GEEffectCommandBuffer CopySetByCallerValuesToOwner(
                in GEEffectCommandBuffer command,
                DynamicBuffer<GESetByCallerValueBuffer> source,
                DynamicBuffer<ActiveEffectMutationSetByCallerValueBuffer> target)
            {
                if (command.SetByCallerCount <= 0)
                {
                    var emptyCommand = command;
                    emptyCommand.SetByCallerStart = 0;
                    emptyCommand.SetByCallerCount = 0;
                    return emptyCommand;
                }

                var ownerCommand = command;
                var ownerStart = target.Length;
                var copied = 0;
                var start = command.SetByCallerStart < 0 ? 0 : command.SetByCallerStart;
                var end = start + command.SetByCallerCount;
                if (end > source.Length)
                    end = source.Length;

                for (var i = start; i < end; i++)
                {
                    var value = source[i];
                    if (value.CommandSequence != command.Sequence)
                        continue;

                    target.Add(new ActiveEffectMutationSetByCallerValueBuffer
                    {
                        Value = new GESetByCallerValueBuffer
                        {
                            CommandSequence = command.Sequence,
                            SpecSequence = value.SpecSequence,
                            Key = value.Key,
                            Value = value.Value,
                        },
                    });
                    copied++;
                }

                ownerCommand.SetByCallerStart = copied > 0 ? ownerStart : 0;
                ownerCommand.SetByCallerCount = copied;
                return ownerCommand;
            }
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEEffectSpecBuildSystem))]
    [UpdateBefore(typeof(GASAttributeSetReduceApplySystem))]
    [UpdateBefore(typeof(GEExecutionCalculationSystem))]
    public partial struct GASActiveEffectMutationApplySystem : ISystem
    {
        private EntityQuery _ownerQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<ASCDestroyingComponent>(),
                    ComponentType.ReadWrite<ASCActiveEffectsComponent>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectBuffer>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectSetByCallerValueBuffer>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectCleanupRecordBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectMutationCommandBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectMutationSetByCallerValueBuffer>(),
                    ComponentType.ReadWrite<OwnerLocalInstantNextFrameCommandBuffer>(),
                    ComponentType.ReadWrite<OwnerLocalInstantNextFrameSetByCallerValueBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectNextFrameMutationCommandBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectNextFrameMutationSetByCallerValueBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectMutationBuffer>(),
                    ComponentType.ReadWrite<TagMaskComponent>(),
                    ComponentType.ReadOnly<TagFixedMaskComponent>(),
                    ComponentType.ReadWrite<AttributeValueBuffer>(),
                    ComponentType.ReadWrite<AttributeActiveModifierBuffer>(),
                    ComponentType.ReadWrite<TagTemporarySourceBuffer>(),
                    ComponentType.ReadWrite<AbilitySlotBuffer>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
            });
            state.RequireForUpdate(_ownerQuery);
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var grantedAbilityArchetype = GASRuntimeEntityArchetypes.GrantedAbility(em);
            var commandCapacity = _ownerQuery.CalculateEntityCountWithoutFiltering() * 4;
            if (commandCapacity < 64)
                commandCapacity = 64;

            var activeMutationCommands = new NativeList<GEEffectCommandBuffer>(commandCapacity, state.WorldUpdateAllocator);
            var activeMutationSetByCallerValues =
                new NativeList<GESetByCallerValueBuffer>(commandCapacity * 2, state.WorldUpdateAllocator);
            var activeMutationOwnerRanges =
                new NativeParallelHashMap<Entity, GASGeneratedActiveEffectRuntime.ActiveMutationCommandRange>(
                    commandCapacity,
                    state.WorldUpdateAllocator);
            var activeMutationSourceAttributeSnapshotCapacity = commandCapacity * 16;
            if (activeMutationSourceAttributeSnapshotCapacity < 256)
                activeMutationSourceAttributeSnapshotCapacity = 256;
            var activeMutationSourceAttributeSnapshots =
                new NativeParallelHashMap<long, float>(
                    activeMutationSourceAttributeSnapshotCapacity,
                    state.WorldUpdateAllocator);

            var collectJob = new GASGeneratedActiveEffectRuntime.GEActiveEffectMutationOwnerCommandCollectJob
            {
                CommandBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<ActiveEffectMutationCommandBuffer>(isReadOnly: true),
                SetByCallerBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<ActiveEffectMutationSetByCallerValueBuffer>(isReadOnly: true),
                ActiveMutationCommands = activeMutationCommands,
                ActiveMutationSetByCallerValues = activeMutationSetByCallerValues,
            };
            var collectDependency = collectJob.Schedule(_ownerQuery, state.Dependency);

            var gatherJob = new GASGeneratedActiveEffectRuntime.GEActiveEffectMutationOwnerCommandFinalizeJob
            {
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: true),
                Catalog = catalogComponent.Catalog,
                ActiveMutationCommands = activeMutationCommands,
                ActiveMutationOwnerRanges = activeMutationOwnerRanges,
                ActiveMutationSourceAttributeSnapshots = activeMutationSourceAttributeSnapshots,
                StreamEntity = streamEntity,
            };
            var gatherDependency = gatherJob.Schedule(collectDependency);

            state.Dependency = new GASGeneratedActiveEffectRuntime.GEActiveEffectMutationChunkApplyJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                DestroyingTypeHandle = SystemAPI.GetComponentTypeHandle<ASCDestroyingComponent>(isReadOnly: true),
                ActiveEffectsTypeHandle =
                    SystemAPI.GetComponentTypeHandle<ASCActiveEffectsComponent>(isReadOnly: false),
                ActiveEffectSlotBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(isReadOnly: false),
                SetByCallerSnapshotBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectSetByCallerValueBuffer>(isReadOnly: false),
                CleanupRecordBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectCleanupRecordBuffer>(isReadOnly: false),
                MutationBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveEffectMutationBuffer>(isReadOnly: false),
                TagMaskTypeHandle = SystemAPI.GetComponentTypeHandle<TagMaskComponent>(isReadOnly: false),
                TagFixedMaskTypeHandle = SystemAPI.GetComponentTypeHandle<TagFixedMaskComponent>(isReadOnly: true),
                AttributeBufferTypeHandle = SystemAPI.GetBufferTypeHandle<AttributeValueBuffer>(isReadOnly: false),
                ActiveModifierBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<AttributeActiveModifierBuffer>(isReadOnly: false),
                TagSourceBufferTypeHandle = SystemAPI.GetBufferTypeHandle<TagTemporarySourceBuffer>(isReadOnly: false),
                AbilitySlotBufferTypeHandle = SystemAPI.GetBufferTypeHandle<AbilitySlotBuffer>(isReadOnly: false),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                NextFrameInstantCommandLookup =
                    SystemAPI.GetBufferLookup<OwnerLocalInstantNextFrameCommandBuffer>(isReadOnly: false),
                NextFrameInstantSetByCallerLookup =
                    SystemAPI.GetBufferLookup<OwnerLocalInstantNextFrameSetByCallerValueBuffer>(isReadOnly: false),
                NextFrameActiveMutationCommandLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectNextFrameMutationCommandBuffer>(isReadOnly: false),
                NextFrameActiveMutationSetByCallerLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectNextFrameMutationSetByCallerValueBuffer>(isReadOnly: false),
                AttributeOwnerMarkerRequestLookup =
                    SystemAPI.GetBufferLookup<AttributeOwnerMarkerRequestBuffer>(isReadOnly: false),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: false),
                AbilityGrantedLookup = SystemAPI.GetComponentLookup<AbilityGrantedByEffectComponent>(isReadOnly: true),
                AbilityLifecycleRequestLookup =
                    SystemAPI.GetBufferLookup<AbilityLifecycleRequestBuffer>(isReadOnly: false),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),
                StructuralEcb = structuralEcb,
                GrantedAbilityArchetype = grantedAbilityArchetype,
                Catalog = catalogComponent.Catalog,
                ActiveMutationCommands = activeMutationCommands,
                ActiveMutationOwnerRanges = activeMutationOwnerRanges,
                ActiveMutationSourceAttributeSnapshots = activeMutationSourceAttributeSnapshots,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
            }.Schedule(_ownerQuery, gatherDependency);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup), OrderFirst = true)]
    public partial struct GASActiveEffectPreTickSystem : ISystem
    {
        private EntityQuery _ownerQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<ASCActiveEffectsComponent>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectMutationBuffer>(),
                },
            });
            state.RequireForUpdate(_ownerQuery);
            state.RequireForUpdate<GlobalTimer>();
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || !EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var grantedAbilityArchetype = GASRuntimeEntityArchetypes.GrantedAbility(em);
            var ownerChunkCount = _ownerQuery.CalculateChunkCountWithoutFiltering();
            if (ownerChunkCount <= 0)
                return;

            var ownerCapacity = _ownerQuery.CalculateEntityCount();
            ref var catalog = ref catalogComponent.Catalog.Value;
            var activeEffectSlotSourceAttributeSnapshotCapacity =
                GASGeneratedActiveEffectRuntime.EstimateActiveEffectSlotSourceAttributeSnapshotCapacity(
                    ref catalog,
                    ownerCapacity);
            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            EffectCommandSpecStream.SetActiveEffectSlotSourceSnapshotCapacity(
                ref stream,
                activeEffectSlotSourceAttributeSnapshotCapacity);
            em.SetComponentData(streamEntity, stream);

            var activeEffectSlotSourceAttributeSnapshots =
                new NativeParallelHashMap<GASGeneratedActiveEffectRuntime.ActiveEffectSlotSourceAttributeSnapshotKey, float>(
                    activeEffectSlotSourceAttributeSnapshotCapacity,
                    state.WorldUpdateAllocator);
            var activeEffectSlotSourceSnapshotLaneCounters =
                CollectionHelper.CreateNativeArray<GASGeneratedActiveEffectRuntime.ActiveEffectSlotSourceSnapshotLaneCounters>(
                    ownerChunkCount,
                    state.WorldUpdateAllocator,
                    NativeArrayOptions.ClearMemory);
            var snapshotGatherJob = new GASGeneratedActiveEffectRuntime.GEActiveEffectPreTickSourceAttributeSnapshotGatherJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                ActiveEffectSlotBufferTypeHandle =
                    SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(isReadOnly: true),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: true),
                Catalog = catalogComponent.Catalog,
                ActiveEffectSlotSourceAttributeSnapshots = activeEffectSlotSourceAttributeSnapshots.AsParallelWriter(),
                SnapshotLaneCounters = activeEffectSlotSourceSnapshotLaneCounters,
                Frame = frame,
            };
            var snapshotDependency = snapshotGatherJob.ScheduleParallel(_ownerQuery, state.Dependency);
            var tickJob = new GASGeneratedActiveEffectRuntime.GEActiveEffectPreTickJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                ActiveEffectsTypeHandle = SystemAPI.GetComponentTypeHandle<ASCActiveEffectsComponent>(isReadOnly: false),
                ActiveEffectSlotBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(isReadOnly: false),
                MutationBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveEffectMutationBuffer>(isReadOnly: false),
                CleanupRecordLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectCleanupRecordBuffer>(isReadOnly: false),
                SetByCallerSnapshotLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectSetByCallerValueBuffer>(isReadOnly: false),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: false),
                ActiveModifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(isReadOnly: false),
                AttributeOwnerMarkerRequestLookup =
                    SystemAPI.GetBufferLookup<AttributeOwnerMarkerRequestBuffer>(isReadOnly: false),
                TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: false),
                TagFixedMaskLookup = SystemAPI.GetComponentLookup<TagFixedMaskComponent>(isReadOnly: true),
                TagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(isReadOnly: false),
                AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(isReadOnly: false),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: false),
                AbilityGrantedLookup = SystemAPI.GetComponentLookup<AbilityGrantedByEffectComponent>(isReadOnly: true),
                RemoveCommandBufferTypeHandle = SystemAPI.GetBufferTypeHandle<GERemoveCommandBuffer>(isReadOnly: false),
                RemovePendingTypeHandle =
                    SystemAPI.GetComponentTypeHandle<GERemoveCommandPendingComponent>(isReadOnly: false),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),
                CommandSetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),
                ActiveMutationCommandLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationCommandBuffer>(isReadOnly: false),
                ActiveMutationSetByCallerLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationSetByCallerValueBuffer>(isReadOnly: false),
                AbilityLifecycleRequestLookup =
                    SystemAPI.GetBufferLookup<AbilityLifecycleRequestBuffer>(isReadOnly: false),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),
                StructuralEcb = structuralEcb,
                GrantedAbilityArchetype = grantedAbilityArchetype,
                Catalog = catalogComponent.Catalog,
                ActiveEffectSlotSourceAttributeSnapshots = activeEffectSlotSourceAttributeSnapshots,
                SnapshotLaneCounters = activeEffectSlotSourceSnapshotLaneCounters,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
                ProcessTickRecords = true,
            };
            state.Dependency = tickJob.Schedule(_ownerQuery, snapshotDependency);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GASActiveEffectPreTickSystem))]
    public partial struct GASActiveEffectRemoveSystem : ISystem
    {
        private EntityQuery _removeCommandQuery;

        public void OnCreate(ref SystemState state)
        {
            _removeCommandQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<GERemoveCommandPendingComponent>(),
                    ComponentType.ReadWrite<GERemoveCommandBuffer>(),
                    ComponentType.ReadWrite<ASCActiveEffectsComponent>(),
                    ComponentType.ReadWrite<ActiveGameplayEffectBuffer>(),
                    ComponentType.ReadWrite<ActiveEffectMutationBuffer>(),
                },
            });
            state.RequireForUpdate(_removeCommandQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || !EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var emptyActiveEffectSlotSourceAttributeSnapshots =
                new NativeParallelHashMap<GASGeneratedActiveEffectRuntime.ActiveEffectSlotSourceAttributeSnapshotKey, float>(
                    1,
                    state.WorldUpdateAllocator);
            var removeChunkCount = _removeCommandQuery.CalculateChunkCountWithoutFiltering();
            if (removeChunkCount <= 0)
                return;

            var activeEffectSlotSourceSnapshotLaneCounters =
                CollectionHelper.CreateNativeArray<GASGeneratedActiveEffectRuntime.ActiveEffectSlotSourceSnapshotLaneCounters>(
                    removeChunkCount,
                    state.WorldUpdateAllocator,
                    NativeArrayOptions.ClearMemory);

            state.Dependency = new GASGeneratedActiveEffectRuntime.GEActiveEffectPreTickJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                ActiveEffectsTypeHandle = SystemAPI.GetComponentTypeHandle<ASCActiveEffectsComponent>(isReadOnly: false),
                ActiveEffectSlotBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(isReadOnly: false),
                MutationBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveEffectMutationBuffer>(isReadOnly: false),
                RemoveCommandBufferTypeHandle = SystemAPI.GetBufferTypeHandle<GERemoveCommandBuffer>(isReadOnly: false),
                CleanupRecordLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectCleanupRecordBuffer>(isReadOnly: false),
                SetByCallerSnapshotLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectSetByCallerValueBuffer>(isReadOnly: false),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: false),
                ActiveModifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(isReadOnly: false),
                AttributeOwnerMarkerRequestLookup =
                    SystemAPI.GetBufferLookup<AttributeOwnerMarkerRequestBuffer>(isReadOnly: false),
                TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: false),
                TagFixedMaskLookup = SystemAPI.GetComponentLookup<TagFixedMaskComponent>(isReadOnly: true),
                TagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(isReadOnly: false),
                AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(isReadOnly: false),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: false),
                AbilityGrantedLookup = SystemAPI.GetComponentLookup<AbilityGrantedByEffectComponent>(isReadOnly: true),
                RemovePendingTypeHandle =
                    SystemAPI.GetComponentTypeHandle<GERemoveCommandPendingComponent>(isReadOnly: false),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),
                CommandSetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),
                AbilityLifecycleRequestLookup =
                    SystemAPI.GetBufferLookup<AbilityLifecycleRequestBuffer>(isReadOnly: false),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),
                StructuralEcb = structuralEcb,
                ActiveEffectSlotSourceAttributeSnapshots = emptyActiveEffectSlotSourceAttributeSnapshots,
                SnapshotLaneCounters = activeEffectSlotSourceSnapshotLaneCounters,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
                ProcessExplicitRemoveCommands = true,
            }.Schedule(_removeCommandQuery, state.Dependency);
        }
    }

    public static class GASGeneratedActiveEffectRuntime
    {
        public struct ActiveMutationCommandRange
        {
            public int Start;
            public int End;

            public ActiveMutationCommandRange(int start, int end)
            {
                Start = start;
                End = end;
            }
        }

        private static long MakeActiveMutationSourceAttributeSnapshotKey(int commandSequence, int modifierIndex)
        {
            return ((long)commandSequence << 32) ^ (uint)modifierIndex;
        }

        public readonly struct ActiveEffectSlotSourceAttributeSnapshotKey : IEquatable<ActiveEffectSlotSourceAttributeSnapshotKey>
        {
            public readonly Entity Owner;
            public readonly int SlotSequence;
            public readonly int ModifierIndex;

            public ActiveEffectSlotSourceAttributeSnapshotKey(Entity owner, int slotSequence, int modifierIndex)
            {
                Owner = owner;
                SlotSequence = slotSequence;
                ModifierIndex = modifierIndex;
            }

            public bool Equals(ActiveEffectSlotSourceAttributeSnapshotKey other)
            {
                return Owner.Equals(other.Owner)
                       && SlotSequence == other.SlotSequence
                       && ModifierIndex == other.ModifierIndex;
            }

            public override bool Equals(object obj)
            {
                return obj is ActiveEffectSlotSourceAttributeSnapshotKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = Owner.GetHashCode();
                    hash = (hash * 397) ^ SlotSequence;
                    hash = (hash * 397) ^ ModifierIndex;
                    return hash;
                }
            }
        }

        public static int EstimateActiveEffectSlotSourceAttributeSnapshotCapacity(
            ref GASDefinitionCatalogBlob catalog,
            int ownerCapacity)
        {
            var maxSourceAttributeModifierCount = 1;
            for (var gameplayEffectIndex = 0; gameplayEffectIndex < catalog.GameplayEffects.Length; gameplayEffectIndex++)
            {
                ref readonly var gameplayEffect = ref catalog.GameplayEffects[gameplayEffectIndex];
                var sourceAttributeModifierCount = 0;
                for (var i = 0; i < gameplayEffect.ModifierCount; i++)
                {
                    var modifierIndex = gameplayEffect.ModifierStart + i;
                    if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                        continue;

                    if (catalog.Modifiers[modifierIndex].MagnitudeSource == EMagnitudeSource.SourceAttribute)
                        sourceAttributeModifierCount++;
                }

                if (sourceAttributeModifierCount > maxSourceAttributeModifierCount)
                    maxSourceAttributeModifierCount = sourceAttributeModifierCount;
            }

            var capacity = (long)ownerCapacity * ActiveEffectStore.InlineSlotCapacity * maxSourceAttributeModifierCount;
            if (capacity < 256)
                return 256;
            return capacity > int.MaxValue ? int.MaxValue : (int)capacity;
        }

        private static ActiveEffectSlotSourceAttributeSnapshotKey MakeActiveEffectSlotSourceAttributeSnapshotKey(
            Entity owner,
            int slotSequence,
            int modifierIndex)
        {
            return new ActiveEffectSlotSourceAttributeSnapshotKey(owner, slotSequence, modifierIndex);
        }

        public struct ActiveEffectSlotSourceSnapshotLaneCounters
        {
            public int GatherAttemptCount;
            public int SnapshotWriteCount;
            public int SnapshotWriteFailureCount;
            public int AttributeMissCount;
            public int ApplyHitCount;
            public int ApplyMissCount;
            public int FallbackValueCount;
            public int CapacityPressureCount;
            public int SpillCount;

            public bool HasEvidence =>
                GatherAttemptCount > 0
                || SnapshotWriteCount > 0
                || SnapshotWriteFailureCount > 0
                || AttributeMissCount > 0
                || ApplyHitCount > 0
                || ApplyMissCount > 0
                || FallbackValueCount > 0
                || CapacityPressureCount > 0
                || SpillCount > 0;

            public void RecordSnapshotWrite(bool success)
            {
                GatherAttemptCount++;
                if (success)
                {
                    SnapshotWriteCount++;
                    return;
                }

                SnapshotWriteFailureCount++;
                CapacityPressureCount++;
                SpillCount++;
            }

            public void AddToStream(ref GEEffectCommandStreamComponent stream)
            {
                if (!HasEvidence)
                    return;

                EffectCommandSpecStream.AddActiveEffectSlotSourceSnapshotCounters(
                    ref stream,
                    GatherAttemptCount,
                    SnapshotWriteCount,
                    SnapshotWriteFailureCount,
                    AttributeMissCount,
                    ApplyHitCount,
                    ApplyMissCount,
                    FallbackValueCount,
                    CapacityPressureCount,
                    SpillCount);
            }
        }

        private struct ActiveEffectMagnitudeSourceCounters
        {
            public int CurrentValueLookupCount;
            public int CapturedValueHitCount;
            public int CaptureMissCount;
            public int FallbackValueCount;
            public int SourceAttributeLookupCount;
            public int TargetAttributeLookupCount;

            public bool HasEvidence =>
                CurrentValueLookupCount > 0
                || CapturedValueHitCount > 0
                || CaptureMissCount > 0
                || FallbackValueCount > 0
                || SourceAttributeLookupCount > 0
                || TargetAttributeLookupCount > 0;

            public void AddToStream(ref GEEffectCommandStreamComponent stream)
            {
                if (!HasEvidence)
                    return;

                EffectCommandSpecStream.AddMagnitudeSourceCounters(
                    ref stream,
                    CurrentValueLookupCount,
                    CapturedValueHitCount,
                    CaptureMissCount,
                    captureMissLiveLookups: 0,
                    FallbackValueCount,
                    fallbackFacts: 0,
                    SourceAttributeLookupCount,
                    TargetAttributeLookupCount,
                    executionInputLookups: 0);
            }
        }

        [BurstCompile]
        public struct GEActiveEffectMutationOwnerCommandCollectJob : IJobChunk
        {
            [ReadOnly] public BufferTypeHandle<ActiveEffectMutationCommandBuffer> CommandBufferTypeHandle;
            [ReadOnly] public BufferTypeHandle<ActiveEffectMutationSetByCallerValueBuffer> SetByCallerBufferTypeHandle;
            public NativeList<GEEffectCommandBuffer> ActiveMutationCommands;
            public NativeList<GESetByCallerValueBuffer> ActiveMutationSetByCallerValues;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var commandBuffers = chunk.GetBufferAccessor(ref CommandBufferTypeHandle);
                var setByCallerBuffers = chunk.GetBufferAccessor(ref SetByCallerBufferTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var commands = commandBuffers[entityIndex];
                    var setByCallerValues = setByCallerBuffers[entityIndex];
                    for (var i = 0; i < commands.Length; i++)
                    {
                        var command = commands[i].Command;
                        if (command.Kind != GEEffectCommandKind.ActiveMutation)
                            continue;

                        ActiveMutationCommands.Add(CopySetByCallerValues(in command, setByCallerValues));
                    }
                }
            }

            private GEEffectCommandBuffer CopySetByCallerValues(
                in GEEffectCommandBuffer command,
                DynamicBuffer<ActiveEffectMutationSetByCallerValueBuffer> source)
            {
                if (command.SetByCallerCount <= 0)
                {
                    var emptyCommand = command;
                    emptyCommand.SetByCallerStart = 0;
                    emptyCommand.SetByCallerCount = 0;
                    return emptyCommand;
                }

                var collectedCommand = command;
                var collectedStart = ActiveMutationSetByCallerValues.Length;
                var copied = 0;
                var start = command.SetByCallerStart < 0 ? 0 : command.SetByCallerStart;
                var end = start + command.SetByCallerCount;
                if (end > source.Length)
                    end = source.Length;

                for (var i = start; i < end; i++)
                {
                    var value = source[i].Value;
                    if (value.CommandSequence != command.Sequence)
                        continue;

                    ActiveMutationSetByCallerValues.Add(new GESetByCallerValueBuffer
                    {
                        CommandSequence = command.Sequence,
                        SpecSequence = value.SpecSequence,
                        Key = value.Key,
                        Value = value.Value,
                    });
                    copied++;
                }

                collectedCommand.SetByCallerStart = copied > 0 ? collectedStart : 0;
                collectedCommand.SetByCallerCount = copied;
                return collectedCommand;
            }
        }

        [BurstCompile]
        public struct GEActiveEffectMutationOwnerCommandFinalizeJob : IJob
        {
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            [ReadOnly] public BufferLookup<AttributeValueBuffer> AttributeLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public NativeList<GEEffectCommandBuffer> ActiveMutationCommands;
            public NativeParallelHashMap<Entity, ActiveMutationCommandRange> ActiveMutationOwnerRanges;
            public NativeParallelHashMap<long, float> ActiveMutationSourceAttributeSnapshots;
            public Entity StreamEntity;

            public void Execute()
            {
                ActiveMutationOwnerRanges.Clear();
                ActiveMutationSourceAttributeSnapshots.Clear();
                if (ActiveMutationCommands.Length == 0)
                    return;

                var sortMoveCount = SortActiveMutationCommandsByOwner();
                var ownerGroupCount = BuildActiveMutationOwnerRanges(out var maxOwnerRange);
                BuildActiveMutationSourceAttributeSnapshots();

                if (StreamEntity != Entity.Null && StreamLookup.HasComponent(StreamEntity))
                {
                    var stream = StreamLookup[StreamEntity];
                    WriteActiveMutationStats(ref stream, sortMoveCount, ownerGroupCount, maxOwnerRange);
                    StreamLookup[StreamEntity] = stream;
                }
            }

            private void BuildActiveMutationSourceAttributeSnapshots()
            {
                if (!Catalog.IsCreated || ActiveMutationCommands.Length == 0)
                    return;

                ref var catalog = ref Catalog.Value;
                for (var commandIndex = 0; commandIndex < ActiveMutationCommands.Length; commandIndex++)
                {
                    var command = ActiveMutationCommands[commandIndex];
                    if (command.SourceAsc == Entity.Null
                        || CompareEntity(command.SourceAsc, command.TargetAsc) == 0
                        || !AttributeLookup.HasBuffer(command.SourceAsc)
                        || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, command.GameplayEffectCode, out var gameplayEffectIndex))
                    {
                        continue;
                    }

                    ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                    var durationFrame = ResolveDurationFrame(in command, in gameplayEffect);
                    if (!HasPersistentRuntimeState(in gameplayEffect, durationFrame) || gameplayEffect.ModifierCount <= 0)
                        continue;

                    var sourceAttributes = AttributeLookup[command.SourceAsc];
                    for (var i = 0; i < gameplayEffect.ModifierCount; i++)
                    {
                        var modifierIndex = gameplayEffect.ModifierStart + i;
                        if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                            continue;

                        var modifier = catalog.Modifiers[modifierIndex];
                        if (modifier.MagnitudeSource != EMagnitudeSource.SourceAttribute
                            || !TryReadSnapshotAttributeValue(sourceAttributes, in modifier, out var sourceValue))
                        {
                            continue;
                        }

                        var snapshotKey = MakeActiveMutationSourceAttributeSnapshotKey(command.Sequence, modifierIndex);
                        ActiveMutationSourceAttributeSnapshots.TryAdd(snapshotKey, sourceValue);
                    }
                }
            }

            private static bool TryReadSnapshotAttributeValue(
                DynamicBuffer<AttributeValueBuffer> attributes,
                in GASCatalogModifierDefinitionBlob modifier,
                out float value)
            {
                value = 0f;
                var attrSetCode = modifier.CaptureAttributeSetCode != 0
                    ? modifier.CaptureAttributeSetCode
                    : modifier.AttributeSetCode;
                var attrCode = modifier.CaptureAttributeCode != 0
                    ? modifier.CaptureAttributeCode
                    : modifier.AttributeCode;
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
                if (attrIndex < 0)
                    return false;

                value = attributes[attrIndex].CurrentValue;
                return true;
            }

            private int BuildActiveMutationOwnerRanges(out int maxOwnerRange)
            {
                ActiveMutationOwnerRanges.Clear();
                maxOwnerRange = 0;
                if (ActiveMutationCommands.Length == 0)
                    return 0;

                var ownerGroupCount = 0;
                var rangeStart = 0;
                while (rangeStart < ActiveMutationCommands.Length)
                {
                    var owner = ActiveMutationCommands[rangeStart].TargetAsc;
                    var rangeEnd = rangeStart + 1;
                    while (rangeEnd < ActiveMutationCommands.Length
                           && CompareEntity(owner, ActiveMutationCommands[rangeEnd].TargetAsc) == 0)
                    {
                        rangeEnd++;
                    }

                    var rangeLength = rangeEnd - rangeStart;
                    if (rangeLength > maxOwnerRange)
                        maxOwnerRange = rangeLength;

                    ActiveMutationOwnerRanges.Add(owner, new ActiveMutationCommandRange(rangeStart, rangeEnd));
                    ownerGroupCount++;
                    rangeStart = rangeEnd;
                }

                return ownerGroupCount;
            }

            private int SortActiveMutationCommandsByOwner()
            {
                var moveCount = 0;
                for (var i = 1; i < ActiveMutationCommands.Length; i++)
                {
                    var value = ActiveMutationCommands[i];
                    var j = i - 1;
                    while (j >= 0 && CompareActiveMutationCommand(ActiveMutationCommands[j], value) > 0)
                    {
                        ActiveMutationCommands[j + 1] = ActiveMutationCommands[j];
                        moveCount++;
                        j--;
                    }

                    ActiveMutationCommands[j + 1] = value;
                }

                return moveCount;
            }

            private void WriteActiveMutationStats(
                ref GEEffectCommandStreamComponent stream,
                int sortMoveCount,
                int ownerGroupCount,
                int maxOwnerRange)
            {
                var commandCount = ActiveMutationCommands.Length;
                if (commandCount == 0)
                    return;

                stream.ActiveMutationCommandCount += commandCount;
                stream.ActiveMutationOwnerGroupCount += ownerGroupCount;
                if (maxOwnerRange > stream.ActiveMutationMaxOwnerRange)
                    stream.ActiveMutationMaxOwnerRange = maxOwnerRange;
                stream.ActiveMutationSortMoveCount += sortMoveCount;
            }

            private static int CompareActiveMutationCommand(
                in GEEffectCommandBuffer left,
                in GEEffectCommandBuffer right)
            {
                var result = CompareEntity(left.TargetAsc, right.TargetAsc);
                if (result != 0)
                    return result;

                result = left.Sequence.CompareTo(right.Sequence);
                if (result != 0)
                    return result;

                return left.ParentContextId.CompareTo(right.ParentContextId);
            }

            private static int CompareEntity(Entity left, Entity right)
            {
                var result = left.Index.CompareTo(right.Index);
                if (result != 0)
                    return result;

                return left.Version.CompareTo(right.Version);
            }
        }

        [BurstCompile]
        public struct GEActiveEffectMutationChunkApplyJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<ASCDestroyingComponent> DestroyingTypeHandle;
            public ComponentTypeHandle<ASCActiveEffectsComponent> ActiveEffectsTypeHandle;
            public BufferTypeHandle<ActiveGameplayEffectBuffer> ActiveEffectSlotBufferTypeHandle;
            public BufferTypeHandle<ActiveGameplayEffectSetByCallerValueBuffer> SetByCallerSnapshotBufferTypeHandle;
            public BufferTypeHandle<ActiveGameplayEffectCleanupRecordBuffer> CleanupRecordBufferTypeHandle;
            public BufferTypeHandle<ActiveEffectMutationBuffer> MutationBufferTypeHandle;
            public ComponentTypeHandle<TagMaskComponent> TagMaskTypeHandle;
            [ReadOnly] public ComponentTypeHandle<TagFixedMaskComponent> TagFixedMaskTypeHandle;
            public BufferTypeHandle<AttributeValueBuffer> AttributeBufferTypeHandle;
            public BufferTypeHandle<AttributeActiveModifierBuffer> ActiveModifierBufferTypeHandle;
            public BufferTypeHandle<TagTemporarySourceBuffer> TagSourceBufferTypeHandle;
            public BufferTypeHandle<AbilitySlotBuffer> AbilitySlotBufferTypeHandle;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<OwnerLocalInstantNextFrameCommandBuffer> NextFrameInstantCommandLookup;
            public BufferLookup<OwnerLocalInstantNextFrameSetByCallerValueBuffer> NextFrameInstantSetByCallerLookup;
            public BufferLookup<ActiveEffectNextFrameMutationCommandBuffer> NextFrameActiveMutationCommandLookup;
            public BufferLookup<ActiveEffectNextFrameMutationSetByCallerValueBuffer> NextFrameActiveMutationSetByCallerLookup;
            public BufferLookup<AttributeOwnerMarkerRequestBuffer> AttributeOwnerMarkerRequestLookup;
            public ComponentLookup<AbilityStateComponent> AbilityStateLookup;
            [ReadOnly] public ComponentLookup<AbilityGrantedByEffectComponent> AbilityGrantedLookup;
            public BufferLookup<AbilityLifecycleRequestBuffer> AbilityLifecycleRequestLookup;
            public BufferLookup<GameplayEventBuffer> FactLookup;
            public EntityCommandBuffer StructuralEcb;
            public EntityArchetype GrantedAbilityArchetype;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            [ReadOnly] public NativeList<GEEffectCommandBuffer> ActiveMutationCommands;
            [ReadOnly] public NativeList<GESetByCallerValueBuffer> ActiveMutationSetByCallerValues;
            [ReadOnly] public NativeParallelHashMap<Entity, ActiveMutationCommandRange> ActiveMutationOwnerRanges;
            [ReadOnly] public NativeParallelHashMap<long, float> ActiveMutationSourceAttributeSnapshots;
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public int Frame;

            private struct ActiveMutationOwnerResources
            {
                public Entity Owner;
                public ASCActiveEffectsComponent Store;
                public DynamicBuffer<ActiveGameplayEffectBuffer> Slots;
                public DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> SetByCallerSnapshot;
                public TagMaskComponent TargetTags;
                public bool TargetTagsDirty;
                public bool HasFixedTags;
                public TagFixedMaskComponent FixedTags;
                public bool HasAttributes;
                public DynamicBuffer<AttributeValueBuffer> Attributes;
                public bool HasActiveModifiers;
                public DynamicBuffer<AttributeActiveModifierBuffer> ActiveModifiers;
                public bool HasTagSources;
                public DynamicBuffer<TagTemporarySourceBuffer> TagSources;
                public bool HasAbilitySlots;
                public DynamicBuffer<AbilitySlotBuffer> AbilitySlots;
                public bool HasCleanupRecords;
                public DynamicBuffer<ActiveGameplayEffectCleanupRecordBuffer> CleanupRecords;
                public DynamicBuffer<ActiveEffectMutationBuffer> Mutations;
            }

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Catalog.IsCreated
                    || ActiveMutationCommands.Length == 0
                    || StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity))
                {
                    return;
                }

                var entities = chunk.GetNativeArray(EntityTypeHandle);
                var destroyingMask = chunk.GetEnabledMask(ref DestroyingTypeHandle);
                var stores = chunk.GetNativeArray(ref ActiveEffectsTypeHandle);
                var tagMasks = chunk.GetNativeArray(ref TagMaskTypeHandle);
                var fixedTagMasks = chunk.GetNativeArray(ref TagFixedMaskTypeHandle);
                var slots = chunk.GetBufferAccessor(ref ActiveEffectSlotBufferTypeHandle);
                var setByCallerSnapshots = chunk.GetBufferAccessor(ref SetByCallerSnapshotBufferTypeHandle);
                var cleanupRecords = chunk.GetBufferAccessor(ref CleanupRecordBufferTypeHandle);
                var mutations = chunk.GetBufferAccessor(ref MutationBufferTypeHandle);
                var attributes = chunk.GetBufferAccessor(ref AttributeBufferTypeHandle);
                var activeModifiers = chunk.GetBufferAccessor(ref ActiveModifierBufferTypeHandle);
                var tagSources = chunk.GetBufferAccessor(ref TagSourceBufferTypeHandle);
                var abilitySlots = chunk.GetBufferAccessor(ref AbilitySlotBufferTypeHandle);
                var stream = StreamLookup[StreamEntity];
                ref var catalog = ref Catalog.Value;
                var magnitudeSourceCounters = default(ActiveEffectMagnitudeSourceCounters);
                var enumerator = new ChunkEntityEnumerator(false, default, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    if (destroyingMask[entityIndex])
                        continue;

                    var owner = entities[entityIndex];
                    if (!ActiveMutationOwnerRanges.TryGetValue(owner, out var range))
                        continue;

                    var ownerMutations = mutations[entityIndex];
                    ownerMutations.Clear();
                    var ownerResources = new ActiveMutationOwnerResources
                    {
                        Owner = owner,
                        Store = stores[entityIndex],
                        Slots = slots[entityIndex],
                        SetByCallerSnapshot = setByCallerSnapshots[entityIndex],
                        TargetTags = tagMasks[entityIndex],
                        HasFixedTags = true,
                        FixedTags = fixedTagMasks[entityIndex],
                        HasAttributes = true,
                        Attributes = attributes[entityIndex],
                        HasActiveModifiers = true,
                        ActiveModifiers = activeModifiers[entityIndex],
                        HasTagSources = true,
                        TagSources = tagSources[entityIndex],
                        HasAbilitySlots = true,
                        AbilitySlots = abilitySlots[entityIndex],
                        HasCleanupRecords = true,
                        CleanupRecords = cleanupRecords[entityIndex],
                        Mutations = ownerMutations,
                    };

                    for (var commandIndex = range.Start; commandIndex < range.End; commandIndex++)
                    {
                        var command = ActiveMutationCommands[commandIndex];
                        TryApplyActiveMutationToOwner(
                            ref stream,
                            ref catalog,
                            ref ownerResources,
                            in command,
                            ActiveMutationSetByCallerValues,
                            ref magnitudeSourceCounters);
                    }

                    stores[entityIndex] = ownerResources.Store;
                    if (ownerResources.TargetTagsDirty)
                        tagMasks[entityIndex] = ownerResources.TargetTags;
                }

                magnitudeSourceCounters.AddToStream(ref stream);
                StreamLookup[StreamEntity] = stream;
            }

            private static int CompareEntity(Entity left, Entity right)
            {
                var result = left.Index.CompareTo(right.Index);
                if (result != 0)
                    return result;

                return left.Version.CompareTo(right.Version);
            }

            private bool TryApplyActiveMutationToOwner(
                ref GEEffectCommandStreamComponent stream,
                ref GASDefinitionCatalogBlob catalog,
                ref ActiveMutationOwnerResources ownerResources,
                in GEEffectCommandBuffer command,
                NativeList<GESetByCallerValueBuffer> setByCallerValues,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters)
            {
                if (CompareEntity(ownerResources.Owner, command.TargetAsc) != 0
                    || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, command.GameplayEffectCode, out var gameplayEffectIndex))
                {
                    return false;
                }

                ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                if (!GASGeneratedRequirementEvaluator.EvaluateGameplayEffectRequirements(
                        ref catalog,
                        in gameplayEffect,
                        in ownerResources.TargetTags,
                        out _))
                    return false;

                RemoveGameplayEffectsWithTags(
                    ref catalog,
                    ref ownerResources,
                    in gameplayEffect);

                var durationFrame = ResolveDurationFrame(in command, in gameplayEffect);
                if (!HasPersistentRuntimeState(in gameplayEffect, durationFrame))
                {
                    ownerResources.Mutations.Add(new ActiveEffectMutationBuffer
                    {
                        Sequence = command.Sequence,
                        SourceCommandSequence = command.Sequence,
                        Frame = Frame,
                        Kind = ActiveEffectMutationKind.Apply,
                        ActiveEffect = Entity.Null,
                        SourceAsc = command.SourceAsc,
                        TargetAsc = command.TargetAsc,
                        SourceAbility = command.SourceAbility,
                        SourceEffect = command.SourceEffect,
                        GameplayEffectCode = command.GameplayEffectCode,
                        ContextId = command.ContextId,
                        ParentContextId = command.ParentContextId,
                        StackCount = 1,
                        DurationFrameOverride = durationFrame,
                        PeriodFrame = gameplayEffect.PeriodFrames,
                    });
                    EnqueueAppliedEvents(in command, in gameplayEffect, Entity.Null);
                    ActiveEffectStore.RefreshChunkSkipIndexCounters(ref ownerResources.Store, ownerResources.Slots, Frame);
                    return true;
                }

                var slotIndex = FindRefreshableSlot(ownerResources.Slots, in command);
                if (slotIndex < 0 && ownerResources.Slots.Length >= GASParameterSetting.ASC_MAX_GAMEPLAY_EFFECT_COUNT)
                    return false;

                var isNewSlot = slotIndex < 0;
                var previous = isNewSlot ? default : ownerResources.Slots[slotIndex];
                var previousStackCount = previous.StackCount <= 0 ? 1 : previous.StackCount;
                var isOverflow = !isNewSlot && IsStackOverflow(in gameplayEffect, previousStackCount);
                if (isOverflow)
                {
                    EmitOverflowCommand(
                        ref stream,
                        ref catalog,
                        in command,
                        in gameplayEffect);
                    EnqueueStackOverflowEvent(in command, in gameplayEffect);
                    if (gameplayEffect.ClearStackOnOverflow != 0)
                    {
                        RemoveSlotAt(ref ownerResources, slotIndex);
                        slotIndex = -1;
                        isNewSlot = true;
                        previous = default;
                        previousStackCount = 1;
                        EnqueueStackClearedByOverflowEvent(in command);
                    }

                    if (gameplayEffect.DenyOverflowApplication != 0)
                    {
                        EnqueueStackOverflowDeniedEvent(in command);
                        ActiveEffectStore.RefreshChunkSkipIndexCounters(ref ownerResources.Store, ownerResources.Slots, Frame);
                        return true;
                    }
                }

                var slot = isNewSlot
                    ? CreateNewSlot(ref ownerResources.Store, in command, in gameplayEffect, durationFrame, Frame)
                    : RefreshExistingSlot(
                        previous,
                        in command,
                        in gameplayEffect,
                        durationFrame,
                        Frame,
                        ShouldRefreshDuration(in gameplayEffect),
                        ShouldResetPeriod(in gameplayEffect));

                slot.StackCount = ResolveNextStackCount(in gameplayEffect, previousStackCount, isNewSlot);
                RemoveActiveModifiersForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                GASGeneratedActiveEffectRuntime.RemoveSetByCallerSnapshotForSlot(
                    ownerResources.SetByCallerSnapshot,
                    slot.Sequence,
                    slot.GameplayEffectCode);
                CopySetByCallerSnapshot(in command, setByCallerValues, ownerResources.SetByCallerSnapshot, slot.Sequence, slot.GameplayEffectCode);

                var activeModifierCount = ApplyActiveModifiers(
                    ref catalog,
                    ref ownerResources,
                    in gameplayEffect,
                    in command,
                    setByCallerValues,
                    slot.Sequence,
                    slot.StackCount,
                    ref magnitudeSourceCounters);
                slot.ActiveGrantedTagCount = ApplyGrantedTags(
                    ref catalog,
                    ref ownerResources,
                    in gameplayEffect,
                    slot.Sequence);
                slot.ActiveGrantedAbilityCount = ApplyGrantedAbilities(
                    ref catalog,
                    ref ownerResources,
                    in gameplayEffect,
                    slot.Sequence);
                slot.Flags = ResolveSlotFlags(
                    in gameplayEffect,
                    durationFrame,
                    activeModifierCount,
                    slot.ActiveGrantedTagCount,
                    slot.ActiveGrantedAbilityCount);

                if (slotIndex >= 0)
                    ownerResources.Slots[slotIndex] = slot;
                else
                    ownerResources.Slots.Add(slot);

                ActiveEffectStore.RefreshChunkSkipIndexCounters(ref ownerResources.Store, ownerResources.Slots, Frame);

                ownerResources.Mutations.Add(new ActiveEffectMutationBuffer
                {
                    Sequence = slot.Sequence,
                    SourceCommandSequence = command.Sequence,
                    Frame = Frame,
                    Kind = isNewSlot
                        ? ActiveEffectMutationKind.Apply
                        : slot.StackCount > previousStackCount
                            ? ActiveEffectMutationKind.Stack
                            : ActiveEffectMutationKind.Refresh,
                    ActiveEffect = Entity.Null,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    SourceEffect = command.SourceEffect,
                    GameplayEffectCode = command.GameplayEffectCode,
                    ContextId = command.ContextId,
                    ParentContextId = command.ParentContextId,
                    StackCount = slot.StackCount,
                    DurationFrameOverride = durationFrame,
                    PeriodFrame = slot.PeriodFrame,
                });

                EnqueueAppliedEvents(in command, in gameplayEffect, Entity.Null);
                return true;
            }

            private int ApplyActiveModifiers(
                ref GASDefinitionCatalogBlob catalog,
                ref ActiveMutationOwnerResources ownerResources,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
                in GEEffectCommandBuffer command,
                NativeList<GESetByCallerValueBuffer> setByCallerValues,
                int slotSequence,
                int stackCount,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters)
            {
                if (gameplayEffect.ModifierCount <= 0
                    || !ownerResources.HasAttributes
                    || !ownerResources.HasActiveModifiers)
                {
                    return 0;
                }

                var attributes = ownerResources.Attributes;
                var activeModifiers = ownerResources.ActiveModifiers;
                var added = 0;
                for (var i = 0; i < gameplayEffect.ModifierCount; i++)
                {
                    var modifierIndex = gameplayEffect.ModifierStart + i;
                    if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                        continue;

                    var modifier = catalog.Modifiers[modifierIndex];
                    var attrIndex = attributes.IndexOfAttribute(modifier.AttributeSetCode, modifier.AttributeCode);
                    if (attrIndex < 0)
                        continue;

                    var context = BuildMagnitudeContext(
                        ref ownerResources,
                        in command,
                        setByCallerValues,
                        in modifier,
                        modifierIndex,
                        stackCount,
                        ref magnitudeSourceCounters);
                    if (!GASGeneratedMagnitudeEvaluator.TryResolveMagnitude(in modifier, in context, out var magnitude))
                        continue;

                    if (HasActiveModifier(activeModifiers, slotSequence, command.GameplayEffectCode, modifier.AttributeSetCode, modifier.AttributeCode, modifier.Operation))
                        continue;

                    activeModifiers.Add(new AttributeActiveModifierBuffer
                    {
                        AttrSetCode = modifier.AttributeSetCode,
                        AttributeCode = modifier.AttributeCode,
                        SourceEntity = Entity.Null,
                        SourceSequence = slotSequence,
                        SourceGameplayEffectCode = command.GameplayEffectCode,
                        Magnitude = magnitude,
                        Op = modifier.Operation,
                    });
                    MarkActiveModifierAdded(ownerResources.Owner);
                    MarkCurrentValueDirty(attributes, ownerResources.Owner, modifier.AttributeSetCode, modifier.AttributeCode);
                    added++;
                }

                return added;
            }

            private MagnitudeEvalContext BuildMagnitudeContext(
                ref ActiveMutationOwnerResources ownerResources,
                in GEEffectCommandBuffer command,
                NativeList<GESetByCallerValueBuffer> setByCallerValues,
                in GASCatalogModifierDefinitionBlob modifier,
                int modifierIndex,
                int stackCount,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters)
            {
                var context = new MagnitudeEvalContext
                {
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    GameplayEffectCode = command.GameplayEffectCode,
                    Level = command.Level,
                    StackCount = stackCount <= 0 ? 1 : stackCount,
                    SetByCallerKey = modifier.MagnitudeKey,
                };

                if (modifier.MagnitudeSource == EMagnitudeSource.SetByCaller
                    && TryFindSetByCallerValue(in command, setByCallerValues, modifier.MagnitudeKey, out var setByCallerValue))
                {
                    context.HasSetByCallerValue = 1;
                    context.SetByCallerValue = setByCallerValue;
                }

                if (modifier.MagnitudeSource == EMagnitudeSource.SourceAttribute)
                {
                    magnitudeSourceCounters.SourceAttributeLookupCount++;
                    if (TryReadSourceAttributeValue(
                            ref ownerResources,
                            in command,
                            modifierIndex,
                            in modifier,
                            ref magnitudeSourceCounters,
                            out var sourceValue))
                    {
                        context.HasSourceAttributeValue = 1;
                        context.SourceAttributeValue = sourceValue;
                    }
                    else
                    {
                        magnitudeSourceCounters.FallbackValueCount++;
                    }
                }

                if (modifier.MagnitudeSource == EMagnitudeSource.TargetAttribute)
                {
                    magnitudeSourceCounters.TargetAttributeLookupCount++;
                    magnitudeSourceCounters.CurrentValueLookupCount++;
                    if (TryReadOwnerAttributeValue(ref ownerResources, in modifier, out var targetValue))
                    {
                        context.HasTargetAttributeValue = 1;
                        context.TargetAttributeValue = targetValue;
                    }
                    else
                    {
                        magnitudeSourceCounters.FallbackValueCount++;
                    }
                }

                return context;
            }

            private bool TryReadSourceAttributeValue(
                ref ActiveMutationOwnerResources ownerResources,
                in GEEffectCommandBuffer command,
                int modifierIndex,
                in GASCatalogModifierDefinitionBlob modifier,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                out float value)
            {
                if (CompareEntity(ownerResources.Owner, command.SourceAsc) == 0)
                {
                    magnitudeSourceCounters.CurrentValueLookupCount++;
                    return TryReadOwnerAttributeValue(ref ownerResources, in modifier, out value);
                }

                if (command.SourceAsc == Entity.Null || !ActiveMutationSourceAttributeSnapshots.IsCreated)
                {
                    magnitudeSourceCounters.CaptureMissCount++;
                    value = 0f;
                    return false;
                }

                var snapshotKey = MakeActiveMutationSourceAttributeSnapshotKey(command.Sequence, modifierIndex);
                if (ActiveMutationSourceAttributeSnapshots.TryGetValue(snapshotKey, out value))
                {
                    magnitudeSourceCounters.CapturedValueHitCount++;
                    return true;
                }

                magnitudeSourceCounters.CaptureMissCount++;
                return false;
            }

            private bool TryReadOwnerAttributeValue(
                ref ActiveMutationOwnerResources ownerResources,
                in GASCatalogModifierDefinitionBlob modifier,
                out float value)
            {
                value = 0f;
                if (!ownerResources.HasAttributes)
                    return false;

                var attrSetCode = modifier.CaptureAttributeSetCode != 0
                    ? modifier.CaptureAttributeSetCode
                    : modifier.AttributeSetCode;
                var attributeCode = modifier.CaptureAttributeCode != 0
                    ? modifier.CaptureAttributeCode
                    : modifier.AttributeCode;
                var attributes = ownerResources.Attributes;
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attributeCode);
                if (attrIndex < 0)
                    return false;

                value = attributes[attrIndex].CurrentValue;
                return true;
            }

            private int RemoveActiveModifiersForSlot(
                ref ActiveMutationOwnerResources ownerResources,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (!ownerResources.HasActiveModifiers)
                    return 0;

                var modifiers = ownerResources.ActiveModifiers;
                var hasAttributes = ownerResources.HasAttributes;
                var attributes = hasAttributes ? ownerResources.Attributes : default;
                var removed = 0;
                for (var i = modifiers.Length - 1; i >= 0; i--)
                {
                    var modifier = modifiers[i];
                    if (modifier.SourceSequence != slotSequence
                        || modifier.SourceGameplayEffectCode != gameplayEffectCode)
                    {
                        continue;
                    }

                    modifiers.RemoveAt(i);
                    if (hasAttributes)
                        MarkCurrentValueDirty(attributes, ownerResources.Owner, modifier.AttrSetCode, modifier.AttributeCode);
                    removed++;
                }

                RefreshActiveModifierPresence(ownerResources.Owner, modifiers);
                return removed;
            }

            private int ApplyGrantedTags(
                ref GASDefinitionCatalogBlob catalog,
                ref ActiveMutationOwnerResources ownerResources,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
                int slotSequence)
            {
                if (gameplayEffect.GrantedTagMaskIndex < 0
                    || gameplayEffect.GrantedTagMaskIndex >= catalog.TagMasks.Length
                    || !ownerResources.HasTagSources)
                {
                    return 0;
                }

                var grantedMask = catalog.TagMasks[gameplayEffect.GrantedTagMaskIndex].Mask;
                var ownerTags = ownerResources.TargetTags;
                var sources = ownerResources.TagSources;
                for (var tagIndex = 0; tagIndex < TagMaskComponent.Capacity; tagIndex++)
                {
                    if (!grantedMask.HasTag(tagIndex)
                        || HasTempTagSource(sources, tagIndex, slotSequence, gameplayEffect.GameplayEffectCode))
                    {
                        continue;
                    }

                    var wasActive = ownerTags.HasTag(tagIndex);
                    ownerTags.AddTag(tagIndex);
                    sources.Add(new TagTemporarySourceBuffer
                    {
                        TagIndex = tagIndex,
                        Source = Entity.Null,
                        SourceSequence = slotSequence,
                        SourceGameplayEffectCode = gameplayEffect.GameplayEffectCode,
                    });

                    if (!wasActive)
                    {
                        EnqueueTagChangedEvent(ownerResources.Owner, tagIndex, true);
                    }
                }

                ownerResources.TargetTags = ownerTags;
                ownerResources.TargetTagsDirty = true;
                return CountTempTagSourcesForSlot(sources, slotSequence, gameplayEffect.GameplayEffectCode);
            }

            private void RemoveGrantedTagsForSlot(
                ref ActiveMutationOwnerResources ownerResources,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (!ownerResources.HasTagSources)
                    return;

                var sources = ownerResources.TagSources;
                for (var i = sources.Length - 1; i >= 0; i--)
                {
                    var source = sources[i];
                    if (source.SourceSequence != slotSequence
                        || source.SourceGameplayEffectCode != gameplayEffectCode)
                    {
                        continue;
                    }

                    sources.RemoveAt(i);
                    var removedFromMask = RemoveTagIndexFromEffectiveMaskIfUnreferenced(ref ownerResources, source.TagIndex);
                    if (removedFromMask)
                    {
                        EnqueueTagChangedEvent(ownerResources.Owner, source.TagIndex, false);
                    }
                }
            }

            private bool RemoveTagIndexFromEffectiveMaskIfUnreferenced(ref ActiveMutationOwnerResources ownerResources, int tagIndex)
            {
                if (ownerResources.HasFixedTags
                    && ownerResources.FixedTags.Mask.HasTag(tagIndex))
                {
                    return false;
                }
                if (HasAnyTemporarySourceForTag(ownerResources.TagSources, tagIndex))
                    return false;

                var mask = ownerResources.TargetTags;
                if (!mask.HasTag(tagIndex))
                    return false;
                mask.RemoveTag(tagIndex);
                ownerResources.TargetTags = mask;
                ownerResources.TargetTagsDirty = true;
                return true;
            }

            private static bool HasAnyTemporarySourceForTag(
                DynamicBuffer<TagTemporarySourceBuffer> sources,
                int tagIndex)
            {
                for (var i = 0; i < sources.Length; i++)
                {
                    if (sources[i].TagIndex == tagIndex)
                        return true;
                }

                return false;
            }

            private int ApplyGrantedAbilities(
                ref GASDefinitionCatalogBlob catalog,
                ref ActiveMutationOwnerResources ownerResources,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
                int slotSequence)
            {
                if (gameplayEffect.GrantedAbilityCount <= 0
                    || !ownerResources.HasAbilitySlots)
                {
                    return 0;
                }

                var grantedAbilities = ownerResources.AbilitySlots;
                var added = 0;
                for (var i = 0; i < gameplayEffect.GrantedAbilityCount; i++)
                {
                    var grantedIndex = gameplayEffect.GrantedAbilityStart + i;
                    if ((uint)grantedIndex >= (uint)catalog.GrantedAbilities.Length)
                        continue;

                    var granted = catalog.GrantedAbilities[grantedIndex];
                    if (granted.AbilityCode <= 0
                        || HasGrantedAbilityForSlot(grantedAbilities, granted.AbilityCode, slotSequence, gameplayEffect.GameplayEffectCode))
                    {
                        continue;
                    }

                    var ability = StructuralEcb.CreateEntity(GrantedAbilityArchetype);
                    GASRuntimeEntityArchetypes.InitializeAbilityEntity(StructuralEcb, ability);
                    StructuralEcb.SetComponent(
                        ability,
                        AbilityStateComponent.Create(granted.AbilityCode, granted.Level, ownerResources.Owner));
                    StructuralEcb.SetComponent(ability, new AbilityMainTargetComponent { TargetAsc = Entity.Null });
                    StructuralEcb.SetComponent(ability, new AbilityGrantedByEffectComponent
                    {
                        SourceEffect = Entity.Null,
                        SourceSequence = slotSequence,
                        SourceGameplayEffectCode = gameplayEffect.GameplayEffectCode,
                        ActivationPolicy = (GrantedAbilityActivationPolicy)granted.ActivationPolicy,
                        DeactivationPolicy = GrantedAbilityDeactivationPolicy.SyncWithEffect,
                        RemovePolicy = granted.RemovePolicy == 0
                            ? GrantedAbilityRemovePolicy.SyncWithEffect
                            : (GrantedAbilityRemovePolicy)granted.RemovePolicy,
                    });

                    StructuralEcb.AppendToBuffer(ownerResources.Owner, new AbilitySlotBuffer { AbilityEntity = ability });
                    added++;

                    var activationPolicy = (GrantedAbilityActivationPolicy)granted.ActivationPolicy;
                    StructuralEcb.SetComponentEnabled<AbilityActivationPendingComponent>(
                        ability,
                        activationPolicy == GrantedAbilityActivationPolicy.WhenAdded
                        || activationPolicy == GrantedAbilityActivationPolicy.SyncWithEffect);
                }

                return CountGrantedAbilitiesForSlot(grantedAbilities, slotSequence, gameplayEffect.GameplayEffectCode) + added;
            }

            private void RemoveGrantedAbilitiesForSlot(
                ref ActiveMutationOwnerResources ownerResources,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (!ownerResources.HasAbilitySlots)
                    return;

                var grantedAbilities = ownerResources.AbilitySlots;
                for (var i = grantedAbilities.Length - 1; i >= 0; i--)
                {
                    var ability = grantedAbilities[i].AbilityEntity;
                    if (!IsGrantedBySlot(ability, slotSequence, gameplayEffectCode))
                        continue;

                    grantedAbilities.RemoveAt(i);
                    RemoveGrantedAbilityEntity(ability);
                }
            }

            private bool HasGrantedAbilityForSlot(
                DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
                int abilityCode,
                int slotSequence,
                int gameplayEffectCode)
            {
                for (var i = 0; i < grantedAbilities.Length; i++)
                {
                    var ability = grantedAbilities[i].AbilityEntity;
                    if (!IsGrantedBySlot(ability, slotSequence, gameplayEffectCode)
                        || !AbilityStateLookup.HasComponent(ability))
                    {
                        continue;
                    }

                    if (AbilityStateLookup[ability].Code == abilityCode)
                        return true;
                }

                return false;
            }

            private int CountGrantedAbilitiesForSlot(
                DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
                int slotSequence,
                int gameplayEffectCode)
            {
                var count = 0;
                for (var i = 0; i < grantedAbilities.Length; i++)
                {
                    if (IsGrantedBySlot(grantedAbilities[i].AbilityEntity, slotSequence, gameplayEffectCode))
                        count++;
                }

                return count;
            }

            private bool IsGrantedBySlot(
                Entity ability,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (ability == Entity.Null
                    || !AbilityGrantedLookup.HasComponent(ability))
                {
                    return false;
                }

                var granted = AbilityGrantedLookup[ability];
                return granted.SourceSequence == slotSequence
                    && granted.SourceGameplayEffectCode == gameplayEffectCode;
            }

            private void RemoveGrantedAbilityEntity(Entity ability)
            {
                if (ability == Entity.Null || !AbilityStateLookup.HasComponent(ability))
                    return;

                var runtime = AbilityStateLookup[ability];
                var isRunning = runtime.Phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending;
                if (isRunning)
                {
                    EnqueueAbilityLifecycleRequest(ability, runtime);
                    return;
                }

                StructuralEcb.DestroyEntity(ability);
            }

            private void EnqueueAbilityLifecycleRequest(Entity ability, in AbilityStateComponent runtime)
            {
                if (EventBusEntity == Entity.Null
                    || !AbilityLifecycleRequestLookup.HasBuffer(EventBusEntity))
                    return;

                var requests = AbilityLifecycleRequestLookup[EventBusEntity];
                requests.Add(new AbilityLifecycleRequestBuffer
                {
                    Sequence = requests.Length,
                    RequestKind = EAbilityLifecycleRequestKind.Cancel,
                    Reason = EAbilityLifecycleReason.GrantedEffectRemoved,
                    Ability = ability,
                    SourceAbility = Entity.Null,
                    SourceEffect = Entity.Null,
                    SourceAbilityCode = 0,
                    DestroyOnCleanup = 1,
                });
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.AbilityCancelRequested,
                    Domain = EGameplayFactDomain.Ability,
                    Category = EGameplayFactCategory.Request,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = runtime.Owner,
                    TargetAsc = runtime.Owner,
                    SourceAbility = ability,
                    EventCode = runtime.Code,
                    ReasonCode = (int)EAbilityLifecycleReason.GrantedEffectRemoved,
                });
            }

            private void RemoveGameplayEffectsWithTags(
                ref GASDefinitionCatalogBlob catalog,
                ref ActiveMutationOwnerResources ownerResources,
                in GASCatalogGameplayEffectDefinitionBlob appliedGameplayEffect)
            {
                var removeQuery = appliedGameplayEffect.RemoveGameplayEffectTagQuery;
                if (removeQuery.IsEmpty)
                    return;

                for (var i = ownerResources.Slots.Length - 1; i >= 0; i--)
                {
                    var slot = ownerResources.Slots[i];
                    if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var slotGameplayEffectIndex))
                        continue;

                    ref readonly var slotGameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, slotGameplayEffectIndex);
                    if (slotGameplayEffect.GrantedTagMaskIndex < 0
                        || slotGameplayEffect.GrantedTagMaskIndex >= catalog.TagMasks.Length)
                    {
                        continue;
                    }

                    var slotGrantedMask = catalog.TagMasks[slotGameplayEffect.GrantedTagMaskIndex].Mask;
                    if (removeQuery.Evaluate(slotGrantedMask))
                        RemoveSlotAt(ref ownerResources, i);
                }
            }

            private void RemoveSlotAt(
                ref ActiveMutationOwnerResources ownerResources,
                int slotIndex)
            {
                if ((uint)slotIndex >= (uint)ownerResources.Slots.Length)
                    return;

                var slot = ownerResources.Slots[slotIndex];
                var activeModifierCount = RemoveActiveModifiersForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                RemoveGrantedTagsForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                RemoveGrantedAbilitiesForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                GASGeneratedActiveEffectRuntime.RemoveSetByCallerSnapshotForSlot(
                    ownerResources.SetByCallerSnapshot,
                    slot.Sequence,
                    slot.GameplayEffectCode);
                RecordCleanup(ref ownerResources, in slot, activeModifierCount);
                ownerResources.Mutations.Add(new ActiveEffectMutationBuffer
                {
                    Sequence = slot.Sequence,
                    Frame = Frame,
                    Kind = ActiveEffectMutationKind.Remove,
                    ActiveEffect = Entity.Null,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = ownerResources.Owner,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    GameplayEffectCode = slot.GameplayEffectCode,
                    ContextId = slot.ContextId,
                    ParentContextId = slot.ParentContextId,
                    StackCount = slot.StackCount,
                    DurationFrameOverride = slot.DurationFrame,
                    PeriodFrame = slot.PeriodFrame,
                });
                EnqueueRemovedEvent(in slot);
                ownerResources.Slots.RemoveAt(slotIndex);
            }

            private void RecordCleanup(
                ref ActiveMutationOwnerResources ownerResources,
                in ActiveGameplayEffectBuffer slot,
                int activeModifierCount)
            {
                if (!ownerResources.HasCleanupRecords)
                    return;

                var records = ownerResources.CleanupRecords;
                while (records.Length >= ActiveEffectStore.MaxCleanupRecordCount)
                    records.RemoveAt(0);

                var cleanupFlags = ActiveEffectCleanupWorkFlags.OwnerLocalSlot;
                if (activeModifierCount > 0)
                    cleanupFlags |= ActiveEffectCleanupWorkFlags.RuntimeModifiers;
                if (slot.ActiveGrantedTagCount > 0)
                    cleanupFlags |= ActiveEffectCleanupWorkFlags.GrantedTags;
                if (slot.ActiveGrantedAbilityCount > 0)
                    cleanupFlags |= ActiveEffectCleanupWorkFlags.GrantedAbilities;

                records.Add(new ActiveGameplayEffectCleanupRecordBuffer
                {
                    Sequence = slot.Sequence,
                    ActiveEffectEntity = Entity.Null,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = ownerResources.Owner,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    Instigator = slot.Instigator,
                    Causer = slot.Causer,
                    GameplayEffectCode = slot.GameplayEffectCode,
                    Level = slot.Level,
                    StackCount = slot.StackCount,
                    ContextId = slot.ContextId,
                    ParentContextId = slot.ParentContextId,
                    CleanupFrame = Frame,
                    CleanupState = EGameplayEffectLifecycleState.PendingRemove,
                    SlotState = ActiveEffectSlotState.PendingRemove,
                    PreviousSlotState = slot.State,
                    DurationFrame = slot.DurationFrame,
                    RemainingFrame = slot.RemainingFrame,
                    PeriodFrame = slot.PeriodFrame,
                    LastPeriodFrame = slot.LastPeriodFrame,
                    ActiveGrantedTagCount = slot.ActiveGrantedTagCount,
                    ActiveGrantedAbilityCount = slot.ActiveGrantedAbilityCount,
                    ActiveModifierCount = activeModifierCount,
                    RequestedCleanupWorkFlags = (int)cleanupFlags,
                    ResolvedCleanupWorkFlags = (int)cleanupFlags,
                    CleanupResolvedFrame = Frame,
                    Flags = slot.Flags,
                });

                ownerResources.Store.LastCleanupFrame = Frame;
                ownerResources.Store.CleanupRecordCount = records.Length;
            }

            private void EmitOverflowCommand(
                ref GEEffectCommandStreamComponent stream,
                ref GASDefinitionCatalogBlob catalog,
                in GEEffectCommandBuffer sourceCommand,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect)
            {
                if (gameplayEffect.OverflowGameplayEffectCode <= 0
                    || gameplayEffect.OverflowGameplayEffectCode == sourceCommand.GameplayEffectCode
                    || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, gameplayEffect.OverflowGameplayEffectCode, out var overflowIndex))
                {
                    return;
                }

                ref readonly var overflowEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, overflowIndex);
                var durationFrame = overflowEffect.DurationFrames;
                var kind = RequiresActiveMutationLane(in overflowEffect, durationFrame)
                    ? GEEffectCommandKind.ActiveMutation
                    : GEEffectCommandKind.Instant;
                var command = new GEEffectCommandBuffer
                {
                    Frame = Frame,
                    Kind = kind,
                    Source = GEEffectCommandSource.Overflow,
                    SourceAsc = sourceCommand.SourceAsc,
                    TargetAsc = sourceCommand.TargetAsc,
                    SourceAbility = sourceCommand.SourceAbility,
                    SourceEffect = sourceCommand.SourceEffect,
                    Instigator = sourceCommand.Instigator,
                    Causer = sourceCommand.Causer,
                    GameplayEffectCode = gameplayEffect.OverflowGameplayEffectCode,
                    Level = sourceCommand.Level,
                    DurationFrameOverride = durationFrame,
                    ParentContextId = sourceCommand.ContextId,
                    TargetDataKind = sourceCommand.TargetDataKind,
                    Flags = kind == GEEffectCommandKind.ActiveMutation ? GASGECommandSeedFlags.ActiveMutation : GASGECommandSeedFlags.None,
                };

                if (kind == GEEffectCommandKind.ActiveMutation)
                {
                    var targetAsc = command.TargetAsc != Entity.Null ? command.TargetAsc : command.SourceAsc;
                    if (targetAsc == Entity.Null
                        || !NextFrameActiveMutationCommandLookup.HasBuffer(targetAsc)
                        || !NextFrameActiveMutationSetByCallerLookup.HasBuffer(targetAsc))
                    {
                        return;
                    }

                    var ownerSetByCallerValues = NextFrameActiveMutationSetByCallerLookup[targetAsc];
                    var ownerCommand = PrepareCommand(
                        ref stream,
                        ownerSetByCallerValues.Length,
                        in command,
                        0,
                        Frame);
                    NextFrameActiveMutationCommandLookup[targetAsc].Add(new ActiveEffectNextFrameMutationCommandBuffer
                    {
                        Command = ownerCommand,
                    });
                    return;
                }

                var instantTargetAsc = command.TargetAsc != Entity.Null ? command.TargetAsc : command.SourceAsc;
                if (instantTargetAsc == Entity.Null
                    || !NextFrameInstantCommandLookup.HasBuffer(instantTargetAsc)
                    || !NextFrameInstantSetByCallerLookup.HasBuffer(instantTargetAsc))
                {
                    return;
                }

                var instantSetByCallerValues = NextFrameInstantSetByCallerLookup[instantTargetAsc];
                var instantCommand = PrepareCommand(
                    ref stream,
                    instantSetByCallerValues.Length,
                    in command,
                    0,
                    Frame);
                NextFrameInstantCommandLookup[instantTargetAsc].Add(new OwnerLocalInstantNextFrameCommandBuffer
                {
                    Command = instantCommand,
                });
            }

            private GEEffectCommandBuffer PrepareCommand(
                ref GEEffectCommandStreamComponent stream,
                int setByCallerStart,
                in GEEffectCommandBuffer command,
                int setByCallerCount,
                int currentFrame)
            {
                var resolved = command;
                if (resolved.Sequence <= 0)
                    resolved.Sequence = Allocate(ref stream.NextCommandSequence);
                if (resolved.Frame <= 0)
                    resolved.Frame = currentFrame;
                if (resolved.ContextId <= 0)
                    resolved.ContextId = Allocate(ref stream.NextContextId);
                if (resolved.TargetAsc == Entity.Null)
                    resolved.TargetAsc = resolved.SourceAsc;
                if (resolved.Instigator == Entity.Null)
                    resolved.Instigator = resolved.SourceAsc;
                if (resolved.Causer == Entity.Null)
                    resolved.Causer = resolved.SourceAbility;

                resolved.SetByCallerStart = setByCallerStart;
                resolved.SetByCallerCount = setByCallerCount;
                return resolved;
            }

            private void MarkActiveModifierAdded(Entity asc)
            {
                EnqueueAttributeOwnerMarkerRequest(asc, EAttributeOwnerMarkerRequestKind.SetActiveModifierPresent);
            }

            private void RefreshActiveModifierPresence(
                Entity asc,
                DynamicBuffer<AttributeActiveModifierBuffer> modifiers)
            {
                EnqueueAttributeOwnerMarkerRequest(asc, EAttributeOwnerMarkerRequestKind.SetActiveModifierPresent);
            }

            private void MarkCurrentValueDirty(
                DynamicBuffer<AttributeValueBuffer> attributes,
                Entity asc,
                int attrSetCode,
                int attrCode)
            {
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
                if (attrIndex == -1)
                    return;

                var attr = attributes[attrIndex];
                attr.Dirty = true;
                attributes[attrIndex] = attr;
                EnqueueAttributeOwnerMarkerRequest(asc, EAttributeOwnerMarkerRequestKind.MarkDirty);
            }

            private void EnqueueAttributeOwnerMarkerRequest(Entity asc, EAttributeOwnerMarkerRequestKind requestKind)
            {
                if (asc == Entity.Null
                    || EventBusEntity == Entity.Null
                    || !AttributeOwnerMarkerRequestLookup.HasBuffer(EventBusEntity))
                {
                    return;
                }

                var requests = AttributeOwnerMarkerRequestLookup[EventBusEntity];
                requests.Add(new AttributeOwnerMarkerRequestBuffer
                {
                    Sequence = requests.Length,
                    ASC = asc,
                    RequestKind = requestKind,
                    Value = 1,
                });
            }

            private void EnqueueStackOverflowEvent(
                in GEEffectCommandBuffer command,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.StackOverflow,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    ContextId = command.ContextId,
                    EventCode = command.GameplayEffectCode,
                    ReasonCode = gameplayEffect.OverflowGameplayEffectCode,
                });
            }

            private void EnqueueStackOverflowDeniedEvent(in GEEffectCommandBuffer command)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.StackOverflowDenied,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.Failure,
                    Severity = EGameplayFactSeverity.Warning,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    ContextId = command.ContextId,
                    EventCode = command.GameplayEffectCode,
                });
            }

            private void EnqueueStackClearedByOverflowEvent(in GEEffectCommandBuffer command)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.StackClearedByOverflow,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    ContextId = command.ContextId,
                    EventCode = command.GameplayEffectCode,
                });
            }

            private void EnqueueAppliedEvents(
                in GEEffectCommandBuffer command,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
                Entity gameplayEffectEntity)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.GameplayEffectApplied,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    SourceEffect = gameplayEffectEntity,
                    ContextId = command.ContextId,
                    EventCode = command.GameplayEffectCode,
                });

                if (gameplayEffect.GameplayCueCode <= 0)
                    return;

                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.CueRequested,
                    Domain = EGameplayFactDomain.Cue,
                    Category = EGameplayFactCategory.Request,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    SourceEffect = gameplayEffectEntity,
                    ContextId = command.ContextId,
                    EventCode = (int)EGameplayCueEvent.OnApply,
                    ReasonCode = gameplayEffect.GameplayCueCode,
                });
            }

            private void EnqueueRemovedEvent(in ActiveGameplayEffectBuffer slot)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.GameplayEffectRemoved,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = slot.TargetAsc,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    ContextId = slot.ContextId,
                    EventCode = slot.GameplayEffectCode,
                });
            }

            private void EnqueueGameplayEvent(GameplayEventBuffer evt)
            {
                if (StreamEntity == Entity.Null || !FactLookup.HasBuffer(StreamEntity))
                    return;

                evt.Frame = Frame;
                if (StreamLookup.HasComponent(StreamEntity))
                {
                    var stream = StreamLookup[StreamEntity];
                    evt.Sequence = Allocate(ref stream.NextFactSequence);
                    StreamLookup[StreamEntity] = stream;
                }
                else
                {
                    evt.Sequence = 0;
                }

                FactLookup[StreamEntity].Add(evt);
            }

            private void EnqueueTagChangedEvent(Entity owner, int tagIndex, bool added)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.TagChanged,
                    Domain = EGameplayFactDomain.Tag,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    TargetAsc = owner,
                    EventCode = tagIndex,
                    ReasonCode = added ? 1 : 0,
                });
            }
        }

        [BurstCompile]
        public struct GEActiveEffectPreTickSourceAttributeSnapshotGatherJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public BufferTypeHandle<ActiveGameplayEffectBuffer> ActiveEffectSlotBufferTypeHandle;
            [ReadOnly] public BufferLookup<AttributeValueBuffer> AttributeLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public NativeParallelHashMap<ActiveEffectSlotSourceAttributeSnapshotKey, float>.ParallelWriter ActiveEffectSlotSourceAttributeSnapshots;
            [NativeDisableParallelForRestriction] public NativeArray<ActiveEffectSlotSourceSnapshotLaneCounters> SnapshotLaneCounters;
            public int Frame;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Catalog.IsCreated)
                    return;

                ref var catalog = ref Catalog.Value;
                var owners = chunk.GetNativeArray(EntityTypeHandle);
                var slotBuffers = chunk.GetBufferAccessorRO(ref ActiveEffectSlotBufferTypeHandle);
                var snapshotLaneCounters = default(ActiveEffectSlotSourceSnapshotLaneCounters);
                var hasSnapshotLaneCounters = SnapshotLaneCounters.IsCreated
                    && (uint)unfilteredChunkIndex < (uint)SnapshotLaneCounters.Length;
                if (hasSnapshotLaneCounters)
                    snapshotLaneCounters = SnapshotLaneCounters[unfilteredChunkIndex];

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var owner = owners[entityIndex];
                    var slots = slotBuffers[entityIndex];
                    for (var slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                    {
                        var slot = slots[slotIndex];
                        var actionFlags = ActiveEffectStore.CreateTickActionFlags(in slot, Frame);
                        if (actionFlags == (int)ActiveEffectTickActionFlags.None
                            || slot.SourceAsc == Entity.Null
                            || CompareEntity(slot.SourceAsc, owner) == 0
                            || !AttributeLookup.HasBuffer(slot.SourceAsc)
                            || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
                        {
                            continue;
                        }

                        ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                        if (gameplayEffect.ModifierCount <= 0)
                            continue;

                        var sourceAttributes = AttributeLookup[slot.SourceAsc];
                        for (var i = 0; i < gameplayEffect.ModifierCount; i++)
                        {
                            var modifierIndex = gameplayEffect.ModifierStart + i;
                            if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                                continue;

                            var modifier = catalog.Modifiers[modifierIndex];
                            if (modifier.MagnitudeSource != EMagnitudeSource.SourceAttribute)
                            {
                                continue;
                            }

                            if (!TryReadSnapshotAttributeValue(sourceAttributes, in modifier, out var sourceValue))
                            {
                                snapshotLaneCounters.AttributeMissCount++;
                                continue;
                            }

                            var snapshotKey = MakeActiveEffectSlotSourceAttributeSnapshotKey(owner, slot.Sequence, modifierIndex);
                            snapshotLaneCounters.RecordSnapshotWrite(
                                ActiveEffectSlotSourceAttributeSnapshots.TryAdd(snapshotKey, sourceValue));
                        }
                    }
                }

                if (hasSnapshotLaneCounters)
                    SnapshotLaneCounters[unfilteredChunkIndex] = snapshotLaneCounters;
            }

            private static bool TryReadSnapshotAttributeValue(
                DynamicBuffer<AttributeValueBuffer> attributes,
                in GASCatalogModifierDefinitionBlob modifier,
                out float value)
            {
                value = 0f;
                var attrSetCode = modifier.CaptureAttributeSetCode != 0
                    ? modifier.CaptureAttributeSetCode
                    : modifier.AttributeSetCode;
                var attrCode = modifier.CaptureAttributeCode != 0
                    ? modifier.CaptureAttributeCode
                    : modifier.AttributeCode;
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
                if (attrIndex < 0)
                    return false;

                value = attributes[attrIndex].CurrentValue;
                return true;
            }

            private static int CompareEntity(Entity left, Entity right)
            {
                var result = left.Index.CompareTo(right.Index);
                if (result != 0)
                    return result;

                return left.Version.CompareTo(right.Version);
            }
        }

        [BurstCompile]
        public struct GEActiveEffectPreTickJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<ASCActiveEffectsComponent> ActiveEffectsTypeHandle;
            public BufferTypeHandle<ActiveGameplayEffectBuffer> ActiveEffectSlotBufferTypeHandle;
            public BufferTypeHandle<ActiveEffectMutationBuffer> MutationBufferTypeHandle;
            public BufferTypeHandle<GERemoveCommandBuffer> RemoveCommandBufferTypeHandle;
            public BufferLookup<ActiveGameplayEffectCleanupRecordBuffer> CleanupRecordLookup;
            public BufferLookup<ActiveGameplayEffectSetByCallerValueBuffer> SetByCallerSnapshotLookup;
            public BufferLookup<AttributeValueBuffer> AttributeLookup;
            public BufferLookup<AttributeActiveModifierBuffer> ActiveModifierLookup;
            public BufferLookup<AttributeOwnerMarkerRequestBuffer> AttributeOwnerMarkerRequestLookup;
            public ComponentLookup<TagMaskComponent> TagMaskLookup;
            [ReadOnly] public ComponentLookup<TagFixedMaskComponent> TagFixedMaskLookup;
            public BufferLookup<TagTemporarySourceBuffer> TagSourceLookup;
            public BufferLookup<AbilitySlotBuffer> AbilitySlotLookup;
            [ReadOnly] public ComponentLookup<AbilityStateComponent> AbilityStateLookup;
            [ReadOnly] public ComponentLookup<AbilityGrantedByEffectComponent> AbilityGrantedLookup;
            public ComponentTypeHandle<GERemoveCommandPendingComponent> RemovePendingTypeHandle;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            public BufferLookup<GESetByCallerValueBuffer> CommandSetByCallerLookup;
            public BufferLookup<ActiveEffectMutationCommandBuffer> ActiveMutationCommandLookup;
            public BufferLookup<ActiveEffectMutationSetByCallerValueBuffer> ActiveMutationSetByCallerLookup;
            public BufferLookup<AbilityLifecycleRequestBuffer> AbilityLifecycleRequestLookup;
            public BufferLookup<GameplayEventBuffer> FactLookup;
            public EntityCommandBuffer StructuralEcb;
            public EntityArchetype GrantedAbilityArchetype;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            [ReadOnly] public NativeParallelHashMap<ActiveEffectSlotSourceAttributeSnapshotKey, float> ActiveEffectSlotSourceAttributeSnapshots;
            [NativeDisableParallelForRestriction] public NativeArray<ActiveEffectSlotSourceSnapshotLaneCounters> SnapshotLaneCounters;
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public int Frame;
            public bool ProcessTickRecords;
            public bool ProcessExplicitRemoveCommands;

            private struct ActiveEffectOwnerResources
            {
                public Entity Owner;
                public ASCActiveEffectsComponent Store;
                public DynamicBuffer<ActiveGameplayEffectBuffer> Slots;
                public bool HasSetByCallerSnapshot;
                public DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> SetByCallerSnapshot;
                public bool HasTargetTags;
                public TagMaskComponent TargetTags;
                public bool TargetTagsDirty;
                public bool HasFixedTags;
                public TagFixedMaskComponent FixedTags;
                public bool HasAttributes;
                public DynamicBuffer<AttributeValueBuffer> Attributes;
                public bool HasActiveModifiers;
                public DynamicBuffer<AttributeActiveModifierBuffer> ActiveModifiers;
                public bool HasTagSources;
                public DynamicBuffer<TagTemporarySourceBuffer> TagSources;
                public bool HasAbilitySlots;
                public DynamicBuffer<AbilitySlotBuffer> AbilitySlots;
                public bool HasCleanupRecords;
                public DynamicBuffer<ActiveGameplayEffectCleanupRecordBuffer> CleanupRecords;
            }

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if ((!ProcessTickRecords && !ProcessExplicitRemoveCommands)
                    || StreamEntity == Entity.Null
                    || (ProcessTickRecords && !Catalog.IsCreated))
                {
                    return;
                }

                var owners = chunk.GetNativeArray(EntityTypeHandle);
                var stores = chunk.GetNativeArray(ref ActiveEffectsTypeHandle);
                var slotBuffers = chunk.GetBufferAccessor(ref ActiveEffectSlotBufferTypeHandle);
                var mutationBuffers = chunk.GetBufferAccessor(ref MutationBufferTypeHandle);
                var magnitudeSourceCounters = default(ActiveEffectMagnitudeSourceCounters);
                var snapshotLaneCounters = default(ActiveEffectSlotSourceSnapshotLaneCounters);
                var hasSnapshotLaneCounters = SnapshotLaneCounters.IsCreated
                    && (uint)unfilteredChunkIndex < (uint)SnapshotLaneCounters.Length;
                if (hasSnapshotLaneCounters)
                    snapshotLaneCounters = SnapshotLaneCounters[unfilteredChunkIndex];

                var removeCommandBuffers = ProcessExplicitRemoveCommands
                    ? chunk.GetBufferAccessor(ref RemoveCommandBufferTypeHandle)
                    : default;
                EnabledMask removePendingMask = default;
                if (ProcessExplicitRemoveCommands)
                    removePendingMask = chunk.GetEnabledMask(ref RemovePendingTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var owner = owners[entityIndex];
                    var mutations = mutationBuffers[entityIndex];
                    var ownerResources = CaptureActiveEffectOwnerResources(
                        owner,
                        stores[entityIndex],
                        slotBuffers[entityIndex]);

                    if (ProcessTickRecords)
                    {
                        if (ownerResources.Store.ChunkSkipMatchedSlotCount > 0
                            || ownerResources.Store.ChunkSkipDuePeriodSlotCount > 0
                            || ownerResources.Store.CleanupRecordCount > 0)
                        {
                            ref var catalog = ref Catalog.Value;
                            ProcessTickOwner(
                                ref ownerResources,
                                mutations,
                                ref catalog,
                                ref magnitudeSourceCounters,
                                ref snapshotLaneCounters);
                        }
                    }

                    if (ProcessExplicitRemoveCommands)
                    {
                        var removeCommands = removeCommandBuffers[entityIndex];
                        for (var commandIndex = 0; commandIndex < removeCommands.Length; commandIndex++)
                        {
                            RemoveMatchingOwnerLocalEffects(
                                ref ownerResources,
                                removeCommands[commandIndex].GameplayEffectCode,
                                mutations);
                        }

                        removeCommands.Clear();
                        removePendingMask[entityIndex] = false;
                    }

                    ActiveEffectStore.RefreshChunkSkipIndexCounters(ref ownerResources.Store, ownerResources.Slots, Frame);
                    FlushActiveEffectOwnerResources(ref ownerResources);
                    stores[entityIndex] = ownerResources.Store;
                }

                if (hasSnapshotLaneCounters)
                    SnapshotLaneCounters[unfilteredChunkIndex] = snapshotLaneCounters;

                FlushMagnitudeSourceCounters(ref magnitudeSourceCounters, ref snapshotLaneCounters);
            }

            private ActiveEffectOwnerResources CaptureActiveEffectOwnerResources(
                Entity owner,
                in ASCActiveEffectsComponent store,
                DynamicBuffer<ActiveGameplayEffectBuffer> slots)
            {
                var ownerResources = new ActiveEffectOwnerResources
                {
                    Owner = owner,
                    Store = store,
                    Slots = slots,
                    HasSetByCallerSnapshot = SetByCallerSnapshotLookup.HasBuffer(owner),
                    HasTargetTags = TagMaskLookup.HasComponent(owner),
                    HasFixedTags = TagFixedMaskLookup.HasComponent(owner),
                    HasAttributes = AttributeLookup.HasBuffer(owner),
                    HasActiveModifiers = ActiveModifierLookup.HasBuffer(owner),
                    HasTagSources = TagSourceLookup.HasBuffer(owner),
                    HasAbilitySlots = AbilitySlotLookup.HasBuffer(owner),
                    HasCleanupRecords = CleanupRecordLookup.HasBuffer(owner),
                };

                ownerResources.SetByCallerSnapshot = ownerResources.HasSetByCallerSnapshot
                    ? SetByCallerSnapshotLookup[owner]
                    : default;
                ownerResources.TargetTags = ownerResources.HasTargetTags
                    ? TagMaskLookup[owner]
                    : default;
                ownerResources.FixedTags = ownerResources.HasFixedTags
                    ? TagFixedMaskLookup[owner]
                    : default;
                ownerResources.Attributes = ownerResources.HasAttributes
                    ? AttributeLookup[owner]
                    : default;
                ownerResources.ActiveModifiers = ownerResources.HasActiveModifiers
                    ? ActiveModifierLookup[owner]
                    : default;
                ownerResources.TagSources = ownerResources.HasTagSources
                    ? TagSourceLookup[owner]
                    : default;
                ownerResources.AbilitySlots = ownerResources.HasAbilitySlots
                    ? AbilitySlotLookup[owner]
                    : default;
                ownerResources.CleanupRecords = ownerResources.HasCleanupRecords
                    ? CleanupRecordLookup[owner]
                    : default;

                return ownerResources;
            }

            private void FlushActiveEffectOwnerResources(ref ActiveEffectOwnerResources ownerResources)
            {
                if (ownerResources.Owner == Entity.Null)
                    return;

                if (ownerResources.TargetTagsDirty && ownerResources.HasTargetTags)
                    TagMaskLookup[ownerResources.Owner] = ownerResources.TargetTags;
            }

            private void FlushMagnitudeSourceCounters(
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                ref ActiveEffectSlotSourceSnapshotLaneCounters snapshotLaneCounters)
            {
                if (!magnitudeSourceCounters.HasEvidence
                    && !snapshotLaneCounters.HasEvidence)
                {
                    return;
                }

                if (StreamEntity == Entity.Null || !StreamLookup.HasComponent(StreamEntity))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                magnitudeSourceCounters.AddToStream(ref stream);
                snapshotLaneCounters.AddToStream(ref stream);
                StreamLookup[StreamEntity] = stream;
            }

            private static int CompareEntity(Entity left, Entity right)
            {
                var result = left.Index.CompareTo(right.Index);
                if (result != 0)
                    return result;

                return left.Version.CompareTo(right.Version);
            }

            private void ProcessTickOwner(
                ref ActiveEffectOwnerResources ownerResources,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations,
                ref GASDefinitionCatalogBlob catalog,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                ref ActiveEffectSlotSourceSnapshotLaneCounters snapshotLaneCounters)
            {
                if (!StreamLookup.HasComponent(StreamEntity))
                    return;

                for (var slotIndex = ownerResources.Slots.Length - 1; slotIndex >= 0; slotIndex--)
                {
                    var slot = ownerResources.Slots[slotIndex];
                    var actionFlags = ActiveEffectStore.CreateTickActionFlags(in slot, Frame);
                    if (actionFlags == (int)ActiveEffectTickActionFlags.None)
                        continue;

                    if ((actionFlags & (int)ActiveEffectTickActionFlags.Period) != 0)
                    {
                        EmitPeriodCommand(ref catalog, in slot, ref ownerResources);
                        slot.LastPeriodFrame = Frame;
                        ownerResources.Slots[slotIndex] = slot;
                        mutations.Add(new ActiveEffectMutationBuffer
                        {
                            Sequence = slot.Sequence,
                            Frame = Frame,
                            Kind = ActiveEffectMutationKind.PeriodTick,
                            ActiveEffect = Entity.Null,
                            SourceAsc = slot.SourceAsc,
                            TargetAsc = ownerResources.Owner,
                            SourceAbility = slot.SourceAbility,
                            SourceEffect = slot.SourceEffect,
                            GameplayEffectCode = slot.GameplayEffectCode,
                            ContextId = slot.ContextId,
                            ParentContextId = slot.ParentContextId,
                            StackCount = slot.StackCount,
                            DurationFrameOverride = slot.DurationFrame,
                            PeriodFrame = slot.PeriodFrame,
                        });
                    }

                    if ((actionFlags & (int)ActiveEffectTickActionFlags.DurationExpire) != 0)
                    {
                        HandleDurationExpired(
                            ref ownerResources,
                                slotIndex,
                                ref catalog,
                                mutations,
                                ref magnitudeSourceCounters,
                                ref snapshotLaneCounters);
                    }
                }
            }

            private void RemoveMatchingOwnerLocalEffects(
                ref ActiveEffectOwnerResources ownerResources,
                int gameplayEffectCode,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations)
            {
                if (ownerResources.Owner == Entity.Null)
                    return;

                for (var i = ownerResources.Slots.Length - 1; i >= 0; i--)
                {
                    var slot = ownerResources.Slots[i];
                    if (gameplayEffectCode > 0 && slot.GameplayEffectCode != gameplayEffectCode)
                        continue;

                    RemoveSlotAt(ref ownerResources, i, mutations);
                }
            }

            private void HandleDurationExpired(
                ref ActiveEffectOwnerResources ownerResources,
                int slotIndex,
                ref GASDefinitionCatalogBlob catalog,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                ref ActiveEffectSlotSourceSnapshotLaneCounters snapshotLaneCounters)
            {
                if ((uint)slotIndex >= (uint)ownerResources.Slots.Length)
                    return;

                var slot = ownerResources.Slots[slotIndex];
                if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
                {
                    RemoveSlotAt(ref ownerResources, slotIndex, mutations);
                    return;
                }

                ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                var expirationPolicy = (EffectExpirationPolicy)gameplayEffect.EffectExpirationPolicy;
                if (expirationPolicy == EffectExpirationPolicy.RefreshDuration)
                {
                    RefreshSlotDuration(ref slot, Frame, resetPeriod: true);
                    ownerResources.Slots[slotIndex] = slot;
                    mutations.Add(CreateRefreshMutation(in slot, Frame));
                    return;
                }

                if (expirationPolicy == EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration
                    && slot.StackCount > 1)
                {
                    slot.StackCount--;
                    RefreshSlotDuration(ref slot, Frame, resetPeriod: true);
                    ownerResources.Slots[slotIndex] = slot;
                    RebuildActiveModifiersForSlot(
                        ref ownerResources,
                        ref catalog,
                        in gameplayEffect,
                        in slot,
                        ref magnitudeSourceCounters,
                        ref snapshotLaneCounters);
                    mutations.Add(CreateStackMutation(in slot, Frame));
                    EnqueueStackCountChangedEvent(in slot);
                    return;
                }

                RemoveSlotAt(ref ownerResources, slotIndex, mutations);
            }

            private void RemoveSlotAt(
                ref ActiveEffectOwnerResources ownerResources,
                int slotIndex,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations)
            {
                if ((uint)slotIndex >= (uint)ownerResources.Slots.Length)
                    return;

                var slot = ownerResources.Slots[slotIndex];
                var activeModifierCount = RemoveActiveModifiersForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                RemoveGrantedTagsForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                RemoveGrantedAbilitiesForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                if (ownerResources.HasSetByCallerSnapshot)
                {
                    GASGeneratedActiveEffectRuntime.RemoveSetByCallerSnapshotForSlot(
                        ownerResources.SetByCallerSnapshot,
                        slot.Sequence,
                        slot.GameplayEffectCode);
                }
                RecordCleanup(ref ownerResources, in slot, activeModifierCount);
                mutations.Add(new ActiveEffectMutationBuffer
                {
                    Sequence = slot.Sequence,
                    Frame = Frame,
                    Kind = ActiveEffectMutationKind.Remove,
                    ActiveEffect = Entity.Null,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = ownerResources.Owner,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    GameplayEffectCode = slot.GameplayEffectCode,
                    ContextId = slot.ContextId,
                    ParentContextId = slot.ParentContextId,
                    StackCount = slot.StackCount,
                    DurationFrameOverride = slot.DurationFrame,
                    PeriodFrame = slot.PeriodFrame,
                });
                EnqueueRemovedEvent(in slot);
                ownerResources.Slots.RemoveAt(slotIndex);
            }

            private int RebuildActiveModifiersForSlot(
                ref ActiveEffectOwnerResources ownerResources,
                ref GASDefinitionCatalogBlob catalog,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
                in ActiveGameplayEffectBuffer slot,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                ref ActiveEffectSlotSourceSnapshotLaneCounters snapshotLaneCounters)
            {
                if (gameplayEffect.ModifierCount <= 0
                    || !ownerResources.HasAttributes
                    || !ownerResources.HasActiveModifiers)
                {
                    return 0;
                }

                RemoveActiveModifiersForSlot(ref ownerResources, slot.Sequence, slot.GameplayEffectCode);
                var attributes = ownerResources.Attributes;
                var activeModifiers = ownerResources.ActiveModifiers;
                var setByCallerSnapshot = ownerResources.HasSetByCallerSnapshot
                    ? ownerResources.SetByCallerSnapshot
                    : default;
                var added = 0;
                for (var i = 0; i < gameplayEffect.ModifierCount; i++)
                {
                    var modifierIndex = gameplayEffect.ModifierStart + i;
                    if ((uint)modifierIndex >= (uint)catalog.Modifiers.Length)
                        continue;

                    var modifier = catalog.Modifiers[modifierIndex];
                    var attrIndex = attributes.IndexOfAttribute(modifier.AttributeSetCode, modifier.AttributeCode);
                    if (attrIndex < 0)
                        continue;

                    var context = BuildMagnitudeContextFromSlot(
                        ref ownerResources,
                        in slot,
                        setByCallerSnapshot,
                        modifierIndex,
                        in modifier,
                        ref magnitudeSourceCounters,
                        ref snapshotLaneCounters);
                    if (!GASGeneratedMagnitudeEvaluator.TryResolveMagnitude(in modifier, in context, out var magnitude))
                        continue;

                    activeModifiers.Add(new AttributeActiveModifierBuffer
                    {
                        AttrSetCode = modifier.AttributeSetCode,
                        AttributeCode = modifier.AttributeCode,
                        SourceEntity = Entity.Null,
                        SourceSequence = slot.Sequence,
                        SourceGameplayEffectCode = slot.GameplayEffectCode,
                        Magnitude = magnitude,
                        Op = modifier.Operation,
                    });
                    MarkActiveModifierAdded(ownerResources.Owner);
                    MarkCurrentValueDirty(attributes, ownerResources.Owner, modifier.AttributeSetCode, modifier.AttributeCode);
                    added++;
                }

                return added;
            }

            private MagnitudeEvalContext BuildMagnitudeContextFromSlot(
                ref ActiveEffectOwnerResources ownerResources,
                in ActiveGameplayEffectBuffer slot,
                DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> setByCallerSnapshot,
                int modifierIndex,
                in GASCatalogModifierDefinitionBlob modifier,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                ref ActiveEffectSlotSourceSnapshotLaneCounters snapshotLaneCounters)
            {
                var context = new MagnitudeEvalContext
                {
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = slot.TargetAsc,
                    GameplayEffectCode = slot.GameplayEffectCode,
                    Level = slot.Level,
                    StackCount = slot.StackCount <= 0 ? 1 : slot.StackCount,
                    SetByCallerKey = modifier.MagnitudeKey,
                };

                if (modifier.MagnitudeSource == EMagnitudeSource.SetByCaller
                    && TryFindSetByCallerSnapshotValue(setByCallerSnapshot, slot.Sequence, slot.GameplayEffectCode, modifier.MagnitudeKey, out var setByCallerValue))
                {
                    context.HasSetByCallerValue = 1;
                    context.SetByCallerValue = setByCallerValue;
                }

                if (modifier.MagnitudeSource == EMagnitudeSource.SourceAttribute)
                {
                    magnitudeSourceCounters.SourceAttributeLookupCount++;
                    if (TryReadSourceAttributeValueFromSlot(
                            ref ownerResources,
                            in slot,
                            modifierIndex,
                            in modifier,
                            ref magnitudeSourceCounters,
                            ref snapshotLaneCounters,
                            out var sourceValue))
                    {
                        context.HasSourceAttributeValue = 1;
                        context.SourceAttributeValue = sourceValue;
                    }
                    else
                    {
                        magnitudeSourceCounters.FallbackValueCount++;
                        snapshotLaneCounters.FallbackValueCount++;
                    }
                }

                if (modifier.MagnitudeSource == EMagnitudeSource.TargetAttribute)
                {
                    magnitudeSourceCounters.TargetAttributeLookupCount++;
                    magnitudeSourceCounters.CurrentValueLookupCount++;
                    if (TryReadOwnerAttributeValue(ref ownerResources, in modifier, out var targetValue))
                    {
                        context.HasTargetAttributeValue = 1;
                        context.TargetAttributeValue = targetValue;
                    }
                    else
                    {
                        magnitudeSourceCounters.FallbackValueCount++;
                    }
                }

                return context;
            }

            private bool TryReadSourceAttributeValueFromSlot(
                ref ActiveEffectOwnerResources ownerResources,
                in ActiveGameplayEffectBuffer slot,
                int modifierIndex,
                in GASCatalogModifierDefinitionBlob modifier,
                ref ActiveEffectMagnitudeSourceCounters magnitudeSourceCounters,
                ref ActiveEffectSlotSourceSnapshotLaneCounters snapshotLaneCounters,
                out float value)
            {
                if (CompareEntity(ownerResources.Owner, slot.SourceAsc) == 0)
                {
                    magnitudeSourceCounters.CurrentValueLookupCount++;
                    return TryReadOwnerAttributeValue(ref ownerResources, in modifier, out value);
                }

                value = 0f;
                if (slot.SourceAsc == Entity.Null || !ActiveEffectSlotSourceAttributeSnapshots.IsCreated)
                {
                    magnitudeSourceCounters.CaptureMissCount++;
                    snapshotLaneCounters.ApplyMissCount++;
                    return false;
                }

                var snapshotKey = MakeActiveEffectSlotSourceAttributeSnapshotKey(ownerResources.Owner, slot.Sequence, modifierIndex);
                if (ActiveEffectSlotSourceAttributeSnapshots.TryGetValue(snapshotKey, out value))
                {
                    magnitudeSourceCounters.CapturedValueHitCount++;
                    snapshotLaneCounters.ApplyHitCount++;
                    return true;
                }

                magnitudeSourceCounters.CaptureMissCount++;
                snapshotLaneCounters.ApplyMissCount++;
                return false;
            }

            private bool TryReadOwnerAttributeValue(
                ref ActiveEffectOwnerResources ownerResources,
                in GASCatalogModifierDefinitionBlob modifier,
                out float value)
            {
                value = 0f;
                if (!ownerResources.HasAttributes)
                    return false;

                var attrSetCode = modifier.CaptureAttributeSetCode != 0
                    ? modifier.CaptureAttributeSetCode
                    : modifier.AttributeSetCode;
                var attributeCode = modifier.CaptureAttributeCode != 0
                    ? modifier.CaptureAttributeCode
                    : modifier.AttributeCode;
                var attributes = ownerResources.Attributes;
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attributeCode);
                if (attrIndex < 0)
                    return false;

                value = attributes[attrIndex].CurrentValue;
                return true;
            }

            private int RemoveActiveModifiersForSlot(
                ref ActiveEffectOwnerResources ownerResources,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (!ownerResources.HasActiveModifiers)
                    return 0;

                var modifiers = ownerResources.ActiveModifiers;
                var hasAttributes = ownerResources.HasAttributes;
                var attributes = hasAttributes ? ownerResources.Attributes : default;
                var removed = 0;
                for (var i = modifiers.Length - 1; i >= 0; i--)
                {
                    var modifier = modifiers[i];
                    if (modifier.SourceSequence != slotSequence
                        || modifier.SourceGameplayEffectCode != gameplayEffectCode)
                    {
                        continue;
                    }

                    modifiers.RemoveAt(i);
                    if (hasAttributes)
                        MarkCurrentValueDirty(attributes, ownerResources.Owner, modifier.AttrSetCode, modifier.AttributeCode);
                    removed++;
                }

                RefreshActiveModifierPresence(ownerResources.Owner, modifiers);
                return removed;
            }

            private void MarkActiveModifierAdded(Entity asc)
            {
                EnqueueAttributeOwnerMarkerRequest(asc, EAttributeOwnerMarkerRequestKind.SetActiveModifierPresent);
            }

            private void RefreshActiveModifierPresence(
                Entity asc,
                DynamicBuffer<AttributeActiveModifierBuffer> modifiers)
            {
                EnqueueAttributeOwnerMarkerRequest(asc, EAttributeOwnerMarkerRequestKind.SetActiveModifierPresent);
            }

            private void MarkCurrentValueDirty(
                DynamicBuffer<AttributeValueBuffer> attributes,
                Entity asc,
                int attrSetCode,
                int attrCode)
            {
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attrCode);
                if (attrIndex == -1)
                    return;

                var attr = attributes[attrIndex];
                attr.Dirty = true;
                attributes[attrIndex] = attr;
                EnqueueAttributeOwnerMarkerRequest(asc, EAttributeOwnerMarkerRequestKind.MarkDirty);
            }

            private void EnqueueAttributeOwnerMarkerRequest(Entity asc, EAttributeOwnerMarkerRequestKind requestKind)
            {
                if (asc == Entity.Null
                    || EventBusEntity == Entity.Null
                    || !AttributeOwnerMarkerRequestLookup.HasBuffer(EventBusEntity))
                {
                    return;
                }

                var requests = AttributeOwnerMarkerRequestLookup[EventBusEntity];
                requests.Add(new AttributeOwnerMarkerRequestBuffer
                {
                    Sequence = requests.Length,
                    ASC = asc,
                    RequestKind = requestKind,
                    Value = 1,
                });
            }

            private void RemoveGrantedTagsForSlot(
                ref ActiveEffectOwnerResources ownerResources,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (!ownerResources.HasTagSources)
                    return;

                var sources = ownerResources.TagSources;
                for (var i = sources.Length - 1; i >= 0; i--)
                {
                    var source = sources[i];
                    if (source.SourceSequence != slotSequence
                        || source.SourceGameplayEffectCode != gameplayEffectCode)
                    {
                        continue;
                    }

                    sources.RemoveAt(i);
                    var removedFromMask = RemoveTagIndexFromEffectiveMaskIfUnreferenced(ref ownerResources, source.TagIndex);
                    if (removedFromMask)
                    {
                        EnqueueTagChangedEvent(ownerResources.Owner, source.TagIndex, false);
                    }
                }
            }

            private bool RemoveTagIndexFromEffectiveMaskIfUnreferenced(ref ActiveEffectOwnerResources ownerResources, int tagIndex)
            {
                if (!ownerResources.HasTargetTags)
                    return false;
                if (ownerResources.HasFixedTags
                    && ownerResources.FixedTags.Mask.HasTag(tagIndex))
                {
                    return false;
                }
                if (HasAnyTemporarySourceForTag(ownerResources.TagSources, tagIndex))
                    return false;

                var mask = ownerResources.TargetTags;
                if (!mask.HasTag(tagIndex))
                    return false;
                mask.RemoveTag(tagIndex);
                ownerResources.TargetTags = mask;
                ownerResources.TargetTagsDirty = true;
                return true;
            }

            private static bool HasAnyTemporarySourceForTag(
                DynamicBuffer<TagTemporarySourceBuffer> sources,
                int tagIndex)
            {
                for (var i = 0; i < sources.Length; i++)
                {
                    if (sources[i].TagIndex == tagIndex)
                        return true;
                }

                return false;
            }

            private void RemoveGrantedAbilitiesForSlot(
                ref ActiveEffectOwnerResources ownerResources,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (!ownerResources.HasAbilitySlots)
                    return;

                var grantedAbilities = ownerResources.AbilitySlots;
                for (var i = grantedAbilities.Length - 1; i >= 0; i--)
                {
                    var ability = grantedAbilities[i].AbilityEntity;
                    if (!IsGrantedBySlot(ability, slotSequence, gameplayEffectCode))
                        continue;

                    grantedAbilities.RemoveAt(i);
                    RemoveGrantedAbilityEntity(ability);
                }
            }

            private bool IsGrantedBySlot(
                Entity ability,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (ability == Entity.Null
                    || !AbilityGrantedLookup.HasComponent(ability))
                {
                    return false;
                }

                var granted = AbilityGrantedLookup[ability];
                return granted.SourceSequence == slotSequence
                    && granted.SourceGameplayEffectCode == gameplayEffectCode;
            }

            private void RemoveGrantedAbilityEntity(Entity ability)
            {
                if (ability == Entity.Null || !AbilityStateLookup.HasComponent(ability))
                    return;

                var runtime = AbilityStateLookup[ability];
                var isRunning = runtime.Phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending;
                if (isRunning)
                {
                    EnqueueAbilityLifecycleRequest(ability, runtime);
                    return;
                }

                StructuralEcb.DestroyEntity(ability);
            }

            private void EnqueueAbilityLifecycleRequest(Entity ability, in AbilityStateComponent runtime)
            {
                if (EventBusEntity == Entity.Null
                    || !AbilityLifecycleRequestLookup.HasBuffer(EventBusEntity))
                    return;

                var requests = AbilityLifecycleRequestLookup[EventBusEntity];
                requests.Add(new AbilityLifecycleRequestBuffer
                {
                    Sequence = requests.Length,
                    RequestKind = EAbilityLifecycleRequestKind.Cancel,
                    Reason = EAbilityLifecycleReason.GrantedEffectRemoved,
                    Ability = ability,
                    SourceAbility = Entity.Null,
                    SourceEffect = Entity.Null,
                    SourceAbilityCode = 0,
                    DestroyOnCleanup = 1,
                });
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.AbilityCancelRequested,
                    Domain = EGameplayFactDomain.Ability,
                    Category = EGameplayFactCategory.Request,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = runtime.Owner,
                    TargetAsc = runtime.Owner,
                    SourceAbility = ability,
                    EventCode = runtime.Code,
                    ReasonCode = (int)EAbilityLifecycleReason.GrantedEffectRemoved,
                });
            }

            private void RecordCleanup(
                ref ActiveEffectOwnerResources ownerResources,
                in ActiveGameplayEffectBuffer slot,
                int activeModifierCount)
            {
                if (!ownerResources.HasCleanupRecords)
                    return;

                var records = ownerResources.CleanupRecords;
                while (records.Length >= ActiveEffectStore.MaxCleanupRecordCount)
                    records.RemoveAt(0);

                var cleanupFlags = ActiveEffectCleanupWorkFlags.OwnerLocalSlot;
                if (activeModifierCount > 0)
                    cleanupFlags |= ActiveEffectCleanupWorkFlags.RuntimeModifiers;
                if (slot.ActiveGrantedTagCount > 0)
                    cleanupFlags |= ActiveEffectCleanupWorkFlags.GrantedTags;
                if (slot.ActiveGrantedAbilityCount > 0)
                    cleanupFlags |= ActiveEffectCleanupWorkFlags.GrantedAbilities;

                records.Add(new ActiveGameplayEffectCleanupRecordBuffer
                {
                    Sequence = slot.Sequence,
                    ActiveEffectEntity = Entity.Null,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = ownerResources.Owner,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    Instigator = slot.Instigator,
                    Causer = slot.Causer,
                    GameplayEffectCode = slot.GameplayEffectCode,
                    Level = slot.Level,
                    StackCount = slot.StackCount,
                    ContextId = slot.ContextId,
                    ParentContextId = slot.ParentContextId,
                    CleanupFrame = Frame,
                    CleanupState = EGameplayEffectLifecycleState.PendingRemove,
                    SlotState = ActiveEffectSlotState.PendingRemove,
                    PreviousSlotState = slot.State,
                    DurationFrame = slot.DurationFrame,
                    RemainingFrame = slot.RemainingFrame,
                    PeriodFrame = slot.PeriodFrame,
                    LastPeriodFrame = slot.LastPeriodFrame,
                    ActiveGrantedTagCount = slot.ActiveGrantedTagCount,
                    ActiveGrantedAbilityCount = slot.ActiveGrantedAbilityCount,
                    ActiveModifierCount = activeModifierCount,
                    RequestedCleanupWorkFlags = (int)cleanupFlags,
                    ResolvedCleanupWorkFlags = (int)cleanupFlags,
                    CleanupResolvedFrame = Frame,
                    Flags = slot.Flags,
                });

                ownerResources.Store.LastCleanupFrame = Frame;
                ownerResources.Store.CleanupRecordCount = records.Length;
            }

            private void EmitPeriodCommand(
                ref GASDefinitionCatalogBlob catalog,
                in ActiveGameplayEffectBuffer slot,
                ref ActiveEffectOwnerResources ownerResources)
            {
                if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
                    return;

                ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                if (gameplayEffect.PeriodGameplayEffectCode <= 0
                    || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, gameplayEffect.PeriodGameplayEffectCode, out var periodGameplayEffectIndex)
                    || !StreamLookup.HasComponent(StreamEntity))
                {
                    return;
                }

                ref readonly var periodGameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, periodGameplayEffectIndex);
                var durationFrame = periodGameplayEffect.DurationFrames;
                var kind = RequiresActiveMutationLane(in periodGameplayEffect, durationFrame)
                    ? GEEffectCommandKind.ActiveMutation
                    : GEEffectCommandKind.Instant;
                var command = new GEEffectCommandBuffer
                {
                    Kind = kind,
                    Source = GEEffectCommandSource.Period,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = ownerResources.Owner,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = Entity.Null,
                    Instigator = slot.Instigator != Entity.Null ? slot.Instigator : slot.SourceAsc,
                    Causer = slot.Causer != Entity.Null ? slot.Causer : slot.SourceAbility,
                    GameplayEffectCode = gameplayEffect.PeriodGameplayEffectCode,
                    Level = slot.Level,
                    DurationFrameOverride = durationFrame,
                    ParentContextId = slot.ContextId,
                    TargetDataKind = CompareEntity(slot.SourceAsc, ownerResources.Owner) == 0 ? ETargetDataKind.Self : ETargetDataKind.Entity,
                    Flags = kind == GEEffectCommandKind.ActiveMutation ? GASGECommandSeedFlags.ActiveMutation : GASGECommandSeedFlags.None,
                };

                var sourceSetByCallerValues = ownerResources.HasSetByCallerSnapshot
                    ? ownerResources.SetByCallerSnapshot
                    : default;
                var setByCallerCount = CountSetByCallerValues(sourceSetByCallerValues, slot.Sequence, slot.GameplayEffectCode);
                var stream = StreamLookup[StreamEntity];
                if (kind == GEEffectCommandKind.ActiveMutation)
                {
                    if (!ActiveMutationCommandLookup.HasBuffer(ownerResources.Owner)
                        || !ActiveMutationSetByCallerLookup.HasBuffer(ownerResources.Owner))
                    {
                        return;
                    }

                    var ownerSetByCallerValues = ActiveMutationSetByCallerLookup[ownerResources.Owner];
                    var ownerCommand = PrepareCommand(
                        ref stream,
                        ownerSetByCallerValues.Length,
                        in command,
                        setByCallerCount,
                        Frame);
                    CopySetByCallerValues(
                        ownerSetByCallerValues,
                        sourceSetByCallerValues,
                        slot.Sequence,
                        slot.GameplayEffectCode,
                        ownerCommand.Sequence);
                    ActiveMutationCommandLookup[ownerResources.Owner].Add(new ActiveEffectMutationCommandBuffer
                    {
                        Command = ownerCommand,
                    });
                    StreamLookup[StreamEntity] = stream;
                    return;
                }

                if (!CommandLookup.HasBuffer(ownerResources.Owner)
                    || !CommandSetByCallerLookup.HasBuffer(ownerResources.Owner))
                {
                    return;
                }

                var ownerInstantSetByCallerValues = CommandSetByCallerLookup[ownerResources.Owner];
                var resolved = PrepareCommand(ref stream, ownerInstantSetByCallerValues, in command, setByCallerCount, Frame);
                CopySetByCallerValues(
                    ownerInstantSetByCallerValues,
                    sourceSetByCallerValues,
                    slot.Sequence,
                    slot.GameplayEffectCode,
                    resolved.Sequence);
                CommandLookup[ownerResources.Owner].Add(resolved);
                StreamLookup[StreamEntity] = stream;
            }

            private GEEffectCommandBuffer PrepareCommand(
                ref GEEffectCommandStreamComponent stream,
                DynamicBuffer<GESetByCallerValueBuffer> setByCallerBuffer,
                in GEEffectCommandBuffer command,
                int setByCallerCount,
                int currentFrame)
            {
                var resolved = command;
                if (resolved.Sequence <= 0)
                    resolved.Sequence = Allocate(ref stream.NextCommandSequence);
                if (resolved.Frame <= 0)
                    resolved.Frame = currentFrame;
                if (resolved.ContextId <= 0)
                    resolved.ContextId = Allocate(ref stream.NextContextId);
                if (resolved.TargetAsc == Entity.Null)
                    resolved.TargetAsc = resolved.SourceAsc;
                if (resolved.Instigator == Entity.Null)
                    resolved.Instigator = resolved.SourceAsc;
                if (resolved.Causer == Entity.Null)
                    resolved.Causer = resolved.SourceAbility;

                resolved.SetByCallerStart = setByCallerBuffer.Length;
                resolved.SetByCallerCount = setByCallerCount;
                return resolved;
            }

            private GEEffectCommandBuffer PrepareCommand(
                ref GEEffectCommandStreamComponent stream,
                int setByCallerStart,
                in GEEffectCommandBuffer command,
                int setByCallerCount,
                int currentFrame)
            {
                var resolved = command;
                if (resolved.Sequence <= 0)
                    resolved.Sequence = Allocate(ref stream.NextCommandSequence);
                if (resolved.Frame <= 0)
                    resolved.Frame = currentFrame;
                if (resolved.ContextId <= 0)
                    resolved.ContextId = Allocate(ref stream.NextContextId);
                if (resolved.TargetAsc == Entity.Null)
                    resolved.TargetAsc = resolved.SourceAsc;
                if (resolved.Instigator == Entity.Null)
                    resolved.Instigator = resolved.SourceAsc;
                if (resolved.Causer == Entity.Null)
                    resolved.Causer = resolved.SourceAbility;

                resolved.SetByCallerStart = setByCallerStart;
                resolved.SetByCallerCount = setByCallerCount;
                return resolved;
            }

            private int CountSetByCallerValues(
                DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> setByCallerValues,
                int sourceSequence,
                int sourceGameplayEffectCode)
            {
                if (!setByCallerValues.IsCreated)
                    return 0;

                var count = 0;
                for (var i = 0; i < setByCallerValues.Length; i++)
                {
                    var value = setByCallerValues[i];
                    if (value.SourceSequence == sourceSequence
                        && value.SourceGameplayEffectCode == sourceGameplayEffectCode)
                    {
                        count++;
                    }
                }

                return count;
            }

            private void CopySetByCallerValues(
                DynamicBuffer<GESetByCallerValueBuffer> target,
                DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> source,
                int sourceSequence,
                int sourceGameplayEffectCode,
                int commandSequence)
            {
                if (!source.IsCreated)
                    return;

                for (var i = 0; i < source.Length; i++)
                {
                    var value = source[i];
                    if (value.SourceSequence != sourceSequence
                        || value.SourceGameplayEffectCode != sourceGameplayEffectCode)
                    {
                        continue;
                    }

                    target.Add(new GESetByCallerValueBuffer
                    {
                        CommandSequence = commandSequence,
                        SpecSequence = 0,
                        Key = value.Key,
                        Value = value.Value,
                    });
                }
            }

            private void CopySetByCallerValues(
                DynamicBuffer<ActiveEffectMutationSetByCallerValueBuffer> target,
                DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> source,
                int sourceSequence,
                int sourceGameplayEffectCode,
                int commandSequence)
            {
                if (!source.IsCreated)
                    return;

                for (var i = 0; i < source.Length; i++)
                {
                    var value = source[i];
                    if (value.SourceSequence != sourceSequence
                        || value.SourceGameplayEffectCode != sourceGameplayEffectCode)
                    {
                        continue;
                    }

                    target.Add(new ActiveEffectMutationSetByCallerValueBuffer
                    {
                        Value = new GESetByCallerValueBuffer
                        {
                            CommandSequence = commandSequence,
                            SpecSequence = 0,
                            Key = value.Key,
                            Value = value.Value,
                        },
                    });
                }
            }

            private void EnqueueStackCountChangedEvent(in ActiveGameplayEffectBuffer slot)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.StackCountChanged,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = slot.TargetAsc,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    ContextId = slot.ContextId,
                    EventCode = slot.GameplayEffectCode,
                    Value = slot.StackCount,
                });
            }

            private void EnqueueRemovedEvent(in ActiveGameplayEffectBuffer slot)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.GameplayEffectRemoved,
                    Domain = EGameplayFactDomain.GameplayEffect,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = slot.TargetAsc,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = slot.SourceEffect,
                    ContextId = slot.ContextId,
                    EventCode = slot.GameplayEffectCode,
                });
            }

            private void EnqueueGameplayEvent(GameplayEventBuffer evt)
            {
                if (StreamEntity == Entity.Null || !FactLookup.HasBuffer(StreamEntity))
                    return;

                evt.Frame = Frame;
                if (StreamLookup.HasComponent(StreamEntity))
                {
                    var stream = StreamLookup[StreamEntity];
                    evt.Sequence = Allocate(ref stream.NextFactSequence);
                    StreamLookup[StreamEntity] = stream;
                }
                else
                {
                    evt.Sequence = 0;
                }

                FactLookup[StreamEntity].Add(evt);
            }

            private void EnqueueTagChangedEvent(Entity owner, int tagIndex, bool added)
            {
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.TagChanged,
                    Domain = EGameplayFactDomain.Tag,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    TargetAsc = owner,
                    EventCode = tagIndex,
                    ReasonCode = added ? 1 : 0,
                });
            }
        }

        public static int ClampCursor(int cursor, int length)
        {
            if (cursor < 0) return 0;
            if (cursor > length) return length;
            return cursor;
        }

        public static bool TryNormalizeCommand(ref GASDefinitionCatalogBlob catalog, ref GEEffectCommandBuffer command)
        {
            if (command.GameplayEffectCode <= 0
                || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, command.GameplayEffectCode, out var gameplayEffectIndex))
            {
                return false;
            }

            ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            var durationFrame = ResolveDurationFrame(in command, in gameplayEffect);
            var isActiveMutation = RequiresActiveMutationLane(in gameplayEffect, durationFrame);
            command.Kind = isActiveMutation ? GEEffectCommandKind.ActiveMutation : GEEffectCommandKind.Instant;
            command.DurationFrameOverride = durationFrame;
            if (isActiveMutation)
                command.Flags |= GASGECommandSeedFlags.ActiveMutation;
            else
                command.Flags &= ~GASGECommandSeedFlags.ActiveMutation;
            return true;
        }

        private static ActiveGameplayEffectBuffer CreateNewSlot(
            ref ASCActiveEffectsComponent store,
            in GEEffectCommandBuffer command,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int durationFrame,
            int frame)
        {
            return new ActiveGameplayEffectBuffer
            {
                Sequence = Allocate(ref store.NextSequence),
                State = ActiveEffectSlotState.Active,
                PreviousState = ActiveEffectSlotState.Active,
                ActiveEffectEntity = Entity.Null,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                SourceEffect = command.SourceEffect,
                Instigator = command.Instigator,
                Causer = command.Causer,
                GameplayEffectCode = command.GameplayEffectCode,
                Level = command.Level,
                StackCount = 1,
                ContextId = command.ContextId,
                ParentContextId = command.ParentContextId,
                StartFrame = frame,
                StateStartFrame = frame,
                DurationFrame = durationFrame,
                RemainingFrame = durationFrame,
                PeriodFrame = gameplayEffect.PeriodFrames,
                LastPeriodFrame = frame,
            };
        }

        private static ActiveGameplayEffectBuffer RefreshExistingSlot(
            in ActiveGameplayEffectBuffer previous,
            in GEEffectCommandBuffer command,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int durationFrame,
            int frame,
            bool refreshDuration,
            bool resetPeriod)
        {
            var slot = previous;
            slot.PreviousState = slot.State;
            slot.State = ActiveEffectSlotState.Active;
            slot.SourceAsc = command.SourceAsc;
            slot.TargetAsc = command.TargetAsc;
            slot.SourceAbility = command.SourceAbility;
            slot.SourceEffect = command.SourceEffect;
            slot.Instigator = command.Instigator;
            slot.Causer = command.Causer;
            slot.Level = command.Level;
            slot.ContextId = command.ContextId;
            slot.ParentContextId = command.ParentContextId;
            slot.StateStartFrame = frame;
            if (refreshDuration)
            {
                slot.StartFrame = frame;
                slot.DurationFrame = durationFrame;
                slot.RemainingFrame = durationFrame;
            }
            slot.PeriodFrame = gameplayEffect.PeriodFrames;
            if (resetPeriod || slot.LastPeriodFrame <= 0)
                slot.LastPeriodFrame = frame;
            return slot;
        }

        private static bool IsStackOverflow(
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int previousStackCount)
        {
            return gameplayEffect.StackLimitCount > 0
                && previousStackCount >= gameplayEffect.StackLimitCount;
        }

        private static bool ShouldRefreshDuration(in GASCatalogGameplayEffectDefinitionBlob gameplayEffect)
        {
            return gameplayEffect.EffectDurationRefreshPolicy == (int)EffectDurationRefreshPolicy.RefreshOnSuccessfulApplication;
        }

        private static bool ShouldResetPeriod(in GASCatalogGameplayEffectDefinitionBlob gameplayEffect)
        {
            return gameplayEffect.EffectPeriodResetPolicy == (int)EffectPeriodResetPolicy.ResetOnSuccessfulApplication;
        }

        private static int ResolveNextStackCount(
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int previousStackCount,
            bool isNewSlot)
        {
            if (isNewSlot)
                return 1;
            if (gameplayEffect.StackLimitCount <= 0)
                return previousStackCount <= 0 ? 1 : previousStackCount;
            var next = previousStackCount <= 0 ? 1 : previousStackCount + 1;
            return next > gameplayEffect.StackLimitCount ? gameplayEffect.StackLimitCount : next;
        }

        private static int ResolveDurationFrame(
            in GEEffectCommandBuffer command,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect)
        {
            return command.DurationFrameOverride > 0
                ? command.DurationFrameOverride
                : gameplayEffect.DurationFrames;
        }

        private static bool HasPersistentRuntimeState(
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int durationFrame)
        {
            return durationFrame > 0
                || gameplayEffect.PeriodFrames > 0
                || gameplayEffect.StackLimitCount > 0
                || gameplayEffect.GrantedTagMaskIndex >= 0
                || gameplayEffect.GrantedAbilityCount > 0;
        }

        private static bool RequiresActiveMutationLane(
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int durationFrame)
        {
            return HasPersistentRuntimeState(in gameplayEffect, durationFrame)
                || !gameplayEffect.RemoveGameplayEffectTagQuery.IsEmpty;
        }

        private static int ResolveSlotFlags(
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int durationFrame,
            int activeModifierCount,
            int activeGrantedTagCount,
            int activeGrantedAbilityCount)
        {
            var flags = ActiveEffectSlotFlags.None;
            if (durationFrame > 0)
                flags |= ActiveEffectSlotFlags.HasDuration;
            if (gameplayEffect.PeriodFrames > 0)
                flags |= ActiveEffectSlotFlags.HasPeriod;
            if (gameplayEffect.StackLimitCount > 0)
                flags |= ActiveEffectSlotFlags.HasStacking;
            if (activeGrantedTagCount > 0)
                flags |= ActiveEffectSlotFlags.HasGrantedTags;
            if (activeGrantedAbilityCount > 0)
                flags |= ActiveEffectSlotFlags.HasGrantedAbilities;
            return (int)flags;
        }




        private static bool TryFindSetByCallerValue(
            in GEEffectCommandBuffer command,
            NativeList<GESetByCallerValueBuffer> setByCallerValues,
            int key,
            out float value)
        {
            var start = command.SetByCallerStart < 0 ? 0 : command.SetByCallerStart;
            var end = start + command.SetByCallerCount;
            if (end > setByCallerValues.Length)
                end = setByCallerValues.Length;
            for (var i = start; i < end; i++)
            {
                var setByCaller = setByCallerValues[i];
                if (setByCaller.Key != key || setByCaller.CommandSequence != command.Sequence)
                    continue;
                value = setByCaller.Value;
                return true;
            }

            value = 0f;
            return false;
        }











        private static void CopySetByCallerSnapshot(
            in GEEffectCommandBuffer command,
            NativeList<GESetByCallerValueBuffer> setByCallerValues,
            DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> snapshot,
            int slotSequence,
            int gameplayEffectCode)
        {
            if (command.SetByCallerCount <= 0
                || slotSequence <= 0
                || gameplayEffectCode <= 0)
            {
                return;
            }

            var start = command.SetByCallerStart < 0 ? 0 : command.SetByCallerStart;
            var end = start + command.SetByCallerCount;
            if (end > setByCallerValues.Length)
                end = setByCallerValues.Length;
            for (var i = start; i < end; i++)
            {
                var value = setByCallerValues[i];
                if (value.CommandSequence != command.Sequence)
                    continue;

                snapshot.Add(new ActiveGameplayEffectSetByCallerValueBuffer
                {
                    SourceSequence = slotSequence,
                    SourceGameplayEffectCode = gameplayEffectCode,
                    Key = value.Key,
                    Value = value.Value,
                });
            }
        }


        private static void RemoveSetByCallerSnapshotForSlot(
            DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> snapshot,
            int slotSequence,
            int gameplayEffectCode)
        {
            for (var i = snapshot.Length - 1; i >= 0; i--)
            {
                var value = snapshot[i];
                if (value.SourceSequence == slotSequence
                    && value.SourceGameplayEffectCode == gameplayEffectCode)
                {
                    snapshot.RemoveAt(i);
                }
            }
        }



        private static void RefreshSlotDuration(ref ActiveGameplayEffectBuffer slot, int frame, bool resetPeriod)
        {
            slot.StartFrame = frame;
            slot.RemainingFrame = slot.DurationFrame;
            slot.StateStartFrame = frame;
            if (resetPeriod && slot.PeriodFrame > 0)
                slot.LastPeriodFrame = frame;
        }

        private static ActiveEffectMutationBuffer CreateRefreshMutation(in ActiveGameplayEffectBuffer slot, int frame)
        {
            return new ActiveEffectMutationBuffer
            {
                Sequence = slot.Sequence,
                Frame = frame,
                Kind = ActiveEffectMutationKind.Refresh,
                ActiveEffect = Entity.Null,
                SourceAsc = slot.SourceAsc,
                TargetAsc = slot.TargetAsc,
                SourceAbility = slot.SourceAbility,
                SourceEffect = slot.SourceEffect,
                GameplayEffectCode = slot.GameplayEffectCode,
                ContextId = slot.ContextId,
                ParentContextId = slot.ParentContextId,
                StackCount = slot.StackCount,
                DurationFrameOverride = slot.DurationFrame,
                PeriodFrame = slot.PeriodFrame,
            };
        }

        private static ActiveEffectMutationBuffer CreateStackMutation(in ActiveGameplayEffectBuffer slot, int frame)
        {
            var mutation = CreateRefreshMutation(in slot, frame);
            mutation.Kind = ActiveEffectMutationKind.Stack;
            return mutation;
        }

        private static bool TryFindSetByCallerSnapshotValue(
            DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> snapshot,
            int slotSequence,
            int gameplayEffectCode,
            int key,
            out float value)
        {
            if (snapshot.IsCreated)
            {
                for (var i = 0; i < snapshot.Length; i++)
                {
                    var item = snapshot[i];
                    if (item.SourceSequence == slotSequence
                        && item.SourceGameplayEffectCode == gameplayEffectCode
                        && item.Key == key)
                    {
                        value = item.Value;
                        return true;
                    }
                }
            }

            value = 0f;
            return false;
        }




        private static int FindRefreshableSlot(DynamicBuffer<ActiveGameplayEffectBuffer> slots, in GEEffectCommandBuffer command)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.State == ActiveEffectSlotState.PendingRemove)
                    continue;
                if (slot.GameplayEffectCode == command.GameplayEffectCode
                    && slot.TargetAsc == command.TargetAsc
                    && slot.SourceAsc == command.SourceAsc
                    && slot.SourceAbility == command.SourceAbility)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool HasActiveModifier(
            DynamicBuffer<AttributeActiveModifierBuffer> modifiers,
            int sourceSequence,
            int gameplayEffectCode,
            int attrSetCode,
            int attributeCode,
            EModifierOp op)
        {
            for (var i = 0; i < modifiers.Length; i++)
            {
                var modifier = modifiers[i];
                if (modifier.SourceSequence == sourceSequence
                    && modifier.SourceGameplayEffectCode == gameplayEffectCode
                    && modifier.AttrSetCode == attrSetCode
                    && modifier.AttributeCode == attributeCode
                    && modifier.Op == op)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasTempTagSource(
            DynamicBuffer<TagTemporarySourceBuffer> sources,
            int tagIndex,
            int sourceSequence,
            int gameplayEffectCode)
        {
            for (var i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source.TagIndex == tagIndex
                    && source.SourceSequence == sourceSequence
                    && source.SourceGameplayEffectCode == gameplayEffectCode)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountTempTagSourcesForSlot(
            DynamicBuffer<TagTemporarySourceBuffer> sources,
            int sourceSequence,
            int gameplayEffectCode)
        {
            var count = 0;
            for (var i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source.SourceSequence == sourceSequence
                    && source.SourceGameplayEffectCode == gameplayEffectCode)
                {
                    count++;
                }
            }

            return count;
        }



        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;
            return next++;
        }




    }
}
";

        private static void WriteRequirementEvaluator(IndentedWriter writer)
        {
            writer.WriteLine("public static class GASGeneratedRequirementEvaluator");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static bool EvaluateAbilityRequirements(");
            writer.Indent++;
            writer.WriteLine("ref GASDefinitionCatalogBlob catalog,");
            writer.WriteLine("int abilityDefinitionIndex,");
            writer.WriteLine("in TagMaskComponent ownerTags,");
            writer.WriteLine("out int failureReasonCode)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("failureReasonCode = GASFailureReasonCodes.None;");
            writer.WriteLine("if ((uint)abilityDefinitionIndex >= (uint)catalog.Abilities.Length)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("failureReasonCode = GASFailureReasonCodes.AbilityNotFound;");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("ref readonly var ability = ref GASGeneratedDefinitionCatalogLookup.GetAbility(ref catalog, abilityDefinitionIndex);");
            writer.WriteLine("return EvaluateRange(ref catalog, ability.RequirementStart, ability.RequirementCount, in ownerTags, out failureReasonCode);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public static bool EvaluateGameplayEffectRequirements(");
            writer.Indent++;
            writer.WriteLine("ref GASDefinitionCatalogBlob catalog,");
            writer.WriteLine("int gameplayEffectDefinitionIndex,");
            writer.WriteLine("in TagMaskComponent targetTags,");
            writer.WriteLine("out int failureReasonCode)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("failureReasonCode = GASFailureReasonCodes.None;");
            writer.WriteLine("if ((uint)gameplayEffectDefinitionIndex >= (uint)catalog.GameplayEffects.Length)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("failureReasonCode = GASFailureReasonCodes.GameplayEffectNotFound;");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectDefinitionIndex);");
            writer.WriteLine("return EvaluateGameplayEffectRequirements(ref catalog, in gameplayEffect, in targetTags, out failureReasonCode);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public static bool EvaluateGameplayEffectRequirements(");
            writer.Indent++;
            writer.WriteLine("ref GASDefinitionCatalogBlob catalog,");
            writer.WriteLine("in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,");
            writer.WriteLine("in TagMaskComponent targetTags,");
            writer.WriteLine("out int failureReasonCode)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("return EvaluateRange(ref catalog, gameplayEffect.RequirementStart, gameplayEffect.RequirementCount, in targetTags, out failureReasonCode);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static bool EvaluateRange(");
            writer.Indent++;
            writer.WriteLine("ref GASDefinitionCatalogBlob catalog,");
            writer.WriteLine("int start,");
            writer.WriteLine("int count,");
            writer.WriteLine("in TagMaskComponent ownerTags,");
            writer.WriteLine("out int failureReasonCode)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("failureReasonCode = GASFailureReasonCodes.None;");
            writer.WriteLine("for (var i = 0; i < count; i++)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var index = start + i;");
            writer.WriteLine("if ((uint)index >= (uint)catalog.Requirements.Length)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("failureReasonCode = GASFailureReasonCodes.RequirementFailed;");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("var requirement = catalog.Requirements[index];");
            writer.WriteLine("if (!EvaluateRequirement(ref catalog, in requirement, in ownerTags))");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("failureReasonCode = GASFailureReasonCodes.RequirementFailed;");
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("return true;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("private static bool EvaluateRequirement(ref GASDefinitionCatalogBlob catalog, in GASCatalogRequirementDefinitionBlob requirement, in TagMaskComponent ownerTags)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (requirement.RequirementKind == GASRequirementKind.None)");
            writer.Indent++;
            writer.WriteLine("return true;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("if (requirement.TagQuery.IsEmpty)");
            writer.Indent++;
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var matches = requirement.TagQuery.Evaluate(ownerTags);");
            writer.WriteLine("if (requirement.RequirementKind == GASRequirementKind.RequiredTags)");
            writer.Indent++;
            writer.WriteLine("return matches;");
            writer.Indent--;
            writer.WriteLine("if (requirement.RequirementKind == GASRequirementKind.BlockedTags)");
            writer.Indent++;
            writer.WriteLine("return !matches;");
            writer.Indent--;
            writer.WriteLine("return false;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteMagnitudeEvaluator(IndentedWriter writer)
        {
            writer.WriteLine("public static class GASGeneratedMagnitudeEvaluator");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static bool TryResolveMagnitude(");
            writer.Indent++;
            writer.WriteLine("in GASCatalogModifierDefinitionBlob modifier,");
            writer.WriteLine("in MagnitudeEvalContext context,");
            writer.WriteLine("out float magnitude)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var rawMagnitude = modifier.MagnitudeSource switch");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("EMagnitudeSource.Constant => modifier.BaseMagnitude,");
            writer.WriteLine("EMagnitudeSource.SetByCaller => context.HasSetByCallerValue != 0 && context.SetByCallerKey == modifier.MagnitudeKey ? context.SetByCallerValue : modifier.FallbackMagnitude,");
            writer.WriteLine("EMagnitudeSource.SourceAttribute => context.HasSourceAttributeValue != 0 ? context.SourceAttributeValue : modifier.FallbackMagnitude,");
            writer.WriteLine("EMagnitudeSource.TargetAttribute => context.HasTargetAttributeValue != 0 ? context.TargetAttributeValue : modifier.FallbackMagnitude,");
            writer.WriteLine("EMagnitudeSource.ExecutionCalculation => context.HasExecutionValue != 0 ? context.ExecutionValue : modifier.FallbackMagnitude,");
            writer.WriteLine("EMagnitudeSource.StackCount => context.StackCount,");
            writer.WriteLine("_ => modifier.BaseMagnitude,");
            writer.Indent--;
            writer.WriteLine("};");
            writer.WriteLine("");
            writer.WriteLine("var coefficient = modifier.Coefficient == 0f ? 1f : modifier.Coefficient;");
            writer.WriteLine("magnitude = ((rawMagnitude + modifier.PreAdd) * coefficient) + modifier.PostAdd;");
            writer.WriteLine("return true;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteTargetRuleTable(IndentedWriter writer)
        {
            writer.WriteLine("public static class GASGeneratedTargetRuleTable");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static bool TryResolveSingleTarget(");
            writer.Indent++;
            writer.WriteLine("in AbilityActivationPlanRecord plan,");
            writer.WriteLine("Entity fallbackTarget,");
            writer.WriteLine("out AbilityTargetRecord target)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("target = new AbilityTargetRecord");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("SourceAsc = plan.SourceAsc,");
            writer.WriteLine("TargetAsc = plan.TargetAsc != Entity.Null ? plan.TargetAsc : fallbackTarget,");
            writer.WriteLine("TargetRuleCode = plan.TargetRuleCode,");
            writer.Indent--;
            writer.WriteLine("};");
            writer.WriteLine("return target.TargetAsc != Entity.Null;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }
    }

    internal sealed class RuntimeLifecycleMigrationPhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "RuntimeLifecycleMigration";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[]
        {
            "Runtime/RuntimeAbilityActivation.gen.cs",
            "Runtime/RuntimeEffectInstant.gen.cs",
            "Runtime/RuntimeActiveEffect.gen.cs",
        };

        public override bool RequiresRows => false;

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var systemPath = GetOutputPath(context, OutputFileNames[0]);
            var instantEffectPath = GetOutputPath(context, OutputFileNames[1]);
            var activeEffectPath = GetOutputPath(context, OutputFileNames[2]);

            RuntimeDefinitionGluePhase.WriteRuntimeAbilityActivationSystem(context, systemPath);
            RuntimeDefinitionGluePhase.WriteRuntimeEffectInstantSystems(context, instantEffectPath);
            RuntimeDefinitionGluePhase.WriteRuntimeActiveEffectSystems(context, activeEffectPath);

            AddRuntimeLifecycleMigrationManifest(manifest, PhaseName, systemPath);
            AddRuntimeLifecycleMigrationManifest(manifest, PhaseName, instantEffectPath);
            AddRuntimeLifecycleMigrationManifest(manifest, PhaseName, activeEffectPath);
        }
    }

    internal sealed class BakerGluePhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "BakerGlue";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[]
        {
            "Runtime/DefinitionComponents.gen.cs",
            "Editor/BakerGlue.gen.cs",
        };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var componentPath = GetOutputPath(context, OutputFileNames[0]);
            var bakerPath = GetOutputPath(context, OutputFileNames[1]);

            WriteRuntimeComponents(context, componentPath);
            WriteBakerGlue(context, bakerPath);

            AddRuntimeManifest(manifest, PhaseName, componentPath);
            AddBakingManifest(manifest, PhaseName, bakerPath);
        }

        private static void WriteRuntimeComponents(GasCodeGenContext context, string path)
        {
            var rows = RuntimeVisibleRows(context);
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public struct GASGeneratedDefinitionBlobComponent<T> : IComponentData where T : unmanaged");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public BlobAssetReference<T> Value;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public struct GASDefinitionCodeComponent : IComponentData");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public int Value;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("public static class GASGeneratedDefinitionBakePlan");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine($"public const int DefinitionCount = {rows.Count};");
            foreach (var row in rows.Select((value, index) => new { value, index }))
            {
                writer.WriteLine($"public const int {row.value.BlobSchemaName}Kind = {row.index};");
                writer.WriteLine($"public const int {row.value.DomainName}DefinitionKind = {(int)row.value.DefinitionKind};");
            }
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteBakerGlue(GasCodeGenContext context, string path)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("using UnityEngine;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;
            WriteRowResolver(writer, context);
            writer.WriteLine("");
            writer.WriteLine("public abstract class GASGeneratedDefinitionAuthoring : MonoBehaviour");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public int Code;");
            writer.Indent--;
            writer.WriteLine("}");

            foreach (var row in RuntimeVisibleRows(context))
            {
                writer.WriteLine("");
                var baseType = UsesDefaultBakerCodeKey(row)
                    ? "GASGeneratedDefinitionAuthoring"
                    : "MonoBehaviour";
                writer.WriteLine($"public sealed class {row.BlobSchemaName}Authoring : {baseType}");
                writer.WriteLine("{");
                writer.Indent++;
                if (row.HasRowFactory && !UsesDefaultBakerCodeKey(row))
                {
                    foreach (var keyFieldName in BakerKeyFieldNames(row))
                        writer.WriteLine($"public int {keyFieldName};");
                }
                else if (!row.HasRowFactory)
                {
                    writer.WriteLine($"public {RowTypeName(row)} Row;");
                }
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");
                writer.WriteLine($"public sealed class {row.BlobSchemaName}Baker : Baker<{row.BlobSchemaName}Authoring>");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"public override void Bake({row.BlobSchemaName}Authoring authoring)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("var entity = GetEntity(TransformUsageFlags.None);");
                if (row.HasRowFactory)
                {
                    writer.WriteLine($"if (!GASGeneratedDefinitionRowResolver.TryGet{row.BlobSchemaName}Row({CreateAuthoringKeyArgumentList(row)}, out var row))");
                    writer.Indent++;
                    writer.WriteLine("return;");
                    writer.Indent--;
                    writer.WriteLine($"var definitionCode = row.{row.CodeFieldName};");
                }
                else
                {
                    writer.WriteLine("var row = authoring.Row;");
                    writer.WriteLine($"var definitionCode = row.{row.CodeFieldName};");
                    writer.WriteLine("if (definitionCode <= 0)");
                    writer.Indent++;
                    writer.WriteLine("return;");
                    writer.Indent--;
                }

                writer.WriteLine($"var blob = GASGeneratedDefinitionBlobBuilder.{row.BakerMethodName}(row);");
                writer.WriteLine("AddBlobAsset(ref blob, out _);");
                writer.WriteLine("AddComponent(entity, new GASDefinitionCodeComponent { Value = definitionCode });");
                writer.WriteLine($"AddComponent(entity, new GASGeneratedDefinitionBlobComponent<{row.BlobSchemaName}> {{ Value = blob }});");
                writer.Indent--;
                writer.WriteLine("}");
                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteRowResolver(IndentedWriter writer, GasCodeGenContext context)
        {
            writer.WriteLine("public static class GASGeneratedDefinitionRowResolver");
            writer.WriteLine("{");
            writer.Indent++;

            var any = false;
            foreach (var row in RuntimeVisibleRows(context))
            {
                if (!row.HasRowFactory)
                    continue;

                any = true;
                writer.WriteLine("");
                writer.WriteLine($"public static bool TryGet{row.BlobSchemaName}Row({CreateResolverKeyParameterList(row)}, out {RowTypeName(row)} row)");
                writer.WriteLine("{");
                writer.Indent++;
                var rowValues = row.RowValues ?? Array.Empty<RowValueSnapshot>();
                foreach (var snapshot in rowValues.OrderBy(value => CreateBakerKeySortValue(value)))
                {
                    writer.WriteLine($"if ({CreateResolverKeyPredicate(row, snapshot)})");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine($"row = {CreateRowInitializer(row, snapshot.Row)};");
                    writer.WriteLine("return true;");
                    writer.Indent--;
                    writer.WriteLine("}");
                }

                writer.WriteLine("row = default;");
                writer.WriteLine("return false;");
                writer.Indent--;
                writer.WriteLine("}");
            }

            if (!any)
            {
                writer.WriteLine("public static bool HasGeneratedRowFactories => false;");
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        private static IReadOnlyList<string> BakerKeyFieldNames(RowMetadata row)
        {
            return row.BakerKeyFieldNames != null && row.BakerKeyFieldNames.Count > 0
                ? row.BakerKeyFieldNames
                : new[] { row.CodeFieldName };
        }

        private static bool UsesDefaultBakerCodeKey(RowMetadata row)
        {
            var keyFields = BakerKeyFieldNames(row);
            return keyFields.Count == 1
                   && string.Equals(keyFields[0], row.CodeFieldName, StringComparison.Ordinal);
        }

        private static string CreateAuthoringKeyArgumentList(RowMetadata row)
        {
            if (UsesDefaultBakerCodeKey(row))
                return "authoring.Code";

            var keyFields = BakerKeyFieldNames(row);
            return string.Join(", ", keyFields.Select(field => $"authoring.{field}"));
        }

        private static string CreateResolverKeyParameterList(RowMetadata row)
        {
            var keyFields = BakerKeyFieldNames(row);
            return string.Join(", ", keyFields.Select(field => $"int {ToCamelInvariant(field)}"));
        }

        private static string CreateResolverKeyPredicate(RowMetadata row, RowValueSnapshot snapshot)
        {
            var keyFields = BakerKeyFieldNames(row);
            var keyValues = snapshot.BakerKeyValues ?? Array.Empty<int>();
            var predicates = new List<string>(keyFields.Count);
            for (var i = 0; i < keyFields.Count; i++)
            {
                var value = i < keyValues.Count ? keyValues[i] : snapshot.Code;
                predicates.Add($"{ToCamelInvariant(keyFields[i])} == {value}");
            }

            return string.Join(" && ", predicates);
        }

        private static string CreateBakerKeySortValue(RowValueSnapshot snapshot)
        {
            var keyValues = snapshot.BakerKeyValues ?? Array.Empty<int>();
            if (keyValues.Count == 0)
                return snapshot.Code.ToString(CultureInfo.InvariantCulture);

            return string.Join(".", keyValues.Select(value => value.ToString("D10", CultureInfo.InvariantCulture)));
        }

        private static string ToCamelInvariant(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return char.ToLowerInvariant(value[0]) + value.Substring(1);
        }

        private static string CreateRowInitializer(RowMetadata row, object sourceRow)
        {
            if (sourceRow == null)
                return "default";

            var fields = sourceRow.GetType()
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .OrderBy(field => field.MetadataToken)
                .Select(field => $"{field.Name} = {CreateLiteral(field.GetValue(sourceRow))}");
            return $"new {RowTypeName(row)} {{ {string.Join(", ", fields)} }}";
        }

        private static string CreateLiteral(object value)
        {
            if (value == null)
                return "null";
            if (value is string text)
                return $"\"{EscapeStringLiteral(text)}\"";
            if (value is bool boolean)
                return boolean ? "true" : "false";
            if (value is float single)
                return CreateFloatLiteral(single);
            if (value is double real)
                return CreateFloatLiteral((float)real);
            if (value is int integer)
                return integer.ToString(CultureInfo.InvariantCulture);
            if (value is long longValue)
                return longValue.ToString(CultureInfo.InvariantCulture);
            if (value is Array array)
                return CreateArrayLiteral(array);
            if (value.GetType().IsEnum)
                return Convert.ToInt32(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "default";
        }

        private static string CreateArrayLiteral(Array array)
        {
            var elementType = array.GetType().GetElementType();
            var elementTypeName = elementType == typeof(int)
                ? "int"
                : elementType == typeof(float)
                    ? "float"
                    : elementType == typeof(bool)
                        ? "bool"
                        : elementType == typeof(string)
                            ? "string"
                            : elementType?.FullName?.Replace('+', '.') ?? "object";
            var values = new List<string>(array.Length);
            for (var i = 0; i < array.Length; i++)
                values.Add(CreateLiteral(array.GetValue(i)));

            return $"new {elementTypeName}[] {{ {string.Join(", ", values)} }}";
        }

        private static string EscapeStringLiteral(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string CreateFloatLiteral(float value)
        {
            if (float.IsNaN(value))
                return "float.NaN";
            if (float.IsPositiveInfinity(value))
                return "float.PositiveInfinity";
            if (float.IsNegativeInfinity(value))
                return "float.NegativeInfinity";
            return value.ToString("R", CultureInfo.InvariantCulture) + "f";
        }
    }

    internal sealed class ComponentTypeSetPhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "ComponentTypeSet";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[] { "Runtime/ComponentTypeSets.gen.cs" };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var path = GetOutputPath(context, OutputFileNames[0]);
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static class GASGeneratedDefinitionComponentTypeSets");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var row in RuntimeVisibleRows(context))
            {
                writer.WriteLine("");
                writer.WriteLine($"public static readonly ComponentTypeSet {row.ComponentSetName} = new ComponentTypeSet(");
                writer.Indent++;
                writer.WriteLine($"ComponentType.ReadWrite<GASGeneratedDefinitionBlobComponent<{row.BlobSchemaName}>>(),");
                writer.WriteLine("ComponentType.ReadWrite<GASDefinitionCodeComponent>()");
                writer.Indent--;
                writer.WriteLine(");");
            }

            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            AddRuntimeManifest(manifest, PhaseName, path);
        }
    }

    internal sealed class QueryLayoutPhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "QueryLayout";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[] { "Editor/QueryLayouts.gen.cs" };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var path = GetOutputPath(context, OutputFileNames[0]);
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static class GASGeneratedDefinitionQueryLayouts");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var row in RuntimeVisibleRows(context))
            {
                writer.WriteLine("");
                writer.WriteLine($"public static readonly EntityQueryDesc {row.QueryDescName} = new EntityQueryDesc");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("All = new ComponentType[]");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"ComponentType.ReadOnly<GASGeneratedDefinitionBlobComponent<{row.BlobSchemaName}>>(),");
                writer.WriteLine("ComponentType.ReadOnly<GASDefinitionCodeComponent>(),");
                writer.Indent--;
                writer.WriteLine("},");
                writer.WriteLine("Options = EntityQueryOptions.IncludeDisabledEntities,");
                writer.Indent--;
                writer.WriteLine("};");
            }

            writer.WriteLine("");
            writer.WriteLine("public static EntityQuery GetQuery(ref SystemState state, EntityQueryDesc desc)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("return state.GetEntityQuery(desc);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            AddEditorCiManifest(manifest, PhaseName, path);
        }
    }

    internal sealed class ValidationReportPhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "ValidationReport";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[] { "GasCodeGenValidationReport.md" };

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var path = GetOutputPath(context, OutputFileNames[0]);
            AddEditorCiManifest(manifest, PhaseName, path);
            var hotPathHits = CollectGeneratedHotPathRegressionHits(context);
            var runtimeForbiddenDependencyHits = CountRuntimeForbiddenDependencyHits(context);
            var duplicateMethodHits = CollectGeneratedDuplicateMethodHits(context, manifest);
            var lubanBoundaryHits = CollectLubanNormalizedRowBoundaryHits(context, manifest);
            var hasAutoChessConfigPhase = HasPhase(manifest, "AutoChessDemoConfig");
            IReadOnlyList<AutoChessConfigBoundaryHit> autoChessBoundaryHits = hasAutoChessConfigPhase
                ? CollectAutoChessConfigBoundaryHits(context, manifest)
                : Array.Empty<AutoChessConfigBoundaryHit>();
            var abilityCommitQueryHits = CollectGeneratedAbilityCommitQueryHits(context);
            var runtimeBoundaryHits = CollectGeneratedRuntimeBoundaryHits(context, manifest);
            var unclassifiedRuntimeBoundaryHits = runtimeBoundaryHits
                .Where(hit => !IsGeneratedRuntimeBoundaryAllowedClassifiedHit(context, manifest, hit))
                .ToArray();

            using var writer = new StreamWriter(path);

            writer.WriteLine("# GAS CodeGen Validation Report");
            writer.WriteLine();
            writer.WriteLine($"InputHash: `{context.InputHash}`");
            writer.WriteLine($"RowCount: `{context.Rows.Count}`");
            writer.WriteLine($"OrphansDeleted: `{context.OrphansDeleted}`");
            writer.WriteLine($"LubanCSharpOutput: `{ToProjectRelativePath(context, context.Settings.LubanCodeOutputPath)}`");
            writer.WriteLine($"LubanJsonOutput: `{ToProjectRelativePath(context, context.Settings.LubanDataOutputPath)}`");
            writer.WriteLine($"RuntimeForbiddenDependencyHits: `{runtimeForbiddenDependencyHits}`");
            writer.WriteLine($"RuntimeGeneratedNamingDebtHits: `{CountGeneratedNamingDebtHits(context, manifest, true)}`");
            writer.WriteLine($"GeneratedNamingDebtHits: `{CountGeneratedNamingDebtHits(context, manifest, false)}`");
            writer.WriteLine($"GeneratedHotPathRegressionHits: `{hotPathHits.Count}`");
            writer.WriteLine($"GeneratedDuplicateMethodHits: `{duplicateMethodHits.Count}`");
            writer.WriteLine($"LubanNormalizedRowBoundaryHits: `{lubanBoundaryHits.Count}`");
            writer.WriteLine($"AutoChessConfigBoundaryHits: `{autoChessBoundaryHits.Count}`");
            writer.WriteLine($"GeneratedAbilityCommitQueryHits: `{abilityCommitQueryHits.Count}`");
            writer.WriteLine($"GeneratedRuntimeBoundaryHits: `{runtimeBoundaryHits.Count}`");
            writer.WriteLine($"GeneratedRuntimePureGlueArtifacts: `{CountManifestArtifactsByCategory(manifest, "RuntimePureGlue")}`");
            writer.WriteLine($"GeneratedRuntimeLifecycleMigrationArtifacts: `{CountManifestArtifactsByCategory(manifest, "RuntimeLifecycleMigration")}`");
            writer.WriteLine($"GeneratedRuntimeLifecycleHits: `{CountGeneratedRuntimeBoundaryHits(runtimeBoundaryHits, "lifecycle-owner")}`");
            writer.WriteLine($"GeneratedRuntimeSystemRegistrationHits: `{CountGeneratedRuntimeBoundaryHits(runtimeBoundaryHits, "system-registration")}`");
            writer.WriteLine($"GeneratedRuntimeStructuralChangeHits: `{CountGeneratedRuntimeBoundaryHits(runtimeBoundaryHits, "structural-owner")}`");
            writer.WriteLine($"GeneratedRuntimeOwnershipHits: `{CountGeneratedRuntimeBoundaryHits(runtimeBoundaryHits, "native-container-owner")}`");
            writer.WriteLine($"GeneratedRuntimeRandomWriteLookupHits: `{CountGeneratedRuntimeBoundaryHits(runtimeBoundaryHits, "random-lookup-owner")}`");
            writer.WriteLine($"GeneratedRuntimeManagedConfigHits: `{CountGeneratedRuntimeBoundaryHits(runtimeBoundaryHits, "managed-config")}`");
            writer.WriteLine("GeneratedRuntimeBoundaryGateMode: `blocking-unclassified-lifecycle-migration`");
            writer.WriteLine($"GeneratedRuntimeUnclassifiedBoundaryHits: `{unclassifiedRuntimeBoundaryHits.Length}`");
            writer.WriteLine();
            writer.WriteLine("## Rows");
            writer.WriteLine();
            writer.WriteLine("| Row | DefinitionKind | CodeField | Blob | Lookup | RowFactory | BlobMembers | SourceRows |");
            writer.WriteLine("| --- | --- | --- | --- | --- | --- | ---: | ---: |");

            foreach (var row in context.Rows)
            {
                var factory = row.HasRowFactory
                    ? $"{row.RowFactoryTypeName}.{row.RowFactoryMethodName}()"
                    : "manual authoring row";
                writer.WriteLine(
                    $"| `{RowTypeName(row)}` | `{row.DefinitionKind}` | `{row.CodeFieldName}` | `{row.BlobSchemaName}` | `{row.LookupName}` | `{factory}` | `{row.BlobMembers.Count}` | `{row.RowValues?.Count ?? 0}` |");
            }

            writer.WriteLine();
            writer.WriteLine("## Layer Checks");
            writer.WriteLine();
            writer.WriteLine("| Layer | RuntimeVisible | Artifacts | Contract |");
            writer.WriteLine("| --- | --- | --- | --- |");
            writer.WriteLine("| Runtime | yes | Runtime asmdef, definition index, definition blobs, static lookups, catalog lookup, runtime definition glue, component type sets | No row, no JSON, no `cfg.*`, no mutable managed registry; runtime asmdef does not reference row source assemblies |");
            writer.WriteLine("| Baking | no | Editor asmdef, row-based Blob builders, lookup builders, catalog builder, authoring/Baker glue | `Baker<TAuthoring>` only adds components and BlobAssets; row source assemblies are editor/baking-only references |");
            writer.WriteLine("| Editor/CI | no | Query layout hints, manifest, validation report, dependency scan | Diagnostics only; not gameplay input |");

            writer.WriteLine();
            writer.WriteLine("## Luban Compile Boundary");
            writer.WriteLine();
            writer.WriteLine("| Artifact | UnityCompiled | RuntimeVisible | Allowed Dependencies | Contract |");
            writer.WriteLine("| --- | --- | --- | --- | --- |");
            writer.WriteLine($"| Luban generated C# | yes | no direct GAS Runtime Core dependency | `{ToProjectRelativePath(context, context.Settings.LubanCodeOutputPath)}` may use `cfg.*`, `Luban.Runtime`, `SimpleJSON` | Source row / table API boundary; compile errors are real gate failures, not hidden by moving files out of Assets |");
            writer.WriteLine($"| Luban generated JSON | asset/data | no | `{ToProjectRelativePath(context, context.Settings.LubanDataOutputPath)}` | Data input for loaders / authoring / baking; not queried by Runtime Core hot path |");
            writer.WriteLine("| GAS generated Runtime | yes | yes | GAS Runtime, Unity.Collections, Unity.Entities, Unity.Burst | May consume IDs, blobs, unmanaged lookups and component type sets only; no `cfg.*` / JSON reader / managed row reference |");
            writer.WriteLine("| GAS generated Editor/Baking | Editor only | no | row source assemblies, GAS Editor, GAS Runtime, Unity DOTS | May convert rows into BlobAssets and Baker outputs; no gameplay lifecycle ownership |");

            writer.WriteLine();
            writer.WriteLine("## Manifest Entries");
            writer.WriteLine();
            writer.WriteLine("| Phase | File | Layer | RuntimeVisible | VersionControlled |");
            writer.WriteLine("| --- | --- | --- | --- | --- |");
            foreach (var entry in manifest.Entries)
            {
                writer.WriteLine(
                    $"| `{entry.PhaseName}` | `{entry.ProjectRelativePath}` | `{entry.Layer}` | `{entry.RuntimeVisible}` | `{entry.VersionControlled}` |");
            }

            writer.WriteLine();
            writer.WriteLine("## DOTS And Naming Checks");
            writer.WriteLine();
            writer.WriteLine("| Rule | Status | Reason |");
            writer.WriteLine("| --- | --- | --- |");
            writer.WriteLine("| `BAKE-01` / `CASE-39` | Adopted | Generated Baker glue only adds components and BlobAssets; it does not read other Baker outputs. |");
            writer.WriteLine("| `BAKE-02` / `CASE-40` | Adopted | Generated Bakers do not cache instance state. |");
            writer.WriteLine("| `BLOB-01` / `BLOB-02` / `CASE-24` | Adopted | Static definitions are emitted as immutable Blob root structs and builder methods are Editor/Baking side. |");
            writer.WriteLine("| `CAT-01` | Adopted | Generated catalog stores Ability/GE data in one Blob root with sorted code arrays and range-based child arrays; Runtime glue consumes the catalog by ref and never queries row entities. Timeline rows are flattened at generation time and are not Runtime Core state. |");
            writer.WriteLine("| `QRY-01` / `JOB-01` / `PRF-05` / `PRF-33` | Partial | Generated Runtime stored queries use `state.GetEntityQuery(EntityQueryDesc)` and generated hot path gate rejects `SystemAPI.QueryBuilder().Build()` / `CreateEntityQuery` regressions. Full chunk-job traversal remains a later optimization pass. |");
            writer.WriteLine("| `SC-01` / `PRF-02` / `ECB-03` | Deferred | CodeGen does not hide structural changes; runtime playback ownership remains a Runtime Core contract. |");
            writer.WriteLine("| `BUR-01` / `BUR-02` | Adopted | Runtime-visible lookup data is unmanaged / Blob based; managed delegate registries remain forbidden. |");
            writer.WriteLine("| `NAT-01` / `NAT-04` | Adopted | Generated lookup structs expose `OwnsMemory`; `Dispose()` only releases NativeArray and Blob memory for owning instances. |");
            writer.WriteLine("| `ASM-01` | Adopted | Generated runtime/editor asmdefs are produced by the same pipeline; only the editor/baking asmdef references row source assemblies. |");
            writer.WriteLine("| `12-命名规范Spec` | Adopted | New generated Core names use `GAS*` for framework artifacts and `*DefinitionBlob` for Blob root types. |");
            writer.WriteLine("| `ODF-13` | Adopted | Luban generated C# is Unity-compiled boundary code, while GAS generated Runtime remains free of managed Luban / JSON dependencies. |");
            writer.WriteLine("| `ODF-*` | Deferred | Official DOTS coverage is reported here as a gate; Player/AOT evidence is still a later CI artifact. |");

            writer.WriteLine();
            writer.WriteLine("## Generated Runtime Boundary Gate");
            writer.WriteLine();
            writer.WriteLine("CurrentMode: `blocking-unclassified-lifecycle-migration`");
            writer.WriteLine("Target: SourceGenerator emits definition / blob / lookup / pure glue / validation only; Runtime lifecycle and ownership stay in handwritten ECS systems.");
            writer.WriteLine("AllowedMigrationProof: only manifest artifacts categorized as `RuntimeLifecycleMigration` may carry lifecycle / lookup / structural owner hits, and they remain bound to R2/R3/R5 exit work.");
            writer.WriteLine();
            writer.WriteLine("| Rule | Gate | Disposition | File | Line | Evidence |");
            writer.WriteLine("| --- | --- | --- | --- | ---: | --- |");
            if (runtimeBoundaryHits.Count == 0)
            {
                writer.WriteLine("| `SYS-01/SYS-03/QRY-04/SC-01/NAT-03` | none | pass | - | - | generated runtime contains no lifecycle, registration, structural, ownership, lookup-owner, or managed-config boundary debt |");
            }
            else
            {
                foreach (var hit in runtimeBoundaryHits)
                {
                    writer.WriteLine(
                        $"| `{hit.Rule}` | `{hit.Kind}` | `{ClassifyGeneratedRuntimeBoundaryHit(context, manifest, hit)}` | `{ToProjectRelativePath(context, hit.Path)}` | `{hit.Line}` | `{EscapeMarkdown(hit.Evidence)}` |");
                }
            }

            if (unclassifiedRuntimeBoundaryHits.Length > 0)
                throw new InvalidOperationException($"Generated Runtime boundary gate failed: {unclassifiedRuntimeBoundaryHits.Length} unclassified hit(s). See {path}.");

            writer.WriteLine();
            writer.WriteLine("## Generated Runtime Hot Path Gate");
            writer.WriteLine();
            writer.WriteLine("| Rule | Hit | File | Line | Evidence |");
            writer.WriteLine("| --- | --- | --- | ---: | --- |");
            if (hotPathHits.Count == 0)
            {
                writer.WriteLine("| `QRY-01` / `PRF-05` / `BUR-01` / `EN-03` | none | - | - | generated runtime passed static hot path regression gate |");
            }
            else
            {
                foreach (var hit in hotPathHits)
                {
                    writer.WriteLine(
                        $"| `{hit.Rule}` | `{hit.Kind}` | `{ToProjectRelativePath(context, hit.Path)}` | `{hit.Line}` | `{EscapeMarkdown(hit.Evidence)}` |");
                }
            }

            if (hotPathHits.Count > 0)
                throw new InvalidOperationException($"Generated Runtime hot path regression gate failed: {hotPathHits.Count} hit(s). See {path}.");
            if (runtimeForbiddenDependencyHits > 0)
                throw new InvalidOperationException($"Runtime forbidden dependency gate failed: {runtimeForbiddenDependencyHits} hit(s). See {path}.");

            writer.WriteLine();
            writer.WriteLine("## Generated Ability Commit Query Gate");
            writer.WriteLine();
            writer.WriteLine("| Rule | File | Line | Evidence |");
            writer.WriteLine("| --- | --- | ---: | --- |");
            if (abilityCommitQueryHits.Count == 0)
            {
                writer.WriteLine("| `EN-03/ABILITY-COMMIT-01` | - | - | ability commit query ignores enableable state and filters by enabled `AbilityCommitRequestComponent` mask inside the job |");
            }
            else
            {
                foreach (var hit in abilityCommitQueryHits)
                {
                    writer.WriteLine(
                        $"| `{hit.Rule}` | `{ToProjectRelativePath(context, hit.Path)}` | `{hit.Line}` | `{EscapeMarkdown(hit.Evidence)}` |");
                }
            }

            if (abilityCommitQueryHits.Count > 0)
                throw new InvalidOperationException($"Generated ability commit query gate failed: {abilityCommitQueryHits.Count} hit(s). See {path}.");

            writer.WriteLine();
            writer.WriteLine("## Generated Duplicate Method Gate");
            writer.WriteLine();
            writer.WriteLine("| Rule | File | Type | Signature | FirstLine | DuplicateLine |");
            writer.WriteLine("| --- | --- | --- | --- | ---: | ---: |");
            if (duplicateMethodHits.Count == 0)
            {
                writer.WriteLine("| `GEN-01` | - | - | - | - | - |");
            }
            else
            {
                foreach (var hit in duplicateMethodHits)
                {
                    writer.WriteLine(
                        $"| `GEN-01` | `{ToProjectRelativePath(context, hit.Path)}` | `{EscapeMarkdown(hit.TypeName)}` | `{EscapeMarkdown(hit.Signature)}` | `{hit.FirstLine}` | `{hit.DuplicateLine}` |");
                }
            }

            if (duplicateMethodHits.Count > 0)
                throw new InvalidOperationException($"Generated duplicate method gate failed: {duplicateMethodHits.Count} hit(s). See {path}.");

            writer.WriteLine();
            writer.WriteLine("## Luban Normalized Row Boundary Gate");
            writer.WriteLine();
            writer.WriteLine("| Rule | File | Line | Evidence |");
            writer.WriteLine("| --- | --- | ---: | --- |");
            if (lubanBoundaryHits.Count == 0)
            {
                writer.WriteLine("| `ODF-13/BLOB-01` | - | - | normalized rows are editor-only literal factories without JSON / Luban runtime dependencies |");
            }
            else
            {
                foreach (var hit in lubanBoundaryHits)
                {
                    writer.WriteLine(
                        $"| `{hit.Rule}` | `{ToProjectRelativePath(context, hit.Path)}` | `{hit.Line}` | `{EscapeMarkdown(hit.Evidence)}` |");
                }
            }

            if (lubanBoundaryHits.Count > 0)
                throw new InvalidOperationException($"Luban normalized row boundary gate failed: {lubanBoundaryHits.Count} hit(s). See {path}.");

            writer.WriteLine();
            writer.WriteLine("## AutoChess Config Boundary Gate");
            writer.WriteLine();
            writer.WriteLine("| Rule | File | Line | Evidence |");
            writer.WriteLine("| --- | --- | ---: | --- |");
            if (!hasAutoChessConfigPhase)
            {
                writer.WriteLine("| `AUTOCHESS-CONFIG-SPLIT` | - | - | AutoChessDemoConfig is generated by the separated AutoChessDemo sourcegen pass, not by the GAS Core validation report |");
            }
            else if (autoChessBoundaryHits.Count == 0)
            {
                writer.WriteLine("| `AUTOCHESS-CONFIG-01` | - | - | generated AutoChess config is the only default room / validation profile source |");
            }
            else
            {
                foreach (var hit in autoChessBoundaryHits)
                {
                    writer.WriteLine(
                        $"| `{hit.Rule}` | `{ToProjectRelativePath(context, hit.Path)}` | `{hit.Line}` | `{EscapeMarkdown(hit.Evidence)}` |");
                }
            }

            if (autoChessBoundaryHits.Count > 0)
                throw new InvalidOperationException($"AutoChess config boundary gate failed: {autoChessBoundaryHits.Count} hit(s). See {path}.");
        }

        private static bool HasPhase(GasCodeGenManifest manifest, string phaseName)
        {
            return manifest.Entries.Any(entry => string.Equals(entry.PhaseName, phaseName, StringComparison.Ordinal));
        }

        private static string ToProjectRelativePath(GasCodeGenContext context, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            var fullPath = Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(context.ProjectRoot, path));

            var projectRoot = Path.GetFullPath(context.ProjectRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!fullPath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath.Replace(Path.DirectorySeparatorChar, '/');

            return fullPath.Substring(projectRoot.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/');
        }

        private static int CountRuntimeForbiddenDependencyHits(GasCodeGenContext context)
        {
            var roots = new[]
            {
                Path.Combine(context.ProjectRoot, "Assets", "GAS", "Runtime"),
                Path.Combine(context.OutputDir, "Runtime"),
            };

            var forbidden = new[]
            {
                "cfg.",
                "XLuban",
                "SimpleJSON",
                "JsonReader",
                "BuildFromRows(",
            };
            var count = 0;
            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    foreach (var line in File.ReadLines(path))
                    {
                        var trimmed = line.TrimStart();
                        if (trimmed.StartsWith("//", StringComparison.Ordinal))
                            continue;

                        for (var i = 0; i < forbidden.Length; i++)
                        {
                            if (line.IndexOf(forbidden[i], StringComparison.Ordinal) >= 0)
                                count++;
                        }

                        if (line.IndexOf("IReadOnlyList<", StringComparison.Ordinal) >= 0
                            && line.IndexOf("DefinitionRow", StringComparison.Ordinal) >= 0)
                            count++;

                        if (line.IndexOf("DefinitionRow", StringComparison.Ordinal) >= 0
                            && !trimmed.StartsWith("///", StringComparison.Ordinal))
                            count++;
                    }
                }
            }

            return count;
        }

        private static IReadOnlyList<GeneratedHotPathRegressionHit> CollectGeneratedHotPathRegressionHits(GasCodeGenContext context)
        {
            var runtimeRoot = Path.Combine(context.OutputDir, "Runtime");
            var hits = new List<GeneratedHotPathRegressionHit>();
            if (!Directory.Exists(runtimeRoot))
                return hits;

            foreach (var path in Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories))
            {
                var isHotPathFile = IsGeneratedRuntimeHotPathFile(path);
                var isActiveEffectOwnerResourceJob = false;
                var lineNumber = 0;
                foreach (var line in File.ReadLines(path))
                {
                    lineNumber++;
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal))
                        continue;

                    AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-01/PRF-05", "sync-wait", "state.Dependency.Complete()");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-01/PRF-05", "main-thread-run", ".Run(");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-01/PRF-33", "stored-query-builder", "SystemAPI.QueryBuilder()");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-01/PRF-33", "entity-manager-query", "CreateEntityQuery");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "CASE-46/QRY-01", "main-thread-query", "SystemAPI.Query<");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "CASE-20/PRF-05", "legacy-event-writer", "EventBusHelper.BeginGameplayEventBatch");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "CASE-20/PRF-05", "legacy-event-writer", "EventBusHelper.Enqueue");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "STORE-03/SEL-01", "legacy-damage-buffer", "DamageEventBuffer");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "STORE-03/SEL-01", "legacy-boundary-buffer-write", "new CueRequestBuffer");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "STORE-03/SEL-01", "legacy-boundary-buffer-write", "new TagChangeEventBuffer");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "STORE-03/SEL-01", "legacy-boundary-buffer-lookup", "GetBufferLookup<CueRequestBuffer>");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "STORE-03/SEL-01", "legacy-boundary-buffer-lookup", "GetBufferLookup<TagChangeEventBuffer>");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "SYS-01/SC-01", "global-entity-manager-facade", "GASManager.EntityManager");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-01/PRF-05", "legacy-active-mutation", "TryApplyActiveMutation(");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-range-regression", "ActiveEffectsLookup[command.TargetAsc]");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-range-regression", "ActiveEffectSlotLookup[command.TargetAsc]");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-range-regression", "SetByCallerSnapshotLookup[command.TargetAsc]");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "PRF-05/NAT-01", "ability-seed-scratch", "NativeList<GECommandSeedRecord>");
                    AddHotPathHitIfContains(hits, path, lineNumber, line, "PRF-05/NAT-01", "ability-seed-scratch", "WriteGECommandSeeds(");

                    if (line.IndexOf("public struct GEActiveEffectMutationApplyJob : IJob", StringComparison.Ordinal) >= 0)
                    {
                        hits.Add(new GeneratedHotPathRegressionHit(
                            "QRY-04/PRF-06",
                            "active-mutation-serial-apply",
                            path,
                            lineNumber,
                            line.Trim()));
                        isActiveEffectOwnerResourceJob = true;
                    }
                    else if (line.IndexOf("public struct GEActiveEffectMutationChunkApplyJob : IJobChunk", StringComparison.Ordinal) >= 0)
                    {
                        isActiveEffectOwnerResourceJob = true;
                    }
                    else if (line.IndexOf("public struct GEActiveEffectPreTickJob : IJobChunk", StringComparison.Ordinal) >= 0)
                    {
                        isActiveEffectOwnerResourceJob = true;
                    }
                    else if (line.IndexOf("public static int ClampCursor", StringComparison.Ordinal) >= 0)
                    {
                        isActiveEffectOwnerResourceJob = false;
                    }

                    if (isActiveEffectOwnerResourceJob)
                    {
                        AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-resource-regression", "RemoveSlotAt(owner, slots");
                        AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-resource-regression", "RemoveActiveModifiersForSlot(owner,");
                        AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-resource-regression", "RemoveGrantedTagsForSlot(owner,");
                        AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-resource-regression", "RemoveGrantedAbilitiesForSlot(owner,");
                        AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-resource-regression", "RemoveSetByCallerSnapshotForSlot(owner,");
                        AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-resource-regression", "CountActiveModifiersForSlot(owner,");
                        AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-resource-regression", "ProcessTickOwner(owner,");
                        AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-resource-regression", "HandleDurationExpired(owner,");
                        AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-resource-regression", "EmitPeriodCommand(ref catalog, in slot, owner");
                        AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-resource-regression", "RebuildActiveModifiersForSlot(ref catalog,");
                        AddHotPathHitIfContains(hits, path, lineNumber, line, "QRY-04/PRF-06", "owner-resource-regression", "RecordCleanup(owner,");
                    }

                    if (isHotPathFile)
                    {
                        AddHotPathHitIfContains(hits, path, lineNumber, line, "PRF-05/NAT-01", "temp-allocation", "Allocator.Temp");
                        AddHotPathHitIfContains(hits, path, lineNumber, line, "PRF-22/EN-03", "chunk-count-for-loop", "entityIndex < chunk.Count");
                    }

                    if (line.IndexOf("HasComponent<ASCDestroyingComponent>", StringComparison.Ordinal) >= 0)
                    {
                        hits.Add(new GeneratedHotPathRegressionHit(
                            "EN-03/CASE-20",
                            "destroying-enabled-bit",
                            path,
                            lineNumber,
                            line.Trim()));
                    }
                }
            }

            return hits;
        }

        private static IReadOnlyList<GeneratedHotPathRegressionHit> CollectGeneratedRuntimeBoundaryHits(
            GasCodeGenContext context,
            GasCodeGenManifest manifest)
        {
            var hits = new List<GeneratedHotPathRegressionHit>();
            foreach (var entry in manifest.Entries)
            {
                if (!entry.RuntimeVisible
                    || !string.Equals(entry.Layer, "Runtime", StringComparison.Ordinal)
                    || !entry.ProjectRelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var path = Path.Combine(
                    context.ProjectRoot,
                    entry.ProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                    continue;

                var lineNumber = 0;
                foreach (var line in File.ReadLines(path))
                {
                    lineNumber++;
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal))
                        continue;

                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "SYS-01/SYS-03", "lifecycle-owner", ": ISystem");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "SYS-01/SYS-03", "lifecycle-owner", "SystemBase");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "SYS-01/SYS-03", "lifecycle-owner", "OnCreate(ref SystemState");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "SYS-01/SYS-03", "lifecycle-owner", "OnUpdate(ref SystemState");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "SYS-01/SYS-03", "lifecycle-owner", "OnDestroy(ref SystemState");

                    if (line.IndexOf("CreateSystem(", StringComparison.Ordinal) >= 0
                        || line.IndexOf("AddSystemToUpdateList(", StringComparison.Ordinal) >= 0
                        || line.IndexOf("GASGeneratedRuntimeSystemRegistration", StringComparison.Ordinal) >= 0)
                    {
                        hits.Add(new GeneratedHotPathRegressionHit(
                            "SYS-02/SYS-03",
                            "system-registration",
                            path,
                            lineNumber,
                            trimmed));
                    }

                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "SC-01/ECB-03", "structural-owner", "EntityCommandBuffer");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "SC-01/ECB-03", "structural-owner", "CreateCommandBuffer(");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "SC-01/ECB-03", "structural-owner", "CreateEntity(");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "SC-01/ECB-03", "structural-owner", "DestroyEntity(");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "SC-01/ECB-03", "structural-owner", "AddComponent(");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "SC-01/ECB-03", "structural-owner", "RemoveComponent(");

                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "NAT-01/NAT-03", "native-container-owner", "new NativeList<");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "NAT-01/NAT-03", "native-container-owner", "new NativeArray<");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "NAT-01/NAT-03", "native-container-owner", "new NativeParallelHashMap<");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "NAT-01/NAT-03", "native-container-owner", "Allocator.TempJob");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "NAT-01/NAT-03", "native-container-owner", "Allocator.Persistent");

                    if (IsGeneratedRuntimeLookupOwner(line))
                    {
                        hits.Add(new GeneratedHotPathRegressionHit(
                            "QRY-04/PRF-06/PRF-19",
                            "random-lookup-owner",
                            path,
                            lineNumber,
                            trimmed));
                    }

                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "ODF-13/BUR-01", "managed-config", "cfg.");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "ODF-13/BUR-01", "managed-config", "XLuban");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "ODF-13/BUR-01", "managed-config", "SimpleJSON");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "ODF-13/BUR-01", "managed-config", "JsonReader");
                    AddRuntimeBoundaryHitIfContains(hits, path, lineNumber, line, "ODF-13/BUR-01", "managed-config", "DefinitionRow");
                }
            }

            return hits;
        }

        private static bool IsGeneratedRuntimeLookupOwner(string line)
        {
            if (line.IndexOf("GetComponentLookup<", StringComparison.Ordinal) >= 0
                || line.IndexOf("GetBufferLookup<", StringComparison.Ordinal) >= 0)
            {
                return line.IndexOf("isReadOnly: true", StringComparison.Ordinal) < 0;
            }

            if (line.IndexOf("public ComponentLookup<", StringComparison.Ordinal) >= 0
                || line.IndexOf("public BufferLookup<", StringComparison.Ordinal) >= 0)
            {
                return line.IndexOf("[ReadOnly]", StringComparison.Ordinal) < 0;
            }

            return false;
        }

        private static void AddRuntimeBoundaryHitIfContains(
            ICollection<GeneratedHotPathRegressionHit> hits,
            string path,
            int lineNumber,
            string line,
            string rule,
            string kind,
            string needle)
        {
            if (line.IndexOf(needle, StringComparison.Ordinal) < 0)
                return;

            hits.Add(new GeneratedHotPathRegressionHit(rule, kind, path, lineNumber, line.Trim()));
        }

        private static int CountGeneratedRuntimeBoundaryHits(
            IEnumerable<GeneratedHotPathRegressionHit> hits,
            string kind)
        {
            return hits.Count(hit => string.Equals(hit.Kind, kind, StringComparison.Ordinal));
        }

        private static int CountManifestArtifactsByCategory(GasCodeGenManifest manifest, string artifactCategory)
        {
            return manifest.Entries.Count(entry =>
                string.Equals(entry.ArtifactCategory, artifactCategory, StringComparison.Ordinal));
        }

        private static string ClassifyGeneratedRuntimeBoundaryHit(
            GasCodeGenContext context,
            GasCodeGenManifest manifest,
            GeneratedHotPathRegressionHit hit)
        {
            if (IsGeneratedRuntimeBoundaryBootstrapDefinitionOwner(context, hit))
                return "BootstrapDefinitionOwner";

            return IsGeneratedRuntimeBoundaryMigrationProof(context, manifest, hit)
                ? "MigrationProofOnly"
                : "Blocking";
        }

        private static bool IsGeneratedRuntimeBoundaryAllowedClassifiedHit(
            GasCodeGenContext context,
            GasCodeGenManifest manifest,
            GeneratedHotPathRegressionHit hit)
        {
            return IsGeneratedRuntimeBoundaryMigrationProof(context, manifest, hit)
                   || IsGeneratedRuntimeBoundaryBootstrapDefinitionOwner(context, hit);
        }

        private static bool IsGeneratedRuntimeBoundaryMigrationProof(
            GasCodeGenContext context,
            GasCodeGenManifest manifest,
            GeneratedHotPathRegressionHit hit)
        {
            if (string.Equals(hit.Kind, "system-registration", StringComparison.Ordinal)
                || string.Equals(hit.Kind, "managed-config", StringComparison.Ordinal))
            {
                return false;
            }

            if (!IsGeneratedRuntimeLifecycleMigrationArtifact(context, manifest, hit.Path))
                return false;

            return string.Equals(hit.Kind, "lifecycle-owner", StringComparison.Ordinal)
                   || string.Equals(hit.Kind, "structural-owner", StringComparison.Ordinal)
                   || string.Equals(hit.Kind, "native-container-owner", StringComparison.Ordinal)
                   || string.Equals(hit.Kind, "random-lookup-owner", StringComparison.Ordinal);
        }

        private static bool IsGeneratedRuntimeLifecycleMigrationArtifact(
            GasCodeGenContext context,
            GasCodeGenManifest manifest,
            string path)
        {
            var projectRelativePath = ToProjectRelativePath(context, path);
            return manifest.Entries.Any(entry =>
                string.Equals(entry.ProjectRelativePath, projectRelativePath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.ArtifactCategory, "RuntimeLifecycleMigration", StringComparison.Ordinal));
        }

        private static bool IsGeneratedRuntimeBoundaryBootstrapDefinitionOwner(
            GasCodeGenContext context,
            GeneratedHotPathRegressionHit hit)
        {
            var fileName = Path.GetFileName(hit.Path);
            return string.Equals(fileName, "DefinitionCatalog.gen.cs", StringComparison.Ordinal)
                   && string.Equals(hit.Kind, "native-container-owner", StringComparison.Ordinal);
        }

        private static IReadOnlyList<GeneratedBoundaryHit> CollectGeneratedAbilityCommitQueryHits(
            GasCodeGenContext context)
        {
            var path = Path.Combine(context.OutputDir, "Runtime", "RuntimeAbilityActivation.gen.cs");
            var hits = new List<GeneratedBoundaryHit>();
            if (!File.Exists(path))
            {
                hits.Add(new GeneratedBoundaryHit(
                    "EN-03/ABILITY-COMMIT-01",
                    path,
                    0,
                    "RuntimeAbilityActivation.gen.cs is missing"));
                return hits;
            }

            var lines = File.ReadAllLines(path);
            if (!Contains(lines, "Options = EntityQueryOptions.IgnoreComponentEnabledState", out var optionsLine))
            {
                hits.Add(new GeneratedBoundaryHit(
                    "EN-03/ABILITY-COMMIT-01",
                    path,
                    0,
                    "AbilityCatalogCommitSystem query must ignore enableable component state"));
            }

            if (!Contains(lines, "if (!commitRequestMask[entityIndex])", out var maskLine))
            {
                hits.Add(new GeneratedBoundaryHit(
                    "EN-03/ABILITY-COMMIT-01",
                    path,
                    optionsLine,
                    "AbilityCatalogCommitJob must filter by enabled AbilityCommitRequestComponent mask"));
            }

            if (maskLine > 0 && Contains(lines, "var commitRequest = commitRequests[entityIndex];", out var requestLine)
                && maskLine > requestLine)
            {
                hits.Add(new GeneratedBoundaryHit(
                    "EN-03/ABILITY-COMMIT-01",
                    path,
                    maskLine,
                    "AbilityCommitRequestComponent mask check must run before reading and applying the request"));
            }

            return hits;
        }

        private static bool Contains(IReadOnlyList<string> lines, string needle, out int lineNumber)
        {
            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].IndexOf(needle, StringComparison.Ordinal) < 0)
                    continue;

                lineNumber = i + 1;
                return true;
            }

            lineNumber = 0;
            return false;
        }

        private static bool IsGeneratedRuntimeHotPathFile(string path)
        {
            var fileName = Path.GetFileName(path);
            return fileName == "RuntimeAbilityActivation.gen.cs"
                   || fileName == "RuntimeEffectInstant.gen.cs"
                   || fileName == "RuntimeActiveEffect.gen.cs";
        }

        private static void AddHotPathHitIfContains(
            ICollection<GeneratedHotPathRegressionHit> hits,
            string path,
            int lineNumber,
            string line,
            string rule,
            string kind,
            string needle)
        {
            if (line.IndexOf(needle, StringComparison.Ordinal) < 0)
                return;

            if (needle == "TryApplyActiveMutation("
                && line.IndexOf("public static", StringComparison.Ordinal) < 0
                && line.IndexOf("GASGeneratedActiveEffectRuntime.", StringComparison.Ordinal) < 0)
            {
                return;
            }

            hits.Add(new GeneratedHotPathRegressionHit(rule, kind, path, lineNumber, line.Trim()));
        }

        private static string EscapeMarkdown(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("`", "'")
                .Replace("|", "\\|");
        }

        private static IReadOnlyList<LubanNormalizedRowBoundaryHit> CollectLubanNormalizedRowBoundaryHits(
            GasCodeGenContext context,
            GasCodeGenManifest manifest)
        {
            var hits = new List<LubanNormalizedRowBoundaryHit>();
            var entries = manifest.Entries
                .Where(entry => string.Equals(entry.PhaseName, "LubanNormalizedRows", StringComparison.Ordinal))
                .ToArray();

            if (entries.Length == 0)
            {
                hits.Add(new LubanNormalizedRowBoundaryHit(
                    "ASM-01/ODF-13",
                    context.OutputDir,
                    0,
                    "manifest is missing LubanNormalizedRows entry"));
                return hits;
            }

            foreach (var entry in entries)
            {
                var path = Path.Combine(
                    context.ProjectRoot,
                    entry.ProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));

                if (!string.Equals(entry.Layer, "Editor", StringComparison.Ordinal))
                {
                    hits.Add(new LubanNormalizedRowBoundaryHit(
                        "ASM-01/ODF-13",
                        path,
                        0,
                        $"LubanNormalizedRows layer must be Editor, actual {entry.Layer}"));
                }

                if (entry.RuntimeVisible)
                {
                    hits.Add(new LubanNormalizedRowBoundaryHit(
                        "ASM-01/ODF-13",
                        path,
                        0,
                        "LubanNormalizedRows must not be runtime visible"));
                }

                if (!File.Exists(path))
                {
                    hits.Add(new LubanNormalizedRowBoundaryHit(
                        "ASM-01/ODF-13",
                        path,
                        0,
                        "LubanNormalizedRows generated file is missing"));
                    continue;
                }

                CollectForbiddenLubanNormalizedRowDependencyHits(path, hits);
            }

            return hits;
        }

        private static void CollectForbiddenLubanNormalizedRowDependencyHits(
            string path,
            ICollection<LubanNormalizedRowBoundaryHit> hits)
        {
            var forbidden = new[]
            {
                "Newtonsoft",
                "JToken",
                "JArray",
                "JObject",
                "File.ReadAllText",
                "JsonReader",
                "SimpleJSON",
                "XLuban",
                "cfg.",
            };

            var lineNumber = 0;
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)
                    || trimmed.StartsWith("///", StringComparison.Ordinal))
                    continue;

                for (var i = 0; i < forbidden.Length; i++)
                {
                    if (line.IndexOf(forbidden[i], StringComparison.Ordinal) >= 0)
                    {
                        hits.Add(new LubanNormalizedRowBoundaryHit(
                            "ODF-13/BLOB-01",
                            path,
                            lineNumber,
                            line.Trim()));
                    }
                }
            }
        }

        private static IReadOnlyList<AutoChessConfigBoundaryHit> CollectAutoChessConfigBoundaryHits(
            GasCodeGenContext context,
            GasCodeGenManifest manifest)
        {
            var hits = new List<AutoChessConfigBoundaryHit>();
            var generatedEntry = manifest.Entries.FirstOrDefault(entry =>
                string.Equals(entry.PhaseName, "AutoChessDemoConfig", StringComparison.Ordinal));
            if (generatedEntry == null)
            {
                hits.Add(new AutoChessConfigBoundaryHit(
                    "AUTOCHESS-CONFIG-01",
                    context.OutputDir,
                    0,
                    "manifest is missing AutoChessDemoConfig entry"));
                return hits;
            }

            var generatedPath = Path.Combine(
                context.ProjectRoot,
                generatedEntry.ProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(generatedPath))
            {
                hits.Add(new AutoChessConfigBoundaryHit(
                    "AUTOCHESS-CONFIG-01",
                    generatedPath,
                    0,
                    "AutoChess generated config file is missing"));
            }

            if (!generatedEntry.RuntimeVisible)
            {
                hits.Add(new AutoChessConfigBoundaryHit(
                    "AUTOCHESS-CONFIG-01",
                    generatedPath,
                    0,
                    "AutoChess generated config must be runtime visible to the demo assembly"));
            }

            CollectHandwrittenAutoChessConfigHits(context, hits);
            CollectAutoChessPhaseSeedHits(context, hits);
            CollectAutoChessModelSeedHits(context, hits);
            CollectAutoChessSourceConfigHits(context, hits);
            CollectAutoChessDotsApiHits(context, hits);
            return hits;
        }

        private static void CollectAutoChessDotsApiHits(
            GasCodeGenContext context,
            ICollection<AutoChessConfigBoundaryHit> hits)
        {
            var root = Path.Combine(context.ProjectRoot, "Assets", "AutoChessDemo");
            if (!Directory.Exists(root))
                return;

            var generatedRoot = Path.Combine(root, "Generated")
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var fullPath = Path.GetFullPath(path);
                if (fullPath.StartsWith(generatedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || fullPath.StartsWith(generatedRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var lineNumber = 0;
                foreach (var line in File.ReadLines(fullPath))
                {
                    lineNumber++;
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)
                        || trimmed.StartsWith("///", StringComparison.Ordinal))
                        continue;

                    if (line.IndexOf("SystemAPI.QueryBuilder()", StringComparison.Ordinal) >= 0)
                    {
                        hits.Add(new AutoChessConfigBoundaryHit(
                            "QRY-01/PRF-33",
                            fullPath,
                            lineNumber,
                            line.Trim()));
                    }
                }
            }
        }

        private static void CollectAutoChessSourceConfigHits(
            GasCodeGenContext context,
            ICollection<AutoChessConfigBoundaryHit> hits)
        {
            var path = Path.Combine(
                context.ProjectRoot,
                context.Settings.ConfigProjectPath,
                "Datas",
                "AutoChessDemo",
                "autochess.sourcegen.json");
            if (!File.Exists(path))
            {
                hits.Add(new AutoChessConfigBoundaryHit(
                    "AUTOCHESS-CONFIG-03",
                    path,
                    0,
                    "AutoChess sourcegen config is missing"));
            }
        }

        private static void CollectAutoChessModelSeedHits(
            GasCodeGenContext context,
            ICollection<AutoChessConfigBoundaryHit> hits)
        {
            var path = Path.Combine(
                context.ProjectRoot,
                "Assets",
                "GAS",
                "Editor",
                "CodeGen",
                "Core",
                "AutoChessDemoConfigModel.cs");
            CollectAutoChessCodeSeedHits(path, hits, "AUTOCHESS-CONFIG-03");
        }

        private static void CollectAutoChessPhaseSeedHits(
            GasCodeGenContext context,
            ICollection<AutoChessConfigBoundaryHit> hits)
        {
            var path = Path.Combine(
                context.ProjectRoot,
                "Assets",
                "GAS",
                "Editor",
                "CodeGen",
                "Phases",
                "AutoChessDemoCodeGenPhase.cs");
            CollectAutoChessCodeSeedHits(path, hits, "AUTOCHESS-CONFIG-02");
        }

        private static void CollectAutoChessCodeSeedHits(
            string path,
            ICollection<AutoChessConfigBoundaryHit> hits,
            string rule)
        {
            if (!File.Exists(path))
                return;

            var forbidden = new[]
            {
                "= 9001",
                "= 9101",
                "= 9102",
                "= 9103",
                "= 9201",
                "= 9202",
                "= 9207",
                "= 9401",
                "= 9402",
                "CreateDefaultUnits",
            };

            var lineNumber = 0;
            foreach (var line in File.ReadLines(path))
            {
                lineNumber++;
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)
                    || trimmed.StartsWith("///", StringComparison.Ordinal))
                    continue;

                for (var i = 0; i < forbidden.Length; i++)
                {
                    if (line.IndexOf(forbidden[i], StringComparison.Ordinal) >= 0)
                    {
                        hits.Add(new AutoChessConfigBoundaryHit(
                            rule,
                            path,
                            lineNumber,
                            line.Trim()));
                    }
                }
            }
        }

        private static void CollectHandwrittenAutoChessConfigHits(
            GasCodeGenContext context,
            ICollection<AutoChessConfigBoundaryHit> hits)
        {
            var root = Path.Combine(context.ProjectRoot, "Assets", "AutoChessDemo");
            if (!Directory.Exists(root))
                return;

            var generatedRoot = Path.Combine(root, "Generated")
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var forbidden = new[]
            {
                "private const int ValidationScale",
                "private const int ValidationMaxTicks",
                "private const int ValidationPostVictoryFlushTicks",
                "private const int ProcessWarmupRuns",
                "AbilityPlayerAttack = 9101",
                "AbilityEnemyAttack = 9102",
                "AbilityPlayerExecute = 9103",
            };

            foreach (var path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var fullPath = Path.GetFullPath(path);
                if (fullPath.StartsWith(generatedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    || fullPath.StartsWith(generatedRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    continue;

                var lineNumber = 0;
                foreach (var line in File.ReadLines(fullPath))
                {
                    lineNumber++;
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)
                        || trimmed.StartsWith("///", StringComparison.Ordinal))
                        continue;

                    for (var i = 0; i < forbidden.Length; i++)
                    {
                        if (line.IndexOf(forbidden[i], StringComparison.Ordinal) >= 0)
                        {
                            hits.Add(new AutoChessConfigBoundaryHit(
                                "AUTOCHESS-CONFIG-01",
                                fullPath,
                                lineNumber,
                                line.Trim()));
                        }
                    }
                }
            }
        }

        private static IReadOnlyList<GeneratedDuplicateMethodHit> CollectGeneratedDuplicateMethodHits(
            GasCodeGenContext context,
            GasCodeGenManifest manifest)
        {
            var hits = new List<GeneratedDuplicateMethodHit>();
            foreach (var entry in manifest.Entries)
            {
                if (!entry.ProjectRelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                var path = Path.Combine(
                    context.ProjectRoot,
                    entry.ProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                    continue;

                CollectGeneratedDuplicateMethodHits(path, hits);
            }

            return hits;
        }

        private static void CollectGeneratedDuplicateMethodHits(
            string path,
            ICollection<GeneratedDuplicateMethodHit> hits)
        {
            var seen = new Dictionary<string, MethodDeclarationLocation>(StringComparer.Ordinal);
            var scopes = new List<TypeScope>();
            var pendingTypeName = string.Empty;
            var braceDepth = 0;
            var lineNumber = 0;

            foreach (var rawLine in File.ReadLines(path))
            {
                lineNumber++;
                var codeLine = StripLineComment(rawLine).Trim();
                if (codeLine.Length == 0)
                {
                    braceDepth += CountChar(rawLine, '{') - CountChar(rawLine, '}');
                    continue;
                }

                TrimClosedScopes(scopes, braceDepth);

                var typeName = TryReadTypeDeclarationName(codeLine);
                if (!string.IsNullOrEmpty(typeName))
                    pendingTypeName = typeName;

                if (scopes.Count > 0 && TryReadMethodSignature(codeLine, out var signature))
                {
                    var currentType = string.Join(".", scopes.Select(scope => scope.Name));
                    var key = currentType + "|" + signature;
                    if (seen.TryGetValue(key, out var first))
                    {
                        hits.Add(new GeneratedDuplicateMethodHit(
                            path,
                            currentType,
                            signature,
                            first.Line,
                            lineNumber));
                    }
                    else
                    {
                        seen.Add(key, new MethodDeclarationLocation(lineNumber));
                    }
                }

                braceDepth += CountChar(rawLine, '{') - CountChar(rawLine, '}');
                if (!string.IsNullOrEmpty(pendingTypeName) && rawLine.IndexOf('{') >= 0)
                {
                    scopes.Add(new TypeScope(pendingTypeName, braceDepth));
                    pendingTypeName = string.Empty;
                }

                TrimClosedScopes(scopes, braceDepth);
            }
        }

        private static void TrimClosedScopes(List<TypeScope> scopes, int braceDepth)
        {
            for (var i = scopes.Count - 1; i >= 0; i--)
            {
                if (braceDepth >= scopes[i].BodyDepth)
                    break;

                scopes.RemoveAt(i);
            }
        }

        private static string TryReadTypeDeclarationName(string line)
        {
            var match = Regex.Match(
                line,
                @"\b(class|struct|interface)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
                RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["name"].Value : string.Empty;
        }

        private static bool TryReadMethodSignature(string line, out string signature)
        {
            signature = string.Empty;
            if (line.IndexOf('(') < 0 || line.IndexOf(')') < 0)
                return false;

            if (line.StartsWith("if ", StringComparison.Ordinal)
                || line.StartsWith("for ", StringComparison.Ordinal)
                || line.StartsWith("foreach ", StringComparison.Ordinal)
                || line.StartsWith("while ", StringComparison.Ordinal)
                || line.StartsWith("switch ", StringComparison.Ordinal)
                || line.StartsWith("using ", StringComparison.Ordinal)
                || line.StartsWith("return ", StringComparison.Ordinal))
                return false;

            var normalized = line.TrimEnd('{').Trim();
            var match = Regex.Match(
                normalized,
                @"^(?:(?:public|private|protected|internal|static|readonly|unsafe|extern|partial|virtual|override|sealed|async|new)\s+)+(?<return>[A-Za-z_][A-Za-z0-9_<>,\.\[\]\? ]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<parameters>[^)]*)\)",
                RegexOptions.CultureInvariant);
            if (!match.Success)
                return false;

            var name = match.Groups["name"].Value;
            if (name == "if" || name == "for" || name == "foreach" || name == "while" || name == "switch")
                return false;

            signature = name + "(" + NormalizeParameterList(match.Groups["parameters"].Value) + ")";
            return true;
        }

        private static string NormalizeParameterList(string parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters))
                return string.Empty;

            return string.Join(
                ",",
                parameters.Split(',')
                    .Select(NormalizeParameter)
                    .Where(value => value.Length > 0));
        }

        private static string NormalizeParameter(string parameter)
        {
            var value = parameter.Trim();
            if (value.Length == 0)
                return string.Empty;

            var defaultValueIndex = value.IndexOf('=');
            if (defaultValueIndex >= 0)
                value = value.Substring(0, defaultValueIndex).Trim();

            var tokens = value
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(token => token != "in" && token != "out" && token != "ref" && token != "params")
                .ToArray();
            if (tokens.Length == 0)
                return string.Empty;
            if (tokens.Length == 1)
                return tokens[0];

            return string.Join(" ", tokens.Take(tokens.Length - 1));
        }

        private static string StripLineComment(string line)
        {
            var index = line.IndexOf("//", StringComparison.Ordinal);
            return index >= 0 ? line.Substring(0, index) : line;
        }

        private static int CountChar(string line, char value)
        {
            var count = 0;
            foreach (var item in line)
            {
                if (item == value)
                    count++;
            }

            return count;
        }

        private readonly struct TypeScope
        {
            public readonly string Name;
            public readonly int BodyDepth;

            public TypeScope(string name, int bodyDepth)
            {
                Name = name;
                BodyDepth = bodyDepth;
            }
        }

        private readonly struct MethodDeclarationLocation
        {
            public readonly int Line;

            public MethodDeclarationLocation(int line)
            {
                Line = line;
            }
        }

        private readonly struct GeneratedDuplicateMethodHit
        {
            public readonly string Path;
            public readonly string TypeName;
            public readonly string Signature;
            public readonly int FirstLine;
            public readonly int DuplicateLine;

            public GeneratedDuplicateMethodHit(
                string path,
                string typeName,
                string signature,
                int firstLine,
                int duplicateLine)
            {
                Path = path;
                TypeName = typeName;
                Signature = signature;
                FirstLine = firstLine;
                DuplicateLine = duplicateLine;
            }
        }

        private readonly struct GeneratedHotPathRegressionHit
        {
            public readonly string Rule;
            public readonly string Kind;
            public readonly string Path;
            public readonly int Line;
            public readonly string Evidence;

            public GeneratedHotPathRegressionHit(string rule, string kind, string path, int line, string evidence)
            {
                Rule = rule;
                Kind = kind;
                Path = path;
                Line = line;
                Evidence = evidence;
            }
        }

        private readonly struct GeneratedBoundaryHit
        {
            public readonly string Rule;
            public readonly string Path;
            public readonly int Line;
            public readonly string Evidence;

            public GeneratedBoundaryHit(string rule, string path, int line, string evidence)
            {
                Rule = rule;
                Path = path;
                Line = line;
                Evidence = evidence;
            }
        }

        private readonly struct LubanNormalizedRowBoundaryHit
        {
            public readonly string Rule;
            public readonly string Path;
            public readonly int Line;
            public readonly string Evidence;

            public LubanNormalizedRowBoundaryHit(string rule, string path, int line, string evidence)
            {
                Rule = rule;
                Path = path;
                Line = line;
                Evidence = evidence;
            }
        }

        private readonly struct AutoChessConfigBoundaryHit
        {
            public readonly string Rule;
            public readonly string Path;
            public readonly int Line;
            public readonly string Evidence;

            public AutoChessConfigBoundaryHit(string rule, string path, int line, string evidence)
            {
                Rule = rule;
                Path = path;
                Line = line;
                Evidence = evidence;
            }
        }

        private static int CountGeneratedNamingDebtHits(
            GasCodeGenContext context,
            GasCodeGenManifest manifest,
            bool runtimeOnly)
        {
            var count = 0;
            foreach (var entry in manifest.Entries)
            {
                if (runtimeOnly && !entry.RuntimeVisible)
                    continue;

                if (!entry.ProjectRelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                var path = Path.Combine(
                    context.ProjectRoot,
                    entry.ProjectRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path))
                    continue;

                foreach (var line in File.ReadLines(path))
                {
                    if (line.IndexOf("public struct Blob", StringComparison.Ordinal) >= 0
                        && line.IndexOf("Definition", StringComparison.Ordinal) >= 0)
                        count++;

                    if (line.IndexOf("BlobAbilityDefinition", StringComparison.Ordinal) >= 0)
                        count++;

                    if (line.IndexOf("public static class BlobDefinitionLookupBuilder", StringComparison.Ordinal) >= 0)
                        count++;

                    if (line.IndexOf("public static class GasGenerated", StringComparison.Ordinal) >= 0)
                        count++;

                    if (line.IndexOf("public struct GeneratedDefinitionBlobComponent", StringComparison.Ordinal) >= 0)
                        count++;

                    if (line.IndexOf("public struct DefinitionCodeComponent", StringComparison.Ordinal) >= 0)
                        count++;

                    if (line.IndexOf("BlobDefinitionBuilder", StringComparison.Ordinal) >= 0)
                        count++;

                    if (line.IndexOf("GasComponentTypeSets", StringComparison.Ordinal) >= 0)
                        count++;

                    if (line.IndexOf("GasQueryLayouts", StringComparison.Ordinal) >= 0)
                        count++;
                }
            }

            return count;
        }
    }
}
