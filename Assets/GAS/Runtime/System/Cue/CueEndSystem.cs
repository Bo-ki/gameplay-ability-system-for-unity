using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup))]
    [UpdateAfter(typeof(CueTickSystem))]
    public partial struct CueEndSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CuePlayableTag>();
            state.RequireForUpdate<CuePlayingTag>();
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (_, mcCue, cue) in
                     SystemAPI.Query<RefRO<CuePlayingTag>, CueManagedInstanceComponent>()
                         .WithDisabled<CuePlayableTag>()
                         .WithEntityAccess())
            {
                SystemAPI.SetComponentEnabled<CuePlayingTag>(cue, false);
                // 失活Cue
                mcCue.Cue.OnDeactivate(Time.time);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
