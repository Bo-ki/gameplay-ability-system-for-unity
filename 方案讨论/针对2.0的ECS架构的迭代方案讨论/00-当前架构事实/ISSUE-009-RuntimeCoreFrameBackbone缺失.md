# ISSUE-009 Runtime Core Frame Backbone 缺失

## 状态

| 字段 | 内容 |
|---|---|
| 状态 | Active |
| 严重度 | P0 |
| 最近复核 | 2026-05-25 |
| 所属层 | GAS Runtime Core Layer / Runtime Boundary Layer |

## DOTS规则引用

| 规则编号 | 规则摘要 | 关联原因 |
|---|---|---|
| `SYS-01` | 权威计算落在 ECS System/Job 数据流 | Frame backbone 是 Runtime Core 权威计算的前置骨架 |
| `SYS-02` | SystemGroup 是 phase owner，禁止手写 Tick 顺序 | 真实 SystemGroup 搬迁未完成，仍用旧 GASCommandGroup 等 6 个 group |
| `SYS-03` | 系统数量是成本源 | backbone 需统一管理 system/job 调度开销 |
| `PRF-04` | 结构变化必须集中到单一 ECB playback phase | `GasStructuralPlaybackSystemGroup` 已声明但未强制执行 |
| `PRF-09` | Query/Filter/Allocator/Dependency/Chunk layout 是架构输入 | Frame Arena 统一准备这些，不是各系统临时创建 |
| `PRF-14` | NativeContainer 必须明确 Allocator 归属和生命周期 | Frame scratch allocator 的 owner 和 rewind 位置需统一 |
| `QRY-01` | Hot path 优先 job 化 | backbone 需为各 phase 提供 query preparation |
| `QRY-02` | Query contract 写清 All/Any/None/Disabled/ChangeFilter | 各 phase 的 query contract 应该在 backbone 中定义 |
| `JOB-01` | 并行批处理说明 IJobEntity/IJobChunk 选择理由 | backbone 应提供 job scheduling 基础设施 |
| `NAT-01` | NativeContainer 说明 allocator owner、生命周期、dispose/rewind 位置 | Frame Arena 的 allocator 管理 |
| `NAT-03` | NativeStream 适合并行 fan-in | stream owner 的 scale 路径评估 |
| `BUF-02` | 单一全局 buffer 限于 proof | singleton EffectCommandSpecStream 是 proof-only，需迁移到 per-owner |
| `CASE-16` | SystemGroup Allocator — per-group scratch allocator | backbone 的 allocator 策略参考 |
| `SEL-02` | 禁止 proof API 固化为 scale-ready 方案 | 当前 singleton stream 不应固化为目标态 |
| `DBG-01` | Debugger 输出 phase/stream/cursor/结构变化预算 | backbone 需要可被 Debugger 观察的 phase contract |

## 问题陈述

