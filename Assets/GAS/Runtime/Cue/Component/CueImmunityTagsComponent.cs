using Unity.Entities;

namespace GAS.Runtime
{
    public struct CueImmunityTagsComponent : IComponentData, IEnableableComponent
    {
        public TagRequirementMask requirement;
    }
}
