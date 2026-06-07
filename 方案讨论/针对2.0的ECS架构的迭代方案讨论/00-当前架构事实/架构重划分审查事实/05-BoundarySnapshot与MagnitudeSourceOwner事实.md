# 05 Boundary Snapshot 与 Magnitude Source Owner 事实

> Owner：00-当前架构事实/架构重划分审查事实 | 状态：当前事实子页 | 拆分来源：../架构重划分审查事实.md | 拆分时间：2026-06-08

承载 Boundary structured snapshot、SourceAttribute / TargetAttribute magnitude source owner、SourceAttributeSnapshotLane 验证补录和剩余风险。

本文件只记录当前代码事实、证据和 DOTS 判定；目标态设计正文回到 ../../01-目标态架构共识/，任务拆分回到 ../../02-主线任务树/。

## 2026-06-07 整体复核：Boundary Snapshot 与 Magnitude Source Owner

本轮继续对 `Assets/GAS/Runtime`、`Assets/GAS/Generated/CodeGen/Runtime`、`Assets/GAS/Editor/CodeGen/Phases`、`Assets/AutoChessDemo` 和 `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` 做静态复核并执行一轮收口。结论是：AutoChess 业务 report snapshot 已向 Boundary structured evidence 收敛，这是 R1/R6 的正向进展；active mutation 的跨 owner `SourceAttribute` 已进入 frame-local snapshot lane；但 Runtime pre-tick / execution calculation 仍存在 source attribute live lookup / snapshot lane 缺口，不能把当前状态写成全部 magnitude source capture 完成态。

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

该日志只能证明 active mutation apply lane 在该规模下已通过 SourceAttribute snapshot map 消费并把旧 random lookup / owner resource lookup / migration carrier counter 压到 0；不能证明 `EffectMagnitudeResolver` capture miss 路径、`GEExecutionCalculationSystem` execution input、generated active effect slot tick、pre-tick magnitude source 或 Profiler 性能闭环已完成。日志仍显示 `profilerCaptureState=profiler disabled; Entities profiler modules collect no data`，因此不能写成 DOTS 优秀水平证明。

### 剩余风险

1. `EffectMagnitudeResolver.ResolveAttributeCapture(...)` 仍支持在 capture miss 时通过 `EntityManager` 读取 `SourceAsc` / `TargetAsc` 的 `AttributeValueBuffer`，再写回 `GEAttributeCaptureValueBuffer`。其中 `StoreCapturedAttributeValue(EntityManager, ref EntityCommandBuffer, ...)` overload 实际没有使用 `ecb`，仍通过 `em.GetBuffer<GEAttributeCaptureValueBuffer>(ge).Add(...)` 直接写 buffer；该 overload 名称不能作为 deferred structural / playback 证明。
2. `GEExecutionCalculationSystem` 的 `ResolveInput(...)` 对 `SourceAttribute` / `TargetAttribute` 走 `ResolveAttributeCapture(...)`，`CurrentValue` 时通过 `AttributeLookup[asc]` 读取 live attribute。它是 job 内 lookup 读路径，不是 owner-local magnitude snapshot lane。
3. generated active effect slot tick 的 `BuildMagnitudeContextFromSlot(...)` 在 `SourceAttribute` 时会调用 `TryReadAttributeValue(ref ActiveEffectOwnerResources, slot.SourceAsc, ...)`；当 `slot.SourceAsc != ownerResources.Owner` 时，它仍通过 `AttributeLookup[owner]` 读取外部 owner attribute。模板 `GasGlueCodeGenPhases.cs` 中保留同一生成逻辑，因此不能手改 `.gen.cs` 规避，后续修复 owner 是 SourceGenerator 模板和目标 lane 设计。
4. `EffectMagnitudeResolver.EnqueueMagnitudeFact(...)` 仍通过 `EffectCommandSpecStream.TryGetSingleton(...)` 与 `BeginGameplayEventWriter(...)` 把 execution calculation missing fact 写入 singleton fact carrier。这是 telemetry / boundary observation 的迁移路径，不是 Core reaction fact 的 scale-ready owner-local fact lane。

### DOTS 判定

| 规则 | 本轮判定 |
|---|---|
| `SYS-05` / `ODF-09` | AutoChess report snapshot 从 structured boundary evidence 派生是正向进展；它把业务 report 与 live Core read 隔开，有利于后续 headless / scene / official diff 共用同一 evidence model。 |
| `QRY-04` / `PRF-06` / `PRF-19` | active mutation apply 不再直接做跨 owner SourceAttribute lookup；但 handwritten resolver、execution calculation job 和 generated active effect slot tick 仍存在不同形态的 live attribute lookup 或 direct buffer capture。 |
| `BUF-02` / `STORE-03` / `NAT-03` / `SEL-02` | Missing execution fact、effect command、spec、delta、mutation、fact 当前仍经过 singleton stream carrier；可以作为 MigrationProofOnly / boundary observation proof，但不能作为 scale-ready fan-in。 |
| `BLOB-01` / `BUR-01` / `ODF-05` | active mutation 修复已回到 CodeGen 模板并重新生成；generated active effect slot tick 的 source attribute lookup 仍来自 CodeGen 模板，后续任务仍必须回到 R5 / SourceGenerator pure glue 与 R3 / magnitude snapshot lane，而不是手工改生成文件。 |

### 新任务输入

1. R1/R6：继续把 AutoChess report / unit result / validation summary 的长期验收建立在 structured evidence、report key 和 snapshot owner 上；禁止恢复 `ReadCombatAttributes` / live ASC read model 来构造业务结果。
2. R2/R3：继续新增 pre-tick / execution Source / Target attribute magnitude snapshot lane 切片。目标是把 current/pre-capture/post-capture 的 source value 明确放入 command/spec/magnitude snapshot record 或 owner-local range，避免 resolver / generated tick 中随手读其他 owner。
3. R5：SourceGenerator 模板已经区分 active mutation apply lane 的 source attribute snapshot；后续必须继续区分 active effect slot tick lane 和 execution calculation lane 的 source attribute 策略。generated artifact 不能继续生成隐藏 random lookup owner，除非显式 `MigrationProofOnly` 并绑定退出门。
4. R4：Debugger evidence 必须输出 magnitude source snapshot miss / fallback / cross-owner lookup counter；否则 AutoChess 跑通无法证明 SourceAttribute lane 已达 DOTS owner-local 要求。
