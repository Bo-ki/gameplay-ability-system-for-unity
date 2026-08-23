# Transform-层级

## 定位

本文件是“Transform-层级”主题的唯一入口。官方机制以 [API 与 EX-GAS 解读](API与EX-GAS解读.md) 为准；原子文件表达 EX-GAS 规则或模式，必须按“类型/证据等级”理解，不能自动视为 Unity 官方硬性规范。

## 阅读顺序

1. 先读 API 解读，确认精确版本下的机制、生命周期和限制。
2. 再按任务读取原子规则；固定阈值只有附项目基准证据时才生效。
3. 跨主题规则以 [全局规则索引](../../元信息/90-规则编号索引.md) 的 Primary Owner 为准。

## 机制与规则

- [TRF-01：Simulation 中按所需精度读取世界变换](<TRF-01.md>)
- [TRF-02：Child / PreviousParent 只由 ParentSystem 管理](<TRF-02.md>)
- [TRF-03：Child Buffer 顺序任意，不承载 sibling index](<TRF-03.md>)
- [TRF-04：TransformUsageFlags 声明最小需求，以烘焙结果验收](<TRF-04.md>)
- [TRF-05：Custom Transform 显式提供 LocalToWorld 并接管写入](<TRF-05.md>)

## 模式与案例

- [CASE-19：TransformUsageFlags 声明最小运行时需求](<CASE-19.md>)
- [CASE-29：Custom Transform via WriteGroup + ManualOverride](<CASE-29.md>)

## 项目策略与性能规则

- [PRF-18：按精度需求选择 LocalToWorld 或 ComputeWorldTransformMatrix](<PRF-18.md>)
- [PRF-28：不直接修改 Child / PreviousParent](<PRF-28.md>)
- [PRF-31：Child Buffer 没有 sibling 顺序语义](<PRF-31.md>)

## 维护约束

- 本主题只保存 Primary Owner 属于本主题的规则。
- 规则文件使用稳定的 `{CODE}.md` 路径；标题调整不改变路径。
- 新增或修改规则时同步更新本入口和全局规则索引。
- 官方来源必须指向当前 PackageCache 中真实存在的文件；项目策略必须与官方事实分栏表述。

返回 [知识库 README](../../README.md)。
