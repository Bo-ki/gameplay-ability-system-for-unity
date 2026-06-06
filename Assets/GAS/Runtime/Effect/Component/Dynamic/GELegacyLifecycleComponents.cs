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

    [InternalBufferCapacity(2)]
    public struct GERemoveCommandBuffer : IBufferElementData
    {
        public int GameplayEffectCode;
    }

    public struct GECreatedByAbilityComponent : IComponentData
    {
        public Entity Ability;
    }
}
