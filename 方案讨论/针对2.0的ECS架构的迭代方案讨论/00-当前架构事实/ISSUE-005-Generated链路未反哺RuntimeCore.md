# ISSUE-005 Generated 链路已反哺但缺同等审查

> 最近复核：2026-06-06 | 状态：Mitigated | 严重度：P1

## 当前结论

旧标题已经不准确：generated 链路现在已经反哺 Runtime Core。当前问题已经从“未反哺”转为“generated runtime 是主链事实，必须有同等 DOTS 审查和 static validation 防回流”。

## 已缓解部分

1. `GASSystemScheduleContract` 通过手写 generated type-name 列表注册 7 个 generated systems；`RuntimeSystemRegistration.gen.cs` 当前已退场。
2. `GASDefinitionCatalogBlob` 被 generated runtime 读取。
3. Ability catalog commit、GE command normalize、spec build、attribute reduce、active effect mutation/tick/remove 都已进入执行链。
4. `GasGlueCodeGenPhases.WriteRuntimeAbilityActivationSystem()` 已同步到当前 owner-local commit marker 形态，不再生成 `NativeStream + state.Dependency.Complete() + Allocator.Temp ECB.Playback` 的旧 ability commit 模板。
5. `RuntimeActiveEffectSystemsTemplate` 已与当前 `RuntimeActiveEffect.gen.cs` 对齐，不再在 codegen 层回滚到 `new EntityCommandBuffer(Allocator.Temp)` / 手动 `Playback(em)` 形态。
6. `RuntimeEffectInstant.gen.cs` 当前 spec-build 与 attribute reduce 已由 codegen 模板生成 scheduled `IJob` 路径；不能再把它们写作主线程 spec/reduce 残留。
7. `AbilityCatalogCommitJob` 已使用 `CommitRequestTypeHandle` + `chunk.GetEnabledMask(ref ...)` 关闭当前 chunk 内 commit request，并用 `EndRequestTypeHandle` + chunk `EnabledMask` 写 auto-end 请求，不再使用 `ComponentLookup.SetComponentEnabled` 做当前 ability 同实体随机 enableable 开关。
8. `RuntimeEffectInstant.gen.cs` 已输出 `using Unity.Burst`，`InstantSpecBuildJob` 与 `AttributeSetReduceApplyJob` 均标注 `[BurstCompile]`。
9. `CanBuildInstantSpec` 已允许 `ModifierCount > 0 || GameplayCueCode > 0`，cue-only instant GE 不再被 spec build 丢弃。
10. `IsDestroyingAsc` 已按 `ASCDestroyingComponent` enabled bit 判定销毁态，不再把默认 disabled 的正常 ASC 误判为 destroying。
11. `GASActiveEffectMutationApplySystem` 已调度 `GASGeneratedActiveEffectRuntime.GEActiveEffectMutationGatherJob : IJob` + `GEActiveEffectMutationChunkApplyJob : IJobChunk`，旧 public/static `TryApplyActiveMutation(EntityManager, ...)` helper 与旧 serial `GEActiveEffectMutationApplyJob : IJob` 已退场。
12. `AbilityCatalogCommitSystem` 已移除 per-frame `NativeList<GECommandSeedRecord>(Allocator.TempJob)` scratch；模板现在直接构造单条 seed 并 append command。
13. `GasCodeGenValidationReport.md` 已加入 generated runtime hot path gate；当前 `GeneratedHotPathRegressionHits: 0`，可阻断 `Complete()`、`.Run()`、legacy EventBus writer、旧 active mutation helper、ability seed scratch、hot path `Allocator.Temp`、`HasComponent<ASCDestroyingComponent>`、旧 ability lifecycle lookup、旧 `AttributeDirtyLookup` / `ActiveModifierPresentLookup`、generated/template `GASManager.EntityManager` 回流。
14. generated active effect granted-ability cleanup 不再直接写 `AbilityCancelRequestLookup` / `AbilityDestroyOnCleanupLookup`；模板和生成结果均改为写 `AbilityLifecycleRequestBuffer`，由 `AbilityLifecycleRequestSystem` 在 ability chunk 内统一应用 cancel/destroy-on-cleanup marker。
15. generated active effect 对 ASC dirty / active modifier present 的写入不再使用 `AttributeDirtyLookup` / `ActiveModifierPresentLookup`；模板和生成结果均改为写 `AttributeOwnerMarkerRequestBuffer`，由 `AttributeOwnerMarkerRequestSystem` 在 ASC chunk 内统一应用。

