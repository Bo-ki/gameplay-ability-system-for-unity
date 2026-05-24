# ISSUE-009 Runtime Core Frame Backbone 缺失

## 状态

| 字段 | 内容 |
|---|---|
| 状态 | Active |
| 严重度 | P0 |
| 最近复核 | 2026-05-24 |
| 所属层 | GAS Runtime Core Layer / Runtime Boundary Layer |

## 问题陈述

当前 Runtime Core 已经有 AM2 command / spec / delta / fact 数据契约、AM3 simple instant 局部 proof、AM5 owner-local ActiveEffectStore 第一刀、Debugger counters baseline，以及 AM2B-A contract-first 的 phase 顺序与结构变化权限表。但这些仍是局部链路和契约层落点。当前实现还缺少 Unity DOTS 意义上的统一 frame backbone：query / lookup budget、allocator owner、dependency budget、deterministic stream merge、唯一 structural playback gate、真实 SystemGroup 搬迁和 Debugger evidence gate 尚未形成同一套可执行骨架。

这会导致后续 AM3 / AM5 即使继续迁移功能，也仍然是在 proof-only stream、legacy-backed mirror 和旧 group 顺序上扩张，无法证明已经进入 DOTS scale-ready 主线。

## 当前证据

官方依据：

1. `../../UnityDOTS官方文档参考/主题/02-查询遍历与Job.md` 要求 Runtime Core 热路径交还 query contract、filtered / unfiltered query count、lookup count、job count，并警告 `SystemAPI.Query` / 同步 query 不是百万实体 hot path 的默认答案。
2. `../../UnityDOTS官方文档参考/主题/03-结构变化-ECB-Enableable.md` 要求 hot path 不直接结构变化，结构变化必须集中到 mutation phase 或 ECB playback，且结构变化后必须重取 DynamicBuffer / lookup / handle。
3. `../../UnityDOTS官方文档参考/主题/04-数据承载-Buffer-Chunk-Store.md` 要求 DynamicBuffer 必须声明容量策略和 externalized 监控，单一全局 buffer 只能作为 proof 或低量 telemetry。
4. `../../UnityDOTS官方文档参考/主题/06-Diagnostics-Profiler-Journaling.md` 要求性能结论拆分 system、query、lookup、job、structural change、allocator 和 Burst warmup，`avgTickMs` 不能作为归因证据。
5. `../../UnityDOTS官方文档参考/主题/10-Collections-Allocator-NativeStream.md` 要求 NativeContainer 说明 allocator owner、生命周期、dispose / rewind 位置，影响 battle hash 的输出必须 deterministic。
6. `../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md` 明确 Effect command fan-in、Attribute delta、Debug telemetry 等模块要评估 NativeStream、per-owner buffer、ECB append、deterministic reduction 和 Profiler / Journaling 证据。
7. `../../UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 要求 Runtime Core 交还 API 选型表、query / buffer / job / structural metrics，并在官方依据改变当前实现问题时反哺 `00`。

代码证据：

1. AM2B-A 已在 `GASSystemScheduleContract` 中新增 contract-first 的 `RuntimeCoreFramePhases`，覆盖 `FramePrepare -> CommandIngest -> SpecEvaluation -> ActiveEffectLifecycle -> DeltaApply -> TypedFactProjection -> StructuralPlayback -> ObservationProjection`，并声明每个 phase 的读写访问和结构变化权限：`Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs`。
2. AM2 的 `SEffectCommandIngest / SInstantEffectSpecBuild / SActiveEffectMutationApply / SAttributeDeltaApply / STypedSimulationFactProjection` 已映射到 Runtime Core frame phase，但真实系统仍挂在旧 `GASCommandGroup` 中，尚未搬迁到独立 `ComponentSystemGroup`：`Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs`, `Assets/GAS/Runtime/System/Effect/SEffectCommandSpecStreamPhases.cs`。
3. `CEffectCommandSpecStream` 以 singleton entity + 多个 DynamicBuffer 承载 command/spec/delta/fact，buffer capacity 已定义，但当前文档和 Debugger 还没有把 spill、merge policy、frame owner、writer / reader phase 和 deterministic ordering 统一落成 frame backbone 证据：`Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs:40-71`, `Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs:88-177`。
4. `EffectCommandSpecStream.TryGetSingleton` 和 `ResolveCurrentFrame` 在 helper 中临时创建 query 并调用 `CalculateEntityCount()`；这说明当前还有 helper 级 query / lookup 行为，尚未统一进入 `Frame Arena / Query Preparation`：`Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs:210-218`, `Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs:365-373`。
5. `EffectCommandSpecStream.ClearFrameLocalData` 已有 frame-local buffer 清理，但它仍是 stream helper 级清理，不是 Runtime Core frame owner 表的一部分；后续消费者若继续扩张，仍难统一证明 clear / write / read / merge phase：`Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs:295-318`。
6. `GasRuntimeDebugger` 已能输出 request/spec/delta/fact、ECB playback、ActiveEffectStore slot pressure 等 counters，但字段仍偏业务流和结构变化近似，未覆盖 query count、lookup update count、allocator owner、dependency wait、NativeStream segment、deterministic merge policy 等 frame backbone counters：`Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs:53-75`, `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs:445-524`, `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs:911-982`。
7. `GASRuntimeQueryLayoutPlan` 已能标记 `GameplayEffectCommandSpecStream` 和 `ActiveEffectStore`，也能标记部分 `StructuralEntityManagerHotspot`，但它是 layout / decision plan，不等同于每帧 query / lookup / allocator / dependency 的实际预算和运行时证据：`Assets/GAS/Runtime/System/SystemGroup/GASRuntimeQueryLayoutPlan.cs:22-23`, `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeQueryLayoutPlan.cs:465-514`, `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeQueryLayoutPlan.cs:522-596`。

文档 / 任务证据：

1. `../../01-目标态架构共识/03-RuntimeCore管线Spec.md` 已将 `Frame Arena / Query Preparation` 列为 phase，并新增 `DOTS Backbone First`；这说明目标态已经明确该骨架是前置条件。
2. `../../02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCoreFrameBackbone.md` 已将 `GAS ECS Runtime - Runtime Core 重构 - Runtime Core Frame Backbone` 拆成 `AM2B-A -> AM2B-F` 连续任务链，并把 AM3 / AM5 后续扩张改为依赖该任务链。
3. `../../04-当前进度状态/当前窗口.md` 已将当前推荐领取推进到 `GAS ECS Runtime - Runtime Core Frame Backbone - Frame Arena 与 Query Budget`（任务ID：`T1-RuntimeCore-AM2B-B`）。

## 执行路径

```text
AM2/AM3/AM5 局部 proof 已落地
-> command/spec/delta/fact 和 owner-local slot 继续在旧 group / helper / singleton buffer 上扩张
-> query / lookup / allocator / dependency / structural playback 没有统一 frame owner
-> Debugger 只能看到业务 counters 和局部 ECB / slot pressure
-> x50 / x1000 / x10w profile 仍难判断问题来自 GAS 语义、query、buffer spill、dependency wait、allocator、structural playback 还是 Burst warmup
```

## 影响

1. AM3 / AM5 继续推进时，Agent 容易把局部 proof 当成目标态主线，扩大旧 lifecycle mirror 和 singleton stream。
2. 性能异常仍会被归因到单个 system timing，而不是被拆成 query、lookup、allocator、dependency、structural playback、buffer spill、job overhead 和 Burst warmup。
3. `ObjectDisposedException` 类结构变化问题可能被局部修复，但缺少全局 phase contract 时仍会以新形式回归。
4. AutoChess x10w / x100w scale gate 无法判断当前 Runtime Core 是否真正走 DOTS 优势路径。
5. Debugger 仍难成为 Goal 循环的自动停止证据，因为它不能证明 frame backbone 健康，只能证明局部 counters 下降。

## 根因反推

当前路线此前把重点放在 GAS 语义拆分：EffectCommand、InstantSpec、AttributeDelta、ActiveEffectStore、TypedFacts。这个方向正确，但 Unity DOTS 官方参考说明，百万实体热路径的基础不是“有 ECS 数据类型”，而是每帧执行骨架必须先清楚：

1. 哪个 SystemGroup 准备 query / lookup / type handle。
2. 哪个 allocator 承载 frame scratch，在哪里 rewind / dispose。
3. 哪个 phase 写 command/spec/delta/fact，哪个 phase 读，哪个 phase merge。
4. 哪个 phase 是唯一 structural playback gate。
5. 哪些输出影响 battle hash，如何 deterministic merge。
6. Debugger 如何把这些证据和 Unity Profiler / Entities Journaling / Burst Inspector 对齐。

因此该问题应从 ISSUE-008 中拆出，作为当前实现层 P0 问题独立维护。

## 目标态入口

1. `../../01-目标态架构共识/00-总览Spec.md`
2. `../../01-目标态架构共识/03-RuntimeCore管线Spec.md`
3. `../../01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
4. `../../01-目标态架构共识/05-ActiveEffectStoreSpec.md`
5. `../../01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
6. `../../UnityDOTS官方文档参考/README.md`
7. `../../UnityDOTS官方文档参考/主题/02-查询遍历与Job.md`
8. `../../UnityDOTS官方文档参考/主题/03-结构变化-ECB-Enableable.md`
9. `../../UnityDOTS官方文档参考/主题/04-数据承载-Buffer-Chunk-Store.md`
10. `../../UnityDOTS官方文档参考/主题/06-Diagnostics-Profiler-Journaling.md`
11. `../../UnityDOTS官方文档参考/主题/10-Collections-Allocator-NativeStream.md`
12. `../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
13. `../../UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`

