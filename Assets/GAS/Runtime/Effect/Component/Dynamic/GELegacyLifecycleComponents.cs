using Unity.Entities;

namespace GAS.Runtime
{
    public struct GEEffectLifecycleComponent : IComponentData
    {
        public EGameplayEffectLifecycleState State;
        public EGameplayEffectLifecycleState PreviousState;
        public int StateStartFrame;
    }

    public struct GEEffectPendingApplyComponent : IComponentData
    {
    }

    public struct GEEffectCleanupComponent : IComponentData
    {
    }

    public struct GEEffectFinalDestroyComponent : IComponentData, IEnableableComponent
    {
    }

    public struct GEEffectDestroyComponent : IComponentData, IEnableableComponent
    {
    }

    public struct GERemoveRequestComponent : IComponentData
    {
        public Entity TargetAsc;
        public int GameplayEffectCode;
    }

    public struct GECreatedByAbilityComponent : IComponentData
    {
        public Entity Ability;
    }
}
