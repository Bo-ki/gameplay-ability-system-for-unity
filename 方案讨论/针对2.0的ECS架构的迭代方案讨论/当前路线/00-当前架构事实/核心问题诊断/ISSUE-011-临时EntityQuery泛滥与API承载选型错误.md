# ISSUE-011 临时EntityQuery泛滥与API承载选型错误

## 状态

| 字段 | 内容 |
|---|---|
| 状态 | Active |
| 严重度 | P0（EntityQuery 部分）/ P1（承载选型部分） |
| 最近复核 | 2026-05-25 |
| 所属层 | GAS Runtime Core Layer / Runtime Boundary Layer |

## DOTS规则引用

| 规则编号 | 规则摘要 | 关联原因 |
|---|---|---|
| `PRF-09` | Query/Filter/Allocator/Dependency/Chunk layout 是架构输入 | 热路径 helper 中临时创建 EntityQuery 违反此原则 |
| `PRF-33` | EntityQuery 必须通过 `SystemState.GetEntityQuery` 创建 | `EffectCommandSpecStream.TryGetSingleton`、Debugger 计数、Presentation / Replay fallback 等路径仍使用 `EntityManager.CreateEntityQuery` |
| `BUF-02` | 单一全局 buffer 不做百万实体 fan-in | `GASManager.EntityEventBus` 是全局 singleton 承载 5 种 buffer |
| `SEL-02` | 禁止 proof API 固化为 scale-ready 方案 | 全局 singleton buffer 不应固化为目标态 |
| `PRF-12` | 审核 SharedComponent 使用；审核 CleanupComponent 使用 | 历史 `[ThreadStatic]` 批量状态已移除；当前仍需审核 EventBus singleton 的承载边界 |
| `BUR-01` | Burst 编译代码不使用 managed 类型或 ThreadStatic | `EventBusHelper` 的 ThreadStatic 批量状态已被显式 writer 替换；后续仍需将全局 singleton EventBus 迁移到 job-safe fan-in 承载 |
| `SEL-01` | 数据性质分类优先：Gameplay/Transient/Telemetry/Presentation | EventBus 混合了多种数据性质的 buffer |
| `PRF-32` | 主线程数据操作禁用 `.Run()` Job | `ResolveCurrentFrame` 重复实现和 fallback query 已收口，但仍需把 current-frame 读取迁移到真实 frame owner / SystemAPI singleton |
| `PRF-34` | NativeContainer 在 IComponentData 上禁止调度 IJobChunk/IJobEntity | `CCueOnApply.cues` 等持有 `NativeArray<Entity>` |
| `CASE-04` | DynamicBuffer — owner-local 可变数组 | EventBus 把 DynamicBuffer 误用为全局消息总线 |

## 问题陈述

当前 Runtime Core 中存在两类严重的 API 承载选型错误：

**第一类（P0）：热路径中大量临时 EntityQuery 创建。** 历史上 5 个文件各自实现了实质相同的 `ResolveCurrentFrame` 方法，并各自调用 `EntityManager.CreateEntityQuery`（违反 `PRF-33`）；当前已收口为 `GASRuntimeFrameContext`，并删除 `GlobalTimer` fallback 临时 query。`EffectCommandSpecStream.AppendCommand` 原先每次调用都触发 `EnsureSingleton` → `TryGetSingleton` → `ResolveCurrentFrame`，在 x50 AutoChess 每帧数十到数百个 command 场景下会放大为数十到数百个临时 query。本轮已通过 `EffectCommandSpecStream.CommandWriter` 收缩 `GameplayEffectRequestWriter.TryAppendSimpleInstantCommands` 多目标 fan-out：批量写入只解析一次 stream entity / current frame / buffer，再循环 append command 并一次 flush；但单条 `AppendCommand`、其它 producer、EventBus、Debugger、Presentation / Replay 的临时 query 仍未闭合。

**第二类（P1）：EventBus 全局 singleton 反模式、历史 ThreadStatic 批量状态、NativeArray 挂在 IComponentData 上阻止 Job 化。** ThreadStatic 批量状态已替换为显式 writer/context，但全局 EventBus singleton 仍是迁移期承载。这些不是独立的 API 误用，而是共同的"承载选型未按 DOTS 规则复核"问题。

## 当前证据

代码证据（详见 `../代码DOTS合规审查报告.md`）：

### 子缺陷 C: 热路径临时 EntityQuery

