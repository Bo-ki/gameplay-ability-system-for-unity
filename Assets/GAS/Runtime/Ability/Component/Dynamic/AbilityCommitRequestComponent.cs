using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Internal marker for the Ability commit gate.
    /// </summary>
    public struct AbilityCommitRequestComponent : IComponentData, IEnableableComponent
    {
        public Entity TargetAsc;
    }
}
