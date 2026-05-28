using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 固有 Tag 来源记录。SourceTagIndex 表示外部显式授予的 Tag，TagIndex 表示它展开出的自身或父级 Tag。
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct TagFixedSourceBuffer : IBufferElementData
    {
        public int SourceTagIndex;
        public int TagIndex;
    }
}
