# PRF-03: 禁止用 Tag Component 做高频状态标记

**严重度**: P0
**Primary Owner**: Enableable-Component选型
**来源**: `performance-chunk-allocations.html`; `主题/13-DOTS编写规范与性能陷阱.md` P0-03

## 规则声明
每个 tag component（无数据字段的 `IComponentData`）使 archetype 排列数翻倍。高频 toggle 的状态标记必须使用 `IEnableableComponent` 或 owner-local bitset，不得使用 Add/Remove Tag Component。

## 为什么
N 个独立的 tag component → 最多 2^N 种 archetype 排列。10 个 tag → 最多 1024 种 archetype → 1024 × 16 KiB = 16 MB 仅 chunk header。50 个 tag → 天文数字。即使实际未达上限，每次 entity 获得/失去 tag = archetype 迁移。

## EX-GAS 诊断
审计所有 `IComponentData` 中无数据字段的 struct——这些是隐式的 tag component，可能在不同 archetype 间产生排列爆炸。特别是 GrantedTags、AbilityTag、EffectTag 等标记类数据。

## 检查方法
- Archetype 窗口检查：archetype 总数是否接近 entity 总数（archetype 膨胀的典型信号）
- 审计所有 `IComponentData` struct 中无数据字段的 "marker" component
- 高频 toggle 的 tag → 改为 `IEnableableComponent`
- Debugger 报告 `archetypeCount` 和 `tagComponentCount` 作为监控指标
