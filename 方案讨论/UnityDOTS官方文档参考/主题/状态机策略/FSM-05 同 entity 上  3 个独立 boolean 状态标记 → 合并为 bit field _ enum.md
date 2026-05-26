# FSM-05: 同 entity 上 > 3 个独立 boolean 状态标记 → 合并为 bit field / enum

**严重度**: P1
**Primary Owner**: 状态机策略
**来源**: 推导自 `performance-chunk-allocations.md` archetype 爆炸风险

## 规则声明
当同一 entity 上存在超过 3 个独立的 boolean 语义状态标记（如 Stunned、Silenced、Disarmed、Slowed、Burning）时，必须合并为单一 IComponentData 内的 bit field 或 enum，禁止每个标记使用独立的 Tag Component 或 IEnableableComponent。

## 为什么
N 个独立 Tag Component → 最多 2^N 种 archetype 排列。N=10 时上限 1024。即使使用 IEnableableComponent，每个 enableable 也扩展 archetype 的 enableable mask 列数。bit field 在一个 struct 内表达所有标记，不对 archetype 产生任何影响。合并后单 job 内 `if (flags.Stunned) { ... }` 分支开销远小于多 component 开销。

## EX-GAS 诊断
Status Effect 标记（眩晕、沉默、缴械、减速、灼烧、中毒等 > 5 种）当前未统一表达。目标态应合并为 `CStatusFlags : IComponentData` bit field。

## 检查方法
搜索项目中独立的 boolean 语义 IComponentData（无字段仅用于标记）和 IEnableableComponent，若同 entity 上超过 3 个 → 标记为合并候选。
