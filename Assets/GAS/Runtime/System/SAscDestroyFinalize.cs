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
            var effects = _effectQuery.ToEntityArray(Allocator.Temp);
            var abilities = _abilityQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < ascEntities.Length; i++)
            {
                var asc = ascEntities[i];
                if (!em.Exists(asc))
                    continue;

                if (HasReferencingEffect(em, asc, effects) || HasOwnedAbility(em, asc, abilities))
                    continue;

                EntityHelper.UnbindGameObjectToEntity(asc);
                em.DestroyEntity(asc);
            }

            abilities.Dispose();
            effects.Dispose();
            ascEntities.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static bool HasReferencingEffect(EntityManager em, Entity asc, NativeArray<Entity> effects)
        {
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

        private static bool HasOwnedAbility(EntityManager em, Entity asc, NativeArray<Entity> abilities)
        {
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
