# ISSUE-005 Generated 链路已反哺但需防主调度回流

> 最近复核：2026-06-08 | 状态：Mitigated | 严重度：P1

## 当前结论

旧标题已经不准确：generated 链路现在已经反哺 Runtime Core，但主调度口径已经再次收窄。当前问题已经从“generated runtime 是主链 owner”转为“generated catalog / pure glue 是正向资产，generated lifecycle / query / ECB / NativeContainer owner 只能作为防回流审查对象”。

## 已缓解部分

1. `GASSystemScheduleContract` 当前直接注册 hand-written Runtime system arrays，`GeneratedCommandResolveSystemTypeNames` 与 `GeneratedCoreSimulationSystemTypeNames` 均为空数组；`RuntimeSystemRegistration.gen.cs` 当前已退场。缺失 generated type 的 fail-fast 只保留为防回流机制，不能再写成当前主调度来源。
2. `GASDefinitionCatalogBlob` 被 generated runtime 读取。
3. Ability catalog commit、GE command normalize、spec build、attribute reduce、active effect mutation/tick/remove 都已进入执行链。
4. `GasGlueCodeGenPhases.WriteRuntimeAbilityActivationSystem()` 已同步到当前 owner-local commit marker 形态，不再生成 `NativeStream + state.Dependency.Complete() + Allocator.Temp ECB.Playback` 的旧 ability commit 模板。
5. `RuntimeActiveEffectSystemsTemplate` 已与当前 `RuntimeActiveEffect.gen.cs` 对齐，不再在 codegen 层回滚到 `new EntityCommandBuffer(Allocator.Temp)` / 手动 `Playback(em)` 形态。
6. `RuntimeEffectInstant.gen.cs` 当前 spec-build 与 attribute reduce 已由 codegen 模板生成 scheduled `IJob` 路径；不能再把它们写作主线程 spec/reduce 残留。
7. `AbilityCatalogCommitJob` 已使用 `CommitRequestTypeHandle` + `chunk.GetEnabledMask(ref ...)` 关闭当前 chunk 内 commit request，并用 `EndRequestTypeHandle` + chunk `EnabledMask` 写 auto-end 请求，不再使用 `ComponentLookup.SetComponentEnabled` 做当前 ability 同实体随机 enableable 开关。
8. `RuntimeEffectInstant.gen.cs` 已输出 `using Unity.Burst`，`InstantSpecBuildJob` 与 `AttributeSetReduceApplyJob` 均标注 `[BurstCompile]`。
9. `CanBuildInstantSpec` 已允许 `ModifierCount > 0 || GameplayCueCode > 0`，cue-only instant GE 不再被 spec build 丢弃。
10. `IsDestroyingAsc` 已按 `ASCDestroyingComponent` enabled bit 判定销毁态，不再把默认 disabled 的正常 ASC 误判为 destroying。
11. `GASActiveEffectMutationApplySystem` 的 active mutation gather / chunk apply 主体已转到 hand-written Runtime owner；`RuntimeActiveEffect.gen.cs` 当前是 12 行 marker。旧 public/static `TryApplyActiveMutation(EntityManager, ...)` helper 与旧 serial `GEActiveEffectMutationApplyJob : IJob` 已退场。
12. `AbilityCatalogCommitSystem` 已移除 per-frame `NativeList<GECommandSeedRecord>(Allocator.TempJob)` scratch；模板现在直接构造单条 seed 并 append command。
13. `GasCodeGenValidationReport.md` 已加入 generated runtime hot path gate；当前 `GeneratedHotPathRegressionHits: 0`，可阻断 `Complete()`、`.Run()`、legacy EventBus writer、旧 active mutation helper、ability seed scratch、hot path `Allocator.Temp`、`HasComponent<ASCDestroyingComponent>`、旧 ability lifecycle lookup、旧 `AttributeDirtyLookup` / `ActiveModifierPresentLookup`、generated/template `GASManager.EntityManager` 回流。
14. generated active effect granted-ability cleanup 不再直接写 `AbilityCancelRequestLookup` / `AbilityDestroyOnCleanupLookup`；模板和生成结果均改为写 `AbilityLifecycleRequestBuffer`，由 `AbilityLifecycleRequestSystem` 在 ability chunk 内统一应用 cancel/destroy-on-cleanup marker。
15. generated active effect 对 ASC dirty / active modifier present 的写入不再使用 `AttributeDirtyLookup` / `ActiveModifierPresentLookup`；模板和生成结果均改为写 `AttributeOwnerMarkerRequestBuffer`，由 `AttributeOwnerMarkerRequestSystem` 在 ASC chunk 内统一应用。
16. `GASGeneratedDefinitionCatalogBuilder.BuildCatalog()` 已从 runtime-visible `DefinitionCatalog.gen.cs` 退场；generated runtime 只保留 `GASGeneratedDefinitionCatalogData.Populate(ref BlobBuilder, ref GASDefinitionCatalogBlob)` 作为数据填充 glue。Blob materialization 分别由 Editor/Baking 的 `GASGeneratedDefinitionCatalogBaker` 和 AutoChess bootstrap install owner 持有，`GasCodeGenValidationReport` 已把 generated runtime 中的 `new BlobBuilder(` / `CreateBlobAssetReference<` 视为 blocking boundary hit。

## 新风险

