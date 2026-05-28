using Unity.Entities;

namespace GAS.Runtime
{
    public struct CueRequiredTagsComponent : IComponentData, IEnableableComponent
    {
        public TagRequirementMask requirement;
    }
}
