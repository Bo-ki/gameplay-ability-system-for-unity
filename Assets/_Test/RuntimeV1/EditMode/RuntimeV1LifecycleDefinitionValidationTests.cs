using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证未物化的 ActiveEffect lifecycle 语义在 Catalog install 阶段 fail-closed。
    /// </summary>
    [TestFixture]
    public class RuntimeV1LifecycleDefinitionValidationTests
    {
        /// <summary>
        /// 验证 StackingId 没有对应物理字段时不能进入 Runtime v1 Catalog。
        /// </summary>
        [Test]
        public void StackingId_未物化维度在安装期拒绝()
        {
            using var catalog = CreateCatalog();
            ref var root = ref catalog.Value;
            root.GameplayEffects[0].StackKey |= GasStackKeyFields.StackingId;

            var result = Validate(ref root);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(GasCatalogValidationError.DefinitionPolicyInvalid));
            Assert.That(result.DefinitionId, Is.EqualTo(100));
        }

        /// <summary>
        /// 验证 PauseDuration 在缺少 inhibition anchor 时不能静默按 continue 解释。
        /// </summary>
        [Test]
        public void PauseDuration_缺少暂停锚点时安装期拒绝()
        {
            using var catalog = CreateCatalog();
            ref var root = ref catalog.Value;
            root.GameplayEffects[0].InhibitTimePolicy = GasInhibitTimePolicy.PauseDuration;

            var result = Validate(ref root);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Is.EqualTo(GasCatalogValidationError.DefinitionPolicyInvalid));
            Assert.That(result.DefinitionId, Is.EqualTo(100));
        }

        /// <summary>
        /// 创建最小但完整的 Duration stack Definition，供安装校验变体复用。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateCatalog()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
            root.SchemaVersion = GasDefinitionCatalogSchema.Version;
            root.SchemaHash = 1;
            root.ContentHash = 2;
            builder.Allocate(ref root.AttributeLayout.Entries, 1)[0] = new GasAttributeLayoutEntryBlob
            {
                AttributeId = 1,
                LayoutIndex = 0,
                MinimumValue = -100f,
                MaximumValue = 100f,
            };
            root.AttributeLayout.LayoutHash = 3;
            builder.Allocate(ref root.TagCatalog.Entries, 0);
            builder.Allocate(ref root.TagCatalog.AncestorIndices, 0);
            root.TagCatalog.CatalogHash = 4;
            builder.Allocate(ref root.GameplayEffectIndex, 1)[0] = new GasDefinitionIndexEntry
            {
                DefinitionId = 100,
                DefinitionIndex = 0,
            };
            builder.Allocate(ref root.GameplayEffects, 1)[0] = new GasGameplayEffectDefinitionBlob
            {
                DefinitionId = 100,
                Lifetime = GasEffectLifetimePolicy.Duration,
                TargetPolicy = new GasTargetPolicyBlob
                {
                    LogicalTarget = GasLogicalTargetPolicy.Self,
                    Avatar = GasAvatarTargetPolicy.FollowAsc,
                    Spatial = GasSpatialTargetPolicy.None,
                    Life = GasTargetLifePolicy.AliveOnly,
                },
                DurationTicks = 5,
                StackLimit = 2,
                StackKey = GasStackKeyFields.Definition |
                           GasStackKeyFields.TargetAsc |
                           GasStackKeyFields.SourceAsc,
                StackPolicy = GasStackPolicy.AggregateBySource,
                StackPayloadPolicy = GasStackPayloadPolicy.ReplaceLatestPayloadAndProvenance,
                StackLimitApplicationPolicy = GasStackLimitApplicationPolicy.RejectAtLimit,
                ExpiryPolicy = GasExpiryPolicy.Remove,
                ExpiryPeriodPolicy = GasExpiryPeriodPolicy.Stop,
                ExpirySameTickPolicy = GasExpirySameTickPolicy.ExpiryBeforePeriodDue,
                InhibitTimePolicy = GasInhibitTimePolicy.DurationContinues,
                InhibitedPeriodPolicy = GasInhibitedPeriodPolicy.None,
                MissedPeriodPolicy = GasMissedPeriodPolicy.SkipNoCatchUp,
            };
            AllocateEmptyArrays(ref root, builder);
            return builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
        }

        /// <summary>
        /// 执行与测试 header 完全匹配的 Catalog validator。
        /// </summary>
        private static GasCatalogValidationResult Validate(ref GasDefinitionCatalogBlob catalog)
        {
            var expectation = new GasCatalogValidationExpectation(1, 1, 2, 3, 4);
            return GasDefinitionCatalogValidator.Validate(ref catalog, in expectation);
        }

        /// <summary>
        /// 分配所有未使用根数组，避免 validator 读取未构造的 BlobArray。
        /// </summary>
        private static void AllocateEmptyArrays(
            ref GasDefinitionCatalogBlob root,
            BlobBuilder builder)
        {
            builder.Allocate(ref root.Requirements, 0);
            builder.Allocate(ref root.RequirementTagIndices, 0);
            builder.Allocate(ref root.AbilityIndex, 0);
            builder.Allocate(ref root.Abilities, 0);
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
