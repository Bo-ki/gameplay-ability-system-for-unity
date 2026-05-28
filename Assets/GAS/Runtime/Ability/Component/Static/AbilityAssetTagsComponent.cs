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
        
        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.SetComponentData(ability, new AbilityAssetTagsComponent
            {
                Tags = TagHelper.BuildMask(tags, includeParents: true)
            });
            _entityManager.SetComponentEnabled<AbilityAssetTagsComponent>(ability, true);
        }
    }
}
