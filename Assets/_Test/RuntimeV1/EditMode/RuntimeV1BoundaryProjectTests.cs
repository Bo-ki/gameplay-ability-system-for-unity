using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 BoundaryProject 预检在重复或非法 CoreFact 下保持 outbox 与 DrainState 零写。
    /// </summary>
    [TestFixture]
    public partial class RuntimeV1BoundaryProjectTests
    {
        private const ulong Epoch = 901;
        private static readonly OwnerAscHandle Owner = new OwnerAscHandle(3001, 2);
        private static readonly BattleInstanceHandle Battle =
            new BattleInstanceHandle(Epoch, 7001, 1);

        /// <summary>
        /// 非法后置 scope 必须在任何 append 前被预检拒绝。
        /// </summary>
        [Test]
        public void BoundaryProject_后置非法Fact预检零部分写()
        {
            using var world = new World("Runtime v1 boundary project preflight");
            var fixture = CreateFixture(world);
            var facts = new NativeArray<GasCoreFactRecord>(2, Allocator.TempJob);
            var admission = new NativeArray<GasAdmissionResult>(1, Allocator.TempJob);
            var execution = new NativeArray<GasTickExecutionState>(1, Allocator.TempJob);
            try
            {
                facts[0] = CreateAscFact(10, 0);
                var invalidFact = facts[0];
                invalidFact.Scope = GasBoundaryFactScope.BattleInstance;
                facts[1] = invalidFact;
                admission[0] = new GasAdmissionResult { Succeeded = 1 };
                execution[0] = new GasTickExecutionState
                {
                    GameplayEnabled = 1,
                    CoreFactCount = 2,
                };
                RunProject(world, fixture, facts, admission, execution);

                Assert.That(execution[0].PostAdmissionFailure,
                    Is.EqualTo(GasTickAdmissionFailureReason.BoundaryProjectionFailure));
                Assert.That(execution[0].BoundaryFactCount, Is.Zero);
                Assert.That(world.EntityManager.GetBuffer<BoundaryFactBuffer>(fixture.Asc).Length,
                    Is.Zero);
                Assert.That(world.EntityManager.GetComponentData<BoundaryDrainState>(fixture.Asc),
                    Is.EqualTo(fixture.AscState));
            }
            finally
            {
                facts.Dispose();
                admission.Dispose();
                execution.Dispose();
            }
        }

        /// <summary>
        /// 相同 canonical key 的 CoreFact 必须被拒绝且不得产生重复物理事实。
        /// </summary>
        [Test]
        public void BoundaryProject_重复CanonicalKey零写()
        {
            using var world = new World("Runtime v1 boundary project duplicate");
            var fixture = CreateFixture(world);
            var facts = new NativeArray<GasCoreFactRecord>(2, Allocator.TempJob);
            var admission = new NativeArray<GasAdmissionResult>(1, Allocator.TempJob);
            var execution = new NativeArray<GasTickExecutionState>(1, Allocator.TempJob);
            try
            {
                facts[0] = CreateAscFact(10, 0);
                facts[1] = facts[0];
                admission[0] = new GasAdmissionResult { Succeeded = 1 };
                execution[0] = new GasTickExecutionState
                {
                    GameplayEnabled = 1,
                    CoreFactCount = 2,
                };
                RunProject(world, fixture, facts, admission, execution);

                Assert.That(execution[0].PostAdmissionFailure,
                    Is.EqualTo(GasTickAdmissionFailureReason.BoundaryProjectionFailure));
                Assert.That(execution[0].BoundaryFactCount, Is.Zero);
                Assert.That(world.EntityManager.GetBuffer<BoundaryFactBuffer>(fixture.Asc).Length,
                    Is.Zero);
                Assert.That(world.EntityManager.GetComponentData<BoundaryDrainState>(fixture.Asc),
                    Is.EqualTo(fixture.AscState));
            }
            finally
            {
                facts.Dispose();
                admission.Dispose();
                execution.Dispose();
            }
        }

        /// <summary>
        /// 创建包含 Session registry、ASC identity、membership 与预留 outbox 的最小 Job fixture。
        /// </summary>
        private static BoundaryProjectFixture CreateFixture(World world)
        {
            var manager = world.EntityManager;
            var session = manager.CreateEntity(
                ComponentType.ReadWrite<AscRegistrySlot>(),
                ComponentType.ReadWrite<BoundaryDrainState>(),
                ComponentType.ReadWrite<BoundaryFactBuffer>());
            var asc = manager.CreateEntity(
                ComponentType.ReadWrite<GasAscIdentity>(),
                ComponentType.ReadWrite<AscLifecycle>(),
                ComponentType.ReadWrite<AscBattleMembership>(),
                ComponentType.ReadWrite<BoundaryDrainState>(),
                ComponentType.ReadWrite<BoundaryFactBuffer>());
            manager.SetComponentData(session,
                BoundaryDrainState.Create(Epoch, GasBoundaryOwnerKind.Session, Epoch, 1, 1));
            var sessionFacts = manager.GetBuffer<BoundaryFactBuffer>(session);
            sessionFacts.EnsureCapacity(4);
            var ascState = BoundaryDrainState.Create(
                Epoch, GasBoundaryOwnerKind.Asc, Owner.AscStableId, Owner.AscGeneration, 1);
            manager.SetComponentData(asc, ascState);
            manager.SetComponentData(asc, new GasAscIdentity
            {
                SimulationEpoch = Epoch,
                OwnerAsc = Owner,
            });
            manager.SetComponentData(asc, new AscLifecycle
            {
                State = GasAscLifecycleState.Alive,
            });
            manager.SetComponentData(asc, new AscBattleMembership
            {
                BattleInstance = Battle,
                ScenarioUnitId = 8001,
            });
            var ascFacts = manager.GetBuffer<BoundaryFactBuffer>(asc);
            ascFacts.EnsureCapacity(4);
            var registry = manager.GetBuffer<AscRegistrySlot>(session);
            var slot = new AscRegistrySlot
            {
                Header = GasSlabSlotHeader.CreateLive(1),
                OwnerAsc = Owner,
                BattleInstance = Battle,
                RegistryOrdinal = 0,
                State = GasAscRegistryState.Ready,
            };
            slot.SetRuntimeEntity(asc);
            registry.Add(slot);
            return new BoundaryProjectFixture(session, asc, ascState);
        }

        /// <summary>
        /// 通过临时 SystemBase 刷新安全 lookup 后同步运行 BoundaryProject Job。
        /// </summary>
        private static void RunProject(
            World world,
            in BoundaryProjectFixture fixture,
            NativeArray<GasCoreFactRecord> facts,
            NativeArray<GasAdmissionResult> admission,
            NativeArray<GasTickExecutionState> execution)
        {
            var lookupSystem = world.CreateSystemManaged<BoundaryProjectLookupSystem>();
            lookupSystem.Update();
            new GasBoundaryProjectJob
            {
                Session = fixture.Session,
                SimulationEpoch = Epoch,
                CoreFacts = facts,
                Admission = admission,
                Registries = lookupSystem.Registries,
                AscIdentities = lookupSystem.AscIdentities,
                AscLifecycles = lookupSystem.AscLifecycles,
                Memberships = lookupSystem.Memberships,
                Drains = lookupSystem.Drains,
                BoundaryFacts = lookupSystem.BoundaryFacts,
                Execution = execution,
            }.Run();
        }

        /// <summary>
        /// 创建一个按 StableFactMerge canonical 顺序可投影的 ASC CoreFact。
        /// </summary>
        private static GasCoreFactRecord CreateAscFact(int phase, int factOrdinal)
        {
            return new GasCoreFactRecord
            {
                Scope = GasBoundaryFactScope.Asc,
                Plane = GasBoundaryFactPlane.Gameplay,
                Kind = GasBoundaryFactKind.AttributeChanged,
                SourceAsc = Owner,
                TargetAsc = Owner,
                SimulationTick = 1,
                SemanticPhaseOrdinal = (ushort)phase,
                WorkClassOrdinal = 1,
                SemanticId = 100 + (ulong)phase,
                OperationOrdinal = 0,
                FactOrdinal = factOrdinal,
            };
        }

        /// <summary>
        /// 保存测试实体与其预检前 DrainState 快照。
        /// </summary>
        private readonly struct BoundaryProjectFixture
        {
            public readonly Entity Session;
            public readonly Entity Asc;
            public readonly BoundaryDrainState AscState;

            /// <summary>
            /// 创建测试 fixture 描述。
            /// </summary>
            public BoundaryProjectFixture(
                Entity session,
                Entity asc,
                in BoundaryDrainState ascState)
            {
                Session = session;
                Asc = asc;
                AscState = ascState;
            }
        }

        /// <summary>
        /// 提供 BoundaryProject 所需的 ComponentLookup 与 BufferLookup。
        /// </summary>
        private sealed partial class BoundaryProjectLookupSystem : SystemBase
        {
            public BufferLookup<AscRegistrySlot> Registries;
            public ComponentLookup<GasAscIdentity> AscIdentities;
            public ComponentLookup<AscLifecycle> AscLifecycles;
            public ComponentLookup<AscBattleMembership> Memberships;
            public ComponentLookup<BoundaryDrainState> Drains;
            public BufferLookup<BoundaryFactBuffer> BoundaryFacts;

            /// <summary>
            /// 在 Update 时捕获本 World 的安全 lookup。
            /// </summary>
            protected override void OnUpdate()
            {
                Registries = GetBufferLookup<AscRegistrySlot>(true);
                AscIdentities = GetComponentLookup<GasAscIdentity>(true);
                AscLifecycles = GetComponentLookup<AscLifecycle>(true);
                Memberships = GetComponentLookup<AscBattleMembership>(true);
                Drains = GetComponentLookup<BoundaryDrainState>();
                BoundaryFacts = GetBufferLookup<BoundaryFactBuffer>();
            }
        }
    }
}
