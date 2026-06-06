using Unity.Burst;
using Unity.Entities;

namespace GAS.Runtime
{
    public struct GlobalTimer : IComponentData
    {
        public int Frame;
        public int Turn;
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASFramePrepareSystemGroup))]
    public partial struct GASGlobalTimerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            //state.RequireForUpdate<GASRunningTag>();
            state.RequireForUpdate<GlobalTimer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var globalFrameTimer = SystemAPI.GetSingletonRW<GlobalTimer>();
            globalFrameTimer.ValueRW.Frame += 1;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {

        }
    }

    public static class GASRuntimeFrameContext
    {
        public static int ResolveCurrentFrame(EntityManager em)
        {
            return TryResolveCurrentFrame(em, out var frame) ? frame : 0;
        }

        public static bool TryResolveCurrentFrame(EntityManager em, out int frame)
        {
            return TryResolveKnownGlobalTimer(em, out frame);
        }

        private static bool TryResolveKnownGlobalTimer(EntityManager em, out int frame)
        {
            if (!GASManager.IsInitialized || !GASManager.EntityManager.Equals(em))
            {
                frame = 0;
                return false;
            }

            var globalTimer = GASManager.EntityGlobalTimer;
            if (globalTimer != Entity.Null
                && em.Exists(globalTimer)
                && em.HasComponent<GlobalTimer>(globalTimer))
            {
                frame = em.GetComponentData<GlobalTimer>(globalTimer).Frame;
                return true;
            }

            frame = 0;
            return false;
        }
    }
}
