using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup))]
    [UpdateAfter(typeof(CueStartSystem))]
    public partial struct CueTickSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CuePlayingTag>();
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (_, mcCue) in SystemAPI.Query<RefRO<CuePlayingTag>, CueManagedInstanceComponent>())
                mcCue.Cue.OnTick(Time.time);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
