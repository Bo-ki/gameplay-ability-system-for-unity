using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Period 触发的 GE 定义引用。每次触发时通过 request 创建新的 GE instance。
    /// </summary>
    public struct BPeriodGEConfig : IBufferElementData
    {
        public int GameplayEffectCode;
    }
}
