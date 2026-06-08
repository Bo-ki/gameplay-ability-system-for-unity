# Debugger DiagnosticEvidenceSnapshot Run4 归档

> 日期：2026-06-08 | 范围：GAS Runtime Debugger / AutoChess validation / 00-01 文档回写

## 原问题

`ISSUE-003 Runtime Core Debugger 证据不足` 的第三刀已经提供 `GasRuntimeMetricFamilySnapshot`，但 AutoChess validation、hotspot、runtime chain、repeat-run 和 presentation bridge 仍容易沿用 `CoreCounters`、`Events`、`Stats`、`FrameBackboneCounters`、`ObservationMaterializationCounters` 或 `MagnitudeSourceCounters` 这些过渡内部结构。当前 GAS DOTS 架构仍处快速迭代期，Debugger 内部 counter、event buffer、materialization pass、official capture owner 和物理存储形态还会继续破坏性重构；如果外部消费面绑定内部字段，后续每次瘦身都会带来重复迁移成本。

## 本轮解决证据

1. 新增 `Assets/GAS/Runtime/Debugger/GasRuntimeDiagnosticEvidenceSnapshot.cs`。
2. `GasRuntimeDiagnosticSnapshot` 新增 `Evidence`，构造 snapshot 时把 raw event、core counter、frame backbone、observation materialization、magnitude source 和 metric family 统一物化成稳定 read model。
3. `DiagnosticEvidenceSnapshot` 对外只承诺语义分组：`Events`、`Workload`、`ActiveEffect`、`ActiveMutation`、`AttributeFact`、`ApiHealth`、`Structural`、`FrameBackbone`、`Observation`、`MagnitudeSource`。
4. `GasRuntimeDerivedExportSink` 新增 `runtimeDiagnosticEvidenceSnapshot|source=GasRuntimeDiagnosticEvidenceSnapshot` 机器可读行，derived export 不再需要外部重新扫描 raw event buffer。
5. AutoChess validation report、hotspot summary、runtime chain gate、repeat-run evidence、presentation bridge 和 README 已改为消费 `RuntimeDiagnostics.Evidence.*`。
6. 当前 `Assets/AutoChessDemo` 已不再直接消费 `RuntimeDiagnostics.CoreCounters`、`RuntimeDiagnostics.FrameBackboneCounters`、`RuntimeDiagnostics.ObservationMaterializationCounters`、`RuntimeDiagnostics.MagnitudeSourceCounters`、`RuntimeDiagnostics.Events` 或 `RuntimeDiagnostics.Stats`。

## 验证记录

1. `dotnet build com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` 通过，0 error；仍有既有 `MSB3277` warning。
2. `dotnet build com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` 通过，0 error；仍有既有 `MSB3277` warning。
3. 本轮未把本地忽略的 `com.exhard.exgas.runtime.csproj` 加入提交；该文件只用于 dotnet 窄验证临时收录新增 Runtime C# 文件。

## 仍未完成

1. `GASRuntimeDiagnosticEventBuffer` 仍是稀疏 mega-row，尚未拆成 typed metric buffers、SoA snapshot 或 generated metric glue。
2. `GasRuntimeDebugger.cs` 仍承载 singleton cache、config、retention、snapshot 采样 API、event schema 和 materialization 逻辑，尚未拆出 `DiagnosticsConfigOwner`、`RuntimeMetricSink`、`DiagnosticMaterializationPass`、`OfficialCorrelationPass`。
3. 本轮未运行 Unity headless AutoChess x50、x100 / x1000 scale profile、Profiler enabled 场景或 Unity Test Runner。

## 复发入口

如果后续 AutoChess、Editor Debugger、CI report 或外部 profiler driver 重新直接读取 raw `GASRuntimeDiagnosticEventBuffer`，或重新绑定 `CoreCounters` / `FrameBackboneCounters` / `ObservationMaterializationCounters` / `MagnitudeSourceCounters` / `Stats` 作为稳定业务验收字段，应重新打开 `ISSUE-003` 并从 R4 / R8 领取：外部必须回到 `GasRuntimeDiagnosticEvidenceSnapshot`、`GasRuntimeMetricFamilySnapshot`、scorecard、hotspot matrix 或 official diff。
