using GAS.Runtime;
using NUnit.Framework;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 Stage D wait 的五种语义、跨 ASC 握手、代际校验与确定性恢复时序。
    /// </summary>
    [TestFixture]
    public class RuntimeV1AbilityWaitProtocolTests
    {
        private const ulong Epoch = 91;
        private static readonly OwnerAscHandle OwnerAsc = new OwnerAscHandle(701, 3);
        private static readonly OwnerAscHandle ObservedAsc = new OwnerAscHandle(702, 5);
        private static readonly AbilityActivationHandle Activation =
            new AbilityActivationHandle(Epoch, OwnerAsc, 4, 7);
        private static readonly AbilityContinuationHandle Continuation =
            new AbilityContinuationHandle(Epoch, OwnerAsc, 6, 9);

        /// <summary>
        /// 验证协议输入、状态与消息均满足编译期 unmanaged 约束。
        /// </summary>
        [Test]
        public void 协议数据_全部为Unmanaged值类型()
        {
            Assert.That(IsUnmanaged<GasAbilityWaitRegistrationRequest>(), Is.True);
            Assert.That(IsUnmanaged<GasAbilityWaitRegistrationSample>(), Is.True);
            Assert.That(IsUnmanaged<GasAbilityWaitMessageIdentity>(), Is.True);
            Assert.That(IsUnmanaged<GasAbilityWaitSignal>(), Is.True);
            Assert.That(IsUnmanaged<GasAbilityWaitProtocolRecord>(), Is.True);
            Assert.That(IsUnmanaged<GasAbilityWaitProtocolResult>(), Is.True);
        }

        /// <summary>
        /// 验证 Level 已真在 sample tick 完成且不建订阅，未满足时才进入注册握手。
        /// </summary>
        [Test]
        public void Level_Sample已真立即完成且恢复不早于下一Tick()
        {
            var request = CreateRequest(GasAbilityWaitSemantic.Level);

            var completed = GasAbilityWaitProtocol.SampleAndRegister(
                in request,
                GasAbilityWaitRegistrationSample.ForLevel(true),
                20,
                out var completedRecord);
            var pending = GasAbilityWaitProtocol.SampleAndRegister(
                in request,
                GasAbilityWaitRegistrationSample.ForLevel(false),
                20,
                out var pendingRecord);

            AssertCompletion(completed, GasAbilityWaitCompletionReason.LevelSatisfied, 20, 21);
            Assert.That(completedRecord.LifecycleState, Is.EqualTo(GasAbilityWaitState.Completed));
            Assert.That(completedRecord.Subscription.IsValid, Is.False);
            Assert.That(pending.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.PendingRegistration));
            Assert.That(pendingRecord.LifecycleState,
                Is.EqualTo(GasAbilityWaitState.PendingRegistration));
        }

        /// <summary>
        /// 验证 Edge 与 Event 只冻结 baseline/watermark，注册后不会回放等于或早于样本的历史。
        /// </summary>
        [TestCase(
            GasAbilityWaitSemantic.Edge,
            GasAbilityWaitSignalKind.EdgeObserved,
            GasAbilityWaitCompletionReason.EdgeObserved)]
        [TestCase(
            GasAbilityWaitSemantic.Event,
            GasAbilityWaitSignalKind.EventObserved,
            GasAbilityWaitCompletionReason.EventObserved)]
        public void Edge与Event_只接受Sample之后的新序号(
            GasAbilityWaitSemantic semantic,
            GasAbilityWaitSignalKind signalKind,
            GasAbilityWaitCompletionReason expectedReason)
        {
            var sample = semantic == GasAbilityWaitSemantic.Edge
                ? GasAbilityWaitRegistrationSample.ForEdge(30)
                : GasAbilityWaitRegistrationSample.ForEvent(30);
            var record = Register(CreateRequest(semantic), sample);
            var identity = GasAbilityWaitMessageIdentity.From(in record);

            var replay = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(in identity, signalKind, 40, 30));
            var fresh = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(in identity, signalKind, 41, 31));

            Assert.That(replay.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpNotMatched));
            AssertCompletion(fresh, expectedReason, 41, 42);
            Assert.That(record.LifecycleState, Is.EqualTo(GasAbilityWaitState.Completed));
        }

        /// <summary>
        /// 验证 Handle invalid、already removed 与后续 removed 分别产生精确 completion 类型。
        /// </summary>
        [Test]
        public void HandleLifecycle_初始异常与后续移除均类型化完成()
        {
            var request = CreateRequest(GasAbilityWaitSemantic.HandleLifecycle);
            var invalid = GasAbilityWaitProtocol.SampleAndRegister(
                in request,
                GasAbilityWaitRegistrationSample.ForHandle(GasAbilityObservedHandleState.Invalid),
                8,
                out var invalidRecord);
            var removed = GasAbilityWaitProtocol.SampleAndRegister(
                in request,
                GasAbilityWaitRegistrationSample.ForHandle(GasAbilityObservedHandleState.AlreadyRemoved),
                9,
                out var removedRecord);
            var liveRecord = Register(
                request,
                GasAbilityWaitRegistrationSample.ForHandle(GasAbilityObservedHandleState.Live));
            var liveIdentity = GasAbilityWaitMessageIdentity.From(in liveRecord);
            var laterRemoved = GasAbilityWaitProtocol.Wake(
                ref liveRecord,
                new GasAbilityWaitSignal(in liveIdentity, GasAbilityWaitSignalKind.HandleRemoved, 10));

            AssertCompletion(invalid, GasAbilityWaitCompletionReason.HandleInvalid, 8, 9);
            AssertCompletion(removed, GasAbilityWaitCompletionReason.HandleAlreadyRemoved, 9, 10);
            AssertCompletion(laterRemoved, GasAbilityWaitCompletionReason.HandleRemoved, 10, 11);
            Assert.That(invalidRecord.Subscription.IsValid, Is.False);
            Assert.That(removedRecord.Subscription.IsValid, Is.False);
        }

        /// <summary>
        /// 验证 Timer 只保存 DueTick、不分配订阅，并且仅由到期检查完成。
        /// </summary>
        [Test]
        public void Timer_只按DueTick本地唤醒且不创建订阅()
        {
            var request = CreateRequest(GasAbilityWaitSemantic.Timer, dueTick: 50);
            var created = GasAbilityWaitProtocol.SampleAndRegister(
                in request,
                GasAbilityWaitRegistrationSample.ForTimer(),
                40,
                out var record);
            var identity = GasAbilityWaitMessageIdentity.From(in record);

            var wrongKind = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(in identity, GasAbilityWaitSignalKind.LevelSatisfied, 50));
            var early = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(in identity, GasAbilityWaitSignalKind.TimerDueEvaluation, 49));
            var ownerGone = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(in identity, GasAbilityWaitSignalKind.ObservedOwnerGone, 50));
            var due = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(in identity, GasAbilityWaitSignalKind.TimerDueEvaluation, 50));

            Assert.That(created.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.Registered));
            Assert.That(record.Subscription.IsValid, Is.False);
            Assert.That(wrongKind.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpNotMatched));
            Assert.That(early.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpNotMatched));
            Assert.That(ownerGone.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpNotMatched));
            AssertCompletion(due, GasAbilityWaitCompletionReason.TimerDue, 50, 51);
        }

        /// <summary>
        /// 验证没有重复周期定义的 persistent Timer 会被明确拒绝。
        /// </summary>
        [Test]
        public void Timer_Persistent组合被明确拒绝()
        {
            var request = CreateRequest(
                GasAbilityWaitSemantic.Timer,
                GasAbilityWaitPolicy.Persistent,
                50);

            var result = GasAbilityWaitProtocol.SampleAndRegister(
                in request,
                GasAbilityWaitRegistrationSample.ForTimer(),
                40,
                out var record);

            Assert.That(result.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.InvalidRequest));
            Assert.That(record.LifecycleState, Is.EqualTo(default(GasAbilityWaitState)));
        }

        /// <summary>
        /// 验证 Ack 必须同时匹配 activation、continuation 与 observed ASC 上的新订阅身份。
        /// </summary>
        [Test]
        public void Ack_拒绝任一稳定身份或Generation不匹配()
        {
            var request = CreateRequest(GasAbilityWaitSemantic.Level);
            CreatePending(request, GasAbilityWaitRegistrationSample.ForLevel(false), out var record);
            var subscription = CreateSubscription();
            var staleActivation = new AbilityActivationHandle(Epoch, OwnerAsc, 4, 8);
            var staleContinuation = new AbilityContinuationHandle(Epoch, OwnerAsc, 6, 10);
            var wrongOwnerSubscription = new AbilitySubscriptionHandle(Epoch, OwnerAsc, 2, 11);

            AssertStaleAck(ref record,
                new GasAbilityWaitMessageIdentity(in staleActivation, in Continuation, in subscription));
            AssertStaleAck(ref record,
                new GasAbilityWaitMessageIdentity(in Activation, in staleContinuation, in subscription));
            AssertStaleAck(ref record,
                new GasAbilityWaitMessageIdentity(in Activation, in Continuation, in wrongOwnerSubscription));

            var validIdentity = new GasAbilityWaitMessageIdentity(
                in Activation,
                in Continuation,
                in subscription);
            var accepted = GasAbilityWaitProtocol.AcknowledgeRegistration(ref record, in validIdentity);
            Assert.That(accepted.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.Registered));
            Assert.That(record.Subscription, Is.EqualTo(subscription));
        }

        /// <summary>
        /// 验证注册方可直接完成 PendingRegistration，取消胜出后 Ack 与 completion 都成为迟到 no-op。
        /// </summary>
        [Test]
        public void PendingRegistration_只会Ack完成或被Cancel屏障截断()
        {
            var request = CreateRequest(GasAbilityWaitSemantic.Level);
            CreatePending(request, GasAbilityWaitRegistrationSample.ForLevel(false), out var completedRecord);
            var pendingIdentity = GasAbilityWaitMessageIdentity.From(in completedRecord);
            var completed = GasAbilityWaitProtocol.CompletePendingRegistration(
                ref completedRecord,
                new GasAbilityWaitSignal(in pendingIdentity, GasAbilityWaitSignalKind.LevelSatisfied, 60));
            AssertCompletion(completed, GasAbilityWaitCompletionReason.LevelSatisfied, 60, 61);

            CreatePending(request, GasAbilityWaitRegistrationSample.ForLevel(false), out var cancelledRecord);
            var cancelIdentity = GasAbilityWaitMessageIdentity.From(in cancelledRecord);
            var cancelled = GasAbilityWaitProtocol.Cancel(ref cancelledRecord, in cancelIdentity);
            var lateAckIdentity = CreateAckIdentity();
            var lateAck = GasAbilityWaitProtocol.AcknowledgeRegistration(
                ref cancelledRecord,
                in lateAckIdentity);
            var lateCompletion = GasAbilityWaitProtocol.CompletePendingRegistration(
                ref cancelledRecord,
                new GasAbilityWaitSignal(in cancelIdentity, GasAbilityWaitSignalKind.LevelSatisfied, 61));

            Assert.That(cancelled.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.Ending));
            Assert.That(lateAck.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpStale));
            Assert.That(lateCompletion.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpStale));
        }

        /// <summary>
        /// 验证 one-shot 首次匹配后消费，后续同身份 wake 也只能成为迟到 no-op。
        /// </summary>
        [Test]
        public void OneShot_首次匹配消费且后续Wake不重复完成()
        {
            var record = Register(
                CreateRequest(GasAbilityWaitSemantic.Event),
                GasAbilityWaitRegistrationSample.ForEvent(3));
            var identity = GasAbilityWaitMessageIdentity.From(in record);
            var firstSignal = new GasAbilityWaitSignal(
                in identity,
                GasAbilityWaitSignalKind.EventObserved,
                70,
                4);

            var first = GasAbilityWaitProtocol.Wake(ref record, in firstSignal);
            var repeated = GasAbilityWaitProtocol.Wake(ref record, in firstSignal);

            Assert.That(first.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.Completed));
            Assert.That(repeated.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpStale));
            Assert.That(record.WakeOrdinal, Is.Zero);
        }

        /// <summary>
        /// 验证 persistent 唤醒序号严格递增、阻止重放，并把 ordinal 溢出暴露为无副作用失败。
        /// </summary>
        [Test]
        public void Persistent_WakeOrdinal单调且溢出不改状态()
        {
            var record = Register(
                CreateRequest(GasAbilityWaitSemantic.Event, GasAbilityWaitPolicy.Persistent),
                GasAbilityWaitRegistrationSample.ForEvent(5));
            var identity = GasAbilityWaitMessageIdentity.From(in record);

            var first = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(in identity, GasAbilityWaitSignalKind.EventObserved, 80, 6));
            var replay = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(in identity, GasAbilityWaitSignalKind.EventObserved, 81, 6));
            var second = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(in identity, GasAbilityWaitSignalKind.EventObserved, 82, 7));

            Assert.That(first.WakeOrdinal, Is.EqualTo(1));
            Assert.That(first.ResumeTick, Is.EqualTo(81));
            Assert.That(replay.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpNotMatched));
            Assert.That(second.WakeOrdinal, Is.EqualTo(2));
            Assert.That(record.LifecycleState, Is.EqualTo(GasAbilityWaitState.Registered));

            record.WakeOrdinal = ulong.MaxValue;
            var overflow = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(in identity, GasAbilityWaitSignalKind.EventObserved, 83, 8));
            Assert.That(overflow.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.WakeOrdinalOverflow));
            Assert.That(record.ObservedOrdinal, Is.EqualTo(7));
            Assert.That(record.WakeOrdinal, Is.EqualTo(ulong.MaxValue));
        }

        /// <summary>
        /// 验证所有 completion 共用 T+1，ulong 最大 tick 会明确失败且不留下半完成状态。
        /// </summary>
        [Test]
        public void Completion_Tick溢出明确失败且状态保持原子()
        {
            var request = CreateRequest(GasAbilityWaitSemantic.Level);
            var immediate = GasAbilityWaitProtocol.SampleAndRegister(
                in request,
                GasAbilityWaitRegistrationSample.ForLevel(true),
                ulong.MaxValue,
                out var emptyRecord);
            Assert.That(immediate.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.ResumeTickOverflow));
            Assert.That(emptyRecord.Activation.IsValid, Is.False);

            var registered = Register(request, GasAbilityWaitRegistrationSample.ForLevel(false));
            var before = registered;
            var identity = GasAbilityWaitMessageIdentity.From(in registered);
            var wake = GasAbilityWaitProtocol.Wake(
                ref registered,
                new GasAbilityWaitSignal(in identity, GasAbilityWaitSignalKind.LevelSatisfied, ulong.MaxValue));
            Assert.That(wake.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.ResumeTickOverflow));
            AssertRecordUnchanged(registered, before);
        }

        /// <summary>
        /// 验证 wake 与 cancel 同样核对订阅代际，Ending 之后所有迟到消息均不再生效。
        /// </summary>
        [Test]
        public void Wake与Cancel_订阅代际错误或Ending后均NoOp()
        {
            var record = Register(
                CreateRequest(GasAbilityWaitSemantic.Level),
                GasAbilityWaitRegistrationSample.ForLevel(false));
            var validIdentity = GasAbilityWaitMessageIdentity.From(in record);
            var staleSubscription = new AbilitySubscriptionHandle(Epoch, ObservedAsc, 2, 12);
            var staleIdentity = new GasAbilityWaitMessageIdentity(
                in Activation,
                in Continuation,
                in staleSubscription);
            var staleActivation = new AbilityActivationHandle(Epoch, OwnerAsc, 4, 8);
            var staleActivationIdentity = new GasAbilityWaitMessageIdentity(
                in staleActivation,
                in Continuation,
                in record.Subscription);
            var staleContinuation = new AbilityContinuationHandle(Epoch, OwnerAsc, 6, 10);
            var staleContinuationIdentity = new GasAbilityWaitMessageIdentity(
                in Activation,
                in staleContinuation,
                in record.Subscription);

            var staleWake = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(in staleIdentity, GasAbilityWaitSignalKind.LevelSatisfied, 90));
            var staleActivationWake = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(
                    in staleActivationIdentity,
                    GasAbilityWaitSignalKind.LevelSatisfied,
                    90));
            var staleCancel = GasAbilityWaitProtocol.Cancel(ref record, in staleContinuationIdentity);
            var cancel = GasAbilityWaitProtocol.Cancel(ref record, in validIdentity);
            var lateWake = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(in validIdentity, GasAbilityWaitSignalKind.LevelSatisfied, 91));
            var ended = GasAbilityWaitProtocol.FinishEnding(ref record, in validIdentity);

            Assert.That(staleWake.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpStale));
            Assert.That(staleActivationWake.Status,
                Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpStale));
            Assert.That(staleCancel.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpStale));
            Assert.That(cancel.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.Ending));
            Assert.That(lateWake.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpStale));
            Assert.That(ended.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.Ended));
            Assert.That(record.LifecycleState, Is.EqualTo(GasAbilityWaitState.Ended));
        }

        /// <summary>
        /// 验证 observed ASC 消失会产生类型化 completion，并终止 persistent 订阅。
        /// </summary>
        [Test]
        public void ObservedOwnerGone_产生类型化终止Completion()
        {
            var record = Register(
                CreateRequest(GasAbilityWaitSemantic.Edge, GasAbilityWaitPolicy.Persistent),
                GasAbilityWaitRegistrationSample.ForEdge(10));
            var identity = GasAbilityWaitMessageIdentity.From(in record);

            var result = GasAbilityWaitProtocol.Wake(
                ref record,
                new GasAbilityWaitSignal(in identity, GasAbilityWaitSignalKind.ObservedOwnerGone, 100));

            AssertCompletion(result, GasAbilityWaitCompletionReason.ObservedOwnerGone, 100, 101);
            Assert.That(result.WakeOrdinal, Is.EqualTo(1));
            Assert.That(record.LifecycleState, Is.EqualTo(GasAbilityWaitState.Completed));
        }

        /// <summary>
        /// 以指定语义创建共享稳定身份的注册请求。
        /// </summary>
        private static GasAbilityWaitRegistrationRequest CreateRequest(
            GasAbilityWaitSemantic semantic,
            GasAbilityWaitPolicy mode = GasAbilityWaitPolicy.OneShot,
            ulong dueTick = 0)
        {
            var observedAsc = semantic == GasAbilityWaitSemantic.Timer ? default : ObservedAsc;
            return new GasAbilityWaitRegistrationRequest(
                in Activation,
                in Continuation,
                in observedAsc,
                semantic,
                mode,
                dueTick);
        }

        /// <summary>
        /// 创建 PendingRegistration 并断言 sample 阶段成功。
        /// </summary>
        private static void CreatePending(
            GasAbilityWaitRegistrationRequest request,
            GasAbilityWaitRegistrationSample sample,
            out GasAbilityWaitProtocolRecord record)
        {
            var result = GasAbilityWaitProtocol.SampleAndRegister(
                in request,
                in sample,
                1,
                out record);
            Assert.That(result.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.PendingRegistration));
        }

        /// <summary>
        /// 完成 sample 与合法 Ack 并返回 Registered record。
        /// </summary>
        private static GasAbilityWaitProtocolRecord Register(
            GasAbilityWaitRegistrationRequest request,
            GasAbilityWaitRegistrationSample sample)
        {
            CreatePending(request, sample, out var record);
            var ack = CreateAckIdentity();
            var result = GasAbilityWaitProtocol.AcknowledgeRegistration(ref record, in ack);
            Assert.That(result.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.Registered));
            return record;
        }

        /// <summary>
        /// 创建 observed ASC 上合法的订阅稳定句柄。
        /// </summary>
        private static AbilitySubscriptionHandle CreateSubscription()
        {
            return new AbilitySubscriptionHandle(Epoch, ObservedAsc, 2, 11);
        }

        /// <summary>
        /// 创建包含合法订阅句柄的 Ack 身份。
        /// </summary>
        private static GasAbilityWaitMessageIdentity CreateAckIdentity()
        {
            var subscription = CreateSubscription();
            return new GasAbilityWaitMessageIdentity(in Activation, in Continuation, in subscription);
        }

        /// <summary>
        /// 断言 Ack 被判定为迟到或错误消息且 record 仍处于待注册态。
        /// </summary>
        private static void AssertStaleAck(
            ref GasAbilityWaitProtocolRecord record,
            GasAbilityWaitMessageIdentity identity)
        {
            var result = GasAbilityWaitProtocol.AcknowledgeRegistration(ref record, in identity);
            Assert.That(result.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.NoOpStale));
            Assert.That(record.LifecycleState,
                Is.EqualTo(GasAbilityWaitState.PendingRegistration));
        }

        /// <summary>
        /// 断言 completion reason、发生 tick 与统一 T+1 恢复 tick。
        /// </summary>
        private static void AssertCompletion(
            GasAbilityWaitProtocolResult result,
            GasAbilityWaitCompletionReason reason,
            ulong completionTick,
            ulong resumeTick)
        {
            Assert.That(result.Status, Is.EqualTo(GasAbilityWaitProtocolStatus.Completed));
            Assert.That(result.CompletionReason, Is.EqualTo(reason));
            Assert.That(result.CompletionTick, Is.EqualTo(completionTick));
            Assert.That(result.ResumeTick, Is.EqualTo(resumeTick));
        }

        /// <summary>
        /// 断言关键状态字段未被失败路径部分改写。
        /// </summary>
        private static void AssertRecordUnchanged(
            GasAbilityWaitProtocolRecord actual,
            GasAbilityWaitProtocolRecord expected)
        {
            Assert.That(actual.LifecycleState, Is.EqualTo(expected.LifecycleState));
            Assert.That(actual.CompletionReason, Is.EqualTo(expected.CompletionReason));
            Assert.That(actual.CompletionTick, Is.EqualTo(expected.CompletionTick));
            Assert.That(actual.ResumeTick, Is.EqualTo(expected.ResumeTick));
            Assert.That(actual.WakeOrdinal, Is.EqualTo(expected.WakeOrdinal));
            Assert.That(actual.ObservedOrdinal, Is.EqualTo(expected.ObservedOrdinal));
        }

        /// <summary>
        /// 通过泛型约束在编译期证明指定协议类型不含托管引用。
        /// </summary>
        private static bool IsUnmanaged<T>() where T : unmanaged
        {
            return true;
        }
    }
}
