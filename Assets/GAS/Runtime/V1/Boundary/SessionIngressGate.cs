using System;
using System.Collections.Generic;

namespace GAS.Runtime
{
    /// <summary>
    /// 标识 managed Gate 的单向生命周期，Unbound 安装态与任一生命周期 cutoff 后均同步拒绝请求。
    /// </summary>
    internal enum SessionIngressGatePhase : byte
    {
        Unbound = 0,
        Open = 1,
        Closing = 2,
        Closed = 3,
    }

    /// <summary>
    /// 冻结首次关闭 Gate 的唯一原因，避免 BattleTerminal、SessionFault 与 OwnerDisposal 互相覆盖。
    /// </summary>
    internal enum SessionIngressCloseCause : byte
    {
        None = 0,
        BattleTerminal = 1,
        SessionFault = 2,
        OwnerDisposal = 3,
    }

    /// <summary>
    /// 汇总一次消费确认中成功推进、已终态与未知序号的数量，避免静默忽略错误 ack。
    /// </summary>
    internal readonly struct GasIngressAcknowledgeResult
    {
        internal readonly int ConsumedCount;
        internal readonly int AlreadyTerminalCount;
        internal readonly int UnknownSequenceCount;

        /// <summary>
        /// 使用三类互斥计数创建消费确认结果。
        /// </summary>
        internal GasIngressAcknowledgeResult(
            int consumedCount,
            int alreadyTerminalCount,
            int unknownSequenceCount)
        {
            ConsumedCount = consumedCount;
            AlreadyTerminalCount = alreadyTerminalCount;
            UnknownSequenceCount = unknownSequenceCount;
        }
    }

    /// <summary>
    /// 冻结 FaultClose 时全部 accepted-outstanding 的序号区间、数量与规范 FNV-1a 指纹。
    /// </summary>
    internal readonly struct GasIngressFaultCloseReceipt
    {
        internal readonly ulong FaultId;
        internal readonly ulong FirstRequestSequence;
        internal readonly ulong LastRequestSequence;
        internal readonly int OutstandingCount;
        internal readonly ulong OutstandingFnv1A64Hash;
        internal readonly ulong AcceptedHighWatermark;

        /// <summary>
        /// 使用 Fault 身份与完整 outstanding 审计摘要创建幂等关闭回执。
        /// </summary>
        internal GasIngressFaultCloseReceipt(
            ulong faultId,
            ulong firstRequestSequence,
            ulong lastRequestSequence,
            int outstandingCount,
            ulong outstandingFnv1A64Hash,
            ulong acceptedHighWatermark)
        {
            FaultId = faultId;
            FirstRequestSequence = firstRequestSequence;
            LastRequestSequence = lastRequestSequence;
            OutstandingCount = outstandingCount;
            OutstandingFnv1A64Hash = outstandingFnv1A64Hash;
            AcceptedHighWatermark = acceptedHighWatermark;
        }
    }

    /// <summary>
    /// 作为 Session 唯一 managed 入站所有者，在同一把锁内维护权限、去重、预算、journal 与 Fault 终态。
    /// </summary>
    internal sealed class SessionIngressGate
    {
        private const ushort BoundaryPayloadSchemaVersion = 1;
        private readonly object _sync = new object();
        private readonly List<GasBoundaryJournalRecord> _journal =
            new List<GasBoundaryJournalRecord>();
        private readonly List<GasBoundaryJournalRecord> _acceptedLedger =
            new List<GasBoundaryJournalRecord>();
        private readonly Dictionary<ulong, GasBoundaryJournalRecord> _byRequestId =
            new Dictionary<ulong, GasBoundaryJournalRecord>();
        private readonly Dictionary<GasRequestKey, GasRequestTerminal> _requestTerminals =
            new Dictionary<GasRequestKey, GasRequestTerminal>();
        private readonly List<GasRequestTerminal> _undrainedRequestTerminals =
            new List<GasRequestTerminal>();

        private GasIngressAuthoritySnapshot _authority;
        private SessionIngressGatePhase _phase;
        private SessionIngressCloseCause _closeCause;
        private ulong _nextRequestSequence = 1;
        private ulong _acceptedHighWatermark;
        private int _outstandingCount;
        private long _outstandingPayloadBytes;
        private GasIngressFaultCloseReceipt _faultCloseReceipt;

        /// <summary>
        /// 创建尚未绑定 Session 的关闭 Gate，供 World 安装阶段安全发布 Port。
        /// </summary>
        internal SessionIngressGate()
        {
            _phase = SessionIngressGatePhase.Unbound;
        }

        /// <summary>
        /// 创建可选预绑定 Gate；三个参数全为零时保持 Unbound，非零时冻结初始总预算。
        /// </summary>
        internal SessionIngressGate(
            ulong simulationEpoch,
            int maxIngressCommandCount,
            long maxIngressPayloadBytes)
            : this()
        {
            if (simulationEpoch == 0 && maxIngressCommandCount == 0 && maxIngressPayloadBytes == 0)
                return;

            var snapshot = new GasIngressAuthoritySnapshot(
                simulationEpoch,
                maxIngressCommandCount,
                maxIngressPayloadBytes,
                Array.Empty<GasIngressBattleAuthority>(),
                Array.Empty<GasIngressAscAuthority>());
            if (!ReplaceAuthoritySnapshot(snapshot))
                throw new ArgumentOutOfRangeException(nameof(simulationEpoch), "初始 ingress 权限或预算无效。");
        }

