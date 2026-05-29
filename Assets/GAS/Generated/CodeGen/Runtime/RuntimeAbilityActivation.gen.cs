///////////////////////////////////
//// This is a generated file. ////
////     Do not modify it.     ////
///////////////////////////////////

using GAS.Runtime;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime.Generated
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateAfter(typeof(AbilityTryActivateSystem))]
    [UpdateBefore(typeof(AbilityCommitSystem))]
    [UpdateBefore(typeof(GEEffectCommandIngestSystem))]
    public partial struct AbilityCatalogCommitSystem : ISystem
    {
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<AbilityCommitRequestComponent, AbilityStateComponent>()
                .Build();
            state.RequireForUpdate<GASDefinitionCatalogComponent>();
            state.RequireForUpdate<AbilityCommandBuffer>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogComponent = SystemAPI.GetSingleton<GASDefinitionCatalogComponent>();
            if (!GASGeneratedDefinitionCatalogLookup.IsCatalogCreated(catalogComponent.Catalog))
                return;

            var em = state.EntityManager;
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            var seeds = new NativeList<GECommandSeedRecord>(Allocator.Temp);
            var streamActivations = new NativeParallelHashMap<Entity, AbilityCommandRequestComponent>(1, Allocator.Temp);
            var commandWriter = EffectCommandSpecStream.BeginCommandWriter(em, frame);
            var gameplayEventWriter = EventBusHelper.BeginGameplayEventBatch(em, GASManager.EntityEventBus);
            var streamCommands = ResolveAbilityCommandBuffer(em);
            ref var catalog = ref catalogComponent.Catalog.Value;

            try
            {
                if (streamCommands.IsCreated)
                {
                    if (streamCommands.Length > streamActivations.Capacity)
                        streamActivations.Capacity = streamCommands.Length;

                    for (var i = 0; i < streamCommands.Length; i++)
                    {
                        AddTrustedStreamActivation(streamCommands[i].Command, ref streamActivations);
                    }

                    streamCommands.Clear();
                }

                if (!streamActivations.IsEmpty)
                {
                    foreach (var (stateRef, ability) in SystemAPI
                                 .Query<RefRW<AbilityStateComponent>>()
                                 .WithEntityAccess())
                    {
                        if (!streamActivations.TryGetValue(ability, out var command))
                            continue;

                        TryCommitTrustedStreamActivation(
                            em,
                            command,
                            ability,
                            frame,
                            ref catalog,
                            ref commandWriter,
                            ref gameplayEventWriter,
                            ref seeds,
                            ref stateRef.ValueRW);
                    }
                }

                foreach (var (stateRef, commitData, commitRequest, ability) in SystemAPI
                             .Query<
                                 RefRW<AbilityStateComponent>,
                                 RefRO<AbilityCommitRequestComponent>,
                                 EnabledRefRW<AbilityCommitRequestComponent>>()
                             .WithEntityAccess())
                {
                    ApplyAbilityActivationCommitRecord(
                        em,
                        ability,
                        commitData.ValueRO.TargetAsc,
                        frame,
                        ref catalog,
                        ref commandWriter,
                        ref gameplayEventWriter,
                        ref seeds,
                        ref stateRef.ValueRW);
                    commitRequest.ValueRW = false;
                }

                commandWriter.Flush();
            }
            finally
            {
                gameplayEventWriter.Dispose();
                streamActivations.Dispose();
                seeds.Dispose();
            }
        }

        private static DynamicBuffer<AbilityCommandBuffer> ResolveAbilityCommandBuffer(EntityManager em)
        {
            if (!EffectCommandSpecStream.TryGetSingleton(em, out var streamEntity)
                || streamEntity == Entity.Null
                || !em.Exists(streamEntity)
                || !em.HasBuffer<AbilityCommandBuffer>(streamEntity))
            {
                return default;
            }

            return em.GetBuffer<AbilityCommandBuffer>(streamEntity);
        }

        private static void AddTrustedStreamActivation(
            AbilityCommandRequestComponent command,
            ref NativeParallelHashMap<Entity, AbilityCommandRequestComponent> streamActivations)
        {
            if (command.CommandType != EAbilityCommandType.Activate || command.AbilityEntity == Entity.Null)
                return;

            streamActivations.TryAdd(command.AbilityEntity, command);
        }

        private static bool TryCommitTrustedStreamActivation(
            EntityManager em,
            AbilityCommandRequestComponent command,
            Entity ability,
            int frame,
            ref GASDefinitionCatalogBlob catalog,
            ref EffectCommandSpecStream.CommandWriter commandWriter,
            ref EventBusHelper.GameplayEventBusWriter gameplayEventWriter,
            ref NativeList<GECommandSeedRecord> seeds,
            ref AbilityStateComponent state)
        {
            if (command.AbilityCode > 0 && state.Code != command.AbilityCode)
                return false;
            if (command.Owner != Entity.Null && state.Owner != command.Owner)
                return false;

            if (!ApplyAbilityActivationCommitRecord(
                    em,
                    ability,
                    command.TargetAsc,
                    frame,
                    ref catalog,
                    ref commandWriter,
                    ref gameplayEventWriter,
                    ref seeds,
                    ref state))
            {
                return false;
            }

            return true;
        }

        private static bool ApplyAbilityActivationCommitRecord(
            EntityManager em,
            Entity ability,
            Entity requestedTarget,
            int frame,
            ref GASDefinitionCatalogBlob catalog,
            ref EffectCommandSpecStream.CommandWriter commandWriter,
            ref EventBusHelper.GameplayEventBusWriter gameplayEventWriter,
            ref NativeList<GECommandSeedRecord> seeds,
            ref AbilityStateComponent state)
        {
            if (!em.Exists(ability))
                return false;

            var resolvedTarget = ResolveMainTarget(em, requestedTarget, state.Owner);
            seeds.Clear();

            var committed = TryCommitAbility(
                em,
                ability,
                state,
                resolvedTarget,
                frame,
                ref catalog,
                ref commandWriter,
                ref seeds,
                out var nextRuntime);

            if (committed)
            {
                if (em.HasComponent<AbilityAutoEndOnCommitComponent>(ability)
                    && em.IsComponentEnabled<AbilityAutoEndOnCommitComponent>(ability))
                {
                    if (CanCompleteAutoEndOnCommitDirectly(em, ability))
                    {
                        CompleteAutoEndOnCommitDirectly(
                            em,
                            ability,
                            nextRuntime.Owner,
                            HasActivationOwnedTags(ref catalog, state.Code),
                            ref nextRuntime,
                            ref gameplayEventWriter);
                    }
                    else
                    {
                        AbilityRuntimeActions.RequestAbilityEnd(
                            ability,
                            em,
                            EAbilityLifecycleReason.ActivationCompleted,
                            sourceAbility: ability,
                            sourceAbilityCode: nextRuntime.Code);
                    }
                }

                state = nextRuntime;
                return true;
            }

            return false;
        }

        private static bool CanCompleteAutoEndOnCommitDirectly(EntityManager em, Entity ability)
        {
            if (em.HasComponent<AbilityGrantedByEffectComponent>(ability)
                || AbilityRuntimeActions.IsDestroyOnCleanupEnabled(ability, em))
            {
                return false;
            }

            if (em.HasComponent<AbilityCancelRequestComponent>(ability)
                && em.IsComponentEnabled<AbilityCancelRequestComponent>(ability))
            {
                return false;
            }

            return !em.HasComponent<AbilityEndRequestComponent>(ability)
                   || !em.IsComponentEnabled<AbilityEndRequestComponent>(ability);
        }

        private static void CompleteAutoEndOnCommitDirectly(
            EntityManager em,
            Entity ability,
            Entity owner,
            bool removeActivationOwnedTags,
            ref AbilityStateComponent state,
            ref EventBusHelper.GameplayEventBusWriter gameplayEventWriter)
        {
            if (removeActivationOwnedTags)
                AbilityRuntimeActions.RemoveTagsFromSource(owner, ability, em);
            EnqueueAutoEndLifecycleEvent(
                ref gameplayEventWriter,
                EGameplayEventType.AbilityEndRequested,
                ability,
                in state);
            EnqueueAutoEndLifecycleEvent(
                ref gameplayEventWriter,
                EGameplayEventType.AbilityEnded,
                ability,
                in state);

            state.Phase = EAbilityPhase.Ready;
            state.Timer = 0f;
            state.RemainingFrame = 0;
        }

        private static void EnqueueAutoEndLifecycleEvent(
            ref EventBusHelper.GameplayEventBusWriter gameplayEventWriter,
            EGameplayEventType type,
            Entity ability,
            in AbilityStateComponent state)
        {
            gameplayEventWriter.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
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

        private static bool TryCommitAbility(
            EntityManager em,
            Entity ability,
            in AbilityStateComponent state,
            Entity target,
            int frame,
            ref GASDefinitionCatalogBlob catalog,
            ref EffectCommandSpecStream.CommandWriter commandWriter,
            ref NativeList<GECommandSeedRecord> seeds,
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
                frame,
                state.Level,
                out var plan))
                return false;

            ref readonly var abilityDefinition = ref GASGeneratedDefinitionCatalogLookup.GetAbility(ref catalog, plan.AbilityDefinitionIndex);
            if (abilityDefinition.RequirementCount > 0)
            {
                var ownerTags = em.HasComponent<TagMaskComponent>(state.Owner)
                    ? em.GetComponentData<TagMaskComponent>(state.Owner)
                    : default;
                if (!GASGeneratedRequirementEvaluator.EvaluateAbilityRequirements(
                    ref catalog,
                    plan.AbilityDefinitionIndex,
                    in ownerTags,
                    out _))
                    return false;
            }

            ApplyActivationOwnedTags(em, ability, state.Owner, ref catalog, in abilityDefinition);

            GASGeneratedRuntimeDefinitionResolver.WriteGECommandSeeds(
                ref catalog,
                in plan,
                contextId: 0,
                parentContextId: 0,
                ref seeds);
            for (var i = 0; i < seeds.Length; i++)
            {
                var seed = seeds[i];
                if (seed.FailureReasonCode != GASFailureReasonCodes.None)
                    continue;
                commandWriter.AppendCommand(ToEffectCommand(in seed));
            }

            nextRuntime.Phase = EAbilityPhase.Active;
            nextRuntime.Timer = 0f;
            nextRuntime.RemainingFrame = -1;
            return true;
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

        private static void ApplyActivationOwnedTags(
            EntityManager em,
            Entity ability,
            Entity owner,
            ref GASDefinitionCatalogBlob catalog,
            in GASCatalogAbilityDefinitionBlob abilityDefinition)
        {
            var tagMaskIndex = abilityDefinition.ActivationOwnedTagMaskIndex;
            if (tagMaskIndex < 0 || tagMaskIndex >= catalog.TagMasks.Length)
                return;
            if (owner == Entity.Null || !em.Exists(owner) || !em.HasComponent<TagMaskComponent>(owner))
                return;

            var ownedMask = catalog.TagMasks[tagMaskIndex].Mask;
            if (ownedMask.IsEmpty)
                return;

            var ownerTags = em.GetComponentData<TagMaskComponent>(owner);
            ownerTags.Mask0 |= ownedMask.Mask0;
            ownerTags.Mask1 |= ownedMask.Mask1;
            ownerTags.Mask2 |= ownedMask.Mask2;
            ownerTags.Mask3 |= ownedMask.Mask3;
            em.SetComponentData(owner, ownerTags);

            if (!em.HasBuffer<TagTemporarySourceBuffer>(owner))
                return;

            var tempSources = em.GetBuffer<TagTemporarySourceBuffer>(owner);
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

        private static Entity ResolveMainTarget(EntityManager em, Entity requestedTarget, Entity fallbackTarget)
        {
            if (IsAvailableAsc(em, requestedTarget))
                return requestedTarget;

            return fallbackTarget;
        }

        private static bool IsAvailableAsc(EntityManager em, Entity asc)
        {
            return asc != Entity.Null
                && em.Exists(asc)
                && !ASCEntityFactory.IsDestroying(em, asc);
        }
    }
}
