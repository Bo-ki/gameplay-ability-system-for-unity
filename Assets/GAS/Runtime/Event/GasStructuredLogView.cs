using Unity.Entities;

namespace GAS.Runtime
{
    public enum EGasStructuredLogLevel : byte
    {
        Trace = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    public enum EGasStructuredLogModule : byte
    {
        Unknown = 0,
        Ability = 1,
        GameplayEffect = 2,
        Attribute = 3,
        GameplayTag = 4,
        GameplayCue = 5,
        ExecutionCalculation = 6,
        RuntimeBoundary = 7,
        Config = 8,
        Presentation = 9,
        Damage = 10,
    }

    public readonly struct GasStructuredLogEntry
    {
        public readonly int LogIndex;
        public readonly int Frame;
        public readonly int Sequence;
        public readonly EGasStructuredLogLevel Level;
        public readonly EGasStructuredLogModule Module;
        public readonly EGameplayFactDomain FactDomain;
        public readonly EGameplayFactCategory FactCategory;
        public readonly EGameplayFactSeverity FactSeverity;
        public readonly EDebugReplayEventKind ReplayKind;
        public readonly EGameplayEventType GameplayEventType;
        public readonly EGameplayCueEvent CueEvent;
        public readonly Entity SourceAsc;
        public readonly Entity TargetAsc;
        public readonly Entity SourceAbility;
        public readonly Entity GameplayEffect;
        public readonly Entity SourceEntity;
        public readonly Entity RelatedAbility;
        public readonly Entity CueEntity;
        public readonly CueSourceType CueSourceType;
        public readonly int ContextId;
        public readonly int EventCode;
        public readonly int ReasonCode;
        public readonly int RelatedAbilityCode;
        public readonly int AttrSetCode;
        public readonly int AttributeCode;
        public readonly int TagIndex;
        public readonly float Value;
        public readonly float OldValue;
        public readonly float NewValue;
        public readonly float DamageAmount;
        public readonly byte Flag;

        public GasStructuredLogEntry(
            int logIndex,
            int frame,
            int sequence,
            EGasStructuredLogLevel level,
            EGasStructuredLogModule module,
            EGameplayFactDomain factDomain,
            EGameplayFactCategory factCategory,
            EGameplayFactSeverity factSeverity,
            EDebugReplayEventKind replayKind,
            EGameplayEventType gameplayEventType,
            EGameplayCueEvent cueEvent,
            Entity sourceAsc,
            Entity targetAsc,
            Entity sourceAbility,
            Entity gameplayEffect,
            Entity sourceEntity,
            Entity relatedAbility,
            Entity cueEntity,
            CueSourceType cueSourceType,
            int contextId,
            int eventCode,
            int reasonCode,
            int relatedAbilityCode,
            int attrSetCode,
            int attributeCode,
            int tagIndex,
            float value,
            float oldValue,
            float newValue,
            float damageAmount,
            byte flag)
        {
            LogIndex = logIndex;
            Frame = frame;
            Sequence = sequence;
            Level = level;
            Module = module;
            FactDomain = factDomain;
            FactCategory = factCategory;
            FactSeverity = factSeverity;
            ReplayKind = replayKind;
            GameplayEventType = gameplayEventType;
            CueEvent = cueEvent;
            SourceAsc = sourceAsc;
            TargetAsc = targetAsc;
            SourceAbility = sourceAbility;
            GameplayEffect = gameplayEffect;
            SourceEntity = sourceEntity;
            RelatedAbility = relatedAbility;
            CueEntity = cueEntity;
            CueSourceType = cueSourceType;
            ContextId = contextId;
            EventCode = eventCode;
            ReasonCode = reasonCode;
            RelatedAbilityCode = relatedAbilityCode;
            AttrSetCode = attrSetCode;
            AttributeCode = attributeCode;
            TagIndex = tagIndex;
            Value = value;
            OldValue = oldValue;
            NewValue = newValue;
            DamageAmount = damageAmount;
            Flag = flag;
        }

        public bool IsReplayBacked => LogIndex >= 0;
        public bool IsFailure => FactCategory == EGameplayFactCategory.Failure;
        public bool IsDiagnostic => FactCategory == EGameplayFactCategory.Diagnostic;
    }

    public static class GasStructuredLogView
    {
        public const int NoReplayLogIndex = -1;

        public static GasStructuredLogEntry FromReplay(in BDebugReplayEvent evt)
        {
            return Create(evt.LogIndex, evt, GameplayFactClassifier.Classify(evt));
        }