        /// <summary>
        /// 由 World owner 原子替换纯值权限快照；首次绑定 Epoch，之后拒绝跨 Epoch 或预算漂移。
        /// </summary>
        internal bool ReplaceAuthoritySnapshot(in GasIngressAuthoritySnapshot snapshot)
        {
            if (!snapshot.IsWellFormed())
                return false;

            lock (_sync)
            {
                if (_authority.IsConfigured && !MatchesFrozenSession(snapshot))
                    return false;

                _authority = snapshot.Clone();
                if (_phase == SessionIngressGatePhase.Unbound)
                    _phase = SessionIngressGatePhase.Open;
                return true;
            }
        }

        /// <summary>
        /// 在唯一锁内校验并接受候选命令，exact duplicate 复用原 RequestSequence 且不推进任何计数。
        /// </summary>
        internal GasCommandAcceptResult TryAccept(
            in GasBoundaryCommandDraft draft,
            ReadOnlySpan<byte> payload)
        {
            lock (_sync)
            {
                var phaseStatus = GetPhaseRejectionStatus();
                if (phaseStatus.HasValue)
                    return Reject(
                        draft.Context.SimulationEpoch,
                        draft.Context.RequestId,
                        phaseStatus.Value);

                var profileStatus = ValidateRuntimeV1SupportProfile(draft, payload.Length);
                if (profileStatus.HasValue)
                    return Reject(
                        draft.Context.SimulationEpoch,
                        draft.Context.RequestId,
                        profileStatus.Value);

                var basicStatus = ValidateBasicRequest(draft, payload, out var payloadHash);
                if (basicStatus.HasValue)
                    return Reject(
                        draft.Context.SimulationEpoch,
                        draft.Context.RequestId,
                        basicStatus.Value);

                if (_byRequestId.TryGetValue(draft.Context.RequestId, out var previous))
                    return ResolveDuplicate(previous, draft, payloadHash, payload);

                var authorityStatus = ValidateAuthorityAndContract(draft);
                if (authorityStatus.HasValue)
                    return Reject(
                        draft.Context.SimulationEpoch,
                        draft.Context.RequestId,
                        authorityStatus.Value);

                var capacityStatus = ValidateCapacity(payload.Length);
                if (capacityStatus.HasValue)
                    return Reject(
                        draft.Context.SimulationEpoch,
                        draft.Context.RequestId,
                        capacityStatus.Value);

                return AppendAccepted(draft, payloadHash, payload);
            }
        }

        /// <summary>
        /// 原子切走调用时刻的完整 journal window，使锁释放后的新请求只进入 cutoff 之后的 tail。
        /// </summary>
        internal bool TryFreezeIngressWindow(out GasBoundaryJournalRecord[] records)
        {
            return TryFreezeIngressWindow(out records, out _);
        }

        /// <summary>
        /// 原子切走当前 journal window，并返回该窗口最后一个 RequestSequence 作为显式 cutoff。
        /// </summary>
        internal bool TryFreezeIngressWindow(
            out GasBoundaryJournalRecord[] records,
            out ulong cutoffRequestSequence)
        {
            lock (_sync)
            {
                if (_phase != SessionIngressGatePhase.Open || _journal.Count == 0)
                {
                    records = Array.Empty<GasBoundaryJournalRecord>();
                    cutoffRequestSequence = 0;
                    return false;
                }

                records = _journal.ToArray();
                cutoffRequestSequence = records[records.Length - 1].RequestSequence;
                _journal.Clear();
                return true;
            }
        }

        /// <summary>
        /// 在唯一锁内按 RequestSequence 幂等推进 Consumed，并显式统计未知或已终态序号。
        /// </summary>
        internal GasIngressAcknowledgeResult AcknowledgeConsumed(
            ReadOnlySpan<ulong> requestSequences)
        {
            lock (_sync)
            {
                var consumedCount = 0;
                var alreadyTerminalCount = 0;
                var unknownSequenceCount = 0;

                for (var index = 0; index < requestSequences.Length; index++)
                {
                    var record = FindByRequestSequence(requestSequences[index]);
                    if (record == null)
                        unknownSequenceCount++;
                    else if (record.MarkConsumed())
                    {
                        ReleaseOutstandingCapacity(record);
                        consumedCount++;
                    }
                    else
                        alreadyTerminalCount++;
                }

                return new GasIngressAcknowledgeResult(
                    consumedCount,
                    alreadyTerminalCount,
                    unknownSequenceCount);
            }
        }

        /// <summary>
        /// 从持久 RequestTerminal ledger 幂等读取指定 key，drain 只影响交付队列而不影响读取。
        /// </summary>
        internal bool TryReadRequestTerminal(
            in GasRequestKey requestKey,
            out GasRequestTerminal terminal)
        {
            lock (_sync)
                return _requestTerminals.TryGetValue(requestKey, out terminal);
        }

        /// <summary>
        /// 按首次发布顺序原子取走当前尚未 drain 的 RequestTerminal。
        /// </summary>
        internal bool TryDrainRequestTerminals(out GasRequestTerminal[] terminals)
        {
            lock (_sync)
            {
                if (_undrainedRequestTerminals.Count == 0)
                {
                    terminals = Array.Empty<GasRequestTerminal>();
                    return false;
                }

                terminals = _undrainedRequestTerminals.ToArray();
                _undrainedRequestTerminals.Clear();
                return true;
            }
        }

