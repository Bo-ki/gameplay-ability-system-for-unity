using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateBefore(typeof(STryActivateAbility))]
    public partial struct SAbilityCommandRequest : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<CAbilityCommandRequest>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var requests = _query.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < requests.Length; i++)
            {
                var requestEntity = requests[i];
                var request = em.GetComponentData<CAbilityCommandRequest>(requestEntity);
                if (request.CommandType == EAbilityCommandType.Grant
                    && request.Owner != Entity.Null
                    && em.Exists(request.Owner))
                    ProcessGrantRequest(em, request);
            }

            for (var i = 0; i < requests.Length; i++)
            {
                var requestEntity = requests[i];
                var request = em.GetComponentData<CAbilityCommandRequest>(requestEntity);
                if (request.Owner != Entity.Null && em.Exists(request.Owner))
                    ProcessRuntimeRequest(em, request);

                em.DestroyEntity(requestEntity);
            }

            requests.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ProcessGrantRequest(EntityManager em, CAbilityCommandRequest request)
        {
            var abilityEntity = request.AbilityEntity;
            if ((abilityEntity == Entity.Null || !em.Exists(abilityEntity)) && request.AbilityCode > 0)
                abilityEntity = CreateAbilityEntity(request.AbilityCode);

            if (abilityEntity == Entity.Null || !em.Exists(abilityEntity))
                return;
            if (em.HasComponent<CAscDestroying>(request.Owner))
                return;

            if (em.HasComponent<CAbilityBaseInfo>(abilityEntity))
            {
                var info = em.GetComponentData<CAbilityBaseInfo>(abilityEntity);
                info.Owner = request.Owner;
                em.SetComponentData(abilityEntity, info);
            }

            if (!em.HasBuffer<BGrantedAbility>(request.Owner))
                em.AddBuffer<BGrantedAbility>(request.Owner);

            em.GetBuffer<BGrantedAbility>(request.Owner).Add(new BGrantedAbility
            {
                AbilityEntity = abilityEntity,
            });
        }

        private static Entity CreateAbilityEntity(int abilityCode)
        {
            var abilityConfig = AbilityConfigRegistry.GetConfigByID(
                abilityCode,
                new ConfigRegistryReferenceContext(
                    ConfigRegistryConfigKind.None,
                    0,
                    ConfigRegistryReferenceKind.AbilityCommandGrant));
            return abilityConfig != null
                ? AbilityEntityFactory.CreateAbilityEntity(abilityConfig)
                : Entity.Null;
        }

        private static void ProcessRuntimeRequest(EntityManager em, CAbilityCommandRequest request)
        {
            if (request.CommandType == EAbilityCommandType.Grant)
                return;
            if (em.HasComponent<CAscDestroying>(request.Owner))
                return;

            var ability = FindAbility(em, request.Owner, request.AbilityCode);
            if (ability == Entity.Null)
                return;

            switch (request.CommandType)
            {
                case EAbilityCommandType.Activate:
                    SetMainTarget(em, ability, request.TargetAsc);
                    AddMarker<CAbilityInTryActivate>(em, ability);
                    break;
                case EAbilityCommandType.End:
                    AbilityRuntimeActions.RequestAbilityEnd(
                        ability,
                        em,
                        EAbilityLifecycleReason.ExplicitEnd);
                    break;
                case EAbilityCommandType.Cancel:
                    AbilityRuntimeActions.RequestAbilityCancel(
                        ability,
                        em,
                        EAbilityLifecycleReason.ExplicitCancel);
                    break;
                case EAbilityCommandType.Remove:
                    RemoveAbility(em, request.Owner, ability);
                    break;
            }
        }

        private static Entity FindAbility(EntityManager em, Entity owner, int abilityCode)
        {
            if (abilityCode <= 0 || !em.HasBuffer<BGrantedAbility>(owner))
                return Entity.Null;

            var abilities = em.GetBuffer<BGrantedAbility>(owner);
            for (var i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i].AbilityEntity;
                if (!em.Exists(ability) || !em.HasComponent<CAbilityConfig>(ability))
                    continue;

                var config = em.GetComponentData<CAbilityConfig>(ability).Config;
                if (config.IsCreated && config.Value.Code == abilityCode)
                    return ability;
            }

            return Entity.Null;
        }

        private static void SetMainTarget(EntityManager em, Entity ability, Entity targetAsc)
        {
            if (targetAsc == Entity.Null || !em.Exists(targetAsc) || em.HasComponent<CAscDestroying>(targetAsc))
            {
                if (em.HasComponent<CAbilityMainTarget>(ability))
                    em.RemoveComponent<CAbilityMainTarget>(ability);
                return;
            }

            var target = new CAbilityMainTarget
            {
                TargetAsc = targetAsc,
            };

            if (em.HasComponent<CAbilityMainTarget>(ability))
                em.SetComponentData(ability, target);
            else
                em.AddComponentData(ability, target);
        }

        private static void RemoveAbility(EntityManager em, Entity owner, Entity ability)
        {
            RemoveAbilityFromOwner(em, owner, ability);

            if (IsAbilityRunning(em, ability))
            {
                AbilityRuntimeActions.RequestAbilityCancel(
                    ability,
                    em,
                    EAbilityLifecycleReason.RemoveAbility);
                AddMarker<CAbilityDestroyOnCleanup>(em, ability);
                return;
            }

            DestroyAbilityEntity(em, ability);
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
