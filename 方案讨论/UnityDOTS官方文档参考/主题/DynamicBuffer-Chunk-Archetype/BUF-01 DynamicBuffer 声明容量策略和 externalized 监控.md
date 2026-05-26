# BUF-01: DynamicBuffer 声明容量策略和 externalized 监控

**严重度**: P0
**Primary Owner**: DynamicBuffer-Chunk-Archetype
**来源**: `components-buffer-introducing.html` / `components-buffer-set-capacity.md`

## 规则声明
每个 `DynamicBuffer` 声明必须通过 `[InternalBufferCapacity(N)]` 指定明确的内联容量策略，并在 Debugger 中输出 `bufferExternalizedRatio`。

## 为什么
默认 128 字节容量很容易溢出，溢出后数据永不自动迁回 chunk，导致 cache locality 永久丧失。不监控则 externalized ratio 随 entity 数量线性上升而无感知。

## EX-GAS 诊断
Debugger 必须输出 `bufferLength / bufferCapacity / externalizedCount / spillRate / peakUsage / avgUsage`。`externalizedCount / totalBufferCount > 30%` 触发告警。

## 检查方法
搜索所有 `IBufferElementData` 声明，确认有 `[InternalBufferCapacity]` 属性；检查 Debugger 是否输出 buffer pressure 指标。
