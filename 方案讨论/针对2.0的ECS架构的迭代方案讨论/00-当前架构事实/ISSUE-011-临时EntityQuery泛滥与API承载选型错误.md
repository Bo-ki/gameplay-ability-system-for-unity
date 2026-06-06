# ISSUE-011 临时 EntityQuery 泛滥与 API 承载选型错误

> 最近复核：2026-06-06 | 状态：Active | 严重度：P1

## 当前结论

旧的“多处重复 ResolveCurrentFrame / 大量临时 EntityQuery”口径已经明显缓解。当前 `ToEntityArray` 和临时 query 主要不在 Runtime Core hot path 中扩散。

新的 API 承载风险是：singleton owner、global facade、bridge direct EntityManager、managed registry/helper 仍承担了过多 runtime 职责。

## 已缓解部分

1. 旧“22 个 ToEntityArray 热路径”不再成立。
2. 当前 `ToEntityArray()` 主要在 `GasRuntimeDebugger` 和 AutoChess catalog 低频安装。
3. 多数系统使用 `SystemAPI.QueryBuilder()` 创建 query。
4. current frame 通过 `GlobalTimer` singleton / known owner 驱动主链。
5. generated `AbilityCatalogCommitJob` 已从 `ComponentLookup.SetComponentEnabled` 随机访问切到 chunk-local `EnabledMask`；同实体 commit request 关闭不再作为 API 承载选型错误记录。

## 仍成立风险

1. `EffectCommandSpecStream.TryGetSingleton(EntityManager, out Entity)` 等 helper 仍暴露 global singleton 查找入口。
2. `GASManager.EntityManager` 是全局 facade，外部可直接读写 World。
3. `ASCCommandGateway` 是 facade；transient request entity 已退场，但 facade 仍可让外部直接驱动 runtime owner-local command。
4. `GEEffectCommandStreamComponent` singleton owner 承载 command/spec/delta/fact，多职责过载。
5. `AutoChessGasCoreBridge` 集中直接 EM 操作，需继续 owner 化。
6. generated active mutation 仍以 singleton stream + direct `EntityManager` helper 承载 active mutation，API 形态还没有收敛为明确的 owner-local store / deterministic merge。
7. generated active lifecycle 仍通过多个 `ComponentLookup.SetComponentEnabled(...)` 随机开关 enableable；它应继续向 owner-local state、`EnabledRefRW` 或 chunk `EnabledMask` 收口。

## 代码证据

| 事实 | 文件 |
|---|---|
| singleton stream helper | `GEEffectCommandSpecStream.cs` |
| global facade | `GASManager.cs` |
| command gateway | `ASCCommandGateway.cs` |
| ability commit enabled mask | `RuntimeAbilityActivation.gen.cs:56`, `:123-124`, `:138` |
| active mutation direct EM | `RuntimeActiveEffect.gen.cs:97-145` |
| active lifecycle random enableable | `RuntimeActiveEffect.gen.cs:395`, `:852`, `:871`, `:1090`, `:1100`, `:1120` |
| debugger queries | `GasRuntimeDebugger.cs` |
| demo catalog query | `AutoChessBattleDefinitionCatalogBuilder.cs` |
| demo bridge | `AutoChessGasCoreBridge.cs` |

## 退出条件

1. singleton stream owner 的职责拆分或容量/ordering 证明完成。
2. public facade 不再直接暴露 hot path write capability。
3. helper API 明确低频/初始化/observation/hot path 分类。
4. query 创建和 owner 查找纳入 frame budget evidence。
