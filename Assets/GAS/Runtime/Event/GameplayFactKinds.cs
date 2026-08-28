namespace GAS.Runtime
{
    /// <summary>
    /// 标识 managed Boundary 日志中的业务事实类型；它不是 Runtime gameplay 输入或 ECS EventBus。
    /// </summary>
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

    /// <summary>
    /// 标识 Boundary 日志事实所属的业务域。
    /// </summary>
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

    /// <summary>
    /// 标识 Boundary 日志事实的语义类别。
    /// </summary>
    public enum EGameplayFactCategory : byte
    {
        Unknown = 0,
        Request = 1,
        StateTransition = 2,
        StateChange = 3,
        Failure = 4,
        Diagnostic = 5,
    }

    /// <summary>
    /// 标识 Boundary 日志事实的观察严重级别。
    /// </summary>
    public enum EGameplayFactSeverity : byte
    {
        Trace = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    /// <summary>
    /// 标识 Cue 的 managed 展示阶段；该值不承担 Cue lifecycle 权威。
    /// </summary>
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
    /// 标识 managed 日志的投影形状；名称只用于报告兼容，不代表 Replay ECS stream。
    /// </summary>
    public enum EDebugReplayEventKind : byte
    {
        GameplayEvent = 0,
        AttributeChange = 1,
        CueRequest = 2,
        TagChange = 3,
        Damage = 4,
    }
}
