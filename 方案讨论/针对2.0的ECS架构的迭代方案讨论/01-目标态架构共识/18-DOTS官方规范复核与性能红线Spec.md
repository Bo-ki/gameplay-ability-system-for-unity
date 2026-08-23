# 18：DOTS 官方规范复核与性能红线 Spec

> 精确基线：Unity 6000.3 / Entities 1.4.6（以仓库官方快照为证据）
> 状态：v1 架构复核门
> 重要：本文件不修改、不替代官方快照；所有 EX-GAS 选择均标明为项目裁决

## 1. 结论

收敛方案符合 DOTS 的数据布局、依赖、allocator、ECB 与 cleanup 生命周期机制，但“FixedStep/PostPhysics、一个 Group/Kernel、整 Tick admission、ASC slab、same-tick 边界、语义全序、scoped ASC/Session outbox、ScaleProfile”都不是 Unity 官方自动给出的 GAS 答案，而是 EX-GAS v1 的项目决策。

审查时必须区分三种证据：

| 等级 | 含义 | 能否直接作为 EX-GAS 设计 |
|---|---|---|
| 官方事实 | 精确 Package 文档/源码说明的 API 行为与限制 | 只能证明机制 |
| 项目裁决 | 本仓库针对 GAS 语义与规模的唯一选择 | v1 必须遵守 |
| Profile 结论 | 目标硬件与代表场景的测量结果 | 决定容量、算法与数值门槛 |

禁止把项目裁决包装成“DOTS 官方要求”，也禁止用官方 API “允许”反推某方案适合本项目。

## 2. v1 最终裁决

| 主题 | v1 决策 | 性质 |
|---|---|---|
| 时间域 | FixedStep；一渲染帧 `0..N` Tick；整数 `SimulationTick` | 项目裁决 |
| 位置 | `GasFixedTickSystemGroup` 是 FixedStep 直接子组，`UpdateAfter(PhysicsSystemGroup)` | 项目裁决 |
| Physics | GAS 默认 PostPhysics；本 Tick物理结果供 GAS，本 Tick GAS 物理写影响下一 Physics Tick | 项目裁决 |
| 多 PhysicsWorld | 不把全局 GAS 放 `AfterPhysicsSystemGroup`，避免在 custom physics group 中复制 GAS | 项目裁决，依据 Physics 组机制 |
| 系统形态 | 一个 `GasFixedTickSystemGroup` + 必装纯 `GasCommandIngressSystem` + 一个 `GasTickKernelSystem` | 项目裁决 |
| Job | Kernel 内一个连续 Job DAG；lane 不等于 System；所有边为 JobHandle 依赖且无 phase Complete | 项目裁决 |
| Kernel DAG | `Gather/TickStartSnapshot + PlanExpandScratchProvision -> OwnerPlanBuild -> TargetResolve/Expand -> WholeTickInfraAdmission -> AscOwnerCommandWave -> SourceSpecProjection -> GroupByTarget -> AscTargetStateWave -> Stabilize/Death -> StableFactMerge/TerminalResolve -> GroupNextTickRouteByDestination -> BoundaryProject -> Record EndFixed` | 项目裁决 |
| scratch | `SystemState.WorldUpdateAllocator`；项目可用期仅当前 GAS Tick | API + 更严格项目约束 |
| 容量 | 生成上界驱动整 Tick admission；失败时 gameplay 权威零写并 Fault；IBC 不是逻辑 capacity | 项目裁决 |
| 结构变化 | 标准 `EndFixedStepSimulationEntityCommandBufferSystem` | 项目裁决 |
| Session | 一 World 一个 active Tick domain；Spawn Pending/Ready 只保证 gameplay 可见性原子，不承诺 ECB 回滚 | 项目裁决 |
| ASC 状态 | Attribute/Tag 固定逻辑长度 Buffer；Ability/Activation/Continuation/Subscription/Contribution/Effect/Payload/Aggregator/LiveDependency non-compacting slab/range | 项目裁决 |
| 稳定化 | target 间并行、target 内单写并求稳定态；不收敛 fatal | 项目裁决 |
| 顺序 | schema/catalog-hashed SemanticPhaseOrdinal/WorkClassOrdinal；不得使用 physical Job lane | 项目裁决 |
| source/target | committed-work-wins；`TargetLifePolicy` 在每条 application 线性化点检查 | 项目裁决 |
| reaction | same-tick 仅 closed/finite/bounded pre-apply DAG；普通 Fact reaction 下一 Tick | 项目裁决 |
| Boundary | scoped ASC/Session Cleanup Buffer + 每 fact唯一 owner + 两阶段 single managed drain；receipt 后只清 accepted prefix，shell 由下一 Kernel prepass清理 | 项目裁决 |
| 数值门 | 无通用硬编码容量/耗时；由版本化 ScaleProfile 决定 | 项目裁决 |

