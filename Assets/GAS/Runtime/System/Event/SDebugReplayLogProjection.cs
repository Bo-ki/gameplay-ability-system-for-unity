using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Appends current-tick simulation facts to a persistent debug/replay log.
    /// This is a side-channel recording sink, not gameplay input.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCueGroup), OrderFirst = true)]
    [UpdateAfter(typeof(SPresentationOutboxProjection))]
    [UpdateBefore(typeof(SCueRequestBridge))]
    public partial struct SDebugReplayLogProjection : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate<CGameplayEventLogSink>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBus)
                || !SystemAPI.TryGetSingletonEntity<CGameplayEventLogSink>(out var logSink))
            {
                return;
            }

            var em = state.EntityManager;
            if (!em.Exists(eventBus)
                || !em.Exists(logSink)
                || !em.HasBuffer<BDebugReplayEvent>(logSink))
            {
                return;
            }

            var sinkState = em.GetComponentData<CGameplayEventLogSink>(logSink);
            var currentFrame = ResolveCurrentFrame(em);
            ResetProcessedCountsIfFrameChanged(ref sinkState, currentFrame);

            var log = em.GetBuffer<BDebugReplayEvent>(logSink);

            if (em.HasBuffer<BGameplayEvent>(eventBus))
                ProjectGameplayEvents(em.GetBuffer<BGameplayEvent>(eventBus), log, ref sinkState, currentFrame);

            if (em.HasBuffer<BAttributeChangeEvent>(eventBus))
                ProjectAttributeEvents(em.GetBuffer<BAttributeChangeEvent>(eventBus), log, ref sinkState, currentFrame);

            if (em.HasBuffer<BCueRequest>(eventBus))
                ProjectCueRequests(em.GetBuffer<BCueRequest>(eventBus), log, ref sinkState, currentFrame);

            if (em.HasBuffer<BTagChangeEvent>(eventBus))
                ProjectTagEvents(em.GetBuffer<BTagChangeEvent>(eventBus), log, ref sinkState, currentFrame);

            if (em.HasBuffer<BDamageEvent>(eventBus))
                ProjectDamageEvents(em.GetBuffer<BDamageEvent>(eventBus), log, ref sinkState, currentFrame);

            GasReplaySinkPolicy.ApplyRetention(log, ref sinkState);
            em.SetComponentData(logSink, sinkState);
        }

        private static void ResetProcessedCountsIfFrameChanged(ref CGameplayEventLogSink sinkState, int currentFrame)
        {
            if (sinkState.LastProjectedFrame == currentFrame)
                return;

            sinkState.LastProjectedFrame = currentFrame;
            sinkState.ProcessedGameplayEventCount = 0;
            sinkState.ProcessedAttributeEventCount = 0;
            sinkState.ProcessedCueRequestCount = 0;
            sinkState.ProcessedTagEventCount = 0;
            sinkState.ProcessedDamageEventCount = 0;
        }

        private static void ProjectGameplayEvents(
            DynamicBuffer<BGameplayEvent> events,
            DynamicBuffer<BDebugReplayEvent> log,
            ref CGameplayEventLogSink sinkState,
            int fallbackFrame)
        {
            var start = ClampProcessedCount(sinkState.ProcessedGameplayEventCount, events.Length);
            for (var i = start; i < events.Length; i++)
            {
                var evt = events[i];
                log.Add(CreateLogEvent(ref sinkState, new BDebugReplayEvent
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

        private static void ProjectAttributeEvents(
            DynamicBuffer<BAttributeChangeEvent> events,
            DynamicBuffer<BDebugReplayEvent> log,
            ref CGameplayEventLogSink sinkState,
            int frame)
        {
            var start = ClampProcessedCount(sinkState.ProcessedAttributeEventCount, events.Length);
            for (var i = start; i < events.Length; i++)
            {
                var evt = events[i];
                log.Add(CreateLogEvent(ref sinkState, new BDebugReplayEvent
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

        private static void ProjectCueRequests(
            DynamicBuffer<BCueRequest> requests,
            DynamicBuffer<BDebugReplayEvent> log,
            ref CGameplayEventLogSink sinkState,
            int frame)
        {
            var start = ClampProcessedCount(sinkState.ProcessedCueRequestCount, requests.Length);
            for (var i = start; i < requests.Length; i++)
            {
                var request = requests[i];
                log.Add(CreateLogEvent(ref sinkState, new BDebugReplayEvent
                {
                    Kind = EDebugReplayEventKind.CueRequest,
                    Frame = frame,
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
                }));
            }

            sinkState.ProcessedCueRequestCount = requests.Length;
        }

        private static void ProjectTagEvents(
            DynamicBuffer<BTagChangeEvent> events,
            DynamicBuffer<BDebugReplayEvent> log,
            ref CGameplayEventLogSink sinkState,
            int frame)
        {
            var start = ClampProcessedCount(sinkState.ProcessedTagEventCount, events.Length);
            for (var i = start; i < events.Length; i++)
            {
                var evt = events[i];
                log.Add(CreateLogEvent(ref sinkState, new BDebugReplayEvent
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
            DynamicBuffer<BDamageEvent> events,
            DynamicBuffer<BDebugReplayEvent> log,
            ref CGameplayEventLogSink sinkState,
            int frame)
        {
            var start = ClampProcessedCount(sinkState.ProcessedDamageEventCount, events.Length);
            for (var i = start; i < events.Length; i++)
            {
                var evt = events[i];
                log.Add(CreateLogEvent(ref sinkState, new BDebugReplayEvent
                {
                    Kind = EDebugReplayEventKind.Damage,
                    Frame = frame,
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

        private static BDebugReplayEvent CreateLogEvent(
            ref CGameplayEventLogSink sinkState,
            BDebugReplayEvent evt)
        {
            evt.LogIndex = sinkState.NextLogIndex;
            sinkState.NextLogIndex++;
            return evt;
        }

        private static int ResolveCurrentFrame(EntityManager em)
        {
            var globalTimer = GASManager.EntityGlobalTimer;
            if (globalTimer != Entity.Null
                && em.Exists(globalTimer)
                && em.HasComponent<GlobalTimer>(globalTimer))
            {
                return em.GetComponentData<GlobalTimer>(globalTimer).Frame;
            }

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<GlobalTimer>());
            return query.IsEmptyIgnoreFilter ? 0 : query.GetSingleton<GlobalTimer>().Frame;
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
