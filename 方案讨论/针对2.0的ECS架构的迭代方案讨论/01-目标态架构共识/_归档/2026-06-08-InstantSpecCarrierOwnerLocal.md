# InstantSpecCarrierOwnerLocal 目标态消费归档

> 日期：2026-06-08
> 当前入口：`../../00-当前架构事实/P0-致命缺陷.md`

## 目标态约束

`EffectCommand / Spec / Delta / Fact` 是语义链，不是全局总线。instant spec 属于 frame-local gameplay record，默认承载应落在 owner-local 或确定性 fan-in，而不是 singleton DynamicBuffer。

本切片消费的目标态约束：

1. Runtime Core hot path 不以 singleton `GEEffectSpecBuffer` 作为 scale-ready carrier。
2. Generated runtime glue 不能隐藏旧 lifecycle / query owner / singleton scan 回流。
3. AttributeReduce 应尽量通过 chunk-local owner buffer 消费 spec、payload、attribute 和 fact。
4. Cue-on-apply 属于 gameplay fact 投影，不能依赖 stream spec cursor。
5. Debugger 采样必须反映当前 carrier owner，不能继续把已迁出的 buffer 归到 `EffectCommandSpecStream` pressure。

## 实现映射

- ASC owner-local：`GEEffectSpecBuffer`
- build：`GEEffectSpecBuildSystem` 收集 owner-local instant command，并写 `SpecLookup[record.Owner]`
- reduce：`GASAttributeSetReduceApplySystem : IJobChunk`
- cue fact：`GameplayFactProjectionSystem` 写 `OwnerLocalGameplayFactBuffer`
- export：`GameplayOwnerLocalFactFlushSystem` 仍是下一阶段待迁移的 singleton `GameplayEventBuffer` 导出点

## 未宣称完成

本切片不宣称 Full GAS Closure 或 scale-ready fan-in 完成。仍需继续迁移 Boundary fact export、sequence/fan-in owner，并通过无头 AutoChess x50/x1000 证明 buffer pressure 和 deterministic ordering 达标。
