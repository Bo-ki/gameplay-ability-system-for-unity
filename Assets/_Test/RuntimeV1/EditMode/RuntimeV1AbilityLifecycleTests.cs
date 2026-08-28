using System;
using GAS.Runtime;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 GrantedAbility removal 三态与 Activation Ending barrier 的 ASC-local slab 交接。
    /// </summary>
    [TestFixture]
    public class RuntimeV1AbilityLifecycleTests
    {
        /// <summary>
        /// 验证 CancelImmediately 先阻止新激活并结束全部 child，随后两者进入 tombstone。
        /// </summary>
        [Test]
        public void CancelImmediately_结束Child后回收Grant()
        {
            using var fixture = new AbilityLifecycleFixture();

            fixture.ApplyRemoval(GasGrantedAbilityRemovalPolicy.CancelImmediately);

            Assert.That(fixture.Activation.Phase, Is.EqualTo(GasAbilityActivationPhase.Ended));
            Assert.That(fixture.Activation.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(fixture.Grant.RemovalState,
                Is.EqualTo(GasGrantedAbilityRemovalState.PendingRemove));
            Assert.That(fixture.Grant.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
        }

        /// <summary>
        /// 验证 RemoveWhenAllActivationsEnd 立即阻止新激活，但不取消仍在运行的 child。
        /// </summary>
        [Test]
        public void RemoveWhenAllActivationsEnd_等待Child自然结束()
        {
            using var fixture = new AbilityLifecycleFixture();

            fixture.ApplyRemoval(GasGrantedAbilityRemovalPolicy.RemoveWhenAllActivationsEnd, false);

            Assert.That(fixture.Grant.RemovalState,
                Is.EqualTo(GasGrantedAbilityRemovalState.PendingRemove));
            Assert.That(fixture.Grant.Header.StorageState, Is.EqualTo(GasSlabSlotState.Live));
            Assert.That(fixture.Activation.Phase,
                Is.EqualTo(GasAbilityActivationPhase.RunningUncommitted));

            fixture.BeginNaturalEndAndFinalize();
            Assert.That(fixture.Activation.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(fixture.Grant.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
        }

        /// <summary>
        /// 验证 LeaveGranted 只脱钩 cleanup owner，保留冻结 provenance 且仍允许现有 grant 存活。
        /// </summary>
        [Test]
        public void LeaveGranted_脱钩Effect句柄但保留冻结Provenance()
        {
            using var fixture = new AbilityLifecycleFixture();

            fixture.ApplyRemoval(GasGrantedAbilityRemovalPolicy.LeaveGranted);

            Assert.That(fixture.Grant.Header.StorageState, Is.EqualTo(GasSlabSlotState.Live));
            Assert.That(fixture.Grant.RemovalState,
                Is.EqualTo(GasGrantedAbilityRemovalState.ProvenanceDetached));
            Assert.That(fixture.Grant.ProvenanceDetached, Is.EqualTo(1));
            Assert.That(fixture.Grant.GrantingActiveEffect.IsValid, Is.False);
            Assert.That(fixture.Grant.GrantApplicationId, Is.EqualTo(501));
            Assert.That(fixture.Grant.GrantContextId, Is.EqualTo(502));
            Assert.That(fixture.Activation.Header.StorageState, Is.EqualTo(GasSlabSlotState.Live));
        }

        /// <summary>
        /// 验证 removal 只能在当前 ASC 所属 BattleInstance 消费，跨战局命令不得改变 grant。
        /// </summary>
        [Test]
        public void Removal_拒绝跨战局命令且不改变Grant()
        {
            using var fixture = new AbilityLifecycleFixture();
            var battle = fixture.Battle;
            var foreignBattle = new BattleInstanceHandle(
                battle.SimulationEpoch, 8299, 1);

            Assert.That(fixture.EnqueueRemoval(
                in foreignBattle, GasGrantedAbilityRemovalPolicy.CancelImmediately, 1), Is.True);
            Assert.That(fixture.RunPreCommand(in battle, 1), Is.True);

            Assert.That(fixture.Grant.RemovalState,
                Is.EqualTo(GasGrantedAbilityRemovalState.None));
            Assert.That(fixture.Activation.Phase,
                Is.EqualTo(GasAbilityActivationPhase.RunningUncommitted));
        }

        /// <summary>
        /// 验证命令携带的陈旧 provenance 被消费为 no-op，不得清理已换绑的 grant 来源。
        /// </summary>
        [Test]
        public void Removal_拒绝陈旧Provenance且不改变Grant()
        {
            using var fixture = new AbilityLifecycleFixture();
            var battle = fixture.Battle;

            Assert.That(fixture.EnqueueRemoval(
                in battle, GasGrantedAbilityRemovalPolicy.CancelImmediately, 1), Is.True);
            fixture.MutatePendingRemovalApplication(999);
            Assert.That(fixture.RunPreCommand(in battle, 1), Is.True);

            Assert.That(fixture.Grant.RemovalState,
                Is.EqualTo(GasGrantedAbilityRemovalState.None));
            Assert.That(fixture.Activation.Phase,
                Is.EqualTo(GasAbilityActivationPhase.RunningUncommitted));
        }

        /// <summary>
        /// 验证同 Tick removal 按 AvailableTick/CommandSequence 仲裁，首条策略建立不可逆 barrier。
        /// </summary>
        [Test]
        public void Removal_同Tick按CommandSequence首条仲裁()
        {
            using var fixture = new AbilityLifecycleFixture();
            var battle = fixture.Battle;

            Assert.That(fixture.EnqueueRemoval(
                in battle, GasGrantedAbilityRemovalPolicy.LeaveGranted, 2), Is.True);
            Assert.That(fixture.EnqueueRemoval(
                in battle, GasGrantedAbilityRemovalPolicy.CancelImmediately, 1), Is.True);
            Assert.That(fixture.RunPreCommand(in battle, 1), Is.True);

            Assert.That(fixture.Grant.RemovalPolicy,
                Is.EqualTo(GasGrantedAbilityRemovalPolicy.CancelImmediately));
            Assert.That(fixture.Grant.RemovalState,
                Is.EqualTo(GasGrantedAbilityRemovalState.PendingRemove));
            Assert.That(fixture.Activation.Phase,
                Is.EqualTo(GasAbilityActivationPhase.Ending));
            Assert.That(fixture.Grant.ProvenanceDetached, Is.Zero);
        }
    }

    /// <summary>
    /// 构造单 grant、单 child Activation 与空 continuation 的最小 ASC authority。
    /// </summary>
    internal sealed class AbilityLifecycleFixture : IDisposable
    {
        private const ulong Epoch = 81;
        private readonly World _world;
        private readonly Entity _asc;
        private readonly OwnerAscHandle _owner = new OwnerAscHandle(8101, 1);
        private readonly BattleInstanceHandle _battle = new BattleInstanceHandle(Epoch, 8201, 1);

        /// <summary>
        /// 返回 fixture 当前 ASC 所属的冻结 BattleInstance，供跨战局校验测试使用。
        /// </summary>
        internal BattleInstanceHandle Battle => _battle;

        internal GrantedAbilitySlot Grant => _world.EntityManager.GetBuffer<GrantedAbilitySlot>(_asc)[0];
        internal AbilityActivationSlot Activation =>
            _world.EntityManager.GetBuffer<AbilityActivationSlot>(_asc)[0];

        /// <summary>
        /// 创建完整 ASC archetype，并发布一组互相引用的 live slab 槽。
        /// </summary>
        internal AbilityLifecycleFixture()
        {
            _world = new World("Runtime v1 Ability lifecycle EditMode test");
            var manager = _world.EntityManager;
            _asc = manager.CreateEntity(GasRuntimeV1Archetypes.CreateAsc(manager));
            manager.SetComponentData(_asc, new GasAscIdentity
            {
                SimulationEpoch = Epoch,
                OwnerAsc = _owner,
            });
            GasRuntimeV1Archetypes.TryInitializeAscMetadata(manager, _asc);
            GasRuntimeV1Archetypes.TryApplyAscCapacities(manager, _asc, CreateProfile());
            SeedAuthority(manager);
        }

        /// <summary>
        /// 释放测试 World 及其全部 native storage。
        /// </summary>
        public void Dispose()
        {
            _world.Dispose();
        }

        /// <summary>
        /// 通过正式 PendingCommand helper 请求 removal，并执行 owner maintenance。
        /// </summary>
        internal void ApplyRemoval(
            GasGrantedAbilityRemovalPolicy policy,
            bool finalize = true)
        {
            var manager = _world.EntityManager;
            var heads = manager.GetComponentData<AscSlabHeads>(_asc);
            var grants = manager.GetBuffer<GrantedAbilitySlot>(_asc);
            var commands = manager.GetBuffer<PendingCommand>(_asc);
            Assert.That(GasAbilityLifecycleMaintenance.TryEnqueueGrantedRemoval(
                in _battle,
                in _owner,
                Grant.Handle,
                grants,
                policy,
                1,
                1,
                2,
                commands,
                ref heads.PendingCommand), Is.True);
            Assert.That(GasAbilityLifecycleMaintenance.RunPreCommand(
                1,
                in _battle,
                grants,
                manager.GetBuffer<AbilityActivationSlot>(_asc),
                manager.GetBuffer<AbilityContinuationSlot>(_asc),
                manager.GetBuffer<AbilitySubscriptionSlot>(_asc),
                commands,
                ref heads), Is.True);
            if (finalize)
                RunPostCommand(manager, ref heads);
            manager.SetComponentData(_asc, heads);
        }

        /// <summary>
        /// 仅入队一条 removal 命令，保留 pre-command 前的同 Tick 仲裁窗口。
        /// </summary>
        internal bool EnqueueRemoval(
            in BattleInstanceHandle battle,
            GasGrantedAbilityRemovalPolicy policy,
            ulong commandSequence)
        {
            var manager = _world.EntityManager;
            var heads = manager.GetComponentData<AscSlabHeads>(_asc);
            var result = GasAbilityLifecycleMaintenance.TryEnqueueGrantedRemoval(
                in battle,
                in _owner,
                Grant.Handle,
                manager.GetBuffer<GrantedAbilitySlot>(_asc),
                policy,
                1,
                commandSequence,
                2,
                manager.GetBuffer<PendingCommand>(_asc),
                ref heads.PendingCommand);
            manager.SetComponentData(_asc, heads);
            return result;
        }

        /// <summary>
        /// 运行一次指定 BattleInstance 的 pre-command removal 消费，不执行 post-command 回收。
        /// </summary>
        internal bool RunPreCommand(in BattleInstanceHandle battle, ulong candidateTick)
        {
            var manager = _world.EntityManager;
            var heads = manager.GetComponentData<AscSlabHeads>(_asc);
            var result = GasAbilityLifecycleMaintenance.RunPreCommand(
                candidateTick,
                in battle,
                manager.GetBuffer<GrantedAbilitySlot>(_asc),
                manager.GetBuffer<AbilityActivationSlot>(_asc),
                manager.GetBuffer<AbilityContinuationSlot>(_asc),
                manager.GetBuffer<AbilitySubscriptionSlot>(_asc),
                manager.GetBuffer<PendingCommand>(_asc),
                ref heads);
            manager.SetComponentData(_asc, heads);
            return result;
        }

        /// <summary>
        /// 篡改待消费命令的 application provenance，用于复现 stale cleanup 防护。
        /// </summary>
        internal void MutatePendingRemovalApplication(ulong applicationId)
        {
            var commands = _world.EntityManager.GetBuffer<PendingCommand>(_asc);
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Header.StorageState != GasSlabSlotState.Live ||
                    command.CommandKind != (int)GasAbilityPendingCommandKind.RequestGrantedRemoval)
                    continue;
                command.GrantedRemovalApplicationId = applicationId;
                commands[index] = command;
                return;
            }
            Assert.Fail("未找到待消费的 removal 命令");
        }

        /// <summary>
        /// 模拟 Ability program 自然进入 Ending，再运行标准 post-command 收尾。
        /// </summary>
        internal void BeginNaturalEndAndFinalize()
        {
            var manager = _world.EntityManager;
            var activations = manager.GetBuffer<AbilityActivationSlot>(_asc);
            var activation = activations[0];
            activation.Phase = GasAbilityActivationPhase.Ending;
            activation.EndReason = GasAbilityEndReason.Completed;
            activations[0] = activation;
            var heads = manager.GetComponentData<AscSlabHeads>(_asc);
            RunPostCommand(manager, ref heads);
            manager.SetComponentData(_asc, heads);
        }

        /// <summary>
        /// 执行一次标准 post-command lifecycle maintenance。
        /// </summary>
        private void RunPostCommand(EntityManager manager, ref AscSlabHeads heads)
        {
            Assert.That(GasAbilityLifecycleMaintenance.RunPostCommand(
                manager.GetBuffer<GrantedAbilitySlot>(_asc),
                manager.GetBuffer<AbilityActivationSlot>(_asc),
                manager.GetBuffer<AbilityContinuationSlot>(_asc),
                ref heads), Is.True);
        }

        /// <summary>
        /// 写入稳定 grant/activation 句柄、child 链与对应 slab high-water。
        /// </summary>
        private void SeedAuthority(EntityManager manager)
        {
            var grantHandle = new GrantedAbilityHandle(Epoch, _owner, 0, 1);
            var activationHandle = new AbilityActivationHandle(Epoch, _owner, 0, 1);
            manager.GetBuffer<GrantedAbilitySlot>(_asc).Add(new GrantedAbilitySlot
            {
                Header = GasSlabSlotHeader.CreateLive(1),
                Handle = grantHandle,
                DefinitionId = 1,
                DefinitionIndex = 0,
                ChildActivationCount = 1,
                RemovalPolicy = GasGrantedAbilityRemovalPolicy.LeaveGranted,
                GrantSourceKind = GasAbilityGrantSourceKind.ActiveEffect,
                GrantSourceStableId = 503,
                GrantingActiveEffect = new ActiveEffectHandle(Epoch, _owner, 0, 1),
                GrantApplicationId = 501,
                GrantContextId = 502,
            });
            manager.GetBuffer<AbilityActivationSlot>(_asc).Add(new AbilityActivationSlot
            {
                Header = GasSlabSlotHeader.CreateLive(1),
                Handle = activationHandle,
                GrantedAbility = grantHandle,
                Phase = GasAbilityActivationPhase.RunningUncommitted,
            });
            var heads = manager.GetComponentData<AscSlabHeads>(_asc);
            heads.GrantedAbility.HighWater = 1;
            heads.AbilityActivation.HighWater = 1;
            manager.SetComponentData(_asc, heads);
        }

        /// <summary>
        /// 返回只为本 fixture 开放相关 slab 的最小合法 profile。
        /// </summary>
        private static GasScaleProfile CreateProfile()
        {
            return new GasScaleProfile
            {
                MaxGrantedAbilityCount = 2,
                MaxAbilityActivationCount = 2,
                MaxAbilityContinuationCount = 2,
                MaxAbilitySubscriptionCount = 2,
                MaxCooldownGateCount = 2,
                MaxActivationOwnedContributionCount = 2,
                MaxEmittedApplicationRefCount = 2,
                MaxPendingCommandCount = 2,
            };
        }
    }
}
