# JOB-03: 不使用 EntityIndexInQuery 作为 hot path 高频操作

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — PRF-08 节

## 规则声明
`[EntityIndexInQuery]` 的内部实现调用 `CalculateBaseEntityIndexArray`，不是简单索引访问。禁止在高频遍历中将其作为 parallel array 索引使用。

## 为什么
需要额外的内部数组构建步骤——不是 O(1) 简单索引。在 IJobEntity 的每次 `Execute` 中调用会显著增加开销。`CalculateBaseEntityIndexArrayAsync` per-entity 索引有额外数组构建步骤，官方文档明确指出其性能特征。

## EX-GAS 诊断
搜索 `EntityIndexInQuery` 在 Runtime Core 中的使用。AutoChess 遍历代码中可能隐含此属性。高频路径中改用 owner-local buffer 或 chunk-local counter。

## 检查方法
- Grep `EntityIndexInQuery`、`EntityIndexInChunk`、`ChunkIndexInQuery`
- 确认使用场景和频率，标记 hot path 中的 EntityIndexInQuery 为违规
