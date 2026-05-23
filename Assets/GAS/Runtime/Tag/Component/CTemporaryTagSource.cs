using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 临时 Tag 来源记录，与 CTagMask 配合使用。
    /// 当 Temporary Tag 被移除时，需要知道是哪个 Entity 授予的。
    /// </summary>
    public struct BTempTagSource : IBufferElementData
    {
        public int TagIndex;
        public Entity Source;
    }
}
