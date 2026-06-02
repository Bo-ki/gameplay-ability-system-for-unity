# ISSUE-011 临时 EntityQuery 泛滥与 API 承载选型错误

> 最近复核：2026-06-02 | 状态：Active | 严重度：P1

## 当前结论

旧的“多处重复 ResolveCurrentFrame / 大量临时 EntityQuery”口径已经明显缓解。当前 `ToEntityArray` 和临时 query 主要不在 Runtime Core hot path 中扩散。

新的 API 承载风险是：singleton owner、global facade、bridge direct EntityManager、managed registry/helper 仍承担了过多 runtime 职责。

## 已缓解部分

1. 旧“22 个 ToEntityArray 热路径”不再成立。
2. 当前 `ToEntityArray()` 主要在 `GasRuntimeDebugger` 和 AutoChess catalog 低频安装。
3. 多数系统使用 `SystemAPI.QueryBuilder()` 创建 query。
4. current frame 通过 `GlobalTimer` singleton / known owner 驱动主链。

## 仍成立风险

1. `EffectCommandSpecStream.TryGetSingleton(EntityManager, out Entity)` 等 helper 仍暴露 global singleton 查找入口。
2. `GASManager.EntityManager` 是全局 facade，外部可直接读写 World。
3. `ASCCommandGateway` 是 facade，但仍同步创建 request entity。
4. `GEEffectCommandStreamComponent` singleton owner 承载 command/spec/delta/fact，多职责过载。
5. `AutoChessGasCoreBridge` 集中直接 EM 操作，需继续 owner 化。

## 代码证据

| 事实 | 文件 |
|---|---|
| singleton stream helper | `GEEffectCommandSpecStream.cs` |
| global facade | `GASManager.cs` |
| command gateway | `ASCCommandGateway.cs` |
| debugger queries | `GasRuntimeDebugger.cs` |
| demo catalog query | `AutoChessBattleDefinitionCatalogBuilder.cs` |
| demo bridge | `AutoChessGasCoreBridge.cs` |

## 退出条件

1. singleton stream owner 的职责拆分或容量/ordering 证明完成。
2. public facade 不再直接暴露 hot path write capability。
3. helper API 明确低频/初始化/observation/hot path 分类。
4. query 创建和 owner 查找纳入 frame budget evidence。
