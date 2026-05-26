# GAS ECS Runtime - Runtime Core Frame Backbone - Schedule / Phase Contract

## 父节点

[AM2B-FrameBackbone](README.md)

## 任务ID

`T1-RuntimeCore-AM2B-A`

## 状态

已完成（contract-first，Unity验证待补跑）

## 当前问题

1. 当前 `GASSystemScheduleContract` 仍以 `GASCommandGroup / GASEffectGroup / GASAttributeGroup / GASAbilityGroup / GASCueGroup` 为主。
2. AM2 的 stream phase 直接挂在 `GASCommandGroup`，还没有独立的 Runtime Core phase 顺序契约。
3. 后续任务如果没有先固定 phase contract，会继续出现 helper 临时查询、stream 清理时机不统一、structural playback 分散的问题。

## 目标 / 目的

1. 定义 Runtime Core frame backbone 的 phase 顺序契约。
2. 明确哪些 phase 可读、可写、可结构变化、可观察。
3. 为 AM2B-B 到 AM2B-F 提供可测试的 schedule owner。

## 执行范围

1. `Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs`
2. `Assets/_Test/GAS/Runtime/Event/SystemScheduleContractTests.cs`

## 执行细则

1. 先建立契约，不一次性搬迁所有系统。
2. 目标 phase 至少覆盖 `FramePrepare / CommandIngest / SpecEvaluation / ActiveEffectLifecycle / DeltaApply / TypedFactProjection / StructuralPlayback / ObservationProjection`。
3. 每个 phase 必须声明结构变化权限。
4. 如果现阶段只能以 contract / tests 表达而不创建真实 SystemGroup，必须标记为 contract-first。

## API 选型

| 候选 | 本任务用途 | 采用 / 拒绝口径 |
|---|---|---|
| `ComponentSystemGroup` | phase owner | 默认采用或以 contract 表达 |
| `ISystem` | 后续 phase system | 本任务只登记，不强制实现 |
| ECB playback group | structural gate | 只定义入口，不实现所有 playback |

## 验收标准

1. 存在可检索的 Runtime Core backbone phase contract。
2. 测试能验证 phase 顺序和结构变化权限。
3. AM2B-A 交还时指向 AM2B-B 作为下一任务。

## 测试链路

1. `git diff --check`
2. `rg -n "FramePrepare|CommandIngest|SpecEvaluation|StructuralPlayback|RuntimeCoreFrame" Assets/GAS/Runtime Assets/_Test/GAS/Runtime`

## 本轮进展

1. `GASSystemScheduleContract` 已新增 contract-first 的 `RuntimeCoreFramePhases`，覆盖 `FramePrepare / CommandIngest / SpecEvaluation / ActiveEffectLifecycle / DeltaApply / TypedFactProjection / StructuralPlayback / ObservationProjection`。
2. 每个 phase 已声明读写访问口径、结构变化权限和 observation boundary。
3. `EffectCommandSpecStreamTargetSystems` 已映射到 Runtime Core backbone phase。
4. 新增 `SystemScheduleContractTests` 覆盖 phase 顺序、结构变化权限和 AM2 stream system -> phase 映射。
