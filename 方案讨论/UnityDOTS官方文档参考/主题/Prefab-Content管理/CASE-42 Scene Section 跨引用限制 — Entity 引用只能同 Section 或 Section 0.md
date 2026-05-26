# CASE-42: Scene Section 跨引用限制 — Entity 引用只能同 Section 或 Section 0

**Primary Owner**: Prefab-Content管理
**来源**: `streaming-scene-sections.md`
**关联规则**: CONTENT-01

## 使用场景
SubScene 中 ECS component 的 entity 引用有严格限制：**只能引用同一 section 内的 entity，或 section 0 中的 entity**。引用其他 section 的 entity 在加载时被静默设为 `Entity.Null`。

## 模式描述
```csharp
// Section 0 — 常驻，被所有其他 section 引用
// 包含：ASC entity、Ability entity、全局 GameState entity

// Section 1 — 空间分区战斗区域 1
// 包含：该区域的 unit、obstacle、terrain
// 引用限制：只能引用 Section 1 自身或 Section 0 的 entity

// Section 2 — 空间分区战斗区域 2
// 包含：该区域的 unit、obstacle、terrain
// 引用限制：不能直接引用 Section 1 的 entity
```

## 注意事项
- Prefab 实例继承 `SceneSection` component——卸载该 section 的场景时所有关联 prefab 实例被销毁
- 若需持久化需手动移除 `SceneSection` component
- 非 SubScene 模式（纯运行时创建的 entity）无 SceneSection 限制

## EX-GAS 适用点
- AutoChess 场景：ASC entity 放入 section 0
- 跨 section 引用全部指向 section 0
- 避免跨非零 section 引用导致静默 null
