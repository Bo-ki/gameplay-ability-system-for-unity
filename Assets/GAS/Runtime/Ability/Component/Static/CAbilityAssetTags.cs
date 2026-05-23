using Unity.Entities;

namespace GAS.Runtime
{
    public struct CAbilityAssetTags : IComponentData
    {
        public CTagMask Tags;
    }
    
    public sealed class ConfAbilityAssetTags:AbilityComponentConfig
    {
        public int[] tags;
        
        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.AddComponentData(ability, new CAbilityAssetTags
            {
                Tags = TagHelper.BuildMask(tags, includeParents: true)
            });
        }
    }
}
