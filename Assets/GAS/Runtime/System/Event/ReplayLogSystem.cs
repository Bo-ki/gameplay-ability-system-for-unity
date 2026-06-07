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

            GasReplaySinkPolicy.ApplyRetention(log, ref sinkState);
            em.SetComponentData(logSink, sinkState);
        }

        private static void ResetProcessedCountsIfFrameChanged(ref GameplayEventLogSinkComponent sinkState, int currentFrame)
        {
            if (sinkState.LastProjectedFrame == currentFrame)
                return;

            sinkState.LastProjectedFrame = currentFrame;
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

            if (fact.Domain == EGameplayFactDomain.Tag
                && fact.EventType == EGameplayEventType.TagChanged)
            {
                replayEvent = new ReplayLogEventBuffer
                {
                    Kind = EDebugReplayEventKind.TagChange,
                    Frame = fact.Frame != 0 ? fact.Frame : fallbackFrame,
                    Sequence = fact.Sequence,
                    GameplayEventType = fact.EventType,
                    SourceAsc = fact.SourceAsc,
                    TargetAsc = fact.TargetAsc,
                    SourceAbility = fact.SourceAbility,
                    GameplayEffect = fact.SourceEffect,
                    ContextId = fact.ContextId,
                    EventCode = fact.EventCode,
                    ReasonCode = fact.ReasonCode,
                    TagIndex = fact.EventCode,
                    Flag = fact.ReasonCode != 0 ? (byte)1 : (byte)0,
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

            if (!GameplayFactClassifier.IsPresentationMarker(fact.EventType)
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
