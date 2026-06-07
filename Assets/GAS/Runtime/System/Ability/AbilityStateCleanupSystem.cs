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
                    ComponentType.ReadWrite<AbilityCancelRequestComponent>(),
                    ComponentType.ReadWrite<AbilityEndRequestComponent>(),
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
            var streamEntity = SystemAPI.TryGetSingletonEntity<GEEffectCommandStreamComponent>(out var resolvedStream)
                ? resolvedStream
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
                CancelRequestTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityCancelRequestComponent>(),
                EndRequestTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityEndRequestComponent>(),
                ActivationPendingTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityActivationPendingComponent>(),
                CommitRequestTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityCommitRequestComponent>(),
                DestroyOnCleanupTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityDestroyOnCleanupComponent>(),
                MainTargetTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityMainTargetComponent>(),
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
                StreamEntity = streamEntity,
                Frame = frame,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
                OwnerFactLookup = SystemAPI.GetBufferLookup<OwnerLocalGameplayFactBuffer>(),
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
            public ComponentTypeHandle<AbilityCancelRequestComponent> CancelRequestTypeHandle;
            public ComponentTypeHandle<AbilityEndRequestComponent> EndRequestTypeHandle;
            public ComponentTypeHandle<AbilityActivationPendingComponent> ActivationPendingTypeHandle;
            public ComponentTypeHandle<AbilityCommitRequestComponent> CommitRequestTypeHandle;
            public ComponentTypeHandle<AbilityDestroyOnCleanupComponent> DestroyOnCleanupTypeHandle;
            public ComponentTypeHandle<AbilityMainTargetComponent> MainTargetTypeHandle;
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
            public Entity StreamEntity;
            public int Frame;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<OwnerLocalGameplayFactBuffer> OwnerFactLookup;
            public EntityCommandBuffer.ParallelWriter StructuralEcb;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                var abilities = chunk.GetNativeArray(EntityTypeHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);
                var cancelRequests = chunk.GetNativeArray(ref CancelRequestTypeHandle);
                var endRequests = chunk.GetNativeArray(ref EndRequestTypeHandle);
                var mainTargets = chunk.GetNativeArray(ref MainTargetTypeHandle);
                var cancelRequestMask = chunk.GetEnabledMask(ref CancelRequestTypeHandle);
                var endRequestMask = chunk.GetEnabledMask(ref EndRequestTypeHandle);
                var activationPendingMask = chunk.GetEnabledMask(ref ActivationPendingTypeHandle);
                var commitRequestMask = chunk.GetEnabledMask(ref CommitRequestTypeHandle);
                var destroyOnCleanupMask = chunk.GetEnabledMask(ref DestroyOnCleanupTypeHandle);
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var ability = abilities[entityIndex];
                    var state = states[entityIndex];
                    if (!TryResolveCleanupRequest(
                            entityIndex,
                            cancelRequests,
                            endRequests,
                            cancelRequestMask,
                            endRequestMask,
                            out var cleanupRequest))
                    {
                        continue;
                    }

                    var owner = state.Owner;
                    RemoveActivationOwnedTags(owner, ability);
                    var destroyOnCleanupEnabled = destroyOnCleanupMask[entityIndex];
                    var destroyOnCleanup = CleanupGrantedAbilityIfNeeded(
                        ability,
                        owner,
                        cleanupRequest.ShouldCancel,
                        ref destroyOnCleanupEnabled);

                    activationPendingMask[entityIndex] = false;
                    commitRequestMask[entityIndex] = false;
                    cancelRequestMask[entityIndex] = false;
                    endRequestMask[entityIndex] = false;
                    destroyOnCleanupMask[entityIndex] = destroyOnCleanupEnabled;
                    mainTargets[entityIndex] = new AbilityMainTargetComponent { TargetAsc = Entity.Null };

                    state.Phase = EAbilityPhase.Ready;
                    state.Timer = 0f;
                    state.RemainingFrame = 0;
                    states[entityIndex] = state;

                    EnqueueLifecycleEvent(ability, owner, in state, in cleanupRequest);

                    if (destroyOnCleanup || destroyOnCleanupEnabled)
                        StructuralEcb.DestroyEntity(unfilteredChunkIndex, ability);
                }
            }

            private static bool TryResolveCleanupRequest(
                int entityIndex,
                NativeArray<AbilityCancelRequestComponent> cancelRequests,
                NativeArray<AbilityEndRequestComponent> endRequests,
                EnabledMask cancelRequestMask,
                EnabledMask endRequestMask,
                out AbilityLifecycleCleanupRequest cleanupRequest)
            {
                cleanupRequest = default;
                if (cancelRequestMask[entityIndex])
                {
                    var cancelRequest = cancelRequests[entityIndex];
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

                if (endRequestMask[entityIndex])
                {
                    var endRequest = endRequests[entityIndex];
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

            private bool CleanupGrantedAbilityIfNeeded(
                Entity ability,
                Entity owner,
                bool canceled,
                ref bool destroyOnCleanupEnabled)
            {
                if (!GrantedByEffectLookup.HasComponent(ability))
                    return false;

                var granted = GrantedByEffectLookup[ability];
                var shouldRemove = granted.RemovePolicy switch
                {
                    GrantedAbilityRemovePolicy.WhenEnd => !canceled,
                    GrantedAbilityRemovePolicy.WhenCancel => canceled,
                    GrantedAbilityRemovePolicy.WhenCancelOrEnd => true,
                    GrantedAbilityRemovePolicy.SyncWithEffect => destroyOnCleanupEnabled,
                    _ => false,
                };

                if (!shouldRemove)
                    return false;

                RemoveAbilityFromAsc(owner, ability);
                ClearRuntimeGrantedAbility(granted.SourceEffect, ability);
                RefreshGrantedAbilityStoreState(granted.SourceEffect);
                destroyOnCleanupEnabled = true;
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

            private void EnqueueLifecycleEvent(
                Entity ability,
                Entity owner,
                in AbilityStateComponent state,
                in AbilityLifecycleCleanupRequest cleanupRequest)
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || owner == Entity.Null
                    || !OwnerFactLookup.HasBuffer(owner))
                    return;

                var fact = new GameplayEventBuffer
                {
                    Frame = Frame,
                    EventType = cleanupRequest.ShouldCancel
                        ? EGameplayEventType.AbilityCanceled
                        : EGameplayEventType.AbilityEnded,
                    Domain = EGameplayFactDomain.Ability,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = owner,
                    TargetAsc = owner,
                    SourceAbility = ability,
                    SourceEffect = cleanupRequest.SourceEffect,
                    EventCode = state.Code,
                    ReasonCode = (int)cleanupRequest.Reason,
                    Value = cleanupRequest.SourceAbilityCode,
                };
                var stream = StreamLookup[StreamEntity];
                fact.Sequence = Allocate(ref stream.NextFactSequence);
                StreamLookup[StreamEntity] = stream;
                OwnerFactLookup[owner].Add(new OwnerLocalGameplayFactBuffer
                {
                    Fact = fact,
                });
            }

            private static int Allocate(ref int next)
            {
                var value = next;
                next++;
                if (next <= 0)
                    next = 1;
                return value <= 0 ? Allocate(ref next) : value;
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
    }
}
