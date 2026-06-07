# ISSUE-003 Runtime Core Debugger 证据不足

> 最近复核：2026-06-08 | 状态：Active | 严重度：P1

## 当前结论

Debugger 与 official diff 工具已经存在，不再是“没有证据工具”。当前问题是证据还没有系统性服务于架构退出条件：结构变化相位、buffer pressure、dependency complete、Core/Boundary 成本拆分、battle hash 稳定性仍需要固定报告。

## 已成立事实

1. `GasRuntimeDebugger` 能创建 runtime snapshot 并导出文本。
2. `GasRuntimeOfficialToolDiff` 使用 `EntitiesJournaling` 统计 create/destroy/add/remove/enable/disable/set/get。
3. `ReplayLogSystem` 与 structured log exporter 能输出 replay/断言文本。
4. AutoChess bridge 能导出 replay、diagnostics、official diff snapshot。
5. AutoChess validation report 已能输出 runtime diagnostics、physical group timing、hotspot summary、official diff summary、dataflow 和 sequence 导出；这些导出是 structured evidence 的消费面，不是机器验收源本身。
6. 2026-06-08 Observation materialization 切片已把 Debugger `ToEntityArray` 成本拆到专用 evidence：`ObservationMaterialization` diagnostic event、`runtimeObservationMaterialization` text export、AutoChess `observationMaterializedQueries` / `observationMaterializationUs` / `performancePassObservationPollutionRisks` 字段均已出现。
7. 2026-06-08 Performance observation isolation 切片已把 AutoChess headless validation 拆成 performance pass、diagnostic pass 和 official diff pass：performance summary / hotspot summary 输出 `performancePassObservationPollutionRisks=0`，diagnostic Debugger summary 仍输出 `observationMaterializedQueries=12` 和 diagnostic 口径的 `performancePassObservationPollutionRisks=12`。
8. 2026-06-08 Magnitude Source 切片已把 `EffectMagnitudeResolver` 与 `GEExecutionCalculationSystem` 的 current value lookup、snapshot hit/miss、capture miss live lookup、fallback value/fact、source/target attribute lookup、execution input lookup 写入 frame-local stream counter，并由 `GasRuntimeDebugger.RecordMagnitudeSourceEvidence(...)` 采样为 `MagnitudeSource` diagnostic event、`GasRuntimeMagnitudeSourceCounters` snapshot 和 `runtimeMagnitudeSource` text export；AutoChess validation report 代码路径已消费 `RuntimeDiagnostics.MagnitudeSourceCounters` 并输出 `magnitudeSourceCaptureMissLiveLookups` / `magnitudeSourceFallbackFacts` 等字段。`PassSplitMagnitudeSource-Run1` x50 日志证明 pass 隔离与导出链路未破坏业务链路，但不是 Profiler enabled、x100/x1000 规模或真实 magnitude 热点覆盖证明。
9. 2026-06-08 `PassSplitMagnitudeSource-Run1` x50 日志显示 `AutoChessDemoValidationRunResult passed=True` / `thresholdsPassed=True` / `runtimeChainPassed=True` / `repeatRunPassed=True`，并导出 `runtimeCoreMagnitudeSource` 与 `runtimeMagnitudeSource`；当前 `magnitudeSource*` 字段全为 0，只能证明 evidence 链路贯通，不能证明真实 SourceAttribute / TargetAttribute / ExecutionCalculation 业务覆盖。
10. 2026-06-08 `TagRequirementQuery-Run5` x50 日志显示 active effect slot pre-tick SourceAttribute snapshot gather / explicit remove lane counter 修复、TagRequirement catalog/evaluator 贯通和 instant GE requirement gate 修复后，AutoChess validation 仍满足 `passed=True` / `thresholdsPassed=True` / `runtimeChainPassed=True` / `repeatRunPassed=True` / `blockingDebugErrors=0`，且未命中 `Exception`、`SnapshotLaneCounters`、`Use CollectionHelper` 或 `error CS`。早期 Run 日志如果业务字段通过但仍含 Unity job safety 异常，不作为最终干净证据。Run5 仍显示 `profiler disabled; Entities profiler modules collect no data`，`magnitudeSource*` 字段仍为 0，因此只能作为本切片业务链路和 safety 回归证据，不能作为 Debugger / Profiler 性能闭环或真实 magnitude source 热点覆盖证明。

