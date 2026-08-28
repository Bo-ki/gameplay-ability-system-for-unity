using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 保存一个 BattleInstance 的纯值入站权限，不包含 Entity 或任何 ECS 容器引用。
    /// </summary>
    internal readonly struct GasIngressBattleAuthority
    {
        internal readonly BattleInstanceHandle BattleInstance;
        internal readonly GasBattleInstanceState State;
        internal readonly bool IngressOpen;

        /// <summary>
        /// 使用稳定战局身份、生命周期与显式 ingress 开关创建权限项。
        /// </summary>
        internal GasIngressBattleAuthority(
            in BattleInstanceHandle battleInstance,
            GasBattleInstanceState state,
            bool ingressOpen)
        {
            BattleInstance = battleInstance;
            State = state;
            IngressOpen = ingressOpen;
        }
    }

    /// <summary>
    /// 保存一个 ASC 在指定 BattleInstance 中的纯值成员资格，不包含 Entity 或查找表引用。
    /// </summary>
    internal readonly struct GasIngressAscAuthority
    {
        internal readonly OwnerAscHandle Asc;
        internal readonly BattleInstanceHandle BattleInstance;
        internal readonly GasAscRegistryState State;

        /// <summary>
        /// 使用稳定 ASC、所属战局与 registry 状态创建成员资格项。
        /// </summary>
        internal GasIngressAscAuthority(
            in OwnerAscHandle asc,
            in BattleInstanceHandle battleInstance,
            GasAscRegistryState state)
        {
            Asc = asc;
            BattleInstance = battleInstance;
            State = state;
        }
    }

    /// <summary>
    /// 冻结一次由 World owner 发布的纯值权限视图与 ingress 总预算，Gate 仅持有其深复制版本。
    /// </summary>
    internal readonly struct GasIngressAuthoritySnapshot
    {
        private readonly GasIngressBattleAuthority[] _battles;
        private readonly GasIngressAscAuthority[] _ascMemberships;

        internal readonly ulong SimulationEpoch;
        internal readonly int MaxIngressCommandCount;
        internal readonly long MaxIngressPayloadBytes;

        internal int BattleCount => _battles?.Length ?? 0;
        internal int AscMembershipCount => _ascMemberships?.Length ?? 0;
        internal bool IsConfigured => SimulationEpoch != 0 &&
                                      MaxIngressCommandCount >= 0 &&
                                      MaxIngressPayloadBytes >= 0;

        /// <summary>
        /// 使用 Epoch、总预算与两组纯值成员项创建自包含权限快照，并立即复制输入数组。
        /// </summary>
        internal GasIngressAuthoritySnapshot(
            ulong simulationEpoch,
            int maxIngressCommandCount,
            long maxIngressPayloadBytes,
            GasIngressBattleAuthority[] battles,
            GasIngressAscAuthority[] ascMemberships)
        {
            SimulationEpoch = simulationEpoch;
            MaxIngressCommandCount = maxIngressCommandCount;
            MaxIngressPayloadBytes = maxIngressPayloadBytes;
            _battles = CloneBattles(battles);
            _ascMemberships = CloneAscMemberships(ascMemberships);
        }

        /// <summary>
        /// 再次复制两个权限数组，确保 Gate 与 World owner 不共享可变数组。
        /// </summary>
        internal GasIngressAuthoritySnapshot Clone()
        {
            return new GasIngressAuthoritySnapshot(
                SimulationEpoch,
                MaxIngressCommandCount,
                MaxIngressPayloadBytes,
                _battles,
                _ascMemberships);
        }

        /// <summary>
        /// 校验 Epoch、预算、战局身份、ASC 成员关系与重复项均形成唯一纯值事实源。
        /// </summary>
        internal bool IsWellFormed()
        {
            return IsConfigured && ValidateBattles() && ValidateAscMemberships();
        }

        /// <summary>
        /// 按完整 BattleInstanceHandle 查找唯一战局权限项。
        /// </summary>
        internal bool TryGetBattle(
            in BattleInstanceHandle battleInstance,
            out GasIngressBattleAuthority authority)
        {
            for (var index = 0; index < BattleCount; index++)
            {
                if (!_battles[index].BattleInstance.Equals(battleInstance))
                    continue;

                authority = _battles[index];
                return true;
            }

            authority = default;
            return false;
        }

        /// <summary>
        /// 按完整 OwnerAscHandle 查找唯一 ASC 成员资格项。
        /// </summary>
        internal bool TryGetAsc(
            in OwnerAscHandle asc,
            out GasIngressAscAuthority authority)
        {
            for (var index = 0; index < AscMembershipCount; index++)
            {
                if (!_ascMemberships[index].Asc.Equals(asc))
                    continue;

                authority = _ascMemberships[index];
                return true;
            }

            authority = default;
            return false;
        }

        /// <summary>
        /// 复制 Battle 权限数组，空输入被规范化为空数组。
        /// </summary>
        private static GasIngressBattleAuthority[] CloneBattles(
            GasIngressBattleAuthority[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<GasIngressBattleAuthority>();

            var clone = new GasIngressBattleAuthority[source.Length];
            Array.Copy(source, clone, source.Length);
            return clone;
        }

        /// <summary>
        /// 复制 ASC 成员数组，空输入被规范化为空数组。
        /// </summary>
        private static GasIngressAscAuthority[] CloneAscMemberships(
            GasIngressAscAuthority[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<GasIngressAscAuthority>();

            var clone = new GasIngressAscAuthority[source.Length];
            Array.Copy(source, clone, source.Length);
            return clone;
        }

        /// <summary>
        /// 校验每个战局属于当前 Epoch 且不存在完整身份重复项。
        /// </summary>
        private bool ValidateBattles()
        {
            for (var index = 0; index < BattleCount; index++)
            {
                var current = _battles[index].BattleInstance;
                if (!current.IsValid || current.SimulationEpoch != SimulationEpoch)
                    return false;

                for (var other = index + 1; other < BattleCount; other++)
                {
                    if (current.Equals(_battles[other].BattleInstance))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 校验每个 ASC 身份唯一、所属战局存在且全部属于当前 Epoch。
        /// </summary>
        private bool ValidateAscMemberships()
        {
            for (var index = 0; index < AscMembershipCount; index++)
            {
                var current = _ascMemberships[index];
                if (!current.Asc.IsValid ||
                    current.BattleInstance.SimulationEpoch != SimulationEpoch ||
                    !TryGetBattle(current.BattleInstance, out _))
                    return false;

                if (HasDuplicateAsc(index, current.Asc))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 检查指定位置后的成员项是否重复使用同一完整 ASC 身份。
        /// </summary>
        private bool HasDuplicateAsc(int index, in OwnerAscHandle asc)
        {
            for (var other = index + 1; other < AscMembershipCount; other++)
            {
                if (asc.Equals(_ascMemberships[other].Asc))
                    return true;
            }

            return false;
        }
    }
}
