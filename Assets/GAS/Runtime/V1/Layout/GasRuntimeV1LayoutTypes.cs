using Unity.Entities;

namespace GAS.Runtime
{
    /// <summary>
    /// 定义 Session 从安装到销毁的唯一生命周期状态。
    /// </summary>
    public enum GasSessionLifecycleState : byte
    {
        Install,
        SpawnPending,
        Ready,
        Running,
        Terminalizing,
        FinalDrain,
        Faulted,
        Disposing,
        Disposed,
    }

    /// <summary>
    /// 定义同一 Session 内单个 BattleInstance 的隔离生命周期状态。
    /// </summary>
    public enum GasBattleInstanceState : byte
    {
        Free,
        SpawnPending,
        Ready,
        Running,
        Terminal,
        OutcomeFrozen,
        Tombstone,
    }

    /// <summary>
    /// 定义 ASC registry 项对 gameplay lookup 的可见状态。
    /// </summary>
    public enum GasAscRegistryState : byte
    {
        Free,
        Pending,
        Ready,
        Tombstone,
    }

    /// <summary>
    /// 定义 ASC 业务生命状态，避免以 Entity 是否存在推导 gameplay liveness。
    /// </summary>
    public enum GasAscLifecycleState : byte
    {
        Pending,
        Ready,
        Alive,
        Terminal,
        DestroyPending,
        Dead,
    }

    /// <summary>
    /// 定义 non-compacting slab 槽的物理存储状态。
    /// </summary>
    public enum GasSlabSlotState : byte
    {
        Free,
        Live,
        Tombstone,
    }

    /// <summary>
    /// 定义 Boundary outbox 的物理 owner 类型。
    /// </summary>
    public enum GasBoundaryOwnerKind : byte
    {
        None,
        Asc,
        Session,
    }

    /// <summary>
    /// 定义 Boundary staging receipt 的持久接管状态。
    /// </summary>
    public enum GasBoundaryDrainPhase : byte
    {
        Idle,
        Pending,
        InFlight,
        Accepted,
    }

    /// <summary>
    /// 定义 Boundary fact 的逻辑路由范围。
    /// </summary>
    public enum GasBoundaryFactScope : byte
    {
        Asc,
        BattleInstance,
        Session,
    }

    /// <summary>
    /// 区分 gameplay 结果事实与 teardown 审计事实。
    /// </summary>
    public enum GasBoundaryFactPlane : byte
    {
        Gameplay,
        TeardownAudit,
    }

    /// <summary>
    /// 保存所有长期 slab 槽共有的 generation 与 free-list 元数据，业务状态由各槽独立字段承载。
    /// </summary>
    public struct GasSlabSlotHeader
    {
        public uint Generation;
        public int NextFreeIndex;
        public GasSlabSlotState StorageState;

        /// <summary>
        /// 创建一个尚未进入任何 free-list 的 live 槽头。
        /// </summary>
        public static GasSlabSlotHeader CreateLive(uint generation)
        {
            return new GasSlabSlotHeader
            {
                Generation = generation,
                NextFreeIndex = -1,
                StorageState = GasSlabSlotState.Live,
            };
        }

        /// <summary>
        /// 将槽标记为 tombstone，回收方完成交接后才能进入 free-list。
        /// </summary>
        public void MarkTombstone()
        {
            NextFreeIndex = -1;
            StorageState = GasSlabSlotState.Tombstone;
        }

        /// <summary>
        /// 在 generation 已完成溢出检查后把 tombstone 接入 free-list。
        /// </summary>
        public void LinkFree(uint nextGeneration, int nextFreeIndex)
        {
            Generation = nextGeneration;
            NextFreeIndex = nextFreeIndex;
            StorageState = GasSlabSlotState.Free;
        }
    }

    /// <summary>
    /// 保存版本化 ScaleProfile 的全部逻辑容量，运行时不得以 IBC 或隐藏常量替代这些上限。
    /// </summary>
    public struct GasScaleProfile : IComponentData
    {
        public int ProfileId;
        public int ProfileVersion;
        public ulong ProfileHash;
        public int MaxFixedTicksPerBatch;
        public int MaximumDeltaTimeTicks;
        public int MaxSpawnBatchSize;
        public int MaxBattleInstanceCount;
        public int MaxAscRegistryCount;
        public int MaxBoundaryCommandCount;
        public int MaxBoundaryCommandPayloadCount;
        public int MaxOwnerPlanCount;
        public int MaxResolvedTargetCount;
        public int MaxEffectOperationCount;
        public int MaxOwnerReservationCount;
        public int MaxTargetReservationCount;
        public int MaxCoreFactCount;
        public int MaxNextTickRouteCount;
        public int MaxStructuralIntentCount;
        public int MaxSessionBoundaryFactCount;
        public int MaxAscBoundaryFactCount;
        public int MaxPendingAttributeInitializationCount;
        public int MaxPendingTagInitializationCount;
        public int MaxPendingGrantedAbilityInitializationCount;
        public int MaxGrantedAbilityCount;
        public int MaxAbilityActivationCount;
        public int MaxAbilityContinuationCount;
        public int MaxAbilitySubscriptionCount;
        public int MaxCooldownGateCount;
        public int MaxActivationOwnedContributionCount;
        public int MaxEmittedApplicationRefCount;
        public int MaxActiveEffectCount;
        public int MaxPayloadRangeRecordCount;
        public int MaxPayloadValueCount;
        public int MaxAttributeAggregatorCount;
        public int MaxLiveDependencyCount;
        public int MaxLiveDependencyRouteCount;
        public int MaxPendingCommandCount;
    }

    /// <summary>
    /// 保存最近一次 gameplay Tick DAG 的准入与 lane 诊断，不参与任何 gameplay 判定。
    /// </summary>
    public struct GasTickDiagnostics : IComponentData
    {
        public ulong CandidateTick;
        public ulong ExecutedLaneMask;
        public int AdmissionReasonCode;
        public int SealedCommandCount;
        public int SourceSpecCount;
        public int ApplicationOutcomeCount;
        public int AttributeMutationCount;
        public int DeathFactCount;
        public int CoreFactCount;
        public int BoundaryFactCount;
        public byte AdmissionSucceeded;
    }
}
