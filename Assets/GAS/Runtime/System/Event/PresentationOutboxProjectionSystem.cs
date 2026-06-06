using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Projects current-tick simulation facts to per-ASC presentation outboxes.
    /// This system does not consume or mutate gameplay state.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GASBoundaryProjectionSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(CueRequestBridgeSystem))]
    public partial struct PresentationOutboxProjectionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayEventBusComponent>();
            state.RequireForUpdate<PresentationOutboxProjectionStateComponent>();
            state.RequireForUpdate<PresentationOutboxProjectionOptionsComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<GameplayEventBusComponent>(out var eventBus))
                return;

            var em = state.EntityManager;
            if (!em.Exists(eventBus)
                || !em.HasComponent<PresentationOutboxProjectionStateComponent>(eventBus)
                || !em.HasComponent<PresentationOutboxProjectionOptionsComponent>(eventBus))
            {
                return;
            }

            if (em.GetComponentData<PresentationOutboxProjectionOptionsComponent>(eventBus).ProjectRawFacts == 0)
                return;

            var currentFrame = GASRuntimeFrameContext.ResolveCurrentFrame(em);
            var projectionState = em.GetComponentData<PresentationOutboxProjectionStateComponent>(eventBus);
            ResetProcessedCountsIfFrameChanged(ref projectionState, currentFrame);

            if (SystemAPI.TryGetSingletonEntity<GEEffectCommandStreamComponent>(out var streamEntity)
                && em.Exists(streamEntity)
                && em.HasBuffer<GameplayEventBuffer>(streamEntity))
            {
                ProjectTypedSimulationFacts(
                    em,
                    eventBus,
                    em.GetBuffer<GameplayEventBuffer>(streamEntity),
                    ref projectionState,
                    currentFrame);
            }

            em.SetComponentData(eventBus, projectionState);
        }

        private static void ResetProcessedCountsIfFrameChanged(
            ref PresentationOutboxProjectionStateComponent projectionState,
            int currentFrame)
        {
            if (projectionState.LastProjectedFrame == currentFrame)
                return;

            projectionState.LastProjectedFrame = currentFrame;
            projectionState.ProcessedTypedFactCount = 0;
        }

        private static void ProjectTypedSimulationFacts(
            EntityManager em,
            Entity eventBus,
            DynamicBuffer<GameplayEventBuffer> facts,
            ref PresentationOutboxProjectionStateComponent projectionState,
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
            in GameplayEventBuffer fact,
            int fallbackFrame,
            out PresentationEventBuffer presentationEvent)
        {
            presentationEvent = default;

            if (fact.TargetAsc == Entity.Null)
            {
                return false;
            }

            if (fact.Domain == EGameplayFactDomain.Attribute
                && fact.EventType == EGameplayEventType.AttributeBaseValueChanged)
            {
                presentationEvent = new PresentationEventBuffer
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
                presentationEvent = new PresentationEventBuffer
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

            if (fact.Domain == EGameplayFactDomain.Tag
                && fact.EventType == EGameplayEventType.TagChanged)
            {
                presentationEvent = new PresentationEventBuffer
                {
                    Kind = EPresentationEventKind.TagChange,
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
                presentationEvent = new PresentationEventBuffer
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
                presentationEvent = new PresentationEventBuffer
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

        private static int ResolveGameplayEventCode(in GameplayEventBuffer fact)
        {
            return fact.EventCode != 0 ? fact.EventCode : fact.GameplayEffectCode;
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

        private static void AppendToRelevantAsc(
            EntityManager em,
            Entity eventBus,
            Entity primaryAsc,
            Entity secondaryAsc,
            in PresentationEventBuffer presentationEvent)
        {
            AppendToAsc(em, eventBus, primaryAsc, presentationEvent);

            if (secondaryAsc != primaryAsc)
                AppendToAsc(em, eventBus, secondaryAsc, presentationEvent);
        }

        private static void AppendToAsc(
            EntityManager em,
            Entity eventBus,
            Entity asc,
            in PresentationEventBuffer presentationEvent)
        {
            EventBusHelper.AppendPresentationEvent(em, eventBus, asc, presentationEvent);
        }

        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
