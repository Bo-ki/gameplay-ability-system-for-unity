# ActiveEffect command normalize 手写 Runtime owner 目标态兑现记录

> 日期：2026-06-08

本记录对应 Runtime Core 目标态中 “command resolve / lane normalize 必须由手写 Runtime Core owner 持有，generated runtime 不持有生命周期系统” 的兑现切片。

## 目标态结论

Gameplay effect command normalize 属于运行时执行链路，不是 generated glue。它会读取 catalog、改写 command kind/flag/duration override，并把 active mutation command fan-in 到 ASC owner-local buffer，因此 owner 必须在 `GAS.Runtime` 内。

本轮落地后，`GEEffectCommandCatalogNormalizeSystem` 已迁入手写 Runtime Core。generated active-effect artifact 仍残留其它 lifecycle owner 债务，但 schedule contract 已不再调度 generated normalize wrapper。

## 兑现点

- `GEEffectCommandCatalogNormalizeSystem` 由 `Assets/GAS/Runtime/System/Effect/GEActiveEffectCommandNormalizeSystem.cs` 承担。
- gameplay effect catalog normalize 规则沉入 `GASRuntimeDefinitionResolver.TryNormalizeGameplayEffectCommand(...)`。
- Normalize 系统明确排在 `GEEffectSpecBuildSystem` 前，保障 instant spec build 输入已经完成 lane 判定。
- generated `GASActiveEffectRemoveSystem` 只通过 `UpdateBefore(typeof(GAS.Runtime.GEEffectCommandCatalogNormalizeSystem))` 保持顺序；手写 Runtime Core 不直接依赖 generated active-effect type。
- `GASSystemScheduleContract` 注册手写 normalize owner，并禁止注册 `GAS.Runtime.Generated.GEEffectCommandCatalogNormalizeSystem`。
- active mutation set-by-caller payload 仍保持 ASC owner-local carrier，后续 apply 链路不用消费 stream/global payload。

## 剩余目标

- 删除 generated normalize wrapper 和旧 generated helper，避免 dead lifecycle code 长期停留在 `RuntimeActiveEffect.gen.cs`。
- 继续迁移 `GASActiveEffectMutationApplySystem`、pre-tick/remove/finalize 等 active-effect lifecycle owner，使 `RuntimeActiveEffect.gen.cs` 退出 `RuntimeLifecycleMigration`。
- AutoChess capability 断言需要单独收口，恢复 `Verify-GAS-RuntimeCoreBoundary.ps1` 全量通过。
- 目标达成前仍需要 Unity headless AutoChess 全链路、Unity Test Runner 和 x100/x1000 profile 证明业务承载与性能。
