using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 标识一个 ASC 生命周期；稳定编号与生命周期代际共同拒绝重建后的旧引用。
    /// </summary>
    public readonly struct OwnerAscHandle : IEquatable<OwnerAscHandle>
    {
        public readonly ulong AscStableId;
        public readonly uint AscGeneration;

        /// <summary>
        /// 使用稳定编号与生命周期代际创建 ASC 身份。
        /// </summary>
        public OwnerAscHandle(ulong ascStableId, uint ascGeneration)
        {
            AscStableId = ascStableId;
            AscGeneration = ascGeneration;
        }

        public bool IsValid => AscStableId != 0 && AscGeneration != 0;

        /// <summary>
        /// 比较两个 ASC 身份的全部值字段。
        /// </summary>
        public bool Equals(OwnerAscHandle other)
        {
            return AscStableId == other.AscStableId && AscGeneration == other.AscGeneration;
        }

        /// <summary>
        /// 比较对象是否为相同 ASC 身份。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is OwnerAscHandle other && Equals(other);
        }

        /// <summary>
        /// 计算覆盖稳定编号与生命周期代际的哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)(AscStableId ^ (AscStableId >> 32));
                return (hashCode * 397) ^ (int)AscGeneration;
            }
        }

        /// <summary>
        /// 判断两个 ASC 身份是否值相等。
        /// </summary>
        public static bool operator ==(OwnerAscHandle left, OwnerAscHandle right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个 ASC 身份是否存在任一值差异。
        /// </summary>
        public static bool operator !=(OwnerAscHandle left, OwnerAscHandle right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 标识一个 Session 内的 BattleInstance 生命周期，并通过 Epoch 拒绝跨 Session 复用。
    /// </summary>
    public readonly struct BattleInstanceHandle : IEquatable<BattleInstanceHandle>
    {
        public readonly ulong SimulationEpoch;
        public readonly ulong BattleStableId;
        public readonly uint BattleGeneration;

        /// <summary>
        /// 使用 Session Epoch、战局稳定编号与生命周期代际创建战局身份。
        /// </summary>
        public BattleInstanceHandle(ulong simulationEpoch, ulong battleStableId, uint battleGeneration)
        {
            SimulationEpoch = simulationEpoch;
            BattleStableId = battleStableId;
            BattleGeneration = battleGeneration;
        }

        public bool IsValid => SimulationEpoch != 0 && BattleStableId != 0 && BattleGeneration != 0;

        /// <summary>
        /// 比较两个战局身份的全部值字段。
        /// </summary>
        public bool Equals(BattleInstanceHandle other)
        {
            return SimulationEpoch == other.SimulationEpoch &&
                   BattleStableId == other.BattleStableId &&
                   BattleGeneration == other.BattleGeneration;
        }

        /// <summary>
        /// 比较对象是否为相同战局身份。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is BattleInstanceHandle other && Equals(other);
        }

        /// <summary>
        /// 计算覆盖 Epoch、稳定编号与生命周期代际的哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)(SimulationEpoch ^ (SimulationEpoch >> 32));
                hashCode = (hashCode * 397) ^ (int)(BattleStableId ^ (BattleStableId >> 32));
                return (hashCode * 397) ^ (int)BattleGeneration;
            }
        }

        /// <summary>
        /// 判断两个战局身份是否值相等。
        /// </summary>
        public static bool operator ==(BattleInstanceHandle left, BattleInstanceHandle right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// 判断两个战局身份是否存在任一值差异。
        /// </summary>
        public static bool operator !=(BattleInstanceHandle left, BattleInstanceHandle right)
        {
            return !left.Equals(right);
        }
    }
}
