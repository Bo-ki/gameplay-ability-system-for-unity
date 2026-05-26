namespace GAS.Runtime
{
    public enum EGameplayEffectLifecycleState : byte
    {
        PendingApply = 0,
        Active = 1,
        Inhibited = 2,
        PendingRemove = 3,
    }
}
