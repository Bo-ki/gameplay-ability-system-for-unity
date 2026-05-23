using Unity.Entities;

namespace GAS.Runtime
{
    public struct CBlockAbilityWithTags : IComponentData
    {
        public CTagMask Tags;
    }
    
    public sealed class ConfBlockAbilityWithTags:AbilityComponentConfig
    {
        public int[] tags;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.AddComponentData(ability, new CBlockAbilityWithTags
            {
                Tags = TagHelper.BuildMask(tags, includeParents: false)
            });
        }
    }
}
