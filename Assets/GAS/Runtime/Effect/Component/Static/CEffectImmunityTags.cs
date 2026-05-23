using Unity.Entities;

namespace GAS.Runtime
{
    public struct CEffectImmunityTags : IComponentData
    {
        public TagRequirementMask requirement;
    }
    
    public sealed class ConfEffectImmunityTags:GameplayEffectComponentConfig
    {
        public int[] tags;
        public int[] all;
        public int[] any;
        public int[] none;
        
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            GASManager.EntityManager.AddComponentData(ge, new CEffectImmunityTags
            {
                requirement = TagHelper.BuildRequirementMask(all, any ?? tags, none)
            });
        }
    }
}
