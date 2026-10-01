using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 实现 Runtime v1 唯一 Boundary 命令入口；实例只持 SessionIngressGate 且不暴露通用提交方法。
    /// </summary>
    public sealed class GasCommandPort : IGasCommandPort
    {
        private readonly SessionIngressGate _gate;

        /// <summary>
        /// 由 World owner 使用唯一 Session Gate 创建端口，外部调用方不能自行替换权威源。
        /// </summary>
        internal GasCommandPort(SessionIngressGate gate)
        {
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        }

        /// <summary>
        /// 将 GrantedAbilityHandle 固化为 Activate 候选并交给唯一 Gate 原子接受。
        /// </summary>
        public GasCommandAcceptResult RequestActivate(
            in GasBoundaryCommandContext context,
            in GrantedAbilityHandle ability,
            in BoundaryCommandPayloadDescriptor payloadDescriptor,
            ReadOnlySpan<byte> payload)
        {
            var handle = ability.ToDiagnosticCarrier();
            return Submit(
                GasBoundaryCommandKind.Activate,
                context,
                handle,
                0,
                payloadDescriptor,
                payload);
        }

        /// <summary>
        /// 将 AbilityActivationHandle 与 context 中可选的规范 ASC target 固化为 Commit 候选。
        /// </summary>
        public GasCommandAcceptResult RequestCommit(
            in GasBoundaryCommandContext context,
            in AbilityActivationHandle activation,
            in BoundaryCommandPayloadDescriptor payloadDescriptor,
            ReadOnlySpan<byte> payload)
        {
            var handle = activation.ToDiagnosticCarrier();
            return Submit(
                GasBoundaryCommandKind.Commit,
                context,
                handle,
                0,
                payloadDescriptor,
                payload);
        }

        /// <summary>
        /// 将 AbilityActivationHandle 固化为 Cancel 候选并交给唯一 Gate 原子接受。
        /// </summary>
        public GasCommandAcceptResult RequestCancel(
            in GasBoundaryCommandContext context,
            in AbilityActivationHandle activation,
            in BoundaryCommandPayloadDescriptor payloadDescriptor,
            ReadOnlySpan<byte> payload)
        {
            var handle = activation.ToDiagnosticCarrier();
            return Submit(
                GasBoundaryCommandKind.Cancel,
                context,
                handle,
                0,
                payloadDescriptor,
                payload);
        }

        /// <summary>
        /// 将稳定效果定义固化为无槽句柄的 ApplyEffect 候选并交给唯一 Gate 原子接受。
        /// </summary>
        public GasCommandAcceptResult RequestApplyEffect(
            in GasBoundaryCommandContext context,
            int effectDefinitionId,
            in BoundaryCommandPayloadDescriptor payloadDescriptor,
            ReadOnlySpan<byte> payload)
        {
            var emptyHandle = default(StableHandleDiagnosticCarrier);
            return Submit(
                GasBoundaryCommandKind.ApplyEffect,
                context,
                emptyHandle,
                effectDefinitionId,
                payloadDescriptor,
                payload);
        }

        /// <summary>
        /// 将 ActiveEffectHandle 固化为 RemoveEffect 候选并交给唯一 Gate 原子接受。
        /// </summary>
        public GasCommandAcceptResult RequestRemoveEffect(
            in GasBoundaryCommandContext context,
            in ActiveEffectHandle activeEffect,
            in BoundaryCommandPayloadDescriptor payloadDescriptor,
            ReadOnlySpan<byte> payload)
        {
            var handle = activeEffect.ToDiagnosticCarrier();
            return Submit(
                GasBoundaryCommandKind.RemoveEffect,
                context,
                handle,
                0,
                payloadDescriptor,
                payload);
        }

        /// <summary>
        /// 从 Gate 持久 ledger 幂等读取指定 Accepted RequestKey 的唯一业务终态。
        /// </summary>
        public bool TryReadRequestTerminal(
            in GasRequestKey requestKey,
            out GasRequestTerminal terminal)
        {
            return _gate.TryReadRequestTerminal(in requestKey, out terminal);
        }

        /// <summary>
        /// 按首次发布顺序取走尚未 drain 的 RequestTerminal，不删除可读取的持久 ledger。
        /// </summary>
        public bool TryDrainRequestTerminals(out GasRequestTerminal[] terminals)
        {
            return _gate.TryDrainRequestTerminals(out terminals);
        }

        /// <summary>
        /// 仅在 Port 内部组装不可变 draft，保持 public API 不存在 raw generic submit。
        /// </summary>
        private GasCommandAcceptResult Submit(
            GasBoundaryCommandKind kind,
            in GasBoundaryCommandContext context,
            in StableHandleDiagnosticCarrier handle,
            int definitionId,
            in BoundaryCommandPayloadDescriptor payloadDescriptor,
            ReadOnlySpan<byte> payload)
        {
            var draft = new GasBoundaryCommandDraft(
                kind,
                context,
                handle,
                definitionId,
                payloadDescriptor);
            return _gate.TryAccept(draft, payload);
        }
    }
}
