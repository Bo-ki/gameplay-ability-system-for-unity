using Unity.Entities;

namespace GAS.Runtime
{
    public struct GEApplicationRequiredTagsComponent : IComponentData, IEnableableComponent
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
            GASManager.EntityManager.SetComponentData(ge, new GEApplicationRequiredTagsComponent
            {
                requirement = TagHelper.BuildRequirementMask(all ?? tags, any, none)
            });
            GASManager.EntityManager.SetComponentEnabled<GEApplicationRequiredTagsComponent>(ge, true);
        }
    }
}
