namespace GAS.Runtime
{
    /// <summary>
    /// 描述属性修改器的算术操作；仅作为配置表与编辑器的纯值协议，不承载运行时状态。
    /// </summary>
    public enum EModifierOp : byte
    {
        Add = 0,
        Subtract = 3,
        Multiply = 1,
        Divide = 4,
        Override = 2,
    }

    /// <summary>
    /// 描述 GameplayEffect 的堆叠归属策略。
    /// </summary>
    public enum EffectStackType
    {
        AggregateBySource,
        AggregateByTarget,
    }

    /// <summary>
    /// 描述成功应用时是否刷新 Effect 持续时间。
    /// </summary>
    public enum EffectDurationRefreshPolicy
    {
        NeverRefresh,
        RefreshOnSuccessfulApplication,
    }

    /// <summary>
    /// 描述成功应用时是否重置 Effect 周期计时。
    /// </summary>
    public enum EffectPeriodResetPolicy
    {
        NeverRefresh,
        ResetOnSuccessfulApplication,
    }

    /// <summary>
    /// 描述 Effect 堆叠到期后的清理策略。
    /// </summary>
    public enum EffectExpirationPolicy
    {
        ClearEntireStack,
        RemoveSingleStackAndRefreshDuration,
        RefreshDuration,
    }
}
