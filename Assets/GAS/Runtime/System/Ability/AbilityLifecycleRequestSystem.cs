using Unity.Entities;

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
    public partial struct AbilityLifecycleRequestSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<AbilityStateComponent>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            foreach (var (abilityState, ability) in SystemAPI
                         .Query<RefRO<AbilityStateComponent>>()
                         .WithEntityAccess())
            {
                var runtime = abilityState.ValueRO;
                if (runtime.Phase != EAbilityPhase.Ending)
                    continue;

                if (AbilityRuntimeActions.IsCancelRequested(ability, em)
                    || AbilityRuntimeActions.IsEndRequested(ability, em))
                {
                    continue;
                }

                AbilityRuntimeActions.RequestAbilityEnd(
                    ability,
                    em,
                    EAbilityLifecycleReason.LifetimeExpired,
                    sourceAbility: ability,
                    sourceAbilityCode: runtime.Code);
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

    }
}