## 仍成立风险

1. Debugger 自身有 query / `ToEntityArray` 成本；当前 observation materialization 已进入专用 counter 和 AutoChess evidence，但仍不能混入 CoreSimulation hot path。
2. 当前文档不能仅凭“工具存在”宣称结构变化已收口。
3. generated runtime 的 query/dependency/buffer pressure 缺少专项 counters。
4. x50/x1000 profile 与 battle hash 需要与 Debugger snapshot 联动。
5. `GASRuntimeDebuggerEvidenceGateContract` 当前提供的是 contract / plan 计数；`ProfilerMarkerCount`、`JournalingMarkerCount`、cost group split 不能直接消费为 Unity Profiler / Entities Journaling 已 captured。
6. CoreSimulation 的 query materialization、Boundary managed query、Debugger observation query 和 runner sync 必须分别计入 evidence owner；Debugger observation 当前已有 `performancePassObservationPollutionRisks` 信号，否则平均 tick 会掩盖真实热点。
7. 最新 AutoChess x50 日志显示 performance pass 对 Debugger observation materialization 的污染风险已为 0；但 diagnostic pass 仍有 `ToEntityArray` 物化，且同轮日志仍显示 `profiler disabled; Entities profiler modules collect no data` 与 UTP shutdown memory report，不能把这轮写成 Profiler 性能闭环或 Debugger 成本完全消失。
8. Magnitude Source evidence 当前只把 capture miss / fallback / lookup 热点显性化；它不等于 active effect slot tick、pre-tick magnitude source、generated template capacity / spill 或完整 snapshot lane 已终局。

## 代码证据

| 事实 | 文件 |
|---|---|
| runtime debugger | `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs` |
| official diff | `Assets/GAS/Runtime/Debugger/GasRuntimeOfficialToolDiff.cs` |
| structured log | `Assets/GAS/Runtime/Event/GasStructuredLogExport.cs`, `GasStructuredLogView.cs` |
| AutoChess export | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasCoreBridge.cs` |
| observation materialization x50 | `00-当前架构事实/_归档/2026-06-08-AutoChessBattleValidation-ObservationMaterialization-Run1.log` |
| performance observation isolation x50 | `00-当前架构事实/_归档/2026-06-08-AutoChessBattleValidation-PerformanceObservationIsolation-Run1.log` |
| magnitude source evidence | `Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs`, `Assets/GAS/Runtime/System/Effect/EffectMagnitudeResolver.cs`, `Assets/GAS/Runtime/System/Effect/GEExecutionCalculationSystem.cs`, `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs`, `Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationReport.cs` |
| pass split + magnitude source x50 | `00-当前架构事实/_归档/2026-06-08-AutoChessBattleValidation-PassSplitMagnitudeSource-Run1.log` |
| tag requirement / pre-tick snapshot x50 | `00-当前架构事实/_归档/2026-06-08-AutoChessBattleValidation-TagRequirementQuery-Run5.log` |

## 退出条件

1. 每次架构验证报告拆分 CoreSimulation、BoundaryProjection、Demo Integration。
2. Journaling 能列出结构变化来源 system 和 phase。
3. Debugger 输出 buffer pressure、query count、dependency complete、event/fact counts。
4. AutoChess 验证报告包含 battle hash 与 blocking debug error count。
5. Performance pass 不再触发 Debugger observation materialization，validation report 和边界脚本能阻断回流；后续仍需 x100/x1000 与 Profiler enabled 场景证明 Debugger/Boundary 成本不会污染 CoreSimulation 结论。
6. Magnitude Source capture miss / fallback / live lookup 有稳定 event、snapshot、validation evidence 代码路径，并与 active effect slot tick / pre-tick / generated template snapshot lane 的退出门分开验收；后续仍需用真实业务样本覆盖非零热点和规模 profile。
