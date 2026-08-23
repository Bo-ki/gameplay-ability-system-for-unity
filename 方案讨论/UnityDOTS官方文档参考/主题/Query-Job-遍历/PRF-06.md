# PRF-06: 高频 Random Access Lookup 必须基于数据布局与 Profiler 审核

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `iterating-data-ijobchunk-implement.md`、`components-enableable-use.md`

## 规则声明
`ComponentLookup` / `BufferLookup` 是官方支持的任意实体访问方式，在实体依赖其他实体数据时可以使用。热点循环中若随机访问成为实测瓶颈，应评估改为 query/chunk 顺序访问、owner-local 数据或预先分组；不得仅凭出现次数或实体数量判违规。

## 为什么
Lookup 需要从 `Entity` 定位其 chunk 与 chunk 内索引，访问局部性通常弱于顺序 chunk 迭代。Unity 只定性说明 random access 额外开销更高，并未给出“哈希查找”实现承诺或固定的 10–20 倍成本；具体差异依赖缓存命中、组件布局和平台。

## EX-GAS 诊断
Effect application 中 `ComponentLookup<BAttribute>` 跨 entity 读取 source ASC 属性、ActiveEffect MMC 计算中 lookup source/target 属性。应先确认访问集合、局部性和 Profiler 热点；owner-local buffer 只有在不制造重复数据和同步复杂度时才是候选方案。

## 检查方法
- Grep `TryGetComponent` 和 `TryGetBuffer` 在 IJobEntity/IJobChunk 中的使用
- 评估 lookup 频率和 entity 规模
- 对实测热点记录替代布局的基准结果，再决定是否整改
