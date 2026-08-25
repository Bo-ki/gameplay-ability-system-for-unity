using GAS.Runtime;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.PlayMode
{
    /// <summary>
    /// 在真实 World 中冻结迁移前 ASC archetype 同时持有两套 legacy Entity buffer 的事实。
    /// </summary>
    [TestFixture]
    public class LegacyAscArchetypePlayModeTests
    {
        /// <summary>
        /// 创建 ASC archetype 实体并验证 Ability 与 GameplayEffect 的 legacy buffer 同时存在。
        /// </summary>
        [Test]
        public void 旧ASCArchetype_同时包含AbilitySlot与LegacyGameplayEffectBuffer()
        {
            var world = new World("RuntimeV1 baseline PlayMode test");
            try
            {
                var entityManager = world.EntityManager;
                var ascArchetype = GASRuntimeEntityArchetypes.ASC(entityManager);
                var asc = entityManager.CreateEntity(ascArchetype);

                Assert.That(entityManager.HasBuffer<AbilitySlotBuffer>(asc), Is.True);
                Assert.That(entityManager.HasBuffer<LegacyGameplayEffectEntityBuffer>(asc), Is.True);
            }
            finally
            {
                GASRuntimeEntityArchetypes.ResetCache();
                world.Dispose();
            }
        }
    }
}
