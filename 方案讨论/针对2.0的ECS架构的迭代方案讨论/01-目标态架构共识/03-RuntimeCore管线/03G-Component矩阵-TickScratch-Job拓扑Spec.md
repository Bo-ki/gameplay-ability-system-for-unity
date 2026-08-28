# 03G：Component 矩阵、Tick Scratch 与 Job 拓扑 Spec
> 状态：目标态规范

## 1. 结论

v1 使用固定步进组继承的 group allocator，并由 `GasTickKernelSystem` 通过 `SystemState.WorldUpdateAllocator` 创建当前 Tick scratch。所有 Core lane 组成一个连续 Job DAG；没有 phase 级 `Complete()`，也没有跨 System 传递临时 NativeContainer。整 Tick在任何 gameplay 权威写前完成一次 `WholeTickInfraAdmission`，之后 owner writer 执行 no-fail CommitPlan，target writer 执行 `TargetPrepare -> SessionFaultReduce -> TargetPublish`。Prepare 只写 shadow；Publish 只在全 Session 无 fatal 时无失败写 durable state。

性能扩展的基本分区是“不同 ASC/target 并行，同一 target 单写者稳定应用”，不是继续增加 SystemGroup。

## 2. Component/Buffer 访问矩阵

| 数据 | 存放 | 逻辑长度/稳定性 | Kernel 权限 |
|---|---|---|---|
| Session config/hash/tick/lifecycle | Session Component | 一 World 一个 active domain；Tick 单调递增 | Kernel 单写 Tick/Fault/终局，其他只读 |
| `SessionFaultLatch` | Session Component | 固定大小；Detected/IngressClosed、fault、sealed subset 与 accepted-outstanding first/last/count/hash | `FaultLatchJob` 写 Detected；outer Boundary control 只在同 gate 关闭后终结 IngressClosed |
| Definition/Catalog/Layout | Blob | 不可变 | 全部只读 |
| ASC identity/Owner/Avatar/RNG/lifecycle | ASC Component | 固定形态；`Pending/Ready/Alive/Terminal/DestroyPending` | OwnerWave、TargetPrepare/Publish 按字段唯一写并以 JobHandle 串联 |
| `AttributeValueSlot` | ASC Buffer | 等于 AttributeLayout count；Base/Current/Revision | OwnerWave 只写显式 Cost contract，TargetPrepare shadow/TargetPublish 写普通 Effect；JobHandle 串联 |
| `TagCountSlot` | ASC Buffer | 等于 TagCatalog count | OwnerWave 只写 Cooldown/activation-owned contribution，TargetPrepare shadow/TargetPublish 写普通 Effect；JobHandle 串联 |
| Tag presence/ancestor words | ASC Buffer | 由 Catalog word count 固定 | OwnerWave/target finalize 按阶段单写 |
| Granted Ability slab | ASC Buffer | non-compacting；generation 句柄 | direct command 由 OwnerWave 单写；GE-derived grant/revoke 由同 ASC TargetPrepare shadow、TargetPublish durable 单写，lane 严格串联 |
| Activation/Continuation slab | owner ASC Buffer | non-compacting；可跨 Tick | OwnerWave 单写；GE grant policy 触发的 child End/本地 cleanup 由同 ASC TargetPrepare/Publish 单写，lane 严格串联 |
| `CooldownGateSlot` | owner ASC Buffer | non-compacting；持续到 EndTick/显式 removal，不随 Activation End | owner lane 单写 |
| Subscription slab | observed ASC Buffer | non-compacting；generation 反向引用 | observed owner lane 单写；外部 target cleanup 只能发 bounded cancel intent，Ack 后释放 tombstone |
| OwnedContribution slab | activation owner ASC Buffer | non-compacting；精确 provenance | OwnerWave 单写；GE grant child cleanup 由同 ASC TargetPrepare/Publish 单写 |
| EmittedApplicationRef slab | activation owner ASC Buffer | non-compacting；`AuditOnly/CleanupRight` 分型、有 retention watermark/硬上界 | owner lane 单写；target 只回 T+1 terminal Ack，不直接回写 |
| Active Effect slab | target ASC Buffer | non-compacting；可跨 Tick | TargetPrepare shadow、TargetPublish durable 单写 |
| ActiveEffect payload/capture range | target ASC Buffer | variable range；non-compacting；版本化 variant | TargetPrepare shadow、TargetPublish durable 单写 |
| Aggregator/LiveDependency slab | target ASC + observed source route Buffer | non-compacting；Revision/generation 校验 | 各 ASC owner 单写；跨 ASC 只投递原子 `{Revision,FrozenProjection}` payload |
| Pending Command | ASC/Session Buffer | 跨 Tick 持久 | ingress 消费/append |
| Boundary Fact | ASC + Session scoped Cleanup Buffer | 到 managed drain 为止 | BoundaryProject 按唯一物理 owner range 单写；Battle/Session facts 走 Session |
| `BoundaryDrainState` | ASC + Session Cleanup Component | physical owner identity + 持久 `NextOwnerSequence` + `Idle/Pending/InFlight/Accepted` + BatchId/InFlightWatermark；无 Session-wide 伪 Battle identity | managed drain 接管；Kernel cleanup prepass 清理 |
| work buckets/sort keys/target shadow/FaultCandidate/fact merge scratch | NativeContainer | 仅当前 group update；shadow 与 durable reservation 独立记账 | Kernel Job DAG |

