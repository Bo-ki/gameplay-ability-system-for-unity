# 03C：SystemGroup 合约与核心数据形态 Spec

> 状态：v1 目标态裁决
> 适用基线：Unity 6000.3 / Entities 1.4.6
> 性质：项目架构决策；其中 DOTS API 行为以对应官方精确快照为准

## 1. 结论

EX-GAS v1 只建立一个固定步进执行域：`GasFixedTickSystemGroup`。该组是 `FixedStepSimulationSystemGroup` 的直接子组，并在 `PhysicsSystemGroup` 之后更新；组内默认只有一个拥有完整 Tick Job DAG 的 `GasTickKernelSystem`，最多允许再放一个纯 ingress 系统。

Runtime Core 的权威状态全部归属 ASC Entity：

- Attribute 与 Tag 是按 Session Catalog/Layout 建立的固定逻辑长度 DynamicBuffer。
- Granted Ability、Activation、Continuation、Subscription、Cooldown Gate、Active Effect、payload/capture 与 Aggregator contribution 是 ASC-local、带 generation 的非压缩 slab/range。
- Command 是意图；Core Fact 是已发生的事实；Boundary Fact 是对托管边界的持久投影。
- Runtime Entity 只承载真正需要独立查询、独立生命周期或独立 Transform/Physics 身份的对象，不能把 slot 普遍提升为 Entity。

## 2. SystemGroup 合约

```csharp
/// <summary>
/// EX-GAS 的唯一固定步进执行域；直接跟随当前世界的 PhysicsSystemGroup，避免绑定某个自定义 PhysicsWorld 的复制组。
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateAfter(typeof(PhysicsSystemGroup))]
public partial class GasFixedTickSystemGroup : ComponentSystemGroup
{
}

/// <summary>
/// 拥有单个 SimulationTick 内全部 Runtime Core Job 与临时内存，负责建立依赖而不是在阶段之间 Complete。
/// </summary>
[UpdateInGroup(typeof(GasFixedTickSystemGroup))]
public partial struct GasTickKernelSystem : ISystem
{
}
```

必须满足：

1. 固定步进组一渲染帧可更新 `0..N` 次；任何逻辑不得假设“每帧恰好一次 Tick”。
2. 所有持续时间、周期、冷却与事实顺序使用整数 `SimulationTick`；浮点秒只存在于配置烘焙或展示换算。
3. Session 创建后 Tick Rate 不可变，并进入 Session/规则版本哈希。联机、回放或存档双方哈希不一致时必须拒绝继续。
4. GAS 默认 PostPhysics，因此本 Tick 写入 Transform/Physics 的结果最早影响下一次 Physics Tick。
5. 不把 GAS 放进 `AfterPhysicsSystemGroup`。该组会随 custom PhysicsWorld group 复制运行；全局 authority GAS 放入其中会在主/自定义 PhysicsWorld 路径重复 Tick。

## 3. Session 与 ASC 权威数据

### 3.1 Session Entity

一个 GAS World 同时恰有一个 active Session tick domain；可以保留已 Disposed/Faulted 的诊断记录，但不得有第二个 Runtime authority。active Session Entity 持有：

| 数据 | 形态 | 合约 |
|---|---|---|
| `GasSessionConfig` | `IComponentData` | Tick Rate、规则版本与 Catalog 哈希；创建后不可变 |
| Definition/Catalog/Layout | `BlobAssetReference<T>` | 静态只读、可共享、Bake/Build 期验证 |
| `SimulationTickState` | `IComponentData` | 当前整数 Tick 与确定性序号根 |
| `GasSessionLifecycle` | `IComponentData` | Install/SpawnPending/Ready/Running/Terminalizing/FinalDrain/Faulted/Disposing/Disposed |
| `SessionFaultLatch` | `IComponentData` | 固定大小 FaultId/Epoch/Tick/Reason、Detected/IngressClosed、sealed subset 证据与 fault-close 时 accepted-outstanding first/last/count/hash；不写逐 request fact |
| BattleInstance registry | Buffer | 一个 Session 内 0..N 个独立终局域；单实例终局不停止其他实例 |
| ASC registry | non-compacting Buffer | Pending entry不对 gameplay可见；SpawnFinalize整批置 Ready，Gather据此建 tick-local handle→Entity lookup |
| BoundaryCommandInbox | persistent Buffer | 唯一 ingress，0 FixedStep不丢；按 Battle/source identity seal |
| Session Boundary outbox/state | Cleanup Buffer + Cleanup Component | Battle/Session-scope facts 与两阶段 drain receipt；零 ASC仍可交付终局 |
| ScaleProfile 标识 | `IComponentData` 或 Blob 字段 | 选择经测量的容量与验收档位，不在 Spec 写机器常数 |