1. generated catalog / pure glue 可以反哺 Runtime Core，但 generated output 不得重新拥有 runtime lifecycle、query、ECB、NativeContainer 或 system registration。
2. `Complete()` 已清零；`GASActiveEffectMutationApplySystem` 已进入 gather + ASC chunk-local apply，active store、slot、mutation、tag、attribute、modifier 与 ability buffer 在 owner chunk 内直接访问；相邻 pending AttributeDelta owner-local apply 也已进入 ASC chunk-local path，旧 stream migration fallback 已退出。剩余联动风险是 singleton command carrier、gather serial command scan、SourceAttribute 跨 owner snapshot lane 缺口，以及 instant / execution delta record 仍经 singleton stream carrier。
3. generated active lifecycle 的 ability cancel/destroy cleanup 已通过 frame-local lifecycle request buffer 收口；active modifier present / attribute dirty 已通过 frame-local attribute owner marker request buffer 收口。当前 `ComponentLookup.SetComponentEnabled(...)` 随机 enableable 风险不再集中于这些 marker，而是需要继续扫描未来模板是否回流。
4. hand-written active effect lifecycle 与 ExecutionCalculation / Attribute / Fact projection 的 ordering、capacity、deterministic merge 需要证据；stale generated lifecycle 文件名 / 改名回流必须单独进入防回流扫描。
5. generated catalog lookup 的 revision/lifecycle owner 仍需要明确；本轮只把 Blob materialization owner 从 generated runtime artifact 退出，尚未完成长期 `DefinitionCatalogLifetime` capability、runtime-created catalog entity、Blob dispose owner 和 catalog install/uninstall 证据闭环。
6. codegen static validation 已有第一道 gate，并覆盖 ability lifecycle / ASC dirty-present 旧 lookup、global EntityManager facade、旧 active mutation serial apply、旧 active mutation random lookup 估算与 chunk buffer/lookup alias 回流；后续还需要继续扩展到 singleton command carrier 阈值、pending AttributeDelta owner materialization / stream fallback 回流、generated delta record carrier 证据、无 `[BurstCompile]` hot job、Temp ECB playback 和 `state.Dependency.Complete()`。

## 代码证据

| 事实 | 文件 |
|---|---|
| generated registration 防回流 | `GASSystemScheduleContract.cs` 当前 `GeneratedCommandResolveSystemTypeNames` / `GeneratedCoreSimulationSystemTypeNames` 为空数组；`AddSystemsByTypeName(...)` 缺失 type 会 fail-fast |
| catalog lookup | `DefinitionCatalog.gen.cs` |
| runtime glue | `RuntimeDefinitionGlue.gen.cs` |
| ability activation uses chunk enabled mask | `RuntimeAbilityActivation.gen.cs:55-58`, `:83-85`, `:115-117`, `:136-138` |
| instant effect Burst job + cue-only spec | `RuntimeEffectInstant.gen.cs:7`, `:49-50`, `:138-139`, `:242-243`, `:426-430` |
| active mutation gather + chunk apply | `GEActiveEffectCommandNormalizeSystem.cs`、`GASActiveEffectRuntime.cs`、`GEActiveEffectLifecycleSystems.cs`；`RuntimeActiveEffect.gen.cs` 当前只是 marker |
| active lifecycle request aggregation | `AbilityLifecycleRequestBuffer`、`AbilityLifecycleRequestSystem`、`GASActiveEffectRuntime.cs`、`GEActiveEffectLifecycleSystems.cs` |
| attribute owner marker aggregation | `AttributeOwnerMarkerRequestBuffer`、`AttributeOwnerMarkerRequestSystem`、`GASActiveEffectRuntime.cs`、`GEExecutionCalculationOutputModifierSystem.cs` |
| ability activation template | `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs:1689`, `:1713-1714`, `:1756-1757`, `:1771` |
| instant effect template | `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs:2206`, `:2269-2270`, `:2453-2454` |
| active mutation template + hot path gate | `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs:2961`, `:3186`, `:6238-6278` |
| validation report | `Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md` |
| catalog materialization 防回流 | `DefinitionCatalog.gen.cs` 只暴露 `GASGeneratedDefinitionCatalogData.Populate(...)`；`DefinitionCatalogBuilder.gen.cs` 与 `AutoChessBattleDefinitionCatalogBuilder.cs` 在各自 Baking / bootstrap owner 内构建 Blob；`GasGlueCodeGenPhases.cs` 对 generated runtime 的 `new BlobBuilder(` / `CreateBlobAssetReference<` 回流计入 boundary hit |

## 退出条件

1. Generated catalog / pure glue 纳入每次 Runtime 审查范围，但不得写成 lifecycle owner。
2. 任一 generated lifecycle / system registration 回流都必须标注 phase、reads/writes、query pattern、dependency policy，并默认 blocking。
3. generated runtime 不得重新暴露 `GASGeneratedDefinitionCatalogBuilder.BuildCatalog()` 或等价 Blob materialization owner；`new BlobBuilder(` / `CreateBlobAssetReference<` 必须留在 Baking / bootstrap owner 或由 validation gate 阻断。
4. generated output 的 static validation / codegen report 保持通过，并继续覆盖 active mutation gather + chunk apply contract、禁止旧 serial apply / random lookup 估算 / chunk buffer alias 回流。
5. active mutation 的 command carrier 有 `NativeStream` / target grouped frame / proof-only 阈值结论；已 job 化的 ability commit、normalize/spec-build/reduce、pre-tick/remove/mutation 需要补 dependency/order/capacity 证据。
