namespace GAS.Runtime
{
    /// <summary>
    /// 授予能力的取消激活策略
    /// </summary>
    public enum GrantedAbilityDeactivationPolicy
    {
        /// <summary>
        /// 无相关取消激活逻辑, 需要用户调用ASC取消激活
        /// </summary>
        None,

        /// <summary>
        /// 同步GE，GE失活时取消激活
        /// </summary>
        SyncWithEffect,
    }
}
