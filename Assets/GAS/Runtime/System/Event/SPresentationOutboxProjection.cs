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

            var currentFrame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            var projectionState = em.GetComponentData<CPresentationOutboxProjectionState>(eventBus);
            ResetProcessedCountsIfFrameChanged(ref projectionState, currentFrame);

            if (SystemAPI.TryGetSingletonEntity<CEffectCommandSpecStream>(out var streamEntity)
                && em.Exists(streamEntity)
                && em.HasBuffer<BTypedSimulationFact>(streamEntity))
            {
                ProjectTypedSimulationFacts(
                    em,
                    eventBus,
                    em.GetBuffer<BTypedSimulationFact>(streamEntity),
                    ref projectionState,
                    currentFrame);
            }

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
            projectionState.ProcessedTypedFactCount = 0;
        }

        private static void ProjectTypedSimulationFacts(
            EntityManager em,
            Entity eventBus,
            DynamicBuffer<BTypedSimulationFact> facts,
            ref CPresentationOutboxProjectionState projectionState,
            int fallbackFrame)
        {
            for (var i = 0; i < facts.Length; i++)
            {
                var fact = facts[i];
                if (fact.Sequence <= 0
                    || fact.Sequence <= projectionState.LastProjectedTypedFactSequence)
                {
                    continue;
                }

                if (!TryCreateTypedPresentationEvent(in fact, fallbackFrame, out var presentationEvent))
                    continue;

                AppendToRelevantAsc(em, eventBus, fact.TargetAsc, fact.SourceAsc, presentationEvent);
                if (fact.Sequence > projectionState.LastProjectedTypedFactSequence)
                    projectionState.LastProjectedTypedFactSequence = fact.Sequence;
            }

            projectionState.ProcessedTypedFactCount = facts.Length;
        }

        private static bool TryCreateTypedPresentationEvent(
            in BTypedSimulationFact fact,
            int fallbackFrame,
            out BPresentationEvent presentationEvent)
        {
            presentationEvent = default;

            if (fact.TargetAsc == Entity.Null)
            {
                return false;
            }

            if (fact.Domain == EGameplayFactDomain.Attribute
                && fact.EventType == EGameplayEventType.AttributeBaseValueChanged)
            {
                presentationEvent = new BPresentationEvent
                {
                    Kind = EPresentationEventKind.AttributeChange,
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
                presentationEvent = new BPresentationEvent
                {
                    Kind = EPresentationEventKind.CueRequest,
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
                presentationEvent = new BPresentationEvent
                {
                    Kind = EPresentationEventKind.Damage,
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
                presentationEvent = new BPresentationEvent
                {
                    Kind = EPresentationEventKind.GameplayEvent,
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

        private static int ResolveGameplayEventCode(in BTypedSimulationFact fact)
        {
            return fact.EventCode != 0 ? fact.EventCode : fact.GameplayEffectCode;
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
                if (ShouldSkipMirroredTypedFact(
                        evt.SourceFactSequence,
                        projectionState.LastProjectedTypedFactSequence))
                {
                    continue;
                }

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
                if (ShouldSkipMirroredTypedFact(
                        evt.SourceFactSequence,
                        projectionState.LastProjectedTypedFactSequence))
                {
                    continue;
                }

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

        private static bool ShouldSkipMirroredTypedFact(
            int sourceFactSequence,
            int lastProjectedTypedFactSequence)
        {
            return sourceFactSequence > 0
                   && lastProjectedTypedFactSequence >= sourceFactSequence;
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
                if (ShouldSkipMirroredTypedFact(
                        request.SourceFactSequence,
                        projectionState.LastProjectedTypedFactSequence))
                {
                    continue;
                }

                var presentationEvent = new BPresentationEvent
                {
                    Kind = EPresentationEventKind.CueRequest,
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
                if (ShouldSkipMirroredTypedFact(
                        evt.SourceFactSequence,
                        projectionState.LastProjectedTypedFactSequence))
                {
                    continue;
                }

                var presentationEvent = new BPresentationEvent
                {
                    Kind = EPresentationEventKind.Damage,
                    Frame = frame,
                    Sequence = evt.SourceFactSequence,
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

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
