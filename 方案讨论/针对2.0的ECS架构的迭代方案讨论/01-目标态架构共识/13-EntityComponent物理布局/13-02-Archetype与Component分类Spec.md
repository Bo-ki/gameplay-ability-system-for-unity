# 13-02：Archetype 与 Component 分类 Spec

> 状态：v1 目标态
> 目标：内容变化不制造 archetype，业务热路径不依赖结构变化

## 1. 结论

同一 Session 的 live ASC 采用一个固定基础 archetype。Attribute、Tag、Ability 定义、Effect 状态与工作量差异全部表达在固定 Buffer/槽元素中，不通过增删 Component 表达。

允许的额外 archetype 只来自明确生命周期边界：Session、外部 Integration Request、独立 gameplay Entity，以及 ASC 销毁后的 cleanup shell。

## 2. 类型选择矩阵

| 类型 | v1 用途 | 示例 | 约束 |
|---|---|---|---|
| `IComponentData` | 单值、固定形态状态/句柄 | Session/ASC lifecycle、identity、`AscBattleMembership`、Actor refs、RNG、slab heads | unmanaged；不因内容增删 |
| `IBufferElementData` | Session registry或 ASC-local 索引数组、队列、slab/range store | `BattleInstanceSlot`、Attribute、Tag、Ability/Activation/Continuation/Subscription、Effect/Payload/Aggregator、Pending Command | 逻辑长度/句柄规则显式 |
| `ICleanupBufferElementData` | 销毁后仍需 drain 的事实 | Boundary Fact outbox | spawn 显式添加；drain 后在标准 EndFixed 移除 |
| `ICleanupComponentData` | 销毁后仍需保留的接管 receipt | BoundaryDrainState/frozen physical owner identity/NextOwnerSequence/BatchId/InFlightWatermark | staging receipt 后 Accepted；Kernel cleanup prepass 清理 |
| `IEnableableComponent` | 高频启停的派生 work marker | OutboxDirty、HasPendingWork（按 profile） | 预挂载；不是业务权威 |
| `BlobAssetReference<T>` | 静态定义/Catalog/Layout/程序 | GE/GA definition、tag parent、attribute index | 不可变；build 时验证 |
| NativeContainer | 当前 Tick scratch | sort keys、target buckets、fact partitions | Kernel-owned；不跨 Tick/System |

`ISharedComponentData` 与 chunk component 不是 v1 默认。前者可能造成 chunk 分片，后者会把本应 per-ASC 的状态提升为 chunk 共享；只有 profile 与独立 ADR 证明必要时才能引入。

## 3. Session 与 Live ASC archetype

Session archetype 固定包含：

```text
GasSessionIdentity
GasSessionConfig
GasDefinitionRegistry
GasCatalogRegistry
SimulationTickState
GasSessionLifecycle
SessionFaultLatch
BattleInstanceSlot[]
AscRegistrySlot[]
BoundaryCommandInbox[]
BoundaryDrainState             (cleanup component)
BoundaryFactBuffer[]           (cleanup buffer；Battle/Session scope)
```

`BattleInstanceSlot` 使用非压缩 slot/generation 与稳定 `BattleInstanceId`，保存 BattleLocalTick、ingress/lifecycle、member range/count 和 terminal/outcome state。单 slot Terminal 不改变其他 slot；全部终局或显式 stop才推进 Session Terminalizing。`BoundaryCommandInbox` 是唯一 ingress写入、Kernel Gather消费的跨 tick持久队列；不能与内部 `PendingCommand(T+1)` 合并为无来源队列。

### 3.1 Live ASC

概念形态：

```text
GasAscIdentity
AscBattleMembership
AscLifecycle
AscActorRefs
AscRandomState
AscSlabHeads
BoundaryDrainState             (cleanup component)
[pre-attached enableable work markers]
AttributeValueSlot[]
AttributeDirtyWord[]
TagCountSlot[]
TagPresenceWord[]
GrantedAbilitySlot[]
AbilityActivationSlot[]
ContinuationSlot[]
AbilitySubscriptionSlot[]
CooldownGateSlot[]
ActivationOwnedContributionSlot[]
EmittedApplicationRefSlot[]
ActiveEffectSlot[]
ActiveEffectPayload[]          (non-compacting variable ranges)
ActiveEffectCaptureSlot[]
AggregatorSlot[]
LiveDependencySlot[]
LiveDependencyRouteSlot[]
PendingCommand[]
BoundaryFactBuffer[]  (cleanup buffer)
```

是否“有激活 Ability”“有 Active Effect”“有 Boundary Fact”都不能触发 archetype 变化。空状态由零 live slots/零长度可变队列/disabled marker 表达。

`AscBattleMembership` 固定保存 BattleInstance handle/id、稳定 ScenarioUnitId、side/team 与 membership ordinal；ASC 发布 Ready 后不可变。Target grouping、Battle terminal 与 semantic hash 不得回退到 raw Entity 或 chunk 顺序。

Attribute 与 Tag 权威 Buffer 是固定逻辑长度，其中 Attribute 元素包含 `Base/Current/Revision`；slab/队列可以改变物理长度，但复用 free slot/free range 且不移动 live slot 或 live payload range。`CooldownGateSlot` 是 owner ASC 的长期 slab，不随 Activation End/Cancel 释放。Buffer 物理外溢到 heap 不是语义错误，是否可接受由 ScaleProfile 测量。

