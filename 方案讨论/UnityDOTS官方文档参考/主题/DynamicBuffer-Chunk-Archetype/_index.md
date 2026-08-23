# DynamicBuffer-Chunk-Archetype

## 定位

本文件是“DynamicBuffer-Chunk-Archetype”主题的唯一入口。官方机制以 [API 与 EX-GAS 解读](API与EX-GAS解读.md) 为准；原子文件表达 EX-GAS 规则或模式，必须按“类型/证据等级”理解，不能自动视为 Unity 官方硬性规范。

## 阅读顺序

1. 先读 API 解读，确认精确版本下的机制、生命周期和限制。
2. 再按任务读取原子规则；固定阈值只有附项目基准证据时才生效。
3. 跨主题规则以 [全局规则索引](../../元信息/90-规则编号索引.md) 的 Primary Owner 为准。

## 机制与规则

- [BUF-01：DynamicBuffer 必须记录容量策略并监控关键类型](<BUF-01.md>)
- [BUF-02：多 writer fan-in 不直接并行写单一全局 DynamicBuffer](<BUF-02.md>)
- [BUF-03：结构变化后必须重新获取 DynamicBuffer 引用](<BUF-03.md>)
- [BUF-04：InternalBufferCapacity 必须按长度分布与 chunk 成本选择](<BUF-04.md>)

## 模式与案例

- [CASE-04：Owner-local DynamicBuffer](<CASE-04.md>)
- [CASE-23：在 IJobChunk 中使用 BufferAccessor](<CASE-23.md>)
- [CASE-28：Chunk Component](<CASE-28.md>)
- [CASE-33：ComponentTypeSet 批量结构变化](<CASE-33.md>)
- [CASE-36：DynamicBuffer.Reinterpret](<CASE-36.md>)

## 项目策略与性能规则

- [PRF-01：无独立身份的高频瞬时记录不默认建 Entity](<PRF-01.md>)
- [PRF-10：监控关键 DynamicBuffer 的容量与 chunk 代价](<PRF-10.md>)
- [PRF-24：已知最终布局时避免逐 Component 构建 Entity](<PRF-24.md>)

## 维护约束

- 本主题只保存 Primary Owner 属于本主题的规则。
- 规则文件使用稳定的 `{CODE}.md` 路径；标题调整不改变路径。
- 新增或修改规则时同步更新本入口和全局规则索引。
- 官方来源必须指向当前 PackageCache 中真实存在的文件；项目策略必须与官方事实分栏表述。

返回 [知识库 README](../../README.md)。
