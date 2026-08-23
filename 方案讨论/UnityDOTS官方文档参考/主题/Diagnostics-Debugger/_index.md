# Diagnostics-Debugger

## 定位

本文件是“Diagnostics-Debugger”主题的唯一入口。官方机制以 [API 与 EX-GAS 解读](API与EX-GAS解读.md) 为准；原子文件表达 EX-GAS 规则或模式，必须按“类型/证据等级”理解，不能自动视为 Unity 官方硬性规范。

## 阅读顺序

1. 先读 API 解读，确认精确版本下的机制、生命周期和限制。
2. 再按任务读取原子规则；固定阈值只有附项目基准证据时才生效。
3. 跨主题规则以 [全局规则索引](../../元信息/90-规则编号索引.md) 的 Primary Owner 为准。

## 机制与规则

- [DBG-01: 三层诊断体系各有明确职责边界，不可互相替代](<DBG-01.md>)
- [DBG-02: Entities Journaling 必须在性能测试中关闭](<DBG-02.md>)
- [DBG-03: Structural Changes Profiler 是热点定位工具，不能替代自动验收](<DBG-03.md>)
- [DBG-04: 手写 Debugger 只能记录计数，不做字符串拼接、不分配托管内存](<DBG-04.md>)
- [DBG-05: 性能报告必须按 cost 分组统计，禁止混合归因](<DBG-05.md>)

## 维护约束

- 本主题只保存 Primary Owner 属于本主题的规则。
- 规则文件使用稳定的 `{CODE}.md` 路径；标题调整不改变路径。
- 新增或修改规则时同步更新本入口和全局规则索引。
- 官方来源必须指向当前 PackageCache 中真实存在的文件；项目策略必须与官方事实分栏表述。

返回 [知识库 README](../../README.md)。
