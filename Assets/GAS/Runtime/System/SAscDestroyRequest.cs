using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SRemoveGameplayEffectRequest))]
    [UpdateBefore(typeof(SApplyGameplayEffectRequest))]
    public partial struct SAscDestroyRequest : ISystem
    {
        private EntityQuery _requestQuery;
        private EntityQuery _effectQuery;
        private EntityQuery _abilityQuery;

        public void OnCreate(ref SystemState state)
        {
            _requestQuery = SystemAPI.QueryBuilder()
                .WithAll<CAscDestroyRequest>()
                .Build();
            _effectQuery = SystemAPI.QueryBuilder()
                .WithAll<CEffectContext>()
                .Build();
            _abilityQuery = SystemAPI.QueryBuilder()
                .WithAll<CAbilityBaseInfo>()
                .Build();
            state.RequireForUpdate(_requestQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var requests = _requestQuery.ToEntityArray(Allocator.Temp);
            var effects = _effectQuery.ToEntityArray(Allocator.Temp);
            var abilities = _abilityQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                var requestEntity = requests[i];
                var request = em.GetComponentData<CAscDestroyRequest>(requestEntity);
                if (request.ASC != Entity.Null && em.Exists(request.ASC))
                    ProcessDestroyRequest(em, request.ASC, effects, abilities);

                em.DestroyEntity(requestEntity);
            }

            abilities.Dispose();
            effects.Dispose();
            requests.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ProcessDestroyRequest(
            EntityManager em,
            Entity asc,
            NativeArray<Entity> effects,
            NativeArray<Entity> abilities)
        {
            if (!em.HasComponent<CAscDestroying>(asc))
                em.AddComponent<CAscDestroying>(asc);

            MarkReferencingEffectsForDestroy(em, asc, effects);
            DestroyOwnedAbilities(em, asc, abilities);
        }

        private static void MarkReferencingEffectsForDestroy(EntityManager em, Entity asc, NativeArray<Entity> effects)
        {
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (!em.Exists(effect) || !em.HasComponent<CEffectContext>(effect))
                    continue;

                var context = em.GetComponentData<CEffectContext>(effect);
                if (context.SourceAsc != asc && context.TargetAsc != asc)
                    continue;

                if (!em.HasComponent<CEffectDestroy>(effect))
                    em.AddComponent<CEffectDestroy>(effect);
            }
        }

        private static void DestroyOwnedAbilities(EntityManager em, Entity asc, NativeArray<Entity> abilities)
        {
            for (var i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i];
                if (!em.Exists(ability) || !em.HasComponent<CAbilityBaseInfo>(ability))
                    continue;

                var baseInfo = em.GetComponentData<CAbilityBaseInfo>(ability);
                if (baseInfo.Owner != asc)
                    continue;

                RemoveAbilityFromOwner(em, asc, ability);
                if (IsAbilityRunning(em, ability))
                {
                    AbilityRuntimeActions.RequestAbilityCancel(
                        ability,
                        em,
                        EAbilityLifecycleReason.AscDestroy);
                    AddMarker<CAbilityDestroyOnCleanup>(em, ability);
                }
                else
                {
                    DestroyAbilityEntity(em, ability);
                }
            }
        }

        private static bool IsAbilityRunning(EntityManager em, Entity ability)
        {
            if (em.HasComponent<CAbilityActive>(ability))
                return true;
            if (!em.HasComponent<CAbilityRuntimeState>(ability))
                return false;

            var runtime = em.GetComponentData<CAbilityRuntimeState>(ability);
            return runtime.Phase is EAbilityPhase.Activating or EAbilityPhase.Active or EAbilityPhase.Ending;
        }

        private static void RemoveAbilityFromOwner(EntityManager em, Entity owner, Entity ability)
        {
            if (!em.Exists(owner) || !em.HasBuffer<BGrantedAbility>(owner))
                return;

            var abilities = em.GetBuffer<BGrantedAbility>(owner);
            for (var i = abilities.Length - 1; i >= 0; i--)
            {
                if (abilities[i].AbilityEntity == ability)
                    abilities.RemoveAt(i);
            }
        }

        private static void DestroyAbilityEntity(EntityManager em, Entity ability)
        {
            if (!em.Exists(ability))
                return;

            if (em.HasComponent<CAbilityConfig>(ability))
            {
                var config = em.GetComponentData<CAbilityConfig>(ability).Config;
                if (config.IsCreated)
                    config.Dispose();
            }

            em.DestroyEntity(ability);
        }

        private static void AddMarker<T>(EntityManager em, Entity entity)
            where T : unmanaged, IComponentData
        {
            if (!em.HasComponent<T>(entity))
                em.AddComponent<T>(entity);
        }
    }
}
