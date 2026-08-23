# EX-GAS Runtime v1 总览 Spec

## 结论

EX-GAS Runtime v1 是一次不可兼容的 Runtime Core 换代：保留 UE GAS 的领域对象、生命周期、捕获、贡献与因果语义，采用 Unity DOTS 的单固定步内核、ASC-local generational slab、目标单写者和稳定态延迟反应；不保留现有五阶段物理管线、Ability/GameplayEffect runtime Entity 双权威，也不实现网络预测。

本 Spec 是目标态总入口。当前实现只能写入 `../00-当前架构事实/`，实施顺序只能写入 `../02-主线任务树/`，短期状态只能写入 `../04-当前进度状态/`。

## 设计基线

目标态同时受三类基线约束：

1. Unity Entities 1.4.6、Collections 2.6.6、Burst 1.8.29 与 Unity Physics 的官方规则，入口见 [Unity DOTS 官方文档参考](../../UnityDOTS官方文档参考/README.md)。
2. 本地 UE-GAS 源码中的 Ability、GameplayEffect、Aggregator、Tag、Cue、Task 与 EffectContext 语义，冻结点见 [24-GAS 官方概念对照复核](24-GAS官方概念对照复核Spec.md)。
3. EX-GAS 的单一权威、确定性、性能、边界和文档治理不变量，见 [90-目标态不变量](90-目标态不变量.md)。

“借鉴 UE GAS”不代表复制 UObject、同步调用栈、复制/预测协议或全套动态扩展面；“使用 DOTS”也不代表把每个领域概念变成 Entity/System。每个承载必须由 owner、生命周期、读写方向和规模证明共同决定。

## 四层边界

| 层 | 唯一职责 | 可以持有 | 禁止持有 |
|---|---|---|---|
| Layer 1 应用壳层 | 输入、AI、网络适配、场景、Demo、UI 业务 | 业务意图、稳定业务 ID、不可变 Boundary batch | Core buffer、query、allocator、raw Entity |
| Layer 2 运行时边界层 | 命令持久化入站、只读投影、Cue/Replay/Debugger 导出 | CommandPort、immutable batch/ring、资源绑定 | gameplay 决策、Core 写权限、消费者游标权威 |
| Layer 3 GAS Runtime Core | 全部 gameplay 权威和确定性计算 | ASC Entity、slab、Attribute/Tag buffer、tick scratch | managed 配置查询、表现资源、第二套状态权威 |
| Layer 4 定义与生成层 | Luban、Blob、静态索引、pure evaluator、bake validation | immutable catalog、schema/content hash、生成报告 | runtime lifecycle、query、ECB、NativeContainer owner |

层间只传递 intent、强类型 opaque handle、不可变 definition、tick-local record、稳定 ID、snapshot、fact 与 evidence。Runtime Boundary 对外不得暴露 `World`、`EntityManager`、`EntityQuery`、raw `Entity`、可写 `DynamicBuffer` 或 NativeContainer。

## Runtime 物理骨架

```text
SimulationSystemGroup
├─ FixedStepSimulationSystemGroup
│  ├─ PhysicsSystemGroup
│  ├─ GasFixedTickSystemGroup                 [UpdateAfter PhysicsSystemGroup]
│  │  ├─ GasCommandIngressSystem              唯一把 Boundary journal 搬入 ECS inbox
│  │  └─ GasTickKernelSystem                  唯一 tick scratch / Job DAG owner
│  └─ EndFixedStepSimulationEntityCommandBufferSystem
└─ GasBoundaryDrainSystem                     catch-up batch 后唯一 managed drain
```

`GasFixedTickSystemGroup` 是 `FixedStepSimulationSystemGroup` 的直接子组，不挂到 `AfterPhysicsSystemGroup`，避免未来 `CustomPhysicsSystemGroup` 对多个 PhysicsWorld 复制更新全局 GAS。一个渲染帧可以执行 `0..N` 个 `SimulationTick`；duration、period、cooldown 与 wake time 都是整数 tick。Session 启动后 tick rate 不可变，并进入 session/content hash。

AutoChess、Headless 与独立 World 必须通过唯一 Session runner 更新完整父链及 Boundary Drain，禁止直接更新 `GasTickKernelSystem` 或子 Group。独立 World 必须显式创建和排序标准 Begin/End FixedStep ECB 系统。

## 单内核数据流

