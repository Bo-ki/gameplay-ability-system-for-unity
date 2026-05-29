using Unity.Entities;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(AbilityStateTickSystem))]
    [DisableAutoCreation]
    public partial struct AbilityStateCleanupSystem : ISystem
    {
        private EntityQuery _cleanupQuery;

        public void OnCreate(ref SystemState state)
        {
            _cleanupQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<AbilityStateComponent>(),
                },
                Any = new[]
                {
                    ComponentType.ReadOnly<AbilityCancelRequestComponent>(),
                    ComponentType.ReadOnly<AbilityEndRequestComponent>(),
                },
            });
            state.RequireForUpdate(_cleanupQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var abilityChunkCount = _cleanupQuery.CalculateChunkCount();
            if (abilityChunkCount <= 0)
                return;

            var em = state.EntityManager;
            var ecb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var gameplayEventWriter = EventBusHelper.BeginGameplayEventBatch(em, GASManager.EntityEventBus);

            try
            {
                foreach (var (runtime, cancelRequest, ability) in SystemAPI
                             .Query<RefRO<AbilityStateComponent>, RefRO<AbilityCancelRequestComponent>>()
                             .WithEntityAccess())
                {
                    CleanupAbility(
                        em,
                        ref ecb,
                        ref gameplayEventWriter,
                        new AbilityStateCleanupRecord
                        {
                            Ability = ability,
                            State = runtime.ValueRO,
                            ShouldCancel = true,
                            CancelRequest = cancelRequest.ValueRO,
                        });
                }

                foreach (var (runtime, endRequest, ability) in SystemAPI
                             .Query<RefRO<AbilityStateComponent>, RefRO<AbilityEndRequestComponent>>()
                             .WithEntityAccess())
                {
                    if (!em.IsComponentEnabled<AbilityEndRequestComponent>(ability))
                        continue;

                    CleanupAbility(
                        em,
                        ref ecb,
                        ref gameplayEventWriter,
                        new AbilityStateCleanupRecord
                        {
                            Ability = ability,
                            State = runtime.ValueRO,
                            ShouldEnd = true,
                            EndRequest = endRequest.ValueRO,
                        });
                }
            }
            finally
            {
                gameplayEventWriter.Dispose();
            }
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        private static void CleanupAbility(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            ref EventBusHelper.GameplayEventBusWriter gameplayEventWriter,
            in AbilityStateCleanupRecord abilityRecord)
        {
            var ability = abilityRecord.Ability;
            if (!em.Exists(ability))
                return;

            if (!abilityRecord.ShouldCancel && !abilityRecord.ShouldEnd)
                return;

            var state = abilityRecord.State;
            var owner = state.Owner;
            var lifecycleRequest = ResolveLifecycleRequest(in abilityRecord);
            AbilityRuntimeActions.RemoveActivationOwnedTags(ability, em);
            var destroyOnCleanup = CleanupGrantedAbilityIfNeeded(em, ref ecb, ability, owner, abilityRecord.ShouldCancel);

            DisableComponentIfPresent<AbilityActivationPendingComponent>(em, ability);
            DisableComponentIfPresent<AbilityCommitRequestComponent>(em, ability);
            DisableComponentIfPresent<AbilityCancelRequestComponent>(em, ability);
            DisableComponentIfPresent<AbilityEndRequestComponent>(em, ability);
            if (em.HasComponent<AbilityMainTargetComponent>(ability))
                em.SetComponentData(ability, new AbilityMainTargetComponent { TargetAsc = Entity.Null });

            state.Phase = EAbilityPhase.Ready;
            state.Timer = 0f;
            state.RemainingFrame = 0;
            ecb.SetComponent(ability, state);

            gameplayEventWriter.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
            {
                Type = abilityRecord.ShouldCancel ? EGameplayEventType.AbilityCanceled : EGameplayEventType.AbilityEnded,
                SourceAsc = owner,
                TargetAsc = owner,
                SourceAbility = ability,
                GameplayEffect = lifecycleRequest.SourceEffect,
                RelatedAbility = lifecycleRequest.SourceAbility,
                EventCode = state.Code,
                ReasonCode = (int)lifecycleRequest.Reason,
                RelatedAbilityCode = lifecycleRequest.SourceAbilityCode,
                Value = lifecycleRequest.SourceAbilityCode,
            });

            if (destroyOnCleanup || AbilityRuntimeActions.IsDestroyOnCleanupEnabled(ability, em))
                DestroyAbilityEntity(em, ref ecb, ability);
        }

        private static AbilityLifecycleCleanupRequest ResolveLifecycleRequest(in AbilityStateCleanupRecord abilityRecord)
        {
            if (abilityRecord.ShouldCancel)
                return new AbilityLifecycleCleanupRequest
                {
                    Reason = abilityRecord.CancelRequest.Reason,
                    SourceAbility = abilityRecord.CancelRequest.SourceAbility,
                    SourceEffect = abilityRecord.CancelRequest.SourceEffect,
                    SourceAbilityCode = abilityRecord.CancelRequest.SourceAbilityCode,
                };

            if (abilityRecord.ShouldEnd)
                return new AbilityLifecycleCleanupRequest
                {
                    Reason = abilityRecord.EndRequest.Reason,
                    SourceAbility = abilityRecord.EndRequest.SourceAbility,
                    SourceEffect = abilityRecord.EndRequest.SourceEffect,
                    SourceAbilityCode = abilityRecord.EndRequest.SourceAbilityCode,
                };

            return default;
        }

        private struct AbilityStateCleanupRecord
        {
            public Entity Ability;
            public AbilityStateComponent State;
            public bool ShouldCancel;
            public bool ShouldEnd;
            public AbilityCancelRequestComponent CancelRequest;
            public AbilityEndRequestComponent EndRequest;
        }

        private struct AbilityLifecycleCleanupRequest
        {
            public EAbilityLifecycleReason Reason;
            public Entity SourceAbility;
            public Entity SourceEffect;
            public int SourceAbilityCode;
        }

        private static bool CleanupGrantedAbilityIfNeeded(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ability,
            Entity owner,
            bool canceled)
        {
            if (!em.HasComponent<AbilityGrantedByEffectComponent>(ability))
                return false;

            var granted = em.GetComponentData<AbilityGrantedByEffectComponent>(ability);
            var shouldRemove = granted.RemovePolicy switch
            {
                GrantedAbilityRemovePolicy.WhenEnd => !canceled,
                GrantedAbilityRemovePolicy.WhenCancel => canceled,
                GrantedAbilityRemovePolicy.WhenCancelOrEnd => true,
                GrantedAbilityRemovePolicy.SyncWithEffect => AbilityRuntimeActions.IsDestroyOnCleanupEnabled(ability, em),
                _ => false,
            };

            if (!shouldRemove)
                return false;

            RemoveAbilityFromAsc(em, owner, ability);
            ClearRuntimeGrantedAbility(em, granted.SourceEffect, ability);
            RefreshGrantedAbilityStoreState(em, granted.SourceEffect);
            AbilityRuntimeActions.EnableDestroyOnCleanup(ability, em, ref ecb);

            return true;
        }

        private static void RemoveAbilityFromAsc(EntityManager em, Entity owner, Entity ability)
        {
            if (!em.Exists(owner) || !em.HasBuffer<AbilitySlotBuffer>(owner))
                return;

            var abilities = em.GetBuffer<AbilitySlotBuffer>(owner);
            for (var i = abilities.Length - 1; i >= 0; i--)
            {
                if (abilities[i].AbilityEntity == ability)
                    abilities.RemoveAt(i);
            }
        }

        private static void ClearRuntimeGrantedAbility(EntityManager em, Entity effect, Entity ability)
        {
            if (effect == Entity.Null || !em.Exists(effect) || !em.HasBuffer<GEGrantedAbilityRuntimeBuffer>(effect))
                return;

            var runtimeAbilities = em.GetBuffer<GEGrantedAbilityRuntimeBuffer>(effect);
            for (var i = 0; i < runtimeAbilities.Length; i++)
            {
                if (runtimeAbilities[i].AbilityEntity != ability)
                    continue;

                var runtime = runtimeAbilities[i];
                runtime.AbilityEntity = Entity.Null;
                runtimeAbilities[i] = runtime;
            }
        }

        private static void RefreshGrantedAbilityStoreState(EntityManager em, Entity effect)
        {
            if (effect == Entity.Null
                || !em.Exists(effect)
                || !em.HasComponent<GEContextComponent>(effect))
            {
                return;
            }

            ActiveEffectStore.TryRefreshGrantedAbilityState(
                em,
                effect,
                em.GetComponentData<GEContextComponent>(effect));
        }

        private static void DestroyAbilityEntity(
            EntityManager em,
            ref EntityCommandBuffer ecb,
            Entity ability)
        {
            if (!em.Exists(ability))
                return;

            ecb.DestroyEntity(ability);
        }

        private static void DisableComponentIfPresent<T>(
            EntityManager em,
            Entity entity)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            if (em.HasComponent<T>(entity) && em.IsComponentEnabled<T>(entity))
                em.SetComponentEnabled<T>(entity, false);
        }
    }
}
