# ISSUE-008 目标态 Spec 尚未充分 Unity Entities 机制化

> 最近复核：2026-06-02 | 状态：Active | 严重度：P1

## 当前结论

当前代码已经吸收了一部分 Entities 机制：FixedStep group、ComponentSystemGroup、ECB System singleton、BlobAssetReference、`SystemAPI.QueryBuilder`、`EntitiesJournaling`。但目标态仍需要继续把官方规则转成可执行的 query/job/baking/blob/structural evidence。

## 已机制化部分

1. `FixedStepSimulationSystemGroup` 下 5 段 GAS group。
2. `Begin/EndGASStructuralCommitECBSystem` 作为 structural commit gate。
3. `GASDefinitionCatalogBlob` 作为 runtime catalog。
4. `SystemAPI.QueryBuilder()` 用于部分 query 预创建。
5. `GasRuntimeOfficialToolDiff` 使用 `EntitiesJournaling`。

## 仍不足部分

1. `SystemAPI.Query` 主线程 foreach 仍被用在 hot path proof 中。
2. `Complete()` 未被系统性治理。
3. 实际 `Baker<T>` / `BlobAssetStore` 还没有落到当前 Runtime/AutoChess 代码树。
4. NativeStream / deterministic merge 仍是目标，不是当前事实。
5. Contract-only 文件未和 runtime evidence 强绑定。

## 退出条件

1. 每条 DOTS 规则都能映射到当前代码检查项。
2. Contract 文件只作为约束，完成度由 runtime registration、Profiler、Journaling、battle hash 证明。
3. Baking/Blob/Query/Job/Structural 规则有独立 evidence gate。
