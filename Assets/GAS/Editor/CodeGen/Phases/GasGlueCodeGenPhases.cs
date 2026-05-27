using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GAS.Editor
{
    internal abstract class GasCodeGenPhaseBase : IGasCodeGenPhase
    {
        public abstract string PhaseName { get; }

        public abstract IReadOnlyList<string> OutputFileNames { get; }

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
                var assemblyName = row.RowType.Assembly.GetName().Name;
                if (string.IsNullOrWhiteSpace(assemblyName))
                    continue;

                if (!IsReferenceableSourceAssembly(assemblyName))
                    continue;

                references.Add(assemblyName);
            }

            return references.ToArray();
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
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
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
            writer.WriteLine($"public const int Count = {context.Rows.Count};");
            writer.WriteLine("");
            writer.WriteLine("public static bool TryGetAt(int index, out GASGeneratedDefinitionIndexEntry entry)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("switch (index)");
            writer.WriteLine("{");
            writer.Indent++;
            foreach (var row in context.Rows.Select((value, index) => new { value, index }))
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
            foreach (var row in context.Rows
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

            foreach (var row in context.Rows)
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

            foreach (var row in context.Rows)
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

            foreach (var row in context.Rows)
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

            foreach (var row in context.Rows)
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
            writer.WriteLine($"public const int DefinitionCount = {context.Rows.Count};");
            foreach (var row in context.Rows.Select((value, index) => new { value, index }))
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

            foreach (var row in context.Rows)
            {
                writer.WriteLine("");
                writer.WriteLine($"public sealed class {row.BlobSchemaName}Authoring : GASGeneratedDefinitionAuthoring");
                writer.WriteLine("{");
                if (!row.HasRowFactory)
                {
                    writer.Indent++;
                    writer.WriteLine($"public {RowTypeName(row)} Row;");
                    writer.Indent--;
                }
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
                    writer.WriteLine($"if (!GASGeneratedDefinitionRowResolver.TryGet{row.BlobSchemaName}Row(authoring.Code, out var row))");
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
            foreach (var row in context.Rows)
            {
                if (!row.HasRowFactory)
                    continue;

                any = true;
                writer.WriteLine("");
                writer.WriteLine($"public static bool TryGet{row.BlobSchemaName}Row(int code, out {RowTypeName(row)} row)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"var rows = {row.RowFactoryTypeName}.{row.RowFactoryMethodName}();");
                writer.WriteLine("for (var i = 0; i < rows.Length; i++)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"if (rows[i].{row.CodeFieldName} != code)");
                writer.Indent++;
                writer.WriteLine("continue;");
                writer.Indent--;
                writer.WriteLine("row = rows[i];");
                writer.WriteLine("return true;");
                writer.Indent--;
                writer.WriteLine("}");
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

            foreach (var row in context.Rows)
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

            foreach (var row in context.Rows)
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

            using var writer = new StreamWriter(path);

            writer.WriteLine("# GAS CodeGen Validation Report");
            writer.WriteLine();
            writer.WriteLine($"InputHash: `{context.InputHash}`");
            writer.WriteLine($"RowCount: `{context.Rows.Count}`");
            writer.WriteLine($"OrphansDeleted: `{context.OrphansDeleted}`");
            writer.WriteLine($"LubanCSharpOutput: `{ToProjectRelativePath(context, context.Settings.LubanCodeOutputPath)}`");
            writer.WriteLine($"LubanJsonOutput: `{ToProjectRelativePath(context, context.Settings.LubanDataOutputPath)}`");
            writer.WriteLine($"RuntimeForbiddenDependencyHits: `{CountRuntimeForbiddenDependencyHits(context)}`");
            writer.WriteLine($"RuntimeGeneratedNamingDebtHits: `{CountGeneratedNamingDebtHits(context, manifest, true)}`");
            writer.WriteLine($"GeneratedNamingDebtHits: `{CountGeneratedNamingDebtHits(context, manifest, false)}`");
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
            writer.WriteLine("| Runtime | yes | Runtime asmdef, definition index, definition blobs, static lookups, component type sets | No row, no JSON, no `cfg.*`, no mutable managed registry; runtime asmdef does not reference row source assemblies |");
            writer.WriteLine("| Baking | no | Editor asmdef, row-based Blob builders, lookup builders, authoring/Baker glue | `Baker<TAuthoring>` only adds outputs and calls `AddBlobAsset()`; row source assemblies are editor/baking-only references |");
            writer.WriteLine("| Editor/CI | no | Query layout hints, manifest, validation report, dependency scan | Diagnostics only; not gameplay input |");

            writer.WriteLine();
            writer.WriteLine("## Luban Compile Boundary");
            writer.WriteLine();
            writer.WriteLine("| Artifact | UnityCompiled | RuntimeVisible | Allowed Dependencies | Contract |");
            writer.WriteLine("| --- | --- | --- | --- | --- |");
            writer.WriteLine($"| Luban generated C# | yes | no direct GAS Runtime Core dependency | `{ToProjectRelativePath(context, context.Settings.LubanCodeOutputPath)}` may use `cfg.*`, `Luban.Runtime`, `SimpleJSON` | Source row / table API boundary; compile errors are real gate failures, not hidden by moving files out of Assets |");
            writer.WriteLine($"| Luban generated JSON | asset/data | no | `{ToProjectRelativePath(context, context.Settings.LubanDataOutputPath)}` | Data input for loaders / authoring / baking; not queried by Runtime Core hot path |");
            writer.WriteLine("| GAS generated Runtime | yes | yes | GAS Runtime, Unity.Collections, Unity.Entities | May consume IDs, blobs, unmanaged lookups and component type sets only; no `cfg.*` / JSON reader / managed row reference |");
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
            writer.WriteLine("| `QRY-01` / `JOB-01` / `PRF-05` | Deferred | CodeGen emits query layout descriptions only; it does not generate runtime lifecycle systems or hot path traversal. |");
            writer.WriteLine("| `SC-01` / `PRF-02` / `ECB-03` | Deferred | CodeGen does not hide structural changes; runtime playback ownership remains a Runtime Core contract. |");
            writer.WriteLine("| `BUR-01` / `BUR-02` | Adopted | Runtime-visible lookup data is unmanaged / Blob based; managed delegate registries remain forbidden. |");
            writer.WriteLine("| `NAT-01` / `NAT-04` | Adopted | Generated lookup structs expose `OwnsMemory`; `Dispose()` only releases NativeArray and Blob memory for owning instances. |");
            writer.WriteLine("| `ASM-01` | Adopted | Generated runtime/editor asmdefs are produced by the same pipeline; only the editor/baking asmdef references row source assemblies. |");
            writer.WriteLine("| `12-命名规范Spec` | Adopted | New generated Core names use `GAS*` for framework artifacts and `*DefinitionBlob` for Blob root types. |");
            writer.WriteLine("| `ODF-13` | Adopted | Luban generated C# is Unity-compiled boundary code, while GAS generated Runtime remains free of managed Luban / JSON dependencies. |");
            writer.WriteLine("| `ODF-*` | Deferred | Official DOTS coverage is reported here as a gate; Player/AOT evidence is still a later CI artifact. |");
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
