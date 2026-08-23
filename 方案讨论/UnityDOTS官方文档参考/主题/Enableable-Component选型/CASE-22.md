# CASE-22: EntityQueryMask 快速检查 Archetype 是否匹配 Query

**Primary Owner**: Enableable-Component选型
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**: Unity 6000.3 / Entities 1.4.6
**官方来源**: `Unity.Entities/Iterators/EntityQuery.cs`（`EntityQueryMask`、`EntityQuery.Matches`）
**关联规则**: EN-02

## 使用场景
需要快速判断任意 entity 的 archetype 是否属于某个 query 的 archetype 集合，用于分类或路由；不要求考虑 shared/change/order filter 或 enableable 状态。

## 模式描述
通过 `query.GetEntityQueryMask()` 获取 mask，再调用 `mask.MatchesIgnoreFilter(entity)`。该 API 检查 entity 是否存在以及其 archetype 是否匹配；它会忽略所有 chunk filter 与 enableable component 的当前启用状态。

```csharp
var mask = query.GetEntityQueryMask();
bool matches = mask.MatchesIgnoreFilter(someEntity);
```

## 注意事项
- `EntityQueryMask.Matches(Entity)` 在 Entities 1.4.6 已是编译错误级 obsolete；不要用它。需要完整 query 语义时调用 `query.Matches(entity)`，该同步 API会等待影响结果的相关写 job。
- Mask 不会因为 entity 的结构变化而要求手动重建；新 archetype 的 mask 位由 World 的 query/archetype 管理维护。但 mask 绑定创建它的 World，不能跨 World 或在 World 销毁后使用。
- 每个 World 最多分配 1024 个 `EntityQueryMask`。只缓存稳定分类 query 的 mask，避免为临时 query 无界创建。
- “快速”是 API 设计语义；是否值得引入 mask 缓存仍应以实际调用频率和 Profiler 为准，不使用固定收益倍率。

## EX-GAS 适用点
- Entity 分类路由到不同 processing pipeline
- Archetype 分组判断
- Effect target 类型检查
