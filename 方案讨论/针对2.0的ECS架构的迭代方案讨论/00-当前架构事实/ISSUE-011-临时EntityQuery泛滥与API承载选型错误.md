# ISSUE-011 临时 EntityQuery 泛滥与 API 承载选型错误

> 最近复核：2026-06-07 | 状态：Active | 严重度：P1

## 当前结论

旧的“多处重复 ResolveCurrentFrame / 大量临时 EntityQuery”口径已经明显缓解，但“current-frame fallback query 已删除”的旧验证结论也已经不符合当前代码。当前 `ToEntityArray` 和临时 query 必须按 owner 分类，而不能再只用数量判断。

本轮复核后，`Assets/GAS/Runtime/System` 与 `Assets/GAS/Generated/CodeGen/Runtime` 内 `SystemAPI.QueryBuilder().Build()` 扫描为 0；手写 Runtime Core stored query 已统一改为 `state.GetEntityQuery(EntityQueryDesc)`。因此本 issue 不能再以“多数 Runtime 系统 QueryBuilder 承载长期 query 生命周期”作为当前诊断。

新的 API 承载风险是：singleton owner、global facade、bridge direct EntityManager、managed registry/helper、generated active mutation random lookup store 仍承担了过多 runtime 职责。

## 已缓解部分

1. 旧“22 个 ToEntityArray 热路径”不再成立。
2. 当前 `ToEntityArray()` 精确命中 5 处：`GasRuntimeDebugger.cs:814/2014/2212` 属于 Debugger observation；`CueManagedLifecycleSystem.cs:39` 属于 Boundary managed presentation；`GASGlobalTimerSystem.cs:109` 属于 current-frame singleton fallback。`ActiveEffectStore` global index owner fallback 当前是 `CreateEntityQuery + CalculateEntityCount + GetSingletonEntity`，不再是 `ToEntityArray` 命中。
3. Runtime Core stored query 已从 `SystemAPI.QueryBuilder().Build()` 迁到 `state.GetEntityQuery(EntityQueryDesc)`，对齐 `PRF-33` / `CASE-46`。
4. current frame 主链由 `GlobalTimer` singleton 驱动，但 `GASRuntimeFrameContext.TryResolveSingletonGlobalTimer()` 仍有 `EntityManager.CreateEntityQuery + ToEntityArray` fallback，需要 owner 化或 cache miss 计数证明低频。
5. generated `AbilityCatalogCommitJob` 已从 `ComponentLookup.SetComponentEnabled` 随机访问切到 chunk-local `EnabledMask`；同实体 commit request 关闭和 auto-end request 写入不再作为 API 承载选型错误记录。
6. generated `GASActiveEffectMutationApplySystem` 已从旧 public/static `TryApplyActiveMutation(EntityManager, ...)` 路径迁入 scheduled `GEActiveEffectMutationApplyJob : IJob`；当前不再把它写作主线程 helper 问题。
7. generated `AbilityCatalogCommitSystem` 已移除 per-frame `NativeList<GECommandSeedRecord>(Allocator.TempJob)` scratch；command seed API 不再以临时 NativeList 承载单条输出。
8. ASC owner-local pending/destroying/dirty、ability lifecycle natural end、ability cleanup current markers、generated explicit remove pending 都已改为 chunk `EnabledMask`。
9. `IJobChunk.Execute` 内直接 `for (... chunk.Count ...)` 遍历在 Runtime/System 与 generated runtime 扫描为 0；需要处理 enabled mask 的 job 已改为 `ChunkEntityEnumerator`。
10. ASC dirty / active modifier present 已从 generated active effect / execution output random lookup toggle 收口到 `AttributeOwnerMarkerRequestBuffer` + ASC chunk applicator。
11. execution output applied marker 已从 `AppliedLookup.SetComponentEnabled` 收口到 effect chunk applicator。
12. `AbilityRuntimeActions.RequestCostGameplayEffect(Entity)`、`RequestCooldownGameplayEffect(Entity)` 与 `AttributeHelper.RecalculateCurrentValue(Entity, ...)`、`MarkCurrentValueDirty(Entity, ...)` 这类无参全局 `GASManager.EntityManager` facade 已删除；Runtime helper 调用方必须显式传入 `EntityManager` / buffer。
13. generated hot path static gate 已扩展 `GASManager.EntityManager` 规则，防止 `.gen.cs` 或 codegen 模板把全局 EntityManager facade 写回 Runtime Core。
14. `GameplayEffectComponentConfig` / `AbilityComponentConfig` 已删除 protected static `_entityManager => GASManager.EntityManager`，所有 config 子类的 `LoadTo...Entity` 改为显式 `EntityManager` 参数；`GameplayEffectEntityFactory` 在 prototype/runtime GE 构建时传入当前 world owner。

