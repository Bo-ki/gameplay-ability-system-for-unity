using Unity.Entities;

namespace GAS.Runtime
{
    public struct CEffectLifecycle : IComponentData
    {
        public EGameplayEffectLifecycleState State;
        public EGameplayEffectLifecycleState PreviousState;
        public int StateStartFrame;
    }

    public struct CEffectPendingApply : IComponentData
    {
    }

    public struct CEffectCleanup : IComponentData
    {
    }

    public struct CEffectFinalDestroy : IComponentData
    {
    }

    public struct CEffectDestroy : IComponentData
    {
    }

    public struct CRemoveGameplayEffectRequest : IComponentData
    {
        public Entity TargetAsc;
        public int GameplayEffectCode;
    }

    public struct CCreatedByAbility : IComponentData
    {
        public Entity Ability;
    }
}
