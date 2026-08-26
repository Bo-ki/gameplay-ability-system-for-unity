namespace GAS.Editor
{
    /// <summary>
    /// 仅用于 Editor 行归类与生成输入指纹，不代表 Runtime 的定义权威或执行类型。
    /// </summary>
    public enum GasDefinitionKind : byte
    {
        None = 0,
        Ability = 1,
        GameplayEffect = 2,
        AttributeSet = 3,
        Attribute = 4,
        GameplayTag = 5,
        GameplayCue = 6,
    }
}
