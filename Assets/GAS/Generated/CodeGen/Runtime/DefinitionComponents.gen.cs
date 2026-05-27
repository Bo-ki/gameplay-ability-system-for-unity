///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using Unity.Entities;

namespace GAS.Runtime.Generated
{
    public struct GASGeneratedDefinitionBlobComponent<T> : IComponentData where T : unmanaged
    {
        public BlobAssetReference<T> Value;
    }

    public struct GASDefinitionCodeComponent : IComponentData
    {
        public int Value;
    }

    public static class GASGeneratedDefinitionBakePlan
    {
        public const int DefinitionCount = 10;
        public const int AbilityDefinitionBlobKind = 0;
        public const int AbilityDefinitionBlobDefinitionKind = 1;
        public const int AttributeDefinitionBlobKind = 1;
        public const int AttributeDefinitionBlobDefinitionKind = 4;
        public const int AttributeSetDefinitionBlobKind = 2;
        public const int AttributeSetDefinitionBlobDefinitionKind = 3;
        public const int GameplayCueDefinitionBlobKind = 3;
        public const int GameplayCueDefinitionBlobDefinitionKind = 6;
        public const int GameplayEffectDefinitionBlobKind = 4;
        public const int GameplayEffectDefinitionBlobDefinitionKind = 2;
        public const int GameplayTagDefinitionBlobKind = 5;
        public const int GameplayTagDefinitionBlobDefinitionKind = 5;
        public const int ScenarioSpawnDefinitionBlobKind = 6;
        public const int ScenarioSpawnDefinitionBlobDefinitionKind = 0;
        public const int SummonDefinitionBlobKind = 7;
        public const int SummonDefinitionBlobDefinitionKind = 0;
        public const int TimelineDefinitionBlobKind = 8;
        public const int TimelineDefinitionBlobDefinitionKind = 7;
        public const int UnitDefinitionBlobKind = 9;
        public const int UnitDefinitionBlobDefinitionKind = 0;
    }
}
