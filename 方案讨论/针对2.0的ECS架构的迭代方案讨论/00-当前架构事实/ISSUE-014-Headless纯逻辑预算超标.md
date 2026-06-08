# ISSUE-014 Headless 纯逻辑预算超标

> 最近复核：2026-06-08 | 状态：Active | 严重度：P0/P1 交界

## 当前结论

当前 AutoChess x50 无头验证已经能把“业务链路通过”“strict pure logic budget 通过”和“DOTS Profiler-backed performance excellent”拆成三种不同结论。无头场景没有模型、特效、UI、动画、音频和真实 PlayerLoop 表现成本；真实游戏里这些成本会占用大部分帧预算，因此 GAS Runtime 纯逻辑平均耗时必须远低于 16.67ms / 33.33ms 的整帧预算。最新 DebuggerProbe Run3 已通过 x50 strict pure logic budget，但仍不能写成性能优秀，因为 Profiler evidence disabled，且 Debugger/Journaling 已定位出明显的数据形态热点。

上一轮 x50 / 200 units / measuredTicks=9 证据显示：

| 指标 | 当前值 | 新严格预算 | 判定 |
|---|---:|---:|---|
| `avgTickMs` | 3.692ms | 1.500ms | 不通过 |
| `GASTickTotal.avgMs` | 3.664ms | 1.500ms | 不通过 |
| `GASTickTotal.maxMs` | 6.200ms | 3.000ms | 不通过 |
| `CoreRuntimeOwner.avgMs` | 2.540ms | 1.000ms | 不通过 |
| `GASCoreSimulationSystemGroup.avgMs` | 2.255ms | 0.900ms | 不通过 |
| `BoundaryOwner.avgMs` | 1.006ms | 0.350ms | 不通过 |
| `RunnerOwner.avgMs` | 0.119ms | 0.150ms | 通过 |
| `DebuggerOwner.avgMs` | 56.168ms | 不并入 performance pass | diagnostic-only |

最新 DebuggerProbe Run3 / x50 / 200 units / measuredTicks=9 证据显示：

| 指标 | Run3 当前值 | 严格预算 | 判定 |
|---|---:|---:|---|
| `avgTickMs` | 0.834ms | 1.500ms | 通过 |
| `GASTickTotal.avgMs` | 0.825ms | 1.500ms | 通过 |
| `GASTickTotal.maxMs` | 1.151ms | 3.000ms | 通过 |
| `CoreRuntimeOwner.avgMs` | 0.569ms | 1.000ms | 通过 |
| `GASCoreSimulationSystemGroup.avgMs` | 0.479ms | 0.900ms | 通过 |
| `BoundaryOwner.avgMs` | 0.218ms | 0.350ms | 通过 |
| `RunnerOwner.avgMs` | 0.038ms | 0.150ms | 通过 |
| `DebuggerOwner.avgMs` | 49.094ms | 不并入 performance pass | diagnostic-only |
| `performanceExcellentPassed` | false | 必须 profiler evidence enabled | 阻塞 |

## 当前代码事实

1. `AutoChessBattleValidationRun` 已新增 `AutoChessHeadlessLogicBudgetResult`，把严格无头逻辑预算接入 `AutoChessValidationRunResult.Passed`。
2. `AutoChessBattleValidationReport.CreateRunResultSummary(...)` 已输出 `headlessLogicBudgetPassed` 与 `performanceExcellentPassed`。
3. `AutoChessBattleValidationReport.CreateHeadlessLogicBudgetSummary(...)` 已输出预算阈值、实测值、failure mask、Profiler evidence 和 observation pollution。
4. `AutoChessRuntimeRunner` 已输出 `AutoChessDemoHeadlessLogicBudget` / `AutoChessDemoHeadlessLogicBudget:` 报告行。
5. 2026-06-08 DebuggerProbe Run3 已把 scorecard source 修正为 `DiagnosticPassGasData+PerformancePassOverhead`：timing / strict budget 仍来自 performance pass，GAS concept / data shape / API health / structural family 来自 diagnostic pass，performance observation pollution gate 仍来自 performance pass overhead。
6. `Tools/Diagnostics/Analyze-AutoChessProfile.ps1` 已能解析 `AutoChessDemoHeadless*` 新 summary、`runtimeDataOrientedScorecard` 机器行、Journaling TopN、Debugger evidence 和 scorecard family 覆盖度，并在旧 Run2 上识别 `GAS-MEASURE-03` 口径缺陷。

## 官方规则对照