## 4. Enableable Component 规则

可使用 enableable marker 的前提：

1. 所有 live ASC 在 spawn 时已挂载该类型；
2. 它只表示“是否值得查询/调度”，真实状态可从权威 Buffer 重建；
3. 查询明确选择 enabled/disabled 语义；
4. 写 enable bit 的 Job 依赖被完整追踪。

禁止用 enable bit 表示 Tag presence、Ability 激活或 Effect inhibition 的唯一事实。禁止用 `IgnoreComponentEnabledState` 来掩盖并发写或错误查询。

## 5. Cleanup archetype

`DestroyEntity` 后，只要 ASC 或 Session 仍有 cleanup buffer/component，Entity 会保留为 cleanup shell。该 shell：

- 不再有 `GasAscIdentity`/`GasSessionIdentity` 或 owner 的普通 gameplay 数据；
- 不能被 live ASC/Session query 或 Registry 当作业务对象；
- 只由 `GasBoundaryDrainSystem`/cleanup path 读取；
- managed staging 以 `BatchId/physical owner/InFlightWatermark` 返回幂等 receipt 后只清 accepted prefix；late tail回 Pending，无 tail/NoFactReceipt才写 `BoundaryDrainState=Accepted`；`NextOwnerSequence` 不重置；
- 下一次 Kernel cleanup prepass 把移除 outbox 与 drain state 记录到该 Tick 标准 EndFixed，随后最终销毁。

事实元素本身必须保存 identity scope、FactPlane、Battle、Source/Target 的 stable id + generation；空 shell由 `BoundaryDrainState` 冻结的 physical owner identity/range产生 NoFactReceipt，不能 lookup 已移除的普通 Component，也不得为 Session shell伪造唯一 Battle。

## 6. Spawn 与 Destroy

### 6.1 Spawn

通过 ECB/初始化路径一次性：

1. 创建固定 ASC archetype；
2. 设置 identity、`AscBattleMembership`、Actor refs/RNG/slab heads，并令 `AscLifecycle=Pending`；同时在目标 `BattleInstanceSlot` 预留稳定 member ordinal；
3. 按 Session Layout/Catalog 初始化 Attribute/Tag 固定 Buffer；
4. 预热 slab/队列的物理容量（由 ScaleProfile 决定）；
5. 显式添加 cleanup outbox 与 drain state；
6. setup update 的 EndFixed只建立 Pending Entity/Registry entry；下一 FixedStep 的 Kernel `SpawnFinalize` maintenance lane在 scratch 完成 canonical `SpawnInitializationTransaction`，将 Attribute init、initial tags、default grants 和静态有界 self-initial effects 全部计算/准入成功后，才 no-fail统一写入权威状态、initial fact/Cue、`Ready` 并发布 Registry；该 update不计 gameplay Tick且不运行 gameplay DAG。

cleanup component 不依赖 prefab instantiate 复制；spawn 验收必须直接检查其存在。SpawnBatch 只保证 gameplay 可见性原子，不承诺 ECB 物理回滚；失败批次进入 Session `Faulted/Disposing`，全部 Pending Entity 都不得部分发布。

### 6.2 Destroy

1. 将 `AscLifecycle` 转为 `DestroyPending`，阻止新 Command 并使 identity/generation 失效；
2. 生成必要的 cancel/remove/death Boundary Fact；
3. 记录 EndFixed Destroy；
4. managed staging receipt 成功后只清 `<=InFlightWatermark`，无 tail才标记 `BoundaryDrainState=Accepted`；失败则保留 outbox/InFlight identity且阻止 FinalDrain 完成；
5. 下一 Kernel cleanup prepass 记录当前 Tick EndFixed 移除 cleanup buffer/state。若 shutdown 无下一 Tick，在完整 EndFixed/producer completion/FinalDrain receipt 后由停止世界 teardown 直接清 shell。

## 7. Archetype 风险与监控

ScaleProfile 至少采集：

- live ASC archetype 数与 chunk utilization；
- 各 Buffer chunk 内/外分布；
- enableable marker 筛选收益；
- structural change 与 ECB command 数；
- cleanup shell 数量与存活 Tick；
- staging backlog、Accepted 等待 prepass 数量与 BatchId/InFlightWatermark 重试；
- slab high-water/free ratio。

Spec 不规定“最多几个 archetype/多少 overflow”为通用常数。门槛由目标平台 profile 设定，但 live ASC 因内容类型分裂 archetype 本身属于设计失败。

## 8. 禁止方向

- 一属性一 Component 或一 Tag 一 Component。
- 给每种 Ability/Effect/状态添加 marker Component。
- Attribute/Tag Buffer 与 generated component 双写。
- 为了 Query 方便把每个 Ability/Effect slot 建成 Entity。
- 运行中反复增删 work marker，而不是预挂载 enableable。
- 把 cleanup shell 当 live ASC。
- 用 ECB 表达本可通过槽字段完成的 Activate/Commit/Cancel。
- 把 ASC SpawnBatch 的 gameplay 可见性原子误写成 ECB 物理回滚。
- Post-Fixed drain 排跨 batch ECB，或 staging receipt 前清 outbox。
