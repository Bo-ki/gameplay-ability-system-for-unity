namespace GAS.Runtime
{
    /// <summary>
    /// ASC 固有 Tag 投影。CTagMask 是当前有效 Tag，CFixedTagMask 只记录不会随 GE/Ability 生命周期移除的部分。
    /// </summary>
    public struct CFixedTagMask : Unity.Entities.IComponentData
    {
        public CTagMask Mask;
    }
}
