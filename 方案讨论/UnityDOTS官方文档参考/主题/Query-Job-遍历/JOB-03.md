# JOB-03: 不使用 EntityIndexInQuery 作为 hot path 高频操作

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `iterating-data-ijobentity.md`、`iterating-data-ijobchunk-implement.md`

## 规则声明
`[EntityIndexInQuery]` 需要为匹配 chunk 预先计算 base entity index 数组。只有确实需要“过滤后连续 packed index”时才使用；若只需要 ECB sort key 或 chunk 内索引，优先使用 `[ChunkIndexInQuery]` / `[EntityIndexInChunk]`。

## 为什么
调度包含该参数的 `IJobEntity` 时，生成代码会调度/执行一次 `CalculateBaseEntityIndexArray[Async]` 来构建 chunk offset；不是在每次 `Execute` 内重新计算。该准备步骤会增加时间和临时内存，但不意味着该 API 在所有 hot path 中都不可用。

## EX-GAS 诊断
搜索 `EntityIndexInQuery` 在 Runtime Core 中的使用，确认消费者是否真的要求 packed index，并用 Profiler 对比准备成本；仅为 ECB 排序时改用 `ChunkIndexInQuery`。

## 检查方法
- Grep `EntityIndexInQuery`、`EntityIndexInChunk`、`ChunkIndexInQuery`
- 确认使用场景、频率和 Profiler 数据；无法说明 packed index 需求时标记为待整改
