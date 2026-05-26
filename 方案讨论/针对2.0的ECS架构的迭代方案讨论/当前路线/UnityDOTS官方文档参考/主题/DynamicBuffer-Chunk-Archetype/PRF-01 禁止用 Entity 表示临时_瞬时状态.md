# PRF-01: 禁止用 Entity 表示临时/瞬时状态

**严重度**: P0
**Primary Owner**: DynamicBuffer-Chunk-Archetype
**来源**: `performance-chunk-allocations.html`

## 规则声明
每帧创建并随后销毁的 entity 表示临时状态，必须用 `DynamicBuffer`、`NativeStream` 或 `Enableable` 替代。

## 为什么
每次 `CreateEntity` + `DestroyEntity` = 2 次结构变化 + 1 次 archetype 迁移。不同的临时 entity 创建不同 archetype。高频 create/destroy 导致 chunk 碎片化。100K entity 各有独特 archetype -> >1.5 GB chunk 浪费。

## EX-GAS 诊断
ISSUE-001 — Instant GE 走 `CApplyGameplayEffectRequest -> runtime GE entity -> lifecycle -> destroy`。x50 下 GameplayEffectApplied=3479 等呈线性增长。

## 检查方法
Debugger 输出 `entityCreated - entityDestroyed ~= 0`。若 entityCreated 随 game event 数量线性增长 -> 违规。