| 文件 | 行号 | 调用频率 |
|---|---|---|
| `SGlobalTimer.cs` | `GASRuntimeFrameContext` | current-frame 读取已统一；fallback query 已删除，现为 `GASManager.EntityGlobalTimer` known-owner main-thread read |
| `GasRuntimeDebugger.cs` | `ResolveCurrentFrame` wrapper | 兼容入口已转发到 `GASRuntimeFrameContext`；重复实现已删除 |
| `EventBusHelper.cs` | `GASRuntimeFrameContext.ResolveCurrentFrame` | 每次事件入队 / 批量开始仍读取 current frame；重复实现已删除 |
| `SPresentationOutboxProjection.cs` | `GASRuntimeFrameContext.ResolveCurrentFrame` | 每帧调用仍读取 current frame；重复实现已删除 |
| `SDebugReplayLogProjection.cs` | `GASRuntimeFrameContext.ResolveCurrentFrame` | 每帧调用仍读取 current frame；重复实现已删除 |
| `EffectCommandSpecStream.cs` | `BeginCommandWriter(em)` | 单条 `AppendCommand` / `BeginCommandWriter(em)` 仍会解析 current frame；多目标 `TryAppendSimpleInstantCommands` 已改为一次 writer 解析后批量 append |
| `EffectCommandSpecStream.cs` | `TryGetSingleton` | 单条 `EnsureSingleton` 仍创建临时 query；多目标 writer 路径只触发一次 |
| `GasRuntimeDebugger.cs` | 1458-1459 | `CountEntitiesWith<T>` — 每帧调用 6+ 次 |
| `GasRuntimeDebugger.cs` | 1551-1552 | `ReadActiveEffectStoreCounters` — 每帧调用 |
| `GasRuntimeDebugger.cs` | 1616 | `CountPresentationOutboxEvents` fallback |

### 子缺陷 F: EventBus 全局 singleton 反模式

`GASManager.EntityEventBus` 是一个全局 singleton entity，承载 5 种 Buffer：
- `BGameplayEvent`
- `BAttributeChangeEvent`
- `BCueRequest`
- `BTagChangeEvent`
- `BDamageEvent`

每个事件产生时同时写入旧 EventBus 路径和新 typed fact stream 路径。

### 子缺陷 H: ThreadStatic 批量状态

本子缺陷已局部闭合：`EventBusHelper` 中的 `[ThreadStatic]` ambient batch 字段已移除，批量路径改为显式 `GameplayEventBusWriter` 持有 `EntityManager`、`eventBusEntity`、`CGameplayEventBus` snapshot、frame 和可写 buffer 标记，并在 `Dispose/Flush` 时一次回写 `CGameplayEventBus`。

当前边界：

1. `AbilityCommit` / `AbilityStateCleanup` 已迁移为显式 writer 写入 gameplay event。
2. `SApplyGameplayEffectRequest` 的 legacy instant bypass / runtime GE instantiate event 已迁移为显式 writer，`CEffectContext.ContextId` 和 `BGameplayEvent.Sequence` 在同一 apply 流程内复用 writer snapshot。
3. `EffectMagnitudeResolver` 已新增 writer overload，避免 execution calculation missing fact 在 `SApplyGameplayEffectRequest` 外层 writer 尚未 flush 时走静态 enqueue 读旧 `NextSequence`。
4. `SEffectCommandSpecStreamPhases` 中 attribute typed fact bridge 与 Cue-on-Apply projection 已迁移为显式 writer 写 legacy `BGameplayEvent` / `BCueRequest`。
5. 保留的单条 `EventBusHelper.Enqueue*` 入口仍是 legacy / 低频路径，每次 gameplay event 仍单独解析 current frame 和写回 `CGameplayEventBus`。
6. 显式 writer 只是去 ThreadStatic 与 legacy callsite 收缩的 main-thread 迁移承载，不等于 Burst/job-safe fan-in；全局 singleton EventBus 后续仍需按 `NativeStream` / owner-local fact buffer / deterministic merge 重新选型。

历史违规代码：

```csharp
[ThreadStatic] private static bool _gameplayEventBatchActive;
[ThreadStatic] private static EntityManager _gameplayEventBatchEntityManager;
[ThreadStatic] private static Entity _gameplayEventBatchEventBusEntity;
[ThreadStatic] private static CGameplayEventBus _gameplayEventBatchEventBus;
[ThreadStatic] private static int _gameplayEventBatchFrame;
[ThreadStatic] private static bool _gameplayEventBatchCanAppend;
// ... 还有更多
```

`[ThreadStatic]` 在 Burst 编译的 job 中不工作。类文档声称"在 Burst 并行 Job 中使用 AsParallelWriter 安全入队"与此矛盾。

