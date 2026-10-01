using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 区分 Ability grant 的稳定来源，cleanup 只能解释与来源匹配的 provenance。
    /// </summary>
    public enum GasAbilityGrantSourceKind : byte
    {
        Bootstrap = 1,
        Command = 2,
        ActiveEffect = 3,
    }

    /// <summary>
    /// 冻结 granting owner 消失时 GrantedAbility 的三种互斥处理策略。
    /// </summary>
    public enum GasGrantedAbilityRemovalPolicy : byte
    {
        CancelImmediately = 1,
        RemoveWhenAllActivationsEnd = 2,
        LeaveGranted = 3,
    }

    /// <summary>
    /// 表示 GrantedAbility cleanup 是否已阻止新激活或完成 provenance 脱钩。
    /// </summary>
    public enum GasGrantedAbilityRemovalState : byte
    {
        None = 0,
        PendingRemove = 1,
        ProvenanceDetached = 2,
    }

    /// <summary>
    /// 定义 AbilityActivation 从未提交运行到结束回收的单向业务阶段。
    /// </summary>
    public enum GasAbilityActivationPhase : byte
    {
        RunningUncommitted = 1,
        Committed = 2,
        Ending = 3,
        Ended = 4,
    }

    /// <summary>
    /// 冻结首次进入 Ending 的原因，后续结束命令不得覆盖。
    /// </summary>
    public enum GasAbilityEndReason : ushort
    {
        None = 0,
        Completed = 1,
        Cancelled = 2,
        GrantRemoved = 3,
        OwnerTerminal = 4,
        CommitRejected = 5,
    }

    /// <summary>
    /// 表示 Boundary Ability command 的确定性业务结果，基础设施 fault 不使用本枚举。
    /// </summary>
    public enum GasAbilityCommandResult : ushort
    {
        None = 0,
        Activated = 1,
        Committed = 2,
        Ended = 3,
        DuplicateEndNoOp = 4,
        NotAbilityCommand = 5,
        OwnerNotReady = 100,
        HandleInvalid = 101,
        GrantPendingRemove = 102,
        ConcurrencyBlocked = 103,
        CostUnavailable = 104,
        CooldownActive = 105,
        AlreadyCommitted = 106,
        OwnerEnding = 107,
        DurableCapacityUnavailable = 108,
        DefinitionInvalid = 109,
        ActivationRequirementFailed = 110,
        TargetInvalid = 111,
    }

    /// <summary>
    /// 区分 Activation 结束时可精确撤销的 owner-local contribution 类型。
    /// </summary>
    public enum GasAbilityContributionKind : byte
    {
        OwnedTag = 1,
        Block = 2,
        Cancel = 3,
        LifecycleCue = 4,
        RemoveOnActivationEndApplication = 5,
    }

    /// <summary>
    /// 冻结 emitted application ref 是否只供审计或拥有逐 application cleanup 权。
    /// </summary>
    public enum GasEmittedApplicationCleanupPolicy : byte
    {
        AuditOnly = 1,
        RemoveOnActivationEnd = 2,
    }

    /// <summary>
    /// 定义 ASC-local PendingCommand 承载的 Ability 内部闭世界消息种类。
    /// </summary>
    internal enum GasAbilityPendingCommandKind : int
    {
        None = 0,
        RequestGrantedRemoval = 1,
        WaitRegistration = 2,
        WaitRegistrationAck = 3,
        WaitCompletion = 4,
        WaitUnsubscribe = 5,
        WaitUnsubscribeAck = 6,
        WaitSignal = 7,
    }

    /// <summary>
    /// 冻结 HandleLifecycle wait 可观察的领域句柄联合体，显式 Kind 不得替代为诊断载体。
    /// </summary>
    public readonly struct GasAbilityObservedHandle : IEquatable<GasAbilityObservedHandle>
    {
        public readonly ulong SimulationEpoch;
        public readonly OwnerAscHandle OwnerAsc;
        public readonly int SlotIndex;
        public readonly uint SlotGeneration;
        public readonly HandleKind Kind;

        /// <summary>
        /// 只允许具名 typed handle 工厂建立业务联合体。
        /// </summary>
        private GasAbilityObservedHandle(
            ulong simulationEpoch,
            in OwnerAscHandle ownerAsc,
            int slotIndex,
            uint slotGeneration,
            HandleKind kind)
        {
            SimulationEpoch = simulationEpoch;
            OwnerAsc = ownerAsc;
            SlotIndex = slotIndex;
            SlotGeneration = slotGeneration;
            Kind = kind;
        }

        public bool IsValid => SimulationEpoch != 0 && OwnerAsc.IsValid && SlotIndex >= 0 &&
                               SlotGeneration != 0 && Kind >= HandleKind.GrantedAbility &&
                               Kind <= HandleKind.ActiveEffect;

        /// <summary>
        /// 从 GrantedAbility typed handle 冻结可观察身份。
        /// </summary>
        public static GasAbilityObservedHandle From(in GrantedAbilityHandle handle)
        {
            return Create(handle.SimulationEpoch, in handle.OwnerAsc,
                handle.SlotIndex, handle.SlotGeneration, GrantedAbilityHandle.Kind);
        }

        /// <summary>
        /// 从 AbilityActivation typed handle 冻结可观察身份。
        /// </summary>
        public static GasAbilityObservedHandle From(in AbilityActivationHandle handle)
        {
            return Create(handle.SimulationEpoch, in handle.OwnerAsc,
                handle.SlotIndex, handle.SlotGeneration, AbilityActivationHandle.Kind);
        }

        /// <summary>
        /// 从 AbilityContinuation typed handle 冻结可观察身份。
        /// </summary>
        public static GasAbilityObservedHandle From(in AbilityContinuationHandle handle)
        {
            return Create(handle.SimulationEpoch, in handle.OwnerAsc,
                handle.SlotIndex, handle.SlotGeneration, AbilityContinuationHandle.Kind);
        }

        /// <summary>
        /// 从 AbilitySubscription typed handle 冻结可观察身份。
        /// </summary>
        public static GasAbilityObservedHandle From(in AbilitySubscriptionHandle handle)
        {
            return Create(handle.SimulationEpoch, in handle.OwnerAsc,
                handle.SlotIndex, handle.SlotGeneration, AbilitySubscriptionHandle.Kind);
        }

        /// <summary>
        /// 从 CooldownGate typed handle 冻结可观察身份。
        /// </summary>
        public static GasAbilityObservedHandle From(in CooldownGateHandle handle)
        {
            return Create(handle.SimulationEpoch, in handle.OwnerAsc,
                handle.SlotIndex, handle.SlotGeneration, CooldownGateHandle.Kind);
        }

        /// <summary>
        /// 从 ActiveEffect typed handle 冻结可观察身份。
        /// </summary>
        public static GasAbilityObservedHandle From(in ActiveEffectHandle handle)
        {
            return Create(handle.SimulationEpoch, in handle.OwnerAsc,
                handle.SlotIndex, handle.SlotGeneration, ActiveEffectHandle.Kind);
        }

        /// <summary>
        /// 比较 observed handle 联合体的全部稳定身份字段。
        /// </summary>
        public bool Equals(GasAbilityObservedHandle other)
        {
            return SimulationEpoch == other.SimulationEpoch && OwnerAsc.Equals(other.OwnerAsc) &&
                   SlotIndex == other.SlotIndex && SlotGeneration == other.SlotGeneration &&
                   Kind == other.Kind;
        }

        /// <summary>
        /// 比较对象是否为相同 observed handle 联合体。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is GasAbilityObservedHandle other && Equals(other);
        }

        /// <summary>
        /// 计算覆盖全部稳定身份字段的哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)(SimulationEpoch ^ (SimulationEpoch >> 32));
                hashCode = (hashCode * 397) ^ OwnerAsc.GetHashCode();
                hashCode = (hashCode * 397) ^ SlotIndex;
                hashCode = (hashCode * 397) ^ (int)SlotGeneration;
                return (hashCode * 397) ^ (int)Kind;
            }
        }

        /// <summary>
        /// 仅在 typed source handle 完整有效时创建业务联合体。
        /// </summary>
        private static GasAbilityObservedHandle Create(
            ulong simulationEpoch,
            in OwnerAscHandle ownerAsc,
            int slotIndex,
            uint slotGeneration,
            HandleKind kind)
        {
            var value = new GasAbilityObservedHandle(
                simulationEpoch, in ownerAsc, slotIndex, slotGeneration, kind);
            return value.IsValid ? value : default;
        }
    }
}
