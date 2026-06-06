using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

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
            var rows = RuntimeVisibleRows(context);
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
            WriteRequirementAllocation(writer);
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
            writer.WriteLine("");
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

        private static void WriteRequirementAllocation(IndentedWriter writer)
        {
            writer.WriteLine("builder.Allocate(ref root.Requirements, 0);");
            writer.WriteLine("");
        }

        private static void WriteTagMaskAllocation(IndentedWriter writer, IReadOnlyList<CatalogTagMask> values)
        {
            WriteIntArrayAllocation(writer, "TagMaskCodes", values.Select(item => item.TagCode).ToArray());
            writer.WriteLine($"var tagMasks = builder.Allocate(ref root.TagMasks, {values.Count});");
            for (var i = 0; i < values.Count; i++)
            {
                var item = values[i];
                writer.WriteLine($"var tagMask{i} = new TagMaskComponent();");
                writer.WriteLine($"tagMask{i}.AddTag({item.TagCode});");
                writer.WriteLine($"tagMasks[{i}] = new GASCatalogTagMaskDefinitionBlob");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"TagCode = {item.TagCode},");
                writer.WriteLine($"Mask = tagMask{i},");
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

        private sealed class CatalogModel
        {
            public List<CatalogAbility> Abilities { get; } = new List<CatalogAbility>();
            public List<CatalogGameplayEffect> GameplayEffects { get; } = new List<CatalogGameplayEffect>();
            public List<CatalogModifier> Modifiers { get; } = new List<CatalogModifier>();
            public List<CatalogTagMask> TagMasks { get; } = new List<CatalogTagMask>();

            public static CatalogModel Create(GasCodeGenContext context)
            {
                var model = new CatalogModel();
                var tagMaskIndices = new Dictionary<int, int>();
                var timelinesById = BuildTimelines(context);
                BuildAbilities(context, model, timelinesById, tagMaskIndices);
                BuildGameplayEffects(context, model, tagMaskIndices);
                BuildTagMasks(model, tagMaskIndices);
                return model;
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
                Dictionary<int, int> tagMaskIndices)
            {
                var row = context.Rows.FirstOrDefault(item => item.DefinitionKind == GAS.Runtime.GASDefinitionKind.Ability);
                if (row?.RowValues == null)
                    return;

                foreach (var snapshot in row.RowValues)
                {
                    var timelineId = GetInt(snapshot.Row, "TimelineId");
                    timelinesById.TryGetValue(timelineId, out var timeline);
                    var activationOwnedTagCode = GetInt(snapshot.Row, "ActivationOwnedTagCode");

                    model.Abilities.Add(new CatalogAbility
                    {
                        AbilityCode = GetInt(snapshot.Row, "AbilityCode"),
                        Level = GetInt(snapshot.Row, "Level"),
                        PrimaryGameplayEffectCode = timeline.PrimaryGameplayEffectCode,
                        SecondaryGameplayEffectCode = timeline.SecondaryGameplayEffectCode,
                        CostGameplayEffectCode = GetInt(snapshot.Row, "CostGameplayEffectCode"),
                        CooldownGameplayEffectCode = GetInt(snapshot.Row, "CooldownGameplayEffectCode"),
                        CooldownFrames = GetInt(snapshot.Row, "CooldownFrames"),
                        ActivationOwnedTagMaskIndex = GetOrAddTagMaskIndex(tagMaskIndices, activationOwnedTagCode),
                        TargetRuleCode = timeline.TargetRuleCode,
                    });
                }

                model.Abilities.Sort((left, right) => left.AbilityCode.CompareTo(right.AbilityCode));
            }

            private static void BuildGameplayEffects(
                GasCodeGenContext context,
                CatalogModel model,
                Dictionary<int, int> tagMaskIndices)
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
                    };

                    if (HasModifier(snapshot.Row))
                    {
                        ge.ModifierStart = model.Modifiers.Count;
                        ge.ModifierCount = 1;
                        model.Modifiers.Add(new CatalogModifier
                        {
                            GameplayEffectCode = gameplayEffectCode,
                            ModifierIndex = 0,
                            AttributeSetCode = GetInt(snapshot.Row, "ModifierAttributeSetCode"),
                            AttributeCode = GetInt(snapshot.Row, "ModifierAttributeCode"),
                            Operation = GetEnumInt(snapshot.Row, "ModifierOperation"),
                            BaseMagnitude = GetFloat(snapshot.Row, "ModifierMagnitude"),
                            MagnitudeSource = GetEnumInt(snapshot.Row, "ModifierMagnitudeSource"),
                            MagnitudeKey = GetInt(snapshot.Row, "ModifierMagnitudeKey"),
                            CaptureAttributeSetCode = GetInt(snapshot.Row, "ModifierAttributeSetCode"),
                            CaptureAttributeCode = GetInt(snapshot.Row, "ModifierAttributeCode"),
                            CaptureTiming = 0,
                            FallbackMagnitude = GetFloat(snapshot.Row, "ModifierMagnitude"),
                            Coefficient = 1f,
                        });
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

            private static void BuildTagMasks(CatalogModel model, Dictionary<int, int> tagMaskIndices)
            {
                foreach (var pair in tagMaskIndices.OrderBy(item => item.Value))
                {
                    model.TagMasks.Add(new CatalogTagMask
                    {
                        TagCode = pair.Key,
                    });
                }
            }

            private static bool HasModifier(object row)
            {
                if (TryGetMemberValue(row, "HasModifier", out var hasModifier))
                    return Convert.ToBoolean(hasModifier);

                return GetInt(row, "ModifierAttributeSetCode") > 0
                       && GetInt(row, "ModifierAttributeCode") > 0;
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

        private struct CatalogTagMask
        {
            public int TagCode;
        }
    }

    internal sealed class RuntimeDefinitionGluePhase : GasCodeGenPhaseBase
    {
        public override string PhaseName => "RuntimeDefinitionGlue";

        public override IReadOnlyList<string> OutputFileNames { get; } = new[]
        {
            "Runtime/RuntimeDefinitionGlue.gen.cs",
            "Runtime/RuntimeAbilityActivation.gen.cs",
            "Runtime/RuntimeEffectInstant.gen.cs",
            "Runtime/RuntimeActiveEffect.gen.cs",
            "Runtime/RuntimeSystemRegistration.gen.cs",
        };

        public override bool RequiresRows => false;

        public override void Execute(GasCodeGenContext context, GasCodeGenManifest manifest)
        {
            var gluePath = GetOutputPath(context, OutputFileNames[0]);
            var systemPath = GetOutputPath(context, OutputFileNames[1]);
            var instantEffectPath = GetOutputPath(context, OutputFileNames[2]);
            var activeEffectPath = GetOutputPath(context, OutputFileNames[3]);
            var registrationPath = GetOutputPath(context, OutputFileNames[4]);

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

            WriteRuntimeAbilityActivationSystem(context, systemPath);
            WriteRuntimeEffectInstantSystems(context, instantEffectPath);
            WriteRuntimeActiveEffectSystems(context, activeEffectPath);
            WriteRuntimeSystemRegistration(context, registrationPath);

            AddRuntimeManifest(manifest, PhaseName, gluePath);
            AddRuntimeManifest(manifest, PhaseName, systemPath);
            AddRuntimeManifest(manifest, PhaseName, instantEffectPath);
            AddRuntimeManifest(manifest, PhaseName, activeEffectPath);
            AddRuntimeManifest(manifest, PhaseName, registrationPath);
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
            writer.WriteLine("public static int WriteGECommandSeeds(");
            writer.Indent++;
            writer.WriteLine("ref GASDefinitionCatalogBlob catalog,");
            writer.WriteLine("in AbilityActivationPlanRecord plan,");
            writer.WriteLine("int contextId,");
            writer.WriteLine("int parentContextId,");
            writer.WriteLine("ref NativeList<GECommandSeedRecord> seeds)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (!plan.Succeeded)");
            writer.Indent++;
            writer.WriteLine("return 0;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var count = 0;");
            writer.WriteLine("count += TryAppendSeed(ref catalog, in plan, GASGESeedKind.Cost, GEEffectCommandSource.Ability, plan.CostGameplayEffectCode, contextId, parentContextId, ref seeds) ? 1 : 0;");
            writer.WriteLine("count += TryAppendSeed(ref catalog, in plan, GASGESeedKind.Cooldown, GEEffectCommandSource.Ability, plan.CooldownGameplayEffectCode, contextId, parentContextId, ref seeds) ? 1 : 0;");
            writer.WriteLine("count += TryAppendSeed(ref catalog, in plan, GASGESeedKind.Primary, GEEffectCommandSource.Ability, plan.PrimaryGameplayEffectCode, contextId, parentContextId, ref seeds) ? 1 : 0;");
            writer.WriteLine("count += TryAppendSeed(ref catalog, in plan, GASGESeedKind.Secondary, GEEffectCommandSource.Ability, plan.SecondaryGameplayEffectCode, contextId, parentContextId, ref seeds) ? 1 : 0;");
            writer.WriteLine("return count;");
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
            writer.WriteLine("private static bool TryAppendSeed(");
            writer.Indent++;
            writer.WriteLine("ref GASDefinitionCatalogBlob catalog,");
            writer.WriteLine("in AbilityActivationPlanRecord plan,");
            writer.WriteLine("int seedKind,");
            writer.WriteLine("GEEffectCommandSource source,");
            writer.WriteLine("int gameplayEffectCode,");
            writer.WriteLine("int contextId,");
            writer.WriteLine("int parentContextId,");
            writer.WriteLine("ref NativeList<GECommandSeedRecord> seeds)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
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
            writer.WriteLine("|| gameplayEffect.RemoveGameplayEffectTagMaskIndex >= 0");
            writer.WriteLine("|| gameplayEffect.GrantedAbilityCount > 0");
            writer.WriteLine("|| gameplayEffect.ModifierCount == 0)");
            writer.Indent--;
            writer.Indent++;
            writer.WriteLine("flags |= GASGECommandSeedFlags.ActiveMutation;");
            writer.Indent--;
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("seeds.Add(new GECommandSeedRecord");
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
            writer.WriteLine("});");
            writer.WriteLine("return failureReason == GASFailureReasonCodes.None;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteRuntimeAbilityActivationSystem(GasCodeGenContext context, string path)
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

            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || !EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var seeds = new NativeList<GECommandSeedRecord>(Allocator.TempJob);
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
                EndRequestLookup = SystemAPI.GetComponentLookup<AbilityEndRequestComponent>(),
                DestroyOnCleanupLookup = SystemAPI.GetComponentLookup<AbilityDestroyOnCleanupComponent>(isReadOnly: true),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(),
                SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: true),
                EventBusLookup = SystemAPI.GetComponentLookup<GameplayEventBusComponent>(),
                GameplayEventLookup = SystemAPI.GetBufferLookup<GameplayEventBusEventBuffer>(),
                Catalog = catalogComponent.Catalog,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
                Seeds = seeds,
            }.Schedule(_query, state.Dependency);
            state.Dependency = seeds.Dispose(state.Dependency);
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
            public ComponentLookup<AbilityEndRequestComponent> EndRequestLookup;
            [ReadOnly] public ComponentLookup<AbilityDestroyOnCleanupComponent> DestroyOnCleanupLookup;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            [ReadOnly] public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;
            public ComponentLookup<GameplayEventBusComponent> EventBusLookup;
            public BufferLookup<GameplayEventBusEventBuffer> GameplayEventLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public int Frame;
            public NativeList<GECommandSeedRecord> Seeds;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Catalog.IsCreated
                    || StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !CommandLookup.HasBuffer(StreamEntity)
                    || !SetByCallerLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var abilities = chunk.GetNativeArray(EntityTypeHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);
                var commitRequests = chunk.GetNativeArray(ref CommitRequestTypeHandle);
                var commitRequestMask = chunk.GetEnabledMask(ref CommitRequestTypeHandle);
                ref var catalog = ref Catalog.Value;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var ability = abilities[entityIndex];
                    var state = states[entityIndex];
                    var commitRequest = commitRequests[entityIndex];
                    ApplyAbilityActivationCommitRecord(
                        ability,
                        commitRequest.TargetAsc,
                        ref catalog,
                        ref state);
                    states[entityIndex] = state;
                    commitRequestMask[entityIndex] = false;
                }
            }

            private bool ApplyAbilityActivationCommitRecord(
                Entity ability,
                Entity requestedTarget,
                ref GASDefinitionCatalogBlob catalog,
                ref AbilityStateComponent state)
            {
                var resolvedTarget = ResolveMainTarget(requestedTarget, state.Owner);
                Seeds.Clear();

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
                    if (CanCompleteAutoEndOnCommitDirectly(ability))
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
                            sourceAbility: ability,
                            sourceAbilityCode: nextRuntime.Code);
                    }
                }

                state = nextRuntime;
                return true;
            }

            private bool CanCompleteAutoEndOnCommitDirectly(Entity ability)
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

                return !EndRequestLookup.HasComponent(ability)
                       || !EndRequestLookup.IsComponentEnabled(ability);
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
                EnqueueGameplayEvent(new GameplayEventBusEventBuffer
                {
                    Type = type,
                    SourceAsc = state.Owner,
                    TargetAsc = state.Owner,
                    SourceAbility = ability,
                    RelatedAbility = ability,
                    EventCode = state.Code,
                    ReasonCode = (int)EAbilityLifecycleReason.ActivationCompleted,
                    RelatedAbilityCode = state.Code,
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

                GASGeneratedRuntimeDefinitionResolver.WriteGECommandSeeds(
                    ref catalog,
                    in plan,
                    contextId: 0,
                    parentContextId: 0,
                    ref Seeds);
                for (var i = 0; i < Seeds.Length; i++)
                {
                    var seed = Seeds[i];
                    if (seed.FailureReasonCode != GASFailureReasonCodes.None)
                        continue;
                    AppendEffectCommand(ToEffectCommand(in seed));
                }

                nextRuntime.Phase = EAbilityPhase.Active;
                nextRuntime.Timer = 0f;
                nextRuntime.RemainingFrame = -1;
                return true;
            }

            private void AppendEffectCommand(in GEEffectCommandBuffer command)
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !CommandLookup.HasBuffer(StreamEntity)
                    || !SetByCallerLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                var resolved = PrepareCommand(
                    ref stream,
                    SetByCallerLookup[StreamEntity].Length,
                    in command);
                CommandLookup[StreamEntity].Add(resolved);
                StreamLookup[StreamEntity] = stream;
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
                Entity sourceAbility = default,
                Entity sourceEffect = default,
                int sourceAbilityCode = 0)
            {
                if (!EndRequestLookup.HasComponent(ability)
                    || EndRequestLookup.IsComponentEnabled(ability))
                {
                    return;
                }

                EndRequestLookup[ability] = new AbilityEndRequestComponent
                {
                    Reason = reason,
                    SourceAbility = sourceAbility,
                    SourceEffect = sourceEffect,
                    SourceAbilityCode = sourceAbilityCode,
                };
                EndRequestLookup.SetComponentEnabled(ability, true);
                EnqueueGameplayEvent(new GameplayEventBusEventBuffer
                {
                    Type = EGameplayEventType.AbilityEndRequested,
                    SourceAbility = ability,
                    GameplayEffect = sourceEffect,
                    RelatedAbility = sourceAbility,
                    ReasonCode = (int)reason,
                    RelatedAbilityCode = sourceAbilityCode,
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

            private void EnqueueGameplayEvent(GameplayEventBusEventBuffer evt)
            {
                if (EventBusEntity == Entity.Null || !GameplayEventLookup.HasBuffer(EventBusEntity))
                    return;

                evt.Frame = Frame;
                if (EventBusLookup.HasComponent(EventBusEntity))
                {
                    var eventBus = EventBusLookup[EventBusEntity];
                    evt.Sequence = eventBus.NextSequence;
                    eventBus.NextSequence++;
                    EventBusLookup[EventBusEntity] = eventBus;
                }
                else
                {
                    evt.Sequence = 0;
                }

                GameplayEventLookup[EventBusEntity].Add(evt);
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

        private static void WriteRuntimeEffectInstantSystems(GasCodeGenContext context, string path)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using GAS.Runtime;");
            writer.WriteLine("using Unity.Burst;");
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
            writer.WriteLine("[UpdateBefore(typeof(GASActiveEffectMutationApplySystem))]");
            writer.WriteLine("public partial struct GEEffectSpecBuildSystem : ISystem");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public void OnCreate(ref SystemState state)");
            writer.WriteLine("{");
            writer.Indent++;
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
            writer.WriteLine("var em = state.EntityManager;");
            writer.WriteLine("var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();");
            writer.WriteLine("if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))");
            writer.Indent++;
            writer.WriteLine("return;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("state.Dependency = new InstantSpecBuildJob");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),");
            writer.WriteLine("CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: true),");
            writer.WriteLine("SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(),");
            writer.WriteLine("SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(),");
            writer.WriteLine("EntityStorageInfoLookup = SystemAPI.GetEntityStorageInfoLookup(),");
            writer.WriteLine("DestroyingLookup = SystemAPI.GetComponentLookup<ASCDestroyingComponent>(isReadOnly: true),");
            writer.WriteLine("Catalog = catalogComponent.Catalog,");
            writer.WriteLine("StreamEntity = streamEntity,");
            writer.Indent--;
            writer.WriteLine("}.Schedule(state.Dependency);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("[BurstCompile]");
            writer.WriteLine("private struct InstantSpecBuildJob : IJob");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;");
            writer.WriteLine("[ReadOnly] public BufferLookup<GEEffectCommandBuffer> CommandLookup;");
            writer.WriteLine("public BufferLookup<GEEffectSpecBuffer> SpecLookup;");
            writer.WriteLine("public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;");
            writer.WriteLine("[ReadOnly] public EntityStorageInfoLookup EntityStorageInfoLookup;");
            writer.WriteLine("[ReadOnly] public ComponentLookup<ASCDestroyingComponent> DestroyingLookup;");
            writer.WriteLine("[ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;");
            writer.WriteLine("public Entity StreamEntity;");
            writer.WriteLine("");
            writer.WriteLine("public void Execute()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (!Catalog.IsCreated");
            writer.Indent++;
            writer.WriteLine("|| StreamEntity == Entity.Null");
            writer.WriteLine("|| !StreamLookup.HasComponent(StreamEntity)");
            writer.WriteLine("|| !CommandLookup.HasBuffer(StreamEntity)");
            writer.WriteLine("|| !SpecLookup.HasBuffer(StreamEntity)");
            writer.WriteLine("|| !SetByCallerLookup.HasBuffer(StreamEntity))");
            writer.Indent--;
            writer.Indent++;
            writer.WriteLine("return;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var stream = StreamLookup[StreamEntity];");
            writer.WriteLine("var commands = CommandLookup[StreamEntity];");
            writer.WriteLine("var specs = SpecLookup[StreamEntity];");
            writer.WriteLine("var setByCallerValues = SetByCallerLookup[StreamEntity];");
            writer.WriteLine("ref var catalog = ref Catalog.Value;");
            writer.WriteLine("var start = ClampCursor(stream.SpecBuildCommandCursor, commands.Length);");
            writer.WriteLine("for (var i = start; i < commands.Length; i++)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("var command = commands[i];");
            writer.WriteLine("if (command.Kind != GEEffectCommandKind.Instant)");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("if (!CanBuildInstantSpec(ref catalog, in command, EntityStorageInfoLookup, DestroyingLookup, out var gameplayEffectIndex))");
            writer.Indent++;
            writer.WriteLine("continue;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("specs.Add(new GEEffectSpecBuffer");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("Sequence = Allocate(ref stream.NextSpecSequence),");
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
            writer.WriteLine("SetByCallerStart = command.SetByCallerStart,");
            writer.WriteLine("SetByCallerCount = command.SetByCallerCount,");
            writer.WriteLine("Flags = gameplayEffectIndex,");
            writer.Indent--;
            writer.WriteLine("});");
            writer.WriteLine("AssignSpecSequence(setByCallerValues, command.SetByCallerStart, command.SetByCallerCount, command.Sequence, specs[specs.Length - 1].Sequence);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("stream.SpecBuildCommandCursor = commands.Length;");
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
            writer.WriteLine("return gameplayEffect.DurationFrames <= 0");
            writer.Indent++;
            writer.WriteLine("&& gameplayEffect.PeriodFrames <= 0");
            writer.WriteLine("&& gameplayEffect.StackLimitCount <= 0");
            writer.WriteLine("&& gameplayEffect.GrantedTagMaskIndex < 0");
            writer.WriteLine("&& gameplayEffect.RemoveGameplayEffectTagMaskIndex < 0");
            writer.WriteLine("&& gameplayEffect.GrantedAbilityCount == 0");
            writer.WriteLine("&& (gameplayEffect.ModifierCount > 0");
            writer.WriteLine("    || gameplayEffect.GameplayCueCode > 0);");
            writer.Indent--;
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
            WriteGeneratedEffectSharedHelpers(writer);
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteGeneratedAttributeDeltaApplySystem(IndentedWriter writer)
        {
            writer.WriteLine("[UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]");
            writer.WriteLine("[UpdateAfter(typeof(GASActiveEffectMutationApplySystem))]");
            writer.WriteLine("[UpdateAfter(typeof(GEExecutionCalculationOutputModifierSystem))]");
            writer.WriteLine("[UpdateBefore(typeof(GameplayFactProjectionSystem))]");
            writer.WriteLine("public partial struct GASAttributeSetReduceApplySystem : ISystem");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public void OnCreate(ref SystemState state)");
            writer.WriteLine("{");
            writer.Indent++;
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
            writer.WriteLine("var em = state.EntityManager;");
            writer.WriteLine("var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();");
            writer.WriteLine("if (!EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))");
            writer.Indent++;
            writer.WriteLine("return;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("state.Dependency = new AttributeSetReduceApplyJob");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),");
            writer.WriteLine("SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(),");
            writer.WriteLine("DeltaLookup = SystemAPI.GetBufferLookup<AttributeModifierBuffer>(),");
            writer.WriteLine("SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: true),");
            writer.WriteLine("AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(),");
            writer.WriteLine("DestroyingLookup = SystemAPI.GetComponentLookup<ASCDestroyingComponent>(isReadOnly: true),");
            writer.WriteLine("Catalog = catalogComponent.Catalog,");
            writer.WriteLine("StreamEntity = streamEntity,");
            writer.Indent--;
            writer.WriteLine("}.Schedule(state.Dependency);");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("[BurstCompile]");
            writer.WriteLine("private struct AttributeSetReduceApplyJob : IJob");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;");
            writer.WriteLine("public BufferLookup<GEEffectSpecBuffer> SpecLookup;");
            writer.WriteLine("public BufferLookup<AttributeModifierBuffer> DeltaLookup;");
            writer.WriteLine("[ReadOnly] public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;");
            writer.WriteLine("public BufferLookup<AttributeValueBuffer> AttributeLookup;");
            writer.WriteLine("[ReadOnly] public ComponentLookup<ASCDestroyingComponent> DestroyingLookup;");
            writer.WriteLine("[ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;");
            writer.WriteLine("public Entity StreamEntity;");
            writer.WriteLine("");
            writer.WriteLine("public void Execute()");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (!Catalog.IsCreated");
            writer.Indent++;
            writer.WriteLine("|| StreamEntity == Entity.Null");
            writer.WriteLine("|| !StreamLookup.HasComponent(StreamEntity)");
            writer.WriteLine("|| !SpecLookup.HasBuffer(StreamEntity)");
            writer.WriteLine("|| !DeltaLookup.HasBuffer(StreamEntity)");
            writer.WriteLine("|| !SetByCallerLookup.HasBuffer(StreamEntity))");
            writer.Indent--;
            writer.Indent++;
            writer.WriteLine("return;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var stream = StreamLookup[StreamEntity];");
            writer.WriteLine("var specs = SpecLookup[StreamEntity];");
            writer.WriteLine("var deltas = DeltaLookup[StreamEntity];");
            writer.WriteLine("var setByCallerValues = SetByCallerLookup[StreamEntity];");
            writer.WriteLine("ref var catalog = ref Catalog.Value;");
            writer.WriteLine("var start = ClampCursor(stream.DeltaApplySpecCursor, specs.Length);");
            writer.WriteLine("for (var i = start; i < specs.Length; i++)");
            writer.Indent++;
            writer.WriteLine("ApplySpec(ref stream, ref catalog, specs[i], setByCallerValues, deltas, AttributeLookup, DestroyingLookup);");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("stream.DeltaApplySpecCursor = specs.Length;");
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
            writer.WriteLine("in GEEffectSpecBuffer spec,");
            writer.WriteLine("DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,");
            writer.WriteLine("DynamicBuffer<AttributeModifierBuffer> deltas,");
            writer.WriteLine("BufferLookup<AttributeValueBuffer> attributeLookup,");
            writer.WriteLine("ComponentLookup<ASCDestroyingComponent> destroyingLookup)");
            writer.Indent--;
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("if (spec.TargetAsc == Entity.Null");
            writer.Indent++;
            writer.WriteLine("|| IsDestroyingAsc(destroyingLookup, spec.TargetAsc)");
            writer.WriteLine("|| !attributeLookup.HasBuffer(spec.TargetAsc)");
            writer.WriteLine("|| !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, spec.GameplayEffectCode, out var gameplayEffectIndex))");
            writer.Indent--;
            writer.Indent++;
            writer.WriteLine("return;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);");
            writer.WriteLine("var attributes = attributeLookup[spec.TargetAsc];");
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
            writer.WriteLine("deltas.Add(new AttributeModifierBuffer");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("Sequence = Allocate(ref stream.NextDeltaSequence),");
            writer.WriteLine("SourceCommandSequence = spec.SourceCommandSequence,");
            writer.WriteLine("SourceSpecSequence = spec.Sequence,");
            writer.WriteLine("Frame = spec.Frame,");
            writer.WriteLine("SourceAsc = spec.SourceAsc,");
            writer.WriteLine("TargetAsc = spec.TargetAsc,");
            writer.WriteLine("SourceAbility = spec.SourceAbility,");
            writer.WriteLine("SourceEffect = spec.SourceEffect,");
            writer.WriteLine("GameplayEffectCode = spec.GameplayEffectCode,");
            writer.WriteLine("ContextId = spec.ContextId,");
            writer.WriteLine("ParentContextId = spec.ParentContextId,");
            writer.WriteLine("AttrSetCode = modifier.AttributeSetCode,");
            writer.WriteLine("AttributeCode = modifier.AttributeCode,");
            writer.WriteLine("Op = modifier.Operation,");
            writer.WriteLine("ValueKind = AttributeDeltaValueKind.BaseValue,");
            writer.WriteLine("Magnitude = magnitude,");
            writer.WriteLine("OldValue = oldValue,");
            writer.WriteLine("NewValue = newValue,");
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

        private static void WriteRuntimeActiveEffectSystems(GasCodeGenContext context, string path)
        {
            File.WriteAllText(
                path,
                RuntimeActiveEffectSystemsTemplate.Replace("__ROOT_NAMESPACE__", context.RootNamespace));
        }

        private static void WriteRuntimeSystemRegistration(GasCodeGenContext context, string path)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteHeader(writer);
            writer.WriteLine("using GAS.Runtime;");
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine($"namespace {context.RootNamespace}");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static class GASGeneratedRuntimeSystemRegistration");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public static void Register(World world, GASSystemGroups groups)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("groups.CommandResolve.AddSystemToUpdateList(world.CreateSystem(typeof(AbilityCatalogCommitSystem)));");
            writer.WriteLine("groups.CoreSimulation.AddSystemToUpdateList(world.CreateSystem(typeof(GEEffectCommandCatalogNormalizeSystem)));");
            writer.WriteLine("groups.CoreSimulation.AddSystemToUpdateList(world.CreateSystem(typeof(GEEffectSpecBuildSystem)));");
            writer.WriteLine("groups.CoreSimulation.AddSystemToUpdateList(world.CreateSystem(typeof(GASActiveEffectMutationApplySystem)));");
            writer.WriteLine("groups.CoreSimulation.AddSystemToUpdateList(world.CreateSystem(typeof(GASAttributeSetReduceApplySystem)));");
            writer.WriteLine("groups.CoreSimulation.AddSystemToUpdateList(world.CreateSystem(typeof(GASActiveEffectPreTickSystem)));");
            writer.WriteLine("groups.CoreSimulation.AddSystemToUpdateList(world.CreateSystem(typeof(GASActiveEffectRemoveSystem)));");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private const string RuntimeActiveEffectSystemsTemplate = @"///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;
using Unity.Burst.Intrinsics;
using Unity.Collections;
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
                Catalog = catalogComponent.Catalog,
            }.Schedule(_query, state.Dependency);
        }

        private struct GEEffectCommandCatalogNormalizeJob : IJobChunk
        {
            public BufferTypeHandle<GEEffectCommandBuffer> CommandTypeHandle;
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
                for (var bufferIndex = 0; bufferIndex < commandBuffers.Length; bufferIndex++)
                {
                    var commands = commandBuffers[bufferIndex];
                    for (var i = 0; i < commands.Length; i++)
                    {
                        var command = commands[i];
                        if (GASGeneratedActiveEffectRuntime.TryNormalizeCommand(ref catalog, ref command))
                            commands[i] = command;
                    }
                }
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
        public void OnCreate(ref SystemState state)
        {
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
            var stream = em.GetComponentData<GEEffectCommandStreamComponent>(streamEntity);
            var commands = em.GetBuffer<GEEffectCommandBuffer>(streamEntity);
            var mutations = em.GetBuffer<ActiveEffectMutationBuffer>(streamEntity);
            var setByCallerValues = em.GetBuffer<GESetByCallerValueBuffer>(streamEntity);
            ref var catalog = ref catalogComponent.Catalog.Value;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var eventWriter = eventBusEntity != Entity.Null
                ? EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity)
                : default;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            try
            {
                var start = GASGeneratedActiveEffectRuntime.ClampCursor(stream.ActiveMutationCommandCursor, commands.Length);
                for (var i = start; i < commands.Length; i++)
                {
                    var command = commands[i];
                    if (command.Kind != GEEffectCommandKind.ActiveMutation)
                        continue;

                    GASGeneratedActiveEffectRuntime.TryApplyActiveMutation(
                        em,
                        ref stream,
                        ref catalog,
                        in command,
                        commands,
                        setByCallerValues,
                        mutations,
                        frame,
                        ref structuralEcb,
                        ref eventWriter);
                }

                stream.ActiveMutationCommandCursor = commands.Length;
                em.SetComponentData(streamEntity, stream);
            }
            finally
            {
                eventWriter.Dispose();
            }
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup), OrderFirst = true)]
    public partial struct GASActiveEffectPreTickSystem : ISystem
    {
        private EntityQuery _ownerQuery;

        public void OnCreate(ref SystemState state)
        {
            _ownerQuery = SystemAPI.QueryBuilder()
                .WithAll<ASCActiveEffectsComponent, ActiveGameplayEffectBuffer>()
                .Build();
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
            var tickJob = new GASGeneratedActiveEffectRuntime.GEActiveEffectPreTickJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                ActiveEffectsTypeHandle = SystemAPI.GetComponentTypeHandle<ASCActiveEffectsComponent>(isReadOnly: false),
                ActiveEffectSlotBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(isReadOnly: false),
                CleanupRecordLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectCleanupRecordBuffer>(isReadOnly: false),
                SetByCallerSnapshotLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectSetByCallerValueBuffer>(isReadOnly: false),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: false),
                ActiveModifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(isReadOnly: false),
                AttributeDirtyLookup = SystemAPI.GetComponentLookup<AttributeDirtyComponent>(isReadOnly: false),
                ActiveModifierPresentLookup =
                    SystemAPI.GetComponentLookup<AttributeActiveModifierPresentComponent>(isReadOnly: false),
                TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: false),
                TagFixedMaskLookup = SystemAPI.GetComponentLookup<TagFixedMaskComponent>(isReadOnly: true),
                TagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(isReadOnly: false),
                AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(isReadOnly: false),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: false),
                AbilityGrantedLookup = SystemAPI.GetComponentLookup<AbilityGrantedByEffectComponent>(isReadOnly: true),
                AbilityCancelRequestLookup =
                    SystemAPI.GetComponentLookup<AbilityCancelRequestComponent>(isReadOnly: false),
                AbilityDestroyOnCleanupLookup =
                    SystemAPI.GetComponentLookup<AbilityDestroyOnCleanupComponent>(isReadOnly: false),
                RemoveCommandBufferTypeHandle = SystemAPI.GetBufferTypeHandle<GERemoveCommandBuffer>(isReadOnly: false),
                RemovePendingLookup = SystemAPI.GetComponentLookup<GERemoveCommandPendingComponent>(isReadOnly: false),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),
                CommandSetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),
                MutationLookup = SystemAPI.GetBufferLookup<ActiveEffectMutationBuffer>(isReadOnly: false),
                GameplayEventBusLookup = SystemAPI.GetComponentLookup<GameplayEventBusComponent>(isReadOnly: false),
                GameplayEventLookup = SystemAPI.GetBufferLookup<GameplayEventBusEventBuffer>(isReadOnly: false),
                TagChangeEventLookup = SystemAPI.GetBufferLookup<TagChangeEventBuffer>(isReadOnly: false),
                StructuralEcb = structuralEcb,
                GrantedAbilityArchetype = GASRuntimeEntityArchetypes.GrantedAbility(em),
                Catalog = catalogComponent.Catalog,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
                ProcessTickRecords = true,
            };
            state.Dependency = tickJob.Schedule(_ownerQuery, state.Dependency);
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
            _removeCommandQuery = SystemAPI.QueryBuilder()
                .WithAll<
                    GERemoveCommandPendingComponent,
                    GERemoveCommandBuffer,
                    ASCActiveEffectsComponent,
                    ActiveGameplayEffectBuffer>()
                .Build();
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

            state.Dependency = new GASGeneratedActiveEffectRuntime.GEActiveEffectPreTickJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                ActiveEffectsTypeHandle = SystemAPI.GetComponentTypeHandle<ASCActiveEffectsComponent>(isReadOnly: false),
                ActiveEffectSlotBufferTypeHandle = SystemAPI.GetBufferTypeHandle<ActiveGameplayEffectBuffer>(isReadOnly: false),
                RemoveCommandBufferTypeHandle = SystemAPI.GetBufferTypeHandle<GERemoveCommandBuffer>(isReadOnly: false),
                CleanupRecordLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectCleanupRecordBuffer>(isReadOnly: false),
                SetByCallerSnapshotLookup =
                    SystemAPI.GetBufferLookup<ActiveGameplayEffectSetByCallerValueBuffer>(isReadOnly: false),
                AttributeLookup = SystemAPI.GetBufferLookup<AttributeValueBuffer>(isReadOnly: false),
                ActiveModifierLookup = SystemAPI.GetBufferLookup<AttributeActiveModifierBuffer>(isReadOnly: false),
                AttributeDirtyLookup = SystemAPI.GetComponentLookup<AttributeDirtyComponent>(isReadOnly: false),
                ActiveModifierPresentLookup =
                    SystemAPI.GetComponentLookup<AttributeActiveModifierPresentComponent>(isReadOnly: false),
                TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(isReadOnly: false),
                TagFixedMaskLookup = SystemAPI.GetComponentLookup<TagFixedMaskComponent>(isReadOnly: true),
                TagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(isReadOnly: false),
                AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(isReadOnly: false),
                AbilityStateLookup = SystemAPI.GetComponentLookup<AbilityStateComponent>(isReadOnly: false),
                AbilityGrantedLookup = SystemAPI.GetComponentLookup<AbilityGrantedByEffectComponent>(isReadOnly: true),
                AbilityCancelRequestLookup =
                    SystemAPI.GetComponentLookup<AbilityCancelRequestComponent>(isReadOnly: false),
                AbilityDestroyOnCleanupLookup =
                    SystemAPI.GetComponentLookup<AbilityDestroyOnCleanupComponent>(isReadOnly: false),
                RemovePendingLookup = SystemAPI.GetComponentLookup<GERemoveCommandPendingComponent>(isReadOnly: false),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),
                CommandSetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),
                MutationLookup = SystemAPI.GetBufferLookup<ActiveEffectMutationBuffer>(isReadOnly: false),
                GameplayEventBusLookup = SystemAPI.GetComponentLookup<GameplayEventBusComponent>(isReadOnly: false),
                GameplayEventLookup = SystemAPI.GetBufferLookup<GameplayEventBusEventBuffer>(isReadOnly: false),
                TagChangeEventLookup = SystemAPI.GetBufferLookup<TagChangeEventBuffer>(isReadOnly: false),
                StructuralEcb = structuralEcb,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
                ProcessExplicitRemoveCommands = true,
            }.Schedule(_removeCommandQuery, state.Dependency);
        }
    }

    internal static class GASGeneratedActiveEffectRuntime
    {
        public struct GEActiveEffectPreTickJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<ASCActiveEffectsComponent> ActiveEffectsTypeHandle;
            public BufferTypeHandle<ActiveGameplayEffectBuffer> ActiveEffectSlotBufferTypeHandle;
            public BufferTypeHandle<GERemoveCommandBuffer> RemoveCommandBufferTypeHandle;
            public BufferLookup<ActiveGameplayEffectCleanupRecordBuffer> CleanupRecordLookup;
            public BufferLookup<ActiveGameplayEffectSetByCallerValueBuffer> SetByCallerSnapshotLookup;
            public BufferLookup<AttributeValueBuffer> AttributeLookup;
            public BufferLookup<AttributeActiveModifierBuffer> ActiveModifierLookup;
            public ComponentLookup<AttributeDirtyComponent> AttributeDirtyLookup;
            public ComponentLookup<AttributeActiveModifierPresentComponent> ActiveModifierPresentLookup;
            public ComponentLookup<TagMaskComponent> TagMaskLookup;
            [ReadOnly] public ComponentLookup<TagFixedMaskComponent> TagFixedMaskLookup;
            public BufferLookup<TagTemporarySourceBuffer> TagSourceLookup;
            public BufferLookup<AbilitySlotBuffer> AbilitySlotLookup;
            [ReadOnly] public ComponentLookup<AbilityStateComponent> AbilityStateLookup;
            [ReadOnly] public ComponentLookup<AbilityGrantedByEffectComponent> AbilityGrantedLookup;
            public ComponentLookup<AbilityCancelRequestComponent> AbilityCancelRequestLookup;
            public ComponentLookup<AbilityDestroyOnCleanupComponent> AbilityDestroyOnCleanupLookup;
            public ComponentLookup<GERemoveCommandPendingComponent> RemovePendingLookup;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            public BufferLookup<GESetByCallerValueBuffer> CommandSetByCallerLookup;
            public BufferLookup<ActiveEffectMutationBuffer> MutationLookup;
            public ComponentLookup<GameplayEventBusComponent> GameplayEventBusLookup;
            public BufferLookup<GameplayEventBusEventBuffer> GameplayEventLookup;
            public BufferLookup<TagChangeEventBuffer> TagChangeEventLookup;
            public EntityCommandBuffer StructuralEcb;
            public EntityArchetype GrantedAbilityArchetype;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public int Frame;
            public bool ProcessTickRecords;
            public bool ProcessExplicitRemoveCommands;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if ((!ProcessTickRecords && !ProcessExplicitRemoveCommands)
                    || StreamEntity == Entity.Null
                    || !MutationLookup.HasBuffer(StreamEntity)
                    || (ProcessTickRecords && !Catalog.IsCreated))
                {
                    return;
                }

                var owners = chunk.GetNativeArray(EntityTypeHandle);
                var stores = chunk.GetNativeArray(ref ActiveEffectsTypeHandle);
                var slotBuffers = chunk.GetBufferAccessor(ref ActiveEffectSlotBufferTypeHandle);
                var mutations = MutationLookup[StreamEntity];
                var removeCommandBuffers = ProcessExplicitRemoveCommands
                    ? chunk.GetBufferAccessor(ref RemoveCommandBufferTypeHandle)
                    : default;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var owner = owners[entityIndex];
                    var store = stores[entityIndex];
                    var slots = slotBuffers[entityIndex];

                    if (ProcessTickRecords)
                    {
                        if (store.ChunkSkipMatchedSlotCount > 0
                            || store.ChunkSkipDuePeriodSlotCount > 0
                            || store.CleanupRecordCount > 0)
                        {
                            ref var catalog = ref Catalog.Value;
                            ProcessTickOwner(owner, slots, mutations, ref catalog, ref store);
                        }
                    }

                    if (ProcessExplicitRemoveCommands)
                    {
                        var removeCommands = removeCommandBuffers[entityIndex];
                        for (var commandIndex = 0; commandIndex < removeCommands.Length; commandIndex++)
                        {
                            RemoveMatchingOwnerLocalEffects(
                                owner,
                                slots,
                                removeCommands[commandIndex].GameplayEffectCode,
                                mutations,
                                ref store);
                        }

                        removeCommands.Clear();
                        if (RemovePendingLookup.HasComponent(owner))
                            RemovePendingLookup.SetComponentEnabled(owner, false);
                    }

                    ActiveEffectStore.RefreshChunkSkipIndexCounters(ref store, slots, Frame);
                    stores[entityIndex] = store;
                }
            }

            private void ProcessTickOwner(
                Entity owner,
                DynamicBuffer<ActiveGameplayEffectBuffer> slots,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations,
                ref GASDefinitionCatalogBlob catalog,
                ref ASCActiveEffectsComponent store)
            {
                if (!StreamLookup.HasComponent(StreamEntity))
                    return;

                for (var slotIndex = slots.Length - 1; slotIndex >= 0; slotIndex--)
                {
                    var slot = slots[slotIndex];
                    var actionFlags = ActiveEffectStore.CreateTickActionFlags(in slot, Frame);
                    if (actionFlags == (int)ActiveEffectTickActionFlags.None)
                        continue;

                    if ((actionFlags & (int)ActiveEffectTickActionFlags.Period) != 0)
                    {
                        EmitPeriodCommand(ref catalog, in slot, owner);
                        slot.LastPeriodFrame = Frame;
                        slots[slotIndex] = slot;
                        mutations.Add(new ActiveEffectMutationBuffer
                        {
                            Sequence = slot.Sequence,
                            Frame = Frame,
                            Kind = ActiveEffectMutationKind.PeriodTick,
                            ActiveEffect = Entity.Null,
                            SourceAsc = slot.SourceAsc,
                            TargetAsc = owner,
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
                        HandleDurationExpired(owner, slots, slotIndex, ref catalog, mutations, ref store);
                }
            }

            private void RemoveMatchingOwnerLocalEffects(
                Entity owner,
                DynamicBuffer<ActiveGameplayEffectBuffer> slots,
                int gameplayEffectCode,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations,
                ref ASCActiveEffectsComponent store)
            {
                if (owner == Entity.Null)
                    return;

                for (var i = slots.Length - 1; i >= 0; i--)
                {
                    var slot = slots[i];
                    if (gameplayEffectCode > 0 && slot.GameplayEffectCode != gameplayEffectCode)
                        continue;

                    RemoveSlotAt(owner, slots, i, mutations, ref store);
                }
            }

            private void HandleDurationExpired(
                Entity owner,
                DynamicBuffer<ActiveGameplayEffectBuffer> slots,
                int slotIndex,
                ref GASDefinitionCatalogBlob catalog,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations,
                ref ASCActiveEffectsComponent store)
            {
                if ((uint)slotIndex >= (uint)slots.Length)
                    return;

                var slot = slots[slotIndex];
                if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
                {
                    RemoveSlotAt(owner, slots, slotIndex, mutations, ref store);
                    return;
                }

                ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                var expirationPolicy = (EffectExpirationPolicy)gameplayEffect.EffectExpirationPolicy;
                if (expirationPolicy == EffectExpirationPolicy.RefreshDuration)
                {
                    RefreshSlotDuration(ref slot, Frame, resetPeriod: true);
                    slots[slotIndex] = slot;
                    mutations.Add(CreateRefreshMutation(in slot, Frame));
                    return;
                }

                if (expirationPolicy == EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration
                    && slot.StackCount > 1)
                {
                    slot.StackCount--;
                    RefreshSlotDuration(ref slot, Frame, resetPeriod: true);
                    slots[slotIndex] = slot;
                    RebuildActiveModifiersForSlot(ref catalog, in gameplayEffect, in slot);
                    mutations.Add(CreateStackMutation(in slot, Frame));
                    EnqueueStackCountChangedEvent(in slot);
                    return;
                }

                RemoveSlotAt(owner, slots, slotIndex, mutations, ref store);
            }

            private void RemoveSlotAt(
                Entity owner,
                DynamicBuffer<ActiveGameplayEffectBuffer> slots,
                int slotIndex,
                DynamicBuffer<ActiveEffectMutationBuffer> mutations,
                ref ASCActiveEffectsComponent store)
            {
                if ((uint)slotIndex >= (uint)slots.Length)
                    return;

                var slot = slots[slotIndex];
                var activeModifierCount = CountActiveModifiersForSlot(owner, slot.Sequence, slot.GameplayEffectCode);
                RemoveActiveModifiersForSlot(owner, slot.Sequence, slot.GameplayEffectCode);
                RemoveGrantedTagsForSlot(owner, slot.Sequence, slot.GameplayEffectCode);
                RemoveGrantedAbilitiesForSlot(owner, slot.Sequence, slot.GameplayEffectCode);
                RemoveSetByCallerSnapshotForSlot(owner, slot.Sequence, slot.GameplayEffectCode);
                RecordCleanup(owner, in slot, activeModifierCount, ref store);
                mutations.Add(new ActiveEffectMutationBuffer
                {
                    Sequence = slot.Sequence,
                    Frame = Frame,
                    Kind = ActiveEffectMutationKind.Remove,
                    ActiveEffect = Entity.Null,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = owner,
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
                slots.RemoveAt(slotIndex);
            }

            private int RebuildActiveModifiersForSlot(
                ref GASDefinitionCatalogBlob catalog,
                in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
                in ActiveGameplayEffectBuffer slot)
            {
                if (gameplayEffect.ModifierCount <= 0
                    || slot.TargetAsc == Entity.Null
                    || !AttributeLookup.HasBuffer(slot.TargetAsc)
                    || !ActiveModifierLookup.HasBuffer(slot.TargetAsc))
                {
                    return 0;
                }

                RemoveActiveModifiersForSlot(slot.TargetAsc, slot.Sequence, slot.GameplayEffectCode);
                var attributes = AttributeLookup[slot.TargetAsc];
                var activeModifiers = ActiveModifierLookup[slot.TargetAsc];
                var setByCallerSnapshot = SetByCallerSnapshotLookup.HasBuffer(slot.TargetAsc)
                    ? SetByCallerSnapshotLookup[slot.TargetAsc]
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

                    var context = BuildMagnitudeContextFromSlot(in slot, setByCallerSnapshot, in modifier);
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
                    MarkActiveModifierAdded(slot.TargetAsc);
                    MarkCurrentValueDirty(attributes, slot.TargetAsc, modifier.AttributeSetCode, modifier.AttributeCode);
                    added++;
                }

                return added;
            }

            private MagnitudeEvalContext BuildMagnitudeContextFromSlot(
                in ActiveGameplayEffectBuffer slot,
                DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> setByCallerSnapshot,
                in GASCatalogModifierDefinitionBlob modifier)
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

                if (modifier.MagnitudeSource == EMagnitudeSource.SourceAttribute
                    && TryReadAttributeValue(slot.SourceAsc, in modifier, out var sourceValue))
                {
                    context.HasSourceAttributeValue = 1;
                    context.SourceAttributeValue = sourceValue;
                }

                if (modifier.MagnitudeSource == EMagnitudeSource.TargetAttribute
                    && TryReadAttributeValue(slot.TargetAsc, in modifier, out var targetValue))
                {
                    context.HasTargetAttributeValue = 1;
                    context.TargetAttributeValue = targetValue;
                }

                return context;
            }

            private bool TryReadAttributeValue(
                Entity owner,
                in GASCatalogModifierDefinitionBlob modifier,
                out float value)
            {
                value = 0f;
                if (owner == Entity.Null || !AttributeLookup.HasBuffer(owner))
                    return false;

                var attrSetCode = modifier.CaptureAttributeSetCode != 0
                    ? modifier.CaptureAttributeSetCode
                    : modifier.AttributeSetCode;
                var attributeCode = modifier.CaptureAttributeCode != 0
                    ? modifier.CaptureAttributeCode
                    : modifier.AttributeCode;
                var attributes = AttributeLookup[owner];
                var attrIndex = attributes.IndexOfAttribute(attrSetCode, attributeCode);
                if (attrIndex < 0)
                    return false;

                value = attributes[attrIndex].CurrentValue;
                return true;
            }

            private void RemoveActiveModifiersForSlot(
                Entity owner,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (owner == Entity.Null || !ActiveModifierLookup.HasBuffer(owner))
                    return;

                var modifiers = ActiveModifierLookup[owner];
                var hasAttributes = AttributeLookup.HasBuffer(owner);
                var attributes = hasAttributes ? AttributeLookup[owner] : default;
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
                        MarkCurrentValueDirty(attributes, owner, modifier.AttrSetCode, modifier.AttributeCode);
                }

                RefreshActiveModifierPresence(owner, modifiers);
            }

            private int CountActiveModifiersForSlot(
                Entity owner,
                int sourceSequence,
                int gameplayEffectCode)
            {
                if (owner == Entity.Null || !ActiveModifierLookup.HasBuffer(owner))
                    return 0;

                var count = 0;
                var modifiers = ActiveModifierLookup[owner];
                for (var i = 0; i < modifiers.Length; i++)
                {
                    var modifier = modifiers[i];
                    if (modifier.SourceSequence == sourceSequence
                        && modifier.SourceGameplayEffectCode == gameplayEffectCode)
                    {
                        count++;
                    }
                }

                return count;
            }

            private void RemoveGrantedTagsForSlot(
                Entity owner,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (owner == Entity.Null || !TagSourceLookup.HasBuffer(owner))
                    return;

                var sources = TagSourceLookup[owner];
                for (var i = sources.Length - 1; i >= 0; i--)
                {
                    var source = sources[i];
                    if (source.SourceSequence != slotSequence
                        || source.SourceGameplayEffectCode != gameplayEffectCode)
                    {
                        continue;
                    }

                    sources.RemoveAt(i);
                    var removedFromMask = RemoveTagIndexFromEffectiveMaskIfUnreferenced(owner, source.TagIndex);
                    if (removedFromMask)
                    {
                        EnqueueTagChangeEvent(new TagChangeEventBuffer
                        {
                            ASC = owner,
                            TagIndex = source.TagIndex,
                            Added = false,
                        });
                    }
                }
            }

            private bool RemoveTagIndexFromEffectiveMaskIfUnreferenced(Entity owner, int tagIndex)
            {
                if (owner == Entity.Null || !TagMaskLookup.HasComponent(owner))
                    return false;
                if (TagFixedMaskLookup.HasComponent(owner)
                    && TagFixedMaskLookup[owner].Mask.HasTag(tagIndex))
                {
                    return false;
                }
                if (HasAnyTemporarySourceForTag(owner, tagIndex))
                    return false;

                var mask = TagMaskLookup[owner];
                if (!mask.HasTag(tagIndex))
                    return false;
                mask.RemoveTag(tagIndex);
                TagMaskLookup[owner] = mask;
                return true;
            }

            private bool HasAnyTemporarySourceForTag(Entity owner, int tagIndex)
            {
                if (owner == Entity.Null || !TagSourceLookup.HasBuffer(owner))
                    return false;

                var sources = TagSourceLookup[owner];
                for (var i = 0; i < sources.Length; i++)
                {
                    if (sources[i].TagIndex == tagIndex)
                        return true;
                }

                return false;
            }

            private void RemoveGrantedAbilitiesForSlot(
                Entity owner,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (owner == Entity.Null || !AbilitySlotLookup.HasBuffer(owner))
                    return;

                var grantedAbilities = AbilitySlotLookup[owner];
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
                    RequestAbilityCancel(ability, runtime);
                    EnableDestroyOnCleanup(ability);
                    return;
                }

                StructuralEcb.DestroyEntity(ability);
            }

            private void RequestAbilityCancel(Entity ability, in AbilityStateComponent runtime)
            {
                if (!AbilityCancelRequestLookup.HasComponent(ability)
                    || AbilityCancelRequestLookup.IsComponentEnabled(ability))
                {
                    return;
                }

                AbilityCancelRequestLookup[ability] = new AbilityCancelRequestComponent
                {
                    Reason = EAbilityLifecycleReason.GrantedEffectRemoved,
                    SourceAbility = Entity.Null,
                    SourceEffect = Entity.Null,
                    SourceAbilityCode = 0,
                };
                AbilityCancelRequestLookup.SetComponentEnabled(ability, true);
                EnqueueGameplayEvent(new GameplayEventBusEventBuffer
                {
                    Type = EGameplayEventType.AbilityCancelRequested,
                    SourceAsc = runtime.Owner,
                    TargetAsc = runtime.Owner,
                    SourceAbility = ability,
                    GameplayEffect = Entity.Null,
                    RelatedAbility = Entity.Null,
                    EventCode = runtime.Code,
                    ReasonCode = (int)EAbilityLifecycleReason.GrantedEffectRemoved,
                });
            }

            private void EnableDestroyOnCleanup(Entity ability)
            {
                if (AbilityDestroyOnCleanupLookup.HasComponent(ability)
                    && !AbilityDestroyOnCleanupLookup.IsComponentEnabled(ability))
                {
                    AbilityDestroyOnCleanupLookup.SetComponentEnabled(ability, true);
                }
            }

            private void RemoveSetByCallerSnapshotForSlot(
                Entity owner,
                int slotSequence,
                int gameplayEffectCode)
            {
                if (owner == Entity.Null || !SetByCallerSnapshotLookup.HasBuffer(owner))
                    return;

                GASGeneratedActiveEffectRuntime.RemoveSetByCallerSnapshotForSlot(
                    SetByCallerSnapshotLookup[owner],
                    slotSequence,
                    gameplayEffectCode);
            }

            private void RecordCleanup(
                Entity owner,
                in ActiveGameplayEffectBuffer slot,
                int activeModifierCount,
                ref ASCActiveEffectsComponent store)
            {
                if (owner == Entity.Null || !CleanupRecordLookup.HasBuffer(owner))
                    return;

                var records = CleanupRecordLookup[owner];
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
                    TargetAsc = owner,
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

                store.LastCleanupFrame = Frame;
                store.CleanupRecordCount = records.Length;
            }

            private void EmitPeriodCommand(
                ref GASDefinitionCatalogBlob catalog,
                in ActiveGameplayEffectBuffer slot,
                Entity owner)
            {
                if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
                    return;

                ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
                if (gameplayEffect.PeriodGameplayEffectCode <= 0
                    || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, gameplayEffect.PeriodGameplayEffectCode, out var periodGameplayEffectIndex)
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !CommandLookup.HasBuffer(StreamEntity)
                    || !CommandSetByCallerLookup.HasBuffer(StreamEntity))
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
                    TargetAsc = owner,
                    SourceAbility = slot.SourceAbility,
                    SourceEffect = Entity.Null,
                    Instigator = slot.Instigator != Entity.Null ? slot.Instigator : slot.SourceAsc,
                    Causer = slot.Causer != Entity.Null ? slot.Causer : slot.SourceAbility,
                    GameplayEffectCode = gameplayEffect.PeriodGameplayEffectCode,
                    Level = slot.Level,
                    DurationFrameOverride = durationFrame,
                    ParentContextId = slot.ContextId,
                    TargetDataKind = slot.SourceAsc == owner ? ETargetDataKind.Self : ETargetDataKind.Entity,
                    Flags = kind == GEEffectCommandKind.ActiveMutation ? GASGECommandSeedFlags.ActiveMutation : GASGECommandSeedFlags.None,
                };

                var commandSetByCallerValues = CommandSetByCallerLookup[StreamEntity];
                var sourceSetByCallerValues = SetByCallerSnapshotLookup.HasBuffer(owner)
                    ? SetByCallerSnapshotLookup[owner]
                    : default;
                var setByCallerCount = CountSetByCallerValues(sourceSetByCallerValues, slot.Sequence, slot.GameplayEffectCode);
                var stream = StreamLookup[StreamEntity];
                var resolved = PrepareCommand(ref stream, commandSetByCallerValues, in command, setByCallerCount, Frame);
                CopySetByCallerValues(
                    commandSetByCallerValues,
                    sourceSetByCallerValues,
                    slot.Sequence,
                    slot.GameplayEffectCode,
                    resolved.Sequence);
                CommandLookup[StreamEntity].Add(resolved);
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

            private void MarkActiveModifierAdded(Entity asc)
            {
                if (asc == Entity.Null
                    || !ActiveModifierPresentLookup.HasComponent(asc)
                    || ActiveModifierPresentLookup.IsComponentEnabled(asc))
                {
                    return;
                }

                ActiveModifierPresentLookup.SetComponentEnabled(asc, true);
            }

            private void RefreshActiveModifierPresence(
                Entity asc,
                DynamicBuffer<AttributeActiveModifierBuffer> modifiers)
            {
                if (asc == Entity.Null || !ActiveModifierPresentLookup.HasComponent(asc))
                    return;

                ActiveModifierPresentLookup.SetComponentEnabled(asc, modifiers.Length > 0);
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
                if (asc != Entity.Null
                    && AttributeDirtyLookup.HasComponent(asc)
                    && !AttributeDirtyLookup.IsComponentEnabled(asc))
                {
                    AttributeDirtyLookup.SetComponentEnabled(asc, true);
                }
            }

            private void EnqueueStackCountChangedEvent(in ActiveGameplayEffectBuffer slot)
            {
                EnqueueGameplayEvent(new GameplayEventBusEventBuffer
                {
                    Type = EGameplayEventType.StackCountChanged,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = slot.TargetAsc,
                    SourceAbility = slot.SourceAbility,
                    GameplayEffect = Entity.Null,
                    ContextId = slot.ContextId,
                    EventCode = slot.GameplayEffectCode,
                    Value = slot.StackCount,
                });
            }

            private void EnqueueRemovedEvent(in ActiveGameplayEffectBuffer slot)
            {
                EnqueueGameplayEvent(new GameplayEventBusEventBuffer
                {
                    Type = EGameplayEventType.GameplayEffectRemoved,
                    SourceAsc = slot.SourceAsc,
                    TargetAsc = slot.TargetAsc,
                    SourceAbility = slot.SourceAbility,
                    GameplayEffect = Entity.Null,
                    ContextId = slot.ContextId,
                    EventCode = slot.GameplayEffectCode,
                });
            }

            private void EnqueueGameplayEvent(GameplayEventBusEventBuffer evt)
            {
                if (EventBusEntity == Entity.Null || !GameplayEventLookup.HasBuffer(EventBusEntity))
                    return;

                evt.Frame = Frame;
                if (GameplayEventBusLookup.HasComponent(EventBusEntity))
                {
                    var eventBus = GameplayEventBusLookup[EventBusEntity];
                    evt.Sequence = eventBus.NextSequence;
                    eventBus.NextSequence++;
                    GameplayEventBusLookup[EventBusEntity] = eventBus;
                }
                else
                {
                    evt.Sequence = 0;
                }

                GameplayEventLookup[EventBusEntity].Add(evt);
            }

            private void EnqueueTagChangeEvent(TagChangeEventBuffer evt)
            {
                if (EventBusEntity != Entity.Null && TagChangeEventLookup.HasBuffer(EventBusEntity))
                    TagChangeEventLookup[EventBusEntity].Add(evt);
            }
        }

        public static bool TryApplyActiveMutation(
            EntityManager em,
            ref GEEffectCommandStreamComponent stream,
            ref GASDefinitionCatalogBlob catalog,
            in GEEffectCommandBuffer command,
            DynamicBuffer<GEEffectCommandBuffer> commands,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            DynamicBuffer<ActiveEffectMutationBuffer> mutations,
            int frame,
            ref EntityCommandBuffer structuralEcb,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (!IsAvailableAsc(em, command.TargetAsc)
                || !HasActiveEffectStorage(em, command.TargetAsc)
                || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, command.GameplayEffectCode, out var gameplayEffectIndex))
            {
                return false;
            }

            ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            var targetTags = em.GetComponentData<TagMaskComponent>(command.TargetAsc);
            if (!EvaluateGameplayEffectRequirements(ref catalog, in gameplayEffect, in targetTags))
                return false;

            var slots = em.GetBuffer<ActiveGameplayEffectBuffer>(command.TargetAsc);
            var setByCallerSnapshot = em.GetBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(command.TargetAsc);
            RemoveGameplayEffectsWithTags(
                em,
                ref catalog,
                command.TargetAsc,
                in gameplayEffect,
                slots,
                mutations,
                frame,
                ref structuralEcb,
                ref eventWriter);

            var durationFrame = ResolveDurationFrame(in command, in gameplayEffect);
            if (!HasPersistentRuntimeState(in gameplayEffect, durationFrame))
            {
                mutations.Add(new ActiveEffectMutationBuffer
                {
                    Sequence = command.Sequence,
                    SourceCommandSequence = command.Sequence,
                    Frame = frame,
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
                EnqueueAppliedEvents(ref eventWriter, in command, in gameplayEffect, Entity.Null);
                return true;
            }

            var store = em.GetComponentData<ASCActiveEffectsComponent>(command.TargetAsc);
            var slotIndex = FindRefreshableSlot(slots, in command);
            if (slotIndex < 0 && slots.Length >= GASParameterSetting.ASC_MAX_GAMEPLAY_EFFECT_COUNT)
                return false;

            var isNewSlot = slotIndex < 0;
            var previous = isNewSlot ? default : slots[slotIndex];
            var previousStackCount = previous.StackCount <= 0 ? 1 : previous.StackCount;
            var isOverflow = !isNewSlot && IsStackOverflow(in gameplayEffect, previousStackCount);
            if (isOverflow)
            {
                EmitOverflowCommand(
                    ref stream,
                    ref catalog,
                    commands,
                    setByCallerValues,
                    in command,
                    in gameplayEffect,
                    frame);
                EnqueueStackOverflowEvent(ref eventWriter, in command, in gameplayEffect);
                if (gameplayEffect.ClearStackOnOverflow != 0)
                {
                    RemoveSlotAt(em, command.TargetAsc, slots, slotIndex, mutations, frame, ref structuralEcb, ref eventWriter);
                    if (em.HasComponent<ASCActiveEffectsComponent>(command.TargetAsc))
                        store = em.GetComponentData<ASCActiveEffectsComponent>(command.TargetAsc);
                    slotIndex = -1;
                    isNewSlot = true;
                    previous = default;
                    previousStackCount = 1;
                    EnqueueStackClearedByOverflowEvent(ref eventWriter, in command);
                }

                if (gameplayEffect.DenyOverflowApplication != 0)
                {
                    EnqueueStackOverflowDeniedEvent(ref eventWriter, in command);
                    return true;
                }
            }

            var slot = isNewSlot
                ? CreateNewSlot(ref store, in command, in gameplayEffect, durationFrame, frame)
                : RefreshExistingSlot(
                    previous,
                    in command,
                    in gameplayEffect,
                    durationFrame,
                    frame,
                    ShouldRefreshDuration(in gameplayEffect),
                    ShouldResetPeriod(in gameplayEffect));

            slot.StackCount = ResolveNextStackCount(in gameplayEffect, previousStackCount, isNewSlot);
            RemoveActiveModifiersForSlot(em, command.TargetAsc, slot.Sequence, slot.GameplayEffectCode);
            RemoveSetByCallerSnapshotForSlot(setByCallerSnapshot, slot.Sequence, slot.GameplayEffectCode);
            CopySetByCallerSnapshot(in command, setByCallerValues, setByCallerSnapshot, slot.Sequence, slot.GameplayEffectCode);

            var activeModifierCount = ApplyActiveModifiers(
                em,
                ref catalog,
                in gameplayEffect,
                in command,
                setByCallerValues,
                slot.Sequence,
                slot.StackCount);
            slot.ActiveGrantedTagCount = ApplyGrantedTags(
                em,
                ref catalog,
                command.TargetAsc,
                in gameplayEffect,
                slot.Sequence,
                ref eventWriter);
            slot.ActiveGrantedAbilityCount = ApplyGrantedAbilities(
                em,
                ref catalog,
                command.TargetAsc,
                in gameplayEffect,
                slot.Sequence,
                ref structuralEcb);
            slot.Flags = ResolveSlotFlags(
                in gameplayEffect,
                durationFrame,
                activeModifierCount,
                slot.ActiveGrantedTagCount,
                slot.ActiveGrantedAbilityCount);

            if (slotIndex >= 0)
                slots[slotIndex] = slot;
            else
                slots.Add(slot);

            ActiveEffectStore.RefreshChunkSkipIndexCounters(ref store, slots, frame);
            em.SetComponentData(command.TargetAsc, store);

            mutations.Add(new ActiveEffectMutationBuffer
            {
                Sequence = slot.Sequence,
                SourceCommandSequence = command.Sequence,
                Frame = frame,
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

            EnqueueAppliedEvents(ref eventWriter, in command, in gameplayEffect, Entity.Null);
            return true;
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
                || gameplayEffect.RemoveGameplayEffectTagMaskIndex >= 0;
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

        private static int ApplyActiveModifiers(
            EntityManager em,
            ref GASDefinitionCatalogBlob catalog,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            in GEEffectCommandBuffer command,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            int slotSequence,
            int stackCount)
        {
            if (gameplayEffect.ModifierCount <= 0
                || command.TargetAsc == Entity.Null
                || !em.Exists(command.TargetAsc)
                || !em.HasBuffer<AttributeValueBuffer>(command.TargetAsc)
                || !em.HasBuffer<AttributeActiveModifierBuffer>(command.TargetAsc))
            {
                return 0;
            }

            var attributes = em.GetBuffer<AttributeValueBuffer>(command.TargetAsc);
            var activeModifiers = em.GetBuffer<AttributeActiveModifierBuffer>(command.TargetAsc);
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

                var context = BuildMagnitudeContext(em, in command, setByCallerValues, in modifier, stackCount);
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
                AttributeHelper.MarkActiveModifierAdded(em, command.TargetAsc);
                AttributeHelper.MarkCurrentValueDirty(
                    em,
                    command.TargetAsc,
                    attributes,
                    modifier.AttributeSetCode,
                    modifier.AttributeCode);
                added++;
            }

            return added;
        }

        private static MagnitudeEvalContext BuildMagnitudeContext(
            EntityManager em,
            in GEEffectCommandBuffer command,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            in GASCatalogModifierDefinitionBlob modifier,
            int stackCount)
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

            if (modifier.MagnitudeSource == EMagnitudeSource.SourceAttribute
                && TryReadAttributeValue(em, command.SourceAsc, in modifier, out var sourceValue))
            {
                context.HasSourceAttributeValue = 1;
                context.SourceAttributeValue = sourceValue;
            }

            if (modifier.MagnitudeSource == EMagnitudeSource.TargetAttribute
                && TryReadAttributeValue(em, command.TargetAsc, in modifier, out var targetValue))
            {
                context.HasTargetAttributeValue = 1;
                context.TargetAttributeValue = targetValue;
            }

            return context;
        }

        private static bool TryReadAttributeValue(
            EntityManager em,
            Entity owner,
            in GASCatalogModifierDefinitionBlob modifier,
            out float value)
        {
            value = 0f;
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<AttributeValueBuffer>(owner))
                return false;

            var attrSetCode = modifier.CaptureAttributeSetCode != 0
                ? modifier.CaptureAttributeSetCode
                : modifier.AttributeSetCode;
            var attributeCode = modifier.CaptureAttributeCode != 0
                ? modifier.CaptureAttributeCode
                : modifier.AttributeCode;
            var attributes = em.GetBuffer<AttributeValueBuffer>(owner);
            var attrIndex = attributes.IndexOfAttribute(attrSetCode, attributeCode);
            if (attrIndex < 0)
                return false;

            value = attributes[attrIndex].CurrentValue;
            return true;
        }

        private static bool TryFindSetByCallerValue(
            in GEEffectCommandBuffer command,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
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

        private static void RemoveActiveModifiersForSlot(
            EntityManager em,
            Entity owner,
            int slotSequence,
            int gameplayEffectCode)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<AttributeActiveModifierBuffer>(owner))
                return;

            var modifiers = em.GetBuffer<AttributeActiveModifierBuffer>(owner);
            var attributes = em.HasBuffer<AttributeValueBuffer>(owner)
                ? em.GetBuffer<AttributeValueBuffer>(owner)
                : default;
            for (var i = modifiers.Length - 1; i >= 0; i--)
            {
                var modifier = modifiers[i];
                if (modifier.SourceSequence != slotSequence
                    || modifier.SourceGameplayEffectCode != gameplayEffectCode)
                {
                    continue;
                }

                modifiers.RemoveAt(i);
                if (attributes.IsCreated)
                    AttributeHelper.MarkCurrentValueDirty(
                        em,
                        owner,
                        attributes,
                        modifier.AttrSetCode,
                        modifier.AttributeCode);
            }

            AttributeHelper.RefreshActiveModifierPresence(em, owner, modifiers);
        }

        private static int ApplyGrantedTags(
            EntityManager em,
            ref GASDefinitionCatalogBlob catalog,
            Entity owner,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int slotSequence,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (gameplayEffect.GrantedTagMaskIndex < 0
                || gameplayEffect.GrantedTagMaskIndex >= catalog.TagMasks.Length
                || owner == Entity.Null
                || !em.Exists(owner))
            {
                return 0;
            }

            if (!em.HasComponent<TagMaskComponent>(owner) || !em.HasBuffer<TagTemporarySourceBuffer>(owner))
                return 0;

            var grantedMask = catalog.TagMasks[gameplayEffect.GrantedTagMaskIndex].Mask;
            var ownerTags = em.GetComponentData<TagMaskComponent>(owner);
            var sources = em.GetBuffer<TagTemporarySourceBuffer>(owner);
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

                if (!wasActive && eventWriter.IsCreated)
                {
                    eventWriter.EnqueueTagChangeEvent(new TagChangeEventBuffer
                    {
                        ASC = owner,
                        TagIndex = tagIndex,
                        Added = true,
                    });
                }
            }

            em.SetComponentData(owner, ownerTags);
            return CountTempTagSourcesForSlot(sources, slotSequence, gameplayEffect.GameplayEffectCode);
        }

        private static void RemoveGrantedTagsForSlot(
            EntityManager em,
            Entity owner,
            int slotSequence,
            int gameplayEffectCode,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<TagTemporarySourceBuffer>(owner))
                return;

            var sources = em.GetBuffer<TagTemporarySourceBuffer>(owner);
            for (var i = sources.Length - 1; i >= 0; i--)
            {
                var source = sources[i];
                if (source.SourceSequence != slotSequence
                    || source.SourceGameplayEffectCode != gameplayEffectCode)
                {
                    continue;
                }

                sources.RemoveAt(i);
                var removedFromMask = RemoveTagIndexFromEffectiveMaskIfUnreferenced(em, owner, source.TagIndex);
                if (removedFromMask && eventWriter.IsCreated)
                {
                    eventWriter.EnqueueTagChangeEvent(new TagChangeEventBuffer
                    {
                        ASC = owner,
                        TagIndex = source.TagIndex,
                        Added = false,
                    });
                }
            }
        }

        private static int ApplyGrantedAbilities(
            EntityManager em,
            ref GASDefinitionCatalogBlob catalog,
            Entity owner,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int slotSequence,
            ref EntityCommandBuffer structuralEcb)
        {
            if (gameplayEffect.GrantedAbilityCount <= 0
                || owner == Entity.Null
                || !em.Exists(owner)
                || !em.HasBuffer<AbilitySlotBuffer>(owner))
            {
                return 0;
            }

            var grantedAbilities = em.GetBuffer<AbilitySlotBuffer>(owner);
            var added = 0;
            for (var i = 0; i < gameplayEffect.GrantedAbilityCount; i++)
            {
                var grantedIndex = gameplayEffect.GrantedAbilityStart + i;
                if ((uint)grantedIndex >= (uint)catalog.GrantedAbilities.Length)
                    continue;

                var granted = catalog.GrantedAbilities[grantedIndex];
                if (granted.AbilityCode <= 0
                    || HasGrantedAbilityForSlot(em, grantedAbilities, granted.AbilityCode, slotSequence, gameplayEffect.GameplayEffectCode))
                {
                    continue;
                }

                var ability = structuralEcb.CreateEntity(GASRuntimeEntityArchetypes.GrantedAbility(em));
                GASRuntimeEntityArchetypes.InitializeAbilityEntity(structuralEcb, ability);
                structuralEcb.SetComponent(
                    ability,
                    AbilityStateComponent.Create(granted.AbilityCode, granted.Level, owner));
                structuralEcb.SetComponent(ability, new AbilityMainTargetComponent { TargetAsc = Entity.Null });
                structuralEcb.SetComponent(ability, new AbilityGrantedByEffectComponent
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

                structuralEcb.AppendToBuffer(owner, new AbilitySlotBuffer { AbilityEntity = ability });
                added++;

                var activationPolicy = (GrantedAbilityActivationPolicy)granted.ActivationPolicy;
                if ((activationPolicy == GrantedAbilityActivationPolicy.WhenAdded
                        || activationPolicy == GrantedAbilityActivationPolicy.SyncWithEffect))
                {
                    structuralEcb.SetComponentEnabled<AbilityActivationPendingComponent>(ability, true);
                }
                else
                {
                    DisableMarker<AbilityActivationPendingComponent>(ref structuralEcb, ability);
                }
            }

            return CountGrantedAbilitiesForSlot(em, grantedAbilities, slotSequence, gameplayEffect.GameplayEffectCode) + added;
        }

        private static void DisableMarker<T>(ref EntityCommandBuffer ecb, Entity entity)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            ecb.SetComponentEnabled<T>(entity, false);
        }

        private static void RemoveGrantedAbilitiesForSlot(
            EntityManager em,
            ref EntityCommandBuffer structuralEcb,
            Entity owner,
            int slotSequence,
            int gameplayEffectCode)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<AbilitySlotBuffer>(owner))
                return;

            var grantedAbilities = em.GetBuffer<AbilitySlotBuffer>(owner);
            for (var i = grantedAbilities.Length - 1; i >= 0; i--)
            {
                var ability = grantedAbilities[i].AbilityEntity;
                if (!IsGrantedBySlot(em, ability, slotSequence, gameplayEffectCode))
                    continue;

                grantedAbilities.RemoveAt(i);
                RemoveGrantedAbilityEntity(em, ref structuralEcb, ability);
            }
        }

        private static bool HasGrantedAbilityForSlot(
            EntityManager em,
            DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
            int abilityCode,
            int slotSequence,
            int gameplayEffectCode)
        {
            for (var i = 0; i < grantedAbilities.Length; i++)
            {
                var ability = grantedAbilities[i].AbilityEntity;
                if (!IsGrantedBySlot(em, ability, slotSequence, gameplayEffectCode)
                    || !em.HasComponent<AbilityStateComponent>(ability))
                {
                    continue;
                }

                if (em.GetComponentData<AbilityStateComponent>(ability).Code == abilityCode)
                    return true;
            }

            return false;
        }

        private static int CountGrantedAbilitiesForSlot(
            EntityManager em,
            DynamicBuffer<AbilitySlotBuffer> grantedAbilities,
            int slotSequence,
            int gameplayEffectCode)
        {
            var count = 0;
            for (var i = 0; i < grantedAbilities.Length; i++)
            {
                if (IsGrantedBySlot(em, grantedAbilities[i].AbilityEntity, slotSequence, gameplayEffectCode))
                    count++;
            }

            return count;
        }

        private static bool IsGrantedBySlot(
            EntityManager em,
            Entity ability,
            int slotSequence,
            int gameplayEffectCode)
        {
            if (ability == Entity.Null
                || !em.Exists(ability)
                || !em.HasComponent<AbilityGrantedByEffectComponent>(ability))
            {
                return false;
            }

            var granted = em.GetComponentData<AbilityGrantedByEffectComponent>(ability);
            return granted.SourceSequence == slotSequence
                && granted.SourceGameplayEffectCode == gameplayEffectCode;
        }

        private static void RemoveGrantedAbilityEntity(
            EntityManager em,
            ref EntityCommandBuffer structuralEcb,
            Entity ability)
        {
            if (ability == Entity.Null || !em.Exists(ability))
                return;

            var isRunning = false;
            if (em.HasComponent<AbilityStateComponent>(ability))
            {
                var runtime = em.GetComponentData<AbilityStateComponent>(ability);
                isRunning = runtime.Phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending;
            }

            if (isRunning)
            {
                AbilityRuntimeActions.RequestAbilityCancel(
                    ability,
                    em,
                    ref structuralEcb,
                    EAbilityLifecycleReason.GrantedEffectRemoved,
                    sourceEffect: Entity.Null);
                AbilityRuntimeActions.EnableDestroyOnCleanup(ability, em, ref structuralEcb);
                return;
            }

            structuralEcb.DestroyEntity(ability);
        }

        private static void CopySetByCallerSnapshot(
            in GEEffectCommandBuffer command,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
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
            EntityManager em,
            Entity owner,
            int slotSequence,
            int gameplayEffectCode)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(owner))
                return;

            RemoveSetByCallerSnapshotForSlot(
                em.GetBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(owner),
                slotSequence,
                gameplayEffectCode);
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

        private static bool RemoveTagIndexFromEffectiveMaskIfUnreferenced(EntityManager em, Entity owner, int tagIndex)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasComponent<TagMaskComponent>(owner))
                return false;
            if (em.HasComponent<TagFixedMaskComponent>(owner)
                && em.GetComponentData<TagFixedMaskComponent>(owner).Mask.HasTag(tagIndex))
            {
                return false;
            }
            if (HasAnyTemporarySourceForTag(em, owner, tagIndex))
                return false;

            var mask = em.GetComponentData<TagMaskComponent>(owner);
            if (!mask.HasTag(tagIndex))
                return false;
            mask.RemoveTag(tagIndex);
            em.SetComponentData(owner, mask);
            return true;
        }

        private static bool HasAnyTemporarySourceForTag(EntityManager em, Entity owner, int tagIndex)
        {
            if (!em.Exists(owner) || !em.HasBuffer<TagTemporarySourceBuffer>(owner))
                return false;

            var sources = em.GetBuffer<TagTemporarySourceBuffer>(owner);
            for (var i = 0; i < sources.Length; i++)
            {
                if (sources[i].TagIndex == tagIndex)
                    return true;
            }

            return false;
        }

        private static void HandleDurationExpired(
            EntityManager em,
            Entity owner,
            DynamicBuffer<ActiveGameplayEffectBuffer> slots,
            int slotIndex,
            ref GASDefinitionCatalogBlob catalog,
            DynamicBuffer<ActiveEffectMutationBuffer> mutations,
            int frame,
            ref EntityCommandBuffer structuralEcb,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if ((uint)slotIndex >= (uint)slots.Length)
                return;

            var slot = slots[slotIndex];
            if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
            {
                RemoveSlotAt(em, owner, slots, slotIndex, mutations, frame, ref structuralEcb, ref eventWriter);
                return;
            }

            ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            var expirationPolicy = (EffectExpirationPolicy)gameplayEffect.EffectExpirationPolicy;
            if (expirationPolicy == EffectExpirationPolicy.RefreshDuration)
            {
                RefreshSlotDuration(ref slot, frame, resetPeriod: true);
                slots[slotIndex] = slot;
                mutations.Add(CreateRefreshMutation(in slot, frame));
                return;
            }

            if (expirationPolicy == EffectExpirationPolicy.RemoveSingleStackAndRefreshDuration
                && slot.StackCount > 1)
            {
                slot.StackCount--;
                RefreshSlotDuration(ref slot, frame, resetPeriod: true);
                slots[slotIndex] = slot;
                RebuildActiveModifiersForSlot(em, ref catalog, in gameplayEffect, in slot);
                mutations.Add(CreateStackMutation(in slot, frame));
                EnqueueStackCountChangedEvent(ref eventWriter, in slot);
                return;
            }

            RemoveSlotAt(em, owner, slots, slotIndex, mutations, frame, ref structuralEcb, ref eventWriter);
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

        private static int RebuildActiveModifiersForSlot(
            EntityManager em,
            ref GASDefinitionCatalogBlob catalog,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            in ActiveGameplayEffectBuffer slot)
        {
            if (gameplayEffect.ModifierCount <= 0
                || slot.TargetAsc == Entity.Null
                || !em.Exists(slot.TargetAsc)
                || !em.HasBuffer<AttributeValueBuffer>(slot.TargetAsc)
                || !em.HasBuffer<AttributeActiveModifierBuffer>(slot.TargetAsc))
            {
                return 0;
            }

            RemoveActiveModifiersForSlot(em, slot.TargetAsc, slot.Sequence, slot.GameplayEffectCode);
            var attributes = em.GetBuffer<AttributeValueBuffer>(slot.TargetAsc);
            var activeModifiers = em.GetBuffer<AttributeActiveModifierBuffer>(slot.TargetAsc);
            var setByCallerSnapshot = em.HasBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(slot.TargetAsc)
                ? em.GetBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(slot.TargetAsc)
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

                var context = BuildMagnitudeContextFromSlot(em, in slot, setByCallerSnapshot, in modifier);
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
                AttributeHelper.MarkActiveModifierAdded(em, slot.TargetAsc);
                AttributeHelper.MarkCurrentValueDirty(
                    em,
                    slot.TargetAsc,
                    attributes,
                    modifier.AttributeSetCode,
                    modifier.AttributeCode);
                added++;
            }

            return added;
        }

        private static MagnitudeEvalContext BuildMagnitudeContextFromSlot(
            EntityManager em,
            in ActiveGameplayEffectBuffer slot,
            DynamicBuffer<ActiveGameplayEffectSetByCallerValueBuffer> setByCallerSnapshot,
            in GASCatalogModifierDefinitionBlob modifier)
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

            if (modifier.MagnitudeSource == EMagnitudeSource.SourceAttribute
                && TryReadAttributeValue(em, slot.SourceAsc, in modifier, out var sourceValue))
            {
                context.HasSourceAttributeValue = 1;
                context.SourceAttributeValue = sourceValue;
            }

            if (modifier.MagnitudeSource == EMagnitudeSource.TargetAttribute
                && TryReadAttributeValue(em, slot.TargetAsc, in modifier, out var targetValue))
            {
                context.HasTargetAttributeValue = 1;
                context.TargetAttributeValue = targetValue;
            }

            return context;
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

        private static void RemoveGameplayEffectsWithTags(
            EntityManager em,
            ref GASDefinitionCatalogBlob catalog,
            Entity owner,
            in GASCatalogGameplayEffectDefinitionBlob appliedGameplayEffect,
            DynamicBuffer<ActiveGameplayEffectBuffer> slots,
            DynamicBuffer<ActiveEffectMutationBuffer> mutations,
            int frame,
            ref EntityCommandBuffer structuralEcb,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (appliedGameplayEffect.RemoveGameplayEffectTagMaskIndex < 0
                || appliedGameplayEffect.RemoveGameplayEffectTagMaskIndex >= catalog.TagMasks.Length)
            {
                return;
            }

            var removeMask = catalog.TagMasks[appliedGameplayEffect.RemoveGameplayEffectTagMaskIndex].Mask;
            if (removeMask.IsEmpty)
                return;

            for (var i = slots.Length - 1; i >= 0; i--)
            {
                var slot = slots[i];
                if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var slotGameplayEffectIndex))
                    continue;

                ref readonly var slotGameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, slotGameplayEffectIndex);
                if (slotGameplayEffect.GrantedTagMaskIndex < 0
                    || slotGameplayEffect.GrantedTagMaskIndex >= catalog.TagMasks.Length)
                {
                    continue;
                }

                var slotGrantedMask = catalog.TagMasks[slotGameplayEffect.GrantedTagMaskIndex].Mask;
                if (slotGrantedMask.HasAnyTag(removeMask))
                    RemoveSlotAt(em, owner, slots, i, mutations, frame, ref structuralEcb, ref eventWriter);
            }
        }

        public static void RemoveSlotAt(
            EntityManager em,
            Entity owner,
            DynamicBuffer<ActiveGameplayEffectBuffer> slots,
            int slotIndex,
            DynamicBuffer<ActiveEffectMutationBuffer> mutations,
            int frame,
            ref EntityCommandBuffer structuralEcb,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if ((uint)slotIndex >= (uint)slots.Length)
                return;

            var slot = slots[slotIndex];
            var activeModifierCount = CountActiveModifiersForSlot(em, owner, slot.Sequence, slot.GameplayEffectCode);
            RemoveActiveModifiersForSlot(em, owner, slot.Sequence, slot.GameplayEffectCode);
            RemoveGrantedTagsForSlot(em, owner, slot.Sequence, slot.GameplayEffectCode, ref eventWriter);
            RemoveGrantedAbilitiesForSlot(em, ref structuralEcb, owner, slot.Sequence, slot.GameplayEffectCode);
            RemoveSetByCallerSnapshotForSlot(em, owner, slot.Sequence, slot.GameplayEffectCode);
            RecordCleanup(em, owner, in slot, activeModifierCount, frame);
            mutations.Add(new ActiveEffectMutationBuffer
            {
                Sequence = slot.Sequence,
                Frame = frame,
                Kind = ActiveEffectMutationKind.Remove,
                ActiveEffect = Entity.Null,
                SourceAsc = slot.SourceAsc,
                TargetAsc = owner,
                SourceAbility = slot.SourceAbility,
                SourceEffect = slot.SourceEffect,
                GameplayEffectCode = slot.GameplayEffectCode,
                ContextId = slot.ContextId,
                ParentContextId = slot.ParentContextId,
                StackCount = slot.StackCount,
                DurationFrameOverride = slot.DurationFrame,
                PeriodFrame = slot.PeriodFrame,
            });
            EnqueueRemovedEvent(ref eventWriter, in slot);
            slots.RemoveAt(slotIndex);
        }

        private static void RecordCleanup(
            EntityManager em,
            Entity owner,
            in ActiveGameplayEffectBuffer slot,
            int activeModifierCount,
            int frame)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<ActiveGameplayEffectCleanupRecordBuffer>(owner))
                return;

            var records = em.GetBuffer<ActiveGameplayEffectCleanupRecordBuffer>(owner);
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
                TargetAsc = owner,
                SourceAbility = slot.SourceAbility,
                SourceEffect = slot.SourceEffect,
                Instigator = slot.Instigator,
                Causer = slot.Causer,
                GameplayEffectCode = slot.GameplayEffectCode,
                Level = slot.Level,
                StackCount = slot.StackCount,
                ContextId = slot.ContextId,
                ParentContextId = slot.ParentContextId,
                CleanupFrame = frame,
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
                CleanupResolvedFrame = frame,
                Flags = slot.Flags,
            });

            if (em.HasComponent<ASCActiveEffectsComponent>(owner))
            {
                var store = em.GetComponentData<ASCActiveEffectsComponent>(owner);
                store.LastCleanupFrame = frame;
                store.CleanupRecordCount = records.Length;
                em.SetComponentData(owner, store);
            }
        }

        private static void EmitPeriodCommand(
            EntityManager em,
            ref GASDefinitionCatalogBlob catalog,
            in ActiveGameplayEffectBuffer slot,
            Entity owner,
            ref EffectCommandSpecStream.CommandWriter commandWriter)
        {
            if (!GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, slot.GameplayEffectCode, out var gameplayEffectIndex))
                return;

            ref readonly var gameplayEffect = ref GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref catalog, gameplayEffectIndex);
            if (gameplayEffect.PeriodGameplayEffectCode <= 0
                || !GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(ref catalog, gameplayEffect.PeriodGameplayEffectCode, out var periodGameplayEffectIndex))
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
                TargetAsc = owner,
                SourceAbility = slot.SourceAbility,
                SourceEffect = Entity.Null,
                Instigator = slot.Instigator != Entity.Null ? slot.Instigator : slot.SourceAsc,
                Causer = slot.Causer != Entity.Null ? slot.Causer : slot.SourceAbility,
                GameplayEffectCode = gameplayEffect.PeriodGameplayEffectCode,
                Level = slot.Level,
                DurationFrameOverride = durationFrame,
                ParentContextId = slot.ContextId,
                TargetDataKind = slot.SourceAsc == owner ? ETargetDataKind.Self : ETargetDataKind.Entity,
                Flags = kind == GEEffectCommandKind.ActiveMutation ? GASGECommandSeedFlags.ActiveMutation : GASGECommandSeedFlags.None,
            };

            if (owner != Entity.Null
                && em.Exists(owner)
                && em.HasBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(owner))
            {
                commandWriter.AppendCommand(
                    command,
                    em.GetBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(owner),
                    slot.Sequence,
                    slot.GameplayEffectCode);
                return;
            }

            commandWriter.AppendCommand(command);
        }

        private static bool EvaluateGameplayEffectRequirements(
            ref GASDefinitionCatalogBlob catalog,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            in TagMaskComponent targetTags)
        {
            for (var i = 0; i < gameplayEffect.RequirementCount; i++)
            {
                var index = gameplayEffect.RequirementStart + i;
                if ((uint)index >= (uint)catalog.Requirements.Length)
                    return false;

                var requirement = catalog.Requirements[index];
                if (requirement.RequirementKind == GASRequirementKind.None)
                    continue;
                if (requirement.TagMaskIndex < 0 || requirement.TagMaskIndex >= catalog.TagMasks.Length)
                    continue;

                var mask = catalog.TagMasks[requirement.TagMaskIndex].Mask;
                if (requirement.RequirementKind == GASRequirementKind.RequiredTags && !targetTags.HasAllTags(mask))
                    return false;
                if (requirement.RequirementKind == GASRequirementKind.BlockedTags && targetTags.HasAnyTag(mask))
                    return false;
            }

            return true;
        }

        private static bool HasActiveEffectStorage(EntityManager em, Entity owner)
        {
            return ASCEntityFactory.HasASCRuntimeCoreComponents(em, owner);
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

        private static int CountActiveModifiersForSlot(
            EntityManager em,
            Entity owner,
            int sourceSequence,
            int gameplayEffectCode)
        {
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasBuffer<AttributeActiveModifierBuffer>(owner))
                return 0;

            var count = 0;
            var modifiers = em.GetBuffer<AttributeActiveModifierBuffer>(owner);
            for (var i = 0; i < modifiers.Length; i++)
            {
                var modifier = modifiers[i];
                if (modifier.SourceSequence == sourceSequence
                    && modifier.SourceGameplayEffectCode == gameplayEffectCode)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsAvailableAsc(EntityManager em, Entity asc)
        {
            return asc != Entity.Null
                && em.Exists(asc)
                && !ASCEntityFactory.IsDestroying(em, asc);
        }

        private static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;
            return next++;
        }

        private static void EmitOverflowCommand(
            ref GEEffectCommandStreamComponent stream,
            ref GASDefinitionCatalogBlob catalog,
            DynamicBuffer<GEEffectCommandBuffer> commands,
            DynamicBuffer<GESetByCallerValueBuffer> setByCallerValues,
            in GEEffectCommandBuffer sourceCommand,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            int frame)
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
            EffectCommandSpecStream.AppendPreparedCommand(
                ref stream,
                commands,
                setByCallerValues,
                new GEEffectCommandBuffer
                {
                    Frame = frame,
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
                },
                frame);
        }

        private static void EnqueueStackOverflowEvent(
            ref EventBusHelper.GameplayEventBusWriter writer,
            in GEEffectCommandBuffer command,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.StackOverflow,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = Entity.Null,
                ContextId = command.ContextId,
                EventCode = command.GameplayEffectCode,
                ReasonCode = gameplayEffect.OverflowGameplayEffectCode,
            });
        }

        private static void EnqueueStackOverflowDeniedEvent(
            ref EventBusHelper.GameplayEventBusWriter writer,
            in GEEffectCommandBuffer command)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.StackOverflowDenied,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = Entity.Null,
                ContextId = command.ContextId,
                EventCode = command.GameplayEffectCode,
            });
        }

        private static void EnqueueStackClearedByOverflowEvent(
            ref EventBusHelper.GameplayEventBusWriter writer,
            in GEEffectCommandBuffer command)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.StackClearedByOverflow,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = Entity.Null,
                ContextId = command.ContextId,
                EventCode = command.GameplayEffectCode,
            });
        }

        private static void EnqueueStackCountChangedEvent(
            ref EventBusHelper.GameplayEventBusWriter writer,
            in ActiveGameplayEffectBuffer slot)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.StackCountChanged,
                SourceAsc = slot.SourceAsc,
                TargetAsc = slot.TargetAsc,
                SourceAbility = slot.SourceAbility,
                GameplayEffect = Entity.Null,
                ContextId = slot.ContextId,
                EventCode = slot.GameplayEffectCode,
                Value = slot.StackCount,
            });
        }

        private static void EnqueueAppliedEvents(
            ref EventBusHelper.GameplayEventBusWriter writer,
            in GEEffectCommandBuffer command,
            in GASCatalogGameplayEffectDefinitionBlob gameplayEffect,
            Entity gameplayEffectEntity)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.GameplayEffectApplied,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = gameplayEffectEntity,
                ContextId = command.ContextId,
                EventCode = command.GameplayEffectCode,
            });

            if (gameplayEffect.GameplayCueCode <= 0)
                return;

            writer.EnqueueCueRequest(new CueRequestBuffer
            {
                TargetAsc = command.TargetAsc,
                SourceAsc = command.SourceAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = gameplayEffectEntity,
                SourceEntity = command.SourceAbility,
                SourceType = CueSourceType.GameplayEffect,
                CueEntity = Entity.Null,
                ContextId = command.ContextId,
                ReasonCode = gameplayEffect.GameplayCueCode,
                CueEvent = EGameplayCueEvent.OnApply,
            });
            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.CueRequested,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = gameplayEffectEntity,
                ContextId = command.ContextId,
                EventCode = (int)EGameplayCueEvent.OnApply,
                ReasonCode = gameplayEffect.GameplayCueCode,
            });
        }

        private static void EnqueueRemovedEvent(
            ref EventBusHelper.GameplayEventBusWriter writer,
            in ActiveGameplayEffectBuffer slot)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = EGameplayEventType.GameplayEffectRemoved,
                SourceAsc = slot.SourceAsc,
                TargetAsc = slot.TargetAsc,
                SourceAbility = slot.SourceAbility,
                GameplayEffect = Entity.Null,
                ContextId = slot.ContextId,
                EventCode = slot.GameplayEffectCode,
            });
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
            writer.WriteLine("if (requirement.TagMaskIndex < 0 || requirement.TagMaskIndex >= catalog.TagMasks.Length)");
            writer.Indent++;
            writer.WriteLine("return true;");
            writer.Indent--;
            writer.WriteLine("");
            writer.WriteLine("var mask = catalog.TagMasks[requirement.TagMaskIndex].Mask;");
            writer.WriteLine("if (requirement.RequirementKind == GASRequirementKind.RequiredTags)");
            writer.Indent++;
            writer.WriteLine("return ownerTags.HasAllTags(mask);");
            writer.Indent--;
            writer.WriteLine("if (requirement.RequirementKind == GASRequirementKind.BlockedTags)");
            writer.Indent++;
            writer.WriteLine("return !ownerTags.HasAnyTag(mask);");
            writer.Indent--;
            writer.WriteLine("return true;");
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
            foreach (var row in RuntimeVisibleRows(context))
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
            writer.WriteLine("| `QRY-01` / `JOB-01` / `PRF-05` | Partial | Generated Runtime now owns catalog-driven ability, instant GE, and active effect lifecycle systems; full chunk-job traversal remains a later optimization pass. |");
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
