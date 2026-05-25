using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace GAS.Editor
{
    /// <summary>
    /// Phase 2: 从 Luban DefinitionRow 生成 StaticLookup 二分查找表。
    /// 为每种 Row 类型生成一个静态查找类，持有序 Code 数组和 BlobAssetReference 数组，
    /// 提供 O(log n) 二分查找替代当前 GASDefinitionTable 中的 O(n) 线性扫描。
    /// </summary>
    public static class GasStaticLookupGenerator
    {
        [MenuItem("EXTool/EX-GAS/生成脚本/Glue/生成 StaticLookup")]
        public static void GenerateStaticLookup()
        {
            var rowTypes = FindDefinitionRowTypes();
            if (rowTypes.Count == 0)
            {
                Debug.LogWarning("[GasStaticLookupGenerator] 未找到任何 *DefinitionRow 类型。");
                return;
            }

            var setting = GASSettingAsset.LoadOrCreate();
            var outputDir = setting.CodeGeneratePath;
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var path = Path.Combine(outputDir, "StaticLookups.gen.cs");
            WriteStaticLookups(path, rowTypes);

            AssetDatabase.Refresh();
            Debug.Log($"[GasStaticLookupGenerator] 生成完成: {rowTypes.Count} 个 StaticLookup 类\n  {path}");
        }

        private static List<Type> FindDefinitionRowTypes()
        {
            var result = new List<Type>();
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.IsDynamic) continue;
                try
                {
                    foreach (var t in asm.GetExportedTypes())
                    {
                        if (!t.IsValueType) continue;
                        if (t.Name.EndsWith("DefinitionRow"))
                            result.Add(t);
                    }
                }
                catch (NotSupportedException) { }
                catch (ReflectionTypeLoadException) { }
            }
            return result;
        }

        private static void WriteStaticLookups(string path, List<Type> rowTypes)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteGeneratedHeader(writer);
            writer.WriteLine("using System;");
            writer.WriteLine("using System.Collections.Generic;");
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("using Unity.Collections;");
            writer.WriteLine("");
            writer.WriteLine("namespace GAS.Runtime.Generated");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var rowType in rowTypes)
            {
                var schemaName = GasBlobSchemaGenerator.RowTypeToBlobSchemaName(rowType);
                var lookupName = schemaName + "Lookup";
                var codeMember = FindCodeMember(rowType);

                writer.WriteLine("");
                writer.WriteLine("/// <summary>");
                writer.WriteLine($"/// {rowType.Name} 的 BlobAsset 二分查找表。冷启动构建，O(log n) 查找。");
                writer.WriteLine("/// </summary>");
                writer.WriteLine($"public class {lookupName} : IDisposable");
                writer.WriteLine("{");
                writer.Indent++;

                // Fields: sorted codes + BlobAssetReference array
                writer.WriteLine($"private int[] _sortedCodes;");
                writer.WriteLine($"private BlobAssetReference<{schemaName}>[] _entries;");
                writer.WriteLine("private bool _initialized;");
                writer.WriteLine("");

                // BuildFromRows method
                writer.WriteLine("/// <summary>");
                writer.WriteLine($"/// 从 Luban Row 列表构建查找表。按 Code 排序，每个 Row 构建一个 BlobAsset。");
                writer.WriteLine("/// </summary>");
                writer.WriteLine($"public void BuildFromRows(IReadOnlyList<{rowType.FullName ?? rowType.Name}> rows, Allocator allocator = Allocator.Persistent)");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine("if (rows == null || rows.Count == 0) return;");
                writer.WriteLine("Dispose();");
                writer.WriteLine("");

                writer.WriteLine("// Sort by Code");
                writer.WriteLine("var sorted = new (int Code, int Index)[rows.Count];");
                writer.WriteLine("for (int i = 0; i < rows.Count; i++)");
                writer.Indent++;
                writer.WriteLine($"sorted[i] = (rows[i].{codeMember}, i);");
                writer.Indent--;
                writer.WriteLine("Array.Sort(sorted, (a, b) => a.Code.CompareTo(b.Code));");
                writer.WriteLine("");

                writer.WriteLine($"_sortedCodes = new int[rows.Count];");
                writer.WriteLine($"_entries = new BlobAssetReference<{schemaName}>[rows.Count];");
                writer.WriteLine("for (int i = 0; i < sorted.Length; i++)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("_sortedCodes[i] = sorted[i].Code;");
                writer.WriteLine($"_entries[i] = BlobDefinitionBuilder.Build{schemaName}(rows[sorted[i].Index], allocator);");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                writer.WriteLine("_initialized = true;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // BinarySearch method
                writer.WriteLine("/// <summary>");
                writer.WriteLine($"/// 二分查找。返回匹配的 BlobAssetReference，未找到返回 default。");
                writer.WriteLine("/// </summary>");
                writer.WriteLine($"public BlobAssetReference<{schemaName}> Find(int code)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("if (!_initialized || _sortedCodes == null) return default;");
                writer.WriteLine("");
                writer.WriteLine("int lo = 0, hi = _sortedCodes.Length - 1;");
                writer.WriteLine("while (lo <= hi)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("int mid = lo + (hi - lo) / 2;");
                writer.WriteLine("int midCode = _sortedCodes[mid];");
                writer.WriteLine("if (midCode == code) return _entries[mid];");
                writer.WriteLine("if (midCode < code) lo = mid + 1;");
                writer.WriteLine("else hi = mid - 1;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("return default;");
                writer.Indent--;
                writer.WriteLine("}");
                writer.WriteLine("");

                // Count property
                writer.WriteLine($"public int Count => _sortedCodes?.Length ?? 0;");
                writer.WriteLine($"public bool IsInitialized => _initialized;");
                writer.WriteLine("");

                // Dispose
                writer.WriteLine("public void Dispose()");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("if (_entries != null)");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("for (int i = 0; i < _entries.Length; i++)");
                writer.Indent++;
                writer.WriteLine("if (_entries[i].IsCreated) _entries[i].Dispose();");
                writer.Indent -= 2;
                writer.WriteLine("}");
                writer.WriteLine("_entries = null;");
                writer.WriteLine("_sortedCodes = null;");
                writer.WriteLine("_initialized = false;");
                writer.Indent--;
                writer.WriteLine("}");

                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// 找到 Row 类型中的 Code 字段名（按优先级：AbilityCode, GameplayEffectCode,
        /// AttributeSetCode, GameplayTagCode, GameplayCueCode, TimelineId, SummonGameplayEffectCode）。
        /// </summary>
        private static string FindCodeMember(Type rowType)
        {
            var fields = rowType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var fieldNames = new HashSet<string>(fields.Select(f => f.Name));

            // Priority-ordered candidates (most common naming patterns)
            var candidates = new[]
            {
                "AbilityCode", "GameplayEffectCode", "AttributeSetCode", "AttributeCode",
                "GameplayTagCode", "GameplayCueCode", "TimelineId", "SummonGameplayEffectCode",
                // Generic fallback: first field ending with "Code"
            };

            foreach (var c in candidates)
                if (fieldNames.Contains(c))
                    return c;

            // Fallback: first field ending with "Code"
            var codeField = fields.FirstOrDefault(f => f.Name.EndsWith("Code"));
            if (codeField != null) return codeField.Name;

            // Last resort: first field ending with "Id"
            var idField = fields.FirstOrDefault(f => f.Name.EndsWith("Id"));
            if (idField != null) return idField.Name;

            // Absolute fallback: first public field
            if (fields.Length > 0) return fields[0].Name;

            return "UnknownCode";
        }

        private static void WriteGeneratedHeader(IndentedWriter writer)
        {
            writer.WriteLine("///////////////////////////////////");
            writer.WriteLine("//// This is a generated file. ////");
            writer.WriteLine("////     Do not modify it.     ////");
            writer.WriteLine("///////////////////////////////////");
            writer.WriteLine("");
        }
    }
}