| 规则族 | 当前采用结论 |
|---|---|
| `SYS-03` / `PRF-04` | system 数量、phase 划分和结构变化都会转化为帧成本；不能只看功能通过 |
| `QRY-04` / `PRF-05` | lookup、query materialization、sync point 和 random access 必须按 owner 归因 |
| `DBG-01..05` | Debugger / Profiler / Journaling 必须作为证据链，不能用字符串日志替代性能结论 |
| `ODF-18` | 无头 Demo 只省略真实资源和画面，不省略业务边界链路；无头性能不能按整帧预算放宽 |

## DOTS 原理驱动指标

本 ISSUE 不把 ms 数字当作唯一优化依据。ms 是验收结果，真正的诊断指标必须回答“当前实现是否符合 DOTS 的数据和调度原理”：

| 原理 | 必看指标 | 超标时说明 |
|---|---|---|
| chunk-linear iteration | `measuredUsPerUnit`、`coreSimulationUsPerUnit`、chunk count、enabled mask 分布 | 单位数增长时成本不接近线性或单位成本偏高，说明 chunk locality / owner-local layout 不够好 |
| scheduled job + Burst | job schedule count、Burst 覆盖、main-thread system timing、managed allocation | hot path 仍有主线程工作、托管依赖或 job 粒度错误 |
| lookup / random access | `GetComponentDataRW`、`GetBufferRW`、lookup refresh、random lookup read/write count | 说明数据没有按 owner / target group 聚合，仍靠跨 entity lookup 消费 |
| DynamicBuffer / carrier pressure | buffer peak/capacity、externalized ratio、spill / overflow、owner group range | 说明 singleton carrier 或 per-owner buffer 容量设计不足 |
| structural playback | required / recorded playback、Journaling structural record、ECB playback ms | 说明结构变化相位和批量播放成本没有闭环 |
| sync / materialization | dependency drain、`ToEntityArray`、query materialization、performance pollution | 说明 Debugger / Boundary / Runner 成本可能混入 Core 结论 |
| workload-normalized cost | `commandsPerMeasuredTick`、`gasTickUsPerCommand`、`coreFactsPerMeasuredTick`、`coreSimulationUsPerCoreFact` | 说明业务吞吐和数据量不匹配，不能只用总 tick ms 判断 |

因此下一轮优化不能只追 `avgTickMs` 下降；必须同时让 TopN 和 normalized cost 指向更健康的数据布局：更少跨 owner lookup、更少 singleton buffer 热点、更明确的 owner-local range、更少 main-thread materialization、更稳定的 structural playback 和更高 Burst 覆盖。

## 风险判定

1. `passed=True` 的旧 x50 日志必须降级为“业务链路通过”，不能再作为性能优秀证据。
2. `ProfilerCaptureState=profiler disabled; Entities profiler modules collect no data` 时，`performanceExcellentPassed` 必须为 false。
3. Debugger diagnostic pass 的 `DebuggerOwner` 成本只能用于热点定位，不能并入 performance pass，也不能被忽略。
4. `CoreRuntimeOwner` 与 `GASCoreSimulationSystemGroup` 是下一轮首要瘦身目标；`BoundaryOwner` 是第二优先级，说明 observation / projection / report side 仍然过重。
5. `GetBufferRW=67884`、`GetComponentDataRW=18892`、`GetBufferRW@GASActiveEffectPreTickSystem=15800`、`OwnerLocalGameplayFactBuffer=11250`、`GEEffectCommandStreamComponent=9237` 这类 TopN 必须按数据 owner 继续拆，不得只作为日志数字归档。
6. Run3 虽然 x50 strict budget 通过，但仍命中 `dependencyWaitRisks=4`、`syncQueryBudget=13`、`ownerLocalFactMaxOwnerRange=9`、`executionSpecScans/executionMatchedEffectSpecs=3.86:1` 和 `Profiler disabled`；因此 ISSUE-014 不关闭，只从“x50 预算超标”升级为“x50 预算通过但规模化 / Profiler / 数据形态未达 DOTS 优秀”。

## 退出条件

1. AutoChess x50 headless `headlessLogicBudgetPassed=True`。
2. x100 / x1000 至少有同构 summary，并输出 scale blocked reason 或通过结果。
3. `performanceExcellentPassed=True` 只能在 strict budget 通过且 Profiler evidence enabled / captured 后成立。
4. Hotspot TopN 必须能解释 `GetBufferRW`、`GetComponentDataRW`、`OwnerLocalGameplayFactBuffer`、`GEEffectCommandStreamComponent` 等高热来源。
5. 达不到预算时，报告必须输出 failure mask 和下一轮 owner，不得只写功能失败。
