# CASE-33: ComponentTypeSet 批量结构变化

**Primary Owner**: DynamicBuffer-Chunk-Archetype
**来源**: DynamicBuffer-Chunk-Archetype.md / `optimize-structural-changes.md`
**关联规则**: PRF-24, PRF-14

## 使用场景
需要一次性添加或移除多个 component 到 entity，避免逐 component 操作导致多个中间 archetype。

## 模式描述
使用 `ComponentTypeSet` 在一次结构变化中添加/移除多个组件，最小化 archetype 中间态。

```csharp
var typeSet = new ComponentTypeSet(typeof(A), typeof(B), typeof(C));
EntityManager.AddComponent(entity, typeSet);  // 一次结构变化
```

## 注意事项
- 避免逐次调用 `AddComponent<T>()` 产生 N-1 个中间 archetype
- ComponentTypeSet 适用于批量添加和批量移除
- 可与 `CreateEntity(archetype, count)` 结合使用

## EX-GAS 适用点
- Battle 初始化时的批量 entity 创建
- Definition 加载期的 entity 预分配
- Effect 批量应用时的 component 添加操作
