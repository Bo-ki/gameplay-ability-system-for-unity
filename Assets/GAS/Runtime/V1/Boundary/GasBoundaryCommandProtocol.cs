using System;

namespace GAS.Runtime
{
    /// <summary>
    /// 定义 Boundary 入站唯一允许提交的五种命令，避免运行时保留通用或兼容命令入口。
    /// </summary>
    public enum GasBoundaryCommandKind : byte
    {
        Activate = 1,
        Commit = 2,
        Cancel = 3,
        ApplyEffect = 4,
        RemoveEffect = 5,
    }

    /// <summary>
    /// 标识已接受命令在持久 managed journal 中的终态，供消费确认与 Fault 审计使用。
    /// </summary>
    public enum GasBoundaryCommandState : byte
    {
        Pending = 0,
        Sealed = 1,
        Consumed = 2,
        FaultTerminated = 3,
    }

    /// <summary>
    /// 定义消费时才解析的 Boundary 目标引用种类，禁止外部直接提交 Entity。
    /// </summary>
    public enum GasBoundaryTargetKind : byte
    {
        None = 0,
        Asc = 1,
        BattleSelector = 2,
        DefinitionRule = 3,
    }

    /// <summary>
    /// 定义目标引用的唯一解析时点，保证提交线程不读取 ECS 世界。
    /// </summary>
    public enum GasBoundaryTargetResolutionPolicy : byte
    {
        None = 0,
        ResolveAtConsume = 1,
    }

    /// <summary>
    /// 区分 Boundary 命令 payload 的闭世界用途，具体字节布局由 SchemaVersion 冻结。
    /// </summary>
    public enum GasBoundaryCommandPayloadKind : ushort
    {
        None = 0,
        AbilityRequest = 1,
        AbilityCancellation = 2,
        EffectApplication = 3,
        EffectRemoval = 4,
    }

    /// <summary>
    /// 表示 Boundary 命令在唯一入口被接受或拒绝的精确原因。
    /// </summary>
    public enum GasCommandAcceptStatus : byte
    {
        None = 0,
        Accepted = 1,
        DuplicateAccepted = 2,
        GateUnbound = 3,
        FaultClosed = 4,
        InvalidRequest = 5,
        EpochMismatch = 6,
        BattleNotFound = 7,
        BattleNotAccepting = 8,
        SourceRequired = 9,
        SourceMustBeEmpty = 10,
        SourceMembershipMissing = 11,
        SourceBattleMismatch = 12,
        TargetInvalid = 13,
        TargetMembershipMissing = 14,
        TargetBattleMismatch = 15,
        HandleInvalid = 16,
        HandleKindMismatch = 17,
        HandleOwnerMismatch = 18,
        PayloadInvalid = 19,
        IngressCountExceeded = 20,
        IngressPayloadBytesExceeded = 21,
        RequestIdConflict = 22,
        RequestSequenceExhausted = 23,
    }

