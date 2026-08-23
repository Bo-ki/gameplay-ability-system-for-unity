# BUF-01：DynamicBuffer 必须记录容量策略并监控关键类型

**严重度**：P1
**Primary Owner**：DynamicBuffer-Chunk-Archetype
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `components-buffer-introducing.md`、`components-buffer-set-capacity.md`

## 规则声明
关键 DynamicBuffer 类型必须记录为何使用默认 128-byte 内部容量、定制 `[InternalBufferCapacity(N)]` 或显式 `0`，并以实际长度分布/chunk capacity 验证。不是每个 buffer 都必须机械添加属性。

## 为什么
容量过小会外部化且不自动迁回；容量过大则为每个 entity 预留 inline bytes、降低 chunk capacity。单看 externalized ratio 不能判断哪种策略更好。

## EX-GAS 诊断
Debugger 对热点 buffer 记录 Length/Capacity 分布、峰值、chunk capacity 和 unused bytes。历史“30% 告警”仅可作为待校准初始配置，不能标为官方阈值或全局硬门槛。

## 检查方法
用代表性场景比较候选容量的内存与迭代成本；记录平台、实体规模和采样版本。
