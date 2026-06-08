# Debugger RetentionPolicy Run5 归档

> 日期：2026-06-08 | 范围：GAS Runtime Debugger / ISSUE-003 / 00-01 文档回写

## 原问题

`ISSUE-003 Runtime Core Debugger 证据不足` 已经完成 scorecard、derived export、metric family snapshot 和 diagnostic evidence envelope，但 `GasRuntimeDebugger.cs` 仍内联 event retention 删除策略。每个写入点都在 `Append(...)` 后调用同一个私有 `ApplyRetention(...)`，导致 retention budget、first retained sequence、dropped count 和 raw buffer compaction 仍和采样 API / event schema / snapshot / text export 混在一个 4k 行级单体里。

## 本轮解决证据

1. 新增 `Assets/GAS/Runtime/Debugger/GasRuntimeDiagnosticRetentionPolicy.cs`。
2. `GasRuntimeDebugger` 的所有 retention 调用点改为 `GasRuntimeDiagnosticRetentionPolicy.Apply(...)`。
3. `GasRuntimeDebugger.cs` 中的私有 `ApplyRetention(...)` 已删除，retention policy 成为独立 owner。
4. `ISSUE-003` 与 `07-RuntimeCoreDebuggerSpec.md` 已补充 `DiagnosticRetentionPolicy` owner 口径。

## 仍未完成

1. `GASRuntimeDebuggerComponent` 仍同时承载 `Enabled`、capture flags、retention state、threshold config 和所有 runtime numeric counters。
2. `GASRuntimeDiagnosticEventBuffer` 仍是稀疏 mega-row，尚未拆成 typed metric family buffers 或 SoA snapshot。
3. `DiagnosticMaterializationPass`、`RuntimeMetricSink`、`OfficialCorrelationPass` 仍未物理拆出。

## 复发入口

如果后续新 event / metric 写入点重新在 `GasRuntimeDebugger.cs` 内联 retention 删除逻辑，或绕过 `GasRuntimeDiagnosticRetentionPolicy` 直接修改 first retained sequence / dropped count，应重新打开 `ISSUE-003`：retention 是 Debugger policy owner，不是采样 API、业务 counter 或 derived export 的职责。
