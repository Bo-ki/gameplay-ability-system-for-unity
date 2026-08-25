using System;
using GAS.Runtime;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 managed Drain 已实际驱动 ECS cleanup owner，并保持失败重试与空 shell合同。
    /// </summary>
    [TestFixture]
    public class RuntimeV1BoundaryDrainCoordinatorTests
    {
        private const ulong Epoch = 401;

        /// <summary>
        /// 验证 live ASC outbox 经 coordinator 接管后清 accepted prefix 并进入 Idle。
        /// </summary>
        [Test]
        public void Coordinator_真实Owner接管Fact并折回Idle()
        {
            using var world = new World("Runtime v1 drain coordinator live owner");
            var entity = world.EntityManager.CreateEntity(
                ComponentType.ReadWrite<GasAscIdentity>(),
                ComponentType.ReadWrite<BoundaryDrainState>(),
                ComponentType.ReadWrite<BoundaryFactBuffer>());
            var owner = new OwnerAscHandle(701, 2);
            world.EntityManager.SetComponentData(entity, new GasAscIdentity
            {
                SimulationEpoch = Epoch,
                OwnerAsc = owner,
            });
            var state = BoundaryDrainState.Create(
                Epoch,
                GasBoundaryOwnerKind.Asc,
                owner.AscStableId,
                owner.AscGeneration,
                1);
            var buffer = world.EntityManager.GetBuffer<BoundaryFactBuffer>(entity);
            Assert.That(GasBoundaryDrainProtocol.TryAppendFactWithSequence(
                ref state, buffer, CreateAscFact(owner), out _, out _), Is.True);
            world.EntityManager.SetComponentData(entity, state);

            var ring = new GasBoundaryDrainRing();
            var coordinator = new GasBoundaryDrainCoordinator(ring);
            Assert.That(coordinator.TryDrain(world.EntityManager, out var result), Is.True);

            Assert.That(result.AcceptedOwnerCount, Is.EqualTo(1));
            Assert.That(buffer.Length, Is.Zero);
            Assert.That(world.EntityManager.GetComponentData<BoundaryDrainState>(entity).Phase,
                Is.EqualTo(GasBoundaryDrainPhase.Idle));
            Assert.That(ring.Count, Is.EqualTo(1));
            Assert.That(ring.TryDequeue(out var batch), Is.True);
            Assert.That(batch.Facts.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// 验证 staging 失败保留源与 InFlight identity，成功重试复用同一 BatchId。
        /// </summary>
        [Test]
        public void Coordinator_Staging失败保留源并复用InFlightBatch()
        {
            using var world = new World("Runtime v1 drain coordinator retry");
            var entity = world.EntityManager.CreateEntity(
                ComponentType.ReadWrite<GasSessionIdentity>(),
                ComponentType.ReadWrite<BoundaryDrainState>(),
                ComponentType.ReadWrite<BoundaryFactBuffer>());
            world.EntityManager.SetComponentData(entity, new GasSessionIdentity
            {
                SimulationEpoch = Epoch,
            });
            var state = BoundaryDrainState.Create(
                Epoch, GasBoundaryOwnerKind.Session, Epoch, 1, 1);
            var buffer = world.EntityManager.GetBuffer<BoundaryFactBuffer>(entity);
            Assert.That(GasBoundaryDrainProtocol.TryAppendFactWithSequence(
                ref state, buffer, CreateSessionFact(), out _, out _), Is.True);
            world.EntityManager.SetComponentData(entity, state);
            var stager = new FailOnceStager();
            var coordinator = new GasBoundaryDrainCoordinator(stager, 77);

            Assert.That(coordinator.TryDrain(world.EntityManager, out var first), Is.False);
            var inFlight = world.EntityManager.GetComponentData<BoundaryDrainState>(entity);
            Assert.That(inFlight.Phase, Is.EqualTo(GasBoundaryDrainPhase.InFlight));
            Assert.That(inFlight.BatchId, Is.EqualTo(77));
            Assert.That(buffer.Length, Is.EqualTo(1));

            Assert.That(coordinator.TryDrain(world.EntityManager, out var second), Is.True);
            var accepted = world.EntityManager.GetComponentData<BoundaryDrainState>(entity);
            Assert.That(second.Failure, Is.EqualTo(GasBoundaryDrainFailure.None));
            Assert.That(accepted.Phase, Is.EqualTo(GasBoundaryDrainPhase.Idle));
            Assert.That(buffer.Length, Is.Zero);
            Assert.That(stager.LastBatchId, Is.EqualTo(77));
        }

        /// <summary>
        /// 验证销毁后的空 shell 必须先生成 NoFactReceipt，随后才移除 cleanup 载体。
        /// </summary>
        [Test]
        public void Coordinator_空CleanupShell生成NoFactReceipt后移除()
        {
            using var world = new World("Runtime v1 drain coordinator no fact");
            var entity = world.EntityManager.CreateEntity(
                ComponentType.ReadWrite<BoundaryDrainState>(),
                ComponentType.ReadWrite<BoundaryFactBuffer>());
            world.EntityManager.SetComponentData(entity,
                BoundaryDrainState.Create(
                    Epoch, GasBoundaryOwnerKind.Session, Epoch, 1, 1));
            var ring = new GasBoundaryDrainRing();
            var coordinator = new GasBoundaryDrainCoordinator(ring, 91);

            Assert.That(coordinator.TryDrain(world.EntityManager, out var result), Is.True);
            Assert.That(result.AcceptedOwnerCount, Is.EqualTo(1));
            Assert.That(world.EntityManager.Exists(entity), Is.True);
            Assert.That(coordinator.TryRemoveAcceptedShells(
                world.EntityManager, out var removedCount, out _), Is.True);
            Assert.That(removedCount, Is.EqualTo(1));
            Assert.That(world.EntityManager.Exists(entity), Is.False);
            Assert.That(ring.TryDequeue(out var batch), Is.True);
            Assert.That(batch.IsNoFact, Is.True);
            Assert.That(batch.Receipt.BatchId, Is.EqualTo(91));
        }

        /// <summary>
        /// 验证 owner final drain 成功后才释放 registration 与 cleanup shell。
        /// </summary>
        [Test]
        public void WorldOwner_FinalDrain成功后移除Shell()
        {
            using var world = new World("Runtime v1 owner final drain success");
            using var owner = GasRuntimeWorldOwner.Install(world);
            var shell = CreateSessionShell(world.EntityManager);

            owner.Dispose();

            Assert.That(world.EntityManager.Exists(shell), Is.False);
            Assert.That(owner.BoundaryDrainRing.Count, Is.EqualTo(1));
        }

        /// <summary>
        /// 验证 final staging 失败时 Dispose 抛出明确异常且 registration/owner 仍可重试。
        /// </summary>
        [Test]
        public void WorldOwner_FinalDrain失败不得进入Disposed()
        {
            using var world = new World("Runtime v1 owner final drain failure");
            var stager = new FailOnceStager();
            var owner = GasRuntimeWorldOwner.Install(world, stager);
            var shell = CreateSessionShell(world.EntityManager);

            Assert.Throws<InvalidOperationException>(() => owner.Dispose());
            Assert.That(world.EntityManager.Exists(shell), Is.True);
            Assert.That(owner.LastBoundaryDrainFailure,
                Is.EqualTo(GasBoundaryDrainFailure.StagingRejected));
            Assert.That(GasRuntimeWorldOwner.Install(world), Is.SameAs(owner));

            owner.Dispose();
            Assert.That(world.EntityManager.Exists(shell), Is.False);
        }

        /// <summary>
        /// 创建最小合法 ASC-scope fact。
        /// </summary>
        private static BoundaryFactBuffer CreateAscFact(in OwnerAscHandle owner)
        {
            return new BoundaryFactBuffer
            {
                Scope = GasBoundaryFactScope.Asc,
                ScopeStableId = owner.AscStableId,
                ScopeGeneration = owner.AscGeneration,
                Kind = GasBoundaryFactKind.AttributeChanged,
            };
        }

        /// <summary>
        /// 创建最小合法 Session-scope fact。
        /// </summary>
        private static BoundaryFactBuffer CreateSessionFact()
        {
            return new BoundaryFactBuffer
            {
                Scope = GasBoundaryFactScope.Session,
                ScopeStableId = Epoch,
                ScopeGeneration = 1,
                Kind = GasBoundaryFactKind.SessionLifecycle,
            };
        }

        /// <summary>
        /// 创建仅保留 Session Boundary cleanup 载体的 shell。
        /// </summary>
        private static Entity CreateSessionShell(EntityManager entityManager)
        {
            var entity = entityManager.CreateEntity(
                ComponentType.ReadWrite<BoundaryDrainState>(),
                ComponentType.ReadWrite<BoundaryFactBuffer>());
            var state = BoundaryDrainState.Create(
                Epoch, GasBoundaryOwnerKind.Session, Epoch, 1, 1);
            var buffer = entityManager.GetBuffer<BoundaryFactBuffer>(entity);
            Assert.That(GasBoundaryDrainProtocol.TryAppendFactWithSequence(
                ref state, buffer, CreateSessionFact(), out _, out _), Is.True);
            entityManager.SetComponentData(entity, state);
            return entity;
        }

        /// <summary>
        /// 第一次拒绝 staging，后续调用记录最后成功接管的 batch identity。
        /// </summary>
        private sealed class FailOnceStager : IGasBoundaryDrainStager
        {
            private bool _failed;

            public ulong LastBatchId { get; private set; }

            /// <summary>
            /// 模拟可恢复的 managed staging 故障。
            /// </summary>
            public bool TryStage(in GasBoundaryDrainBatch batch)
            {
                if (!_failed)
                {
                    _failed = true;
                    return false;
                }

                LastBatchId = batch.Receipt.BatchId;
                return true;
            }
        }
    }
}
