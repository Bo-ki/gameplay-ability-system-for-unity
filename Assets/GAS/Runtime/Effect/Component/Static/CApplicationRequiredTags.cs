using Unity.Entities;

namespace GAS.Runtime
{
    public struct CApplicationRequiredTags : IComponentData
    {
        public TagRequirementMask requirement;
    }
    
    public sealed class ConfApplicationRequiredTags:GameplayEffectComponentConfig
    {
        public int[] tags;
        public int[] all;
        public int[] any;
        public int[] none;
        
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            GASManager.EntityManager.AddComponentData(ge, new CApplicationRequiredTags
            {
                requirement = TagHelper.BuildRequirementMask(all ?? tags, any, none)
            });
        }
    }
}
