# PRF-19: ComponentLookup/BufferLookup 随机访问与 Job 数据重叠导致竞态

**严重度**: P1
**Primary Owner**: 数据流-系统生命周期
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3 / Entities 1.4.6
**官方来源**: `systems-looking-up-data.md`、`common-errors.md`

## 规则声明

在并行 IJobEntity / IJobChunk 中通过 `ComponentLookup` 或 `BufferLookup` 写入任意 entity 时，必须证明不同 worker 的写集合互不重叠，并与其他并发读写保持依赖安全。`[NativeDisableParallelForRestriction]` 只关闭容器的并行访问限制检查，不会消除真实竞态；仅在已经证明访问集合不重叠时才能使用。

## 为什么

两个 worker 线程可能通过 lookup 和直接遍历同时访问同一 entity 的同一 component。安全系统会检测许多容器级冲突，但禁用限制后无法替你证明 entity 集合互斥。Lookup 是官方支持的随机访问方式，但需要定位目标 entity 数据且局部性通常弱于线性迭代；实际差异取决于数据布局和硬件，不能写成固定倍率。

## EX-GAS 诊断

Effect application 中通过 lookup 跨 entity 读取 source ASC 属性，可以将 lookup 声明为 `[ReadOnly]` 并正确链接依赖。若要随机写 source/target 属性，则必须保证目标集合互斥，或改成 owner-local/chunk-local 顺序处理。

## 检查方法

Grep `ComponentLookup` / `BufferLookup` 在 IJobEntity / IJobChunk 中的使用。检查 lookup 的 entity 来源是否与 job 的 query 条件可能重叠。若无 `[NativeDisableParallelForRestriction]` 且 ECS 安全系统报警 → 确认竞态风险。
