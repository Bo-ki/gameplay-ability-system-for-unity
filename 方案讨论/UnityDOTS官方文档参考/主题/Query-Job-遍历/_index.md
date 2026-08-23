# Query-Job-遍历

## 定位

本文件是“Query-Job-遍历”主题的唯一入口。官方机制以 [API 与 EX-GAS 解读](API与EX-GAS解读.md) 为准；原子文件表达 EX-GAS 规则或模式，必须按“类型/证据等级”理解，不能自动视为 Unity 官方硬性规范。

## 阅读顺序

1. 先读 API 解读，确认精确版本下的机制、生命周期和限制。
2. 再按任务读取原子规则；固定阈值只有附项目基准证据时才生效。
3. 跨主题规则以 [全局规则索引](../../元信息/90-规则编号索引.md) 的 Primary Owner 为准。

## 机制与规则

- [JOB-01: 并行批处理说明 IJobEntity/IJobChunk/主线程选择理由](<JOB-01.md>)
- [JOB-02: 区分 job scheduling overhead、main-thread sync、Burst warmup](<JOB-02.md>)
- [JOB-03: 不使用 EntityIndexInQuery 作为 hot path 高频操作](<JOB-03.md>)
- [JOB-04: IJobEntity 的 Execute 参数明确 ref/in 语义](<JOB-04.md>)
- [QRY-01: Hot path 遍历方式由数据形态与 Profiler 决定](<QRY-01.md>)
- [QRY-02: Query contract 写清 All/Any/None/Disabled/ChangeFilter](<QRY-02.md>)
- [QRY-03: Optional 分支用 chunk 级判断](<QRY-03.md>)
- [QRY-04: 高频 random lookup 重构为 owner-local 或 chunk-local](<QRY-04.md>)

## 模式与案例

- [CASE-01: SystemAPI.Query 遍历](<CASE-01.md>)
- [CASE-02: IJobEntity 遍历](<CASE-02.md>)
- [CASE-03: IJobChunk 遍历](<CASE-03.md>)
- [CASE-13: Aspects（已废弃）](<CASE-13.md>)
- [CASE-18: chunk.DidChange 精细变更检测](<CASE-18.md>)

## 项目策略与性能规则

- [PRF-05: Hot Path 主线程遍历必须经 Profiler 验证](<PRF-05.md>)
- [PRF-06: 高频 Random Access Lookup 必须基于数据布局与 Profiler 审核](<PRF-06.md>)
- [PRF-08: EntityIndexInQuery 仅在需要 packed index 时使用](<PRF-08.md>)
- [PRF-09: 注意 Query 操作的 Sync 触发](<PRF-09.md>)
- [PRF-12: 审核 SharedComponent 使用](<PRF-12.md>)
- [PRF-17: 禁止使用 IAspect](<PRF-17.md>)
- [PRF-20: IJobEntity 显式 EntityQuery 的校验边界](<PRF-20.md>)
- [PRF-23: 禁止从 Job 内部启动新 Job](<PRF-23.md>)

## 跨主题引用

- [PRF-11：Prefab 与 Chunk 证据（Primary Owner：Prefab-Content管理）](<../Prefab-Content管理/PRF-11.md>)
- [PRF-13：确定性输出（Primary Owner：数据流-系统生命周期）](<../数据流-系统生命周期/PRF-13.md>)
- [PRF-19：Lookup 并行访问集合（Primary Owner：数据流-系统生命周期）](<../数据流-系统生命周期/PRF-19.md>)

## 维护约束

- 本主题只保存 Primary Owner 属于本主题的规则。
- 规则文件使用稳定的 `{CODE}.md` 路径；标题调整不改变路径。
- 新增或修改规则时同步更新本入口和全局规则索引。
- 官方来源必须指向当前 PackageCache 中真实存在的文件；项目策略必须与官方事实分栏表述。

返回 [知识库 README](../../README.md)。
