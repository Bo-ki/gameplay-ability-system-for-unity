using System;
using System.Linq;
using GAS.Runtime;
using GAS.Runtime.Generated;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 冻结迁移前调度组与 Ability slot 的旧物理形状，供破坏性替换时主动转红。
    /// </summary>
    [TestFixture]
    public class LegacyRuntimeShapeCharacterizationTests
    {
        private static readonly Type[] LegacyScheduleGroupTypes =
        {
            typeof(GASFramePrepareSystemGroup),
            typeof(GASCommandResolveSystemGroup),
            typeof(GASCoreSimulationSystemGroup),
            typeof(GASStructuralCommitSystemGroup),
            typeof(GASBoundaryProjectionSystemGroup),
        };

        /// <summary>
        /// 验证旧 Runtime 仍由五个 GAS 主调度组组成。
        /// </summary>
        [Test]
        public void 旧Runtime调度契约_恰好包含五个主调度组()
        {
            Assert.That(LegacyScheduleGroupTypes.Length, Is.EqualTo(5));
            Assert.That(LegacyScheduleGroupTypes.Distinct().Count(), Is.EqualTo(5));

            foreach (var groupType in LegacyScheduleGroupTypes)
                Assert.That(groupType.IsSubclassOf(typeof(ComponentSystemGroup)), Is.True);
        }

        /// <summary>
        /// 验证旧 Ability slot 直接保存 raw Entity，而不是强类型稳定句柄。
        /// </summary>
        [Test]
        public void AbilitySlotBuffer_仍直接保存RawEntity()
        {
            var abilityEntityField = typeof(AbilitySlotBuffer)
                .GetField(nameof(AbilitySlotBuffer.AbilityEntity));

            Assert.That(abilityEntityField, Is.Not.Null);
            Assert.That(abilityEntityField.FieldType, Is.EqualTo(typeof(Entity)));
        }
    }

    /// <summary>
    /// 冻结 generated catalog 中 AutoChess 9203 与 9207 的旧生成结果。
    /// </summary>
    [TestFixture]
    public class GeneratedCatalogLegacyCharacterizationTests
    {
        private const int PoisonGameplayEffectCode = 9203;
        private const int PoisonPeriodGameplayEffectCode = 9204;
        private const int ExecuteMarkerGameplayEffectCode = 9207;

        /// <summary>
        /// 验证 9203 保留 Duration8、Period2、child9204 与 StackType9203 的 legacy 异常。
        /// </summary>
        [Test]
        public void GeneratedCatalog_9203保留Legacy异常形状()
        {
            var catalog = CreateGeneratedCatalog();
            try
            {
                var poison = GetGameplayEffect(catalog, PoisonGameplayEffectCode);
                Assert.That(poison.DurationFrames, Is.EqualTo(8));
                Assert.That(poison.PeriodFrames, Is.EqualTo(2));
                Assert.That(poison.PeriodGameplayEffectCode, Is.EqualTo(PoisonPeriodGameplayEffectCode));
                Assert.That(poison.StackType, Is.EqualTo(PoisonGameplayEffectCode));
            }
            finally
            {
                catalog.Dispose();
            }
        }

        /// <summary>
        /// 验证 9207 仍以 Duration=-1 充当旧无限时长 marker。
        /// </summary>
        [Test]
        public void GeneratedCatalog_9207保留无限时长Marker()
        {
            var catalog = CreateGeneratedCatalog();
            try
            {
                var marker = GetGameplayEffect(catalog, ExecuteMarkerGameplayEffectCode);
                Assert.That(marker.DurationFrames, Is.EqualTo(-1));
            }
            finally
            {
                catalog.Dispose();
            }
        }

        /// <summary>
        /// 使用 generated populate 入口构造当前不可变 catalog，避免测试复制生成数据。
        /// </summary>
        private static BlobAssetReference<GASDefinitionCatalogBlob> CreateGeneratedCatalog()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GASDefinitionCatalogBlob>();
                GASGeneratedDefinitionCatalogData.Populate(ref builder, ref root);
                return builder.CreateBlobAssetReference<GASDefinitionCatalogBlob>(Allocator.Persistent);
            }
            finally
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// 按编号读取指定 GameplayEffect，并让编号缺失直接表现为测试失败。
        /// </summary>
        private static GASCatalogGameplayEffectDefinitionBlob GetGameplayEffect(
            BlobAssetReference<GASDefinitionCatalogBlob> catalog,
            int gameplayEffectCode)
        {
            ref var root = ref catalog.Value;
            var found = GASGeneratedDefinitionCatalogLookup.TryGetGameplayEffectIndex(
                ref root,
                gameplayEffectCode,
                out var index);
            Assert.That(found, Is.True, "Generated catalog 缺少 GE " + gameplayEffectCode);
            return GASGeneratedDefinitionCatalogLookup.GetGameplayEffect(ref root, index);
        }
    }

    /// <summary>
    /// 验证 Tier B 最低向量清单的完整编号、owner 与初始红灯状态。
    /// </summary>
    [TestFixture]
    public class TierBVectorManifestTests
    {
        private static readonly string[] ExpectedOwners =
        {
            "V3", "V3", "V3", "V3",
            "V3 + V4", "V3 + V4", "V3 + V4", "V3",
            "V4", "V4 + V5", "V4 + V5", "V1 + V3 + V6",
            "V3 + V6", "V3 + V6", "V2 + V3", "V5 + V6", "V7",
        };

        /// <summary>
        /// 验证清单编号唯一、连续且完整覆盖 1 到 17。
        /// </summary>
        [Test]
        public void TierB清单_编号唯一且覆盖一到十七()
        {
            var entries = TierBVectorManifest.Entries;
            var numbers = entries.Select(entry => entry.Number).ToArray();

            Assert.That(entries.Count, Is.EqualTo(17));
            Assert.That(numbers.Distinct().Count(), Is.EqualTo(17));
            Assert.That(numbers, Is.EqualTo(Enumerable.Range(1, 17).ToArray()));
        }

        /// <summary>
        /// 验证每个最低向量均映射到任务树冻结的实施 owner。
        /// </summary>
        [Test]
        public void TierB清单_Owner映射符合冻结任务树()
        {
            var entries = TierBVectorManifest.Entries;
            Assert.That(entries.Count, Is.EqualTo(ExpectedOwners.Length));

            for (var index = 0; index < entries.Count; index++)
                Assert.That(entries[index].Owner, Is.EqualTo(ExpectedOwners[index]));
        }

        /// <summary>
        /// 验证 V0 只登记目标向量，尚未实现的 Tier B 语义不得误报为 green。
        /// </summary>
        [Test]
        public void TierB清单_V0阶段全部保持Red()
        {
            Assert.That(
                TierBVectorManifest.Entries.All(entry => entry.Status == TierBVectorStatus.Red),
                Is.True);
        }
    }
}
