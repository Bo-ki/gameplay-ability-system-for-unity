using Unity.Entities;

namespace GAS.Runtime
{
    public enum EffectStackType
    {
        AggregateBySource, //目标(Target)上的每个源(Source)ASC都有一个单独的堆栈实例, 每个源(Source)可以应用堆栈中的X个GameplayEffect.

        AggregateByTarget //目标(Target)上只有一个堆栈实例而不管源(Source)如何, 每个源(Source)都可以在共享堆栈限制(Shared Stack Limit)内应用堆栈.
    }

    public enum EffectDurationRefreshPolicy
    {
        NeverRefresh, //不刷新Effect的持续时间

        RefreshOnSuccessfulApplication //每次apply成功后刷新Effect的持续时间, denyOverflowApplication如果为True则多余的Apply不会刷新Duration
    }

    public enum EffectPeriodResetPolicy
    {
        NeverRefresh, //不重置Effect的周期计时

        ResetOnSuccessfulApplication //每次apply成功后重置Effect的周期计时
    }

    public enum EffectExpirationPolicy
    {
        ClearEntireStack, //持续时间结束时,清除所有层数

        RemoveSingleStackAndRefreshDuration, //持续时间结束时减少一层，然后重新经历一个Duration，一直持续到层数减为0

        RefreshDuration //持续时间结束时,再次刷新Duration，这相当于无限Duration
    }

    public struct GEStackingDefinitionComponent : IComponentData, IEnableableComponent
    {
        public EffectStackType StackType;
        public int StackingCode;
        public int LimitCount;

        public EffectDurationRefreshPolicy EffectDurationRefreshPolicy;
        public EffectPeriodResetPolicy EffectPeriodResetPolicy;
        public EffectExpirationPolicy EffectExpirationPolicy;

        // Overflow 溢出逻辑处理
        public bool DenyOverflowApplication;
        public bool ClearStackOnOverflow;
    }

    public struct GEStackingRuntimeComponent : IComponentData, IEnableableComponent
    {
        public int StackCount;
    }

    public sealed class ConfStacking : GameplayEffectComponentConfig
    {
        public EffectStackType StackType;
        public int StackingCode;
        public int LimitCount;

        public EffectDurationRefreshPolicy EffectDurationRefreshPolicy;
        public EffectPeriodResetPolicy EffectPeriodResetPolicy;
        public EffectExpirationPolicy EffectExpirationPolicy;

        public bool denyOverflowApplication;
        public bool clearStackOnOverflow;
        public int[] OverflowEffectCodes;

        public override void LoadToGameplayEffectEntity(EntityManager entityManager, Entity ge)
        {
            var em = entityManager;
            em.SetComponentData(ge, new GEStackingDefinitionComponent
            {
                StackType = StackType,
                StackingCode = StackingCode,
                LimitCount = LimitCount,
                EffectDurationRefreshPolicy = EffectDurationRefreshPolicy,
                EffectPeriodResetPolicy = EffectPeriodResetPolicy,
                EffectExpirationPolicy = EffectExpirationPolicy,
                DenyOverflowApplication = denyOverflowApplication,
                ClearStackOnOverflow = clearStackOnOverflow,
            });
            em.SetComponentEnabled<GEStackingDefinitionComponent>(ge, true);

            if (OverflowEffectCodes == null || OverflowEffectCodes.Length == 0)
                return;

            var overflowBuf = em.GetBuffer<GEOverflowConfigBuffer>(ge);
            overflowBuf.Clear();
            foreach (var effectCode in OverflowEffectCodes)
            {
                if (effectCode > 0)
                    overflowBuf.Add(new GEOverflowConfigBuffer { GameplayEffectCode = effectCode });
            }
            em.SetComponentEnabled<GEOverflowConfigBuffer>(ge, overflowBuf.Length > 0);
        }
    }
}
