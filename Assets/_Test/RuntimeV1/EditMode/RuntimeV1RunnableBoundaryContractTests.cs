using System;
using GAS.Runtime;
using NUnit.Framework;

namespace GAS.RuntimeV1.Tests.EditMode
{
    /// <summary>
    /// 验证 RuntimeV1Runnable 支持面在 RequestKey 分配前拒绝，以及 Accepted 请求的唯一终态消费合同。
    /// </summary>
    [TestFixture]
    [Category("RuntimeV1Runnable")]
    public class RuntimeV1RunnableBoundaryContractTests
    {
        private const ulong SimulationEpoch = 401;
        private static readonly BattleInstanceHandle Battle =
            new BattleInstanceHandle(SimulationEpoch, 801, 1);
        private static readonly OwnerAscHandle SourceAsc =
            new OwnerAscHandle(901, 1);

        /// <summary>
        /// 证明 Cancel、RemoveEffect 与非空 one-shot Ability payload 均不分配 key 或产生任何 ledger 写入。
        /// </summary>
        [Test]
        public void UnsupportedCommandsAndPayloads_DoNotAccept()
        {
            var gate = CreateGate();
            var port = new GasCommandPort(gate);
            var activation = new AbilityActivationHandle(SimulationEpoch, SourceAsc, 0, 1);
            var activeEffect = new ActiveEffectHandle(SimulationEpoch, SourceAsc, 0, 1);
            var grant = new GrantedAbilityHandle(SimulationEpoch, SourceAsc, 0, 1);
            var cancelContext = CreateContext(1, BoundaryTargetRef.None);
            var removeTarget = BoundaryTargetRef.ForAsc(in Battle, in SourceAsc);
            var removeContext = CreateContext(2, removeTarget);
            var payloadContext = CreateContext(3, BoundaryTargetRef.None);
            var payloadDescriptor = new BoundaryCommandPayloadDescriptor(
                1,
                GasBoundaryCommandPayloadKind.AbilityRequest);

            var cancel = port.RequestCancel(
                in cancelContext,
                in activation,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);
            var remove = port.RequestRemoveEffect(
                in removeContext,
                in activeEffect,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);
            var payload = port.RequestActivate(
                in payloadContext,
                in grant,
                in payloadDescriptor,
                new byte[] { 1 });

            AssertUnsupported(cancel);
            AssertUnsupported(remove);
            AssertUnsupported(payload);
            Assert.That(gate.TryFreezeIngressWindow(out var rejectedWindow), Is.False);
            Assert.That(rejectedWindow, Is.Empty);
            Assert.That(port.TryDrainRequestTerminals(out var rejectedTerminals), Is.False);
            Assert.That(rejectedTerminals, Is.Empty);

            var acceptedContext = CreateContext(4, BoundaryTargetRef.None);
            var accepted = port.RequestActivate(
                in acceptedContext,
                in grant,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);
            Assert.That(accepted.Status, Is.EqualTo(GasCommandAcceptStatus.Accepted));
            Assert.That(accepted.RequestSequence, Is.EqualTo(1));
            Assert.That(accepted.RequestKey.IsValid, Is.True);
        }

        /// <summary>
        /// 证明 ApplyEffect 的 None、selector 与 rule 目标在 source/无 source 下均于 RequestKey 前拒绝。
        /// </summary>
        [Test]
        public void ApplyEffectNonAscTargets_DoNotAcceptOrWriteJournal()
        {
            var gate = CreateGate();
            var port = new GasCommandPort(gate);
            var selector = BoundaryTargetRef.ForBattleSelector(in Battle, 91);
            var rule = BoundaryTargetRef.ForDefinitionRule(in Battle, 2);
            var selectorWithSource = CreateContext(31, selector, true);
            var selectorWithoutSource = CreateContext(32, selector, false);
            var ruleWithSource = CreateContext(33, rule, true);
            var ruleWithoutSource = CreateContext(34, rule, false);
            var noneWithSource = CreateContext(35, BoundaryTargetRef.None, true);

            AssertUnsupported(RequestApplyEffect(port, in selectorWithSource));
            AssertUnsupported(RequestApplyEffect(port, in selectorWithoutSource));
            AssertUnsupported(RequestApplyEffect(port, in ruleWithSource));
            AssertUnsupported(RequestApplyEffect(port, in ruleWithoutSource));
            AssertUnsupported(RequestApplyEffect(port, in noneWithSource));
            Assert.That(gate.TryFreezeIngressWindow(out var rejectedWindow), Is.False);
            Assert.That(rejectedWindow, Is.Empty);
            Assert.That(port.TryDrainRequestTerminals(out var rejectedTerminals), Is.False);
            Assert.That(rejectedTerminals, Is.Empty);
        }

