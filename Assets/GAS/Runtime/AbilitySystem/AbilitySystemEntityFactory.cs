using Unity.Entities;

namespace GAS.Runtime
{
    internal static class AbilitySystemEntityFactory
    {
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
            entityManager.AddBuffer<BAttribute>(asc);
            entityManager.AddBuffer<BActiveModifier>(asc);
            entityManager.AddBuffer<BGrantedAbility>(asc);
            entityManager.AddBuffer<BFixedTagSource>(asc);
            entityManager.AddBuffer<BTempTagSource>(asc);
            entityManager.AddBuffer<BGameplayEffect>(asc);
            entityManager.AddBuffer<BPresentationEvent>(asc);
        }

        private static void AddCoreComponents(EntityCommandBuffer commandBuffer, Entity asc)
        {
            commandBuffer.AddComponent<CTagMask>(asc);
            commandBuffer.AddComponent<CFixedTagMask>(asc);
            commandBuffer.AddBuffer<BAttribute>(asc);
            commandBuffer.AddBuffer<BActiveModifier>(asc);
            commandBuffer.AddBuffer<BGrantedAbility>(asc);
            commandBuffer.AddBuffer<BFixedTagSource>(asc);
            commandBuffer.AddBuffer<BTempTagSource>(asc);
            commandBuffer.AddBuffer<BGameplayEffect>(asc);
            commandBuffer.AddBuffer<BPresentationEvent>(asc);
        }
    }
}
