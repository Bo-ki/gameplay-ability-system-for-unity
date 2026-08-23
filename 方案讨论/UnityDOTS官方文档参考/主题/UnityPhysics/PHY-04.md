# PHY-04：Physics 查询必须声明 broadphase 构建时点

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Unity Physics 1.4.6
**严重度**：P1
**Primary Owner**：UnityPhysics
**来源**：`collision-queries.md` > Broadphase 说明

## 规则声明

每个 collision-world query 调用点都必须明确：

1. 查询系统相对 `PhysicsInitializeGroup` / `PhysicsSimulationGroup` 的 update order。
2. 是否接受默认 broadphase 构建时点。
3. 是否启用 `PhysicsStep.SynchronizeCollisionWorld`，以及同步后的查询窗口。

“必须在代码旁声明”是 EX-GAS 的审查策略；broadphase 构建与同步语义是官方事实。

## 为什么

默认 broadphase 在 `PhysicsInitializeGroup` 构建，并不会在 simulation 后自动反映最新位置。启用同步会增加性能成本，但能提供更新后的 collision world。只写“上一帧/当前帧”不足以描述一个渲染帧可能多 fixed steps 的情况。

## 检查方法

搜索 `OverlapAabb`、`CastRay`、`CastCollider` 和 distance query；核对系统排序、`SynchronizeCollisionWorld` 配置和消费时点是否一致。
