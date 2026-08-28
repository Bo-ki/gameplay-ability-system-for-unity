# EX-GAS Wiki - GameplayCue

更新时间：2026-05-23

> 迁移提示：本文部分链路描述的是已删除的旧 ECS Cue 实现，仅保留作历史参考。当前 Runtime v1 的 Cue 是纯托管表现/authoring 类型，运行时入口位于 `Assets/GAS/Runtime/V1`；请通过 Boundary facts / read model 消费表现请求，不要引用旧 Cue Entity、组件或 system。

GameplayCue 是 EX-GAS 的表现层扩展点，用于播放特效、音效、动画、浮字、UI 标记或其他可视化反馈。当前架构中 Cue 可以保留托管表现对象，但它不属于 Simulation 权威。

## 原则

Cue 必须遵守：

1. 不修改 Attribute、Tag、GE、Ability runtime state。
2. 不作为 gameplay 判定输入。
3. 不反向驱动 Ability commit、GE apply、damage routing 或 lifecycle。
4. 生命周期必须绑定到 Boundary fact 携带的 source/target stable id、generation 与 presentation event。
5. 失败、播放、停止、销毁应可以被 fact / replay / log 追踪。

## 当前位置

Cue 位于 Observation / Presentation Plane：

```text
Runtime v1 simulation
  -> Boundary fact / read model
  -> Presentation adapter
  -> Cue / UI / VFX / SFX
```

无头 Demo 中，Cue / UI / VFX / SFX / FloatingText / Settlement 会被折算为 presentation marker 和 outbox event，用于自动验收表现交互没有丢失。

## Runtime 组件

旧版本 Cue Entity 组件已删除；当前保留的表现对象包括：

- `GameplayCueBase<TParam>`：表现逻辑基类。
- `CueLog`、`CueMountPrefab`、`CuePlaySound`：纯表现适配实现。

这些对象只能处理表现生命周期，不应写 simulation state。

## 参数与配置

Cue 参数来自 Luban 的 `GameplayCueBase` 多态 Bean：

1. Cue 类继承 `GameplayCueBase<TParam>`。
2. `TParam` 继承 `XParam`，并用 `[BeanField]` 暴露入表字段。
3. `BeanUpdater` 把 Cue 类型写入 `__beans__.xlsx`。
4. CodeGen 生成 normalized rows；不再生成旧 Cue Entity 或 Runtime lifecycle。
5. 运行时由 `CueHelper` / Boundary adapter 创建 Cue 实例或配置。

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

表现层问题优先从这些 Boundary 派生数据看：

- Cue request / presentation fact snapshot
- `GasStructuredLogExportSnapshot`
- `GasRuntimeV1DiagnosticSnapshot`
- headless presentation marker summary

不要通过让 Cue 写 gameplay state 来“补救”表现时序问题。
