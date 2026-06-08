# Debugger DerivedExportSink Run2

> 日期：2026-06-08
> 范围：`Assets/GAS/Runtime/Debugger`、`Assets/AutoChessDemo/Battle/Validation`、Debugger 事实 / 目标态 / 任务树 / 当前窗口文档

## 代码变更

1. 新增 `GasRuntimeDerivedExportSink`，把 `runtimeDataOrientedScorecard` 派生文本导出从 `GasRuntimeDebugger.cs` 单体拆出。
2. `GasRuntimeDebugger.ExportToText(snapshot, scorecard, maxEvents)` 与 `ExportDataOrientedScorecardToText(scorecard)` 保留 public facade，但委托给 `GasRuntimeDerivedExportSink`。
3. `GasRuntimeDataOrientedScorecard` 新增 `GasRuntimeDiagnosticsMetricFamilyMask`、`GasRuntimeDataOrientedDominantRisk`、`MetricFamilyMask` 与 `DominantRisk`。
4. Runtime text export 与 AutoChess headless budget summary 输出 `metricFamilyMask=0x...` 与 `dominantRisk=...`。

## 架构结论

本轮是 Debugger 数据导向瘦身第二刀：`DerivedExportSink` owner 已开始物理落地，scorecard 也开始具备 machine-readable task routing 字段。该结果只说明 derived export 不再继续膨胀 `GasRuntimeDebugger.cs`，不能推出 Debugger 架构已完成。

仍未完成：

1. `GASRuntimeDiagnosticEventBuffer` 仍是稀疏 mega-row。
2. `RuntimeMetricSink`、`DiagnosticMaterializationPass`、`OfficialCorrelationPass` 仍未物理拆出。
3. `MetricFamilyMask` / `DominantRisk` 仍由 scorecard 派生，不是底层 metric family buffers 或 SoA snapshot。
4. 本轮没有重跑 Unity headless AutoChess、x100 / x1000、Profiler enabled 或 Unity Test Runner。

## 验证

1. `dotnet build com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`：通过，0 error；仍有既有 `MSB3277` warning。
2. `dotnet build com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`：通过，0 error；仍有既有 `MSB3277` warning。
3. `.\.aibridge\cli\AIBridgeCLI.exe compile unity`：返回 `status=timeout`、`statusConfirmed=false`、`errorCount=0`、`warningCount=0`；不能写成 Unity compile 通过。
4. 前一次并行构建出现 `CS2012` 文件占用，原因是 runtime 与 AutoChess demo 同时写 `obj\Debug\com.exhard.exgas.runtime.dll`；后续验证需顺序执行相关 `dotnet build`。

## 下一刀

1. 拆 `GASRuntimeDiagnosticEventBuffer` 为 metric family buffers 或等价 SoA snapshot。
2. 把 workload、GAS concept、data shape、API health、timing、overhead 从 scorecard 分类推进到底层 typed evidence。
3. 把 diagnostic materialization 的 query / `ToEntityArray` / TopN 成本独立到 `DiagnosticMaterializationPass`。
4. 把 Profiler / Entities Journaling 读取独立到 `OfficialCorrelationPass`，并在 performance pass 中默认关闭。
