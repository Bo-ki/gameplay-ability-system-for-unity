using Unity.Entities;

namespace GAS.Runtime
{
    internal static class GameplayEffectEntityFactory
    {
        public static Entity CreateFromConfig(
            EntityManager entityManager,
            GameplayEffectComponentConfig[] componentConfigs)
        {
            var entity = entityManager.CreateEntity(GASRuntimeEntityArchetypes.GameplayEffectRuntime(entityManager));
            GASRuntimeEntityArchetypes.InitializeGameplayEffectEntity(entityManager, entity, prototype: false);

            LoadConfigComponents(entityManager, entity, componentConfigs);
            AddRuntimeComponents(entityManager, entity);
            return entity;
        }

        public static Entity CreatePrototypeFromConfig(
            EntityManager entityManager,
            int gameplayEffectCode,
            GameplayEffectComponentConfig[] componentConfigs)
        {
            var entity = entityManager.CreateEntity(GASRuntimeEntityArchetypes.GameplayEffectPrototype(entityManager));
            GASRuntimeEntityArchetypes.InitializeGameplayEffectEntity(entityManager, entity, prototype: true);
            entityManager.SetName(entity, $"GE_Prototype_{gameplayEffectCode}_{entity.Index}");
            entityManager.SetComponentData(entity, new GEPrototypeComponent
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
            GASRuntimeEntityArchetypes.InitializeGameplayEffectRuntimeInstance(entityManager, entity);

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
                    config?.LoadToGameplayEffectEntity(entityManager, entity);
            }

            ConvertToRuntimeBuffers(entityManager, entity);
        }

        private static void AddRuntimeComponents(EntityManager entityManager, Entity entity)
        {
            if (entityManager.IsComponentEnabled<GEDurationDefinitionComponent>(entity))
            {
                var definition = entityManager.GetComponentData<GEDurationDefinitionComponent>(entity);
                entityManager.SetComponentData(entity, new GEDurationRuntimeComponent
                {
                    ResolvedDuration = definition.Duration,
                    ResolvedTimeUnit = definition.TimeUnit,
                });
                entityManager.SetComponentEnabled<GEDurationRuntimeComponent>(entity, true);
            }

            if (entityManager.IsComponentEnabled<GEPeriodDefinitionComponent>(entity))
            {
                entityManager.SetComponentData(entity, new GEPeriodRuntimeComponent());
                entityManager.SetComponentEnabled<GEPeriodRuntimeComponent>(entity, true);
            }

            if (entityManager.IsComponentEnabled<GEStackingDefinitionComponent>(entity))
            {
                entityManager.SetComponentData(entity, new GEStackingRuntimeComponent
                {
                    StackCount = 1,
                });
                entityManager.SetComponentEnabled<GEStackingRuntimeComponent>(entity, true);
            }

            entityManager.SetComponentData(entity, ActiveEffectStore.CreateGlobalIndexStableRowDefault(entity));

            EnsureLegacyLifecycleComponents(entityManager, entity);
            EnsureMagnitudeResolverBuffers(entityManager, entity);
            EnsureExecutionCalculationBuffers(entityManager, entity);
            EnsureRuntimeGrantedAbilityBuffer(entityManager, entity);
        }

        private static void EnsureLegacyLifecycleComponents(EntityManager entityManager, Entity entity)
        {
            entityManager.SetComponentData(entity, new GEEffectLifecycleComponent());

            entityManager.SetComponentEnabled<GEEffectDestroyComponent>(entity, false);

            entityManager.SetComponentEnabled<GEEffectFinalDestroyComponent>(entity, false);
        }

        private static void EnsureMagnitudeResolverBuffers(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasBuffer<GEModifierConfigBuffer>(entity)
                && !entityManager.HasBuffer<GEResolvedModifierBuffer>(entity))
                return;

            if (RequiresAttributeCaptureBuffer(entityManager, entity)
                && !entityManager.HasBuffer<GEAttributeCaptureValueBuffer>(entity))
                return;
        }

        private static bool RequiresAttributeCaptureBuffer(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.IsComponentEnabled<GEMagnitudeDefinitionBuffer>(entity))
                return false;

            var definitions = entityManager.GetBuffer<GEMagnitudeDefinitionBuffer>(entity);
            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition.CaptureTiming == EAttributeCaptureTiming.CurrentValue)
                    continue;

                if (definition.Source == EMagnitudeSource.SourceAttribute
                    || definition.Source == EMagnitudeSource.TargetAttribute)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureExecutionCalculationBuffers(EntityManager entityManager, Entity entity)
        {
            var hasExecutionDefinition =
                entityManager.IsComponentEnabled<GEExecutionCalculationDefinitionBuffer>(entity)
                || entityManager.IsComponentEnabled<GEExecutionCalculationInputDefinitionBuffer>(entity)
                || entityManager.IsComponentEnabled<GEExecutionCalculationOutputModifierDefinitionBuffer>(entity);

            if (!hasExecutionDefinition)
                return;

            if (!entityManager.HasBuffer<GEExecutionCalculationValueBuffer>(entity)
                || !entityManager.HasBuffer<GESetByCallerRequestValueBuffer>(entity)
                || !entityManager.HasBuffer<GEResolvedModifierBuffer>(entity))
                return;
        }

        private static void EnsureRuntimeGrantedAbilityBuffer(EntityManager entityManager, Entity entity)
        {
            if (!entityManager.IsComponentEnabled<GEGrantedAbilityConfigBuffer>(entity))
            {
                return;
            }

            var definitions = entityManager.GetBuffer<GEGrantedAbilityConfigBuffer>(entity);
            if (definitions.Length == 0)
                return;

            entityManager.GetBuffer<GEGrantedAbilityRuntimeBuffer>(entity).EnsureCapacity(definitions.Length);
        }

        private static void ConvertToRuntimeBuffers(EntityManager entityManager, Entity ge)
        {
            if (!entityManager.IsComponentEnabled<GEGrantedTagsComponent>(ge))
                return;

            var grantedTags = entityManager.GetComponentData<GEGrantedTagsComponent>(ge);
            if (grantedTags.Tags.IsEmpty)
                return;

            var tagBuffer = entityManager.GetBuffer<GEGrantedTagConfigBuffer>(ge);
            tagBuffer.Clear();

            for (var tagIndex = 0; tagIndex < TagMaskComponent.Capacity; tagIndex++)
            {
                if (grantedTags.Tags.HasTag(tagIndex))
                    tagBuffer.Add(new GEGrantedTagConfigBuffer { TagIndex = tagIndex });
            }
            entityManager.SetComponentEnabled<GEGrantedTagConfigBuffer>(ge, tagBuffer.Length > 0);
        }
    }
}
