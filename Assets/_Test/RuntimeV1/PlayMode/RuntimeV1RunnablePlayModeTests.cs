using GAS.AutoChessDemo;
using GAS.Runtime;
using NUnit.Framework;

namespace GAS.RuntimeV1.Tests.PlayMode
{
    /// <summary>
    /// 验证 Runtime V1 Accepted 请求在 Session fault 下恰好一次发布 terminal 并关闭 ingress。
    /// </summary>
    [TestFixture]
    [Category("RuntimeV1Runnable")]
    public class RuntimeV1RunnableBoundaryPlayModeTests
    {
        /// <summary>
        /// 验证唯一 RequestTerminal、SessionFault 身份和后续同步 FaultClosed。
        /// </summary>
        [Test]
        public void AcceptedRequests_PublishOneRequestTerminalAndCloseOnSessionFault()
        {
            var evidence = RuntimeV1RunnableScenarioRunner.RunBoundaryFaultVector();

            Assert.That(evidence.Passed, Is.True, evidence.Failure + evidence.Summary);
            Assert.That(evidence.SupportProfileAdmission, Is.EqualTo("None"));
            Assert.That(evidence.TerminalCount, Is.EqualTo(1));
            Assert.That(evidence.SessionFaultObserved, Is.True);
            Assert.That(evidence.GateClosed, Is.True);
        }

        /// <summary>
        /// 验证 unsupported Blob 在记录前返回 ProfileInvalid，且 playback 后仍可重试同一开放 Gate。
        /// </summary>
        [Test]
        public void UnsupportedBlob_StageBRejectsProfileWithoutRecordingEcb()
        {
            using var fixture = new RuntimeV1SpawnTestWorld();

            Assert.That(
                fixture.RecordTwoAscBatch(),
                Is.EqualTo(GasStageBSpawnFaultReason.ProfileInvalid));
            fixture.UpdateFixedStep();
            Assert.That(
                fixture.RecordTwoAscBatch(),
                Is.EqualTo(GasStageBSpawnFaultReason.ProfileInvalid));
        }
    }

    /// <summary>
    /// 验证 production Ability 的公开命令链和两个冻结结束原因。
    /// </summary>
    [TestFixture]
    [Category("RuntimeV1Runnable")]
    public class RuntimeV1RunnableAbilityPlayModeTests
    {
        /// <summary>
        /// 验证 Activate/Commit terminal、Completed normal end 与死亡 OwnerTerminal。
        /// </summary>
        [Test]
        public void PublicActivateCommit_NormalEndAndOwnerTerminal()
        {
            var evidence = RuntimeV1RunnableScenarioRunner.RunAbilityVector();

            Assert.That(evidence.Vector.Passed, Is.True, evidence.Failure + evidence.Vector.Summary);
            Assert.That(evidence.SupportProfileAdmission, Is.EqualTo("None"));
            Assert.That(evidence.ActivateTerminalCount, Is.EqualTo(1));
            Assert.That(evidence.CommitTerminalCount, Is.EqualTo(1));
            Assert.That(evidence.NormalEndObserved, Is.True);
            Assert.That(evidence.OwnerTerminalObserved, Is.True);
            Assert.That(evidence.Vector.SemanticHash, Has.Length.EqualTo(64));
            Assert.That(evidence.Vector.StateHash, Has.Length.EqualTo(64));
        }
    }

    /// <summary>
    /// 验证 production GameplayEffect 9203 的数值、stack 与 expiry 闭环。
    /// </summary>
    [TestFixture]
    [Category("RuntimeV1Runnable")]
    public class RuntimeV1RunnableGameplayEffectPlayModeTests
    {
        /// <summary>
        /// 验证三次终态、三档 period 数值、逐层过期与最终 active-effect 移除。
        /// </summary>
        [Test]
        public void Production9203_NumericStackPeriodExpiry()
        {
            var evidence = RuntimeV1RunnableScenarioRunner.RunGameplayEffect9203Vector();

            Assert.That(evidence.Vector.Passed, Is.True, evidence.Failure + evidence.Vector.Summary);
            Assert.That(evidence.SupportProfileAdmission, Is.EqualTo("None"));
            Assert.That(evidence.TerminalCount, Is.EqualTo(3));
            Assert.That(evidence.StackTerminalSequenceObserved, Is.True);
            Assert.That(evidence.NumericPeriodsObserved, Is.True);
            Assert.That(evidence.FinalRemovalObserved, Is.True);
            Assert.That(evidence.PeriodDeltas.Length, Is.EqualTo(11));
            for (var index = 0; index < 3; index++)
                Assert.That(evidence.PeriodDeltas[index], Is.EqualTo(-14.4f).Within(0.001f));
            for (var index = 3; index < 7; index++)
                Assert.That(evidence.PeriodDeltas[index], Is.EqualTo(-9.6f).Within(0.001f));
            for (var index = 7; index < 11; index++)
                Assert.That(evidence.PeriodDeltas[index], Is.EqualTo(-4.8f).Within(0.001f));
            Assert.That(evidence.Vector.SemanticHash, Has.Length.EqualTo(64));
            Assert.That(evidence.Vector.StateHash, Has.Length.EqualTo(64));
        }
    }

    /// <summary>
    /// 验证 production AutoChess Scale=1 标准战局达到无故障精确终局。
    /// </summary>
    [TestFixture]
    [Category("RuntimeV1Runnable")]
    public class RuntimeV1RunnableAutoChessPlayModeTests
    {
        /// <summary>
        /// 验证四 ASC、玩家胜利、敌方全灭与 Boundary 观测无 fault/invalid/drain failure。
        /// </summary>
        [Test]
        public void StandardSession_ReachesExactBattleTerminal()
        {
            var evidence = RuntimeV1RunnableScenarioRunner.RunAutoChessVector();

            Assert.That(evidence.Vector.Passed, Is.True, evidence.Failure + evidence.Vector.Summary);
            Assert.That(evidence.SupportProfileAdmission, Is.EqualTo("None"));
            Assert.That(evidence.Scale, Is.EqualTo(1));
            Assert.That(evidence.AscCount, Is.EqualTo(4));
            Assert.That(evidence.ReadyAscCount, Is.EqualTo(4));
            Assert.That(evidence.SessionState, Is.EqualTo(GasSessionLifecycleState.Terminalizing));
            Assert.That(evidence.FirstBattleState, Is.EqualTo(GasBattleInstanceState.OutcomeFrozen));
            Assert.That(evidence.FirstBattleIngressOpen, Is.False);
            Assert.That(evidence.Winner, Is.EqualTo(AutoChessTeam.Player));
            Assert.That(evidence.PlayerAliveCount, Is.GreaterThan(0));
            Assert.That(evidence.EnemyAliveCount, Is.Zero);
            Assert.That(evidence.FaultFactCount, Is.Zero);
            Assert.That(evidence.InvalidFactCount, Is.Zero);
            Assert.That(evidence.DrainFailureCount, Is.Zero);
            Assert.That(evidence.Vector.SemanticHash, Has.Length.EqualTo(64));
            Assert.That(evidence.Vector.StateHash, Has.Length.EqualTo(64));
        }
    }
}
