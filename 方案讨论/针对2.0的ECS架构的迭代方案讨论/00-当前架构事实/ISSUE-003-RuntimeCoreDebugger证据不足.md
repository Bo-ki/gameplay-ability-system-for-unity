# ISSUE-003 Runtime Core Debugger 证据不足

> 最近复核：2026-06-08 | 状态：Active | 严重度：P1

## 当前结论

Debugger 与 official diff 工具已经存在，不再是“没有证据工具”。当前问题已经升级为：Debugger 模块本身还没有成为数据导向的性能证据系统。它能输出 GAS 概念计数、timing、buffer pressure、observation materialization 和 official diff，但 `GasRuntimeDebugger.cs` 当前仍把配置、singleton 解析、采样、snapshot、diagnostic event schema、retention、官方工具差分口径和文本导出塞进同一条大链路。性能优化下一步必须落到 Debugger 模块的架构瘦身：把 hot-path numeric counters、diagnostic materialization、official capture 和 derived export 拆成不同 owner，并把 GAS 概念字段映射到 DOTS 数据维度，而不是继续向单个大事件结构追加字段。由于当前 GAS DOTS 架构仍处快速迭代期，Debugger 内部物理结构允许继续破坏性重构；外部 AutoChess、CI、Editor Debugger 和报告链路只能消费稳定 evidence surface，避免每次内部 counter / buffer / pass owner 调整都产生迁移适配成本。

## 已成立事实

