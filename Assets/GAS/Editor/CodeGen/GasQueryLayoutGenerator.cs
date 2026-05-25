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
    /// Phase 5: 为每种 Definition Entity 类型生成预定义的 EntityQueryDesc。
    /// 基于 Phase 4 的 ComponentTypeSet，生成 All/None 约束。
    ///
    /// QueryLayout 标注：
    /// - All：Entity 必须具备的组件
    /// - None：Entity 不能具备的组件（如 CDestroyTag）
    /// - 预估 entity count 数量级
    /// - Burst 兼容性（全部 ReadOnly + 无 managed 组件）
    ///
    /// 遵循 CASE-22/23/26/27/28: Query/Chunk 高级模式。
    /// </summary>
    public static class GasQueryLayoutGenerator
    {
        [MenuItem("EXTool/EX-GAS/生成脚本/Glue/生成 QueryLayout")]
        public static void GenerateQueryLayouts()
        {
            var rowTypes = FindDefinitionRowTypes();
            if (rowTypes.Count == 0)
            {
                Debug.LogWarning("[GasQueryLayoutGenerator] 未找到任何 *DefinitionRow 类型。");
                return;
            }

            var setting = GASSettingAsset.LoadOrCreate();
            var outputDir = setting.CodeGeneratePath;
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var path = Path.Combine(outputDir, "QueryLayouts.gen.cs");
            WriteQueryLayouts(path, rowTypes);

            AssetDatabase.Refresh();
            Debug.Log($"[GasQueryLayoutGenerator] 生成完成: {rowTypes.Count} 个 EntityQueryDesc\n  {path}");
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

        private static void WriteQueryLayouts(string path, List<Type> rowTypes)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteGeneratedHeader(writer);
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine("namespace GAS.Runtime.Generated");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// CASE-22/23/26/27/28: 预定义的 EntityQueryDesc。");
            writer.WriteLine("/// 在 System.OnCreate 中通过 SystemState.GetEntityQuery() 创建。");
            writer.WriteLine("/// 标注 All/None 约束、预估数量和 Burst 兼容性。");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("public static class GasQueryLayouts");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var rowType in rowTypes)
            {
                var schemaName = GasBlobSchemaGenerator.RowTypeToBlobSchemaName(rowType);
                var queryName = schemaName + "Query";
                var codeComponent = DeriveCodeComponentTypeStr(rowType);

                writer.WriteLine("");
                writer.WriteLine("/// <summary>");
                writer.WriteLine($"/// {rowType.Name} 的 EntityQuery 布局。");
                writer.WriteLine($"/// All: CGeneratedDefinitionBlob<{schemaName}>, {codeComponent}");
                writer.WriteLine($"/// None: CDestroyTag（标记已销毁 Entity，不应被查询命中）");
                writer.WriteLine($"/// Burst: 兼容（全部 ReadOnly + 无 managed 组件）");
                writer.WriteLine("/// </summary>");
                writer.WriteLine($"public static readonly EntityQueryDesc {queryName} = new EntityQueryDesc");
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine("All = new ComponentType[]");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine($"ComponentType.ReadOnly<CGeneratedDefinitionBlob<{schemaName}>>(),");
                writer.WriteLine($"ComponentType.ReadOnly<{codeComponent}>(),");
                writer.Indent--;
                writer.WriteLine("},");
                writer.WriteLine("");
                writer.WriteLine("None = new ComponentType[]");
                writer.WriteLine("{");
                writer.Indent++;
                writer.WriteLine("ComponentType.ReadOnly<CDestroyTag>(),");
                writer.Indent--;
                writer.WriteLine("},");
                writer.WriteLine("");
                writer.WriteLine("Options = EntityQueryOptions.IncludeDisabledEntities,");

                writer.Indent--;
                writer.WriteLine("};");
            }

            // SystemState.GetEntityQuery 辅助扩展
            writer.WriteLine("");
            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// 从 EntityQueryDesc 创建 EntityQuery 的辅助方法。");
            writer.WriteLine("/// 用法: State.GetQuery(GasQueryLayouts.BlobAbilityDefinitionQuery)");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("public static EntityQuery GetQuery(this SystemState state, EntityQueryDesc desc)");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("return state.GetEntityQuery(desc);");
            writer.Indent--;
            writer.WriteLine("}");

            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        private static string DeriveCodeComponentTypeStr(Type rowType)
        {
            var name = rowType.Name;
            if (name.Contains("Ability")) return "CAbilityCode";
            if (name.Contains("GameplayEffect")) return "CGameplayEffectPrototype";
            return "CDefinitionCode";
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
