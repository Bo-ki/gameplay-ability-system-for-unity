# 四层架构 Spec

## 结论

四层是依赖与权限边界，不是四套 Runtime。Gameplay 权威只存在于 Layer 3；Layer 1/2 只写入意图、读取不可变结果，Layer 4 只提供不可变定义和纯计算。任何跨层接口一旦暴露 ECS 句柄、可写容器或调度 owner，就视为边界失败。

## 依赖方向

```text
Layer 4 Definition & Generation ──immutable catalog/pure glue──► Layer 3 Core
Layer 1 Application Shell ──intent──► Layer 2 Boundary ──persistent inbox──► Layer 3 Core
Layer 3 Core ──cleanup outbox──► Layer 2 Boundary ──immutable batch──► Layer 1 Consumers
```

禁止 Layer 3 依赖 Layer 1 的资源、UI、Demo 或 managed service；禁止 Layer 4 生成 Layer 3 lifecycle；禁止 Layer 1 绕过 Boundary 直接操作 Core。

## Layer 1：Application Shell

### 职责

- 收集玩家输入、AI、网络权威输入和场景事件。
- 以业务稳定 ID 表达目标，调用 `CommandPort.Request*`。
- 消费不可变 `BoundaryBatch`、ReadModel 与 Diagnostics snapshot。
- 驱动唯一 Session runner；无头模式只省略真实资源，不省略 Boundary 语义。

### 禁止

- 直接 `World.Update()` 某个 GAS 子 Group/System。
- 保存 ASC/Ability/Effect 的 raw `Entity`。
- 读取或清理 Core buffer，参与 cost/cooldown/target/effect 计算。
- 以 UI、Cue 或日志是否成功反向决定 gameplay。

## Layer 2：Runtime Boundary

### 入站 owner

`CommandPort` 在唯一 `SessionIngressGate` 上线性化 accept，把外部意图先复制到 Runtime Boundary 跨渲染帧持久 `BoundaryIngressJournal`；只有 pre-Fixed `GasCommandIngressSystem` 能在 Kernel 读 Job 之前搬入 ECS `BoundaryCommandInbox`。FixedStep 为 0 次时不得丢失；catch-up 为 N 次时每条命令只能被一个 `SimulationTick` 消费一次。命令携带 session/target identity、request id、payload 与明确的 stale/reject 规则，不保存 tick 临时内存引用。Kernel 读 inbox 期间禁止并发 append 同一 DynamicBuffer。

### 出站 owner

`GasBoundaryDrainSystem` 是唯一 ECS→managed 交接点：

1. Drain live ASC 的 dirty outbox。
2. Drain 分别查询 live ASC、live Session 与没有任一 live identity 的 cleanup shell outbox；禁止把 live Session 因 `WithNone<GasAscIdentity>` 误判为 dead shell。
3. 按正式全序复制到 managed staging；BatchId/InFlightWatermark 幂等接管成功后才清 ECS facts。
4. 把死 shell 标记 Accepted；下一 Kernel prepass记录本 tick标准 EndFixed removal，Session teardown 是无下一 tick且 FinalDrain完成后的显式例外。
5. 发布一个不可变 batch/ring或机器可读 drop/fatal receipt，所有 Cue/UI/Replay/Debugger/Headless consumer 只读同一批次。

Boundary 不维护 gameplay 权威或 per-consumer cursor，不允许多个 consumer 竞争清空 ECS 数据。

### 公开身份

公开身份使用 `SimulationEpoch`、稳定 ASC/Avatar ID、BindingGeneration、强类型 generational handle、Definition/Application/Causality ID。raw `Entity` 只能留在 Core 内的 ActorBinding；`EntityManager.Exists` 不能作为 ASC liveness 判据，因为 cleanup shell 仍存在。

## Layer 3：GAS Runtime Core

### 聚合根

ASC Entity 是唯一 gameplay 聚合根；其 owner-local 数据包括：

