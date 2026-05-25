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
    /// Phase 4: 从 Baker 方法使用的组件类型生成 ComponentTypeSet 常量。
    /// 为每种 DefinitionKind 聚合一个 ComponentTypeSet，用于：
    /// - QueryLayout 的 All/Any/None 约束
    /// - 批量销毁（ecb.RemoveComponent）
    /// - Archetype 预验证
    ///
    /// 遵循 CASE-33: ComponentTypeSet 批量操作的官方推荐模式。
    /// </summary>
    public static class GasComponentTypeSetGenerator
    {
        [MenuItem("EXTool/EX-GAS/生成脚本/Glue/生成 ComponentTypeSet")]
        public static void GenerateComponentTypeSets()
        {
            var rowTypes = FindDefinitionRowTypes();
            if (rowTypes.Count == 0)
            {
                Debug.LogWarning("[GasComponentTypeSetGenerator] 未找到任何 *DefinitionRow 类型。");
                return;
            }

            var setting = GASSettingAsset.LoadOrCreate();
            var outputDir = setting.CodeGeneratePath;
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var path = Path.Combine(outputDir, "ComponentTypeSets.gen.cs");
            WriteComponentTypeSets(path, rowTypes);

            AssetDatabase.Refresh();
            Debug.Log($"[GasComponentTypeSetGenerator] 生成完成: {rowTypes.Count} 个 ComponentTypeSet\n  {path}");
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

        private static void WriteComponentTypeSets(string path, List<Type> rowTypes)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteGeneratedHeader(writer);
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine("namespace GAS.Runtime.Generated");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// CASE-33: 为每种 Definition Entity 类型预定义的 ComponentTypeSet。");
            writer.WriteLine("/// 用于 QueryLayout、批量操作和 Archetype 验证。");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("public static class GasComponentTypeSets");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var rowType in rowTypes)
            {
                var schemaName = GasBlobSchemaGenerator.RowTypeToBlobSchemaName(rowType);
                var setName = schemaName + "Components";
                var components = DeriveComponentTypes(rowType, schemaName);

                writer.WriteLine("");
                writer.WriteLine("/// <summary>");
                writer.WriteLine($"/// {rowType.Name} → Entity 的组件集合。");
                writer.WriteLine("/// </summary>");
                writer.WriteLine($"public static readonly ComponentTypeSet {setName} = new ComponentTypeSet(");
                writer.Indent++;

                for (int i = 0; i < components.Count; i++)
                {
                    var comma = i < components.Count - 1 ? "," : "";
                    writer.WriteLine($"{components[i]}{comma}");
                }

                writer.Indent--;
                writer.WriteLine(");");
            }

            writer.Indent--;
            writer.WriteLine("}");
            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// 根据 Row 类型和生成的 BlobSchema 推导 Entity 应持有的组件类型集合。
        /// 当前生成最小集合（Blob 引用组件 + Code 组件），后续可扩展。
        /// </summary>
        private static List<string> DeriveComponentTypes(Type rowType, string schemaName)
        {
            var components = new List<string>();

            // Blob 引用组件 — 使用泛型通用组件
            components.Add($"ComponentType.ReadWrite<CGeneratedDefinitionBlob<{schemaName}>>()");

            // Code 组件
            var codeComponent = DeriveCodeComponentFullType(rowType);
            if (!string.IsNullOrEmpty(codeComponent))
                components.Add($"ComponentType.ReadWrite<{codeComponent}>()");
            else
                components.Add("ComponentType.ReadWrite<CDefinitionCode>()");

            return components;
        }

        private static string DeriveCodeComponentFullType(Type rowType)
        {
            var name = rowType.Name;
            if (name.Contains("Ability")) return "CAbilityCode";
            if (name.Contains("GameplayEffect")) return "CGameplayEffectPrototype";
            return "";
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
