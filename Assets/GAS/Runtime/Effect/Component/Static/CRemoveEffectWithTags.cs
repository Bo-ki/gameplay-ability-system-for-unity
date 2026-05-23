using Unity.Entities;

namespace GAS.Runtime
{
    public struct CRemoveEffectWithTags : IComponentData
    {
        public TagRequirementMask requirement;
    }
    
    public sealed class ConfRemoveEffectWithTags:GameplayEffectComponentConfig
    {
        public int[] tags;
        public int[] all;
        public int[] any;
        public int[] none;
        
        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            GASManager.EntityManager.AddComponentData(ge, new CRemoveEffectWithTags
            {
                requirement = TagHelper.BuildRequirementMask(all, any ?? tags, none)
            });
        }
    }
}
