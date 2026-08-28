# 03I：System / Lane Catalog 与禁止方向 Spec

> 状态：v1 系统目录冻结
> 目的：固定“哪些是 System、哪些只是 Kernel 内 lane”，阻止旧多阶段组重新出现

## 1. v1 System Catalog

| 名称 | 类型/父组 | 必需 | 单一职责 |
|---|---|---:|---|
| `GasCommandIngressSystem` | `ISystem`；`GasFixedTickSystemGroup` | 是 | 在 Tick cutoff 前把 `SessionIngressGate` 已接收的 Boundary journal record 搬入 ECS inbox；不得执行 GAS 语义 |
| `GasFixedTickSystemGroup` | `ComponentSystemGroup`；FixedStep 直接子组、After Physics | 是 | 定义 GAS PostPhysics 固定步进域 |
| `GasTickKernelSystem` | `ISystem`；GasFixedTick | 是 | 拥有完整 Runtime Core Job DAG、Tick scratch 与 Core 写权限 |
| `EndFixedStepSimulationEntityCommandBufferSystem` | Unity 标准系统 | 是 | 播放当 Tick 真实结构变化 |
| `GasBoundaryDrainSystem` | managed；固定步进批次之后 | 是 | 唯一接管 live/cleanup-shell outbox，并分发到托管层 |

除 ingress 外，新增 Runtime Core System 必须满足：需要独立生命周期/更新频率/托管边界，或 Unity API 强制独立更新点。仅为“代码太长”“方便排序”不能新建 System。

## 2. 更新关系

```text
SimulationSystemGroup
├─ FixedStepSimulationSystemGroup        (0..N updates/render frame)
│  ├─ PhysicsSystemGroup
│  ├─ GasFixedTickSystemGroup
│  │  ├─ GasCommandIngressSystem
│  │  └─ GasTickKernelSystem
│  └─ EndFixedStepSimulationEntityCommandBufferSystem
└─ GasBoundaryDrainSystem                (once after the fixed-step batch)
```

若实际 PlayerLoop 组装使 drain 不适合放在 `SimulationSystemGroup`，可由明确的 TickBatch owner 在完整 FixedStep 批次后调用同一 drain service；不能因此把 drain 塞进 Kernel 或让多个消费者直接扫 ECS。cleanup shell 成功接管后只清 accepted prefix，无 tail才标记 Accepted；下一次 Kernel cleanup prepass 将 remove 记录到该 Tick 标准 EndFixed，drain 不创建跨 batch ECB。shutdown 无后续 Tick 时走显式 teardown。

`GasCommandIngressSystem` 与公开 CommandPort capability 成对必装，必须同时声明 `[UpdateInGroup(typeof(GasFixedTickSystemGroup), OrderFirst = true)]` 与 `[UpdateBefore(typeof(GasTickKernelSystem))]`。它只从 `BoundaryIngressJournal` 搬运完整 record，完成 durable ECS inbox append 后再让 Kernel seal cutoff；外部 ECS Request Entity 若存在也只能先规范化进同一 journal，不能绕过 gate 或新增第二 inbox writer。

## 3. Kernel Lane Catalog

| Lane | 输入 | 输出 | 并行/所有权 |
|---|---|---|---|
| CleanupAcceptedPrepass（所有未Disposed lifecycle） | Accepted live owner/shell | live owner→Idle；shell cleanup remove intent→本 update标准EndFixed | 不依赖 gameplay admission 的 lifecycle maintenance；ECB allocator/OOM 仍属 fatal environment failure；不递增 gameplay Tick |
| SpawnFinalize（仅 SpawnPending maintenance update） | EndFixed 已创建的完整 Pending batch、Session registry/layout/hash、generated SpawnInitializationProgram | shadow Attribute/Tag/Grant/self-initial-effect transaction 全量成功后的整批权威/initial fact/Cue/Ready publish，或 Faulted + destroy intent | Kernel 唯一 writer；ReadyTick 为下一 gameplay Tick；不递增 gameplay Tick、不运行其余 gameplay lanes |
| Gather/TickStartSnapshot + PlanExpandScratchProvision | persistent inbox、due work、ASC state、Catalog per-definition maxima、ScaleProfile | immutable Tick input/snapshot；checked envelope 或 fault candidate 的 `PlanExpandScratchEnvelopeToken`，并在 Plan/Expand 写前 provision 定长 scratch | 只读 durable state；逻辑超限使预排 Plan/Expand no-op |
| OwnerPlanBuild | Tick-start snapshot、有效 envelope token、Blob、同 ASC shadow plans | CommitPlan、post-commit capture candidate、生成上界 | owner ASC shadow RYW；零权威写 |
| TargetResolve/Expand | CommitPlan、有效 envelope token、target rules、verified program | bounded target/effect ops 与完整上界 | 只写已 provision scratch |
| WholeTickInfraAdmission | 全 Tick 上界、PlanExpandScratchEnvelopeToken、ScaleProfile、slab/queue 元数据 | downstream/durable reservation token/ranges 或 InfraAdmissionFault | 验证 envelope；任何 gameplay 权威写之前 |
| AscOwnerCommandWave | admitted CommitPlan | Activation/Continuation/Subscription/owned contribution 与 source work | owner ASC 单写；no-fail mutation |
| SourceSpecProjection | committed plan、post-commit candidate | immutable source-bound specs | 只密封成功 Commit |
| GroupByTarget | admitted effect ops | per-target canonical ranges | stable target key |
| TargetPrepare/Stabilize/Death | target ranges + durable snapshot + reservations | prepared application outcome、shadow Effect/Attribute/Tag/Grant/Death delta、intent partitions 或固定大小 fatal candidate | target 间并行、target 内单写；零 durable target mutation |
| SessionFaultReduce | 每个 target 的 Ready/Fatal record | 唯一 publish token 或确定性 Session fault | 等待全部 prepare；任一 fatal 丢弃本 Tick 全 target shadow |
| TargetPublish | publish token + prepared deltas + 预分配 durable ranges | no-fail durable target state、published fact partitions | target 间并行、target 内单写；不得再验证或分配 |
| StableFactMerge/TerminalResolve | partition facts/death candidates | canonical facts、per-BattleInstance terminal decision | 全 target 完成后唯一 resolver |
| GroupNextTickRouteByDestination | public reaction/live dirty | destination-grouped PendingCommand(T+1) | 跨 ASC 按 destination 分组 |
| BoundaryProject | canonical facts | 已预留 scoped cleanup outbox ranges | ASC facts 按 ASC-local writer；Battle/Session facts 由 Session唯一 writer |
| Record EndFixed | admitted spawn/destroy intent | parallel ECB commands | 仅记录，标准 EndFixed playback |

