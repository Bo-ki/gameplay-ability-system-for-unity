# DBG-05: 性能报告必须按 cost 分组统计，禁止混合归因

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1
**严重度**: P1
**Primary Owner**: Diagnostics-Debugger
**来源**: EX-GAS 性能报告口径

## 规则声明
性能报告必须按 `coreSimulationTickMs`、`physicsStepMs`、`renderMs`、`runnerBootstrapMs`、`observationTickMs` 分组统计。禁止将不同分组的 cost 混合后归因于单一系统。

## 为什么
混入 physics 或 rendering 耗时后归因 GAS 性能问题，掩盖真实瓶颈，导致优化方向错误。

## EX-GAS 诊断
GasRuntimeFrameBudgetContract 已声明分组预算，但 Debugger 输出尚未按分组聚合。应实现 `ReportGroupedCost()` 输出各分组占比。

## 检查方法
Debugger validation summary 包含 cost 分组表格；未启用的分组显式标记为“不适用/未采集”，已采集分组说明计时边界。帧预算是否通过由对应项目基准判定。
