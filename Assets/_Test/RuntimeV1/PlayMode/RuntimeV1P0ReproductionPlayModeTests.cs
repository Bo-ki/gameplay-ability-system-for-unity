using GAS.Runtime;
using NUnit.Framework;
using Unity.Entities;

namespace GAS.RuntimeV1.Tests.PlayMode
{
    /// <summary>
    /// 保存 N2-R0 已确认 P0 的目标语义复现；Explicit 用例只供精确领取，禁止计入默认 Green。
    /// </summary>
    [TestFixture]
    public class RuntimeV1P0ReproductionPlayModeTests
    {
        /// <summary>
        /// 验证 Battle 终局一经 Core 发布，outer fence 前到达的 tail 也必须同步拒绝。
        /// </summary>
        [Test]
        [Explicit("N2-R0 SYS-02 defect reproduction; current implementation is expected to fail.")]
        public void SYS02_终局发布后OuterFence前Tail必须同步拒绝()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(
                lethalEffect: true,
                withRemoteAsc: true);
            SetOpposingFactions(fixture);

            Assert.That(
                fixture.SubmitApplyEffect(9001, fixture.CurrentTick).IsAccepted,
                Is.True);

            var world = fixture.EntityManager.World;
            world.GetExistingSystemManaged<GasFixedTickSystemGroup>().Update();
            fixture.EntityManager.CompleteAllTrackedJobs();

            var battle = fixture.EntityManager.GetBuffer<BattleInstanceSlot>(fixture.Session)[0];
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(GasAscLifecycleState.Dead));
            Assert.That(battle.State, Is.EqualTo(GasBattleInstanceState.OutcomeFrozen));
            Assert.That(battle.IngressClosed, Is.EqualTo(1));
            Assert.That(
                fixture.SessionLifecycle.State,
                Is.EqualTo(GasSessionLifecycleState.Terminalizing));

            var tail = fixture.SubmitApplyEffect(9002, fixture.CurrentTick);
            Assert.That(
                tail.IsAccepted,
                Is.False,
                "Core 已发布终局后，managed Gate 仍按旧 authority 接受 tail。当前缺口已复现。");
        }

        /// <summary>
        /// 将两个测试 ASC 分到对立阵营，使单方死亡可触发真实 Battle terminal。
        /// </summary>
        private static void SetOpposingFactions(RuntimeV1TickDagTestWorld fixture)
        {
            var ownerMembership = fixture.EntityManager.GetComponentData<AscBattleMembership>(
                fixture.Asc);
            ownerMembership.SideId = 1;
            ownerMembership.TeamId = 10;
            fixture.EntityManager.SetComponentData(fixture.Asc, ownerMembership);

            var remoteMembership = fixture.EntityManager.GetComponentData<AscBattleMembership>(
                fixture.RemoteAsc);
            remoteMembership.SideId = 2;
            remoteMembership.TeamId = 20;
            fixture.EntityManager.SetComponentData(fixture.RemoteAsc, remoteMembership);
        }
    }
}
