using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.Runtime
{
    /// <summary>
    /// 保存 managed journal 到 ECS inbox 之间的一次性 unmanaged 搬运记录。
    /// </summary>
    internal struct GasIngressTransferRecord
    {
        public ulong SimulationEpoch;
        public ulong RequestId;
        public ulong RequestSequence;
        public ulong SourceSequence;
        public ulong AvailableTick;
        public byte HasSource;
        public BattleInstanceHandle BattleInstance;
        public OwnerAscHandle SourceAsc;
        public BoundaryTargetRef Target;
        public StableHandleDiagnosticCarrier SubjectHandle;
        public int DefinitionId;
        public GasBoundaryCommandKind CommandKind;
        public ushort SemanticPhaseOrdinal;
        public ushort WorkClassOrdinal;
        public ushort PayloadSchemaVersion;
        public GasBoundaryCommandPayloadKind PayloadKind;
        public int PayloadOffset;
        public int PayloadLength;
        public ulong PayloadHash;
        public ulong SemanticHash;
        public ulong CommandHash;
    }

    /// <summary>
    /// 在 Kernel 前把 Gate 冻结窗口原样搬入唯一 ECS inbox，且从不执行 GAS 业务语义。
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(GasFixedTickSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(GasTickKernelSystem))]
    internal partial class GasCommandIngressSystem : SystemBase
    {
        private const ushort ExternalCommandSemanticPhaseOrdinal = 1;
        private readonly List<ulong> _deliveredRequestSequences = new List<ulong>();
        private readonly List<ulong> _acknowledgedForCompaction = new List<ulong>();
        private EntityQuery _sessionQuery;
        private SessionIngressGate _ingressGate;

        /// <summary>
        /// 建立唯一完整 Session 查询，缺少物理 inbox 契约时不得切走 managed journal。
        /// </summary>
        protected override void OnCreate()
        {
            _sessionQuery = GetEntityQuery(
                ComponentType.ReadOnly<GasActiveSessionAuthority>(),
                ComponentType.ReadOnly<GasSessionIdentity>(),
                ComponentType.ReadOnly<SimulationTickState>(),
                ComponentType.ReadWrite<GasSessionLifecycle>(),
                ComponentType.ReadWrite<SessionFaultLatch>(),
                ComponentType.ReadWrite<BoundaryCommandInbox>(),
                ComponentType.ReadWrite<BoundaryCommandFrozenPayload>());
        }

        /// <summary>
        /// 绑定与当前 World owner 相同的唯一 ingress gate，禁止运行时重绑。
        /// </summary>
        internal void Bind(SessionIngressGate ingressGate)
        {
            if (ingressGate == null)
                throw new ArgumentNullException(nameof(ingressGate));
            if (_ingressGate != null && !ReferenceEquals(_ingressGate, ingressGate))
                throw new InvalidOperationException("GasCommandIngressSystem 不允许重绑到另一 ingress gate。");
            _ingressGate = ingressGate;
        }

        /// <summary>
        /// 原子冻结当前 Gate window，转换为 unmanaged transfer 后调度唯一 inbox append writer。
        /// </summary>
        protected override void OnUpdate()
        {
            if (_ingressGate == null)
                throw new InvalidOperationException("GasCommandIngressSystem 尚未绑定 SessionIngressGate。");
            if (_sessionQuery.CalculateEntityCount() != 1)
                return;
            _ingressGate.TryFreezeIngressWindow(out var records, out var cutoffSequence);
            if (records.Length == 0 && _acknowledgedForCompaction.Count == 0)
                return;

            var session = _sessionQuery.GetSingletonEntity();
            var identity = EntityManager.GetComponentData<GasSessionIdentity>(session);
            CreateTransferArrays(records, out var transfers, out var payloadBytes);
            var acknowledged = CreateAcknowledgedArray();
            TrackDeliveredSequences(records);
            var handle = new GasIngressAppendJob
            {
                Session = session,
                SimulationEpoch = identity.SimulationEpoch,
                CutoffRequestSequence = cutoffSequence,
                Transfers = transfers,
                TransferPayload = payloadBytes,
                AcknowledgedSequences = acknowledged,
                Ticks = GetComponentLookup<SimulationTickState>(true),
                Lifecycles = GetComponentLookup<GasSessionLifecycle>(),
                FaultLatches = GetComponentLookup<SessionFaultLatch>(),
                Inboxes = GetBufferLookup<BoundaryCommandInbox>(),
                FrozenPayloads = GetBufferLookup<BoundaryCommandFrozenPayload>(),
            }.Schedule(Dependency);
            handle = transfers.Dispose(handle);
            handle = acknowledged.Dispose(handle);
            Dependency = payloadBytes.Dispose(handle);
        }

        /// <summary>
        /// 在完整 batch fence 后向 Gate 确认本批次已成功消费的 RequestSequence。
        /// </summary>
        internal void AcknowledgeConsumedAfterBatch(EntityManager entityManager)
        {
            if (_deliveredRequestSequences.Count == 0)
                return;
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GasActiveSessionAuthority>(),
                ComponentType.ReadOnly<BoundaryCommandInbox>());
            if (query.CalculateEntityCount() != 1)
                return;

            var inbox = entityManager.GetBuffer<BoundaryCommandInbox>(query.GetSingletonEntity(), true);
            var consumed = CollectConsumedSequences(inbox);
            if (consumed.Length == 0)
                return;
            var result = _ingressGate.AcknowledgeConsumed(consumed);
            if (result.UnknownSequenceCount != 0)
                throw new InvalidOperationException("ECS inbox 确认了 Gate ledger 中不存在的 RequestSequence。");
            for (var index = 0; index < consumed.Length; index++)
                _acknowledgedForCompaction.Add(consumed[index]);
            RemoveDeliveredSequences(consumed);
        }

        /// <summary>
        /// 清除已由 FaultClose 统一终结的 delivered 跟踪，ECS audit record 仍保留在 inbox。
        /// </summary>
        internal void ForgetFaultTerminatedDeliveries()
        {
            _deliveredRequestSequences.Clear();
        }

        /// <summary>
        /// 把 managed records 深复制到两个 TempJob 容器，payload offset 只解释本 transfer 数组。
        /// </summary>
        private static void CreateTransferArrays(
            GasBoundaryJournalRecord[] records,
            out NativeArray<GasIngressTransferRecord> transfers,
            out NativeArray<byte> payloadBytes)
        {
            var totalPayloadLength = CalculatePayloadLength(records);
            transfers = new NativeArray<GasIngressTransferRecord>(
                records.Length,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            payloadBytes = new NativeArray<byte>(
                totalPayloadLength,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            var payloadOffset = 0;
            for (var index = 0; index < records.Length; index++)
            {
                var record = records[index];
                transfers[index] = CreateTransfer(record, payloadOffset);
                CopyManagedPayload(record.Payload.Span, payloadBytes, payloadOffset);
                payloadOffset += record.PayloadLength;
            }
        }

        /// <summary>
        /// 将 managed journal 的只读 payload 逐字节深复制到 TempJob transfer buffer。
        /// </summary>
        private static void CopyManagedPayload(
            ReadOnlySpan<byte> source,
            NativeArray<byte> destination,
            int destinationOffset)
        {
            for (var index = 0; index < source.Length; index++)
                destination[destinationOffset + index] = source[index];
        }

        /// <summary>
        /// 以 long 汇总冻结窗口 payload，拒绝无法由唯一 ECS byte buffer 表达的长度。
        /// </summary>
        private static int CalculatePayloadLength(GasBoundaryJournalRecord[] records)
        {
            var total = 0L;
            for (var index = 0; index < records.Length; index++)
                total += records[index].PayloadLength;
            if (total > int.MaxValue)
                throw new InvalidOperationException("Ingress window payload 超出 DynamicBuffer 可表达范围。");
            return (int)total;
        }

        /// <summary>
        /// 把单条 journal record 的全部稳定字段投影到 unmanaged transfer。
        /// </summary>
        private static GasIngressTransferRecord CreateTransfer(
            GasBoundaryJournalRecord record,
            int payloadOffset)
        {
            var semanticPhase = ExternalCommandSemanticPhaseOrdinal;
            var workClass = (ushort)record.Kind;
            return new GasIngressTransferRecord
            {
                SimulationEpoch = record.SimulationEpoch,
                RequestId = record.RequestId,
                RequestSequence = record.RequestSequence,
                SourceSequence = record.SourceSequence,
                AvailableTick = record.AvailableTick,
                HasSource = record.HasSource,
                BattleInstance = record.BattleInstance,
                SourceAsc = record.SourceAsc,
                Target = record.Target,
                SubjectHandle = record.Handle,
                DefinitionId = record.DefinitionId,
                CommandKind = record.Kind,
                SemanticPhaseOrdinal = semanticPhase,
                WorkClassOrdinal = workClass,
                PayloadSchemaVersion = record.PayloadSchemaVersion,
                PayloadKind = record.PayloadKind,
                PayloadOffset = payloadOffset,
                PayloadLength = record.PayloadLength,
                PayloadHash = record.PayloadHash,
                SemanticHash = ComputeSemanticHash(record, semanticPhase, workClass),
                CommandHash = record.CommandHash,
            };
        }

        /// <summary>
        /// 计算排除 RequestId/RequestSequence 的完整业务语义指纹，transport 到达顺序不得参与。
        /// </summary>
        private static ulong ComputeSemanticHash(
            GasBoundaryJournalRecord record,
            ushort semanticPhase,
            ushort workClass)
        {
            var hash = GasBoundaryFnv1A64.Create();
            hash.AddUInt64(record.SimulationEpoch);
            hash.AddUInt64(record.AvailableTick);
            AddOwner(ref hash, record.SourceAsc);
            hash.AddUInt16(semanticPhase);
            hash.AddUInt16(workClass);
            hash.AddUInt64(record.SourceSequence);
            hash.AddByte((byte)record.Kind);
            AddSemanticBody(ref hash, record);
            return hash.Value;
        }

        /// <summary>
        /// 将 owner stable identity 加入业务语义指纹。
        /// </summary>
        private static void AddOwner(ref GasBoundaryFnv1A64 hash, OwnerAscHandle owner)
        {
            hash.AddUInt64(owner.AscStableId);
            hash.AddUInt32(owner.AscGeneration);
        }

        /// <summary>
        /// 将目标、typed handle、定义和 payload 指纹加入业务语义哈希。
        /// </summary>
        private static void AddSemanticBody(
            ref GasBoundaryFnv1A64 hash,
            GasBoundaryJournalRecord record)
        {
            hash.AddByte(record.HasSource);
            hash.AddUInt64(record.BattleInstance.BattleStableId);
            hash.AddUInt32(record.BattleInstance.BattleGeneration);
            AddTarget(ref hash, record.Target);
            hash.AddUInt64(record.Handle.SimulationEpoch);
            AddOwner(ref hash, record.Handle.OwnerAsc);
            hash.AddInt32(record.Handle.SlotIndex);
            hash.AddUInt32(record.Handle.SlotGeneration);
            hash.AddByte((byte)record.Handle.Kind);
            hash.AddInt32(record.DefinitionId);
            hash.AddUInt16(record.PayloadSchemaVersion);
            hash.AddUInt16((ushort)record.PayloadKind);
            hash.AddInt32(record.PayloadLength);
            hash.AddUInt64(record.PayloadHash);
        }

        /// <summary>
        /// 将延迟目标引用的完整稳定字段加入业务语义哈希。
        /// </summary>
        private static void AddTarget(ref GasBoundaryFnv1A64 hash, BoundaryTargetRef target)
        {
            hash.AddByte((byte)target.Kind);
            hash.AddByte((byte)target.ResolutionPolicy);
            hash.AddUInt64(target.SimulationEpoch);
            hash.AddUInt64(target.BattleInstance.BattleStableId);
            hash.AddUInt32(target.BattleInstance.BattleGeneration);
            AddOwner(ref hash, target.TargetAsc);
            hash.AddUInt64(target.SelectorStableId);
            hash.AddInt32(target.DefinitionRuleIndex);
        }

        /// <summary>
        /// 记录已搬入 ECS 的 request sequence，直到成功 consume ack 或统一 FaultClose。
        /// </summary>
        private void TrackDeliveredSequences(GasBoundaryJournalRecord[] records)
        {
            for (var index = 0; index < records.Length; index++)
                _deliveredRequestSequences.Add(records[index].RequestSequence);
        }

        /// <summary>
        /// 将上一 outer fence 已确认的序号复制给下一 Ingress Job，并立刻切走本地窗口。
        /// </summary>
        private NativeArray<ulong> CreateAcknowledgedArray()
        {
            var values = new NativeArray<ulong>(
                _acknowledgedForCompaction.Count,
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            for (var index = 0; index < _acknowledgedForCompaction.Count; index++)
                values[index] = _acknowledgedForCompaction[index];
            _acknowledgedForCompaction.Clear();
            return values;
        }

        /// <summary>
        /// 从当前 inbox 收集既属于本 writer 又已进入 Consumed 终态的唯一序号。
        /// </summary>
        private ulong[] CollectConsumedSequences(DynamicBuffer<BoundaryCommandInbox> inbox)
        {
            var values = new List<ulong>();
            for (var index = 0; index < _deliveredRequestSequences.Count; index++)
            {
                var sequence = _deliveredRequestSequences[index];
                if (HasState(inbox, sequence, GasBoundaryCommandState.Consumed))
                    values.Add(sequence);
            }
            return values.ToArray();
        }

        /// <summary>
        /// 查询指定 RequestSequence 是否已处于目标 ECS inbox 状态。
        /// </summary>
        private static bool HasState(
            DynamicBuffer<BoundaryCommandInbox> inbox,
            ulong sequence,
            GasBoundaryCommandState state)
        {
            for (var index = 0; index < inbox.Length; index++)
            {
                if (inbox[index].RequestSequence == sequence && inbox[index].State == state)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 从本地 delivered 集移除已被 Gate 幂等确认的序号。
        /// </summary>
        private void RemoveDeliveredSequences(ulong[] consumed)
        {
            for (var index = 0; index < consumed.Length; index++)
                _deliveredRequestSequences.Remove(consumed[index]);
        }
    }

    /// <summary>
    /// 作为唯一 ECS writer 原子压缩已消费载体并追加完整冻结窗口。
    /// </summary>
    internal struct GasIngressAppendJob : IJob
    {
        public Entity Session;
        public ulong SimulationEpoch;
        public ulong CutoffRequestSequence;
        [ReadOnly] public NativeArray<GasIngressTransferRecord> Transfers;
        [ReadOnly] public NativeArray<byte> TransferPayload;
        [ReadOnly] public NativeArray<ulong> AcknowledgedSequences;
        [ReadOnly] public ComponentLookup<SimulationTickState> Ticks;
        public ComponentLookup<GasSessionLifecycle> Lifecycles;
        public ComponentLookup<SessionFaultLatch> FaultLatches;
        public BufferLookup<BoundaryCommandInbox> Inboxes;
        public BufferLookup<BoundaryCommandFrozenPayload> FrozenPayloads;

        /// <summary>
        /// 先完整验证容量/range，再执行只向左搬移的稳定压缩与原样 append。
        /// </summary>
        public void Execute()
        {
            var inbox = Inboxes[Session];
            var payload = FrozenPayloads[Session];
            if (!TryMeasureFinalLayout(inbox, payload, out var keptCount, out var keptPayloadLength,
                    out var failureReason))
            {
                MarkIngressFault(failureReason);
                return;
            }

            CompactExisting(ref inbox, ref payload, out var writtenCount, out var writtenPayloadLength);
            ResizeForAppend(ref inbox, ref payload, keptCount, keptPayloadLength);
            AppendTransfers(ref inbox, ref payload, writtenCount, writtenPayloadLength);
        }

        /// <summary>
        /// 验证所有现存 range 与状态并计算最终逻辑长度，任何错误都发生在首次写入前。
        /// </summary>
        private bool TryMeasureFinalLayout(
            DynamicBuffer<BoundaryCommandInbox> inbox,
            DynamicBuffer<BoundaryCommandFrozenPayload> payload,
            out int keptCount,
            out int keptPayloadLength,
            out GasTickAdmissionFailureReason failureReason)
        {
            keptCount = 0;
            keptPayloadLength = 0;
            var expectedPayloadOffset = 0;
            for (var index = 0; index < inbox.Length; index++)
            {
                var command = inbox[index];
                if (!HasValidRange(command, payload.Length) ||
                    command.FrozenPayloadOffset != expectedPayloadOffset ||
                    !IsKnownState(command.State))
                {
                    failureReason = GasTickAdmissionFailureReason.IngressPayloadRangeInvalid;
                    return false;
                }
                expectedPayloadOffset += command.FrozenPayloadLength;
                if (CanCompact(in command))
                    continue;
                keptCount++;
                keptPayloadLength += command.FrozenPayloadLength;
            }

            if (expectedPayloadOffset != payload.Length || !HasCanonicalTransferPayload())
            {
                failureReason = GasTickAdmissionFailureReason.IngressPayloadRangeInvalid;
                return false;
            }

            var finalCount = (long)keptCount + Transfers.Length;
            var finalPayload = (long)keptPayloadLength + TransferPayload.Length;
            if (finalCount > inbox.Capacity || finalPayload > payload.Capacity)
            {
                failureReason = GasTickAdmissionFailureReason.IngressPhysicalCapacityUnavailable;
                return false;
            }
            failureReason = GasTickAdmissionFailureReason.None;
            return true;
        }

        /// <summary>
        /// 校验 transfer payload ranges 按 record 顺序首尾相接并完整覆盖唯一 transfer byte array。
        /// </summary>
        private bool HasCanonicalTransferPayload()
        {
            var expectedOffset = 0;
            for (var index = 0; index < Transfers.Length; index++)
            {
                var transfer = Transfers[index];
                if (transfer.PayloadOffset != expectedOffset || transfer.PayloadLength < 0)
                    return false;
                expectedOffset += transfer.PayloadLength;
            }
            return expectedOffset == TransferPayload.Length;
        }

        /// <summary>
        /// 稳定保留非终态条目并向左复制 payload，使 entry 顺序和业务字段均不改变。
        /// </summary>
        private void CompactExisting(
            ref DynamicBuffer<BoundaryCommandInbox> inbox,
            ref DynamicBuffer<BoundaryCommandFrozenPayload> payload,
            out int writtenCount,
            out int writtenPayloadLength)
        {
            writtenCount = 0;
            writtenPayloadLength = 0;
            for (var index = 0; index < inbox.Length; index++)
            {
                var command = inbox[index];
                if (CanCompact(in command))
                    continue;
                CopyPayload(payload, command.FrozenPayloadOffset, writtenPayloadLength,
                    command.FrozenPayloadLength);
                command.FrozenPayloadOffset = writtenPayloadLength;
                inbox[writtenCount++] = command;
                writtenPayloadLength += command.FrozenPayloadLength;
            }
        }

        /// <summary>
        /// 在物理容量已证明后一次设置最终长度，禁止 Job 内隐式扩容。
        /// </summary>
        private void ResizeForAppend(
            ref DynamicBuffer<BoundaryCommandInbox> inbox,
            ref DynamicBuffer<BoundaryCommandFrozenPayload> payload,
            int keptCount,
            int keptPayloadLength)
        {
            inbox.ResizeUninitialized(keptCount + Transfers.Length);
            payload.ResizeUninitialized(keptPayloadLength + TransferPayload.Length);
        }

        /// <summary>
        /// 按 Gate RequestSequence 顺序写入完整 inbox record 与冻结 payload 字节。
        /// </summary>
        private void AppendTransfers(
            ref DynamicBuffer<BoundaryCommandInbox> inbox,
            ref DynamicBuffer<BoundaryCommandFrozenPayload> payload,
            int commandOffset,
            int payloadOffset)
        {
            for (var index = 0; index < Transfers.Length; index++)
            {
                var transfer = Transfers[index];
                CopyTransferPayload(payload, transfer, payloadOffset);
                inbox[commandOffset + index] = CreateInboxRecord(transfer, payloadOffset);
                payloadOffset += transfer.PayloadLength;
            }
        }

        /// <summary>
        /// 复制一条 transfer 的冻结 payload 到 Session 唯一 byte buffer。
        /// </summary>
        private void CopyTransferPayload(
            DynamicBuffer<BoundaryCommandFrozenPayload> payload,
            in GasIngressTransferRecord transfer,
            int destinationOffset)
        {
            for (var index = 0; index < transfer.PayloadLength; index++)
            {
                payload[destinationOffset + index] = new BoundaryCommandFrozenPayload
                {
                    Value = TransferPayload[transfer.PayloadOffset + index],
                };
            }
        }

        /// <summary>
        /// 从 transfer 创建唯一 Pending ECS 记录；managed Sealed 只表示已接受，不泄漏到 Tick seal 状态。
        /// </summary>
        private static BoundaryCommandInbox CreateInboxRecord(
            in GasIngressTransferRecord transfer,
            int payloadOffset)
        {
            return new BoundaryCommandInbox
            {
                SimulationEpoch = transfer.SimulationEpoch,
                RequestId = transfer.RequestId,
                RequestSequence = transfer.RequestSequence,
                SourceSequence = transfer.SourceSequence,
                HasSource = transfer.HasSource,
                SourceAsc = transfer.SourceAsc,
                BattleInstance = transfer.BattleInstance,
                Target = transfer.Target,
                SubjectHandle = transfer.SubjectHandle,
                DefinitionId = transfer.DefinitionId,
                AvailableTick = transfer.AvailableTick,
                CommandKind = transfer.CommandKind,
                SemanticPhaseOrdinal = transfer.SemanticPhaseOrdinal,
                WorkClassOrdinal = transfer.WorkClassOrdinal,
                PayloadSchemaVersion = transfer.PayloadSchemaVersion,
                PayloadKind = transfer.PayloadKind,
                FrozenPayloadOffset = payloadOffset,
                FrozenPayloadLength = transfer.PayloadLength,
                FrozenPayloadGeneration = 1,
                PayloadHash = transfer.PayloadHash,
                SemanticHash = transfer.SemanticHash,
                CommandHash = transfer.CommandHash,
                State = GasBoundaryCommandState.Pending,
            };
        }

        /// <summary>
        /// 对重叠安全的向左 payload 搬移逐元素复制，禁止创建临时容器。
        /// </summary>
        private static void CopyPayload(
            DynamicBuffer<BoundaryCommandFrozenPayload> payload,
            int sourceOffset,
            int destinationOffset,
            int length)
        {
            for (var index = 0; index < length; index++)
                payload[destinationOffset + index] = payload[sourceOffset + index];
        }

        /// <summary>
        /// 校验 payload range 非负且完全位于当前冻结 byte buffer。
        /// </summary>
        private static bool HasValidRange(in BoundaryCommandInbox command, int payloadLength)
        {
            var end = (long)command.FrozenPayloadOffset + command.FrozenPayloadLength;
            return command.FrozenPayloadOffset >= 0 && command.FrozenPayloadLength >= 0 &&
                   end <= payloadLength;
        }

        /// <summary>
        /// 判断 inbox 状态是否属于冻结四态闭世界。
        /// </summary>
        private static bool IsKnownState(GasBoundaryCommandState state)
        {
            return state == GasBoundaryCommandState.Pending || state == GasBoundaryCommandState.Sealed ||
                   state == GasBoundaryCommandState.Consumed ||
                   state == GasBoundaryCommandState.FaultTerminated;
        }

        /// <summary>
        /// 仅允许压缩已在上一 outer fence 向 Gate 成功确认的 Consumed 记录。
        /// </summary>
        private bool CanCompact(in BoundaryCommandInbox command)
        {
            if (command.State != GasBoundaryCommandState.Consumed)
                return false;
            for (var index = 0; index < AcknowledgedSequences.Length; index++)
            {
                if (AcknowledgedSequences[index] == command.RequestSequence)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 在 append 首次写入前锁存固定 ingress fault，使后续 Kernel gameplay mode 自动关闭。
        /// </summary>
        private void MarkIngressFault(GasTickAdmissionFailureReason reason)
        {
            if (FaultLatches[Session].Detected != 0)
                return;
            var tick = Ticks[Session].CurrentTick;
            var faultId = CreateFaultId(tick, reason);
            FaultLatches[Session] = new SessionFaultLatch
            {
                FaultId = faultId,
                FaultEpoch = SimulationEpoch,
                FaultTick = tick == ulong.MaxValue ? tick : tick + 1,
                ReasonCode = (int)reason,
                Detected = 1,
            };
            var lifecycle = Lifecycles[Session];
            lifecycle.State = GasSessionLifecycleState.Faulted;
            Lifecycles[Session] = lifecycle;
        }

        /// <summary>
        /// 由 Epoch、当前 Tick、失败原因与冻结 cutoff 生成非零稳定 fault 身份。
        /// </summary>
        private ulong CreateFaultId(ulong tick, GasTickAdmissionFailureReason reason)
        {
            var hash = GasBoundaryFnv1A64.Create();
            hash.AddUInt64(SimulationEpoch);
            hash.AddUInt64(tick);
            hash.AddInt32((int)reason);
            hash.AddUInt64(CutoffRequestSequence);
            return hash.Value == 0 ? 1UL : hash.Value;
        }
    }
}