Blob 适合定义、字节码、Tag 父链、Attribute/Tag 索引表与依赖图。Blob 不能承载运行时计数、句柄 generation、冷却或实例状态。

### 3.2 ASC Entity

ASC 是稳定 authority owner。`OwnerActor` 与 `AvatarActor` 可以不同，但二者都只是稳定引用/句柄；Avatar 更换不得迁移 ASC 权威状态。

| 类别 | 推荐形态 | 权威性 |
|---|---|---|
| ASC 身份、Owner/Avatar 句柄、随机数状态 | `IComponentData` | 权威 |
| ASC lifecycle / BattleInstance / Ready visibility | `IComponentData` | 权威 |
| Attribute 值 | 固定逻辑长度 `DynamicBuffer<AttributeValueSlot>` | 权威 |
| Tag 计数 | 固定逻辑长度 `DynamicBuffer<TagCountSlot>` | 权威 |
| Tag presence/ancestor bitset | 固定逻辑长度 Buffer | 派生缓存，可重建 |
| Granted/Activation/Continuation/Subscription/Effect | ASC-local non-compacting slab Buffer | 权威 |
| Cooldown Gate | owner ASC-local non-compacting slab Buffer | 权威；不随 Activation End/Cancel 释放 |
| OwnedContribution / EmittedApplicationRef | non-compacting ledger/range | 前者权威所有权；后者只作审计 |
| ActiveEffect payload/capture / Aggregator contribution / Live dependency | non-compacting slab/range | 权威 |
| 待处理跨 Tick Command | Buffer | 权威队列 |
| Boundary Fact outbox | `ICleanupBufferElementData` Buffer | 持久边界事实 |
| Cleanup handoff state | Cleanup Component/Buffer | staging Accepted 前不得移除 shell |
| Dirty mask / work marker | Buffer 或预挂载 Enableable Component | 派生调度状态 |

Attribute 唯一事实源是 `AttributeValueSlot { Base, Current, Revision }`。Base/Current 使用同一种、编译期确定的 unmanaged scalar，Revision 单调驱动 dirty/Live dependency；具体 float/定点表示由数值与确定性 ADR 决定，但一个构建中不能并存两套权威表示，也不存在 generated component 镜像。

Cost 不建立独立 store；生成的 `CostMutationContract` 在 OwnerPlanBuild shadow 中验证，再由 OwnerWave 直接写本 ASC 的同一 `AttributeValueSlot`。Cooldown 的唯一 gate 权威是 `CooldownGateSlot { GateKey, AbilityDefinitionId, SourceCommitIdentity, StartTick, EndTick, Generation, State, OwnedTagContributionRange }`。它由 Commit 创建，tick-start due maintenance 释放，不属于 Activation 或 ActiveEffect。

Tag 唯一事实源必须区分精确计数和包含父链后的计数：

```csharp
/// <summary>
/// Catalog 索引对应的 Tag 计数；ExactCount 表示直接授予，InclusiveCount 表示包含后代传播后的总计数。
/// </summary>
public struct TagCountSlot : IBufferElementData
{
    public int ExactCount;
    public int InclusiveCount;
}
```

