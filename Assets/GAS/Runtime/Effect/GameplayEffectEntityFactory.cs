using Unity.Entities;

namespace GAS.Runtime
{
    internal static class GameplayEffectEntityFactory
    {
        public static Entity CreateFromConfig(
            EntityManager entityManager,
            GameplayEffectComponentConfig[] componentConfigs)
        {
            var entity = entityManager.CreateEntity();

            LoadConfigComponents(entityManager, entity, componentConfigs);
            AddRuntimeComponents(entityManager, entity);
            return entity;
        }

        public static Entity CreatePrototypeFromConfig(
            EntityManager entityManager,
            int gameplayEffectCode,
            GameplayEffectComponentConfig[] componentConfigs)
        {
            var entity = entityManager.CreateEntity();
            entityManager.SetName(entity, $"GE_Prototype_{gameplayEffectCode}_{entity.Index}");
            entityManager.AddComponentData(entity, new CGameplayEffectPrototype
            {
                GameplayEffectCode = gameplayEffectCode,
            });

            LoadConfigComponents(entityManager, entity, componentConfigs);
            return entity;
        }

        public static Entity InstantiateFromPrototype(EntityManager entityManager, Entity prototype)
        {
            if (prototype == Entity.Null || !entityManager.Exists(prototype))
                return Entity.Null;

            var entity = entityManager.Instantiate(prototype);
            if (entityManager.HasComponent<CGameplayEffectPrototype>(entity))
                entityManager.RemoveComponent<CGameplayEffectPrototype>(entity);

            AddRuntimeComponents(entityManager, entity);
            return entity;
        }

        public static bool CanCreatePrototypeFromConfig(GameplayEffectComponentConfig[] componentConfigs)
        {
            if (componentConfigs != null)
            {
                foreach (var config in componentConfigs)
                {
                    if (config == null)
                        continue;

                    if (!config.SupportsPrototypeCache)
                        return false;
                }
            }

            return true;
        }

        private static void LoadConfigComponents(
            EntityManager entityManager,
            Entity entity,
            GameplayEffectComponentConfig[] componentConfigs)
        {
            if (componentConfigs != null)
            {
                foreach (var config in componentConfigs)
                    config?.LoadToGameplayEffectEntity(entity);
            }

            ConvertToRuntimeBuffers(entityManager, entity);
        }

        private static void AddRuntimeComponents(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<CDurationDefinition>(entity)
                && !entityManager.HasComponent<CDurationRuntime>(entity))
            {
                var definition = entityManager.GetComponentData<CDurationDefinition>(entity);
                entityManager.AddComponentData(entity, new CDurationRuntime
                {
                    ResolvedDuration = definition.Duration,
                    ResolvedTimeUnit = definition.TimeUnit,
                });
            }

            if (entityManager.HasComponent<CPeriodDefinition>(entity)
                && !entityManager.HasComponent<CPeriodRuntime>(entity))
            {
                entityManager.AddComponentData(entity, new CPeriodRuntime());
            }

            if (entityManager.HasComponent<CStackingDefinition>(entity)
                && !entityManager.HasComponent<CStackingRuntime>(entity))
            {
                entityManager.AddComponentData(entity, new CStackingRuntime
                {
                    StackCount = 1,
                });
            }

            if (!entityManager.HasComponent<CActiveEffectGlobalIndexStableRow>(entity))
                entityManager.AddComponentData(entity, ActiveEffectStore.CreateGlobalIndexStableRowDefault(entity));

            EnsureRuntimeGrantedAbilityBuffer(entityManager, entity);
        }

        private static void EnsureRuntimeGrantedAbilityBuffer(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasBuffer<BGrantedAbilityRuntime>(entity)
                || !entityManager.HasBuffer<BGrantedAbilityConfig>(entity))
            {
                return;
            }

            var definitions = entityManager.GetBuffer<BGrantedAbilityConfig>(entity);
            if (definitions.Length == 0)
                return;

            entityManager.AddBuffer<BGrantedAbilityRuntime>(entity).EnsureCapacity(definitions.Length);
        }

        private static void ConvertToRuntimeBuffers(EntityManager entityManager, Entity ge)
        {
            if (!entityManager.HasComponent<CEffectGrantedTags>(ge))
                return;

            var grantedTags = entityManager.GetComponentData<CEffectGrantedTags>(ge);
            if (grantedTags.Tags.IsEmpty)
                return;

            var tagBuffer = entityManager.HasBuffer<BGrantedTagConfig>(ge)
                ? entityManager.GetBuffer<BGrantedTagConfig>(ge)
                : entityManager.AddBuffer<BGrantedTagConfig>(ge);

            for (var tagIndex = 0; tagIndex < CTagMask.Capacity; tagIndex++)
            {
                if (grantedTags.Tags.HasTag(tagIndex))
                    tagBuffer.Add(new BGrantedTagConfig { TagIndex = tagIndex });
            }
        }
    }
}
