using Unity.Entities;

namespace GAS.Runtime
{
    public struct CPlayImmunitedTags : IComponentData
    {
        public TagRequirementMask requirement;
    }
}
