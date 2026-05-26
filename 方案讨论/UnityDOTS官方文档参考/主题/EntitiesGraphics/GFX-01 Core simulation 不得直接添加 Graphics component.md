# GFX-01: Core simulation 不得直接添加 Graphics component

**严重度**: P0
**Primary Owner**: EntitiesGraphics
**来源**: `requirements-and-compatibility.md`、官方案例模式 CASE-10

## 规则声明
Runtime Core system（在 `SimulationSystemGroup` 中运行的所有 GAS system）禁止直接添加、修改或读取 Entities Graphics 专有 component（`RenderMeshArray`、`MaterialMeshInfo`、`RenderBounds` 等）。Graphics component 的操作限于 Presentation/Boundary 层。

## 为什么
1) Core simulation 直接添加 graphics component 产生非必要的结构变化和 archetype 膨胀；2) 无头模式不加载 Entities Graphics 包，直接引用 graphics component 导致编译/加载错误；3) 违反表现与逻辑分离原则。

## EX-GAS 诊断
Debugger 检测 `SimulationSystemGroup` 中对 `RenderMesh*` / `MaterialMeshInfo` / `RenderBounds` 等 graphics 组件的引用。

## 检查方法
Grep `RenderMesh` / `MaterialMeshInfo` / `RenderBounds` 在 `Assets/GAS/Runtime/System/` 目录下；确认 Core layer 零引用。
