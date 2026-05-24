using Unity.Entities;

namespace GAS.Runtime
{
    public static class AbilitySystemEntityFactory
    {
        private const int AttributeCapacity = 32;
        private const int ActiveModifierCapacity = 32;
        private const int GrantedAbilityCapacity = 16;
        private const int TagSourceCapacity = 32;
        private const int GameplayEffectCapacity = 32;
        private const int PresentationEventCapacity = 512;

        public static Entity Create(EntityManager entityManager)
        {
            var asc = entityManager.CreateEntity();
            entityManager.SetName(asc, "ASC");
            AddCoreComponents(entityManager, asc);
            return asc;
        }

        public static Entity Create(EntityCommandBuffer commandBuffer)
        {
            var asc = commandBuffer.CreateEntity();
            commandBuffer.SetName(asc, "ASC");
            AddCoreComponents(commandBuffer, asc);
            return asc;
        }

        private static void AddCoreComponents(EntityManager entityManager, Entity asc)
        {
            entityManager.AddComponent<CTagMask>(asc);
            entityManager.AddComponent<CFixedTagMask>(asc);
            entityManager.AddBuffer<BAttribute>(asc).EnsureCapacity(AttributeCapacity);
            entityManager.AddBuffer<BActiveModifier>(asc).EnsureCapacity(ActiveModifierCapacity);
            entityManager.AddBuffer<BGrantedAbility>(asc).EnsureCapacity(GrantedAbilityCapacity);
            entityManager.AddBuffer<BFixedTagSource>(asc).EnsureCapacity(TagSourceCapacity);
            entityManager.AddBuffer<BTempTagSource>(asc).EnsureCapacity(TagSourceCapacity);
            entityManager.AddBuffer<BGameplayEffect>(asc).EnsureCapacity(GameplayEffectCapacity);
            entityManager.AddBuffer<BPresentationEvent>(asc).EnsureCapacity(PresentationEventCapacity);
        }

        private static void AddCoreComponents(EntityCommandBuffer commandBuffer, Entity asc)
        {
            commandBuffer.AddComponent<CTagMask>(asc);
            commandBuffer.AddComponent<CFixedTagMask>(asc);
            commandBuffer.AddBuffer<BAttribute>(asc).EnsureCapacity(AttributeCapacity);
            commandBuffer.AddBuffer<BActiveModifier>(asc).EnsureCapacity(ActiveModifierCapacity);
            commandBuffer.AddBuffer<BGrantedAbility>(asc).EnsureCapacity(GrantedAbilityCapacity);
            commandBuffer.AddBuffer<BFixedTagSource>(asc).EnsureCapacity(TagSourceCapacity);
            commandBuffer.AddBuffer<BTempTagSource>(asc).EnsureCapacity(TagSourceCapacity);
            commandBuffer.AddBuffer<BGameplayEffect>(asc).EnsureCapacity(GameplayEffectCapacity);
            commandBuffer.AddBuffer<BPresentationEvent>(asc).EnsureCapacity(PresentationEventCapacity);
        }
    }
}
