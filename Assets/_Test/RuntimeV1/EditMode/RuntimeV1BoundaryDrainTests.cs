using GAS.Runtime;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Runtime v1 scoped Boundary outbox 的唯一 owner、两阶段接管、重试与空 shell 协议。
    /// </summary>
    [TestFixture]
    public class RuntimeV1BoundaryDrainTests
    {
        private const ulong Epoch = 801;
        private static readonly OwnerAscHandle Asc = new OwnerAscHandle(1001, 3);
        private static readonly GasBoundaryOwnerKey AscOwner =
            GasBoundaryOwnerKey.ForAsc(Epoch, in Asc);
        private static readonly GasBoundaryOwnerKey SessionOwner =
            GasBoundaryOwnerKey.ForSession(Epoch, Epoch, 1);

        /// <summary>
        /// 验证 ASC scope 只写 ASC outbox，Battle/Session scope 只写 Session outbox。
        /// </summary>
        [Test]
        public void Scope路由_每条事实只有一个物理Owner()
        {
            using var world = new World("Runtime v1 boundary route test");
            var entityManager = world.EntityManager;
            var ascEntity = entityManager.CreateEntity(ComponentType.ReadWrite<BoundaryFactBuffer>());
            var sessionEntity = entityManager.CreateEntity(ComponentType.ReadWrite<BoundaryFactBuffer>());
            var ascBuffer = entityManager.GetBuffer<BoundaryFactBuffer>(ascEntity);
            var sessionBuffer = entityManager.GetBuffer<BoundaryFactBuffer>(sessionEntity);
            var ascState = BoundaryDrainState.Create(Epoch, GasBoundaryOwnerKind.Asc, 1001, 3, 1);
            var sessionState = BoundaryDrainState.Create(Epoch, GasBoundaryOwnerKind.Session, Epoch, 1, 1);

            var ascFact = CreateAscFact(1001, 3);
            Assert.That(GasBoundaryDrainProtocol.TryAppendFactWithSequence(
                ref ascState, ascBuffer, in ascFact, out var ascSequence, out var ascFailure), Is.True);
            Assert.That(ascFailure, Is.EqualTo(GasBoundaryDrainFailure.None));
            Assert.That(ascSequence, Is.EqualTo(1));
            Assert.That(GasBoundaryDrainProtocol.TryAppendFactWithSequence(
                ref sessionState, sessionBuffer, in ascFact, out _, out var wrongOwnerFailure), Is.False);
            Assert.That(wrongOwnerFailure, Is.EqualTo(GasBoundaryDrainFailure.FactScopeMismatch));

            var battleFact = CreateBattleFact(901, 2);
            Assert.That(GasBoundaryDrainProtocol.TryAppendFactWithSequence(
                ref sessionState, sessionBuffer, in battleFact, out var sessionSequence, out var sessionFailure),
                Is.True);
            Assert.That(sessionFailure, Is.EqualTo(GasBoundaryDrainFailure.None));
            Assert.That(sessionSequence, Is.EqualTo(1));
            Assert.That(GasBoundaryDrainProtocol.TryAppendFactWithSequence(
                ref ascState, ascBuffer, in battleFact, out _, out var wrongScopeFailure), Is.False);
            Assert.That(wrongScopeFailure, Is.EqualTo(GasBoundaryDrainFailure.FactScopeMismatch));
            Assert.That(ascBuffer.Length, Is.EqualTo(1));
            Assert.That(sessionBuffer.Length, Is.EqualTo(1));
        }

        /// <summary>
        /// 验证 staging 失败不清源，成功只清冻结 prefix，late tail 留待下一批。
        /// </summary>
        [Test]
        public void Drain接管_失败保留源记录且成功后保留LateTail()
        {
            using var world = new World("Runtime v1 boundary retry test");
            var entity = world.EntityManager.CreateEntity(ComponentType.ReadWrite<BoundaryFactBuffer>());
            var buffer = world.EntityManager.GetBuffer<BoundaryFactBuffer>(entity);
            var state = BoundaryDrainState.Create(Epoch, GasBoundaryOwnerKind.Asc, 1001, 3, 1);
            AppendAscFact(ref state, buffer, 1001);
            AppendAscFact(ref state, buffer, 1001);

            Assert.That(GasBoundaryDrainProtocol.TryFreeze(
                ref state, buffer, 700, false, out var receipt, out var freezeFailure), Is.True);
            Assert.That(freezeFailure, Is.EqualTo(GasBoundaryDrainFailure.None));
            Assert.That(state.Phase, Is.EqualTo(GasBoundaryDrainPhase.InFlight));
            Assert.That(buffer.Length, Is.EqualTo(2));

            var wrongReceipt = new GasBoundaryDrainReceiptForTest(receipt, 701).ToReceipt();
            Assert.That(GasBoundaryDrainProtocol.TryAccept(
                ref state, buffer, in wrongReceipt, out _, out var failedAcceptance), Is.False);
            Assert.That(failedAcceptance, Is.EqualTo(GasBoundaryDrainFailure.ReceiptMismatch));
            Assert.That(buffer.Length, Is.EqualTo(2));
            Assert.That(state.Phase, Is.EqualTo(GasBoundaryDrainPhase.InFlight));

            AppendAscFact(ref state, buffer, 1001);
            Assert.That(GasBoundaryDrainProtocol.TryFreeze(
                ref state, buffer, 700, false, out var retryReceipt, out var retryFailure), Is.True);
            Assert.That(retryFailure, Is.EqualTo(GasBoundaryDrainFailure.None));
            Assert.That(retryReceipt.BatchId, Is.EqualTo(receipt.BatchId));
            Assert.That(retryReceipt.InFlightWatermark, Is.EqualTo(receipt.InFlightWatermark));
            Assert.That(retryReceipt.FactCount, Is.EqualTo(2));

            Assert.That(GasBoundaryDrainProtocol.TryAccept(
                ref state, buffer, in retryReceipt, out var acceptance, out var acceptanceFailure), Is.True);
            Assert.That(acceptanceFailure, Is.EqualTo(GasBoundaryDrainFailure.None));
            Assert.That(acceptance.ClearedFactCount, Is.EqualTo(2));
            Assert.That(acceptance.RetainedLateTail, Is.True);
            Assert.That(state.Phase, Is.EqualTo(GasBoundaryDrainPhase.Pending));
            Assert.That(buffer.Length, Is.EqualTo(1));
            Assert.That(buffer[0].EventId.OwnerSequence, Is.EqualTo(3));

            Assert.That(GasBoundaryDrainProtocol.TryAccept(
                ref state, buffer, in retryReceipt, out _, out var staleFailure), Is.False);
            Assert.That(staleFailure, Is.EqualTo(GasBoundaryDrainFailure.ReceiptMismatch));

            Assert.That(GasBoundaryDrainProtocol.TryFreeze(
                ref state, buffer, 701, false, out var tailReceipt, out _), Is.True);
            Assert.That(GasBoundaryDrainProtocol.TryAccept(
                ref state, buffer, in tailReceipt, out var tailAcceptance, out _), Is.True);
            Assert.That(tailAcceptance.RetainedLateTail, Is.False);
            Assert.That(state.Phase, Is.EqualTo(GasBoundaryDrainPhase.Accepted));
            Assert.That(buffer.Length, Is.Zero);
            Assert.That(GasBoundaryDrainProtocol.TryFoldAcceptedToIdle(ref state, out _), Is.True);
            Assert.That(state.Phase, Is.EqualTo(GasBoundaryDrainPhase.Idle));
            Assert.That(state.NextOwnerSequence, Is.EqualTo(4));
        }

        /// <summary>
        /// 验证空 cleanup shell 只能通过显式 NoFactReceipt 进入 Accepted。
        /// </summary>
        [Test]
        public void 空CleanupShell_NoFactReceipt后才可折叠Idle()
        {
            using var world = new World("Runtime v1 no fact receipt test");
            var entity = world.EntityManager.CreateEntity(ComponentType.ReadWrite<BoundaryFactBuffer>());
            var buffer = world.EntityManager.GetBuffer<BoundaryFactBuffer>(entity);
            var state = BoundaryDrainState.Create(Epoch, GasBoundaryOwnerKind.Session, Epoch, 1, 8);

            Assert.That(GasBoundaryDrainProtocol.TryBeginNoFactDrain(
                ref state, buffer, 900, out var noFact, out var beginFailure), Is.True);
            Assert.That(beginFailure, Is.EqualTo(GasBoundaryDrainFailure.None));
            Assert.That(noFact.IsValid, Is.True);
            Assert.That(noFact.Receipt.IsNoFact, Is.True);
            Assert.That(noFact.Receipt.InFlightWatermark, Is.Zero);
            Assert.That(state.Phase, Is.EqualTo(GasBoundaryDrainPhase.InFlight));
            var noFactReceipt = noFact.Receipt;

            Assert.That(GasBoundaryDrainProtocol.TryAccept(
                ref state, buffer, in noFactReceipt, out var acceptance, out var acceptFailure), Is.True);
            Assert.That(acceptFailure, Is.EqualTo(GasBoundaryDrainFailure.None));
            Assert.That(acceptance.ClearedFactCount, Is.Zero);
            Assert.That(state.Phase, Is.EqualTo(GasBoundaryDrainPhase.Accepted));
            Assert.That(GasBoundaryDrainProtocol.TryAccept(
                ref state, buffer, in noFactReceipt, out var duplicate, out _), Is.True);
            Assert.That(duplicate.AlreadyAccepted, Is.True);
            Assert.That(GasBoundaryDrainProtocol.TryFoldAcceptedToIdle(ref state, out _), Is.True);
            Assert.That(state.NextOwnerSequence, Is.EqualTo(8));
        }

        /// <summary>
        /// 验证物理 owner key 同时包含 Epoch、类型、稳定编号和代际，避免跨生命期碰撞。
        /// </summary>
        [Test]
        public void OwnerKey_完整身份参与相等性()
        {
            Assert.That(AscOwner, Is.EqualTo(new GasBoundaryOwnerKey(Epoch,
                GasBoundaryOwnerKind.Asc, 1001, 3)));
            Assert.That(AscOwner, Is.Not.EqualTo(new GasBoundaryOwnerKey(Epoch,
                GasBoundaryOwnerKind.Asc, 1001, 4)));
            Assert.That(AscOwner, Is.Not.EqualTo(SessionOwner));
            Assert.That(AscOwner.IsValid, Is.True);
        }

        /// <summary>
        /// 验证冻结 prefix 遇到序号缺口时拒绝接管，避免清理非连续事实造成不可审计丢失。
        /// </summary>
        [Test]
        public void Drain接管_序号缺口拒绝冻结()
        {
            using var world = new World("Runtime v1 boundary gap test");
            var entity = world.EntityManager.CreateEntity(ComponentType.ReadWrite<BoundaryFactBuffer>());
            var buffer = world.EntityManager.GetBuffer<BoundaryFactBuffer>(entity);
            var state = BoundaryDrainState.Create(Epoch, GasBoundaryOwnerKind.Asc, 1001, 3, 1);
            AppendAscFact(ref state, buffer, 1001);
            var gap = CreateAscFact(1001, 3);
            gap.EventId.SimulationEpoch = Epoch;
            gap.EventId.OwnerKind = GasBoundaryOwnerKind.Asc;
            gap.EventId.OwnerStableId = 1001;
            gap.EventId.OwnerGeneration = 3;
            gap.EventId.OwnerSequence = 3;
            buffer.Add(gap);

            Assert.That(GasBoundaryDrainProtocol.TryFreeze(
                ref state, buffer, 902, false, out _, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(GasBoundaryDrainFailure.FactSequenceMismatch));
            Assert.That(state.Phase, Is.EqualTo(GasBoundaryDrainPhase.Pending));
            Assert.That(buffer.Length, Is.EqualTo(2));
        }

        /// <summary>
        /// 创建一条 ASC-scope 的最小自包含事实。
        /// </summary>
        private static BoundaryFactBuffer CreateAscFact(ulong ownerStableId, uint ownerGeneration)
        {
            return new BoundaryFactBuffer
            {
                Scope = GasBoundaryFactScope.Asc,
                ScopeStableId = ownerStableId,
                ScopeGeneration = ownerGeneration,
                Kind = GasBoundaryFactKind.AttributeChanged,
                Plane = GasBoundaryFactPlane.Gameplay,
                SimulationTick = 1,
                SemanticId = 101,
            };
        }

        /// <summary>
        /// 创建一条 Battle-scope 的最小自包含事实。
        /// </summary>
        private static BoundaryFactBuffer CreateBattleFact(ulong battleStableId, uint battleGeneration)
        {
            return new BoundaryFactBuffer
            {
                Scope = GasBoundaryFactScope.BattleInstance,
                ScopeStableId = battleStableId,
                ScopeGeneration = battleGeneration,
                BattleInstanceId = battleStableId,
                BattleInstanceGeneration = battleGeneration,
                Kind = GasBoundaryFactKind.BattleOutcome,
                Plane = GasBoundaryFactPlane.Gameplay,
                SimulationTick = 1,
                SemanticId = 202,
            };
        }

        /// <summary>
        /// 追加一条 ASC fact 并断言 OwnerSequence 分配成功，缩短状态机测试样板。
        /// </summary>
        private static void AppendAscFact(
            ref BoundaryDrainState state,
            DynamicBuffer<BoundaryFactBuffer> buffer,
            ulong ownerStableId)
        {
            var fact = CreateAscFact(ownerStableId, 3);
            Assert.That(GasBoundaryDrainProtocol.TryAppendFactWithSequence(
                ref state, buffer, in fact, out _, out var failure), Is.True);
            Assert.That(failure, Is.EqualTo(GasBoundaryDrainFailure.None));
        }

        /// <summary>
        /// 在测试内部复制 receipt 并替换 BatchId，以验证 staging identity 不可伪造。
        /// </summary>
        private readonly struct GasBoundaryDrainReceiptForTest
        {
            private readonly GasBoundaryDrainReceipt _source;
            private readonly ulong _batchId;

            /// <summary>
            /// 创建一个只改变 BatchId 的测试 receipt。
            /// </summary>
            public GasBoundaryDrainReceiptForTest(
                in GasBoundaryDrainReceipt source,
                ulong batchId)
            {
                _source = source;
                _batchId = batchId;
            }

            /// <summary>
            /// 返回替换 BatchId 后的 receipt；生产代码不能绕过 internal 构造器伪造它。
            /// </summary>
            public GasBoundaryDrainReceipt ToReceipt()
            {
                return new GasBoundaryDrainReceiptForReflection(
                    _source,
                    _batchId).ToReceipt();
            }
        }

        /// <summary>
        /// 通过 runtime assembly 的友元可见构造器生成测试用 receipt 变体。
        /// </summary>
        private readonly struct GasBoundaryDrainReceiptForReflection
        {
            private readonly GasBoundaryDrainReceipt _source;
            private readonly ulong _batchId;

            /// <summary>
            /// 创建 receipt 变体。
            /// </summary>
            public GasBoundaryDrainReceiptForReflection(
                in GasBoundaryDrainReceipt source,
                ulong batchId)
            {
                _source = source;
                _batchId = batchId;
            }

            /// <summary>
            /// 返回测试使用的替代 receipt。
            /// </summary>
            public GasBoundaryDrainReceipt ToReceipt()
            {
                return new GasBoundaryDrainReceipt(
                    _source.Owner,
                    _batchId,
                    _source.Kind,
                    _source.FirstOwnerSequence,
                    _source.LastOwnerSequence,
                    _source.InFlightWatermark,
                    _source.FactCount);
            }
        }
    }
}
