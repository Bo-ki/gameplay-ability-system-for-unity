# Execution-only GE instant spec 不变量归档

> 日期：2026-06-08
> 范围：Definition CodeGen / GE command seed / ActiveMutation lane / instant spec chain

## 目标态不变量

`ActiveMutation` 只表示 GameplayEffect 需要跨帧或状态型 mutation lane：

1. duration。
2. period。
3. stack。
4. granted tags。
5. remove GameplayEffect query。
6. granted ability。

`ModifierCount == 0` 不能作为 ActiveMutation 判定依据。execution-only / cue-only GE 即使没有 modifier，也必须保持 instant command / instant spec 链路，交给 execution、cue 或后续 fact projection 消费。

## 架构理由

1. execution-only GE 的业务含义是触发 execution evaluator，不是创建 active effect slot。
2. cue-only GE 的业务含义是产生 cue / presentation fact，不是占用 active mutation apply lane。
3. 用 `ModifierCount == 0` 推断 ActiveMutation 会把没有 attribute modifier 的 GE 送入 active lane，导致 instant spec consumer 看不到 spec。
4. Runtime Core 的 fan-in 应按语义 owner 选择 lane：状态型 GE 进 ActiveMutation，瞬时 execution / cue / instant delta 进 owner-local spec。

## 验收规则

1. Generated `GECommandSeedRecord` 分类和 handwritten command normalize 必须共用同一语义：只看 persistent runtime state，不看 `ModifierCount == 0`。
2. AutoChess execution-only GE 必须能在 x50 headless validation 中输出 `executionMatchedEffectSpecs > 0`、`executionOutputs > 0`、`pendingAttributeAppliedDeltas > 0`。
3. owner-local command/spec/fact 链路下，runtime chain gate 看 `SpecCount / FactCount / PendingAttributeAppliedDeltaCount`，不看 legacy request entity count。
4. 如果后续引入新的 GE 语义类型，必须先声明其 lane owner，再进入 generated seed 分类，不能复用 modifier 数量作为隐式分类。

## 关联事实

- `../../00-当前架构事实/_归档/2026-06-08-ExecutionFactOwnerLocalSpecChain.md`
- `../14-DefinitionCodeGen目标链路Spec.md`
- `../10-AutoChess无头验收Spec.md`
