using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// Simulation fact stream singleton.
    /// Buffers on this entity are one-tick transient streams: SEventBusClear clears
    /// the previous tick at the start of GASCommandGroup, and later systems append
    /// current-tick facts for Cue/presentation/debug consumers.
    /// </summary>
    public struct CGameplayEventBus : IComponentData
    {
        public int NextSequence;
    }

    /// <summary>
    /// 伤害事件。入队到事件总线 Singleton 的 BDamageEvent Buffer。
    /// </summary>
    public struct BDamageEvent : IBufferElementData
    {
        public Entity Target;
        public Entity Source;
        public float Amount;
    }

    /// <summary>
    /// Tag 变更事件。入队到事件总线 Singleton 的 BTagChangeEvent Buffer。
    /// </summary>
    public struct BTagChangeEvent : IBufferElementData
    {
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
        AutoChessUnitDefeated = 27,
        AutoChessBattleResolved = 28,
        AutoChessPassiveTriggered = 29,
        AutoChessKillManaGranted = 30,
        AutoChessReviveRequested = 31,
        AutoChessReviveApplied = 32,
        AutoChessSynergyActivated = 33,
        AutoChessSynergyExpired = 34,
        AutoChessSynergyAllyBuffRequested = 35,
        AutoChessSynergyEnemyDebuffRequested = 36,
        AutoChessSynergyPeriodTicked = 37,
        AutoChessPresentationUiMarker = 38,
        AutoChessPresentationVfxMarker = 39,
        AutoChessPresentationSfxMarker = 40,
        AutoChessPresentationFloatingTextMarker = 41,
        AutoChessPresentationCueMarker = 42,
        AutoChessPresentationSettlementMarker = 43,
        AutoChessControlTurnSkipped = 44,
        AutoChessShieldApplied = 45,
        AutoChessShieldAbsorbed = 46,
        AutoChessShieldBroken = 47,
        AutoChessSummonRequested = 48,
        AutoChessSummonSpawned = 49,
        AutoChessSummonExpired = 50,
        AutoChessSummonDespawned = 51,
        AutoChessDamageTypeResolved = 52,
        AutoChessDamageResisted = 53,
        AutoChessEquipmentApplied = 54,
        AutoChessCounterTriggered = 55,
        AutoChessCounterDamageApplied = 56,
        AutoChessCleanseRequested = 57,
        AutoChessCleanseApplied = 58,
        AutoChessCleanseEffectRemoved = 59,
        AutoChessCleanseRallyRequested = 60,
        AutoChessCleanseRallyApplied = 61,
        AutoChessRallyComboTriggered = 62,
        AutoChessRallyComboDamageApplied = 63,
        AutoChessLifeStealTriggered = 64,
        AutoChessLifeStealHealed = 65,
        AutoChessPoisonStackRequested = 66,
        AutoChessPoisonStackChanged = 67,
        AutoChessPoisonOverflowTriggered = 68,
        AutoChessPoisonOverflowDamageApplied = 69,
        AutoChessPoisonPeriodDamageApplied = 70,
        AutoChessExecuteTriggered = 71,
        AutoChessExecuteDamageApplied = 72,
        AutoChessDeathBurstTriggered = 73,
        AutoChessDeathBurstDamageApplied = 74,
        AutoChessEnrageTriggered = 75,
        AutoChessEnrageApplied = 76,
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

                EGameplayEventType.AutoChessUnitDefeated => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessBattleResolved => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessPassiveTriggered => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessKillManaGranted => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessReviveRequested => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessReviveApplied => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessSynergyActivated => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessSynergyExpired => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessSynergyAllyBuffRequested => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessSynergyEnemyDebuffRequested => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessSynergyPeriodTicked => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessControlTurnSkipped => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessShieldApplied => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessShieldAbsorbed => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessShieldBroken => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessSummonRequested => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessSummonSpawned => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessSummonExpired => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessSummonDespawned => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessDamageTypeResolved => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessDamageResisted => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessEquipmentApplied => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessCounterTriggered => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessCounterDamageApplied => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessCleanseRequested => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessCleanseApplied => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessCleanseEffectRemoved => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessCleanseRallyRequested => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessCleanseRallyApplied => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessRallyComboTriggered => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessRallyComboDamageApplied => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessLifeStealTriggered => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessLifeStealHealed => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessPoisonStackRequested => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessPoisonStackChanged => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessPoisonOverflowTriggered => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessPoisonOverflowDamageApplied => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessPoisonPeriodDamageApplied => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessExecuteTriggered => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessExecuteDamageApplied => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessDeathBurstTriggered => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessDeathBurstDamageApplied => StateChange(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessEnrageTriggered => Request(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessEnrageApplied => StateTransition(EGameplayFactDomain.RuntimeBoundary),
                EGameplayEventType.AutoChessPresentationUiMarker => StateChange(EGameplayFactDomain.Presentation),
                EGameplayEventType.AutoChessPresentationVfxMarker => StateChange(EGameplayFactDomain.Presentation),
                EGameplayEventType.AutoChessPresentationSfxMarker => StateChange(EGameplayFactDomain.Presentation),
                EGameplayEventType.AutoChessPresentationFloatingTextMarker => StateChange(EGameplayFactDomain.Presentation),
                EGameplayEventType.AutoChessPresentationCueMarker => Request(EGameplayFactDomain.Presentation),
                EGameplayEventType.AutoChessPresentationSettlementMarker => StateTransition(EGameplayFactDomain.Presentation),

                _ => new GameplayFactClassification(
                    EGameplayFactDomain.Unknown,
                    EGameplayFactCategory.Unknown,
                    EGameplayFactSeverity.Warning),
            };
        }

        public static GameplayFactClassification Classify(in BDebugReplayEvent evt)
        {
            return evt.Kind switch
            {
                EDebugReplayEventKind.GameplayEvent => Classify(evt.GameplayEventType),
                EDebugReplayEventKind.AttributeChange => StateChange(EGameplayFactDomain.Attribute),
                EDebugReplayEventKind.CueRequest => Request(EGameplayFactDomain.Cue),
                EDebugReplayEventKind.TagChange => StateChange(EGameplayFactDomain.Tag),
                EDebugReplayEventKind.Damage => StateChange(EGameplayFactDomain.Attribute),
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
    /// Gameplay 事实事件。Simulation 写入，表现层/调试层消费。
    /// </summary>
    public struct BGameplayEvent : IBufferElementData
    {
        public int Frame;
        public int Sequence;
        public EGameplayEventType Type;
        public Entity SourceAsc;
        public Entity TargetAsc;
        public Entity SourceAbility;
        public Entity GameplayEffect;
        public Entity RelatedAbility;
        public int ContextId;
        public int EventCode;
        public int ReasonCode;
        public int RelatedAbilityCode;
        public float Value;
    }

    /// <summary>
    /// Attribute 变更事件。替代核心计算路径中的托管回调。
    /// </summary>
    public struct BAttributeChangeEvent : IBufferElementData
    {
        public Entity ASC;
        public Entity SourceAsc;
        public Entity SourceAbility;
        public Entity GameplayEffect;
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
    public struct BCueRequest : IBufferElementData
    {
        public Entity TargetAsc;
        public Entity SourceAsc;
        public Entity SourceAbility;
        public Entity GameplayEffect;
        public Entity SourceEntity;
        public CueSourceType SourceType;
        public Entity CueEntity;
        public int ContextId;
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
    public struct BPresentationEvent : IBufferElementData
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
    /// Tracks how much of the current-tick simulation fact stream has already
    /// been projected into per-ASC presentation outboxes.
    /// </summary>
    public struct CPresentationOutboxProjectionState : IComponentData
    {
        public int LastProjectedFrame;
        public int ProcessedGameplayEventCount;
        public int ProcessedAttributeEventCount;
        public int ProcessedCueRequestCount;
        public int ProcessedTagEventCount;
        public int ProcessedDamageEventCount;
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
    public struct CGameplayEventLogSink : IComponentData
    {
        public int NextLogIndex;
        public int FirstRetainedLogIndex;
        public int DroppedEventCount;
        public int MaxRetainedEvents;
        public int LastProjectedFrame;
        public int ProcessedGameplayEventCount;
        public int ProcessedAttributeEventCount;
        public int ProcessedCueRequestCount;
        public int ProcessedTagEventCount;
        public int ProcessedDamageEventCount;
    }

    /// <summary>
    /// Persistent debug/replay event. It mirrors simulation facts into a single
    /// append-only stream until explicitly cleared by tooling or tests.
    /// </summary>
    public struct BDebugReplayEvent : IBufferElementData
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
