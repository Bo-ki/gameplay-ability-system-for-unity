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
                    ComponentType.ReadWrite<AbilityEndRequestComponent>(),
                },
                Options = EntityQueryOptions.IgnoreComponentEnabledState,
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
                EndRequestTypeHandle = SystemAPI.GetComponentTypeHandle<AbilityEndRequestComponent>(),
                DestroyOnCleanupLookup = SystemAPI.GetComponentLookup<AbilityDestroyOnCleanupComponent>(isReadOnly: true),
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(),
                SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: true),
                ActiveMutationCommandLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationCommandBuffer>(isReadOnly: false),
                ActiveMutationSetByCallerLookup =
                    SystemAPI.GetBufferLookup<ActiveEffectMutationSetByCallerValueBuffer>(isReadOnly: false),
                OwnerFactLookup = SystemAPI.GetBufferLookup<OwnerLocalGameplayFactBuffer>(),
                Catalog = catalogComponent.Catalog,
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
            }.Schedule(_query, state.Dependency);
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
            public ComponentTypeHandle<AbilityEndRequestComponent> EndRequestTypeHandle;
            [ReadOnly] public ComponentLookup<AbilityDestroyOnCleanupComponent> DestroyOnCleanupLookup;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            [ReadOnly] public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;
            public BufferLookup<ActiveEffectMutationCommandBuffer> ActiveMutationCommandLookup;
            public BufferLookup<ActiveEffectMutationSetByCallerValueBuffer> ActiveMutationSetByCallerLookup;
            public BufferLookup<OwnerLocalGameplayFactBuffer> OwnerFactLookup;
            [ReadOnly] public BlobAssetReference<GASDefinitionCatalogBlob> Catalog;
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public int Frame;

            public void Execute(
                in ArchetypeChunk chunk,
                int unfilteredChunkIndex,
                bool useEnabledMask,
                in v128 chunkEnabledMask)
            {
                if (!Catalog.IsCreated
                    || StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity))
                {
                    return;
                }

                var abilities = chunk.GetNativeArray(EntityTypeHandle);
                var states = chunk.GetNativeArray(ref StateTypeHandle);
                var commitRequests = chunk.GetNativeArray(ref CommitRequestTypeHandle);
                var commitRequestMask = chunk.GetEnabledMask(ref CommitRequestTypeHandle);
                var endRequests = chunk.GetNativeArray(ref EndRequestTypeHandle);
                var endRequestMask = chunk.GetEnabledMask(ref EndRequestTypeHandle);
                ref var catalog = ref Catalog.Value;
                var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                while (enumerator.NextEntityIndex(out var entityIndex))
                {
                    if (!commitRequestMask[entityIndex])
                        continue;

                    var ability = abilities[entityIndex];
                    var state = states[entityIndex];
                    var commitRequest = commitRequests[entityIndex];
                    ApplyAbilityActivationCommitRecord(
                        ability,
                        commitRequest.TargetAsc,
                        ref catalog,
                        ref state,
                        ref endRequests,
                        endRequestMask,
                        entityIndex);
                    states[entityIndex] = state;
                    commitRequestMask[entityIndex] = false;
                }
            }

            private bool ApplyAbilityActivationCommitRecord(
                Entity ability,
                Entity requestedTarget,
                ref GASDefinitionCatalogBlob catalog,
                ref AbilityStateComponent state,
                ref NativeArray<AbilityEndRequestComponent> endRequests,
                EnabledMask endRequestMask,
                int entityIndex)
            {
                var resolvedTarget = ResolveMainTarget(requestedTarget, state.Owner);

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
                    if (CanCompleteAutoEndOnCommitDirectly(ability, endRequestMask, entityIndex))
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
                            ref endRequests,
                            endRequestMask,
                            entityIndex,
                            owner: nextRuntime.Owner,
                            sourceAbility: ability,
                            sourceAbilityCode: nextRuntime.Code);
                    }
                }

                state = nextRuntime;
                return true;
            }

            private bool CanCompleteAutoEndOnCommitDirectly(
                Entity ability,
                EnabledMask endRequestMask,
                int entityIndex)
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

                return !endRequestMask[entityIndex];
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
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = type,
                    Domain = EGameplayFactDomain.Ability,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = state.Owner,
                    TargetAsc = state.Owner,
                    SourceAbility = ability,
                    EventCode = state.Code,
                    ReasonCode = (int)EAbilityLifecycleReason.ActivationCompleted,
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

                AppendAbilityEffectCommand(ref catalog, in plan, GASGESeedKind.Cost, plan.CostGameplayEffectCode);
                AppendAbilityEffectCommand(ref catalog, in plan, GASGESeedKind.Cooldown, plan.CooldownGameplayEffectCode);
                AppendAbilityEffectCommand(ref catalog, in plan, GASGESeedKind.Primary, plan.PrimaryGameplayEffectCode);
                AppendAbilityEffectCommand(ref catalog, in plan, GASGESeedKind.Secondary, plan.SecondaryGameplayEffectCode);

                nextRuntime.Phase = EAbilityPhase.Active;
                nextRuntime.Timer = 0f;
                nextRuntime.RemainingFrame = -1;
                return true;
            }

            private void AppendAbilityEffectCommand(
                ref GASDefinitionCatalogBlob catalog,
                in AbilityActivationPlanRecord plan,
                int seedKind,
                int gameplayEffectCode)
            {
                if (!GASGeneratedRuntimeDefinitionResolver.TryBuildGECommandSeed(
                        ref catalog,
                        in plan,
                        seedKind,
                        GEEffectCommandSource.Ability,
                        gameplayEffectCode,
                        contextId: 0,
                        parentContextId: 0,
                        out var seed))
                {
                    return;
                }

                AppendEffectCommand(ToEffectCommand(in seed));
            }

            private void AppendEffectCommand(in GEEffectCommandBuffer command)
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity))
                {
                    return;
                }

                if (command.Kind == GEEffectCommandKind.ActiveMutation
                    && !CanAppendActiveMutationCommand(in command))
                {
                    return;
                }

                if (command.Kind == GEEffectCommandKind.Instant
                    && !CanAppendInstantCommand(in command))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                var targetAsc = ResolveTargetAsc(in command);
                var resolved = PrepareCommand(
                    ref stream,
                    command.Kind == GEEffectCommandKind.ActiveMutation
                        ? 0
                        : SetByCallerLookup[targetAsc].Length,
                    in command);
                if (resolved.Kind == GEEffectCommandKind.ActiveMutation)
                    AppendActiveMutationCommand(in resolved);
                else
                    AppendInstantCommand(in resolved);
                StreamLookup[StreamEntity] = stream;
            }

            private bool CanAppendInstantCommand(in GEEffectCommandBuffer command)
            {
                var targetAsc = ResolveTargetAsc(in command);
                return targetAsc != Entity.Null
                    && CommandLookup.HasBuffer(targetAsc)
                    && SetByCallerLookup.HasBuffer(targetAsc);
            }

            private bool CanAppendActiveMutationCommand(in GEEffectCommandBuffer command)
            {
                var targetAsc = ResolveTargetAsc(in command);
                return targetAsc != Entity.Null
                    && ActiveMutationCommandLookup.HasBuffer(targetAsc)
                    && ActiveMutationSetByCallerLookup.HasBuffer(targetAsc);
            }

            private void AppendActiveMutationCommand(in GEEffectCommandBuffer command)
            {
                var targetAsc = ResolveTargetAsc(in command);
                var ownerCommand = command;
                ownerCommand.SetByCallerStart = 0;
                ownerCommand.SetByCallerCount = 0;
                ActiveMutationCommandLookup[targetAsc].Add(new ActiveEffectMutationCommandBuffer
                {
                    Command = ownerCommand,
                });
            }

            private void AppendInstantCommand(in GEEffectCommandBuffer command)
            {
                var targetAsc = ResolveTargetAsc(in command);
                var ownerCommand = command;
                ownerCommand.SetByCallerStart = SetByCallerLookup[targetAsc].Length;
                ownerCommand.SetByCallerCount = 0;
                CommandLookup[targetAsc].Add(ownerCommand);
            }

            private static Entity ResolveTargetAsc(in GEEffectCommandBuffer command)
            {
                return command.TargetAsc != Entity.Null ? command.TargetAsc : command.SourceAsc;
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
                ref NativeArray<AbilityEndRequestComponent> endRequests,
                EnabledMask endRequestMask,
                int entityIndex,
                Entity sourceAbility = default,
                Entity sourceEffect = default,
                Entity owner = default,
                int sourceAbilityCode = 0)
            {
                if (endRequestMask[entityIndex])
                {
                    return;
                }

                endRequests[entityIndex] = new AbilityEndRequestComponent
                {
                    Reason = reason,
                    SourceAbility = sourceAbility,
                    SourceEffect = sourceEffect,
                    SourceAbilityCode = sourceAbilityCode,
                };
                endRequestMask[entityIndex] = true;
                EnqueueGameplayEvent(new GameplayEventBuffer
                {
                    EventType = EGameplayEventType.AbilityEndRequested,
                    Domain = EGameplayFactDomain.Ability,
                    Category = EGameplayFactCategory.Request,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = owner,
                    TargetAsc = owner,
                    SourceAbility = ability,
                    SourceEffect = sourceEffect,
                    ReasonCode = (int)reason,
                    EventCode = sourceAbilityCode,
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

            private void EnqueueGameplayEvent(GameplayEventBuffer evt)
            {
                var owner = evt.TargetAsc != Entity.Null ? evt.TargetAsc : evt.SourceAsc;
                if (owner == Entity.Null || !OwnerFactLookup.HasBuffer(owner))
                    return;

                evt.Frame = Frame;
                if (StreamLookup.HasComponent(StreamEntity))
                {
                    var stream = StreamLookup[StreamEntity];
                    evt.Sequence = Allocate(ref stream.NextFactSequence);
                    StreamLookup[StreamEntity] = stream;
                }
                else
                {
                    evt.Sequence = 0;
                }

                OwnerFactLookup[owner].Add(new OwnerLocalGameplayFactBuffer
                {
                    Fact = evt,
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
        }
    }
}
