# SC-03: 批量同类结构变化优先 EntityQuery Bulk

**严重度**: P1
**Primary Owner**: 结构变化-ECB
**来源**: `performance-sync-points.md`、`optimize-structural-changes.md`

## 规则声明

当需要对大量 entity 执行相同的结构变化时（如批量 AddComponent、批量 DestroyEntity），优先使用 `EntityManager.AddComponent<T>(query)` 等批量 API，而非逐个 entity 操作。

## 为什么

逐个 entity 操作导致 O(n) 次 archetype 迁移和内部检查。批量操作在内部一次性遍历所有匹配 chunk，大幅减少开销。

## EX-GAS 诊断

当前无批量操作模式对照。AutoChess battle 初始化/批量效果移除路径中可能存在逐个操作的性能浪费。

## 检查方法

搜索 `foreach` + `AddComponent` / `DestroyEntity` 模式；排查批量操作的可替代性。
