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
    /// Phase 1: 从 Luban DefinitionRow struct 生成 BlobSchema + BlobBuilder 胶水代码。
    /// 扫描所有加载的程序集，找到 *DefinitionRow 值类型，反射其 public 字段和
    /// IReadOnlyList&lt;T&gt; 属性，生成对应的 BlobAsset struct 和 BuildFromRow 工厂方法。
    /// </summary>
    public static class GasBlobSchemaGenerator
    {
        [MenuItem("EXTool/EX-GAS/生成脚本/Glue/生成 BlobSchema + BlobBuilder")]
        public static void GenerateBlobSchemaAndBuilder()
        {
            var rowTypes = FindDefinitionRowTypes();
            if (rowTypes.Count == 0)
            {
                Debug.LogWarning("[GasBlobSchemaGenerator] 未找到任何 *DefinitionRow 类型。请确认 Luban 已导出 DefinitionRow。");
                return;
            }

            var setting = GASSettingAsset.LoadOrCreate();
            var outputDir = setting.CodeGeneratePath;
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var schemaPath = Path.Combine(outputDir, "BlobSchemas.gen.cs");
            var builderPath = Path.Combine(outputDir, "BlobBuilders.gen.cs");

            WriteBlobSchemas(schemaPath, rowTypes);
            WriteBlobBuilders(builderPath, rowTypes);

            AssetDatabase.Refresh();
            Debug.Log($"[GasBlobSchemaGenerator] 生成完成: {rowTypes.Count} 个 Row 类型 → BlobSchema + BlobBuilder\n  {schemaPath}\n  {builderPath}");
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

        private static void WriteBlobSchemas(string path, List<Type> rowTypes)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteGeneratedHeader(writer);
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine("namespace GAS.Runtime.Generated");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var rowType in rowTypes)
            {
                var schemaName = RowTypeToBlobSchemaName(rowType);
                var members = GetBlobMembers(rowType);

                writer.WriteLine("");
                writer.WriteLine("/// <summary>");
                writer.WriteLine($"/// BlobAsset 形态的 {rowType.Name}。全部字段 unmanaged，Burst 兼容。");
                writer.WriteLine("/// </summary>");
                writer.WriteLine($"public struct {schemaName}");
                writer.WriteLine("{");
                writer.Indent++;

                foreach (var member in members)
                    writer.WriteLine($"public {member.BlobTypeName} {member.Name};");

                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");
        }

        private static void WriteBlobBuilders(string path, List<Type> rowTypes)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteGeneratedHeader(writer);
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("using Unity.Collections;");
            writer.WriteLine("");
            writer.WriteLine("namespace GAS.Runtime.Generated");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("public static class BlobDefinitionBuilder");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var rowType in rowTypes)
            {
                var schemaName = RowTypeToBlobSchemaName(rowType);
                var members = GetBlobMembers(rowType);
                var methodName = $"Build{schemaName}";
                var rowTypeFullName = rowType.FullName ?? rowType.Name;

                writer.WriteLine("");
                writer.WriteLine($"public static BlobAssetReference<{schemaName}> {methodName}(");
                writer.Indent++;
                writer.WriteLine($"{rowTypeFullName} row,");
                writer.WriteLine("Allocator allocator = Allocator.Persistent)");
                writer.Indent--;
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine($"var builder = new BlobBuilder(Allocator.Temp);");
                writer.WriteLine($"ref var root = ref builder.ConstructRoot<{schemaName}>();");
                writer.WriteLine("");

                foreach (var member in members)
                {
                    var rowAccess = GetRowAccessExpression(rowType, member);
                    if (member.RequiresAllocate)
                    {
                        // BlobArray<T> — builder.Allocate + loop
                        writer.WriteLine($"if ({rowAccess} != null)");
                        writer.WriteLine("{");
                        writer.Indent++;
                        writer.WriteLine($"var count = {rowAccess}.Count;");
                        writer.WriteLine($"var target = builder.Allocate(ref root.{member.Name}, count);");
                        writer.WriteLine($"for (var i = 0; i < count; i++) target[i] = {rowAccess}[i];");
                        writer.Indent--;
                        writer.WriteLine("}");
                    }
                    else if (member.IsBlobString)
                    {
                        // BlobString — builder.AllocateString
                        writer.WriteLine($"builder.AllocateString(ref root.{member.Name}, {rowAccess} ?? string.Empty);");
                    }
                    else if (member.RequiresCast)
                    {
                        // enum → int 显式转换
                        writer.WriteLine($"root.{member.Name} = (int){rowAccess};");
                    }
                    else
                    {
                        // 基本值类型直接赋值
                        writer.WriteLine($"root.{member.Name} = {rowAccess};");
                    }
                }

                writer.WriteLine("");
                writer.WriteLine($"var blob = builder.CreateBlobAssetReference<{schemaName}>(allocator);");
                writer.WriteLine("builder.Dispose();");
                writer.WriteLine("return blob;");
                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        public static string RowTypeToBlobSchemaName(Type rowType)
        {
            // HeadlessAutoChessAbilityDefinitionRow → BlobAbilityDefinition
            // 去掉前缀 "HeadlessAutoChess" 和后缀 "DefinitionRow"
            var name = rowType.Name;
            if (name.StartsWith("HeadlessAutoChess"))
                name = name.Substring("HeadlessAutoChess".Length);
            if (name.EndsWith("DefinitionRow"))
                name = name.Substring(0, name.Length - "DefinitionRow".Length);
            return $"Blob{name}Definition";
        }

        private struct BlobMember
        {
            public string Name;
            public string BlobTypeName;   // Blob struct 中的字段类型
            public string RowAccessor;    // 从 Row 访问数据的表达式
            public bool IsBlobString;
            public bool IsBlobArray;
            public bool RequiresAllocate;
            public bool RequiresCast;
        }

        private static List<BlobMember> GetBlobMembers(Type rowType)
        {
            var result = new List<BlobMember>();

            // 1. Public instance fields
            foreach (var field in rowType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var member = MapFieldToBlob(field.FieldType, field.Name, field.Name);
                if (member.HasValue)
                    result.Add(member.Value);
            }

            // 2. IReadOnlyList<T> public properties (e.g., ParentCodes, ChildCodes on TagDefinitionRow)
            foreach (var prop in rowType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.PropertyType.IsGenericType) continue;
                var genDef = prop.PropertyType.GetGenericTypeDefinition();
                if (genDef != typeof(IReadOnlyList<>)) continue;
                if (!prop.CanRead || prop.GetMethod == null) continue;

                var elemType = prop.PropertyType.GetGenericArguments()[0];
                var member = MapFieldToBlob(prop.PropertyType, prop.Name, prop.Name);
                if (member.HasValue)
                    result.Add(member.Value);
            }

            return result;
        }

        private static BlobMember? MapFieldToBlob(Type fieldType, string name, string rowAccessor)
        {
            var member = new BlobMember
            {
                Name = name,
                RowAccessor = rowAccessor,
            };

            if (fieldType == typeof(int))
            {
                member.BlobTypeName = "int";
            }
            else if (fieldType == typeof(float))
            {
                member.BlobTypeName = "float";
            }
            else if (fieldType == typeof(bool))
            {
                member.BlobTypeName = "bool";
            }
            else if (fieldType == typeof(string))
            {
                member.BlobTypeName = "BlobString";
                member.IsBlobString = true;
            }
            else if (fieldType.IsEnum)
            {
                // enum → int (Burst 兼容)
                member.BlobTypeName = "int";
                member.RequiresCast = true;
            }
            else if (fieldType.IsArray)
            {
                var elemType = fieldType.GetElementType();
                var blobElem = BlobElementTypeName(elemType);
                member.BlobTypeName = $"BlobArray<{blobElem}>";
                member.IsBlobArray = true;
                member.RequiresAllocate = true;
            }
            else if (fieldType.IsGenericType)
            {
                var genDef = fieldType.GetGenericTypeDefinition();
                if (genDef == typeof(IReadOnlyList<>) || genDef == typeof(List<>))
                {
                    var elemType = fieldType.GetGenericArguments()[0];
                    var blobElem = BlobElementTypeName(elemType);
                    member.BlobTypeName = $"BlobArray<{blobElem}>";
                    member.IsBlobArray = true;
                    member.RequiresAllocate = true;
                    // For IReadOnlyList<T>, access via .Count and [i]
                    // RowAccessor is already set to the property name
                }
                else
                {
                    return null; // unsupported generic type, skip
                }
            }
            else
            {
                return null; // unsupported type, skip
            }

            return member;
        }

        private static string BlobElementTypeName(Type t)
        {
            if (t == typeof(int)) return "int";
            if (t == typeof(float)) return "float";
            if (t == typeof(bool)) return "bool";
            if (t.IsEnum) return "int";
            return "int"; // fallback
        }

        private static string GetRowAccessExpression(Type rowType, BlobMember member)
        {
            // If the member has a custom RowAccessor (from property), use "row.{Name}"
            // For fields, also "row.{Name}"
            return $"row.{member.RowAccessor}";
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
