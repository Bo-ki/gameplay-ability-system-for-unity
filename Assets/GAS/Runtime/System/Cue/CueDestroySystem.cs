using Unity.Burst;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup))]
    [UpdateAfter(typeof(CueEndSystem))]
    public partial struct CueDestroySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CueKillRequestTag>();
        }

        //[BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            foreach (var (_,mcCue,cueEntity) in SystemAPI.Query<RefRO<CueKillRequestTag>,CueManagedInstanceComponent>().WithEntityAccess())
            {
                // 触发销毁时回调
                mcCue.Cue.OnDestroy(Time.time);
                // 销毁Cue
                ecb.DestroyEntity(cueEntity);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }
}