        /// <summary>
        /// 将 Kernel 已提交的 typed result 原子写入 Accepted RequestKey 的唯一终态 ledger。
        /// </summary>
        internal GasRequestTerminalPublishStatus TryPublishRequestTerminal(
            in GasRequestTerminal terminal)
        {
            lock (_sync)
            {
                if (!terminal.IsWellFormed())
                    return GasRequestTerminalPublishStatus.InvalidTerminal;
                if (_requestTerminals.TryGetValue(terminal.RequestKey, out var previous))
                {
                    return previous.Equals(terminal)
                        ? GasRequestTerminalPublishStatus.Duplicate
                        : GasRequestTerminalPublishStatus.Conflict;
                }
                if (_phase == SessionIngressGatePhase.Closed ||
                    !TryResolveAcceptedRecord(in terminal.RequestKey, out var record))
                    return GasRequestTerminalPublishStatus.UnknownRequest;
                if (record.Kind != terminal.CommandKind)
                    return GasRequestTerminalPublishStatus.InvalidTerminal;

                PublishRequestTerminalUnsafe(in terminal);
                return GasRequestTerminalPublishStatus.Published;
            }
        }

        /// <summary>
        /// 原子关闭 Gate、终结全部 accepted-outstanding 并生成可重复读取的 FNV-1a 审计回执。
        /// </summary>
        internal GasIngressFaultCloseReceipt CloseForFault(ulong faultId)
        {
            if (faultId == 0)
                throw new ArgumentOutOfRangeException(nameof(faultId), "FaultClose 必须携带非零 FaultId。");
            lock (_sync)
            {
                if (_phase == SessionIngressGatePhase.Closed)
                {
                    return _closeCause == SessionIngressCloseCause.SessionFault
                        ? _faultCloseReceipt
                        : new GasIngressFaultCloseReceipt(
                            faultId,
                            0,
                            0,
                            0,
                            0,
                            _acceptedHighWatermark);
                }

                _phase = SessionIngressGatePhase.Closing;
                _closeCause = SessionIngressCloseCause.SessionFault;
                _faultCloseReceipt = BuildFaultCloseReceipt(faultId);
                _journal.Clear();
                _phase = SessionIngressGatePhase.Closed;
                return _faultCloseReceipt;
            }
        }

        /// <summary>
        /// 在 BattleTerminal 收敛点同步关闭 Gate，并为全部尚无业务终态的 Accepted 请求发布战局终态。
        /// </summary>
        internal void CloseForBattleTerminal(
            in BattleInstanceHandle battleInstance,
            int outcomeCode)
        {
            if (!battleInstance.IsValid)
                throw new ArgumentOutOfRangeException(
                    nameof(battleInstance),
                    "BattleTerminal 必须携带有效 BattleInstanceHandle。");
            lock (_sync)
            {
                if (_phase == SessionIngressGatePhase.Closed)
                    return;

                _phase = SessionIngressGatePhase.Closing;
                _closeCause = SessionIngressCloseCause.BattleTerminal;
                PublishCutoffTerminals(
                    GasRequestTerminalStatus.BattleTerminal,
                    0,
                    in battleInstance,
                    outcomeCode,
                    GasBoundaryCommandState.BattleTerminated);
                _journal.Clear();
                _phase = SessionIngressGatePhase.Closed;
            }
        }

        /// <summary>
        /// 在 owner 释放时同步关闭 Port，并终结尚未消费的 ledger，禁止产生无人搬运的新 journal。
        /// </summary>
        internal void CloseForOwnerDisposal()
        {
            lock (_sync)
            {
                if (_phase == SessionIngressGatePhase.Closed)
                    return;
                _phase = SessionIngressGatePhase.Closing;
                _closeCause = SessionIngressCloseCause.OwnerDisposal;
                PublishCutoffTerminals(
                    GasRequestTerminalStatus.OwnerDisposal,
                    0,
                    default,
                    0,
                    GasBoundaryCommandState.OwnerDisposed);
                _journal.Clear();
                _phase = SessionIngressGatePhase.Closed;
            }
        }

        /// <summary>
        /// 判断替换快照是否保持首次绑定的 Epoch 与两项总预算完全不变。
        /// </summary>
        private bool MatchesFrozenSession(in GasIngressAuthoritySnapshot snapshot)
        {
            return _authority.SimulationEpoch == snapshot.SimulationEpoch &&
                   _authority.MaxIngressCommandCount == snapshot.MaxIngressCommandCount &&
                   _authority.MaxIngressPayloadBytes == snapshot.MaxIngressPayloadBytes;
        }

        /// <summary>
        /// 将当前 Gate phase 映射为同步拒绝状态，Open 返回空结果。
        /// </summary>
        private GasCommandAcceptStatus? GetPhaseRejectionStatus()
        {
            if (_phase == SessionIngressGatePhase.Unbound)
                return GasCommandAcceptStatus.GateUnbound;
            if (_phase != SessionIngressGatePhase.Open)
            {
                if (_closeCause == SessionIngressCloseCause.BattleTerminal)
                    return GasCommandAcceptStatus.BattleTerminalClosed;
                if (_closeCause == SessionIngressCloseCause.OwnerDisposal)
                    return GasCommandAcceptStatus.OwnerDisposed;
                return GasCommandAcceptStatus.FaultClosed;
            }
            return null;
        }

