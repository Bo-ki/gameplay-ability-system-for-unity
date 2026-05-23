using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASAbilityGroup))]
    [UpdateAfter(typeof(SAbilityTick))]
    [DisableAutoCreation]
    public partial struct SAbilityStateCleanup : ISystem
    {
        private EntityQuery _cleanupQuery;

        public void OnCreate(ref SystemState state)
        {
            _cleanupQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<CAbilityBaseInfo>(),
                    ComponentType.ReadWrite<CAbilityRuntimeState>(),
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<CAbilityInTryCancel>(),
                    ComponentType.ReadOnly<CAbilityInTryEnd>(),
                },
            });
            state.RequireForUpdate(_cleanupQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var abilities = _cleanupQuery.ToEntityArray(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            using var gameplayEventBatch = EventBusHelper.BeginGameplayEventBatch(em, GASManager.EntityEventBus);

            foreach (var ability in abilities)
            {
                var baseInfo = em.GetComponentData<CAbilityBaseInfo>(ability);
                var runtime = em.GetComponentData<CAbilityRuntimeState>(ability);
                var shouldCancel = em.HasComponent<CAbilityInTryCancel>(ability);
                var shouldEnd = em.HasComponent<CAbilityInTryEnd>(ability);

                if (!shouldCancel && !shouldEnd)
                    continue;

                var owner = baseInfo.Owner;
                var lifecycleRequest = ResolveLifecycleRequest(em, ability, shouldCancel);
                CleanupAbilityCreatedEffects(em, ref ecb, ability, owner);
                AbilityRuntimeActions.RemoveActivationOwnedTags(ability, em);
                var destroyOnCleanup = CleanupGrantedAbilityIfNeeded(em, ref ecb, ability, owner, shouldCancel);

                RemoveComponentIfPresent<CAbilityActive>(em, ref ecb, ability);
                RemoveComponentIfPresent<CAbilityInTryActivate>(em, ref ecb, ability);
                RemoveComponentIfPresent<CAbilityCommitRequest>(em, ref ecb, ability);
                RemoveComponentIfPresent<CAbilityInTryCancel>(em, ref ecb, ability);
                RemoveComponentIfPresent<CAbilityInTryEnd>(em, ref ecb, ability);
                RemoveComponentIfPresent<CAbilityTimelineRuntime>(em, ref ecb, ability);
                RemoveComponentIfPresent<CAbilityMainTarget>(em, ref ecb, ability);

                runtime.Phase = EAbilityPhase.Ready;
                runtime.Timer = 0f;
                runtime.RemainingFrame = 0;
                ecb.SetComponent(ability, runtime);

                EventBusHelper.EnqueueGameplayEvent(em, GASManager.EntityEventBus, new BGameplayEvent
                {
                    Type = shouldCancel ? EGameplayEventType.AbilityCanceled : EGameplayEventType.AbilityEnded,
                    SourceAsc = owner,
                    TargetAsc = owner,
                    SourceAbility = ability,
                    GameplayEffect = lifecycleRequest.SourceEffect,
                    RelatedAbility = lifecycleRequest.SourceAbility,
                    EventCode = baseInfo.Code,
                    ReasonCode = (int)lifecycleRequest.Reason,
                    RelatedAbilityCode = lifecycleRequest.SourceAbilityCode,
                    Value = lifecycleRequest.SourceAbilityCode,
                });

                if (destroyOnCleanup || em.HasComponent<CAbilityDestroyOnCleanup>(ability))
                    DestroyAbilityEntity(em, ref ecb, ability);
            }

            ecb.Playback(em);
            ecb.Dispose();
            abilities.Dispose();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static AbilityLifecycleCleanupRequest ResolveLifecycleRequest(
            EntityManager em,
            Entity ability,
            bool shouldCancel)
        {
            if (shouldCancel)
            {
                var cancel = em.GetComponentData<CAbilityInTryCancel>(ability);
                return new AbilityLifecycleCleanupRequest
                {
                    Reason = cancel.Reason,
                    SourceAbility = cancel.SourceAbility,
                    SourceEffect = cancel.SourceEffect,
                    SourceAbilityCode = cancel.SourceAbilityCode,
                };
            }

            if (em.HasComponent<CAbilityInTryEnd>(ability))
            {
                var end = em.GetComponentData<CAbilityInTryEnd>(ability);
                return new AbilityLifecycleCleanupRequest
                {
                    Reason = end.Reason,
                    SourceAbility = end.SourceAbility,
                    SourceEffect = end.SourceEffect,
                    SourceAbilityCode = end.SourceAbilityCode,
                };
            }

            return default;
        }

        private struct AbilityLifecycleCleanupRequest
        {
            public EAbilityLifecycleReason Reason;
            public Entity SourceAbility;
            public Entity SourceEffect;
            public int SourceAbilityCode;
        }

        private static void CleanupAbilityCreatedEffects(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ability,
            Entity owner)
        {
            if (!em.Exists(owner) || !em.HasBuffer<BGameplayEffect>(owner))
                return;

            var effects = em.GetBuffer<BGameplayEffect>(owner);
            var toRemove = new List<Entity>();
            for (var i = 0; i < effects.Length; i++)
            {
                var effect = effects[i].GameplayEffect;
                if (!em.Exists(effect) || !em.HasComponent<CCreatedByAbility>(effect))
                    continue;

                if (em.GetComponentData<CCreatedByAbility>(effect).sourceAbility == ability)
                    toRemove.Add(effect);
            }

            for (var i = 0; i < toRemove.Count; i++)
                MarkEffectForDestroy(em, ref ecb, toRemove[i]);
        }

        private static void MarkEffectForDestroy(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity effect)
        {
            if (!em.Exists(effect) || !em.HasComponent<CEffectContext>(effect))
                return;

            if (!em.HasComponent<CEffectDestroy>(effect))
                ecb.AddComponent<CEffectDestroy>(effect);
        }

        private static bool CleanupGrantedAbilityIfNeeded(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ability,
            Entity owner,
            bool canceled)
        {
            if (!em.HasComponent<CGrantedByEffect>(ability))
                return false;

            var granted = em.GetComponentData<CGrantedByEffect>(ability);
            var shouldRemove = granted.RemovePolicy switch
            {
                GrantedAbilityRemovePolicy.WhenEnd => !canceled,
                GrantedAbilityRemovePolicy.WhenCancel => canceled,
                GrantedAbilityRemovePolicy.WhenCancelOrEnd => true,
                GrantedAbilityRemovePolicy.SyncWithEffect => em.HasComponent<CAbilityDestroyOnCleanup>(ability),
                _ => false,
            };

            if (!shouldRemove)
                return false;

            RemoveAbilityFromAsc(em, owner, ability);
            ClearRuntimeGrantedAbility(em, granted.SourceEffect, ability);
            if (!em.HasComponent<CAbilityDestroyOnCleanup>(ability))
                ecb.AddComponent<CAbilityDestroyOnCleanup>(ability);

            return true;
        }

        private static void RemoveAbilityFromAsc(EntityManager em, Entity owner, Entity ability)
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

        private static void ClearRuntimeGrantedAbility(EntityManager em, Entity effect, Entity ability)
        {
            if (effect == Entity.Null || !em.Exists(effect) || !em.HasBuffer<BGrantedAbilityRuntime>(effect))
                return;

            var runtimeAbilities = em.GetBuffer<BGrantedAbilityRuntime>(effect);
            for (var i = 0; i < runtimeAbilities.Length; i++)
            {
                if (runtimeAbilities[i].AbilityEntity != ability)
                    continue;

                var runtime = runtimeAbilities[i];
                runtime.AbilityEntity = Entity.Null;
                runtimeAbilities[i] = runtime;
            }
        }

        private static void DestroyAbilityEntity(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ability)
        {
            if (!em.Exists(ability))
                return;

            if (em.HasComponent<CAbilityConfig>(ability))
            {
                var config = em.GetComponentData<CAbilityConfig>(ability).Config;
                if (config.IsCreated)
                    config.Dispose();
            }

            ecb.DestroyEntity(ability);
        }

        private static void RemoveComponentIfPresent<T>(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity entity)
            where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(entity))
                ecb.RemoveComponent<T>(entity);
        }
    }
}