- `GasAscIdentity` 与 `GasActorBinding`；
- GrantedAbility、Activation、Continuation、Subscription、ActiveEffect 非压缩 slabs；
- 固定长度 Attribute/Tag authority buffers 与派生缓存；
- pending work、provenance、outbox 和 diagnostics counters。

Ability、ActiveEffect、Task、Spec 不创建权威 Entity。Projectile/Aura/Zone 可以是派生 Entity，但只能复制命中所需的 definition/context/capture/provenance，不能延长或镜像 Activation 权威。

### 调度 owner

`GasFixedTickSystemGroup` 位于主 Physics 后，内部由 `GasTickKernelSystem` 独占 tick scratch、Job DAG 与最终 dependency。Kernel 以 target ASC 为 mutation partition，完成 ability、effect、tag、attribute、stabilization 和最终 fact。结构变化交给标准 EndFixed ECB；managed drain 在 catch-up 后执行。

### 写入规则

- Core 外部只能写 Boundary inbox；Core 内不同目标可并行，同一目标只有一个逻辑写 lane。
- 长期 authority 只通过强类型 handle 与 owner-local slot 访问。
- 临时数据使用 `WorldUpdateAllocator`，不能跨 System/tick 保存。
- public reaction 默认下一 tick；同 tick 只允许 kernel invariant 与生成期闭合有界的 DirectEffectProgram。
- ActiveEffect stabilization 先达到最终稳定态再发 fact/Cue；不收敛为 fatal fault。

## Layer 4：Definition & Generation

### 唯一权威

Luban rows 是策划源，生成产物把它们规范化为 Session 内不可变 Blob catalog。生成期必须预解析 Attribute/Tag index、requirement 分区、capture contract、DirectEffectProgram、依赖图、容量报告、schema/content hash 和 Editor metadata。

### Pure Glue

Generated Runtime Glue 只能是 Burst-compatible 的静态 lookup/evaluator/record builder。它不得拥有：

- `ISystem`、`OnUpdate` 或 system registration；
- query、type handle refresh、ECB、`EntityManager` 写入；
- NativeContainer allocator/lifetime；
- gameplay lifecycle、fallback backend 或 managed runtime lookup。

无法由封闭生成契约证明的动态 Capture/Execution/Prediction 能力必须 bake fail。

## 跨层 Contract

| 方向 | 允许 | 禁止 |
|---|---|---|
| Shell → Boundary | intent、stable id、request id、冻结 payload | raw Entity、callback 闭包、同步 gameplay 返回值 |
| Boundary → Core | persistent inbox record、immutable session config | managed object、consumer cursor、资源引用 |
| Core → Boundary | final fact、stable provenance、snapshot/evidence | writable buffer、CoreReaction 中间态、临时 NativeContainer |
| Definition → Core | Blob、index/range、pure evaluator、validation metadata | lifecycle System、运行时 Dictionary、可变 row |

## Session 生命周期

Session install 必须一次性固定：World/SimulationEpoch、tick rate、AttributeLayout、TagCatalog、Definition content hash、scale profile 与 Boundary policy。运行中禁止热替换 layout/catalog；变化必须新建 Session/World。Session dispose 必须等待 Jobs、drain terminal facts、释放 runtime-created Blob，并使旧 handle/command 因 Epoch 不匹配而失效。

## 验收

- Shell 和 Boundary public API 零 raw Entity/World/EntityManager/EntityQuery/writable buffer。
- Runtime assembly 的 generated artifact 零 lifecycle owner。
- 所有 gameplay mutation 能归属到一个 target-owned kernel lane。
- 所有 managed consumer 只读同一 immutable Boundary batch。
- Headless、Scene 和 AutoChess runner 经过同一完整 FixedStep/EndFixed/Drain 路径。
- current facts、target spec、tasks、handoff 各自只写入 00/01/02/04 对应 owner。
