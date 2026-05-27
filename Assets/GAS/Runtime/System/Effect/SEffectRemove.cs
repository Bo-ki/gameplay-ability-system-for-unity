using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASEffectGroup))]
    [UpdateAfter(typeof(SEffectTick))]
    public partial struct SEffectRemove : ISystem
    {
        private EntityQuery _destroyQuery;
        private EntityQuery _removeRequestQuery;

        public void OnCreate(ref SystemState state)
        {
            _destroyQuery = SystemAPI.QueryBuilder()
                .WithAll<CEffectDestroy>()
                .Build();
            _removeRequestQuery = SystemAPI.QueryBuilder()
                .WithAll<CRemoveGameplayEffectRequest>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var eventBusEntity = SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var eventWriter = eventBusEntity != Entity.Null
                ? EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity)
                : default;

            try
            {
                ProcessRemoveRequests(em);
                ProcessDestroyMarkers(em, ref eventWriter);
            }
            finally
            {
                eventWriter.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state) { }

        private void ProcessRemoveRequests(EntityManager em)
        {
            using var requests = _removeRequestQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < requests.Length; i++)
            {
                var requestEntity = requests[i];
                if (!em.Exists(requestEntity)
                    || !em.HasComponent<CRemoveGameplayEffectRequest>(requestEntity))
                {
                    continue;
                }

                var request = em.GetComponentData<CRemoveGameplayEffectRequest>(requestEntity);
                MarkMatchingEffectsForRemoval(em, in request);
                em.DestroyEntity(requestEntity);
            }
        }

        private void ProcessDestroyMarkers(
            EntityManager em,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            using var effects = _destroyQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (em.Exists(effect))
                    EffectRuntimeUtility.CleanupActiveEffect(em, effect, ref eventWriter);
            }
        }

        private static void MarkMatchingEffectsForRemoval(
            EntityManager em,
            in CRemoveGameplayEffectRequest request)
        {
            if (request.TargetAsc == Entity.Null
                || !em.Exists(request.TargetAsc)
                || !em.HasBuffer<BGameplayEffect>(request.TargetAsc))
            {
                return;
            }

            var effects = em.GetBuffer<BGameplayEffect>(request.TargetAsc);
            for (var i = effects.Length - 1; i >= 0; i--)
            {
                var effect = effects[i].GameplayEffect;
                if (effect == Entity.Null || !em.Exists(effect))
                    continue;
                if (request.GameplayEffectCode > 0
                    && (!em.HasComponent<CEffectSpecData>(effect)
                        || em.GetComponentData<CEffectSpecData>(effect).GameplayEffectCode != request.GameplayEffectCode))
                {
                    continue;
                }

                EffectRuntimeUtility.MarkEffectForRemoval(em, effect);
            }
        }
    }
}
