using System;
using GAS.Runtime;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.PlayMode
{
    /// <summary>
    /// 在正式 Tick DAG 中验证跨 ASC Ability 取消握手的逐 Tick durable 交接与 owner-gone 收口。
    /// </summary>
    [TestFixture]
    public class RuntimeV1AbilityCancellationHandshakePlayModeTests
    {
        /// <summary>
        /// 验证 Cancel 经 T/T+1/T+2 完成 unsubscribe、observed fence、Ack 与 owner 双 tombstone。
        /// </summary>
        [Test]
        public void TickDag_跨Asc取消按三拍完成双端交接()
        {
            using var fixture = new RuntimeV1AbilityCancellationDagTestWorld(true);
            var seed = fixture.SeedRegisteredCrossAscWait();
            var startTick = fixture.CurrentTick;

            Assert.That(fixture.SubmitCancel(2101, seed.Activation).IsAccepted, Is.True);
            fixture.TickBatch();
            fixture.AssertCancellationRoutedAtT(in seed, startTick);

            fixture.TickBatch();
            fixture.AssertObservedFenceAndAckAtTPlusOne(in seed, startTick);

            fixture.TickBatch();
            fixture.AssertOwnerTombstonesAtTPlusTwo(in seed, startTick);
        }

        /// <summary>
        /// 验证 observed owner 缺失时 route 转 typed Ack，下一 Tick 仍能终结 owner wait 与 Activation。
        /// </summary>
        [Test]
        public void TickDag_ObservedOwner缺失时TypedAck阻止Owner悬挂()
        {
            using var fixture = new RuntimeV1AbilityCancellationDagTestWorld(false);
            var seed = fixture.SeedMissingObservedWait();
            var startTick = fixture.CurrentTick;

            Assert.That(fixture.SubmitCancel(2201, seed.Activation).IsAccepted, Is.True);
            fixture.TickBatch();
            fixture.AssertMissingOwnerAckAtT(in seed, startTick);

            fixture.TickBatch();
            fixture.AssertOwnerTombstonesAtTPlusOne(in seed, startTick);
        }
    }

    /// <summary>
    /// 复用正式单 ASC bootstrap，并只在测试准备期补齐第二个 Ready ASC 与冻结 wait slabs。
    /// </summary>
    internal sealed class RuntimeV1AbilityCancellationDagTestWorld : IDisposable
    {
        private const uint InitialGeneration = 1;
        private const string ReusedWorldName = "Runtime v1 Tick DAG PlayMode test";

        private readonly RuntimeV1TickDagTestWorld _runtime;
        private readonly World _world;
        private readonly Entity _observedEntity;
        private readonly OwnerAscHandle _missingObserved = new OwnerAscHandle(202, 1);

        internal ulong CurrentTick => _runtime.CurrentTick;

        private EntityManager EntityManager => _world.EntityManager;

        /// <summary>
        /// 创建 Ready Session；需要真实 observed writer 时再发布第二个 Ready registry 成员。
        /// </summary>
        internal RuntimeV1AbilityCancellationDagTestWorld(bool observedOwnerReady)
        {
            _runtime = new RuntimeV1TickDagTestWorld();
            _world = ResolveReusedWorld();
            _observedEntity = CreateObservedAsc();
            if (!observedOwnerReady)
                MarkObservedOwnerMissing();
        }

        /// <summary>
        /// 释放被复用 fixture 持有的 WorldOwner、World 与 Catalog。
        /// </summary>
        public void Dispose()
        {
            _runtime.Dispose();
        }

        /// <summary>
        /// 通过正式 typed Port 提交 Activation Cancel，不直接写 inbox。
        /// </summary>
        internal GasCommandAcceptResult SubmitCancel(
            ulong requestId,
            in AbilityActivationHandle activation)
        {
            return _runtime.SubmitCancel(requestId, in activation);
        }

        /// <summary>
        /// 推进恰好一个完整 FixedStep 父组与 batch fence。
        /// </summary>
        internal void TickBatch()
        {
            _runtime.TickBatch();
        }

        /// <summary>
        /// 在 owner 与 observed 两端种入已完成注册的 cross-ASC wait。
        /// </summary>
        internal GasCancellationHandshakeSeed SeedRegisteredCrossAscWait()
        {
            Assert.That(_observedEntity, Is.Not.EqualTo(Entity.Null));
            var subscription = new AbilitySubscriptionHandle(
                _runtime.Battle.SimulationEpoch, _missingObserved, 0, InitialGeneration);
            var seed = SeedOwnerWait(
                _missingObserved, in subscription, GasAbilityWaitState.Registered);
            SeedObservedSubscription(in seed);
            return seed;
        }

        /// <summary>
        /// 在 owner 种入尚待远端注册且 observed owner 已缺失的 wait。
        /// </summary>
        internal GasCancellationHandshakeSeed SeedMissingObservedWait()
        {
            Assert.That(EntityManager.GetComponentData<AscLifecycle>(_observedEntity).State,
                Is.EqualTo(GasAscLifecycleState.Terminal));
            return SeedOwnerWait(
                _missingObserved, default, GasAbilityWaitState.PendingRegistration);
        }

        /// <summary>
        /// 断言 T 的 owner Ending 与投递到 observed 的 T+1 WaitUnsubscribe。
        /// </summary>
        internal void AssertCancellationRoutedAtT(
            in GasCancellationHandshakeSeed seed,
            ulong startTick)
        {
            AssertSuccessfulTick(startTick + 1);
            var activation = GetActivation(in seed);
            var continuation = GetContinuation(in seed);
            var unsubscribe = FindOnlyLiveCommand(
                _observedEntity, GasAbilityPendingCommandKind.WaitUnsubscribe);

            Assert.That(activation.Header.StorageState, Is.EqualTo(GasSlabSlotState.Live));
            Assert.That(activation.Phase, Is.EqualTo(GasAbilityActivationPhase.Ending));
            Assert.That(continuation.Header.StorageState, Is.EqualTo(GasSlabSlotState.Live));
            Assert.That(continuation.WaitState, Is.EqualTo(GasAbilityWaitState.Ending));
            Assert.That(continuation.RegistrationGeneration, Is.EqualTo(2));
            Assert.That(unsubscribe.AvailableTick, Is.EqualTo(startTick + 2));
            Assert.That(unsubscribe.SourceAsc, Is.EqualTo(_runtime.OwnerAsc));
            Assert.That(unsubscribe.TargetAsc, Is.EqualTo(_missingObserved));
        }

        /// <summary>
        /// 断言 T+1 observed 已建立 tombstone fence，并向 owner 排入 T+2 typed Ack。
        /// </summary>
        internal void AssertObservedFenceAndAckAtTPlusOne(
            in GasCancellationHandshakeSeed seed,
            ulong startTick)
        {
            AssertSuccessfulTick(startTick + 2);
            var subscription = EntityManager.GetBuffer<AbilitySubscriptionSlot>(
                _observedEntity)[seed.Subscription.SlotIndex];
            var continuation = GetContinuation(in seed);
            var ack = FindOnlyLiveCommand(
                _runtime.Asc, GasAbilityPendingCommandKind.WaitUnsubscribeAck);

            Assert.That(subscription.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(subscription.State, Is.EqualTo(GasAbilitySubscriptionState.Ending));
            Assert.That(subscription.RegistrationGeneration, Is.EqualTo(2));
            Assert.That(continuation.Header.StorageState, Is.EqualTo(GasSlabSlotState.Live));
            Assert.That(continuation.WaitState, Is.EqualTo(GasAbilityWaitState.Ending));
            Assert.That(ack.AvailableTick, Is.EqualTo(startTick + 3));
            Assert.That(ack.SourceAsc, Is.EqualTo(_missingObserved));
            Assert.That(ack.TargetAsc, Is.EqualTo(_runtime.OwnerAsc));
        }

        /// <summary>
        /// 断言 T+2 owner 消费 Ack 后 Continuation 与 Activation 同 Tick 成为 tombstone。
        /// </summary>
        internal void AssertOwnerTombstonesAtTPlusTwo(
            in GasCancellationHandshakeSeed seed,
            ulong startTick)
        {
            AssertSuccessfulTick(startTick + 3);
            AssertOwnerTombstones(in seed);
        }

        /// <summary>
        /// 断言缺失 observed 的 outbound 在 T 被 route 转为回送 owner 的 T+1 typed Ack。
        /// </summary>
        internal void AssertMissingOwnerAckAtT(
            in GasCancellationHandshakeSeed seed,
            ulong startTick)
        {
            AssertSuccessfulTick(startTick + 1);
            var continuation = GetContinuation(in seed);
            Assert.That(continuation.Header.StorageState, Is.EqualTo(GasSlabSlotState.Live));
            Assert.That(continuation.WaitState, Is.EqualTo(GasAbilityWaitState.Ending));
            Assert.That(continuation.RegistrationGeneration, Is.EqualTo(2));
            var ack = FindOnlyLiveCommand(
                _runtime.Asc, GasAbilityPendingCommandKind.WaitUnsubscribeAck);
            Assert.That(ack.AvailableTick, Is.EqualTo(startTick + 2));
            Assert.That(ack.SourceAsc, Is.EqualTo(_missingObserved));
            Assert.That(ack.TargetAsc, Is.EqualTo(_runtime.OwnerAsc));
            Assert.That(CountLiveCommands(_runtime.Asc), Is.EqualTo(1));
        }

        /// <summary>
        /// 断言 owner-gone typed Ack 在 T+1 被消费，owner 不保留任何 live child。
        /// </summary>
        internal void AssertOwnerTombstonesAtTPlusOne(
            in GasCancellationHandshakeSeed seed,
            ulong startTick)
        {
            AssertSuccessfulTick(startTick + 2);
            AssertOwnerTombstones(in seed);
        }

        /// <summary>
        /// 在 owner 侧写入一个 live grant、Committed Activation 与冻结 continuation。
        /// </summary>
        private GasCancellationHandshakeSeed SeedOwnerWait(
            in OwnerAscHandle observed,
            in AbilitySubscriptionHandle subscription,
            GasAbilityWaitState waitState)
        {
            var epoch = _runtime.Battle.SimulationEpoch;
            var grant = new GrantedAbilityHandle(epoch, _runtime.OwnerAsc, 0, InitialGeneration);
            var activation = new AbilityActivationHandle(epoch, _runtime.OwnerAsc, 0, InitialGeneration);
            var continuation = new AbilityContinuationHandle(epoch, _runtime.OwnerAsc, 0, InitialGeneration);
            var seed = new GasCancellationHandshakeSeed(
                in grant, in activation, in continuation, in subscription);

            SeedGrant(in seed);
            SeedActivation(in seed);
            SeedContinuation(in seed, in observed, waitState);
            SetOwnerSlabHighWater();
            return seed;
        }

        /// <summary>
        /// 写入 Cancel 计划所需的唯一 live grant，并冻结一个 child Activation。
        /// </summary>
        private void SeedGrant(in GasCancellationHandshakeSeed seed)
        {
            var grants = EntityManager.GetBuffer<GrantedAbilitySlot>(_runtime.Asc);
            Assert.That(grants.Length, Is.Zero);
            grants.Add(new GrantedAbilitySlot
            {
                Header = GasSlabSlotHeader.CreateLive(InitialGeneration),
                Handle = seed.Grant,
                GrantSourceKind = GasAbilityGrantSourceKind.Bootstrap,
                ChildActivationCount = 1,
            });
        }

        /// <summary>
        /// 写入包含一个 cross-ASC continuation child 的 Committed Activation。
        /// </summary>
        private void SeedActivation(in GasCancellationHandshakeSeed seed)
        {
            var activations = EntityManager.GetBuffer<AbilityActivationSlot>(_runtime.Asc);
            Assert.That(activations.Length, Is.Zero);
            activations.Add(new AbilityActivationSlot
            {
                Header = GasSlabSlotHeader.CreateLive(InitialGeneration),
                Handle = seed.Activation,
                GrantedAbility = seed.Grant,
                ContinuationCount = 1,
                Phase = GasAbilityActivationPhase.Committed,
                LastCommandResult = GasAbilityCommandResult.Committed,
            });
        }

        /// <summary>
        /// 写入冻结 observed owner、Subscription 与 generation 的 owner continuation。
        /// </summary>
        private void SeedContinuation(
            in GasCancellationHandshakeSeed seed,
            in OwnerAscHandle observed,
            GasAbilityWaitState waitState)
        {
            var continuations = EntityManager.GetBuffer<AbilityContinuationSlot>(_runtime.Asc);
            Assert.That(continuations.Length, Is.Zero);
            continuations.Add(new AbilityContinuationSlot
            {
                Header = GasSlabSlotHeader.CreateLive(InitialGeneration),
                Handle = seed.Continuation,
                Activation = seed.Activation,
                ObservedAsc = observed,
                Subscription = seed.Subscription,
                RegistrationGeneration = 1,
                WaitSemantic = GasAbilityWaitSemantic.Event,
                WaitPolicy = GasAbilityWaitPolicy.OneShot,
                WaitState = waitState,
            });
        }

        /// <summary>
        /// 同步 owner 三类测试 slab 的 non-compacting high-water。
        /// </summary>
        private void SetOwnerSlabHighWater()
        {
            var heads = EntityManager.GetComponentData<AscSlabHeads>(_runtime.Asc);
            heads.GrantedAbility.HighWater = 1;
            heads.AbilityActivation.HighWater = 1;
            heads.AbilityContinuation.HighWater = 1;
            EntityManager.SetComponentData(_runtime.Asc, heads);
        }

        /// <summary>
        /// 在 observed writer 侧写入与 owner continuation 三句柄一致的 Registered Subscription。
        /// </summary>
        private void SeedObservedSubscription(in GasCancellationHandshakeSeed seed)
        {
            var subscriptions = EntityManager.GetBuffer<AbilitySubscriptionSlot>(_observedEntity);
            Assert.That(subscriptions.Length, Is.Zero);
            subscriptions.Add(new AbilitySubscriptionSlot
            {
                Header = GasSlabSlotHeader.CreateLive(InitialGeneration),
                Handle = seed.Subscription,
                ObservedAsc = _missingObserved,
                SubscriberAsc = _runtime.OwnerAsc,
                Activation = seed.Activation,
                Continuation = seed.Continuation,
                RegistrationGeneration = 1,
                WaitSemantic = GasAbilityWaitSemantic.Event,
                WaitPolicy = GasAbilityWaitPolicy.OneShot,
                State = GasAbilitySubscriptionState.Registered,
            });
            var heads = EntityManager.GetComponentData<AscSlabHeads>(_observedEntity);
            heads.AbilitySubscription.HighWater = 1;
            EntityManager.SetComponentData(_observedEntity, heads);
        }

        /// <summary>
        /// 创建完整 ASC 布局、预留 profile 容量并发布到唯一 Session registry。
        /// </summary>
        private Entity CreateObservedAsc()
        {
            ExpandSessionProfile();
            var entity = EntityManager.CreateEntity(GasRuntimeV1Archetypes.CreateAsc(EntityManager));
            var profile = EntityManager.GetComponentData<GasScaleProfile>(_runtime.Session);
            var catalog = EntityManager.GetComponentData<GasCatalogRegistry>(_runtime.Session);
            Assert.That(GasRuntimeV1Archetypes.TryInitializeAscMetadata(
                EntityManager, entity), Is.True);
            Assert.That(GasRuntimeV1Archetypes.TryApplyAscCapacities(
                EntityManager, entity, in profile), Is.True);
            Assert.That(GasRuntimeV1Archetypes.TryInitializeFixedBuffers(
                EntityManager, entity, in catalog), Is.True);
            InitializeObservedComponents(entity);
            PublishObservedRegistry(entity);
            return entity;
        }

        /// <summary>
        /// 把测试 Session 的成员上限扩为二，并同步物理 buffer capacity。
        /// </summary>
        private void ExpandSessionProfile()
        {
            var profile = EntityManager.GetComponentData<GasScaleProfile>(_runtime.Session);
            profile.MaxSpawnBatchSize = 2;
            profile.MaxAscRegistryCount = 2;
            EntityManager.SetComponentData(_runtime.Session, profile);
            Assert.That(GasRuntimeV1Archetypes.TryApplySessionCapacities(
                EntityManager, _runtime.Session, in profile), Is.True);
        }

        /// <summary>
        /// 保留 registry 稳定身份证据，但将 observed lifecycle 置 Terminal 使其不再是可路由 owner。
        /// </summary>
        private void MarkObservedOwnerMissing()
        {
            var lifecycle = EntityManager.GetComponentData<AscLifecycle>(_observedEntity);
            lifecycle.State = GasAscLifecycleState.Terminal;
            lifecycle.TerminalTick = CurrentTick;
            lifecycle.IngressClosed = 1;
            EntityManager.SetComponentData(_observedEntity, lifecycle);
        }

        /// <summary>
        /// 用稳定 observed 身份初始化新增 ASC 的全部 owner-specific 组件并关闭 spawn marker。
        /// </summary>
        private void InitializeObservedComponents(Entity entity)
        {
            var epoch = _runtime.Battle.SimulationEpoch;
            var ownerMembership = EntityManager.GetComponentData<AscBattleMembership>(_runtime.Asc);
            var ownerLifecycle = EntityManager.GetComponentData<AscLifecycle>(_runtime.Asc);
            EntityManager.SetComponentData(entity, new GasAscIdentity
            {
                SimulationEpoch = epoch,
                OwnerAsc = _missingObserved,
            });
            EntityManager.SetComponentData(entity, new AscBattleMembership
            {
                BattleInstance = _runtime.Battle,
                ScenarioUnitId = ownerMembership.ScenarioUnitId + 1,
                SideId = ownerMembership.SideId,
                TeamId = ownerMembership.TeamId,
                MembershipOrdinal = ownerMembership.MembershipOrdinal + 1,
            });
            EntityManager.SetComponentData(entity, ownerLifecycle);
            EntityManager.SetComponentData(entity, new GasActorBinding
            {
                OwnerActorStableId = 3000,
                AvatarActorStableId = 4000,
                BindingGeneration = 1,
            });
            EntityManager.SetComponentData(entity, new AscRandomState { State0 = 5000, State1 = 6000 });
            EntityManager.SetComponentData(entity, GasPayloadRangeAllocatorState.Create(epoch, in _missingObserved));
            EntityManager.SetComponentData(entity, BoundaryDrainState.Create(
                epoch, GasBoundaryOwnerKind.Asc, _missingObserved.AscStableId,
                _missingObserved.AscGeneration, 1));
            EntityManager.SetComponentEnabled<GasSpawnBatchMarker>(entity, false);
        }

        /// <summary>
        /// 追加第二个 Ready registry 槽，并同步 Battle 成员计数保持 authority 自洽。
        /// </summary>
        private void PublishObservedRegistry(Entity entity)
        {
            var registry = EntityManager.GetBuffer<AscRegistrySlot>(_runtime.Session);
            Assert.That(registry.Length, Is.EqualTo(1));
            var ownerSlot = registry[0];
            var observedSlot = new AscRegistrySlot
            {
                Header = GasSlabSlotHeader.CreateLive(_missingObserved.AscGeneration),
                OwnerAsc = _missingObserved,
                BattleInstance = _runtime.Battle,
                RegistryOrdinal = 1,
                SpawnBatchId = ownerSlot.SpawnBatchId,
                ReadyTick = ownerSlot.ReadyTick,
                State = GasAscRegistryState.Ready,
            };
            observedSlot.SetRuntimeEntity(entity);
            registry.Add(observedSlot);

            var battles = EntityManager.GetBuffer<BattleInstanceSlot>(_runtime.Session);
            var battle = battles[0];
            battle.MemberCount = 2;
            battle.ReadyMemberCount = 2;
            battles[0] = battle;
        }

        /// <summary>
        /// 从唯一复用 fixture World 定位 EntityManager，避免测试绕过正式 owner bootstrap。
        /// </summary>
        private World ResolveReusedWorld()
        {
            World resolved = null;
            var matches = 0;
            foreach (var candidate in World.All)
            {
                if (!candidate.IsCreated || candidate.Name != ReusedWorldName)
                    continue;
                var manager = candidate.EntityManager;
                if (!manager.Exists(_runtime.Session) || !manager.Exists(_runtime.Asc) ||
                    !manager.HasComponent<GasSessionIdentity>(_runtime.Session) ||
                    !manager.HasComponent<GasAscIdentity>(_runtime.Asc))
                    continue;
                if (manager.GetComponentData<GasSessionIdentity>(_runtime.Session).SimulationEpoch !=
                    _runtime.Battle.SimulationEpoch ||
                    !manager.GetComponentData<GasAscIdentity>(_runtime.Asc).OwnerAsc.Equals(
                        _runtime.OwnerAsc))
                    continue;
                resolved = candidate;
                matches++;
            }
            Assert.That(matches, Is.EqualTo(1), "应精确定位复用 bootstrap 创建的唯一 World。");
            return resolved;
        }

        /// <summary>
        /// 断言当前 Tick 已成功经过完整 gameplay lane 链。
        /// </summary>
        private void AssertSuccessfulTick(ulong expectedTick)
        {
            Assert.That(CurrentTick, Is.EqualTo(expectedTick));
            Assert.That(_runtime.Diagnostics.AdmissionSucceeded, Is.EqualTo(1));
            Assert.That(_runtime.Diagnostics.ExecutedLaneMask, Is.EqualTo(GasTickLaneMask.AllGameplay));
        }

        /// <summary>
        /// 断言 owner wait 与 Activation 均已终结为 tombstone，且 child count 已归零。
        /// </summary>
        private void AssertOwnerTombstones(in GasCancellationHandshakeSeed seed)
        {
            var activation = GetActivation(in seed);
            var continuation = GetContinuation(in seed);
            Assert.That(continuation.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(continuation.WaitState, Is.EqualTo(GasAbilityWaitState.Ended));
            Assert.That(activation.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(activation.Phase, Is.EqualTo(GasAbilityActivationPhase.Ended));
            Assert.That(activation.ContinuationCount, Is.Zero);
            Assert.That(CountLiveCommands(_runtime.Asc), Is.Zero);
        }

        /// <summary>
        /// 按冻结句柄索引读取 owner Activation 稳定槽。
        /// </summary>
        private AbilityActivationSlot GetActivation(in GasCancellationHandshakeSeed seed)
        {
            return EntityManager.GetBuffer<AbilityActivationSlot>(
                _runtime.Asc)[seed.Activation.SlotIndex];
        }

        /// <summary>
        /// 按冻结句柄索引读取 owner Continuation 稳定槽。
        /// </summary>
        private AbilityContinuationSlot GetContinuation(in GasCancellationHandshakeSeed seed)
        {
            return EntityManager.GetBuffer<AbilityContinuationSlot>(
                _runtime.Asc)[seed.Continuation.SlotIndex];
        }

        /// <summary>
        /// 查找指定 ASC 上唯一 live、Pending 且类型匹配的内部命令。
        /// </summary>
        private PendingCommand FindOnlyLiveCommand(
            Entity entity,
            GasAbilityPendingCommandKind kind)
        {
            var commands = EntityManager.GetBuffer<PendingCommand>(entity);
            var match = default(PendingCommand);
            var count = 0;
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (command.Header.StorageState != GasSlabSlotState.Live ||
                    command.State != GasSlotBusinessState.Pending ||
                    command.CommandKind != (int)kind)
                    continue;
                match = command;
                count++;
            }
            Assert.That(count, Is.EqualTo(1),
                $"应存在唯一 live {kind} 命令。实际槽：{DescribeCommands(commands)}");
            return match;
        }

        /// <summary>
        /// 输出 pending slab 的物理状态、业务状态、类型与时点，精确定位 route 交接缺口。
        /// </summary>
        private static string DescribeCommands(DynamicBuffer<PendingCommand> commands)
        {
            if (commands.Length == 0)
                return "<empty>";
            var description = string.Empty;
            for (var index = 0; index < commands.Length; index++)
            {
                var command = commands[index];
                if (description.Length > 0)
                    description += "; ";
                description += $"#{index}[{command.Header.StorageState}," +
                    $"{command.State},{(GasAbilityPendingCommandKind)command.CommandKind}," +
                    $"tick={command.AvailableTick},src={command.SourceAsc.AscStableId}," +
                    $"dst={command.TargetAsc.AscStableId}]";
            }
            return description;
        }

        /// <summary>
        /// 统计指定 ASC 上仍待处理的全部 live internal commands。
        /// </summary>
        private int CountLiveCommands(Entity entity)
        {
            var commands = EntityManager.GetBuffer<PendingCommand>(entity);
            var count = 0;
            for (var index = 0; index < commands.Length; index++)
            {
                if (commands[index].Header.StorageState == GasSlabSlotState.Live &&
                    commands[index].State == GasSlotBusinessState.Pending)
                    count++;
            }
            return count;
        }
    }

    /// <summary>
    /// 冻结单次取消握手中 owner grant、Activation、Continuation 与 observed Subscription 的稳定身份。
    /// </summary>
    internal readonly struct GasCancellationHandshakeSeed
    {
        internal readonly GrantedAbilityHandle Grant;
        internal readonly AbilityActivationHandle Activation;
        internal readonly AbilityContinuationHandle Continuation;
        internal readonly AbilitySubscriptionHandle Subscription;

        /// <summary>
        /// 保存测试准备期已经发布到两端 slab 的完整 typed handles。
        /// </summary>
        internal GasCancellationHandshakeSeed(
            in GrantedAbilityHandle grant,
            in AbilityActivationHandle activation,
            in AbilityContinuationHandle continuation,
            in AbilitySubscriptionHandle subscription)
        {
            Grant = grant;
            Activation = activation;
            Continuation = continuation;
            Subscription = subscription;
        }
    }
}
