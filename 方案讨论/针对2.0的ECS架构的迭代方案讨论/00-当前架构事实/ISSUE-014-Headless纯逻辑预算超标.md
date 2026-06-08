# ISSUE-014 Headless 纯逻辑预算超标

> 最近复核：2026-06-08 | 状态：Active | 严重度：P0/P1 交界

## 当前结论

当前 AutoChess x50 无头验证已经能把“业务链路通过”“strict pure logic budget 通过”和“DOTS Profiler-backed performance excellent”拆成三种不同结论。无头场景没有模型、特效、UI、动画、音频和真实 PlayerLoop 表现成本；真实游戏里这些成本会占用大部分帧预算，因此 GAS Runtime 纯逻辑平均耗时必须远低于 16.67ms / 33.33ms 的整帧预算。最新有效基线是 DebuggerProbe Run10c：x50 strict pure logic budget 通过，instant / active mutation prepare 的 owner 稀疏扫描已被 dirty owner index 压下，ActiveEffect pre-tick 的 due lane 仍保持 Run9b 改善；但仍不能写成性能优秀，因为 Profiler evidence disabled，且 Hotspot Matrix 继续把下一轮热点定位到 fact fan-in、active effect pre-tick、singleton stream RW、execution spec scan、dependency wait 和 official profiler gate。

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

最新 DebuggerProbe Run4 / x50 / 200 units / measuredTicks=9 证据显示：

| 指标 | Run4 当前值 | 严格预算 | 判定 |
|---|---:|---:|---|
| `avgTickMs` | 0.926ms | 1.500ms | 通过 |
| `GASTickTotal.avgMs` | 0.913ms | 1.500ms | 通过 |
| `GASTickTotal.maxMs` | 1.408ms | 3.000ms | 通过 |
| `CoreRuntimeOwner.avgMs` | 0.638ms | 1.000ms | 通过 |
| `GASCoreSimulationSystemGroup.avgMs` | 0.508ms | 0.900ms | 通过 |
| `BoundaryOwner.avgMs` | 0.234ms | 0.350ms | 通过 |
| `RunnerOwner.avgMs` | 0.041ms | 0.150ms | 通过 |
| `DebuggerOwner.avgMs` | 169.728ms | 不并入 performance pass | diagnostic-only |
| `performanceExcellentPassed` | false | 必须 profiler evidence enabled | 阻塞 |

DebuggerProbe Run6 / pending marker 尝试是明确反例：它把 `OwnerLocalInstantCommandPendingComponent` / `ActiveEffectMutationPendingComponent` 作为高频 enableable 查询门控，结果 `EnableComponent=37384`、`journalingWorldRecords=505543`、`avgTickMs=12.643ms`、`CoreSimulation.avgMs=11.698ms`，并触发多条 Unity Job safety / aliasing 异常，业务链路 `completed=False`。该结果不能作为优化成功证据，只能作为“高频 enableable marker 不适合当前 owner-local command / mutation 热路径”的反面事实。

最新 DebuggerProbe Run7 / pending marker 退出后 / x50 / 200 units / measuredTicks=9 证据显示：

| 指标 | Run7 当前值 | 严格预算 | 判定 |
|---|---:|---:|---|
| `passed` | true | 业务链路必须通过 | 通过 |
| `avgTickMs` | 0.970ms | 1.500ms | 通过 |
| `GASTickTotal.avgMs` | 0.960ms | 1.500ms | 通过 |
| `GASTickTotal.maxMs` | 1.425ms | 3.000ms | 通过 |
| `CoreRuntimeOwner.avgMs` | 0.597ms | 1.000ms | 通过 |
| `GASCoreSimulationSystemGroup.avgMs` | 0.497ms | 0.900ms | 通过 |
| `BoundaryOwner.avgMs` | 0.293ms | 0.350ms | 通过 |
| `RunnerOwner.avgMs` | 0.070ms | 0.150ms | 通过 |
| `DebuggerOwner.avgMs` | 42.750ms | 不并入 performance pass | diagnostic-only |
| `performanceExcellentPassed` | false | 必须 profiler evidence enabled | 阻塞 |

