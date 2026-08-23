# 命名规范 Spec

## 结论

名称必须暴露 owner、生命周期、读写方向和语义身份；不得用 `Manager`、`Helper`、`Utility`、`Adapter`、`EventBus`、`Frame` 或 `SpecStream` 掩盖混合职责。v1 统一使用 SimulationTick、Kernel、Slot、Fact、Inbox/Outbox 与强类型 Handle 口径。

## 核心类型

| 概念 | 目标命名 | 禁止/淘汰口径 |
|---|---|---|
| ASC 身份 | `GasAscIdentity`、`AscInstanceId` | 仅 raw `Entity` 的 `ASCHandle` |
| Owner/Avatar | `GasActorBinding`、`StableAvatarId`、`BindingGeneration` | `OwnerEntity` 同时代表逻辑 Owner 和 Avatar |
| Ability 定义 | `AbilityDefinition` | runtime Ability UObject/Entity |
| 授予实例 | `GrantedAbilitySlot`、`GrantedAbilityHandle` | `AbilitySlotBuffer<Entity>` |
| 激活实例 | `AbilityActivationSlot`、`AbilityActivationHandle` | 单 `AbilityStateComponent`、无类型 instance id |
| 异步状态 | `AbilityContinuationSlot`、`ContinuationHandle` | 单 `ProgramCounter + WaitMask` 冒充多个 Task |
| 外部监听 | `AbilitySubscriptionSlot`、`SubscriptionHandle` | callback delegate 作为 Core 权威 |
| Effect 施加值 | `EffectApplicationSpec`、`EffectApplicationId` | 把 Spec 与 ActiveEffect 混称 Effect instance |
| 存活 Effect | `ActiveEffectSlot`、`ActiveEffectHandle` | ActiveEffect Entity / global row handle |
| 属性 | `AttributeValueSlot`、`AttributeAggregatorSlot` | per-attribute component mirror |
| Tag | `TagCountSlot`、`ExactCount`、`InclusiveCount` | 固定 256 位 mask 作为唯一权威 |
| 出站事实 | `BoundaryFactBuffer`、`BoundaryEventId` | 通用 EventBus / ObservationFact 混用 |
| Cue 生命周期 | `CueLifecycleKey`、`ActiveCycleOrdinal` | 用 EventId 代替 Cue lifetime key；无 cycle 的旧 CueInstanceKey |

## 调度名称

物理类型只保留：

- `GasFixedTickSystemGroup`
- `GasCommandIngressSystem`（CommandPort 的必装配对；唯一执行 Boundary journal → ECS inbox）
- `GasTickKernelSystem`
- Unity 标准 `EndFixedStepSimulationEntityCommandBufferSystem`
- `GasBoundaryDrainSystem`

Command Gather、Activation、Effect Fan-In、TargetOwned Apply、Stabilize、Attribute Apply、Fact Route 是 Kernel 内 logical stage/Job 名，不得命名为 `*SystemGroup`。旧 `GASFramePrepareSystemGroup`、`GASCommandResolveSystemGroup`、`GASCoreSystemGroup`、`GASStructuralCommitSystemGroup`、`GASBoundaryProjectionSystemGroup` 只允许在 00 当前事实或迁移删除清单中出现。

## 时间名称

- 权威离散时间使用 `SimulationTick`、`DueTick`、`EmitTick`、`DeliverTick`、`WakeTick`。
- 渲染循环使用 `RenderFrame`；不能用“本帧”同时指 RenderFrame 和 FixedStep。
- `tick-local record/scratch` 取代含糊的 `frame-local`/`FrameArena`。
- Tick rate 进入 Session config/hash；duration、period、cooldown 不使用浮点秒作为权威存储。

## Handle 规则

每类长期对象使用强类型 handle：

```text
GrantedAbilityHandle
AbilityActivationHandle
ContinuationHandle
SubscriptionHandle
ActiveEffectHandle
PendingEffectSpecHandle（仅跨 tick Spec 确需独立所有权时）
```

结构字段统一包含 `SimulationEpoch + OwnerAscInstanceId + SlotIndex + Generation`。跨无类型 carrier 时额外携带 `HandleKind`；不同池相同数字不得合法别名。`CausalityId` 用于追踪/排序/环检测，不得命名为 PredictionId。

## Command、Fact 与 API 动词

- 外部异步意图：`RequestActivateAbility`、`RequestCancelActivation`、`RequestApplyEffect`。
- Kernel 命令记录：`ActivateAbilityCommand`、`ApplyEffectCommand`。
- 纯验证：`CanActivate`、`CheckApplicationRequirements`。
- 原子提交：`CommitCheck`、`CommitExecute` 或封装后的 `TryCommitActivation`，名称必须保留二次检查语义。
- 状态读取：`TryGetSnapshot`、`ResolveIndex`、`Read*`；禁止返回 writable buffer 的 `Get*`。
- 结果：`RejectReason`、`EndReason`、`RemovalReason`、`StabilizationFault`，禁止只用 bool 隐藏原因。

## Capture 名称

使用：

- `CaptureSource = Source | Target`
- `CaptureTiming = Snapshot | Live`
- `CapturePhase`
- `CaptureProjectionContract`
- `ScalarSnapshot`、`AggregatorSnapshot`、`LiveCaptureBinding`
- `PhaseSample`：明确表示一次 phase read，不得称为 Live Capture。

Projection contract 的轴使用 `ValueView`、`EvaluationBinding`、`Lifetime`。未声明视图的调用应命名为 validation/bake error，不提供 `DynamicCapture` 逃生口。

## Boundary 名称

- ECS 持久入站：`BoundaryCommandInbox`。
- ECS scoped cleanup 出站：ASC-scope `BoundaryFactBuffer` 位于 ASC，BattleInstance/Session-scope 位于 Session。
- managed 不可变导出：`BoundaryBatch`、`BoundaryBatchRing`。
- 只读状态投影：`ReadModelSnapshot`。
- 诊断证据：`DiagnosticsSnapshot`、`ValidationEvidence`。

`Inbox` 表示待 Core 消费；`Outbox/FactBuffer` 表示待唯一 Drain 导出；`Batch` 表示已经脱离 ECS 的不可变结果。三者不能用一个 `EventBuffer` 泛称。

## Definition 与生成

- `*Definition`：不可变语义定义。
- `*CatalogBlob` / `*LayoutBlob`：Session 内不可变运行时目录。
- `Generated*Lookup/Evaluator/Metadata`：纯生成物。
- `*Baker` / `*Bootstrap`：初始化 owner。

禁止 `Generated*System`、`Generated*Manager`、`Generated*Lifecycle` 作为目标态 Runtime 类型。`RuntimeGlue` 只有在内容确为纯函数/静态索引时可用。

## 命名验收

1. 看名称即可判断是 Definition、tick-local command、owner-local slot、Core fact、Boundary batch 还是 evidence。
2. 名称中 `Frame` 不再用于 fixed simulation 权威语义。
3. 目标态正文不再把逻辑 stage 写成五个 SystemGroup。
4. public API 不出现 raw Entity、PredictionKey、无类型 slot id 或 writable buffer。
5. 同一术语只由 [91-术语表](91-术语表.md) 给出全局定义，专题 Spec 只引用或增加领域约束。
