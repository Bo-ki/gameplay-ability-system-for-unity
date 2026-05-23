# EX-GAS Wiki - GameplayEffect

更新时间：2026-05-23

GameplayEffect 是 EX-GAS 中改变运行时状态的核心载体。当前架构中，GE 分为 definition、apply request、spec/context、runtime instance 和 observation facts，不再通过托管 `GameplayEffectSpec` 作为主入口。

## 职责

GE 可以表达：

- Attribute modifier。
- ExecutionCalculation。
- Duration / Infinite / Instant lifecycle。
- Period 派生 GE。
- Stacking / overflow。
- Granted tags / remove tags / immunity。
- Granted ability。
- Cue / presentation request。

原则：运行时 Attribute 修改应通过 GE 链路进入，避免在业务脚本中直接写 Attribute current value。

## Definition

Definition 来自 Luban / generated config，经 registry 和 summary 进入 runtime：

- `GameplayEffectConfigRegistry` 是 GE definition cache lifecycle owner。
- `GEStaticDefinitionBlob` 是 static definition Blob 边界。
- `GASDefinitionTable` 可汇总 GE duration、period、stacking、modifier、granted ability、tag requirement 等形状。

Definition 不保存：

- `CEffectSpecData`
- `CEffectContext`
- stack runtime count
- duration 剩余时间
- Attribute current value
- runtime GE entity

## Apply Request

GE 施加入口是 `CApplyGameplayEffectRequest`：

```text
CApplyGameplayEffectRequest
  SourceAsc
  SourceAbility
  SourceEffect
  Instigator
  Causer
  GameplayEffectCode
  Level
  ParentContextId
  DurationFrameOverride
```

目标不直接写在 component 里，而是：

- `CTargetDataHeader`
- `BTargetEntity`
- point / direction / hit 摘要 buffer

SetByCaller 走 `BSetByCallerValue`，request 被消费后复制到 runtime GE instance，不进入 prototype、static definition 或全局字典。

## Runtime Instance

`SApplyGameplayEffectRequest` 消费 request 后创建 runtime GE entity，并写入：

- `CEffectContext`：本次施加上下文事实源。
- `CEffectSpecData`：本次 spec 最小权威字段。
- `CEffectLifecycle`：生命周期状态。
- capture / resolved modifier / set-by-caller / stacking 等 runtime buffer。

`CEffectContext` 记录 source、target、instigator、causer、context id、parent context id、target data kind。`Level` 不属于 context，保存在 `CEffectSpecData`。

## Apply / Tick / Remove

主要系统：

- `SApplyGameplayEffectRequest`：request -> runtime GE instance。
- `SEffectApply`：应用 pending GE，写 modifier / tag / granted ability / cue facts。
- `SOngoingTagRequirements`：处理 ongoing requirement 和 inhibited 状态。
- `SEffectTick`：推进 duration / period。
- `SEffectRemove`：移除 GE 并清理 granted state。
- `SExecutionCalculation` / extension group：计算 ExecutionCalculation output。
- `SExecutionCalculationOutputModifier`：把 execution output 转换为 modifier。
- `SAttributeRecalculate`：根据 modifier / execution result 更新 Attribute。

## 派生 GE

Period / overflow / reaction 派生 GE 应通过标准 request 创建：

- `SourceEffect` 指向派生来源 GE。
- `ParentContextId` 继承来源 context。
- `Level` 从来源 GE 的 `CEffectSpecData.Level` 继承，或显式 override。

不要直接复制 runtime GE state 或手动伪造 Attribute change。

## 标签条件

GE 标签判定统一由 `TagRequirementEvaluator` 处理：

- Required：query 必须满足。
- Blocked：blocked query 命中即拒绝。
- Immunity：immunity query 满足即拒绝。

缺失实体、缺少 `CTagMask`、缺失 definition、required 未满足、blocked 命中、immunity 命中都有统一 failure code。

## Cue 与 Presentation

GE 可以触发 Cue / presentation，但 Cue 是 observation / presentation 边界：

- Cue request 可以进入 `BCueRequest`。
- `SPresentationOutboxProjection` 投影为 `BPresentationEvent`。
- Cue / VFX / SFX / UI 不反向决定 Attribute、Tag、GE 或 Ability lifecycle。
