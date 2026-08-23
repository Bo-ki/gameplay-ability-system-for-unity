# 完整端到端消息流代码骨架 Spec

## 结论

骨架只冻结四个 owner：Session/CommandPort、单 Kernel、ASC-local authority、单 Drain。具体字段布局由 13 系列维护，Ability/Effect 语义由 01/03D/03E/04/05 维护。

## 1. 强类型身份

```csharp
/// <summary>
/// 标识一次 World/Session 生命周期，防止跨世界复用旧句柄。
/// </summary>
public struct SimulationEpoch
{
    public ulong Value;
}

/// <summary>
/// 标识一个 ASC 实例；StableId 与 Generation 共同定义其生命周期。
/// </summary>
public struct GasAscIdentity : IComponentData
{
    public ulong Epoch;
    public ulong StableId;
    public uint Generation;
}

/// <summary>
/// 标识一次 Ability 激活；强类型避免与其他 slot pool 产生数字别名。
/// </summary>
public struct AbilityActivationHandle
{
    public ulong Epoch;
    public ulong OwnerAscId;
    public uint OwnerAscGeneration;
    public int SlotIndex;
    public uint Generation;
}

/// <summary>
/// 标识一个存活 GameplayEffect；只能由所属 ASC 解析。
/// </summary>
public struct ActiveEffectHandle
{
    public ulong Epoch;
    public ulong OwnerAscId;
    public uint OwnerAscGeneration;
    public int SlotIndex;
    public uint Generation;
}
```

GrantedAbility、Continuation、Subscription 使用各自强类型 handle；若某个无类型调试/序列化 carrier 必须统一承载，再增加 `HandleKind`，不能只复制四元组。

## 2. ASC 聚合根

```csharp
/// <summary>
/// 绑定稳定逻辑 Owner 与可替换 Avatar；raw Entity 不越过 Core Boundary。
/// </summary>
public struct GasActorBinding : IComponentData
{
    public ulong OwnerStableId;
    public ulong AvatarStableId;
    public Entity AvatarEntity;
    public uint BindingGeneration;
}

/// <summary>
/// 保存一个授予 Ability 的稳定槽；空槽通过 free-list 回收而不压缩 buffer。
/// </summary>
public struct GrantedAbilitySlot : IBufferElementData
{
    public int DefinitionIndex;
    public uint Generation;
    public int NextFreeIndex;
    public byte State;
}

/// <summary>
/// 保存一次独立激活的权威状态及其子 Continuation 链。
/// </summary>
public struct AbilityActivationSlot : IBufferElementData
{
    public int GrantedSlotIndex;
    public uint Generation;
    public int ContinuationHead;
    public int ContinuationCount;
    public ulong CausalityId;
    public byte Phase;
    public byte WasCancelled;
}

/// <summary>
/// 保存一个可独立唤醒或取消的异步等待；一个 Activation 可以拥有多个槽。
/// </summary>
public struct AbilityContinuationSlot : IBufferElementData
{
    public int OwnerActivationIndex;
    public uint OwnerActivationGeneration;
    public uint Generation;
    public int NextOwnedIndex;
    public ulong WakeTick;
    public int ProgramCounter;
    public byte Kind;
    public byte State;
}

/// <summary>
/// 保存目标局部 ActiveEffect；状态转换为 tombstone 后才允许进入 free-list。
/// </summary>
public struct ActiveEffectSlot : IBufferElementData
{
    public int DefinitionIndex;
    public uint Generation;
    public int NextFreeIndex;
    public ulong ApplicationId;
    public ulong SourceAscId;
    public uint SourceAscGeneration;
    public ulong StartTick;
    public ulong EndTick;
    public ulong NextPeriodTick;
    public int StackCount;
    public byte State;
}

/// <summary>
/// 保存固定 AttributeLayout 中一个属性的 Base、Current 与精细变更版本。
/// </summary>
public struct AttributeValueSlot : IBufferElementData
{
    public float Base;
    public float Current;
    public uint Revision;
}

/// <summary>
/// 保存一个 GameplayTag 的精确计数与包含后代贡献的层级计数。
/// </summary>
public struct TagCountSlot : IBufferElementData
{
    public int ExactCount;
    public int InclusiveCount;
}
```

