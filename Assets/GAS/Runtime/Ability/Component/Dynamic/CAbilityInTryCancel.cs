using Unity.Entities;

namespace GAS.Runtime
{
    public enum EAbilityLifecycleReason : byte
    {
        Unknown = 0,
        ExplicitEnd = 1,
        ExplicitCancel = 2,
        TimelineCompleted = 3,
        LifetimeExpired = 4,
        CancelMatchedAbility = 5,
        RemoveAbility = 6,
        AscDestroy = 7,
        GrantedEffectDeactivated = 8,
        GrantedEffectRemoved = 9,
        AttributeThreshold = 10,
    }

    /// <summary>
    /// 能力取消请求。Cancel 与 End 共用 cleanup 收尾，但保留触发原因和来源。
    /// </summary>
    public struct CAbilityInTryCancel : IComponentData
    {
        public EAbilityLifecycleReason Reason;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public int SourceAbilityCode;
    }

    /// <summary>
    /// Ability 在本次 End/Cancel 清理后销毁。
    /// </summary>
    public struct CAbilityDestroyOnCleanup : IComponentData
    {
    }
}
