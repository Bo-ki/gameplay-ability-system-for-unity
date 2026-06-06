///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using Unity.Entities;

namespace GAS.Runtime.Generated
{
    public static class GASGeneratedDefinitionQueryLayouts
    {

        public static readonly EntityQueryDesc AbilityDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GASGeneratedDefinitionBlobComponent<AbilityDefinitionBlob>>(),
                ComponentType.ReadOnly<GASDefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc AttributeDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GASGeneratedDefinitionBlobComponent<AttributeDefinitionBlob>>(),
                ComponentType.ReadOnly<GASDefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc AttributeSetDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GASGeneratedDefinitionBlobComponent<AttributeSetDefinitionBlob>>(),
                ComponentType.ReadOnly<GASDefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc GameplayCueDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GASGeneratedDefinitionBlobComponent<GameplayCueDefinitionBlob>>(),
                ComponentType.ReadOnly<GASDefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc GameplayEffectDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GASGeneratedDefinitionBlobComponent<GameplayEffectDefinitionBlob>>(),
                ComponentType.ReadOnly<GASDefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static readonly EntityQueryDesc GameplayTagDefinitionQuery = new EntityQueryDesc
        {
            All = new ComponentType[]
            {
                ComponentType.ReadOnly<GASGeneratedDefinitionBlobComponent<GameplayTagDefinitionBlob>>(),
                ComponentType.ReadOnly<GASDefinitionCodeComponent>(),
            },
            Options = EntityQueryOptions.IncludeDisabledEntities,
        };

        public static EntityQuery GetQuery(ref SystemState state, EntityQueryDesc desc)
        {
            return state.GetEntityQuery(desc);
        }
    }
}
