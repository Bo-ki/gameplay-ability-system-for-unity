# ISSUE-005 Generated 链路已反哺但缺同等审查

> 最近复核：2026-06-06 | 状态：Mitigated | 严重度：P1

## 当前结论

旧标题已经不准确：generated 链路现在已经反哺 Runtime Core。当前问题已经从“未反哺”转为“generated runtime 是主链事实，必须有同等 DOTS 审查和 static validation 防回流”。

## 已缓解部分

1. `RuntimeSystemRegistration.gen.cs` 注册 7 个 generated systems。
2. `GASDefinitionCatalogBlob` 被 generated runtime 读取。
3. Ability catalog commit、GE command normalize、spec build、attribute reduce、active effect mutation/tick/remove 都已进入执行链。
4. `GasGlueCodeGenPhases.WriteRuntimeAbilityActivationSystem()` 已同步到当前 owner-local commit marker 形态，不再生成 `NativeStream + state.Dependency.Complete() + Allocator.Temp ECB.Playback` 的旧 ability commit 模板。
5. `RuntimeActiveEffectSystemsTemplate` 已与当前 `RuntimeActiveEffect.gen.cs` 对齐，不再在 codegen 层回滚到 `new EntityCommandBuffer(Allocator.Temp)` / 手动 `Playback(em)` 形态。
6. `RuntimeEffectInstant.gen.cs` 当前 spec-build 与 attribute reduce 已由 codegen 模板生成 scheduled `IJob` 路径；不能再把它们写作主线程 spec/reduce 残留。
7. `AbilityCatalogCommitJob` 已使用 `CommitRequestTypeHandle` + `chunk.GetEnabledMask(ref ...)` 关闭当前 chunk 内 commit request，不再使用 `ComponentLookup.SetComponentEnabled` 做同实体随机 enableable 开关。
8. `RuntimeEffectInstant.gen.cs` 已输出 `using Unity.Burst`，`InstantSpecBuildJob` 与 `AttributeSetReduceApplyJob` 均标注 `[BurstCompile]`。
9. `CanBuildInstantSpec` 已允许 `ModifierCount > 0 || GameplayCueCode > 0`，cue-only instant GE 不再被 spec build 丢弃。
10. `IsDestroyingAsc` 已按 `ASCDestroyingComponent` enabled bit 判定销毁态，不再把默认 disabled 的正常 ASC 误判为 destroying。

## 新风险

1. generated systems 修改 runtime state，不能被视为“只读生成物”。
2. generated output 中 `Complete()` 已清零；但 `GASActiveEffectMutationApplySystem` 仍存在主线程 buffer for loop、`EntityManager` 过渡 helper、singleton stream 写入和 legacy EventBus writer。
3. generated active lifecycle 虽已 job 化，仍存在 `ComponentLookup.SetComponentEnabled(...)` 随机 enableable 开关，需要按 `EN-03` / `CASE-20` 收口为 owner-local state、`EnabledRefRW` 或 chunk `EnabledMask`。
4. generated active effect lifecycle 与 handwritten ExecutionCalculation/Attribute/Fact projection 的 ordering、capacity、deterministic merge 需要证据。
5. generated catalog lookup 的 revision/lifecycle owner 需要明确。
6. 缺少 codegen static validation：需要自动阻断无 `[BurstCompile]` hot job、hot path random `ComponentLookup.SetComponentEnabled`、`HasComponent<ASCDestroyingComponent>` 销毁误判、Temp ECB playback 和 `state.Dependency.Complete()` 回流。

## 代码证据

| 事实 | 文件 |
|---|---|
| generated registration | `RuntimeSystemRegistration.gen.cs` |
| catalog lookup | `DefinitionCatalog.gen.cs` |
| runtime glue | `RuntimeDefinitionGlue.gen.cs` |
| ability activation uses chunk enabled mask | `RuntimeAbilityActivation.gen.cs:56`, `:80`, `:123-124`, `:138` |
| instant effect Burst job + cue-only spec | `RuntimeEffectInstant.gen.cs:7`, `:49-50`, `:138-139`, `:242-243`, `:426-430` |
| active mutation residual | `RuntimeActiveEffect.gen.cs:97-145` |
| active lifecycle random enableable residual | `RuntimeActiveEffect.gen.cs:395`, `:852`, `:871`, `:1090`, `:1100`, `:1120` |
| ability activation template | `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs:1689`, `:1713-1714`, `:1756-1757`, `:1771` |
| instant effect template | `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs:2206`, `:2269-2270`, `:2453-2454` |

## 退出条件

1. Generated runtime 纳入每次 Runtime 审查范围。
2. 每个 generated system 标注 phase、reads/writes、query pattern、dependency policy。
3. generated output 有 static validation 或 codegen report，证明不会重新生成已退场的 request entity / Temp ECB playback / legacy bridge。
4. generated active mutation 的 serial buffer loop 迁入可解释的 job/store chain 或 deterministic merge；已 job 化的 ability commit、normalize/spec-build/reduce、pre-tick/remove 需要补 static validation 防回流。
