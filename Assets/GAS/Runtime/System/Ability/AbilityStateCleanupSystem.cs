using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
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
            var abilityChunkCount = _cleanupQuery.CalculateChunkCountWithoutFiltering();
            if (abilityChunkCount <= 0)
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var frame = SystemAPI.TryGetSingleton<GlobalTimer>(out var timer)
                ? timer.Frame
                : 0;
            var structuralEcb = SystemAPI.GetSingleton<EndGASStructuralCommitECBSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged)
                .AsParallelWriter();

            state.Dependency = new AbilityStateCleanupJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                StateTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityStateComponent>(),
                CancelRequestLookup = SystemAPI.GetComponentLookup<AbilityCancelRequestComponent>(),
                EndRequestLookup = SystemAPI.GetComponentLookup<AbilityEndRequestComponent>(),
                ActivationPendingLookup = SystemAPI.GetComponentLookup<AbilityActivationPendingComponent>(),
                CommitRequestLookup = SystemAPI.GetComponentLookup<AbilityCommitRequestComponent>(),
                DestroyOnCleanupLookup = SystemAPI.GetComponentLookup<AbilityDestroyOnCleanupComponent>(),
                MainTargetLookup = SystemAPI.GetComponentLookup<AbilityMainTargetComponent>(),
                GrantedByEffectLookup = SystemAPI.GetComponentLookup<AbilityGrantedByEffectComponent>(isReadOnly: true),
                AbilitySlotLookup = SystemAPI.GetBufferLookup<AbilitySlotBuffer>(),
                GrantedAbilityRuntimeLookup = SystemAPI.GetBufferLookup<GEGrantedAbilityRuntimeBuffer>(),
                EffectContextLookup = SystemAPI.GetComponentLookup<GEContextComponent>(isReadOnly: true),
                ActiveEffectStoreLookup = SystemAPI.GetComponentLookup<ASCActiveEffectsComponent>(),
                ActiveEffectSlotLookup = SystemAPI.GetBufferLookup<ActiveGameplayEffectBuffer>(),
                TemporaryTagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(),
                TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(),
                FixedTagMaskLookup = SystemAPI.GetComponentLookup<TagFixedMaskComponent>(isReadOnly: true),
                EventBusEntity = eventBusEntity,
                Frame = frame,
                EventBusLookup = SystemAPI.GetComponentLookup<GameplayEventBusComponent>(),
                GameplayEventLookup = SystemAPI.GetBufferLookup<GameplayEventBusEventBuffer>(),
                StructuralEcb = structuralEcb,
            }.Schedule(_cleanupQuery, state.Dependency);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        private struct AbilityStateCleanupJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<AbilityStateComponent> StateTypeHandle;
            public ComponentLookup<AbilityCancelRequestComponent> CancelRequestLookup;
            public ComponentLookup<AbilityEndRequestComponent> EndRequestLookup;
            public ComponentLookup<AbilityActivationPendingComponent> ActivationPendingLookup;
            public ComponentLookup<AbilityCommitRequestComponent> CommitRequestLookup;
            public ComponentLookup<AbilityDestroyOnCleanupComponent> DestroyOnCleanupLookup;
            public ComponentLookup<AbilityMainTargetComponent> MainTargetLookup;
            [ReadOnly] public ComponentLookup<AbilityGrantedByEffectComponent> GrantedByEffectLookup;
            public BufferLookup<AbilitySlotBuffer> AbilitySlotLookup;
            public BufferLookup<GEGrantedAbilityRuntimeBuffer> GrantedAbilityRuntimeLookup;
            [ReadOnly] public ComponentLookup<GEContextComponent> EffectContextLookup;
            public ComponentLookup<ASCActiveEffectsComponent> ActiveEffectStoreLookup;
            public BufferLookup<ActiveGameplayEffectBuffer> ActiveEffectSlotLookup;
            public BufferLookup<TagTemporarySourceBuffer> TemporaryTagSourceLookup;
            public ComponentLookup<TagMaskComponent> TagMaskLookup;
            [ReadOnly] public ComponentLookup<TagFixedMaskComponent> FixedTagMaskLookup;
            public Entity EventBusEntity;
            public int Frame;
            public ComponentLookup<GameplayEventBusComponent> EventBusLookup;
            public BufferLookup<GameplayEventBusEventBuffer> GameplayEventLookup;
            public EntityCommandBuffer.ParallelWriter StructuralEcb;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var abilities = chunk.GetNativeArray(EntityTypeHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var ability = abilities[entityIndex];
                    var state = states[entityIndex];
                    if (!TryResolveCleanupRequest(ability, out var cleanupRequest))
                        continue;

                    var owner = state.Owner;
                    RemoveActivationOwnedTags(owner, ability);
                    var destroyOnCleanup = CleanupGrantedAbilityIfNeeded(ability, owner, cleanupRequest.ShouldCancel);

                    DisableComponentIfPresent(ref ActivationPendingLookup, ability);
                    DisableComponentIfPresent(ref CommitRequestLookup, ability);
                    DisableComponentIfPresent(ref CancelRequestLookup, ability);
                    DisableComponentIfPresent(ref EndRequestLookup, ability);
                    if (MainTargetLookup.HasComponent(ability))
                        MainTargetLookup[ability] = new AbilityMainTargetComponent { TargetAsc = Entity.Null };

                    state.Phase = EAbilityPhase.Ready;
                    state.Timer = 0f;
                    state.RemainingFrame = 0;
                    states[entityIndex] = state;

                    EnqueueLifecycleEvent(ability, owner, in state, in cleanupRequest);

                    if (destroyOnCleanup || IsDestroyOnCleanupEnabled(ability))
                        StructuralEcb.DestroyEntity(unfilteredChunkIndex, ability);
                }
            }

            private bool TryResolveCleanupRequest(Entity ability, out AbilityLifecycleCleanupRequest cleanupRequest)
            {
                cleanupRequest = default;
                if (CancelRequestLookup.HasComponent(ability)
                    && CancelRequestLookup.IsComponentEnabled(ability))
                {
                    var cancelRequest = CancelRequestLookup[ability];
                    cleanupRequest = new AbilityLifecycleCleanupRequest
                    {
                        ShouldCancel = true,
                        Reason = cancelRequest.Reason,
                        SourceAbility = cancelRequest.SourceAbility,
                        SourceEffect = cancelRequest.SourceEffect,
                        SourceAbilityCode = cancelRequest.SourceAbilityCode,
                    };
                    return true;
                }

                if (EndRequestLookup.HasComponent(ability)
                    && EndRequestLookup.IsComponentEnabled(ability))
                {
                    var endRequest = EndRequestLookup[ability];
                    cleanupRequest = new AbilityLifecycleCleanupRequest
                    {
                        ShouldEnd = true,
                        Reason = endRequest.Reason,
                        SourceAbility = endRequest.SourceAbility,
                        SourceEffect = endRequest.SourceEffect,
                        SourceAbilityCode = endRequest.SourceAbilityCode,
                    };
                    return true;
                }

                return false;
            }

            private bool CleanupGrantedAbilityIfNeeded(Entity ability, Entity owner, bool canceled)
            {
                if (!GrantedByEffectLookup.HasComponent(ability))
                    return false;

                var granted = GrantedByEffectLookup[ability];
                var shouldRemove = granted.RemovePolicy switch
                {
                    GrantedAbilityRemovePolicy.WhenEnd => !canceled,
                    GrantedAbilityRemovePolicy.WhenCancel => canceled,
                    GrantedAbilityRemovePolicy.WhenCancelOrEnd => true,
                    GrantedAbilityRemovePolicy.SyncWithEffect => IsDestroyOnCleanupEnabled(ability),
                    _ => false,
                };

                if (!shouldRemove)
                    return false;

                RemoveAbilityFromAsc(owner, ability);
                ClearRuntimeGrantedAbility(granted.SourceEffect, ability);
                RefreshGrantedAbilityStoreState(granted.SourceEffect);
                EnableDestroyOnCleanup(ability);
                return true;
            }

            private void RemoveAbilityFromAsc(Entity owner, Entity ability)
            {
                if (owner == Entity.Null || !AbilitySlotLookup.HasBuffer(owner))
                    return;

                var abilities = AbilitySlotLookup[owner];
                for (var i = abilities.Length - 1; i >= 0; i--)
                    if (abilities[i].AbilityEntity == ability)
                        abilities.RemoveAt(i);
            }

            private void ClearRuntimeGrantedAbility(Entity effect, Entity ability)
            {
                if (effect == Entity.Null || !GrantedAbilityRuntimeLookup.HasBuffer(effect))
                    return;

                var runtimeAbilities = GrantedAbilityRuntimeLookup[effect];
                for (var i = 0; i < runtimeAbilities.Length; i++)
                {
                    if (runtimeAbilities[i].AbilityEntity != ability)
                        continue;

                    var runtime = runtimeAbilities[i];
                    runtime.AbilityEntity = Entity.Null;
                    runtimeAbilities[i] = runtime;
                }
            }

            private void RefreshGrantedAbilityStoreState(Entity effect)
            {
                if (effect == Entity.Null
                    || !EffectContextLookup.HasComponent(effect)
                    || !GrantedAbilityRuntimeLookup.HasBuffer(effect))
                    return;

                var context = EffectContextLookup[effect];
                var owner = context.TargetAsc;
                if (owner == Entity.Null
                    || !ActiveEffectStoreLookup.HasComponent(owner)
                    || !ActiveEffectSlotLookup.HasBuffer(owner))
                    return;

                var store = ActiveEffectStoreLookup[owner];
                var slots = ActiveEffectSlotLookup[owner];
                var slotIndex = FindSlot(slots, effect);
                if (slotIndex < 0)
                    return;

                var slot = slots[slotIndex];
                slot.ActiveGrantedTagCount = CountActiveGrantedTags(owner, effect);
                slot.ActiveGrantedAbilityCount = CountActiveGrantedAbilities(effect);
                slots[slotIndex] = slot;
                ActiveEffectStore.RefreshChunkSkipIndexCounters(
                    ref store,
                    slots,
                    store.LastChunkSkipIndexFrame);
                ActiveEffectStoreLookup[owner] = store;
            }

            private int CountActiveGrantedAbilities(Entity effect)
            {
                if (effect == Entity.Null || !GrantedAbilityRuntimeLookup.HasBuffer(effect))
                    return 0;

                var runtimeAbilities = GrantedAbilityRuntimeLookup[effect];
                var count = 0;
                for (var i = 0; i < runtimeAbilities.Length; i++)
                    if (runtimeAbilities[i].AbilityEntity != Entity.Null)
                        count++;
                return count;
            }

            private int CountActiveGrantedTags(Entity owner, Entity effect)
            {
                if (owner == Entity.Null
                    || effect == Entity.Null
                    || !TemporaryTagSourceLookup.HasBuffer(owner))
                    return 0;

                var sources = TemporaryTagSourceLookup[owner];
                var seen = new TagMaskComponent();
                var count = 0;
                for (var i = 0; i < sources.Length; i++)
                {
                    var source = sources[i];
                    if (source.Source != effect
                        || !TagMaskComponent.IsValidIndex(source.TagIndex)
                        || seen.HasTag(source.TagIndex))
                        continue;

                    seen.AddTag(source.TagIndex);
                    count++;
                }

                return count;
            }

            private void RemoveActivationOwnedTags(Entity owner, Entity ability)
            {
                if (owner == Entity.Null || !TemporaryTagSourceLookup.HasBuffer(owner))
                    return;

                var sources = TemporaryTagSourceLookup[owner];
                for (var i = sources.Length - 1; i >= 0; i--)
                {
                    if (sources[i].Source != ability)
                        continue;

                    var tagIndex = sources[i].TagIndex;
                    sources.RemoveAt(i);
                    RemoveTagIndexFromEffectiveMaskIfUnreferenced(owner, sources, tagIndex);
                }
            }

            private void RemoveTagIndexFromEffectiveMaskIfUnreferenced(
                Entity owner,
                DynamicBuffer<TagTemporarySourceBuffer> sources,
                int tagIndex)
            {
                if (!TagMaskLookup.HasComponent(owner))
                    return;
                if (FixedTagMaskLookup.HasComponent(owner)
                    && FixedTagMaskLookup[owner].Mask.HasTag(tagIndex))
                    return;
                for (var i = 0; i < sources.Length; i++)
                    if (sources[i].TagIndex == tagIndex)
                        return;

                var mask = TagMaskLookup[owner];
                mask.RemoveTag(tagIndex);
                TagMaskLookup[owner] = mask;
            }

            private void EnableDestroyOnCleanup(Entity ability)
            {
                if (DestroyOnCleanupLookup.HasComponent(ability))
                    DestroyOnCleanupLookup.SetComponentEnabled(ability, true);
            }

            private bool IsDestroyOnCleanupEnabled(Entity ability)
            {
                return DestroyOnCleanupLookup.HasComponent(ability)
                       && DestroyOnCleanupLookup.IsComponentEnabled(ability);
            }

            private void EnqueueLifecycleEvent(
                Entity ability,
                Entity owner,
                in AbilityStateComponent state,
                in AbilityLifecycleCleanupRequest cleanupRequest)
            {
                if (EventBusEntity == Entity.Null
                    || !EventBusLookup.HasComponent(EventBusEntity)
                    || !GameplayEventLookup.HasBuffer(EventBusEntity))
                    return;

                var eventBus = EventBusLookup[EventBusEntity];
                var events = GameplayEventLookup[EventBusEntity];
                events.Add(new GameplayEventBusEventBuffer
                {
                    Frame = Frame,
                    Sequence = eventBus.NextSequence,
                    Type = cleanupRequest.ShouldCancel
                        ? EGameplayEventType.AbilityCanceled
                        : EGameplayEventType.AbilityEnded,
                    SourceAsc = owner,
                    TargetAsc = owner,
                    SourceAbility = ability,
                    GameplayEffect = cleanupRequest.SourceEffect,
                    RelatedAbility = cleanupRequest.SourceAbility,
                    EventCode = state.Code,
                    ReasonCode = (int)cleanupRequest.Reason,
                    RelatedAbilityCode = cleanupRequest.SourceAbilityCode,
                    Value = cleanupRequest.SourceAbilityCode,
                });
                eventBus.NextSequence++;
                EventBusLookup[EventBusEntity] = eventBus;
            }

            private static int FindSlot(DynamicBuffer<ActiveGameplayEffectBuffer> slots, Entity effect)
            {
                for (var i = 0; i < slots.Length; i++)
                    if (slots[i].ActiveEffectEntity == effect)
                        return i;
                return -1;
            }
        }

        private struct AbilityLifecycleCleanupRequest
        {
            public bool ShouldCancel;
            public bool ShouldEnd;
            public EAbilityLifecycleReason Reason;
            public Entity SourceAbility;
            public Entity SourceEffect;
            public int SourceAbilityCode;
        }

        private static void DisableComponentIfPresent<T>(
            ref ComponentLookup<T> lookup,
            Entity entity)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            if (lookup.HasComponent(entity) && lookup.IsComponentEnabled(entity))
                lookup.SetComponentEnabled(entity, false);
        }
    }
}
