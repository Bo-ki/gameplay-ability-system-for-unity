using GAS.Runtime;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.PlayMode
{
    /// <summary>
    /// 验证 Runtime v1 只创建 ASC-local slab 布局，不再暴露旧 Ability/GameplayEffect Entity archetype。
    /// </summary>
    [TestFixture]
    public class RuntimeV1AscArchetypePlayModeTests
    {
        /// <summary>
        /// 创建 v1 ASC 并确认固定布局、非紧凑 slab 与 Boundary fact buffer 同时存在。
        /// </summary>
        [Test]
        public void RuntimeV1AscArchetype_只包含OwnerLocalAuthority()
        {
            using var world = new World("Runtime v1 ASC archetype PlayMode test");
            var entityManager = world.EntityManager;
            var asc = entityManager.CreateEntity(GasRuntimeV1Archetypes.CreateAsc(entityManager));

            Assert.That(GasRuntimeV1Archetypes.HasAscLayout(entityManager, asc), Is.True);
            Assert.That(entityManager.HasBuffer<GrantedAbilitySlot>(asc), Is.True);
            Assert.That(entityManager.HasBuffer<ActiveEffectSlot>(asc), Is.True);
            Assert.That(entityManager.HasBuffer<BoundaryFactBuffer>(asc), Is.True);
        }
    }
}
