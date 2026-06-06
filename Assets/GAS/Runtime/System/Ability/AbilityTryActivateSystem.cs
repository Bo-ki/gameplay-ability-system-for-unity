using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Converts try-activate markers into explicit commit requests.
    /// The gameplay commit gate lives in AbilityCommitSystem.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateBefore(typeof(AbilityCommitSystem))]
    [BurstCompile]
    public partial struct AbilityTryActivateSystem : ISystem
    {
        private EntityQuery _activationPendingQuery;
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _activationPendingQuery = SystemAPI.QueryBuilder()
                .WithAll<
                    AbilityActivationPendingComponent>()
                .Build();

            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<AbilityActivationPendingComponent>(),
                    ComponentType.ReadWrite<AbilityCommitRequestComponent>(),
                    ComponentType.ReadOnly<AbilityMainTargetComponent>(),
                    ComponentType.ReadOnly<AbilityStateComponent>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
            });
            state.RequireForUpdate(_activationPendingQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new AbilityTryActivateJob
            {
                ActivationPendingTypeHandle =
                    SystemAPI.GetComponentTypeHandle<AbilityActivationPendingComponent>(),
                CommitRequestTypeHandle =
                    SystemAPI.GetComponentTypeHandle<AbilityCommitRequestComponent>(),
                MainTargetTypeHandle =
                    SystemAPI.GetComponentTypeHandle<AbilityMainTargetComponent>(isReadOnly: true),
            }.ScheduleParallel(_query, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private struct AbilityTryActivateJob : IJobChunk
        {
            public ComponentTypeHandle<AbilityActivationPendingComponent> ActivationPendingTypeHandle;
            public ComponentTypeHandle<AbilityCommitRequestComponent> CommitRequestTypeHandle;
            [ReadOnly] public ComponentTypeHandle<AbilityMainTargetComponent> MainTargetTypeHandle;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var activationPendingMask = chunk.GetEnabledMask(ref ActivationPendingTypeHandle);
                var commitRequestMask = chunk.GetEnabledMask(ref CommitRequestTypeHandle);
                var commitRequests = chunk.GetNativeArray(ref CommitRequestTypeHandle);
                var targets = chunk.GetNativeArray(ref MainTargetTypeHandle);

                for (var entityIndex = 0; entityIndex < chunk.Count; entityIndex++)
                {
                    if (!activationPendingMask.GetBit(entityIndex))
                        continue;

                    var commitRequest = commitRequests[entityIndex];
                    commitRequest.TargetAsc = targets[entityIndex].TargetAsc;
                    commitRequests[entityIndex] = commitRequest;
                    commitRequestMask[entityIndex] = true;
                    activationPendingMask[entityIndex] = false;
                }
            }
        }
    }
}
