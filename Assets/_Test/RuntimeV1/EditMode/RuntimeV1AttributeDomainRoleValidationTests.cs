using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Attribute domain role 的 Health 单一性与零下限契约，防止死亡 crossing 依赖未验证的布局元数据。
    /// </summary>
    [TestFixture]
    public class RuntimeV1AttributeDomainRoleValidationTests
    {
        private const ulong SchemaHash = 0x6101UL;
        private const ulong ContentHash = 0x6202UL;
        private const ulong AttributeLayoutHash = 0x6303UL;
        private const ulong TagCatalogHash = 0x6404UL;

        /// <summary>
        /// 验证唯一 Health 属性在启用零下限 clamp 时可以通过 Catalog 安装校验。
        /// </summary>
        [Test]
        public void HealthDomainRole_唯一且零下限时通过()
        {
            using var catalog = CreateCatalog(CreateHealthEntry(11, 0, 50f, 0f, 100f, 1));
            ref var root = ref catalog.Value;

            Assert.That(Validate(ref root).Succeeded, Is.True);
        }

        /// <summary>
        /// 验证 Catalog 不允许声明两个 Health domain 属性，避免 death crossing 出现双事实源。
        /// </summary>
        [Test]
        public void HealthDomainRole_重复声明时拒绝()
        {
            using var catalog = CreateCatalog(
                CreateHealthEntry(11, 0, 50f, 0f, 100f, 1),
                CreateHealthEntry(12, 1, 75f, 0f, 100f, 1));
            ref var root = ref catalog.Value;

            var result = Validate(ref root);

            Assert.That(result.Error, Is.EqualTo(GasCatalogValidationError.AttributeLayoutInvalid));
            Assert.That(result.ElementIndex, Is.EqualTo(1));
        }

        /// <summary>
        /// 验证 Health 的最小值必须为零且必须开启最小值 clamp。
        /// </summary>
        [TestCase(1f, (byte)1)]
        [TestCase(0f, (byte)0)]
        public void HealthDomainRole_非零Minimum或未启用Clamp时拒绝(
            float minimumValue,
            byte clampMinimum)
        {
            using var catalog = CreateCatalog(
                CreateHealthEntry(11, 0, 50f, minimumValue, 100f, clampMinimum));
            ref var root = ref catalog.Value;

            var result = Validate(ref root);

            Assert.That(result.Error, Is.EqualTo(GasCatalogValidationError.AttributeLayoutInvalid));
            Assert.That(result.ElementIndex, Is.Zero);
        }

        /// <summary>
        /// 使用测试固定 header 调用与安装端相同的 Catalog validator。
        /// </summary>
        private static GasCatalogValidationResult Validate(ref GasDefinitionCatalogBlob catalog)
        {
            var expectation = new GasCatalogValidationExpectation(
                GasDefinitionCatalogSchema.Version,
                SchemaHash,
                ContentHash,
                AttributeLayoutHash,
                TagCatalogHash);
            return GasDefinitionCatalogValidator.Validate(ref catalog, in expectation);
        }

        /// <summary>
        /// 构造仅含 AttributeLayout 的最小 immutable Catalog，其他闭世界数组显式为空。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateCatalog(
            params GasAttributeLayoutEntryBlob[] entries)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                root.SchemaVersion = GasDefinitionCatalogSchema.Version;
                root.SchemaHash = SchemaHash;
                root.ContentHash = ContentHash;
                root.AttributeLayout.LayoutHash = AttributeLayoutHash;
                root.TagCatalog.CatalogHash = TagCatalogHash;

                var attributes = builder.Allocate(ref root.AttributeLayout.Entries, entries.Length);
                for (var index = 0; index < entries.Length; index++)
                    attributes[index] = entries[index];

                AllocateEmptyArrays(ref builder, ref root);
                return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// 创建一条用于测试 Health 领域约束的 AttributeLayout entry。
        /// </summary>
        private static GasAttributeLayoutEntryBlob CreateHealthEntry(
            int attributeId,
            int layoutIndex,
            float defaultValue,
            float minimumValue,
            float maximumValue,
            byte clampMinimum)
        {
            return new GasAttributeLayoutEntryBlob
            {
                AttributeId = attributeId,
                LayoutIndex = layoutIndex,
                DomainRole = GasAttributeDomainRole.Health,
                DefaultValue = defaultValue,
                MinimumValue = minimumValue,
                MaximumValue = maximumValue,
                ClampMinimum = clampMinimum,
                ClampMaximum = 1,
            };
        }

        /// <summary>
        /// 显式分配 validator 会访问的全部 BlobArray，保持空 Catalog 形状可验证。
        /// </summary>
        private static void AllocateEmptyArrays(
            ref BlobBuilder builder,
            ref GasDefinitionCatalogBlob root)
        {
            builder.Allocate(ref root.TagCatalog.Entries, 0);
            builder.Allocate(ref root.TagCatalog.AncestorIndices, 0);
            builder.Allocate(ref root.AbilityIndex, 0);
            builder.Allocate(ref root.Abilities, 0);
            builder.Allocate(ref root.GameplayEffectIndex, 0);
            builder.Allocate(ref root.GameplayEffects, 0);
            builder.Allocate(ref root.Requirements, 0);
            builder.Allocate(ref root.RequirementTagIndices, 0);
            builder.Allocate(ref root.CaptureDescriptors, 0);
            builder.Allocate(ref root.Modifiers, 0);
            builder.Allocate(ref root.DirectEffectProgramNodes, 0);
            builder.Allocate(ref root.CueReferences, 0);
            builder.Allocate(ref root.ValueViews, 0);
            builder.Allocate(ref root.EvaluatorInstructions, 0);
            builder.Allocate(ref root.SetByCallerDescriptors, 0);
            builder.Allocate(ref root.TargetDataDescriptors, 0);
            builder.Allocate(ref root.EffectContextFieldDescriptors, 0);
        }
    }
}
