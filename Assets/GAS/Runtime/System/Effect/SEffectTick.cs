using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASEffectGroup))]
    public partial struct SEffectTick : ISystem
    {
        public void OnCreate(ref SystemState state) { }
        public void OnUpdate(ref SystemState state) { }
        public void OnDestroy(ref SystemState state) { }
    }
}