### 子缺陷 J: ResolveCurrentFrame 重复实现 5 次

本子缺陷已局部闭合：`GasRuntimeDebugger.cs`、`EventBusHelper.cs`、`SPresentationOutboxProjection.cs`、`SDebugReplayLogProjection.cs`、`EffectCommandSpecStream.cs` 中的重复 `ResolveCurrentFrame` 实现已删除或转发到 `GASRuntimeFrameContext`，且 `GASRuntimeFrameContext` 的 fallback 临时 `GlobalTimer` query 已删除。目标态尚未完成：current-frame 仍由 `GASManager.EntityGlobalTimer` static known owner 提供，后续必须迁移到 frame owner / `SystemAPI.GetSingleton` 或 `SystemState.GetEntityQuery` 预创建路径。

### 已完成的局部收缩

2026-05-25 本轮 `T1-RuntimeCore-AM3 EventBus legacy callsite writer 迁移`：

1. `SApplyGameplayEffectRequest` 在 `OnUpdate` 中创建一次 `GameplayEventBusWriter`，并通过 `ref` 传入 target 处理、runtime GE instantiate event、legacy instant bypass 与 direct bypass。
2. legacy instant bypass 的 `EffectInstanced`、`CueRequested`、`AttributeChange`、`GameplayEffectApplied` 写入改为使用 writer，不再直接调用静态 `EventBusHelper.Enqueue*`。
3. `EffectMagnitudeResolver` 新增 writer 版 `ResolveModifiers` / `ResolveExecutionCalculation` / `EnqueueMagnitudeFact`，解决外层 writer 与内部静态 enqueue 争用 `CGameplayEventBus.NextSequence` snapshot 的风险。
4. `SEffectCommandSpecStreamPhases` 的 attribute fact bridge 与 Cue-on-Apply projection 改为显式 writer 写入 legacy EventBus buffer。
5. 这些只解决 AM3 / legacy instant 相关 callsite 的 writer 一致性；`EffectRuntimeUtility`、`SExecutionCalculation*`、`AbilityRuntimeActions`、`SAbilityTimelineAction`、`SAscCommandRequest`、`AttributeHelper` 等仍有静态 `EventBusHelper.Enqueue*`，全局 EventBus singleton 仍未拆分。

2026-05-25 本轮 `T1-RuntimeCore-AM3 Command Writer Frame Query 收缩`：

1. `EffectCommandSpecStream` 新增 `CommandWriter`，持有 stream entity、`CEffectCommandSpecStream` counters、`BEffectCommand` buffer、`BEffectCommandSetByCallerValue` buffer 和 current frame。
2. `GameplayEffectRequestWriter.TryAppendSimpleInstantCommands` 在 all-or-fallback 预检成功后只调用一次 `BeginCommandWriter`，再对目标列表循环 append command，最后一次 `Flush()` 回写 counters。
3. 新增测试 `CommandWriterReusesResolvedFrameAndFlushesStreamCounters` 覆盖批量 command 同一 frame、连续 sequence/context、SetByCaller range 和 flush 后 counters。
4. `GASRuntimeFrameContext` 统一 current-frame 读取，删除 `EventBusHelper`、`SPresentationOutboxProjection`、`SDebugReplayLogProjection`、`EffectCommandSpecStream` 中的重复 `ResolveCurrentFrame`，`GasRuntimeDebugger.ResolveCurrentFrame` 保留为兼容 wrapper。
5. 后续小闭环已删除 `GASRuntimeFrameContext` 的 `GlobalTimer` fallback query，并将 `RuntimeFrameContextCurrentFrame` 降级为 0 query budget 的 known-owner main-thread read。
6. 这些只解决已迁入 simple instant 多目标 producer 的 per-command query 放大、current-frame 代码重复和 current-frame fallback query 问题；单条 producer、EventBus、Debugger、Presentation / Replay 其它临时 query 和真实 frame owner / SystemState query 预创建仍是未闭合项。

### 子缺陷 L: NativeArray 挂在 IComponentData 上

`CCueOnApply.cues`、`CCueOnAdd.cues` 等 Cue 组件持有 `NativeArray<Entity>` 字段。这阻止了这些组件被 `IJobChunk`/`IJobEntity` 处理（安全系统无法追踪 component 内嵌容器的读写依赖）。

## 执行路径

