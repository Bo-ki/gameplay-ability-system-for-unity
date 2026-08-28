using System;
using GAS.Runtime;
using NUnit.Framework;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Runtime v1 唯一 managed ingress Gate 的复制、序号、窗口、确认与 Fault 终态契约。
    /// </summary>
    [TestFixture]
    public class RuntimeV1IngressGateTests
    {
        private const ulong SimulationEpoch = 101;
        private const ushort PayloadSchemaVersion = 1;
        private static readonly BattleInstanceHandle Battle =
            new BattleInstanceHandle(SimulationEpoch, 301, 2);
        private static readonly OwnerAscHandle SourceAsc =
            new OwnerAscHandle(501, 3);

        /// <summary>
        /// 验证权限输入数组与命令 payload 均在进入持久事实源时完成深复制。
        /// </summary>
        [Test]
        public void 权限快照与Payload_接受时完成深复制()
        {
            var battles = new[]
            {
                new GasIngressBattleAuthority(Battle, GasBattleInstanceState.Running, true),
            };
            var memberships = new[]
            {
                new GasIngressAscAuthority(SourceAsc, Battle, GasAscRegistryState.Ready),
            };
            var snapshot = new GasIngressAuthoritySnapshot(
                SimulationEpoch,
                4,
                16,
                battles,
                memberships);
            battles[0] = default;
            memberships[0] = default;

            var gate = new SessionIngressGate();
            Assert.That(gate.ReplaceAuthoritySnapshot(snapshot), Is.True);
            var port = new GasCommandPort(gate);
            var payload = new byte[] { 11, 22, 33 };
            var accepted = RequestActivate(port, 1, payload);
            payload[0] = 99;
            payload[1] = 88;

            Assert.That(accepted.Status, Is.EqualTo(GasCommandAcceptStatus.Accepted));
            Assert.That(gate.TryFreezeIngressWindow(out var records), Is.True);
            Assert.That(records, Has.Length.EqualTo(1));
            Assert.That(records[0].Payload.ToArray(), Is.EqualTo(new byte[] { 11, 22, 33 }));
        }

        /// <summary>
        /// 验证 exact duplicate 复用原序号，冲突、非法与两类容量拒绝均不产生新接受序号。
        /// </summary>
        [Test]
        public void 重复冲突非法与容量拒绝_仅首次接受推进序号()
        {
            var gate = CreateGate(2, 4);
            var port = new GasCommandPort(gate);
            var firstPayload = new byte[] { 4, 5 };

            var first = RequestActivate(port, 10, firstPayload, 7, 3);
            var duplicate = RequestActivate(port, 10, firstPayload, 7, 3);
            var conflict = RequestActivate(port, 10, new byte[] { 4, 6 }, 7, 3);
            var invalid = RequestActivate(port, 0, Array.Empty<byte>());
            var payloadRejected = RequestActivate(port, 11, new byte[] { 1, 2, 3 });
            var second = RequestActivate(port, 12, new byte[] { 8, 9 });
            var countRejected = RequestActivate(port, 13, Array.Empty<byte>());

            Assert.That(first.Status, Is.EqualTo(GasCommandAcceptStatus.Accepted));
            Assert.That(first.RequestSequence, Is.EqualTo(1));
            Assert.That(duplicate.Status, Is.EqualTo(GasCommandAcceptStatus.DuplicateAccepted));
            Assert.That(duplicate.RequestSequence, Is.EqualTo(first.RequestSequence));
            AssertRejected(conflict, GasCommandAcceptStatus.RequestIdConflict);
            AssertRejected(invalid, GasCommandAcceptStatus.InvalidRequest);
            AssertRejected(payloadRejected, GasCommandAcceptStatus.IngressPayloadBytesExceeded);
            Assert.That(second.RequestSequence, Is.EqualTo(2));
            AssertRejected(countRejected, GasCommandAcceptStatus.IngressCountExceeded);

            var receipt = gate.CloseForFault(7001);
            Assert.That(receipt.AcceptedHighWatermark, Is.EqualTo(2));
            Assert.That(receipt.OutstandingCount, Is.EqualTo(2));
        }

        /// <summary>
        /// 验证 RequestId 与 RequestSequence 仅用于传输审计，不能覆盖 SourceSequence 的 gameplay 顺序。
        /// </summary>
        [Test]
        public void Gameplay排序_只跟随SourceSequence而忽略相反的Request序号()
        {
            var sourceFirst = CreateSealedCommand(90, 90, 3);
            var requestFirst = CreateSealedCommand(1, 1, 4);
            var comparer = new GasSealedCommandComparer();

            Assert.That(comparer.Compare(sourceFirst, requestFirst), Is.LessThan(0));
            Assert.That(comparer.Compare(requestFirst, sourceFirst), Is.GreaterThan(0));
        }

        /// <summary>
        /// 验证零容量是有效的冻结配置，首条容量拒绝不会推进 accepted high watermark。
        /// </summary>
        [Test]
        public void 零容量快照_保持可配置且拒绝不推进HighWatermark()
        {
            var gate = CreateGate(0, 0);
            var port = new GasCommandPort(gate);

            var rejected = RequestActivate(port, 20, Array.Empty<byte>());
            var receipt = gate.CloseForFault(7002);

            AssertRejected(rejected, GasCommandAcceptStatus.IngressCountExceeded);
            Assert.That(receipt.AcceptedHighWatermark, Is.Zero);
            Assert.That(receipt.OutstandingCount, Is.Zero);
        }

        /// <summary>
        /// 验证 Freeze 固定当前 cutoff，锁释放后接受的记录只进入下一次 tail window。
        /// </summary>
        [Test]
        public void Freeze窗口_固定Cutoff并把后续接受留在Tail()
        {
            var gate = CreateGate();
            var port = new GasCommandPort(gate);
            var first = RequestActivate(port, 21, Array.Empty<byte>());
            var second = RequestActivate(port, 22, Array.Empty<byte>());

            Assert.That(gate.TryFreezeIngressWindow(out var firstWindow, out var firstCutoff), Is.True);
            Assert.That(firstCutoff, Is.EqualTo(second.RequestSequence));
            Assert.That(GetRequestSequences(firstWindow), Is.EqualTo(new[]
            {
                first.RequestSequence,
                second.RequestSequence,
            }));

            var tail = RequestActivate(port, 23, Array.Empty<byte>());
            Assert.That(gate.TryFreezeIngressWindow(out var tailWindow, out var tailCutoff), Is.True);
            Assert.That(GetRequestSequences(tailWindow), Is.EqualTo(new[] { tail.RequestSequence }));
            Assert.That(tailCutoff, Is.EqualTo(tail.RequestSequence));
            Assert.That(gate.TryFreezeIngressWindow(out var emptyWindow, out var emptyCutoff), Is.False);
            Assert.That(emptyWindow, Is.Empty);
            Assert.That(emptyCutoff, Is.Zero);
        }

        /// <summary>
        /// 验证消费确认分别统计首次推进、已终态重复与未知序号，并更新对应 journal 状态。
        /// </summary>
        [Test]
        public void 消费确认_区分首次终态与未知序号()
        {
            var gate = CreateGate();
            var port = new GasCommandPort(gate);
            var first = RequestActivate(port, 31, Array.Empty<byte>());
            var second = RequestActivate(port, 32, Array.Empty<byte>());
            RequestActivate(port, 33, Array.Empty<byte>());
            Assert.That(gate.TryFreezeIngressWindow(out var records), Is.True);

            var result = gate.AcknowledgeConsumed(new[]
            {
                first.RequestSequence,
                first.RequestSequence,
                ulong.MaxValue,
                second.RequestSequence,
            });

            Assert.That(result.ConsumedCount, Is.EqualTo(2));
            Assert.That(result.AlreadyTerminalCount, Is.EqualTo(1));
            Assert.That(result.UnknownSequenceCount, Is.EqualTo(1));
            Assert.That(records[0].State, Is.EqualTo(GasBoundaryCommandState.Consumed));
            Assert.That(records[1].State, Is.EqualTo(GasBoundaryCommandState.Consumed));
            Assert.That(records[2].State, Is.EqualTo(GasBoundaryCommandState.Sealed));
        }

        /// <summary>
        /// 验证首次消费确认释放 outstanding 条数与 payload 容量，同时保留历史 RequestId 的去重序号。
        /// </summary>
        [Test]
        public void 消费确认_释放Outstanding容量但保留终身去重Ledger()
        {
            var gate = CreateGate(1, 2);
            var port = new GasCommandPort(gate);
            var first = RequestActivate(port, 34, new byte[] { 7, 8 });
            Assert.That(gate.TryFreezeIngressWindow(out _), Is.True);

            var acknowledged = gate.AcknowledgeConsumed(new[] { first.RequestSequence });
            var second = RequestActivate(port, 35, new byte[] { 9, 10 });
            var duplicate = RequestActivate(port, 34, new byte[] { 7, 8 });

            Assert.That(acknowledged.ConsumedCount, Is.EqualTo(1));
            Assert.That(second.Status, Is.EqualTo(GasCommandAcceptStatus.Accepted));
            Assert.That(second.RequestSequence, Is.EqualTo(first.RequestSequence + 1));
            Assert.That(duplicate.Status, Is.EqualTo(GasCommandAcceptStatus.DuplicateAccepted));
            Assert.That(duplicate.RequestSequence, Is.EqualTo(first.RequestSequence));
        }

        /// <summary>
        /// 验证 FaultClose 仅汇总 accepted-outstanding，冻结审计值并永久同步拒绝新请求。
        /// </summary>
        [Test]
        public void FaultClose_冻结Outstanding审计并永久拒绝新请求()
        {
            const ulong faultId = 9001;
            var gate = CreateGate();
            var port = new GasCommandPort(gate);
            var first = RequestActivate(port, 41, Array.Empty<byte>());
            RequestActivate(port, 42, new byte[] { 1 });
            RequestActivate(port, 43, new byte[] { 2, 3 });
            Assert.That(gate.TryFreezeIngressWindow(out var records), Is.True);
            gate.AcknowledgeConsumed(new[] { first.RequestSequence });
            var expectedHash = ComputeOutstandingHash(records[1], records[2]);

            var receipt = gate.CloseForFault(faultId);

            Assert.That(receipt.FaultId, Is.EqualTo(faultId));
            Assert.That(receipt.FirstRequestSequence, Is.EqualTo(2));
            Assert.That(receipt.LastRequestSequence, Is.EqualTo(3));
            Assert.That(receipt.OutstandingCount, Is.EqualTo(2));
            Assert.That(receipt.OutstandingFnv1A64Hash, Is.EqualTo(expectedHash));
            Assert.That(receipt.AcceptedHighWatermark, Is.EqualTo(3));
            Assert.That(records[0].State, Is.EqualTo(GasBoundaryCommandState.Consumed));
            Assert.That(records[1].State, Is.EqualTo(GasBoundaryCommandState.FaultTerminated));
            Assert.That(records[2].State, Is.EqualTo(GasBoundaryCommandState.FaultTerminated));

            var repeated = gate.CloseForFault(faultId + 1);
            Assert.That(repeated.FaultId, Is.EqualTo(faultId));
            Assert.That(repeated.OutstandingFnv1A64Hash, Is.EqualTo(expectedHash));
            var rejected = RequestActivate(port, 44, Array.Empty<byte>());
            AssertRejected(rejected, GasCommandAcceptStatus.FaultClosed);
        }

        /// <summary>
        /// 创建绑定当前测试战局与来源 ASC 的开放 Gate。
        /// </summary>
        private static SessionIngressGate CreateGate(
            int maxIngressCommandCount = 16,
            long maxIngressPayloadBytes = 128)
        {
            var gate = new SessionIngressGate();
            var snapshot = new GasIngressAuthoritySnapshot(
                SimulationEpoch,
                maxIngressCommandCount,
                maxIngressPayloadBytes,
                new[]
                {
                    new GasIngressBattleAuthority(Battle, GasBattleInstanceState.Running, true),
                },
                new[]
                {
                    new GasIngressAscAuthority(SourceAsc, Battle, GasAscRegistryState.Ready),
                });
            Assert.That(gate.ReplaceAuthoritySnapshot(snapshot), Is.True);
            return gate;
        }

        /// <summary>
        /// 通过唯一 typed Port 提交一个规范 Activate 请求，并按 payload 有无选择唯一合法描述。
        /// </summary>
        private static GasCommandAcceptResult RequestActivate(
            GasCommandPort port,
            ulong requestId,
            byte[] payload,
            ulong sourceSequence = 1,
            ulong availableTick = 0)
        {
            var context = new GasBoundaryCommandContext(
                SimulationEpoch,
                requestId,
                sourceSequence,
                availableTick,
                true,
                Battle,
                SourceAsc,
                BoundaryTargetRef.None);
            var ability = new GrantedAbilityHandle(SimulationEpoch, SourceAsc, 2, 5);
            var descriptor = payload.Length == 0
                ? BoundaryCommandPayloadDescriptor.None
                : new BoundaryCommandPayloadDescriptor(
                    PayloadSchemaVersion,
                    GasBoundaryCommandPayloadKind.AbilityRequest);
            return port.RequestActivate(in context, in ability, in descriptor, payload);
        }

        /// <summary>
        /// 断言拒绝结果既返回精确状态，也绝不分配 RequestSequence。
        /// </summary>
        private static void AssertRejected(
            GasCommandAcceptResult result,
            GasCommandAcceptStatus expectedStatus)
        {
            Assert.That(result.Status, Is.EqualTo(expectedStatus));
            Assert.That(result.RequestSequence, Is.Zero);
            Assert.That(result.IsAccepted, Is.False);
        }

        /// <summary>
        /// 提取冻结窗口中的接受序号，便于直接断言其边界与顺序。
        /// </summary>
        private static ulong[] GetRequestSequences(GasBoundaryJournalRecord[] records)
        {
            var sequences = new ulong[records.Length];
            for (var index = 0; index < records.Length; index++)
                sequences[index] = records[index].RequestSequence;
            return sequences;
        }

        /// <summary>
        /// 创建除传输序号与 SourceSequence 外 key 完全相同的 sealed command。
        /// </summary>
        private static GasSealedCommand CreateSealedCommand(
            ulong requestId,
            ulong requestSequence,
            ulong sourceSequence)
        {
            return new GasSealedCommand
            {
                InboxIndex = 0,
                Command = new BoundaryCommandInbox
                {
                    SimulationEpoch = SimulationEpoch,
                    RequestId = requestId,
                    RequestSequence = requestSequence,
                    SourceSequence = sourceSequence,
                    SourceAsc = SourceAsc,
                    AvailableTick = 7,
                    SemanticPhaseOrdinal = 2,
                    WorkClassOrdinal = 5,
                    CommandKind = GasBoundaryCommandKind.Activate,
                },
            };
        }

        /// <summary>
        /// 按 Gate 冻结规范计算 outstanding 的序号与命令哈希摘要。
        /// </summary>
        private static ulong ComputeOutstandingHash(params GasBoundaryJournalRecord[] records)
        {
            var hash = GasBoundaryFnv1A64.Create();
            for (var index = 0; index < records.Length; index++)
            {
                hash.AddUInt64(records[index].RequestSequence);
                hash.AddUInt64(records[index].CommandHash);
            }

            return hash.Value;
        }
    }
}
