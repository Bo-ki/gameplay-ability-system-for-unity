using System;
using GAS.Runtime;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 RuntimeV1Runnable one-shot Ability 的 normal End 与无 wait OwnerTerminal 合同。
    /// </summary>
    [TestFixture]
    [Category("RuntimeV1Runnable")]
    public class RuntimeV1RunnableAbilityContractTests
    {
        /// <summary>
        /// 验证只有已冻结 committed work 可进入 Completed Ending，且 child 仅释放一次。
        /// </summary>
        [Test]
        public void CommittedOneShot_NormalEndPreservesWorkAndReleasesChildOnce()
        {
            using var fixture = new RunnableAbilityContractFixture(true);
            var plan = fixture.CreateCommittedPlan();
            var notFrozen = plan;
            notFrozen.ProducesCommittedWork = 0;

            Assert.That(GasAbilityLifecycleMaintenance.TryBeginCommittedOneShotNormalEnd(
                in notFrozen, fixture.Activations), Is.False);
            Assert.That(fixture.Activation.Phase, Is.EqualTo(GasAbilityActivationPhase.Committed));

            Assert.That(GasAbilityLifecycleMaintenance.TryBeginCommittedOneShotNormalEnd(
                in plan, fixture.Activations), Is.True);
            var ending = fixture.Activation;
            Assert.That(ending.Phase, Is.EqualTo(GasAbilityActivationPhase.Ending));
            Assert.That(ending.EndReason, Is.EqualTo(GasAbilityEndReason.Completed));
            Assert.That(ending.WasCancelled, Is.Zero);
            Assert.That(ending.CommitSequence, Is.EqualTo(RunnableAbilityContractFixture.CommitSequence));
            Assert.That(GasAbilityLifecycleMaintenance.TryBeginCommittedOneShotNormalEnd(
                in plan, fixture.Activations), Is.False);

            Assert.That(fixture.RunPostCommand(), Is.True);
            Assert.That(fixture.Activation.Phase, Is.EqualTo(GasAbilityActivationPhase.Ended));
            Assert.That(fixture.Activation.Header.StorageState,
                Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(fixture.Grant.ChildActivationCount, Is.Zero);
            Assert.That(fixture.RunPostCommand(), Is.True);
            Assert.That(fixture.Grant.ChildActivationCount, Is.Zero);
        }

        /// <summary>
        /// 验证 owner 死亡对未 Commit/已 Commit one-shot 均精确收口，且不撤销 Commit 身份。
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void OwnerTerminal_UncommittedOrCommitted_FinalizesExactlyOnce(bool committed)
        {
            using var fixture = new RunnableAbilityContractFixture(committed);

            Assert.That(fixture.RunOwnerTerminal(out var firstFinalizedCount), Is.True);
            Assert.That(firstFinalizedCount, Is.EqualTo(1));
            var ended = fixture.Activation;
            Assert.That(ended.Phase, Is.EqualTo(GasAbilityActivationPhase.Ended));
            Assert.That(ended.EndReason, Is.EqualTo(GasAbilityEndReason.OwnerTerminal));
            Assert.That(ended.WasCancelled, Is.EqualTo(1));
            Assert.That(ended.CommitSequence,
                Is.EqualTo(committed ? RunnableAbilityContractFixture.CommitSequence : 0UL));
            Assert.That(fixture.Grant.ChildActivationCount, Is.Zero);

            Assert.That(fixture.RunOwnerTerminal(out var repeatedFinalizedCount), Is.True);
            Assert.That(repeatedFinalizedCount, Is.Zero);
            Assert.That(fixture.Grant.ChildActivationCount, Is.Zero);
        }

        /// <summary>
        /// 验证本轮未支持的 live wait 不会被 OwnerTerminal helper 静默清理。
        /// </summary>
        [Test]
        public void OwnerTerminal_LiveWait_IsRejectedWithoutMutation()
        {
            using var fixture = new RunnableAbilityContractFixture(false);
            fixture.AddLiveContinuation();

            Assert.That(fixture.RunOwnerTerminal(out var finalizedCount), Is.False);
            Assert.That(finalizedCount, Is.Zero);
            Assert.That(fixture.Activation.Phase,
                Is.EqualTo(GasAbilityActivationPhase.RunningUncommitted));
            Assert.That(fixture.Activation.EndReason, Is.EqualTo(GasAbilityEndReason.None));
            Assert.That(fixture.Grant.ChildActivationCount, Is.EqualTo(1));
            Assert.That(fixture.Continuation.Header.StorageState,
                Is.EqualTo(GasSlabSlotState.Live));
        }
    }

    /// <summary>
    /// 构造单 grant、单 activation 与可选 continuation 的最小 ASC-local authority。
    /// </summary>
    internal sealed class RunnableAbilityContractFixture : IDisposable
    {
        internal const ulong CommitSequence = 7301;
        private const ulong SimulationEpoch = 73;
        private readonly World _world;
        private readonly Entity _asc;
        private readonly OwnerAscHandle _owner = new OwnerAscHandle(7302, 1);
        private readonly GrantedAbilityHandle _grant;
        private readonly AbilityActivationHandle _activation;

        /// <summary>
        /// 返回测试中的 grant buffer。
        /// </summary>
        internal DynamicBuffer<GrantedAbilitySlot> Grants =>
            _world.EntityManager.GetBuffer<GrantedAbilitySlot>(_asc);

        /// <summary>
        /// 返回测试中的 activation buffer。
        /// </summary>
        internal DynamicBuffer<AbilityActivationSlot> Activations =>
            _world.EntityManager.GetBuffer<AbilityActivationSlot>(_asc);

        /// <summary>
        /// 返回当前 grant 快照。
        /// </summary>
        internal GrantedAbilitySlot Grant => Grants[0];

        /// <summary>
        /// 返回当前 activation 快照。
        /// </summary>
        internal AbilityActivationSlot Activation => Activations[0];

        /// <summary>
        /// 返回当前 continuation 快照。
        /// </summary>
        internal AbilityContinuationSlot Continuation =>
            _world.EntityManager.GetBuffer<AbilityContinuationSlot>(_asc)[0];

        /// <summary>
        /// 创建 committed 或 uncommitted one-shot authority，并冻结匹配的 typed handles。
        /// </summary>
        internal RunnableAbilityContractFixture(bool committed)
        {
            _world = new World("RuntimeV1Runnable Ability contract test");
            var manager = _world.EntityManager;
            _asc = manager.CreateEntity(typeof(AscSlabHeads));
            manager.SetComponentData(_asc, AscSlabHeads.CreateEmpty());
            manager.AddBuffer<GrantedAbilitySlot>(_asc);
            manager.AddBuffer<AbilityActivationSlot>(_asc);
            manager.AddBuffer<AbilityContinuationSlot>(_asc);
            manager.AddBuffer<AbilitySubscriptionSlot>(_asc);
            _grant = new GrantedAbilityHandle(SimulationEpoch, _owner, 0, 1);
            _activation = new AbilityActivationHandle(SimulationEpoch, _owner, 0, 1);
            SeedAuthority(manager, committed);
        }

        /// <summary>
        /// 释放测试 World 与其 native storage。
        /// </summary>
        public void Dispose()
        {
            _world.Dispose();
        }

        /// <summary>
        /// 创建与 committed activation 严格匹配的 work-frozen CommitPlan。
        /// </summary>
        internal GasOwnerPlanRecord CreateCommittedPlan()
        {
            return new GasOwnerPlanRecord
            {
                OwnerAsc = _owner,
                StableSequence = CommitSequence,
                GrantedAbility = _grant,
                Activation = _activation,
                CommandKind = GasBoundaryCommandKind.Commit,
                Result = GasAbilityCommandResult.Committed,
                ProducesCommittedWork = 1,
                BusinessAccepted = 1,
            };
        }

        /// <summary>
        /// 执行标准 post-command 收尾并保存 slab heads。
        /// </summary>
        internal bool RunPostCommand()
        {
            var manager = _world.EntityManager;
            var heads = manager.GetComponentData<AscSlabHeads>(_asc);
            var result = GasAbilityLifecycleMaintenance.RunPostCommand(
                Grants,
                Activations,
                manager.GetBuffer<AbilityContinuationSlot>(_asc),
                ref heads);
            manager.SetComponentData(_asc, heads);
            return result;
        }

        /// <summary>
        /// 执行无 wait OwnerTerminal helper 并保存 slab heads。
        /// </summary>
        internal bool RunOwnerTerminal(out int finalizedActivationCount)
        {
            var manager = _world.EntityManager;
            var heads = manager.GetComponentData<AscSlabHeads>(_asc);
            var result = GasAbilityLifecycleMaintenance.TryFinalizeOneShotOwnerTerminal(
                in _owner,
                Grants,
                Activations,
                manager.GetBuffer<AbilityContinuationSlot>(_asc),
                manager.GetBuffer<AbilitySubscriptionSlot>(_asc),
                ref heads,
                out finalizedActivationCount);
            manager.SetComponentData(_asc, heads);
            return result;
        }

        /// <summary>
        /// 添加一个 live continuation，复现 SupportProfile 应拒绝的 wait 形状。
        /// </summary>
        internal void AddLiveContinuation()
        {
            var manager = _world.EntityManager;
            var continuations = manager.GetBuffer<AbilityContinuationSlot>(_asc);
            continuations.Add(new AbilityContinuationSlot
            {
                Header = GasSlabSlotHeader.CreateLive(1),
                Handle = new AbilityContinuationHandle(SimulationEpoch, _owner, 0, 1),
                Activation = _activation,
            });
            var activations = Activations;
            var activation = activations[0];
            activation.ContinuationCount = 1;
            activations[0] = activation;
            var heads = manager.GetComponentData<AscSlabHeads>(_asc);
            heads.AbilityContinuation.HighWater = 1;
            manager.SetComponentData(_asc, heads);
        }

        /// <summary>
        /// 写入互相引用的 live grant/activation，并初始化对应 slab high-water。
        /// </summary>
        private void SeedAuthority(EntityManager manager, bool committed)
        {
            Grants.Add(new GrantedAbilitySlot
            {
                Header = GasSlabSlotHeader.CreateLive(1),
                Handle = _grant,
                DefinitionId = 9101,
                DefinitionIndex = 0,
                ChildActivationCount = 1,
                RemovalPolicy = GasGrantedAbilityRemovalPolicy.LeaveGranted,
                GrantSourceKind = GasAbilityGrantSourceKind.Bootstrap,
                GrantSourceStableId = 7303,
            });
            Activations.Add(new AbilityActivationSlot
            {
                Header = GasSlabSlotHeader.CreateLive(1),
                Handle = _activation,
                GrantedAbility = _grant,
                CausalityId = 7304,
                ActivationSequence = 7304,
                CommitSequence = committed ? CommitSequence : 0,
                StartTick = 1,
                Phase = committed
                    ? GasAbilityActivationPhase.Committed
                    : GasAbilityActivationPhase.RunningUncommitted,
                LastCommandResult = committed
                    ? GasAbilityCommandResult.Committed
                    : GasAbilityCommandResult.Activated,
            });
            var heads = manager.GetComponentData<AscSlabHeads>(_asc);
            heads.GrantedAbility.HighWater = 1;
            heads.AbilityActivation.HighWater = 1;
            manager.SetComponentData(_asc, heads);
        }
    }
}
