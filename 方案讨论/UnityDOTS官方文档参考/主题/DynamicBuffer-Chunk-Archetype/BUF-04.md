# BUF-04：InternalBufferCapacity 必须按长度分布与 chunk 成本选择

**严重度**：P1
**Primary Owner**：DynamicBuffer-Chunk-Archetype
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `components-buffer-set-capacity.md`、`performance-chunk-allocations.md`

## 规则声明
可以保留默认容量、设置自定义 `N` 或设置 `0`；选择必须同时基于典型/峰值 Length、访问热度、外部化频率和 archetype chunk capacity，不得只追求“零溢出”。

## 为什么
更大的 inline capacity 降低外部分配概率，却会减少每 chunk entity 数；频繁大幅变化的 buffer 可能更适合始终外部化。默认 128-byte 策略是合法基线。

## EX-GAS 诊断
`BActiveEffectSlot` 的 16/64 等候选值必须由实际分布验证，不把设计估计写成官方推荐。

## 检查方法
在同一 workload 下对比候选容量的 chunk count、unused bytes、externalized 数和系统耗时。
