# Hand-written Ability Fact Owner-local 目标态兑现记录

> 日期：2026-06-08

本记录对应 `03E-04-GameplayFactSpec` 中 Core reaction fact 首跳进入 owner-local carrier、Boundary observation 只做导出的目标。

## 兑现点

- `ASCCommandBufferResolveSystem` 不再把 tag / attribute / ability request facts 追加到 singleton fact stream。
- `AbilityStateCleanupSystem` 不再把 ability cleanup lifecycle facts 追加到 singleton fact stream。
- `AttributeThresholdAbilityLifecycleRequestSystem` 不再把 threshold lifecycle request facts 追加到 singleton fact stream。
- hand-written ability / ASC producer 的 fact 首跳统一落在 ASC `OwnerLocalGameplayFactBuffer`，随后由 `GameplayBoundaryFactExportSystem` 导出到 `BoundaryObservationFactBuffer`。

## 未完成点

- sequence owner 尚未从 `GEEffectCommandStreamComponent.NextFactSequence` 迁出。
- generated active effect lifecycle 与 execution calculation merge 仍有 stream fact producer。
- 真实业务规模验收仍需等 legacy stream fact producer 清理完后统一跑 x50/x1000。
