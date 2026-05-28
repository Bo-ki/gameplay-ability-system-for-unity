using Unity.Entities;

namespace GAS.Runtime
{
    public struct GEImmunityTagsComponent : IComponentData, IEnableableComponent
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
            GASManager.EntityManager.SetComponentData(ge, new GEImmunityTagsComponent
            {
                requirement = TagHelper.BuildRequirementMask(all, any ?? tags, none)
            });
            GASManager.EntityManager.SetComponentEnabled<GEImmunityTagsComponent>(ge, true);
        }
    }
}
