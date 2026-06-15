# 06 ActiveEffectSlot Magnitude Snapshot 事实

> Owner：00-当前架构事实/架构重划分审查事实 | 状态：当前事实子页 | 创建时间：2026-06-08

本文件只记录 active effect slot tick 中 SourceAttribute magnitude snapshot lane 的当前代码事实。目标态设计回到 `../../01-目标态架构共识/`；后续任务拆分回到 `../../02-主线任务树/`。

## 2026-06-08 切片：active effect slot SourceAttribute snapshot gather

### 事实消费卡

```markdown
来源类型：代码截面复核 / diagnostics 脚本审查 / AutoChess x50 batchmode 验证
原始证据：
  - `Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs`
  - `Assets/GAS/Runtime/System/Effect/GEActiveEffectLifecycleSystems.cs`
  - `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` 当前仅为 marker / pure glue 对账项
  - `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1`
  - `00-当前架构事实/_归档/2026-06-08-AutoChessBattleValidation-TagRequirementQuery-Run5.log`
第一 owner：00
长期有效：待复核；hand-written active effect Runtime owner、generated marker 和 R5/R3 gate 仍需联动对账
需要反哺：R3 magnitude snapshot lane、R5 SourceGenerator pure glue 收权、R4 Debugger evidence gate
```

### 正向事实

1. 手写 `GASActiveEffectPreTickSystem` 现在会在 `GEActiveEffectPreTickJob` 前调度 `GASActiveEffectRuntime.GEActiveEffectPreTickSourceAttributeSnapshotGatherJob`。
2. 新 gather job 使用 read-only `AttributeValueBuffer` lookup 预采样跨 owner `SourceAttribute`，写入 `NativeParallelHashMap<ActiveEffectSlotSourceAttributeSnapshotKey, float>.ParallelWriter ActiveEffectSlotSourceAttributeSnapshots`。
3. Snapshot key 包含 `Owner + SlotSequence + ModifierIndex`；这是必须项，因为 active effect slot sequence 是 owner-local，不是全局唯一。
4. `GEActiveEffectPreTickJob.BuildMagnitudeContextFromSlot(...)` 不再通过旧 `TryReadAttributeValue(ref ownerResources, slot.SourceAsc, ...)` 做跨 owner live lookup；同 owner 仍走 owner-local attributes，跨 owner 只消费 snapshot map。
5. Snapshot 容量口径由 `ownerCount * ActiveEffectStore.InlineSlotCapacity * maxSourceAttributeModifierCount` 估算，`maxSourceAttributeModifierCount` 来自 catalog 中各 GE 的 SourceAttribute modifier 数。
6. CodeGen 模板和当前 generated 输出已同步，避免只手改 `.gen.cs` 后下一次生成回流。
7. `Verify-GAS-RuntimeCoreBoundary.ps1` 已加入防回流门：要求手写 Runtime owner、generated marker 和模板口径对账，禁止 owner-local-only key，并禁止 slot SourceAttribute 退回 live owner-resource lookup。
8. `GASActiveEffectPreTickSystem` 已把 `GASRuntimeEntityArchetypes.GrantedAbility(em)` 前移到任何 snapshot gather job schedule 之前，避免结构性 archetype 查询/创建在 `state.Dependency` 写回前触发 Unity safety 检查。
9. `GASActiveEffectRemoveSystem` 复用 `GEActiveEffectPreTickJob` 的 explicit remove 分支时会传入 frame-local 空 snapshot map；这是 NativeContainer 调度有效性要求，不代表 remove 路径需要或读取 active effect slot SourceAttribute snapshot。

## 2026-06-08 切片：R4 lane-specific snapshot evidence

### 正向事实

1. `GEEffectCommandStreamComponent` 已增加 active effect slot SourceAttribute snapshot lane counters：capacity、gather attempt、write success、write failure、attribute miss、apply hit、apply miss、fallback、capacity pressure、spill。
2. `GasRuntimeMagnitudeSourceCounters` 和 `GASRuntimeDiagnosticEventBuffer` 已承载同一组 lane counters；`GasRuntimeDebugger.RecordMagnitudeSourceEvidence(...)` 从 stream 读取后写入 `MagnitudeSource` 诊断事件。
3. `GasRuntimeDebugger.ExportToText(...)` 的 `runtimeMagnitudeSource` 汇总行和单条 `MagnitudeSource` event 行已输出 `activeEffectSlotSourceSnapshot*` 字段，后续 AutoChess assertion/profile 可以直接消费文本证据。
4. `GASActiveEffectPreTickSystem` 会记录本帧 `activeEffectSlotSourceAttributeSnapshotCapacity`，并用按 chunk 分配的 `NativeArray<ActiveEffectSlotSourceSnapshotLaneCounters>` 在 gather job 和 apply job 之间传递统计。
5. `GEActiveEffectPreTickSourceAttributeSnapshotGatherJob` 在 parallel gather 中记录 source attribute snapshot 写入尝试、成功、失败和 attribute miss；`TryAdd(...)` 失败会被标记为 write failure、capacity pressure 和 spill evidence。
6. `GEActiveEffectPreTickJob` 在 SourceAttribute snapshot apply 中记录 hit / miss，并在 fallback value 路径记录 lane-specific fallback。
7. CodeGen 模板和当前 generated 输出已同步 `ActiveEffectSlotSourceSnapshotLaneCounters`、`SnapshotLaneCounters` 传递、capacity 记录、hit/miss/fallback 记录，避免只修改 `.gen.cs` 后生成回流。
8. `Verify-GAS-RuntimeCoreBoundary.ps1` 已加入 lane-specific counter 防回流门：要求 stream/debugger 输出字段、generated/template counter array、gather write evidence、apply hit/miss/fallback evidence 同时存在。

