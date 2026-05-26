# QRY-01: Hot path 优先 job 化；SystemAPI.Query 限于小规模/debug

**严重度**: P0
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — 核心概念 + JOB-01 节

## 规则声明
Hot path（每帧执行、entity 数 > 100）禁止使用 `SystemAPI.Query` foreach 或 `Run()`。必须使用 `IJobEntity` 或 `IJobChunk` 并标记 `[BurstCompile]`。`SystemAPI.Query` 仅限 Debugger 快照、Editor 工具、<100 entity 的 proof 验证场景。

## 为什么
主线程 foreach 先触发 sync point（等待所有相关 job 完成），再串行遍历，同时间内所有 worker 线程闲置。失去 Burst 编译后托管代码性能比 Burst 慢 10-100x。在 x50 规模下，一次 `SystemAPI.Query` 遍历的 sync + 串行执行成本可导致帧时间超标。

## EX-GAS 诊断
当前 `SHeadlessAutoChessDriver` 使用 `ToEntityArray` 全量扫描 + 大 `UnitSnapshot` 构造 → 违反本规则。搜索 Runtime Core system 目录中的 `SystemAPI.Query` 出现，标记为 proof-only 或要求迁移到 IJobEntity。

## 检查方法
- Grep 扫描 Runtime Core 目录中的 `SystemAPI.Query` 和 `.Run(` 调用
- Code review 确认新增遍历路径的遍历方式和 entity 规模
- 每小时级别自动扫描，发现违规标记为 block merge