所有 buffer 在 ASC spawn 时按 Session layout 初始化。长期 slab 禁止 `RemoveAt`、SwapBack 与 compact；Job 内不得跨结构变化点保存 `DynamicBuffer` 引用。

## 3. Boundary 入站

```csharp
/// <summary>
/// 表达跨 Tick 的显式目标引用；完整 ASC incarnation 与 Battle-scoped selector 不共享隐式含义。
/// </summary>
public struct BoundaryTargetRef
{
    public int Kind;
    public ulong Epoch;
    public ulong TargetAscId;
    public uint TargetAscGeneration;
    public ulong BattleSelectorId;
    public int DefinitionRuleIndex;
    public int ResolutionPolicy;
}

/// <summary>
/// 冻结 producer incarnation 内的幂等请求身份；同 key 只能对应一个 canonical payload。
/// </summary>
public struct BoundaryRequestKey
{
    public ulong SimulationEpoch;
    public ulong ProducerId;
    public uint ProducerGeneration;
    public ulong ProducerLocalRequestSequence;
}

/// <summary>
/// 冻结 producer-scoped 业务源顺序；裸序号禁止跨 producer 比较。
/// </summary>
public struct BoundaryProducerSequence
{
    public ulong ProducerId;
    public uint ProducerGeneration;
    public ulong ProducerLocalSourceSequence;
}

/// <summary>
/// 保存 canonical payload 的稳定摘要，用于幂等冲突检测而非 transport 排序。
/// </summary>
public struct BoundaryPayloadHash
{
    public ulong High;
    public ulong Low;
}

/// <summary>
/// 表达跨渲染帧持久的外部意图；payload 已冻结且不引用临时内存。
/// </summary>
public struct BoundaryCommandRecord
{
    public BoundaryRequestKey RequestKey;
    public ulong CorrelationRequestId;
    public ulong RequestSequence;
    public BoundaryProducerSequence SourceSequence;
    public ulong AssignedAvailableTick;
    public BoundaryPayloadHash PayloadHash;
    public ulong BattleInstanceId;
    public uint BattleInstanceGeneration;
    public ulong SourceAscId;
    public uint SourceAscGeneration;
    public BoundaryTargetRef Target;
    public int CommandKind;
    public BoundaryCommandPayload Payload;
}

/// <summary>
/// 区分首次接收、幂等重送与所有同步拒绝；拒绝项不创建 Accepted ledger record。
/// </summary>
public enum GasRequestStatus
{
    Accepted,
    DuplicateAccepted,
    IdempotencyConflict,
    IdempotencyHistoryExpired,
    InvalidProducerSequence,
    InboxFull,
    BattleClosed,
    SessionClosing,
    SessionFaulted,
    SessionMismatch
}

/// <summary>
/// 返回 CommandPort 接收线性化结果；Accepted/duplicate 都回显首次冻结的 ledger 分配。
/// </summary>
public struct GasRequestResult
{
    public GasRequestStatus Status;
    public BoundaryRequestKey RequestKey;
    public ulong RequestSequence;
    public ulong AssignedAvailableTick;
    public BoundaryPayloadHash PayloadHash;
}

/// <summary>
/// 提供应用壳层到 Runtime 的窄写入能力，只负责接收和复制意图。
/// </summary>
public interface IGasCommandPort
{
    /// <summary>
    /// 请求激活已授予 Ability；返回值只表示入站是否被接收。
    /// </summary>
    GasRequestResult RequestActivate(
        ulong producerId,
        uint producerGeneration,
        ulong producerLocalRequestSequence,
        ulong producerLocalSourceSequence,
        ulong battleInstanceId,
        uint battleInstanceGeneration,
        ulong ownerAscId,
        uint ownerAscGeneration,
        int grantedSlotIndex,
        uint grantedGeneration,
        ulong correlationRequestId);
}
```

