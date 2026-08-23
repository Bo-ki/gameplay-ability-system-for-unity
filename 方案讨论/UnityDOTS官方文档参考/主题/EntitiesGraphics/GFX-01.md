# GFX-01：Core simulation 不得直接访问 Graphics component

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：EX-GAS 当前分层架构；Entities Graphics 1.4.19
**严重度**：P0
**Primary Owner**：EntitiesGraphics
**来源**：EX-GAS Presentation Outbox 设计；官方 `overview.md` 仅提供渲染桥接机制，不要求所有项目采用该分层

## 规则声明

EX-GAS Runtime Core 禁止添加、修改或读取 Entities Graphics 专有组件，例如 `RenderMeshArray`、`MaterialMeshInfo`、`RenderBounds`。Core 只产出 Presentation Outbox，由表现边界系统消费。

## 为什么

这是为了让无头与有头模式共享同一 Core simulation、避免表现资源反向污染 battle hash，并让 Runtime Core 不依赖可选渲染包。它不是 Unity 对所有 `SimulationSystemGroup` 系统的通用禁令。

## 检查方法

检查 Runtime Core assembly 是否引用 `Unity.Rendering`，并搜索 Core system 对 Graphics 组件的访问。允许的访问点必须位于明确的 Presentation/Boundary assembly。