```text
每帧多个系统调用 AppendCommand / EnqueueGameplayEvent / diagnostic record
-> 单条调用仍可能触发 EnsureSingleton / TryGetSingleton
-> 多目标 simple instant command writer 已收缩为一次 BeginCommandWriter + 多次 append + 一次 Flush
-> 内部创建临时 EntityManager.CreateEntityQuery
-> CalculateEntityCount() / HasSingleton() 触发 sync point
-> 临时 query 被 GC 回收
-> 每帧可能数十到数百个临时 query + sync point

同时：
-> EventBus 全局 singleton buffer 被多个系统并发读写
-> 历史 ThreadStatic 批量状态已移除，但 EventBus singleton 仍不是 job-safe fan-in 承载
-> NativeArray<IComponentData> 组件阻止 Job 化
```

## 影响

1. **临时 EntityQuery 泛滥**（P0）：`EffectCommandSpecStream.AppendCommand` 单条路径、Debugger 计数和 Presentation / Replay 其它 helper 仍可能创建临时 query，每次都可能带来 sync point + GC alloc；`GASRuntimeFrameContext` fallback query 已删除，但 frame owner static read 仍需后续目标态化。
2. **EventBus 全局 singleton**（P1）：所有事件写同一个全局 buffer，无法并行 fan-in，buffer pressure 随事件数线性增长。
3. **ThreadStatic 阻塞 Burst**（P1，已局部闭合）：`EventBusHelper` 不再依赖 `[ThreadStatic]` ambient batch；但当前显式 writer 仍基于 main-thread `EntityManager`，后续要进入 `NativeStream` / owner-local fact stream 才能支持 Burst/job-safe fan-in。
4. **代码重复**（P2，已局部闭合）：5 个文件各自维护 `ResolveCurrentFrame` 的问题已收口，fallback query 已删除；剩余风险是统一 helper 仍未迁移到目标态 frame owner / SystemAPI singleton。
5. **NativeArray on IComponentData**（P2）：阻止 Cue 组件进入 IJobChunk/IJobEntity，强制回退到主线程。

## 根因反推

这些问题的共同根因是：**当前实现把 API 承载选型当作"实现细节"而非架构决策。**

1. EntityQuery 被视为"需要时创建即可"的工具，而非必须在 `SystemState.OnCreate` 中预创建的架构输入。
2. DynamicBuffer 被视为"全局消息队列"（历史 EventBus 惯性），而非 owner-local 可变数组（DOTS 设计意图）。
3. 历史上 `[ThreadStatic]` 被当作批量优化手段；本轮已替换为显式 writer/context，但 writer 仍是迁移期承载。
4. `NativeArray` 被放在 `IComponentData` 上方便主线程访问，但未考虑 Job 安全系统的限制。

目标态应：
- 所有 EntityQuery 在 system 的 `OnCreate` 中通过 `SystemState.GetEntityQuery` 预创建。
- EventBus 全局 singleton 拆分为 per-owner typed fact buffer + presentation outbox bridge。
- 批量状态使用 job-safe 方式（ECB parallel writer、NativeStream 等）。
- Cue 数据改用 `IBufferElementData` 或拆分到独立 entity 上。

## 目标态入口

1. `../../01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
2. `../../01-目标态架构共识/13-EntityComponent物理布局Spec.md`
3. `../../01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`
4. `../../UnityDOTS官方文档参考/主题/02-查询遍历与Job.md`
5. `../../UnityDOTS官方文档参考/主题/04-数据承载-Buffer-Chunk-Store.md`
6. `../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`

## 任务入口

1. `../../02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCore重构.md`
2. `../../02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCoreFrameBackbone.md`

## 退出条件

1. Runtime Core 热路径中零 `EntityManager.CreateEntityQuery` 调用；所有 query 在 system `OnCreate` 中通过 `SystemState.GetEntityQuery` 预创建。
2. `ResolveCurrentFrame` 重复实现已统一，并且 `GASRuntimeFrameContext` 不再创建临时 EntityQuery。
3. `GASManager.EntityEventBus` 全局 singleton 已拆分为 per-owner typed fact buffer + Boundary outbox bridge。
4. `EventBusHelper` 的 `[ThreadStatic]` 字段已移除并替换为显式 writer/context；job-safe 机制（NativeStream / per-owner fact buffer / deterministic merge）仍是 EventBus singleton 拆分的后续退出条件。
5. `CCueOnApply.cues` 等 `NativeArray<Entity>` 字段已替换为 `IBufferElementData` 或独立 entity 方案。
6. AutoChess validation summary 输出 per-frame query create count、singleton buffer pressure per type、legacy single-entry EventBus writer count 和 job-safe fan-in health。
