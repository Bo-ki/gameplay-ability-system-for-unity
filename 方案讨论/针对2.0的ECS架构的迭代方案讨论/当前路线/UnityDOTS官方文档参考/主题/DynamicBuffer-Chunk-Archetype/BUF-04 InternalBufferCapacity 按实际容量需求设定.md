# BUF-04: InternalBufferCapacity 按实际容量需求设定

**严重度**: P1
**Primary Owner**: DynamicBuffer-Chunk-Archetype
**来源**: `components-buffer-set-capacity.md`

## 规则声明
`[InternalBufferCapacity(N)]` 必须根据运行时实际容量需求设定，而非使用默认值。容量不足高频溢出 -> 增大 N；极少超过内联容量 -> 保持默认或减小。

## 为什么
容量过小导致高频溢出 -> 数据外部化 -> 每次访问多一次间接跳转。容量过大浪费 chunk 内宝贵空间 -> 每 chunk 容纳 entity 数减少 -> 插入成本增加。

## EX-GAS 诊断
ActiveEffectSlot buffer 设定 `InternalBufferCapacity=16` 而非默认，匹配一般 ASC 同时持有的 effect 数量。

## 检查方法
审查每个 `IBufferElementData` 的 `[InternalBufferCapacity]` 值是否匹配其运行时典型长度（通过 Debugger 的 peakUsage 指标验证）。
