using Unity.Entities;

namespace GAS.Runtime
{
    public struct CAbilityActivationBlockedTags : IComponentData
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
            _entityManager.AddComponentData(ability, new CAbilityActivationBlockedTags
            {
                requirement = TagHelper.BuildRequirementMask(all, any, none ?? tags)
            });
        }
    }
}