        /// <summary>
        /// 在 payload hash、duplicate、权限与 RequestSequence 前拒绝本轮未实现的公开命令形状。
        /// </summary>
        private static GasCommandAcceptStatus? ValidateRuntimeV1SupportProfile(
            in GasBoundaryCommandDraft draft,
            int payloadLength)
        {
            if (draft.Kind == GasBoundaryCommandKind.Cancel ||
                draft.Kind == GasBoundaryCommandKind.RemoveEffect)
                return GasCommandAcceptStatus.UnsupportedByRuntimeV1Profile;

            if (draft.Kind == GasBoundaryCommandKind.ApplyEffect &&
                draft.Context.Target.Kind != GasBoundaryTargetKind.Asc)
                return GasCommandAcceptStatus.UnsupportedByRuntimeV1Profile;

            var isOneShotAbility = draft.Kind == GasBoundaryCommandKind.Activate ||
                                   draft.Kind == GasBoundaryCommandKind.Commit;
            if (isOneShotAbility &&
                (payloadLength != 0 || draft.PayloadDescriptor.SchemaVersion != 0 ||
                 draft.PayloadDescriptor.Kind != GasBoundaryCommandPayloadKind.None))
                return GasCommandAcceptStatus.UnsupportedByRuntimeV1Profile;
            return null;
        }

        /// <summary>
        /// 校验固定请求字段与 payload 描述，并从真实字节计算 FNV-1a 哈希。
        /// </summary>
        private GasCommandAcceptStatus? ValidateBasicRequest(
            in GasBoundaryCommandDraft draft,
            ReadOnlySpan<byte> payload,
            out ulong payloadHash)
        {
            payloadHash = 0;
            if (!IsValidCommandKind(draft.Kind) ||
                draft.Context.RequestId == 0 ||
                draft.Context.SourceSequence == 0 ||
                !draft.Context.BattleInstance.IsValid)
                return GasCommandAcceptStatus.InvalidRequest;

            if (draft.Context.SimulationEpoch != _authority.SimulationEpoch ||
                draft.Context.BattleInstance.SimulationEpoch != _authority.SimulationEpoch)
                return GasCommandAcceptStatus.EpochMismatch;

            if (!IsValidPayloadDescriptor(draft.PayloadDescriptor, payload.Length))
                return GasCommandAcceptStatus.PayloadInvalid;

            var hash = GasBoundaryFnv1A64.Create();
            hash.AddBytes(payload);
            payloadHash = hash.Value;
            return null;
        }

        /// <summary>
        /// 对 exact duplicate 复用原序号，对同 RequestId 的任一字段冲突显式拒绝。
        /// </summary>
        private static GasCommandAcceptResult ResolveDuplicate(
            GasBoundaryJournalRecord previous,
            in GasBoundaryCommandDraft draft,
            ulong payloadHash,
            ReadOnlySpan<byte> payload)
        {
            if (previous.IsExactDuplicate(draft, payloadHash, payload))
            {
                return new GasCommandAcceptResult(
                    GasCommandAcceptStatus.DuplicateAccepted,
                    draft.Context.SimulationEpoch,
                    draft.Context.RequestId,
                    previous.RequestSequence);
            }

            return Reject(
                draft.Context.SimulationEpoch,
                draft.Context.RequestId,
                GasCommandAcceptStatus.RequestIdConflict);
        }

        /// <summary>
        /// 按冻结顺序校验战局、来源、目标、typed 句柄与命令 payload 契约。
        /// </summary>
        private GasCommandAcceptStatus? ValidateAuthorityAndContract(
            in GasBoundaryCommandDraft draft)
        {
            var battleStatus = ValidateBattle(draft.Context.BattleInstance);
            if (battleStatus.HasValue)
                return battleStatus;

            var sourceStatus = ValidateSource(draft.Context);
            if (sourceStatus.HasValue)
                return sourceStatus;

            var targetStatus = ValidateTarget(draft.Context.Target, draft.Context.BattleInstance);
            if (targetStatus.HasValue)
                return targetStatus;

            return ValidateCommandContract(draft);
        }

        /// <summary>
        /// 仅允许权限快照中 IngressOpen 且处于 Ready 或 Running 的完整战局身份。
        /// </summary>
        private GasCommandAcceptStatus? ValidateBattle(in BattleInstanceHandle battleInstance)
        {
            if (!_authority.TryGetBattle(battleInstance, out var battle))
                return GasCommandAcceptStatus.BattleNotFound;

            var isReadyOrRunning = battle.State == GasBattleInstanceState.Ready ||
                                   battle.State == GasBattleInstanceState.Running;
            if (!battle.IngressOpen || !isReadyOrRunning)
                return GasCommandAcceptStatus.BattleNotAccepting;
            return null;
        }

        /// <summary>
        /// 校验显式 HasSource 与 SourceAsc 一致，并要求来源是同一战局中的 Ready 成员。
        /// </summary>
        private GasCommandAcceptStatus? ValidateSource(in GasBoundaryCommandContext context)
        {
            if (!context.HasSource)
            {
                return context.SourceAsc.IsValid
                    ? GasCommandAcceptStatus.SourceMustBeEmpty
                    : (GasCommandAcceptStatus?)null;
            }

            if (!context.SourceAsc.IsValid ||
                !_authority.TryGetAsc(context.SourceAsc, out var source) ||
                source.State != GasAscRegistryState.Ready)
                return GasCommandAcceptStatus.SourceMembershipMissing;

            if (!source.BattleInstance.Equals(context.BattleInstance))
                return GasCommandAcceptStatus.SourceBattleMismatch;
            return null;
        }

