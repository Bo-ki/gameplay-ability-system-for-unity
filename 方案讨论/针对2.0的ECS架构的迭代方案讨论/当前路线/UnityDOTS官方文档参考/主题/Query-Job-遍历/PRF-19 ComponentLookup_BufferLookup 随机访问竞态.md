# PRF-19: ComponentLookup/BufferLookup 随机访问竞态

**严重度**: P1
**Primary Owner**: 数据流-系统生命周期（本主题为跨主题引用）
**来源**: `Query-Job-遍历.md` — PRF-11 节 + PRF-19 节；`systems-looking-up-data.md`

## 规则声明
在 IJobEntity/IJobChunk 中使用 `ComponentLookup` 或 `BufferLookup` 随机访问其他 entity 的 component 时，若被访问数据与 job 直接遍历的 component type 重叠，会导致 race condition。必须确保随机访问的 entity 集合与直接遍历的 entity 集合无交集，否则标记 `[NativeDisableParallelForRestriction]` 并承担正确性责任。

## 为什么
两个 worker 线程可能同时通过 lookup 和直接遍历访问同一 entity 的同一 component。ECS 安全系统能检测并行写入冲突，但不能检测"lookup 读 + 遍历写"的微妙竞态。官方文档明确警告："If the data you look up overlaps the data you want to read and write to in the job, then random access might lead to race conditions."

## EX-GAS 诊断
Effect application 中 `ComponentLookup<BAttribute>` 跨 entity 读取 source ASC 属性、ActiveEffect MMC 计算中 lookup source/target 属性。若 lookup entity 与遍历 entity 同组 → 竞态风险。当前规模下可能不触发，但 x50 规模时 entity 数量增加会提高重叠概率。

## 检查方法
- Grep `ComponentLookup`/`BufferLookup` 在 IJobEntity/IJobChunk 中的使用
- 检查 lookup 的 entity 来源是否与 job 的 query 条件可能重叠
- Code review 时检查所有 ComponentLookup 的使用，确认目标 entity 集合的包含关系