        /// <summary>
        /// 证明普通业务终态可读且只 drain 一次、不会关闭 ingress，OwnerDisposal 只补齐仍未终态请求。
        /// </summary>
        [Test]
        public void RequestTerminalLedger_PublishesOnceAndKeepsIngressOpen()
        {
            var gate = CreateGate();
            var port = new GasCommandPort(gate);
            var grant = new GrantedAbilityHandle(SimulationEpoch, SourceAsc, 0, 1);
            var firstContext = CreateContext(11, BoundaryTargetRef.None);
            var first = port.RequestActivate(
                in firstContext,
                in grant,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);
            var activation = new AbilityActivationHandle(SimulationEpoch, SourceAsc, 0, 2);
            var terminal = GasRequestTerminal.ForAbility(
                first.RequestKey,
                GasBoundaryCommandKind.Activate,
                GasRequestTerminalStatus.Succeeded,
                GasAbilityCommandResult.Activated,
                activation);

            Assert.That(
                gate.TryPublishRequestTerminal(in terminal),
                Is.EqualTo(GasRequestTerminalPublishStatus.Published));
            Assert.That(
                gate.TryPublishRequestTerminal(in terminal),
                Is.EqualTo(GasRequestTerminalPublishStatus.Duplicate));
            Assert.That(port.TryReadRequestTerminal(first.RequestKey, out var observed), Is.True);
            Assert.That(observed, Is.EqualTo(terminal));
            Assert.That(port.TryDrainRequestTerminals(out var firstDrain), Is.True);
            Assert.That(firstDrain, Is.EqualTo(new[] { terminal }));
            Assert.That(port.TryDrainRequestTerminals(out var emptyDrain), Is.False);
            Assert.That(emptyDrain, Is.Empty);

            var secondContext = CreateContext(12, BoundaryTargetRef.None);
            var second = port.RequestActivate(
                in secondContext,
                in grant,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);
            Assert.That(second.Status, Is.EqualTo(GasCommandAcceptStatus.Accepted));
            gate.CloseForOwnerDisposal();

            Assert.That(port.TryDrainRequestTerminals(out var teardownDrain), Is.True);
            Assert.That(teardownDrain, Has.Length.EqualTo(1));
            Assert.That(teardownDrain[0].RequestKey, Is.EqualTo(second.RequestKey));
            Assert.That(
                teardownDrain[0].Status,
                Is.EqualTo(GasRequestTerminalStatus.OwnerDisposal));
            Assert.That(port.TryReadRequestTerminal(first.RequestKey, out var firstAfterClose), Is.True);
            Assert.That(firstAfterClose, Is.EqualTo(terminal));
        }

        /// <summary>
        /// 证明 plan 阶段的 invalid Definition 拒绝以零 ApplicationId 发布，且不会关闭普通 ingress。
        /// </summary>
        [Test]
        public void RejectedDefinitionTerminal_PublishesWithoutApplicationId()
        {
            var gate = CreateGate();
            var port = new GasCommandPort(gate);
            var target = BoundaryTargetRef.ForAsc(in Battle, in SourceAsc);
            var context = CreateContext(13, target);
            var accepted = port.RequestApplyEffect(
                in context,
                int.MaxValue,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);
            var terminal = GasRequestTerminal.ForEffect(
                accepted.RequestKey,
                GasRequestTerminalStatus.Rejected,
                GasEffectRequestResult.RejectedDefinition,
                0,
                default);
            var definitionRejectWithIdentity = GasRequestTerminal.ForEffect(
                accepted.RequestKey,
                GasRequestTerminalStatus.Rejected,
                GasEffectRequestResult.RejectedDefinition,
                1,
                default);
            var applicationRejectWithoutIdentity = GasRequestTerminal.ForEffect(
                accepted.RequestKey,
                GasRequestTerminalStatus.Rejected,
                GasEffectRequestResult.RejectedStaleBinding,
                0,
                default);
            var sourceUnavailable = GasRequestTerminal.ForEffect(
                accepted.RequestKey,
                GasRequestTerminalStatus.Rejected,
                GasEffectRequestResult.RejectedSourceUnavailable,
                0,
                default);

            Assert.That(accepted.Status, Is.EqualTo(GasCommandAcceptStatus.Accepted));
            Assert.That(terminal.IsWellFormed(), Is.True);
            Assert.That(definitionRejectWithIdentity.IsWellFormed(), Is.False);
            Assert.That(applicationRejectWithoutIdentity.IsWellFormed(), Is.False);
            Assert.That(sourceUnavailable.IsWellFormed(), Is.True);
            Assert.That(
                gate.TryPublishRequestTerminal(in terminal),
                Is.EqualTo(GasRequestTerminalPublishStatus.Published));
            Assert.That(port.TryReadRequestTerminal(accepted.RequestKey, out var observed), Is.True);
            Assert.That(observed.EffectResult, Is.EqualTo(GasEffectRequestResult.RejectedDefinition));
            Assert.That(observed.ApplicationId, Is.Zero);
            Assert.That(observed.HasActiveEffect, Is.False);

            var tailContext = CreateContext(14, BoundaryTargetRef.None);
            var grant = new GrantedAbilityHandle(SimulationEpoch, SourceAsc, 0, 1);
            var tail = port.RequestActivate(
                in tailContext,
                in grant,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);
            Assert.That(tail.Status, Is.EqualTo(GasCommandAcceptStatus.Accepted));
        }

