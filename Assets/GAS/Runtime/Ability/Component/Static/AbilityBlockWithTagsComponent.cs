using Unity.Entities;

namespace GAS.Runtime
{
    public struct AbilityBlockWithTagsComponent : IComponentData, IEnableableComponent
    {
        public TagMaskComponent Tags;
    }
    
    public sealed class ConfBlockAbilityWithTags:AbilityComponentConfig
    {
        public int[] tags;

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.SetComponentData(ability, new AbilityBlockWithTagsComponent
            {
                Tags = TagHelper.BuildMask(tags, includeParents: false)
            });
            _entityManager.SetComponentEnabled<AbilityBlockWithTagsComponent>(ability, true);
        }
    }
}
