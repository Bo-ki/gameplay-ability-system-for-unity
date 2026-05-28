using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Presentation bridge: consumes simulation cue facts and drives managed cue entities.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup))]
    [UpdateBefore(typeof(CueStartSystem))]
    public partial struct CueRequestBridgeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayEventBusComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var eventBus)
                || !state.EntityManager.HasBuffer<CueRequestBuffer>(eventBus))
            {
                return;
            }

            var em = state.EntityManager;
            var requests = em.GetBuffer<CueRequestBuffer>(eventBus);
            if (requests.Length == 0)
                return;

            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var snapshot = new NativeArray<CueRequestBuffer>(requests.Length, Allocator.Temp);
            for (var i = 0; i < requests.Length; i++)
                snapshot[i] = requests[i];

            for (var i = 0; i < snapshot.Length; i++)
                Consume(em, ref ecb, snapshot[i]);

            snapshot.Dispose();
        }

        private static void Consume(EntityManager em, ref EntityCommandBuffer ecb, in CueRequestBuffer request)
        {
            if (request.CueEntity == Entity.Null || !em.Exists(request.CueEntity))
                return;

            switch (request.CueEvent)
            {
                case EGameplayCueEvent.OnApply:
                case EGameplayCueEvent.OnAdd:
                case EGameplayCueEvent.OnActivate:
                case EGameplayCueEvent.OnTick:
                case EGameplayCueEvent.OnDeactivate:
                case EGameplayCueEvent.OnRemove:
                case EGameplayCueEvent.Play:
                    CueHelper.TryPlayCueOnAsc(
                        em,
                        request.TargetAsc,
                        request.CueEntity,
                        ResolveSourceEntity(request),
                        ResolveSourceType(request));
                    break;
                case EGameplayCueEvent.StopTick:
                    StopCue(em, request.CueEntity);
                    break;
                case EGameplayCueEvent.Kill:
                    KillCue(em, ref ecb, request.CueEntity);
                    break;
            }
        }

        private static Entity ResolveSourceEntity(in CueRequestBuffer request)
        {
            if (request.SourceEntity != Entity.Null)
                return request.SourceEntity;

            if (request.GameplayEffect != Entity.Null)
                return request.GameplayEffect;

            return request.SourceAbility;
        }

        private static CueSourceType ResolveSourceType(in CueRequestBuffer request)
        {
            if (request.SourceType != CueSourceType.None)
                return request.SourceType;

            if (request.GameplayEffect != Entity.Null)
                return CueSourceType.GameplayEffect;

            return request.SourceAbility != Entity.Null
                ? CueSourceType.GameplayAbility
                : CueSourceType.None;
        }

        private static void StopCue(EntityManager em, Entity cueEntity)
        {
            if (!em.HasComponent<CueManagedInstanceComponent>(cueEntity))
                return;

            em.GetComponentData<CueManagedInstanceComponent>(cueEntity).Cue.Stop();
        }

        private static void KillCue(EntityManager em, ref EntityCommandBuffer ecb, Entity cueEntity)
        {
            if (em.HasComponent<CueKillRequestTag>(cueEntity))
                em.SetComponentEnabled<CueKillRequestTag>(cueEntity, true);
            else
                ecb.DestroyEntity(cueEntity);
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