Attribute/Tag 权威 Buffer 在 ASC 出生时一次初始化；热路径不改变逻辑长度。slab 可复用 free slot、必要时扩充高水位，但绝不压缩 live slot。

## 3. Tick allocator 合约

```csharp
/// <summary>
/// 为当前 Gas Tick 创建临时工作集；所有返回容器只能被同一 Kernel 排出的 Job DAG 使用。
/// </summary>
private static NativeList<ResolvedCommand> CreateTickCommands(ref SystemState state, int initialCapacity)
{
    return new NativeList<ResolvedCommand>(initialCapacity, state.WorldUpdateAllocator);
}
```

约束：

1. 官方 backing allocator 是 double rewindable；EX-GAS 将容器的项目可用期进一步限制为当前 FixedStep Tick DAG。一渲染帧执行多个固定 Tick 时，禁止前一个 Tick 把引用交给后一个 Tick。
2. scratch 不写入 `IComponentData`/`IBufferElementData`、static 字段、managed field 或其他 System。
3. 禁止手工维护 `RewindableAllocator`/`FrameArenaSingleton`，也禁止显式 `Rewind`。
4. 容器初始容量来自版本化 ScaleProfile；它是内存布局/重分配优化，不是逻辑上限。
5. 需要跨 Tick 的 Continuation、Effect、Pending Command 必须进入 ASC 持久 Buffer，不能延长 allocator 生命周期。

`FixedRateCatchUpManager` 在同一 outer World update 的 catch-up 循环内不会为每个 `SimulationTick` rewind group allocator。项目可用期虽按 Tick 截断，物理内存高水位仍按批次累积；预算必须覆盖 `MaxFixedTicksPerBatch × (per-tick scratch + fact partitions)`、period/hot-target/mass-death/boundary burst，以及 double-rewind 同时保留前后 outer batch 的高水位。`ScaleProfile` 必须记录 `MaxFixedTicksPerBatch` 与 `MaximumDeltaTime`；headless 长局必须分批驱动完整 FixedStep 父链，不能一次 outer update 跑完整局。

## 4. 单 Job DAG

```text
Gather / TickStartSnapshot + PlanExpandScratchProvision
  -> OwnerPlanBuild (ASC-local shadow RYW; no authority write)
  -> TargetResolve / Expand
  -> WholeTickInfraAdmission
  -> AscOwnerCommandWave (no-fail CommitPlan)
  -> SourceSpecProjection
  -> GroupByTarget
  -> TargetPrepare (Apply / Stabilize / Death in shadow)
  -> SessionFaultReduce
  -> TargetPublish (no-fail durable publish)
  -> StableFactMerge / TerminalResolve
  -> GroupNextTickRouteByDestination
  -> BoundaryProject
  -> Record EndFixed
```

