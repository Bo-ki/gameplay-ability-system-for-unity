namespace GAS.Runtime
{
    /// <summary>
    /// 声明“已解析目标”规则；实际目标身份由 Runtime v1 TargetResolve 产生，不在 Authoring 类型中携带 Entity。
    /// </summary>
    public sealed class CatchTarget : TargetCatcherBase<XParamNone>
    {
    }
}