当前 Runtime Core 已经有 AM2 command / spec / delta / fact 数据契约、AM3 simple instant 局部 proof、AM5 owner-local ActiveEffectStore 第一刀、Debugger counters baseline，AM2B-A contract-first 的 phase 顺序与结构变化权限表，AM2B-B contract-first / budget-first 的 query / lookup / allocator / dependency budget 表，AM2B-C contract-first 的 stream owner / deterministic merge policy 表，AM2B-D contract-first 的 structural playback gate contract / type anchor / route policy，AM2B-E contract-first 的 Debugger evidence gate counters / export / cost split / overhead 口径，以及 AM2B-F contract-first 的 AM3 / AM5 rebind handoff contract。但这些仍是局部链路和契约层落点。当前实现还缺少 Unity DOTS 意义上的统一可执行 frame backbone：真实 SystemGroup 搬迁、AM3 / AM5 的真实功能迁移复用、真实 Profiler / Journaling 采样和 AutoChess profile 尚未形成同一套运行时骨架。

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
2. AM2B-B 已新增 `GASRuntimeFrameBudgetContract`，声明 Runtime Core 当前 query / lookup / allocator / dependency budget，并显式统计 `WorldUpdateAllocator` owner、Rewindable allocator candidate、helper temp query risk、sync query risk 和 dependency wait risk：`Assets/GAS/Runtime/System/SystemGroup/GASRuntimeFrameBudgetContract.cs`。
3. AM2 的 `SEffectCommandIngest / SInstantEffectSpecBuild / SActiveEffectMutationApply / SAttributeDeltaApply / STypedSimulationFactProjection` 已映射到 Runtime Core frame phase，但真实系统仍挂在旧 `GASCommandGroup` 中，尚未搬迁到独立 `ComponentSystemGroup`：`Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs`, `Assets/GAS/Runtime/System/Effect/SEffectCommandSpecStreamPhases.cs`。
4. `CEffectCommandSpecStream` 以 singleton entity + 多个 DynamicBuffer 承载 command/spec/delta/fact，buffer capacity 已定义；AM2B-B 已把 helper query 风险显式纳入 budget contract，AM2B-C 已把六个 stream slot 明确登记为 migration carrier 并声明 target carrier、clear/write/read/merge phase、deterministic merge policy、sort key、battle hash input 和重新选型触发条件：`Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs:40-71`, `Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs:88-177`, `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeFrameBudgetContract.cs`, `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeStreamOwnerContract.cs`。
5. `EffectCommandSpecStream.TryGetSingleton` 仍在 helper 中临时创建 query 并调用 `CalculateEntityCount()`；`ResolveCurrentFrame` 的重复实现和 `GlobalTimer` fallback query 已删除，但 current-frame 仍由 `GASManager.EntityGlobalTimer` static known owner 提供，真实迁移到 `Frame Arena / Query Preparation` / frame owner 仍未完成：`Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs:210-218`, `Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs:365-373`, `Assets/GAS/Runtime/System/Core/SGlobalTimer.cs`, `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeFrameBudgetContract.cs`。
6. `EffectCommandSpecStream.ClearFrameLocalData` 已有 frame-local buffer 清理，但它仍是 stream helper 级清理，不是 Runtime Core frame owner 表的一部分；后续消费者若继续扩张，仍难统一证明 clear / write / read / merge phase：`Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs:295-318`。
7. `GasRuntimeDebugger` 已能输出 request/spec/delta/fact、ECB playback、ActiveEffectStore slot pressure、`runtimeCoreFrameBudget|...`、AM2B-D 的 structural gate tag / ECB command / bulk query 入口字段，以及 AM2B-E 的 `runtimeCoreFrameBackbone|...`、`runtimeCoreFrameBackboneEvidence|...`、`runtimeCoreCostSplit|...`、`runtimeCoreDebuggerOverhead|...` 文本导出；AM2B-C 已有 deterministic merge policy contract，AM2B-D 已有 structural playback gate contract，AM2B-E 已有 Debugger evidence gate contract，AM2B-F 已有 AM3 / AM5 rebind handoff contract，但 NativeStream segment / merge cost / battle hash stability 仍是 contract 口径，真实 structural playback gate runtime health 和 Profiler / Journaling 采样尚未闭合：`Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs`, `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeStreamOwnerContract.cs`, `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeStructuralPlaybackGateContract.cs`, `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeDebuggerEvidenceGateContract.cs`, `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeFrameBackboneRebindContract.cs`。
8. `GasStructuralPlaybackSystemGroup` 与 `GasEndStructuralEcbSystem` 已作为唯一 structural playback gate 的 type anchor 存在，`GASSystemScheduleContract` 的 `StructuralPlayback` phase 已指向该 group，但真实 group creation 和旧分散 structural playback 搬迁尚未闭合：`Assets/GAS/Runtime/System/SystemGroup/GASGroups.cs`, `Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs`。
9. `GASRuntimeQueryLayoutPlan` 已能标记 `GameplayEffectCommandSpecStream` 和 `ActiveEffectStore`，也能标记部分 `StructuralEntityManagerHotspot`；AM2B-B 的 frame budget contract 补齐了预算口径，但它仍不等同于真实 SystemGroup 中每帧 query / lookup / allocator / dependency 的运行时采样证据：`Assets/GAS/Runtime/System/SystemGroup/GASRuntimeQueryLayoutPlan.cs:22-23`, `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeQueryLayoutPlan.cs:465-514`, `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeQueryLayoutPlan.cs:522-596`, `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeFrameBudgetContract.cs`。

文档 / 任务证据：

1. `../../01-目标态架构共识/03-RuntimeCore管线Spec.md` 已将 `Frame Arena / Query Preparation` 列为 phase，并新增 `DOTS Backbone First`；这说明目标态已经明确该骨架是前置条件。
2. `../../02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCore重构/AM2B-FrameBackbone/README.md` 已将 `GAS ECS Runtime - Runtime Core 重构 - Runtime Core Frame Backbone` 拆成 `AM2B-A -> AM2B-F` 连续任务链，并把 AM3 / AM5 后续扩张改为依赖该任务链。
3. `../../04-当前进度状态/当前窗口.md` 已将当前推荐领取推进到 `GAS ECS Runtime - Runtime Core 重构 - Instant Spec Evaluation 迁移`（任务ID：`T1-RuntimeCore-AM3`），并明确 AutoChess 仍是 Runtime 重构后的后置全链路验收工具。

## 执行路径

