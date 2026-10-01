using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 标识 Runtime v1 可投影到边界的闭世界事实类别，具体解释由版本化 payload schema 决定。
    /// </summary>
    public enum GasBoundaryFactKind : ushort
    {
        None,
        AttributeChanged,
        TagChanged,
        AbilityLifecycle,
        EffectLifecycle,
        Cue,
        BattleOutcome,
        SessionLifecycle,
        Fault,
        Death,
        ExecutionCalculation,
        PeriodTick,
    }

    /// <summary>
    /// 标识 inline Boundary payload 的闭世界数据形状，禁止借此引用 owner 外部存储。
    /// </summary>
    public enum GasBoundaryPayloadKind : byte
    {
        None,
        IntegerPair,
        ScalarPair,
        StableIdentityPair,
        AttributeDelta,
        Death,
    }

    /// <summary>
    /// 保存事实交付的全局去重键，物理 owner 与持久单调序号共同阻断跨 outbox 碰撞。
    /// </summary>
    public struct BoundaryEventId
    {
        public ulong SimulationEpoch;
        public GasBoundaryOwnerKind OwnerKind;
        public ulong OwnerStableId;
        public uint OwnerGeneration;
        public ulong OwnerSequence;
    }

    /// <summary>
    /// 保存由边界 schema 解释的版本化 unmanaged tagged payload，cleanup shell 不依赖外部数据即可交付。
    /// </summary>
    public struct BoundaryFactPayload
    {
        public ushort SchemaVersion;
        public GasBoundaryPayloadKind Kind;
        public long Integer0;
        public long Integer1;
        /// <summary>
        /// 保存定义或业务 schema 的第三个整数槽；零表示当前事实不携带该字段。
        /// </summary>
        public long Integer2;
        public float Scalar0;
        public float Scalar1;
        public float Scalar2;
        public float Scalar3;
        public float Scalar4;
        public float Scalar5;
        public float Scalar6;
        public float Scalar7;
        public float Scalar8;
        public float Scalar9;
        public ulong StableId0;
        public ulong StableId1;
        public ulong StableId2;
        public uint Generation0;
        public uint Generation1;
        public uint Generation2;
    }

    /// <summary>
    /// 保存 Drain 前不能丢失的自包含最终事实，ASC 与 Session cleanup outbox 各自拥有唯一事实集合。
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct BoundaryFactBuffer : ICleanupBufferElementData
    {
        public BoundaryEventId EventId;
        public GasBoundaryFactScope Scope;
        public GasBoundaryFactPlane Plane;
        public ulong ScopeStableId;
        public uint ScopeGeneration;
        public ulong BattleInstanceId;
        public uint BattleInstanceGeneration;
        public ulong OwnerScenarioUnitId;
        public OwnerAscHandle SourceAsc;
        public OwnerAscHandle TargetAsc;
        public ActiveEffectHandle CueActiveEffect;
        public uint CueActiveCycleOrdinal;
        public ushort CueDefinitionOrdinal;
        public ulong SimulationTick;
        public ushort SemanticPhaseOrdinal;
        public ushort WorkClassOrdinal;
        public ulong ParentCausalityId;
        public ulong SemanticId;
        public GasBoundaryFactKind Kind;
        public BoundaryFactPayload Payload;
    }

    /// <summary>
    /// 承载 final-publish 后由 managed fence 交给唯一 Gate ledger 的 typed RequestTerminal intent。
    /// </summary>
    [InternalBufferCapacity(0)]
    internal struct GasRequestTerminalIntent : IBufferElementData
    {
        public GasRequestTerminal Terminal;

        /// <summary>
        /// 从已经完成 invariant 校验的公开终态创建单次桥接 intent。
        /// </summary>
        internal static GasRequestTerminalIntent Create(in GasRequestTerminal terminal)
        {
            return new GasRequestTerminalIntent { Terminal = terminal };
        }
    }

    /// <summary>
    /// 冻结 outbox 物理 owner 与两阶段接管身份，使 owner 销毁后的空 shell 仍能生成 NoFactReceipt。
    /// </summary>
    public struct BoundaryDrainState : ICleanupComponentData
    {
        public ulong SimulationEpoch;
        public GasBoundaryOwnerKind OwnerKind;
        public ulong OwnerStableId;
        public uint OwnerGeneration;
        public ulong NextOwnerSequence;
        public GasBoundaryDrainPhase Phase;
        public ulong BatchId;
        public ulong InFlightWatermark;

        /// <summary>
        /// 使用完整物理 owner key 与显式首序号创建可持久递增的 drain 状态。
        /// </summary>
        public static BoundaryDrainState Create(
            ulong simulationEpoch,
            GasBoundaryOwnerKind ownerKind,
            ulong ownerStableId,
            uint ownerGeneration,
            ulong firstOwnerSequence)
        {
            return new BoundaryDrainState
            {
                SimulationEpoch = simulationEpoch,
                OwnerKind = ownerKind,
                OwnerStableId = ownerStableId,
                OwnerGeneration = ownerGeneration,
                NextOwnerSequence = firstOwnerSequence,
                Phase = GasBoundaryDrainPhase.Idle,
            };
        }
    }
}
