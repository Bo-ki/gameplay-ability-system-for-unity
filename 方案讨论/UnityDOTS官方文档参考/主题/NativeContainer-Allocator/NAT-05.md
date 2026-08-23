# NAT-05：关键 Native allocation 必须可观测；GC-free 不等于 allocation-free

**严重度**：P1
**Primary Owner**：NativeContainer-Allocator
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Collections `2.6.6`
**官方来源**：Collections `allocator-overview.md`、`allocator-rewindable.md`

## 规则声明
对已识别的热路径与长期 owner，Debugger/Profiler 必须能观察项目可靠采集的 allocation count/bytes、高水位、NativeStream item/buffer count 和生命周期异常。告警阈值必须记录场景与基准，不设无证据固定百分比。

## 为什么
无托管 GC allocation 不代表没有 Native 内存分配、块扩容、清零、复制或同步成本。不同容器/allocator 成本不同，不能仅以“GC-free”验收。

## EX-GAS 诊断
优先记录可直接观测的指标；若 API 不暴露精确容量，使用业务 item count/估算 bytes 并明确这是估算，不伪造 `NativeStreamSegmentCapacity`。

## 检查方法
在目标 Player 对比稳态和峰值；确认指标采集本身不显著扰动 gameplay hot path。
