using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASExecutionCalculationExtensionGroup))]
    public partial struct SHeadlessAutoBattleExecuteCalculation : ISystem
    {
        public void OnCreate(ref SystemState state) { }
        public void OnUpdate(ref SystemState state) { }
        public void OnDestroy(ref SystemState state) { }
    }
}
