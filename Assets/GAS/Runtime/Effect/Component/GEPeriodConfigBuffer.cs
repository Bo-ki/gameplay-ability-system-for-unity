using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Period 触发的 GE 定义引用。每次触发时通过 request 创建新的 GE instance。
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct GEPeriodConfigBuffer : IBufferElementData, IEnableableComponent
    {
        public int GameplayEffectCode;
    }
}