`GasTickKernelSystem.OnUpdate` 只组装上述依赖并把最终 `JobHandle` 交回 `state.Dependency`/ECB producer。每个节点应是具名 `IJobChunk`、`IJobParallelFor`、`IJob` 或可单测纯函数。

允许的同步边界：

- Unity 标准 EndFixed ECB playback；
- 固定步进批次后的 managed Boundary drain；
- 显式调试快照/测试断言。

禁止：

- 为了“进入下一 phase”调用 `Complete()`；
- 用多个 System 的 update 顺序代替 JobHandle 数据依赖；
- 在 Burst Job 中持有托管对象或通过 callback 返回主线程；
- 为读取刚记录的 ECB 结构而中断 DAG。

Gather 在 OwnerPlanBuild 前以 sealed/due count + tick-start Definition lookup + Catalog bake maxima 产生 `PlanExpandScratchEnvelopeToken`，用 checked arithmetic 验证 ScaleProfile 上限并在 Plan/Expand Job 写入前 provision 容量。逻辑超限时已预排 Plan/Expand no-op，后续唯一 `WholeTickInfraAdmission` 把 token fault candidate提升为失败；allocator/OOM 是 fatal environment failure。

`WholeTickInfraAdmission` 根据 TargetResolve/Expand 已给出的 [25](../25-配置语义编译契约与CapacityProof统一裁决Spec.md) 生成上界，验证 envelope token，并分别预留 target shadow overlay/payload/fact/cue/route/ECB intent credit 与 durable slab/non-compacting payload/capture/publish credit；两者必须同时计入峰值，不能共用同一 range。PendingCommand、Fact partition、Boundary outbox 也必须预留；structural intent 只冻结逻辑 count/token，因为标准 EndFixed ECB 没有公开 command reserve API，宿主 allocator/OOM 是 fatal environment failure 而非可恢复 admission outcome。

GroupByTarget 还必须以 checked arithmetic 汇总每个 bucket 的 `TargetWorkUnits`。它至少覆盖 application/evaluator node、stack/ledger op、最大 stabilization transition、Grant child cleanup、Live fanout、Fact/Cue/route/ECB intent；任一 bucket 超过 ScaleProfile `MaxTargetWorkUnitsPerTick` 或证明缺失时，唯一 AdmissionResult 在 OwnerWave 前失败。所有下游 Job 必须预排在同一 `JobHandle` DAG 并读取该结果；失败时 Owner/Target/Fact/Structural 分支统一 no-op、gameplay 权威零写，只有 `FaultLatchJob` 写 Session-fatal 控制证据。成功后 OwnerWave、TargetPrepare 和 TargetPublish 不允许再因基础设施容量不足部分提交。禁止为读取 admission 结果在中途 `Complete()`，IBC/初始容量也不等于该逻辑预算。

## 5. 并行写模型

### 5.1 Command canonicalization

外部到达顺序不能成为语义。Command 至少按 `SimulationEpoch、DeliverTick、OwnerAscHandle、SemanticPhaseOrdinal、WorkClassOrdinal、SourceSequence、Command kind` 建立稳定 key；排序算法与容器由 profile 选择。两个 ordinal 由版本化 schema/catalog 生成并进入 content hash，绝不是 Job lane。

OwnerPlanBuild 先 `GroupByOwner`，每个 ASC 在 shadow 中先折叠 tick-start due CooldownGate release，再按 canonical key read-your-writes；它不写权威 Buffer，也不读取本 Tick incoming target effect。admission 成功后，该 ASC 的单 writer 先提交 due maintenance，再依序提交已验证的 CommitPlan。普通 self GE 仍是 effect op，必须进入 target bucket；需要同 Tick 影响后续 CanActivate 的状态只能是显式 `CostMutationContract`/`CooldownGateContract` 或真正 activation-owned contribution，否则 Definition bake fail。两份 contract 的逐字段支持矩阵与固定诊断码只引用 [25](../25-配置语义编译契约与CapacityProof统一裁决Spec.md)，本 Runtime 拓扑不另写简化矩阵。

