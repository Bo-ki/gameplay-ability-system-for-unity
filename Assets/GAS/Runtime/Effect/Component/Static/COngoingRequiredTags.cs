using Unity.Entities;

namespace GAS.Runtime
{
    public struct COngoingRequiredTags : IComponentData
    {
        public TagRequirementMask requirement;
    }

    public sealed class ConfOngoingRequiredTags : GameplayEffectComponentConfig
    {
        public int[] tags;
        public int[] all;
        public int[] any;
        public int[] none;

        public override void LoadToGameplayEffectEntity(Entity ge)
        {
            GASManager.EntityManager.AddComponentData(ge, new COngoingRequiredTags
            {
                requirement = TagHelper.BuildRequirementMask(all ?? tags, any, none)
            });
        }
    }
}
