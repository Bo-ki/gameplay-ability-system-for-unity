# Hand-written Ability / ASC Fact Owner-local 切片事实

> 日期：2026-06-08

本切片把 hand-written ability / ASC lifecycle 相关 fact producer 从 singleton `GameplayEventBuffer` direct append 迁到 ASC owner-local `OwnerLocalGameplayFactBuffer`。

## 已落地

- `ASCCommandBufferResolveSystem` 的 ASC query 新增 `OwnerLocalGameplayFactBuffer`，tag changed、attribute base value changed、ability cancel/end request fact 在 ASC chunk 内直接写当前 owner fact buffer。
- `AbilityStateCleanupSystem` 的 AbilityCanceled / AbilityEnded fact 改为按 `AbilityStateComponent.Owner` 写 owner-local fact buffer。
- `AttributeThresholdAbilityLifecycleRequestSystem` 的 threshold cancel/end request fact 改为按 ability owner 写 owner-local fact buffer，并补齐 `Frame`。
- 三个 hand-written producer 仍只借用 `GEEffectCommandStreamComponent.NextFactSequence` 分配 sequence，不再获取 `BufferLookup<GameplayEventBuffer>`，也不再执行 `FactLookup[StreamEntity].Add(...)`。
- `Verify-GAS-RuntimeCoreBoundary.ps1` 新增防回流断言，阻止这三条链路重新直写 singleton fact stream。

## 剩余风险

- `NextFactSequence` 仍由 `GEEffectCommandStreamComponent` 统一分配，owner-local fact lane 还没有自己的 sequence owner。
- generated active effect lifecycle 与 execution calculation merge 仍有 legacy stream `GameplayEventBuffer` producer，需要继续迁出。
- 本切片已通过 runtime build 和边界脚本，但 x50/x1000 AutoChess 规模验收仍需在 legacy fact writer 清理后统一执行。