## 3. 官方机制对照

### 3.1 System / Group / FixedStep

官方机制允许用 `UpdateInGroup`、`UpdateBefore`、`UpdateAfter` 表达组内顺序，固定步进组可在一次 world/render 更新中追赶多次，也可能不更新。System 有固定调度成本，因此拆分需要权衡。

EX-GAS 推论：统一的整数 Tick 与一个 Kernel 可以消除渲染帧假设和过多 System 固定成本；但“一个 Kernel”仍是项目选择，不是官方上限。

### 3.2 Physics group

Physics 官方文档说明 custom physics group 会运行其 own PhysicsSystemGroup，并包含挂到 `BeforePhysicsSystemGroup`/`AfterPhysicsSystemGroup` 的用户系统。这意味着把全局 GAS 归入 AfterPhysics 会随 PhysicsWorld 复制。

EX-GAS 推论：GAS 应是 FixedStep 的直接子组，通过 `UpdateAfter(PhysicsSystemGroup)` 只跟随当前主世界 Physics；真正属于某个 custom PhysicsWorld 的局部系统另行设计。

### 3.3 Allocator

官方机制：

- World update allocator 是 double rewindable，官方物理生命周期跨两次 world 更新。
- 通过 `SetRateManagerCreateAllocator` 建立的 system group allocator 也是 double rewindable，分配物理生命周期跨两次该组更新；组更新期间它会成为当前 WorldUpdateAllocator。
- Fixed-rate catch-up 的 rate manager 在一个 outer World update 内执行 `0..N` 个固定 Tick 时，不会在每个 `SimulationTick` 之间 rewind 该 group allocator；批次结束才恢复/轮换 allocator。
- allocator 尚未 rewind 不代表数据仍有合法业务所有权。

EX-GAS 更严格规定：Kernel 从 `state.WorldUpdateAllocator` 分配，但所有 scratch 的项目可用期只到当前 GAS Tick Job DAG 结束；不跨 Tick/System 保存，也不手工 rewind。访问所有权按 Tick 失效并不会消除同一 catch-up outer batch 的物理累积，因此 ScaleProfile 必须限制 `MaxFixedTicksPerBatch/MaximumDeltaTime` 并预算 N×scratch/facts、burst 与 double-rewind 高水位。

### 3.4 Job 与依赖

官方安全系统通过读写声明和 JobHandle 建立依赖；提前 `Complete` 会产生主线程同步。Lookup/TypeHandle 需要按更新周期刷新，parallel random write 必须证明不重叠。

EX-GAS 推论：先按 owner 分组构建 shadow CommitPlan，admission 后 owner 单 writer no-fail 提交；再按 target 分 bucket，同 target 单 writer。整个 Tick 由一个 Kernel 以 JobHandle 串接依赖；Job 完成顺序不得成为 Command/Fact 顺序。

### 3.5 ECB

