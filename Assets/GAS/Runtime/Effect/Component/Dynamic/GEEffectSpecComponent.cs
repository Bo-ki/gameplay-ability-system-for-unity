using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// GE instance 本次施加规格。后续 MagnitudeEvaluation / Stack / Capture 都从这里取上下文参数。
    /// </summary>
    public struct GEEffectSpecComponent : IComponentData
    {
        public int GameplayEffectCode;
        public int Level;
        public int StackCount;
        public int DurationFrameOverride;
    }
}
