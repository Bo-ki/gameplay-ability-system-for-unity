namespace GAS.Runtime
{
    /// <summary>
    /// 授予能力的移除策略
    /// </summary>
    public enum GrantedAbilityRemovePolicy
    {
        /// <summary>
        /// 不移除
        /// </summary>
        None,

        /// <summary>
        /// 同步GE，GE移除时移除
        /// </summary>
        SyncWithEffect,

        /// <summary>
        /// 能力结束时自己移除
        /// </summary>
        WhenEnd,

        /// <summary>
        /// 能力取消时自己移除
        /// </summary>
        WhenCancel,

        /// <summary>
        /// 能力结束或取消时自己移除
        /// </summary>
        WhenCancelOrEnd,
    }
}
