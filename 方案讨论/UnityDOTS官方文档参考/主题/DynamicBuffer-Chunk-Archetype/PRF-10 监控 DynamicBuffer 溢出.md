# PRF-10: 监控 DynamicBuffer 溢出

**严重度**: P1
**Primary Owner**: DynamicBuffer-Chunk-Archetype
**来源**: `components-buffer-introducing.html` / `components-buffer-set-capacity.md`

## 规则声明
每个关键 buffer 类型必须设定 `InternalBufferCapacity`，Debugger 报告 `bufferExternalizedRatio`，超过 30% 触发告警。

## 为什么
溢出后每次访问多一次间接内存跳转，大量外部化 buffer 导致缓存局部性丧失。静默性能退化 —— 不会报错，但速度越来越慢。溢出后数据外部化且永不自动迁回。即使缩容也不迁回。

## EX-GAS 诊断
Debugger 输出 buffer pressure 指标（`bufferLength/capacity/externalizedCount/spillRate/peakUsage/avgUsage`）。per-archetype chunk count、entity count、unused capacity。

## 检查方法
Per-buffer-type externalized 统计，`externalizedCount / totalBufferCount > 30%` 时告警。
