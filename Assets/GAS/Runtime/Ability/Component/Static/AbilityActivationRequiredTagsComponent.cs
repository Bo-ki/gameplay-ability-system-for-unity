using Unity.Entities;

namespace GAS.Runtime
{
    public struct AbilityActivationRequiredTagsComponent : IComponentData, IEnableableComponent
    {
        public TagRequirementMask requirement;
    }
    
    public sealed class ConfAbilityActivationRequiredTags:AbilityComponentConfig
    {
        public int[] tags;
        public int[] all;
        public int[] any;
        public int[] none;
        
        public override void LoadToGameplayAbilityEntity(Entity ability)
        {
            _entityManager.SetComponentData(ability, new AbilityActivationRequiredTagsComponent
            {
                requirement = TagHelper.BuildRequirementMask(all ?? tags, any, none)
            });
            _entityManager.SetComponentEnabled<AbilityActivationRequiredTagsComponent>(ability, true);
        }
    }
}
