using System;
using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 标识 Boundary outbox 物理 owner 的完整稳定身份，跨 ASC 与 Session outbox 不共享裸序号。
    /// </summary>
    public readonly struct GasBoundaryOwnerKey : IEquatable<GasBoundaryOwnerKey>
    {
        /// <summary>
        /// 创建一个带 Epoch、owner 类型、稳定编号和代际的物理 owner key。
        /// </summary>
        public GasBoundaryOwnerKey(
            ulong simulationEpoch,
            GasBoundaryOwnerKind ownerKind,
            ulong ownerStableId,
            uint ownerGeneration)
        {
            SimulationEpoch = simulationEpoch;
            OwnerKind = ownerKind;
            OwnerStableId = ownerStableId;
            OwnerGeneration = ownerGeneration;
        }

        public ulong SimulationEpoch { get; }

        public GasBoundaryOwnerKind OwnerKind { get; }

        public ulong OwnerStableId { get; }

        public uint OwnerGeneration { get; }

        public bool IsValid => SimulationEpoch != 0 &&
                                OwnerKind != GasBoundaryOwnerKind.None &&
                                OwnerStableId != 0 &&
                                OwnerGeneration != 0;

        /// <summary>
        /// 从 ASC 稳定身份创建 ASC-scope outbox owner key。
        /// </summary>
        public static GasBoundaryOwnerKey ForAsc(
            ulong simulationEpoch,
            in OwnerAscHandle ownerAsc)
        {
            return new GasBoundaryOwnerKey(
                simulationEpoch,
                GasBoundaryOwnerKind.Asc,
                ownerAsc.AscStableId,
                ownerAsc.AscGeneration);
        }

        /// <summary>
        /// 从 Session 稳定身份创建 Session-scope outbox owner key。
        /// </summary>
        public static GasBoundaryOwnerKey ForSession(
            ulong simulationEpoch,
            ulong ownerStableId,
            uint ownerGeneration)
        {
            return new GasBoundaryOwnerKey(
                simulationEpoch,
                GasBoundaryOwnerKind.Session,
                ownerStableId,
                ownerGeneration);
        }

        /// <summary>
        /// 比较两个物理 owner key 的全部稳定字段。
        /// </summary>
        public bool Equals(GasBoundaryOwnerKey other)
        {
            return SimulationEpoch == other.SimulationEpoch &&
                   OwnerKind == other.OwnerKind &&
                   OwnerStableId == other.OwnerStableId &&
                   OwnerGeneration == other.OwnerGeneration;
        }

        /// <summary>
        /// 比较对象是否表示同一个物理 owner。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is GasBoundaryOwnerKey other && Equals(other);
        }

        /// <summary>
        /// 计算覆盖物理 owner 全部字段的稳定哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)(SimulationEpoch ^ (SimulationEpoch >> 32));
                hash = (hash * 397) ^ (int)OwnerKind;
                hash = (hash * 397) ^ (int)(OwnerStableId ^ (OwnerStableId >> 32));
                return (hash * 397) ^ (int)OwnerGeneration;
            }
        }

        public static bool operator ==(GasBoundaryOwnerKey left, GasBoundaryOwnerKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GasBoundaryOwnerKey left, GasBoundaryOwnerKey right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>
    /// 表示 Boundary outbox 状态机拒绝一次操作的确定性原因。
    /// </summary>
    public enum GasBoundaryDrainFailure : byte
    {
        None,
        InvalidOwner,
        InvalidPhase,
        InvalidBatchId,
        SequenceOverflow,
        FactInvalid,
        FactOwnerMismatch,
        FactScopeMismatch,
        FactSequenceMismatch,
        OutboxEmpty,
        ReceiptMismatch,
        NoFactReceiptMismatch,
        CleanupShellRequired,
        StateInvariantViolation,
        StagingRejected,
    }

    /// <summary>
    /// 区分普通事实批次与空 cleanup shell 的显式无事实确认。
    /// </summary>
    public enum GasBoundaryDrainReceiptKind : byte
    {
        Facts,
        NoFact,
    }

    /// <summary>
    /// 描述一次由 managed staging 接管的稳定 owner range。
    /// </summary>
    public readonly struct GasBoundaryDrainReceipt
    {
        internal GasBoundaryDrainReceipt(
            in GasBoundaryOwnerKey owner,
            ulong batchId,
            GasBoundaryDrainReceiptKind kind,
            ulong firstOwnerSequence,
            ulong lastOwnerSequence,
            ulong inFlightWatermark,
            int factCount)
        {
            Owner = owner;
            BatchId = batchId;
            Kind = kind;
            FirstOwnerSequence = firstOwnerSequence;
            LastOwnerSequence = lastOwnerSequence;
            InFlightWatermark = inFlightWatermark;
            FactCount = factCount;
        }

        public GasBoundaryOwnerKey Owner { get; }

        public ulong BatchId { get; }

        public GasBoundaryDrainReceiptKind Kind { get; }

        public ulong FirstOwnerSequence { get; }

        public ulong LastOwnerSequence { get; }

        public ulong InFlightWatermark { get; }

        public int FactCount { get; }

        public bool IsNoFact => Kind == GasBoundaryDrainReceiptKind.NoFact;

        public bool IsValid => Owner.IsValid && BatchId != 0 &&
                               (IsNoFact ||
                                (FactCount > 0 &&
                                 FirstOwnerSequence != 0 &&
                                 LastOwnerSequence >= FirstOwnerSequence &&
                                 InFlightWatermark == LastOwnerSequence));
    }

    /// <summary>
    /// 保存无事实 shell 的显式 staging receipt；它不携带或伪造任何 Battle identity。
    /// </summary>
    public readonly struct GasBoundaryNoFactReceipt
    {
        internal GasBoundaryNoFactReceipt(in GasBoundaryDrainReceipt receipt)
        {
            Receipt = receipt;
        }

        public GasBoundaryDrainReceipt Receipt { get; }

        public GasBoundaryOwnerKey Owner => Receipt.Owner;

        public ulong BatchId => Receipt.BatchId;

        public bool IsValid => Receipt.IsNoFact && Receipt.IsValid;
    }

    /// <summary>
    /// 返回一次 receipt 应用后的清理数量、late tail 结果和幂等命中信息。
    /// </summary>
    public readonly struct GasBoundaryDrainAcceptance
    {
        internal GasBoundaryDrainAcceptance(
            int clearedFactCount,
            bool retainedLateTail,
            bool alreadyAccepted)
        {
            ClearedFactCount = clearedFactCount;
            RetainedLateTail = retainedLateTail;
            AlreadyAccepted = alreadyAccepted;
        }

        public int ClearedFactCount { get; }

        public bool RetainedLateTail { get; }

        public bool AlreadyAccepted { get; }
    }

    /// <summary>
    /// 将事实逻辑 scope 解析为唯一物理 ASC/Session outbox owner，禁止同一事实双写。
    /// </summary>
    public static class GasBoundaryOutboxRouter
    {
        /// <summary>
        /// 返回一个事实 scope 的唯一物理 owner 类型。
        /// </summary>
        public static GasBoundaryOwnerKind ResolveOwnerKind(GasBoundaryFactScope scope)
        {
            return scope == GasBoundaryFactScope.Asc
                ? GasBoundaryOwnerKind.Asc
                : scope == GasBoundaryFactScope.BattleInstance ||
                  scope == GasBoundaryFactScope.Session
                    ? GasBoundaryOwnerKind.Session
                    : GasBoundaryOwnerKind.None;
        }

        /// <summary>
        /// 按事实 scope 在 ASC owner 与 Session owner 中选择唯一物理 owner。
        /// </summary>
        public static bool TryResolvePhysicalOwner(
            in BoundaryFactBuffer fact,
            in GasBoundaryOwnerKey ascOwner,
            in GasBoundaryOwnerKey sessionOwner,
            out GasBoundaryOwnerKey owner,
            out GasBoundaryDrainFailure failure)
        {
            owner = default;
            failure = GasBoundaryDrainFailure.None;
            var expectedKind = ResolveOwnerKind(fact.Scope);
            if (expectedKind == GasBoundaryOwnerKind.None ||
                fact.ScopeStableId == 0 || fact.ScopeGeneration == 0)
            {
                failure = GasBoundaryDrainFailure.FactInvalid;
                return false;
            }

            if (expectedKind == GasBoundaryOwnerKind.Asc)
            {
                if (!ascOwner.IsValid ||
                    fact.ScopeStableId != ascOwner.OwnerStableId ||
                    fact.ScopeGeneration != ascOwner.OwnerGeneration)
                {
                    failure = GasBoundaryDrainFailure.FactScopeMismatch;
                    return false;
                }

                owner = ascOwner;
            }
            else
            {
                if (!sessionOwner.IsValid ||
                    fact.Scope == GasBoundaryFactScope.Session &&
                    (fact.ScopeStableId != sessionOwner.OwnerStableId ||
                     fact.ScopeGeneration != sessionOwner.OwnerGeneration))
                {
                    failure = GasBoundaryDrainFailure.FactScopeMismatch;
                    return false;
                }

                if (fact.Scope == GasBoundaryFactScope.BattleInstance &&
                    (fact.BattleInstanceId == 0 || fact.BattleInstanceGeneration == 0 ||
                     fact.BattleInstanceId != fact.ScopeStableId ||
                     fact.BattleInstanceGeneration != fact.ScopeGeneration))
                {
                    failure = GasBoundaryDrainFailure.FactScopeMismatch;
                    return false;
                }

                owner = sessionOwner;
            }

            if (fact.EventId.SimulationEpoch != 0 &&
                fact.EventId.SimulationEpoch != owner.SimulationEpoch)
            {
                failure = GasBoundaryDrainFailure.FactOwnerMismatch;
                return false;
            }

            if (fact.EventId.OwnerKind != GasBoundaryOwnerKind.None &&
                (fact.EventId.OwnerKind != owner.OwnerKind ||
                 fact.EventId.OwnerStableId != owner.OwnerStableId ||
                 fact.EventId.OwnerGeneration != owner.OwnerGeneration))
            {
                failure = GasBoundaryDrainFailure.FactOwnerMismatch;
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// 实现 scoped Cleanup Buffer 的 append、freeze、receipt、retry 与 late-tail 状态机。
    /// </summary>
    public static class GasBoundaryDrainProtocol
    {
        /// <summary>
        /// 只验证一次追加所需的 owner、阶段与事实路由，不分配序号也不写入 outbox。
        /// </summary>
        internal static bool TryValidateFactAppend(
            in BoundaryDrainState state,
            in BoundaryFactBuffer fact,
            out GasBoundaryDrainFailure failure)
        {
            failure = ValidateOwner(in state);
            if (failure != GasBoundaryDrainFailure.None)
                return false;

            if (state.Phase == GasBoundaryDrainPhase.Accepted ||
                !IsAppendPhase(state.Phase))
            {
                failure = GasBoundaryDrainFailure.InvalidPhase;
                return false;
            }

            if (state.NextOwnerSequence == ulong.MaxValue)
            {
                failure = GasBoundaryDrainFailure.SequenceOverflow;
                return false;
            }

            return TryValidateFactRoute(in state, in fact, out failure);
        }

        /// <summary>
        /// 纯计算最终 EventId 与追加后的 DrainState，校验失败或成功均不写入 outbox。
        /// </summary>
        internal static bool TryPrepareFactAppend(
            in BoundaryDrainState state,
            in BoundaryFactBuffer fact,
            out BoundaryFactBuffer preparedFact,
            out BoundaryDrainState stateAfter,
            out GasBoundaryDrainFailure failure)
        {
            preparedFact = default;
            stateAfter = default;
            if (!TryValidateFactAppend(in state, in fact, out failure))
                return false;

            var sequence = fact.EventId.OwnerSequence;
            if (sequence != 0 && sequence != state.NextOwnerSequence)
            {
                failure = GasBoundaryDrainFailure.FactSequenceMismatch;
                return false;
            }

            if (state.Phase == GasBoundaryDrainPhase.InFlight &&
                sequence != 0 && sequence <= state.InFlightWatermark)
            {
                failure = GasBoundaryDrainFailure.FactSequenceMismatch;
                return false;
            }

            preparedFact = fact;
            preparedFact.EventId.SimulationEpoch = state.SimulationEpoch;
            preparedFact.EventId.OwnerKind = state.OwnerKind;
            preparedFact.EventId.OwnerStableId = state.OwnerStableId;
            preparedFact.EventId.OwnerGeneration = state.OwnerGeneration;
            preparedFact.EventId.OwnerSequence = state.NextOwnerSequence;
            stateAfter = state;
            stateAfter.NextOwnerSequence++;
            if (stateAfter.Phase == GasBoundaryDrainPhase.Idle)
                stateAfter.Phase = GasBoundaryDrainPhase.Pending;
            return true;
        }

        /// <summary>
        /// 以简化输出形式追加一条 fact，并返回分配到的 OwnerSequence。
        /// </summary>
        public static bool TryAppendFactWithSequence(
            ref BoundaryDrainState state,
            DynamicBuffer<BoundaryFactBuffer> outbox,
            in BoundaryFactBuffer fact,
            out ulong ownerSequence,
            out GasBoundaryDrainFailure failure)
        {
            ownerSequence = 0;
            if (!TryPrepareFactAppend(
                in state,
                in fact,
                out var preparedFact,
                out var stateAfter,
                out failure))
            {
                return false;
            }

            outbox.Add(preparedFact);
            state = stateAfter;
            ownerSequence = preparedFact.EventId.OwnerSequence;
            return true;
        }

        /// <summary>
        /// 冻结 Pending outbox 的稳定 prefix；同一 InFlight batch 的再次调用只返回原 identity。
        /// </summary>
        public static bool TryFreeze(
            ref BoundaryDrainState state,
            DynamicBuffer<BoundaryFactBuffer> outbox,
            ulong batchId,
            bool cleanupShell,
            out GasBoundaryDrainReceipt receipt,
            out GasBoundaryDrainFailure failure)
        {
            receipt = default;
            failure = ValidateOwner(in state);
            if (failure != GasBoundaryDrainFailure.None)
                return false;
            if (batchId == 0)
            {
                failure = GasBoundaryDrainFailure.InvalidBatchId;
                return false;
            }

            if (state.Phase == GasBoundaryDrainPhase.InFlight)
                return TryReuseInFlight(in state, outbox, batchId, out receipt, out failure);

            if (state.Phase != GasBoundaryDrainPhase.Pending &&
                state.Phase != GasBoundaryDrainPhase.Idle)
            {
                failure = GasBoundaryDrainFailure.InvalidPhase;
                return false;
            }

            if (outbox.Length == 0)
            {
                if (!cleanupShell)
                {
                    failure = GasBoundaryDrainFailure.OutboxEmpty;
                    return false;
                }

                state.Phase = GasBoundaryDrainPhase.InFlight;
                state.BatchId = batchId;
                state.InFlightWatermark = 0;
                receipt = CreateNoFactReceipt(in state);
                return true;
            }

            if (state.Phase == GasBoundaryDrainPhase.Idle)
            {
                failure = GasBoundaryDrainFailure.StateInvariantViolation;
                return false;
            }

            if (!TryReadFactRange(in state, outbox, out var first, out var last, out var count,
                    out failure))
                return false;

            state.BatchId = batchId;
            state.InFlightWatermark = last;
            state.Phase = GasBoundaryDrainPhase.InFlight;
            receipt = new GasBoundaryDrainReceipt(
                CreateOwnerKey(in state),
                batchId,
                GasBoundaryDrainReceiptKind.Facts,
                first,
                last,
                last,
                count);
            return true;
        }

        /// <summary>
        /// 使用 explicit NoFactReceipt 启动空 cleanup shell 的 InFlight 接管。
        /// </summary>
        public static bool TryBeginNoFactDrain(
            ref BoundaryDrainState state,
            DynamicBuffer<BoundaryFactBuffer> outbox,
            ulong batchId,
            out GasBoundaryNoFactReceipt receipt,
            out GasBoundaryDrainFailure failure)
        {
            receipt = default;
            var accepted = TryFreeze(
                ref state,
                outbox,
                batchId,
                true,
                out var drainReceipt,
                out failure);
            if (accepted && !drainReceipt.IsNoFact)
            {
                failure = GasBoundaryDrainFailure.NoFactReceiptMismatch;
                return false;
            }

            if (accepted)
                receipt = new GasBoundaryNoFactReceipt(in drainReceipt);
            return accepted;
        }

        /// <summary>
        /// 在 staging 成功后只清理不高于冻结 watermark 的 prefix，并保留 late tail。
        /// </summary>
        public static bool TryAccept(
            ref BoundaryDrainState state,
            DynamicBuffer<BoundaryFactBuffer> outbox,
            in GasBoundaryDrainReceipt receipt,
            out GasBoundaryDrainAcceptance acceptance,
            out GasBoundaryDrainFailure failure)
        {
            acceptance = default;
            failure = ValidateOwner(in state);
            if (failure != GasBoundaryDrainFailure.None)
                return false;
            if (!receipt.IsValid || receipt.Owner != CreateOwnerKey(in state))
            {
                failure = GasBoundaryDrainFailure.ReceiptMismatch;
                return false;
            }

            if (state.Phase == GasBoundaryDrainPhase.Accepted)
            {
                if (!MatchesState(in state, in receipt))
                {
                    failure = GasBoundaryDrainFailure.ReceiptMismatch;
                    return false;
                }

                acceptance = new GasBoundaryDrainAcceptance(0, false, true);
                return true;
            }

            if (state.Phase != GasBoundaryDrainPhase.InFlight ||
                !MatchesState(in state, in receipt))
            {
                failure = receipt.IsNoFact
                    ? GasBoundaryDrainFailure.NoFactReceiptMismatch
                    : GasBoundaryDrainFailure.ReceiptMismatch;
                return false;
            }

            if (receipt.IsNoFact)
                return AcceptNoFact(ref state, outbox, out acceptance, out failure);

            if (!TryValidateFrozenReceipt(in state, outbox, in receipt, out failure))
                return false;

            var clearedCount = ClearAcceptedPrefix(outbox, receipt.InFlightWatermark);
            var retainedTail = outbox.Length != 0;
            if (retainedTail)
            {
                state.Phase = GasBoundaryDrainPhase.Pending;
                state.BatchId = 0;
                state.InFlightWatermark = 0;
            }
            else
            {
                state.Phase = GasBoundaryDrainPhase.Accepted;
            }

            acceptance = new GasBoundaryDrainAcceptance(clearedCount, retainedTail, false);
            return true;
        }

        /// <summary>
        /// 将已接管的 live owner 状态折叠回 Idle，保留永不重置的 NextOwnerSequence。
        /// </summary>
        public static bool TryFoldAcceptedToIdle(
            ref BoundaryDrainState state,
            out GasBoundaryDrainFailure failure)
        {
            failure = ValidateOwner(in state);
            if (failure != GasBoundaryDrainFailure.None)
                return false;
            if (state.Phase == GasBoundaryDrainPhase.Idle)
                return true;
            if (state.Phase != GasBoundaryDrainPhase.Accepted)
            {
                failure = GasBoundaryDrainFailure.InvalidPhase;
                return false;
            }

            state.Phase = GasBoundaryDrainPhase.Idle;
            state.BatchId = 0;
            state.InFlightWatermark = 0;
            return true;
        }

        /// <summary>
        /// 返回当前状态的物理 owner key，供 managed staging 去重而不读取 ECS identity。
        /// </summary>
        public static GasBoundaryOwnerKey GetOwnerKey(in BoundaryDrainState state)
        {
            return CreateOwnerKey(in state);
        }

        /// <summary>
        /// 接受空 shell 的 NoFactReceipt，并把 shell 标记为可清理 Accepted。
        /// </summary>
        private static bool AcceptNoFact(
            ref BoundaryDrainState state,
            DynamicBuffer<BoundaryFactBuffer> outbox,
            out GasBoundaryDrainAcceptance acceptance,
            out GasBoundaryDrainFailure failure)
        {
            acceptance = default;
            failure = outbox.Length == 0 && state.InFlightWatermark == 0
                ? GasBoundaryDrainFailure.None
                : GasBoundaryDrainFailure.NoFactReceiptMismatch;
            if (failure != GasBoundaryDrainFailure.None)
                return false;

            state.Phase = GasBoundaryDrainPhase.Accepted;
            acceptance = new GasBoundaryDrainAcceptance(0, false, false);
            return true;
        }

        /// <summary>
        /// 在 InFlight 重试时复用原 BatchId 与 watermark，绝不重新冻结晚到 tail。
        /// </summary>
        private static bool TryReuseInFlight(
            in BoundaryDrainState state,
            DynamicBuffer<BoundaryFactBuffer> outbox,
            ulong batchId,
            out GasBoundaryDrainReceipt receipt,
            out GasBoundaryDrainFailure failure)
        {
            receipt = default;
            failure = batchId == state.BatchId
                ? GasBoundaryDrainFailure.None
                : GasBoundaryDrainFailure.ReceiptMismatch;
            if (failure != GasBoundaryDrainFailure.None)
                return false;

            if (state.InFlightWatermark == 0)
            {
                if (outbox.Length != 0)
                {
                    failure = GasBoundaryDrainFailure.StateInvariantViolation;
                    return false;
                }

                receipt = CreateNoFactReceipt(in state);
                return true;
            }

            if (!TryReadFactRange(in state, outbox, out var first, out var last, out var count,
                    out failure, state.InFlightWatermark))
                return false;
            receipt = new GasBoundaryDrainReceipt(
                CreateOwnerKey(in state),
                state.BatchId,
                GasBoundaryDrainReceiptKind.Facts,
                first,
                last,
                state.InFlightWatermark,
                count);
            return true;
        }

        /// <summary>
        /// 扫描 outbox 并验证所有事实都属于当前物理 owner，同时冻结指定 prefix 的范围。
        /// </summary>
        private static bool TryReadFactRange(
            in BoundaryDrainState state,
            DynamicBuffer<BoundaryFactBuffer> outbox,
            out ulong first,
            out ulong last,
            out int count,
            out GasBoundaryDrainFailure failure,
            ulong maximumSequence = ulong.MaxValue)
        {
            first = 0;
            last = 0;
            count = 0;
            failure = GasBoundaryDrainFailure.None;
            for (var index = 0; index < outbox.Length; index++)
            {
                var fact = outbox[index];
                if (!IsFactOwnedBy(in state, in fact) || fact.EventId.OwnerSequence == 0)
                {
                    failure = GasBoundaryDrainFailure.FactOwnerMismatch;
                    return false;
                }

                if (fact.EventId.OwnerSequence > maximumSequence)
                    continue;

                if (first == 0 || fact.EventId.OwnerSequence < first)
                    first = fact.EventId.OwnerSequence;
                if (fact.EventId.OwnerSequence > last)
                    last = fact.EventId.OwnerSequence;
                count++;
            }

            if (count == 0 || first == 0 || last == 0)
            {
                failure = GasBoundaryDrainFailure.OutboxEmpty;
                return false;
            }

            // watermark 内必须是连续且无重复的 owner sequence，避免 prefix 清理误删带洞事实。
            if (last - first != (ulong)(count - 1))
            {
                failure = GasBoundaryDrainFailure.FactSequenceMismatch;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 验证 receipt 的冻结 prefix 统计仍与源 outbox 一致，防止越界清理或伪造确认。
        /// </summary>
        private static bool TryValidateFrozenReceipt(
            in BoundaryDrainState state,
            DynamicBuffer<BoundaryFactBuffer> outbox,
            in GasBoundaryDrainReceipt receipt,
            out GasBoundaryDrainFailure failure)
        {
            if (!TryReadFactRange(in state, outbox, out var first, out var last, out var count,
                    out failure, receipt.InFlightWatermark))
                return false;
            if (first != receipt.FirstOwnerSequence ||
                last != receipt.LastOwnerSequence ||
                count != receipt.FactCount)
            {
                failure = GasBoundaryDrainFailure.ReceiptMismatch;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 从尾部向前删除已被 staging 接管的 prefix，保持晚到事实和相对顺序不变。
        /// </summary>
        private static int ClearAcceptedPrefix(
            DynamicBuffer<BoundaryFactBuffer> outbox,
            ulong watermark)
        {
            var cleared = 0;
            for (var index = outbox.Length - 1; index >= 0; index--)
            {
                if (outbox[index].EventId.OwnerSequence > watermark)
                    continue;
                outbox.RemoveAt(index);
                cleared++;
            }

            return cleared;
        }

        /// <summary>
        /// 检查事实在当前 owner 上的 scope、Epoch、序号和身份字段均满足冻结契约。
        /// </summary>
        private static bool TryValidateFactRoute(
            in BoundaryDrainState state,
            in BoundaryFactBuffer fact,
            out GasBoundaryDrainFailure failure)
        {
            failure = GasBoundaryDrainFailure.None;
            var expectedKind = GasBoundaryOutboxRouter.ResolveOwnerKind(fact.Scope);
            if (expectedKind == GasBoundaryOwnerKind.None ||
                fact.ScopeStableId == 0 || fact.ScopeGeneration == 0 ||
                fact.Kind == GasBoundaryFactKind.None)
            {
                failure = GasBoundaryDrainFailure.FactInvalid;
                return false;
            }

            if (expectedKind != state.OwnerKind)
            {
                failure = GasBoundaryDrainFailure.FactScopeMismatch;
                return false;
            }

            if (fact.Scope == GasBoundaryFactScope.Asc &&
                (fact.ScopeStableId != state.OwnerStableId ||
                 fact.ScopeGeneration != state.OwnerGeneration))
            {
                failure = GasBoundaryDrainFailure.FactScopeMismatch;
                return false;
            }

            if (fact.Scope == GasBoundaryFactScope.Session &&
                (fact.ScopeStableId != state.OwnerStableId ||
                 fact.ScopeGeneration != state.OwnerGeneration))
            {
                failure = GasBoundaryDrainFailure.FactScopeMismatch;
                return false;
            }

            if (fact.Scope == GasBoundaryFactScope.BattleInstance &&
                (fact.BattleInstanceId == 0 || fact.BattleInstanceGeneration == 0 ||
                 fact.BattleInstanceId != fact.ScopeStableId ||
                 fact.BattleInstanceGeneration != fact.ScopeGeneration))
            {
                failure = GasBoundaryDrainFailure.FactScopeMismatch;
                return false;
            }

            if (fact.EventId.SimulationEpoch != 0 &&
                fact.EventId.SimulationEpoch != state.SimulationEpoch)
            {
                failure = GasBoundaryDrainFailure.FactOwnerMismatch;
                return false;
            }

            if (fact.EventId.OwnerKind != GasBoundaryOwnerKind.None &&
                (fact.EventId.OwnerKind != state.OwnerKind ||
                 fact.EventId.OwnerStableId != state.OwnerStableId ||
                 fact.EventId.OwnerGeneration != state.OwnerGeneration))
            {
                failure = GasBoundaryDrainFailure.FactOwnerMismatch;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 检查 outbox 当前阶段是否允许追加事实。
        /// </summary>
        private static bool IsAppendPhase(GasBoundaryDrainPhase phase)
        {
            return phase == GasBoundaryDrainPhase.Idle ||
                   phase == GasBoundaryDrainPhase.Pending ||
                   phase == GasBoundaryDrainPhase.InFlight;
        }

        /// <summary>
        /// 验证 drain state 的物理 owner identity 与持久序号根没有损坏。
        /// </summary>
        private static GasBoundaryDrainFailure ValidateOwner(in BoundaryDrainState state)
        {
            return state.SimulationEpoch != 0 &&
                   state.OwnerKind != GasBoundaryOwnerKind.None &&
                   state.OwnerStableId != 0 &&
                   state.OwnerGeneration != 0 &&
                   state.NextOwnerSequence != 0
                ? GasBoundaryDrainFailure.None
                : GasBoundaryDrainFailure.InvalidOwner;
        }

        /// <summary>
        /// 构造当前状态的稳定物理 owner key。
        /// </summary>
        private static GasBoundaryOwnerKey CreateOwnerKey(in BoundaryDrainState state)
        {
            return new GasBoundaryOwnerKey(
                state.SimulationEpoch,
                state.OwnerKind,
                state.OwnerStableId,
                state.OwnerGeneration);
        }

        /// <summary>
        /// 判断 receipt 是否精确对应当前 InFlight/Accepted 状态。
        /// </summary>
        private static bool MatchesState(
            in BoundaryDrainState state,
            in GasBoundaryDrainReceipt receipt)
        {
            return state.BatchId == receipt.BatchId &&
                   state.InFlightWatermark == receipt.InFlightWatermark &&
                   receipt.IsNoFact == (state.InFlightWatermark == 0);
        }

        /// <summary>
        /// 为当前空 shell 构造不携带 Battle 语义的 NoFactReceipt。
        /// </summary>
        private static GasBoundaryDrainReceipt CreateNoFactReceipt(
            in BoundaryDrainState state)
        {
            return new GasBoundaryDrainReceipt(
                CreateOwnerKey(in state),
                state.BatchId,
                GasBoundaryDrainReceiptKind.NoFact,
                0,
                0,
                0,
                0);
        }

        /// <summary>
        /// 判断一条现存事实是否仍归属于当前物理 owner，供 retry 前置验证复用。
        /// </summary>
        private static bool IsFactOwnedBy(
            in BoundaryDrainState state,
            in BoundaryFactBuffer fact)
        {
            return fact.EventId.SimulationEpoch == state.SimulationEpoch &&
                   fact.EventId.OwnerKind == state.OwnerKind &&
                   fact.EventId.OwnerStableId == state.OwnerStableId &&
                   fact.EventId.OwnerGeneration == state.OwnerGeneration;
        }
    }
}