Lane 应用具名 Job/纯函数和 ProfilerMarker 暴露，不用 SystemGroup 暴露。

Kernel 外层 dispatch固定为：先对所有未Disposed状态运行`CleanupAcceptedPrepass`，再按`SpawnPending / Ready|Running / Terminalizing|FinalDrain|Faulted|Disposing / Disposed`分派 SpawnFinalize、完整gameplay DAG、teardown maintenance或no-op。这样最后gameplay Tick后的Accepted shell不依赖“再跑一个假 gameplay Tick”；shutdown确实无下一FixedStep时才使用03F定义的FinalDrain后direct cleanup。

## 4. 调试契约

每条拒绝、fault 与可选 trace 至少包含：

- `SimulationTick`；
- Lane Id；
- Source/Target ASC stable id；
- Command/Fact/Event stable id；
- Definition id 与 slot+generation（若适用）；
- 稳定枚举错误码，不依赖托管异常文本。

Trace 写入 Tick-local 分区 Buffer，稳定 merge 后才输出。不得在 worker Job 内直接写 Unity Console。

## 5. 独立 World / AutoChess Runner

runner 不拥有 GAS phase 清单。它只拥有 TickBatch：

1. 按固定 Tick accumulator 决定本渲染/驱动批次的 `0..N` Tick；
2. 更新完整 `FixedStepSimulationSystemGroup` 父链；
3. 让父链处理 group allocator、Physics、GAS 与 EndFixed；
4. 批次结束调用单 managed drain；
5. Session Tick Rate 与规则哈希和标准 World 一致。

任何“手工 Update 五个 GAS 组”的 runner 都必须迁移，避免遗漏 allocator reset、PhysicsWorld 或标准 EndFixed。

## 6. 明确禁止

### 6.1 执行域

- 恢复多个 GAS phase SystemGroup。
- 把全局 GAS 放到 `AfterPhysicsSystemGroup` 并假设它覆盖所有 PhysicsWorld。
- 以渲染帧 delta/time 驱动 Duration/Period。
- runner 直接 Update Kernel 或部分 GAS 子组。

### 6.2 数据模型

- Attribute/Tag Buffer 与 generated component 同时作为权威。
- 一属性一 Component。
- Definition-time slot/Entity 双模型；v1 的 Ability/Continuation/Effect 权威实例只能是 ASC slab。
- slab 压缩 live slot 或省略 generation。
- 用 presence bitset 反推 Tag exact count。

### 6.3 Job 与内存

- phase 级 `Complete()`。
- 自定义 FrameArena Singleton、手工 rewind 或跨 System scratch。
- 同 target 无分区并行写。
- 用 Job 完成顺序作为 Fact 顺序。
- 把长期 continuation/effect 放临时 NativeContainer。

### 6.4 语义与生命周期

- 仅凭无环就允许 same-tick 任意 reaction；必须同时 finite、closed、bounded。
- 固定 pass 静默截断 stabilization。
- 在未稳定状态上发布 Fact。
- 自定义 GAS ECB playback、phase 中途 playback。
- cleanup outbox 未 drain 就销毁，或让多个托管消费者直接清 Buffer。
- 以 `EntityManager.Exists` 判断 cleanup shell 仍是业务 ASC。

### 6.5 验收

- 在 Spec 硬编码通用实体数、容量、毫秒或百分比阈值。
- 缺少输入分布与环境版本的性能结论。
- 只测一渲染帧一个 Tick。
- 只测 live ASC，不测同 Tick destroy 与 shutdown drain。

## 7. 可变但必须有 ADR/Profile 的选择

以下不在 v1 写死：

- 具体 NativeContainer、排序算法、并行 batch size；
- DynamicBuffer InternalBufferCapacity 与 scratch 初始容量；
- trace 采样率与托管 retention；
- float/fixed-point 数值表示（但必须满足项目确定性目标）；
- 是否为特定独立 gameplay object 建 Entity；
- profiling 后是否整体调整 outbox owner 策略；同一事实始终只能有一个 owner，不能建立 ASC/Session 镜像。

任何变化不得破坏唯一事实源、Tick 时序、单 target writer、standard EndFixed 与单 drain 契约。