最新 DebuggerProbe Run8 / frame-lane counters / x50 / 200 units / measuredTicks=9 证据显示：

| 指标 | Run8 当前值 | 严格预算 | 判定 |
|---|---:|---:|---|
| `passed` | true | 业务链路必须通过 | 通过 |
| `avgTickMs` | 1.217ms | 1.500ms | 通过 |
| `GASTickTotal.avgMs` | 1.201ms | 1.500ms | 通过 |
| `GASCoreSimulationSystemGroup.avgMs` | 0.726ms | 0.900ms | 通过 |
| `BoundaryOwner.avgMs` | 0.273ms | 0.350ms | 通过 |
| `DebuggerOwner.avgMs` | 115.012ms | 不并入 performance pass | diagnostic-only |
| `performanceExcellentPassed` | false | 必须 profiler evidence enabled | 阻塞 |
| instant prepare scanned/skipped/dirty owners | 2400 / 1450 / 950 | 必须可对比下降 | 新 R7/R3 基线 |
| active mutation prepare scanned/skipped/dirty owners | 2400 / 1750 / 650 | 必须可对比下降 | 新 R7/R3 基线 |
| active effect pre-tick owners scanned/processed/skipped | 2400 / 950 / 1450 | 必须可对比下降 | 新 R7/R3 基线 |
| active effect pre-tick slots scanned/due/noop | 1200 / 550 / 650 | 必须可对比下降 | 新 R7/R3 基线 |

最新 DebuggerProbe Run9b / ActiveEffect NextTickFrame skip / x50 / 200 units / measuredTicks=9 证据显示：

| 指标 | Run9b 当前值 | 严格预算 | 判定 |
|---|---:|---:|---|
| `passed` | true | 业务链路必须通过 | 通过 |
| `headlessLogicBudgetPassed` | true | strict pure logic budget | 通过 |
| `avgTickMs` | 1.057ms | 1.500ms | 通过 |
| `GASTickTotal.avgMs` | 1.041ms | 1.500ms | 通过 |
| `GASCoreSimulationSystemGroup.avgMs` | 0.571ms | 0.900ms | 通过 |
| `BoundaryOwner.avgMs` | 0.268ms | 0.350ms | 通过 |
| `RunnerOwner.avgMs` | 0.065ms | 0.150ms | 通过 |
| `DebuggerOwner.avgMs` | 179.795ms | 不并入 performance pass | diagnostic-only |
| `performanceExcellentPassed` | false | 必须 profiler evidence enabled | 阻塞 |
| active effect pre-tick owners scanned/processed/skipped | 2400 / 600 / 1800 | 相比 Run8 processed 下降 350、skipped 上升 350 | 通过本切片目标 |
| active effect pre-tick slots scanned/due/noop | 750 / 550 / 200 | 相比 Run8 scanned 下降 450、noop 下降 450 | 通过本切片目标 |

Run9 首跑同一代码输出 `avgTickMs=3.650ms`、`headlessLogicBudgetPassed=False`，但日志显示本次启动包含脚本编译 / domain reload；Run9b 复跑通过并写出同构业务 hash 与 counters。因此 Run9 首跑只能归档为冷启动 / 编译污染样本，不能作为性能回归结论。

最新 DebuggerProbe Run10c / Prepare Dirty Owner Index / x50 / 200 units / measuredTicks=9 证据显示：

