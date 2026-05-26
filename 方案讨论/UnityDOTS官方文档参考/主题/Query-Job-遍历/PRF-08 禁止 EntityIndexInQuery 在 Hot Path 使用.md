# PRF-08: 禁止 EntityIndexInQuery 在 Hot Path 使用

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — PRF-08 节；`iterating-data-ijobchunk-implement.md`

## 规则声明
`[EntityIndexInQuery]` 的内部实现调用 `CalculateBaseEntityIndexArray`，不是简单索引访问。禁止在高频遍历中使用。改用 `[ChunkIndexInQuery]`、`[EntityIndexInChunk]` 或 owner-local buffer。

## 为什么
`CalculateBaseEntityIndexArrayAsync` per-entity 索引有额外数组构建步骤。不是 O(1) 简单索引。在 IJobEntity 的每次 `Execute` 中调用 `EntityIndexInQuery` 会显著增加开销，且随 entity 数量增大而恶化。

## EX-GAS 诊断
搜索 `EntityIndexInQuery` 在 Runtime Core 中的使用。AutoChess 遍历代码中可能隐含此属性。高频路径强制替换为 `EntityIndexInChunk` 或 owner-local buffer。

## 检查方法
- Grep `EntityIndexInQuery`、`EntityIndexInChunk`、`ChunkIndexInQuery`
- 确认使用场景和频率，标记 hot path 使用为违规
