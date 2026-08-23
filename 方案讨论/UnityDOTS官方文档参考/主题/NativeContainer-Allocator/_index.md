# NativeContainer-Allocator

## 定位

本文件是“NativeContainer-Allocator”主题的唯一入口。官方机制以 [API 与 EX-GAS 解读](API与EX-GAS解读.md) 为准；原子文件表达 EX-GAS 规则或模式，必须按“类型/证据等级”理解，不能自动视为 Unity 官方硬性规范。

## 阅读顺序

1. 先读 API 解读，确认精确版本下的机制、生命周期和限制。
2. 再按任务读取原子规则；固定阈值只有附项目基准证据时才生效。
3. 跨主题规则以 [全局规则索引](../../元信息/90-规则编号索引.md) 的 Primary Owner 为准。

## 机制与规则

- [NAT-01：每个 NativeContainer 必须说明 allocator、owner 和释放依赖](<NAT-01.md>)
- [NAT-02：确定性结果不得依赖 ParallelWriter 的物理写入顺序](<NAT-02.md>)
- [NAT-03：NativeStream 必须定义逻辑 buffer 映射、合并顺序和预算](<NAT-03.md>)
- [NAT-04：Persistent 容器必须有唯一 owner 和 teardown](<NAT-04.md>)
- [NAT-05：关键 Native allocation 必须可观测；GC-free 不等于 allocation-free](<NAT-05.md>)

## 模式与案例

- [CASE-12：NativeStream 并行 Fan-In 与确定性合并](<CASE-12.md>)
- [CASE-16：System Group Allocator](<CASE-16.md>)

## 项目策略与性能规则

- [PRF-14：任务交还必须说明 NativeContainer 所有权](<PRF-14.md>)
- [PRF-21：ExclusiveEntityTransaction 仅限 Secondary/Streaming World](<PRF-21.md>)
- [PRF-34：禁止对嵌套 NativeContainer 的 component 调度 IJobChunk/IJobEntity](<PRF-34.md>)

## 维护约束

- 本主题只保存 Primary Owner 属于本主题的规则。
- 规则文件使用稳定的 `{CODE}.md` 路径；标题调整不改变路径。
- 新增或修改规则时同步更新本入口和全局规则索引。
- 官方来源必须指向当前 PackageCache 中真实存在的文件；项目策略必须与官方事实分栏表述。

返回 [知识库 README](../../README.md)。
