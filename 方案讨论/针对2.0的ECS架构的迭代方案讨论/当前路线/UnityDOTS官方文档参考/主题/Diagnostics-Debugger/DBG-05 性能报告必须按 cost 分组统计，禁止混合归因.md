# DBG-05: 性能报告必须按 cost 分组统计，禁止混合归因

**严重度**: P1
**Primary Owner**: Diagnostics-Debugger
**来源**: 06-Diagnostics-Profiler-Journaling.md

## 规则声明
性能报告必须按 `coreSimulationTickMs`、`physicsStepMs`、`renderMs`、`runnerBootstrapMs`、`observationTickMs` 分组统计。禁止将不同分组的 cost 混合后归因于单一系统。

## 为什么
混入 physics 或 rendering 耗时后归因 GAS 性能问题，掩盖真实瓶颈，导致优化方向错误。

## EX-GAS 诊断
GasRuntimeFrameBudgetContract 已声明分组预算，但 Debugger 输出尚未按分组聚合。应实现 `ReportGroupedCost()` 输出各分组占比。

## 检查方法
Debugger validation summary 包含 cost 分组表格，各分组占比不为 0 且总和不超过帧预算。
