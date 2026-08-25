using System.Collections.Generic;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 表示 Tier B 目标语义向量当前是否已经由真实测试证明。
    /// </summary>
    internal enum TierBVectorStatus
    {
        Red,
        Green,
    }

    /// <summary>
    /// 保存一个 Tier B 最低向量的冻结编号、职责描述、实施 owner 与当前状态。
    /// </summary>
    internal readonly struct TierBVectorManifestEntry
    {
        /// <summary>
        /// 创建一条可由后续 V1-V7 逐项转绿的 Tier B 向量记录。
        /// </summary>
        public TierBVectorManifestEntry(
            int number,
            string description,
            string owner,
            TierBVectorStatus status)
        {
            Number = number;
            Description = description;
            Owner = owner;
            Status = status;
        }

        public int Number { get; }

        public string Description { get; }

        public string Owner { get; }

        public TierBVectorStatus Status { get; }
    }

    /// <summary>
    /// 维护 10B-08 冻结的 17 项 Tier B 最低固定向量及其实施归属。
    /// </summary>
    internal static class TierBVectorManifest
    {
        private static readonly TierBVectorManifestEntry[] Manifest =
        {
            Create(1, "双 Activation 争抢同一 cost/cooldown", "V3"),
            Create(2, "Commit→Cancel 与 Cancel→Commit", "V3"),
            Create(3, "Instant Ability End 后 cooldown 仍存活", "V3"),
            Create(4, "普通 self GE 的 OwnerWave 可见性屏障", "V3"),
            Create(5, "同 target 前序 application 改变后序 requirement/immunity", "V3 + V4"),
            Create(6, "source death 的 committed-work-wins", "V3 + V4"),
            Create(7, "AliveOnly 首死 reject 与 overkill 归属", "V3 + V4"),
            Create(8, "Level/Edge/Event 与跨 ASC wait cancel-ack", "V3"),
            Create(9, "9203 reapply/cap/due/end/expiry/inhibit/self-delete", "V4"),
            Create(10, "LeaveGranted 与重复 grant provenance", "V4 + V5"),
            Create(11, "两轮 Cue active lifecycle", "V4 + V5"),
            Create(12, "Avatar rebind 与 FrozenSpatial", "V1 + V3 + V6"),
            Create(13, "双杀/平局与 multi-BattleInstance 终局隔离", "V3 + V6"),
            Create(14, "SpawnBatch 原子 Ready", "V3 + V6"),
            Create(15, "admission fault 零写与 IngressClosed", "V2 + V3"),
            Create(16, "Boundary retry、无下一 tick teardown、NoFactReceipt 与 Result 后零事实", "V5 + V6"),
            Create(17, "不同 TickBatch 切分不改变 semantic hash", "V7"),
        };

        public static IReadOnlyList<TierBVectorManifestEntry> Entries => Manifest;

        /// <summary>
        /// 创建 V0 初始为 red 的 Tier B 清单项，避免各条目重复声明状态。
        /// </summary>
        private static TierBVectorManifestEntry Create(int number, string description, string owner)
        {
            return new TierBVectorManifestEntry(number, description, owner, TierBVectorStatus.Red);
        }
    }
}
