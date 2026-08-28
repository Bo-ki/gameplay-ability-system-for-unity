# EX-GAS Wiki - GameplayEffect

更新时间：2026-05-23

> 迁移提示：本文的旧 request entity、runtime GE entity、registry 和 system 名称仅作历史参考，已不再是当前入口。Runtime v1 的 GE 通过 `GasRuntimeWorldOwner` 管理的 definition/catalog 与 transaction，由 `GasCommandPort` 接收 intent、`GasTickDag` 推进，并经 Boundary facts 输出观察结果。

GameplayEffect 是 EX-GAS 中改变运行时状态的核心载体。Runtime v1 中，GE 由不可变 definition、owner transaction 与 Boundary facts 表达，不通过旧 request entity、spec/context component 或托管 `GameplayEffectSpec` 作为主入口。

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

Definition 来自 Luban normalized rows，经不可变 catalog / blob schema 进入 Runtime v1：

- Runtime v1 catalog 是 GE definition 的唯一读取边界。
- Blob schema 只保存不可变 definition，不持有 runtime 生命周期。
- normalized rows 可描述 GE duration、period、stacking、modifier、granted ability、tag requirement 等形状。

Definition 不保存运行时 spec/context/lifecycle、stack count、duration 剩余时间、Attribute current value 或任何 runtime entity。

## Apply Request（旧流程，仅作迁移对照）

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

## Runtime Instance（旧流程，仅作迁移对照）

`SApplyGameplayEffectRequest` 消费 request 后创建 runtime GE entity，并写入：

- `CEffectContext`：本次施加上下文事实源。
- `CEffectSpecData`：本次 spec 最小权威字段。
- `CEffectLifecycle`：生命周期状态。
- capture / resolved modifier / set-by-caller / stacking 等 runtime buffer。

`CEffectContext` 记录 source、target、instigator、causer、context id、parent context id、target data kind。`Level` 不属于 context，保存在 `CEffectSpecData`。

## Apply / Tick / Remove（Runtime v1）

当前语义：

- `GasCommandPort` 接收 GE apply intent。
- `GasTickDag` 驱动 owner effect transaction，依次处理 instant、active、period、remove 与派生 fact。
- `GasBoundaryDrainCoordinator` 将结果投影为只读 Boundary facts。

旧版主要系统（已删除，仅作迁移对照）：

- `SApplyGameplayEffectRequest`：request -> runtime GE instance。
- `SEffectApply`：应用 pending GE，写 modifier / tag / granted ability / cue facts。
- `SOngoingTagRequirements`：处理 ongoing requirement 和 inhibited 状态。
- `SEffectTick`：推进 duration / period。
- `SEffectRemove`：移除 GE 并清理 granted state。
- `SExecutionCalculation` / extension group：计算 ExecutionCalculation output。
- `SExecutionCalculationOutputModifier`：把 execution output 转换为 modifier。
- `SAttributeRecalculate`：根据 modifier / execution result 更新 Attribute。

## 派生 GE

Period / overflow / reaction 派生 GE 在 TargetResolve 生成 planned token，经 OwnerWave 成功提交后由 SourceSpecProjection 密封正式 EffectSpecId / ApplicationId，并携带 source stable id、parent provenance 与 level 快照。

不要复制 runtime state，也不要绕过 transaction 直接伪造 Attribute change。

## 标签条件（Runtime v1）

GE 标签条件由 V1 transaction 使用不可变 `GasRequirementBlob` 与 target-local tag slots 判定：

- Required：query 必须满足。
- Blocked：blocked query 命中即拒绝。
- Immunity：immunity query 满足即拒绝。

缺失 target、缺失 definition、required 未满足、blocked 命中、immunity 命中都有统一 failure code。

## Cue 与 Presentation

GE 可以产生 Cue / presentation fact，但 Cue 是 observation / presentation 边界：

- transaction 将 Cue 结果写入目标 ASC 或 Session scope 的 Boundary fact。
- `GasBoundaryDrainCoordinator` 投影只读 snapshot，managed Cue adapter 再消费 snapshot。
- Cue / VFX / SFX / UI 不反向决定 Attribute、Tag、GE 或 Ability lifecycle。
