# 05 Boundary Snapshot 与 Magnitude Source Owner 事实

> Owner：00-当前架构事实/架构重划分审查事实 | 状态：当前事实子页 | 拆分来源：../架构重划分审查事实.md | 拆分时间：2026-06-08

承载 Boundary structured snapshot、SourceAttribute / TargetAttribute magnitude source owner、SourceAttributeSnapshotLane 验证补录和剩余风险。

本文件只记录当前代码事实、证据和 DOTS 判定；目标态设计正文回到 `../../01-目标态架构共识/`，任务拆分回到 `../../02-主线任务树/`。

## 2026-06-08 续轮复核：Magnitude Source Evidence 与三 pass validation

本轮代码截面显示，Magnitude Source 风险已经从“人工读 resolver / execution calculation 路径”推进到机器可读 evidence：frame-local counter、Debugger snapshot、text export、AutoChess validation evidence 和静态防回流门均已出现。同时 AutoChess validation runner 已把 performance pass、diagnostic pass 和 official diff pass 拆开。这个事实只说明证据链和 pass 隔离路径建立，不能写成完整 SourceAttribute / TargetAttribute 捕获语义或 Profiler 性能闭环完成。2026-06-08 追加复核后，active effect slot tick 的 SourceAttribute live lookup 已由 `06-ActiveEffectSlotMagnitudeSnapshot事实.md` 记录为 snapshot gather 正向进展，但仍缺 lane-specific attribution、capacity / spill、非零业务样本和规模 profile。

### 事实消费卡

```markdown
来源类型：代码截面复核 / diagnostics 脚本审查 / 04 验证摘要消费
原始证据：
  - `GEEffectCommandStreamComponent` 已有 `MagnitudeSource*` frame-local counter 字段和 `AddMagnitudeSourceCounters(...)`
  - `EffectMagnitudeResolver` 已记录 current value lookup、captured hit、capture miss、capture miss live lookup、fallback value / fact、source / target attribute lookup
  - `GEExecutionCalculationSystem` 已用 `ExecutionMagnitudeSourceChunkCounters` / `NativeArray` 汇总 execution input lookup 和 attribute lookup，再在 deterministic merge job 写回 stream
  - `GasRuntimeDebugger` 已有 `GasRuntimeMagnitudeSourceCounters`、`MagnitudeSource` diagnostic event、`runtimeMagnitudeSource` / `runtimeCoreMagnitudeSource` text export
  - `DiagnosticsSnapshotSystem` 每帧采样 `RecordMagnitudeSourceEvidence(...)`
  - `AutoChessBattleValidationReport` 代码路径已消费 diagnostic result 的 `RuntimeDiagnostics.MagnitudeSourceCounters`
  - `AutoChessBattleValidationRun` 已拆 `performanceResult` / `diagnosticResult` / `officialDiffResult`；performance 与 official diff pass 关闭 Debugger、system timings 和 buffer pressure，diagnostic pass 打开这些观测
  - `Verify-GAS-RuntimeCoreBoundary.ps1` 已加入 Magnitude Source evidence 与三 pass 分离静态门
  - `_归档/2026-06-08-AutoChessBattleValidation-PassSplitMagnitudeSource-Run1.log`: `passed=True` / `runtimeChainPassed=True` / `repeatRunPassed=True`，performance summary 为 `performancePassObservationPollutionRisks=0`，diagnostic Debugger summary 仍有 `observationMaterializedQueries=12`，`runtimeCoreMagnitudeSource` / `runtimeMagnitudeSource` 导出字段全为 0
第一 owner：00
长期有效：待复核；counter 字段和 pass 形态会随 R3/R4/R5/R8 执行变化
需要反哺：01 Debugger evidence model / 90 不变量，02 R3/R4/R5/R8 领取门，04 最近验证摘要
```

### 正向事实

