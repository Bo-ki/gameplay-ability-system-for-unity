using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 区分无类型诊断载体中的领域槽池，防止相同数字跨池误解析。
    /// </summary>
    public enum HandleKind : byte
    {
        None = 0,
        GrantedAbility = 1,
        AbilityActivation = 2,
        AbilityContinuation = 3,
        AbilitySubscription = 4,
        CooldownGate = 5,
        ActiveEffect = 6,
    }

    /// <summary>
    /// 标识 ASC 上一个存活的 Ability 授予槽。
    /// </summary>
    public readonly struct GrantedAbilityHandle : IEquatable<GrantedAbilityHandle>
    {
        public const HandleKind Kind = HandleKind.GrantedAbility;
        public readonly ulong SimulationEpoch;
        public readonly OwnerAscHandle OwnerAsc;
        public readonly int SlotIndex;
        public readonly uint SlotGeneration;

        /// <summary>
        /// 使用完整稳定身份创建 Ability 授予句柄。
        /// </summary>
        public GrantedAbilityHandle(
            ulong simulationEpoch,
            OwnerAscHandle ownerAsc,
            int slotIndex,
            uint slotGeneration)
        {
            SimulationEpoch = simulationEpoch;
            OwnerAsc = ownerAsc;
            SlotIndex = slotIndex;
            SlotGeneration = slotGeneration;
        }

        public bool IsValid => SimulationEpoch != 0 && OwnerAsc.IsValid && SlotIndex >= 0 && SlotGeneration != 0;

        /// <summary>
        /// 转换为只供诊断与统一校验使用的无类型载体。
        /// </summary>
        public StableHandleDiagnosticCarrier ToDiagnosticCarrier()
        {
            return new StableHandleDiagnosticCarrier(
                SimulationEpoch,
                OwnerAsc,
                SlotIndex,
                SlotGeneration,
                Kind);
        }

        /// <summary>
        /// 比较两个 Ability 授予句柄的全部身份字段。
        /// </summary>
        public bool Equals(GrantedAbilityHandle other)
        {
            return SimulationEpoch == other.SimulationEpoch &&
                   OwnerAsc.Equals(other.OwnerAsc) &&
                   SlotIndex == other.SlotIndex &&
                   SlotGeneration == other.SlotGeneration;
        }

        /// <summary>
        /// 比较对象是否为相同 Ability 授予句柄。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is GrantedAbilityHandle other && Equals(other);
        }

        /// <summary>
        /// 计算覆盖全部身份字段的哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)(SimulationEpoch ^ (SimulationEpoch >> 32));
                hashCode = (hashCode * 397) ^ OwnerAsc.GetHashCode();
                hashCode = (hashCode * 397) ^ SlotIndex;
                return (hashCode * 397) ^ (int)SlotGeneration;
            }
        }

        /// <summary>
        /// 判断两个 Ability 授予句柄是否值相等。
        /// </summary>
        public static bool operator ==(GrantedAbilityHandle left, GrantedAbilityHandle right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个 Ability 授予句柄是否存在任一身份差异。
        /// </summary>
        public static bool operator !=(GrantedAbilityHandle left, GrantedAbilityHandle right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 标识 ASC 上一次存活的 Ability 激活槽。
    /// </summary>
    public readonly struct AbilityActivationHandle : IEquatable<AbilityActivationHandle>
    {
        public const HandleKind Kind = HandleKind.AbilityActivation;
        public readonly ulong SimulationEpoch;
        public readonly OwnerAscHandle OwnerAsc;
        public readonly int SlotIndex;
        public readonly uint SlotGeneration;

        /// <summary>
        /// 使用完整稳定身份创建 Ability 激活句柄。
        /// </summary>
        public AbilityActivationHandle(
            ulong simulationEpoch,
            OwnerAscHandle ownerAsc,
            int slotIndex,
            uint slotGeneration)
        {
            SimulationEpoch = simulationEpoch;
            OwnerAsc = ownerAsc;
            SlotIndex = slotIndex;
            SlotGeneration = slotGeneration;
        }

        public bool IsValid => SimulationEpoch != 0 && OwnerAsc.IsValid && SlotIndex >= 0 && SlotGeneration != 0;

        /// <summary>
        /// 转换为只供诊断与统一校验使用的无类型载体。
        /// </summary>
        public StableHandleDiagnosticCarrier ToDiagnosticCarrier()
        {
            return new StableHandleDiagnosticCarrier(
                SimulationEpoch,
                OwnerAsc,
                SlotIndex,
                SlotGeneration,
                Kind);
        }

        /// <summary>
        /// 比较两个 Ability 激活句柄的全部身份字段。
        /// </summary>
        public bool Equals(AbilityActivationHandle other)
        {
            return SimulationEpoch == other.SimulationEpoch &&
                   OwnerAsc.Equals(other.OwnerAsc) &&
                   SlotIndex == other.SlotIndex &&
                   SlotGeneration == other.SlotGeneration;
        }

        /// <summary>
        /// 比较对象是否为相同 Ability 激活句柄。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is AbilityActivationHandle other && Equals(other);
        }

        /// <summary>
        /// 计算覆盖全部身份字段的哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)(SimulationEpoch ^ (SimulationEpoch >> 32));
                hashCode = (hashCode * 397) ^ OwnerAsc.GetHashCode();
                hashCode = (hashCode * 397) ^ SlotIndex;
                return (hashCode * 397) ^ (int)SlotGeneration;
            }
        }

        /// <summary>
        /// 判断两个 Ability 激活句柄是否值相等。
        /// </summary>
        public static bool operator ==(AbilityActivationHandle left, AbilityActivationHandle right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个 Ability 激活句柄是否存在任一身份差异。
        /// </summary>
        public static bool operator !=(AbilityActivationHandle left, AbilityActivationHandle right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 标识 ASC 上一个存活的 Ability 延续槽。
    /// </summary>
    public readonly struct AbilityContinuationHandle : IEquatable<AbilityContinuationHandle>
    {
        public const HandleKind Kind = HandleKind.AbilityContinuation;
        public readonly ulong SimulationEpoch;
        public readonly OwnerAscHandle OwnerAsc;
        public readonly int SlotIndex;
        public readonly uint SlotGeneration;

        /// <summary>
        /// 使用完整稳定身份创建 Ability 延续句柄。
        /// </summary>
        public AbilityContinuationHandle(
            ulong simulationEpoch,
            OwnerAscHandle ownerAsc,
            int slotIndex,
            uint slotGeneration)
        {
            SimulationEpoch = simulationEpoch;
            OwnerAsc = ownerAsc;
            SlotIndex = slotIndex;
            SlotGeneration = slotGeneration;
        }

        public bool IsValid => SimulationEpoch != 0 && OwnerAsc.IsValid && SlotIndex >= 0 && SlotGeneration != 0;

        /// <summary>
        /// 转换为只供诊断与统一校验使用的无类型载体。
        /// </summary>
        public StableHandleDiagnosticCarrier ToDiagnosticCarrier()
        {
            return new StableHandleDiagnosticCarrier(
                SimulationEpoch,
                OwnerAsc,
                SlotIndex,
                SlotGeneration,
                Kind);
        }

        /// <summary>
        /// 比较两个 Ability 延续句柄的全部身份字段。
        /// </summary>
        public bool Equals(AbilityContinuationHandle other)
        {
            return SimulationEpoch == other.SimulationEpoch &&
                   OwnerAsc.Equals(other.OwnerAsc) &&
                   SlotIndex == other.SlotIndex &&
                   SlotGeneration == other.SlotGeneration;
        }

        /// <summary>
        /// 比较对象是否为相同 Ability 延续句柄。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is AbilityContinuationHandle other && Equals(other);
        }

        /// <summary>
        /// 计算覆盖全部身份字段的哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)(SimulationEpoch ^ (SimulationEpoch >> 32));
                hashCode = (hashCode * 397) ^ OwnerAsc.GetHashCode();
                hashCode = (hashCode * 397) ^ SlotIndex;
                return (hashCode * 397) ^ (int)SlotGeneration;
            }
        }

        /// <summary>
        /// 判断两个 Ability 延续句柄是否值相等。
        /// </summary>
        public static bool operator ==(AbilityContinuationHandle left, AbilityContinuationHandle right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个 Ability 延续句柄是否存在任一身份差异。
        /// </summary>
        public static bool operator !=(AbilityContinuationHandle left, AbilityContinuationHandle right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 标识 observed ASC 上一个存活的 Ability 订阅槽。
    /// </summary>
    public readonly struct AbilitySubscriptionHandle : IEquatable<AbilitySubscriptionHandle>
    {
        public const HandleKind Kind = HandleKind.AbilitySubscription;
        public readonly ulong SimulationEpoch;
        public readonly OwnerAscHandle OwnerAsc;
        public readonly int SlotIndex;
        public readonly uint SlotGeneration;

        /// <summary>
        /// 使用完整稳定身份创建 Ability 订阅句柄。
        /// </summary>
        public AbilitySubscriptionHandle(
            ulong simulationEpoch,
            OwnerAscHandle ownerAsc,
            int slotIndex,
            uint slotGeneration)
        {
            SimulationEpoch = simulationEpoch;
            OwnerAsc = ownerAsc;
            SlotIndex = slotIndex;
            SlotGeneration = slotGeneration;
        }

        public bool IsValid => SimulationEpoch != 0 && OwnerAsc.IsValid && SlotIndex >= 0 && SlotGeneration != 0;

        /// <summary>
        /// 转换为只供诊断与统一校验使用的无类型载体。
        /// </summary>
        public StableHandleDiagnosticCarrier ToDiagnosticCarrier()
        {
            return new StableHandleDiagnosticCarrier(
                SimulationEpoch,
                OwnerAsc,
                SlotIndex,
                SlotGeneration,
                Kind);
        }

        /// <summary>
        /// 比较两个 Ability 订阅句柄的全部身份字段。
        /// </summary>
        public bool Equals(AbilitySubscriptionHandle other)
        {
            return SimulationEpoch == other.SimulationEpoch &&
                   OwnerAsc.Equals(other.OwnerAsc) &&
                   SlotIndex == other.SlotIndex &&
                   SlotGeneration == other.SlotGeneration;
        }

        /// <summary>
        /// 比较对象是否为相同 Ability 订阅句柄。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is AbilitySubscriptionHandle other && Equals(other);
        }

        /// <summary>
        /// 计算覆盖全部身份字段的哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)(SimulationEpoch ^ (SimulationEpoch >> 32));
                hashCode = (hashCode * 397) ^ OwnerAsc.GetHashCode();
                hashCode = (hashCode * 397) ^ SlotIndex;
                return (hashCode * 397) ^ (int)SlotGeneration;
            }
        }

        /// <summary>
        /// 判断两个 Ability 订阅句柄是否值相等。
        /// </summary>
        public static bool operator ==(AbilitySubscriptionHandle left, AbilitySubscriptionHandle right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个 Ability 订阅句柄是否存在任一身份差异。
        /// </summary>
        public static bool operator !=(AbilitySubscriptionHandle left, AbilitySubscriptionHandle right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 标识 ASC 上一个独立于 Activation 生命周期的冷却门槽。
    /// </summary>
    public readonly struct CooldownGateHandle : IEquatable<CooldownGateHandle>
    {
        public const HandleKind Kind = HandleKind.CooldownGate;
        public readonly ulong SimulationEpoch;
        public readonly OwnerAscHandle OwnerAsc;
        public readonly int SlotIndex;
        public readonly uint SlotGeneration;

        /// <summary>
        /// 使用完整稳定身份创建冷却门句柄。
        /// </summary>
        public CooldownGateHandle(
            ulong simulationEpoch,
            OwnerAscHandle ownerAsc,
            int slotIndex,
            uint slotGeneration)
        {
            SimulationEpoch = simulationEpoch;
            OwnerAsc = ownerAsc;
            SlotIndex = slotIndex;
            SlotGeneration = slotGeneration;
        }

        public bool IsValid => SimulationEpoch != 0 && OwnerAsc.IsValid && SlotIndex >= 0 && SlotGeneration != 0;

        /// <summary>
        /// 转换为只供诊断与统一校验使用的无类型载体。
        /// </summary>
        public StableHandleDiagnosticCarrier ToDiagnosticCarrier()
        {
            return new StableHandleDiagnosticCarrier(
                SimulationEpoch,
                OwnerAsc,
                SlotIndex,
                SlotGeneration,
                Kind);
        }

        /// <summary>
        /// 比较两个冷却门句柄的全部身份字段。
        /// </summary>
        public bool Equals(CooldownGateHandle other)
        {
            return SimulationEpoch == other.SimulationEpoch &&
                   OwnerAsc.Equals(other.OwnerAsc) &&
                   SlotIndex == other.SlotIndex &&
                   SlotGeneration == other.SlotGeneration;
        }

        /// <summary>
        /// 比较对象是否为相同冷却门句柄。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is CooldownGateHandle other && Equals(other);
        }

        /// <summary>
        /// 计算覆盖全部身份字段的哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)(SimulationEpoch ^ (SimulationEpoch >> 32));
                hashCode = (hashCode * 397) ^ OwnerAsc.GetHashCode();
                hashCode = (hashCode * 397) ^ SlotIndex;
                return (hashCode * 397) ^ (int)SlotGeneration;
            }
        }

        /// <summary>
        /// 判断两个冷却门句柄是否值相等。
        /// </summary>
        public static bool operator ==(CooldownGateHandle left, CooldownGateHandle right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个冷却门句柄是否存在任一身份差异。
        /// </summary>
        public static bool operator !=(CooldownGateHandle left, CooldownGateHandle right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 标识 target ASC 上一个存活的 GameplayEffect 槽。
    /// </summary>
    public readonly struct ActiveEffectHandle : IEquatable<ActiveEffectHandle>
    {
        public const HandleKind Kind = HandleKind.ActiveEffect;
        public readonly ulong SimulationEpoch;
        public readonly OwnerAscHandle OwnerAsc;
        public readonly int SlotIndex;
        public readonly uint SlotGeneration;

        /// <summary>
        /// 使用完整稳定身份创建 ActiveEffect 句柄。
        /// </summary>
        public ActiveEffectHandle(
            ulong simulationEpoch,
            OwnerAscHandle ownerAsc,
            int slotIndex,
            uint slotGeneration)
        {
            SimulationEpoch = simulationEpoch;
            OwnerAsc = ownerAsc;
            SlotIndex = slotIndex;
            SlotGeneration = slotGeneration;
        }

        public bool IsValid => SimulationEpoch != 0 && OwnerAsc.IsValid && SlotIndex >= 0 && SlotGeneration != 0;

        /// <summary>
        /// 转换为只供诊断与统一校验使用的无类型载体。
        /// </summary>
        public StableHandleDiagnosticCarrier ToDiagnosticCarrier()
        {
            return new StableHandleDiagnosticCarrier(
                SimulationEpoch,
                OwnerAsc,
                SlotIndex,
                SlotGeneration,
                Kind);
        }

        /// <summary>
        /// 比较两个 ActiveEffect 句柄的全部身份字段。
        /// </summary>
        public bool Equals(ActiveEffectHandle other)
        {
            return SimulationEpoch == other.SimulationEpoch &&
                   OwnerAsc.Equals(other.OwnerAsc) &&
                   SlotIndex == other.SlotIndex &&
                   SlotGeneration == other.SlotGeneration;
        }

        /// <summary>
        /// 比较对象是否为相同 ActiveEffect 句柄。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is ActiveEffectHandle other && Equals(other);
        }

        /// <summary>
        /// 计算覆盖全部身份字段的哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)(SimulationEpoch ^ (SimulationEpoch >> 32));
                hashCode = (hashCode * 397) ^ OwnerAsc.GetHashCode();
                hashCode = (hashCode * 397) ^ SlotIndex;
                return (hashCode * 397) ^ (int)SlotGeneration;
            }
        }

        /// <summary>
        /// 判断两个 ActiveEffect 句柄是否值相等。
        /// </summary>
        public static bool operator ==(ActiveEffectHandle left, ActiveEffectHandle right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个 ActiveEffect 句柄是否存在任一身份差异。
        /// </summary>
        public static bool operator !=(ActiveEffectHandle left, ActiveEffectHandle right)
        {
            return !left.Equals(right);
        }
    }
}
