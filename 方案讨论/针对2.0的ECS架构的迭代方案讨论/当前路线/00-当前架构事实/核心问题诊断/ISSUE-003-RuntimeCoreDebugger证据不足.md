# ISSUE-003 Runtime Core Debugger 证据不足

## 状态

| 字段 | 内容 |
|---|---|
| 状态 | Active |
| 严重度 | P1 |
| 最近复核 | 2026-05-24 |
| 所属层 | Runtime Boundary Layer |

## DOTS规则引用

| 规则编号 | 规则摘要 | 关联原因 |
|---|---|---|
| `DBG-01` | Debugger 输出 phase/stream/cursor/结构变化预算 | 当前 ECB playback 非逐系统预算，spec/delta/fact 仍来自 EventBus 近似 |
| `DBG-02` | Debugger 证据与 Unity Profiler/Journaling 对照 | 缺少 Profiler/Journaling 真实采样对照 |
| `DBG-03` | Debugger counters 不依赖 Demo 巨类本地 collector | 当前依赖 `HeadlessAutoChessScenario` collector |
| `DBG-04` | 结构性 counters 解释为什么慢，非仅 system timing | system timing 只能排序不能归因 |
| `DBG-05` | Debugger 是健康事实流，不反向驱动 Runtime Core | 架构约束 |
| `NAT-05` | Allocator 指标进入 Debugger；GC-free ≠ allocation-free | 当前未监控 allocator 指标 |
| `BUF-01` | DynamicBuffer 声明容量策略和 externalized 监控 | store compact/cleanup/chunk skip 指标缺失 |
| `SYS-04` | Core/Physics/Presentation 成本分组统计 | Debugger 未提供分组成本 |
| `PRF-09` | Query/Filter/Allocator/Dependency 是架构输入 | Debugger 无法解释这些成本来源 |

## 问题陈述

当前 Debugger 已建立 AM-1 baseline，能记录 system timing、buffer pressure、RuntimeCore counters、entity lifecycle 近似量级、主要 ECB playback、presentation/replay cursor lag；AM-5 已补 owner-local ActiveEffectStore slot pressure / state distribution / legacy-backed / externalized owner baseline。但它仍未达到最终目标态：结构变化尚未逐系统预算化，spec / delta / fact 仍有一部分来自当前 EventBus 事实流近似，cleanup / chunk skip / compact 等 store 深层指标还要随后续 Runtime Core 重构继续补齐。

## 当前证据

代码证据：

1. `GasRuntimeDebugger.CollectRuntimeCoreCounters` 已从 `EntityManager` 和 EventBus / ReplaySink 采样 request/spec/delta/fact/cue/presentation、active effect peak、apply request peak、event bus buffer length、presentation/replay cursor lag：`Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs`。
2. `GasRuntimeDebugger.RecordRuntimeCoreEcbPlayback` 已提供结构化 ECB playback 计数入口，当前覆盖 effect utility、effect systems、ability cleanup/commit、ASC initialize、cue destroy 等主要路径：`Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs`。
3. AutoChess validation 已在 tick timing 后调用 `CollectAndRecordRuntimeCoreCounters`，summary 输出 `runtimeCoreCounters` 和 `runtimeCoreCountersPeak`：`Assets/AutoChessDemo/Validation/HeadlessAutoChessScenarioRuntimeTiming.cs`, `Assets/AutoChessDemo/Validation/HeadlessAutoChessScenario.cs`。
4. `GasRuntimeDebugger.CollectRuntimeCoreCounters` 已采样 owner-local ActiveEffectStore 的 owner 数、slot 数、capacity、PendingApply / Active / Inhibited / PendingRemove 分布、legacy-backed 数和 externalized owner 数，并在 `runtimeCoreActiveEffectStore` export 中输出：`Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs`。
5. 新增 `GasRuntimeDebuggerTests` 覆盖 counters 采样、cursor lag、ActiveEffectStore slot pressure、export 和 structural playback 事件：`Assets/_Test/GAS/Runtime/Debugger/GasRuntimeDebuggerTests.cs`。
6. 剩余不足：ECB playback 还不是逐系统预算；spec / delta / fact 的最终口径仍依赖后续 AM-2/AM-4 把 Runtime 主干切到 EffectCommand / SpecStream / AttributeDelta / TypedSimulationFact；ActiveEffectStore compact / cleanup / chunk skip 指标要随 store-driven lifecycle 继续补齐。

记录证据：

1. 技术停顿明确要求 Debugger 输出 request/spec/active effect/attribute delta/fact/cue/presentation 数量、生产者/消费者、结构变化次数、ECB playback、buffer pressure、cursor lag、phase barrier：`../../../迭代记录/91-T6-CHESS-AL-ArchitecturePipelineDiagnosis.md:77-85`。
2. 路线反思指出性能门槛没有同步升级为结构性指标，Debugger 早期偏 replay/log/export：`../../../迭代记录/92-T6-CHESS-AL-RouteReflectionFromPlans12To15.md:64-68`。

## 执行路径

```text
Runtime task runs AutoChess profile
-> avgTickMs / group timing indicates slow path
-> Agent reads validation summary / replay / presentation counts
-> Agent greps systems and manually correlates event count, buffer pressure, GE lifecycle
-> diagnosis depends on human reconstruction instead of debugger facts
```

## 影响

1. Agent 容易继续补局部 fast path，而不是识别 pipeline 粒度错误。
2. Debugger 不能作为 Runtime Core 的健康模块，无法在 Goal 循环中快速交还证据。
3. x10w / x100w 压力验收无法只靠人读日志维护。

## 根因反推

Debugger baseline 已能连接 Runtime Core 与 Runtime Boundary 的关键指标，AM-5 又补上了 owner-local ActiveEffectStore 的 slot pressure 证据。但仍需要随 AM-2/AM-4 的新主干迁移，把“事件流近似计数”升级为 EffectCommand / SpecStream / AttributeDelta / TypedSimulationFact 的权威生产消费计数，并随 AM-5 后续 lifecycle 迁移补齐 cleanup / compact / chunk skip。目标态 Debugger 应观察 phase、stream、cursor、结构变化预算和事实生产消费关系，但不能参与 gameplay routing。

## 目标态入口

1. `../../01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
2. `../../01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`

## 任务入口

`../../02-主线任务树/T4-Observation_Presentation_Debugger/RuntimeCoreDebugger.md`

## 退出条件

1. AutoChess 默认 profile 输出 request/spec/delta/fact/entity create/destroy/ECB playback/cursor lag/buffer pressure。
2. system timing 只作为排序证据，结构性 counters 能解释为什么慢。
3. Debugger 证据不依赖 Demo 巨类本地 collector 才能生成。
4. 逐系统结构变化预算、EffectCommand / SpecStream / AttributeDelta 权威计数、ActiveEffectStore cleanup / compact / chunk skip 随后续 Runtime 重构任务补齐。
