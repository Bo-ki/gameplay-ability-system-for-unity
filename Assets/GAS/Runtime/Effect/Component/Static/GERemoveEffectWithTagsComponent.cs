using Unity.Entities;

namespace GAS.Runtime
{
    public struct GERemoveEffectWithTagsComponent : IComponentData, IEnableableComponent
    {
        public TagRequirementMask requirement;
    }
    
    public sealed class ConfRemoveEffectWithTags:GameplayEffectComponentConfig
    {
        public int[] tags;
        public int[] all;
        public int[] any;
        public int[] none;
        
        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            entityManager.SetComponentData(ge, new GERemoveEffectWithTagsComponent
            {
                requirement = TagHelper.BuildRequirementMask(all, any ?? tags, none)
            });
            entityManager.SetComponentEnabled<GERemoveEffectWithTagsComponent>(ge, true);
        }
    }
}
