# DBG-03: Structural Changes Profiler 是热点定位工具，不能替代自动验收

**严重度**: P1
**Primary Owner**: Diagnostics-Debugger
**来源**: 06-Diagnostics-Profiler-Journaling.md

## 规则声明
Structural Changes Profiler Module 用于人工分析 heat path 的结构变化来源。自动验收必须依赖手写 Debugger 的结构变化计数器。

## 为什么
Profiler 需要人工操作和观察，不能嵌入 CI/自动测试流程。自动验收要求机器可读的计数指标。

## EX-GAS 诊断
Debugger 已输出 `EntityCreateCount`/`EntityDestroyCount`/`StructuralChangeCount`，但尚未按 system 归类结构变化来源。应扩展为记录各 system 的 `StructuralChangeCountBySystem`。

## 检查方法
Debugger 报告中包含按 system 分组的结构变化计数；目标：每个 hot path system 的结构变化为 0。