```text
AM2/AM3/AM5 局部 proof 已落地
-> command/spec/delta/fact 和 owner-local slot 继续在旧 group / helper / singleton buffer 上扩张
-> query / lookup / allocator / dependency 已有预算 contract，stream owner / deterministic merge 已有 owner contract，structural playback 已有唯一 gate contract，Debugger evidence gate 已有 counters / export 口径，AM3 / AM5 已有 rebind handoff contract，但真实 SystemGroup 搬迁尚未完成，AM3 / AM5 功能迁移仍未证明完整复用该 backbone
-> Debugger 能看到预算口径、业务 counters、structural gate tag、frame backbone counters、Profiler / Journaling 对照口径和 cost split，但还不能给出真实 merge cost、真实 structural playback health 与 Profiler / Journaling 采样证据
-> x50 / x1000 / x10w profile 仍难判断问题来自 GAS 语义、query、buffer spill、dependency wait、allocator、structural playback 还是 Burst warmup
```

## 影响

1. AM3 / AM5 继续推进时，Agent 容易把局部 proof 当成目标态主线，扩大旧 lifecycle mirror 和 singleton stream。
2. 性能异常仍会被归因到单个 system timing，而不是被拆成 query、lookup、allocator、dependency、structural playback、buffer spill、job overhead 和 Burst warmup。
3. `ObjectDisposedException` 类结构变化问题可能被局部修复，但缺少全局 phase contract 时仍会以新形式回归。
4. AutoChess x10w / x100w scale gate 无法判断当前 Runtime Core 是否真正走 DOTS 优势路径。
5. Debugger 已能输出 frame budget 预算口径、structural gate 入口字段和 frame backbone evidence gate 口径，但仍难成为 Goal 循环的自动停止证据，因为它还不能证明 deterministic stream、真实 structural playback health、真实运行时等待健康和 AutoChess 业务 profile 归因。

## 根因反推

当前路线此前把重点放在 GAS 语义拆分：EffectCommand、InstantSpec、AttributeDelta、ActiveEffectStore、TypedFacts。这个方向正确，但 Unity DOTS 官方参考说明，百万实体热路径的基础不是“有 ECS 数据类型”，而是每帧执行骨架必须先清楚：

1. 哪个 SystemGroup 准备 query / lookup / type handle。
2. 哪个 allocator 承载 frame scratch，在哪里 rewind / dispose。
3. 哪个 phase 写 command/spec/delta/fact，哪个 phase 读，哪个 phase merge。
4. 真实 SystemGroup 搬迁如何接入唯一 structural playback gate，并确保旧分散结构变化点不绕过它。
5. 哪些输出影响 battle hash，如何 deterministic merge。
6. Debugger 已有对齐 Unity Profiler / Entities Journaling / Burst Inspector 的字段口径，但真实采样、对照日志和性能归因仍未完成。

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

1. `../../02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCore重构/AM2B-FrameBackbone/README.md`
2. `../../02-主线任务树/T4-Observation_Presentation_Debugger/RuntimeCoreDebugger/README.md`

## 退出条件

1. Runtime Core 存在明确的 frame backbone phase 顺序，至少覆盖 `FramePrepare -> CommandIngest -> SpecEvaluation -> ActiveEffectLifecycle -> DeltaApply -> TypedFactProjection -> StructuralPlayback -> ObservationProjection`。（AM2B-A 已完成 contract-first）
2. 存在 `GasRuntimeFramePrepareSystemGroup` 或等价 phase，统一准备 query、lookup、type handle、frame scratch、allocator 和 dependency budget。（AM2B-B 已完成预算 contract；真实 SystemGroup / lookup update 运行时证据后续继续）
3. 存在 `GasStructuralPlaybackSystemGroup` 或等价契约，作为 hot path 唯一 structural playback gate；其他 phase 不直接执行结构变化。（AM2B-D 已完成 contract-first；真实 SystemGroup 搬迁仍待后续任务闭合）
4. command / spec / delta / fact / active mutation stream 具备 frame owner 表，说明 owner、clear、write、read、merge、deterministic ordering 和重新选型触发条件。（AM2B-C 已完成 contract-first；真实 NativeStream / owner-local migration 后续继续）
5. Debugger 输出 frame backbone counters：query count、lookup update count、random lookup count、allocator owner、dependency wait、stream segment / merge cost、structural playback count、ECB command count、bulk query count、Burst / safety 口径。（AM2B-B 已输出预算字段；AM2B-D 已提供 structural gate tag 入口；AM2B-E 已完成 Debugger evidence gate counters / export 口径；真实运行时证据与 Profiler / Journaling 采样后续继续）
6. AM3 / AM5 后续任务行动报告能说明如何复用 frame backbone，而不是继续扩张 proof-only singleton stream 或 legacy-backed mirror。（AM2B-F 已完成 rebind contract；后续需由 AM3 / AM5 真实功能迁移继续证明）
