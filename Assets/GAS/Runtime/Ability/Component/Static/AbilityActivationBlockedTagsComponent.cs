using Unity.Entities;

namespace GAS.Runtime
{
    public struct AbilityActivationBlockedTagsComponent : IComponentData, IEnableableComponent
    {
        public TagRequirementMask requirement;
    }
    
    public sealed class ConfAbilityActivationBlockedTags:AbilityComponentConfig
    {
        public int[] tags;
        public int[] all;
        public int[] any;
        public int[] none;

        public override void LoadToGameplayAbilityEntity(EntityManager entityManager, Entity ability)
        {
            entityManager.SetComponentData(ability, new AbilityActivationBlockedTagsComponent
            {
                requirement = TagHelper.BuildRequirementMask(all, any, none ?? tags)
            });
            entityManager.SetComponentEnabled<AbilityActivationBlockedTagsComponent>(ability, true);
        }
    }
}
