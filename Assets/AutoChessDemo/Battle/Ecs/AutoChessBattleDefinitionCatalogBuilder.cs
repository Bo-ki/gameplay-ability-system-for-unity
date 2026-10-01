using System;
using GAS.Runtime;
using Unity.Collections;
using Unity.Entities;

namespace GAS.AutoChessDemo
{
    /// <summary>
    /// 持有由 Luban 生成代码提供的 Runtime v1 Catalog 数据，不承载任何手写玩法语义。
    /// </summary>
    internal sealed class AutoChessDefinitionCatalogSource
    {
        internal GasAttributeLayoutEntryBlob[] Attributes = Array.Empty<GasAttributeLayoutEntryBlob>();
        internal GasTagCatalogEntryBlob[] Tags = Array.Empty<GasTagCatalogEntryBlob>();
        internal int[] TagAncestors = Array.Empty<int>();
        internal GasDefinitionIndexEntry[] AbilityIndex = Array.Empty<GasDefinitionIndexEntry>();
        internal GasAbilityDefinitionBlob[] Abilities = Array.Empty<GasAbilityDefinitionBlob>();
        internal GasDefinitionIndexEntry[] GameplayEffectIndex = Array.Empty<GasDefinitionIndexEntry>();
        internal GasGameplayEffectDefinitionBlob[] GameplayEffects = Array.Empty<GasGameplayEffectDefinitionBlob>();
        internal GasRequirementBlob[] Requirements = Array.Empty<GasRequirementBlob>();
        internal int[] RequirementTagIndices = Array.Empty<int>();
        internal GasCaptureDescriptorBlob[] CaptureDescriptors = Array.Empty<GasCaptureDescriptorBlob>();
        internal GasModifierDefinitionBlob[] Modifiers = Array.Empty<GasModifierDefinitionBlob>();
        internal GasDirectEffectProgramNodeBlob[] DirectEffectProgramNodes = Array.Empty<GasDirectEffectProgramNodeBlob>();
        internal GasCueReferenceBlob[] CueReferences = Array.Empty<GasCueReferenceBlob>();
        internal GasValueViewDescriptorBlob[] ValueViews = Array.Empty<GasValueViewDescriptorBlob>();
        internal GasEvaluatorInstructionBlob[] EvaluatorInstructions = Array.Empty<GasEvaluatorInstructionBlob>();
        internal GasSetByCallerDescriptorBlob[] SetByCallerDescriptors = Array.Empty<GasSetByCallerDescriptorBlob>();
        internal GasTargetDataDescriptorBlob[] TargetDataDescriptors = Array.Empty<GasTargetDataDescriptorBlob>();
        internal GasEffectContextFieldDescriptorBlob[] EffectContextFieldDescriptors = Array.Empty<GasEffectContextFieldDescriptorBlob>();
    }

    /// <summary>
    /// 把 generated pure data 两遍物化为带 canonical 身份的 immutable Blob，避免候选 header 自证。
    /// </summary>
    internal static class AutoChessDefinitionCatalogCompiler
    {
        /// <summary>
        /// 先用 Temp draft 计算外部期望身份，再用同一份 generated source 创建最终 Blob。
        /// </summary>
        internal static BlobAssetReference<GasDefinitionCatalogBlob> Build(
            AutoChessDefinitionCatalogSource source,
            Allocator allocator,
            out GasCatalogValidationExpectation expectation)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            using var draft = BuildBlob(source, Allocator.Temp, 0UL, 0UL, 0UL);
            ref var draftCatalog = ref draft.Value;
            var contentHash = GasDefinitionCatalogContentHasher.Compute(ref draftCatalog);
            var attributeHash = GasDefinitionCatalogContentHasher.ComputeAttributeLayout(
                ref draftCatalog.AttributeLayout);
            var tagHash = GasDefinitionCatalogContentHasher.ComputeTagCatalog(
                ref draftCatalog.TagCatalog);
            expectation = new GasCatalogValidationExpectation(
                GasDefinitionCatalogSchema.Version,
                GasDefinitionCatalogSchema.Hash,
                contentHash,
                attributeHash,
                tagHash);
            return BuildBlob(source, allocator, contentHash, attributeHash, tagHash);
        }

