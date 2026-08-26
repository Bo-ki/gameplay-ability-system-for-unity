namespace GAS.Runtime
{
    /// <summary>
    /// 声明 Self 目标规则；实际 Owner identity 由 Runtime v1 session 解析。
    /// </summary>
    public sealed class CatchSelf : TargetCatcherBase<XParamNone>
    {
    }
}
