using Unity.Burst;
using Unity.Collections;
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
    [BurstCompile]
    public partial struct AbilityLifecycleRequestSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<AbilityStateComponent>()
                .Build();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new AbilityLifecycleRequestJob
            {
                CancelRequestLookup = SystemAPI.GetComponentLookup<AbilityCancelRequestComponent>(isReadOnly: true),
                EndRequestLookup = SystemAPI.GetComponentLookup<AbilityEndRequestComponent>(),
            }.Schedule(_query, state.Dependency);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private partial struct AbilityLifecycleRequestJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<AbilityCancelRequestComponent> CancelRequestLookup;
            public ComponentLookup<AbilityEndRequestComponent> EndRequestLookup;

            private void Execute(Entity ability, in AbilityStateComponent runtime)
            {
                if (runtime.Phase != EAbilityPhase.Ending)
                    return;

                if ((CancelRequestLookup.HasComponent(ability)
                        && CancelRequestLookup.IsComponentEnabled(ability))
                    || !EndRequestLookup.HasComponent(ability)
                    || EndRequestLookup.IsComponentEnabled(ability))
                {
                    return;
                }

                EndRequestLookup[ability] = new AbilityEndRequestComponent
                {
                    Reason = EAbilityLifecycleReason.LifetimeExpired,
                    SourceAbility = ability,
                    SourceAbilityCode = runtime.Code,
                };
                EndRequestLookup.SetComponentEnabled(ability, true);
            }
        }
    }
}