        public static GasStructuredLogEntry FromGameplayEvent(in BGameplayEvent evt)
        {
            var replay = new BDebugReplayEvent
            {
                LogIndex = NoReplayLogIndex,
                Frame = evt.Frame,
                Sequence = evt.Sequence,
                Kind = EDebugReplayEventKind.GameplayEvent,
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

            return Create(NoReplayLogIndex, replay, GameplayFactClassifier.Classify(evt.Type));
        }

        public static GasStructuredLogEntry FromAttributeChange(in BAttributeChangeEvent evt)
        {
            var replay = new BDebugReplayEvent
            {
                LogIndex = NoReplayLogIndex,
                Kind = EDebugReplayEventKind.AttributeChange,
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

            return Create(NoReplayLogIndex, replay, GameplayFactClassifier.Classify(replay));
        }

        public static GasStructuredLogEntry FromCueRequest(in BCueRequest evt)
        {
            var replay = new BDebugReplayEvent
            {
                LogIndex = NoReplayLogIndex,
                Kind = EDebugReplayEventKind.CueRequest,
                CueEvent = evt.CueEvent,
                SourceAsc = evt.SourceAsc,
                TargetAsc = evt.TargetAsc,
                SourceAbility = evt.SourceAbility,
                GameplayEffect = evt.GameplayEffect,
                SourceEntity = evt.SourceEntity,
                CueEntity = evt.CueEntity,
                CueSourceType = evt.SourceType,
                ContextId = evt.ContextId,
                EventCode = (int)evt.CueEvent,
            };

            return Create(NoReplayLogIndex, replay, GameplayFactClassifier.Classify(replay));
        }

        public static GasStructuredLogEntry FromTagChange(in BTagChangeEvent evt)
        {
            var replay = new BDebugReplayEvent
            {
                LogIndex = NoReplayLogIndex,
                Kind = EDebugReplayEventKind.TagChange,
                TargetAsc = evt.ASC,
                TagIndex = evt.TagIndex,
                Flag = evt.Added ? (byte)1 : (byte)0,
            };

            return Create(NoReplayLogIndex, replay, GameplayFactClassifier.Classify(replay));
        }

        public static GasStructuredLogEntry FromDamage(in BDamageEvent evt)
        {
            var replay = new BDebugReplayEvent
            {
                LogIndex = NoReplayLogIndex,
                Kind = EDebugReplayEventKind.Damage,
                SourceAsc = evt.Source,
                TargetAsc = evt.Target,
                DamageAmount = evt.Amount,
                Value = evt.Amount,
            };

            return Create(NoReplayLogIndex, replay, GameplayFactClassifier.Classify(replay));
        }

        public static EGasStructuredLogModule ToModule(EGameplayFactDomain domain)
        {
            return domain switch
            {
                EGameplayFactDomain.Ability => EGasStructuredLogModule.Ability,
                EGameplayFactDomain.GameplayEffect => EGasStructuredLogModule.GameplayEffect,
                EGameplayFactDomain.Attribute => EGasStructuredLogModule.Attribute,
                EGameplayFactDomain.Tag => EGasStructuredLogModule.GameplayTag,
                EGameplayFactDomain.Cue => EGasStructuredLogModule.GameplayCue,
                EGameplayFactDomain.ExecutionCalculation => EGasStructuredLogModule.ExecutionCalculation,
                EGameplayFactDomain.RuntimeBoundary => EGasStructuredLogModule.RuntimeBoundary,
                EGameplayFactDomain.Presentation => EGasStructuredLogModule.Presentation,
                EGameplayFactDomain.Damage => EGasStructuredLogModule.Damage,
                _ => EGasStructuredLogModule.Unknown,
            };
        }

        public static EGasStructuredLogLevel ToLevel(EGameplayFactSeverity severity)
        {
            return severity switch
            {
                EGameplayFactSeverity.Trace => EGasStructuredLogLevel.Trace,
                EGameplayFactSeverity.Info => EGasStructuredLogLevel.Info,
                EGameplayFactSeverity.Warning => EGasStructuredLogLevel.Warning,
                EGameplayFactSeverity.Error => EGasStructuredLogLevel.Error,
                _ => EGasStructuredLogLevel.Warning,
            };
        }

        private static GasStructuredLogEntry Create(
            int logIndex,
            in BDebugReplayEvent evt,
            GameplayFactClassification classification)
        {
            return new GasStructuredLogEntry(
                logIndex,
                evt.Frame,
                evt.Sequence,
                ToLevel(classification.Severity),
                ToModule(classification.Domain),
                classification.Domain,
                classification.Category,
                classification.Severity,
                evt.Kind,
                evt.GameplayEventType,
                evt.CueEvent,
                evt.SourceAsc,
                evt.TargetAsc,
                evt.SourceAbility,
                evt.GameplayEffect,
                evt.SourceEntity,
                evt.RelatedAbility,
                evt.CueEntity,
                evt.CueSourceType,
                evt.ContextId,
                evt.EventCode,
                evt.ReasonCode,
                evt.RelatedAbilityCode,
                evt.AttrSetCode,
                evt.AttributeCode,
                evt.TagIndex,
                evt.Value,
                evt.OldValue,
                evt.NewValue,
                evt.DamageAmount,
                evt.Flag);
        }
    }
}
