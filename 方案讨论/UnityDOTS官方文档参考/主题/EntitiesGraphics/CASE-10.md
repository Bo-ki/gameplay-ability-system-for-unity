# CASE-10：Graphics runtime create 限于 Presentation 层

**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Entities Graphics 1.4.19；EX-GAS 当前分层架构
**Primary Owner**：EntitiesGraphics
**来源**：`runtime-entity-creation.md` > `Usage instructions`；EX-GAS Presentation Outbox 设计
**关联规则**：GFX-01、GFX-02、GFX-03

## 使用场景

Presentation 层根据 Outbox 请求生成大量同构渲染实体。`RenderMeshUtility.AddComponents` 仅用于主线程创建一次原型；批量实体通过 ECB 实例化原型。

## 模式描述

```csharp
// 初始化阶段（主线程）：创建一次 runtime prototype。
var prototype = entityManager.CreateEntity();
RenderMeshUtility.AddComponents(
    prototype,
    entityManager,
    renderMeshDescription,
    renderMeshArray,
    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
entityManager.AddComponentData(prototype, new LocalToWorld
{
    Value = float4x4.identity
});

// 批量阶段：ECB 可以实例化已具备完整 Graphics 组件集的原型。
var renderEntity = ecb.Instantiate(sortKey, prototype);
ecb.SetComponent(sortKey, renderEntity, new LocalToWorld
{
    Value = float4x4.Translate(worldPosition)
});
```

`ecb` 在并行 job 中必须是 `EntityCommandBuffer.ParallelWriter`。若对象复杂且可在 Editor 定义，优先使用 baked entity prefab，而不是运行时构建原型。

## EX-GAS 边界

- Core simulation 只写 Presentation Outbox，不引用 Graphics 组件。
- 原型创建和实例化系统放入允许相应修改的 Presentation 例外组，遵循 GFX-03。
- 无头模式消费同一 Outbox，但不创建真实渲染实体。
