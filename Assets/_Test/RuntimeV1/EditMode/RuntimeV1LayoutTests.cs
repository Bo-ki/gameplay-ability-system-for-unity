using GAS.Runtime;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Runtime v1 的 Session/ASC 固定布局、Catalog 长度与 ScaleProfile 容量契约。
    /// </summary>
    [TestFixture]
    public class RuntimeV1LayoutTests
    {
        /// <summary>
        /// 验证 Session 与 ASC 出生时即预挂 registry、slab 和 scoped cleanup outbox。
        /// </summary>
        [Test]
        public void Session与AscArchetype_出生时拥有完整固定布局()
        {
            using var world = new World("Runtime v1 layout test");
            var entityManager = world.EntityManager;
            var session = entityManager.CreateEntity(GasRuntimeV1Archetypes.CreateSession(entityManager));
            var asc = entityManager.CreateEntity(GasRuntimeV1Archetypes.CreateAsc(entityManager));

            Assert.That(GasRuntimeV1Archetypes.HasSessionLayout(entityManager, session), Is.True);
            Assert.That(GasRuntimeV1Archetypes.HasAscLayout(entityManager, asc), Is.True);
            Assert.That(entityManager.HasComponent<BoundaryDrainState>(session), Is.True);
            Assert.That(entityManager.HasBuffer<BoundaryFactBuffer>(session), Is.True);
            Assert.That(entityManager.HasComponent<BoundaryDrainState>(asc), Is.True);
            Assert.That(entityManager.HasBuffer<BoundaryFactBuffer>(asc), Is.True);
            Assert.That(entityManager.HasBuffer<ActiveEffectSlot>(asc), Is.True);
            Assert.That(entityManager.HasBuffer<GasPayloadRangeRecord>(asc), Is.True);
            Assert.That(entityManager.HasBuffer<GasPayloadValueSlot>(asc), Is.True);
            Assert.That(entityManager.HasBuffer<AbilityContinuationSlot>(asc), Is.True);
        }

        /// <summary>
        /// 验证 Attribute/Tag 只采用 Catalog 固定长度，所有可变 buffer 容量只取自 ScaleProfile。
        /// </summary>
        [Test]
        public void AscBuffers_固定长度与预留容量来自显式Registry和Profile()
        {
            using var world = new World("Runtime v1 capacity test");
            var entityManager = world.EntityManager;
            var asc = entityManager.CreateEntity(GasRuntimeV1Archetypes.CreateAsc(entityManager));
            var catalog = new GasCatalogRegistry
            {
                AttributeCount = 3,
                AttributeDirtyWordCount = 1,
                TagCount = 70,
                TagPresenceWordCount = 2,
            };
            var profile = CreateCapacityProfile();

            Assert.That(GasRuntimeV1Archetypes.TryInitializeFixedBuffers(entityManager, asc, in catalog), Is.True);
            Assert.That(GasRuntimeV1Archetypes.TryApplyAscCapacities(entityManager, asc, in profile), Is.True);
            Assert.That(GasRuntimeV1Archetypes.HasExpectedFixedBufferLengths(entityManager, asc, in catalog), Is.True);
            Assert.That(entityManager.GetBuffer<AttributeValueSlot>(asc).Length, Is.EqualTo(3));
            Assert.That(entityManager.GetBuffer<TagCountSlot>(asc).Length, Is.EqualTo(70));
            Assert.That(entityManager.GetBuffer<TagPresenceWord>(asc).Length, Is.EqualTo(2));
            Assert.That(entityManager.GetBuffer<GrantedAbilitySlot>(asc).Capacity,
                Is.GreaterThanOrEqualTo(profile.MaxGrantedAbilityCount));
            Assert.That(entityManager.GetBuffer<ActiveEffectSlot>(asc).Capacity,
                Is.GreaterThanOrEqualTo(profile.MaxActiveEffectCount));
            Assert.That(entityManager.GetBuffer<BoundaryFactBuffer>(asc).Capacity,
                Is.GreaterThanOrEqualTo(profile.MaxAscBoundaryFactCount));
        }

        /// <summary>
        /// 构造覆盖全部 ASC 可变 buffer 的非零版本化容量档位。
        /// </summary>
        private static GasScaleProfile CreateCapacityProfile()
        {
            return new GasScaleProfile
            {
                ProfileId = 7,
                ProfileVersion = 1,
                ProfileHash = 7001,
                MaxFixedTicksPerBatch = 1,
                MaximumDeltaTimeTicks = 1,
                MaxPendingAttributeInitializationCount = 3,
                MaxPendingTagInitializationCount = 4,
                MaxPendingGrantedAbilityInitializationCount = 2,
                MaxGrantedAbilityCount = 5,
                MaxAbilityActivationCount = 6,
                MaxAbilityContinuationCount = 7,
                MaxAbilitySubscriptionCount = 8,
                MaxCooldownGateCount = 9,
                MaxActivationOwnedContributionCount = 10,
                MaxEmittedApplicationRefCount = 11,
                MaxActiveEffectCount = 12,
                MaxPayloadRangeRecordCount = 13,
                MaxPayloadValueCount = 14,
                MaxAttributeAggregatorCount = 17,
                MaxLiveDependencyCount = 18,
                MaxLiveDependencyRouteCount = 19,
                MaxPendingCommandCount = 20,
                MaxAscBoundaryFactCount = 21,
            };
        }
    }
}
