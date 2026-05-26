using Unity.Entities;

namespace GAS.Runtime
{
    public struct CEffectCleanup : IComponentData
    {
        public int RequestedFrame;
        public EGameplayEffectLifecycleState CleanupState;
        public int RequestedCleanupWorkFlags;
        public Entity SourceAsc;
        public Entity TargetAsc;
    }

    public struct CEffectFinalDestroy : IComponentData
    {
        public int RequestedFrame;
        public int CleanupSequence;
        public Entity TargetAsc;
    }

    /// <summary>
    /// Legacy removal marker retained as a broad query/fallback guard while cleanup semantics move to CEffectCleanup.
    /// </summary>
    public struct CEffectDestroy : IComponentData
    {
    }
}