1. Magnitude Source 相关计数已进入 runtime stream owner：current value lookup、captured value hit、capture miss、capture miss live lookup、fallback value、fallback fact、source attribute lookup、target attribute lookup 和 execution input lookup 都有 frame-local counter。
2. Managed resolver 路径不再只有人工读代码才能定位风险：resolver 在 capture hit / miss / fallback 和 SourceAttribute / TargetAttribute 解析时会写 counter；execution calculation job 也把 chunk 内 input lookup 与 attribute lookup 合并后写回 stream。
3. Debugger evidence 已具备三层消费面：diagnostic event、snapshot counter 和 derived text export。它们能解释 magnitude source 热点来源，但 text export 仍只是派生格式，不是验收源。
4. AutoChess validation report 的机器 evidence 已能消费 diagnostic pass 中的 magnitude source counter，并把 capture miss / live lookup / fallback / execution input lookup 输出到 summary / hotspot 口径。
5. AutoChess validation runner 当前代码路径已经把 performance pass、diagnostic pass 和 official diff pass 分开：performance pass 用于业务和 tick 口径，diagnostic pass 负责 Debugger / timing / buffer pressure 证据，official diff pass 独立捕获并与 performance pass 对比关键业务计数。

### 仍成立风险

1. 当前已有 x50 pass split 原始日志，但没有 AutoChess x100 / x1000、Profiler enabled、Luban 或 SourceGenerator 重跑证据；不能把 counter 存在、静态门通过、build 通过或 x50 跑通写成 DOTS 性能优秀。
2. `PassSplitMagnitudeSource-Run1` 中的 `magnitudeSource*` 字段全为 0，只能证明字段链路可导出且未破坏该业务链路；不能证明真实业务已经覆盖 capture miss、fallback、SourceAttribute / TargetAttribute 和 ExecutionCalculation 热点。
3. Magnitude Source evidence 是风险显性化，不是 snapshot lane 终局。active effect slot tick 已出现 SourceAttribute snapshot gather，但 generated template capacity / spill、snapshot timing key、fallback fact 语义、execution calculation snapshot lane 和 handwritten resolver capture miss 仍需要 R3/R5/R8 后续切片闭合。
4. 三 pass 分离只把 Debugger observation 从 performance pass 中隔离出来；diagnostic pass 中仍允许 observation materialization，且这些成本必须继续归 Debugger / Boundary owner，不能混进 CoreSimulation 性能结论。

### DOTS 判定

| 规则 | 本轮判定 |
|---|---|
| `DBG-01..05` / `ODF-07` | Debugger 已具备 Magnitude Source 机器 evidence 和 pass 隔离字段；后续必须把这些字段并入统一 validation evidence，而不是只看字符串 summary。 |
| `QRY-04` / `PRF-06` / `PRF-19` | counter 能定位 cross-owner / live lookup 风险，但 live lookup 只被显性化，尚未全部替换为 owner-local snapshot record。 |
| `NAT-03` / `BUF-02` / `SEL-02` | frame-local counter 仍挂在迁移期 stream owner 上；它是观测证据，不证明 command/spec/delta/fact carrier 已 scale-ready。 |
| `CASE-12` / `ODF-18` | performance / diagnostic / official diff pass 已有代码分工，但仍缺 Profiler enabled、规模曲线、warmup / measurement 长窗口和官方工具 capture 闭环。 |

### 新任务输入

1. R3：把 current / captured-on-apply / captured-on-tick / captured-before-execution 的 magnitude source timing key、owner 和 fallback fact 收敛为目标态 snapshot record。
2. R4：把 magnitude source counter、observation materialization counter、official diff 和 timing split 纳入同一个 validation evidence model；禁止 derived export 作为机器验收源。
3. R5：SourceGenerator 模板已补 active effect slot tick SourceAttribute snapshot gather，但 generated lifecycle / lookup owner 仍不能写成 pure glue 完成；execution calculation 隐藏 lookup owner 若暂留，必须标记 `MigrationProofOnly` 并绑定退出门。
4. R8：AutoChess x100 / x1000 与 Profiler enabled 场景必须证明 performance pass、diagnostic pass 和 official diff pass 的业务计数可对齐，且 Debugger / Boundary 成本不污染 CoreSimulation 结论。

