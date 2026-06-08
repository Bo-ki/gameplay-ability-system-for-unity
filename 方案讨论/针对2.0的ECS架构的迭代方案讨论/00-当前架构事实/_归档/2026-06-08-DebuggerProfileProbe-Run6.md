# Debugger Profile Probe Run6

> 日期：2026-06-08
> 范围：Runtime Debugger / AutoChess headless validation / profile analysis
> 状态：已验证并归档

## 本轮目的

1. 跑一轮真实 Unity batchmode AutoChess 无头实测，验证 Debugger 是否能定位性能热点。
2. 修复 scorecard 证据源误导：performance pass 关闭 Debugger 后不能让 GAS concept / data shape family 在 budget summary 中变成 0。
3. 用数据导向指标反推下一轮 Runtime / Debugger 架构瘦身入口。

## 实现变更

1. `AutoChessHeadlessLogicBudgetResult.Evaluate(...)` 增加 performance / diagnostic 双 pass 输入。
2. scorecard 输入拆成：
   - performance timing：来自 performance pass；
   - GAS concept / data shape / API health / structural family：来自 diagnostic pass；
   - performance observation pollution gate：来自 performance pass overhead。
3. `AutoChessDemoHeadlessLogicBudget` summary 输出 `metricFamilySource=DiagnosticPassGasData+PerformancePassOverhead`。
4. `runtimeDataOrientedScorecard` 派生文本行改为 `passMode=DerivedExport`，并输出 `readModel=PerformanceTiming+MetricFamilySnapshot`。
5. `Tools/Diagnostics/Analyze-AutoChessProfile.ps1` 支持最新 `AutoChessDemoHeadless*` summary、`runtimeDataOrientedScorecard` 机器行、Debugger evidence、TopN、scorecard family 覆盖度和逆推 finding。

## 验证记录

| 验证 | 结果 |
|---|---|
| `Tools/Diagnostics/Analyze-AutoChessProfile.ps1` 解析旧 Run2 | 通过；额外识别 `GAS-MEASURE-03`：scorecard coreFacts 为 0 但 debugger facts 为 5200 |
| `dotnet build .\com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` | 通过；仍有既有 `MSB3277` warning |
| `dotnet build .\com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` | 通过；仍有既有 `MSB3277` warning |
| Unity batchmode AutoChess Run3 | exit code 0，报告写入 `TestResults/AutoChess/Headless/AutoChessHeadlessValidation-DebuggerProbe-20260608-Run3.txt` |
| Run3 profile analysis | 写入 `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run3/AutoChessProfileAnalysis.json` 与 `.md` |

## Run3 关键结果

| 指标 | 值 | 判定 |
|---|---:|---|
| `passed` | true | 业务链路通过 |
| `headlessLogicBudgetPassed` | true | x50 strict pure logic budget 通过 |
| `performanceExcellentPassed` | false | Profiler evidence disabled |
| `blockingDebugErrors` | 0 | 通过 |
| `avgTickMs` | 0.834ms | 低于 1.500ms strict budget |
| `GASTickTotal.avgMs` | 0.825ms | 低于 1.500ms strict budget |
| `CoreRuntimeOwner.avgMs` | 0.569ms | 低于 1.000ms strict budget |
| `CoreSimulation.avgMs` | 0.479ms | 低于 0.900ms strict budget |
| `BoundaryOwner.avgMs` | 0.218ms | 低于 0.350ms strict budget |
| `DebuggerOwner.avgMs` | 49.094ms | diagnostic-only，不并入 performance pass |
| `metricFamilyMask` | `0x7F` | workload / GAS concept / data shape / API health / structural / timing / overhead 均覆盖 |
| `coreFacts` | 5200 | scorecard 证据源修复 |
| `activeMutationCommands` | 200 | scorecard 证据源修复 |
| `ownerLocalFactMaxOwnerRange` | 9 | fact fan-in 数据形态风险 |
| `performanceObservationPollutionRisks` | 0 | performance pass 未被 observation materialization 污染 |

## Debugger 定位出的热点

| 类别 | 热点 |
|---|---|
| Journaling record | `GetBufferRW=67884`、`GetComponentDataRW=18892`、`EnableComponent=650` |
| System TopN | `GASActiveEffectPreTickSystem=15800`、`OwnerLocalInstantCommandFramePrepareSystem=9000`、`ActiveEffectOwnerLocalMutationFramePrepareSystem=9000`、`GASAttributeModifierDeltaApplySystem=5400`、`ASCCommandBufferResolveSystem=4550` |
| Component TopN | `OwnerLocalGameplayFactBuffer=11250`、`GEEffectCommandStreamComponent=9237`、`AttributeValueBuffer=8250`、`GEEffectCommandBuffer=4650` |
| Execution fan-out | `executionSpecScans=1350`、`executionMatchedEffectSpecs=350`、ratio `3.86:1` |
| Owner-local fact | `ownerLocalFactFlushes=5200`、`ownerLocalFactMaxOwnerRange=9` |
| Debugger overhead | `observationMaterializedQueries=12`、`observationMaterializedEntities=2400`、`DebuggerOwner.avgMs=49.094` |

## 结论

Debugger 当前已经能定位真实业务链路中的性能热点，不再只是日志系统。它能把 GAS 概念计数、DOTS API / data shape / owner-local range、Journaling TopN 和 owner timing 合并成可执行证据。

但当前架构仍有明确缺陷：

1. `OwnerLocalGameplayFactBuffer` 和 active effect pre-tick 仍是最热 buffer RW 路径。
2. enableable toggle 协议仍有每 tick 72.2 次开关记录。
3. execution calculation 仍存在 spec fan-out，matched effect 前需要扫描 3.86 倍 spec。
4. diagnostic materialization / derived export 本身是大开销，必须继续拆成可预算 owner。
5. Profiler evidence disabled，因此不能宣称 DOTS 性能优秀。

## 下一轮建议

优先领取以下切片：

1. `OwnerLocalGameplayFactBuffer` dirty owner / dirty fact span counter + fact reduce/apply lane。
2. `GASActiveEffectPreTickSystem` / instant command prepare 的 buffer RW TopN 拆解到 phase / lane / source component。
3. execution calculation code -> effect spec generated index，降低 scan / matched ratio。
4. `DiagnosticMaterializationPass` 与 `DerivedExportSink` 的预算、采样窗口和外部报告隔离。
5. Profiler enabled validation mode 或 batchmode Profiler disabled 的硬理由记录。