1. `GasRuntimeDebugger` 能创建 runtime snapshot 并导出文本。
2. `GasRuntimeOfficialToolDiff` 使用 `EntitiesJournaling` 统计 create/destroy/add/remove/enable/disable/set/get。
3. `ReplayLogSystem` 与 structured log exporter 能输出 replay/断言文本。
4. AutoChess bridge 能导出 replay、diagnostics、official diff snapshot。
5. AutoChess validation report 已能输出 runtime diagnostics、physical group timing、hotspot summary、official diff summary、dataflow 和 sequence 导出；这些导出是 structured evidence 的消费面，不是机器验收源本身。
6. 2026-06-08 Observation materialization 切片已把 Debugger `ToEntityArray` 成本拆到专用 evidence：`ObservationMaterialization` diagnostic event、`runtimeObservationMaterialization` text export、AutoChess `observationMaterializedQueries` / `observationMaterializationUs` / `performancePassObservationPollutionRisks` 字段均已出现。
7. 2026-06-08 Performance observation isolation 切片已把 AutoChess headless validation 拆成 performance pass、diagnostic pass 和 official diff pass：performance summary / hotspot summary 输出 `performancePassObservationPollutionRisks=0`，diagnostic Debugger summary 仍输出 `observationMaterializedQueries=12` 和 diagnostic 口径的 `performancePassObservationPollutionRisks=12`。
8. 2026-06-08 Magnitude Source 切片已把 `EffectMagnitudeResolver` 与 `GEExecutionCalculationSystem` 的 current value lookup、snapshot hit/miss、capture miss live lookup、fallback value/fact、source/target attribute lookup、execution input lookup 写入 frame-local stream counter，并由 `GasRuntimeDebugger.RecordMagnitudeSourceEvidence(...)` 采样为 `MagnitudeSource` diagnostic event、`GasRuntimeMagnitudeSourceCounters` snapshot 和 `runtimeMagnitudeSource` text export；最新 AutoChess validation report 代码路径已改为消费 `RuntimeDiagnostics.Evidence.MagnitudeSource` 并输出 `magnitudeSourceCaptureMissLiveLookups` / `magnitudeSourceFallbackFacts` 等字段。`PassSplitMagnitudeSource-Run1` x50 日志证明 pass 隔离与导出链路未破坏业务链路，但不是 Profiler enabled、x100/x1000 规模或真实 magnitude 热点覆盖证明。
9. 2026-06-08 `PassSplitMagnitudeSource-Run1` x50 日志显示 `AutoChessDemoValidationRunResult passed=True` / `thresholdsPassed=True` / `runtimeChainPassed=True` / `repeatRunPassed=True`，并导出 `runtimeCoreMagnitudeSource` 与 `runtimeMagnitudeSource`；当前 `magnitudeSource*` 字段全为 0，只能证明 evidence 链路贯通，不能证明真实 SourceAttribute / TargetAttribute / ExecutionCalculation 业务覆盖。
10. 2026-06-08 `TagRequirementQuery-Run5` x50 日志显示 active effect slot pre-tick SourceAttribute snapshot gather / explicit remove lane counter 修复、TagRequirement catalog/evaluator 贯通和 instant GE requirement gate 修复后，AutoChess validation 仍满足 `passed=True` / `thresholdsPassed=True` / `runtimeChainPassed=True` / `repeatRunPassed=True` / `blockingDebugErrors=0`，且未命中 `Exception`、`SnapshotLaneCounters`、`Use CollectionHelper` 或 `error CS`。早期 Run 日志如果业务字段通过但仍含 Unity job safety 异常，不作为最终干净证据。Run5 仍显示 `profiler disabled; Entities profiler modules collect no data`，`magnitudeSource*` 字段仍为 0，因此只能作为本切片业务链路和 safety 回归证据，不能作为 Debugger / Profiler 性能闭环或真实 magnitude source 热点覆盖证明。
11. 2026-06-08 Debugger 模块复审确认 `GasRuntimeDebugger.cs` 已膨胀为约 4k 行级单体：文件同时声明 `GASRuntimeDebuggerComponent`、`GASRuntimeDiagnosticEventBuffer`、`GasRuntimeCoreDiagnosticCounters`、`GasRuntimeFrameBackboneDiagnosticCounters`、snapshot、singleton cache、采样 API、retention、text export 和多个私有读取器。这是当前架构事实，不是目标态形态。
12. `GASRuntimeDebuggerComponent` 与 `GASRuntimeDiagnosticEventBuffer` 当前重复保存大量同名 metric family；event buffer 是稀疏 mega-row：无论记录的是 `SystemTiming`、`BufferPressure`、`MagnitudeSource` 还是 `ObservationMaterialization`，结构体都携带同一批无关字段。该形态有利于快速拼接验证，但不符合 DOTS 数据导向：字段增长会放大 buffer 写入带宽、cache footprint 和维护成本。
13. `DiagnosticsSnapshotSystem` 位于 `GASBoundaryProjectionSystemGroup`，每帧依次调用 `CollectAndRecordRuntimeCoreCounters`、`RecordEffectCommandSpecStreamPressure`、`RecordMagnitudeSourceEvidence` 和 `RecordCurrentRuntimeCoreFrameBackboneEvidence`。这说明 Debugger 当前是 Boundary diagnostic pass owner，不是 CoreSimulation hot path owner；任何 performance pass 都必须显式关闭或隔离该链路。
14. `CollectRuntimeCoreCounters` 当前在一个方法内混合 `CalculateEntityCount()`、buffer length 读取、`BoundaryObservationFactBuffer` 遍历、`GEEffectCommandStreamComponent` counter 读取、active effect global index 遍历和 active effect store `ToEntityArray` materialization。按照本地 DOTS 规则，`CalculateEntityCount()` / `ToEntityArray()` 在 enableable/filter 场景可能触发同步等待，因此它们只能属于 diagnostic materialization evidence，不能进入 strict performance pass。
15. `GasRuntimeOfficialToolDiff` 正确地把 Entities Journaling / Profiler category state 放到 official-diff separate pass；但实现使用 `Dictionary<string,int>`、字符串 TopN 和 batchmode 下 reflection 清理 Journaling state。该工具适合作为官方差分证据，不适合作为常态低开销 runtime counter。
16. AutoChess strict headless budget 已开始消费数据导向字段：units、measured ticks、commands/facts per tick、us per unit/command/fact、owner group/range、lookup/query/sync/materialization budget。但这些字段目前由 AutoChess report 从 `RuntimeDiagnostics` 和 timing summary 二次组装，Debugger 模块本身还没有提供统一的 `DataOrientedScorecard` / hotspot attribution matrix。
17. 2026-06-08 第一轮 Debugger 改造已新增 `Assets/GAS/Runtime/Debugger/GasRuntimeDataOrientedScorecard.cs`，把 workload-normalized、owner locality、lookup/query/sync、observation pollution 和 timing split 汇总为 Runtime-owned `GasRuntimeDataOrientedScorecard`。`AutoChessHeadlessLogicBudgetResult.Evaluate(...)` 当前改为先创建 scorecard，再做预算阈值判定；`AutoChessDemoHeadlessLogicBudget` summary 输出 `scorecardSource=GasRuntimeDataOrientedScorecard`。
18. 2026-06-08 第二刀已在 `GasRuntimeDebugger` 增加 `ExportDataOrientedScorecardToText(...)` 与 snapshot + scorecard overload，输出机器可读 `runtimeDataOrientedScorecard` 行；`AutoChessRuntimeRunner` 的日志和 headless report 已改为调用 Runtime Debugger export，而不是只依赖 AutoChess report 自己拼接。该结果仍不是完整 Debugger 瘦身完成，只是把性能预算证据 owner 和 derived export owner 从 AutoChess validation 进一步前移到 GAS Runtime Debugger 模块。
19. 2026-06-08 第二轮 Debugger 瘦身已新增 `Assets/GAS/Runtime/Debugger/GasRuntimeDerivedExportSink.cs`，把 `runtimeDataOrientedScorecard` 文本派生导出从 4k 行级 `GasRuntimeDebugger.cs` 物理拆出；`GasRuntimeDebugger` 现在只保留 public facade overload，并委托给 `GasRuntimeDerivedExportSink`。这说明 `DerivedExportSink` owner 已开始落地，但 snapshot 读取、event schema、retention、materialization 和 official diff 仍未拆出。
20. `GasRuntimeDataOrientedScorecard` 当前已新增 `MetricFamilyMask` 与 `DominantRisk`，AutoChess headless budget summary 和 Runtime text export 均输出 `metricFamilyMask=0x...` / `dominantRisk=...`。该字段能把 workload、GAS concept、data shape、API health、timing、overhead 聚合成机器可读风险分类；但它仍只是 scorecard 派生分类，不等于 `GASRuntimeDiagnosticEventBuffer` 已拆为 metric family buffers 或 SoA snapshot。
21. 2026-06-08 第三刀已新增 `Assets/GAS/Runtime/Debugger/GasRuntimeMetricFamilySnapshot.cs`：`GasRuntimeDiagnosticSnapshot` 现在在构造时生成 `MetricFamilies`，把 workload、GAS concept、data shape、API health、structural、overhead 分为只读 family snapshot；magnitude source family 口径会用 core counter 与 diagnostic event counter 取最大值，避免 performance pass 关闭 raw event 时丢 evidence，也避免 diagnostic pass 双计；`GasRuntimeDataOrientedScorecard` 改为消费该 snapshot，而不是直接读取 raw core counters / observation counters；`GasRuntimeDerivedExportSink` 新增机器可读 `runtimeMetricFamilySnapshot|source=GasRuntimeMetricFamilySnapshot` 行；AutoChess headless budget summary 新增 `metricFamilySource=GasRuntimeMetricFamilySnapshot`。这说明外部 evidence 面已经从易变内部 counter 解耦出第一层稳定合约，但 event buffer 物理形态仍是 mega-row，`DiagnosticMaterializationPass` / `RuntimeMetricSink` / `OfficialCorrelationPass` 也仍未拆出。
22. 2026-06-08 第四刀已新增 `Assets/GAS/Runtime/Debugger/GasRuntimeDiagnosticEvidenceSnapshot.cs`：`GasRuntimeDiagnosticSnapshot` 现在构造 `Evidence`，把 raw event / core counter / materialization counter 物化成 `Events`、`Workload`、`ActiveEffect`、`ActiveMutation`、`AttributeFact`、`ApiHealth`、`Structural`、`FrameBackbone`、`Observation`、`MagnitudeSource` 等稳定 read model；`GasRuntimeDerivedExportSink` 新增机器可读 `runtimeDiagnosticEvidenceSnapshot|source=GasRuntimeDiagnosticEvidenceSnapshot` 行；AutoChess validation、hotspot、runtime chain、repeat-run、presentation bridge 和 README 均已改为消费 `RuntimeDiagnostics.Evidence.*`，当前 AutoChess 代码不再直接绑定 `RuntimeDiagnostics.CoreCounters` / `FrameBackboneCounters` / `ObservationMaterializationCounters` / `MagnitudeSourceCounters` / `Events` / `Stats`。这降低了 Debugger 内部继续拆 typed buffer / SoA snapshot / pass owner 时的外部迁移成本，但不代表 `GASRuntimeDiagnosticEventBuffer` 物理拆分已完成。

