# GFX-02：从零创建渲染实体使用 AddComponents；批量创建使用 Instantiate

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities Graphics 1.4.19
**严重度**：P0
**Primary Owner**：EntitiesGraphics
**来源**：`overview.md` > `Runtime functionality`；`runtime-entity-creation.md` > `RenderMeshUtility - AddComponents`、`Usage instructions`

## 规则声明

1. 复杂对象优先使用 baked entity prefab。
2. 从 C# 从零创建渲染实体时，在主线程调用接收 `RenderMeshArray` 的 `RenderMeshUtility.AddComponents(Entity, EntityManager, RenderMeshDescription, RenderMeshArray, MaterialMeshInfo)`。
3. 大量同构实例先准备一个完整原型，再使用 `Instantiate` 克隆并设置实例差异。
4. 禁止手工拼装 Entities Graphics 内部组件。

## 为什么

内部必需组件集合会随管线和包版本变化。`AddComponents` 会产生结构变化且仅能在主线程调用；逐实例调用不适合批量生成。接收 `RenderMesh` 的重载仅供 Baking 使用，运行时调用不会产出可渲染实体。

## 检查方法

- 搜索手工添加 `RenderBounds`、`MaterialMeshInfo` 等内部渲染组件的代码。
- 检查运行时是否误用 `RenderMesh` 重载或向 `AddComponents` 传 ECB。
- 检查批量路径是否复用 baked prefab/runtime prototype 并走 `Instantiate`。