### 验证补录：AutoChess Run5

`00-当前架构事实/_归档/2026-06-08-AutoChessBattleValidation-TagRequirementQuery-Run5.log` 显示本切片通过 AutoChess x50 headless validation：`AutoChessDemoValidationRunResult passed=True`、`thresholdsPassed=True`、`runtimeChainPassed=True`、`repeatRunPassed=True`、`blockingDebugErrors=0`。业务链路字段包括 `activeEffectSlots=100`、`periodTickDamageFacts=150`、`pendingAttributeAppliedDeltas=250`、`executionOutputs=344`、`cueRequests=1404`，performance pass summary 为 `performancePassObservationPollutionRisks=0`。同轮日志未命中 `Exception`、`SnapshotLaneCounters`、`Use CollectionHelper` 或 `error CS`。

本轮早期运行曾暴露 `GEActiveEffectPreTickSourceAttributeSnapshotGatherJob` 调度后未先写回依赖就触发 archetype 结构性检查、以及 explicit remove 分支复用 tick job 时没有构造 `SnapshotLaneCounters` / `ActiveEffectSlotSourceAttributeSnapshots` NativeContainer 的问题。修复点已经回写 CodeGen 模板并重新生成；Run5 是最终干净证据，早期 Run 日志如果业务字段通过但仍含 DOTS safety 异常，不能作为本切片完成证据。

### 仍成立风险

1. 该切片把 active effect slot tick 的 SourceAttribute live lookup 前移为 snapshot gather，并让 key / 容量口径对齐 owner-local slot 事实；Run5 已证明 x50 业务链路和 DOTS safety 回归通过，R4 已补 lane-specific hit / miss / fallback / capacity pressure / spill counters，但尚未新增非零 `activeEffectSlotSourceSnapshot*` 业务样本或规模 profile。
2. gather job 当前已在手写 Runtime owner 中；R5 口径不再把它写成 generated runtime lifecycle owner。release-ready 形态仍需要把 Runtime Core lane owner、generated marker / pure glue、防回流 gate、capacity / spill 和 scale evidence 继续分离验收。
3. execution calculation 和 managed resolver 的 capture miss live lookup 仍只是 evidence 显性化，尚未全部替换成 owner-local snapshot record。
4. 本切片已有 Unity batchmode x50 运行证据，但尚未新增 x100 / x1000 / Profiler enabled 运行证据，也没有触发非零 active effect slot SourceAttribute counter，不能证明 DOTS 性能优秀。

### DOTS 判定

| 规则 | 本轮判定 |
|---|---|
| `QRY-04` / `PRF-06` / `PRF-19` | active effect slot tick 的跨 owner SourceAttribute 读取已从 apply job live lookup 改为 frame-local snapshot gather。 |
| `NAT-03` / `BUF-02` | snapshot map 具备并行写入和 owner-aware deterministic key；容量已由 catalog SourceAttribute modifier 上界估算，capacity pressure / spill counter 已接入 Debugger evidence，但 scale evidence 仍待 R8 补齐。 |
| `BLOB-01` / `BUR-01` / `ODF-05` | 修改已回到 CodeGen 模板和 generated 输出；后续仍必须把 generated lifecycle owner 收权为 pure glue 或明确 MigrationProofOnly。 |

### 新任务输入

1. R5：继续对账 `RuntimeActiveEffect.gen.cs` marker、hand-written Runtime Core owner、manifest/report/file/schedule 和 generated lifecycle 防回流 gate。
2. R8：用 AutoChess 规模 profile 验证 snapshot map capacity、gather cost 和 Debugger overhead 不污染 performance pass，并要求 `activeEffectSlotSourceSnapshot*` counters 出现非零业务样本。