## 2026-06-07 整体复核：Boundary Snapshot 与 Magnitude Source Owner

本轮继续对 `Assets/GAS/Runtime`、`Assets/GAS/Generated/CodeGen/Runtime`、`Assets/GAS/Editor/CodeGen/Phases`、`Assets/AutoChessDemo` 和 `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` 做静态复核并执行一轮收口。结论是：AutoChess 业务 report snapshot 已向 Boundary structured evidence 收敛，这是 R1/R6 的正向进展；active mutation 的跨 owner `SourceAttribute` 已进入 frame-local snapshot lane。2026-06-08 追加复核后，generated active effect slot tick 的跨 owner `SourceAttribute` 也已进入 owner-aware snapshot gather；但 execution calculation 和 handwritten resolver 仍只是 evidence 显性化，不能把当前状态写成全部 magnitude source capture 完成态。

### 事实消费卡

```markdown
来源类型：整体代码审查 / codedb 截面 / diagnostics 脚本审查
原始证据：
  - `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasBattleUnitSnapshotProjector.cs:8-56`
  - `Assets/AutoChessDemo/Battle/AutoChessBattleSession.cs:64-86`、`:110-125`
  - `Assets/AutoChessDemo/Battle/AutoChessBattleResultBuilder.cs:25-26`
  - `Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationReport.cs:306-320`
  - `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1:87-108`、`:274-308`
  - `Assets/GAS/Runtime/System/Effect/EffectMagnitudeResolver.cs:315-374`、`:378-493`、`:537-549`
  - `Assets/GAS/Runtime/System/Effect/GEExecutionCalculationSystem.cs:209-310`
  - `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs:141-201`、`:372-460`、`:949-1008`、`:2026-2068`
  - `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs:3032-3089`、`:3268-3351`、`:3840-3899`、`:4917-4960`
第一 owner：00
长期有效：待复核；当前代码事实会随后续 R1/R3/R5/R6 执行变化
需要反哺：01 目标态不变量、02 R 任务领取约束、04 当前验证摘要
```

### 正向事实

1. AutoChess unit result snapshot 已从 live ASC read model 转向 structured boundary evidence：`AutoChessBattleSession.CreateUnitResults(...)` 接收 `GasStructuredLogExportSnapshot`，`AutoChessBattleResultBuilder.Build(...)` 传入 `coreObservation.StructuredLog`，`AutoChessGasBattleUnitSnapshotProjector.Project(...)` 用 `TargetReportKey` 匹配 attribute change 并回放 health / energy。`AutoChessBattleValidationReport.CreateBoundaryOwnerSummary(...)` 已输出 `snapshotOwner=AutoChessGasBattleUnitSnapshotProjector.StructuredLog`。2026-06-07 batchmode 归档日志 `_归档/2026-06-07-AutoChessBattleValidation-StructuredSnapshot-Run1.log` 验证该 owner 进入 runner 输出，且 `passed=True` / `runtimeChainPassed=True` / `repeatRunPassed=True` / `blockingDebugErrors=0`。
2. diagnostics 脚本已把这条边界收敛做成静态门：禁止 `AutoChessBattleSession` / `AutoChessGasCoreBridge` / `AutoChessGasBattleEntityLifecycle` 继续出现 `ReadCombatAttributes` 或 `TryCaptureASCReadModel`，并要求 result builder 把 structured log 传给 unit results。
3. AutoChess 旧 live-read 关键词扫描当前只在 snapshot projector、session 调用和 validation summary 正向命中；这说明业务 report 层已经不再依赖 session 阶段直接抓取 ASC read model 来生成最终单位结果。
4. generated active mutation 的 `BuildMagnitudeContext(...)` 已改为 `TryReadSourceAttributeValue(...)`：同 owner SourceAttribute 继续读取当前 owner chunk attribute；跨 owner SourceAttribute 只读取 `ActiveMutationSourceAttributeSnapshots`。snapshot 由 `GEActiveEffectMutationGatherJob : IJob` 通过 `[ReadOnly] BufferLookup<AttributeValueBuffer>` 在 apply 前构建，`GEActiveEffectMutationChunkApplyJob : IJobChunk` 不再打开 `AttributeValueBuffer` lookup alias。
5. diagnostics 脚本已加入 SourceAttribute snapshot 防回流门：要求 generated 输出与 CodeGen 模板同时存在 `BuildActiveMutationSourceAttributeSnapshots`，要求 gather job 拥有 read-only `AttributeLookup`，要求 chunk apply 消费 `NativeParallelHashMap<long, float> ActiveMutationSourceAttributeSnapshots`，并禁止重新生成 `TryReadAttributeValue(ref ownerResources, command.SourceAsc, ...)`。