| 指标 | Run10c 当前值 | 严格预算 | 判定 |
|---|---:|---:|---|
| `passed` | true | 业务链路必须通过 | 通过 |
| `headlessLogicBudgetPassed` | true | strict pure logic budget | 通过 |
| `repeatRunPassed` | true | 确定性复跑 | 通过 |
| `avgTickMs` | 0.898ms | 1.500ms | 通过 |
| `GASTickTotal.avgMs` | 0.886ms | 1.500ms | 通过 |
| `GASCoreSimulationSystemGroup.avgMs` | 0.494ms | 0.900ms | 通过 |
| `BoundaryOwner.avgMs` | 0.231ms | 0.350ms | 通过 |
| `RunnerOwner.avgMs` | 0.041ms | 0.150ms | 通过 |
| `DebuggerOwner.avgMs` | 121.053ms | 不并入 performance pass | diagnostic-only |
| `performanceExcellentPassed` | false | 必须 profiler evidence enabled | 阻塞 |
| instant prepare scanned/skipped/dirty owners | 950 / 0 / 950 | 相比 Run9b scanned 下降 1450、skipped 归零 | 通过本切片目标 |
| active mutation prepare scanned/skipped/dirty owners | 200 / 0 / 200 | 相比 Run9b scanned 下降 2200、skipped 归零 | 通过本切片目标 |
| active effect pre-tick owners scanned/processed/skipped | 2400 / 600 / 1800 | 保持 Run9b due lane 改善 | 通过 |
| active effect pre-tick slots scanned/due/noop | 750 / 550 / 200 | 保持 Run9b due lane 改善 | 通过 |
| `GetBufferRW` / `GetComponentDataRW` / `EnableComponent` | 48852 / 20204 / 650 | 不允许 EnableComponent 膨胀 | 通过 C2 回归门 |

Run10 首跑写出业务报告但日志包含脚本编译 / domain reload，`avgTickMs=4.369ms`、`headlessLogicBudgetPassed=False`，只能作为冷启动污染样本。Run10b 因项目锁冲突退出，没有生成业务 summary，不能作为验证样本。Run10c 才是本轮有效性能样本。

## 当前代码事实

