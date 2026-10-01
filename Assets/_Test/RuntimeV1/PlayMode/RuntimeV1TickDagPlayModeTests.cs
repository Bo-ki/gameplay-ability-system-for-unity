using System;
using System.Linq;
using GAS.Runtime;
using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Physics.Systems;

namespace GAS.RuntimeV1.Tests.PlayMode
{
    /// <summary>
    /// 在完整 WorldOwner 批次边界中验证 Stage-C Ingress、单 Kernel DAG 与 WholeTick fault close。
    /// </summary>
    [TestFixture]
    public class RuntimeV1TickDagPlayModeTests
    {
        /// <summary>
        /// 验证 WorldOwner 幂等安装唯一正式父链、Ingress 与 Kernel。
        /// </summary>
        [Test]
        public void WorldOwner_幂等安装唯一正式拓扑()
        {
            using var world = new World("Runtime v1 topology PlayMode test");
            using var owner = GasRuntimeWorldOwner.Install(world);
            var repeated = GasRuntimeWorldOwner.Install(world);

            Assert.That(repeated, Is.SameAs(owner));
            AssertFormalParentOrder(world);
            AssertIngressBeforeKernel(world);
        }

        /// <summary>
        /// 验证 owner 释放与 Port accept 在同一 Gate 锁上线性化，缓存 Port 不会留下无人消费的 journal。
        /// </summary>
        [Test]
        public void WorldOwner_Dispose后缓存Port同步拒绝新请求()
        {
            using var fixture = new RuntimeV1TickDagTestWorld();
            fixture.DisposeOwner();

            var rejected = fixture.SubmitApplyEffect(601, fixture.CurrentTick);

            Assert.That(rejected.Status, Is.EqualTo(GasCommandAcceptStatus.FaultClosed));
            Assert.That(rejected.RequestSequence, Is.Zero);
        }

        /// <summary>
        /// 验证 Ready Session 的空 Tick 仍执行完整 DAG 并只推进一次 gameplay tick。
        /// </summary>
        [Test]
        public void TickDag_空Tick成功推进且执行完整Lane链()
        {
            using var fixture = new RuntimeV1TickDagTestWorld();
            var before = fixture.CurrentTick;

            fixture.TickBatch();

            Assert.That(fixture.CurrentTick, Is.EqualTo(before + 1));
            var diagnostics = fixture.Diagnostics;
            Assert.That(diagnostics.CandidateTick, Is.EqualTo(before + 1));
            Assert.That(diagnostics.AdmissionSucceeded, Is.EqualTo(1));
            Assert.That(diagnostics.SealedCommandCount, Is.Zero);
            Assert.That(diagnostics.ExecutedLaneMask, Is.EqualTo(GasTickLaneMask.AllGameplay));
            Assert.That(fixture.SessionLifecycle.State, Is.EqualTo(GasSessionLifecycleState.Running));
            Assert.That(fixture.FixedTimestep, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(fixture.MaximumDeltaTime, Is.EqualTo(0.05f).Within(0.0001f));
        }

        /// <summary>
        /// 验证 Accepted cleanup shell 在下一 Kernel prepass 记录标准 EndFixed removal，而非 outer batch 直接结构写。
        /// </summary>
        [Test]
        public void BoundaryAcceptedShell_下一Kernel经EndFixed回收()
        {
            using var fixture = new RuntimeV1TickDagTestWorld();
            var shell = fixture.CreateEmptyBoundaryShell();

            fixture.TickBatch();
            Assert.That(fixture.EntityManager.Exists(shell), Is.True);
            Assert.That(
                fixture.EntityManager.GetComponentData<BoundaryDrainState>(shell).Phase,
                Is.EqualTo(GasBoundaryDrainPhase.Accepted));
            Assert.That(fixture.AcceptedBoundaryShellCount, Is.EqualTo(1));

            fixture.TickBatch();

            Assert.That(fixture.EntityManager.Exists(shell), Is.False);
        }

        /// <summary>
        /// 验证 AvailableTick 尚未到达的命令只进入持久 inbox，不会被当前 Tick 提前 seal。
        /// </summary>
        [Test]
        public void Ingress_FutureAvailableTick不提前消费()
        {
            using var fixture = new RuntimeV1TickDagTestWorld();
            var before = fixture.CurrentTick;
            var accepted = fixture.SubmitApplyEffect(701, before + 1);

            Assert.That(accepted.Status, Is.EqualTo(GasCommandAcceptStatus.Accepted));
            fixture.TickBatch();

            Assert.That(fixture.CurrentTick, Is.EqualTo(before + 1));
            var inbox = fixture.Inbox;
            Assert.That(inbox.Length, Is.EqualTo(1));
            Assert.That(inbox[0].RequestSequence, Is.EqualTo(accepted.RequestSequence));
            Assert.That(inbox[0].State, Is.EqualTo(GasBoundaryCommandState.Pending));
            Assert.That(fixture.Diagnostics.SealedCommandCount, Is.Zero);
        }

        /// <summary>
        /// 验证一条 due 命令经 Port、journal、Ingress、Kernel 恰好消费一次并在确认后压缩。
        /// </summary>
        [Test]
        public void Ingress_Due命令经完整链恰好消费一次()
        {
            using var fixture = new RuntimeV1TickDagTestWorld();
            var dueTick = fixture.CurrentTick;
            var accepted = fixture.SubmitApplyEffect(801, dueTick);

            Assert.That(accepted.Status, Is.EqualTo(GasCommandAcceptStatus.Accepted));
            fixture.TickBatch();
            fixture.AssertSingleConsumed(accepted.RequestSequence);

            var duplicate = fixture.SubmitApplyEffect(801, dueTick);
            Assert.That(duplicate.Status, Is.EqualTo(GasCommandAcceptStatus.DuplicateAccepted));
            Assert.That(duplicate.RequestSequence, Is.EqualTo(accepted.RequestSequence));
            fixture.TickBatch();

            Assert.That(fixture.Inbox.Length, Is.Zero);
            Assert.That(fixture.CurrentTick, Is.EqualTo(dueTick + 2));
        }

        /// <summary>
        /// 验证直接 Effect 请求经过 OwnerPlan 身份分配、SourceSpec、Target transaction、CoreFact 与 ASC outbox 的完整闭环。
        /// </summary>
        [Test]
        public void EffectApplication_经SourceSpec到BoundaryFact完整闭环()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(withEffect: true);
            var accepted = fixture.SubmitApplyEffect(850, fixture.CurrentTick);

            Assert.That(accepted.IsAccepted, Is.True);
            fixture.TickBatch();

            var diagnostics = fixture.Diagnostics;
            Assert.That(diagnostics.AdmissionSucceeded, Is.EqualTo(1));
            Assert.That(diagnostics.SourceSpecCount, Is.EqualTo(1));
            Assert.That(diagnostics.ApplicationOutcomeCount, Is.EqualTo(1));
            Assert.That(diagnostics.AttributeMutationCount, Is.EqualTo(1));
            Assert.That(diagnostics.CoreFactCount, Is.EqualTo(2));
            Assert.That(diagnostics.BoundaryFactCount, Is.EqualTo(2));
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(105f));

            Assert.That(fixture.TryDequeueBoundaryBatch(out var batch), Is.True);
            Assert.That(batch.IsNoFact, Is.False);
            Assert.That(batch.Facts.Count, Is.EqualTo(2));
            var mutationFact = batch.Facts[0];
            Assert.That(mutationFact.Scope, Is.EqualTo(GasBoundaryFactScope.Asc));
            Assert.That(mutationFact.Kind, Is.EqualTo(GasBoundaryFactKind.AttributeChanged));
            Assert.That(mutationFact.EventId.OwnerKind, Is.EqualTo(GasBoundaryOwnerKind.Asc));
            Assert.That(mutationFact.EventId.OwnerSequence, Is.EqualTo(1UL));
            Assert.That(mutationFact.Payload.Integer0, Is.EqualTo(1L));
            Assert.That(mutationFact.Payload.Scalar0, Is.EqualTo(5f));
            Assert.That(mutationFact.Payload.Scalar1, Is.EqualTo(5f));
            Assert.That(mutationFact.Payload.Scalar2, Is.EqualTo(100f));
            Assert.That(mutationFact.Payload.Scalar3, Is.EqualTo(100f));
            Assert.That(mutationFact.Payload.Scalar4, Is.EqualTo(105f));
            Assert.That(mutationFact.Payload.Scalar5, Is.EqualTo(105f));
            Assert.That(mutationFact.Payload.Scalar8, Is.EqualTo(5f));
            Assert.That(mutationFact.Payload.Scalar9, Is.EqualTo(5f));
            Assert.That(mutationFact.ParentCausalityId, Is.Not.Zero);
            Assert.That(mutationFact.Payload.StableId0, Is.Not.Zero);
            Assert.That(mutationFact.Payload.StableId2, Is.Not.Zero);
            var fact = batch.Facts[1];
            Assert.That(fact.Scope, Is.EqualTo(GasBoundaryFactScope.Asc));
            Assert.That(fact.Kind, Is.EqualTo(GasBoundaryFactKind.EffectLifecycle));
            Assert.That(fact.EventId.OwnerKind, Is.EqualTo(GasBoundaryOwnerKind.Asc));
            Assert.That(fact.EventId.OwnerSequence, Is.EqualTo(2UL));
            Assert.That(
                fact.Payload.Integer0,
                Is.EqualTo((long)GasGameplayEffectApplicationOutcome.AppliedInstant));
            Assert.That(fact.Payload.StableId0, Is.Not.Zero);
        }

        /// <summary>
        /// 验证 TargetPrepare 已完成 mutation 后发生 Session fatal 时，完整 shadow 被丢弃且 durable authority 零写。
        /// </summary>
        [Test]
        public void TargetPrepare_FatalReduce丢弃完整Shadow且不写Durable()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(withEffect: true);
            var beforeTick = fixture.CurrentTick;
            var beforeTarget = fixture.CaptureTargetAuthority();
            fixture.InjectTargetPrepareFailureAfter(1);
            var accepted = fixture.SubmitApplyEffect(8510, fixture.CurrentTick);

            Assert.That(accepted.IsAccepted, Is.True);
            fixture.TickBatch();