### 验证补录：SourceAttributeSnapshotLane Run1

`00-当前架构事实/_归档/2026-06-07-AutoChessBattleValidation-SourceAttributeSnapshotLane-Run1.log` 已补入 active mutation SourceAttribute snapshot lane 的 x50 validation 证据。关键字段为：`completed=True`、`passed=True`、`runtimeChainPassed=True`、`repeatRunPassed=True`、`activeMutationCommands=200`、`activeMutationOwnerGroups=200`、`activeMutationEstimatedRandomLookups=0`、`activeMutationOwnerResourceLookups=0`、`activeMutationMigrationCarriers=0`、`streamCarrierPressureWarnings=34`、`summaryHash=0x6681A05F`。

该日志只能证明 active mutation apply lane 在该规模下已通过 SourceAttribute snapshot map 消费并把旧 random lookup / owner resource lookup / migration carrier counter 压到 0；不能证明 `EffectMagnitudeResolver` capture miss 路径、`GEExecutionCalculationSystem` execution input、active effect slot tick snapshot gather 的非零业务覆盖、pre-tick magnitude source 或 Profiler 性能闭环已完成。日志仍显示 `profilerCaptureState=profiler disabled; Entities profiler modules collect no data`，因此不能写成 DOTS 优秀水平证明。

### 验证补录：PassSplitMagnitudeSource Run1

`00-当前架构事实/_归档/2026-06-08-AutoChessBattleValidation-PassSplitMagnitudeSource-Run1.log` 已补入 performance / diagnostic / official diff pass 拆分和 Magnitude Source evidence 贯通后的 x50 validation 证据。关键字段为：`completed=True`、`passed=True`、`thresholdsPassed=True`、`runtimeChainPassed=True`、`repeatRunPassed=True`、`blockingDebugErrors=0`、`performancePassObservationPollutionRisks=0`、`observationMaterializedQueries=12`、`observationMaterializedEntities=2400`、`observationMaterializationUs=57`、`magnitudeSourceCurrentValueLookups=0`、`magnitudeSourceCaptureMisses=0`、`magnitudeSourceFallbackValues=0`、`magnitudeSourceExecutionInputLookups=0`、`factsHash=0xA4A93C35`、`summaryHash=0x0D2FDB38`。

该日志证明 pass-split 后的 performance evidence 不再被 Debugger observation materialization 污染，并证明 `runtimeCoreMagnitudeSource` / `runtimeMagnitudeSource` export 可读；但它没有触发真实 magnitude source counter，因此不能写成 SourceAttribute / TargetAttribute / ExecutionCalculation 语义覆盖完成。`debuggerOwnerAvgMs=56.546` 来自最终 diagnostics export / Debugger snapshot 物化，不是 tick hot path；日志仍显示 `profiler disabled; Entities profiler modules collect no data`，因此也不能写成 Profiler 性能闭环。

### 剩余风险

