# 2026-06-08 Debugger Data-Oriented Scorecard Run1

## 范围

本归档记录 Debugger 数据导向性能指标第一轮代码切片。范围只包含 GAS Runtime Debugger、AutoChess validation / runner 消费面和对应事实 / 任务 / 当前窗口文档。

## 已完成

1. 新增 `GasRuntimeDataOrientedScorecard` 与 `GasRuntimeDataOrientedScorecardInput`，把 workload-normalized、owner locality、lookup / query / sync、observation pollution 和 timing split 前移到 Runtime Debugger 模块。
2. `AutoChessHeadlessLogicBudgetResult` 改为先创建 Runtime scorecard，再执行 strict pure logic budget 判定。
3. `GasRuntimeDebugger` 新增 `ExportDataOrientedScorecardToText(...)` 与 snapshot + scorecard overload，输出 `runtimeDataOrientedScorecard` 机器可读行。
4. `AutoChessRuntimeRunner` 的日志与 headless report 调用 Runtime Debugger export 输出 scorecard。
5. `00`、`02`、`04` 已回写本轮事实、任务状态和未跑项边界。

## 验证

1. `dotnet build com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`
   - 结果：通过，0 error。
   - 备注：仍有既有 `MSB3277` 引用版本 warning。
2. `dotnet build com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`
   - 结果：通过，0 error。
   - 备注：仍有既有 `MSB3277` 引用版本 warning。
3. `git diff --check` scoped 到本轮文件。
   - 结果：未发现空白错误，仅有 LF / CRLF 提示。
4. 新增 `GasRuntimeDataOrientedScorecard.cs` 与 `.meta` 单独尾随空白扫描。
   - 结果：未命中。

## 未完成

1. 尚未重跑 Unity headless AutoChess x50。
2. 尚未运行 x100 / x1000 scale profile 或 Profiler enabled 场景。
3. `GasRuntimeDebugger.cs` 仍是 diagnostics proof 单体。
4. `GASRuntimeDiagnosticEventBuffer` 仍是稀疏大事件 row，metric family buffer / snapshot 尚未替换。
