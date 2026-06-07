# GameplayEventWriter owner-local 目标态兑现记录

> 日期：2026-06-08

本记录对应 `03E-04-GameplayFactSpec` 中 “Core reaction fact 归属 ASC owner-local carrier，Boundary observation 由导出系统单向投影” 的 runtime helper 落地切片。

## 兑现点

- EntityManager helper 层的 fact append 不再把 singleton `GameplayEventBuffer` 当写目标。
- `GameplayEventWriter.AppendGameplayEvent(...)` 自行解析 fact owner，按 `TargetAsc` 优先、`SourceAsc` 兜底写入 `OwnerLocalGameplayFactBuffer`。
- `NextFactSequence` 仍由 `GEEffectCommandStreamComponent` 管理，但只有确认 owner-local 写入可执行后才消耗 sequence。
- 现有 `AbilityRuntimeActions` / `ExecutionCalculationRuntimeActions` / `EffectRuntimeUtility` / `EffectMagnitudeResolver` 调用端无需各自复制 owner-local lookup 逻辑。
- 诊断脚本禁止 `_facts` / singleton fact buffer writer 回流。

## 未完成点

- `GameplayEventWriter` 名称和 `GameplayEventBuffer` payload 仍保留，后续可在 carrier 清理完成后再重命名为更贴近 owner-local fact lane 的 API。
- singleton `GameplayEventBuffer` 仍作为 legacy stream carrier 存在，FramePrepare / migration input / Boundary export 读面尚未整体清理。
- 本切片未运行 Unity headless AutoChess 或性能档。
