using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Normalizes phase-driven lifecycle transitions into explicit End/Cancel requests.
    /// Cleanup only consumes lifecycle request components.
    /// </summary>
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SAbilityTick))]
    [UpdateBefore(typeof(SAbilityStateCleanup))]
    [DisableAutoCreation]
    public partial struct SAbilityLifecycleRequest : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CAbilityActive, CAbilityBaseInfo, CAbilityRuntimeState>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var abilities = _query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i];
                if (!em.Exists(ability))
                    continue;

                var runtime = em.GetComponentData<CAbilityRuntimeState>(ability);
                if (runtime.Phase != EAbilityPhase.Ending)
                    continue;

                if (em.HasComponent<CAbilityInTryCancel>(ability)
                    || em.HasComponent<CAbilityInTryEnd>(ability))
                {
                    continue;
                }

                var baseInfo = em.GetComponentData<CAbilityBaseInfo>(ability);
                AbilityRuntimeActions.RequestAbilityEnd(
                    ability,
                    em,
                    EAbilityLifecycleReason.LifetimeExpired,
                    sourceAbility: ability,
                    sourceAbilityCode: baseInfo.Code);
            }

            abilities.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
