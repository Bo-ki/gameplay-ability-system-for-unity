# 数据流-系统生命周期

## 定位

本文件是“数据流-系统生命周期”主题的唯一入口。官方机制以 [API 与 EX-GAS 解读](API与EX-GAS解读.md) 为准；原子文件表达 EX-GAS 规则或模式，必须按“类型/证据等级”理解，不能自动视为 Unity 官方硬性规范。

## 阅读顺序

1. 先读 API 解读，确认精确版本下的机制、生命周期和限制。
2. 再按任务读取原子规则；固定阈值只有附项目基准证据时才生效。
3. 跨主题规则以 [全局规则索引](../../元信息/90-规则编号索引.md) 的 Primary Owner 为准。

## 模式与案例

- [CASE-14: Write Group 写保护](<CASE-14.md>)
- [CASE-15: Cleanup Component 生命周期](<CASE-15.md>)
- [CASE-27: chunk.Has<T>() Chunk 级可选组件检查](<CASE-27.md>)
- [CASE-38: IJobEntityChunkBeginEnd Chunk 预评估](<CASE-38.md>)
- [CASE-45: System-Associated Entity Data](<CASE-45.md>)
- [CASE-46: SystemAPI.Query 不可存储复用](<CASE-46.md>)
- [CASE-48: Early-Out 不得丢失已调度 JobHandle](<CASE-48.md>)

## 项目策略与性能规则

- [PRF-13: 确定性输出不得依赖无序写入](<PRF-13.md>)
- [PRF-19: ComponentLookup/BufferLookup 随机访问与 Job 数据重叠导致竞态](<PRF-19.md>)
- [PRF-22: IJobChunk 中 Enableable Mask 处理](<PRF-22.md>)
- [PRF-25: 每个并行 Job 使用独立 ECB](<PRF-25.md>)
- [PRF-26: 读写数据分离到不同 Component](<PRF-26.md>)
- [PRF-27: 禁止手动调用其他数据处理 System 的 Update()](<PRF-27.md>)
- [PRF-29: Singleton API 不自动完成 Job 依赖](<PRF-29.md>)
- [PRF-30: SystemAPI.Query 中 DynamicBuffer<T> 默认读写](<PRF-30.md>)
- [PRF-32: 主线程 foreach 与 Run 按语义和 Profiler 选择](<PRF-32.md>)
- [PRF-33: 调度 Job 的 System 必须持有自身注册的 EntityQuery](<PRF-33.md>)

## 跨主题引用

- [CASE-31：Baker DependsOn Early-Out（Primary Owner：Baking-BlobAsset）](<../Baking-BlobAsset/CASE-31.md>)

## 维护约束

- 本主题只保存 Primary Owner 属于本主题的规则。
- 规则文件使用稳定的 `{CODE}.md` 路径；标题调整不改变路径。
- 新增或修改规则时同步更新本入口和全局规则索引。
- 官方来源必须指向当前 PackageCache 中真实存在的文件；项目策略必须与官方事实分栏表述。

返回 [知识库 README](../../README.md)。
