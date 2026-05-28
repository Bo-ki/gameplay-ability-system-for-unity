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

        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.SetComponentData(ability, new AbilityActivationBlockedTagsComponent
            {
                requirement = TagHelper.BuildRequirementMask(all, any, none ?? tags)
            });
            _entityManager.SetComponentEnabled<AbilityActivationBlockedTagsComponent>(ability, true);
        }
    }
}
