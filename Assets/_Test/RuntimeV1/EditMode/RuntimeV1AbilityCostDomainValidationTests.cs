using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Runtime v1 Ability cost 只能引用非 Health 属性，避免绕过统一死亡事务与事实链。
    /// </summary>
    [TestFixture]
    public class RuntimeV1AbilityCostDomainValidationTests
    {
        private const int AbilityDefinitionId = 7301;
        private const ulong SchemaHash = 0x7101UL;

        /// <summary>
        /// 验证启用的 cost 引用 Health 时在 Catalog 边界拒绝并返回 Definition 与属性定位。
        /// </summary>
        [Test]
        public void AbilityCost_引用Health时拒绝并定位Definition与Attribute()
        {
            using var catalog = CreateCatalog(costAttributeLayoutIndex: 0);
            ref var root = ref catalog.Value;

            var result = Validate(ref root);

            Assert.That(result.Error, Is.EqualTo(GasCatalogValidationError.AbilityCostTargetsHealth));
            Assert.That(result.RangeKind, Is.EqualTo(GasCatalogRangeKind.None));
            Assert.That(result.DefinitionId, Is.EqualTo(AbilityDefinitionId));
            Assert.That(result.ElementIndex, Is.Zero);
        }

        /// <summary>
        /// 验证 Catalog 可以包含 Health，只要 cost 精确引用非 Health 资源属性即可安装。
        /// </summary>
        [Test]
        public void AbilityCost_Catalog含Health但引用资源属性时通过()
        {
            using var catalog = CreateCatalog(costAttributeLayoutIndex: 1);
            ref var root = ref catalog.Value;

            Assert.That(Validate(ref root).Succeeded, Is.True);
        }

        /// <summary>
        /// 使用已 canonical 封印的测试 header expectation 执行安装前校验。
        /// </summary>
        private static GasCatalogValidationResult Validate(ref GasDefinitionCatalogBlob catalog)
        {
            var expectation = new GasCatalogValidationExpectation(
                catalog.SchemaVersion,
                catalog.SchemaHash,
                catalog.ContentHash,
                catalog.AttributeLayout.LayoutHash,
                catalog.TagCatalog.CatalogHash);
            return GasDefinitionCatalogValidator.Validate(ref catalog, in expectation);
        }

        /// <summary>
        /// 构造同时包含 Health 与资源属性、仅启用一个 owner-local cost 的最小 Catalog。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateCatalog(
            int costAttributeLayoutIndex)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                PopulateHeader(ref root);
                PopulateAttributes(ref builder, ref root);
                PopulateAbility(ref builder, ref root, costAttributeLayoutIndex);
                AllocateEmptyArrays(ref builder, ref root);
                var catalog = builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
                GasDefinitionCatalogContentHasher.Stamp(ref catalog.Value);
                return catalog;
            }
            finally
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// 写入测试 Catalog 的固定版本与 hash。
        /// </summary>
        private static void PopulateHeader(ref GasDefinitionCatalogBlob root)
        {
            root.SchemaVersion = GasDefinitionCatalogSchema.Version;
            root.SchemaHash = SchemaHash;
        }

        /// <summary>
        /// 写入唯一 Health 与一个普通资源属性，供正负例仅切换 cost 引用。
        /// </summary>
        private static void PopulateAttributes(
            ref BlobBuilder builder,
            ref GasDefinitionCatalogBlob root)
        {
            var attributes = builder.Allocate(ref root.AttributeLayout.Entries, 2);
            attributes[0] = new GasAttributeLayoutEntryBlob
            {
                AttributeId = 11,
                LayoutIndex = 0,
                DomainRole = GasAttributeDomainRole.Health,
                DefaultValue = 50f,
                MinimumValue = 0f,
                MaximumValue = 100f,
                ClampMinimum = 1,
                ClampMaximum = 1,
            };
            attributes[1] = new GasAttributeLayoutEntryBlob
            {
                AttributeId = 12,
                LayoutIndex = 1,
                DomainRole = GasAttributeDomainRole.None,
                DefaultValue = 100f,
                MinimumValue = 0f,
                MaximumValue = 100f,
                ClampMinimum = 1,
                ClampMaximum = 1,
            };
        }

        /// <summary>
        /// 写入一个合法 Self Ability，并把 cost 指向测试指定的 Attribute layout index。
        /// </summary>
        private static void PopulateAbility(
            ref BlobBuilder builder,
            ref GasDefinitionCatalogBlob root,
            int costAttributeLayoutIndex)
        {
            builder.Allocate(ref root.AbilityIndex, 1)[0] = new GasDefinitionIndexEntry
            {
                DefinitionId = AbilityDefinitionId,
                DefinitionIndex = 0,
            };
            builder.Allocate(ref root.Abilities, 1)[0] = new GasAbilityDefinitionBlob
            {
                DefinitionId = AbilityDefinitionId,
                Level = 1,
                TargetPolicy = new GasTargetPolicyBlob
                {
                    LogicalTarget = GasLogicalTargetPolicy.Self,
                    Avatar = GasAvatarTargetPolicy.FollowAsc,
                    Spatial = GasSpatialTargetPolicy.None,
                    Life = GasTargetLifePolicy.AliveOnly,
                },
                CostMutationContract = new GasCostMutationContractBlob
                {
                    Enabled = 1,
                    AttributeLayoutIndex = costAttributeLayoutIndex,
                    CurrentDelta = -1f,
                },
            };
        }

        /// <summary>
        /// 显式分配 validator 会访问的其余 BlobArray，保持最小 Catalog 形状闭合。
        /// </summary>
        private static void AllocateEmptyArrays(
            ref BlobBuilder builder,
            ref GasDefinitionCatalogBlob root)
        {
            builder.Allocate(ref root.TagCatalog.Entries, 0);
            builder.Allocate(ref root.TagCatalog.AncestorIndices, 0);
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