ECB 是延迟结构变化工具；ECB system 在自己的更新点完成 producer 并 playback。Playback 后直接数据引用可能失效，多个 playback 点会增加同步和生命周期复杂度。

Package 源码还表明 ECB system 销毁时会 dispose 尚未播放的 pending buffer，而不是替调用方补做 playback。因此 World teardown 不能把 post-Fixed pending ECB 留给 `OnDestroy`。

EX-GAS 推论：业务槽/Buffer 写不进 ECB，真实结构变化只用标准 EndFixed。Activate/Commit/Cancel 的 same-tick 语义不能依赖 ECB；post-Fixed Boundary drain 也不能排一个跨 batch ECB。

### 3.6 Cleanup

官方 cleanup 机制在 Destroy 时移除非 cleanup 组件，保留 Entity 直到所有 cleanup component 被移除；cleanup component 不会从 prefab instance 自动继承，也不会被跨 World copy。

EX-GAS 推论：ASC spawn 显式添加 cleanup outbox 与 drain state；Destroy 后事实必须自包含稳定身份。managed staging 先以 `BatchId/InFlightWatermark` 幂等接管，receipt 成功后 drain 才清 outbox并标 `Accepted`；下一 Kernel cleanup prepass 把 shell removal 记录到该 Tick 标准 EndFixed。shutdown 无下一 Tick时，必须在完整 EndFixed/producer completion/FinalDrain receipt 后直接清 shell。

## 4. 数据布局复核

### 4.1 Attribute

通过 Layout Blob 做 Id→index，ASC 上单一固定 Buffer 保存 `Base/Current/Revision`，符合 chunk 数据与 Buffer 索引访问模型。避免一属性一 Component 的 archetype 爆炸，也避免 generated component 镜像双写。Revision 为 Live capture 提供 dirty 证据，但 source 只能投递下一 Tick destination work，不能随机写 target。

风险：Attribute 数量大时 Buffer 可能 overflow；是否设置 IBC、设多少只能由 ScaleProfile 决定。

### 4.2 Tag

Catalog-indexed count Buffer 是唯一权威；exact/inclusive count 明确父链传播。presence/ancestor bitset 只作为派生查询缓存，避免 bit 与计数分叉。

风险：Catalog 很大时固定 Buffer/bitset 占用上升；不能用任意固定 Tag 上限回避，应以内容规模/目标平台建立 profile。

### 4.3 长期 slab

ASC-local slot+generation 支撑稳定引用、跨 Tick continuation 和 target-local effect owner；non-compacting 避免移动引发 stale 引用。统一 handle 必须包含 `Epoch + OwnerAscHandle + Slot + Generation + Kind`；variable payload/capture 使用同等受保护的 non-compacting range，live range 不移动。

物理布局必须覆盖 Granted、Activation、Continuation、Subscription、OwnedContribution、EmittedApplicationRef、ActiveEffect、Payload/Capture、Aggregator 与 LiveDependency。`OwnedContribution` 与 `EmittedApplicationRef` 不可混为一个“Activation 所有 Effect”列表；全部 emitted application 都保留受 retention/watermark 约束的审计 ref，但只有显式 `RemoveOnActivationEnd` ref 拥有 cleanup 权并在 End 时生成 remove work。普通审计 ref 从不拥有或移除 Effect，可在审计水位后回收。

风险：hot target 的 slab/bucket 会形成负载倾斜。v1 优先确定性与单 writer，不能为表面并行把同 target 拆成竞态写；必要优化需保留 canonical semantics。

## 5. 稳定化与 same-tick 复核

ongoing requirement、inhibition、tag grant/remove 会相互影响，必须在 target-local 闭包内稳定后再发布 Fact。

生成期验证至少需要：

- 带正负语义的依赖边，而非仅检查无向/无符号图；
- 环、可达性、程序长度与扩展深度；
- 每个 same-tick program 的 closed inputs 和静态上界。

运行时超过证明的上界必须产生稳定 fault，并停止发布该 target 的半成品事实。禁止“固定跑若干 pass 后当作成功”。

