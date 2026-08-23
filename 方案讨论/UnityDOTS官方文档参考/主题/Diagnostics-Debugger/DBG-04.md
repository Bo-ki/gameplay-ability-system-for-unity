# DBG-04: 手写 Debugger 只能记录计数，不做字符串拼接、不分配托管内存

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**严重度**: P1
**Primary Owner**: Diagnostics-Debugger
**来源**: `performance-sync-points.md`；项目 Debugger 开销基准

## 规则声明
Runtime Core Debugger 的 hot path 仅记录预分配的结构化数据。人读字符串格式化和导出放到采样边界或非 hot path；不得引入未计入报告的依赖完成或托管分配。

## 为什么
Debugger 自身的开销如果进入主线程关键路径，会污染性能数据。字符串拼接和托管分配会产生 GC 压力和非确定性暂停。

## EX-GAS 诊断
所有 Debugger 输出走预分配 `FixedList`/`NativeArray` 和 struct 级计数器。GasRuntimeDebugger 中的字符串应全部采用预格式化或结构化数据，不做运行时 `string.Format`/`$""`。

## 检查方法
代码审查搜索 Debugger hot path 中的 `$""`、`string.Format`、`new string`、`new object[]`、`Debug.Log`。
