# PHY-01：Physics pipeline 内保持物理实体布局稳定

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Unity Physics 1.4.6
**严重度**：P0
**Primary Owner**：UnityPhysics
**来源**：`physics-data-types.md` > physics ECS data stability

## 规则声明

从 `PhysicsInitializeGroup` 构建 simulation data 到 `ExportPhysicsWorld` 回写完成之间，不得改变 rigid body 对应 physics entities 的 chunk layout：不得创建/销毁 physics entity，也不得给 physics entity 添加或移除组件。Physics ECS 数据应在 `PhysicsSystemGroup` 之前或之后修改。

这不是“期间禁止任何 ECS 结构变化”。与 physics entities 无关的结构变化不受这条 Physics 专属约束，但仍需满足一般 ECS job 依赖和结构变化安全要求。

## 为什么

`PhysicsInitializeGroup` 与 `ExportPhysicsWorld` 要求两端的 rigid-body chunk layout 一致。Pipeline 内直接修改 physics ECS data 不会正确影响当前 simulation，可能被导出覆盖，并可能触发 integrity error。

## 检查方法

- 检查位于 `PhysicsSystemGroup` 内的系统是否创建/销毁 physics entity，或修改其组件集合。
- 检查 physics component value 写入是否移到 group 之前/之后。
- 若要修改中间模拟结果，检查是否使用 `PhysicsSimulationGroup` 官方子组和 simulation-data API。
