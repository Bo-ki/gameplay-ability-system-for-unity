# PRF-19: ComponentLookup/BufferLookup 随机访问与 Job 数据重叠导致竞态

**严重度**: P1
**Primary Owner**: 数据流-系统生命周期
**来源**: `systems-looking-up-data.md`、`common-errors.md`

## 规则声明

在 IJobEntity / IJobChunk 中使用 `ComponentLookup` 或 `BufferLookup` 随机访问其他 entity 的 component 时，若被访问数据与 job 直接遍历的 component type 重叠，会导致 race condition。必须确保随机访问的 entity 集合与直接遍历的 entity 集合无交集，否则标记 `[NativeDisableParallelForRestriction]` 并承担正确性责任。

## 为什么

两个 worker 线程可能同时通过 lookup 和直接遍历访问同一 entity 的同一 component。ECS 安全系统能检测并行写入冲突，但不能检测"lookup 读 + 遍历写"的微妙竞态。高频随机 lookup 成本远高于顺序遍历（cache miss x N）。

## EX-GAS 诊断

Effect application 中 `ComponentLookup<BAttribute>` 跨 entity 读取 source ASC 属性；ActiveEffect MMC 计算中 lookup source/target 属性。必须确保 lookup entity 集合与遍历 entity 集合分属不同组。

## 检查方法

Grep `ComponentLookup` / `BufferLookup` 在 IJobEntity / IJobChunk 中的使用。检查 lookup 的 entity 来源是否与 job 的 query 条件可能重叠。若无 `[NativeDisableParallelForRestriction]` 且 ECS 安全系统报警 → 确认竞态风险。
