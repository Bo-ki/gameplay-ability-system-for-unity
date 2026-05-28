using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Appends current-tick simulation facts to a persistent debug/replay log.
    /// This is a side-channel recording sink, not gameplay input.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(PresentationOutboxProjectionSystem))]
    [UpdateBefore(typeof(CueRequestBridgeSystem))]
    public partial struct ReplayLogSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayEventBusComponent>();
            state.RequireForUpdate<GameplayEventLogSinkComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var eventBus)
                || !SystemAPI.TryGetSingletonEntity<GameplayEventLogSinkComponent>(out var logSink))
            {
                return;
            }

            var em = state.EntityManager;
            if (!em.Exists(eventBus)
                || !em.Exists(logSink)
                || !em.HasBuffer<ReplayLogEventBuffer>(logSink))
            {
                return;
            }

            var sinkState = em.GetComponentData<GameplayEventLogSinkComponent>(logSink);
            var currentFrame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            ResetProcessedCountsIfFrameChanged(ref sinkState, currentFrame);

            var log = em.GetBuffer<ReplayLogEventBuffer>(logSink);

            if (SystemAPI.TryGetSingletonEntity<GEEffectCommandStreamComponent>(out var streamEntity)
                && em.Exists(streamEntity)
                && em.HasBuffer<GameplayEventBuffer>(streamEntity))
            {
                ProjectTypedSimulationFacts(
                    em.GetBuffer<GameplayEventBuffer>(streamEntity),
                    log,
                    ref sinkState,
                    currentFrame);
            }

            if (em.HasBuffer<GameplayEventBusEventBuffer>(eventBus))
                ProjectGameplayEvents(em.GetBuffer<GameplayEventBusEventBuffer>(eventBus), log, ref sinkState, currentFrame);

            if (em.HasBuffer<AttributeChangeEventBuffer>(eventBus))
                ProjectAttributeEvents(em.GetBuffer<AttributeChangeEventBuffer>(eventBus), log, ref sinkState, currentFrame);

            if (em.HasBuffer<CueRequestBuffer>(eventBus))
                ProjectCueRequests(em.GetBuffer<CueRequestBuffer>(eventBus), log, ref sinkState, currentFrame);

            if (em.HasBuffer<TagChangeEventBuffer>(eventBus))
                ProjectTagEvents(em.GetBuffer<TagChangeEventBuffer>(eventBus), log, ref sinkState, currentFrame);

            if (em.HasBuffer<DamageEventBuffer>(eventBus))
                ProjectDamageEvents(em.GetBuffer<DamageEventBuffer>(eventBus), log, ref sinkState, currentFrame);

            GasReplaySinkPolicy.ApplyRetention(log, ref sinkState);
            em.SetComponentData(logSink, sinkState);
        }

        private static void ResetProcessedCountsIfFrameChanged(ref GameplayEventLogSinkComponent sinkState, int currentFrame)
        {
            if (sinkState.LastProjectedFrame == currentFrame)
                return;

            sinkState.LastProjectedFrame = currentFrame;
            sinkState.ProcessedGameplayEventCount = 0;
            sinkState.ProcessedAttributeEventCount = 0;
            sinkState.ProcessedCueRequestCount = 0;
            sinkState.ProcessedTagEventCount = 0;
            sinkState.ProcessedDamageEventCount = 0;
            sinkState.ProcessedTypedFactCount = 0;
        }

        private static void ProjectTypedSimulationFacts(
            DynamicBuffer<GameplayEventBuffer> facts,
            DynamicBuffer<ReplayLogEventBuffer> log,
            ref GameplayEventLogSinkComponent sinkState,
            int fallbackFrame)
        {
            for (var i = 0; i < facts.Length; i++)
            {
                var fact = facts[i];
                if (fact.Sequence <= 0
                    || fact.Sequence <= sinkState.LastProjectedTypedFactSequence)
                {
                    continue;
                }

                if (!TryCreateTypedReplayEvent(in fact, fallbackFrame, out var replayEvent))
                    continue;

                log.Add(CreateLogEvent(ref sinkState, replayEvent));
                if (fact.Sequence > sinkState.LastProjectedTypedFactSequence)
                    sinkState.LastProjectedTypedFactSequence = fact.Sequence;
            }

            sinkState.ProcessedTypedFactCount = facts.Length;
        }

        private static bool TryCreateTypedReplayEvent(
            in GameplayEventBuffer fact,
            int fallbackFrame,
            out ReplayLogEventBuffer replayEvent)
        {
            replayEvent = default;

            if (fact.TargetAsc == Entity.Null)
            {
                return false;
            }

            if (fact.Domain == EGameplayFactDomain.Attribute
                && fact.EventType == EGameplayEventType.AttributeBaseValueChanged)
            {
                replayEvent = new ReplayLogEventBuffer
                {
                    Kind = EDebugReplayEventKind.AttributeChange,
                    Frame = fact.Frame != 0 ? fact.Frame : fallbackFrame,
                    Sequence = fact.Sequence,
                    SourceAsc = fact.SourceAsc,
                    TargetAsc = fact.TargetAsc,
                    SourceAbility = fact.SourceAbility,
                    GameplayEffect = fact.SourceEffect,
                    ContextId = fact.ContextId,
                    EventCode = fact.GameplayEffectCode,
                    AttrSetCode = fact.AttrSetCode,
                    AttributeCode = fact.AttributeCode,
                    Value = fact.Value,
                    OldValue = fact.OldValue,
                    NewValue = fact.NewValue,
                    Flag = 1,
                };
                return true;
            }

            if (fact.Domain == EGameplayFactDomain.Cue
                && fact.EventType == EGameplayEventType.CueRequested)
            {
                replayEvent = new ReplayLogEventBuffer
                {
                    Kind = EDebugReplayEventKind.CueRequest,
                    Frame = fact.Frame != 0 ? fact.Frame : fallbackFrame,
                    Sequence = fact.Sequence,
                    GameplayEventType = fact.EventType,
                    CueEvent = (EGameplayCueEvent)fact.EventCode,
                    SourceAsc = fact.SourceAsc,
                    TargetAsc = fact.TargetAsc,
                    SourceAbility = fact.SourceAbility,
                    GameplayEffect = fact.SourceEffect,
                    SourceEntity = fact.SourceAbility,
                    CueSourceType = CueSourceType.GameplayEffect,
                    ContextId = fact.ContextId,
                    EventCode = fact.EventCode,
                    ReasonCode = fact.ReasonCode,
                    Flag = 1,
                };
                return true;
            }

            if (fact.Domain == EGameplayFactDomain.Damage)
            {
                replayEvent = new ReplayLogEventBuffer
                {
                    Kind = EDebugReplayEventKind.Damage,
                    Frame = fact.Frame != 0 ? fact.Frame : fallbackFrame,
                    Sequence = fact.Sequence,
                    GameplayEventType = fact.EventType,
                    SourceAsc = fact.SourceAsc,
                    TargetAsc = fact.TargetAsc,
                    SourceAbility = fact.SourceAbility,
                    GameplayEffect = fact.SourceEffect,
                    ContextId = fact.ContextId,
                    EventCode = ResolveGameplayEventCode(in fact),
                    ReasonCode = fact.ReasonCode,
                    AttrSetCode = fact.AttrSetCode,
                    AttributeCode = fact.AttributeCode,
                    DamageAmount = fact.Value,
                    Value = fact.Value,
                    Flag = 1,
                };
                return true;
            }

            if (!IsPresentationMarker(fact.EventType)
                && fact.Domain != EGameplayFactDomain.Unknown)
            {
                replayEvent = new ReplayLogEventBuffer
                {
                    Kind = EDebugReplayEventKind.GameplayEvent,
                    Frame = fact.Frame != 0 ? fact.Frame : fallbackFrame,
                    Sequence = fact.Sequence,
                    GameplayEventType = fact.EventType,
                    SourceAsc = fact.SourceAsc,
                    TargetAsc = fact.TargetAsc,
                    SourceAbility = fact.SourceAbility,
                    GameplayEffect = fact.SourceEffect,
                    ContextId = fact.ContextId,
                    EventCode = ResolveGameplayEventCode(in fact),
                    ReasonCode = fact.ReasonCode,
                    Value = fact.Value,
                    Flag = 1,
                };
                return true;
            }

            return false;
        }

        private static int ResolveGameplayEventCode(in GameplayEventBuffer fact)
        {
            return fact.EventCode != 0 ? fact.EventCode : fact.GameplayEffectCode;
        }

        private static void ProjectGameplayEvents(
            DynamicBuffer<GameplayEventBusEventBuffer> events,
            DynamicBuffer<ReplayLogEventBuffer> log,
            ref GameplayEventLogSinkComponent sinkState,
            int fallbackFrame)
        {
            var start = ClampProcessedCount(sinkState.ProcessedGameplayEventCount, events.Length);
            for (var i = start; i < events.Length; i++)
            {
                var evt = events[i];
                if (ShouldSkipMirroredTypedFact(
                        evt.SourceFactSequence,
                        sinkState.LastProjectedTypedFactSequence))
                {
                    continue;
                }

                if (IsPresentationMarker(evt.Type))
                    continue;

                log.Add(CreateLogEvent(ref sinkState, new ReplayLogEventBuffer
                {
                    Kind = EDebugReplayEventKind.GameplayEvent,
                    Frame = evt.Frame != 0 ? evt.Frame : fallbackFrame,
                    Sequence = evt.Sequence,
                    GameplayEventType = evt.Type,
                    SourceAsc = evt.SourceAsc,
                    TargetAsc = evt.TargetAsc,
                    SourceAbility = evt.SourceAbility,
                    GameplayEffect = evt.GameplayEffect,
                    RelatedAbility = evt.RelatedAbility,
                    ContextId = evt.ContextId,
                    EventCode = evt.EventCode,
                    ReasonCode = evt.ReasonCode,
                    RelatedAbilityCode = evt.RelatedAbilityCode,
                    Value = evt.Value,
                }));
            }

            sinkState.ProcessedGameplayEventCount = events.Length;
        }

        private static bool IsPresentationMarker(EGameplayEventType type)
        {
            return type == EGameplayEventType.AutoChessPresentationUiMarker
                   || type == EGameplayEventType.AutoChessPresentationVfxMarker
                   || type == EGameplayEventType.AutoChessPresentationSfxMarker
                   || type == EGameplayEventType.AutoChessPresentationFloatingTextMarker
                   || type == EGameplayEventType.AutoChessPresentationCueMarker
                   || type == EGameplayEventType.AutoChessPresentationSettlementMarker;
        }

        private static void ProjectAttributeEvents(
            DynamicBuffer<AttributeChangeEventBuffer> events,
            DynamicBuffer<ReplayLogEventBuffer> log,
            ref GameplayEventLogSinkComponent sinkState,
            int frame)
        {
            var start = ClampProcessedCount(sinkState.ProcessedAttributeEventCount, events.Length);
            for (var i = start; i < events.Length; i++)
            {
                var evt = events[i];
                if (ShouldSkipMirroredTypedFact(
                        evt.SourceFactSequence,
                        sinkState.LastProjectedTypedFactSequence))
                {
                    continue;
                }

                log.Add(CreateLogEvent(ref sinkState, new ReplayLogEventBuffer
                {
                    Kind = EDebugReplayEventKind.AttributeChange,
                    Frame = frame,
                    SourceAsc = evt.SourceAsc,
                    TargetAsc = evt.ASC,
                    SourceAbility = evt.SourceAbility,
                    GameplayEffect = evt.GameplayEffect,
                    ContextId = evt.ContextId,
                    EventCode = evt.EventCode,
                    AttrSetCode = evt.AttrSetCode,
                    AttributeCode = evt.AttributeCode,
                    OldValue = evt.OldValue,
                    NewValue = evt.NewValue,
                    Flag = evt.IsBaseValue ? (byte)1 : (byte)0,
                }));
            }

            sinkState.ProcessedAttributeEventCount = events.Length;
        }

        private static bool ShouldSkipMirroredTypedFact(
            int sourceFactSequence,
            int lastProjectedTypedFactSequence)
        {
            return sourceFactSequence > 0
                   && lastProjectedTypedFactSequence >= sourceFactSequence;
        }

        private static void ProjectCueRequests(
            DynamicBuffer<CueRequestBuffer> requests,
            DynamicBuffer<ReplayLogEventBuffer> log,
            ref GameplayEventLogSinkComponent sinkState,
            int frame)
        {
            var start = ClampProcessedCount(sinkState.ProcessedCueRequestCount, requests.Length);
            for (var i = start; i < requests.Length; i++)
            {
                var request = requests[i];
                if (ShouldSkipMirroredTypedFact(
                        request.SourceFactSequence,
                        sinkState.LastProjectedTypedFactSequence))
                {
                    continue;
                }

                log.Add(CreateLogEvent(ref sinkState, new ReplayLogEventBuffer
                {
                    Kind = EDebugReplayEventKind.CueRequest,
                    Frame = frame,
                    Sequence = request.SourceFactSequence,
                    CueEvent = request.CueEvent,
                    SourceAsc = request.SourceAsc,
                    TargetAsc = request.TargetAsc,
                    SourceAbility = request.SourceAbility,
                    GameplayEffect = request.GameplayEffect,
                    SourceEntity = request.SourceEntity,
                    CueEntity = request.CueEntity,
                    CueSourceType = request.SourceType,
                    ContextId = request.ContextId,
                    EventCode = (int)request.CueEvent,
                    ReasonCode = request.ReasonCode,
                }));
            }

            sinkState.ProcessedCueRequestCount = requests.Length;
        }

        private static void ProjectTagEvents(
            DynamicBuffer<TagChangeEventBuffer> events,
            DynamicBuffer<ReplayLogEventBuffer> log,
            ref GameplayEventLogSinkComponent sinkState,
            int frame)
        {
            var start = ClampProcessedCount(sinkState.ProcessedTagEventCount, events.Length);
            for (var i = start; i < events.Length; i++)
            {
                var evt = events[i];
                log.Add(CreateLogEvent(ref sinkState, new ReplayLogEventBuffer
                {
                    Kind = EDebugReplayEventKind.TagChange,
                    Frame = frame,
                    TargetAsc = evt.ASC,
                    TagIndex = evt.TagIndex,
                    Flag = evt.Added ? (byte)1 : (byte)0,
                }));
            }

            sinkState.ProcessedTagEventCount = events.Length;
        }

        private static void ProjectDamageEvents(
            DynamicBuffer<DamageEventBuffer> events,
            DynamicBuffer<ReplayLogEventBuffer> log,
            ref GameplayEventLogSinkComponent sinkState,
            int frame)
        {
            var start = ClampProcessedCount(sinkState.ProcessedDamageEventCount, events.Length);
            for (var i = start; i < events.Length; i++)
            {
                var evt = events[i];
                if (ShouldSkipMirroredTypedFact(
                        evt.SourceFactSequence,
                        sinkState.LastProjectedTypedFactSequence))
                {
                    continue;
                }

                log.Add(CreateLogEvent(ref sinkState, new ReplayLogEventBuffer
                {
                    Kind = EDebugReplayEventKind.Damage,
                    Frame = frame,
                    Sequence = evt.SourceFactSequence,
                    SourceAsc = evt.Source,
                    TargetAsc = evt.Target,
                    DamageAmount = evt.Amount,
                    Value = evt.Amount,
                }));
            }

            sinkState.ProcessedDamageEventCount = events.Length;
        }

        private static int ClampProcessedCount(int processedCount, int currentLength)
        {
            return processedCount > currentLength ? 0 : processedCount;
        }

        private static ReplayLogEventBuffer CreateLogEvent(
            ref GameplayEventLogSinkComponent sinkState,
            ReplayLogEventBuffer evt)
        {
            evt.LogIndex = sinkState.NextLogIndex;
            sinkState.NextLogIndex++;
            return evt;
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
