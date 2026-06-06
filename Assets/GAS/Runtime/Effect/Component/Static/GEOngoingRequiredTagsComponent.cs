using Unity.Entities;

namespace GAS.Runtime
{
    public struct GEOngoingRequiredTagsComponent : IComponentData, IEnableableComponent
    {
        public TagRequirementMask requirement;
    }

    public sealed class ConfOngoingRequiredTags : GameplayEffectComponentConfig
    {
        public int[] tags;
        public int[] all;
        public int[] any;
        public int[] none;

        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            entityManager.SetComponentData(ge, new GEOngoingRequiredTagsComponent
            {
                requirement = TagHelper.BuildRequirementMask(all ?? tags, any, none)
            });
            entityManager.SetComponentEnabled<GEOngoingRequiredTagsComponent>(ge, true);
        }
    }
}
