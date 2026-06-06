using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using static GAS.Runtime.EffectCommandSpecStreamPhaseUtility;

namespace GAS.Runtime
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASFramePrepareSystemGroup))]
    [UpdateAfter(typeof(GASGlobalTimerSystem))]
    [BurstCompile]
    public partial struct GEEffectCommandSpecStreamFramePrepareSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GlobalTimer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;
            state.Dependency = new PrepareFrameLocalDataJob
            {
                StreamEntity = streamEntity,
                Frame = frame,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                CommandLookup = SystemAPI.GetBufferLookup<GEEffectCommandBuffer>(isReadOnly: false),
                SetByCallerLookup = SystemAPI.GetBufferLookup<GESetByCallerValueBuffer>(isReadOnly: false),
                SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(isReadOnly: false),
                DeltaLookup = SystemAPI.GetBufferLookup<AttributeModifierBuffer>(isReadOnly: false),
                MutationLookup = SystemAPI.GetBufferLookup<ActiveEffectMutationBuffer>(isReadOnly: false),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private struct PrepareFrameLocalDataJob : IJob
        {
            public Entity StreamEntity;
            public int Frame;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<GEEffectCommandBuffer> CommandLookup;
            public BufferLookup<GESetByCallerValueBuffer> SetByCallerLookup;
            public BufferLookup<GEEffectSpecBuffer> SpecLookup;
            public BufferLookup<AttributeModifierBuffer> DeltaLookup;
            public BufferLookup<ActiveEffectMutationBuffer> MutationLookup;
            public BufferLookup<GameplayEventBuffer> FactLookup;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !CommandLookup.HasBuffer(StreamEntity)
                    || !SetByCallerLookup.HasBuffer(StreamEntity)
                    || !SpecLookup.HasBuffer(StreamEntity)
                    || !DeltaLookup.HasBuffer(StreamEntity)
                    || !MutationLookup.HasBuffer(StreamEntity)
                    || !FactLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                EffectCommandSpecStream.PrepareFrameLocalData(
                    ref stream,
                    CommandLookup[StreamEntity],
                    SetByCallerLookup[StreamEntity],
                    SpecLookup[StreamEntity],
                    DeltaLookup[StreamEntity],
                    MutationLookup[StreamEntity],
                    FactLookup[StreamEntity],
                    Frame);
                StreamLookup[StreamEntity] = stream;
            }
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCommandResolveSystemGroup))]
    [UpdateAfter(typeof(AbilityTryActivateSystem))]
    public partial struct GEEffectCommandIngestSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEExecutionCalculationOutputModifierSystem))]
    [BurstCompile]
    public partial struct GameplayFactProjectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GlobalTimer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var streamEntity = SystemAPI.GetSingletonEntity<GEEffectCommandStreamComponent>();
            var eventBusEntity = SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var resolvedEventBus)
                ? resolvedEventBus
                : Entity.Null;
            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;

            state.Dependency = new GameplayFactProjectionJob
            {
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                Frame = frame,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                DeltaLookup = SystemAPI.GetBufferLookup<AttributeModifierBuffer>(isReadOnly: true),
                SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(isReadOnly: true),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),
                EventBusLookup = SystemAPI.GetComponentLookup<GameplayEventBusComponent>(isReadOnly: false),
                GameplayEventLookup = SystemAPI.GetBufferLookup<GameplayEventBusEventBuffer>(isReadOnly: false),
                AttributeEventLookup = SystemAPI.GetBufferLookup<AttributeChangeEventBuffer>(isReadOnly: false),
                CueRequestLookup = SystemAPI.GetBufferLookup<CueRequestBuffer>(isReadOnly: false),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private struct GameplayFactProjectionJob : IJob
        {
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public int Frame;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<AttributeModifierBuffer> DeltaLookup;
            public BufferLookup<GEEffectSpecBuffer> SpecLookup;
            public BufferLookup<GameplayEventBuffer> FactLookup;
            public ComponentLookup<GameplayEventBusComponent> EventBusLookup;
            public BufferLookup<GameplayEventBusEventBuffer> GameplayEventLookup;
            public BufferLookup<AttributeChangeEventBuffer> AttributeEventLookup;
            public BufferLookup<CueRequestBuffer> CueRequestLookup;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !DeltaLookup.HasBuffer(StreamEntity)
                    || !SpecLookup.HasBuffer(StreamEntity)
                    || !FactLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                var deltas = DeltaLookup[StreamEntity];
                var specs = SpecLookup[StreamEntity];
                var facts = FactLookup[StreamEntity];
                var eventWriter = CreateEventWriter();

                BridgeExistingAttributeFacts(facts, ClampCursor(stream.EventBridgeFactCursor, facts.Length), ref eventWriter);

                var deltaStart = ClampCursor(stream.FactProjectionDeltaCursor, deltas.Length);
                for (var i = deltaStart; i < deltas.Length; i++)
                {
                    var delta = deltas[i];
                    var fact = new GameplayEventBuffer
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
                    };
                    facts.Add(fact);
                    BridgeAttributeFact(in fact, ref eventWriter);
                }

                var cueStart = ClampCursor(stream.CueProjectionSpecCursor, specs.Length);
                for (var i = cueStart; i < specs.Length; i++)
                    ProjectCueRequest(specs[i], facts, ref stream, ref eventWriter);

                stream.FactProjectionDeltaCursor = deltas.Length;
                stream.EventBridgeFactCursor = facts.Length;
                stream.CueProjectionSpecCursor = specs.Length;
                StreamLookup[StreamEntity] = stream;
                FlushEventWriter(ref eventWriter);
            }

            private ProjectionEventWriter CreateEventWriter()
            {
                if (EventBusEntity == Entity.Null)
                    return default;

                var writer = new ProjectionEventWriter
                {
                    EventBusEntity = EventBusEntity,
                    Frame = Frame,
                };

                if (EventBusLookup.HasComponent(EventBusEntity))
                {
                    writer.HasEventBus = true;
                    writer.EventBus = EventBusLookup[EventBusEntity];
                }

                if (GameplayEventLookup.HasBuffer(EventBusEntity))
                {
                    writer.HasGameplayEvents = true;
                    writer.GameplayEvents = GameplayEventLookup[EventBusEntity];
                }

                if (AttributeEventLookup.HasBuffer(EventBusEntity))
                {
                    writer.HasAttributeEvents = true;
                    writer.AttributeEvents = AttributeEventLookup[EventBusEntity];
                }

                if (CueRequestLookup.HasBuffer(EventBusEntity))
                {
                    writer.HasCueRequests = true;
                    writer.CueRequests = CueRequestLookup[EventBusEntity];
                }

                return writer;
            }

            private void FlushEventWriter(ref ProjectionEventWriter writer)
            {
                if (writer.HasEventBus && writer.EventBusDirty)
                    EventBusLookup[writer.EventBusEntity] = writer.EventBus;
            }
        }

        private static void BridgeExistingAttributeFacts(
            DynamicBuffer<GameplayEventBuffer> facts,
            int start,
            ref ProjectionEventWriter eventBusWriter)
        {
            for (var i = start; i < facts.Length; i++)
                BridgeAttributeFact(facts[i], ref eventBusWriter);
        }

        private static void BridgeAttributeFact(
            in GameplayEventBuffer fact,
            ref ProjectionEventWriter eventBusWriter)
        {
            if (!eventBusWriter.IsCreated
                || fact.Domain != EGameplayFactDomain.Attribute
                || fact.EventType != EGameplayEventType.AttributeBaseValueChanged
                || fact.TargetAsc == Entity.Null)
            {
                return;
            }

            eventBusWriter.EnqueueAttributeChangeEvent(new AttributeChangeEventBuffer
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

        private static void ProjectCueRequest(
            in GEEffectSpecBuffer spec,
            DynamicBuffer<GameplayEventBuffer> facts,
            ref GEEffectCommandStreamComponent stream,
            ref ProjectionEventWriter eventBusWriter)
        {
            if (spec.CueRequestOnApplyCode <= 0
                || spec.TargetAsc == Entity.Null)
            {
                return;
            }

            var factSequence = Allocate(ref stream.NextFactSequence);
            facts.Add(new GameplayEventBuffer
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

            if (!eventBusWriter.IsCreated)
                return;

            eventBusWriter.EnqueueCueRequest(new CueRequestBuffer
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

            eventBusWriter.EnqueueGameplayEvent(new GameplayEventBusEventBuffer
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

        private struct ProjectionEventWriter
        {
            public Entity EventBusEntity;
            public int Frame;
            public bool HasEventBus;
            public bool EventBusDirty;
            public bool HasGameplayEvents;
            public bool HasAttributeEvents;
            public bool HasCueRequests;
            public GameplayEventBusComponent EventBus;
            public DynamicBuffer<GameplayEventBusEventBuffer> GameplayEvents;
            public DynamicBuffer<AttributeChangeEventBuffer> AttributeEvents;
            public DynamicBuffer<CueRequestBuffer> CueRequests;

            public bool IsCreated =>
                EventBusEntity != Entity.Null
                && (HasEventBus || HasGameplayEvents || HasAttributeEvents || HasCueRequests);

            public void EnqueueGameplayEvent(GameplayEventBusEventBuffer evt)
            {
                if (!HasGameplayEvents)
                    return;

                evt.Frame = Frame;
                if (HasEventBus)
                {
                    evt.Sequence = EventBus.NextSequence;
                    EventBus.NextSequence++;
                    EventBusDirty = true;
                }
                else
                {
                    evt.Sequence = 0;
                }

                GameplayEvents.Add(evt);
            }

            public void EnqueueAttributeChangeEvent(AttributeChangeEventBuffer evt)
            {
                if (HasAttributeEvents)
                    AttributeEvents.Add(evt);
            }

            public void EnqueueCueRequest(CueRequestBuffer evt)
            {
                if (HasCueRequests)
                    CueRequests.Add(evt);
            }
        }
    }


    internal static class EffectCommandSpecStreamPhaseUtility
    {
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
    }
}
