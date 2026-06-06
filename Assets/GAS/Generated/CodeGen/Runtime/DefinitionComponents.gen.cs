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
        public const int DefinitionCount = 6;
        public const int AbilityDefinitionBlobKind = 0;
        public const int AbilityDefinitionKind = 1;
        public const int AttributeDefinitionBlobKind = 1;
        public const int AttributeDefinitionKind = 4;
        public const int AttributeSetDefinitionBlobKind = 2;
        public const int AttributeSetDefinitionKind = 3;
        public const int GameplayCueDefinitionBlobKind = 3;
        public const int GameplayCueDefinitionKind = 6;
        public const int GameplayEffectDefinitionBlobKind = 4;
        public const int GameplayEffectDefinitionKind = 2;
        public const int GameplayTagDefinitionBlobKind = 5;
        public const int GameplayTagDefinitionKind = 5;
    }
}