```text
BoundaryIngressJournal
  -> pre-Fixed GasCommandIngressSystem
  -> ECS BoundaryCommandInbox
  -> Gather / TickStartSnapshot + PlanExpandScratchProvision
  -> OwnerPlanBuild（shadow RYW，零权威写）
  -> TargetResolve / bounded Expand
  -> WholeTickInfraAdmission（任何 gameplay 权威写之前）
  -> AscOwnerCommandWave（no-fail CommitPlan）
  -> SourceSpecProjection
  -> GroupByTarget / AscTargetStateWave
  -> Stabilize / Death
  -> StableFactMerge / per-BattleInstance TerminalResolve
  -> GroupNextTickRouteByDestination
  -> BoundaryProject / scoped cleanup outbox
  -> standard EndFixed structural playback
  -> managed staging accept / Single Drain
```

`GasTickKernelSystem` 拥有本 tick 的 query、`WorldUpdateAllocator`、全部临时 NativeContainer、完整 Job DAG 与最终 `JobHandle`。业务实现拆为命名 Job 和纯 evaluator；不得拆回多个物理 phase System，不得把临时容器跨 System 保存，不得在 phase 之间 `Complete()`。

所有写入按目标 ASC 稳定 ID 与 canonical key 分组。每个 ASC 每 tick 只有一个逻辑 mutation lane；该 lane 内可按固定子步骤串行，不同 ASC 并行。禁止通过解除安全限制或随机 `BufferLookup` 写入掩盖多写者。

## 权威数据模型

ASC Entity 是唯一 gameplay 聚合根。Ability、Activation、Continuation、Subscription 与 ActiveEffect 都是 ASC-local 非压缩 slab 槽，不是 Entity：

```text
GrantedAbilitySlot 1 ── N ActivationSlot 1 ── N ContinuationSlot
                                             └── N SubscriptionSlot
CooldownGateSlot
ActiveEffectSlot
AttributeValueSlot[AttributeLayout.Count]
TagCountSlot[TagCatalog.Count]
BoundaryFactBuffer (ASC + Session scoped cleanup outbox)
```

句柄统一包含 `SimulationEpoch + OwnerAscHandle(AscStableId, AscGeneration) + SlotIndex + SlotGeneration`，并以强类型防止不同池别名。ASC lifecycle generation 与 slot generation不可合并。槽只允许 tombstone/free-list 回收，禁止 `RemoveAt`、SwapBack 和存活期 compact；Generation 回绕是确定性 fault。回收前必须完成 terminal fact、子记录、订阅和队列引用交接。

ASC 的逻辑 Owner 与可替换 Avatar 分离。Core 内部 ActorBinding 可以保存当前 Avatar Entity，但跨 Boundary 只允许 `StableAvatarId + BindingGeneration`。

## Ability 与 Effect 语义

- Ability 遵循 `Definition -> GrantedAbilitySlot -> ActivationSlot`；同一 grant 是否并发由实例策略决定。
- `CanActivate` 与延迟发生的 `CommitCheck/CommitExecute` 分离；commit 对 owner-local cost、cooldown gate 与 activation state 原子，第二次 commit 显式拒绝。cost 直接更新同一 ASC 的 `AttributeValueSlot`；cooldown 进入不属于 Activation/ActiveEffect 的 `CooldownGateSlot`，所以 Commit 后 Cancel/End 不会把冷却撤销。
- 一个 Activation 可以同时拥有多个 Continuation/Subscription；结束或取消必须精确撤销该 Activation 的 Tag、Block、Cue、Effect contribution 和全部等待项。
- GameplayEffect 遵循 `Definition -> ApplicationSpec -> ActiveEffectSlot`；Instant 不创建 ActiveEffectSlot，一个 Spec 对多个目标生成独立 target capture/application record。
- Attribute 保留 Base/Current 和可逆 contributor；Aggregator 的 channel、op、qualifier、稳定顺序和 override tie-breaker 是权威语义。
- ActiveEffect 区分 Active、Inhibited 与 Removed；ongoing requirement 改变必须撤销/恢复 contribution，而不是销毁 definition identity。

## Capture 与反应时序

每个 capture 必须声明 Source/Target、Snapshot/Live、精确 capture phase、missing-source policy 和闭世界 `CaptureProjectionContract`。`ScalarSnapshot` 只有在生成器能证明全部 contributor、qualifier、tag/filter/ignore 输入已冻结且后续 API 不会升级查询视图时才合法；否则保存规范化 `AggregatorSnapshot`。跨 ASC Live Capture 必须有确定序依赖通知；未实现时 Definition 必须 bake fail，不能降级成“每 phase 采样”。

