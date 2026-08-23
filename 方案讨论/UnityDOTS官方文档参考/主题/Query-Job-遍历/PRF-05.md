# PRF-05: Hot Path 主线程遍历必须经 Profiler 验证

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `systems-systemapi-query.md`、`performance-sync-points.md`、`iterating-data-ijobentity.md`

## 规则声明
Runtime Core hot path 中使用主线程 `SystemAPI.Query` 或 job `.Run()` 时，必须用目标设备 Profiler 证明主线程遍历本体与依赖等待均满足预算。出现稳定的主线程瓶颈且工作量足以摊薄调度成本时，改用可 Burst 的 `IJobEntity` / `IJobChunk` 调度。

## 为什么
`SystemAPI.Query` foreach 会在遍历前完成必要的读写依赖，`.Run()` 会完成该 job 的输入依赖；只有依赖尚未完成时才产生等待。二者均在主线程执行遍历，不提供 worker 并行，但对很小或不可并行的工作，调度 job 也可能更慢，因此不能用实体数量作通用裁决。

## EX-GAS 诊断
当前 `SHeadlessAutoChessDriver` 使用 `ToEntityArray` 全量扫描 + 大 `UnitSnapshot` 构造是 x50 下的 top 热点。目标态改为异步 query 或 stable read model。搜索 Runtime Core 目录中的 `SystemAPI.Query` 和 `.Run(`，确认每个出现处的 entity 规模和执行频率。

## 检查方法
- 搜索 Runtime Core 目录中的 `SystemAPI.Query` 和 `.Run(`
- 记录每个出现处的执行频率、匹配量、单实体工作量及主线程等待样本
- 只有超过项目预算且有可验证替代方案时才阻断合并