        /// <summary>
        /// 按闭世界目标种类校验延迟引用形状、Epoch、战局与 ASC 成员资格。
        /// </summary>
        private GasCommandAcceptStatus? ValidateTarget(
            in BoundaryTargetRef target,
            in BattleInstanceHandle commandBattle)
        {
            switch (target.Kind)
            {
                case GasBoundaryTargetKind.None:
                    return IsCanonicalNoneTarget(target)
                        ? (GasCommandAcceptStatus?)null
                        : GasCommandAcceptStatus.TargetInvalid;
                case GasBoundaryTargetKind.Asc:
                    return ValidateAscTarget(target, commandBattle);
                case GasBoundaryTargetKind.BattleSelector:
                    return ValidateSelectorTarget(target, commandBattle);
                case GasBoundaryTargetKind.DefinitionRule:
                    return ValidateDefinitionTarget(target, commandBattle);
                default:
                    return GasCommandAcceptStatus.TargetInvalid;
            }
        }

        /// <summary>
        /// 校验 ASC 目标形状及其在命令战局中的 Ready 成员资格。
        /// </summary>
        private GasCommandAcceptStatus? ValidateAscTarget(
            in BoundaryTargetRef target,
            in BattleInstanceHandle commandBattle)
        {
            var commonStatus = ValidateResolvedTargetCommon(target, commandBattle);
            if (commonStatus.HasValue)
                return commonStatus;
            if (!target.TargetAsc.IsValid || target.SelectorStableId != 0 ||
                target.DefinitionRuleIndex != 0 || !HasCanonicalAvatarFields(target) ||
                !HasCanonicalSpatialFields(target))
                return GasCommandAcceptStatus.TargetInvalid;
            if (!_authority.TryGetAsc(target.TargetAsc, out var membership) ||
                membership.State != GasAscRegistryState.Ready)
                return GasCommandAcceptStatus.TargetMembershipMissing;
            return membership.BattleInstance.Equals(commandBattle)
                ? (GasCommandAcceptStatus?)null
                : GasCommandAcceptStatus.TargetBattleMismatch;
        }

        /// <summary>
        /// 校验战局选择器目标仅携带非零稳定选择器并属于命令战局。
        /// </summary>
        private GasCommandAcceptStatus? ValidateSelectorTarget(
            in BoundaryTargetRef target,
            in BattleInstanceHandle commandBattle)
        {
            var commonStatus = ValidateResolvedTargetCommon(target, commandBattle);
            if (commonStatus.HasValue)
                return commonStatus;
            return !target.TargetAsc.IsValid &&
                   target.SelectorStableId != 0 &&
                   target.DefinitionRuleIndex == 0 &&
                   HasNoAvatarOrSpatialFields(target)
                ? (GasCommandAcceptStatus?)null
                : GasCommandAcceptStatus.TargetInvalid;
        }

        /// <summary>
        /// 校验定义规则目标仅携带非负冻结规则索引并属于命令战局。
        /// </summary>
        private GasCommandAcceptStatus? ValidateDefinitionTarget(
            in BoundaryTargetRef target,
            in BattleInstanceHandle commandBattle)
        {
            var commonStatus = ValidateResolvedTargetCommon(target, commandBattle);
            if (commonStatus.HasValue)
                return commonStatus;
            return !target.TargetAsc.IsValid &&
                   target.SelectorStableId == 0 &&
                   target.DefinitionRuleIndex >= 0 &&
                   HasNoAvatarOrSpatialFields(target)
                ? (GasCommandAcceptStatus?)null
                : GasCommandAcceptStatus.TargetInvalid;
        }

        /// <summary>
        /// 校验非空目标统一使用消费时解析策略、当前 Epoch 与命令 BattleInstance。
        /// </summary>
        private GasCommandAcceptStatus? ValidateResolvedTargetCommon(
            in BoundaryTargetRef target,
            in BattleInstanceHandle commandBattle)
        {
            if (target.ResolutionPolicy != GasBoundaryTargetResolutionPolicy.ResolveAtConsume ||
                target.SimulationEpoch != _authority.SimulationEpoch)
                return GasCommandAcceptStatus.TargetInvalid;
            return target.BattleInstance.Equals(commandBattle)
                ? (GasCommandAcceptStatus?)null
                : GasCommandAcceptStatus.TargetBattleMismatch;
        }

        /// <summary>
        /// 校验 None 目标不夹带任何可解析身份或策略字段。
        /// </summary>
        private static bool IsCanonicalNoneTarget(in BoundaryTargetRef target)
        {
            return target.ResolutionPolicy == GasBoundaryTargetResolutionPolicy.None &&
                   target.SimulationEpoch == 0 &&
                   !target.BattleInstance.IsValid &&
                   !target.TargetAsc.IsValid &&
                   target.TargetAvatarStableId == 0 &&
                   target.TargetAvatarBindingGeneration == 0 &&
                   target.SpatialSnapshot.Equals(GasBoundarySpatialSnapshot.None) &&
                   target.SelectorStableId == 0 &&
                   target.DefinitionRuleIndex == 0;
        }

        /// <summary>
        /// 校验 ASC 目标的 Avatar 字段要么完整出现，要么完全省略，禁止半截 generation。
        /// </summary>
        private static bool HasCanonicalAvatarFields(in BoundaryTargetRef target)
        {
            return (target.TargetAvatarStableId == 0 &&
                    target.TargetAvatarBindingGeneration == 0) ||
                   target.HasAvatarBinding;
        }

