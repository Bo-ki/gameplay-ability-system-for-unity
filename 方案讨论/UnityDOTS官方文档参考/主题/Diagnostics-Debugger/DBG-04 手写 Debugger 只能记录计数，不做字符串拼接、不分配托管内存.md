# DBG-04: 手写 Debugger 只能记录计数，不做字符串拼接、不分配托管内存

**严重度**: P1
**Primary Owner**: Diagnostics-Debugger
**来源**: 06-Diagnostics-Profiler-Journaling.md

## 规则声明
Runtime Core Debugger 的输出仅限于 struct 级计数器和预分配的固定缓冲区。禁止在 hot path 拼接人读日志字符串、分配托管内存或触发 sync point。

## 为什么
Debugger 自身的开销如果进入主线程关键路径，会污染性能数据。字符串拼接和托管分配会产生 GC 压力和非确定性暂停。

## EX-GAS 诊断
所有 Debugger 输出走预分配 `FixedList`/`NativeArray` 和 struct 级计数器。GasRuntimeDebugger 中的字符串应全部采用预格式化或结构化数据，不做运行时 `string.Format`/`$""`。

## 检查方法
代码审查搜索 Debugger hot path 中的 `$""`、`string.Format`、`new string`、`new object[]`、`Debug.Log`。