两种 Buffer 的逻辑长度分别等于 Session AttributeLayout 与 TagCatalog 数量，出生时一次初始化，热路径禁止 `ResizeUninitialized`。InternalBufferCapacity 只是 profile 测量后的布局选择，不是逻辑容量上限。

## 4. 非压缩 slab 合约

v1 的 Granted Ability、Activation、Continuation、Subscription、Active Effect 及其可变 payload/capture/contribution 都使用 ASC-local slab/range：

- `SimulationEpoch + OwnerAscHandle + SlotIndex + Generation + HandleKind` 构成稳定强类型句柄；任何外部引用必须校验完整身份。
- 释放只递增 generation 并链接 free list，不移动其他活跃槽。
- 允许复用空槽，禁止为了压缩而重排 live slot。
- variable payload 使用 non-compacting range allocator；live range 不因相邻记录回收而搬迁。
- 逻辑 hard capacity、增长与 overflow由版本化 ScaleProfile定义；IBC不是逻辑上限，运行到 OOM不是有效失败模型。
- 同一个 ASC 在每个 mutation wave 内只有一个写者；不同 ASC 可并行。

Granted Ability 默认是 owner-local definition slot。Activation 与 Commit 是两个状态跃迁：Activate 成功不等于资源、冷却或 cost 已提交；Commit 必须显式、幂等并可返回确定性拒绝原因。

Continuation 用于跨 Tick AbilityTask/等待状态，不能依赖 Tick 临时容器。Active Effect 由 target ASC 的 slab 持有，保留 Source/Target capture、snapshot/live、stack、period、inhibition 与 aggregator channel 所需实例字段。

## 5. Command、Core Fact 与 Boundary Fact

| 流 | 含义 | 生命周期 | 默认可见性 |
|---|---|---|---|
| Command | 请求系统尝试做某事 | 消费后结束，必要时跨 Tick 排队 | ingress 截止前可进入本 Tick |
| Core Fact | Runtime Core 已确认发生的事实 | 当前 Tick 内部 | reaction 默认下一 Tick |
| Boundary Fact | 面向 Cue/Presentation/日志的稳定投影 | 持久到单一 drain 消费 | 固定步进批次后 |

事实必须带 `SimulationEpoch + SimulationTick` 与该 Tick 内稳定序号。Cue lifecycle 还必须带 `ActiveEffectHandle + ActiveCycleOrdinal + CueDefinitionOrdinal`；Boundary Event Id 与 Cue Lifecycle Key 不得混为一个字段。

same-tick 只允许两类闭包内路径：

1. Ability 直接输出已声明的操作；
2. Definition 内经生成期验证、有限且有静态最大深度的 pre-apply effect program。

“图无环”不等于“有界”。若无法证明静态上界，或输入在 apply 后才产生，则必须写入下一 Tick Command。Gameplay Fact reaction 默认下一 Tick。

## 6. Kernel 内部 lane，而非多个 System

`GasTickKernelSystem.OnUpdate` 只负责获取句柄、创建 Tick 临时容器并排出依赖；业务由具名 Job/纯函数承载：

1. Gather / TickStart snapshot
2. OwnerPlanBuild（ASC-local shadow read-your-writes，零权威写）
3. TargetResolve / bounded expansion
4. WholeTickInfrastructureAdmission
5. AscOwnerCommandWave（no-fail CommitPlan）
6. SourceSpecProjection / target bucket build
7. TargetPrepare / stabilization / Death（tick-local shadow）
8. SessionFaultReduce / TargetPublish（no-fail durable publish）
9. Core Fact stable merge / BattleInstance TerminalResolve
10. destination-grouped T+1 route / Boundary projection
10. ECB record / diagnostics

这些是可采样、可测试的 lane，不是十个 SystemGroup。Debug Trace 至少记录 `Tick、Lane、ASC stable id、Command/Fact stable id、拒绝/故障码`，从而在单 Kernel 下仍可定位。

## 7. 写权限与稳定化

