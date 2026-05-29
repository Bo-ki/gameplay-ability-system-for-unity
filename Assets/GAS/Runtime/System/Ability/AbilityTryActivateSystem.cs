using Unity.Burst;
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
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<AbilityActivationPendingComponent, AbilityStateComponent>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var commitRequests = SystemAPI.GetComponentLookup<AbilityCommitRequestComponent>();
            foreach (var (target, activationPending, ability) in SystemAPI
                         .Query<RefRO<AbilityMainTargetComponent>, EnabledRefRW<AbilityActivationPendingComponent>>()
                         .WithAll<AbilityStateComponent, AbilityActivationPendingComponent>()
                         .WithEntityAccess())
            {
                if (commitRequests.HasComponent(ability))
                {
                    commitRequests[ability] = new AbilityCommitRequestComponent
                    {
                        TargetAsc = target.ValueRO.TargetAsc,
                    };
                    if (!commitRequests.IsComponentEnabled(ability))
                        commitRequests.SetComponentEnabled(ability, true);
                }

                activationPending.ValueRW = false;
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

    }
}
