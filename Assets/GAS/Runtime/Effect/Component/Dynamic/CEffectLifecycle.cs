using Unity.Entities;

namespace GAS.Runtime
{
    public enum EGameplayEffectLifecycleState : byte
    {
        PendingApply = 0,
        Active = 1,
        Inhibited = 2,
        PendingRemove = 3,
    }

    /// <summary>
    /// GE instance 的运行时生命周期。Inhibited 表示 GE 仍已应用，但暂时撤销运行时贡献。
    /// </summary>
    public struct CEffectLifecycle : IComponentData
    {
        public EGameplayEffectLifecycleState State;
        public EGameplayEffectLifecycleState PreviousState;
        public int StateStartFrame;
    }
}
