///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Generated
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateAfter(typeof(AbilityTryActivateSystem))]
    [UpdateBefore(typeof(AbilityCommitSystem))]
    public partial struct AbilityCatalogCommitSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadWrite<AbilityCommitRequestComponent>(),
                    ComponentType.ReadWrite<AbilityStateComponent>(),
                },
            });
            state.RequireForUpdate(_query);
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || !EffectCommandSpecStream.HasRequiredBuffers(em, streamEntity))
                return;

            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var seeds = new NativeList<GECommandSeedRecord>(Allocator.TempJob);
            state.Dependency = new AbilityCatalogCommitJob
            {
                EntityTypeHandle = SystemAPI.GetEntityTypeHandle(),
                StateTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityStateComponent>(),
                CommitRequestTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityCommitRequestComponent>(),
                DestroyingLookup = SystemAPI.GetComponentLookup<ASCDestroyingComponent>(isReadOnly: true),
                TagMaskLookup = SystemAPI.GetComponentLookup<TagMaskComponent>(),
                FixedTagMaskLookup = SystemAPI.GetComponentLookup<TagFixedMaskComponent>(isReadOnly: true),
                TemporaryTagSourceLookup = SystemAPI.GetBufferLookup<TagTemporarySourceBuffer>(),
                AutoEndOnCommitLookup = SystemAPI.GetComponentLookup<AbilityAutoEndOnCommitComponent>(isReadOnly: true),
                GrantedByEffectLookup = SystemAPI.GetComponentLookup<AbilityGrantedByEffectComponent>(isReadOnly: true),
                CancelRequestLookup = SystemAPI.GetComponentLookup<AbilityCancelRequestComponent>(),
                EndRequestLookup = SystemAPI.GetComponentLookup<AbilityEndRequestComponent>(),
                DestroyOnCleanupLookup = SystemAPI.GetComponentLookup<AbilityDestroyOnCleanupComponent>(isReadOnly: true),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(),
                SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: true),
                EventBusLookup = SystemAPI.GetComponentLookup<GameplayEventBusComponent>(),
                GameplayEventLookup = SystemAPI.GetBufferLookup<GameplayEventBusEventBuffer>(),
                Catalog = catalogComponent.Catalog,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
                Seeds = seeds,
            }.Schedule(_query, state.Dependency);
            state.Dependency = seeds.Dispose(state.Dependency);
        }

        [BurstCompile]
        private struct AbilityCatalogCommitJob : IJobChunk
        {
            [ReadOnly] public EntityTypeHandle EntityTypeHandle;
            public ComponentTypeHandle<AbilityStateComponent> StateTypeHandle;
            public ComponentTypeHandle<AbilityCommitRequestComponent> CommitRequestTypeHandle;
            [ReadOnly] public ComponentLookup<ASCDestroyingComponent> DestroyingLookup;
            public ComponentLookup<TagMaskComponent> TagMaskLookup;
            [ReadOnly] public ComponentLookup<TagFixedMaskComponent> FixedTagMaskLookup;
            public BufferLookup<TagTemporarySourceBuffer> TemporaryTagSourceLookup;
            [ReadOnly] public ComponentLookup<AbilityAutoEndOnCommitComponent> AutoEndOnCommitLookup;
            [ReadOnly] public ComponentLookup<AbilityGrantedByEffectComponent> GrantedByEffectLookup;
            public ComponentLookup<AbilityCancelRequestComponent> CancelRequestLookup;
            public ComponentLookup<AbilityEndRequestComponent> EndRequestLookup;
            [ReadOnly] public ComponentLookup<AbilityDestroyOnCleanupComponent> DestroyOnCleanupLookup;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            [ReadOnly] public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;
            public ComponentLookup<GameplayEventBusComponent> EventBusLookup;
            public BufferLookup<GameplayEventBusEventBuffer> GameplayEventLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public int Frame;
            public NativeList<GECommandSeedRecord> Seeds;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Catalog.IsCreated
                    || StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !CommandLookup.HasBuffer(StreamEntity)
                    || !SetByCallerLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var abilities = chunk.GetNativeArray(EntityTypeHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);
                var commitRequests = chunk.GetNativeArray(ref CommitRequestTypeHandle);
                var commitRequestMask = chunk.GetEnabledMask(ref CommitRequestTypeHandle);
                ref var catalog = ref Catalog.Value;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    var ability = abilities[entityIndex];
                    var state = states[entityIndex];
                    var commitRequest = commitRequests[entityIndex];
                    ApplyAbilityActivationCommitRecord(
                        ability,
                        commitRequest.TargetAsc,
                        ref catalog,
                        ref state);
                    states[entityIndex] = state;
                    commitRequestMask[entityIndex] = false;
                }
            }

            private bool ApplyAbilityActivationCommitRecord(
                Entity ability,
                Entity requestedTarget,
                ref GASDefinitionCatalogBlob catalog,
                ref AbilityStateComponent state)
            {
                var resolvedTarget = ResolveMainTarget(requestedTarget, state.Owner);
                Seeds.Clear();

                var committed = TryCommitAbility(
                    ability,
                    state,
                    resolvedTarget,
                    ref catalog,
                    out var nextRuntime);

                if (!committed)
                    return false;

                if (AutoEndOnCommitLookup.HasComponent(ability)
                    && AutoEndOnCommitLookup.IsComponentEnabled(ability))
                {
                    if (CanCompleteAutoEndOnCommitDirectly(ability))
                    {
                        CompleteAutoEndOnCommitDirectly(
                            ability,
                            nextRuntime.Owner,
                            HasActivationOwnedTags(ref catalog, state.Code),
                            ref nextRuntime);
                    }
                    else
                    {
                        RequestAbilityEnd(
                            ability,
                            EAbilityLifecycleReason.ActivationCompleted,
                            sourceAbility: ability,
                            sourceAbilityCode: nextRuntime.Code);
                    }
                }

                state = nextRuntime;
                return true;
            }

            private bool CanCompleteAutoEndOnCommitDirectly(Entity ability)
            {
                if (GrantedByEffectLookup.HasComponent(ability)
                    || IsDestroyOnCleanupEnabled(ability))
                {
                    return false;
                }

                if (CancelRequestLookup.HasComponent(ability)
                    && CancelRequestLookup.IsComponentEnabled(ability))
                {
                    return false;
                }

                return !EndRequestLookup.HasComponent(ability)
                       || !EndRequestLookup.IsComponentEnabled(ability);
            }

            private void CompleteAutoEndOnCommitDirectly(
                Entity ability,
                Entity owner,
                bool removeActivationOwnedTags,
                ref AbilityStateComponent state)
            {
                if (removeActivationOwnedTags)
                    RemoveTagsFromSource(owner, ability);
                EnqueueAutoEndLifecycleEvent(
                    EGameplayEventType.AbilityEndRequested,
                    ability,
                    in state);
                EnqueueAutoEndLifecycleEvent(
                    EGameplayEventType.AbilityEnded,
                    ability,
                    in state);

                state.Phase = EAbilityPhase.Ready;
                state.Timer = 0f;
                state.RemainingFrame = 0;
            }

            private void EnqueueAutoEndLifecycleEvent(
                EGameplayEventType type,
                Entity ability,
                in AbilityStateComponent state)
            {
                EnqueueGameplayEvent(new GameplayEventBusEventBuffer
                {
                    Type = type,
                    SourceAsc = state.Owner,
                    TargetAsc = state.Owner,
                    SourceAbility = ability,
                    RelatedAbility = ability,
                    EventCode = state.Code,
                    ReasonCode = (int)EAbilityLifecycleReason.ActivationCompleted,
                    RelatedAbilityCode = state.Code,
                    Value = state.Code,
                });
            }

            private bool TryCommitAbility(
                Entity ability,
                in AbilityStateComponent state,
                Entity target,
                ref GASDefinitionCatalogBlob catalog,
                out AbilityStateComponent nextRuntime)
            {
                nextRuntime = state;
                if (state.Phase == EAbilityPhase.Activating || state.Phase == EAbilityPhase.Active)
                    return false;

                if (!GASGeneratedRuntimeDefinitionResolver.TryBuildAbilityActivationPlan(
                    ref catalog,
                    state.Code,
                    state.Owner,
                    target,
                    ability,
                    Frame,
                    state.Level,
                    out var plan))
                    return false;

                ref readonly var abilityDefinition = ref GASGeneratedDefinitionCatalogLookup.GetAbility(ref catalog, plan.AbilityDefinitionIndex);
                if (abilityDefinition.RequirementCount > 0)
                {
                    var ownerTags = TagMaskLookup.HasComponent(state.Owner)
                        ? TagMaskLookup[state.Owner]
                        : default;
                    if (!GASGeneratedRequirementEvaluator.EvaluateAbilityRequirements(
                        ref catalog,
                        plan.AbilityDefinitionIndex,
                        in ownerTags,
                        out _))
                        return false;
                }

                ApplyActivationOwnedTags(ability, state.Owner, ref catalog, in abilityDefinition);

                GASGeneratedRuntimeDefinitionResolver.WriteGECommandSeeds(
                    ref catalog,
                    in plan,
                    contextId: 0,
                    parentContextId: 0,
                    ref Seeds);
                for (var i = 0; i < Seeds.Length; i++)
                {
                    var seed = Seeds[i];
                    if (seed.FailureReasonCode != GASFailureReasonCodes.None)
                        continue;
                    AppendEffectCommand(ToEffectCommand(in seed));
                }

                nextRuntime.Phase = EAbilityPhase.Active;
                nextRuntime.Timer = 0f;
                nextRuntime.RemainingFrame = -1;
                return true;
            }

            private void AppendEffectCommand(in GEEffectCommandBuffer command)
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !CommandLookup.HasBuffer(StreamEntity)
                    || !SetByCallerLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                var resolved = PrepareCommand(
                    ref stream,
                    SetByCallerLookup[StreamEntity].Length,
                    in command);
                CommandLookup[StreamEntity].Add(resolved);
                StreamLookup[StreamEntity] = stream;
            }

            private GEEffectCommandBuffer PrepareCommand(
                ref GEEffectCommandStreamComponent stream,
                int setByCallerStart,
                in GEEffectCommandBuffer command)
            {
                var resolved = command;
                if (resolved.Sequence <= 0)
                    resolved.Sequence = Allocate(ref stream.NextCommandSequence);
                if (resolved.Frame <= 0)
                    resolved.Frame = Frame;
                if (resolved.ContextId <= 0)
                    resolved.ContextId = Allocate(ref stream.NextContextId);
                if (resolved.TargetAsc == Entity.Null)
                    resolved.TargetAsc = resolved.SourceAsc;
                if (resolved.Instigator == Entity.Null)
                    resolved.Instigator = resolved.SourceAsc;
                if (resolved.Causer == Entity.Null)
                    resolved.Causer = resolved.SourceAbility;

                resolved.SetByCallerStart = setByCallerStart;
                resolved.SetByCallerCount = 0;
                return resolved;
            }

            private static GEEffectCommandBuffer ToEffectCommand(in GECommandSeedRecord seed)
            {
                var isActiveMutation = (seed.Flags & GASGECommandSeedFlags.ActiveMutation) != 0;
                return new GEEffectCommandBuffer
                {
                    Frame = seed.Frame,
                    Kind = isActiveMutation ? GEEffectCommandKind.ActiveMutation : GEEffectCommandKind.Instant,
                    Source = seed.Source,
                    SourceAsc = seed.SourceAsc,
                    TargetAsc = seed.TargetAsc,
                    SourceAbility = seed.SourceAbility,
                    SourceEffect = seed.SourceEffect,
                    Instigator = seed.SourceAsc,
                    Causer = seed.SourceAbility,
                    GameplayEffectCode = seed.GameplayEffectCode,
                    Level = seed.Level,
                    DurationFrameOverride = seed.DurationFrameOverride,
                    ContextId = seed.ContextId,
                    ParentContextId = seed.ParentContextId,
                    TargetDataKind = seed.TargetAsc == seed.SourceAsc ? ETargetDataKind.Self : ETargetDataKind.Entity,
                    Flags = seed.Flags,
                };
            }

            private static bool HasActivationOwnedTags(
                ref GASDefinitionCatalogBlob catalog,
                int abilityCode)
            {
                for (var i = 0; i < catalog.Abilities.Length; i++)
                {
                    var abilityDefinition = catalog.Abilities[i];
                    if (abilityDefinition.AbilityCode != abilityCode)
                        continue;

                    var tagMaskIndex = abilityDefinition.ActivationOwnedTagMaskIndex;
                    return tagMaskIndex >= 0
                           && tagMaskIndex < catalog.TagMasks.Length
                           && !catalog.TagMasks[tagMaskIndex].Mask.IsEmpty;
                }

                return false;
            }

            private void ApplyActivationOwnedTags(
                Entity ability,
                Entity owner,
                ref GASDefinitionCatalogBlob catalog,
                in GASCatalogAbilityDefinitionBlob abilityDefinition)
            {
                var tagMaskIndex = abilityDefinition.ActivationOwnedTagMaskIndex;
                if (tagMaskIndex < 0 || tagMaskIndex >= catalog.TagMasks.Length)
                    return;
                if (owner == Entity.Null || !TagMaskLookup.HasComponent(owner))
                    return;

                var ownedMask = catalog.TagMasks[tagMaskIndex].Mask;
                if (ownedMask.IsEmpty)
                    return;

                var ownerTags = TagMaskLookup[owner];
                ownerTags.Mask0 |= ownedMask.Mask0;
                ownerTags.Mask1 |= ownedMask.Mask1;
                ownerTags.Mask2 |= ownedMask.Mask2;
                ownerTags.Mask3 |= ownedMask.Mask3;
                TagMaskLookup[owner] = ownerTags;

                if (!TemporaryTagSourceLookup.HasBuffer(owner))
                    return;

                var tempSources = TemporaryTagSourceLookup[owner];
                for (var tagIndex = 0; tagIndex < TagMaskComponent.Capacity; tagIndex++)
                {
                    if (!ownedMask.HasTag(tagIndex))
                        continue;
                    tempSources.Add(new TagTemporarySourceBuffer
                    {
                        TagIndex = tagIndex,
                        Source = ability,
                    });
                }
            }

            private Entity ResolveMainTarget(Entity requestedTarget, Entity fallbackTarget)
            {
                if (IsAvailableAsc(requestedTarget))
                    return requestedTarget;

                return fallbackTarget;
            }

            private bool IsAvailableAsc(Entity asc)
            {
                return asc != Entity.Null
                    && DestroyingLookup.HasComponent(asc)
                    && !DestroyingLookup.IsComponentEnabled(asc);
            }

            private bool IsDestroyOnCleanupEnabled(Entity ability)
            {
                return DestroyOnCleanupLookup.HasComponent(ability)
                       && DestroyOnCleanupLookup.IsComponentEnabled(ability);
            }

            private void RequestAbilityEnd(
                Entity ability,
                EAbilityLifecycleReason reason,
                Entity sourceAbility = default,
                Entity sourceEffect = default,
                int sourceAbilityCode = 0)
            {
                if (!EndRequestLookup.HasComponent(ability)
                    || EndRequestLookup.IsComponentEnabled(ability))
                {
                    return;
                }

                EndRequestLookup[ability] = new AbilityEndRequestComponent
                {
                    Reason = reason,
                    SourceAbility = sourceAbility,
                    SourceEffect = sourceEffect,
                    SourceAbilityCode = sourceAbilityCode,
                };
                EndRequestLookup.SetComponentEnabled(ability, true);
                EnqueueGameplayEvent(new GameplayEventBusEventBuffer
                {
                    Type = EGameplayEventType.AbilityEndRequested,
                    SourceAbility = ability,
                    GameplayEffect = sourceEffect,
                    RelatedAbility = sourceAbility,
                    ReasonCode = (int)reason,
                    RelatedAbilityCode = sourceAbilityCode,
                    Value = sourceAbilityCode,
                });
            }

            private void RemoveTagsFromSource(Entity owner, Entity source)
            {
                if (owner == Entity.Null
                    || !TagMaskLookup.HasComponent(owner)
                    || !TemporaryTagSourceLookup.HasBuffer(owner))
                {
                    return;
                }

                var sources = TemporaryTagSourceLookup[owner];
                for (var i = sources.Length - 1; i >= 0; i--)
                {
                    if (sources[i].Source != source)
                        continue;

                    var tagIndex = sources[i].TagIndex;
                    sources.RemoveAt(i);
                    RemoveTagIndexFromEffectiveMaskIfUnreferenced(owner, tagIndex);
                }
            }

            private void RemoveTagIndexFromEffectiveMaskIfUnreferenced(Entity owner, int tagIndex)
            {
                if (!TagMaskLookup.HasComponent(owner))
                    return;

                if (FixedTagMaskLookup.HasComponent(owner)
                    && FixedTagMaskLookup[owner].Mask.HasTag(tagIndex))
                {
                    return;
                }

                if (HasAnyTemporarySourceForTag(owner, tagIndex))
                    return;

                var mask = TagMaskLookup[owner];
                mask.RemoveTag(tagIndex);
                TagMaskLookup[owner] = mask;
            }

            private bool HasAnyTemporarySourceForTag(Entity owner, int tagIndex)
            {
                if (!TemporaryTagSourceLookup.HasBuffer(owner))
                    return false;

                var temporaryTags = TemporaryTagSourceLookup[owner];
                for (var i = 0; i < temporaryTags.Length; i++)
                    if (temporaryTags[i].TagIndex == tagIndex)
                        return true;
                return false;
            }

            private void EnqueueGameplayEvent(GameplayEventBusEventBuffer evt)
            {
                if (EventBusEntity == Entity.Null || !GameplayEventLookup.HasBuffer(EventBusEntity))
                    return;

                evt.Frame = Frame;
                if (EventBusLookup.HasComponent(EventBusEntity))
                {
                    var eventBus = EventBusLookup[EventBusEntity];
                    evt.Sequence = eventBus.NextSequence;
                    eventBus.NextSequence++;
                    EventBusLookup[EventBusEntity] = eventBus;
                }
                else
                {
                    evt.Sequence = 0;
                }

                GameplayEventLookup[EventBusEntity].Add(evt);
            }

            private static int Allocate(ref int next)
            {
                var value = next;
                next++;
                if (next <= 0)
                    next = 1;
                return value <= 0 ? Allocate(ref next) : value;
            }
        }
    }
}
