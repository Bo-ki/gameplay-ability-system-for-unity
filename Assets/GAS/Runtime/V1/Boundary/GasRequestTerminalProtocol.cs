using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 冻结一个已接受 Boundary 请求的公开稳定身份，三项字段共同阻断跨 Session 与重试碰撞。
    /// </summary>
    public readonly struct GasRequestKey : IEquatable<GasRequestKey>
    {
        public readonly ulong SimulationEpoch;
        public readonly ulong RequestId;
        public readonly ulong RequestSequence;

        /// <summary>
        /// 使用完整 Session、调用方与 Gate 分配身份创建请求键。
        /// </summary>
        public GasRequestKey(
            ulong simulationEpoch,
            ulong requestId,
            ulong requestSequence)
        {
            SimulationEpoch = simulationEpoch;
            RequestId = requestId;
            RequestSequence = requestSequence;
        }

        public bool IsValid => SimulationEpoch != 0 && RequestId != 0 && RequestSequence != 0;

        /// <summary>
        /// 比较请求键的 Epoch、RequestId 与 RequestSequence。
        /// </summary>
        public bool Equals(GasRequestKey other)
        {
            return SimulationEpoch == other.SimulationEpoch &&
                   RequestId == other.RequestId &&
                   RequestSequence == other.RequestSequence;
        }

        /// <summary>
        /// 比较对象是否表示相同的公开请求键。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is GasRequestKey other && Equals(other);
        }

        /// <summary>
        /// 计算覆盖请求键全部字段的哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)(SimulationEpoch ^ (SimulationEpoch >> 32));
                hash = (hash * 397) ^ (int)(RequestId ^ (RequestId >> 32));
                return (hash * 397) ^ (int)(RequestSequence ^ (RequestSequence >> 32));
            }
        }

        /// <summary>
        /// 判断两个公开请求键是否值相等。
        /// </summary>
        public static bool operator ==(GasRequestKey left, GasRequestKey right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个公开请求键是否存在任一字段差异。
        /// </summary>
        public static bool operator !=(GasRequestKey left, GasRequestKey right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 区分单请求业务结果与三个会关闭 ingress 的 Session/Battle 生命周期原因。
    /// </summary>
    public enum GasRequestTerminalStatus : byte
    {
        None = 0,
        Succeeded = 1,
        Rejected = 2,
        SessionFault = 3,
        BattleTerminal = 4,
        OwnerDisposal = 5,
    }

    /// <summary>
    /// 将内部 GameplayEffect transaction outcome 投影为可由 Boundary consumer 稳定读取的闭世界结果。
    /// </summary>
    public enum GasEffectRequestResult : byte
    {
        None = 0,
        AppliedInstant = 1,
        CreatedActive = 2,
        MergedStack = 3,
        RejectedRequirement = 4,
        RejectedImmunity = 5,
        RejectedTargetLife = 6,
        RejectedStackPolicy = 7,
        RejectedStaleBinding = 8,
        RejectedDefinition = 9,
        RejectedSourceUnavailable = 10,
        InfrastructureFault = 11,
    }

    /// <summary>
    /// 保存一个 Accepted RequestKey 恰好一次的 typed 业务终态及继续调用所需稳定句柄。
    /// </summary>
    public readonly struct GasRequestTerminal : IEquatable<GasRequestTerminal>
    {
        public readonly GasRequestKey RequestKey;
        public readonly GasBoundaryCommandKind CommandKind;
        public readonly GasRequestTerminalStatus Status;
        public readonly GasAbilityCommandResult AbilityResult;
        public readonly GasEffectRequestResult EffectResult;
        public readonly AbilityActivationHandle AbilityActivation;
        public readonly ulong ApplicationId;
        public readonly ActiveEffectHandle ActiveEffect;
        public readonly ulong FaultId;
        public readonly BattleInstanceHandle BattleInstance;
        public readonly int BattleOutcomeCode;

        public bool IsSuccess => Status == GasRequestTerminalStatus.Succeeded;
        public bool HasAbilityActivation => AbilityActivation.IsValid;
        public bool HasApplication => ApplicationId != 0;
        public bool HasActiveEffect => ActiveEffect.IsValid;

        /// <summary>
        /// 从 Kernel 冻结的 typed 结果创建只读 RequestTerminal；公开调用方只能读取，不能伪造 ledger 写入。
        /// </summary>
        internal GasRequestTerminal(
            in GasRequestKey requestKey,
            GasBoundaryCommandKind commandKind,
            GasRequestTerminalStatus status,
            GasAbilityCommandResult abilityResult,
            GasEffectRequestResult effectResult,
            in AbilityActivationHandle abilityActivation,
            ulong applicationId,
            in ActiveEffectHandle activeEffect,
            ulong faultId,
            in BattleInstanceHandle battleInstance,
            int battleOutcomeCode)
        {
            RequestKey = requestKey;
            CommandKind = commandKind;
            Status = status;
            AbilityResult = abilityResult;
            EffectResult = effectResult;
            AbilityActivation = abilityActivation;
            ApplicationId = applicationId;
            ActiveEffect = activeEffect;
            FaultId = faultId;
            BattleInstance = battleInstance;
            BattleOutcomeCode = battleOutcomeCode;
        }

        /// <summary>
        /// 创建 Ability 成功或业务拒绝的单请求终态。
        /// </summary>
        internal static GasRequestTerminal ForAbility(
            in GasRequestKey requestKey,
            GasBoundaryCommandKind commandKind,
            GasRequestTerminalStatus status,
            GasAbilityCommandResult result,
            in AbilityActivationHandle activation)
        {
            return new GasRequestTerminal(
                in requestKey,
                commandKind,
                status,
                result,
                GasEffectRequestResult.None,
                in activation,
                0,
                default,
                0,
                default,
                0);
        }

        /// <summary>
        /// 创建 ApplyEffect 成功或业务拒绝的单请求终态。
        /// </summary>
        internal static GasRequestTerminal ForEffect(
            in GasRequestKey requestKey,
            GasRequestTerminalStatus status,
            GasEffectRequestResult result,
            ulong applicationId,
            in ActiveEffectHandle activeEffect)
        {
            return new GasRequestTerminal(
                in requestKey,
                GasBoundaryCommandKind.ApplyEffect,
                status,
                GasAbilityCommandResult.None,
                result,
                default,
                applicationId,
                in activeEffect,
                0,
                default,
                0);
        }

        /// <summary>
        /// 为尚无业务终态的 Accepted 请求创建唯一 Session/Battle teardown 结果。
        /// </summary>
        internal static GasRequestTerminal ForCutoff(
            in GasRequestKey requestKey,
            GasBoundaryCommandKind commandKind,
            GasRequestTerminalStatus status,
            ulong faultId,
            in BattleInstanceHandle battleInstance,
            int battleOutcomeCode)
        {
            return new GasRequestTerminal(
                in requestKey,
                commandKind,
                status,
                GasAbilityCommandResult.None,
                GasEffectRequestResult.None,
                default,
                0,
                default,
                faultId,
                in battleInstance,
                battleOutcomeCode);
        }

        /// <summary>
        /// 校验 RequestTerminal 的 key、命令种类、状态与 continuation handle 形成规范闭世界组合。
        /// </summary>
        internal bool IsWellFormed()
        {
            if (!RequestKey.IsValid || !IsKnownCommand(CommandKind) ||
                Status == GasRequestTerminalStatus.None)
                return false;
            if (Status == GasRequestTerminalStatus.SessionFault)
                return FaultId != 0 && !HasBusinessResult();
            if (Status == GasRequestTerminalStatus.BattleTerminal)
                return BattleInstance.IsValid && FaultId == 0 && !HasBusinessResult();
            if (Status == GasRequestTerminalStatus.OwnerDisposal)
                return FaultId == 0 && !HasBusinessResult();
            if (FaultId != 0 || BattleInstance.IsValid || BattleOutcomeCode != 0)
                return false;
            if (CommandKind == GasBoundaryCommandKind.ApplyEffect)
                return IsWellFormedEffectResult();
            return IsWellFormedAbilityResult();
        }

        /// <summary>
        /// 比较 RequestTerminal 的全部稳定字段，供重复发布保持幂等。
        /// </summary>
        public bool Equals(GasRequestTerminal other)
        {
            return RequestKey.Equals(other.RequestKey) &&
                   CommandKind == other.CommandKind &&
                   Status == other.Status &&
                   AbilityResult == other.AbilityResult &&
                   EffectResult == other.EffectResult &&
                   AbilityActivation.Equals(other.AbilityActivation) &&
                   ApplicationId == other.ApplicationId &&
                   ActiveEffect.Equals(other.ActiveEffect) &&
                   FaultId == other.FaultId &&
                   BattleInstance.Equals(other.BattleInstance) &&
                   BattleOutcomeCode == other.BattleOutcomeCode;
        }

        /// <summary>
        /// 比较对象是否表示相同的 RequestTerminal。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is GasRequestTerminal other && Equals(other);
        }

        /// <summary>
        /// 计算覆盖 RequestTerminal 全部稳定字段的哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = RequestKey.GetHashCode();
                hash = (hash * 397) ^ (int)CommandKind;
                hash = (hash * 397) ^ (int)Status;
                hash = (hash * 397) ^ (int)AbilityResult;
                hash = (hash * 397) ^ (int)EffectResult;
                hash = (hash * 397) ^ AbilityActivation.GetHashCode();
                hash = (hash * 397) ^ (int)(ApplicationId ^ (ApplicationId >> 32));
                hash = (hash * 397) ^ ActiveEffect.GetHashCode();
                hash = (hash * 397) ^ (int)(FaultId ^ (FaultId >> 32));
                hash = (hash * 397) ^ BattleInstance.GetHashCode();
                return (hash * 397) ^ BattleOutcomeCode;
            }
        }

        /// <summary>
        /// 判断两个 RequestTerminal 是否值相等。
        /// </summary>
        public static bool operator ==(GasRequestTerminal left, GasRequestTerminal right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个 RequestTerminal 是否存在任一字段差异。
        /// </summary>
        public static bool operator !=(GasRequestTerminal left, GasRequestTerminal right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// 判断终态是否错误夹带了 Ability 或 Effect 业务结果。
        /// </summary>
        private bool HasBusinessResult()
        {
            return AbilityResult != GasAbilityCommandResult.None ||
                   EffectResult != GasEffectRequestResult.None ||
                   HasAbilityActivation || HasApplication || HasActiveEffect;
        }

        /// <summary>
        /// 校验 ApplyEffect terminal 的结果类别、ApplicationId 与 ActiveEffect lifetime 句柄。
        /// </summary>
        private bool IsWellFormedEffectResult()
        {
            if (AbilityResult != GasAbilityCommandResult.None || HasAbilityActivation ||
                EffectResult == GasEffectRequestResult.None)
                return false;
            var successful = EffectResult == GasEffectRequestResult.AppliedInstant ||
                             EffectResult == GasEffectRequestResult.CreatedActive ||
                             EffectResult == GasEffectRequestResult.MergedStack;
            if (successful)
            {
                var activeLifetime = EffectResult == GasEffectRequestResult.CreatedActive ||
                                     EffectResult == GasEffectRequestResult.MergedStack;
                return Status == GasRequestTerminalStatus.Succeeded &&
                       ApplicationId != 0 && HasActiveEffect == activeLifetime;
            }
            if (Status != GasRequestTerminalStatus.Rejected || HasActiveEffect)
                return false;
            if (EffectResult == GasEffectRequestResult.RejectedDefinition ||
                EffectResult == GasEffectRequestResult.RejectedSourceUnavailable)
                return ApplicationId == 0;
            return IsApplicationStageEffectRejection(EffectResult) && ApplicationId != 0;
        }

        /// <summary>
        /// 区分已经分配 ApplicationId 的 application-stage 业务拒绝，基础设施 fault 不得伪装成普通终态。
        /// </summary>
        private static bool IsApplicationStageEffectRejection(GasEffectRequestResult result)
        {
            return result == GasEffectRequestResult.RejectedRequirement ||
                   result == GasEffectRequestResult.RejectedImmunity ||
                   result == GasEffectRequestResult.RejectedTargetLife ||
                   result == GasEffectRequestResult.RejectedStackPolicy ||
                   result == GasEffectRequestResult.RejectedStaleBinding;
        }

        /// <summary>
        /// 校验 Activate/Commit terminal 的 typed result 与仅 Activate 成功可返回的新激活句柄。
        /// </summary>
        private bool IsWellFormedAbilityResult()
        {
            if (EffectResult != GasEffectRequestResult.None || HasApplication || HasActiveEffect ||
                AbilityResult == GasAbilityCommandResult.None)
                return false;
            var successful = AbilityResult == GasAbilityCommandResult.Activated ||
                             AbilityResult == GasAbilityCommandResult.Committed;
            if ((Status == GasRequestTerminalStatus.Succeeded) != successful)
                return false;
            if (CommandKind == GasBoundaryCommandKind.Activate)
                return successful ? HasAbilityActivation : !HasAbilityActivation;
            return CommandKind == GasBoundaryCommandKind.Commit && !HasAbilityActivation;
        }

        /// <summary>
        /// 判断命令是否属于本轮可产生 Accepted RequestTerminal 的三项公开支持面。
        /// </summary>
        private static bool IsKnownCommand(GasBoundaryCommandKind commandKind)
        {
            return commandKind == GasBoundaryCommandKind.Activate ||
                   commandKind == GasBoundaryCommandKind.Commit ||
                   commandKind == GasBoundaryCommandKind.ApplyEffect;
        }
    }

    /// <summary>
    /// 表示公开 bootstrap grant resolver 的互斥结果，避免调用方通过 ECS buffer 猜测句柄。
    /// </summary>
    public enum GasBootstrapGrantResolveStatus : byte
    {
        None = 0,
        Resolved = 1,
        InvalidRequest = 2,
        SessionUnavailable = 3,
        OwnerNotReady = 4,
        GrantNotFound = 5,
        AmbiguousGrant = 6,
    }

    /// <summary>
    /// 返回指定 ASC 与 Ability definition 的唯一 bootstrap GrantedAbilityHandle。
    /// </summary>
    public readonly struct GasBootstrapGrantReceipt
    {
        public readonly GasBootstrapGrantResolveStatus Status;
        public readonly ulong SimulationEpoch;
        public readonly OwnerAscHandle OwnerAsc;
        public readonly int AbilityDefinitionId;
        public readonly GrantedAbilityHandle GrantedAbility;

        public bool IsResolved => Status == GasBootstrapGrantResolveStatus.Resolved &&
                                  GrantedAbility.IsValid;

        /// <summary>
        /// 创建不暴露 Entity 或 buffer 的 bootstrap grant 查询回执。
        /// </summary>
        internal GasBootstrapGrantReceipt(
            GasBootstrapGrantResolveStatus status,
            ulong simulationEpoch,
            in OwnerAscHandle ownerAsc,
            int abilityDefinitionId,
            in GrantedAbilityHandle grantedAbility)
        {
            Status = status;
            SimulationEpoch = simulationEpoch;
            OwnerAsc = ownerAsc;
            AbilityDefinitionId = abilityDefinitionId;
            GrantedAbility = grantedAbility;
        }
    }

    /// <summary>
    /// 区分 Kernel terminal intent 首次发布、幂等重复与违反唯一终态的不一致写入。
    /// </summary>
    internal enum GasRequestTerminalPublishStatus : byte
    {
        Published = 1,
        Duplicate = 2,
        UnknownRequest = 3,
        InvalidTerminal = 4,
        Conflict = 5,
    }
}
