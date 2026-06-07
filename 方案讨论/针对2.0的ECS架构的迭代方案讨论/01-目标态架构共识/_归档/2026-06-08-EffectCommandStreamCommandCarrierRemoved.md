# EffectCommandStream command carrier 移除目标态兑现记录

> 日期：2026-06-08

本记录对应 `04-EffectCommand-SpecStream-AttributeDeltaSpec` 与 Runtime Core 目标态中 “EffectCommand / Spec / Delta / Fact 是语义链，不是全局 singleton 总线” 的 stream command carrier 清理切片。

## 兑现点

- singleton `EffectCommandStream` 不再拥有 `GEEffectCommandBuffer` 或 `GESetByCallerValueBuffer`。
- `GEEffectCommandStreamFramePrepareSystem` 只重置 stream component counter，不再清理旧 command payload buffer。
- EntityManager helper 的 `CommandWriter` 不再提供写 singleton stream command 的 `AppendCommand(...)` API。
- runtime boundary/simple request 仍通过统一 writer 分配 sequence / context，但写入目标 ASC owner-local instant command buffer 或 active mutation command buffer。
- AutoChess damage execution 已从 command consumer 切到 owner-local spec consumer，调度位置改为 spec build 后、attribute delta apply 前。
- 诊断脚本已把 stream command/set-by-caller carrier、旧 fan-in record、旧 frame clear 和 AutoChess command consumer 列为 forbidden。

## 未完成点

- `GEEffectCommandStreamComponent` 仍是 sequence / context / telemetry owner，后续需要继续收缩为更明确的 runtime frame counter owner。
- 生成 AbilityActivation / ActiveEffect lifecycle 仍有跨 owner `BufferLookup` 面，需要后续按 owner-local fan-in 或 chunk-local owner group 继续拆。
- 本切片未运行 Unity headless AutoChess、Unity Test Runner 或性能 profile。
