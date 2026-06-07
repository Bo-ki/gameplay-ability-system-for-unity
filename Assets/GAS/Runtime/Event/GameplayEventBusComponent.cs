using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Simulation fact stream singleton.
    /// Buffers on this entity are one-tick transient streams: GameplayEventBusClearSystem clears
    /// the previous tick at the start of GASFramePrepareSystemGroup, and later systems append
    /// current-tick facts for Cue/presentation/debug consumers.
    /// </summary>
    public struct GameplayEventBusComponent : IComponentData
    {
        public int NextSequence;
        public int NextContextId;
    }

    public enum EBoundaryObservationFactSource : byte
    {
        OwnerLocalCore = 0,
    }

    /// <summary>
    /// Boundary-only observation facts exported from core carriers for presentation, replay and diagnostics.
    /// Gameplay systems must not read this buffer as simulation input.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct BoundaryObservationFactBuffer : IBufferElementData
    {
        public GameplayEventBuffer Fact;
        public Entity Owner;
        public int LocalIndex;
        public EBoundaryObservationFactSource Source;
    }

    /// <summary>
    /// Tag 变更事件。入队到事件总线 Singleton 的 TagChangeEventBuffer Buffer。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct TagChangeEventBuffer : IBufferElementData
    {
        public int SourceFactSequence;
        public Entity ASC;
        public int TagIndex;
        public bool Added;
    }

    public enum EGameplayEventType : byte
    {
        GameplayEffectRequested = 0,
        GameplayEffectInstanced = 1,
        GameplayEffectApplied = 2,
        GameplayEffectRemoved = 3,
        AttributeBaseValueChanged = 4,
        ActiveModifierAdded = 5,
        ActiveModifierRemoved = 6,
        CueRequested = 7,
        AbilityEnded = 8,
        AbilityCanceled = 9,
        StackCountChanged = 10,
        ActiveModifierUpdated = 11,
        StackOverflow = 12,
        StackOverflowDenied = 13,
        StackClearedByOverflow = 14,
        StackOverflowRefreshed = 15,
        GameplayEffectInhibited = 16,
        GameplayEffectReactivated = 17,
        ExecutionCalculationOutputUpdated = 18,
        ExecutionCalculationInputMissing = 19,
        ExecutionCalculationOutputMissing = 20,
        AbilityCommitSucceeded = 21,
        AbilityCommitFailed = 22,
        AbilityActivationBlockedByAbility = 23,
        AbilityCancelRequested = 24,
        GameplayEffectApplicationRejected = 25,
        AbilityEndRequested = 26,
        RuntimeBoundaryRequest = 27,
        RuntimeBoundaryStateTransition = 28,
        RuntimeBoundaryStateChange = 29,
        RuntimeBoundaryFailure = 30,
        RuntimeBoundaryDiagnostic = 31,
        RuntimeLifecycleRequested = 32,
        RuntimeLifecycleApplied = 33,
        RuntimeActionTriggered = 34,
        RuntimeActionApplied = 35,
        RuntimeResourceGranted = 36,
        RuntimeBuffApplied = 37,
        RuntimeBuffExpired = 38,
        RuntimeBuffRemoved = 39,
        RuntimeDamageResolved = 40,
        RuntimeDamageApplied = 41,
        RuntimeDamageResisted = 42,
        RuntimeDamageAbsorbed = 43,
        RuntimeControlStateChanged = 44,
        RuntimeEntitySpawnRequested = 45,
        RuntimeEntitySpawned = 46,
        RuntimeEntityExpired = 47,
        RuntimeEntityDespawned = 48,
        PresentationUiMarker = 49,
        PresentationVfxMarker = 50,
        PresentationSfxMarker = 51,
        PresentationFloatingTextMarker = 52,
        PresentationCueMarker = 53,
        PresentationSettlementMarker = 54,
        TagChanged = 77,
    }

    public enum EGameplayFactDomain : byte
    {
        Unknown = 0,
        Ability = 1,
        GameplayEffect = 2,
        Attribute = 3,
        Tag = 4,
        Cue = 5,
        ExecutionCalculation = 6,
        RuntimeBoundary = 7,
        Presentation = 8,
        Damage = 9,
    }

    public enum EGameplayFactCategory : byte
    {
        Unknown = 0,
        Request = 1,
        StateTransition = 2,
        StateChange = 3,
        Failure = 4,
        Diagnostic = 5,
    }

    public enum EGameplayFactSeverity : byte
    {
        Trace = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    public readonly struct GameplayFactClassification
    {
        public readonly EGameplayFactDomain Domain;
        public readonly EGameplayFactCategory Category;
        public readonly EGameplayFactSeverity Severity;

        public GameplayFactClassification(
            EGameplayFactDomain domain,
            EGameplayFactCategory category,
            EGameplayFactSeverity severity)
        {
            Domain = domain;
            Category = category;
            Severity = severity;
        }

        public bool IsFailure => Category == EGameplayFactCategory.Failure;

        public bool IsDiagnostic => Category == EGameplayFactCategory.Diagnostic;
    }

    public static class GameplayFactClassifier
    {
        public static GameplayFactClassification Classify(EGameplayEventType type)
        {
            return type switch
            {
                EGameplayEventType.GameplayEffectRequested => Request(EGameplayFactDomain.GameplayEffect),
                EGameplayEventType.GameplayEffectInstanced => StateTransition(EGameplayFactDomain.GameplayEffect),
                EGameplayEventType.GameplayEffectApplied => StateTransition(EGameplayFactDomain.GameplayEffect),
                EGameplayEventType.GameplayEffectRemoved => StateTransition(EGameplayFactDomain.GameplayEffect),
                EGameplayEventType.GameplayEffectInhibited => StateTransition(EGameplayFactDomain.GameplayEffect),
                EGameplayEventType.GameplayEffectReactivated => StateTransition(EGameplayFactDomain.GameplayEffect),
                EGameplayEventType.GameplayEffectApplicationRejected => Failure(EGameplayFactDomain.GameplayEffect),

                EGameplayEventType.AttributeBaseValueChanged => StateChange(EGameplayFactDomain.Attribute),
                EGameplayEventType.ActiveModifierAdded => StateChange(EGameplayFactDomain.Attribute),
                EGameplayEventType.ActiveModifierRemoved => StateChange(EGameplayFactDomain.Attribute),
                EGameplayEventType.ActiveModifierUpdated => StateChange(EGameplayFactDomain.Attribute),

                EGameplayEventType.TagChanged => StateChange(EGameplayFactDomain.Tag),

                EGameplayEventType.CueRequested => Request(EGameplayFactDomain.Cue),

                EGameplayEventType.AbilityCommitSucceeded => StateTransition(EGameplayFactDomain.Ability),
                EGameplayEventType.AbilityCommitFailed => Failure(EGameplayFactDomain.Ability),
                EGameplayEventType.AbilityActivationBlockedByAbility => Failure(EGameplayFactDomain.Ability),
                EGameplayEventType.AbilityEndRequested => Request(EGameplayFactDomain.Ability),
                EGameplayEventType.AbilityCancelRequested => Request(EGameplayFactDomain.Ability),
                EGameplayEventType.AbilityCanceled => StateTransition(EGameplayFactDomain.Ability),
                EGameplayEventType.AbilityEnded => StateTransition(EGameplayFactDomain.Ability),

                EGameplayEventType.StackCountChanged => StateChange(EGameplayFactDomain.GameplayEffect),
                EGameplayEventType.StackOverflow => StateChange(EGameplayFactDomain.GameplayEffect),
                EGameplayEventType.StackOverflowDenied => Failure(EGameplayFactDomain.GameplayEffect),
                EGameplayEventType.StackClearedByOverflow => StateChange(EGameplayFactDomain.GameplayEffect),
                EGameplayEventType.StackOverflowRefreshed => StateChange(EGameplayFactDomain.GameplayEffect),

                EGameplayEventType.ExecutionCalculationOutputUpdated => StateChange(EGameplayFactDomain.ExecutionCalculation),
                EGameplayEventType.ExecutionCalculationInputMissing => Diagnostic(EGameplayFactDomain.ExecutionCalculation),
                EGameplayEventType.ExecutionCalculationOutputMissing => Diagnostic(EGameplayFactDomain.ExecutionCalculation),

                EGameplayEventType.RuntimeBoundaryRequest => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeBoundaryStateTransition => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeBoundaryStateChange => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeBoundaryFailure => Failure(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeBoundaryDiagnostic => Diagnostic(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeLifecycleRequested => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeLifecycleApplied => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeActionTriggered => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeActionApplied => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeResourceGranted => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeBuffApplied => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeBuffExpired => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeBuffRemoved => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeDamageResolved => StateChange(EGameplayFactDomain.Damage),
                EGameplayEventType.RuntimeDamageApplied => StateChange(EGameplayFactDomain.Damage),
                EGameplayEventType.RuntimeDamageResisted => StateChange(EGameplayFactDomain.Damage),
                EGameplayEventType.RuntimeDamageAbsorbed => StateChange(EGameplayFactDomain.Damage),
                EGameplayEventType.RuntimeControlStateChanged => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeEntitySpawnRequested => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeEntitySpawned => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeEntityExpired => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.RuntimeEntityDespawned => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.PresentationUiMarker => StateChange(EGameplayFactDomain.Presentation),
                EGameplayEventType.PresentationVfxMarker => StateChange(EGameplayFactDomain.Presentation),
                EGameplayEventType.PresentationSfxMarker => StateChange(EGameplayFactDomain.Presentation),
                EGameplayEventType.PresentationFloatingTextMarker => StateChange(EGameplayFactDomain.Presentation),
                EGameplayEventType.PresentationCueMarker => Request(EGameplayFactDomain.Presentation),
                EGameplayEventType.PresentationSettlementMarker => StateTransition(EGameplayFactDomain.Presentation),

                _ => new GameplayFactClassification(
                    EGameplayFactDomain.Unknown,
                    EGameplayFactCategory.Unknown,
                    EGameplayFactSeverity.Warning),
            };
        }

        public static bool IsPresentationMarker(EGameplayEventType type)
        {
            return type == EGameplayEventType.PresentationUiMarker
                   || type == EGameplayEventType.PresentationVfxMarker
                   || type == EGameplayEventType.PresentationSfxMarker
                   || type == EGameplayEventType.PresentationFloatingTextMarker
                   || type == EGameplayEventType.PresentationCueMarker
                   || type == EGameplayEventType.PresentationSettlementMarker;
        }

        public static GameplayFactClassification Classify(in ReplayLogEventBuffer evt)
        {
            return evt.Kind switch
            {
                EDebugReplayEventKind.GameplayEvent => Classify(evt.GameplayEventType),
                EDebugReplayEventKind.AttributeChange => StateChange(EGameplayFactDomain.Attribute),
                EDebugReplayEventKind.CueRequest => Request(EGameplayFactDomain.Cue),
                EDebugReplayEventKind.TagChange => StateChange(EGameplayFactDomain.Tag),
                EDebugReplayEventKind.Damage => StateChange(EGameplayFactDomain.Damage),
                _ => new GameplayFactClassification(
                    EGameplayFactDomain.Unknown,
                    EGameplayFactCategory.Unknown,
                    EGameplayFactSeverity.Warning),
            };
        }

        private static GameplayFactClassification Request(EGameplayFactDomain domain)
        {
            return new GameplayFactClassification(
                domain,
                EGameplayFactCategory.Request,
                EGameplayFactSeverity.Info);
        }

        private static GameplayFactClassification StateTransition(EGameplayFactDomain domain)
        {
            return new GameplayFactClassification(
                domain,
                EGameplayFactCategory.StateTransition,
                EGameplayFactSeverity.Info);
        }

        private static GameplayFactClassification StateChange(EGameplayFactDomain domain)
        {
            return new GameplayFactClassification(
                domain,
                EGameplayFactCategory.StateChange,
                EGameplayFactSeverity.Info);
        }

        private static GameplayFactClassification Failure(EGameplayFactDomain domain)
        {
            return new GameplayFactClassification(
                domain,
                EGameplayFactCategory.Failure,
                EGameplayFactSeverity.Warning);
        }

        private static GameplayFactClassification Diagnostic(EGameplayFactDomain domain)
        {
            return new GameplayFactClassification(
                domain,
                EGameplayFactCategory.Diagnostic,
                EGameplayFactSeverity.Warning);
        }
    }

    public enum EGameplayCueEvent : byte
    {
        OnApply = 0,
        OnAdd = 1,
        OnActivate = 2,
        OnTick = 3,
        OnDeactivate = 4,
        OnRemove = 5,
        StopTick = 6,
        Kill = 7,
        Play = 8,
    }

    /// <summary>
    /// Attribute 变更事件。替代核心计算路径中的托管回调。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct AttributeChangeEventBuffer : IBufferElementData
    {
        public Entity ASC;
        public Entity SourceAsc;
        public Entity SourceAbility;
        public Entity GameplayEffect;
        public int SourceFactSequence;
        public int EventCode;
        public int AttrSetCode;
        public int AttributeCode;
        public float OldValue;
        public float NewValue;
        public int ContextId;
        public bool IsBaseValue;
    }

    /// <summary>
    /// Cue 请求事件。后续 Cue bridge 只消费事实，不反向修改 gameplay 状态。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct CueRequestBuffer : IBufferElementData
    {
        public Entity TargetAsc;
        public Entity SourceAsc;
        public Entity SourceAbility;
        public Entity GameplayEffect;
        public Entity SourceEntity;
        public CueSourceType SourceType;
        public Entity CueEntity;
        public int SourceFactSequence;
        public int ContextId;
        public int ReasonCode;
        public EGameplayCueEvent CueEvent;
    }

    public enum EPresentationEventKind : byte
    {
        GameplayEvent = 0,
        AttributeChange = 1,
        CueRequest = 2,
        TagChange = 3,
        Damage = 4,
    }

    /// <summary>
    /// Per-ASC presentation outbox. Presentation/UI/network code may read it after
    /// the GAS tick, but gameplay systems must not use it as simulation input.
    /// </summary>
    [InternalBufferCapacity(16)]
    public struct PresentationEventBuffer : IBufferElementData
    {
        public int Frame;
        public int Sequence;
        public EPresentationEventKind Kind;
        public EGameplayEventType GameplayEventType;
        public EGameplayCueEvent CueEvent;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public Entity GameplayEffect;
        public Entity SourceEntity;
        public Entity RelatedAbility;
        public Entity CueEntity;
        public CueSourceType CueSourceType;
        public int ContextId;
        public int EventCode;
        public int ReasonCode;
        public int RelatedAbilityCode;
        public int AttrSetCode;
        public int AttributeCode;
        public int TagIndex;
        public float Value;
        public float OldValue;
        public float NewValue;
        public float DamageAmount;
        public byte Flag;
    }

    /// <summary>
    /// ASC outboxes touched during the current GAS tick. The next tick clears
    /// only these buffers instead of scanning every ASC with a presentation outbox.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct PresentationOutboxOwnerBuffer : IBufferElementData
    {
        public Entity ASC;
    }

    /// <summary>
    /// Tracks how much of the current-tick simulation fact stream has already
    /// been projected into per-ASC presentation outboxes.
    /// </summary>
    public struct PresentationOutboxProjectionStateComponent : IComponentData
    {
        public int LastProjectedFrame;
        public int ProcessedTypedFactCount;
        public int LastProjectedTypedFactSequence;
    }

    /// <summary>
    /// Runtime switch for raw fact fan-out into per-ASC presentation outboxes.
    /// Marker projection can stay enabled while raw facts remain in replay/debug streams.
    /// </summary>
    public struct PresentationOutboxProjectionOptionsComponent : IComponentData
    {
        public byte ProjectRawFacts;
    }

    public enum EDebugReplayEventKind : byte
    {
        GameplayEvent = 0,
        AttributeChange = 1,
        CueRequest = 2,
        TagChange = 3,
        Damage = 4,
    }

    /// <summary>
    /// Persistent debug/replay log sink state. It is a recording boundary only;
    /// gameplay systems must not read it as simulation input.
    /// </summary>
    public struct GameplayEventLogSinkComponent : IComponentData
    {
        public int NextLogIndex;
        public int FirstRetainedLogIndex;
        public int DroppedEventCount;
        public int MaxRetainedEvents;
        public int LastProjectedFrame;
        public int ProcessedTypedFactCount;
        public int LastProjectedTypedFactSequence;
    }

    /// <summary>
    /// Persistent debug/replay event. It mirrors simulation facts into a single
    /// append-only stream until explicitly cleared by tooling or tests.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ReplayLogEventBuffer : IBufferElementData
    {
        public int LogIndex;
        public int Frame;
        public int Sequence;
        public EDebugReplayEventKind Kind;
        public EGameplayEventType GameplayEventType;
        public EGameplayCueEvent CueEvent;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public Entity GameplayEffect;
        public Entity SourceEntity;
        public Entity RelatedAbility;
        public Entity CueEntity;
        public CueSourceType CueSourceType;
        public int ContextId;
        public int EventCode;
        public int ReasonCode;
        public int RelatedAbilityCode;
        public int AttrSetCode;
        public int AttributeCode;
        public int TagIndex;
        public float Value;
        public float OldValue;
        public float NewValue;
        public float DamageAmount;
        public byte Flag;
    }
}