v1 有意采用 stable-state deferred reaction：GameplayEvent、OwnedTag 触发 Ability、外部 Continuation 唤醒和跨 ASC reaction 在发射 tick 不重入，冻结 payload 后在下一 tick 投递，每经过一条公开 reaction 边至少增加一 tick。这与 UE GAS 的同步 call-stack 语义不同。

同 tick 只允许：

- application requirement、immunity、stack/overflow 决策；
- activate/commit/cancel 与 owner-local cost/cooldown；
- modifier/tag/block/grant 的精确增加或撤销；
- ongoing/inhibition/aggregator 的目标局部稳定化；
- 生成期可完全展开、闭合、有限且静态有界的 pre-apply `DirectEffectProgram`。

本 tick Apply 后产生的公开 fact 不得重新进入本 tick fan-in。稳定化无固定点时必须以重复状态检测或安全预算触发确定性 fatal fault，禁止输出中间 Cue/fact，也禁止携带半稳定状态继续。

## Boundary 与结构变化

Core final fact 按 scope 写入唯一 `BoundaryFactBuffer : ICleanupBufferElementData`：ASC-scope 写所属 ASC，BattleInstance/Session-scope 写唯一 Session outbox；同一事实不得复制。EndFixed 销毁 owner 后，cleanup shell 仍保留终态事实；唯一 `GasBoundaryDrainSystem` 在 fixed catch-up 后按 scope identity/generation 稳定排序，managed staging 接管成功后只清 accepted watermark prefix并保留 late tail。Accepted shell 由下一 Kernel prepass记录本 tick标准 EndFixed removal；若 shutdown 后没有下一 tick，只在 FinalDrain完成后由 Session teardown显式清理。

`BoundaryEventId` 是交付去重键，`CueLifecycleKey` 是带 active-cycle identity 的 Cue 生命周期键，两者不得混用。Core 不维护 per-consumer cursor。overflow 必须显式：验证/Headless 失败，表现消费者可丢弃明确范围后走 snapshot reconcile；永不静默丢失。

结构变化统一录入标准 `EndFixedStepSimulationEntityCommandBufferSystem`。playback 后创建的 projectile、aura、zone 等派生 Entity 最早下一 tick 成为 Core 输入；它们可以承载空间/物理生命周期，但不得镜像 Ability/ActiveEffect 权威。

## Definition 与生成

Luban 和 SourceGenerator 只产出：

- immutable Blob catalog 与预解析 index/range；
- pure requirement/magnitude/target evaluator；
- `CaptureProjectionContract`、DirectEffectProgram、依赖图和规模报告；
- bake/CI validation、content/schema hash 与 Editor metadata。

生成器不得产出 Runtime `ISystem`、system registration、query、ECB、allocator、NativeContainer owner 或 gameplay lifecycle。Catalog/Layout 在 Session 内不可变；内容变化必须新建 Session/World，不迁移存活 ASC。

## v1 明确不做

- 网络复制、客户端预测、rollback、ack/reject/catch-up；schema/API 中不得保留恒为零的 Prediction 字段。
- UE 任意动态 Execution/Capture API；未在生成契约声明的动态视图必须 bake fail。
- 跨多个目标的分布式原子事务。
- Ability Entity、ActiveEffect Entity、Task Entity、Spec Entity 或 definition 可选 Entity backend。
- 自定义 FrameArena、手工 rewind 或分散结构变化组。
- dense/sparse Attribute 或 Tag 双 backend；规模失败后另立 ADR 并整体替换。

## Release Gate

目标态只有在以下证据同时成立时才可称为完成：

1. 旧五组、Ability Entity、legacy GE Entity、global ActiveEffect authority 和旧 Cue bridge 零运行引用。
2. stale handle、并发 Activation/Continuation、commit、capture、stack/period/inhibition、Tag count、Cue lifecycle 和 deferred reaction 语义测试通过。
3. replicated groups、hot-target、period burst、mass-death teardown、Boundary retry、wait fanout 与 cross-ASC live profile 分别给出内存、spill、merge、stabilization、Core、Drain 与 consumer 成本。
4. 同一 input/content hash 重跑得到相同 command trace、slot lifecycle、最终状态、fact/Cue 顺序和 battle hash。
5. 无正常 tick `CompleteAllTrackedJobs()`、无跨 System scratch、无 raw Entity 越过 Boundary、无 Prediction schema。

细节由本目录专题 Spec 唯一维护；本总览只冻结全局方向与 owner map。
