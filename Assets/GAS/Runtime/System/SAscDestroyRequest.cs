using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    public partial struct SAscDestroyRequest : ISystem
    {
        private EntityQuery _requestQuery;

        public void OnCreate(ref SystemState state)
        {
            _requestQuery = SystemAPI.QueryBuilder()
                .WithAll<CAscDestroyRequest>()
                .Build();
            state.RequireForUpdate(_requestQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var requests = _requestQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                var requestEntity = requests[i];
                var request = em.GetComponentData<CAscDestroyRequest>(requestEntity);
                if (request.ASC != Entity.Null && em.Exists(request.ASC))
                    ProcessDestroyRequest(em, request.ASC);

                em.DestroyEntity(requestEntity);
            }

            requests.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ProcessDestroyRequest(EntityManager em, Entity asc)
        {
            if (!em.HasComponent<CAscDestroying>(asc))
                em.AddComponent<CAscDestroying>(asc);

            DestroyOwnedAbilities(em, asc);
        }

        private static void DestroyOwnedAbilities(EntityManager em, Entity asc)
        {
            if (!em.HasBuffer<BGrantedAbility>(asc))
                return;

            var grantedAbilities = em.GetBuffer<BGrantedAbility>(asc);
            if (grantedAbilities.Length == 0)
                return;

            var abilitySnapshot = new NativeArray<Entity>(grantedAbilities.Length, Allocator.Temp);
            try
            {
                for (var i = 0; i < grantedAbilities.Length; i++)
                    abilitySnapshot[i] = grantedAbilities[i].AbilityEntity;

                for (var i = 0; i < abilitySnapshot.Length; i++)
                {
                    var ability = abilitySnapshot[i];
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
            finally
            {
                abilitySnapshot.Dispose();
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
