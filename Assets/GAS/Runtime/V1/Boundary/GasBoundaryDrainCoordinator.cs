using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 保存一次 managed staging 接管的不可变事实批次；批次只携带自包含 Boundary record。
    /// </summary>
    public readonly struct GasBoundaryDrainBatch
    {
        internal GasBoundaryDrainBatch(
            in GasBoundaryDrainReceipt receipt,
            IReadOnlyList<BoundaryFactBuffer> facts)
        {
            Receipt = receipt;
            var copy = new BoundaryFactBuffer[facts.Count];
            for (var index = 0; index < facts.Count; index++)
                copy[index] = facts[index];
            Facts = Array.AsReadOnly(copy);
        }

        /// <summary>
        /// 返回 staging 使用的稳定 receipt 身份与冻结 watermark。
        /// </summary>
        public GasBoundaryDrainReceipt Receipt { get; }

        /// <summary>
        /// 返回不可变事实快照；空 shell 的 NoFactReceipt 批次长度为零。
        /// </summary>
        public IReadOnlyList<BoundaryFactBuffer> Facts { get; }

        /// <summary>
        /// 判断当前批次是否是空 cleanup shell 的显式 receipt。
        /// </summary>
        public bool IsNoFact => Receipt.IsNoFact;
    }

    /// <summary>
    /// 定义唯一 managed Boundary staging 入口；返回 false 时源 outbox 必须保持不变。
    /// </summary>
    public interface IGasBoundaryDrainStager
    {
        /// <summary>
        /// 尝试把一份 immutable batch 接管到 managed ring 或外部消费者。
        /// </summary>
        bool TryStage(in GasBoundaryDrainBatch batch);
    }

    /// <summary>
    /// 提供有界、幂等的 managed immutable batch ring，容量耗尽时阻断源清理。
    /// </summary>
    public sealed class GasBoundaryDrainRing : IGasBoundaryDrainStager
    {
        private readonly int _capacity;
        private readonly List<GasBoundaryDrainBatch> _batches =
            new List<GasBoundaryDrainBatch>();

        /// <summary>
        /// 创建具有固定批次数上限的 managed ring。
        /// </summary>
        public GasBoundaryDrainRing(int capacity = 1024)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        /// <summary>
        /// 返回当前 ring 中尚未交付的 immutable batch 数量。
        /// </summary>
        public int Count => _batches.Count;

        /// <summary>
        /// 返回 ring 的固定批次数上限。
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// 幂等接管同一 owner/batch；ring 满时拒绝新批次并保留源 outbox。
        /// </summary>
        public bool TryStage(in GasBoundaryDrainBatch batch)
        {
            if (!batch.Receipt.IsValid)
                return false;
            for (var index = 0; index < _batches.Count; index++)
            {
                if (!_batches[index].Receipt.Owner.Equals(batch.Receipt.Owner) ||
                    _batches[index].Receipt.BatchId != batch.Receipt.BatchId)
                    continue;
                var existing = _batches[index];
                return HasSameIdentity(existing, batch);
            }

            if (_batches.Count >= _capacity)
                return false;
            _batches.Add(new GasBoundaryDrainBatch(batch.Receipt, batch.Facts));
            return true;
        }

        /// <summary>
        /// 按接管顺序取出一份 immutable batch，供 Boundary consumer 消费。
        /// </summary>
        public bool TryDequeue(out GasBoundaryDrainBatch batch)
        {
            if (_batches.Count == 0)
            {
                batch = default;
                return false;
            }

            batch = _batches[0];
            _batches.RemoveAt(0);
            return true;
        }

        /// <summary>
        /// 比较 retry 的 receipt 与事实序列，拒绝同 BatchId 的内容漂移。
        /// </summary>
        private static bool HasSameIdentity(
            GasBoundaryDrainBatch left,
            GasBoundaryDrainBatch right)
        {
            if (left.Receipt.Kind != right.Receipt.Kind ||
                left.Receipt.InFlightWatermark != right.Receipt.InFlightWatermark ||
                left.Facts.Count != right.Facts.Count)
                return false;
            for (var index = 0; index < left.Facts.Count; index++)
            {
                if (left.Facts[index].EventId.OwnerSequence !=
                    right.Facts[index].EventId.OwnerSequence ||
                    !left.Facts[index].Equals(right.Facts[index]))
                    return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 汇总一次 owner 扫描的 Drain 结果，供 owner 决定是否允许最终 teardown。
    /// </summary>
    public readonly struct GasBoundaryDrainRunResult
    {
        internal GasBoundaryDrainRunResult(
            int ownerCount,
            int acceptedOwnerCount,
            int blockedOwnerCount,
            GasBoundaryDrainFailure failure)
        {
            OwnerCount = ownerCount;
            AcceptedOwnerCount = acceptedOwnerCount;
            BlockedOwnerCount = blockedOwnerCount;
            Failure = failure;
        }

        public int OwnerCount { get; }

        public int AcceptedOwnerCount { get; }

        public int BlockedOwnerCount { get; }

        public GasBoundaryDrainFailure Failure { get; }

        public bool Succeeded => BlockedOwnerCount == 0 &&
                                  Failure == GasBoundaryDrainFailure.None;
    }

    /// <summary>
    /// 在完成所有 Runtime Job 后驱动 ASC/Session cleanup outbox 的唯一 managed Drain。
    /// </summary>
    public sealed class GasBoundaryDrainCoordinator
    {
        private const int MaxFinalDrainPasses = 4;
        private readonly IGasBoundaryDrainStager _stager;
        private ulong _nextBatchId;

        /// <summary>
        /// 创建带有持久批次序号根的 Drain coordinator。
        /// </summary>
        public GasBoundaryDrainCoordinator(
            IGasBoundaryDrainStager stager,
            ulong firstBatchId = 1)
        {
            _stager = stager ?? throw new ArgumentNullException(nameof(stager));
            if (firstBatchId == 0)
                throw new ArgumentOutOfRangeException(nameof(firstBatchId));
            _nextBatchId = firstBatchId;
        }

        /// <summary>
        /// 扫描全部 cleanup owner，staging 失败时保留源记录并返回阻断证据。
        /// </summary>
        public bool TryDrain(
            EntityManager entityManager,
            out GasBoundaryDrainRunResult result)
        {
            // cleanup-only archetype 必须通过全实体扫描识别，普通 query 可能在移除 gameplay identity 后失配。
            var owners = CollectBoundaryOwners(entityManager);
            var ownerCount = 0;
            var accepted = 0;
            var blocked = 0;
            var firstFailure = GasBoundaryDrainFailure.None;
            for (var index = 0; index < owners.Count; index++)
            {
                var owner = owners[index];
                ownerCount++;
                if (TryDrainOwner(entityManager, owner, out var failure))
                {
                    accepted++;
                    continue;
                }

                if (failure == GasBoundaryDrainFailure.None)
                    continue;
                blocked++;
                if (firstFailure == GasBoundaryDrainFailure.None)
                    firstFailure = failure;
            }

            result = new GasBoundaryDrainRunResult(
                ownerCount,
                accepted,
                blocked,
                firstFailure);
            return result.Succeeded;
        }

        /// <summary>
        /// 在 shutdown 前反复完成 drain、live owner 折叠和 shell 回收，直到没有未接管事实。
        /// </summary>
        public bool TryFinalDrain(
            EntityManager entityManager,
            out GasBoundaryDrainRunResult result)
        {
            result = default;
            for (var pass = 0; pass < MaxFinalDrainPasses; pass++)
            {
                if (!TryDrain(entityManager, out result))
                    return false;
                if (!TryRemoveAcceptedShells(
                        entityManager,
                        out _,
                        out var cleanupFailure))
                {
                    result = CreateFailureResult(result.OwnerCount, cleanupFailure);
                    return false;
                }
                if (!HasUndrainedWork(entityManager))
                    return true;
            }

            result = CreateFailureResult(
                result.OwnerCount,
                GasBoundaryDrainFailure.StateInvariantViolation);
            return false;
        }

        /// <summary>
        /// 处理单个 ASC/Session owner 的冻结、staging、accepted 清理和 shell 回收。
        /// </summary>
        private bool TryDrainOwner(
            EntityManager entityManager,
            Entity owner,
            out GasBoundaryDrainFailure failure)
        {
            failure = GasBoundaryDrainFailure.None;
            var state = entityManager.GetComponentData<BoundaryDrainState>(owner);
            var outbox = entityManager.GetBuffer<BoundaryFactBuffer>(owner);
            if (!IsValidOwnerState(in state))
            {
                failure = GasBoundaryDrainFailure.InvalidOwner;
                return false;
            }
            var shell = IsCleanupShell(entityManager, owner, in state);
            if (state.Phase == GasBoundaryDrainPhase.Accepted)
            {
                if (shell)
                    return true;
                return FoldAcceptedLive(entityManager, owner, ref state, out failure);
            }
            if (!shell && outbox.Length == 0 && state.Phase == GasBoundaryDrainPhase.Idle)
                return true;

            var batchId = state.Phase == GasBoundaryDrainPhase.InFlight
                ? state.BatchId
                : AllocateBatchId(out failure);
            if (failure != GasBoundaryDrainFailure.None)
                return false;
            if (!GasBoundaryDrainProtocol.TryFreeze(
                    ref state,
                    outbox,
                    batchId,
                    shell,
                    out var receipt,
                    out failure))
            {
                return false;
            }

            // Freeze 成功即持久化 InFlight identity；staging 失败时下一次调用必须复用同一 BatchId。
            entityManager.SetComponentData(owner, state);

            if (!TryCreateBatch(outbox, in receipt, out var batch, out failure))
                return false;
            if (!TryStage(in batch))
            {
                failure = GasBoundaryDrainFailure.StagingRejected;
                return false;
            }
            if (!GasBoundaryDrainProtocol.TryAccept(
                    ref state,
                    outbox,
                    in receipt,
                    out _,
                    out failure))
            {
                return false;
            }

            if (state.Phase == GasBoundaryDrainPhase.Pending)
            {
                // Late tail 已被明确保留，当前 owner 仍需参与下一轮 drain，不能伪造 Accepted/移除 shell。
                entityManager.SetComponentData(owner, state);
                return true;
            }

            if (shell)
            {
                // Accepted shell 先持久化状态，下一次 prepass 才能安全移除 cleanup 载体。
                entityManager.SetComponentData(owner, state);
                return true;
            }
            if (!GasBoundaryDrainProtocol.TryFoldAcceptedToIdle(ref state, out failure))
                return false;
            entityManager.SetComponentData(owner, state);
            return true;
        }

        /// <summary>
        /// 在下一 Kernel prepass 或 shutdown final drain 移除已 Accepted 的 cleanup shell。
        /// </summary>
        public bool TryRemoveAcceptedShells(
            EntityManager entityManager,
            out int removedCount,
            out GasBoundaryDrainFailure failure)
        {
            // 先冻结实体快照，再逐个移除已 Accepted 的 shell，避免结构变更影响扫描。
            removedCount = 0;
            failure = GasBoundaryDrainFailure.None;
            var owners = CollectBoundaryOwners(entityManager);
            for (var index = 0; index < owners.Count; index++)
            {
                var owner = owners[index];
                var state = entityManager.GetComponentData<BoundaryDrainState>(owner);
                if (state.Phase != GasBoundaryDrainPhase.Accepted ||
                    !IsCleanupShell(entityManager, owner, in state))
                    continue;
                if (!GasBoundaryDrainProtocol.TryFoldAcceptedToIdle(ref state, out failure))
                    return false;
                RemoveCleanupShell(entityManager, owner);
                removedCount++;
            }

            return true;
        }

        /// <summary>
        /// 判断实体是否仍携带完整 Boundary cleanup owner 载体。
        /// </summary>
        private static bool HasBoundaryOwner(EntityManager entityManager, Entity entity)
        {
            return entityManager.Exists(entity) &&
                   entityManager.HasComponent<BoundaryDrainState>(entity) &&
                   entityManager.HasBuffer<BoundaryFactBuffer>(entity);
        }

        /// <summary>
        /// 在 idle 快路径前验证 owner identity 与持久序号根，禁止吞掉损坏 shell。
        /// </summary>
        private static bool IsValidOwnerState(in BoundaryDrainState state)
        {
            return state.SimulationEpoch != 0 &&
                   state.OwnerKind != GasBoundaryOwnerKind.None &&
                   state.OwnerStableId != 0 &&
                   state.OwnerGeneration != 0 &&
                   state.NextOwnerSequence != 0;
        }

        /// <summary>
        /// 收集并按 owner identity 排序，保证跨 ASC/Session 的 batch 发布顺序稳定。
        /// </summary>
        private static List<Entity> CollectBoundaryOwners(EntityManager entityManager)
        {
            using var entities = entityManager.GetAllEntities(Allocator.Temp);
            var owners = new List<Entity>();
            for (var index = 0; index < entities.Length; index++)
            {
                if (HasBoundaryOwner(entityManager, entities[index]))
                    owners.Add(entities[index]);
            }

            owners.Sort((left, right) => CompareOwnerIdentity(entityManager, left, right));
            return owners;
        }

        /// <summary>
        /// 比较 Boundary owner 的 Epoch、scope、stable id、generation 与实体 tie-breaker。
        /// </summary>
        private static int CompareOwnerIdentity(
            EntityManager entityManager,
            Entity left,
            Entity right)
        {
            var leftState = entityManager.GetComponentData<BoundaryDrainState>(left);
            var rightState = entityManager.GetComponentData<BoundaryDrainState>(right);
            var comparison = leftState.SimulationEpoch.CompareTo(rightState.SimulationEpoch);
            if (comparison != 0)
                return comparison;
            comparison = leftState.OwnerKind.CompareTo(rightState.OwnerKind);
            if (comparison != 0)
                return comparison;
            comparison = leftState.OwnerStableId.CompareTo(rightState.OwnerStableId);
            if (comparison != 0)
                return comparison;
            comparison = leftState.OwnerGeneration.CompareTo(rightState.OwnerGeneration);
            if (comparison != 0)
                return comparison;
            comparison = left.Index.CompareTo(right.Index);
            return comparison != 0 ? comparison : left.Version.CompareTo(right.Version);
        }

        /// <summary>
        /// 检查所有 cleanup owner 是否已经进入无事实、可 teardown 的稳定状态。
        /// </summary>
        private static bool HasUndrainedWork(EntityManager entityManager)
        {
            using var entities = entityManager.GetAllEntities(Allocator.Temp);
            for (var index = 0; index < entities.Length; index++)
            {
                var owner = entities[index];
                if (!HasBoundaryOwner(entityManager, owner))
                    continue;
                var state = entityManager.GetComponentData<BoundaryDrainState>(owner);
                var outbox = entityManager.GetBuffer<BoundaryFactBuffer>(owner);
                if (outbox.Length != 0 ||
                    state.Phase == GasBoundaryDrainPhase.Pending ||
                    state.Phase == GasBoundaryDrainPhase.InFlight ||
                    state.Phase == GasBoundaryDrainPhase.Accepted &&
                    !IsCleanupShell(entityManager, owner, in state))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 创建带有指定失败原因的 drain 结果，保留当前扫描的 owner 数量证据。
        /// </summary>
        private static GasBoundaryDrainRunResult CreateFailureResult(
            int ownerCount,
            GasBoundaryDrainFailure failure)
        {
            return new GasBoundaryDrainRunResult(ownerCount, 0, 1, failure);
        }

        /// <summary>
        /// 为新 owner 分配永不回绕的 BatchId；InFlight retry 不经过此路径。
        /// </summary>
        private ulong AllocateBatchId(out GasBoundaryDrainFailure failure)
        {
            if (_nextBatchId == 0 || _nextBatchId == ulong.MaxValue)
            {
                failure = GasBoundaryDrainFailure.SequenceOverflow;
                return 0;
            }

            failure = GasBoundaryDrainFailure.None;
            return _nextBatchId++;
        }

        /// <summary>
        /// 将冻结 prefix 复制为排序稳定的 managed immutable batch。
        /// </summary>
        private static bool TryCreateBatch(
            DynamicBuffer<BoundaryFactBuffer> outbox,
            in GasBoundaryDrainReceipt receipt,
            out GasBoundaryDrainBatch batch,
            out GasBoundaryDrainFailure failure)
        {
            var facts = new List<BoundaryFactBuffer>(receipt.FactCount);
            for (var index = 0; index < outbox.Length; index++)
            {
                var fact = outbox[index];
                if (fact.EventId.OwnerSequence <= receipt.InFlightWatermark)
                    facts.Add(fact);
            }
            facts.Sort(CompareFactsBySequence);
            if (receipt.IsNoFact)
            {
                if (facts.Count != 0)
                {
                    batch = default;
                    failure = GasBoundaryDrainFailure.NoFactReceiptMismatch;
                    return false;
                }
            }
            else if (facts.Count != receipt.FactCount)
            {
                batch = default;
                failure = GasBoundaryDrainFailure.ReceiptMismatch;
                return false;
            }

            batch = new GasBoundaryDrainBatch(in receipt, facts);
            failure = GasBoundaryDrainFailure.None;
            return true;
        }

        /// <summary>
        /// 调用 managed stager；异常直接向 owner 暴露，冻结源仍保持可重试。
        /// </summary>
        private bool TryStage(in GasBoundaryDrainBatch batch)
        {
            return _stager.TryStage(in batch);
        }

        /// <summary>
        /// Accepted live owner 折回 Idle，cleanup shell 的移除由下一次 prepass 负责。
        /// </summary>
        private static bool FoldAcceptedLive(
            EntityManager entityManager,
            Entity owner,
            ref BoundaryDrainState state,
            out GasBoundaryDrainFailure failure)
        {
            if (!GasBoundaryDrainProtocol.TryFoldAcceptedToIdle(ref state, out failure))
                return false;
            entityManager.SetComponentData(owner, state);
            return true;
        }

        /// <summary>
        /// 移除 shell 上最后的 Boundary cleanup 载体；该调用只接受已成功 staging 的状态。
        /// </summary>
        private static void RemoveCleanupShell(EntityManager entityManager, Entity owner)
        {
            if (entityManager.HasComponent<BoundaryDrainState>(owner))
                entityManager.RemoveComponent<BoundaryDrainState>(owner);
            if (entityManager.Exists(owner) &&
                entityManager.HasComponent<BoundaryFactBuffer>(owner))
                entityManager.RemoveComponent<BoundaryFactBuffer>(owner);
            if (entityManager.Exists(owner))
                entityManager.DestroyEntity(owner);
        }

        /// <summary>
        /// 判断 owner 是否已经失去 gameplay identity，只剩 cleanup shell。
        /// </summary>
        private static bool IsCleanupShell(
            EntityManager entityManager,
            Entity owner,
            in BoundaryDrainState state)
        {
            // shell 必须同时失去 ASC 与 Session gameplay identity，避免错配 state 被误清理。
            return state.OwnerKind == GasBoundaryOwnerKind.Asc ||
                   state.OwnerKind == GasBoundaryOwnerKind.Session
                ? !entityManager.HasComponent<GasAscIdentity>(owner) &&
                  !entityManager.HasComponent<GasSessionIdentity>(owner)
                : false;
        }

        /// <summary>
        /// 以 OwnerSequence 作为唯一排序键，保持 batch 内容与 protocol watermark 一致。
        /// </summary>
        private static int CompareFactsBySequence(
            BoundaryFactBuffer left,
            BoundaryFactBuffer right)
        {
            return left.EventId.OwnerSequence < right.EventId.OwnerSequence
                ? -1
                : left.EventId.OwnerSequence > right.EventId.OwnerSequence ? 1 : 0;
        }
    }
}
