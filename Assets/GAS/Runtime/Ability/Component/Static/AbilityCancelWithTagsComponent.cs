using Unity.Entities;

namespace GAS.Runtime
{
    public struct AbilityCancelWithTagsComponent : IComponentData, IEnableableComponent
    {
        public TagMaskComponent Tags;
    }
    
    public sealed class ConfCancelAbilityWithTags:AbilityComponentConfig
    {
        public int[] tags;

        public override void LoadToGameplayAbilityEntity(EntityManager entityManager, Entity ability)
        {
            entityManager.SetComponentData(ability, new AbilityCancelWithTagsComponent
            {
                Tags = TagHelper.BuildMask(tags, includeParents: false)
            });
            entityManager.SetComponentEnabled<AbilityCancelWithTagsComponent>(ability, true);
        }
    }
}
