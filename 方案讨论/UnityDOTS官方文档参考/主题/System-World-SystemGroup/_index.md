# System-World-SystemGroup

## 定位

本文件是“System-World-SystemGroup”主题的唯一入口。官方机制以 [API 与 EX-GAS 解读](API与EX-GAS解读.md) 为准；原子文件表达 EX-GAS 规则或模式，必须按“类型/证据等级”理解，不能自动视为 Unity 官方硬性规范。

## 阅读顺序

1. 先读 API 解读，确认精确版本下的机制、生命周期和限制。
2. 再按任务读取原子规则；固定阈值只有附项目基准证据时才生效。
3. 跨主题规则以 [全局规则索引](../../元信息/90-规则编号索引.md) 的 Primary Owner 为准。

## 机制与规则

- [SYS-01: 权威计算落在 ECS System/Job 数据流，禁止托管 manager 驱动](<SYS-01.md>)
- [SYS-02: SystemGroup 是 phase owner，禁止手写 Tick 顺序](<SYS-02.md>)
- [SYS-03: 系统数量是成本源，避免不必要的 system 拆分](<SYS-03.md>)
- [SYS-04: Core/Physics/Presentation 成本分组统计](<SYS-04.md>)
- [SYS-05: World 边界：Debugger/Demo/Presentation 只能通过 Boundary 观察 Core](<SYS-05.md>)

## 模式与案例

- [CASE-17: ICustomBootstrap 多世界](<CASE-17.md>)

## 项目策略与性能规则

- [PRF-07: 避免不必要的 System 拆分](<PRF-07.md>)
- [PRF-16: System 创建依赖用 CreateAfter；ISystem 优于 SystemBase](<PRF-16.md>)

## 跨主题引用

- [CASE-16：System Group Allocator（Primary Owner：NativeContainer-Allocator）](<../NativeContainer-Allocator/CASE-16.md>)

## 维护约束

- 本主题只保存 Primary Owner 属于本主题的规则。
- 规则文件使用稳定的 `{CODE}.md` 路径；标题调整不改变路径。
- 新增或修改规则时同步更新本入口和全局规则索引。
- 官方来源必须指向当前 PackageCache 中真实存在的文件；项目策略必须与官方事实分栏表述。

返回 [知识库 README](../../README.md)。