## 新风险

1. generated systems 修改 runtime state，不能被视为“只读生成物”。
2. generated output 中 `Complete()` 已清零；`GASActiveEffectMutationApplySystem` 已进入 gather + ASC chunk-local apply，active store、slot、mutation、tag、attribute、modifier 与 ability buffer 在 owner chunk 内直接访问；相邻 pending AttributeDelta owner-local apply 也已进入 ASC chunk-local path。剩余 generated/runtime 联动风险是 singleton command carrier、gather serial command scan、SourceAttribute 跨 owner snapshot lane 缺口，以及 stream migration fallback 仍未退出。
3. generated active lifecycle 的 ability cancel/destroy cleanup 已通过 frame-local lifecycle request buffer 收口；active modifier present / attribute dirty 已通过 frame-local attribute owner marker request buffer 收口。当前 `ComponentLookup.SetComponentEnabled(...)` 随机 enableable 风险不再集中于这些 marker，而是需要继续扫描未来模板是否回流。
4. generated active effect lifecycle 与 handwritten ExecutionCalculation/Attribute/Fact projection 的 ordering、capacity、deterministic merge 需要证据。
5. generated catalog lookup 的 revision/lifecycle owner 需要明确。
6. codegen static validation 已有第一道 gate，并覆盖 ability lifecycle / ASC dirty-present 旧 lookup、global EntityManager facade、旧 active mutation serial apply、旧 active mutation random lookup 估算与 chunk buffer/lookup alias 回流；后续还需要继续扩展到 singleton command carrier 阈值、pending AttributeDelta owner materialization 回流、stream migration fallback 证据、无 `[BurstCompile]` hot job、Temp ECB playback 和 `state.Dependency.Complete()`。

## 代码证据

| 事实 | 文件 |
|---|---|
| generated registration | `GASSystemScheduleContract.cs:207-235`、`:323-328`、`:371-374` |
| catalog lookup | `DefinitionCatalog.gen.cs` |
| runtime glue | `RuntimeDefinitionGlue.gen.cs` |
| ability activation uses chunk enabled mask | `RuntimeAbilityActivation.gen.cs:55-58`, `:83-85`, `:115-117`, `:136-138` |
| instant effect Burst job + cue-only spec | `RuntimeEffectInstant.gen.cs:7`, `:49-50`, `:138-139`, `:242-243`, `:426-430` |
| active mutation gather + chunk apply | `RuntimeActiveEffect.gen.cs:142-186`, `:362-700` |
| active lifecycle request aggregation | `AbilityLifecycleRequestBuffer`、`AbilityLifecycleRequestSystem`、`RuntimeActiveEffect.gen.cs` |
| attribute owner marker aggregation | `AttributeOwnerMarkerRequestBuffer`、`AttributeOwnerMarkerRequestSystem`、`RuntimeActiveEffect.gen.cs` |
| ability activation template | `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs:1689`, `:1713-1714`, `:1756-1757`, `:1771` |
| instant effect template | `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs:2206`, `:2269-2270`, `:2453-2454` |
| active mutation template + hot path gate | `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs:2961`, `:3186`, `:6238-6278` |
| validation report | `Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md` |

## 退出条件

1. Generated runtime 纳入每次 Runtime 审查范围。
2. 每个 generated system 标注 phase、reads/writes、query pattern、dependency policy。
3. generated output 的 static validation / codegen report 保持通过，并继续覆盖 active mutation gather + chunk apply contract、禁止旧 serial apply / random lookup 估算 / chunk buffer alias 回流。
4. generated active mutation 的 command carrier 有 `NativeStream` / target grouped frame / proof-only 阈值结论；已 job 化的 ability commit、normalize/spec-build/reduce、pre-tick/remove/mutation 需要补 dependency/order/capacity 证据。