## 任务入口

1. `../../02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCoreFrameBackbone.md`
2. `../../02-主线任务树/T4-Observation_Presentation_Debugger/RuntimeCoreDebugger.md`

## 退出条件

1. Runtime Core 存在明确的 frame backbone phase 顺序，至少覆盖 `FramePrepare -> CommandIngest -> SpecEvaluation -> ActiveEffectLifecycle -> DeltaApply -> TypedFactProjection -> StructuralPlayback -> ObservationProjection`。（AM2B-A 已完成 contract-first）
2. 存在 `GasRuntimeFramePrepareSystemGroup` 或等价 phase，统一准备 query、lookup、type handle、frame scratch、allocator 和 dependency budget。
3. 存在 `GasStructuralPlaybackSystemGroup` 或等价契约，作为 hot path 唯一 structural playback gate；其他 phase 不直接执行结构变化。
4. command / spec / delta / fact / active mutation stream 具备 frame owner 表，说明 owner、clear、write、read、merge、deterministic ordering 和重新选型触发条件。
5. Debugger 输出 frame backbone counters：query count、lookup update count、random lookup count、allocator owner、dependency wait、stream segment / merge cost、structural playback count、ECB command count、bulk query count、Burst / safety 口径。
6. AM3 / AM5 后续任务行动报告能说明如何复用 frame backbone，而不是继续扩张 proof-only singleton stream 或 legacy-backed mirror。
