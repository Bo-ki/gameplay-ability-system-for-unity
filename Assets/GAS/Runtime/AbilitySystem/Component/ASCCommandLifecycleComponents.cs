using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// ASC owner-local 销毁命令。由边界入口追加，Core resolve 后清空。
    /// </summary>
    [InternalBufferCapacity(1)]
    public struct ASCDestroyCommandBuffer : IBufferElementData
    {
        public byte Requested;
    }

    public struct ASCCommandPendingComponent : IComponentData, IEnableableComponent
    {
    }

    public struct GERemoveCommandPendingComponent : IComponentData, IEnableableComponent
    {
    }

    /// <summary>
    /// ASC 正在销毁的 owner-local 状态。默认 disabled，仅销毁流程中 enabled。
    /// </summary>
    public struct ASCDestroyingComponent : IComponentData, IEnableableComponent
    {
    }
}
