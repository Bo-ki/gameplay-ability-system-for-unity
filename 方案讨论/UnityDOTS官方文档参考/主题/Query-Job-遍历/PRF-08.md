# PRF-08: EntityIndexInQuery 仅在需要 packed index 时使用

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `iterating-data-ijobentity.md`、`iterating-data-ijobchunk-implement.md`

## 规则声明
`[EntityIndexInQuery]` 会引入 `CalculateBaseEntityIndexArray[Async]` 准备步骤。需要过滤后连续 packed index（例如写入紧凑输出数组）时可以使用；只需要 ECB sort key 或 chunk 内索引时改用 `[ChunkIndexInQuery]` / `[EntityIndexInChunk]`。

## 为什么
生成代码在 job 调度阶段为每个匹配 chunk 计算 base index，并在 `Execute` 中用 base index 加 chunk 内索引得到 packed index；不是每个 `Execute` 都调用一次计算 API。额外成本是否显著必须由 Profiler 判断。

## EX-GAS 诊断
搜索 `EntityIndexInQuery` 在 Runtime Core 中的使用。若只是排序或局部索引则替换；若消费者要求紧凑全局索引则保留并记录理由。

## 检查方法
- Grep `EntityIndexInQuery`、`EntityIndexInChunk`、`ChunkIndexInQuery`
- 确认使用场景和频率，标记 hot path 使用为违规
