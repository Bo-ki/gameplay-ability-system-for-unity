# CASE-22: EntityQueryMask O(1) 实体-Query 匹配检查

**Primary Owner**: Enableable-Component选型
**来源**: Enableable-Component选型.md / `components-enableable-use.html`
**关联规则**: EN-02

## 使用场景
需要快速判断任意 entity 是否匹配某个 EntityQuery，用于 entity 分类过滤、archetype 分组路由。

## 模式描述
`EntityQueryMask` 提供 O(1) 实体-query 匹配检查：通过 `query.GetEntityQueryMask()` 构建 mask，使用 `mask.MatchesIgnoreFilter(entity)` 检查（忽略 enableable 过滤）。

```csharp
var mask = query.GetEntityQueryMask();
bool matches = mask.MatchesIgnoreFilter(someEntity);
```

## 注意事项
- 构建 mask 有初始开销，高频时才值得
- `MatchesIgnoreFilter` 忽略 enableable 过滤，需要精确匹配时用 `Matches(entity)`
- Mask 在结构变化后可能失效，需要重建

## EX-GAS 适用点
- Entity 分类路由到不同 processing pipeline
- Archetype 分组判断
- Effect target 类型检查
