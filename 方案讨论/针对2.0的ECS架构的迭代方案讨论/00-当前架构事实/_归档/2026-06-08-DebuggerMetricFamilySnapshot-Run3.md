# Debugger MetricFamilySnapshot Run3 归档

> 日期：2026-06-08 | 范围：GAS Runtime Debugger / AutoChess validation summary / 00-01-02-04 文档回写

## 原问题

`ISSUE-003 Runtime Core Debugger 证据不足` 的第二刀已把 derived export sink 从 `GasRuntimeDebugger.cs` 单体拆出，但外部 scorecard / AutoChess summary 仍直接依赖 raw counters 语义拼装。项目仍处快速迭代期，Debugger 内部 counter、event schema 和 materialization owner 后续会继续大改；如果外部报告直接绑定内部字段，后续迁移成本会持续放大。

## 本轮解决证据

1. 新增 `Assets/GAS/Runtime/Debugger/GasRuntimeMetricFamilySnapshot.cs`。
2. `GasRuntimeDiagnosticSnapshot` 新增 `MetricFamilies`，并在构造时统一从 core、frame backbone、observation materialization 和 magnitude source counters 生成 family snapshot。
3. magnitude source family 口径用 core counter 与 diagnostic event counter 取最大值，避免 performance pass 关闭 raw event 时丢 evidence，也避免 diagnostic pass 双计。
4. `GasRuntimeDataOrientedScorecard` 改为消费 `GasRuntimeMetricFamilySnapshot`，不再直接从 raw core counters / observation counters 组装 data-oriented 字段。
5. `GasRuntimeDerivedExportSink` 新增 `runtimeMetricFamilySnapshot|source=GasRuntimeMetricFamilySnapshot` 机器可读行，覆盖 workload、GAS concept、data shape、API health、structural 和 overhead 主要指标。
6. `AutoChessBattleValidationReport.CreateHeadlessLogicBudgetSummary(...)` 新增 `metricFamilySource=GasRuntimeMetricFamilySnapshot`，明确 AutoChess 预算摘要消费 Runtime family snapshot 合约。
7. `GasRuntimeDiagnosticsMetricFamilyMask` 新增 `Structural`，避免 structural evidence 被塞进 timing / overhead 混合口径。

## 验证记录

1. `git diff --check` 对本轮触达代码和文档无 whitespace error，仅有 LF/CRLF warning。
2. `dotnet build com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` 通过，0 error；仍有既有 `MSB3277` warning。
3. `dotnet build com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` 通过，0 error；仍有既有 `MSB3277` warning。
4. `.aibridge/cli/AIBridgeCLI.exe compile unity` 返回 `status=timeout` / `statusConfirmed=false` / `errorCount=0` / `warningCount=0`，不能写成 Unity compile 通过。

## 仍未完成

1. `GASRuntimeDiagnosticEventBuffer` 仍是稀疏 mega-row，尚未拆成 typed metric family buffers 或等价 SoA storage。
2. `GasRuntimeDebugger.cs` 仍承载 config、singleton cache、snapshot、retention、采样 API 和 materialization 逻辑，尚未拆出 `DiagnosticsConfigOwner`、`RuntimeMetricSink`、`DiagnosticMaterializationPass`、`OfficialCorrelationPass`。
3. 本轮未运行 Unity headless AutoChess x50、x100 / x1000 scale profile、Profiler enabled 场景或 Unity Test Runner。

## 复发入口

如果后续 AutoChess、Editor Debugger、CI report 或外部 profiler driver 重新直接消费 `GASRuntimeDiagnosticEventBuffer` 内部字段或 `GasRuntimeCoreDiagnosticCounters` raw fields，应重新打开 `ISSUE-003` 并从 R4 / R8 领取：外部稳定面必须回到 `GasRuntimeMetricFamilySnapshot`、scorecard、hotspot matrix 或 official diff。
