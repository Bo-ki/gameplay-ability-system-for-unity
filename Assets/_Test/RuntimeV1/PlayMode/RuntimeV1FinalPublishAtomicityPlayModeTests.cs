using GAS.Runtime;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.PlayMode
{
    /// <summary>
    /// 验证最后一个 Prepare 阶段失败时，统一发布令牌会阻止此前所有 durable intent 生效。
    /// </summary>
    [TestFixture]
    public class RuntimeV1FinalPublishAtomicityPlayModeTests
    {
        /// <summary>
        /// 验证 BoundaryPrepare 已冻结全部事实后触发 fatal，Target 与 Boundary 权威面仍保持零写。
        /// </summary>
        [Test]
        public void BoundaryPrepare_晚失败阻止所有Durable发布()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(withEffect: true);
            var manager = fixture.EntityManager;
            var beforeTick = fixture.CurrentTick;
            var beforeTarget = fixture.CaptureTargetAuthority();
            var beforeAscDrain = manager.GetComponentData<BoundaryDrainState>(fixture.Asc);
            var beforeSessionDrain = manager.GetComponentData<BoundaryDrainState>(fixture.Session);
            var beforeAscFacts = CopyBuffer(manager.GetBuffer<BoundaryFactBuffer>(fixture.Asc));
            var beforeSessionFacts = CopyBuffer(manager.GetBuffer<BoundaryFactBuffer>(fixture.Session));
            manager.AddComponentData(fixture.Session, new GasFinalPublishFaultInjection
            {
                Stage = GasFinalPublishPrepareStage.Boundary,
                FailAfterPreparedIntentCount = 2,
            });

            Assert.That(fixture.SubmitApplyEffect(8520, fixture.CurrentTick).IsAccepted, Is.True);
            fixture.TickBatch();

            Assert.That(fixture.CurrentTick, Is.EqualTo(beforeTick));
            fixture.AssertTargetAuthorityUnchanged(in beforeTarget);
            Assert.That(manager.GetComponentData<BoundaryDrainState>(fixture.Asc),
                Is.EqualTo(beforeAscDrain));
            Assert.That(manager.GetComponentData<BoundaryDrainState>(fixture.Session),
                Is.EqualTo(beforeSessionDrain));
            AssertBufferEquals(beforeAscFacts, manager.GetBuffer<BoundaryFactBuffer>(fixture.Asc));
            AssertBufferEquals(beforeSessionFacts, manager.GetBuffer<BoundaryFactBuffer>(fixture.Session));
            Assert.That(fixture.SessionLifecycle.State, Is.EqualTo(GasSessionLifecycleState.Faulted));
            var latch = manager.GetComponentData<SessionFaultLatch>(fixture.Session);
            Assert.That(latch.Detected, Is.EqualTo(1));
            Assert.That(
                latch.ReasonCode,
                Is.EqualTo((int)GasTickAdmissionFailureReason.PostAdmissionInvariantViolation));
            Assert.That(fixture.Diagnostics.ApplicationOutcomeCount, Is.EqualTo(1));
            Assert.That(fixture.Diagnostics.AttributeMutationCount, Is.EqualTo(1));
            Assert.That(fixture.Diagnostics.CoreFactCount, Is.EqualTo(2));
            Assert.That(fixture.Diagnostics.BoundaryFactCount, Is.Zero);
        }

        /// <summary>
        /// 将 durable buffer 复制为脱离 World 生命周期的逐值快照。
        /// </summary>
        private static T[] CopyBuffer<T>(DynamicBuffer<T> buffer)
            where T : unmanaged, IBufferElementData
        {
            var values = new T[buffer.Length];
            for (var index = 0; index < buffer.Length; index++)
                values[index] = buffer[index];
            return values;
        }

        /// <summary>
        /// 逐元素验证 durable buffer 未因失败发布发生长度或内容变化。
        /// </summary>
        private static void AssertBufferEquals<T>(T[] expected, DynamicBuffer<T> actual)
            where T : unmanaged, IBufferElementData
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (var index = 0; index < expected.Length; index++)
                Assert.That(actual[index], Is.EqualTo(expected[index]), $"buffer index {index}");
        }
    }
}
