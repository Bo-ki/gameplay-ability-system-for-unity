# CASE-33：ComponentTypeSet 批量结构变化

**Primary Owner**：DynamicBuffer-Chunk-Archetype
**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `optimize-structural-changes.md`

## 使用场景
运行时需要对一个 entity、query 或 system-associated entity 同时添加/移除多个 component type。

## 模式描述
`ComponentTypeSet` 将多个类型交给一个 EntityManager API 调用，减少逐类型结构变化和冗余中间 archetype。

```csharp
var typeSet = new ComponentTypeSet(typeof(A), typeof(B), typeof(C));
entityManager.AddComponent(entity, typeSet);
```

## 注意事项
- 已知创建时最终布局，优先 `CreateArchetype` + 批量 `CreateEntity`。
- 对大量已存在 entity，优先比较 query/bulk API，而不是 entity-by-entity 循环。
- job 中结构变化使用 ECB；ComponentTypeSet 的 EntityManager 调用只能在主线程按其 API 契约执行。
- 是否 ECB 更快取决于 sync point 和立即可见性，使用 Profiler 决策。

## EX-GAS 适用点
Battle 初始化和加载阶段的批量布局建立；热路径首先评估是否可用 enableable/value 状态避免结构变化。
