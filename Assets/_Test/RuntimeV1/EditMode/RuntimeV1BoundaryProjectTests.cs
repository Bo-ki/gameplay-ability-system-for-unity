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
        /// 成功预计算只返回冻结 fact 与 StateAfter，旧追加入口再一次性提交相同结果。
        /// </summary>
        [Test]
        public void TryPrepareFactAppend_成功预计算零写并由Append提交()
        {
            using var world = new World("Runtime v1 boundary append prepare success");
            var fixture = CreateFixture(world);
            var outbox = world.EntityManager.GetBuffer<BoundaryFactBuffer>(fixture.Asc);
            var state = fixture.AscState;
            var template = CreateAscBoundaryFact();
            var templateBefore = template;

            Assert.That(GasBoundaryDrainProtocol.TryPrepareFactAppend(
                in state, in template, out var preparedFact, out var stateAfter, out var failure),
                Is.True);
            Assert.That(failure, Is.EqualTo(GasBoundaryDrainFailure.None));
            Assert.That(outbox.Length, Is.Zero);
            Assert.That(state, Is.EqualTo(fixture.AscState));
            Assert.That(template, Is.EqualTo(templateBefore));
            Assert.That(preparedFact.EventId.SimulationEpoch, Is.EqualTo(Epoch));
            Assert.That(preparedFact.EventId.OwnerKind, Is.EqualTo(GasBoundaryOwnerKind.Asc));
            Assert.That(preparedFact.EventId.OwnerStableId, Is.EqualTo(Owner.AscStableId));
            Assert.That(preparedFact.EventId.OwnerGeneration, Is.EqualTo(Owner.AscGeneration));
            Assert.That(preparedFact.EventId.OwnerSequence, Is.EqualTo(1));
            Assert.That(stateAfter.NextOwnerSequence, Is.EqualTo(2));
            Assert.That(stateAfter.Phase, Is.EqualTo(GasBoundaryDrainPhase.Pending));

            Assert.That(GasBoundaryDrainProtocol.TryAppendFactWithSequence(
                ref state, outbox, in template, out var sequence, out var appendFailure), Is.True);
            Assert.That(appendFailure, Is.EqualTo(GasBoundaryDrainFailure.None));
            Assert.That(sequence, Is.EqualTo(1));
            Assert.That(outbox.Length, Is.EqualTo(1));
            Assert.That(outbox[0], Is.EqualTo(preparedFact));
            Assert.That(state, Is.EqualTo(stateAfter));
        }

        /// <summary>
        /// 序号耗尽必须在预计算阶段拒绝，且旧追加入口不得写 outbox 或推进状态。
        /// </summary>
        [Test]
        public void TryPrepareFactAppend_SequenceOverflow拒绝且零写()
        {
            using var world = new World("Runtime v1 boundary append sequence overflow");
            var fixture = CreateFixture(world);
            var outbox = world.EntityManager.GetBuffer<BoundaryFactBuffer>(fixture.Asc);
            var state = fixture.AscState;
            state.NextOwnerSequence = ulong.MaxValue;
            var template = CreateAscBoundaryFact();

            AssertPrepareRejectedWithoutWrite(
                in state, outbox, in template, GasBoundaryDrainFailure.SequenceOverflow);
        }

        /// <summary>
        /// InFlight 显式序号不得越过冻结 watermark 契约，失败时保持 owner 零写。
        /// </summary>
        [Test]
        public void TryPrepareFactAppend_InFlightWatermark拒绝且零写()
        {
            using var world = new World("Runtime v1 boundary append in-flight watermark");
            var fixture = CreateFixture(world);
            var outbox = world.EntityManager.GetBuffer<BoundaryFactBuffer>(fixture.Asc);
            var state = fixture.AscState;
            state.NextOwnerSequence = 5;
            state.Phase = GasBoundaryDrainPhase.InFlight;
            state.BatchId = 77;
            state.InFlightWatermark = 5;
            var template = CreateAscBoundaryFact();
            template.EventId.OwnerSequence = 5;

            AssertPrepareRejectedWithoutWrite(
                in state, outbox, in template, GasBoundaryDrainFailure.FactSequenceMismatch);
        }

        /// <summary>
        /// 已冻结身份冲突或缺失事实 kind 均复用既有 append 校验，并保持 owner 零写。
        /// </summary>
        [Test]
        public void TryPrepareFactAppend_身份或FactSchema非法拒绝且零写()
        {
            using var world = new World("Runtime v1 boundary append identity schema rejection");
            var fixture = CreateFixture(world);
            var outbox = world.EntityManager.GetBuffer<BoundaryFactBuffer>(fixture.Asc);
            var state = fixture.AscState;
            var identityMismatch = CreateAscBoundaryFact();
            identityMismatch.EventId.SimulationEpoch = Epoch;
            identityMismatch.EventId.OwnerKind = GasBoundaryOwnerKind.Asc;
            identityMismatch.EventId.OwnerStableId = Owner.AscStableId + 1;
            identityMismatch.EventId.OwnerGeneration = Owner.AscGeneration;
            AssertPrepareRejectedWithoutWrite(
                in state, outbox, in identityMismatch, GasBoundaryDrainFailure.FactOwnerMismatch);

            var invalidSchema = CreateAscBoundaryFact();
            invalidSchema.Kind = GasBoundaryFactKind.None;
            AssertPrepareRejectedWithoutWrite(
                in state, outbox, in invalidSchema, GasBoundaryDrainFailure.FactInvalid);
        }

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
                invalidFact.BattleInstance = new BattleInstanceHandle(Epoch, Battle.BattleStableId + 1, 1);
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
        /// 合法 BattleInstance fact 必须写入唯一 Session outbox，并保留 Battle scope identity。
        /// </summary>
        [Test]
        public void BoundaryProject_BattleScope路由SessionOutbox()
        {
            using var world = new World("Runtime v1 battle boundary project");
            var fixture = CreateFixture(world);
            var facts = new NativeArray<GasCoreFactRecord>(1, Allocator.TempJob);
            var admission = new NativeArray<GasAdmissionResult>(1, Allocator.TempJob);
            var execution = new NativeArray<GasTickExecutionState>(1, Allocator.TempJob);
            try
            {
                facts[0] = new GasCoreFactRecord
                {
                    Scope = GasBoundaryFactScope.BattleInstance,
                    Plane = GasBoundaryFactPlane.Gameplay,
                    Kind = GasBoundaryFactKind.BattleOutcome,
                    BattleInstance = Battle,
                    SimulationTick = 1,
                    SemanticPhaseOrdinal = 1,
                    WorkClassOrdinal = 1,
                    SemanticId = 200,
                    OperationOrdinal = 0,
                    FactOrdinal = 0,
                };
                admission[0] = new GasAdmissionResult { Succeeded = 1 };
                execution[0] = new GasTickExecutionState
                {
                    GameplayEnabled = 1,
                    CoreFactCount = 1,
                };
                RunProject(world, fixture, facts, admission, execution);

                Assert.That(execution[0].PostAdmissionFailure,
                    Is.EqualTo(GasTickAdmissionFailureReason.None));
                Assert.That(execution[0].BoundaryFactCount, Is.EqualTo(1));
                var sessionFacts = world.EntityManager.GetBuffer<BoundaryFactBuffer>(fixture.Session);
                Assert.That(sessionFacts.Length, Is.EqualTo(1));
                Assert.That(world.EntityManager.GetBuffer<BoundaryFactBuffer>(fixture.Asc).Length,
                    Is.Zero);
                var fact = sessionFacts[0];
                Assert.That(fact.Scope, Is.EqualTo(GasBoundaryFactScope.BattleInstance));
                Assert.That(fact.ScopeStableId, Is.EqualTo(Battle.BattleStableId));
                Assert.That(fact.ScopeGeneration, Is.EqualTo(Battle.BattleGeneration));
                Assert.That(fact.BattleInstanceId, Is.EqualTo(Battle.BattleStableId));
                Assert.That(fact.BattleInstanceGeneration, Is.EqualTo(Battle.BattleGeneration));
                Assert.That(fact.EventId.OwnerKind, Is.EqualTo(GasBoundaryOwnerKind.Session));
                Assert.That(fact.EventId.OwnerStableId, Is.EqualTo(Epoch));
                Assert.That(fact.EventId.OwnerSequence, Is.EqualTo(1));
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
        /// 同时验证纯预计算失败输出与旧追加入口都不改变输入、状态或 outbox。
        /// </summary>
        private static void AssertPrepareRejectedWithoutWrite(
            in BoundaryDrainState state,
            DynamicBuffer<BoundaryFactBuffer> outbox,
            in BoundaryFactBuffer template,
            GasBoundaryDrainFailure expectedFailure)
        {
            var stateBefore = state;
            var templateBefore = template;
            var outboxLengthBefore = outbox.Length;
            Assert.That(GasBoundaryDrainProtocol.TryPrepareFactAppend(
                in state, in template, out var preparedFact, out var stateAfter, out var failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(expectedFailure));
            Assert.That(preparedFact, Is.EqualTo(default(BoundaryFactBuffer)));
            Assert.That(stateAfter, Is.EqualTo(default(BoundaryDrainState)));
            Assert.That(state, Is.EqualTo(stateBefore));
            Assert.That(template, Is.EqualTo(templateBefore));
            Assert.That(outbox.Length, Is.EqualTo(outboxLengthBefore));

            var appendState = state;
            Assert.That(GasBoundaryDrainProtocol.TryAppendFactWithSequence(
                ref appendState, outbox, in template, out var sequence, out var appendFailure), Is.False);
            Assert.That(appendFailure, Is.EqualTo(expectedFailure));
            Assert.That(sequence, Is.Zero);
            Assert.That(appendState, Is.EqualTo(stateBefore));
            Assert.That(outbox.Length, Is.EqualTo(outboxLengthBefore));
        }

        /// <summary>
        /// 创建包含 Session registry、ASC identity、membership 与预留 outbox 的最小 Job fixture。
        /// </summary>
        private static BoundaryProjectFixture CreateFixture(World world)
        {
            var manager = world.EntityManager;
            var session = manager.CreateEntity(
                ComponentType.ReadWrite<AscRegistrySlot>(),
                ComponentType.ReadWrite<BattleInstanceSlot>(),
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
            var battles = manager.GetBuffer<BattleInstanceSlot>(session);
            battles.Add(new BattleInstanceSlot
            {
                Header = GasSlabSlotHeader.CreateLive(Battle.BattleGeneration),
                Handle = Battle,
                BattleInstanceId = Battle.BattleStableId,
                State = GasBattleInstanceState.Ready,
            });
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
            var targetShadows = new NativeArray<GasTargetShadowState>(1, Allocator.TempJob);
            var boundaryIntents = new NativeArray<GasBoundaryFactPublishIntent>(facts.Length, Allocator.TempJob);
            var decision = new NativeArray<GasFinalPublishDecision>(1, Allocator.TempJob);
            try
            {
                targetShadows[0] = new GasTargetShadowState
                {
                    Target = fixture.Asc,
                    OwnerAsc = Owner,
                    Lifecycle = world.EntityManager.GetComponentData<AscLifecycle>(fixture.Asc),
                    Prepared = 1,
                };
                new GasBoundaryProjectPrepareJob
                {
                    Session = fixture.Session,
                    SimulationEpoch = Epoch,
                    CoreFacts = facts,
                    Admission = admission,
                    Registries = lookupSystem.Registries,
                    AscIdentities = lookupSystem.AscIdentities,
                    TargetShadows = targetShadows,
                    Memberships = lookupSystem.Memberships,
                    Battles = lookupSystem.Battles,
                    Drains = lookupSystem.Drains,
                    BoundaryFacts = lookupSystem.BoundaryFacts,
                    FaultInjections = lookupSystem.FaultInjections,
                    BoundaryIntents = boundaryIntents,
                    Execution = execution,
                }.Run();
                decision[0] = new GasFinalPublishDecision
                {
                    FailureReason = execution[0].PostAdmissionFailure,
                    Succeeded = execution[0].PostAdmissionFailure ==
                        GasTickAdmissionFailureReason.None ? (byte)1 : (byte)0,
                };
                new GasBoundaryPublishJob
                {
                    Decision = decision,
                    BoundaryIntents = boundaryIntents,
                    Drains = lookupSystem.Drains,
                    BoundaryFacts = lookupSystem.BoundaryFacts,
                    Execution = execution,
                }.Run();
            }
            finally
            {
                targetShadows.Dispose();
                boundaryIntents.Dispose();
                decision.Dispose();
            }
        }

        /// <summary>
        /// 创建一条 EventId 尚未冻结、可由 ASC owner 追加的 Boundary fact 模板。
        /// </summary>
        private static BoundaryFactBuffer CreateAscBoundaryFact()
        {
            return new BoundaryFactBuffer
            {
                Scope = GasBoundaryFactScope.Asc,
                Plane = GasBoundaryFactPlane.Gameplay,
                ScopeStableId = Owner.AscStableId,
                ScopeGeneration = Owner.AscGeneration,
                SourceAsc = Owner,
                TargetAsc = Owner,
                SimulationTick = 1,
                SemanticPhaseOrdinal = 10,
                WorkClassOrdinal = 1,
                SemanticId = 100,
                Kind = GasBoundaryFactKind.AttributeChanged,
                Payload = new BoundaryFactPayload
                {
                    SchemaVersion = 1,
                    Kind = GasBoundaryPayloadKind.AttributeDelta,
                },
            };
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
            public ComponentLookup<AscBattleMembership> Memberships;
            public BufferLookup<BattleInstanceSlot> Battles;
            public ComponentLookup<BoundaryDrainState> Drains;
            public BufferLookup<BoundaryFactBuffer> BoundaryFacts;
            public ComponentLookup<GasFinalPublishFaultInjection> FaultInjections;

            /// <summary>
            /// 在 Update 时捕获本 World 的安全 lookup。
            /// </summary>
            protected override void OnUpdate()
            {
                Registries = GetBufferLookup<AscRegistrySlot>(true);
                AscIdentities = GetComponentLookup<GasAscIdentity>(true);
                Memberships = GetComponentLookup<AscBattleMembership>(true);
                Battles = GetBufferLookup<BattleInstanceSlot>(true);
                Drains = GetComponentLookup<BoundaryDrainState>();
                BoundaryFacts = GetBufferLookup<BoundaryFactBuffer>();
                FaultInjections = GetComponentLookup<GasFinalPublishFaultInjection>(true);
            }
        }
    }
}
