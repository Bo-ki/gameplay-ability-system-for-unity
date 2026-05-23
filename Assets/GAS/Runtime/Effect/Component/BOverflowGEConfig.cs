using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Stacking overflow 触发的 GE 定义引用。触发时通过 request 创建新的 GE instance。
    /// </summary>
    public struct BOverflowGEConfig : IBufferElementData
    {
        public int GameplayEffectCode;
    }
}
