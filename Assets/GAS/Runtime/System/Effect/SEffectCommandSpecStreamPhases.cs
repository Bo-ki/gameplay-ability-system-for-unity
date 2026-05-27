using Unity.Entities;
using static GAS.Runtime.EffectCommandSpecStreamPhaseUtility;

namespace GAS.Runtime
{
    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateBefore(typeof(SEffectCommandIngest))]
    public partial struct SEffectCommandSpecStreamFramePrepare : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectCommandSpecStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<CEffectCommandSpecStream>();
            EffectCommandSpecStream.PrepareFrameLocalData(
                em,
                streamEntity,
                GASRuntimeFrameContext.ResolveCurrentFrame(em));
        }
    }

    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(STryActivateAbility))]
    public partial struct SEffectCommandIngest : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectCommandSpecStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }

    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SEffectCommandIngest))]
    public partial struct SInstantEffectSpecBuild : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectCommandSpecStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<CEffectCommandSpecStream>();
            EffectCommandSpecStream.EnsureBuffers(em, streamEntity);

            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var commands = em.GetBuffer<BEffectCommand>(streamEntity);
            var specs = em.GetBuffer<BInstantEffectSpec>(streamEntity);
            var setByCallerValues = em.GetBuffer<BEffectCommandSetByCallerValue>(streamEntity);

            var start = ClampCursor(stream.SpecBuildCommandCursor, commands.Length);
            for (var i = start; i < commands.Length; i++)
            {
                var command = commands[i];
                if (command.Kind != EEffectCommandKind.Instant
                    || !TryGetSimpleInstantDefinition(em, in command, out var blob))
                {
                    continue;
                }

                ref var definition = ref blob.Value;
                var spec = new BInstantEffectSpec
                {
                    Sequence = Allocate(ref stream.NextSpecSequence),
                    SourceCommandSequence = command.Sequence,
                    Frame = command.Frame,
                    SourceAsc = command.SourceAsc,
                    TargetAsc = command.TargetAsc,
                    SourceAbility = command.SourceAbility,
                    SourceEffect = command.SourceEffect,
                    Instigator = command.Instigator,
                    Causer = command.Causer,
                    GameplayEffectCode = command.GameplayEffectCode,
                    CueRequestOnApplyCode = definition.HasCueRequestOnApply
                        ? definition.CueRequestOnApplyCode
                        : 0,
                    Level = command.Level,
                    StackCount = 1,
                    DurationFrameOverride = command.DurationFrameOverride,
                    ContextId = command.ContextId,
                    ParentContextId = command.ParentContextId,
                    TargetDataKind = command.TargetDataKind,
                    SetByCallerStart = command.SetByCallerStart,
                    SetByCallerCount = command.SetByCallerCount,
                    Flags = command.Flags,
                };

                AssignSpecSequence(
                    setByCallerValues,
                    spec.SetByCallerStart,
                    spec.SetByCallerCount,
                    spec.SourceCommandSequence,
                    spec.Sequence);
                specs.Add(spec);
            }

            stream.SpecBuildCommandCursor = commands.Length;
            em.SetComponentData(streamEntity, stream);
        }
    }

    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SActiveEffectMutationApply))]
    public partial struct SAttributeDeltaApply : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectCommandSpecStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<CEffectCommandSpecStream>();
            EffectCommandSpecStream.EnsureBuffers(em, streamEntity);

            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var specs = em.GetBuffer<BInstantEffectSpec>(streamEntity);
            var deltas = em.GetBuffer<BAttributeDelta>(streamEntity);
            var setByCallerValues = em.GetBuffer<BEffectCommandSetByCallerValue>(streamEntity);

            var start = ClampCursor(stream.DeltaApplySpecCursor, specs.Length);
            for (var i = start; i < specs.Length; i++)
                ApplySimpleInstantSpec(em, ref stream, specs[i], setByCallerValues, deltas);

            stream.DeltaApplySpecCursor = specs.Length;
            em.SetComponentData(streamEntity, stream);
        }
    }

    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SAttributeDeltaApply))]
    [UpdateBefore(typeof(SAscDestroyRequest))]
    public partial struct STypedSimulationFactProjection : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectCommandSpecStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<CEffectCommandSpecStream>();
            EffectCommandSpecStream.EnsureBuffers(em, streamEntity);

            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var deltas = em.GetBuffer<BAttributeDelta>(streamEntity);
            var facts = em.GetBuffer<BTypedSimulationFact>(streamEntity);

            var start = ClampCursor(stream.FactProjectionDeltaCursor, deltas.Length);
            for (var i = start; i < deltas.Length; i++)
            {
                var delta = deltas[i];
                facts.Add(new BTypedSimulationFact
                {
                    Sequence = Allocate(ref stream.NextFactSequence),
                    SourceCommandSequence = delta.SourceCommandSequence,
                    SourceSpecSequence = delta.SourceSpecSequence,
                    SourceDeltaSequence = delta.Sequence,
                    Frame = delta.Frame,
                    EventType = EGameplayEventType.AttributeBaseValueChanged,
                    Domain = EGameplayFactDomain.Attribute,
                    Category = EGameplayFactCategory.StateChange,
                    Severity = EGameplayFactSeverity.Info,
                    SourceAsc = delta.SourceAsc,
                    TargetAsc = delta.TargetAsc,
                    SourceAbility = delta.SourceAbility,
                    SourceEffect = delta.SourceEffect,
                    GameplayEffectCode = delta.GameplayEffectCode,
                    ContextId = delta.ContextId,
                    ParentContextId = delta.ParentContextId,
                    AttrSetCode = delta.AttrSetCode,
                    AttributeCode = delta.AttributeCode,
                    Value = delta.Magnitude,
                    OldValue = delta.OldValue,
                    NewValue = delta.NewValue,
                });
            }

            stream.FactProjectionDeltaCursor = deltas.Length;
            em.SetComponentData(streamEntity, stream);
        }
    }

    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(STypedSimulationFactProjection))]
    [UpdateBefore(typeof(SAscDestroyRequest))]
    public partial struct STypedSimulationFactEventBridge : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectCommandSpecStream>();
            state.RequireForUpdate<CGameplayEventBus>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity) || !em.HasBuffer<BAttributeChangeEvent>(eventBusEntity))
                return;

            var eventBusWriter = EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity);
            if (!eventBusWriter.IsCreated)
                return;

            var streamEntity = SystemAPI.GetSingletonEntity<CEffectCommandSpecStream>();
            EffectCommandSpecStream.EnsureBuffers(em, streamEntity);

            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var facts = em.GetBuffer<BTypedSimulationFact>(streamEntity);

            try
            {
                var start = ClampCursor(stream.EventBridgeFactCursor, facts.Length);
                for (var i = start; i < facts.Length; i++)
                {
                    var fact = facts[i];
                    if (fact.Domain != EGameplayFactDomain.Attribute
                        || fact.EventType != EGameplayEventType.AttributeBaseValueChanged
                        || fact.TargetAsc == Entity.Null)
                    {
                        continue;
                    }

                    eventBusWriter.EnqueueAttributeChangeEvent(new BAttributeChangeEvent
                    {
                        ASC = fact.TargetAsc,
                        SourceAsc = fact.SourceAsc,
                        SourceAbility = fact.SourceAbility,
                        GameplayEffect = fact.SourceEffect,
                        SourceFactSequence = fact.Sequence,
                        EventCode = fact.GameplayEffectCode,
                        AttrSetCode = fact.AttrSetCode,
                        AttributeCode = fact.AttributeCode,
                        OldValue = fact.OldValue,
                        NewValue = fact.NewValue,
                        ContextId = fact.ContextId,
                        IsBaseValue = true,
                    });
                }

                stream.EventBridgeFactCursor = facts.Length;
                em.SetComponentData(streamEntity, stream);
            }
            finally
            {
                eventBusWriter.Dispose();
            }
        }
    }

    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(STypedSimulationFactEventBridge))]
    [UpdateBefore(typeof(SAscDestroyRequest))]
    public partial struct SInstantEffectCueRequestProjection : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectCommandSpecStream>();
            state.RequireForUpdate<CGameplayEventBus>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBusEntity))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBusEntity) || !em.HasComponent<CGameplayEventBus>(eventBusEntity))
                return;

            var eventBusWriter = EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity);
            if (!eventBusWriter.IsCreated)
                return;

            var streamEntity = SystemAPI.GetSingletonEntity<CEffectCommandSpecStream>();
            EffectCommandSpecStream.EnsureBuffers(em, streamEntity);

            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var specs = em.GetBuffer<BInstantEffectSpec>(streamEntity);
            var facts = em.GetBuffer<BTypedSimulationFact>(streamEntity);

            try
            {
                var start = ClampCursor(stream.CueProjectionSpecCursor, specs.Length);
                for (var i = start; i < specs.Length; i++)
                {
                    var spec = specs[i];
                    if (spec.CueRequestOnApplyCode <= 0
                        || spec.TargetAsc == Entity.Null)
                    {
                        continue;
                    }

                    var factSequence = Allocate(ref stream.NextFactSequence);
                    facts.Add(new BTypedSimulationFact
                    {
                        Sequence = factSequence,
                        SourceCommandSequence = spec.SourceCommandSequence,
                        SourceSpecSequence = spec.Sequence,
                        Frame = spec.Frame,
                        EventType = EGameplayEventType.CueRequested,
                        Domain = EGameplayFactDomain.Cue,
                        Category = EGameplayFactCategory.Request,
                        Severity = EGameplayFactSeverity.Info,
                        SourceAsc = spec.SourceAsc,
                        TargetAsc = spec.TargetAsc,
                        SourceAbility = spec.SourceAbility,
                        SourceEffect = spec.SourceEffect,
                        GameplayEffectCode = spec.GameplayEffectCode,
                        ContextId = spec.ContextId,
                        ParentContextId = spec.ParentContextId,
                        EventCode = (int)EGameplayCueEvent.OnApply,
                        ReasonCode = spec.CueRequestOnApplyCode,
                    });

                    eventBusWriter.EnqueueCueRequest(new BCueRequest
                    {
                        TargetAsc = spec.TargetAsc,
                        SourceAsc = spec.SourceAsc,
                        SourceAbility = spec.SourceAbility,
                        GameplayEffect = Entity.Null,
                        SourceEntity = spec.SourceAbility,
                        SourceType = CueSourceType.GameplayEffect,
                        CueEntity = Entity.Null,
                        SourceFactSequence = factSequence,
                        ContextId = spec.ContextId,
                        ReasonCode = spec.CueRequestOnApplyCode,
                        CueEvent = EGameplayCueEvent.OnApply,
                    });

                    eventBusWriter.EnqueueGameplayEvent(new BGameplayEvent
                    {
                        SourceFactSequence = factSequence,
                        Type = EGameplayEventType.CueRequested,
                        SourceAsc = spec.SourceAsc,
                        TargetAsc = spec.TargetAsc,
                        SourceAbility = spec.SourceAbility,
                        ContextId = spec.ContextId,
                        EventCode = (int)EGameplayCueEvent.OnApply,
                        ReasonCode = spec.CueRequestOnApplyCode,
                    });
                }

                stream.CueProjectionSpecCursor = specs.Length;
                em.SetComponentData(streamEntity, stream);
            }
            finally
            {
                eventBusWriter.Dispose();
            }
        }
    }

    [UpdateInGroup(typeof(GASCommandGroup))]
    [UpdateAfter(typeof(SInstantEffectSpecBuild))]
    public partial struct SActiveEffectMutationApply : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CEffectCommandSpecStream>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var streamEntity = SystemAPI.GetSingletonEntity<CEffectCommandSpecStream>();
            var stream = em.GetComponentData<CEffectCommandSpecStream>(streamEntity);
            var commands = em.GetBuffer<BEffectCommand>(streamEntity);
            var mutations = em.GetBuffer<BActiveEffectMutation>(streamEntity);
            var setByCallerValues = em.GetBuffer<BEffectCommandSetByCallerValue>(streamEntity);
            var frame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            var eventBusEntity = SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var eventWriter = eventBusEntity != Entity.Null
                ? EventBusHelper.BeginGameplayEventBatch(em, eventBusEntity)
                : default;

            try
            {
                var start = ClampCursor(stream.ActiveMutationCommandCursor, commands.Length);
                for (var i = start; i < commands.Length; i++)
                {
                    var command = commands[i];
                    if (command.Kind != EEffectCommandKind.ActiveMutation
                        || !CanBuildActiveEffectMutation(em, in command))
                    {
                        continue;
                    }

                    ApplyActiveMutation(em, mutations, in command, setByCallerValues, frame, ref eventWriter);
                }

                stream.ActiveMutationCommandCursor = commands.Length;
                em.SetComponentData(streamEntity, stream);
            }
            finally
            {
                eventWriter.Dispose();
            }
        }

        private static void ApplyActiveMutation(
            EntityManager em,
            DynamicBuffer<BActiveEffectMutation> mutations,
            in BEffectCommand command,
            DynamicBuffer<BEffectCommandSetByCallerValue> setByCallerValues,
            int frame,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            var effect = GameplayEffectConfigRegistry.CreateRuntimeEffectInstance(
                em,
                command.GameplayEffectCode,
                new ConfigRegistryReferenceContext(
                    ConfigRegistryConfigKind.GameplayEffect,
                    command.GameplayEffectCode,
                    ConfigRegistryReferenceKind.ApplyGameplayEffectRequest));
            if (effect == Entity.Null || !em.Exists(effect))
                return;

            em.SetName(effect, $"GE_{command.GameplayEffectCode}_{effect.Index}");
            var context = CreateContext(in command);
            SetOrAdd(em, effect, context);
            SetOrAdd(em, effect, new CEffectSpecData
            {
                GameplayEffectCode = command.GameplayEffectCode,
                Level = command.Level,
                StackCount = 1,
                DurationFrameOverride = command.DurationFrameOverride,
            });
            CopySetByCallerValues(em, effect, command, setByCallerValues);

            var spec = em.GetComponentData<CEffectSpecData>(effect);
            SetOrAdd(em, effect, new CEffectLifecycle
            {
                State = EGameplayEffectLifecycleState.Active,
                PreviousState = EGameplayEffectLifecycleState.PendingApply,
                StateStartFrame = frame,
            });

            ApplyRemoveGameplayEffectsWithTags(em, effect, command.TargetAsc);
            EffectRuntimeUtility.ApplyResolvedModifiersAsActive(
                em,
                effect,
                in context,
                in spec,
                ref eventWriter);
            EnsureIndefiniteActiveRuntime(em, effect);

            var hasDuration = em.HasComponent<CDurationRuntime>(effect);
            if (hasDuration)
            {
                var duration = em.GetComponentData<CDurationRuntime>(effect);
                if (command.DurationFrameOverride > 0)
                    duration.ResolvedDuration = command.DurationFrameOverride;
                duration.Active = true;
                duration.ActiveTime = frame;
                duration.LastActiveTime = frame;
                duration.RemainingTime = duration.ResolvedDuration;
                em.SetComponentData(effect, duration);
                if (em.HasComponent<CPeriodRuntime>(effect))
                    em.SetComponentData(effect, new CPeriodRuntime { StartTime = frame });

                AddTargetEffect(em, command.TargetAsc, effect);
                ApplyGrantedTags(em, command.TargetAsc, effect, ref eventWriter);
                ActiveEffectStore.TryUpsertDurationEffect(
                    em,
                    effect,
                    in context,
                    in duration,
                    EActiveEffectSlotState.Active,
                    frame);
            }

            mutations.Add(new BActiveEffectMutation
            {
                Sequence = command.Sequence,
                SourceCommandSequence = command.Sequence,
                Frame = frame,
                Kind = EActiveEffectMutationKind.Apply,
                ActiveEffect = effect,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                SourceEffect = command.SourceEffect,
                GameplayEffectCode = command.GameplayEffectCode,
                ContextId = command.ContextId,
                ParentContextId = command.ParentContextId,
                StackCount = 1,
                DurationFrameOverride = command.DurationFrameOverride,
                PeriodFrame = em.HasComponent<CPeriodDefinition>(effect)
                    ? em.GetComponentData<CPeriodDefinition>(effect).Period
                    : 0,
            });

            EnqueueGameplayEffectEvents(ref eventWriter, effect, in command);
            if (!hasDuration && em.Exists(effect))
                EffectRuntimeUtility.CleanupActiveEffect(em, effect, ref eventWriter);
        }

        private static void EnsureIndefiniteActiveRuntime(EntityManager em, Entity effect)
        {
            if (em.HasComponent<CDurationRuntime>(effect)
                || !HasPersistentActiveState(em, effect))
            {
                return;
            }

            em.AddComponentData(effect, new CDurationRuntime
            {
                ResolvedDuration = 0,
                RemainingTime = 0,
            });
        }

        private static void CopySetByCallerValues(
            EntityManager em,
            Entity effect,
            in BEffectCommand command,
            DynamicBuffer<BEffectCommandSetByCallerValue> setByCallerValues)
        {
            if (command.SetByCallerCount <= 0)
                return;

            var values = em.HasBuffer<BSetByCallerValue>(effect)
                ? em.GetBuffer<BSetByCallerValue>(effect)
                : em.AddBuffer<BSetByCallerValue>(effect);
            values.Clear();

            var start = command.SetByCallerStart;
            var end = start + command.SetByCallerCount;
            if (start < 0)
                start = 0;
            if (end > setByCallerValues.Length)
                end = setByCallerValues.Length;

            for (var i = start; i < end; i++)
            {
                var value = setByCallerValues[i];
                if (value.CommandSequence != command.Sequence)
                    continue;

                values.Add(new BSetByCallerValue
                {
                    Key = value.Key,
                    Value = value.Value,
                });
            }
        }

        private static CEffectContext CreateContext(in BEffectCommand command)
        {
            return new CEffectContext
            {
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                SourceEffect = command.SourceEffect,
                Instigator = command.Instigator,
                Causer = command.Causer,
                ContextId = command.ContextId,
                ParentContextId = command.ParentContextId,
                TargetDataKind = command.TargetDataKind,
            };
        }

        private static void AddTargetEffect(EntityManager em, Entity targetAsc, Entity effect)
        {
            if (targetAsc == Entity.Null || !em.Exists(targetAsc))
                return;
            if (!em.HasBuffer<BGameplayEffect>(targetAsc))
                em.AddBuffer<BGameplayEffect>(targetAsc);

            var effects = em.GetBuffer<BGameplayEffect>(targetAsc);
            for (var i = 0; i < effects.Length; i++)
            {
                if (effects[i].GameplayEffect == effect)
                    return;
            }

            effects.Add(new BGameplayEffect { GameplayEffect = effect });
        }

        private static void ApplyGrantedTags(
            EntityManager em,
            Entity targetAsc,
            Entity effect,
            ref EventBusHelper.GameplayEventBusWriter eventWriter)
        {
            if (targetAsc == Entity.Null
                || !em.Exists(targetAsc)
                || !em.HasComponent<CTagMask>(targetAsc)
                || !em.HasBuffer<BTempTagSource>(targetAsc)
                || !TryGetGrantedTags(em, effect, out var grantedTags)
                || grantedTags.IsEmpty)
            {
                return;
            }

            var mask = em.GetComponentData<CTagMask>(targetAsc);
            var sources = em.GetBuffer<BTempTagSource>(targetAsc);
            for (var tagIndex = 0; tagIndex < CTagMask.Capacity; tagIndex++)
            {
                if (!grantedTags.HasTag(tagIndex) || HasTempTagSource(sources, tagIndex, effect))
                    continue;

                mask.AddTag(tagIndex);
                sources.Add(new BTempTagSource
                {
                    TagIndex = tagIndex,
                    Source = effect,
                });
                if (eventWriter.IsCreated)
                {
                    eventWriter.EnqueueTagChangeEvent(new BTagChangeEvent
                    {
                        ASC = targetAsc,
                        TagIndex = tagIndex,
                        Added = true,
                    });
                }
            }

            em.SetComponentData(targetAsc, mask);
        }

        private static void ApplyRemoveGameplayEffectsWithTags(
            EntityManager em,
            Entity effect,
            Entity targetAsc)
        {
            if (targetAsc == Entity.Null
                || !em.Exists(targetAsc)
                || !em.HasComponent<CRemoveEffectWithTags>(effect)
                || !em.HasBuffer<BTempTagSource>(targetAsc))
            {
                return;
            }

            var requirement = em.GetComponentData<CRemoveEffectWithTags>(effect).requirement;
            if (requirement.IsEmpty)
                return;

            for (var tagIndex = 0; tagIndex < CTagMask.Capacity; tagIndex++)
            {
                if (!ShouldRemoveTag(requirement, tagIndex))
                    continue;

                EffectRuntimeUtility.RemoveActiveGameplayEffectsWithTags(em, targetAsc, tagIndex);
            }
        }

        private static bool TryGetGrantedTags(EntityManager em, Entity effect, out CTagMask grantedTags)
        {
            if (em.HasComponent<CEffectGrantedTags>(effect))
            {
                grantedTags = em.GetComponentData<CEffectGrantedTags>(effect).Tags;
                return !grantedTags.IsEmpty;
            }

            grantedTags = default;
            if (!em.HasBuffer<BGrantedTagConfig>(effect))
                return false;

            var tags = em.GetBuffer<BGrantedTagConfig>(effect);
            for (var i = 0; i < tags.Length; i++)
                grantedTags.AddTag(tags[i].TagIndex);
            return !grantedTags.IsEmpty;
        }

        private static bool HasPersistentActiveState(EntityManager em, Entity effect)
        {
            return em.HasComponent<CPeriodRuntime>(effect)
                   || em.HasComponent<CStackingRuntime>(effect)
                   || em.HasBuffer<BGrantedAbilityConfig>(effect)
                   || (em.HasBuffer<BResolvedModifier>(effect)
                       && em.GetBuffer<BResolvedModifier>(effect).Length > 0)
                   || (TryGetGrantedTags(em, effect, out var grantedTags)
                       && !grantedTags.IsEmpty);
        }

        private static bool HasTempTagSource(
            DynamicBuffer<BTempTagSource> sources,
            int tagIndex,
            Entity source)
        {
            for (var i = 0; i < sources.Length; i++)
            {
                if (sources[i].TagIndex == tagIndex && sources[i].Source == source)
                    return true;
            }

            return false;
        }

        private static bool ShouldRemoveTag(in TagRequirementMask requirement, int tagIndex)
        {
            return requirement.Any.HasTag(tagIndex) || requirement.All.HasTag(tagIndex);
        }

        private static void EnqueueGameplayEffectEvents(
            ref EventBusHelper.GameplayEventBusWriter writer,
            Entity effect,
            in BEffectCommand command)
        {
            if (!writer.IsCreated)
                return;

            writer.EnqueueGameplayEvent(new BGameplayEvent
            {
                Type = EGameplayEventType.GameplayEffectInstanced,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = effect,
                ContextId = command.ContextId,
                EventCode = command.GameplayEffectCode,
            });
            writer.EnqueueGameplayEvent(new BGameplayEvent
            {
                Type = EGameplayEventType.GameplayEffectApplied,
                SourceAsc = command.SourceAsc,
                TargetAsc = command.TargetAsc,
                SourceAbility = command.SourceAbility,
                GameplayEffect = effect,
                ContextId = command.ContextId,
                EventCode = command.GameplayEffectCode,
            });
        }

        private static void SetOrAdd<T>(EntityManager em, Entity entity, T component)
            where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(entity))
                em.SetComponentData(entity, component);
            else
                em.AddComponentData(entity, component);
        }
    }

    internal static class EffectCommandSpecStreamPhaseUtility
    {
        public static bool CanBuildSimpleInstantSpec(
            EntityManager em,
            in BEffectCommand command)
        {
            return TryGetSimpleInstantDefinition(em, in command, out _);
        }

        public static bool CanBuildActiveEffectMutation(
            EntityManager em,
            in BEffectCommand command)
        {
            if (command.GameplayEffectCode <= 0
                || command.TargetAsc == Entity.Null
                || IsUnavailableAsc(em, command.SourceAsc)
                || IsUnavailableAsc(em, command.TargetAsc)
                || !GameplayEffectConfigRegistry.TryGetOrCreateStaticDefinitionBlob(
                    em,
                    command.GameplayEffectCode,
                    out var blob,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.GameplayEffect,
                        command.GameplayEffectCode,
                        ConfigRegistryReferenceKind.ApplyGameplayEffectRequest)))
            {
                return false;
            }

            ref var definition = ref blob.Value;
            if (CanApplySimpleInstantSpec(ref definition))
                return false;

            return definition.HasDuration
                   || definition.HasPeriod
                   || definition.HasStacking
                   || definition.HasRemoveGameplayEffectsWithTags
                   || definition.HasOngoingRequiredTags
                   || !definition.GrantedTags.IsEmpty
                   || definition.GrantedAbilities.Length > 0
                   || definition.Modifiers.Length == 0;
        }

        public static bool TryGetSimpleInstantDefinition(
            EntityManager em,
            in BEffectCommand command,
            out BlobAssetReference<GEStaticDefinitionBlob> blob)
        {
            if (command.GameplayEffectCode <= 0
                || command.TargetAsc == Entity.Null
                || IsUnavailableAsc(em, command.SourceAsc)
                || IsUnavailableAsc(em, command.TargetAsc)
                || !GameplayEffectConfigRegistry.TryGetOrCreateStaticDefinitionBlob(
                    em,
                    command.GameplayEffectCode,
                    out blob,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.GameplayEffect,
                        command.GameplayEffectCode,
                        ConfigRegistryReferenceKind.ApplyGameplayEffectRequest)))
            {
                blob = default;
                return false;
            }

            ref var definition = ref blob.Value;
            return CanApplySimpleInstantSpec(ref definition);
        }

        public static bool CanApplySimpleInstantSpec(ref GEStaticDefinitionBlob definition)
        {
            if (definition.HasDuration
                || definition.HasPeriod
                || definition.HasStacking
                || definition.HasApplicationRequiredTags
                || definition.HasOngoingRequiredTags
                || definition.HasRemoveGameplayEffectsWithTags
                || definition.HasImmunityTags
                || !definition.GrantedTags.IsEmpty
                || definition.GrantedAbilities.Length != 0
                || definition.Modifiers.Length == 0)
            {
                return false;
            }

            for (var i = 0; i < definition.Modifiers.Length; i++)
            {
                if (!CanResolveSimpleInstantMagnitude(in definition.Modifiers[i]))
                    return false;
            }

            return true;
        }

        public static bool CanResolveSimpleInstantMagnitude(in GEModifierDefinition modifier)
        {
            return modifier.MagnitudeSource == EMagnitudeSource.Constant
                   || modifier.MagnitudeSource == EMagnitudeSource.SetByCaller;
        }

        public static void ApplySimpleInstantSpec(
            EntityManager em,
            ref CEffectCommandSpecStream stream,
            in BInstantEffectSpec spec,
            DynamicBuffer<BEffectCommandSetByCallerValue> setByCallerValues,
            DynamicBuffer<BAttributeDelta> deltas)
        {
            if (spec.TargetAsc == Entity.Null
                || !em.Exists(spec.TargetAsc)
                || !em.HasBuffer<BAttribute>(spec.TargetAsc)
                || !GameplayEffectConfigRegistry.TryGetOrCreateStaticDefinitionBlob(
                    em,
                    spec.GameplayEffectCode,
                    out var blob,
                    new ConfigRegistryReferenceContext(
                        ConfigRegistryConfigKind.GameplayEffect,
                        spec.GameplayEffectCode,
                        ConfigRegistryReferenceKind.ApplyGameplayEffectRequest)))
            {
                return;
            }

            ref var definition = ref blob.Value;
            if (!CanApplySimpleInstantSpec(ref definition))
                return;

            var attributes = em.GetBuffer<BAttribute>(spec.TargetAsc);
            for (var i = 0; i < definition.Modifiers.Length; i++)
            {
                var modifier = definition.Modifiers[i];
                if (!TryResolveSimpleInstantMagnitude(
                        in spec,
                        setByCallerValues,
                        in modifier,
                        out var magnitude))
                {
                    continue;
                }

                var attrIndex = attributes.IndexOfAttribute(modifier.AttrSetCode, modifier.AttributeCode);
                if (attrIndex < 0)
                    continue;

                var attribute = attributes[attrIndex];
                var oldValue = attribute.BaseValue;
                var oldCurrentValue = attribute.CurrentValue;
                var newValue = AttributeHelper.ApplyModifier(attribute.BaseValue, modifier.Operation, magnitude);
                attribute.CurrentValue = newValue;
                AttributeHelper.Clamp(ref attribute);
                newValue = attribute.CurrentValue;
                attribute.BaseValue = newValue;
                attribute.CurrentValue = newValue;

                if (newValue != oldValue)
                {
                    attribute.Dirty = true;
                    if (oldCurrentValue != attribute.CurrentValue)
                    {
                        attribute.PreviousCurrentValue = oldCurrentValue;
                        attribute.CurrentValueChangePending = true;
                    }

                    deltas.Add(new BAttributeDelta
                    {
                        Sequence = Allocate(ref stream.NextDeltaSequence),
                        SourceCommandSequence = spec.SourceCommandSequence,
                        SourceSpecSequence = spec.Sequence,
                        Frame = spec.Frame,
                        SourceAsc = spec.SourceAsc,
                        TargetAsc = spec.TargetAsc,
                        SourceAbility = spec.SourceAbility,
                        SourceEffect = spec.SourceEffect,
                        GameplayEffectCode = spec.GameplayEffectCode,
                        ContextId = spec.ContextId,
                        ParentContextId = spec.ParentContextId,
                        AttrSetCode = modifier.AttrSetCode,
                        AttributeCode = modifier.AttributeCode,
                        Op = modifier.Operation,
                        ValueKind = EAttributeDeltaValueKind.BaseValue,
                        Magnitude = magnitude,
                        OldValue = oldValue,
                        NewValue = newValue,
                    });
                }

                attributes[attrIndex] = attribute;
            }
        }

        public static bool TryResolveSimpleInstantMagnitude(
            in BInstantEffectSpec spec,
            DynamicBuffer<BEffectCommandSetByCallerValue> setByCallerValues,
            in GEModifierDefinition modifier,
            out float magnitude)
        {
            switch (modifier.MagnitudeSource)
            {
                case EMagnitudeSource.Constant:
                    magnitude = ApplySimpleInstantMagnitudeTransform(modifier.Magnitude, in modifier);
                    return true;
                case EMagnitudeSource.SetByCaller:
                    var value = modifier.FallbackMagnitude;
                    var start = spec.SetByCallerStart;
                    var end = start + spec.SetByCallerCount;
                    if (start < 0)
                        start = 0;
                    if (end > setByCallerValues.Length)
                        end = setByCallerValues.Length;

                    for (var i = start; i < end; i++)
                    {
                        var setByCaller = setByCallerValues[i];
                        if (setByCaller.Key != modifier.MagnitudeKey
                            || setByCaller.CommandSequence != spec.SourceCommandSequence)
                        {
                            continue;
                        }

                        value = setByCaller.Value;
                        break;
                    }

                    magnitude = ApplySimpleInstantMagnitudeTransform(value, in modifier);
                    return true;
                default:
                    magnitude = 0f;
                    return false;
            }
        }

        public static float ApplySimpleInstantMagnitudeTransform(
            float rawMagnitude,
            in GEModifierDefinition modifier)
        {
            var coefficient = modifier.Coefficient == 0f ? 1f : modifier.Coefficient;
            return ((rawMagnitude + modifier.PreAdd) * coefficient) + modifier.PostAdd;
        }

        public static void AssignSpecSequence(
            DynamicBuffer<BEffectCommandSetByCallerValue> setByCallerValues,
            int start,
            int count,
            int commandSequence,
            int specSequence)
        {
            if (count <= 0)
                return;

            var end = start + count;
            if (start < 0)
                start = 0;
            if (end > setByCallerValues.Length)
                end = setByCallerValues.Length;

            for (var i = start; i < end; i++)
            {
                var value = setByCallerValues[i];
                if (value.CommandSequence != commandSequence)
                    continue;

                value.SpecSequence = specSequence;
                setByCallerValues[i] = value;
            }
        }

        public static int ClampCursor(int cursor, int length)
        {
            if (cursor < 0)
                return 0;
            return cursor > length ? length : cursor;
        }

        public static int Allocate(ref int next)
        {
            if (next <= 0)
                next = 1;

            return next++;
        }

        private static bool IsUnavailableAsc(EntityManager em, Entity asc)
        {
            return asc != Entity.Null && (!em.Exists(asc) || em.HasComponent<CAscDestroying>(asc));
        }
    }
}
