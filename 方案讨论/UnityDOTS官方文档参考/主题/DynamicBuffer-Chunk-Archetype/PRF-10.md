# PRF-10：监控关键 DynamicBuffer 的容量与 chunk 代价

**严重度**：P1
**Primary Owner**：DynamicBuffer-Chunk-Archetype
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `components-buffer-set-capacity.md`、`performance-chunk-allocations.md`

## 规则声明
关键 buffer 监控 Length/Capacity 分布、外部化估计、chunk capacity/unused bytes 和访问耗时。告警按 buffer 类型、平台与 workload 校准，不使用全局固定 30%。

## 为什么
外部化增加间接访问且不会自动迁回，但更大的 inline capacity 也会降低 chunk capacity；单独追求低 spill rate 可能使整体更慢。

## EX-GAS 诊断
历史 `externalizedCount / total > 30%` 只保留为待基准配置候选，并在未建立基准前不作为 P1/P0 验收失败。

## 检查方法
同时观察 spill 与 chunk 使用率，对候选 InternalBufferCapacity 做目标 Player A/B 测试。
