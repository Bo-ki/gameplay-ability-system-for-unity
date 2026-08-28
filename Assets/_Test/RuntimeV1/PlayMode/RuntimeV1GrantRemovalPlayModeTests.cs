using GAS.Runtime;
using NUnit.Framework;

namespace GAS.RuntimeV1.Tests.PlayMode
{
    /// <summary>
    /// 通过正式 Tick DAG 验证 grant removal 三态及其与同 Tick Ability 命令的冻结交互。
    /// </summary>
    [TestFixture]
    public class RuntimeV1GrantRemovalPlayModeTests
    {
        private const ulong SourceStableId = 13001;
        private const ulong ApplicationId = 13002;
        private const ulong ContextId = 13003;

        /// <summary>
        /// 验证 due CancelImmediately 在 Commit 前建立 Ending barrier，且不新增 cost/cooldown 工作。
        /// </summary>
        [Test]
        public void CancelImmediately_同TickCommit被EndingBarrier拒绝且零提交副作用()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(withAbility: true);
            var grant = fixture.GrantedAbilities[0].Handle;
            Assert.That(fixture.SubmitActivate(1201, grant).IsAccepted, Is.True);
            fixture.TickBatch();
            var activationHandle = fixture.Activations[0].Handle;

            fixture.EnqueueGrantedRemoval(
                1202, in grant, GasGrantedAbilityRemovalPolicy.CancelImmediately);
            Assert.That(fixture.SubmitCommit(1203, activationHandle).IsAccepted, Is.True);
            fixture.TickBatch();

            var activation = fixture.Activations[activationHandle.SlotIndex];
            var removedGrant = fixture.GrantedAbilities[grant.SlotIndex];
            Assert.That(activation.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(activation.Phase, Is.EqualTo(GasAbilityActivationPhase.Ended));
            Assert.That(activation.EndReason, Is.EqualTo(GasAbilityEndReason.GrantRemoved));
            Assert.That(activation.CommitSequence, Is.Zero);
            Assert.That(removedGrant.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(100f));
            Assert.That(fixture.Cooldowns.Length, Is.Zero);
        }

        /// <summary>
        /// 验证 RemoveWhenAllActivationsEnd 同 Tick 阻止新 Activate，并等待既有 child 独立结束。
        /// </summary>
        [Test]
        public void RemoveWhenAllActivationsEnd_阻止新激活且保留既有Child至独立结束()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(withAbility: true);
            var grant = fixture.GrantedAbilities[0].Handle;
            Assert.That(fixture.SubmitActivate(1211, grant).IsAccepted, Is.True);
            fixture.TickBatch();
            var existing = fixture.Activations[0].Handle;

            fixture.EnqueueGrantedRemoval(
                1212, in grant, GasGrantedAbilityRemovalPolicy.RemoveWhenAllActivationsEnd);
            Assert.That(fixture.SubmitActivate(1213, grant).IsAccepted, Is.True);
            fixture.TickBatch();

            var waitingGrant = fixture.GrantedAbilities[grant.SlotIndex];
            var retained = fixture.Activations[existing.SlotIndex];
            Assert.That(waitingGrant.Header.StorageState, Is.EqualTo(GasSlabSlotState.Live));
            Assert.That(waitingGrant.RemovalState,
                Is.EqualTo(GasGrantedAbilityRemovalState.PendingRemove));
            Assert.That(waitingGrant.ChildActivationCount, Is.EqualTo(1));
            Assert.That(retained.Phase,
                Is.EqualTo(GasAbilityActivationPhase.RunningUncommitted));
            Assert.That(fixture.Activations.Length, Is.EqualTo(1));

            Assert.That(fixture.SubmitCancel(1214, existing).IsAccepted, Is.True);
            fixture.TickBatch();
            Assert.That(fixture.Activations[existing.SlotIndex].EndReason,
                Is.EqualTo(GasAbilityEndReason.Cancelled));
            Assert.That(fixture.Activations[existing.SlotIndex].Header.StorageState,
                Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(fixture.GrantedAbilities[grant.SlotIndex].Header.StorageState,
                Is.EqualTo(GasSlabSlotState.Tombstone));
        }

        /// <summary>
        /// 验证 LeaveGranted 清除 effect handle、保留审计 provenance，并允许同 Tick Activate。
        /// </summary>
        [Test]
        public void LeaveGranted_脱钩Provenance后同Tick仍可激活()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(withAbility: true);
            var grant = fixture.GrantedAbilities[0].Handle;
            fixture.AttachActiveEffectProvenance(
                in grant, SourceStableId, ApplicationId, ContextId);
            fixture.EnqueueGrantedRemoval(
                1221, in grant, GasGrantedAbilityRemovalPolicy.LeaveGranted);
            Assert.That(fixture.SubmitActivate(1222, grant).IsAccepted, Is.True);

            fixture.TickBatch();

            var detached = fixture.GrantedAbilities[grant.SlotIndex];
            Assert.That(detached.Header.StorageState, Is.EqualTo(GasSlabSlotState.Live));
            Assert.That(detached.RemovalState,
                Is.EqualTo(GasGrantedAbilityRemovalState.ProvenanceDetached));
            Assert.That(detached.ProvenanceDetached, Is.EqualTo(1));
            Assert.That(detached.GrantingActiveEffect.IsValid, Is.False);
            Assert.That(detached.GrantSourceStableId, Is.EqualTo(SourceStableId));
            Assert.That(detached.GrantApplicationId, Is.EqualTo(ApplicationId));
            Assert.That(detached.GrantContextId, Is.EqualTo(ContextId));
            Assert.That(detached.ChildActivationCount, Is.EqualTo(1));
            Assert.That(fixture.Activations[0].Header.StorageState,
                Is.EqualTo(GasSlabSlotState.Live));
            Assert.That(fixture.Activations[0].Phase,
                Is.EqualTo(GasAbilityActivationPhase.RunningUncommitted));
        }
    }
}