### 5.2 Target bucket

解析后按 target stable id 建 bucket：

- bucket 之间并行；
- 一个 bucket 由一个逻辑 Prepare writer 处理；
- bucket 内按 canonical key 在同一 overlay 串行应用，前序 prepared mutation 对后序可见；
- Attribute、Tag、Effect/Grant/Activation cleanup、stabilization 与该 target 的 Core Fact/Cue/route/ECB intent 都在同一 shadow 所有权内完成；
- `TargetWorkUnits` 由 bucket 内所有 definition proof 的 work、stabilization、grant cleanup 与 fanout 上界求和；N+1 在 admission 时失败，不进入 OwnerWave。

这消除了对同一 DynamicBuffer 的并行随机写，也不需要全局锁/原子浮点聚合。

多目标只逐 target application 业务原子；typed rejection 不做跨 target rollback。每条 application 在 overlay 线性化点检查 `TargetLifePolicy`，`AliveOnly` 在首次 death crossing 后对本 range 后续 work typed reject。source 在 OwnerWave 后死亡不撤回已 Commit work；target writer 不跨 ASC 回写 source。

调度器可以按 `TargetWorkUnits` 降序投放 bucket、使用 work stealing 或调整 batch size，但这些只是性能偏好，不能进入 semantic key。禁止把已 Accepted 的单 bucket 在内核中静默切到下一 tick；若产品要分批，必须在 ingress accept 前形成不同请求/DeliverTick，否则会改变 period、death、requirement 与 committed-work-wins 的可见时序。

### 5.3 TargetPrepare、SessionFaultReduce 与 TargetPublish

ongoing requirement、inhibition、tag/ability grant/remove、Grant child End/cleanup 形成 target-local 状态闭包。定义构建期输出依赖元数据与可证明的最大收敛/work界；TargetPrepare 超过界或遇到 identity/proof invariant 破坏时，不写 durable state，而是写固定大小：

```text
FaultCandidateKey =
  (SimulationEpoch, SimulationTick, TargetAscStableId,
   EffectApplicationIdOrZero, StabilizationRound,
   StateHash, FaultKindOrdinal)
```

每个 target 恰有一个 `Ready/Fatal` record。`SessionFaultReduce` 等待全部 Prepare 依赖后按 key 取字典序最小 fatal；同 key 非等价 payload 是 identity fault。任一 fatal 都不生成 publish token，丢弃本 Tick **所有 target** shadow 与 Fact/Cue/route/ECB intent，锁存 v1 Session-fatal。OwnerWave durable mutation 保留并进入 `CommittedPrefixHash`；不得用 battle-local fault 或“只丢该 target facts”掩盖其他 target 已写状态。

零 fatal 时，TargetPublish 只复制/应用已验证 `PreparedTargetDelta` 到 admission 预分配的 slot/range；不得在 Publish 中 grow、分配、重做 requirement/capture/stabilization 或产生新 child。全部 publish 完成后 Fact merge 才能看见 intents。

禁止固定次数 pass 后继续，也禁止把未稳定 shadow、未 reduce facts 或 fault Tick 的任何 target intent 提交给其他 target、Boundary 或 ECB。

## 6. Fact merge

worker 先写分区结果；最终 merge 使用稳定 key，不使用 Job 完成时序：

```text
(SimulationEpoch,
 DeliverTick,
 TargetAscStableId,
 SemanticPhaseOrdinal,
 WorkClassOrdinal,
 EmitTick,
 SourceAscStableId,
 SourceSequence,
 ProgramNodeOrdinal,
 TargetOrdinal,
 ApplicationOrEventId)
```

v1 semantic work precedence 至少冻结为 `DestinationMaintenance/LiveDependencyDirty -> PeriodDue -> Expiration -> SealedRemove/Inhibit -> CommittedApplication`。同一完整 key 出现非等价 work 是 validation fault；禁止追加 worker/chunk/stream/ECB sort key 作为隐藏 tie-breaker。

