using Unity.Burst;
using Unity.Collections;
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
        private static EntityManager _cachedTimerEntityManager;
        private static Entity _cachedGlobalTimer;
        private static bool _hasCachedGlobalTimer;

        public static void RegisterKnownGlobalTimer(EntityManager em, Entity globalTimer)
        {
            if (!IsValidGlobalTimer(em, globalTimer))
                return;

            _cachedTimerEntityManager = em;
            _cachedGlobalTimer = globalTimer;
            _hasCachedGlobalTimer = true;
        }

        public static void ResetKnownGlobalTimer(EntityManager em)
        {
            if (_hasCachedGlobalTimer && _cachedTimerEntityManager.Equals(em))
            {
                _hasCachedGlobalTimer = false;
                _cachedGlobalTimer = Entity.Null;
                _cachedTimerEntityManager = default;
            }
        }

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
            if (TryResolveCachedGlobalTimer(em, out frame))
                return true;

            return TryResolveSingletonGlobalTimer(em, out frame);
        }

        private static bool TryResolveCachedGlobalTimer(EntityManager em, out int frame)
        {
            if (!_hasCachedGlobalTimer || !_cachedTimerEntityManager.Equals(em))
            {
                frame = 0;
                return false;
            }

            if (IsValidGlobalTimer(em, _cachedGlobalTimer))
            {
                frame = em.GetComponentData<GlobalTimer>(_cachedGlobalTimer).Frame;
                return true;
            }

            _hasCachedGlobalTimer = false;
            _cachedGlobalTimer = Entity.Null;
            frame = 0;
            return false;
        }

        private static bool TryResolveSingletonGlobalTimer(EntityManager em, out int frame)
        {
            frame = 0;
            if (em.World == null || !em.World.IsCreated)
                return false;

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GlobalTimer>());
            using var timers = query.ToEntityArray(Allocator.Temp);
            if (timers.Length != 1 || !IsValidGlobalTimer(em, timers[0]))
                return false;

            RegisterKnownGlobalTimer(em, timers[0]);
            frame = em.GetComponentData<GlobalTimer>(timers[0]).Frame;
            return true;
        }

        private static bool IsValidGlobalTimer(EntityManager em, Entity globalTimer)
        {
            return globalTimer != Entity.Null
                   && em.World != null
                   && em.World.IsCreated
                   && em.Exists(globalTimer)
                   && em.HasComponent<GlobalTimer>(globalTimer);
        }
    }
}