        /// <summary>
        /// 校验 ASC 目标的空间字段要么为空，要么是有限且注册的 FrozenSpatial 变体。
        /// </summary>
        private static bool HasCanonicalSpatialFields(in BoundaryTargetRef target)
        {
            return target.SpatialSnapshot.Equals(GasBoundarySpatialSnapshot.None) ||
                   target.HasFrozenSpatial;
        }

        /// <summary>
        /// 校验 selector/rule 目标不夹带只能属于已解析 ASC 的 Avatar 或空间快照。
        /// </summary>
        private static bool HasNoAvatarOrSpatialFields(in BoundaryTargetRef target)
        {
            return target.TargetAvatarStableId == 0 &&
                   target.TargetAvatarBindingGeneration == 0 &&
                   target.SpatialSnapshot.Equals(GasBoundarySpatialSnapshot.None);
        }

        /// <summary>
        /// 校验五种命令各自唯一的来源、目标、句柄、定义与 payload kind 组合。
        /// </summary>
        private GasCommandAcceptStatus? ValidateCommandContract(in GasBoundaryCommandDraft draft)
        {
            var payloadStatus = ValidatePayloadKindForCommand(draft.Kind, draft.PayloadDescriptor.Kind);
            if (payloadStatus.HasValue)
                return payloadStatus;

            switch (draft.Kind)
            {
                case GasBoundaryCommandKind.Activate:
                    return ValidateActivateContract(draft);
                case GasBoundaryCommandKind.Commit:
                    return ValidateCommitContract(draft);
                case GasBoundaryCommandKind.Cancel:
                    return ValidateCancelContract(draft);
                case GasBoundaryCommandKind.ApplyEffect:
                    return ValidateApplyEffectContract(draft);
                case GasBoundaryCommandKind.RemoveEffect:
                    return ValidateRemoveEffectContract(draft);
                default:
                    return GasCommandAcceptStatus.InvalidRequest;
            }
        }

        /// <summary>
        /// 校验 Activate 必须来自句柄 owner 且使用 GrantedAbility 句柄。
        /// </summary>
        private GasCommandAcceptStatus? ValidateActivateContract(in GasBoundaryCommandDraft draft)
        {
            if (!draft.Context.HasSource)
                return GasCommandAcceptStatus.SourceRequired;
            if (draft.DefinitionId != 0 ||
                draft.Context.Target.Kind != GasBoundaryTargetKind.None)
                return GasCommandAcceptStatus.InvalidRequest;
            return ValidateHandle(draft, HandleKind.GrantedAbility);
        }

        /// <summary>
        /// 校验 Commit 必须来自句柄 owner，且非空目标只能是已通过通用权限校验的 ASC。
        /// </summary>
        private GasCommandAcceptStatus? ValidateCommitContract(in GasBoundaryCommandDraft draft)
        {
            if (!draft.Context.HasSource)
                return GasCommandAcceptStatus.SourceRequired;
            var targetKind = draft.Context.Target.Kind;
            if (draft.DefinitionId != 0 ||
                (targetKind != GasBoundaryTargetKind.None && targetKind != GasBoundaryTargetKind.Asc))
                return GasCommandAcceptStatus.InvalidRequest;
            return ValidateHandle(draft, HandleKind.AbilityActivation);
        }

        /// <summary>
        /// 校验 Cancel 必须来自句柄 owner、无额外目标且使用 AbilityActivation 句柄。
        /// </summary>
        private GasCommandAcceptStatus? ValidateCancelContract(in GasBoundaryCommandDraft draft)
        {
            if (!draft.Context.HasSource)
                return GasCommandAcceptStatus.SourceRequired;
            if (draft.Context.Target.Kind != GasBoundaryTargetKind.None || draft.DefinitionId != 0)
                return GasCommandAcceptStatus.InvalidRequest;
            return ValidateHandle(draft, HandleKind.AbilityActivation);
        }

        /// <summary>
        /// 校验 ApplyEffect 必须携带稳定定义与非空目标，并且不得伪造槽句柄。
        /// </summary>
        private static GasCommandAcceptStatus? ValidateApplyEffectContract(
            in GasBoundaryCommandDraft draft)
        {
            if (draft.DefinitionId <= 0 || draft.Context.Target.Kind == GasBoundaryTargetKind.None)
                return GasCommandAcceptStatus.InvalidRequest;
            return IsEmptyHandle(draft.Handle)
                ? (GasCommandAcceptStatus?)null
                : GasCommandAcceptStatus.HandleInvalid;
        }

        /// <summary>
        /// 校验 RemoveEffect 必须来自效果 owner、目标同一 ASC 且使用 ActiveEffect 句柄。
        /// </summary>
        private GasCommandAcceptStatus? ValidateRemoveEffectContract(
            in GasBoundaryCommandDraft draft)
        {
            if (!draft.Context.HasSource)
                return GasCommandAcceptStatus.SourceRequired;
            if (draft.DefinitionId != 0 ||
                draft.Context.Target.Kind != GasBoundaryTargetKind.Asc ||
                !draft.Context.Target.TargetAsc.Equals(draft.Handle.OwnerAsc))
                return GasCommandAcceptStatus.InvalidRequest;
            return ValidateHandle(draft, HandleKind.ActiveEffect);
        }

