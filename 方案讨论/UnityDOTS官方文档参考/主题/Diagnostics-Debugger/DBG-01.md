# DBG-01: 三层诊断体系各有明确职责边界，不可互相替代

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**严重度**: P0
**Primary Owner**: Diagnostics-Debugger
**来源**: `entities-journaling.md`、`profiler-modules-entities-introduction.md`、`profiler-module-structural-changes.md`

## 规则声明
日常自动验收默认使用低侵入 counters，热点定位使用 Profiler + Structural Changes module，疑难 ECS 变更检查使用 Journaling。Journaling 记录操作历史，不等同于 simulation replay。

## 为什么
Journaling 和 Profiler 都会改变观测开销及工作流；项目 counters 更适合机器可读的持续验收。三层职责划分是 EX-GAS 的项目选择，具体开销必须测量。

## EX-GAS 诊断
GasRuntimeDebugger 当前已实现 counters 但缺少与 Journaling/Profiler 的切换引导机制。不应把 journaling 当作"实时日志"使用。

## 检查方法
Debugger 启动时检查 Journaling 是否开启；若开启且非排查模式则告警。
