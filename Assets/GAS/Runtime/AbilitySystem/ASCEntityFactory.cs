using Unity.Entities;

namespace GAS.Runtime
{
    public static class ASCEntityFactory
    {
        private const int AttributeCapacity = 32;
        private const int PendingAttributeModifierCapacity = 8;
        private const int ActiveModifierCapacity = 32;
        private const int AscCommandCapacity = 8;
        private const int AbilityCommandCapacity = 8;
        private const int AscDestroyCommandCapacity = 1;
        private const int GameplayEffectRemoveCommandCapacity = 4;
        private const int GrantedAbilityCapacity = 16;
        private const int TagSourceCapacity = 32;
        private const int GameplayEffectCapacity = 32;
        private const int ActiveEffectSlotInitialCapacity = ActiveEffectStore.InlineSlotCapacity;
        private const int ActiveEffectSetByCallerInitialCapacity = ActiveEffectStore.InlineSetByCallerCapacity;
        private const int ActiveEffectCleanupRecordInitialCapacity = ActiveEffectStore.InlineCleanupRecordCapacity;
        private const int ActiveEffectMutationInitialCapacity = 4;
        private const int PresentationEventCapacity = 16;

        public static Entity Create(EntityManager entityManager)
        {
            var asc = entityManager.CreateEntity(GASRuntimeEntityArchetypes.ASC(entityManager));
            entityManager.SetName(asc, "ASC");
            InitializeCoreComponents(entityManager, asc);
            return asc;
        }

        internal static Entity Create(EntityManager entityManager, EntityArchetype archetype)
        {
            if (!archetype.Valid)
                return Entity.Null;

            var asc = entityManager.CreateEntity(archetype);
            entityManager.SetName(asc, "ASC");
            InitializeCoreComponents(entityManager, asc);
            return asc;
        }

        public static Entity Create(EntityCommandBuffer commandBuffer, EntityManager entityManager)
        {
            var asc = commandBuffer.CreateEntity(GASRuntimeEntityArchetypes.ASC(entityManager));
            commandBuffer.SetName(asc, "ASC");
            InitializeCoreComponents(commandBuffer, asc);
            return asc;
        }

        private static void InitializeCoreComponents(EntityManager entityManager, Entity asc)
        {
            entityManager.SetComponentEnabled<ASCDestroyingComponent>(asc, false);
            entityManager.SetComponentEnabled<ASCCommandPendingComponent>(asc, false);
            entityManager.SetComponentEnabled<GERemoveCommandPendingComponent>(asc, false);
            entityManager.SetComponentEnabled<AttributeDirtyComponent>(asc, false);
            entityManager.SetComponentEnabled<AttributeChangeEventPendingComponent>(asc, false);
            entityManager.SetComponentEnabled<AttributeActiveModifierPresentComponent>(asc, false);
            entityManager.SetComponentEnabled<PendingAttributeModifierComponent>(asc, false);
            entityManager.SetComponentData(asc, ActiveEffectStore.CreateDefault());
            entityManager.GetBuffer<AttributeValueBuffer>(asc).EnsureCapacity(AttributeCapacity);
            entityManager.GetBuffer<AttributeModifierBuffer>(asc).EnsureCapacity(PendingAttributeModifierCapacity);
            entityManager.GetBuffer<AttributeActiveModifierBuffer>(asc).EnsureCapacity(ActiveModifierCapacity);
            entityManager.GetBuffer<ASCCommandBuffer>(asc).EnsureCapacity(AscCommandCapacity);
            entityManager.GetBuffer<AbilityCommandBuffer>(asc).EnsureCapacity(AbilityCommandCapacity);
            entityManager.GetBuffer<ASCDestroyCommandBuffer>(asc).EnsureCapacity(AscDestroyCommandCapacity);
            entityManager.GetBuffer<GERemoveCommandBuffer>(asc).EnsureCapacity(GameplayEffectRemoveCommandCapacity);
            entityManager.GetBuffer<AbilitySlotBuffer>(asc).EnsureCapacity(GrantedAbilityCapacity);
            entityManager.GetBuffer<TagFixedSourceBuffer>(asc).EnsureCapacity(TagSourceCapacity);
            entityManager.GetBuffer<TagTemporarySourceBuffer>(asc).EnsureCapacity(TagSourceCapacity);
            entityManager.GetBuffer<LegacyGameplayEffectEntityBuffer>(asc).EnsureCapacity(GameplayEffectCapacity);
            entityManager.GetBuffer<ActiveGameplayEffectBuffer>(asc).EnsureCapacity(ActiveEffectSlotInitialCapacity);
            entityManager.GetBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(asc).EnsureCapacity(ActiveEffectSetByCallerInitialCapacity);
            entityManager.GetBuffer<ActiveGameplayEffectCleanupRecordBuffer>(asc).EnsureCapacity(ActiveEffectCleanupRecordInitialCapacity);
            entityManager.GetBuffer<ActiveEffectMutationBuffer>(asc).EnsureCapacity(ActiveEffectMutationInitialCapacity);
            entityManager.GetBuffer<PresentationEventBuffer>(asc).EnsureCapacity(PresentationEventCapacity);
        }

