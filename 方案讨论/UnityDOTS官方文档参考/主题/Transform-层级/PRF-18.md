# PRF-18：按精度需求选择 LocalToWorld 或 ComputeWorldTransformMatrix

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities 1.4.6
**严重度**：P1
**Primary Owner**：Transform-层级
**来源**：`transforms-concepts.md`、`transforms-helpers.md`

## 规则声明

`LocalToWorld` 在 `SimulationSystemGroup` 中可能过期、无效或含图形平滑偏移。延迟可接受时可作为快速近似；gameplay 需要当前精确层级世界矩阵时，调用 `TransformHelpers.ComputeWorldTransformMatrix(entity, out matrix, ref ...)`。

无 parent 的根 entity 若只需当前 position/rotation/uniform scale，可直接读最新 `LocalTransform`，不必支付层级遍历成本。

## 检查方法

审查 Simulation 中的 `LocalToWorld` 读取并标注精度需求。只有把近似值用于要求精确的范围、碰撞输入或规则裁决时才判违规。
