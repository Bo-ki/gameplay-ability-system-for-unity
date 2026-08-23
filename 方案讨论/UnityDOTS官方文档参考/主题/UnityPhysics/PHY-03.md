# PHY-03：Collision/Trigger event 引用不能跨下一次 simulation 更新

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Unity Physics 1.4.6
**严重度**：P0
**Primary Owner**：UnityPhysics
**来源**：`simulation-results.md` > `Events`

## 规则声明

Collision/trigger events 在 `PhysicsSimulationGroup` 完成后有效，直到下一次该 group 开始更新。禁止跨越这个窗口保存或读取 event iterator、simulation stream 引用及其内部指针。

可以在有效窗口内把业务所需的稳定值（例如 `Entity`、位置、法线、业务键）复制到项目拥有的组件或 NativeContainer，供后续系统使用。

## 为什么

Event 数据存储在 `Simulation` 内部 streams 中，下一次 simulation 更新会复用或重建相应存储。一个渲染帧可能运行多个 fixed steps，因此“同一渲染帧内”不等于仍然有效。

## 检查方法

搜索持久字段、component 或跨 step container 中是否保存 event iterator/reference。允许的持久化结果必须是已复制的值，而不是对 Physics 内部 event storage 的引用。
