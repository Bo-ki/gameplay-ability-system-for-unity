using Unity.Burst;
using Unity.Collections;
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
    [UpdateInGroup(typeof(GASCoreSimulationSystemGroup))]
    [UpdateAfter(typeof(GEExecutionCalculationOutputModifierSystem))]
    [UpdateAfter(typeof(GASAttributeModifierDeltaApplySystem))]
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
            var frame = SystemAPI.GetSingleton<GlobalTimer>().Frame;

            state.Dependency = new GameplayFactProjectionJob
            {
                StreamEntity = streamEntity,
                Frame = frame,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                DeltaLookup = SystemAPI.GetBufferLookup<AttributeModifierBuffer>(isReadOnly: true),
                SpecLookup = SystemAPI.GetBufferLookup<GEEffectSpecBuffer>(isReadOnly: true),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: false),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private struct GameplayFactProjectionJob : IJob
        {
            public Entity StreamEntity;
            public int Frame;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            public BufferLookup<AttributeModifierBuffer> DeltaLookup;
            public BufferLookup<GEEffectSpecBuffer> SpecLookup;
            public BufferLookup<GameplayEventBuffer> FactLookup;

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

                var deltaStart = ClampCursor(stream.FactProjectionDeltaCursor, deltas.Length);
                for (var i = deltaStart; i < deltas.Length; i++)
                {
                    var delta = deltas[i];
                    if (!AttributeModifierBufferFlags.ShouldProjectFact(delta.Flags))
                        continue;

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
                }

                var cueStart = ClampCursor(stream.CueProjectionSpecCursor, specs.Length);
                for (var i = cueStart; i < specs.Length; i++)
                    ProjectCueRequest(specs[i], facts, ref stream);

                stream.FactProjectionDeltaCursor = deltas.Length;
                stream.CueProjectionSpecCursor = specs.Length;
                StreamLookup[StreamEntity] = stream;
            }
        }

        private static void ProjectCueRequest(
            in GEEffectSpecBuffer spec,
            DynamicBuffer<GameplayEventBuffer> facts,
            ref GEEffectCommandStreamComponent stream)
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
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(PresentationOutboxProjectionSystem))]
    [UpdateBefore(typeof(ReplayLogSystem))]
    [BurstCompile]
    public partial struct GameplayFactBoundaryProjectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GEEffectCommandStreamComponent>();
            state.RequireForUpdate<GameplayEventBusComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GEEffectCommandStreamComponent>(out var streamEntity)
                || !SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var eventBusEntity))
            {
                return;
            }

            state.Dependency = new GameplayFactBoundaryProjectionJob
            {
                StreamEntity = streamEntity,
                EventBusEntity = eventBusEntity,
                StreamLookup = SystemAPI.GetComponentLookup<GEEffectCommandStreamComponent>(isReadOnly: false),
                FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>(isReadOnly: true),
                AttributeEventLookup = SystemAPI.GetBufferLookup<AttributeChangeEventBuffer>(isReadOnly: false),
                CueRequestLookup = SystemAPI.GetBufferLookup<CueRequestBuffer>(isReadOnly: false),
                TagChangeEventLookup = SystemAPI.GetBufferLookup<TagChangeEventBuffer>(isReadOnly: false),
            }.Schedule(state.Dependency);
        }

        [BurstCompile]
        private struct GameplayFactBoundaryProjectionJob : IJob
        {
            public Entity StreamEntity;
            public Entity EventBusEntity;
            public ComponentLookup<GEEffectCommandStreamComponent> StreamLookup;
            [ReadOnly] public BufferLookup<GameplayEventBuffer> FactLookup;
            public BufferLookup<AttributeChangeEventBuffer> AttributeEventLookup;
            public BufferLookup<CueRequestBuffer> CueRequestLookup;
            public BufferLookup<TagChangeEventBuffer> TagChangeEventLookup;

            public void Execute()
            {
                if (StreamEntity == Entity.Null
                    || EventBusEntity == Entity.Null
                    || !StreamLookup.HasComponent(StreamEntity)
                    || !FactLookup.HasBuffer(StreamEntity))
                {
                    return;
                }

                var stream = StreamLookup[StreamEntity];
                var facts = FactLookup[StreamEntity];
                var start = ClampCursor(stream.EventBridgeFactCursor, facts.Length);

                var hasAttributeEvents = AttributeEventLookup.HasBuffer(EventBusEntity);
                var hasCueRequests = CueRequestLookup.HasBuffer(EventBusEntity);
                var hasTagChanges = TagChangeEventLookup.HasBuffer(EventBusEntity);
                if (!hasAttributeEvents && !hasCueRequests && !hasTagChanges)
                {
                    stream.EventBridgeFactCursor = facts.Length;
                    StreamLookup[StreamEntity] = stream;
                    return;
                }

                var attributeEvents = hasAttributeEvents
                    ? AttributeEventLookup[EventBusEntity]
                    : default;
                var cueRequests = hasCueRequests
                    ? CueRequestLookup[EventBusEntity]
                    : default;
                var tagChanges = hasTagChanges
                    ? TagChangeEventLookup[EventBusEntity]
                    : default;

                for (var i = start; i < facts.Length; i++)
                {
                    var fact = facts[i];
                    if (hasAttributeEvents
                        && TryCreateAttributeChangeEvent(in fact, out var attributeEvent))
                    {
                        attributeEvents.Add(attributeEvent);
                        continue;
                    }

                    if (hasCueRequests
                        && TryCreateCueRequest(in fact, out var cueRequest))
                    {
                        cueRequests.Add(cueRequest);
                        continue;
                    }

                    if (hasTagChanges
                        && TryCreateTagChange(in fact, out var tagChange))
                    {
                        tagChanges.Add(tagChange);
                    }
                }

                stream.EventBridgeFactCursor = facts.Length;
                StreamLookup[StreamEntity] = stream;
            }
        }

        private static bool TryCreateAttributeChangeEvent(
            in GameplayEventBuffer fact,
            out AttributeChangeEventBuffer evt)
        {
            evt = default;

            if (fact.Domain != EGameplayFactDomain.Attribute
                || fact.EventType != EGameplayEventType.AttributeBaseValueChanged
                || fact.TargetAsc == Entity.Null)
            {
                return false;
            }

            evt = new AttributeChangeEventBuffer
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
            };
            return true;
        }

        private static bool TryCreateCueRequest(
            in GameplayEventBuffer fact,
            out CueRequestBuffer request)
        {
            request = default;

            if (fact.Domain != EGameplayFactDomain.Cue
                || fact.EventType != EGameplayEventType.CueRequested
                || fact.TargetAsc == Entity.Null)
            {
                return false;
            }

            request = new CueRequestBuffer
            {
                TargetAsc = fact.TargetAsc,
                SourceAsc = fact.SourceAsc,
                SourceAbility = fact.SourceAbility,
                GameplayEffect = fact.SourceEffect,
                SourceEntity = fact.SourceAbility,
                SourceType = CueSourceType.GameplayEffect,
                CueEntity = Entity.Null,
                SourceFactSequence = fact.Sequence,
                ContextId = fact.ContextId,
                ReasonCode = fact.ReasonCode,
                CueEvent = (EGameplayCueEvent)fact.EventCode,
            };
            return true;
        }

        private static bool TryCreateTagChange(
            in GameplayEventBuffer fact,
            out TagChangeEventBuffer evt)
        {
            evt = default;

            if (fact.Domain != EGameplayFactDomain.Tag
                || fact.EventType != EGameplayEventType.TagChanged
                || fact.TargetAsc == Entity.Null)
            {
                return false;
            }

            evt = new TagChangeEventBuffer
            {
                SourceFactSequence = fact.Sequence,
                ASC = fact.TargetAsc,
                TagIndex = fact.EventCode,
                Added = fact.ReasonCode != 0,
            };
            return true;
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