## 仍成立风险

1. `EffectCommandSpecStream.TryGetSingleton(EntityManager, out Entity)` 等 helper 仍暴露 global singleton 查找入口。
2. `GASManager.EntityManager` 仍是全局 facade，外部可直接读写 World；当前已删除 Runtime helper 无参写入口、config component 隐式全局写入口，并用 generated 防回流规则阻断模板回流。handwritten bootstrap/prototype/boundary/debugger 使用面仍需分类收口。
3. `ASCCommandPort` 是 command-only shell 入口；transient request entity 已退场，但该入口仍可让外部直接驱动 runtime owner-local command。
4. `GEEffectCommandStreamComponent` singleton owner 承载 command/spec/delta/fact，多职责过载。
5. `AutoChessGasCoreBridge` 集中直接 EM 操作，需继续 owner 化。
6. generated active mutation 仍以 singleton stream + serial `IJob` + BufferLookup/ComponentLookup random access 承载 active mutation，API 形态还没有收敛为明确的 owner-local store / deterministic merge。
7. ability lifecycle cross-entity marker、attribute owner marker 和 execution output applied marker 已分别改为 request buffer / owner chunk applicator；剩余 API 承载风险集中在 generated active mutation singleton stream、serial store owner、Buffer/ComponentLookup random access、剩余边界缓冲和 global facade。

## 代码证据

| 事实 | 文件 |
|---|---|
| singleton stream helper | `GEEffectCommandSpecStream.cs` |
| global facade | `GASManager.cs` |
| command port | `ASCCommandGateway.cs` |
| Runtime Core stored query 已迁到 SystemState owner | `ASCCommandBufferResolveSystem.cs`, `ASCDestroyFinalizeSystem.cs`, `AbilityLifecycleRequestSystem.cs`, `AbilityStateCleanupSystem.cs`, `AbilityStateTickSystem.cs`, `AbilityTryActivateSystem.cs`, `AttributeThresholdAbilityLifecycleRequestSystem.cs`, `AttributeRecalculateSystem.cs`, `GEExecutionCalculationSystem.cs`, `GEExecutionCalculationOutputModifierSystem.cs` |
| ability commit/end enabled mask | `RuntimeAbilityActivation.gen.cs:55-58`, `:115-117`, `:136-138` |
| active mutation singleton serial job | `RuntimeActiveEffect.gen.cs:80-145`, `:305-377` |
| ability lifecycle request aggregation | `AbilityLifecycleRequestBuffer`, `AbilityLifecycleRequestSystem`, `RuntimeActiveEffect.gen.cs` |
| attribute owner marker aggregation | `AttributeOwnerMarkerRequestBuffer`, `AttributeOwnerMarkerRequestSystem`, `RuntimeActiveEffect.gen.cs`, `GEExecutionCalculationOutputModifierSystem.cs` |
| execution output applied chunk applicator | `GEExecutionCalculationOutputModifierSystem.cs` |
| generated hot path gate | `GasGlueCodeGenPhases.cs:6238-6278`, `GasCodeGenValidationReport.md` |
| runtime helper global overload 已删除 | `AbilityRuntimeActions.cs`, `AttributeHelper.cs` |
| config component global facade 已删除 | `GameplayEffectComponentConfig.cs`, `AbilityComponentConfig.cs`, `GameplayEffectEntityFactory.cs`, GE/Ability static config component classes |
| debugger queries | `GasRuntimeDebugger.cs:813-814`, `:2014`, `:2212` |
| current-frame fallback query | `GASGlobalTimerSystem.cs:108-109` |
| active effect global index fallback query | `ActiveEffectStore.cs:1374-1382` |
| demo catalog direct init | `AutoChessBattleDefinitionCatalogBuilder.cs:51-58` |
| demo bridge | `AutoChessGasCoreBridge.cs` |

## 退出条件

1. singleton stream owner 的职责拆分或容量/ordering 证明完成。
2. public facade 不再直接暴露 hot path write capability。
3. helper API 明确低频/初始化/observation/hot path 分类。
4. query 创建和 owner 查找纳入 frame budget evidence。
5. generated/template 中 `GASManager.EntityManager` 回流由 static gate 阻断。
