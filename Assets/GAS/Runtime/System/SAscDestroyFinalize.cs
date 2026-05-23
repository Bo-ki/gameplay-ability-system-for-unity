using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCueGroup))]
    [UpdateAfter(typeof(SCueDestroy))]
    public partial struct SAscDestroyFinalize : ISystem
    {
        private EntityQuery _ascQuery;
        private EntityQuery _effectQuery;
        private EntityQuery _abilityQuery;

        public void OnCreate(ref SystemState state)
        {
            _ascQuery = SystemAPI.QueryBuilder()
                .WithAll<CAscDestroying>()
                .Build();
            _effectQuery = SystemAPI.QueryBuilder()
                .WithAll<CEffectContext>()
                .Build();
            _abilityQuery = SystemAPI.QueryBuilder()
                .WithAll<CAbilityBaseInfo>()
                .Build();
            state.RequireForUpdate(_ascQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ascEntities = _ascQuery.ToEntityArray(Allocator.Temp);
            var effects = default(NativeArray<Entity>);
            var abilities = default(NativeArray<Entity>);

            for (var i = 0; i < ascEntities.Length; i++)
            {
                var asc = ascEntities[i];
                if (!em.Exists(asc))
                    continue;

                if (HasOwnedTargetEffect(em, asc)
                    || HasReferencingEffect(em, asc, _effectQuery, ref effects)
                    || HasOwnedAbility(em, asc, _abilityQuery, ref abilities))
                {
                    continue;
                }

                EntityHelper.UnbindGameObjectToEntity(asc);
                em.DestroyEntity(asc);
            }

            if (abilities.IsCreated)
                abilities.Dispose();
            if (effects.IsCreated)
                effects.Dispose();
            ascEntities.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static bool HasOwnedTargetEffect(EntityManager em, Entity asc)
        {
            if (!em.HasBuffer<BGameplayEffect>(asc))
                return false;

            var activeEffects = em.GetBuffer<BGameplayEffect>(asc);
            for (var i = 0; i < activeEffects.Length; i++)
            {
                var effect = activeEffects[i].GameplayEffect;
                if (effect != Entity.Null && em.Exists(effect))
                    return true;
            }

            return false;
        }

        private static bool HasReferencingEffect(
            EntityManager em,
            Entity asc,
            EntityQuery effectQuery,
            ref NativeArray<Entity> effects)
        {
            if (!effects.IsCreated)
                effects = effectQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (!em.Exists(effect) || !em.HasComponent<CEffectContext>(effect))
                    continue;

                var context = em.GetComponentData<CEffectContext>(effect);
                if (context.SourceAsc == asc || context.TargetAsc == asc)
                    return true;
            }

            return false;
        }

        private static bool HasOwnedAbility(
            EntityManager em,
            Entity asc,
            EntityQuery abilityQuery,
            ref NativeArray<Entity> abilities)
        {
            if (em.HasBuffer<BGrantedAbility>(asc))
            {
                var grantedAbilities = em.GetBuffer<BGrantedAbility>(asc);
                for (var i = 0; i < grantedAbilities.Length; i++)
                {
                    var ability = grantedAbilities[i].AbilityEntity;
                    if (ability != Entity.Null && em.Exists(ability))
                        return true;
                }
            }

            if (!abilities.IsCreated)
                abilities = abilityQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < abilities.Length; i++)
            {
                var ability = abilities[i];
                if (!em.Exists(ability) || !em.HasComponent<CAbilityBaseInfo>(ability))
                    continue;

                if (em.GetComponentData<CAbilityBaseInfo>(ability).Owner == asc)
                    return true;
            }

            return false;
        }
    }
}