        /// <summary>
        /// 校验诊断载体的 Kind、稳定形状、当前 Epoch 与来源 owner，不读取 live 槽状态。
        /// </summary>
        private GasCommandAcceptStatus? ValidateHandle(
            in GasBoundaryCommandDraft draft,
            HandleKind expectedKind)
        {
            if (draft.Handle.Kind != expectedKind)
                return GasCommandAcceptStatus.HandleKindMismatch;
            if (draft.Handle.SimulationEpoch == 0 ||
                !draft.Handle.OwnerAsc.IsValid ||
                draft.Handle.SlotIndex < 0 ||
                draft.Handle.SlotGeneration == 0)
                return GasCommandAcceptStatus.HandleInvalid;
            if (draft.Handle.SimulationEpoch != _authority.SimulationEpoch)
                return GasCommandAcceptStatus.EpochMismatch;
            return draft.Handle.OwnerAsc.Equals(draft.Context.SourceAsc)
                ? (GasCommandAcceptStatus?)null
                : GasCommandAcceptStatus.HandleOwnerMismatch;
        }

        /// <summary>
        /// 校验 payload kind 与 typed 命令形成闭世界组合，None 对所有命令保持合法。
        /// </summary>
        private static GasCommandAcceptStatus? ValidatePayloadKindForCommand(
            GasBoundaryCommandKind commandKind,
            GasBoundaryCommandPayloadKind payloadKind)
        {
            if (payloadKind == GasBoundaryCommandPayloadKind.None)
                return null;
            if ((commandKind == GasBoundaryCommandKind.Activate ||
                 commandKind == GasBoundaryCommandKind.Commit) &&
                payloadKind == GasBoundaryCommandPayloadKind.AbilityRequest)
                return null;
            if (commandKind == GasBoundaryCommandKind.Cancel &&
                payloadKind == GasBoundaryCommandPayloadKind.AbilityCancellation)
                return null;
            if (commandKind == GasBoundaryCommandKind.ApplyEffect &&
                payloadKind == GasBoundaryCommandPayloadKind.EffectApplication)
                return null;
            if (commandKind == GasBoundaryCommandKind.RemoveEffect &&
                payloadKind == GasBoundaryCommandPayloadKind.EffectRemoval)
                return null;
            return GasCommandAcceptStatus.PayloadInvalid;
        }

        /// <summary>
        /// 校验当前 outstanding 条数与 payload 字节预算，已消费 ledger 只保留去重审计而不占用容量。
        /// </summary>
        private GasCommandAcceptStatus? ValidateCapacity(int payloadLength)
        {
            if (_outstandingCount >= _authority.MaxIngressCommandCount)
                return GasCommandAcceptStatus.IngressCountExceeded;
            if (payloadLength > _authority.MaxIngressPayloadBytes - _outstandingPayloadBytes)
                return GasCommandAcceptStatus.IngressPayloadBytesExceeded;
            if (_nextRequestSequence == ulong.MaxValue)
                return GasCommandAcceptStatus.RequestSequenceExhausted;
            return null;
        }

        /// <summary>
        /// 深复制 payload、追加 journal 与持久 ledger 后才返回 Accepted，并最后推进单调序号。
        /// </summary>
        private GasCommandAcceptResult AppendAccepted(
            in GasBoundaryCommandDraft draft,
            ulong payloadHash,
            ReadOnlySpan<byte> payload)
        {
            var payloadCopy = new byte[payload.Length];
            payload.CopyTo(payloadCopy);
            var requestSequence = _nextRequestSequence;
            var record = new GasBoundaryJournalRecord(
                draft,
                requestSequence,
                payloadCopy,
                payloadHash);

            _journal.Add(record);
            _acceptedLedger.Add(record);
            _byRequestId.Add(record.RequestId, record);
            _outstandingCount++;
            _outstandingPayloadBytes += record.PayloadLength;
            _acceptedHighWatermark = requestSequence;
            _nextRequestSequence++;
            record.MarkSealed();
            return new GasCommandAcceptResult(
                GasCommandAcceptStatus.Accepted,
                record.SimulationEpoch,
                record.RequestId,
                requestSequence);
        }

        /// <summary>
        /// 在同一 Gate 锁内释放一个首次进入终态的 outstanding 容量，ledger 与 RequestId 去重索引保持不变。
        /// </summary>
        private void ReleaseOutstandingCapacity(GasBoundaryJournalRecord record)
        {
            if (_outstandingCount <= 0 || record.PayloadLength > _outstandingPayloadBytes)
                throw new InvalidOperationException("Ingress outstanding 容量账本已损坏。");
            _outstandingCount--;
            _outstandingPayloadBytes -= record.PayloadLength;
        }

        /// <summary>
        /// 按接受顺序查找 RequestSequence，保持 ledger 是 fault 审计的唯一有序事实源。
        /// </summary>
        private GasBoundaryJournalRecord FindByRequestSequence(ulong requestSequence)
        {
            if (requestSequence == 0 || requestSequence > _acceptedHighWatermark)
                return null;

            for (var index = 0; index < _acceptedLedger.Count; index++)
            {
                if (_acceptedLedger[index].RequestSequence == requestSequence)
                    return _acceptedLedger[index];
            }

            return null;
        }

        /// <summary>
        /// 以完整公开 key 解析 accepted ledger，拒绝仅凭 RequestSequence 跨 Epoch 或 RequestId 命中。
        /// </summary>
        private bool TryResolveAcceptedRecord(
            in GasRequestKey requestKey,
            out GasBoundaryJournalRecord record)
        {
            record = FindByRequestSequence(requestKey.RequestSequence);
            return record != null &&
                   record.SimulationEpoch == requestKey.SimulationEpoch &&
                   record.RequestId == requestKey.RequestId;
        }

