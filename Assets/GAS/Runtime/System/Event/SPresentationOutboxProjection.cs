using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Projects current-tick simulation facts to per-ASC presentation outboxes.
    /// This system does not consume or mutate gameplay state.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASCueGroup), OrderFirst = true)]
    [UpdateBefore(typeof(SCueRequestBridge))]
    public partial struct SPresentationOutboxProjection : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CGameplayEventBus>();
            state.RequireForUpdate<CPresentationOutboxProjectionState>();
            state.RequireForUpdate<CPresentationOutboxProjectionOptions>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<CGameplayEventBus>(out var eventBus))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBus)
                || !em.HasComponent<CPresentationOutboxProjectionState>(eventBus)
                || !em.HasComponent<CPresentationOutboxProjectionOptions>(eventBus))
            {
                return;
            }

            if (em.GetComponentData<CPresentationOutboxProjectionOptions>(eventBus).ProjectRawFacts == 0)
                return;

            var currentFrame = ResolveCurrentFrame(em);
            var projectionState = em.GetComponentData<CPresentationOutboxProjectionState>(eventBus);
            ResetProcessedCountsIfFrameChanged(ref projectionState, currentFrame);

            if (em.HasBuffer<BGameplayEvent>(eventBus))
                ProjectGameplayEvents(em, eventBus, em.GetBuffer<BGameplayEvent>(eventBus), ref projectionState, currentFrame);

            if (em.HasBuffer<BAttributeChangeEvent>(eventBus))
                ProjectAttributeEvents(em, eventBus, em.GetBuffer<BAttributeChangeEvent>(eventBus), ref projectionState, currentFrame);

            if (em.HasBuffer<BCueRequest>(eventBus))
                ProjectCueRequests(em, eventBus, em.GetBuffer<BCueRequest>(eventBus), ref projectionState, currentFrame);

            if (em.HasBuffer<BTagChangeEvent>(eventBus))
                ProjectTagEvents(em, eventBus, em.GetBuffer<BTagChangeEvent>(eventBus), ref projectionState, currentFrame);

            if (em.HasBuffer<BDamageEvent>(eventBus))
                ProjectDamageEvents(em, eventBus, em.GetBuffer<BDamageEvent>(eventBus), ref projectionState, currentFrame);

            em.SetComponentData(eventBus, projectionState);
        }

        private static void ResetProcessedCountsIfFrameChanged(
            ref CPresentationOutboxProjectionState projectionState,
            int currentFrame)
        {
            if (projectionState.LastProjectedFrame == currentFrame)
                return;

            projectionState.LastProjectedFrame = currentFrame;
            projectionState.ProcessedGameplayEventCount = 0;
            projectionState.ProcessedAttributeEventCount = 0;
            projectionState.ProcessedCueRequestCount = 0;
            projectionState.ProcessedTagEventCount = 0;
            projectionState.ProcessedDamageEventCount = 0;
        }

        private static void ProjectGameplayEvents(
            EntityManager em,
            Entity eventBus,
            DynamicBuffer<BGameplayEvent> events,
            ref CPresentationOutboxProjectionState projectionState,
            int fallbackFrame)
        {
            var start = ClampProcessedCount(projectionState.ProcessedGameplayEventCount, events.Length);
            for (var i = start; i < events.Length; i++)
            {
                var evt = events[i];
                if (IsPresentationMarker(evt.Type))
                    continue;

                var presentationEvent = new BPresentationEvent
                {
                    Kind = EPresentationEventKind.GameplayEvent,
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
                };

                AppendToRelevantAsc(em, eventBus, evt.TargetAsc, evt.SourceAsc, presentationEvent);
            }

            projectionState.ProcessedGameplayEventCount = events.Length;
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
            EntityManager em,
            Entity eventBus,
            DynamicBuffer<BAttributeChangeEvent> events,
            ref CPresentationOutboxProjectionState projectionState,
            int frame)
        {
            var start = ClampProcessedCount(projectionState.ProcessedAttributeEventCount, events.Length);
            for (var i = start; i < events.Length; i++)
            {
                var evt = events[i];
                var presentationEvent = new BPresentationEvent
                {
                    Kind = EPresentationEventKind.AttributeChange,
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
                };

                AppendToRelevantAsc(em, eventBus, evt.ASC, evt.SourceAsc, presentationEvent);
            }

            projectionState.ProcessedAttributeEventCount = events.Length;
        }

        private static void ProjectCueRequests(
            EntityManager em,
            Entity eventBus,
            DynamicBuffer<BCueRequest> requests,
            ref CPresentationOutboxProjectionState projectionState,
            int frame)
        {
            var start = ClampProcessedCount(projectionState.ProcessedCueRequestCount, requests.Length);
            for (var i = start; i < requests.Length; i++)
            {
                var request = requests[i];
                var presentationEvent = new BPresentationEvent
                {
                    Kind = EPresentationEventKind.CueRequest,
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
                };

                AppendToRelevantAsc(em, eventBus, request.TargetAsc, request.SourceAsc, presentationEvent);
            }

            projectionState.ProcessedCueRequestCount = requests.Length;
        }

        private static void ProjectTagEvents(
            EntityManager em,
            Entity eventBus,
            DynamicBuffer<BTagChangeEvent> events,
            ref CPresentationOutboxProjectionState projectionState,
            int frame)
        {
            var start = ClampProcessedCount(projectionState.ProcessedTagEventCount, events.Length);
            for (var i = start; i < events.Length; i++)
            {
                var evt = events[i];
                AppendToAsc(em, eventBus, evt.ASC, new BPresentationEvent
                {
                    Kind = EPresentationEventKind.TagChange,
                    Frame = frame,
                    TargetAsc = evt.ASC,
                    TagIndex = evt.TagIndex,
                    Flag = evt.Added ? (byte)1 : (byte)0,
                });
            }

            projectionState.ProcessedTagEventCount = events.Length;
        }

        private static void ProjectDamageEvents(
            EntityManager em,
            Entity eventBus,
            DynamicBuffer<BDamageEvent> events,
            ref CPresentationOutboxProjectionState projectionState,
            int frame)
        {
            var start = ClampProcessedCount(projectionState.ProcessedDamageEventCount, events.Length);
            for (var i = start; i < events.Length; i++)
            {
                var evt = events[i];
                var presentationEvent = new BPresentationEvent
                {
                    Kind = EPresentationEventKind.Damage,
                    Frame = frame,
                    SourceAsc = evt.Source,
                    TargetAsc = evt.Target,
                    DamageAmount = evt.Amount,
                    Value = evt.Amount,
                };

                AppendToRelevantAsc(em, eventBus, evt.Target, evt.Source, presentationEvent);
            }

            projectionState.ProcessedDamageEventCount = events.Length;
        }

        private static int ClampProcessedCount(int processedCount, int currentLength)
        {
            return processedCount > currentLength ? 0 : processedCount;
        }

        private static void AppendToRelevantAsc(
            EntityManager em,
            Entity eventBus,
            Entity primaryAsc,
            Entity secondaryAsc,
            in BPresentationEvent presentationEvent)
        {
            AppendToAsc(em, eventBus, primaryAsc, presentationEvent);

            if (secondaryAsc != primaryAsc)
                AppendToAsc(em, eventBus, secondaryAsc, presentationEvent);
        }

        private static void AppendToAsc(
            EntityManager em,
            Entity eventBus,
            Entity asc,
            in BPresentationEvent presentationEvent)
        {
            EventBusHelper.AppendPresentationEvent(em, eventBus, asc, presentationEvent);
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
