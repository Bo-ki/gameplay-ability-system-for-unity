using System;
using GAS.Runtime;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证跨 ASC wait 协议写入 Continuation/Subscription slabs 后的握手、唤醒与取消屏障。
    /// </summary>
    [TestFixture]
    public class RuntimeV1AbilityWaitSlabTransactionTests
    {
        /// <summary>
        /// 验证 Level 在 observed writer 的 sample+register 线性化点即时完成且统一到 T+1 恢复。
        /// </summary>
        [Test]
        public void Level_SampleAndRegister即时完成且不分配Subscription()
        {
            using var fixture = new AbilityWaitSlabFixture();
            Assert.That(fixture.TryBeginWait(
                GasAbilityWaitSemantic.Level,
                GasAbilityWaitPolicy.OneShot,
                10,
                0,
                out var continuation,
                out var registration), Is.True);

            var sample = GasAbilityWaitRegistrationSample.ForLevel(true);
            var status = fixture.SampleAndRegister(
                in registration,
                in sample,
                11,
                out var completion);

            Assert.That(status, Is.EqualTo(GasAbilityWaitProtocolStatus.Completed));
            Assert.That(completion.AvailableTick, Is.EqualTo(12));
            Assert.That(completion.CompletionTick, Is.EqualTo(11));
            Assert.That(completion.ResumeTick, Is.EqualTo(12));
            Assert.That(completion.Subscription.IsValid, Is.False);
            Assert.That(fixture.SubscriptionHighWater, Is.Zero);
            Assert.That(fixture.ApplyOwnerResponse(in completion), Is.True);
            var slot = fixture.GetContinuation(in continuation);
            Assert.That(slot.WaitState, Is.EqualTo(GasAbilityWaitState.Completed));
            Assert.That(slot.ResumeTick, Is.EqualTo(12));
        }

        /// <summary>
        /// 验证 Edge/Event 只接受新 ordinal，persistent 唤醒序号跨消息严格单调。
        /// </summary>
        [TestCase(GasAbilityWaitSemantic.Edge, GasAbilityWaitSignalKind.EdgeObserved)]
        [TestCase(GasAbilityWaitSemantic.Event, GasAbilityWaitSignalKind.EventObserved)]
        public void Edge与Event_Ack后拒绝旧Ordinal且PersistentWake单调(
            GasAbilityWaitSemantic semantic,
            GasAbilityWaitSignalKind signalKind)
        {
            using var fixture = new AbilityWaitSlabFixture();
            fixture.BeginAndRegisterPersistent(
                semantic,
                20,
                7,
                out var continuation,
                out var subscription);

            var stale = fixture.WakeObserved(
                in subscription, signalKind, 22, 7, out _);
            var first = fixture.WakeObserved(
                in subscription, signalKind, 23, 8, out var firstWake);
            Assert.That(fixture.ApplyOwnerResponse(in firstWake), Is.True);
            var second = fixture.WakeObserved(
                in subscription, signalKind, 24, 9, out var secondWake);
            Assert.That(fixture.ApplyOwnerResponse(in secondWake), Is.True);

            Assert.That(stale, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpNotMatched));
            Assert.That(first, Is.EqualTo(GasAbilityWaitProtocolStatus.PersistentWake));
            Assert.That(second, Is.EqualTo(GasAbilityWaitProtocolStatus.PersistentWake));
            Assert.That(firstWake.WakeOrdinal, Is.EqualTo(1));
            Assert.That(secondWake.WakeOrdinal, Is.EqualTo(2));
            var ownerSlot = fixture.GetContinuation(in continuation);
            var observedSlot = fixture.GetSubscription(in subscription);
            Assert.That(ownerSlot.WaitState, Is.EqualTo(GasAbilityWaitState.Registered));
            Assert.That(ownerSlot.WakeOrdinal, Is.EqualTo(2));
            Assert.That(observedSlot.WakeOrdinal, Is.EqualTo(2));
            Assert.That(observedSlot.ObservedRevisionAtRegister, Is.EqualTo(9));
        }

        /// <summary>
        /// 验证 cancel 先推进 registration generation，使旧 Ack/Completion 失效后再双端 tombstone。
        /// </summary>
        [Test]
        public void Cancel_推进Generation并拒绝迟到消息后完成双端回收()
        {
            using var fixture = new AbilityWaitSlabFixture();
            fixture.BeginAndRegisterOneShot(
                GasAbilityWaitSemantic.Event,
                30,
                0,
                out var continuation,
                out var subscription,
                out var oldAck);
            var wakeStatus = fixture.WakeObserved(
                in subscription,
                GasAbilityWaitSignalKind.EventObserved,
                32,
                1,
                out var oldCompletion);
            Assert.That(wakeStatus, Is.EqualTo(GasAbilityWaitProtocolStatus.Completed));

            Assert.That(fixture.TryCancel(
                in continuation, 33, out var unsubscribe), Is.True);
            Assert.That(unsubscribe.RegistrationGeneration, Is.EqualTo(2));
            Assert.That(fixture.ApplyOwnerResponse(in oldAck), Is.False);
            Assert.That(fixture.ApplyOwnerResponse(in oldCompletion), Is.False);
            Assert.That(fixture.UnsubscribeObserved(
                in unsubscribe, 34, out var cancelAck), Is.True);
            Assert.That(fixture.ApplyCancelAck(in cancelAck), Is.True);

            var ownerSlot = fixture.GetContinuation(in continuation);
            var observedSlot = fixture.GetSubscription(in subscription);
            Assert.That(ownerSlot.WaitState, Is.EqualTo(GasAbilityWaitState.Ended));
            Assert.That(ownerSlot.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(observedSlot.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(fixture.Activation.ContinuationCount, Is.Zero);
        }

        /// <summary>
        /// 验证 observed writer 对同 Tick 重复 registration 返回同一 Ack 且只分配一个订阅槽。
        /// </summary>
        [Test]
        public void Registration_重复投递幂等返回原Subscription()
        {
            using var fixture = new AbilityWaitSlabFixture();
            Assert.That(fixture.TryBeginWait(
                GasAbilityWaitSemantic.Event,
                GasAbilityWaitPolicy.OneShot,
                40,
                0,
                out _,
                out var registration), Is.True);
            var sample = GasAbilityWaitRegistrationSample.ForEvent(3);

            var first = fixture.SampleAndRegister(
                in registration, in sample, 41, out var firstAck);
            var duplicate = fixture.SampleAndRegister(
                in registration, in sample, 41, out var duplicateAck);

            Assert.That(first, Is.EqualTo(GasAbilityWaitProtocolStatus.Registered));
            Assert.That(duplicate, Is.EqualTo(GasAbilityWaitProtocolStatus.Registered));
            Assert.That(duplicateAck.Subscription, Is.EqualTo(firstAck.Subscription));
            Assert.That(duplicateAck.AvailableTick, Is.EqualTo(firstAck.AvailableTick));
            Assert.That(fixture.SubscriptionHighWater, Is.EqualTo(1));
            Assert.That(fixture.ApplyOwnerResponse(in firstAck), Is.True);
            Assert.That(fixture.ApplyOwnerResponse(in duplicateAck), Is.False);
        }

        /// <summary>
        /// 验证 unsubscribe 先到时建立 generation fence，随后同 Tick 的旧 registration 不得复活订阅。
        /// </summary>
        [Test]
        public void CancelBeforeRegistration_Fence拒绝迟到旧注册()
        {
            using var fixture = new AbilityWaitSlabFixture();
            Assert.That(fixture.TryBeginWait(
                GasAbilityWaitSemantic.Event,
                GasAbilityWaitPolicy.OneShot,
                50,
                0,
                out var continuation,
                out var registration), Is.True);
            Assert.That(fixture.TryCancel(
                in continuation, 50, out var unsubscribe), Is.True);
            Assert.That(fixture.UnsubscribeObserved(
                in unsubscribe, 51, out var cancelAck), Is.True);

            var sample = GasAbilityWaitRegistrationSample.ForEvent(0);
            var late = fixture.SampleAndRegister(
                in registration, in sample, 51, out var lateResponse);
            Assert.That(late, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpStale));
            Assert.That(lateResponse.CommandKind, Is.Zero);
            var fence = fixture.GetSubscriptionAt(0);
            Assert.That(fence.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(fence.RegistrationGeneration, Is.EqualTo(2));
            Assert.That(fence.State, Is.EqualTo(GasAbilitySubscriptionState.Ending));
            Assert.That(fixture.ApplyCancelAck(in cancelAck), Is.True);
            Assert.That(fixture.Activation.ContinuationCount, Is.Zero);
        }

        /// <summary>
        /// 验证 cancellation fence 跨 Tick 回收后，违反 exact-T+1 的旧 generation 注册仍不可复活订阅。
        /// </summary>
        [Test]
        public void CancelBeforeRegistration_跨Tick回收后旧注册仍被拒绝()
        {
            using var fixture = new AbilityWaitSlabFixture();
            Assert.That(fixture.TryBeginWait(
                GasAbilityWaitSemantic.Event,
                GasAbilityWaitPolicy.OneShot,
                55,
                0,
                out var continuation,
                out var oldRegistration), Is.True);
            Assert.That(oldRegistration.AvailableTick, Is.EqualTo(56));
            Assert.That(oldRegistration.RegistrationGeneration, Is.EqualTo(1));
            Assert.That(fixture.TryCancel(
                in continuation, 55, out var unsubscribe), Is.True);
            Assert.That(unsubscribe.RegistrationGeneration, Is.EqualTo(2));
            Assert.That(fixture.UnsubscribeObserved(
                in unsubscribe, 56, out var cancelAck), Is.True);

            fixture.RecycleTombstones(57);
            var recycled = fixture.GetSubscriptionAt(0);
            Assert.That(recycled.Header.StorageState, Is.EqualTo(GasSlabSlotState.Free));
            Assert.That(recycled.Header.Generation, Is.EqualTo(2));

            var sample = GasAbilityWaitRegistrationSample.ForEvent(0);
            var first = fixture.SampleAndRegister(
                in oldRegistration, in sample, 57, out var firstResponse);
            var duplicate = fixture.SampleAndRegister(
                in oldRegistration, in sample, 57, out var duplicateResponse);

            Assert.That(first, Is.EqualTo(GasAbilityWaitProtocolStatus.InvalidRequest));
            Assert.That(duplicate, Is.EqualTo(first));
            Assert.That(firstResponse.CommandKind, Is.Zero);
            Assert.That(duplicateResponse.CommandKind, Is.Zero);
            Assert.That(fixture.SubscriptionHighWater, Is.EqualTo(1));
            Assert.That(fixture.GetSubscriptionAt(0).Header.StorageState,
                Is.EqualTo(GasSlabSlotState.Free));
            Assert.That(fixture.ApplyCancelAck(in cancelAck), Is.True);
            Assert.That(fixture.Activation.ContinuationCount, Is.Zero);
        }

        /// <summary>
        /// 验证未知 semantic/policy 与不符合 semantic 的 observed handle 均在写 observed slab 前拒绝。
        /// </summary>
        [Test]
        public void 非法Wait描述_拒绝且不污染ObservedSlab()
        {
            using var fixture = new AbilityWaitSlabFixture();
            Assert.That(fixture.TryBeginWait(
                (GasAbilityWaitSemantic)99,
                GasAbilityWaitPolicy.OneShot,
                60,
                0,
                out var unknownSemantic,
                out _), Is.False);
            Assert.That(fixture.TryBeginWait(
                GasAbilityWaitSemantic.Level,
                (GasAbilityWaitPolicy)99,
                60,
                0,
                out var unknownPolicy,
                out _), Is.False);
            Assert.That(unknownSemantic.IsValid, Is.False);
            Assert.That(unknownPolicy.IsValid, Is.False);

            Assert.That(fixture.TryBeginWait(
                GasAbilityWaitSemantic.Event,
                GasAbilityWaitPolicy.OneShot,
                60,
                0,
                out _,
                out var registration), Is.True);
            var eventSample = GasAbilityWaitRegistrationSample.ForEvent(0);
            var wrongSemantic = registration;
            wrongSemantic.WaitSemantic = (GasAbilityWaitSemantic)99;
            var wrongSemanticStatus = fixture.SampleAndRegister(
                in wrongSemantic, in eventSample, 61, out _);
            var wrongPolicy = registration;
            wrongPolicy.WaitPolicy = (GasAbilityWaitPolicy)99;
            var wrongPolicyStatus = fixture.SampleAndRegister(
                in wrongPolicy, in eventSample, 61, out _);
            var malformed = registration;
            malformed.WaitSemantic = GasAbilityWaitSemantic.HandleLifecycle;
            malformed.ObservedHandle = fixture.CreateObservedHandle(false);
            var handleSample = GasAbilityWaitRegistrationSample.ForHandle(
                GasAbilityObservedHandleState.Live);
            var status = fixture.SampleAndRegister(
                in malformed, in handleSample, 61, out _);

            Assert.That(wrongSemanticStatus,
                Is.EqualTo(GasAbilityWaitProtocolStatus.InvalidRequest));
            Assert.That(wrongPolicyStatus,
                Is.EqualTo(GasAbilityWaitProtocolStatus.InvalidRequest));
            Assert.That(status, Is.EqualTo(GasAbilityWaitProtocolStatus.InvalidRequest));
            Assert.That(fixture.SubscriptionHighWater, Is.Zero);
        }

        /// <summary>
        /// 验证 Timer 只接受 one-shot，并在创建时按 DueTick 区分未到期与 T+1 完成。
        /// </summary>
        [Test]
        public void Timer_未到期保持Registered到期完成且Persistent被拒绝()
        {
            using var fixture = new AbilityWaitSlabFixture();
            Assert.That(fixture.TryBeginWait(
                GasAbilityWaitSemantic.Timer,
                GasAbilityWaitPolicy.OneShot,
                49,
                50,
                out var pending,
                out var pendingRoute), Is.True);
            Assert.That(fixture.TryBeginWait(
                GasAbilityWaitSemantic.Timer,
                GasAbilityWaitPolicy.OneShot,
                50,
                50,
                out var completed,
                out var completedRoute), Is.True);
            Assert.That(fixture.TryBeginWait(
                GasAbilityWaitSemantic.Timer,
                GasAbilityWaitPolicy.Persistent,
                49,
                50,
                out var rejected,
                out _), Is.False);

            var pendingSlot = fixture.GetContinuation(in pending);
            var completedSlot = fixture.GetContinuation(in completed);
            Assert.That(pendingRoute.CommandKind, Is.Zero);
            Assert.That(completedRoute.CommandKind, Is.Zero);
            Assert.That(pendingSlot.Subscription.IsValid, Is.False);
            Assert.That(pendingSlot.WaitState, Is.EqualTo(GasAbilityWaitState.Registered));
            Assert.That(completedSlot.WaitState, Is.EqualTo(GasAbilityWaitState.Completed));
            Assert.That(completedSlot.ResumeTick, Is.EqualTo(51));
            Assert.That(rejected.IsValid, Is.False);
        }

        /// <summary>
        /// 验证本地 Timer 的未到期评估、到期完成、消费回收与独立取消均不生成跨 ASC 消息。
        /// </summary>
        [Test]
        public void Timer_EvaluateConsume与Cancel形成完整本地生命周期()
        {
            using var fixture = new AbilityWaitSlabFixture();
            Assert.That(fixture.TryBeginWait(
                GasAbilityWaitSemantic.Timer,
                GasAbilityWaitPolicy.OneShot,
                70,
                80,
                out var evaluated,
                out _), Is.True);

            Assert.That(fixture.EvaluateTimer(in evaluated, 79),
                Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpNotMatched));
            Assert.That(fixture.EvaluateTimer(in evaluated, 80),
                Is.EqualTo(GasAbilityWaitProtocolStatus.Completed));
            Assert.That(fixture.GetContinuation(in evaluated).ResumeTick, Is.EqualTo(81));
            Assert.That(fixture.ConsumeCompleted(in evaluated), Is.True);
            Assert.That(fixture.EvaluateTimer(in evaluated, 81),
                Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpStale));

            Assert.That(fixture.TryBeginWait(
                GasAbilityWaitSemantic.Timer,
                GasAbilityWaitPolicy.OneShot,
                81,
                90,
                out var cancelled,
                out _), Is.True);
            Assert.That(fixture.CancelTimer(in cancelled), Is.True);
            Assert.That(fixture.CancelTimer(in cancelled), Is.False);
            Assert.That(fixture.GetContinuation(in evaluated).Header.StorageState,
                Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(fixture.GetContinuation(in cancelled).Header.StorageState,
                Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(fixture.Activation.ContinuationCount, Is.Zero);
        }

        /// <summary>
        /// 验证 one-shot completion 在两端结束后可连续复用相同物理槽且每轮递增 generation。
        /// </summary>
        [Test]
        public void OneShotCompletion_多轮回收复用槽且旧句柄失效()
        {
            using var fixture = new AbilityWaitSlabFixture();
            for (var round = 0; round < 3; round++)
            {
                fixture.CompleteAndRecycleOneShotRound(
                    (ulong)(100 + round * 10),
                    out var continuation,
                    out var subscription);

                Assert.That(continuation.SlotIndex, Is.Zero);
                Assert.That(subscription.SlotIndex, Is.Zero);
                Assert.That(continuation.SlotGeneration, Is.EqualTo((uint)(round + 1)));
                Assert.That(subscription.SlotGeneration, Is.EqualTo((uint)(round + 1)));
                Assert.That(fixture.GetContinuationAt(0).Header.StorageState,
                    Is.EqualTo(GasSlabSlotState.Free));
                Assert.That(fixture.GetSubscriptionAt(0).Header.StorageState,
                    Is.EqualTo(GasSlabSlotState.Free));
                Assert.That(fixture.WakeObserved(
                    in subscription,
                    GasAbilityWaitSignalKind.EventObserved,
                    (ulong)(104 + round * 10),
                    2,
                    out _), Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpStale));
                Assert.That(fixture.ConsumeCompleted(in continuation), Is.False);
            }

            Assert.That(fixture.ContinuationHighWater, Is.EqualTo(1));
            Assert.That(fixture.SubscriptionHighWater, Is.EqualTo(1));
            Assert.That(fixture.Activation.ContinuationCount, Is.Zero);
        }
    }

    /// <summary>
    /// 构造 owner/observed 两个完整 ASC Entity，并集中驱动两端 slab 单 writer API。
    /// </summary>
    internal sealed class AbilityWaitSlabFixture : IDisposable
    {
        private const ulong Epoch = 91;
        private const int HardCapacity = 8;
        private readonly World _world;
        private readonly Entity _ownerEntity;
        private readonly Entity _observedEntity;
        private readonly OwnerAscHandle _owner = new OwnerAscHandle(9101, 1);
        private readonly OwnerAscHandle _observed = new OwnerAscHandle(9102, 1);
        private readonly BattleInstanceHandle _battle = new BattleInstanceHandle(Epoch, 1, 1);
        private readonly AbilityActivationHandle _activation;

        /// <summary>
        /// 返回 owner 侧唯一 Activation 槽的当前值。
        /// </summary>
        internal AbilityActivationSlot Activation =>
            _world.EntityManager.GetBuffer<AbilityActivationSlot>(_ownerEntity)[0];

        /// <summary>
        /// 返回 observed Subscription slab 的当前 high-water。
        /// </summary>
        internal int SubscriptionHighWater =>
            _world.EntityManager.GetComponentData<AscSlabHeads>(_observedEntity)
                .AbilitySubscription.HighWater;

        /// <summary>
        /// 返回 owner Continuation slab 的当前 high-water。
        /// </summary>
        internal int ContinuationHighWater =>
            _world.EntityManager.GetComponentData<AscSlabHeads>(_ownerEntity)
                .AbilityContinuation.HighWater;

        /// <summary>
        /// 创建两个完整 ASC 布局并在 owner 侧发布一个 live Activation。
        /// </summary>
        internal AbilityWaitSlabFixture()
        {
            _world = new World("Runtime v1 Ability wait slab EditMode test");
            var manager = _world.EntityManager;
            var archetype = GasRuntimeV1Archetypes.CreateAsc(manager);
            _ownerEntity = manager.CreateEntity(archetype);
            _observedEntity = manager.CreateEntity(archetype);
            manager.SetComponentData(_ownerEntity, new GasAscIdentity
            {
                SimulationEpoch = Epoch,
                OwnerAsc = _owner,
            });
            manager.SetComponentData(_observedEntity, new GasAscIdentity
            {
                SimulationEpoch = Epoch,
                OwnerAsc = _observed,
            });
            Assert.That(GasRuntimeV1Archetypes.TryInitializeAscMetadata(
                manager, _ownerEntity), Is.True);
            Assert.That(GasRuntimeV1Archetypes.TryInitializeAscMetadata(
                manager, _observedEntity), Is.True);
            _activation = SeedActivation(manager);
        }

        /// <summary>
        /// 释放测试 World 及两端全部 native storage。
        /// </summary>
        public void Dispose()
        {
            _world.Dispose();
        }

        /// <summary>
        /// 在 owner Continuation slab 创建指定 wait，并回写 owner slab head。
        /// </summary>
        internal bool TryBeginWait(
            GasAbilityWaitSemantic semantic,
            GasAbilityWaitPolicy policy,
            ulong currentTick,
            ulong dueTick,
            out AbilityContinuationHandle handle,
            out PendingCommand registration)
        {
            var request = new GasAbilityContinuationCreateRequest
            {
                Activation = _activation,
                ObservedAsc = semantic == GasAbilityWaitSemantic.Timer ? default : _observed,
                Semantic = semantic,
                Policy = policy,
                CurrentTick = currentTick,
                DueTick = dueTick,
                CommandSequence = 100 + currentTick,
                ProgramCounter = 3,
                InstanceNameId = 4,
                QueryKey = 5,
            };
            var manager = _world.EntityManager;
            var heads = manager.GetComponentData<AscSlabHeads>(_ownerEntity);
            var success = GasAbilityWaitSlabTransaction.TryBeginWait(
                in request,
                HardCapacity,
                manager.GetBuffer<AbilityActivationSlot>(_ownerEntity),
                manager.GetBuffer<AbilityContinuationSlot>(_ownerEntity),
                ref heads.AbilityContinuation,
                out handle,
                out registration);
            manager.SetComponentData(_ownerEntity, heads);
            return success;
        }

        /// <summary>
        /// 在 observed writer 线性化 sample+register，并回写 Subscription slab head。
        /// </summary>
        internal GasAbilityWaitProtocolStatus SampleAndRegister(
            in PendingCommand registration,
            in GasAbilityWaitRegistrationSample sample,
            ulong currentTick,
            out PendingCommand response)
        {
            var manager = _world.EntityManager;
            var heads = manager.GetComponentData<AscSlabHeads>(_observedEntity);
            var status = GasAbilityWaitSlabTransaction.SampleAndRegisterObserved(
                in _observed,
                in registration,
                in sample,
                currentTick,
                200 + currentTick,
                HardCapacity,
                manager.GetBuffer<AbilitySubscriptionSlot>(_observedEntity),
                ref heads.AbilitySubscription,
                out response);
            manager.SetComponentData(_observedEntity, heads);
            return status;
        }

        /// <summary>
        /// 在 owner writer 应用 observed Ack 或 Completion。
        /// </summary>
        internal bool ApplyOwnerResponse(in PendingCommand response)
        {
            var manager = _world.EntityManager;
            return GasAbilityWaitSlabTransaction.ApplyOwnerResponse(
                in _owner,
                in response,
                manager.GetBuffer<AbilityActivationSlot>(_ownerEntity),
                manager.GetBuffer<AbilityContinuationSlot>(_ownerEntity));
        }

        /// <summary>
        /// 在 observed writer 驱动指定订阅，并返回跨 ASC completion 消息。
        /// </summary>
        internal GasAbilityWaitProtocolStatus WakeObserved(
            in AbilitySubscriptionHandle subscription,
            GasAbilityWaitSignalKind signalKind,
            ulong currentTick,
            ulong observedOrdinal,
            out PendingCommand response)
        {
            return GasAbilityWaitSlabTransaction.WakeObserved(
                in _observed,
                in subscription,
                signalKind,
                currentTick,
                observedOrdinal,
                _world.EntityManager.GetBuffer<AbilitySubscriptionSlot>(_observedEntity),
                out response);
        }

        /// <summary>
        /// 在 owner writer 推进 handshake generation 并创建 unsubscribe。
        /// </summary>
        internal bool TryCancel(
            in AbilityContinuationHandle continuation,
            ulong currentTick,
            out PendingCommand unsubscribe)
        {
            var manager = _world.EntityManager;
            return GasAbilityWaitSlabTransaction.TryCancelOwnerWait(
                in continuation,
                currentTick,
                300 + currentTick,
                manager.GetBuffer<AbilityActivationSlot>(_ownerEntity),
                manager.GetBuffer<AbilityContinuationSlot>(_ownerEntity),
                out unsubscribe);
        }

        /// <summary>
        /// 在 observed writer 结束订阅并回写 Subscription slab head。
        /// </summary>
        internal bool UnsubscribeObserved(
            in PendingCommand unsubscribe,
            ulong currentTick,
            out PendingCommand response)
        {
            var manager = _world.EntityManager;
            var heads = manager.GetComponentData<AscSlabHeads>(_observedEntity);
            var success = GasAbilityWaitSlabTransaction.UnsubscribeObserved(
                in _observed,
                in unsubscribe,
                currentTick,
                HardCapacity,
                manager.GetBuffer<AbilitySubscriptionSlot>(_observedEntity),
                ref heads.AbilitySubscription,
                out response);
            manager.SetComponentData(_observedEntity, heads);
            return success;
        }

        /// <summary>
        /// 在 owner writer 应用 cancel Ack，并回写 Continuation slab head。
        /// </summary>
        internal bool ApplyCancelAck(in PendingCommand response)
        {
            var manager = _world.EntityManager;
            var heads = manager.GetComponentData<AscSlabHeads>(_ownerEntity);
            var success = GasAbilityWaitSlabTransaction.ApplyCancelAckOwner(
                in _owner,
                in response,
                manager.GetBuffer<AbilityActivationSlot>(_ownerEntity),
                manager.GetBuffer<AbilityContinuationSlot>(_ownerEntity),
                ref heads.AbilityContinuation);
            manager.SetComponentData(_ownerEntity, heads);
            return success;
        }

        /// <summary>
        /// 在 owner writer 评估本地 Timer 是否到期。
        /// </summary>
        internal GasAbilityWaitProtocolStatus EvaluateTimer(
            in AbilityContinuationHandle continuation,
            ulong currentTick)
        {
            return GasAbilityWaitSlabTransaction.EvaluateTimer(
                in continuation,
                currentTick,
                _world.EntityManager.GetBuffer<AbilityContinuationSlot>(_ownerEntity));
        }

        /// <summary>
        /// 消费 owner 侧已完成 one-shot，并回写 Continuation slab head。
        /// </summary>
        internal bool ConsumeCompleted(in AbilityContinuationHandle continuation)
        {
            var manager = _world.EntityManager;
            var heads = manager.GetComponentData<AscSlabHeads>(_ownerEntity);
            var success = GasAbilityWaitSlabTransaction.TryConsumeCompletedOwnerWait(
                in continuation,
                manager.GetBuffer<AbilityActivationSlot>(_ownerEntity),
                manager.GetBuffer<AbilityContinuationSlot>(_ownerEntity),
                ref heads.AbilityContinuation);
            manager.SetComponentData(_ownerEntity, heads);
            return success;
        }

        /// <summary>
        /// 取消 owner 侧未完成 Timer，并回写 Continuation slab head。
        /// </summary>
        internal bool CancelTimer(in AbilityContinuationHandle continuation)
        {
            var manager = _world.EntityManager;
            var heads = manager.GetComponentData<AscSlabHeads>(_ownerEntity);
            var success = GasAbilityWaitSlabTransaction.TryCancelLocalTimerWait(
                in continuation,
                manager.GetBuffer<AbilityActivationSlot>(_ownerEntity),
                manager.GetBuffer<AbilityContinuationSlot>(_ownerEntity),
                ref heads.AbilityContinuation);
            manager.SetComponentData(_ownerEntity, heads);
            return success;
        }

        /// <summary>
        /// 在 observed writer 把 one-shot completion 对应 Subscription 固定为 tombstone。
        /// </summary>
        internal bool FinalizeObservedCompletion(
            in AbilitySubscriptionHandle subscription)
        {
            var manager = _world.EntityManager;
            var heads = manager.GetComponentData<AscSlabHeads>(_observedEntity);
            var success = GasAbilityWaitSlabTransaction.FinalizeObservedCompletion(
                in subscription,
                manager.GetBuffer<AbilitySubscriptionSlot>(_observedEntity),
                ref heads.AbilitySubscription);
            manager.SetComponentData(_observedEntity, heads);
            return success;
        }

        /// <summary>
        /// 创建 persistent wait，完成 observed 注册与 owner Ack 后返回两端稳定句柄。
        /// </summary>
        internal void BeginAndRegisterPersistent(
            GasAbilityWaitSemantic semantic,
            ulong beginTick,
            ulong baseline,
            out AbilityContinuationHandle continuation,
            out AbilitySubscriptionHandle subscription)
        {
            Assert.That(TryBeginWait(
                semantic,
                GasAbilityWaitPolicy.Persistent,
                beginTick,
                0,
                out continuation,
                out var registration), Is.True);
            var sample = CreateOrdinalSample(semantic, baseline);
            var status = SampleAndRegister(
                in registration,
                in sample,
                beginTick + 1,
                out var ack);
            Assert.That(status, Is.EqualTo(GasAbilityWaitProtocolStatus.Registered));
            Assert.That(ApplyOwnerResponse(in ack), Is.True);
            subscription = ack.Subscription;
        }

        /// <summary>
        /// 创建 one-shot wait，完成注册 Ack 并保留该旧 generation Ack 供迟到消息验证。
        /// </summary>
        internal void BeginAndRegisterOneShot(
            GasAbilityWaitSemantic semantic,
            ulong beginTick,
            ulong baseline,
            out AbilityContinuationHandle continuation,
            out AbilitySubscriptionHandle subscription,
            out PendingCommand ack)
        {
            Assert.That(TryBeginWait(
                semantic,
                GasAbilityWaitPolicy.OneShot,
                beginTick,
                0,
                out continuation,
                out var registration), Is.True);
            var sample = CreateOrdinalSample(semantic, baseline);
            var status = SampleAndRegister(
                in registration,
                in sample,
                beginTick + 1,
                out ack);
            Assert.That(status, Is.EqualTo(GasAbilityWaitProtocolStatus.Registered));
            Assert.That(ApplyOwnerResponse(in ack), Is.True);
            subscription = ack.Subscription;
        }

        /// <summary>
        /// 完成一轮 Event one-shot 的 observed finalize、owner consume 与下一 Tick maintenance 回收。
        /// </summary>
        internal void CompleteAndRecycleOneShotRound(
            ulong beginTick,
            out AbilityContinuationHandle continuation,
            out AbilitySubscriptionHandle subscription)
        {
            BeginAndRegisterOneShot(
                GasAbilityWaitSemantic.Event,
                beginTick,
                0,
                out continuation,
                out subscription,
                out _);
            var status = WakeObserved(
                in subscription,
                GasAbilityWaitSignalKind.EventObserved,
                beginTick + 2,
                1,
                out var completion);
            Assert.That(status, Is.EqualTo(GasAbilityWaitProtocolStatus.Completed));
            Assert.That(FinalizeObservedCompletion(in subscription), Is.True);
            Assert.That(ApplyOwnerResponse(in completion), Is.True);
            Assert.That(ConsumeCompleted(in continuation), Is.True);
            RecycleTombstones(beginTick + 3);
        }

        /// <summary>
        /// 运行两端标准 pre-command maintenance，回收上一 Tick 的 wait tombstones。
        /// </summary>
        internal void RecycleTombstones(ulong candidateTick)
        {
            Assert.That(RecycleEntityTombstones(_ownerEntity, candidateTick), Is.True);
            Assert.That(RecycleEntityTombstones(_observedEntity, candidateTick), Is.True);
        }

        /// <summary>
        /// 读取并复验指定 owner Continuation 稳定槽。
        /// </summary>
        internal AbilityContinuationSlot GetContinuation(
            in AbilityContinuationHandle continuation)
        {
            var slot = _world.EntityManager
                .GetBuffer<AbilityContinuationSlot>(_ownerEntity)[continuation.SlotIndex];
            Assert.That(slot.Handle, Is.EqualTo(continuation));
            return slot;
        }

        /// <summary>
        /// 读取并复验指定 observed Subscription 稳定槽。
        /// </summary>
        internal AbilitySubscriptionSlot GetSubscription(
            in AbilitySubscriptionHandle subscription)
        {
            var slot = _world.EntityManager
                .GetBuffer<AbilitySubscriptionSlot>(_observedEntity)[subscription.SlotIndex];
            Assert.That(slot.Handle, Is.EqualTo(subscription));
            return slot;
        }

        /// <summary>
        /// 按物理索引读取 owner Continuation 槽，不要求当前 generation 仍与旧句柄相同。
        /// </summary>
        internal AbilityContinuationSlot GetContinuationAt(int slotIndex)
        {
            return _world.EntityManager
                .GetBuffer<AbilityContinuationSlot>(_ownerEntity)[slotIndex];
        }

        /// <summary>
        /// 按物理索引读取 observed Subscription 槽，供 generation fence 与回收状态断言。
        /// </summary>
        internal AbilitySubscriptionSlot GetSubscriptionAt(int slotIndex)
        {
            return _world.EntityManager
                .GetBuffer<AbilitySubscriptionSlot>(_observedEntity)[slotIndex];
        }

        /// <summary>
        /// 创建 observed 或 foreign owner 上结构有效的 typed observed handle。
        /// </summary>
        internal GasAbilityObservedHandle CreateObservedHandle(bool useObservedOwner)
        {
            var owner = useObservedOwner ? _observed : _owner;
            var handle = new ActiveEffectHandle(Epoch, owner, 0, 1);
            return GasAbilityObservedHandle.From(in handle);
        }

        /// <summary>
        /// 在 owner 侧写入唯一 live Activation 并初始化对应 slab high-water。
        /// </summary>
        private AbilityActivationHandle SeedActivation(EntityManager manager)
        {
            var handle = new AbilityActivationHandle(Epoch, _owner, 0, 1);
            manager.GetBuffer<AbilityActivationSlot>(_ownerEntity).Add(new AbilityActivationSlot
            {
                Header = GasSlabSlotHeader.CreateLive(1),
                Handle = handle,
                Phase = GasAbilityActivationPhase.Committed,
            });
            var heads = manager.GetComponentData<AscSlabHeads>(_ownerEntity);
            heads.AbilityActivation.HighWater = 1;
            manager.SetComponentData(_ownerEntity, heads);
            return handle;
        }

        /// <summary>
        /// 对指定完整 ASC 运行标准 pre-command tombstone 回收。
        /// </summary>
        private bool RecycleEntityTombstones(Entity entity, ulong candidateTick)
        {
            var manager = _world.EntityManager;
            var heads = manager.GetComponentData<AscSlabHeads>(entity);
            var success = GasAbilityLifecycleMaintenance.RunPreCommand(
                candidateTick,
                in _battle,
                manager.GetBuffer<GrantedAbilitySlot>(entity),
                manager.GetBuffer<AbilityActivationSlot>(entity),
                manager.GetBuffer<AbilityContinuationSlot>(entity),
                manager.GetBuffer<AbilitySubscriptionSlot>(entity),
                manager.GetBuffer<PendingCommand>(entity),
                ref heads);
            manager.SetComponentData(entity, heads);
            return success;
        }

        /// <summary>
        /// 按 Edge/Event 语义创建不回放历史的 ordinal sample。
        /// </summary>
        private static GasAbilityWaitRegistrationSample CreateOrdinalSample(
            GasAbilityWaitSemantic semantic,
            ulong baseline)
        {
            return semantic == GasAbilityWaitSemantic.Edge
                ? GasAbilityWaitRegistrationSample.ForEdge(baseline)
                : GasAbilityWaitRegistrationSample.ForEvent(baseline);
        }
    }
}