## 仍成立风险

1. Debugger 自身有 query / `ToEntityArray` 成本；当前 observation materialization 已进入专用 counter 和 AutoChess evidence，但仍不能混入 CoreSimulation hot path。
2. 当前文档不能仅凭“工具存在”宣称结构变化已收口。
3. generated runtime 的 query/dependency/buffer pressure 缺少专项 counters。
4. x50/x1000 profile 与 battle hash 需要与 Debugger snapshot 联动。
5. `GASRuntimeDebuggerEvidenceGateContract` 当前提供的是 contract / plan 计数；`ProfilerMarkerCount`、`JournalingMarkerCount`、cost group split 不能直接消费为 Unity Profiler / Entities Journaling 已 captured。
6. CoreSimulation 的 query materialization、Boundary managed query、Debugger observation query 和 runner sync 必须分别计入 evidence owner；Debugger observation 当前已有 `performancePassObservationPollutionRisks` 信号，否则平均 tick 会掩盖真实热点。
7. 最新 AutoChess x50 日志显示 performance pass 对 Debugger observation materialization 的污染风险已为 0；但 diagnostic pass 仍有 `ToEntityArray` 物化，且同轮日志仍显示 `profiler disabled; Entities profiler modules collect no data` 与 UTP shutdown memory report，不能把这轮写成 Profiler 性能闭环或 Debugger 成本完全消失。
8. Magnitude Source evidence 当前只把 capture miss / fallback / lookup 热点显性化；它不等于 active effect slot tick、pre-tick magnitude source、generated template capacity / spill 或完整 snapshot lane 已终局。
9. Debugger 当前没有把 GAS 概念维度和 DOTS 数据维度建成一张统一矩阵。Ability / GE / Attribute / Tag / Cue 的业务计数已经存在，query / lookup / buffer / sync / structural / owner-local range 的 DOTS 计数也开始出现，但二者还没有统一 evidence id、cost domain、phase/lane、source component、carrier、overhead owner 和 official diff correlation。
10. 如果继续向 `GASRuntimeDiagnosticEventBuffer` 增加字段，短期能输出更多日志，长期会把 Debugger 固化成“日志总线 + 大结构体快照”，无法支撑数据导向性能优化。第三刀 `GasRuntimeMetricFamilySnapshot` 和第四刀 `GasRuntimeDiagnosticEvidenceSnapshot` 已降低外部消费迁移成本，但仍不能替代后续物理 buffer / pass owner 拆分。