        /// <summary>
        /// 证明 BattleTerminal 以独立原因关闭 Gate，并只为尚未终态请求产生战局终态。
        /// </summary>
        [Test]
        public void BattleTerminal_ClosesGateWithDistinctTerminalCause()
        {
            var gate = CreateGate();
            var port = new GasCommandPort(gate);
            var grant = new GrantedAbilityHandle(SimulationEpoch, SourceAsc, 0, 1);
            var context = CreateContext(21, BoundaryTargetRef.None);
            var accepted = port.RequestActivate(
                in context,
                in grant,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);

            gate.CloseForBattleTerminal(in Battle, 7);

            Assert.That(port.TryReadRequestTerminal(accepted.RequestKey, out var terminal), Is.True);
            Assert.That(terminal.Status, Is.EqualTo(GasRequestTerminalStatus.BattleTerminal));
            Assert.That(terminal.BattleInstance, Is.EqualTo(Battle));
            Assert.That(terminal.BattleOutcomeCode, Is.EqualTo(7));
            var tailContext = CreateContext(22, BoundaryTargetRef.None);
            var tail = port.RequestActivate(
                in tailContext,
                in grant,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);
            Assert.That(tail.Status, Is.EqualTo(GasCommandAcceptStatus.BattleTerminalClosed));
            Assert.That(tail.RequestSequence, Is.Zero);
        }

        /// <summary>
        /// 创建绑定单 Battle 与单 Ready ASC 的 RuntimeV1Runnable Gate。
        /// </summary>
        private static SessionIngressGate CreateGate()
        {
            var gate = new SessionIngressGate();
            var snapshot = new GasIngressAuthoritySnapshot(
                SimulationEpoch,
                8,
                32,
                new[]
                {
                    new GasIngressBattleAuthority(
                        Battle,
                        GasBattleInstanceState.Running,
                        true),
                },
                new[]
                {
                    new GasIngressAscAuthority(
                        SourceAsc,
                        Battle,
                        GasAscRegistryState.Ready),
                });
            Assert.That(gate.ReplaceAuthoritySnapshot(snapshot), Is.True);
            return gate;
        }

        /// <summary>
        /// 创建 source、Battle 与请求序号均规范的命令上下文。
        /// </summary>
        private static GasBoundaryCommandContext CreateContext(
            ulong requestId,
            in BoundaryTargetRef target)
        {
            return CreateContext(requestId, in target, true);
        }

        /// <summary>
        /// 创建可显式选择 source 是否存在的规范命令上下文。
        /// </summary>
        private static GasBoundaryCommandContext CreateContext(
            ulong requestId,
            in BoundaryTargetRef target,
            bool hasSource)
        {
            return new GasBoundaryCommandContext(
                SimulationEpoch,
                requestId,
                requestId,
                0,
                hasSource,
                Battle,
                hasSource ? SourceAsc : default,
                target);
        }

        /// <summary>
        /// 以无 payload 的 9203 调用 ApplyEffect，供 profile target precheck 复用。
        /// </summary>
        private static GasCommandAcceptResult RequestApplyEffect(
            GasCommandPort port,
            in GasBoundaryCommandContext context)
        {
            return port.RequestApplyEffect(
                in context,
                9203,
                BoundaryCommandPayloadDescriptor.None,
                ReadOnlySpan<byte>.Empty);
        }

        /// <summary>
        /// 断言 profile 拒绝既不分配 RequestSequence，也不形成可用 RequestKey。
        /// </summary>
        private static void AssertUnsupported(GasCommandAcceptResult result)
        {
            Assert.That(
                result.Status,
                Is.EqualTo(GasCommandAcceptStatus.UnsupportedByRuntimeV1Profile));
            Assert.That(result.RequestSequence, Is.Zero);
            Assert.That(result.RequestKey.IsValid, Is.False);
            Assert.That(result.IsAccepted, Is.False);
        }
    }
}
