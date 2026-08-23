# GFX-03：Presentation 数据修改只允许在两个例外组中

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities Graphics 1.4.19
**严重度**：P0
**Primary Owner**：EntitiesGraphics
**来源**：`overview.md` > `Runtime functionality` 的 PresentationGroup NOTE

## 规则声明

- 普通 `PresentationSystemGroup` 系统不得修改 ECS 数据。
- `UpdatePresentationSystemGroup` 可以修改组件数据，但不得结构变化。
- `StructuralChangePresentationSystemGroup` 可以修改组件数据并执行结构变化。
- Presentation 结束后不得再修改 ECS 数据；需要延迟的修改安排在下一帧开始，而不是 Presentation 之后。

## 为什么

Presentation 之后运行的 culling jobs 依赖 ECS 数据保持与 Presentation 阶段结束时一致。该约束来自官方渲染管线，不应被简化为“整个 Presentation group 一律禁止结构变化”。

## 检查方法

审查所有 Presentation system 的 `UpdateInGroup`：数据写入必须在 `UpdatePresentationSystemGroup`；创建、销毁、添加或移除组件必须在 `StructuralChangePresentationSystemGroup`。同时检查 Presentation 之后是否仍有 ECS 写入。