普通 Gameplay Fact reaction 默认下一 Tick；这样 Fact merge 是终点而非再入入口，单 Job DAG 有确定边界。

OwnerPlanBuild 只能读取 Tick-start snapshot 与同 ASC 前序 shadow CommitPlan，不读取本 Tick incoming target effect；普通 self GE 也进入 TargetWave。必须同 Tick影响后续 CanActivate 的字段只能是 CommitPlan 中声明的 activation-owned invariant，否则 Definition bake fail。

语义 work precedence 至少冻结为：

```text
DestinationMaintenance / LiveDependencyDirty
  -> PeriodDue
  -> Expiration
  -> SealedRemove / Inhibit
  -> CommittedApplication
```

`SemanticPhaseOrdinal/WorkClassOrdinal` 由版本化 schema/catalog 生成并进入 content hash，不得取 Job/worker/chunk/stream/ECB lane。完整 canonical key 冲突且语义不等价时必须 validation fault。

OwnerWave 已 Commit 的远端 work 采用 committed-work-wins；source 后续死亡不撤回。target 在每条 application 线性化点读取 `AscLifecycle` 并检查 `TargetLifePolicy`；`AliveOnly` 首次 death crossing 冻结后，后续 AliveOnly work typed reject。逻辑 ASC identity、Avatar binding、spatial sample 与 life policy 必须正交，禁止目标失效时 implicit fallback self；`FrozenSpatial` 是显式采样语义，不是 fallback。

## 6. 整 Tick admission 与事务边界

OwnerPlanBuild/TargetResolve 在准入前已需写 variable scratch。因此 Gather 先以 sealed/due count、tick-start Definition lookup 与 Catalog bake maxima 用 checked arithmetic 建立 `PlanExpandScratchEnvelopeToken`并在两个 Job 写入前 provision；超 ScaleProfile 逻辑上限时 Plan/Expand 预排 no-op，由后续 admission 提升 fault。这是 pre-admission memory envelope，不是第二 gameplay admission。

`WholeTickInfraAdmission` 位于 TargetResolve/Expand 后、任何 gameplay 权威写前。它先验证 envelope token，再按实际生成上界覆盖 downstream scratch、slab、payload/capture、PendingCommand、fact、outbox 与 structural intent；标准 EndFixed ECB没有公开 command reserve API，因此 structural项只做逻辑 count/token budget，allocator/OOM是宿主 fatal failure而非可恢复 gameplay outcome：

- 失败：预排的 owner/target/fact/structural jobs读取 `AdmissionResult` 后 no-op，不记录半 Tick gameplay/structural intent，只有 FaultLatch锁存确定性 Session Fault。
- 成功：OwnerWave CommitPlan 与 TargetWave 不再允许基础设施容量分支；可预检的不足不能退化为部分 mutation。
- 多目标：仍只逐 target application 业务原子，不提供跨 ASC rollback。
- Boundary：staging 发生在 gameplay 提交之后，失败不回滚 gameplay；保留 outbox并阻止 FinalDrain/Disposed。

## 7. 形态性能红线

下列任一项无需等待毫秒阈值即可判为架构失败：

- 热路径 phase 级 `Complete()` 或多次 ECB playback。
- Tick scratch 跨 System/Tick/managed/static 生存。
- Attribute/Tag 每 Tick resize 或按定义增删 Component。
- Attribute/Tag 双权威镜像。
- slab 压缩 live slot、句柄无 generation。
- 同一 target 多 writer 随机写同一 Buffer。
- Fact/ECB 顺序依赖 worker 完成先后。
- physical Job lane 进入 gameplay canonical key，或同 key 非等价 work 静默 tie-break。
- stabilization 静默截断或在未稳定状态发 Fact。
- 多个 Boundary 消费者直接清 ECS outbox。
- staging receipt 前清 outbox、post-Fixed drain 排跨 batch ECB，或 staging 失败仍完成 FinalDrain。
- manual runner 绕过完整 FixedStep 父链。
- 把 IBC 当逻辑 capacity，或 admission 后才发现可预检容量不足并留下部分权威写。
- headless 整局塞进一次 outer batch，或按“每 SimulationTick rewind”低估 allocator 高水位。

