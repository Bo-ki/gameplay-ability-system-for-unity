using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Legacy config-registry commit gate removed by the catalog Runtime Core path.
    /// The generated system AbilityCatalogCommitSystem owns Ability commit execution.
    /// This type remains only as a stable ordering anchor for generated system attributes.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateAfter(typeof(AbilityTryActivateSystem))]
    [UpdateBefore(typeof(GEEffectCommandIngestSystem))]
    public partial struct AbilityCommitSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
