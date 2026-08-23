# 结构变化-ECB

## 定位

本文件是“结构变化-ECB”主题的唯一入口。官方机制以 [API 与 EX-GAS 解读](API与EX-GAS解读.md) 为准；原子文件表达 EX-GAS 规则或模式，必须按“类型/证据等级”理解，不能自动视为 Unity 官方硬性规范。

## 阅读顺序

1. 先读 API 解读，确认精确版本下的机制、生命周期和限制。
2. 再按任务读取原子规则；固定阈值只有附项目基准证据时才生效。
3. 跨主题规则以 [全局规则索引](../../元信息/90-规则编号索引.md) 的 Primary Owner 为准。

## 机制与规则

- [ECB-01: ECB 是延迟结构变化工具，不是 Gameplay Event Bus](<ECB-01.md>)
- [ECB-02: AppendToBuffer 前必须确保 Buffer 已存在](<ECB-02.md>)
- [ECB-03: ECB Playback 位置必须属于明确 SystemGroup Phase](<ECB-03.md>)
- [ECB-04: 区分 ECB System 管理与手工 ECB 生命周期](<ECB-04.md>)
- [SC-01: Hot Path 禁止直接结构变化](<SC-01.md>)
- [SC-02: 区分直接数据引用失效与缓存 Handle 更新](<SC-02.md>)
- [SC-03: 批量同类结构变化优先 EntityQuery Bulk](<SC-03.md>)

## 模式与案例

- [CASE-05: ECB 延迟结构变化](<CASE-05.md>)
- [CASE-21: ECB PlaybackPolicy.MultiPlayback + Deferred Entity Remapping](<CASE-21.md>)
- [CASE-25: IECBSingleton 自定义 ECB System](<CASE-25.md>)
- [CASE-34: EntityQueryCaptureMode.AtPlayback](<CASE-34.md>)
- [CASE-35: [ChunkIndexInQuery] sortKey 确定性 ECB 回放](<CASE-35.md>)
- [CASE-47: ECB AppendToBuffer + sortKey 多源并行 fan-in](<CASE-47.md>)

## 项目策略与性能规则

- [PRF-02: 禁止在 Hot Path 直接执行结构变化（P0 致命）](<PRF-02.md>)
- [PRF-04: Runtime Core 默认集中到约定 ECB Playback Phase](<PRF-04.md>)

## 维护约束

- 本主题只保存 Primary Owner 属于本主题的规则。
- 规则文件使用稳定的 `{CODE}.md` 路径；标题调整不改变路径。
- 新增或修改规则时同步更新本入口和全局规则索引。
- 官方来源必须指向当前 PackageCache 中真实存在的文件；项目策略必须与官方事实分栏表述。

返回 [知识库 README](../../README.md)。
