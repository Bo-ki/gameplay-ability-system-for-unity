# PRF-09: 注意 Query 操作的 Sync 触发

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `components-enableable-use.md`、`systems-entityquery-create.md`、`performance-sync-points.md`

## 规则声明
所有遵守 enableable 过滤的同步 `EntityQuery` 操作，在存在尚未完成且写入相关 enableable component 的 job 时，都会等待这些 job。`IgnoreFilter` 变体忽略过滤，因此不为 enabled 状态等待；`Async` 变体不在调用点阻塞，而是把相关 job 加为输入依赖。是否替换必须保持调用方需要的过滤语义。

## 为什么
无意识的依赖等待会占用主线程预算；等待时间取决于尚未完成的写 job，Unity 没有给出固定毫秒数。异步 Query 方法会调度收集 job，调用方只能在返回的依赖完成后访问结果容器。

**同步 vs 异步对比：**

| 方法类型 | Sync Point | 返回值 | 说明 |
|----------|------------|--------|------|
| 同步（`ToEntityArray`） | 依赖未完成时等待 | `NativeArray` | 立即返回可读结果 |
| 异步（`ToEntityArrayAsync`） | 调用点不等待 | `NativeList` | 返回列表在收集 job 完成前不可访问 |

`EntityQueryOptions.IgnoreComponentEnabledState` 可以绕过 enableable 过滤，**不需要 sync point**，效率更高。

## EX-GAS 诊断
审查 EventBus 与快照路径中的同步 Query，区分“必须立即得到结果”和“可把列表传给后续依赖 job”。不要为了规避等待而无条件改用 `IgnoreFilter`，否则会把 disabled entity 纳入结果。

## 检查方法
- 搜索 `CalculateEntityCount`、`ToEntityArray(`、`ToComponentDataArray(` 在 OnUpdate/Body 中的使用
- 判断 enableable 过滤是否生效
- 查看是否能替换为 Async 变体或 IgnoreFilter 选项