Core Fact merge 后由单一 `TerminalResolve` 按 `BattleInstanceId` 汇总 death candidate并冻结各战局终局；单个战局只关闭自身 ingress。仅当全部 BattleInstance 终局或显式 stop 时才推进 Session Terminalizing，target worker 不得抢先关闭任一层级。普通 Fact reaction 在 `GroupNextTickRouteByDestination` 中按 destination ASC 分组并形成下一 Tick Command；same-tick reaction 不从 merge 后任意回灌，只有 closed bounded pre-apply DAG 可以在 admission 前展开。

## 7. Enableable 与结构形态

只有高频切换且所有 ASC 预挂载的 marker 才可考虑 `IEnableableComponent`，例如 “有待 drain 事实”。Enable bit 是派生调度提示，不得成为 Attribute/Tag/Ability 状态的第二事实源。

查询 enableable 状态必须显式符合语义；禁止用 `IgnoreComponentEnabledState` 掩盖 Job 写冲突或混用启用/禁用语义。

## 8. Debug 与 Profile

单 Kernel 不等于不可观测。每个 lane 至少暴露：

- ProfilerMarker 与 scheduled/completed dependency 信息；
- 输入/输出数量、target bucket 分布、最大 bucket 长度、每 bucket `TargetWorkUnits` 分位与最大值；
- scratch 峰值与扩容次数，以及 target shadow/durable publish 两类 credit 的预算、实耗与峰值；
- admission 预算、预留量、失败资源类型与“准入后容量分支”为零；
- slab live/high-water/free 数量；
- DynamicBuffer chunk 内/外分布；
- stabilization 迭代/work 分布、FaultCandidate 数量、SessionFaultReduce winner key 与全 shadow discard 计数；
- Boundary live/shell 数量及 drain 延迟；
- catch-up outer batch Tick 数、N×scratch/fact 高水位、double-rewind 高水位与 managed staging backlog；
- 主线程同步次数和原因。

不在 Spec 写固定实体数、毫秒或容量阈值。ScaleProfile 必须记录目标硬件、Burst/Jobs 配置、场景生成器、采样区间与门槛；CI/性能验收只引用该 profile。

## 9. Runner 验收

标准 PlayerLoop 与独立/manual World 使用同一契约：

- TickBatch owner 更新完整 `FixedStepSimulationSystemGroup` 父链；
- TickBatch owner 尊重 group allocator 的 outer-batch 生命周期，不假设每个 SimulationTick rewind；
- Physics、`GasFixedTickSystemGroup` 与标准 EndFixed 顺序一致；
- 批次结束再执行一次 managed drain；
- managed staging receipt 成功后才清 outbox；下一 Kernel cleanup prepass 才把 shell remove 记录到同 Tick EndFixed；
- 禁止手工更新旧的多个 GAS phase group。
- `R3-STB` 在不同 worker/batch 切分下得到同一 FaultCandidate winner、CommittedPrefixHash，且 fatal Tick 所有 target durable delta/fact/cue/ECB intent 为零。
- `R3-HOT` 以相同总 work 分布到 1/8/64/1000 targets，验证合法输入 gameplay hash 不随调度改变；单 target work N 成功、N+1 在 OwnerWave 前 Session-fatal。

## 10. 禁止方向

- 自定义 FrameArena Singleton、手工 rewind、跨 Tick scratch。
- 一个 lane 一个 SystemGroup，或 phase 级强制完成依赖。
- 同一 target 多 Job 无所有权地写 Attribute/Tag/slab Buffer。
- 把 Ability/Effect slot 普遍提升为 Entity 来换取并行查询。
- Attribute/Tag 同时保留 Buffer 与 generated component 镜像。
- 以固定容量或固定硬件耗时作为所有项目通用红线。
- 把 IBC 当逻辑容量，或 admission 后才暴露可预检的容量不足。
- Post-Fixed drain 排跨 batch ECB，或用 physical Job lane 参与 gameplay 全序。
- 在 OwnerWave 后把 hot target、未完成 Prepare 或 publish delta 静默 time-slice 到下一 Tick。
