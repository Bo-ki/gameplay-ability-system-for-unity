# AbilityLifecycleFactOwnerLocal 目标态消费归档

> 日期：2026-06-08
> 当前入口：`../../00-当前架构事实/P0-致命缺陷.md`

## 目标态约束

`EffectCommand / Spec / Delta / Fact` 是语义链，不是全局总线。Ability lifecycle fact 属于 ASC owner 的 core gameplay fact，目标态承载应先落到 owner-local fact lane，再由边界投影统一导出，而不是由 generated ability system 直接写 singleton stream。

本切片消费的目标态约束：

1. Generated runtime glue 不直接持有 singleton `GameplayEventBuffer` 作为 lifecycle fact writer。
2. Ability lifecycle fact 必须携带 ASC owner，避免 fact export 只能依赖 stream 全局顺序。
3. CodeGen 模板和生成结果必须同步，否则下一次生成会把旧 singleton writer 带回 runtime core。
4. 诊断脚本要守住 generated ability fact writer 的 carrier owner，不只检查 hand-written runtime systems。

## 实现映射

- producer：generated `AbilityCatalogCommitSystem`
- fact carrier：ASC owner-local `OwnerLocalGameplayFactBuffer`
- sequence owner：暂仍复用 `GEEffectCommandStreamComponent.NextFactSequence`
- export：现有 owner-local fact export / flush 仍会把 fact 投影到边界观察与 legacy stream

## 未宣称完成

本切片不宣称 Gameplay Fact export 已完全 scale-ready。`RuntimeActiveEffect.gen.cs`、active effect lifecycle hand-written system、AutoChess execution extension 等仍有直接或 legacy fact writer 需要继续迁移；fact sequence / fan-in owner 也仍需要后续切片处理。
