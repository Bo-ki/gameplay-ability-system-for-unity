# NAT-03：NativeStream 必须定义逻辑 buffer 映射、合并顺序和预算

**严重度**：P0
**Primary Owner**：NativeContainer-Allocator
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Collections `2.6.6`
**官方来源**：Collections `parallel-readers.md`、`NativeStream.cs` API 注释

## 规则声明
每个 NativeStream 使用点必须定义：

1. `bufferCount` 及 buffer index 到稳定逻辑输入的映射；
2. 单 buffer 的唯一 writer 与 Begin/End 边界；
3. producer → reader → consumer → Dispose 的 JobHandle 链；
4. 按 index 合并是否已经满足业务顺序；否则使用稳定全序排序；
5. 最大 buffer/item/byte 预算和溢出处理。

## 为什么
`ForEachCount` 等于构造的固定 buffer 数，不会随线程数波动。NativeStream 隔离 buffer 可以避免普通 ParallelWriter 的调度顺序竞争，但不会自动定义业务 total order，也没有“每 segment 固定容量”设置；写入超过当前块时会链接新块。

## EX-GAS 诊断
EffectCommand 默认采用一稳定输入索引一 buffer。Debugger 记录 buffer count、item count、估算 bytes、峰值和 merge/sort 成本；阈值由场景基准确定。

## 检查方法
验证 index 不越界/不重复 Begin；读在写完成后；Dispose 依赖最后消费者；排序 comparer 覆盖 tie-breaker。
