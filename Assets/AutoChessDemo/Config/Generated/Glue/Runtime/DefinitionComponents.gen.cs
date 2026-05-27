///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using Unity.Entities;

namespace GAS.Runtime.Generated
{
    public struct GeneratedDefinitionBlobComponent<T> : IComponentData where T : unmanaged
    {
        public BlobAssetReference<T> Value;
    }

    public struct DefinitionCodeComponent : IComponentData
    {
        public int Value;
    }

    public static class GasGeneratedDefinitionBakePlan
    {
        public const int DefinitionCount = 10;
        public const int BlobAbilityDefinitionKind = 0;
        public const int BlobAbilityDefinitionDefinitionKind = 1;
        public const int BlobAttributeDefinitionKind = 1;
        public const int BlobAttributeDefinitionDefinitionKind = 4;
        public const int BlobAttributeSetDefinitionKind = 2;
        public const int BlobAttributeSetDefinitionDefinitionKind = 3;
        public const int BlobGameplayCueDefinitionKind = 3;
        public const int BlobGameplayCueDefinitionDefinitionKind = 6;
        public const int BlobGameplayEffectDefinitionKind = 4;
        public const int BlobGameplayEffectDefinitionDefinitionKind = 2;
        public const int BlobGameplayTagDefinitionKind = 5;
        public const int BlobGameplayTagDefinitionDefinitionKind = 5;
        public const int BlobScenarioSpawnDefinitionKind = 6;
        public const int BlobScenarioSpawnDefinitionDefinitionKind = 0;
        public const int BlobSummonDefinitionKind = 7;
        public const int BlobSummonDefinitionDefinitionKind = 0;
        public const int BlobTimelineDefinitionKind = 8;
        public const int BlobTimelineDefinitionDefinitionKind = 7;
        public const int BlobUnitDefinitionKind = 9;
        public const int BlobUnitDefinitionDefinitionKind = 0;
    }
}
