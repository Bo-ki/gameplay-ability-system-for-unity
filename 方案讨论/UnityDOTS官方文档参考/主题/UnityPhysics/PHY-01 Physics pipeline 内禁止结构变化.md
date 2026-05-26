# PHY-01: Physics pipeline 内禁止结构变化

**严重度**: P0
**Primary Owner**: UnityPhysics
**来源**: `physics-pipeline.md`

## 规则声明
`PhysicsSystemGroup` 的 pipeline 执行期间（`PhysicsInitializeGroup`、`PhysicsSimulationGroup`、`PhysicsExportGroup` 的运行阶段），禁止任何结构变化操作（CreateEntity、DestroyEntity、AddComponent、RemoveComponent）。结构变化必须通过 ECB 延迟到 `PhysicsSystemGroup` 之后。

## 为什么
Physics pipeline 依赖稳定的 CollisionWorld 结构。结构变化破坏 Physics 内部数据结构的确定性，导致碰撞检测结果不可预测，甚至在 Physics job 运行时触发安全系统错误。

## EX-GAS 诊断
Debugger 标记 `PhysicsSystemGroup` 内 system 的结构变化操作；ECB playback 应在 `PhysicsSystemGroup` 结束之后。

## 检查方法
搜索 `CreateEntity` / `DestroyEntity` / `AddComponent` / `RemoveComponent` 在 `[UpdateInGroup(typeof(PhysicsSystemGroup))]` 标记的 system 中。
