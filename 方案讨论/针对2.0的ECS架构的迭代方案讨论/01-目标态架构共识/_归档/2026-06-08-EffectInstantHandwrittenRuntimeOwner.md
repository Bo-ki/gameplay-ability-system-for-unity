# EffectInstant 手写 Runtime owner 目标态兑现记录

> 日期：2026-06-08

本记录对应 Runtime Core 目标态中 “generated runtime 只能提供 pure glue，instant effect lifecycle owner 必须收口到手写 Runtime Core” 的兑现切片。

## 目标态结论

Instant GE 的 spec build、attribute set reduce apply 和 fact projection 不应由 generated runtime artifact 担任 lifecycle owner。generated artifact 可以提供 definition catalog / glue marker，但不能持有 runtime query、job dependency、lookup 写入和业务 fan-in。

本轮落地后，`GEEffectSpecBuildSystem` 与 `GASAttributeSetReduceApplySystem` 已归入 `GAS.Runtime` 手写实现，`RuntimeEffectInstant.gen.cs` 退为 pure glue marker。

## 兑现点

- `GEEffectSpecBuildSystem` / `GASAttributeSetReduceApplySystem` 由 `Assets/GAS/Runtime/System/Effect/GEEffectInstantSystems.cs` 承担。
- Runtime Core 直接声明 query、收集 ASC owner-local instant command、完成 deterministic merge、分配 spec sequence，并执行 attribute set reduce apply。
- runtime-side requirement / magnitude evaluator 已沉到 `GASRuntimeDefinitionResolver.cs`，手写 owner 不再调用 generated helper。
- `RuntimeEffectInstant.gen.cs` 只保留 `GASGeneratedEffectInstantRuntimeMarker.HandwrittenRuntimeOwner`。
- `RuntimeEffectInstant.gen.cs` 在 validation report 中分类为 `RuntimePureGlue`。
- schedule contract 不再注册 `GAS.Runtime.Generated.GEEffectSpecBuildSystem` 和 `GAS.Runtime.Generated.GASAttributeSetReduceApplySystem`。
- hand-written instant owner 不直接依赖 generated active-effect lifecycle 类型；跨 artifact 顺序由残留 active-effect owner wrapper 自身声明 `UpdateBefore(typeof(GEEffectSpecBuildSystem))`，并由 schedule contract 对 generated type-name fail-fast 注册。
- validation report 已降到 `GeneratedRuntimeLifecycleMigrationArtifacts = 1`、`GeneratedRuntimeLifecycleHits = 0`。

## 剩余目标

- 下一轮应优先迁移 `RuntimeActiveEffect.gen.cs`，把 active effect lifecycle owner 从 generated runtime 中退出。
- active-effect 迁移完成后，generated runtime lifecycle migration artifact 应归零，`GeneratedRuntimeLifecycleHits` 应保持 `0`。
- codegen 源文件中旧 instant generator writer 可以删除，避免 dead template 与目标态职责长期并存。
- 目标达成前仍需要 Unity headless AutoChess 全链路、Unity Test Runner 和 x100/x1000 profile 证明业务承载与性能。
