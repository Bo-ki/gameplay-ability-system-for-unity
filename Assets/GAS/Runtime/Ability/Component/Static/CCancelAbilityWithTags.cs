using Unity.Entities;

namespace GAS.Runtime
{
    public struct CCancelAbilityWithTags : IComponentData
    {
        public CTagMask Tags;
    }
    
    public sealed class ConfCancelAbilityWithTags:AbilityComponentConfig
    {
        public int[] tags;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.AddComponentData(ability, new CCancelAbilityWithTags
            {
                Tags = TagHelper.BuildMask(tags, includeParents: false)
            });
        }
    }
}