## Debugger 模块重构事实结论

当前代码应被视为 **diagnostics proof implementation**，不是 release-ready Debugger architecture。下一轮 Debugger 重构需要按以下事实切分：

| 当前链路 | 当前 owner | 问题 | 结论 |
|---|---|---|---|
| singleton 解析 / cache | `GasRuntimeDebugger` static cache | 与采样、导出混在同一类 | 保留 capability，但迁出到 runtime diagnostics access / bootstrap owner |
| config / retention | `GASRuntimeDebuggerComponent` | 与累计 counter、frame evidence 混在同一 component | 拆成 config、frame state、aggregate summary |
| runtime numeric counter | `GASRuntimeDebuggerComponent` + event buffer + `GasRuntimeMetricFamilySnapshot` + `GasRuntimeDiagnosticEvidenceSnapshot` | 外部已出现 family snapshot / evidence read model 合约，但底层仍由同一大 component / event row 汇聚 | 继续拆成按 metric family 分组的 buffer / snapshot，外部消费只面对稳定 evidence surface |
| diagnostic materialization | `CollectRuntimeCoreCounters` / `ReadActiveEffectStoreCounters` | `CalculateEntityCount`、`ToEntityArray`、buffer 遍历集中在一个 snapshot | 只允许 diagnostic pass，输出 overhead owner |
| official diff | `GasRuntimeOfficialToolDiff` | 字符串 TopN / dictionary / Journaling 开关有开销 | separate pass，不进入 performance pass |
| derived export | `ExportToText` / AutoChess summary / Mermaid | 文本拼接只适合边界导出 | 派生自机器 evidence，不回写验收源 |
| GAS concept counters | command/spec/delta/fact、magnitude、active store | 能说明发生了什么，但不能单独定位为什么慢 | 必须和 DOTS scorecard 连接 |
| DOTS health counters | query、lookup、buffer pressure、sync、materialization | 已分散出现，但缺统一 evidence id | 需要 Debugger data-oriented scorecard |

