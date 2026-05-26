# DBG-01: 三层诊断体系各有明确职责边界，不可互相替代

**严重度**: P0
**Primary Owner**: Diagnostics-Debugger
**来源**: 06-Diagnostics-Profiler-Journaling.md

## 规则声明
日常开发用手写 Debugger counters，热点定位用 Profiler + Structural Changes module，疑难排查用 Journaling 逐帧回放。不允许用 Journaling 替代 Debugger counters。

## 为什么
Journaling 有巨大运行时开销，不适合持续运行；Profiler 需要人工观察，不适合自动验收。三层工具链设计上各有不可替代的用途。

## EX-GAS 诊断
GasRuntimeDebugger 当前已实现 counters 但缺少与 Journaling/Profiler 的切换引导机制。不应把 journaling 当作"实时日志"使用。

## 检查方法
Debugger 启动时检查 Journaling 是否开启；若开启且非排查模式则告警。