    /// <summary>
    /// 保存不含 ECS 对象的延迟目标身份，并把具体解析留给 Ingress 消费阶段。
    /// </summary>
    public readonly struct BoundaryTargetRef : IEquatable<BoundaryTargetRef>
    {
        public readonly GasBoundaryTargetKind Kind;
        public readonly GasBoundaryTargetResolutionPolicy ResolutionPolicy;
        public readonly ulong SimulationEpoch;
        public readonly BattleInstanceHandle BattleInstance;
        public readonly OwnerAscHandle TargetAsc;
        public readonly ulong SelectorStableId;
        public readonly int DefinitionRuleIndex;

        /// <summary>
        /// 返回不携带任何目标的规范空引用。
        /// </summary>
        public static BoundaryTargetRef None => default;

        /// <summary>
        /// 创建在消费阶段按稳定 ASC 身份解析的目标引用。
        /// </summary>
        public static BoundaryTargetRef ForAsc(
            in BattleInstanceHandle battleInstance,
            in OwnerAscHandle targetAsc)
        {
            return new BoundaryTargetRef(
                GasBoundaryTargetKind.Asc,
                battleInstance.SimulationEpoch,
                battleInstance,
                targetAsc,
                0,
                0);
        }

        /// <summary>
        /// 创建在消费阶段按战局内稳定选择器解析的目标引用。
        /// </summary>
        public static BoundaryTargetRef ForBattleSelector(
            in BattleInstanceHandle battleInstance,
            ulong selectorStableId)
        {
            return new BoundaryTargetRef(
                GasBoundaryTargetKind.BattleSelector,
                battleInstance.SimulationEpoch,
                battleInstance,
                default,
                selectorStableId,
                0);
        }

        /// <summary>
        /// 创建在消费阶段按冻结定义规则解析的目标引用。
        /// </summary>
        public static BoundaryTargetRef ForDefinitionRule(
            in BattleInstanceHandle battleInstance,
            int definitionRuleIndex)
        {
            return new BoundaryTargetRef(
                GasBoundaryTargetKind.DefinitionRule,
                battleInstance.SimulationEpoch,
                battleInstance,
                default,
                0,
                definitionRuleIndex);
        }

        /// <summary>
        /// 比较两个目标引用的全部稳定值字段。
        /// </summary>
        public bool Equals(BoundaryTargetRef other)
        {
            return Kind == other.Kind &&
                   ResolutionPolicy == other.ResolutionPolicy &&
                   SimulationEpoch == other.SimulationEpoch &&
                   BattleInstance.Equals(other.BattleInstance) &&
                   TargetAsc.Equals(other.TargetAsc) &&
                   SelectorStableId == other.SelectorStableId &&
                   DefinitionRuleIndex == other.DefinitionRuleIndex;
        }

        /// <summary>
        /// 比较对象是否为相同目标引用。
        /// </summary>
        public override bool Equals(object obj)
        {
            return obj is BoundaryTargetRef other && Equals(other);
        }

        /// <summary>
        /// 计算覆盖目标引用全部稳定值字段的哈希码。
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int)Kind;
                hashCode = (hashCode * 397) ^ (int)ResolutionPolicy;
                hashCode = (hashCode * 397) ^ (int)(SimulationEpoch ^ (SimulationEpoch >> 32));
                hashCode = (hashCode * 397) ^ BattleInstance.GetHashCode();
                hashCode = (hashCode * 397) ^ TargetAsc.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)(SelectorStableId ^ (SelectorStableId >> 32));
                return (hashCode * 397) ^ DefinitionRuleIndex;
            }
        }

        /// <summary>
        /// 使用闭世界种类与完整稳定身份创建延迟目标引用。
        /// </summary>
        private BoundaryTargetRef(
            GasBoundaryTargetKind kind,
            ulong simulationEpoch,
            BattleInstanceHandle battleInstance,
            OwnerAscHandle targetAsc,
            ulong selectorStableId,
            int definitionRuleIndex)
        {
            Kind = kind;
            ResolutionPolicy = GasBoundaryTargetResolutionPolicy.ResolveAtConsume;
            SimulationEpoch = simulationEpoch;
            BattleInstance = battleInstance;
            TargetAsc = targetAsc;
            SelectorStableId = selectorStableId;
            DefinitionRuleIndex = definitionRuleIndex;
        }
    }

    /// <summary>
    /// 冻结 payload 的 schema 与用途；长度、内容哈希由唯一入口从真实字节计算。
    /// </summary>
    public readonly struct BoundaryCommandPayloadDescriptor
    {
        public readonly ushort SchemaVersion;
        public readonly GasBoundaryCommandPayloadKind Kind;

        /// <summary>
        /// 使用版本与闭世界用途创建命令 payload 描述。
        /// </summary>
        public BoundaryCommandPayloadDescriptor(
            ushort schemaVersion,
            GasBoundaryCommandPayloadKind kind)
        {
            SchemaVersion = schemaVersion;
            Kind = kind;
        }

        /// <summary>
        /// 返回没有 payload 时唯一合法的空描述。
        /// </summary>
        public static BoundaryCommandPayloadDescriptor None => default;
    }

    /// <summary>
    /// 保存五种 typed 请求共享的稳定边界身份与时序字段，不持有 World、EntityManager 或 Entity。
    /// </summary>
    public readonly struct GasBoundaryCommandContext
    {
        public readonly ulong SimulationEpoch;
        public readonly ulong RequestId;
        public readonly ulong SourceSequence;
        public readonly ulong AvailableTick;
        public readonly bool HasSource;
        public readonly BattleInstanceHandle BattleInstance;
        public readonly OwnerAscHandle SourceAsc;
        public readonly BoundaryTargetRef Target;

        /// <summary>
        /// 使用完整稳定身份、独立 SourceSequence 与可用 Tick 创建请求上下文。
        /// </summary>
        public GasBoundaryCommandContext(
            ulong simulationEpoch,
            ulong requestId,
            ulong sourceSequence,
            ulong availableTick,
            bool hasSource,
            in BattleInstanceHandle battleInstance,
            in OwnerAscHandle sourceAsc,
            in BoundaryTargetRef target)
        {
            SimulationEpoch = simulationEpoch;
            RequestId = requestId;
            SourceSequence = sourceSequence;
            AvailableTick = availableTick;
            HasSource = hasSource;
            BattleInstance = battleInstance;
            SourceAsc = sourceAsc;
            Target = target;
        }
    }

    /// <summary>
    /// 返回请求接受状态与唯一 RequestSequence，使重复请求可复用原序号。
    /// </summary>
    public readonly struct GasCommandAcceptResult
    {
        public readonly GasCommandAcceptStatus Status;
        public readonly ulong RequestId;
        public readonly ulong RequestSequence;

        public bool IsAccepted => Status == GasCommandAcceptStatus.Accepted ||
                                  Status == GasCommandAcceptStatus.DuplicateAccepted;

        /// <summary>
        /// 创建携带状态、请求身份与已分配序号的接受结果。
        /// </summary>
        internal GasCommandAcceptResult(
            GasCommandAcceptStatus status,
            ulong requestId,
            ulong requestSequence)
        {
            Status = status;
            RequestId = requestId;
            RequestSequence = requestSequence;
        }
    }

    /// <summary>
    /// 定义 Runtime v1 唯一 Boundary 命令端口，只暴露五个强类型请求入口。
    /// </summary>
    public interface IGasCommandPort
    {
        /// <summary>
        /// 请求激活一个已授予 Ability，并把 payload 深复制到持久 journal。
        /// </summary>
        GasCommandAcceptResult RequestActivate(
            in GasBoundaryCommandContext context,
            in GrantedAbilityHandle ability,
            in BoundaryCommandPayloadDescriptor payloadDescriptor,
            ReadOnlySpan<byte> payload);

        /// <summary>
        /// 请求提交一个存活激活实例，并把 payload 深复制到持久 journal。
        /// </summary>
        GasCommandAcceptResult RequestCommit(
            in GasBoundaryCommandContext context,
            in AbilityActivationHandle activation,
            in BoundaryCommandPayloadDescriptor payloadDescriptor,
            ReadOnlySpan<byte> payload);

        /// <summary>
        /// 请求取消一个存活激活实例，并把 payload 深复制到持久 journal。
        /// </summary>
        GasCommandAcceptResult RequestCancel(
            in GasBoundaryCommandContext context,
            in AbilityActivationHandle activation,
            in BoundaryCommandPayloadDescriptor payloadDescriptor,
            ReadOnlySpan<byte> payload);

        /// <summary>
        /// 请求向延迟目标应用稳定定义，并把 payload 深复制到持久 journal。
        /// </summary>
        GasCommandAcceptResult RequestApplyEffect(
            in GasBoundaryCommandContext context,
            int effectDefinitionId,
            in BoundaryCommandPayloadDescriptor payloadDescriptor,
            ReadOnlySpan<byte> payload);

        /// <summary>
        /// 请求移除目标 ASC 上的存活效果，并把 payload 深复制到持久 journal。
        /// </summary>
        GasCommandAcceptResult RequestRemoveEffect(
            in GasBoundaryCommandContext context,
            in ActiveEffectHandle activeEffect,
            in BoundaryCommandPayloadDescriptor payloadDescriptor,
            ReadOnlySpan<byte> payload);
    }

    /// <summary>
    /// 在 typed Port 与 Gate 之间承载尚未分配 RequestSequence 的不可变候选命令。
    /// </summary>
    internal readonly struct GasBoundaryCommandDraft
    {
        internal readonly GasBoundaryCommandKind Kind;
        internal readonly GasBoundaryCommandContext Context;
        internal readonly StableHandleDiagnosticCarrier Handle;
        internal readonly int DefinitionId;
        internal readonly BoundaryCommandPayloadDescriptor PayloadDescriptor;

        /// <summary>
        /// 使用 typed 入口确定的命令种类与领域句柄创建候选命令。
        /// </summary>
        internal GasBoundaryCommandDraft(
            GasBoundaryCommandKind kind,
            in GasBoundaryCommandContext context,
            in StableHandleDiagnosticCarrier handle,
            int definitionId,
            in BoundaryCommandPayloadDescriptor payloadDescriptor)
        {
            Kind = kind;
            Context = context;
            Handle = handle;
            DefinitionId = definitionId;
            PayloadDescriptor = payloadDescriptor;
        }
    }

    /// <summary>
    /// 保存一次已接受请求的完整不可变命令与深复制 payload，并仅由 Gate 推进消费终态。
    /// </summary>
    internal sealed class GasBoundaryJournalRecord
    {
        private readonly byte[] _payload;

        internal GasBoundaryCommandState State { get; private set; }
        internal GasBoundaryCommandKind Kind { get; }
        internal ulong SimulationEpoch { get; }
        internal ulong RequestId { get; }
        internal ulong RequestSequence { get; }
        internal ulong SourceSequence { get; }
        internal ulong AvailableTick { get; }
        internal byte HasSource { get; }
        internal BattleInstanceHandle BattleInstance { get; }
        internal OwnerAscHandle SourceAsc { get; }
        internal BoundaryTargetRef Target { get; }
        internal StableHandleDiagnosticCarrier Handle { get; }
        internal int DefinitionId { get; }
        internal ushort PayloadSchemaVersion { get; }
        internal GasBoundaryCommandPayloadKind PayloadKind { get; }
        internal int PayloadLength { get; }
        internal ulong PayloadHash { get; }
        internal ulong CommandHash { get; }
        internal ReadOnlyMemory<byte> Payload => new ReadOnlyMemory<byte>(_payload);

        /// <summary>
        /// 从候选命令、唯一序号与已深复制字节创建 Accepted journal 记录。
        /// </summary>
        internal GasBoundaryJournalRecord(
            in GasBoundaryCommandDraft draft,
            ulong requestSequence,
            byte[] payload,
            ulong payloadHash)
        {
            State = GasBoundaryCommandState.Pending;
            Kind = draft.Kind;
            SimulationEpoch = draft.Context.SimulationEpoch;
            RequestId = draft.Context.RequestId;
            RequestSequence = requestSequence;
            SourceSequence = draft.Context.SourceSequence;
            AvailableTick = draft.Context.AvailableTick;
            HasSource = draft.Context.HasSource ? (byte)1 : (byte)0;
            BattleInstance = draft.Context.BattleInstance;
            SourceAsc = draft.Context.SourceAsc;
            Target = draft.Context.Target;
            Handle = draft.Handle;
            DefinitionId = draft.DefinitionId;
            PayloadSchemaVersion = draft.PayloadDescriptor.SchemaVersion;
            PayloadKind = draft.PayloadDescriptor.Kind;
            PayloadLength = payload.Length;
            PayloadHash = payloadHash;
            _payload = payload;
            CommandHash = ComputeCommandHash();
        }

        /// <summary>
        /// 判断相同 RequestId 的候选命令是否与原接受内容逐字段且逐字节完全相同。
        /// </summary>
        internal bool IsExactDuplicate(
            in GasBoundaryCommandDraft draft,
            ulong payloadHash,
            ReadOnlySpan<byte> payload)
        {
            if (!HasSameHeader(draft) ||
                PayloadHash != payloadHash ||
                PayloadLength != payload.Length)
                return false;

            for (var index = 0; index < payload.Length; index++)
            {
                if (_payload[index] != payload[index])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 将 Sealed 记录推进到 Consumed，重复确认保持幂等。
        /// </summary>
        internal bool MarkConsumed()
        {
            if (State != GasBoundaryCommandState.Sealed)
                return false;

            State = GasBoundaryCommandState.Consumed;
            return true;
        }

        /// <summary>
        /// 将已完整追加的 Pending 记录推进到 Sealed，使外部只能观察到完整 Accepted 内容。
        /// </summary>
        internal bool MarkSealed()
        {
            if (State != GasBoundaryCommandState.Pending)
                return false;

            State = GasBoundaryCommandState.Sealed;
            return true;
        }

        /// <summary>
        /// 将仍未消费的 Sealed 记录推进到 FaultTerminated。
        /// </summary>
        internal bool MarkFaultTerminated()
        {
            if (State != GasBoundaryCommandState.Sealed)
                return false;

            State = GasBoundaryCommandState.FaultTerminated;
            return true;
        }

        /// <summary>
        /// 比较除 RequestSequence 外所有请求头字段，供 exact duplicate 判定复用。
        /// </summary>
        private bool HasSameHeader(in GasBoundaryCommandDraft draft)
        {
            return Kind == draft.Kind &&
                   SimulationEpoch == draft.Context.SimulationEpoch &&
                   RequestId == draft.Context.RequestId &&
                   SourceSequence == draft.Context.SourceSequence &&
                   AvailableTick == draft.Context.AvailableTick &&
                   HasSource == (draft.Context.HasSource ? (byte)1 : (byte)0) &&
                   BattleInstance.Equals(draft.Context.BattleInstance) &&
                   SourceAsc.Equals(draft.Context.SourceAsc) &&
                   Target.Equals(draft.Context.Target) &&
                   SameHandle(Handle, draft.Handle) &&
                   DefinitionId == draft.DefinitionId &&
                   PayloadSchemaVersion == draft.PayloadDescriptor.SchemaVersion &&
                   PayloadKind == draft.PayloadDescriptor.Kind;
        }

        /// <summary>
        /// 比较无类型诊断载体的全部稳定身份字段。
        /// </summary>
        private static bool SameHandle(
            in StableHandleDiagnosticCarrier left,
            in StableHandleDiagnosticCarrier right)
        {
            return left.SimulationEpoch == right.SimulationEpoch &&
                   left.OwnerAsc.Equals(right.OwnerAsc) &&
                   left.SlotIndex == right.SlotIndex &&
                   left.SlotGeneration == right.SlotGeneration &&
                   left.Kind == right.Kind;
        }

        /// <summary>
        /// 计算覆盖完整命令头与 payload 指纹的稳定 FNV-1a 哈希。
        /// </summary>
        private ulong ComputeCommandHash()
        {
            var hash = GasBoundaryFnv1A64.Create();
            AppendPrimaryIdentity(ref hash);
            AppendRoutingIdentity(ref hash);
            AppendPayloadIdentity(ref hash);
            return hash.Value;
        }

        /// <summary>
        /// 将请求种类、时序与来源身份追加到命令指纹。
        /// </summary>
        private void AppendPrimaryIdentity(ref GasBoundaryFnv1A64 hash)
        {
            hash.AddByte((byte)Kind);
            hash.AddUInt64(SimulationEpoch);
            hash.AddUInt64(RequestId);
            hash.AddUInt64(RequestSequence);
            hash.AddUInt64(SourceSequence);
            hash.AddUInt64(AvailableTick);
            hash.AddByte(HasSource);
            hash.AddUInt64(BattleInstance.BattleStableId);
            hash.AddUInt32(BattleInstance.BattleGeneration);
            hash.AddUInt64(SourceAsc.AscStableId);
            hash.AddUInt32(SourceAsc.AscGeneration);
        }

        /// <summary>
        /// 将延迟目标与领域句柄追加到命令指纹。
        /// </summary>
        private void AppendRoutingIdentity(ref GasBoundaryFnv1A64 hash)
        {
            hash.AddByte((byte)Target.Kind);
            hash.AddByte((byte)Target.ResolutionPolicy);
            hash.AddUInt64(Target.SimulationEpoch);
            hash.AddUInt64(Target.BattleInstance.BattleStableId);
            hash.AddUInt32(Target.BattleInstance.BattleGeneration);
            hash.AddUInt64(Target.TargetAsc.AscStableId);
            hash.AddUInt32(Target.TargetAsc.AscGeneration);
            hash.AddUInt64(Target.SelectorStableId);
            hash.AddInt32(Target.DefinitionRuleIndex);
            hash.AddUInt64(Handle.SimulationEpoch);
            hash.AddUInt64(Handle.OwnerAsc.AscStableId);
            hash.AddUInt32(Handle.OwnerAsc.AscGeneration);
            hash.AddInt32(Handle.SlotIndex);
            hash.AddUInt32(Handle.SlotGeneration);
            hash.AddByte((byte)Handle.Kind);
        }

        /// <summary>
        /// 将定义与版本化 payload 元数据追加到命令指纹。
        /// </summary>
        private void AppendPayloadIdentity(ref GasBoundaryFnv1A64 hash)
        {
            hash.AddInt32(DefinitionId);
            hash.AddUInt16(PayloadSchemaVersion);
            hash.AddUInt16((ushort)PayloadKind);
            hash.AddInt32(PayloadLength);
            hash.AddUInt64(PayloadHash);
        }
    }

    /// <summary>
    /// 提供与平台字节序无关的 FNV-1a 64 位增量哈希，供 payload 与 Fault 审计复用。
    /// </summary>
    internal struct GasBoundaryFnv1A64
    {
        private const ulong OffsetBasis = 14695981039346656037UL;
        private const ulong Prime = 1099511628211UL;
        private ulong _value;

        internal ulong Value => _value;

        /// <summary>
        /// 创建以标准 offset basis 初始化的 FNV-1a 累加器。
        /// </summary>
        internal static GasBoundaryFnv1A64 Create()
        {
            return new GasBoundaryFnv1A64 { _value = OffsetBasis };
        }

        /// <summary>
        /// 按 FNV-1a 规则追加一个字节。
        /// </summary>
        internal void AddByte(byte value)
        {
            _value ^= value;
            _value *= Prime;
        }

        /// <summary>
        /// 以固定小端顺序追加一个无符号 16 位整数。
        /// </summary>
        internal void AddUInt16(ushort value)
        {
            AddByte((byte)value);
            AddByte((byte)(value >> 8));
        }

        /// <summary>
        /// 以固定小端顺序追加一个有符号 32 位整数。
        /// </summary>
        internal void AddInt32(int value)
        {
            AddUInt32(unchecked((uint)value));
        }

        /// <summary>
        /// 以固定小端顺序追加一个无符号 32 位整数。
        /// </summary>
        internal void AddUInt32(uint value)
        {
            AddUInt16((ushort)value);
            AddUInt16((ushort)(value >> 16));
        }

        /// <summary>
        /// 以固定小端顺序追加一个无符号 64 位整数。
        /// </summary>
        internal void AddUInt64(ulong value)
        {
            AddUInt32((uint)value);
            AddUInt32((uint)(value >> 32));
        }

        /// <summary>
        /// 按原始顺序追加完整 payload 字节。
        /// </summary>
        internal void AddBytes(ReadOnlySpan<byte> bytes)
        {
            for (var index = 0; index < bytes.Length; index++)
                AddByte(bytes[index]);
        }
    }
}
