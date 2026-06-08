# 2026-06-08 Debugger Hotspot Attribution Matrix Run4

> Owner：`00-当前架构事实/_归档`
> 范围：R4 Debugger Hotspot Attribution Matrix
> 结论：AutoChess x50 业务链路和 strict pure logic budget 通过；Profiler-backed performance excellence 仍未通过。

## 验证入口

```powershell
dotnet build .\com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal
dotnet build .\com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal

$unity = 'E:\Unity\UnityEditor\6000.3.14f1\Editor\Unity.exe'
$report = 'TestResults\AutoChess\Headless\AutoChessHeadlessValidation-DebuggerProbe-20260608-Run4-R4Matrix.txt'
$log = 'TestResults\AutoChess\Headless\AutoChessHeadlessValidation-DebuggerProbe-20260608-Run4-R4Matrix.log'
& $unity -batchmode -projectPath . -executeMethod GAS.AutoChessDemo.Editor.AutoChessDemoBatchRunner.RunAutoChessBattleOnceAndExit -autoChessReportPath $report -logFile $log

Tools\Diagnostics\Analyze-AutoChessProfile.ps1 -SummaryPath $report -OutputDirectory TestResults\AutoChess\Analysis\DebuggerProbe-20260608-Run4-R4Matrix
```

## 代码改动

| 路径 | 作用 |
|---|---|
| `Assets/GAS/Runtime/Debugger/GasRuntimeDerivedExportSink.cs` | 在 scorecard 派生导出后输出 Runtime-owned `hotspotAttribution` 行 |
| `Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationReport.cs` | 合并 `JournalingTopN + DiagnosticMetricFamily`，输出 AutoChess validation matrix |
| `Assets/AutoChessDemo/AutoRunner/AutoChessRuntimeRunner.cs` | 日志和 headless report 增加 `AutoChessDemoHeadlessHotspotAttributionMatrix` |
| `Tools/Diagnostics/Analyze-AutoChessProfile.ps1` | 解析 `hotspotAttribution` 行，JSON 输出 `hotspotAttribution`，Markdown 输出 `Hotspot Attribution Matrix` |

## Split Report 输出

离线分析脚本现在同时生成全量报告和 Agent 友好的拆分报告：

| 报告 | 用途 |
|---|---|
| `AutoChessProfileAnalysis.json` | 完整机器结构 |
| `AutoChessProfileAnalysis.md` | 完整人读报告 |
| `AutoChessProfileBrief.md` | 默认 Agent 入口，只读结论、预算快照、Top next owners 和子报告路径 |
| `SubReports/PerformanceBudget.md` | strict budget / timing split / profiler gate |
| `SubReports/HotspotAttribution.md` | `hotspotAttribution` 全量矩阵 |
| `SubReports/JournalingTopN.md` | Journaling record/system/component TopN |
| `SubReports/DebuggerEvidence.md` | Debugger evidence、materialization、finding / next probe |
| `SubReports/RuntimeBattleLog.md` | 中文业务战斗日志 |

## Run4 关键结果

| 字段 | 值 |
|---|---:|
| `passed` | `True` |
| `headlessLogicBudgetPassed` | `True` |
| `performanceExcellentPassed` | `False` |
| `avgTickMs` | `0.926` |
| `GASTickTotal.avgMs` | `0.913` |
| `CoreRuntimeOwner.avgMs` | `0.638` |
| `CoreSimulation.avgMs` | `0.508` |
| `BoundaryOwner.avgMs` | `0.234` |
| `DebuggerOwner.avgMs` | `169.728` diagnostic-only |
| `performanceObservationPollutionRisks` | `0` |
| `profilerEvidencePassed` | `False` |

`profilerCaptureState=profiler disabled; Entities profiler modules collect no data`，因此本轮不能写成 DOTS Profiler-backed 性能优秀。

## Hotspot Attribution Matrix 摘要

| ID | Count | DOTS risk | Next owner |
|---|---:|---|---|
| `R4-DBG-MATRIX` | `87426` | `ManualInference` | R4 |
| `GAS-ARCH-AE-PRETICK` | `15800` | `PerFrameSlotScan` | R7/R3 |
| `GAS-ARCH-AE-MUTATION-PREPARE` | `13000` | `PerFrameBufferClearCopy` | R7/R3 |
| `GAS-ARCH-07` | `11250` | `BroadBufferRW+OwnerLocality` | R3 |
| `GAS-ARCH-STREAM-RW` | `9237` | `SingletonStreamRW` | R4/R3 |
| `GAS-ARCH-CMD-PREPARE` | `9000` | `PerFrameBufferClearCopy` | R7/R3 |
| `GAS-ARCH-ATTR-RW` | `8250` | `BroadBufferRW` | R3 |
| `GAS-ARCH-06` | `1350` | `FanOutScan` | R5/R3 |
| `GAS-DBG-01` | `2412` | `ObservationMaterialization` | R4 |
| `GAS-MEASURE-02` | `1` | `MissingProfilerEvidence` | R8 |

## 下一轮领取

下一轮优先领取 R3：`OwnerLocalGameplayFactBuffer Dirty Span Lane`。理由不是总 ms 最高，而是矩阵同时指向 `OwnerLocalGameplayFactBuffer=11250`、`ownerLocalFactFlushes=5200`、`ownerLocalFactMaxOwnerRange=9` 和 `AttributeValueBuffer=8250`，说明 fact / attribute fan-in 的数据形态仍是 Runtime Core 中最清晰的 DOTS 风险。

并行保留 R7/R3、R5/R3、R4、R8：

1. R7/R3：active effect pre-tick、active mutation prepare、instant command prepare 三个 buffer RW 热点。
2. R5/R3：execution spec scan ratio `3.857` 的 generated index。
3. R4：diagnostic materialization owner 与 DerivedExportSink 预算化。
4. R8：Profiler enabled / x100 / x1000 scale gate。

## 环境日志风险

Unity batchmode exit code 为 0，AutoChess report 成功生成，业务链路通过。但日志中仍出现 licensing access token / HTTP 307、UTP memory leak report 和 `StackAllocator` shutdown report。这些是 Unity / 环境日志风险，不能写成“整轮日志完全干净”。