        /// <summary>
        /// 在 Blob 创建前写入已经冻结的身份字段，保证 Unity Blob header 与 payload 同步。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> BuildBlob(
            AutoChessDefinitionCatalogSource source,
            Allocator allocator,
            ulong contentHash,
            ulong attributeHash,
            ulong tagHash)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                root.SchemaVersion = GasDefinitionCatalogSchema.Version;
                root.SchemaHash = GasDefinitionCatalogSchema.Hash;
                root.ContentHash = contentHash;
                root.AttributeLayout.LayoutHash = attributeHash;
                root.TagCatalog.CatalogHash = tagHash;
                CopyCatalogArrays(builder, ref root, source);
                return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(allocator);
            }
            finally
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// 将 generated source 的所有闭集数组按 schema 字段逐一复制进 Blob。
        /// </summary>
        private static void CopyCatalogArrays(
            BlobBuilder builder,
            ref GasDefinitionCatalogBlob root,
            AutoChessDefinitionCatalogSource source)
        {
            CopyArray(builder, ref root.AttributeLayout.Entries, source.Attributes);
            CopyArray(builder, ref root.TagCatalog.Entries, source.Tags);
            CopyArray(builder, ref root.TagCatalog.AncestorIndices, source.TagAncestors);
            CopyArray(builder, ref root.AbilityIndex, source.AbilityIndex);
            CopyArray(builder, ref root.Abilities, source.Abilities);
            CopyArray(builder, ref root.GameplayEffectIndex, source.GameplayEffectIndex);
            CopyArray(builder, ref root.GameplayEffects, source.GameplayEffects);
            CopyArray(builder, ref root.Requirements, source.Requirements);
            CopyArray(builder, ref root.RequirementTagIndices, source.RequirementTagIndices);
            CopyArray(builder, ref root.CaptureDescriptors, source.CaptureDescriptors);
            CopyArray(builder, ref root.Modifiers, source.Modifiers);
            CopyArray(builder, ref root.DirectEffectProgramNodes, source.DirectEffectProgramNodes);
            CopyArray(builder, ref root.CueReferences, source.CueReferences);
            CopyArray(builder, ref root.ValueViews, source.ValueViews);
            CopyArray(builder, ref root.EvaluatorInstructions, source.EvaluatorInstructions);
            CopyArray(builder, ref root.SetByCallerDescriptors, source.SetByCallerDescriptors);
            CopyArray(builder, ref root.TargetDataDescriptors, source.TargetDataDescriptors);
            CopyArray(builder, ref root.EffectContextFieldDescriptors, source.EffectContextFieldDescriptors);
        }

        /// <summary>
        /// 复制一个 unmanaged generated 数组并保持原始稳定顺序。
        /// </summary>
        private static void CopyArray<T>(
            BlobBuilder builder,
            ref BlobArray<T> destination,
            T[] source)
            where T : unmanaged
        {
            var values = source ?? Array.Empty<T>();
            var target = builder.Allocate(ref destination, values.Length);
            for (var index = 0; index < values.Length; index++)
                target[index] = values[index];
        }
    }

    /// <summary>
    /// 安装并持有唯一 AutoChess Runtime v1 immutable Catalog；玩法数据只来自 generated Luban glue。
    /// </summary>
    internal static class AutoChessBattleDefinitionCatalogBuilder
    {
        private static BlobAssetReference<GasDefinitionCatalogBlob> s_catalog;
        private static GasCatalogValidationExpectation s_expectation;

        /// <summary>
        /// 构造并安装唯一 AutoChess v1 Catalog，随后由 Stage-B 引用该 Blob 与外部冻结期望。
        /// </summary>
        internal static bool Install(EntityManager entityManager)
        {
            if (entityManager.World == null || !entityManager.World.IsCreated)
                return false;

            DisposeCatalog();
            s_catalog = AutoChessGeneratedDefinitionCatalog.Build(
                Allocator.Persistent,
                out s_expectation);
            return s_catalog.IsCreated;
        }

        /// <summary>
        /// 释放 AutoChess v1 Catalog，禁止留下跨 World 或跨 schema 的引用。
        /// </summary>
        internal static void Uninstall(EntityManager entityManager)
        {
            DisposeCatalog();
        }

        /// <summary>
        /// 返回当前 AutoChess v1 Catalog，未安装时返回未创建引用。
        /// </summary>
        internal static BlobAssetReference<GasDefinitionCatalogBlob> Catalog => s_catalog;

        /// <summary>
        /// 返回从 generated draft 冻结、且未读取最终候选 header 的 Stage-B 期望身份。
        /// </summary>
        internal static GasCatalogValidationExpectation Expectation => s_expectation;

        /// <summary>
        /// 释放当前 immutable Blob，并清空配套的外部期望身份。
        /// </summary>
        private static void DisposeCatalog()
        {
            if (s_catalog.IsCreated)
                s_catalog.Dispose();
            s_catalog = default;
            s_expectation = default;
        }
    }
}
