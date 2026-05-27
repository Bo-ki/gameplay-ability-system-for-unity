///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using Unity.Entities;

namespace GAS.Runtime.Generated
{
    public static class GasQueryLayouts
    {

        public static readonly EntityQueryDesc BlobAbilityDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GeneratedDefinitionBlobComponent<BlobAbilityDefinition>>(),
                ComponentType.ReadOnly<DefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc BlobAttributeDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GeneratedDefinitionBlobComponent<BlobAttributeDefinition>>(),
                ComponentType.ReadOnly<DefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc BlobAttributeSetDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GeneratedDefinitionBlobComponent<BlobAttributeSetDefinition>>(),
                ComponentType.ReadOnly<DefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc BlobGameplayCueDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GeneratedDefinitionBlobComponent<BlobGameplayCueDefinition>>(),
                ComponentType.ReadOnly<DefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc BlobGameplayEffectDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GeneratedDefinitionBlobComponent<BlobGameplayEffectDefinition>>(),
                ComponentType.ReadOnly<DefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc BlobGameplayTagDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GeneratedDefinitionBlobComponent<BlobGameplayTagDefinition>>(),
                ComponentType.ReadOnly<DefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc BlobScenarioSpawnDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GeneratedDefinitionBlobComponent<BlobScenarioSpawnDefinition>>(),
                ComponentType.ReadOnly<DefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc BlobSummonDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GeneratedDefinitionBlobComponent<BlobSummonDefinition>>(),
                ComponentType.ReadOnly<DefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc BlobTimelineDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GeneratedDefinitionBlobComponent<BlobTimelineDefinition>>(),
                ComponentType.ReadOnly<DefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc BlobUnitDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GeneratedDefinitionBlobComponent<BlobUnitDefinition>>(),
                ComponentType.ReadOnly<DefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static EntityQuery GetQuery(ref SystemState state, EntityQueryDesc desc)
        {
            return state.GetEntityQuery(desc);
        }
    }
}
