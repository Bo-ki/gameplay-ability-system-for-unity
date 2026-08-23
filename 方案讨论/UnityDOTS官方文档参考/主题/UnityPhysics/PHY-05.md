# PHY-05：Physics 与 Core 成本分离统计

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：EX-GAS 当前 profiling schema；Unity Physics 1.4.6
**严重度**：P1
**Primary Owner**：UnityPhysics
**来源**：EX-GAS profiling 设计（非 Unity Physics 官方字段规范）

## 规则声明

Profiler/压测结果分别记录 `coreTickMs` 与 `physicsStepMs`，并同时记录 fixed timestep、每渲染帧执行的 physics step 数和 Physics enabled 状态。未启用时输出明确的 disabled reason，不把缺失值伪装成 0 ms。

## 为什么

`PhysicsSystemGroup` 可能在一个渲染帧内执行多次。只给出 frame-level 总耗时而不记录 step 数，会使不同运行条件不可比；把 Physics 计入 Core 则无法定位成本来源。

## 检查方法

核对计时器边界是否与系统组一致；报告同时包含 timestep、step count、运行平台和 profile 配置。
