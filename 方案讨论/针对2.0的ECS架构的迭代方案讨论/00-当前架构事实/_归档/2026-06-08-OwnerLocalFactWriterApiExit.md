# OwnerLocalFactWriter API Exit 归档

> 日期：2026-06-08
> 切片：R3 singleton stream owner / helper fact writer API 收口
> 代码范围：`Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs`、`Assets/GAS/Runtime/Ability/AbilityRuntimeActions.cs`、`Assets/GAS/Runtime/Effect/ExecutionCalculationRuntimeActions.cs`、`Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs`、`Assets/GAS/Runtime/System/Effect/EffectMagnitudeResolver.cs`、`Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1`

## 结论

本轮删除 Runtime helper 层的 legacy `GameplayEventWriter` / `BeginGameplayEventWriter` / `AppendGameplayEvent` API 形状，改为显式 `OwnerLocalFactWriter` / `BeginOwnerLocalFactWriter` / `AppendFact`。写入目标仍是 `OwnerLocalGameplayFactBuffer`，owner 由 `TargetAsc` 优先、`SourceAsc` 兜底解析。

这不是 singleton stream owner 完成态。`GEEffectCommandStreamComponent` 仍负责分配 `NextFactSequence` 并承载 owner-local fact 计数；本轮完成的是把 helper API 的语义从“GameplayEvent stream writer”收窄为“owner-local fact writer”，让后续静态门禁可以直接阻断旧命名和旧写入模型回流。

## 已移除 / 替换

| 旧 API | 新 API | 影响 |
|---|---|---|
| `EffectCommandSpecStream.GameplayEventWriter` | `EffectCommandSpecStream.OwnerLocalFactWriter` | helper API 不再暴露 legacy event writer 名称 |
| `BeginGameplayEventWriter(...)` | `BeginOwnerLocalFactWriter(...)` | 入口语义改为 owner-local fact |
| `AppendGameplayEvent(...)` | `AppendFact(...)` | 写入语义改为 fact append，不再表达为 stream event append |

## 已迁移调用面

1. `AbilityRuntimeActions` 的 ability end / cancel request fact。
2. `ExecutionCalculationRuntimeActions` 的 execution output updated helper fact。
3. `EffectRuntimeUtility` 的 gameplay effect removed helper fact。
4. `EffectMagnitudeResolver` 的 magnitude fallback / missing output fact。
5. `Verify-GAS-RuntimeCoreBoundary.ps1` 防回流规则升级为禁止 `GameplayEventWriter` / `BeginGameplayEventWriter` / `AppendGameplayEvent` 回流，并要求 `OwnerLocalFactWriter` 存在。

## 剩余风险

1. `OwnerLocalFactWriter` 仍依赖 `GEEffectCommandStreamComponent.NextFactSequence` 分配 fact sequence；sequence owner 尚未迁出 stream singleton。
2. `OwnerLocalFactCount` / `OwnerLocalFactFlushCount` 等计数仍记录在 stream singleton 上；它们是 evidence owner，不是 gameplay authority owner。
3. 部分系统内部仍有本地 `EnqueueGameplayEvent(...)` 方法名，但实际写入的是 `OwnerLocalGameplayFactBuffer`。这些名称属于后续可读性瘦身，不是本轮阻断项。
4. 本轮没有执行 x50/x100/x1000 无头 AutoChess 规模门，不能把 helper API 收口写成性能终局证明。

## 后续建议

1. 下一轮 R3 可继续迁 `NextFactSequence` 到 dedicated fact sequence owner，或把 fact sequence 分配改成 Boundary export deterministic merge 的一部分。
2. Debugger/validation 可新增 `legacyGameplayEventWriterApiHits=0` 机器字段，但当前静态门禁已足够防止 API 回流。
3. 若保留 `GEEffectCommandStreamComponent` 作为 evidence counter owner，文档必须继续写明它不是 fact carrier 目标态。
