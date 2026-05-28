using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup))]
    public partial struct CueStartSystem : ISystem
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
                     SystemAPI.Query<RefRO<CuePlayableTag>, CueManagedInstanceComponent>()
                         .WithDisabled<CuePlayingTag>()
                         .WithEntityAccess())
            {
                SystemAPI.SetComponentEnabled<CuePlayingTag>(cue, true);
                // 激活Cue
                mcCue.Cue.OnActivate(Time.time);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