            AssertTargetPrepareFault(
                fixture, beforeTick, in beforeTarget, expectedMutationCount: 1);
        }

        /// <summary>
        /// 验证 Duration application 已在 shadow 分配 ActiveEffect/slab 后 fatal，durable 仍完整回滚。
        /// </summary>
        [Test]
        public void TargetPrepare_DurationFatal丢弃ActiveEffect与SlabShadow()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(periodEffect: true);
            var beforeTick = fixture.CurrentTick;
            var beforeTarget = fixture.CaptureTargetAuthority();
            fixture.InjectTargetPrepareFailureAfter(1);

            Assert.That(fixture.SubmitApplyEffect(8511, fixture.CurrentTick).IsAccepted, Is.True);
            fixture.TickBatch();

            AssertTargetPrepareFault(
                fixture, beforeTick, in beforeTarget, expectedMutationCount: 0);
        }

        /// <summary>
        /// 验证 lethal application 已在 shadow 形成 Death provenance 后 fatal，生命周期业务字段不发布。
        /// </summary>
        [Test]
        public void TargetPrepare_LethalFatal丢弃DeathLifecycleShadow()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(lethalEffect: true);
            var beforeTick = fixture.CurrentTick;
            var beforeTarget = fixture.CaptureTargetAuthority();
            fixture.InjectTargetPrepareFailureAfter(1);

            Assert.That(fixture.SubmitApplyEffect(8512, fixture.CurrentTick).IsAccepted, Is.True);
            fixture.TickBatch();

            AssertTargetPrepareFault(
                fixture, beforeTick, in beforeTarget, expectedMutationCount: 1);
        }

        /// <summary>
        /// 统一验证 Prepare 后 fatal 的控制证据、完整 target gameplay 回滚与零 Boundary 发布。
        /// </summary>
        private static void AssertTargetPrepareFault(
            RuntimeV1TickDagTestWorld fixture,
            ulong beforeTick,
            in RuntimeV1TargetAuthoritySnapshot beforeTarget,
            int expectedMutationCount)
        {
            Assert.That(fixture.CurrentTick, Is.EqualTo(beforeTick));
            fixture.AssertTargetAuthorityUnchanged(in beforeTarget);
            Assert.That(fixture.SessionLifecycle.State, Is.EqualTo(GasSessionLifecycleState.Faulted));
            var latch = fixture.EntityManager.GetComponentData<SessionFaultLatch>(fixture.Session);
            Assert.That(latch.Detected, Is.EqualTo(1));
            Assert.That(
                latch.ReasonCode,
                Is.EqualTo((int)GasTickAdmissionFailureReason.PostAdmissionInvariantViolation));
            Assert.That(fixture.Diagnostics.ApplicationOutcomeCount, Is.EqualTo(1));
            Assert.That(fixture.Diagnostics.AttributeMutationCount, Is.EqualTo(expectedMutationCount));
            Assert.That(fixture.Diagnostics.BoundaryFactCount, Is.Zero);
        }

        /// <summary>
        /// 验证 Duration GameplayEffect 在 due tick 实际重放 modifier，并输出携带 current delta 的 PeriodTick fact。
        /// </summary>
        [Test]
        public void PeriodEffect_dueTick应用Modifier并输出PeriodTickFact()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(periodEffect: true);
            var accepted = fixture.SubmitApplyEffect(853, fixture.CurrentTick);

            Assert.That(accepted.IsAccepted, Is.True);
            fixture.TickBatch();
            // 冻结 9203 语义关闭 ExecuteOnApplication：首次 application 只建立
            // ActiveEffect，modifier 必须等到其绝对 NextPeriodTick 才执行。
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(100f));
            Assert.That(
                fixture.TryDequeueBoundaryBatch(out var applicationBatch),
                Is.True,
                $"first period application did not publish: tick={fixture.CurrentTick}, " +
                $"admission={fixture.Diagnostics.AdmissionSucceeded}:{fixture.Diagnostics.AdmissionReasonCode}, " +
                $"outcomes={fixture.Diagnostics.ApplicationOutcomeCount}, " +
                $"mutations={fixture.Diagnostics.AttributeMutationCount}, " +
                $"core={fixture.Diagnostics.CoreFactCount}, boundary={fixture.Diagnostics.BoundaryFactCount}");
            Assert.That(applicationBatch.Facts.Any(
                fact => fact.Kind == GasBoundaryFactKind.EffectLifecycle), Is.True);

            fixture.TickBatch();
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(97f));
            Assert.That(fixture.TryDequeueBoundaryBatch(out var periodBatch), Is.True);
            Assert.That(periodBatch.Facts.Any(
                fact => fact.Kind == GasBoundaryFactKind.PeriodTick), Is.True);

            Assert.That(fixture.Diagnostics.AdmissionSucceeded, Is.EqualTo(1));
            Assert.That(fixture.Diagnostics.ApplicationOutcomeCount, Is.EqualTo(1));
            Assert.That(fixture.Diagnostics.AttributeMutationCount, Is.EqualTo(1));
            Assert.That(fixture.Diagnostics.CoreFactCount, Is.EqualTo(2));
            Assert.That(fixture.Diagnostics.BoundaryFactCount, Is.EqualTo(2));
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(97f));
            var periodFact = periodBatch.Facts.First(
                fact => fact.Kind == GasBoundaryFactKind.PeriodTick);
            Assert.That(periodFact.Payload.Integer0, Is.EqualTo(1L));
            Assert.That(periodFact.Payload.Scalar9, Is.EqualTo(-3f));
            Assert.That(periodFact.Payload.StableId0, Is.Not.Zero);
            Assert.That(periodFact.Payload.StableId1, Is.Not.Zero);
        }

        /// <summary>
        /// 验证同一 target 的 lethal fan-in 只产生一次 death crossing，后续 AliveOnly application typed reject。
        /// </summary>
        [Test]
        public void EffectApplication_同TickDeathCrossing唯一且后续AliveOnly拒绝()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(withEffect: true, lethalEffect: true);
            var first = fixture.SubmitApplyEffect(851, fixture.CurrentTick);
            var second = fixture.SubmitApplyEffect(852, fixture.CurrentTick);

            Assert.That(first.IsAccepted, Is.True);
            Assert.That(second.IsAccepted, Is.True);
            fixture.TickBatch();

            var lifecycle = fixture.EntityManager.GetComponentData<AscLifecycle>(fixture.Asc);
            Assert.That(
                lifecycle.State,
                Is.EqualTo(GasAscLifecycleState.Dead),
                $"state={lifecycle.State}, current={fixture.Attributes[0].Current}, " +
                $"outcomes={fixture.Diagnostics.ApplicationOutcomeCount}, " +
                $"mutations={fixture.Diagnostics.AttributeMutationCount}, " +
                $"facts={fixture.Diagnostics.CoreFactCount}, " +
                $"admission={fixture.Diagnostics.AdmissionSucceeded}:{fixture.Diagnostics.AdmissionReasonCode}");
            Assert.That(lifecycle.DeathTick, Is.EqualTo(fixture.CurrentTick));
            Assert.That(lifecycle.DeathTransitionId, Is.Not.Zero);
            Assert.That(lifecycle.DeathApplicationId, Is.Not.EqualTo(lifecycle.DeathTransitionId));
            Assert.That(lifecycle.DeathOverkill, Is.EqualTo(3f));
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(0f));
            Assert.That(fixture.Diagnostics.ApplicationOutcomeCount, Is.EqualTo(2));
            Assert.That(fixture.Diagnostics.AttributeMutationCount, Is.EqualTo(1));
            Assert.That(fixture.Diagnostics.DeathFactCount, Is.EqualTo(1));
            Assert.That(fixture.Diagnostics.CoreFactCount, Is.EqualTo(4));
            Assert.That(fixture.Diagnostics.BoundaryFactCount, Is.EqualTo(4));

            Assert.That(fixture.TryDequeueBoundaryBatch(out var batch), Is.True);
            Assert.That(batch.Facts.Count, Is.EqualTo(4));
            var deathFacts = 0;
            var rejectedOutcomes = 0;
            for (var index = 0; index < batch.Facts.Count; index++)
            {
                var fact = batch.Facts[index];
                if (fact.Kind == GasBoundaryFactKind.Death)
                    deathFacts++;
                if (fact.Kind == GasBoundaryFactKind.EffectLifecycle &&
                    fact.Payload.Integer0 ==
                    (long)GasGameplayEffectApplicationOutcome.RejectedTargetLife)
                    rejectedOutcomes++;
            }
            Assert.That(deathFacts, Is.EqualTo(1));
            Assert.That(rejectedOutcomes, Is.EqualTo(1));
            var deathFact = batch.Facts.First(fact => fact.Kind == GasBoundaryFactKind.Death);
            Assert.That(deathFact.ParentCausalityId, Is.EqualTo(1UL));
            Assert.That(deathFact.SemanticId, Is.EqualTo(lifecycle.DeathTransitionId));
            Assert.That(deathFact.Payload.Kind, Is.EqualTo(GasBoundaryPayloadKind.Death));
            Assert.That(deathFact.Payload.Integer0, Is.EqualTo(1L));
            Assert.That(deathFact.Payload.Scalar0, Is.EqualTo(3f));
            Assert.That(deathFact.Payload.StableId0, Is.EqualTo(lifecycle.DeathTransitionId));
            Assert.That(deathFact.Payload.StableId1, Is.EqualTo(lifecycle.DeathApplicationId));
            Assert.That(deathFact.Payload.StableId2, Is.Not.Zero);
        }

        /// <summary>
        /// 验证 owner-plan 容量失败不推进 Tick，仍执行全部 lane，并由 outer fence 原子关闭 Gate。
        /// </summary>
        [Test]
        public void Admission_OwnerPlan容量失败零写并在OuterFence关闭Ingress()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(0);
            var before = fixture.CaptureGameplayAuthority();
            var accepted = fixture.SubmitApplyEffect(901, before.Tick.CurrentTick);

            Assert.That(accepted.Status, Is.EqualTo(GasCommandAcceptStatus.Accepted));
            fixture.TickBatch();

            fixture.AssertAdmissionFault(in before, accepted.RequestSequence);
            var rejected = fixture.SubmitApplyEffect(902, before.Tick.CurrentTick);
            var rejectedTail = fixture.SubmitApplyEffect(903, before.Tick.CurrentTick + 1);
            Assert.That(rejected.Status, Is.EqualTo(GasCommandAcceptStatus.FaultClosed));
            Assert.That(rejected.IsAccepted, Is.False);
            Assert.That(rejected.RequestSequence, Is.Zero);
            Assert.That(rejectedTail.Status, Is.EqualTo(GasCommandAcceptStatus.FaultClosed));
            Assert.That(rejectedTail.IsAccepted, Is.False);
            Assert.That(rejectedTail.RequestSequence, Is.Zero);
        }

        /// <summary>
        /// 验证 Activate 不消费资源，Commit 后同 Tick Cancel 仍保留 cost/cooldown 与已提交身份。
        /// </summary>
        [Test]
        public void Ability_ActivateCommit分离且Commit后Cancel保留已提交工作()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(withAbility: true);
            var grant = fixture.GrantedAbilities[0].Handle;

            Assert.That(fixture.SubmitActivate(1001, grant).IsAccepted, Is.True);
            fixture.TickBatch();

            var activation = fixture.Activations[0];
            Assert.That(activation.Phase, Is.EqualTo(GasAbilityActivationPhase.RunningUncommitted));
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(100f));
            Assert.That(fixture.Cooldowns.Length, Is.Zero);

            Assert.That(fixture.SubmitCommit(1002, activation.Handle).IsAccepted, Is.True);
            Assert.That(fixture.SubmitCancel(1003, activation.Handle).IsAccepted, Is.True);
            fixture.TickBatch();

            activation = fixture.Activations[0];
            Assert.That(activation.Phase, Is.EqualTo(GasAbilityActivationPhase.Ended));
            Assert.That(activation.Header.StorageState, Is.EqualTo(GasSlabSlotState.Tombstone));
            Assert.That(activation.EndReason, Is.EqualTo(GasAbilityEndReason.Cancelled));
            Assert.That(activation.WasCancelled, Is.EqualTo(1));
            Assert.That(activation.CommitSequence, Is.Not.Zero);
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(90f));
            Assert.That(fixture.Cooldowns.Length, Is.EqualTo(1));
            Assert.That(fixture.Cooldowns[0].State, Is.EqualTo(GasSlotBusinessState.Active));

            Assert.That(fixture.SubmitCommit(1004, activation.Handle).IsAccepted, Is.True);
            fixture.TickBatch();
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(90f));

            fixture.TickBatch();
            Assert.That(fixture.Cooldowns[0].Header.StorageState, Is.EqualTo(GasSlabSlotState.Free));
        }

        /// <summary>
        /// 验证 Cancel 先于 Commit 时只结束未提交 Activation，迟到 Commit 不得写入 cost 或 cooldown。
        /// </summary>
        [Test]
        public void Ability_Cancel先于Commit_拒绝迟到Commit且不产生资源写入()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(withAbility: true);
            var grant = fixture.GrantedAbilities[0].Handle;

            Assert.That(fixture.SubmitActivate(1005, grant).IsAccepted, Is.True);
            fixture.TickBatch();
            var activation = fixture.Activations[0];
            Assert.That(activation.Phase, Is.EqualTo(GasAbilityActivationPhase.RunningUncommitted));

            Assert.That(fixture.SubmitCancel(1006, activation.Handle).IsAccepted, Is.True);
            Assert.That(fixture.SubmitCommit(1007, activation.Handle).IsAccepted, Is.True);
            fixture.TickBatch();

            activation = fixture.Activations[0];
            Assert.That(activation.Phase, Is.EqualTo(GasAbilityActivationPhase.Ended));
            Assert.That(activation.EndReason, Is.EqualTo(GasAbilityEndReason.Cancelled));
            Assert.That(activation.WasCancelled, Is.EqualTo(1));
            Assert.That(activation.CommitSequence, Is.Zero);
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(100f));
            Assert.That(fixture.Cooldowns.Length, Is.Zero);
        }

        /// <summary>
        /// 验证同 ASC 两个竞争 CommitPlan 按 canonical order 读取前序 cost，失败者保持未提交且零副作用。
        /// </summary>
        [Test]
        public void Ability_竞争Commit仅首个事务成功()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(
                withAbility: true,
                costDelta: -60f,
                withCooldown: false);
            var grant = fixture.GrantedAbilities[0].Handle;
            Assert.That(fixture.SubmitActivate(1101, grant).IsAccepted, Is.True);
            Assert.That(fixture.SubmitActivate(1102, grant).IsAccepted, Is.True);
            fixture.TickBatch();

            var first = fixture.Activations[0].Handle;
            var second = fixture.Activations[1].Handle;
            Assert.That(fixture.SubmitCommit(1103, first).IsAccepted, Is.True);
            Assert.That(fixture.SubmitCommit(1104, second).IsAccepted, Is.True);
            fixture.TickBatch();

            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(40f));
            Assert.That(fixture.Activations[0].Phase, Is.EqualTo(GasAbilityActivationPhase.Committed));
            Assert.That(fixture.Activations[1].Phase,
                Is.EqualTo(GasAbilityActivationPhase.RunningUncommitted));
            Assert.That(fixture.Cooldowns.Length, Is.Zero);
        }

        /// <summary>
        /// 验证同 ASC 两个竞争 CommitPlan 按 canonical order 读取前序 cooldown，失败者不扣费。
        /// </summary>
        [Test]
        public void Ability_竞争Cooldown仅首个事务成功()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(withAbility: true, costDelta: -10f);
            var grant = fixture.GrantedAbilities[0].Handle;
            Assert.That(fixture.SubmitActivate(1111, grant).IsAccepted, Is.True);
            Assert.That(fixture.SubmitActivate(1112, grant).IsAccepted, Is.True);
            fixture.TickBatch();

            var first = fixture.Activations[0].Handle;
            var second = fixture.Activations[1].Handle;
            Assert.That(fixture.SubmitCommit(1113, first).IsAccepted, Is.True);
            Assert.That(fixture.SubmitCommit(1114, second).IsAccepted, Is.True);
            fixture.TickBatch();

            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(90f));
            Assert.That(fixture.Activations[0].Phase, Is.EqualTo(GasAbilityActivationPhase.Committed));
            Assert.That(fixture.Activations[1].Phase,
                Is.EqualTo(GasAbilityActivationPhase.RunningUncommitted));
            Assert.That(fixture.Cooldowns.Length, Is.EqualTo(1));
        }

        /// <summary>
        /// 验证 Activate 不偷跑 Commit 的 cost 条件，资源不足只在 Commit 时确定性拒绝。
        /// </summary>
        [Test]
        public void Ability_Activate不预检Cost而Commit重新裁决()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(withAbility: true, costDelta: -120f);
            var grant = fixture.GrantedAbilities[0].Handle;

            Assert.That(fixture.SubmitActivate(1201, grant).IsAccepted, Is.True);
            fixture.TickBatch();

            var activation = fixture.Activations[0];
            Assert.That(activation.Phase, Is.EqualTo(GasAbilityActivationPhase.RunningUncommitted));
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(100f));

            Assert.That(fixture.SubmitCommit(1202, activation.Handle).IsAccepted, Is.True);
            fixture.TickBatch();

            Assert.That(fixture.Activations[0].Phase,
                Is.EqualTo(GasAbilityActivationPhase.RunningUncommitted));
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(100f));
            Assert.That(fixture.Cooldowns.Length, Is.Zero);
        }

        /// <summary>
        /// 验证 source 在同 Tick 先死亡后，已经 Commit 的远端 DirectEffect 仍按冻结计划结算。
        /// </summary>
        [Test]
        public void Ability_Source在Commit后死亡_远端DirectEffect仍结算()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(committedRemoteWork: true);
            Assert.That(
                fixture.OwnerAsc.AscStableId,
                Is.LessThan(fixture.RemoteOwnerAsc.AscStableId));
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(5f));
            Assert.That(fixture.Attributes[1].Current, Is.EqualTo(100f));
            Assert.That(fixture.RemoteAttributes[0].Current, Is.EqualTo(100f));

            var grant = fixture.GrantedAbilities[0].Handle;
            Assert.That(fixture.SubmitActivate(1301, grant).IsAccepted, Is.True);
            fixture.TickBatch();
            var activation = fixture.Activations[0].Handle;

            var commit = fixture.SubmitCommitTo(1302, in activation, fixture.RemoteOwnerAsc);
            var lethal = fixture.SubmitApplyEffectFrom(
                1303,
                fixture.RemoteOwnerAsc,
                fixture.OwnerAsc);
            Assert.That(commit.IsAccepted, Is.True);
            Assert.That(lethal.IsAccepted, Is.True);
            fixture.TickBatch();

            var committed = fixture.Activations[0];
            Assert.That(committed.Phase, Is.EqualTo(GasAbilityActivationPhase.Committed));
            Assert.That(committed.CommitSequence, Is.Not.Zero);
            Assert.That(fixture.Cooldowns, Has.Length.EqualTo(1));
            Assert.That(fixture.Cooldowns[0].State, Is.EqualTo(GasSlotBusinessState.Active));
            Assert.That(fixture.Lifecycle.State, Is.EqualTo(GasAscLifecycleState.Dead));
            Assert.That(fixture.Lifecycle.DeathSourceAsc, Is.EqualTo(fixture.RemoteOwnerAsc));
            Assert.That(fixture.Lifecycle.DeathOverkill, Is.EqualTo(3f));
            Assert.That(fixture.Attributes[0].Current, Is.Zero);
            Assert.That(fixture.Attributes[1].Base, Is.EqualTo(99f));
            Assert.That(fixture.Attributes[1].Current, Is.EqualTo(99f));
            var remoteState = fixture.RemoteLifecycle.State;
            Assert.That(
                remoteState == GasAscLifecycleState.Ready ||
                remoteState == GasAscLifecycleState.Alive,
                Is.True);
            Assert.That(fixture.RemoteAttributes[0].Current, Is.EqualTo(92f));
            Assert.That(fixture.Diagnostics.SourceSpecCount, Is.EqualTo(2));
            Assert.That(fixture.Diagnostics.ApplicationOutcomeCount, Is.EqualTo(2));
            Assert.That(fixture.Diagnostics.AttributeMutationCount, Is.EqualTo(2));
            Assert.That(fixture.Diagnostics.DeathFactCount, Is.EqualTo(1));
            AssertCommittedRemoteFacts(fixture, committed.CommitSequence);
        }

        /// <summary>
        /// 验证 Self Ability 的远端显式 target 被 owner plan 拒绝，不能静默回退 self 并消费资源。
        /// </summary>
        [Test]
        public void Ability_Self携远端显式Target_拒绝且不消费资源()
        {
            using var fixture = new RuntimeV1TickDagTestWorld(
                withAbility: true,
                withRemoteAsc: true);
            var grant = fixture.GrantedAbilities[0].Handle;
            Assert.That(fixture.SubmitActivate(1311, grant).IsAccepted, Is.True);
            fixture.TickBatch();
            var activation = fixture.Activations[0].Handle;

            Assert.That(
                fixture.SubmitCommitTo(1312, in activation, fixture.RemoteOwnerAsc).IsAccepted,
                Is.True);
            fixture.TickBatch();

            Assert.That(fixture.Activations[0].Phase,
                Is.EqualTo(GasAbilityActivationPhase.RunningUncommitted));
            Assert.That(fixture.Activations[0].CommitSequence, Is.Zero);
            Assert.That(fixture.Attributes[0].Current, Is.EqualTo(100f));
            Assert.That(fixture.RemoteAttributes[0].Current, Is.EqualTo(100f));
            Assert.That(fixture.Cooldowns.Length, Is.Zero);
            Assert.That(fixture.Diagnostics.SourceSpecCount, Is.Zero);
            Assert.That(fixture.Diagnostics.ApplicationOutcomeCount, Is.Zero);
        }

        /// <summary>
        /// 聚合双 ASC outbox，验证远端应用、source death 与 Commit causality 均形成唯一事实。
        /// </summary>
        private static void AssertCommittedRemoteFacts(
            RuntimeV1TickDagTestWorld fixture,
            ulong commitSequence)
        {
            var factCount = 0;
            var deathFactCount = 0;
            var remoteMutationCount = 0;
            var remoteAppliedCount = 0;
            while (fixture.TryDequeueBoundaryBatch(out var batch))
            {
                factCount += batch.Facts.Count;
                for (var index = 0; index < batch.Facts.Count; index++)
                {
                    var fact = batch.Facts[index];
                    if (fact.Kind == GasBoundaryFactKind.Death)
                    {
                        deathFactCount++;
                        Assert.That(fact.SourceAsc, Is.EqualTo(fixture.RemoteOwnerAsc));
                        Assert.That(fact.TargetAsc, Is.EqualTo(fixture.OwnerAsc));
                    }
                    if (fact.Kind == GasBoundaryFactKind.AttributeChanged &&
                        fact.TargetAsc.Equals(fixture.RemoteOwnerAsc))
                    {
                        remoteMutationCount++;
                        Assert.That(fact.SourceAsc, Is.EqualTo(fixture.OwnerAsc));
                        Assert.That(fact.Payload.Scalar0, Is.EqualTo(-8f));
                    }
                    if (fact.Kind == GasBoundaryFactKind.EffectLifecycle &&
                        fact.TargetAsc.Equals(fixture.RemoteOwnerAsc) &&
                        fact.Payload.Integer0 ==
                        (long)GasGameplayEffectApplicationOutcome.AppliedInstant)
                    {
                        remoteAppliedCount++;
                        Assert.That(fact.SourceAsc, Is.EqualTo(fixture.OwnerAsc));
                        Assert.That(fact.ParentCausalityId, Is.EqualTo(commitSequence));
                    }
                }
            }
            Assert.That(factCount, Is.EqualTo(5));
            Assert.That(deathFactCount, Is.EqualTo(1));
            Assert.That(remoteMutationCount, Is.EqualTo(1));
            Assert.That(remoteAppliedCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 验证 FixedStep 的完整系统句柄顺序严格满足 Physics、GAS、标准 EndFixed。
        /// </summary>
        private static void AssertFormalParentOrder(World world)
        {
            var fixedStep = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            var physics = world.GetExistingSystemManaged<PhysicsSystemGroup>();
            var gas = world.GetExistingSystemManaged<GasFixedTickSystemGroup>();
            var endFixed = world.GetExistingSystemManaged<
                EndFixedStepSimulationEntityCommandBufferSystem>();
            using var systems = fixedStep.GetAllSystems();
            var physicsIndex = FindSystemIndex(systems, physics.SystemHandle);
            var gasIndex = FindSystemIndex(systems, gas.SystemHandle);
            var endFixedIndex = FindSystemIndex(systems, endFixed.SystemHandle);

            Assert.That(physicsIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(gasIndex, Is.GreaterThan(physicsIndex));
            Assert.That(endFixedIndex, Is.GreaterThan(gasIndex));
        }

        /// <summary>
        /// 验证 GAS 子组中 managed Ingress 位于唯一 unmanaged Kernel 前。
        /// </summary>
        private static void AssertIngressBeforeKernel(World world)
        {
            var gas = world.GetExistingSystemManaged<GasFixedTickSystemGroup>();
            var ingress = world.GetExistingSystemManaged<GasCommandIngressSystem>();
            var kernel = world.GetExistingSystem<GasTickKernelSystem>();
            using var systems = gas.GetAllSystems();
            var ingressIndex = FindSystemIndex(systems, ingress.SystemHandle);
            var kernelIndex = FindSystemIndex(systems, kernel);

            Assert.That(ingressIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(kernelIndex, Is.GreaterThan(ingressIndex));
        }

        /// <summary>
        /// 返回已排序系统句柄在父组中的位置，未注册时返回 -1。
        /// </summary>
        private static int FindSystemIndex(
            NativeList<SystemHandle> systems,
            SystemHandle expected)
        {
            for (var index = 0; index < systems.Length; index++)
            {
                if (systems[index].Equals(expected))
                    return index;
            }
            return -1;
        }
    }

    /// <summary>
    /// 使用正式 WorldOwner 与 Stage-B recorder 构造一战局、可选双 ASC 的最小 Ready Session。
    /// </summary>
    internal sealed class RuntimeV1TickDagTestWorld : IDisposable
    {
        private const ulong Epoch = 61;
        private const ulong SpawnBatchId = 601;
        private const ulong SchemaHash = 6101;
        private const int EffectDefinitionId = 6201;
        private const float FixedDeltaTime = 0.05f;

        private readonly BlobAssetReference<GasDefinitionCatalogBlob> _catalog;
        private readonly World _world;
        private readonly GasRuntimeWorldOwner _owner;
        private double _elapsedTime = -FixedDeltaTime;
        private GasStageBBootstrapRecordGate _recordGate;
        private readonly bool _withAbility;
        private readonly bool _withEffect;
        private readonly bool _lethalEffect;
        private readonly bool _periodEffect;
        private readonly bool _withRemoteAsc;
        private readonly bool _committedRemoteWork;

        internal Entity Session { get; private set; }
        internal Entity Asc { get; private set; }
        internal Entity RemoteAsc { get; private set; }
        internal AscLifecycle Lifecycle => EntityManager.GetComponentData<AscLifecycle>(Asc);
        internal AscLifecycle RemoteLifecycle => EntityManager.GetComponentData<AscLifecycle>(RemoteAsc);
        internal BattleInstanceHandle Battle { get; } = new BattleInstanceHandle(Epoch, 71, 1);
        internal OwnerAscHandle OwnerAsc { get; } = new OwnerAscHandle(101, 1);
        internal OwnerAscHandle RemoteOwnerAsc { get; } = new OwnerAscHandle(202, 1);

        internal ulong CurrentTick => EntityManager.GetComponentData<SimulationTickState>(Session).CurrentTick;
        internal GasTickDiagnostics Diagnostics => EntityManager.GetComponentData<GasTickDiagnostics>(Session);
        internal GasSessionLifecycle SessionLifecycle => EntityManager.GetComponentData<GasSessionLifecycle>(Session);
        internal DynamicBuffer<BoundaryCommandInbox> Inbox => EntityManager.GetBuffer<BoundaryCommandInbox>(Session);
        internal DynamicBuffer<GrantedAbilitySlot> GrantedAbilities => EntityManager.GetBuffer<GrantedAbilitySlot>(Asc);
        internal DynamicBuffer<AbilityActivationSlot> Activations => EntityManager.GetBuffer<AbilityActivationSlot>(Asc);
        internal DynamicBuffer<CooldownGateSlot> Cooldowns => EntityManager.GetBuffer<CooldownGateSlot>(Asc);
        internal DynamicBuffer<ActiveEffectSlot> ActiveEffects => EntityManager.GetBuffer<ActiveEffectSlot>(Asc);
        internal DynamicBuffer<AttributeValueSlot> Attributes => EntityManager.GetBuffer<AttributeValueSlot>(Asc);
        internal DynamicBuffer<AttributeValueSlot> RemoteAttributes =>
            EntityManager.GetBuffer<AttributeValueSlot>(RemoteAsc);
        internal float FixedTimestep => _world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().Timestep;
        internal float MaximumDeltaTime => _world.MaximumDeltaTime;

        internal EntityManager EntityManager => _world.EntityManager;

        internal int AcceptedBoundaryShellCount
        {
            get
            {
                using var query = EntityManager.CreateEntityQuery(
                    new EntityQueryDesc
                    {
                        All = new[]
                        {
                            ComponentType.ReadOnly<BoundaryDrainState>(),
                            ComponentType.ReadOnly<BoundaryFactBuffer>(),
                        },
                        None = new[]
                        {
                            ComponentType.ReadOnly<GasAscIdentity>(),
                            ComponentType.ReadOnly<GasSessionIdentity>(),
                        },
                    });
                return query.CalculateEntityCount();
            }
        }

        /// <summary>
        /// 安装正式拓扑、按场景记录单/双 ASC SpawnBatch，并用两次完整批次发布 Ready。
        /// </summary>
        internal RuntimeV1TickDagTestWorld(
            int maxOwnerPlanCount = 4,
            bool withAbility = false,
            float costDelta = -10f,
            bool withCooldown = true,
            bool withEffect = false,
            bool lethalEffect = false,
            bool periodEffect = false,
            bool withRemoteAsc = false,
            bool committedRemoteWork = false)
        {
            _withAbility = withAbility || committedRemoteWork;
            _withEffect = withEffect || lethalEffect || periodEffect || committedRemoteWork;
            _lethalEffect = lethalEffect;
            _periodEffect = periodEffect;
            _withRemoteAsc = withRemoteAsc || committedRemoteWork;
            _committedRemoteWork = committedRemoteWork;
            _catalog = committedRemoteWork ? CreateCommittedRemoteWorkCatalog() :
                withAbility ? CreateAbilityCatalog(costDelta, withCooldown) :
                _withEffect ? CreateEffectCatalog(_lethalEffect, _periodEffect) : CreateEmptyCatalog();
            _world = new World("Runtime v1 Tick DAG PlayMode test");
            _owner = GasRuntimeWorldOwner.Install(_world);
            _world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>().Timestep = FixedDeltaTime;
            RecordBootstrap(maxOwnerPlanCount);
            TickBatch();
            TickBatch();
            ResolveReadyEntities();
        }

        /// <summary>
        /// 先释放持有 gate/World 引用的 owner，再释放 World 与测试 Blob。
        /// </summary>
        public void Dispose()
        {
            _owner.Dispose();
            _world.Dispose();
            if (_catalog.IsCreated)
                _catalog.Dispose();
        }

        /// <summary>
        /// 仅释放正式 owner，保留 World 以验证缓存 Port 的同步终态。
        /// </summary>
        internal void DisposeOwner()
        {
            _owner.Dispose();
        }

        /// <summary>
        /// 推进恰好一次完整 FixedStep 并完成 outer batch fence 握手。
        /// </summary>
        internal void TickBatch()
        {
            _elapsedTime += FixedDeltaTime;
            _world.SetTime(new TimeData(_elapsedTime, FixedDeltaTime));
            _owner.TickBatch();
        }

        /// <summary>
        /// 从正式 managed Boundary ring 取出一次已接管的不可变批次。
        /// </summary>
        internal bool TryDequeueBoundaryBatch(out GasBoundaryDrainBatch batch)
        {
            batch = default;
            return _owner.BoundaryDrainRing != null &&
                   _owner.BoundaryDrainRing.TryDequeue(out batch);
        }

        /// <summary>
        /// 创建只含 Boundary cleanup 载体的空 shell，供下一批次验证 NoFactReceipt 与 EndFixed 回收。
        /// </summary>
        internal Entity CreateEmptyBoundaryShell()
        {
            var shell = EntityManager.CreateEntity(
                ComponentType.ReadWrite<BoundaryDrainState>(),
                ComponentType.ReadWrite<BoundaryFactBuffer>());
            EntityManager.SetComponentData(
                shell,
                BoundaryDrainState.Create(Epoch, GasBoundaryOwnerKind.Session, Epoch, 1, 1));
            return shell;
        }

        /// <summary>
        /// 通过唯一 typed Port 提交一个无 payload 的 ApplyEffect 命令。
        /// </summary>
        internal GasCommandAcceptResult SubmitApplyEffect(ulong requestId, ulong availableTick)
        {
            var target = BoundaryTargetRef.ForAsc(Battle, OwnerAsc);
            var context = new GasBoundaryCommandContext(
                Epoch,
                requestId,
                requestId + 1000,
                availableTick,
                false,
                Battle,
                default,
                target);
            return _owner.Port.RequestApplyEffect(
                context,
                EffectDefinitionId,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);
        }

        /// <summary>
        /// 通过唯一 typed Port 从显式 source 向另一个 Ready ASC 提交无 payload Effect。
        /// </summary>
        internal GasCommandAcceptResult SubmitApplyEffectFrom(
            ulong requestId,
            in OwnerAscHandle source,
            in OwnerAscHandle targetOwner)
        {
            var target = BoundaryTargetRef.ForAsc(Battle, targetOwner);
            var context = new GasBoundaryCommandContext(
                Epoch,
                requestId,
                requestId + 1000,
                CurrentTick,
                true,
                Battle,
                source,
                target);
            return _owner.Port.RequestApplyEffect(
                context,
                EffectDefinitionId,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);
        }

        /// <summary>
        /// 在当前 Session 安装一次性 TargetPrepare conformance fault 点。
        /// </summary>
        internal void InjectTargetPrepareFailureAfter(int preparedApplicationCount)
        {
            EntityManager.AddComponentData(
                Session,
                new GasTargetPrepareFaultInjection
                {
                    FailAfterPreparedApplicationCount = preparedApplicationCount,
                });
        }

        /// <summary>
        /// 通过唯一 typed Port 提交当前 Tick due 的 GrantedAbility 激活请求。
        /// </summary>
        internal GasCommandAcceptResult SubmitActivate(ulong requestId, in GrantedAbilityHandle grant)
        {
            var context = CreateAbilityContext(requestId);
            return _owner.Port.RequestActivate(
                context, grant, BoundaryCommandPayloadDescriptor.None, ReadOnlySpan<byte>.Empty);
        }

        /// <summary>
        /// 通过唯一 typed Port 提交当前 Tick due 的 Activation Commit 请求。
        /// </summary>
        internal GasCommandAcceptResult SubmitCommit(ulong requestId, in AbilityActivationHandle activation)
        {
            var context = CreateAbilityContext(requestId);
            return _owner.Port.RequestCommit(
                context, activation, BoundaryCommandPayloadDescriptor.None, ReadOnlySpan<byte>.Empty);
        }

        /// <summary>
        /// 通过唯一 typed Port 把规范 ASC target 与 Activation Commit 原子冻结。
        /// </summary>
        internal GasCommandAcceptResult SubmitCommitTo(
            ulong requestId,
            in AbilityActivationHandle activation,
            in OwnerAscHandle targetOwner)
        {
            var target = BoundaryTargetRef.ForAsc(Battle, targetOwner);
            var context = CreateAbilityContext(requestId, in target);
            return _owner.Port.RequestCommit(
                context, activation, BoundaryCommandPayloadDescriptor.None, ReadOnlySpan<byte>.Empty);
        }

        /// <summary>
        /// 通过唯一 typed Port 提交当前 Tick due 的 Activation Cancel 请求。
        /// </summary>
        internal GasCommandAcceptResult SubmitCancel(ulong requestId, in AbilityActivationHandle activation)
        {
            var context = CreateAbilityContext(requestId);
            return _owner.Port.RequestCancel(
                context, activation, BoundaryCommandPayloadDescriptor.None, ReadOnlySpan<byte>.Empty);
        }

        /// <summary>
        /// 通过正式 lifecycle helper 写入下一 candidate Tick due 的 grant removal 命令。
        /// </summary>
        internal void EnqueueGrantedRemoval(
            ulong commandSequence,
            in GrantedAbilityHandle grant,
            GasGrantedAbilityRemovalPolicy policy)
        {
            var profile = EntityManager.GetComponentData<GasScaleProfile>(Session);
            var heads = EntityManager.GetComponentData<AscSlabHeads>(Asc);
            var commands = EntityManager.GetBuffer<PendingCommand>(Asc);
            var battle = Battle;
            var owner = OwnerAsc;
            var enqueued = GasAbilityLifecycleMaintenance.TryEnqueueGrantedRemoval(
                in battle,
                in owner,
                in grant,
                GrantedAbilities,
                policy,
                CurrentTick + 1,
                commandSequence,
                profile.MaxPendingCommandCount,
                commands,
                ref heads.PendingCommand);
            Assert.That(enqueued, Is.True);
            EntityManager.SetComponentData(Asc, heads);
        }

        /// <summary>
        /// 为 LeaveGranted 验收挂入一个待脱钩 ActiveEffect provenance，同时保留审计字段。
        /// </summary>
        internal void AttachActiveEffectProvenance(
            in GrantedAbilityHandle grant,
            ulong sourceStableId,
            ulong applicationId,
            ulong contextId)
        {
            var grants = GrantedAbilities;
            var slot = grants[grant.SlotIndex];
            Assert.That(slot.Header.StorageState, Is.EqualTo(GasSlabSlotState.Live));
            Assert.That(slot.Handle, Is.EqualTo(grant));
            slot.GrantSourceKind = GasAbilityGrantSourceKind.ActiveEffect;
            slot.GrantSourceStableId = sourceStableId;
            slot.GrantingActiveEffect = new ActiveEffectHandle(Epoch, OwnerAsc, 0, 1);
            slot.GrantApplicationId = applicationId;
            slot.GrantContextId = contextId;
            grants[grant.SlotIndex] = slot;
        }

        /// <summary>
        /// 构造无 target 且 source 与 subject owner 一致的 Ability 请求上下文。
        /// </summary>
        private GasBoundaryCommandContext CreateAbilityContext(ulong requestId)
        {
            var target = BoundaryTargetRef.None;
            return CreateAbilityContext(requestId, in target);
        }

        /// <summary>
        /// 构造 source 与 subject owner 一致、target 由调用方显式冻结的 Ability 请求上下文。
        /// </summary>
        private GasBoundaryCommandContext CreateAbilityContext(
            ulong requestId,
            in BoundaryTargetRef target)
        {
            return new GasBoundaryCommandContext(
                Epoch,
                requestId,
                requestId + 1000,
                CurrentTick,
                true,
                Battle,
                OwnerAsc,
                target);
        }

        /// <summary>
        /// 捕获 fault 前必须保持完全不变的 Tick、RNG、slab 与各类 durable 长度。
        /// </summary>
        internal RuntimeV1GameplayAuthoritySnapshot CaptureGameplayAuthority()
        {
            return new RuntimeV1GameplayAuthoritySnapshot(
                EntityManager.GetComponentData<SimulationTickState>(Session),
                EntityManager.GetComponentData<AscRandomState>(Asc),
                EntityManager.GetComponentData<AscSlabHeads>(Asc),
                EntityManager.GetBuffer<AttributeValueSlot>(Asc).Length,
                EntityManager.GetBuffer<TagCountSlot>(Asc).Length,
                EntityManager.GetBuffer<PendingCommand>(Asc).Length,
                EntityManager.GetBuffer<BoundaryFactBuffer>(Asc).Length,
                EntityManager.GetBuffer<BoundaryFactBuffer>(Session).Length);
        }

        /// <summary>
        /// 逐值冻结 TargetPrepare 可能发布的三个组件与七类 buffer 权威面。
        /// </summary>
        internal RuntimeV1TargetAuthoritySnapshot CaptureTargetAuthority()
        {
            return new RuntimeV1TargetAuthoritySnapshot(
                EntityManager.GetComponentData<AscLifecycle>(Asc),
                EntityManager.GetComponentData<AscSlabHeads>(Asc),
                EntityManager.GetComponentData<GasPayloadRangeAllocatorState>(Asc),
                CopyBuffer(EntityManager.GetBuffer<ActiveEffectSlot>(Asc)),
                CopyBuffer(EntityManager.GetBuffer<AttributeValueSlot>(Asc)),
                CopyBuffer(EntityManager.GetBuffer<AttributeDirtyWord>(Asc)),
                CopyBuffer(EntityManager.GetBuffer<TagCountSlot>(Asc)),
                CopyBuffer(EntityManager.GetBuffer<TagPresenceWord>(Asc)),
                CopyBuffer(EntityManager.GetBuffer<GasPayloadRangeRecord>(Asc)),
                CopyBuffer(EntityManager.GetBuffer<GasPayloadValueSlot>(Asc)));
        }

        /// <summary>
        /// 验证 Target fatal 后所有可发布权威组件与 buffer 的长度、顺序和值均保持不变。
        /// </summary>
        internal void AssertTargetAuthorityUnchanged(in RuntimeV1TargetAuthoritySnapshot before)
        {
            AssertLifecycleGameplayFieldsUnchanged(in before.Lifecycle);
            Assert.That(EntityManager.GetComponentData<AscSlabHeads>(Asc), Is.EqualTo(before.Slabs));
            Assert.That(
                EntityManager.GetComponentData<GasPayloadRangeAllocatorState>(Asc),
                Is.EqualTo(before.PayloadState));
            AssertBufferEquals(before.ActiveEffects, EntityManager.GetBuffer<ActiveEffectSlot>(Asc));
            AssertBufferEquals(before.Attributes, EntityManager.GetBuffer<AttributeValueSlot>(Asc));
            AssertBufferEquals(
                before.AttributeDirtyWords,
                EntityManager.GetBuffer<AttributeDirtyWord>(Asc));
            AssertBufferEquals(before.TagCounts, EntityManager.GetBuffer<TagCountSlot>(Asc));
            AssertBufferEquals(
                before.TagPresenceWords,
                EntityManager.GetBuffer<TagPresenceWord>(Asc));
            AssertBufferEquals(
                before.PayloadRanges,
                EntityManager.GetBuffer<GasPayloadRangeRecord>(Asc));
            AssertBufferEquals(
                before.PayloadValues,
                EntityManager.GetBuffer<GasPayloadValueSlot>(Asc));
        }

        /// <summary>
        /// 逐项验证生命周期业务字段回滚，同时允许并要求 fault-close 控制态关闭 ingress。
        /// </summary>
        private void AssertLifecycleGameplayFieldsUnchanged(in AscLifecycle before)
        {
            var actual = EntityManager.GetComponentData<AscLifecycle>(Asc);
            Assert.That(actual.State, Is.EqualTo(before.State));
            Assert.That(actual.ReadyTick, Is.EqualTo(before.ReadyTick));
            Assert.That(actual.TerminalTick, Is.EqualTo(before.TerminalTick));
            Assert.That(actual.DeathTick, Is.EqualTo(before.DeathTick));
            Assert.That(actual.DeathTransitionId, Is.EqualTo(before.DeathTransitionId));
            Assert.That(actual.DeathApplicationId, Is.EqualTo(before.DeathApplicationId));
            Assert.That(actual.DeathSourceAsc, Is.EqualTo(before.DeathSourceAsc));
            Assert.That(actual.DeathOverkill, Is.EqualTo(before.DeathOverkill));
            Assert.That(actual.IngressClosed, Is.EqualTo(1));
        }

        /// <summary>
        /// 将 DynamicBuffer 复制为不依赖 World 生命周期的逐值测试快照。
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
        /// 逐元素验证 durable buffer，避免只比较长度造成原子性测试假绿。
        /// </summary>
        private static void AssertBufferEquals<T>(T[] expected, DynamicBuffer<T> actual)
            where T : unmanaged, IBufferElementData
        {
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (var index = 0; index < expected.Length; index++)
                Assert.That(actual[index], Is.EqualTo(expected[index]), $"buffer index {index}");
        }

        /// <summary>
        /// 验证已成功消费的唯一 inbox 记录与当 Tick 诊断，尚未进入下一轮物理压缩。
        /// </summary>
        internal void AssertSingleConsumed(ulong requestSequence)
        {
            var inbox = Inbox;
            Assert.That(inbox.Length, Is.EqualTo(1));
            Assert.That(inbox[0].RequestSequence, Is.EqualTo(requestSequence));
            Assert.That(inbox[0].State, Is.EqualTo(GasBoundaryCommandState.Consumed));
            Assert.That(Diagnostics.SealedCommandCount, Is.EqualTo(1));
            Assert.That(Diagnostics.AdmissionSucceeded, Is.EqualTo(1));
            Assert.That(Diagnostics.ExecutedLaneMask, Is.EqualTo(GasTickLaneMask.AllGameplay));
        }

        /// <summary>
        /// 验证 WholeTick 失败证据、零 gameplay 写与同 gate lock 上完成的 fault close 回执。
        /// </summary>
        internal void AssertAdmissionFault(
            in RuntimeV1GameplayAuthoritySnapshot before,
            ulong requestSequence)
        {
            AssertGameplayAuthorityUnchanged(in before);
            AssertFaultDiagnostics(before.Tick.CurrentTick + 1);
            var latch = EntityManager.GetComponentData<SessionFaultLatch>(Session);
            AssertFaultLatch(in latch, requestSequence);
            Assert.That(SessionLifecycle.State, Is.EqualTo(GasSessionLifecycleState.Faulted));
            Assert.That(Inbox.Length, Is.EqualTo(1));
            Assert.That(Inbox[0].State, Is.EqualTo(GasBoundaryCommandState.FaultTerminated));
            Assert.That(EntityManager.GetBuffer<BattleInstanceSlot>(Session)[0].IngressClosed, Is.EqualTo(1));
            Assert.That(EntityManager.GetComponentData<AscLifecycle>(Asc).IngressClosed, Is.EqualTo(1));
        }

        /// <summary>
        /// 通过正式 EndFixed singleton 记录一个无初始化数据的最小 SpawnBatch。
        /// </summary>
        private void RecordBootstrap(int maxOwnerPlanCount)
        {
            using var battles = CreateBattleRequests();
            using var ascs = CreateAscRequests();
            using var attributes = CreateAttributeInitializations();
            using var tags = new NativeArray<PendingTagInitialization>(0, Allocator.Temp);
            using var abilities = CreateAbilityInitializations();
            using var initialEffects = new NativeArray<PendingInitialGameplayEffect>(0, Allocator.Temp);
            var request = CreateSessionRequest(maxOwnerPlanCount);
            var failure = GasStageBBootstrapRecorder.Record(
                EntityManager,
                GetEndFixedSingleton(),
                _world.Unmanaged,
                ref _recordGate,
                in request,
                battles,
                ascs,
                attributes,
                tags,
                abilities,
                initialEffects,
                out _);
            Assert.That(failure, Is.EqualTo(GasStageBSpawnFaultReason.None));
        }

        /// <summary>
        /// 按 fixture 模式构造空或单 grant 初始化数组，并在转为 using 变量前完成写入。
        /// </summary>
        private NativeArray<PendingGrantedAbilityInitialization> CreateAbilityInitializations()
        {
            var abilities = new NativeArray<PendingGrantedAbilityInitialization>(
                _withAbility ? 1 : 0,
                Allocator.Temp);
            if (_withAbility)
            {
                abilities[0] = new PendingGrantedAbilityInitialization
                {
                    LayoutIndex = 0,
                    ConfigOrdinal = _committedRemoteWork ? 1 : 0,
                };
            }
            return abilities;
        }

        /// <summary>
        /// 查询两轮后的唯一 Session 与单/双 ASC，并确认 Stage-B 已完整发布 Ready。
        /// </summary>
        private void ResolveReadyEntities()
        {
            using var query = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<GasSessionIdentity>(),
                ComponentType.ReadOnly<GasActiveSessionAuthority>());
            Assert.That(query.CalculateEntityCount(), Is.EqualTo(1));
            Session = query.GetSingletonEntity();
            var registry = EntityManager.GetBuffer<AscRegistrySlot>(Session);
            Assert.That(registry.Length, Is.EqualTo(_withRemoteAsc ? 2 : 1));
            Assert.That(registry[0].OwnerAsc, Is.EqualTo(OwnerAsc));
            Asc = registry[0].ResolveRuntimeEntity();
            if (_withRemoteAsc)
            {
                Assert.That(registry[1].OwnerAsc, Is.EqualTo(RemoteOwnerAsc));
                Assert.That(registry[1].State, Is.EqualTo(GasAscRegistryState.Ready));
                RemoteAsc = registry[1].ResolveRuntimeEntity();
            }
            Assert.That(SessionLifecycle.State, Is.EqualTo(GasSessionLifecycleState.Ready));
            Assert.That(CurrentTick, Is.Zero);
            Assert.That(registry[0].State, Is.EqualTo(GasAscRegistryState.Ready));
        }

        /// <summary>
        /// 构造 Session、空 Catalog expectation 与完整版本化容量档位。
        /// </summary>
        private GasStageBSessionBootstrapRequest CreateSessionRequest(int maxOwnerPlanCount)
        {
            ref var catalog = ref _catalog.Value;
            return new GasStageBSessionBootstrapRequest
            {
                SimulationEpoch = Epoch,
                SpawnBatchId = SpawnBatchId,
                Config = new GasSessionConfig
                {
                    TickRate = 20,
                    RuleVersion = 1,
                    BoundaryPolicyVersion = 1,
                },
                ScaleProfile = CreateScaleProfile(
                    maxOwnerPlanCount,
                    _withRemoteAsc,
                    _committedRemoteWork),
                Catalog = _catalog,
                CatalogExpectation = new GasCatalogValidationExpectation(
                    catalog.SchemaVersion,
                    catalog.SchemaHash,
                    catalog.ContentHash,
                    catalog.AttributeLayout.LayoutHash,
                    catalog.TagCatalog.CatalogHash),
            };
        }

        /// <summary>
        /// 构造包含单个或两个 Ready 成员范围的 Battle 请求。
        /// </summary>
        private NativeArray<GasStageBBattleBootstrapRequest> CreateBattleRequests()
        {
            var values = new NativeArray<GasStageBBattleBootstrapRequest>(1, Allocator.Temp);
            values[0] = new GasStageBBattleBootstrapRequest
            {
                BattleInstance = Battle,
                MemberStart = 0,
                MemberCount = _withRemoteAsc ? 2 : 1,
                MembershipOrdinalRoot = 10,
            };
            return values;
        }

        /// <summary>
        /// 构造 source 与可选 remote ASC 的稳定成员关系及连续初始化 range。
        /// </summary>
        private NativeArray<GasStageBAscBootstrapRequest> CreateAscRequests()
        {
            var values = new NativeArray<GasStageBAscBootstrapRequest>(
                _withRemoteAsc ? 2 : 1,
                Allocator.Temp);
            values[0] = new GasStageBAscBootstrapRequest
            {
                OwnerAsc = OwnerAsc,
                BattleInstance = Battle,
                RegistryOrdinal = 0,
                ScenarioUnitId = 900,
                MembershipOrdinal = 10,
                OwnerActorStableId = 1000,
                AvatarActorStableId = 2000,
                ActorBindingGeneration = 1,
                RandomState0 = 3000,
                RandomState1 = 4000,
                AttributeInitializationStart = 0,
                AttributeInitializationCount = _committedRemoteWork ? 1 : 0,
                GrantedAbilityInitializationStart = 0,
                GrantedAbilityInitializationCount = _withAbility ? 1 : 0,
            };
            if (_withRemoteAsc)
            {
                values[1] = new GasStageBAscBootstrapRequest
                {
                    OwnerAsc = RemoteOwnerAsc,
                    BattleInstance = Battle,
                    RegistryOrdinal = 1,
                    ScenarioUnitId = 901,
                    MembershipOrdinal = 11,
                    OwnerActorStableId = 1001,
                    AvatarActorStableId = 2001,
                    ActorBindingGeneration = 1,
                    RandomState0 = 3001,
                    RandomState1 = 4001,
                    AttributeInitializationStart = _committedRemoteWork ? 1 : 0,
                    AttributeInitializationCount = _committedRemoteWork ? 1 : 0,
                    GrantedAbilityInitializationStart = _withAbility ? 1 : 0,
                    GrantedAbilityInitializationCount = 0,
                };
            }
            return values;
        }

        /// <summary>
        /// 为 committed-work 场景显式冻结 source=5、remote=100 的 Health 初值。
        /// </summary>
        private NativeArray<PendingAttributeInitialization> CreateAttributeInitializations()
        {
            var values = new NativeArray<PendingAttributeInitialization>(
                _committedRemoteWork ? 2 : 0,
                Allocator.Temp);
            if (_committedRemoteWork)
            {
                values[0] = new PendingAttributeInitialization
                {
                    LayoutIndex = 0,
                    ConfigOrdinal = 0,
                    HasExplicitValue = 1,
                    BaseValue = 5f,
                    CurrentValue = 5f,
                };
                values[1] = new PendingAttributeInitialization
                {
                    LayoutIndex = 0,
                    ConfigOrdinal = 2,
                    HasExplicitValue = 1,
                    BaseValue = 100f,
                    CurrentValue = 100f,
                };
            }
            return values;
        }

        /// <summary>
        /// 返回标准 EndFixed 系统注册的 singleton，确保 bootstrap 只通过正式 playback 边界。
        /// </summary>
        private EndFixedStepSimulationEntityCommandBufferSystem.Singleton GetEndFixedSingleton()
        {
            using var query = EntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>(),
                },
                Options = EntityQueryOptions.IncludeSystems,
            });
            return query.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>();
        }

        /// <summary>
        /// 验证 fault 后 Tick、RNG、slab 与 durable gameplay buffer 均保持原值。
        /// </summary>
        private void AssertGameplayAuthorityUnchanged(
            in RuntimeV1GameplayAuthoritySnapshot before)
        {
            Assert.That(EntityManager.GetComponentData<SimulationTickState>(Session), Is.EqualTo(before.Tick));
            Assert.That(EntityManager.GetComponentData<AscRandomState>(Asc), Is.EqualTo(before.Random));
            Assert.That(EntityManager.GetComponentData<AscSlabHeads>(Asc), Is.EqualTo(before.Slabs));
            Assert.That(EntityManager.GetBuffer<AttributeValueSlot>(Asc).Length, Is.EqualTo(before.AttributeCount));
            Assert.That(EntityManager.GetBuffer<TagCountSlot>(Asc).Length, Is.EqualTo(before.TagCount));
            Assert.That(EntityManager.GetBuffer<PendingCommand>(Asc).Length, Is.EqualTo(before.PendingCount));
            Assert.That(EntityManager.GetBuffer<BoundaryFactBuffer>(Asc).Length, Is.EqualTo(before.AscFactCount));
            Assert.That(EntityManager.GetBuffer<BoundaryFactBuffer>(Session).Length,
                Is.EqualTo(before.SessionFactCount));
        }

        /// <summary>
        /// 验证 admission 失败仍无条件进入全部预排 lane 并发布唯一原因码。
        /// </summary>
        private void AssertFaultDiagnostics(ulong candidateTick)
        {
            var diagnostics = Diagnostics;
            Assert.That(diagnostics.CandidateTick, Is.EqualTo(candidateTick));
            Assert.That(diagnostics.ExecutedLaneMask, Is.EqualTo(GasTickLaneMask.AllGameplay));
            Assert.That(diagnostics.AdmissionSucceeded, Is.Zero);
            Assert.That(diagnostics.AdmissionReasonCode,
                Is.EqualTo((int)GasTickAdmissionFailureReason.OwnerPlanLimit));
            Assert.That(diagnostics.SealedCommandCount, Is.EqualTo(1));
        }

        /// <summary>
        /// 验证 deterministic sealed subset 与 gate outstanding close receipt 指向同一请求。
        /// </summary>
        private static void AssertFaultLatch(
            in SessionFaultLatch latch,
            ulong requestSequence)
        {
            Assert.That(latch.FaultId, Is.Not.Zero);
            Assert.That(latch.Detected, Is.EqualTo(1));
            Assert.That(latch.IngressClosed, Is.EqualTo(1));
            Assert.That(latch.SealedFirstRequestSequence, Is.EqualTo(requestSequence));
            Assert.That(latch.SealedLastRequestSequence, Is.EqualTo(requestSequence));
            Assert.That(latch.SealedRequestCount, Is.EqualTo(1));
            Assert.That(latch.OutstandingFirstRequestSequence, Is.EqualTo(requestSequence));
            Assert.That(latch.OutstandingLastRequestSequence, Is.EqualTo(requestSequence));
            Assert.That(latch.OutstandingRequestCount, Is.EqualTo(1));
            Assert.That(latch.OutstandingRequestHash, Is.Not.Zero);
        }

        /// <summary>
        /// 构造满足 Stage-C DAG 与条件化双 ASC 初始化的最小合法 ScaleProfile。
        /// </summary>
        private static GasScaleProfile CreateScaleProfile(
            int maxOwnerPlanCount,
            bool withRemoteAsc,
            bool withAttributeInitialization)
        {
            return new GasScaleProfile
            {
                ProfileId = 1,
                ProfileVersion = 1,
                ProfileHash = 6001,
                MaxFixedTicksPerBatch = 1,
                MaximumDeltaTimeTicks = 1,
                MaxSpawnBatchSize = withRemoteAsc ? 2 : 1,
                MaxBattleInstanceCount = 1,
                MaxAscRegistryCount = withRemoteAsc ? 2 : 1,
                MaxBoundaryCommandCount = 4,
                MaxBoundaryCommandPayloadCount = 16,
                MaxOwnerPlanCount = maxOwnerPlanCount,
                MaxResolvedTargetCount = 4,
                MaxEffectOperationCount = 4,
                MaxOwnerReservationCount = 4,
                MaxTargetReservationCount = 4,
                MaxCoreFactCount = 8,
                MaxNextTickRouteCount = 4,
                MaxStructuralIntentCount = 4,
                MaxPendingAttributeInitializationCount = withAttributeInitialization ? 1 : 0,
                MaxPendingGrantedAbilityInitializationCount = 1,
                MaxGrantedAbilityCount = 2,
                MaxAbilityActivationCount = 4,
                MaxAbilityContinuationCount = 4,
                MaxAbilitySubscriptionCount = 4,
                MaxCooldownGateCount = 4,
                MaxActivationOwnedContributionCount = 4,
                MaxEmittedApplicationRefCount = 4,
                MaxActiveEffectCount = 4,
                MaxPayloadRangeRecordCount = 4,
                MaxPayloadValueCount = 4,
                MaxAttributeAggregatorCount = 4,
                MaxLiveDependencyCount = 4,
                MaxLiveDependencyRouteCount = 4,
                MaxPendingCommandCount = 8,
                MaxSessionBoundaryFactCount = 8,
                MaxAscBoundaryFactCount = 8,
            };
        }

        /// <summary>
        /// 构造 FrozenAsc Ability、远端 Instant Effect、Health 与 cost 资源的 committed-work 验收 Catalog。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateCommittedRemoteWorkCatalog()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                InitializeCatalogHeader(ref root);
                var attributes = builder.Allocate(ref root.AttributeLayout.Entries, 2);
                attributes[0] = new GasAttributeLayoutEntryBlob
                {
                    AttributeId = 1,
                    LayoutIndex = 0,
                    DefaultValue = 100f,
                    MinimumValue = 0f,
                    MaximumValue = 100f,
                    ClampMinimum = 1,
                    ClampMaximum = 1,
                    DomainRole = GasAttributeDomainRole.Health,
                };
                attributes[1] = new GasAttributeLayoutEntryBlob
                {
                    AttributeId = 2,
                    LayoutIndex = 1,
                    DefaultValue = 100f,
                    MinimumValue = 0f,
                    MaximumValue = 100f,
                    ClampMinimum = 1,
                    ClampMaximum = 1,
                    DomainRole = GasAttributeDomainRole.None,
                };
                builder.Allocate(ref root.TagCatalog.Entries, 0);
                builder.Allocate(ref root.TagCatalog.AncestorIndices, 0);
                builder.Allocate(ref root.AbilityIndex, 1)[0] =
                    new GasDefinitionIndexEntry { DefinitionId = 7001, DefinitionIndex = 0 };
                builder.Allocate(ref root.Abilities, 1)[0] = CreateCommittedRemoteAbility();
                builder.Allocate(ref root.GameplayEffectIndex, 1)[0] =
                    new GasDefinitionIndexEntry
                    {
                        DefinitionId = EffectDefinitionId,
                        DefinitionIndex = 0,
                    };
                builder.Allocate(ref root.GameplayEffects, 1)[0] = CreateCommittedRemoteEffect();
                builder.Allocate(ref root.Requirements, 0);
                builder.Allocate(ref root.RequirementTagIndices, 0);
                builder.Allocate(ref root.CaptureDescriptors, 0);
                builder.Allocate(ref root.Modifiers, 1)[0] = new GasModifierDefinitionBlob
                {
                    AttributeLayoutIndex = 0,
                    Operation = GasModifierOperation.Add,
                    EvaluatorProgramRange = new GasCatalogRange { Start = 0, Count = 1 },
                };
                builder.Allocate(ref root.DirectEffectProgramNodes, 1)[0] =
                    new GasDirectEffectProgramNodeBlob
                    {
                        NodeOrdinal = 0,
                        EffectDefinitionId = EffectDefinitionId,
                        MaximumTargetCount = 1,
                        MaximumOutputCount = 1,
                    };
                builder.Allocate(ref root.CueReferences, 0);
                builder.Allocate(ref root.ValueViews, 0);
                builder.Allocate(ref root.EvaluatorInstructions, 1)[0] =
                    new GasEvaluatorInstructionBlob
                    {
                        Opcode = GasEvaluatorOpcode.PushConstant,
                        ConstantValue = -8f,
                    };
                builder.Allocate(ref root.SetByCallerDescriptors, 0);
                builder.Allocate(ref root.TargetDataDescriptors, 1)[0] =
                    new GasTargetDataDescriptorBlob
                    {
                        Variant = GasTargetDataVariant.StableAsc,
                        FieldOrdinal = 0,
                        Required = 1,
                    };
                builder.Allocate(ref root.EffectContextFieldDescriptors, 0);
                var catalog = builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
                GasDefinitionCatalogContentHasher.Stamp(ref catalog.Value);
                return catalog;
            }
            finally
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// 创建扣除一点 owner 资源、冻结远端 ASC 并发射一个 DirectEffect 的 Ability。
        /// </summary>
        private static GasAbilityDefinitionBlob CreateCommittedRemoteAbility()
        {
            return new GasAbilityDefinitionBlob
            {
                DefinitionId = 7001,
                Level = 1,
                TargetPolicy = CreateFrozenAscTargetPolicy(),
                CostMutationContract = new GasCostMutationContractBlob
                {
                    Enabled = 1,
                    AttributeLayoutIndex = 1,
                    BaseDelta = -1f,
                    CurrentDelta = -1f,
                },
                CooldownGateContract = new GasCooldownGateContractBlob
                {
                    Enabled = 1,
                    GateKey = 9001,
                    DurationTicks = 2,
                    OwnedTagIndex = -1,
                },
                DirectEffectProgramRange = new GasCatalogRange { Start = 0, Count = 1 },
                Maxima = new GasDefinitionMaxima
                {
                    MaximumTargetCount = 1,
                    MaximumPlannedApplicationCount = 1,
                    MaximumDirectProgramNodeCount = 1,
                    MaximumDirectProgramOutputCount = 1,
                },
            };
        }

        /// <summary>
        /// 创建对冻结 ASC Health 施加 -8 的 Instant GameplayEffect。
        /// </summary>
        private static GasGameplayEffectDefinitionBlob CreateCommittedRemoteEffect()
        {
            return new GasGameplayEffectDefinitionBlob
            {
                DefinitionId = EffectDefinitionId,
                Lifetime = GasEffectLifetimePolicy.Instant,
                TargetPolicy = CreateFrozenAscTargetPolicy(),
                ModifierRange = new GasCatalogRange { Start = 0, Count = 1 },
                EvaluatorProgramRange = new GasCatalogRange { Start = 0, Count = 1 },
                TargetDataRange = new GasCatalogRange { Start = 0, Count = 1 },
                ExpiryPolicy = GasExpiryPolicy.Remove,
                ExpiryPeriodPolicy = GasExpiryPeriodPolicy.Stop,
                ExpirySameTickPolicy = GasExpirySameTickPolicy.ExpiryBeforePeriodDue,
                InhibitTimePolicy = GasInhibitTimePolicy.DurationContinues,
                InhibitedPeriodPolicy = GasInhibitedPeriodPolicy.None,
                MissedPeriodPolicy = GasMissedPeriodPolicy.SkipNoCatchUp,
                Maxima = new GasDefinitionMaxima
                {
                    MaximumTargetCount = 1,
                    MaximumPlannedApplicationCount = 1,
                    MaximumModifierCount = 1,
                    MaximumEvaluatorInstructionCount = 1,
                    MaximumTargetDataCount = 1,
                },
            };
        }

        /// <summary>
        /// 返回 FollowAsc、无空间采样且 AliveOnly 的 FrozenAsc target policy。
        /// </summary>
        private static GasTargetPolicyBlob CreateFrozenAscTargetPolicy()
        {
            return new GasTargetPolicyBlob
            {
                LogicalTarget = GasLogicalTargetPolicy.FrozenAsc,
                Avatar = GasAvatarTargetPolicy.FollowAsc,
                Spatial = GasSpatialTargetPolicy.None,
                Life = GasTargetLifePolicy.AliveOnly,
            };
        }

        /// <summary>
        /// 构造带一个 owner-local cost/cooldown Ability 与一个资源 Attribute 的合法 Catalog。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateAbilityCatalog(
            float costDelta,
            bool withCooldown)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                InitializeCatalogHeader(ref root);
                var attributes = builder.Allocate(ref root.AttributeLayout.Entries, 1);
                attributes[0] = new GasAttributeLayoutEntryBlob
                {
                    AttributeId = 1,
                    LayoutIndex = 0,
                    DefaultValue = 100f,
                    MinimumValue = 0f,
                    MaximumValue = 100f,
                    ClampMinimum = 1,
                    ClampMaximum = 1,
                };
                AllocateEmptyCatalogArraysExceptAbility(ref builder, ref root);
                var indices = builder.Allocate(ref root.AbilityIndex, 1);
                indices[0] = new GasDefinitionIndexEntry { DefinitionId = 7001, DefinitionIndex = 0 };
                var abilities = builder.Allocate(ref root.Abilities, 1);
                abilities[0] = CreateAbilityDefinition(costDelta, withCooldown);
                var catalog = builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
                GasDefinitionCatalogContentHasher.Stamp(ref catalog.Value);
                return catalog;
            }
            finally
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// 创建一个 Instant Add GameplayEffect，供 Kernel 真实验证 target-owned application 与 Boundary fact。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateEffectCatalog(
            bool lethal,
            bool period)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                InitializeCatalogHeader(ref root);
                builder.Allocate(ref root.AttributeLayout.Entries, 1)[0] = new GasAttributeLayoutEntryBlob
                {
                    AttributeId = 1,
                    LayoutIndex = 0,
                    DefaultValue = lethal ? 5f : 100f,
                    MinimumValue = 0f,
                    MaximumValue = 1000f,
                    ClampMinimum = 1,
                    ClampMaximum = 1,
                    DomainRole = lethal ? GasAttributeDomainRole.Health : GasAttributeDomainRole.None,
                };
                builder.Allocate(ref root.TagCatalog.Entries, 0);
                builder.Allocate(ref root.TagCatalog.AncestorIndices, 0);
                builder.Allocate(ref root.AbilityIndex, 0);
                builder.Allocate(ref root.Abilities, 0);
                builder.Allocate(ref root.GameplayEffectIndex, 1)[0] =
                    new GasDefinitionIndexEntry { DefinitionId = EffectDefinitionId, DefinitionIndex = 0 };
                builder.Allocate(ref root.GameplayEffects, 1)[0] = new GasGameplayEffectDefinitionBlob
                {
                    DefinitionId = EffectDefinitionId,
                    Lifetime = period
                        ? GasEffectLifetimePolicy.Duration
                        : GasEffectLifetimePolicy.Instant,
                    TargetPolicy = new GasTargetPolicyBlob
                    {
                        LogicalTarget = GasLogicalTargetPolicy.Self,
                        Avatar = GasAvatarTargetPolicy.FollowAsc,
                        Spatial = GasSpatialTargetPolicy.None,
                        Life = GasTargetLifePolicy.AliveOnly,
                    },
                    ModifierRange = new GasCatalogRange { Start = 0, Count = 1 },
                    EvaluatorProgramRange = new GasCatalogRange { Start = 0, Count = 1 },
                    DurationTicks = period ? 5 : 0,
                    PeriodTicks = period ? 1 : 0,
                    ExpiryPolicy = GasExpiryPolicy.Remove,
                    ExpiryPeriodPolicy = GasExpiryPeriodPolicy.Stop,
                    ExpirySameTickPolicy = GasExpirySameTickPolicy.ExpiryBeforePeriodDue,
                    InhibitTimePolicy = GasInhibitTimePolicy.DurationContinues,
                    InhibitedPeriodPolicy = period
                        ? GasInhibitedPeriodPolicy.ContinueExecution
                        : GasInhibitedPeriodPolicy.None,
                    MissedPeriodPolicy = GasMissedPeriodPolicy.SkipNoCatchUp,
                    Maxima = CreateEffectMaxima(),
                };
                builder.Allocate(ref root.Modifiers, 1)[0] = new GasModifierDefinitionBlob
                {
                    AttributeLayoutIndex = 0,
                    Operation = GasModifierOperation.Add,
                    EvaluatorProgramRange = new GasCatalogRange { Start = 0, Count = 1 },
                };
                builder.Allocate(ref root.EvaluatorInstructions, 1)[0] = new GasEvaluatorInstructionBlob
                {
                    Opcode = GasEvaluatorOpcode.PushConstant,
                    ConstantValue = period ? -3f : lethal ? -8f : 5f,
                };
                AllocateEmptyEffectCatalogArrays(ref builder, ref root);
                var catalog = builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
                GasDefinitionCatalogContentHasher.Stamp(ref catalog.Value);
                return catalog;
            }
            finally
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// 返回单 modifier effect 的闭合生成期容量上界。
        /// </summary>
        private static GasDefinitionMaxima CreateEffectMaxima()
        {
            return new GasDefinitionMaxima
            {
                MaximumTargetCount = 1,
                MaximumPlannedApplicationCount = 1,
                MaximumModifierCount = 1,
                MaximumEvaluatorInstructionCount = 1,
            };
        }

        /// <summary>
        /// 分配 Effect Catalog 未使用的全部根数组，确保 Blob 形状完整且可验证。
        /// </summary>
        private static void AllocateEmptyEffectCatalogArrays(
            ref BlobBuilder builder,
            ref GasDefinitionCatalogBlob root)
        {
            builder.Allocate(ref root.Requirements, 0);
            builder.Allocate(ref root.RequirementTagIndices, 0);
            builder.Allocate(ref root.CaptureDescriptors, 0);
            builder.Allocate(ref root.DirectEffectProgramNodes, 0);
            builder.Allocate(ref root.CueReferences, 0);
            builder.Allocate(ref root.ValueViews, 0);
            builder.Allocate(ref root.SetByCallerDescriptors, 0);
            builder.Allocate(ref root.TargetDataDescriptors, 0);
            builder.Allocate(ref root.EffectContextFieldDescriptors, 0);
        }

        /// <summary>
        /// 创建费用直接改写 Attribute、冷却持续两 Tick 的最小 Ability 定义。
        /// </summary>
        private static GasAbilityDefinitionBlob CreateAbilityDefinition(
            float costDelta,
            bool withCooldown)
        {
            return new GasAbilityDefinitionBlob
            {
                DefinitionId = 7001,
                Level = 1,
                TargetPolicy = new GasTargetPolicyBlob
                {
                    LogicalTarget = GasLogicalTargetPolicy.Self,
                    Avatar = GasAvatarTargetPolicy.FollowAsc,
                    Spatial = GasSpatialTargetPolicy.None,
                    Life = GasTargetLifePolicy.AliveOnly,
                },
                CostMutationContract = new GasCostMutationContractBlob
                {
                    Enabled = 1,
                    AttributeLayoutIndex = 0,
                    BaseDelta = costDelta,
                    CurrentDelta = costDelta,
                },
                CooldownGateContract = new GasCooldownGateContractBlob
                {
                    Enabled = withCooldown ? (byte)1 : (byte)0,
                    GateKey = withCooldown ? 9001 : 0,
                    DurationTicks = withCooldown ? 2 : 0,
                    OwnedTagIndex = withCooldown ? -1 : 0,
                },
            };
        }

        /// <summary>
        /// 构造所有定义数组为空但 header/hash 完整的合法 immutable Catalog。
        /// </summary>
        private static BlobAssetReference<GasDefinitionCatalogBlob> CreateEmptyCatalog()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            try
            {
                ref var root = ref builder.ConstructRoot<GasDefinitionCatalogBlob>();
                InitializeCatalogHeader(ref root);
                AllocateEmptyCatalogArrays(ref builder, ref root);
                var catalog = builder.CreateBlobAssetReference<GasDefinitionCatalogBlob>(Allocator.Persistent);
                GasDefinitionCatalogContentHasher.Stamp(ref catalog.Value);
                return catalog;
            }
            finally
            {
                builder.Dispose();
            }
        }

        /// <summary>
        /// 写入测试 Catalog 的稳定 schema 身份，其余 hash 在物化后 canonical 封印。
        /// </summary>
        private static void InitializeCatalogHeader(ref GasDefinitionCatalogBlob root)
        {
            root.SchemaVersion = GasDefinitionCatalogSchema.Version;
            root.SchemaHash = SchemaHash;
        }

        /// <summary>
        /// 分配 Ability 测试 Catalog 中除 Attribute 与 Ability 数组外的全部空根数组。
        /// </summary>
        private static void AllocateEmptyCatalogArraysExceptAbility(
            ref BlobBuilder builder,
            ref GasDefinitionCatalogBlob root)
        {
            builder.Allocate(ref root.TagCatalog.Entries, 0);
            builder.Allocate(ref root.TagCatalog.AncestorIndices, 0);
            builder.Allocate(ref root.GameplayEffectIndex, 0);
            builder.Allocate(ref root.GameplayEffects, 0);
            builder.Allocate(ref root.Requirements, 0);
            builder.Allocate(ref root.RequirementTagIndices, 0);
            builder.Allocate(ref root.CaptureDescriptors, 0);
            builder.Allocate(ref root.Modifiers, 0);
            builder.Allocate(ref root.DirectEffectProgramNodes, 0);
            builder.Allocate(ref root.CueReferences, 0);
            builder.Allocate(ref root.ValueViews, 0);
            builder.Allocate(ref root.EvaluatorInstructions, 0);
            builder.Allocate(ref root.SetByCallerDescriptors, 0);
            builder.Allocate(ref root.TargetDataDescriptors, 0);
            builder.Allocate(ref root.EffectContextFieldDescriptors, 0);
        }

        /// <summary>
        /// 显式分配全部空 BlobArray，避免默认 offset 被误解为另一种 Catalog 形状。
        /// </summary>
        private static void AllocateEmptyCatalogArrays(
            ref BlobBuilder builder,
            ref GasDefinitionCatalogBlob root)
        {
            builder.Allocate(ref root.AttributeLayout.Entries, 0);
            builder.Allocate(ref root.TagCatalog.Entries, 0);
            builder.Allocate(ref root.TagCatalog.AncestorIndices, 0);
            builder.Allocate(ref root.AbilityIndex, 0);
            builder.Allocate(ref root.Abilities, 0);
            builder.Allocate(ref root.GameplayEffectIndex, 0);
            builder.Allocate(ref root.GameplayEffects, 0);
            builder.Allocate(ref root.Requirements, 0);
            builder.Allocate(ref root.RequirementTagIndices, 0);
            builder.Allocate(ref root.CaptureDescriptors, 0);
            builder.Allocate(ref root.Modifiers, 0);
            builder.Allocate(ref root.DirectEffectProgramNodes, 0);
            builder.Allocate(ref root.CueReferences, 0);
            builder.Allocate(ref root.ValueViews, 0);
            builder.Allocate(ref root.EvaluatorInstructions, 0);
            builder.Allocate(ref root.SetByCallerDescriptors, 0);
            builder.Allocate(ref root.TargetDataDescriptors, 0);
            builder.Allocate(ref root.EffectContextFieldDescriptors, 0);
        }
    }

    /// <summary>
    /// 冻结 WholeTick fault 前必须保持不变的最小 gameplay authority 快照。
    /// </summary>
    internal readonly struct RuntimeV1GameplayAuthoritySnapshot
    {
        internal readonly SimulationTickState Tick;
        internal readonly AscRandomState Random;
        internal readonly AscSlabHeads Slabs;
        internal readonly int AttributeCount;
        internal readonly int TagCount;
        internal readonly int PendingCount;
        internal readonly int AscFactCount;
        internal readonly int SessionFactCount;

        /// <summary>
        /// 保存全部权威值与 durable buffer 长度，排除允许写入的 fault/inbox 控制态。
        /// </summary>
        internal RuntimeV1GameplayAuthoritySnapshot(
            SimulationTickState tick,
            AscRandomState random,
            AscSlabHeads slabs,
            int attributeCount,
            int tagCount,
            int pendingCount,
            int ascFactCount,
            int sessionFactCount)
        {
            Tick = tick;
            Random = random;
            Slabs = slabs;
            AttributeCount = attributeCount;
            TagCount = tagCount;
            PendingCount = pendingCount;
            AscFactCount = ascFactCount;
            SessionFactCount = sessionFactCount;
        }
    }

    /// <summary>
    /// 冻结 TargetPrepare/Publish 覆盖的完整 target durable authority。
    /// </summary>
    internal readonly struct RuntimeV1TargetAuthoritySnapshot
    {
        internal readonly AscLifecycle Lifecycle;
        internal readonly AscSlabHeads Slabs;
        internal readonly GasPayloadRangeAllocatorState PayloadState;
        internal readonly ActiveEffectSlot[] ActiveEffects;
        internal readonly AttributeValueSlot[] Attributes;
        internal readonly AttributeDirtyWord[] AttributeDirtyWords;
        internal readonly TagCountSlot[] TagCounts;
        internal readonly TagPresenceWord[] TagPresenceWords;
        internal readonly GasPayloadRangeRecord[] PayloadRanges;
        internal readonly GasPayloadValueSlot[] PayloadValues;

        /// <summary>
        /// 保存 TargetPublish 的全部组件与逐元素 buffer 快照。
        /// </summary>
        internal RuntimeV1TargetAuthoritySnapshot(
            AscLifecycle lifecycle,
            AscSlabHeads slabs,
            GasPayloadRangeAllocatorState payloadState,
            ActiveEffectSlot[] activeEffects,
            AttributeValueSlot[] attributes,
            AttributeDirtyWord[] attributeDirtyWords,
            TagCountSlot[] tagCounts,
            TagPresenceWord[] tagPresenceWords,
            GasPayloadRangeRecord[] payloadRanges,
            GasPayloadValueSlot[] payloadValues)
        {
            Lifecycle = lifecycle;
            Slabs = slabs;
            PayloadState = payloadState;
            ActiveEffects = activeEffects;
            Attributes = attributes;
            AttributeDirtyWords = attributeDirtyWords;
            TagCounts = tagCounts;
            TagPresenceWords = tagPresenceWords;
            PayloadRanges = payloadRanges;
            PayloadValues = payloadValues;
        }
    }
}
