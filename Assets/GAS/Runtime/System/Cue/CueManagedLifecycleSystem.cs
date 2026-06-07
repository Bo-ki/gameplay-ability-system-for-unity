using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace GAS.Runtime
{
    /// <summary>
    /// Managed presentation lifecycle for GameplayCue entities.
    /// This boundary system intentionally stays on the main thread because it drives managed cue objects.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup))]
    public partial struct CueManagedLifecycleSystem : ISystem
    {
        private EntityQuery _cueQuery;

        public void OnCreate(ref SystemState state)
        {
            _cueQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<CueManagedInstanceComponent>(),
                    ComponentType.ReadWrite<CuePresentationRequestComponent>(),
                    ComponentType.ReadWrite<CueRuntimeActiveTag>(),
                },
            });
            state.RequireForUpdate(_cueQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_cueQuery.IsEmpty)
                return;

            var em = state.EntityManager;
            var now = Time.time;

            using var cueEntities = _cueQuery.ToEntityArray(Allocator.Temp);
            ProcessRequests(em, cueEntities, now);
            ProcessStarts(em, cueEntities, now);
            ProcessRequests(em, cueEntities, now);
            ProcessTicks(em, cueEntities, now);
            ProcessRequests(em, cueEntities, now);
            ProcessEnds(em, cueEntities, now);
            ProcessDestroys(em, cueEntities, now);
        }

        private static void ProcessRequests(
            EntityManager em,
            NativeArray<Entity> cueEntities,
            float now)
        {
            for (var i = 0; i < cueEntities.Length; i++)
            {
                var cueEntity = cueEntities[i];
                var request = em.GetComponentData<CuePresentationRequestComponent>(cueEntity);
                if (!request.HasRequests)
                    continue;

                var cue = em.GetComponentData<CueManagedInstanceComponent>(cueEntity).Cue;
                if (request.ResetRequested != 0)
                    cue.Reset();

                if (request.SourceUpdateRequested != 0)
                    cue.SetSourceEntity(request.SourceEntity, request.SourceType);

                if (request.RemoveTargetRequested != 0)
                {
                    cue.ApplyRemoveFromTargetAsc(now);
                    request.TargetAsc = Entity.Null;
                }

                if (request.AddTargetRequested != 0 && request.TargetAsc != Entity.Null)
                    cue.ApplyAddToTargetAsc(request.TargetAsc, now);

                request.ClearRequests();
                em.SetComponentData(cueEntity, request);
            }
        }

        private static void ProcessStarts(
            EntityManager em,
            NativeArray<Entity> cueEntities,
            float now)
        {
            for (var i = 0; i < cueEntities.Length; i++)
            {
                var cueEntity = cueEntities[i];
                if (!em.IsComponentEnabled<CuePlayableTag>(cueEntity)
                    || em.IsComponentEnabled<CuePlayingTag>(cueEntity))
                {
                    continue;
                }

                var cue = em.GetComponentData<CueManagedInstanceComponent>(cueEntity).Cue;
                if (!cue.CanPlayRuntime())
                {
                    em.SetComponentEnabled<CuePlayableTag>(cueEntity, false);
                    continue;
                }

                em.SetComponentEnabled<CuePlayingTag>(cueEntity, true);
                cue.OnActivate(now);
            }
        }

        private static void ProcessTicks(
            EntityManager em,
            NativeArray<Entity> cueEntities,
            float now)
        {
            for (var i = 0; i < cueEntities.Length; i++)
            {
                var cueEntity = cueEntities[i];
                if (!em.IsComponentEnabled<CuePlayingTag>(cueEntity))
                    continue;

                em.GetComponentData<CueManagedInstanceComponent>(cueEntity).Cue.OnTick(now);
            }
        }

        private static void ProcessEnds(
            EntityManager em,
            NativeArray<Entity> cueEntities,
            float now)
        {
            for (var i = 0; i < cueEntities.Length; i++)
            {
                var cueEntity = cueEntities[i];
                if (!em.IsComponentEnabled<CuePlayingTag>(cueEntity)
                    || em.IsComponentEnabled<CuePlayableTag>(cueEntity))
                {
                    continue;
                }

                em.SetComponentEnabled<CuePlayingTag>(cueEntity, false);
                em.GetComponentData<CueManagedInstanceComponent>(cueEntity).Cue.OnDeactivate(now);
            }
        }

        private static void ProcessDestroys(
            EntityManager em,
            NativeArray<Entity> cueEntities,
            float now)
        {
            for (var i = 0; i < cueEntities.Length; i++)
            {
                var cueEntity = cueEntities[i];
                if (!em.IsComponentEnabled<CueKillRequestTag>(cueEntity))
                    continue;

                em.GetComponentData<CueManagedInstanceComponent>(cueEntity).Cue.OnDestroy(now);
                GASRuntimeEntityArchetypes.DeactivateCueEntity(em, cueEntity);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