        /// <summary>
        /// 以接受顺序汇总所有未消费记录并原子推进 FaultTerminated。
        /// </summary>
        private GasIngressFaultCloseReceipt BuildFaultCloseReceipt(ulong faultId)
        {
            var hash = GasBoundaryFnv1A64.Create();
            var first = 0UL;
            var last = 0UL;
            var count = 0;

            for (var index = 0; index < _acceptedLedger.Count; index++)
            {
                var record = _acceptedLedger[index];
                if (record.State == GasBoundaryCommandState.Sealed)
                {
                    if (count == 0)
                        first = record.RequestSequence;
                    last = record.RequestSequence;
                    count++;
                    hash.AddUInt64(record.RequestSequence);
                    hash.AddUInt64(record.CommandHash);
                    if (record.MarkFaultTerminated())
                        ReleaseOutstandingCapacity(record);
                }

                var terminalBattle = record.BattleInstance;
                PublishCutoffTerminalIfMissing(
                    record,
                    GasRequestTerminalStatus.SessionFault,
                    faultId,
                    in terminalBattle,
                    0);
            }

            return new GasIngressFaultCloseReceipt(
                faultId,
                first,
                last,
                count,
                count == 0 ? 0UL : hash.Value,
                _acceptedHighWatermark);
        }

        /// <summary>
        /// 为指定生命周期 cutoff 补齐所有尚未终态的 Accepted 请求并推进对应 transport 审计状态。
        /// </summary>
        private void PublishCutoffTerminals(
            GasRequestTerminalStatus terminalStatus,
            ulong faultId,
            in BattleInstanceHandle battleInstance,
            int battleOutcomeCode,
            GasBoundaryCommandState transportState)
        {
            for (var index = 0; index < _acceptedLedger.Count; index++)
            {
                var record = _acceptedLedger[index];
                var terminalBattle = terminalStatus == GasRequestTerminalStatus.BattleTerminal
                    ? battleInstance
                    : record.BattleInstance;
                PublishCutoffTerminalIfMissing(
                    record,
                    terminalStatus,
                    faultId,
                    in terminalBattle,
                    battleOutcomeCode);

                var transitioned = transportState == GasBoundaryCommandState.BattleTerminated
                    ? record.MarkBattleTerminated()
                    : record.MarkOwnerDisposed();
                if (transitioned)
                    ReleaseOutstandingCapacity(record);
            }
        }

        /// <summary>
        /// 仅在该 RequestKey 尚无业务结果时追加 cutoff terminal，已成功或拒绝的请求保持原终态。
        /// </summary>
        private void PublishCutoffTerminalIfMissing(
            GasBoundaryJournalRecord record,
            GasRequestTerminalStatus status,
            ulong faultId,
            in BattleInstanceHandle battleInstance,
            int battleOutcomeCode)
        {
            var requestKey = new GasRequestKey(
                record.SimulationEpoch,
                record.RequestId,
                record.RequestSequence);
            if (_requestTerminals.ContainsKey(requestKey))
                return;

            var terminal = GasRequestTerminal.ForCutoff(
                in requestKey,
                record.Kind,
                status,
                faultId,
                in battleInstance,
                battleOutcomeCode);
            PublishRequestTerminalUnsafe(in terminal);
        }

        /// <summary>
        /// 在持有 Gate 锁时同时写入永久读取索引与一次性有序 drain 队列。
        /// </summary>
        private void PublishRequestTerminalUnsafe(in GasRequestTerminal terminal)
        {
            _requestTerminals.Add(terminal.RequestKey, terminal);
            _undrainedRequestTerminals.Add(terminal);
        }

        /// <summary>
        /// 判断命令 kind 是否严格属于冻结的五项闭世界集合。
        /// </summary>
        private static bool IsValidCommandKind(GasBoundaryCommandKind kind)
        {
            var value = (byte)kind;
            return value >= (byte)GasBoundaryCommandKind.Activate &&
                   value <= (byte)GasBoundaryCommandKind.RemoveEffect;
        }

        /// <summary>
        /// 校验空 payload 必须使用 None，非空 payload 必须使用冻结 v1 schema 与合法用途。
        /// </summary>
        private static bool IsValidPayloadDescriptor(
            in BoundaryCommandPayloadDescriptor descriptor,
            int payloadLength)
        {
            if (payloadLength == 0)
                return descriptor.SchemaVersion == 0 &&
                       descriptor.Kind == GasBoundaryCommandPayloadKind.None;
            if (descriptor.SchemaVersion != BoundaryPayloadSchemaVersion)
                return false;
            var value = (ushort)descriptor.Kind;
            return value >= (ushort)GasBoundaryCommandPayloadKind.AbilityRequest &&
                   value <= (ushort)GasBoundaryCommandPayloadKind.EffectRemoval;
        }

        /// <summary>
        /// 校验 ApplyEffect 使用的无句柄载体确实是规范 default 值。
        /// </summary>
        private static bool IsEmptyHandle(in StableHandleDiagnosticCarrier handle)
        {
            return handle.SimulationEpoch == 0 &&
                   !handle.OwnerAsc.IsValid &&
                   handle.SlotIndex == 0 &&
                   handle.SlotGeneration == 0 &&
                   handle.Kind == HandleKind.None;
        }

        /// <summary>
        /// 创建不携带 RequestSequence 的显式拒绝结果。
        /// </summary>
        private static GasCommandAcceptResult Reject(
            ulong simulationEpoch,
            ulong requestId,
            GasCommandAcceptStatus status)
        {
            return new GasCommandAcceptResult(status, simulationEpoch, requestId, 0);
        }
    }
}