1. `AutoChessBattleValidationRun` 已新增 `AutoChessHeadlessLogicBudgetResult`，把严格无头逻辑预算接入 `AutoChessValidationRunResult.Passed`。
2. `AutoChessBattleValidationReport.CreateRunResultSummary(...)` 已输出 `headlessLogicBudgetPassed` 与 `performanceExcellentPassed`。
3. `AutoChessBattleValidationReport.CreateHeadlessLogicBudgetSummary(...)` 已输出预算阈值、实测值、failure mask、Profiler evidence 和 observation pollution。
4. `AutoChessRuntimeRunner` 已输出 `AutoChessDemoHeadlessLogicBudget` / `AutoChessDemoHeadlessLogicBudget:` 报告行。
5. 2026-06-08 DebuggerProbe Run3 已把 scorecard source 修正为 `DiagnosticPassGasData+PerformancePassOverhead`：timing / strict budget 仍来自 performance pass，GAS concept / data shape / API health / structural family 来自 diagnostic pass，performance observation pollution gate 仍来自 performance pass overhead。
6. `Tools/Diagnostics/Analyze-AutoChessProfile.ps1` 已能解析 `AutoChessDemoHeadless*` 新 summary、`runtimeDataOrientedScorecard` 机器行、Journaling TopN、Debugger evidence 和 scorecard family 覆盖度，并在旧 Run2 上识别 `GAS-MEASURE-03` 口径缺陷。
7. 2026-06-08 DebuggerProbe Run4 已把 strict budget 报告与 hotspot attribution matrix 合并：报告输出 `AutoChessDemoHeadlessHotspotAttributionMatrix:`，分析脚本输出 Markdown `Hotspot Attribution Matrix` 与 JSON `hotspotAttribution`。该矩阵把 `OwnerLocalGameplayFactBuffer=11250`、`GASActiveEffectPreTickSystem=15800`、`OwnerLocalInstantCommandFramePrepareSystem=9000`、`ActiveEffectOwnerLocalMutationFramePrepareSystem=13000`、`AttributeValueBuffer=8250`、`executionSpecScans=1350` 和 `Profiler disabled` 分派到下一轮 owner。
8. `Analyze-AutoChessProfile.ps1` 现在额外输出 `AutoChessProfileBrief.md` 和 `SubReports/PerformanceBudget.md`、`HotspotAttribution.md`、`JournalingTopN.md`、`DebuggerEvidence.md`、`RuntimeBattleLog.md`。后续性能审查默认先读 brief，只有需要对应领域证据时再读子报告，避免 Agent 为一个性能 owner 读取整份长日志。
9. 2026-06-08 Run7 已移除 `OwnerLocalInstantCommandPendingComponent` / `ActiveEffectMutationPendingComponent` 这条高频 enableable marker 热路径；ASC archetype 回到 38 个 core component，FramePrepare / Normalize / SpecBuild 以 owner-local buffer 作为事实源，并通过 `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` 阻断 pending marker 回流。
10. 2026-06-08 Run8 已接入 frame-lane counters：`OwnerLocalInstantPrepare*`、`ActiveMutationPrepare*`、`ActiveEffectPreTick*` 进入 Runtime Debugger evidence、AutoChess headless report 和 split report。后续 R7/R3 优化必须用这些 counters 证明 scanned owner、skipped owner、dirty owner、due slot、noop slot 或 mutation write 的数据形态变化，不能只用平均耗时波动交还。
11. 2026-06-08 Run9b 已在 `ASCActiveEffectsComponent` / `ActiveEffectChunkSkipIndexSnapshot` 增加 duration due slot 与 `NextTickFrame`，`GASActiveEffectPreTickSystem` 的 source attribute snapshot gather 和 tick job 均通过 `ActiveEffectStore.ShouldProcessTickOwner(...)` 跳过未到期 owner；这属于 owner-level due lane，不引入 Run6 已证伪的高频 enableable marker。
12. 2026-06-08 Run10c 已新增 EffectCommandStream owner 上的 `OwnerLocalInstantPrepareDirtyOwnerBuffer` / `ActiveEffectMutationPrepareDirtyOwnerBuffer`，producer 写入 dirty ASC，FramePrepare 使用 dirty owner index + 去重后再清理 / 晋升 owner-local buffer。该方案没有恢复 Run6 已证伪的高频 enableable marker，并通过 `Verify-GAS-RuntimeCoreBoundary.ps1` 阻断 pending marker 与 singleton spec clear 回流。

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
6. Run10c 虽然 x50 strict budget 通过，且 instant / active mutation prepare 稀疏 owner 扫描相对 Run9b 明确下降，但仍命中 `dependencyWaitRisks=4`、`syncQueryBudget=13`、`ownerLocalFactMaxOwnerRange=9`、`executionSpecScans/executionMatchedEffectSpecs=3.86:1`、`GetBufferRW=48852`、`GetComponentDataRW=20204`、`GAS-ARCH-CMD-PREPARE=4259`、`GAS-ARCH-AE-PRETICK=9100` 和 `Profiler disabled`；因此 ISSUE-014 不关闭，只从“x50 预算超标”升级为“x50 预算通过但规模化 / Profiler / 数据形态未达 DOTS 优秀”。
7. Run7 的 Unity batchmode 首次执行只完成编译刷新，第二次执行才写出有效 summary；后续验证必须以 summary 文件和 `AutoChessDemoRuntimeReport` 为准，不能只看 Unity exit code 0。
8. 高频 enableable marker 会把 dirty lane 成本转移成 `EnableComponent` / Job safety 风险。若后续再尝试 marker 门控，必须证明 toggle 次数按 owner 去重且无 aliasing；否则默认禁止进入 Runtime Core hot path。
9. Unity batchmode 的 exit code 0 不能单独作为通过证据。Run10c 的有效性来自 summary 中 `passed=True` / `headlessLogicBudgetPassed=True` / `repeatRunPassed=True`，而不是进程返回码；Run10b 锁冲突和 Run10 编译污染均必须降级。

## 退出条件

1. AutoChess x50 headless `headlessLogicBudgetPassed=True`。
2. x100 / x1000 至少有同构 summary，并输出 scale blocked reason 或通过结果。
3. `performanceExcellentPassed=True` 只能在 strict budget 通过且 Profiler evidence enabled / captured 后成立。
4. Hotspot TopN 必须能解释 `GetBufferRW`、`GetComponentDataRW`、`OwnerLocalGameplayFactBuffer`、`GEEffectCommandStreamComponent` 等高热来源。
5. 达不到预算时，报告必须输出 failure mask 和下一轮 owner，不得只写功能失败。
6. 即使 strict budget 通过，也必须通过 `hotspotAttribution` 矩阵证明主要 High row 已下降、迁移或被更精确的业务原因解释；否则不能宣称 DOTS 数据形态达标。
7. 每轮 AutoChess profile analysis 必须产出 short brief 和领域子报告；否则不满足 Agent-readable 性能审查门槛。
