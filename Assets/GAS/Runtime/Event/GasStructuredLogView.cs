namespace GAS.Runtime
{
    /// <summary>
    /// 标识 managed 结构化日志级别。
    /// </summary>
    public enum EGasStructuredLogLevel : byte
    {
        Trace = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    /// <summary>
    /// 标识 managed 结构化日志所属模块。
    /// </summary>
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

    /// <summary>
    /// 保存从 Runtime v1 Boundary fact 投影出的不可变 managed 日志；只携带稳定业务键，不暴露 ECS Entity。
    /// </summary>
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
        public readonly int SourceReportKey;
        public readonly int TargetReportKey;
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

        /// <summary>
        /// 构造一个只读日志条目；调用方必须先把 Runtime handle 投影为稳定报告键。
        /// </summary>
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
            int sourceReportKey,
            int targetReportKey,
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
            SourceReportKey = sourceReportKey;
            TargetReportKey = targetReportKey;
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
}