## 代码证据

| 事实 | 文件 |
|---|---|
| runtime debugger | `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs` |
| data-oriented scorecard | `Assets/GAS/Runtime/Debugger/GasRuntimeDataOrientedScorecard.cs` |
| metric family snapshot | `Assets/GAS/Runtime/Debugger/GasRuntimeMetricFamilySnapshot.cs` |
| diagnostic evidence snapshot | `Assets/GAS/Runtime/Debugger/GasRuntimeDiagnosticEvidenceSnapshot.cs` |
| derived export sink | `Assets/GAS/Runtime/Debugger/GasRuntimeDerivedExportSink.cs` |
| official diff | `Assets/GAS/Runtime/Debugger/GasRuntimeOfficialToolDiff.cs` |
| Debugger boundary system | `Assets/GAS/Runtime/System/Event/DiagnosticsSnapshotSystem.cs` |
| AutoChess observation gateway | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasObservationGateway.cs` |
| AutoChess timing gateway | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeTicker.cs` |
| structured log | `Assets/GAS/Runtime/Event/GasStructuredLogExport.cs`, `GasStructuredLogView.cs` |
| AutoChess export | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasCoreBridge.cs` |
| AutoChess stable evidence consumer | `Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationReport.cs`, `Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationRun.cs`, `Assets/AutoChessDemo/Presentation/AutoChessPresentationOutboxBridge.cs` |
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
7. Debugger hot path 只写固定宽度 numeric metrics / FixedString ids / small counters；禁止托管字符串、Dictionary、反射、文本导出或 `ToEntityArray` 进入 performance pass。
8. Debugger 输出 `DataOrientedScorecard`：workload-normalized cost、chunk locality、lookup pressure、buffer pressure、structural phase、sync/materialization、Burst/managed boundary、debugger overhead，且每项能回连 GAS concept、phase/lane、system/component/buffer 和 official diff source。
9. `GASRuntimeDiagnosticEventBuffer` 稀疏 mega-row 被拆成 metric family buffers 或等价 SoA snapshot；新增 GAS 概念不得继续通过扩展大事件结构体落地。短期外部消费必须走 `GasRuntimeMetricFamilySnapshot` / `GasRuntimeDiagnosticEvidenceSnapshot` / scorecard / derived export，不允许 AutoChess、Editor 或 CI 继续按 Debugger 内部 counter 字段二次拼接稳定语义。
