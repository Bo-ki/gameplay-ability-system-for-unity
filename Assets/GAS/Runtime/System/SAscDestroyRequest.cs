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

        public void OnCreate(ref SystemState state)
        {
            _requestQuery = SystemAPI.QueryBuilder()
                .WithAll<CAscDestroyRequest>()
                .Build();
            _effectQuery = SystemAPI.QueryBuilder()
                .WithAll<CEffectContext>()
                .WithNone<CEffectDestroy>()
                .WithNone<CEffectCleanup>()
                .WithNone<CEffectFinalDestroy>()
                .Build();
            state.RequireForUpdate(_requestQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var requests = _requestQuery.ToEntityArray(Allocator.Temp);
            var effects = default(NativeArray<Entity>);
            var currentFrame = SystemAPI.TryGetSingleton<GlobalTimer>(out var timer)
                ? timer.Frame
                : GASRuntimeFrameContext.ResolveCurrentFrame(em);

            for (var i = 0; i < requests.Length; i++)
            {
                var requestEntity = requests[i];
                var request = em.GetComponentData<CAscDestroyRequest>(requestEntity);
                if (request.ASC != Entity.Null && em.Exists(request.ASC))
                    ProcessDestroyRequest(em, request.ASC, _effectQuery, ref effects, currentFrame);

                em.DestroyEntity(requestEntity);
            }

            if (effects.IsCreated)
                effects.Dispose();
            requests.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void ProcessDestroyRequest(
            EntityManager em,
            Entity asc,
            EntityQuery effectQuery,
            ref NativeArray<Entity> effects,
            int currentFrame)
        {
            if (!em.HasComponent<CAscDestroying>(asc))
                em.AddComponent<CAscDestroying>(asc);

            MarkOwnedEffectsForDestroy(em, asc, currentFrame);
            MarkReferencingEffectsForDestroy(em, asc, effectQuery, ref effects, currentFrame);
            DestroyOwnedAbilities(em, asc);
        }

        private static void MarkOwnedEffectsForDestroy(EntityManager em, Entity asc, int currentFrame)
        {
            if (!em.HasBuffer<BGameplayEffect>(asc))
                return;

            var activeEffects = em.GetBuffer<BGameplayEffect>(asc);
            if (activeEffects.Length == 0)
                return;

            var effectSnapshot = new NativeArray<Entity>(activeEffects.Length, Allocator.Temp);
            try
            {
                for (var i = 0; i < activeEffects.Length; i++)
                    effectSnapshot[i] = activeEffects[i].GameplayEffect;

                for (var i = 0; i < effectSnapshot.Length; i++)
                    MarkEffectForDestroy(em, effectSnapshot[i], currentFrame);
            }
            finally
            {
                effectSnapshot.Dispose();
            }
        }

        private static void MarkReferencingEffectsForDestroy(
            EntityManager em,
            Entity asc,
            EntityQuery effectQuery,
            ref NativeArray<Entity> effects,
            int currentFrame)
        {
            if (!effects.IsCreated)
                effects = effectQuery.ToEntityArray(Allocator.Temp);

            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                if (!em.Exists(effect) || !em.HasComponent<CEffectContext>(effect))
                    continue;

                var context = em.GetComponentData<CEffectContext>(effect);
                if (context.SourceAsc != asc && context.TargetAsc != asc)
                    continue;

                MarkEffectForDestroy(em, effect, currentFrame);
            }
        }

        private static void MarkEffectForDestroy(EntityManager em, Entity effect, int currentFrame)
        {
            if (effect == Entity.Null
                || !em.Exists(effect)
                || em.HasComponent<CEffectCleanup>(effect)
                || em.HasComponent<CEffectDestroy>(effect)
                || em.HasComponent<CEffectFinalDestroy>(effect))
            {
                return;
            }

            var cleanupState = ResolveCleanupState(em, effect);
            SetPendingRemoveLifecycle(em, effect, currentFrame);
            if (em.HasComponent<CEffectContext>(effect) && em.HasComponent<CDurationRuntime>(effect))
            {
                var context = em.GetComponentData<CEffectContext>(effect);
                var duration = em.GetComponentData<CDurationRuntime>(effect);
                ActiveEffectStore.TryUpsertDurationEffect(
                    em,
                    effect,
                    context,
                    duration,
                    EActiveEffectSlotState.PendingRemove,
                    currentFrame);
                em.AddComponentData(effect, new CEffectCleanup
                {
                    RequestedFrame = currentFrame,
                    CleanupState = cleanupState,
                    RequestedCleanupWorkFlags = ActiveEffectStore.CreateCleanupWorkFlags(
                        em,
                        effect,
                        context,
                        cleanupState),
                    SourceAsc = context.SourceAsc,
                    TargetAsc = context.TargetAsc,
                });
            }
            else
            {
                em.AddComponentData(effect, new CEffectCleanup
                {
                    RequestedFrame = currentFrame,
                    CleanupState = cleanupState,
                    RequestedCleanupWorkFlags = em.Exists(effect)
                        ? (int)EActiveEffectCleanupWorkFlags.EntityDestroy
                        : (int)EActiveEffectCleanupWorkFlags.None,
                });
            }

            em.AddComponent<CEffectDestroy>(effect);
        }

        private static EGameplayEffectLifecycleState ResolveCleanupState(EntityManager em, Entity effect)
        {
            if (em.HasComponent<CEffectLifecycle>(effect))
            {
                var lifecycle = em.GetComponentData<CEffectLifecycle>(effect);
                return lifecycle.State == EGameplayEffectLifecycleState.PendingRemove
                    ? lifecycle.PreviousState
                    : lifecycle.State;
            }

            return em.HasComponent<CDurationRuntime>(effect) && em.GetComponentData<CDurationRuntime>(effect).Active
                ? EGameplayEffectLifecycleState.Active
                : EGameplayEffectLifecycleState.PendingApply;
        }

        private static void SetPendingRemoveLifecycle(EntityManager em, Entity effect, int currentFrame)
        {
            if (!em.HasComponent<CEffectLifecycle>(effect))
            {
                em.AddComponentData(effect, new CEffectLifecycle
                {
                    State = EGameplayEffectLifecycleState.PendingRemove,
                    PreviousState = EGameplayEffectLifecycleState.PendingRemove,
                    StateStartFrame = currentFrame,
                });
                return;
            }

            var lifecycle = em.GetComponentData<CEffectLifecycle>(effect);
            if (lifecycle.State == EGameplayEffectLifecycleState.PendingRemove)
                return;

            lifecycle.PreviousState = lifecycle.State;
            lifecycle.State = EGameplayEffectLifecycleState.PendingRemove;
            lifecycle.StateStartFrame = currentFrame;
            em.SetComponentData(effect, lifecycle);
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
