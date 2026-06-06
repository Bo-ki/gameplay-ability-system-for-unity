using Unity.Entities;

namespace GAS.Runtime
{
    public struct AbilityAssetTagsComponent : IComponentData, IEnableableComponent
    {
        public TagMaskComponent Tags;
    }
    
    public sealed class ConfAbilityAssetTags:AbilityComponentConfig
    {
        public int[] tags;
        
        public override void LoadToGameplayAbilityEntity(EntityManager entityManager, Entity ability)
        {
            entityManager.SetComponentData(ability, new AbilityAssetTagsComponent
            {
                Tags = TagHelper.BuildMask(tags, includeParents: true)
            });
            entityManager.SetComponentEnabled<AbilityAssetTagsComponent>(ability, true);
        }
    }
}