## 8. ScaleProfile 门

每个 profile 必须版本化：

- Unity/Entities/Burst/Jobs 与目标硬件；
- Tick Rate、ASC/Attribute/Tag/Ability/Effect/Command 分布；
- 典型与压力场景生成方法；
- chunk utilization、Buffer 内/外、高水位/扩容；
- target bucket 偏斜、scratch 峰值；
- `MaxFixedTicksPerBatch`、`MaximumDeltaTime`、outer batch Tick 分布、N×scratch/facts 与 double-rewind 高水位；
- whole-tick admission 上界/实际/失败资源及 admission 后容量分支；
- period burst、hot target、mass death/teardown、Boundary burst 与 managed staging backlog/GC；
- stabilization 分布与 fault；
- Job 调度/主线程 sync/EndFixed playback/drain；
- 明确门槛、采样区间、基线 commit 与回归规则。

IBC、初始容量、batch size、排序/merge 算法、trace 采样和具体耗时报警都属于 profile 决策，不应写死到通用 Spec。

## 9. Backbone 验收

1. 标准 PlayerLoop 与 manual World 在相同 Tick/Command/seed 下结果一致。
2. 每渲染帧 `0、1、N` Tick 都通过；无 `deltaTime` 业务漂移。
3. Tick Rate/规则/Catalog/Layout hash 不一致显式拒绝。
4. Kernel profiler 无 phase `Complete`；scratch 无越界。
5. target-local apply/stabilize 可重放，非收敛显式 fatal。
6. same-tick 仅来自 verified bounded program，Fact reaction 下一 Tick。
7. 同 Tick ASC destroy 后 cleanup outbox 不重不漏；receipt 后由下一 Kernel prepass 清 shell；shutdown 无下一 Tick时显式 direct cleanup。
8. 结构变化只有标准 EndFixed；runner 更新完整父链。
9. admission 失败 gameplay 权威零写；成功后无容量型部分 mutation，多目标仍逐 target 业务原子。
10. Session 一 World 一个 active domain，SpawnBatch 不部分发布 Ready；Faulted 仍完成 FinalDrain/Disposing。
11. source death 不撤回已 Commit work，AliveOnly 首死后拒绝顺序可重放；TargetData 无 implicit self fallback。
12. 性能结论引用含 catch-up batch 上限的 ScaleProfile，而非无环境固定数字。

## 10. 不能由官方文档直接推出的项目决定

以下必须保留 ADR/Spec 所有权，不能写成 Unity 建议：

- GAS 使用 FixedStep 和默认 PostPhysics；
- 采用一个 Group/Kernel 而非多个 System；
- ASC 是 authority owner，Owner/Avatar 分离；
- Attribute/Tag 的具体单一 Buffer 布局；
- Ability/Continuation/Effect 的 slab 模型与 generation；
- Subscription/Contribution/Payload/Aggregator/LiveDependency 的物理布局与完整 handle schema；
- Activate/Commit 分离；
- capture、requirements、inhibition、stack/period/channel 语义；
- target-local stabilization 的 fault 策略；
- same-tick/next-tick 分界；
- Owner shadow RYW、整 Tick admission、committed-work-wins 与 TargetLifePolicy；
- SemanticPhaseOrdinal/WorkClassOrdinal 的 schema 全序；
- 一 World 一个 active Session、Spawn Pending/Ready 可见性原子；
- scoped ASC/Session cleanup outbox、两阶段单 drain 与 shutdown direct cleanup；
- v1 不做 prediction/rollback；
- ScaleProfile 的场景与数值门槛。
