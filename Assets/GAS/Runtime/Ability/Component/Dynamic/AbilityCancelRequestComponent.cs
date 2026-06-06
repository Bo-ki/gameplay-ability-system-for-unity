using Unity.Entities;

namespace GAS.Runtime
{
    public enum EAbilityLifecycleReason : byte
    {
        Unknown = 0,
        ExplicitEnd = 1,
        ExplicitCancel = 2,
        ActivationCompleted = 3,
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
    public struct AbilityCancelRequestComponent : IComponentData, IEnableableComponent
    {
        public EAbilityLifecycleReason Reason;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public int SourceAbilityCode;
    }

    /// <summary>
    /// Ability 在本次 End/Cancel 清理后销毁。
    /// </summary>
    public struct AbilityDestroyOnCleanupComponent : IComponentData, IEnableableComponent
    {
    }

    public enum EAbilityLifecycleRequestKind : byte
    {
        End = 1,
        Cancel = 2,
    }

    /// <summary>
    /// Frame-local ability lifecycle request produced by non-ability-owned systems.
    /// AbilityLifecycleRequestSystem is the only runtime hot-path system that applies these
    /// records to ability enableable markers.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AbilityLifecycleRequestBuffer : IBufferElementData
    {
        public int Sequence;
        public EAbilityLifecycleRequestKind RequestKind;
        public EAbilityLifecycleReason Reason;
        public Entity Ability;
        public Entity SourceAbility;
        public Entity SourceEffect;
        public int SourceAbilityCode;
        public byte DestroyOnCleanup;
    }
}
