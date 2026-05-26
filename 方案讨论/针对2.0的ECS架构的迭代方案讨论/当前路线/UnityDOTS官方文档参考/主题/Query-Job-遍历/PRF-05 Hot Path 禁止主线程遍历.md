# PRF-05: Hot Path 禁止主线程遍历

**严重度**: P0
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — PRF-05 节；`performance-sync-points.md`

## 规则声明
每帧执行且 entity 数 > 100 的遍历路径，禁止使用主线程 `SystemAPI.Query` 或 `.Run()`。必须使用 `IJobEntity` 或 `IJobChunk` + Burst。这是 P0 级强约束，违规需立即修复。

## 为什么
主线程遍历触发 sync point → 等待所有 worker 线程完成 → 丧失并行度 → 帧时间随着 entity 数量线性增长而非 sub-linear。在 x50 规模下，一次 `ToEntityArray` 全量扫描的 sync 成本可达毫秒级。`Run` 和 idiomatic foreach 导致主线程同步阻塞是官方文档明确警告的性能陷阱。

## EX-GAS 诊断
当前 `SHeadlessAutoChessDriver` 使用 `ToEntityArray` 全量扫描 + 大 `UnitSnapshot` 构造是 x50 下的 top 热点。目标态改为异步 query 或 stable read model。搜索 Runtime Core 目录中的 `SystemAPI.Query` 和 `.Run(`，确认每个出现处的 entity 规模和执行频率。

## 检查方法
- 搜索 Runtime Core 目录中的 `SystemAPI.Query` 和 `.Run(`
- 确认每个出现处的 entity 规模和执行频率
- 违规标记为 block merge，P0 级即时修复
