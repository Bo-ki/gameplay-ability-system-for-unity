using Unity.Entities;

namespace GAS.Runtime
{
    public struct CAbilityActivationRequiredTags : IComponentData
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
            _entityManager.AddComponentData(ability, new CAbilityActivationRequiredTags
            {
                requirement = TagHelper.BuildRequirementMask(all ?? tags, any, none)
            });
        }
    }
}
