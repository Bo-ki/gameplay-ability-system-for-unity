using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Runtime v1 按 BattleInstance 独立裁决胜负、平局与终局 ingress 隔离。
    /// </summary>
    [TestFixture]
    public partial class RuntimeV1TerminalResolveEditModeTests
    {
        private const ulong Epoch = 77;

        /// <summary>
        /// 验证单存活阵营生成 winner、全死生成 draw，且只冻结对应战局。
        /// </summary>
        [Test]
        public void TerminalResolve_按Battle独立生成Winner与Draw()
        {
            using var world = new World("Runtime v1 terminal resolve");
            var manager = world.EntityManager;
            var session = manager.CreateEntity(
                ComponentType.ReadWrite<GasSessionLifecycle>(),
                ComponentType.ReadWrite<AscRegistrySlot>(),
                ComponentType.ReadWrite<BattleInstanceSlot>());
            manager.SetComponentData(session, new GasSessionLifecycle
            {
                State = GasSessionLifecycleState.Running,
            });

            var winnerBattle = new BattleInstanceHandle(Epoch, 101, 1);
            var drawBattle = new BattleInstanceHandle(Epoch, 202, 1);
            var runningBattle = new BattleInstanceHandle(Epoch, 303, 1);
            var registry = manager.GetBuffer<AscRegistrySlot>(session);
            AddMember(manager, registry, winnerBattle, 1, 10, GasAscLifecycleState.Alive);
            AddMember(manager, registry, winnerBattle, 2, 20, GasAscLifecycleState.Dead);
            AddMember(manager, registry, drawBattle, 3, 30, GasAscLifecycleState.Dead);
            AddMember(manager, registry, drawBattle, 4, 40, GasAscLifecycleState.Dead);
            AddMember(manager, registry, runningBattle, 5, 50, GasAscLifecycleState.Alive);
            AddMember(manager, registry, runningBattle, 6, 60, GasAscLifecycleState.Alive);
            var battles = manager.GetBuffer<BattleInstanceSlot>(session);
            battles.Add(CreateBattle(winnerBattle, 0, 2));
            battles.Add(CreateBattle(drawBattle, 2, 2));
            battles.Add(CreateBattle(runningBattle, 4, 2));

            var facts = new NativeArray<GasCoreFactRecord>(4, Allocator.TempJob);
            var admission = new NativeArray<GasAdmissionResult>(1, Allocator.TempJob);
            var execution = new NativeArray<GasTickExecutionState>(1, Allocator.TempJob);
            var targetShadows = new NativeArray<GasTargetShadowState>(registry.Length, Allocator.TempJob);
            var battleIntents = new NativeArray<GasTerminalBattlePublishIntent>(battles.Length, Allocator.TempJob);
            var sessionIntent = new NativeArray<GasSessionLifecyclePublishIntent>(1, Allocator.TempJob);
            var decision = new NativeArray<GasFinalPublishDecision>(1, Allocator.TempJob);
            try
            {
                admission[0] = new GasAdmissionResult { Succeeded = 1 };
                execution[0] = new GasTickExecutionState
                {
                    CandidateTick = 9,
                    GameplayEnabled = 1,
                };
                var lookup = world.CreateSystemManaged<TerminalLookupSystem>();
                lookup.Update();
                PrepareTargetShadows(manager, registry, targetShadows);
                var terminalPrepareJob = new GasStableFactMergeTerminalPrepareJob
                {
                    Session = session,
                    SimulationEpoch = Epoch,
                    Admission = admission,
                    Registries = lookup.Registries,
                    AscIdentities = lookup.Identities,
                    Memberships = lookup.Memberships,
                    TargetShadows = targetShadows,
                    SessionLifecycles = lookup.SessionLifecycles,
                    Battles = lookup.Battles,
                    FaultInjections = lookup.FaultInjections,
                    BattleIntents = battleIntents,
                    SessionLifecycleIntent = sessionIntent,
                    CoreFacts = facts,
                    Execution = execution,
                };
                terminalPrepareJob.Execute();

                Assert.That(execution[0].PostAdmissionFailure,
                    Is.EqualTo(GasTickAdmissionFailureReason.None));
                Assert.That(execution[0].CoreFactCount, Is.EqualTo(2));
                Assert.That(facts[0].Kind, Is.EqualTo(GasBoundaryFactKind.BattleOutcome));
                Assert.That(facts[0].BattleInstance, Is.EqualTo(winnerBattle));
                Assert.That(facts[0].Payload.Integer0, Is.EqualTo(1));
                Assert.That(facts[0].Payload.Integer1, Is.EqualTo(1));
                Assert.That(facts[0].Payload.Integer2, Is.EqualTo(10));
                Assert.That(facts[1].BattleInstance, Is.EqualTo(drawBattle));
                Assert.That(facts[1].Payload.Integer0, Is.EqualTo(0));
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[0].State,
                    Is.EqualTo(GasBattleInstanceState.Running));
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[1].State,
                    Is.EqualTo(GasBattleInstanceState.Running));
                decision[0] = new GasFinalPublishDecision
                {
                    FailureReason = GasTickAdmissionFailureReason.PostAdmissionInvariantViolation,
                    Succeeded = 0,
                };
                new GasTerminalPublishJob
                {
                    Session = session,
                    Decision = decision,
                    BattleIntents = battleIntents,
                    SessionLifecycleIntent = sessionIntent,
                    Battles = lookup.Battles,
                    SessionLifecycles = lookup.SessionLifecycles,
                }.Execute();
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[0].State,
                    Is.EqualTo(GasBattleInstanceState.Running));
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[1].State,
                    Is.EqualTo(GasBattleInstanceState.Running));
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[0].IngressClosed, Is.Zero);
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[1].IngressClosed, Is.Zero);
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[0].OutcomeCode, Is.Zero);
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[1].OutcomeCode, Is.Zero);
                Assert.That(manager.GetComponentData<GasSessionLifecycle>(session).State,
                    Is.EqualTo(GasSessionLifecycleState.Running));
                decision[0] = new GasFinalPublishDecision { Succeeded = 1 };
                new GasTerminalPublishJob
                {
                    Session = session,
                    Decision = decision,
                    BattleIntents = battleIntents,
                    SessionLifecycleIntent = sessionIntent,
                    Battles = lookup.Battles,
                    SessionLifecycles = lookup.SessionLifecycles,
                }.Execute();
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[0].State,
                    Is.EqualTo(GasBattleInstanceState.OutcomeFrozen));
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[1].State,
                    Is.EqualTo(GasBattleInstanceState.OutcomeFrozen));
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[0].IngressClosed, Is.EqualTo(1));
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[1].IngressClosed, Is.EqualTo(1));
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[2].State,
                    Is.EqualTo(GasBattleInstanceState.Running));
                Assert.That(manager.GetBuffer<BattleInstanceSlot>(session)[2].IngressClosed, Is.Zero);
                Assert.That(manager.GetComponentData<GasSessionLifecycle>(session).State,
                    Is.EqualTo(GasSessionLifecycleState.Running));

                var completedBattle = battles[2];
                completedBattle.State = GasBattleInstanceState.OutcomeFrozen;
                battles[2] = completedBattle;
                execution[0] = new GasTickExecutionState
                {
                    CandidateTick = 10,
                    GameplayEnabled = 1,
                };
                terminalPrepareJob.Execute();
                decision[0] = new GasFinalPublishDecision { Succeeded = 1 };
                new GasTerminalPublishJob
                {
                    Session = session,
                    Decision = decision,
                    BattleIntents = battleIntents,
                    SessionLifecycleIntent = sessionIntent,
                    Battles = lookup.Battles,
                    SessionLifecycles = lookup.SessionLifecycles,
                }.Execute();
                Assert.That(manager.GetComponentData<GasSessionLifecycle>(session).State,
                    Is.EqualTo(GasSessionLifecycleState.Terminalizing));
            }
            finally
            {
                facts.Dispose();
                admission.Dispose();
                execution.Dispose();
                targetShadows.Dispose();
                battleIntents.Dispose();
                sessionIntent.Dispose();
                decision.Dispose();
            }
        }

        /// <summary>
        /// 按 registry ordinal 冻结直测所需的 projected lifecycle，不为 TerminalPrepare 提供 durable fallback。
        /// </summary>
        private static void PrepareTargetShadows(
            EntityManager manager,
            DynamicBuffer<AscRegistrySlot> registry,
            NativeArray<GasTargetShadowState> targetShadows)
        {
            for (var index = 0; index < registry.Length; index++)
            {
                var slot = registry[index];
                var asc = slot.ResolveRuntimeEntity();
                targetShadows[index] = new GasTargetShadowState
                {
                    Target = asc,
                    OwnerAsc = slot.OwnerAsc,
                    Lifecycle = manager.GetComponentData<AscLifecycle>(asc),
                    Prepared = 1,
                };
            }
        }

        /// <summary>
        /// 创建 Ready registry slot 与其 ASC authority，保证终局统计只读稳定身份。
        /// </summary>
        private static void AddMember(
            EntityManager manager,
            DynamicBuffer<AscRegistrySlot> registry,
            in BattleInstanceHandle battle,
            int sideId,
            int teamId,
            GasAscLifecycleState lifecycleState)
        {
            var owner = new OwnerAscHandle((ulong)registry.Length + 1, 1);
            var asc = manager.CreateEntity(
                ComponentType.ReadWrite<GasAscIdentity>(),
                ComponentType.ReadWrite<AscBattleMembership>(),
                ComponentType.ReadWrite<AscLifecycle>());
            manager.SetComponentData(asc, new GasAscIdentity
            {
                SimulationEpoch = Epoch,
                OwnerAsc = owner,
            });
            manager.SetComponentData(asc, new AscBattleMembership
            {
                BattleInstance = battle,
                SideId = sideId,
                TeamId = teamId,
            });
            manager.SetComponentData(asc, new AscLifecycle { State = lifecycleState });
            var slot = new AscRegistrySlot
            {
                Header = GasSlabSlotHeader.CreateLive(1),
                OwnerAsc = owner,
                BattleInstance = battle,
                State = GasAscRegistryState.Ready,
            };
            slot.SetRuntimeEntity(asc);
            registry.Add(slot);
        }

        /// <summary>
        /// 创建处于 Running 且成员范围已冻结的 BattleInstance registry slot。
        /// </summary>
        private static BattleInstanceSlot CreateBattle(
            in BattleInstanceHandle handle,
            int memberStart,
            int memberCount)
        {
            return new BattleInstanceSlot
            {
                Header = GasSlabSlotHeader.CreateLive(handle.BattleGeneration),
                Handle = handle,
                BattleInstanceId = handle.BattleStableId,
                MemberStart = memberStart,
                MemberCount = memberCount,
                ReadyMemberCount = memberCount,
                State = GasBattleInstanceState.Running,
            };
        }

        /// <summary>
        /// 提供终局 Job 所需的 Session registry、membership 与 lifecycle lookup。
        /// </summary>
        private sealed partial class TerminalLookupSystem : SystemBase
        {
            public BufferLookup<AscRegistrySlot> Registries;
            public ComponentLookup<GasAscIdentity> Identities;
            public ComponentLookup<AscBattleMembership> Memberships;
            public ComponentLookup<AscLifecycle> Lifecycles;
            public ComponentLookup<GasSessionLifecycle> SessionLifecycles;
            public BufferLookup<BattleInstanceSlot> Battles;
            public ComponentLookup<GasFinalPublishFaultInjection> FaultInjections;

            /// <summary>
            /// 在 Update 时捕获当前 World 的可写/只读 lookup。
            /// </summary>
            protected override void OnUpdate()
            {
                Registries = GetBufferLookup<AscRegistrySlot>(true);
                Identities = GetComponentLookup<GasAscIdentity>(true);
                Memberships = GetComponentLookup<AscBattleMembership>(true);
                Lifecycles = GetComponentLookup<AscLifecycle>();
                SessionLifecycles = GetComponentLookup<GasSessionLifecycle>();
                Battles = GetBufferLookup<BattleInstanceSlot>();
                FaultInjections = GetComponentLookup<GasFinalPublishFaultInjection>(true);
            }
        }
    }
}
