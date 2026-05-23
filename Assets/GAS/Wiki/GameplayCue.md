# EX-GAS Wiki - GameplayCue

更新时间：2026-05-23

GameplayCue 是 EX-GAS 的表现层扩展点，用于播放特效、音效、动画、浮字、UI 标记或其他可视化反馈。当前架构中 Cue 可以保留托管表现对象，但它不属于 Simulation 权威。

## 原则

Cue 必须遵守：

1. 不修改 Attribute、Tag、GE、Ability runtime state。
2. 不作为 gameplay 判定输入。
3. 不反向驱动 Ability commit、GE apply、damage routing 或 lifecycle。
4. 生命周期必须绑定到明确的 source entity、GE entity、Ability entity 或 presentation event。
5. 失败、播放、停止、销毁应可以被 fact / replay / log 追踪。

## 当前位置

Cue 位于 Observation / Presentation Plane：

```text
GameplayEffect / Ability / Timeline system
  -> Cue request / gameplay fact
  -> CGameplayEventBus
  -> SPresentationOutboxProjection
  -> BPresentationEvent
  -> Cue system / UI / VFX / SFX
```

无头 Demo 中，Cue / UI / VFX / SFX / FloatingText / Settlement 会被折算为 presentation marker 和 outbox event，用于自动验收表现交互没有丢失。

## Runtime 组件

当前 Cue 相关对象仍包括：

- `GameplayCueBase<TParam>`：表现逻辑基类。
- `MCCue`：Cue Entity 上的 managed component，承载表现对象引用。
- `ECCuePlayable` / `ECCuePlaying` / `ECKillCue`：Cue 播放状态。
- `SCueRequestBridge`、`SCueStart`、`SCueTick`、`SCueEnd`、`SCueDestroy`：Cue system 链路。

这些对象只能处理表现生命周期，不应写 simulation state。

## 参数与配置

Cue 参数来自 Luban 的 `GameplayCueBase` 多态 Bean：

1. Cue 类继承 `GameplayCueBase<TParam>`。
2. `TParam` 继承 `XParam`，并用 `[BeanField]` 暴露入表字段。
3. `BeanUpdater` 把 Cue 类型写入 `__beans__.xlsx`。
4. `CodeGeneratorLubanPart` 生成 `XLuban` 转换代码。
5. 运行时由 `CueHelper` / Cue request 创建 Cue 实例或配置。

`XParamCue` 可配置：

- `RequiredTags`
- `ImmunityTags`
- `CueLogic: GameplayCueBase`

## 标签过滤

Cue 播放条件统一走 tag requirement：

- `RequiredTags`：目标必须满足。
- `ImmunityTags`：命中则阻止播放。

Cue tag 过滤失败只影响表现，不应影响 GE 是否施加或 Ability 是否激活。

## 在 GE / Ability 中使用

GE 中常见 Cue：

- `CueOnApply`
- `CueOnTick`
- `CueOnAdd`
- `CueOnRemove`
- `CueOnActivate`
- `CueOnDeactivate`

Ability / Timeline 中播放 Cue 应输出明确 request 或 presentation fact。对于持续 Cue，必须绑定 source 和销毁策略，避免表现对象泄漏。

## 调试与验收

表现层问题优先从这些数据看：

- `BCueRequest`
- `BPresentationEvent`
- `BDebugReplayEvent`
- structured log export
- headless presentation marker summary

不要通过让 Cue 写 gameplay state 来“补救”表现时序问题。
