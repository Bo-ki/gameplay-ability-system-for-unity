# 06 ActiveEffectSlot Magnitude Snapshot 事实

> Owner：00-当前架构事实/架构重划分审查事实 | 状态：当前事实子页 | 创建时间：2026-06-08

本文件只记录 active effect slot tick 中 SourceAttribute magnitude snapshot lane 的当前代码事实。目标态设计回到 `../../01-目标态架构共识/`；后续任务拆分回到 `../../02-主线任务树/`。

## 2026-06-08 切片：active effect slot SourceAttribute snapshot gather

### 事实消费卡

```markdown
来源类型：代码截面复核 / diagnostics 脚本审查
原始证据：
  - `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs`
  - `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs`
  - `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1`
第一 owner：00
长期有效：待复核；generated active effect runtime 仍属于 R5/R3 联动迁移面
需要反哺：R3 magnitude snapshot lane、R5 SourceGenerator pure glue 收权、R4 Debugger evidence gate
```

### 正向事实

1. `GASActiveEffectPreTickSystem` 现在会在 `GEActiveEffectPreTickJob` 前调度 `GEActiveEffectPreTickSourceAttributeSnapshotGatherJob`。
2. 新 gather job 使用 read-only `AttributeValueBuffer` lookup 预采样跨 owner `SourceAttribute`，写入 `NativeParallelHashMap<ActiveEffectSlotSourceAttributeSnapshotKey, float>.ParallelWriter ActiveEffectSlotSourceAttributeSnapshots`。
3. Snapshot key 包含 `Owner + SlotSequence + ModifierIndex`；这是必须项，因为 active effect slot sequence 是 owner-local，不是全局唯一。
4. `GEActiveEffectPreTickJob.BuildMagnitudeContextFromSlot(...)` 不再通过旧 `TryReadAttributeValue(ref ownerResources, slot.SourceAsc, ...)` 做跨 owner live lookup；同 owner 仍走 owner-local attributes，跨 owner 只消费 snapshot map。
5. Snapshot 容量口径由 `ownerCount * ActiveEffectStore.InlineSlotCapacity * maxSourceAttributeModifierCount` 估算，`maxSourceAttributeModifierCount` 来自 catalog 中各 GE 的 SourceAttribute modifier 数。
6. CodeGen 模板和当前 generated 输出已同步，避免只手改 `.gen.cs` 后下一次生成回流。
7. `Verify-GAS-RuntimeCoreBoundary.ps1` 已加入防回流门：要求生成物和模板都有 active effect pre-tick snapshot gather，禁止 owner-local-only key，并禁止 slot SourceAttribute 退回 live owner-resource lookup。

### 仍成立风险

1. 该切片把 active effect slot tick 的 SourceAttribute live lookup 前移为 snapshot gather，并让 key / 容量口径对齐 owner-local slot 事实；hit / miss / fallback 已可通过通用 Magnitude Source counter 写回 stream，但还没有 lane-specific attribution、capacity pressure、spill counter 或非零业务样本。
2. gather job 仍是 generated runtime artifact 中的 lifecycle / lookup owner，按 R5 口径只能作为迁移期 generated lane proof；release-ready 形态仍需要把 Runtime Core lane owner 与 generated pure glue 继续分离。
3. execution calculation 和 managed resolver 的 capture miss live lookup 仍只是 evidence 显性化，尚未全部替换成 owner-local snapshot record。
4. 本切片尚未新增 Unity batchmode / x100 / x1000 / Profiler enabled 运行证据，也没有触发非零 active effect slot SourceAttribute counter，不能证明 DOTS 性能优秀。

### DOTS 判定

| 规则 | 本轮判定 |
|---|---|
| `QRY-04` / `PRF-06` / `PRF-19` | active effect slot tick 的跨 owner SourceAttribute 读取已从 apply job live lookup 改为 frame-local snapshot gather。 |
| `NAT-03` / `BUF-02` | snapshot map 具备并行写入和 owner-aware deterministic key；容量已由 catalog SourceAttribute modifier 上界估算，但 capacity pressure / spill / scale evidence 仍待 R4/R8 补齐。 |
| `BLOB-01` / `BUR-01` / `ODF-05` | 修改已回到 CodeGen 模板和 generated 输出；后续仍必须把 generated lifecycle owner 收权为 pure glue 或明确 MigrationProofOnly。 |

### 新任务输入

1. R4：为 active effect slot source snapshot gather 增加 lane-specific hit / miss / fallback / capacity pressure / spill counters，并接入 `GasRuntimeMagnitudeSourceCounters` 或更细分的 owner evidence。
2. R5：继续把 generated lifecycle / lookup owner 从 `RuntimeActiveEffect.gen.cs` 迁向手写 Runtime Core owner + generated pure glue。
3. R8：用 AutoChess 规模 profile 验证 snapshot map capacity、gather cost 和 Debugger overhead 不污染 performance pass。