`BoundaryCommandPayload` 是 schema generated、版本化、固定上界的 unmanaged tagged payload；超出上界在 CommandPort 拒绝，不使用跨 tick 裸 index。CommandPort 在 `SessionIngressGate` 上冻结 `RequestKey/PayloadHash/RequestSequence/AssignedAvailableTick` 并把完整 record append 到持久 `BoundaryIngressJournal` 后才返回 Accepted；同 key 同 hash 复用首次分配，同 key 异 hash typed reject。producer-scoped `SourceSequence` 才进入 gameplay canonical order。必装的 pre-Fixed `GasCommandIngressSystem` 先冻结 transfer membership，再通过显式 dependency/single-writer 路径原样搬入 `BoundaryCommandInbox`；CommandPort 不直写 DynamicBuffer，也不执行 `CanActivate`、target resolve 或 Effect。0 次 FixedStep 时 journal/inbox/assigned tick 都不清理；Kernel seal 后到达的命令最早下一 ingress window/合法 tick 消费。完整 ledger、retire 与 per-Battle gate 协议只见 [16-02](../16-02-BoundaryCommand与CoreCommandResolveSpec.md)。

## 4. 单固定步内核

```csharp
/// <summary>
/// 定义 GAS 在主 Physics 之后运行的唯一固定步物理域。
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial class GasFixedTickSystemGroup : ComponentSystemGroup
{
}

/// <summary>
/// 拥有一个 SimulationTick 的查询、临时容器、Job DAG 与最终依赖。
/// </summary>
[UpdateInGroup(typeof(GasFixedTickSystemGroup))]
public partial struct GasTickKernelSystem : ISystem
{
    /// <summary>
    /// 初始化长期 query/type handles；不创建跨 tick 的临时 NativeContainer。
    /// </summary>
    public void OnCreate(ref SystemState state)
    {
    }

    /// <summary>
    /// 所有未 Disposed 状态先运行 cleanup prepass，再按 lifecycle调度 Spawn、gameplay或teardown maintenance。
    /// </summary>
    public void OnUpdate(ref SystemState state)
    {
        var allocator = state.WorldUpdateAllocator;

        // 0. Every non-Disposed lifecycle runs CleanupAcceptedPrepass first:
        //    live Accepted owner -> Idle; Accepted shell -> record cleanup removal to this update's standard EndFixed.
        // SpawnPending: run SpawnFinalize only; validate the whole Pending batch, then no-fail publish Ready or Fault.
        // Terminalizing/FinalDrain/Faulted/Disposing: run teardown/cleanup maintenance only.
        // Maintenance updates never increment SimulationTick and schedule no gameplay lanes.
        // Ready/Running gameplay DAG:
        // 1. Gather/TickStartSnapshot + PlanExpandScratchProvision creates/validates the envelope token.
        // 2. OwnerPlanBuild -> TargetResolve/Expand writes only provisioned scratch.
        // 3. WholeTickInfraAdmission validates the envelope and reserves downstream/durable capacity.
        // 4. AscOwnerCommandWave -> SourceSpecProjection -> GroupByTarget -> TargetPrepare/Stabilize/Death.
        // 5. SessionFaultReduce -> TargetPublish -> StableFactMerge/TerminalResolve -> GroupNextTickRouteByDestination.
        // 6. BoundaryProject -> Record EndFixed; admission failure makes these branches no-op except FaultLatch.
        // state.Dependency = finalKernelHandle;
    }
}
```

逻辑 stage 可以拆为多个 Job/pure evaluator，但不能拆回多个物理 Group 或在 stage 间 `Complete()`。跨 ASC fan-in 先写 tick scratch，再形成 target-owned range；禁止随机写其他 ASC buffer。

## 5. Effect 应用与稳定化

