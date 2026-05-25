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
    /// Phase 3: 生成 Baker-like 转换方法 + Baking System 骨架。
    /// 为每种 DefinitionRow 生成 ECB-based 的 Bake 方法（CASE-39/40 合规：无状态、仅 ECB 写入）。
    /// 生成的 Baker 方法创建 Entity 并附加 BlobAssetReference + Code 组件；
    /// 复杂的业务组件映射在手动编写的 BakingSystem 中完成。
    /// </summary>
    public static class GasBakerBakingSystemGenerator
    {
        [MenuItem("EXTool/EX-GAS/生成脚本/Glue/生成 Baker + BakingSystem")]
        public static void GenerateBakersAndBakingSystem()
        {
            var rowTypes = FindDefinitionRowTypes();
            if (rowTypes.Count == 0)
            {
                Debug.LogWarning("[GasBakerBakingSystemGenerator] 未找到任何 *DefinitionRow 类型。");
                return;
            }

            var setting = GASSettingAsset.LoadOrCreate();
            var outputDir = setting.CodeGeneratePath;
            if (!Directory.Exists(outputDir))
                Directory.CreateDirectory(outputDir);

            var bakersPath = Path.Combine(outputDir, "Bakers.gen.cs");
            WriteBakers(bakersPath, rowTypes);

            AssetDatabase.Refresh();
            Debug.Log($"[GasBakerBakingSystemGenerator] 生成完成: {rowTypes.Count} 个 Baker 方法\n  {bakersPath}");
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

        private static void WriteBakers(string path, List<Type> rowTypes)
        {
            using var writer = new IndentedWriter(new StreamWriter(path));
            WriteGeneratedHeader(writer);
            writer.WriteLine("using Unity.Entities;");
            writer.WriteLine("");
            writer.WriteLine("namespace GAS.Runtime.Generated");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// 生成的 Baker 方法。每个方法创建一个 Entity，附加 BlobAsset 引用和 Code 标识。");
            writer.WriteLine("/// 使用 ECB（非直接 EntityManager），CASE-39/40 合规（无状态、仅写入）。");
            writer.WriteLine("/// 复杂业务组件映射在 GASGeneratedDefinitionBakingSystem 中完成。");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("public static class GasGeneratedBakers");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var rowType in rowTypes)
            {
                var schemaName = GasBlobSchemaGenerator.RowTypeToBlobSchemaName(rowType);
                var methodName = $"Bake{schemaName}";
                var rowTypeName = rowType.FullName ?? rowType.Name;
                var codeField = FindCodeMember(rowType);
                var codeComponentType = DeriveCodeComponentType(rowType);
                var blobComponentType = DeriveBlobComponentType(rowType);

                writer.WriteLine("");
                writer.WriteLine("/// <summary>");
                writer.WriteLine($"/// 从 {rowType.Name} 创建 Definition Entity。");
                writer.WriteLine($"/// - 附加 BlobAssetReference");
                writer.WriteLine($"/// - 附加 Code 标识组件");
                writer.WriteLine("/// </summary>");
                writer.WriteLine($"public static Entity {methodName}(");
                writer.Indent++;
                writer.WriteLine("ref EntityCommandBuffer ecb,");
                writer.WriteLine($"{rowTypeName} row,");
                writer.WriteLine($"BlobAssetReference<{schemaName}> blob)");
                writer.Indent--;
                writer.WriteLine("{");
                writer.Indent++;

                writer.WriteLine("var entity = ecb.CreateEntity();");
                writer.WriteLine("");

                // Add Blob component
                if (!string.IsNullOrEmpty(blobComponentType))
                    writer.WriteLine($"ecb.AddComponent(entity, new {blobComponentType} {{ Config = blob }});");
                else
                    writer.WriteLine($"ecb.AddComponent(entity, new CGeneratedDefinitionBlob<{schemaName}> {{ Value = blob }});");

                // Add Code component
                if (!string.IsNullOrEmpty(codeComponentType))
                    writer.WriteLine($"ecb.AddComponent(entity, new {codeComponentType} {{ Code = row.{codeField} }});");
                else
                    writer.WriteLine($"ecb.AddComponent(entity, new CDefinitionCode {{ Value = row.{codeField} }});");

                writer.WriteLine("");
                writer.WriteLine("return entity;");
                writer.Indent--;
                writer.WriteLine("}");
            }

            writer.Indent--;
            writer.WriteLine("}");

            // Generic fallback components
            writer.WriteLine("");
            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// 通用 Blob 引用组件（当无专用组件类型时使用）。");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("public struct CGeneratedDefinitionBlob<T> : IComponentData");
            writer.WriteLine("    where T : unmanaged");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public BlobAssetReference<T> Value;");
            writer.Indent--;
            writer.WriteLine("}");
            writer.WriteLine("");
            writer.WriteLine("/// <summary>");
            writer.WriteLine("/// 通用 Definition Code 组件（当无专用 Code 组件时使用）。");
            writer.WriteLine("/// </summary>");
            writer.WriteLine("public struct CDefinitionCode : IComponentData");
            writer.WriteLine("{");
            writer.Indent++;
            writer.WriteLine("public int Value;");
            writer.Indent--;
            writer.WriteLine("}");

            writer.Indent--;
            writer.WriteLine("}");
        }

        /// <summary>
        /// 根据 Row 类型名推断专用的 Code 组件类型（如 CAbilityCode, CGameplayEffectCode）。
        /// 如果项目中有对应的 IComponentData，则使用专用类型；否则回退到通用 CDefinitionCode。
        /// </summary>
        private static string DeriveCodeComponentType(Type rowType)
        {
            var name = rowType.Name;
            if (name.Contains("Ability")) return "CAbilityCode";
            if (name.Contains("GameplayEffect")) return "CGameplayEffectPrototype";
            if (name.Contains("AttributeSet")) return ""; // 使用 CDefinitionCode
            if (name.Contains("Attribute") && !name.Contains("AttributeSet")) return ""; // 使用 CDefinitionCode
            if (name.Contains("GameplayTag")) return ""; // 使用 CDefinitionCode
            if (name.Contains("GameplayCue")) return ""; // 使用 CDefinitionCode
            if (name.Contains("Timeline")) return ""; // 使用 CDefinitionCode
            if (name.Contains("Summon")) return ""; // 使用 CDefinitionCode
            return ""; // 回退到通用
        }

        /// <summary>
        /// 根据 Row 类型名推断专用的 Blob 组件类型。
        /// </summary>
        private static string DeriveBlobComponentType(Type rowType)
        {
            var name = rowType.Name;
            if (name.Contains("Ability")) return "CAbilityConfig";
            if (name.Contains("GameplayEffect")) return "CGEConfigRef";
            return ""; // 回退到 CGeneratedDefinitionBlob<T>
        }

        private static string FindCodeMember(Type rowType)
        {
            var fields = rowType.GetFields(BindingFlags.Public | BindingFlags.Instance);
            var fieldNames = new HashSet<string>(fields.Select(f => f.Name));

            var candidates = new[]
            {
                "AbilityCode", "GameplayEffectCode", "AttributeSetCode", "AttributeCode",
                "GameplayTagCode", "GameplayCueCode", "TimelineId", "SummonGameplayEffectCode",
            };

            foreach (var c in candidates)
                if (fieldNames.Contains(c))
                    return c;

            var codeField = fields.FirstOrDefault(f => f.Name.EndsWith("Code"));
            if (codeField != null) return codeField.Name;

            var idField = fields.FirstOrDefault(f => f.Name.EndsWith("Id"));
            if (idField != null) return idField.Name;

            return fields.Length > 0 ? fields[0].Name : "UnknownCode";
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
