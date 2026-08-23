# QRY-01: Hot path 遍历方式由数据形态与 Profiler 决定

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `systems-systemapi-query.md`、`iterating-data-ijobentity.md`、`iterating-data-ijobchunk-implement.md`、`performance-sync-points.md`

## 规则声明
Hot path 默认评估 `IJobEntity` / `IJobChunk` 调度以利用 worker 与 Burst，但 `SystemAPI.Query` 和 `.Run()` 并非按实体数量禁止。选择必须同时考虑依赖是否已完成、单实体工作量、chunk 数、调度开销、结果是否必须立即可用，并以目标设备 Profiler 为准。

## 为什么
主线程 foreach 会在遍历前完成相关依赖，然后串行遍历，同时间内 worker 线程无法参与这段实体处理。即使 `SystemAPI.Query` 在合适 `ISystem` / Burst 上下文中可被 Burst 编译，它仍不是 worker-thread scheduled job；在 x50 规模下，一次 dependency completion + 主线程串行遍历成本可导致帧时间超标。

## EX-GAS 诊断
当前 `SHeadlessAutoChessDriver` 的 `ToEntityArray` 全量扫描与大 `UnitSnapshot` 构造应作为独立热点测量；若瓶颈来自快照构造或立即消费，单纯替换 foreach 不能自动消除成本。

## 检查方法
- Grep 扫描 Runtime Core 目录中的 `SystemAPI.Query` 和 `.Run(` 调用
- Code review 确认遍历方式、数据访问、依赖等待和性能证据
- 静态扫描只生成审计清单，不因出现 API 名称自动 block merge