```text
TargetPrepare(targetRange, targetShadow)
  validate application requirements / immunity
  resolve stack policy
  apply slot/contributor/tag mutations
  repeat in stable slot order
    reevaluate ongoing/removal requirements
    toggle Active/Inhibited/Removing
    update exact/inclusive tag counts and contributions
  until no transition
  recompute dirty aggregators and Attribute Current
  enforce current-tick invariants
  emit prepared facts/cues/routes/ECB intents into shadow only

SessionFaultReduce(allPreparedTargets)
  choose canonical first fatal, if any
  fatal -> discard every target shadow and latch Session fault
  ready -> issue publish token

TargetPublish(publishToken, allPreparedTargets)
  no-fail write pre-reserved durable state and final intents
```

重复 state hash 或 safety budget 命中时产生 fatal `StabilizationFault`；不能把中间 Cue/fact 输出，也不能携带半稳定状态继续。

## 6. Cleanup Outbox 与唯一 Drain

```csharp
/// <summary>
/// 保存 Drain 前不能丢失的自包含最终事实；其唯一物理 owner 是 ASC 或 Session cleanup outbox。
/// </summary>
public struct BoundaryFactBuffer : ICleanupBufferElementData
{
    public BoundaryEventId EventId;
    public BoundaryFactScopeKind ScopeKind;
    public BoundaryFactPlane FactPlane;
    public ulong ScopeStableId;
    public uint ScopeGeneration;
    public ulong BattleInstanceId;
    public uint BattleInstanceGeneration;
    public ulong OwnerScenarioUnitId;
    public ActiveEffectHandle CueActiveEffectHandle;
    public uint CueActiveCycleOrdinal;
    public ushort CueDefinitionOrdinal;
    public ulong SimulationTick;
    public ushort SemanticPhaseOrdinal;
    public ushort WorkClassOrdinal;
    public ulong SourceAscStableId;
    public uint SourceAscGeneration;
    public ulong TargetAscStableId;
    public uint TargetAscGeneration;
    public ulong ParentCausalityId;
    public ulong SemanticId;
    public BoundaryFactKind Kind;
    public BoundaryFactPayload Payload;
}

/// <summary>
/// 事实交付的全局去重键；不允许使用跨 outbox 会碰撞的裸数值序号。
/// </summary>
public struct BoundaryEventId
{
    public ulong SimulationEpoch;
    public BoundaryOutboxOwnerKind OwnerKind;
    public ulong OwnerStableId;
    public uint OwnerGeneration;
    public ulong OwnerSequence;
}

/// <summary>
/// 冻结 outbox owner 与两阶段接管身份；普通 identity被 Destroy移除后仍能生成 NoFactReceipt。
/// </summary>
public struct BoundaryDrainState : ICleanupComponentData
{
    public ulong SimulationEpoch;
    public BoundaryOutboxOwnerKind OwnerKind;
    public ulong OwnerStableId;
    public uint OwnerGeneration;
    public ulong NextOwnerSequence;
    public DrainStatus Status;
    public ulong BatchId;
    public ulong InFlightWatermark;
}

/// <summary>
/// 标识 managed ReadModel 已原子应用到的不可变观察切面；不提供 live ECS 访问。
/// </summary>
public struct SnapshotCut
{
    public ulong SimulationEpoch;
    public ulong SimulationTick;
    public ulong ProjectionVersion;
    public ulong ManagedBoundarySequence;
}

/// <summary>
/// 在固定步 catch-up 后唯一收集、排序，并在 managed staging接管后确认清理事实。
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(FixedStepSimulationSystemGroup))]
public partial class GasBoundaryDrainSystem : SystemBase
{
    /// <summary>
    /// 分别处理 live ASC、live Session 与 cleanup shell；接管失败时保持 ECS outbox 原样。
    /// </summary>
    protected override void OnUpdate()
    {
        // This system updates once per outer Simulation update even when FixedStep ran zero times.
        // Retry an existing InFlight range first with the same BatchId; retry never runs Kernel or increments SimulationTick.
        // Query three disjoint sets: live ASC, live Session, and cleanup shells with neither live identity.
        // Freeze BatchId/per-owner InFlightWatermark, then stable-sort by epoch/tick/fact plane/scope/semantic/work/event id.
        // On staging acceptance, atomically advance the immutable SnapshotCut cache before exposing the lossy ring cursor.
        // After accepted receipt, clear only records whose EventId.OwnerSequence <= InFlightWatermark; retain any later live tail.
        // An empty shell uses only BoundaryDrainState's physical owner key/range for NoFactReceipt; Session has no single Battle identity.
        // The next Kernel cleanup prepass records removal into that tick's standard EndFixed.
    }
}
```

