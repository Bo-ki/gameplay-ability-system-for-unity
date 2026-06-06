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

        public override void LoadToGameplayAbilityEntity(EntityManager entityManager, Entity ability)
        {
            entityManager.SetComponentData(ability, new AbilityBlockWithTagsComponent
            {
                Tags = TagHelper.BuildMask(tags, includeParents: false)
            });
            entityManager.SetComponentEnabled<AbilityBlockWithTagsComponent>(ability, true);
        }
    }
}