- OwnerPlan 与 OwnerWave 都先按 source ASC 分区，同 ASC canonical 请求 read-your-writes；OwnerPlan 先折叠 due CooldownGate maintenance，OwnerWave 可直接写声明过的 cost Attribute/cooldown Tag contribution。普通 self GE不在 OwnerWave回灌。
- target bucket 按稳定 target key排序/分区；每个 target只由一个 Job执行上下文写入。
- target 之间并行，target 内按稳定 key 串行应用。
- ongoing requirement、inhibition 与 tag grant/remove 在同一个 target-local stabilization 中求稳定态。
- Definition 构建期对有符号依赖边进行环与可达性检查；运行时若超过由定义证明的收敛界，产生确定性 fatal fault。
- 禁止固定 pass 后静默截断，也禁止发布半稳定 Attribute、Tag 或 Fact。

## 8. Tick 临时内存与依赖

所有 scratch 由 Kernel 在 `state.WorldUpdateAllocator` 上创建。即使 backing allocator 的物理内存可能跨两次组更新才 rewind，EX-GAS 的项目可用期仍只到当前 GAS Tick DAG 结束。Kernel 建立一个连续 `JobHandle` DAG：

- 禁止 lane 间调用 `Complete()`；
- 禁止把 Tick scratch 存入 Component、static、Singleton 或另一个 System；
- 禁止跨 System 持有 group allocator 分配的容器；
- 只有托管 drain、调试快照或标准 ECB playback 这类真实边界才允许主线程完成依赖。

FixedRateCatchUp 的 group allocator 在整段 catch-up 结束时才轮换，不保证每个 SimulationTick物理 rewind。ScaleProfile 必须限制 `MaxFixedTicksPerBatch/World.MaximumDeltaTime`，预算 `N × tick scratch + N × outbox facts + burst + double-rewind high-water`；Headless 长模拟必须分有界 TickBatch，禁止恢复 manual rewind/FrameArena。

## 9. 结构变化边界

热路径的 slot 状态、Attribute、Tag、dirty mask 与 outbox append 都是非结构写。真正的创建/销毁 Entity、增删 Component/Buffer 统一记录到标准 `EndFixedStepSimulationEntityCommandBufferSystem`。

ECB 在 EndFixed playback 后的结构结果只保证对后续系统/下一 Tick 可见；它不是让同一 Kernel 继续读取新结构的手段。Activate、Commit、Cancel 若需要 same-tick 后续语义，必须在既有 ASC slab 上完成状态变更，而不是等待 Entity/Component 结构变化。

SpawnBatch 只承诺 Ready 可见性原子：setup update 记录 Pending实体/Registry到 EndFixed；下一 FixedStep 的 Kernel `SpawnFinalize` maintenance lane全量校验后才 no-fail发布整批 Ready。该 update不递增 gameplay Tick、不运行 gameplay lanes；失败进入Faulted并teardown，不要求ECB物理回滚。Drain/runner禁止成为 post-EndFixed Ready writer。Accepted shell由下一Kernel prepass记录本tick标准EndFixed removal，无下一tick时在FinalDrain后teardown。

## 10. 验收

- 一渲染帧执行 `0、1、N` 个固定 Tick 时，结果只由 Command 与整数 Tick 决定。
- Session Tick Rate/Definition/Catalog/Layout 哈希不一致会显式拒绝。
- Attribute 与 Tag Buffer 长度在热路径不变化；不存在第二份 generated component 权威镜像。
- stale `Epoch + Owner + SlotIndex + Generation + Kind` 句柄必定拒绝，释放槽不会改变其他句柄。
- Kernel profiler 中没有 phase 级 `Complete`，scratch 没有跨 Tick 生存。
- stabilization 不收敛时显式 deterministic fault，且不发布半成品 Fact。
- 所有容量、chunk overflow、主线程同步与耗时门槛来自版本化 ScaleProfile；WholeTick admission失败时权威零写，Boundary staging失败则保留outbox并阻止FinalDrain。
