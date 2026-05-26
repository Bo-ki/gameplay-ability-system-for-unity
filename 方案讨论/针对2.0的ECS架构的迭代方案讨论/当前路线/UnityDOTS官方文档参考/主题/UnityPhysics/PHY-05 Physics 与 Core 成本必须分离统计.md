# PHY-05: Physics 与 Core 成本必须分离统计

**严重度**: P1
**Primary Owner**: UnityPhysics
**来源**: 项目架构决定（Physics 是可选 profile）

## 规则声明
Debugger 和 profiling 输出中，`coreTickMs`（纯 GAS Runtime Core 时间）与 `physicsStepMs`（PhysicsSystemGroup 时间）必须分离。Physics 未启用时 summary 输出 disabled reason，不能空白跳过。

## 为什么
1) AutoChess 无头模式不启用 Physics，coreTickMs 保持可比性；2) Physics cost 混入 coreTickMs 掩盖 Core 的性能瓶颈；3) 多个 profile 之间需要对比 physicsEnabled 状态的 Core 时间差异。

## EX-GAS 诊断
Summary 输出包含 `physicsStepMs` 列（即使 0 也写入 disabled reason）；压测报告拆分 Core 与 Physics 两类成本。

## 检查方法
确认 `GASManager` 或 `Debugger` 中 physicsTime 与 coreTime 使用独立计时器；无 Physics profile 中输出 "Physics disabled" 标记。
