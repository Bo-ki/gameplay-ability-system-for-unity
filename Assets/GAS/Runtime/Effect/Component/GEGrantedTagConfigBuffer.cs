using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// GE Entity 上的授权 Tag 配置 Buffer。
    /// GE 激活时将这些 Tag 授予 ASC，失活时移除。
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct GEGrantedTagConfigBuffer : IBufferElementData, IEnableableComponent
    {
        public int TagIndex;
    }
}
