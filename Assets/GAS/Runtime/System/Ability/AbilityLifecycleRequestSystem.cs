using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace GAS.Runtime
{
    /// <summary>
    /// Normalizes phase-driven lifecycle transitions into explicit End/Cancel requests.
    /// Cleanup only consumes lifecycle request components.
    /// </summary>
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(AbilityStateTickSystem))]
    [UpdateBefore(typeof(AbilityStateCleanupSystem))]
    [DisableAutoCreation]
    [BurstCompile]
    public partial struct AbilityLifecycleRequestSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<AbilityStateComponent>(),
                    ComponentType.ReadWrite<AbilityCancelRequestComponent>(),
                    ComponentType.ReadWrite<AbilityEndRequestComponent>(),
                    ComponentType.ReadWrite<AbilityDestroyOnCleanupComponent>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
            });
            state.RequireForUpdate<GameplayEventBusComponent>();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var em = state.EntityManager;
            var bufferedRequestCount = eventBusEntity != Entity.Null
                                      && em.HasBuffer<AbilityLifecycleRequestBuffer>(eventBusEntity)
                ? em.GetBuffer<AbilityLifecycleRequestBuffer>(eventBusEntity).Length
                : 0;
            var requestRecords = new NativeList<AbilityLifecycleRequestRecord>(
                math.max(1, bufferedRequestCount + _query.CalculateEntityCountWithoutFiltering()),
                Allocator.TempJob);

            var collectDependency = state.Dependency;
            if (bufferedRequestCount > 0)
            {
                collectDependency = new AbilityLifecycleBufferedRequestCollectJob
                {
                    RequestLookup = SystemAPI.GetBufferLookup<AbilityLifecycleRequestBuffer>(isReadOnly: true),
                    RequestRecords = requestRecords,
                    EventBusEntity = eventBusEntity,
                }.Schedule(collectDependency);
            }

            state.Dependency = new AbilityLifecycleRequestJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                StateTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityStateComponent>(isReadOnly: true),
                CancelRequestTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityCancelRequestComponent>(),
                EndRequestTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityEndRequestComponent>(),
                DestroyOnCleanupTypeHandle =
                    SystemAPI.GetComponentTypeHandle<AbilityDestroyOnCleanupComponent>(),
                RequestRecords = requestRecords,
            }.Schedule(_query, collectDependency);
            state.Dependency = requestRecords.Dispose(state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private struct AbilityLifecycleBufferedRequestCollectJob : IJob
        {
            [ReadOnly] public BufferLookup<AbilityLifecycleRequestBuffer> RequestLookup;
            public NativeList<AbilityLifecycleRequestRecord> RequestRecords;
            public Entity EventBusEntity;

            public void Execute()
            {
                if (EventBusEntity == Entity.Null || !RequestLookup.HasBuffer(EventBusEntity))
                    return;

                var requests = RequestLookup[EventBusEntity];
                for (var i = 0; i < requests.Length; i++)
                {
                    var request = requests[i];
                    if (request.Ability == Entity.Null)
                        continue;

                    RequestRecords.Add(new AbilityLifecycleRequestRecord
                    {
                        Sequence = request.Sequence,
                        RequestKind = request.RequestKind,
                        Reason = request.Reason,
                        Ability = request.Ability,
                        SourceAbility = request.SourceAbility,
                        SourceEffect = request.SourceEffect,
                        SourceAbilityCode = request.SourceAbilityCode,
                        DestroyOnCleanup = request.DestroyOnCleanup,
                    });
                }
            }
        }

        [BurstCompile]
        private struct AbilityLifecycleRequestJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            [ReadOnly] public ComponentTypeHandle<AbilityStateComponent> StateTypeHandle;
            public ComponentTypeHandle<AbilityCancelRequestComponent> CancelRequestTypeHandle;
            public ComponentTypeHandle<AbilityEndRequestComponent> EndRequestTypeHandle;
            public ComponentTypeHandle<AbilityDestroyOnCleanupComponent> DestroyOnCleanupTypeHandle;
            public NativeList<AbilityLifecycleRequestRecord> RequestRecords;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var abilities = chunk.GetNativeArray(EntityTypeHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);
                var cancelRequests = chunk.GetNativeArray(ref CancelRequestTypeHandle);
                var endRequests = chunk.GetNativeArray(ref EndRequestTypeHandle);
                var cancelRequestMask = chunk.GetEnabledMask(ref CancelRequestTypeHandle);
                var endRequestMask = chunk.GetEnabledMask(ref EndRequestTypeHandle);
                var destroyOnCleanupMask = chunk.GetEnabledMask(ref DestroyOnCleanupTypeHandle);

                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var ability = abilities[entityIndex];
                    var runtime = states[entityIndex];

                    if (!cancelRequestMask[entityIndex]
                        && !endRequestMask[entityIndex]
                        && TryResolveBufferedRequest(
                            ability,
                            runtime,
                            RequestRecords,
                            out var bufferedRequest))
                    {
                        if (bufferedRequest.RequestKind == EAbilityLifecycleRequestKind.Cancel)
                        {
                            cancelRequests[entityIndex] = new AbilityCancelRequestComponent
                            {
                                Reason = bufferedRequest.Reason,
                                SourceAbility = bufferedRequest.SourceAbility,
                                SourceEffect = bufferedRequest.SourceEffect,
                                SourceAbilityCode = ResolveSourceAbilityCode(
                                    ability,
                                    runtime,
                                    bufferedRequest.SourceAbility,
                                    bufferedRequest.SourceAbilityCode),
                            };
                            cancelRequestMask[entityIndex] = true;
                        }
                        else
                        {
                            endRequests[entityIndex] = new AbilityEndRequestComponent
                            {
                                Reason = bufferedRequest.Reason,
                                SourceAbility = bufferedRequest.SourceAbility,
                                SourceEffect = bufferedRequest.SourceEffect,
                                SourceAbilityCode = ResolveSourceAbilityCode(
                                    ability,
                                    runtime,
                                    bufferedRequest.SourceAbility,
                                    bufferedRequest.SourceAbilityCode),
                            };
                            endRequestMask[entityIndex] = true;
                        }

                        if (bufferedRequest.DestroyOnCleanup != 0)
                            destroyOnCleanupMask[entityIndex] = true;
                    }

                    if (runtime.Phase != EAbilityPhase.Ending
                        || cancelRequestMask[entityIndex]
                        || endRequestMask[entityIndex])
                    {
                        continue;
                    }

                    endRequests[entityIndex] = new AbilityEndRequestComponent
                    {
                        Reason = EAbilityLifecycleReason.LifetimeExpired,
                        SourceAbility = ability,
                        SourceAbilityCode = runtime.Code,
                    };
                    endRequestMask[entityIndex] = true;
                }
            }

            private static bool TryResolveBufferedRequest(
                Entity ability,
                in AbilityStateComponent runtime,
                NativeList<AbilityLifecycleRequestRecord> requests,
                out AbilityLifecycleRequestRecord resolved)
            {
                resolved = default;
                var bestPriority = 0;
                var bestSequence = int.MaxValue;
                for (var i = 0; i < requests.Length; i++)
                {
                    var request = requests[i];
                    if (request.Ability != ability
                        || !CanApplyBufferedRequest(runtime.Phase, request.RequestKind))
                    {
                        continue;
                    }

                    var priority = request.RequestKind == EAbilityLifecycleRequestKind.Cancel ? 2 : 1;
                    if (priority < bestPriority)
                        continue;
                    if (priority == bestPriority && request.Sequence >= bestSequence)
                        continue;

                    resolved = request;
                    bestPriority = priority;
                    bestSequence = request.Sequence;
                }

                return bestPriority != 0;
            }

            private static bool CanApplyBufferedRequest(
                EAbilityPhase phase,
                EAbilityLifecycleRequestKind requestKind)
            {
                if (requestKind == EAbilityLifecycleRequestKind.Cancel)
                    return phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending;

                return phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending;
            }

            private static int ResolveSourceAbilityCode(
                Entity ability,
                in AbilityStateComponent runtime,
                Entity sourceAbility,
                int sourceAbilityCode)
            {
                if (sourceAbilityCode != 0)
                    return sourceAbilityCode;

                return sourceAbility == ability ? runtime.Code : 0;
            }
        }

        private struct AbilityLifecycleRequestRecord
        {
            public int Sequence;
            public EAbilityLifecycleRequestKind RequestKind;
            public EAbilityLifecycleReason Reason;
            public Entity Ability;
            public Entity SourceAbility;
            public Entity SourceEffect;
            public int SourceAbilityCode;
            public byte DestroyOnCleanup;
        }
    }
}
