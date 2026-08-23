# Enableable-Component选型

## 定位

本文件是“Enableable-Component选型”主题的唯一入口。官方机制以 [API 与 EX-GAS 解读](API与EX-GAS解读.md) 为准；原子文件表达 EX-GAS 规则或模式，必须按“类型/证据等级”理解，不能自动视为 Unity 官方硬性规范。

## 阅读顺序

1. 先读 API 解读，确认精确版本下的机制、生命周期和限制。
2. 再按任务读取原子规则；固定阈值只有附项目基准证据时才生效。
3. 跨主题规则以 [全局规则索引](../../元信息/90-规则编号索引.md) 的 Primary Owner 为准。

## 机制与规则

- [EN-01: 高频开关优先 Enableable，低频生命周期再考虑 Add/Remove](<EN-01.md>)
- [EN-02: Enableable 查询成本和同步等待进入性能诊断](<EN-02.md>)
- [EN-03: Random-Access Enableable 方法有额外开销；迭代优先](<EN-03.md>)

## 模式与案例

- [CASE-06: 高频状态切换用 EnabledRefRW](<CASE-06.md>)
- [CASE-20: 批量状态切换用 EnabledMask](<CASE-20.md>)
- [CASE-22: EntityQueryMask 快速检查 Archetype 是否匹配 Query](<CASE-22.md>)
- [CASE-26: ChunkEntityEnumerator 标准 Enableable 感知迭代](<CASE-26.md>)

## 项目策略与性能规则

- [PRF-03: 禁止用 Tag Component 做高频状态标记](<PRF-03.md>)

## 维护约束

- 本主题只保存 Primary Owner 属于本主题的规则。
- 规则文件使用稳定的 `{CODE}.md` 路径；标题调整不改变路径。
- 新增或修改规则时同步更新本入口和全局规则索引。
- 官方来源必须指向当前 PackageCache 中真实存在的文件；项目策略必须与官方事实分栏表述。

返回 [知识库 README](../../README.md)。
