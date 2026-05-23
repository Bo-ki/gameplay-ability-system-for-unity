using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Presentation bridge: consumes simulation cue facts and drives managed cue entities.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCueGroup))]
    [UpdateBefore(typeof(SCueStart))]
    public partial struct SCueRequestBridge : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CGameplayEventBus>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBus)
                || !state.EntityManager.HasBuffer<BCueRequest>(eventBus))
            {
                return;
            }

            var em = state.EntityManager;
            var requests = em.GetBuffer<BCueRequest>(eventBus);
            if (requests.Length == 0)
                return;

            var snapshot = new NativeArray<BCueRequest>(requests.Length, Allocator.Temp);
            for (var i = 0; i < requests.Length; i++)
                snapshot[i] = requests[i];

            for (var i = 0; i < snapshot.Length; i++)
                Consume(em, snapshot[i]);

            snapshot.Dispose();
        }

        private static void Consume(EntityManager em, in BCueRequest request)
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
                    KillCue(em, request.CueEntity);
                    break;
            }
        }

        private static Entity ResolveSourceEntity(in BCueRequest request)
        {
            if (request.SourceEntity != Entity.Null)
                return request.SourceEntity;

            if (request.GameplayEffect != Entity.Null)
                return request.GameplayEffect;

            return request.SourceAbility;
        }

        private static CueSourceType ResolveSourceType(in BCueRequest request)
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
            if (!em.HasComponent<MCCue>(cueEntity))
                return;

            em.GetComponentData<MCCue>(cueEntity).cue.Stop();
        }

        private static void KillCue(EntityManager em, Entity cueEntity)
        {
            if (em.HasComponent<ECKillCue>(cueEntity))
                em.SetComponentEnabled<ECKillCue>(cueEntity, true);
            else
                em.DestroyEntity(cueEntity);
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
