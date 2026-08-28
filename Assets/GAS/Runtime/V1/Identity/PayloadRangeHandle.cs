using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 区分 ASC-local 非压缩 payload range 的领域用途。
    /// </summary>
    public enum PayloadKind : byte
    {
        None = 0,
        AbilityContinuation = 1,
        EffectSpec = 2,
        ActiveEffectPayload = 3,
        Capture = 4,
        AggregatorContribution = 5,
    }

    /// <summary>
    /// 集中裁决 Runtime v1 允许路由到持久 range store 的 PayloadKind 闭集。
    /// </summary>
    internal static class GasPayloadKindContract
    {
        /// <summary>
        /// 仅接受已冻结的具名 PayloadKind，拒绝 None 与任意越界枚举值。
        /// </summary>
        internal static bool IsKnown(PayloadKind kind)
        {
            var value = (byte)kind;
            return value >= (byte)PayloadKind.AbilityContinuation &&
                   value <= (byte)PayloadKind.AggregatorContribution;
        }
    }

    /// <summary>
    /// 标识 ASC-local 非压缩 payload range，并通过种类与代际拒绝错 range 解析。
    /// </summary>
    public readonly struct PayloadRangeHandle : IEquatable<PayloadRangeHandle>
    {
        public readonly ulong SimulationEpoch;
        public readonly OwnerAscHandle OwnerAsc;
        public readonly int Offset;
        public readonly int Length;
        public readonly uint RangeGeneration;
        public readonly PayloadKind Kind;

        /// <summary>
        /// 使用完整稳定身份、范围与用途创建 payload range 句柄。
        /// </summary>
        public PayloadRangeHandle(
            ulong simulationEpoch,
            OwnerAscHandle ownerAsc,
            int offset,
            int length,
            uint rangeGeneration,
            PayloadKind kind)
        {
            SimulationEpoch = simulationEpoch;
            OwnerAsc = ownerAsc;
            Offset = offset;
            Length = length;
            RangeGeneration = rangeGeneration;
            Kind = kind;
        }

        public bool IsValid => SimulationEpoch != 0 &&
                               OwnerAsc.IsValid &&
                                Offset >= 0 &&
                                Length > 0 &&
                                RangeGeneration != 0 &&
                                GasPayloadKindContract.IsKnown(Kind);

        /// <summary>
        /// 比较两个 payload range 句柄的全部身份字段。
        /// </summary>
        public bool Equals(PayloadRangeHandle other)
        {
            return SimulationEpoch == other.SimulationEpoch &&
                   OwnerAsc.Equals(other.OwnerAsc) &&
                   Offset == other.Offset &&
                   Length == other.Length &&
                   RangeGeneration == other.RangeGeneration &&
                   Kind == other.Kind;
        }

        /// <summary>
        /// 比较对象是否为相同 payload range 句柄。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is PayloadRangeHandle other && Equals(other);
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
                hashCode = (hashCode * 397) ^ Offset;
                hashCode = (hashCode * 397) ^ Length;
                hashCode = (hashCode * 397) ^ (int)RangeGeneration;
                return (hashCode * 397) ^ (int)Kind;
            }
        }

        /// <summary>
        /// 判断两个 payload range 句柄是否值相等。
        /// </summary>
        public static bool operator ==(PayloadRangeHandle left, PayloadRangeHandle right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个 payload range 句柄是否存在任一身份差异。
        /// </summary>
        public static bool operator !=(PayloadRangeHandle left, PayloadRangeHandle right)
        {
            return !left.Equals(right);
        }
    }
}