`BoundaryFactPayload` 是 inline、自包含、版本化 unmanaged tagged payload；不引用随 owner销毁的外部 store。`BoundaryEventId=(Epoch, PhysicalOwnerKind/Id/Generation, OwnerSequence)` 是交付去重键，也是 fact 唯一 Epoch 存储；`NextOwnerSequence` 在 Accepted/Idle 后仍持久递增。Cue lifecycle key由 SimulationEpoch、ActiveEffectHandle、ActiveCycleOrdinal与 CueDefinitionOrdinal组成。`ScopeKind=Asc/BattleInstance/Session` 是 identity/outbox route，`FactPlane=Gameplay/TeardownAudit` 是结果分层，两者正交。scope/Battle/ASC id + generation、ScenarioUnitId与语义 ordinal在事实产生时冻结，cleanup shell不依赖已移除 membership。所有消费者只读同一个 batch；Core不维护 per-consumer cursor。Snapshot API 字段与 cut 语义只见 [16-05](../16-05-SnapshotIdentityApiHealthSpec.md)；per-Battle 双切面、Cue async stale 校验与 Result seals 只见 [06](../../06-Observation-Presentation-ReplaySpec.md)，本骨架不复制其状态机。

## 7. Session runner

```text
TickBatch(absoluteElapsedTime, maxGameplayTicks)
  -> update complete Simulation/FixedStep parent chain
  -> Physics
  -> GasFixedTick 0..N times
  -> standard EndFixed playback each fixed update
  -> BoundaryDrain exactly once after FixedStep (also when N == 0)
       -> retry same InFlight first; no Kernel/SimulationTick advance for retry
  -> return immutable batch metadata + diagnostics snapshot

BeginClose(reason)
  -> close ingress; do not start another gameplay Kernel
PumpShutdown()
  -> terminal/final-drain/cleanup state machine only
Dispose()
  -> only after final drain/cleanup are accepted; return DisposedReceipt + ValidationResultSeal
```

独立 World 必须显式安装标准 Begin/End FixedStep ECB。AutoChess、Scene 与 Headless 不得直接缓存或 `Update()` Kernel/Group。Session API 只见 [16-04](../16-04-ShellCapabilityContractSpec.md)，shutdown 物理顺序与 `ValidationResultSeal` 只见 [03F](../../03-RuntimeCore管线/03F-StructuralCommit与BoundaryProjectionSpec.md)。

## 8. 骨架验收

- authority 类型只存在于 ASC-local slots/buffers，无 Ability/ActiveEffect/Task Entity。
- Kernel 独占 tick scratch，public interface 无 ECS handle。
- 同目标单 writer、跨目标 canonical fan-in、无 phase sync fence。
- EndFixed Destroy 后 terminal facts 可 Drain，dead shell 最终释放。
- 0 FixedStep 仍执行一次 Drain；Boundary retry 复用 InFlight identity 且不推进 Kernel/tick。
- SnapshotCut cache 先于 lossy ring cursor 发布；Cue stale callback 与 per-Battle双 cut 由其 owner Spec验收。
- generated code 只能被 Kernel 作为 pure lookup/evaluator 调用。
- v1 类型中没有 PredictionKey、predictive flags、rollback/ack 字段。
