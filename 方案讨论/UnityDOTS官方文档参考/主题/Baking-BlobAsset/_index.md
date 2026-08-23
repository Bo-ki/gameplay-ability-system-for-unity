# Baking-BlobAsset

## 定位

本文件是“Baking-BlobAsset”主题的唯一入口。官方机制以 [API 与 EX-GAS 解读](API与EX-GAS解读.md) 为准；原子文件表达 EX-GAS 规则或模式，必须按“类型/证据等级”理解，不能自动视为 Unity 官方硬性规范。

## 阅读顺序

1. 先读 API 解读，确认精确版本下的机制、生命周期和限制。
2. 再按任务读取原子规则；固定阈值只有附项目基准证据时才生效。
3. 跨主题规则以 [全局规则索引](../../元信息/90-规则编号索引.md) 的 Primary Owner 为准。

## 机制与规则

- [BAKE-01：Baker 只添加 ECS 组件；Authoring 读取必须走 Baker API](<BAKE-01.md>)
- [BAKE-02: Baker 必须无状态 — 单例实例、Bake() 多次调用且无序](<BAKE-02.md>)
- [BAKE-03：Baking System 必须显式保证输入链与结构变化可还原](<BAKE-03.md>)
- [BLOB-01: BlobAsset 用于 immutable 静态定义；runtime 只读](<BLOB-01.md>)
- [BLOB-02: BlobBuilder 在 Baking 或初始化时使用；不在 hot path 构建](<BLOB-02.md>)

## 模式与案例

- [CASE-07: Baker + Blob 定义管线](<CASE-07.md>)
- [CASE-24: BlobBuilder 标准构建模式](<CASE-24.md>)
- [CASE-31：Baker 的 DependsOn 在引用 Early-Out 之前](<CASE-31.md>)
- [CASE-32：TemporaryBakingType + Baking System Burst 计算](<CASE-32.md>)
- [CASE-39：Baker 无 ECS 输出依赖，但可读取 Authoring 数据](<CASE-39.md>)
- [CASE-40: Baker 必须无状态 — 禁止在 Baker 实例中缓存数据](<CASE-40.md>)
- [CASE-41：Baking System 显式维护增量还原](<CASE-41.md>)
- [CASE-44: Custom Section Metadata — 烘焙阶段向 Section Meta Entity 附加自定义元数据](<CASE-44.md>)

## 维护约束

- 本主题只保存 Primary Owner 属于本主题的规则。
- 规则文件使用稳定的 `{CODE}.md` 路径；标题调整不改变路径。
- 新增或修改规则时同步更新本入口和全局规则索引。
- 官方来源必须指向当前 PackageCache 中真实存在的文件；项目策略必须与官方事实分栏表述。

返回 [知识库 README](../../README.md)。
