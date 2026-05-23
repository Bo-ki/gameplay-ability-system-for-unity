using Unity.Entities;

namespace GAS.Runtime
{
    public enum EAbilityPhase : byte
    {
        Ready = 0,
        Activating = 1,
        Active = 2,
        Ending = 3,
    }

    /// <summary>
    /// Ability Entity 上的数据侧运行时状态，不承载托管 Logic 引用。
    /// 完全 Blittable，兼容 Burst。
    /// </summary>
    public struct CAbilityRuntimeState : IComponentData
    {
        public EAbilityPhase Phase;
        public int RemainingFrame;
        public float Timer;
    }
}
