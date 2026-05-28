namespace GAS.Runtime
{
    /// <summary>
    /// ASC 固有 Tag 投影。TagMaskComponent 是当前有效 Tag，TagFixedMaskComponent 只记录不会随 GE/Ability 生命周期移除的部分。
    /// </summary>
    public struct TagFixedMaskComponent : Unity.Entities.IComponentData
    {
        public TagMaskComponent Mask;
    }
}