1. `EffectMagnitudeResolver.ResolveAttributeCapture(...)` 仍支持在 capture miss 时通过 `EntityManager` 读取 `SourceAsc` / `TargetAsc` 的 `AttributeValueBuffer`，再写回 `GEAttributeCaptureValueBuffer`。其中 `StoreCapturedAttributeValue(EntityManager, ref EntityCommandBuffer, ...)` overload 实际没有使用 `ecb`，仍通过 `em.GetBuffer<GEAttributeCaptureValueBuffer>(ge).Add(...)` 直接写 buffer；该 overload 名称不能作为 deferred structural / playback 证明。
2. `GEExecutionCalculationSystem` 的 `ResolveInput(...)` 对 `SourceAttribute` / `TargetAttribute` 走 `ResolveAttributeCapture(...)`，`CurrentValue` 时通过 `AttributeLookup[asc]` 读取 live attribute。它是 job 内 lookup 读路径，不是 owner-local magnitude snapshot lane。
3. generated active effect slot tick 的旧 `BuildMagnitudeContextFromSlot(...) -> TryReadAttributeValue(ref ActiveEffectOwnerResources, slot.SourceAsc, ...)` 跨 owner live lookup 已被 2026-06-08 追加切片替换为 `GEActiveEffectPreTickSourceAttributeSnapshotGatherJob` + `ActiveEffectSlotSourceAttributeSnapshotKey`。最新事实见 [06 ActiveEffectSlot Magnitude Snapshot](06-ActiveEffectSlotMagnitudeSnapshot事实.md)。剩余风险转为 generated lifecycle owner、capacity / spill evidence、非零业务覆盖和 execution calculation / handwritten resolver snapshot lane。
4. `EffectMagnitudeResolver.EnqueueMagnitudeFact(...)` 仍通过 `EffectCommandSpecStream.TryGetSingleton(...)` 与 `BeginGameplayEventWriter(...)` 把 execution calculation missing fact 写入 singleton fact carrier。这是 telemetry / boundary observation 的迁移路径，不是 Core reaction fact 的 scale-ready owner-local fact lane。

### DOTS 判定

| 规则 | 本轮判定 |
|---|---|
| `SYS-05` / `ODF-09` | AutoChess report snapshot 从 structured boundary evidence 派生是正向进展；它把业务 report 与 live Core read 隔开，有利于后续 headless / scene / official diff 共用同一 evidence model。 |
| `QRY-04` / `PRF-06` / `PRF-19` | active mutation apply 与 generated active effect slot tick 不再直接做跨 owner SourceAttribute live lookup；但 handwritten resolver、execution calculation job 仍存在 evidence-only live lookup / direct buffer capture 路径。 |
| `BUF-02` / `STORE-03` / `NAT-03` / `SEL-02` | Missing execution fact、effect command、spec、delta、mutation、fact 当前仍经过 singleton stream carrier；可以作为 MigrationProofOnly / boundary observation proof，但不能作为 scale-ready fan-in。 |
| `BLOB-01` / `BUR-01` / `ODF-05` | active mutation 与 active effect slot tick 的 SourceAttribute snapshot 修复都已回到 CodeGen 模板并重新生成；但 generated lifecycle owner 仍属 R5 / SourceGenerator pure glue 收权范围，不能写成 release-ready 完成。 |

### 新任务输入

1. R1/R6：继续把 AutoChess report / unit result / validation summary 的长期验收建立在 structured evidence、report key 和 snapshot owner 上；禁止恢复 `ReadCombatAttributes` / live ASC read model 来构造业务结果。
2. R2/R3：继续新增 pre-tick / execution Source / Target attribute magnitude snapshot lane 切片。目标是把 current/pre-capture/post-capture 的 source value 明确放入 command/spec/magnitude snapshot record 或 owner-local range，避免 resolver / generated tick 中随手读其他 owner。
3. R5：SourceGenerator 模板已经区分 active mutation apply lane 与 active effect slot tick lane 的 SourceAttribute snapshot 策略；后续必须继续区分 execution calculation lane 的 source attribute 策略。generated artifact 不能继续生成隐藏 random lookup owner，除非显式 `MigrationProofOnly` 并绑定退出门。
4. R4：Debugger evidence 必须输出 magnitude source snapshot miss / fallback / cross-owner lookup counter；否则 AutoChess 跑通无法证明 SourceAttribute lane 已达 DOTS owner-local 要求。
