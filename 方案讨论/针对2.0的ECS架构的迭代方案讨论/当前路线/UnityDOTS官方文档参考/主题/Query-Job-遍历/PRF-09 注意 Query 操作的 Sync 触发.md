# PRF-09: 注意 Query 操作的 Sync 触发

**严重度**: P1
**Primary Owner**: Query-Job-遍历
**来源**: `Query-Job-遍历.md` — PRF-09 节 + QRY-02 节；`performance-sync-points.md`、`components-enableable-use.html`

## 规则声明
以下操作在有 enableable component 且写 job 未完成时触发 sync point：`CalculateEntityCount()`、`ToEntityArray()`、`ToComponentDataArray()`、`GetSingleton<T>()`。使用 `IgnoreFilter` 或 `Async` 变体可规避。优先使用异步 Query 方法（`ToEntityArrayAsync`、`ToComponentDataArrayAsync`）。

## 为什么
无意识的 sync point 使主线程阻塞等待 worker 线程，丧失并行度。在 16.67ms 帧预算中，5 个意外 sync point 各 0.5ms = 15% 帧时间浪费。异步 Query 方法在 job 中执行查询，不阻塞主线程。

**同步 vs 异步对比：**

| 方法类型 | Sync Point | 返回值 | 说明 |
|----------|------------|--------|------|
| 同步（`ToEntityArray`） | 是 | `NativeArray` | 等待所有相关 job 完成 |
| 异步（`ToEntityArrayAsync`） | 否 | `NativeList` | 调度 job 完成操作 |

`EntityQueryOptions.IgnoreComponentEnabledState` 可以绕过 enableable 过滤，**不需要 sync point**，效率更高。

## EX-GAS 诊断
`SHeadlessAutoChessDriver` 使用 `GetComponentData` 而非 `GetSingletonRW` 的路径可能引入额外 sync。EventBus 的 query 操作也可能触发 sync。所有 Runtime Core 中的 Query 操作应优先使用异步变体。

## 检查方法
- 搜索 `CalculateEntityCount`、`ToEntityArray(`、`ToComponentDataArray(` 在 OnUpdate/Body 中的使用
- 判断 enableable 过滤是否生效
- 查看是否能替换为 Async 变体或 IgnoreFilter 选项