        private static void InitializeCoreComponents(EntityCommandBuffer commandBuffer, Entity asc)
        {
            commandBuffer.SetComponentEnabled<ASCDestroyingComponent>(asc, false);
            commandBuffer.SetComponentEnabled<ASCCommandPendingComponent>(asc, false);
            commandBuffer.SetComponentEnabled<GERemoveCommandPendingComponent>(asc, false);
            commandBuffer.SetComponentEnabled<AttributeDirtyComponent>(asc, false);
            commandBuffer.SetComponentEnabled<AttributeChangeEventPendingComponent>(asc, false);
            commandBuffer.SetComponentEnabled<AttributeActiveModifierPresentComponent>(asc, false);
            commandBuffer.SetComponentEnabled<PendingAttributeModifierComponent>(asc, false);
            commandBuffer.SetComponent(asc, ActiveEffectStore.CreateDefault());
            commandBuffer.SetBuffer<AttributeValueBuffer>(asc).EnsureCapacity(AttributeCapacity);
            commandBuffer.SetBuffer<AttributeModifierBuffer>(asc).EnsureCapacity(PendingAttributeModifierCapacity);
            commandBuffer.SetBuffer<AttributeActiveModifierBuffer>(asc).EnsureCapacity(ActiveModifierCapacity);
            commandBuffer.SetBuffer<ASCCommandBuffer>(asc).EnsureCapacity(AscCommandCapacity);
            commandBuffer.SetBuffer<AbilityCommandBuffer>(asc).EnsureCapacity(AbilityCommandCapacity);
            commandBuffer.SetBuffer<ASCDestroyCommandBuffer>(asc).EnsureCapacity(AscDestroyCommandCapacity);
            commandBuffer.SetBuffer<GERemoveCommandBuffer>(asc).EnsureCapacity(GameplayEffectRemoveCommandCapacity);
            commandBuffer.SetBuffer<AbilitySlotBuffer>(asc).EnsureCapacity(GrantedAbilityCapacity);
            commandBuffer.SetBuffer<TagFixedSourceBuffer>(asc).EnsureCapacity(TagSourceCapacity);
            commandBuffer.SetBuffer<TagTemporarySourceBuffer>(asc).EnsureCapacity(TagSourceCapacity);
            commandBuffer.SetBuffer<LegacyGameplayEffectEntityBuffer>(asc).EnsureCapacity(GameplayEffectCapacity);
            commandBuffer.SetBuffer<ActiveGameplayEffectBuffer>(asc).EnsureCapacity(ActiveEffectSlotInitialCapacity);
            commandBuffer.SetBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(asc).EnsureCapacity(ActiveEffectSetByCallerInitialCapacity);
            commandBuffer.SetBuffer<ActiveGameplayEffectCleanupRecordBuffer>(asc).EnsureCapacity(ActiveEffectCleanupRecordInitialCapacity);
            commandBuffer.SetBuffer<ActiveEffectMutationBuffer>(asc).EnsureCapacity(ActiveEffectMutationInitialCapacity);
            commandBuffer.SetBuffer<PresentationEventBuffer>(asc).EnsureCapacity(PresentationEventCapacity);
        }

        public static bool HasASCRuntimeCoreComponents(EntityManager entityManager, Entity asc)
        {
            return asc != Entity.Null
                && entityManager.Exists(asc)
                && entityManager.HasComponent<ASCIdentityComponent>(asc)
                && entityManager.HasComponent<ASCDestroyingComponent>(asc)
                && entityManager.HasComponent<ASCCommandPendingComponent>(asc)
                && entityManager.HasComponent<GERemoveCommandPendingComponent>(asc)
                && entityManager.HasComponent<AttributeDirtyComponent>(asc)
                && entityManager.HasComponent<AttributeChangeEventPendingComponent>(asc)
                && entityManager.HasComponent<AttributeActiveModifierPresentComponent>(asc)
                && entityManager.HasComponent<PendingAttributeModifierComponent>(asc)
                && entityManager.HasComponent<TagMaskComponent>(asc)
                && entityManager.HasComponent<TagFixedMaskComponent>(asc)
                && entityManager.HasBuffer<AttributeValueBuffer>(asc)
                && entityManager.HasBuffer<AttributeModifierBuffer>(asc)
                && entityManager.HasBuffer<AttributeActiveModifierBuffer>(asc)
                && entityManager.HasBuffer<ASCCommandBuffer>(asc)
                && entityManager.HasBuffer<AbilityCommandBuffer>(asc)
                && entityManager.HasBuffer<ASCDestroyCommandBuffer>(asc)
                && entityManager.HasBuffer<GERemoveCommandBuffer>(asc)
                && entityManager.HasBuffer<AbilitySlotBuffer>(asc)
                && entityManager.HasBuffer<TagFixedSourceBuffer>(asc)
                && entityManager.HasBuffer<TagTemporarySourceBuffer>(asc)
                && entityManager.HasBuffer<LegacyGameplayEffectEntityBuffer>(asc)
                && entityManager.HasComponent<ASCActiveEffectsComponent>(asc)
                && entityManager.HasBuffer<ActiveGameplayEffectBuffer>(asc)
                && entityManager.HasBuffer<ActiveGameplayEffectSetByCallerValueBuffer>(asc)
                && entityManager.HasBuffer<ActiveGameplayEffectCleanupRecordBuffer>(asc)
                && entityManager.HasBuffer<ActiveEffectMutationBuffer>(asc)
                && entityManager.HasBuffer<PresentationEventBuffer>(asc);
        }

        public static bool IsDestroying(EntityManager entityManager, Entity asc)
        {
            return asc != Entity.Null
                   && entityManager.Exists(asc)
                   && entityManager.HasComponent<ASCDestroyingComponent>(asc)
                   && entityManager.IsComponentEnabled<ASCDestroyingComponent>(asc);
        }
    }
}
