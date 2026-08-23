# Runtime v1 不可兼容迁移基线事实

> Owner：`00-当前架构事实` | 审查日期：2026-08-24 | 审查基线：上一轮提交 `564fe711` 之后的当前代码 | 状态：当前事实，不代表 v1 已实现

本文件是 Runtime v1 破坏性重构的当前代码基线。目标态由 [17-GAS业务链路破坏性重划分 Spec](../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md) 维护；可领取任务由 [Runtime v1 不可兼容迁移](../02-主线任务树/RuntimeV1不可兼容迁移/README.md) 维护。

## 结论

当前 EX-GAS 已形成可运行的 DOTS 迁移骨架，但还不是语义闭合、单一权威的 GAS Runtime。五段物理组、Ability Entity、owner-local ActiveEffect slot、legacy GE entity、singleton/EventBus 与多 Boundary consumer 同时存在；它们是当前事实，不是 v1 目标态。

## 当前物理调度

`GASSystemScheduleContract` 当前创建并注册五段自定义 physical group：

1. `GASFramePrepareSystemGroup`
2. `GASCommandResolveSystemGroup`
3. `GASCoreSimulationSystemGroup`
4. `GASStructuralCommitSystemGroup`
5. `GASBoundaryProjectionSystemGroup`

`GEExecutionCalculationExtensionSystemGroup` 嵌在 Core；StructuralCommit 使用自定义 Begin/End ECB。逻辑 phase contract 只覆盖部分系统，不能当作完整执行表。

实际 `BoundaryProjectionSystemTypes` 只注册：

- `GameplayBoundaryFactExportSystem`
- `GameplayFactBoundaryProjectionSystem`
- `PresentationOutboxProjectionSystem`
- `ReplayLogSystem`
- `DiagnosticsSnapshotSystem`
- `ASCDestroyFinalizeSystem`

`CueRequestBridgeSystem` 与 `CueManagedLifecycleSystem` 虽声明位于 Boundary group，但没有进入注册数组。

证据：

- `Assets/GAS/Runtime/System/SystemGroup/GASGroups.cs`
- `Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs:213-272`
- `Assets/GAS/Runtime/System/Cue/CueRequestBridgeSystem.cs`
- `Assets/GAS/Runtime/System/Cue/CueManagedLifecycleSystem.cs`

## 当前权威数据并不唯一

### Ability

- 每个 granted ability 当前是独立 Entity。
- ASC 的 `AbilitySlotBuffer` 只保存 `AbilityEntity`。
- 激活、提交、取消、结束和清理依赖 Ability Entity 上的状态与 enableable request component。
- 普通 grant 和 ActiveEffect grant 都通过 structural ECB 创建 Ability Entity，因此新 Ability 最早下一 tick 进入现有 Ability 主链。

证据：

- `Assets/GAS/Runtime/Ability/Component/AbilitySlotBuffer.cs`
- `Assets/GAS/Runtime/Ability/Component/AbilityStateComponent.cs`
- `Assets/GAS/Runtime/System/ASCCommandBufferResolveSystem.cs:541-567`
- `Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs:1136-1191`

### GameplayEffect

- 新主线把 ActiveEffect authority 放在 ASC-local `ActiveGameplayEffectBuffer`。
- 同一 ASC archetype 仍保留 `LegacyGameplayEffectEntityBuffer`。
- generic execution calculation 仍查询 legacy GE entity components；AutoChess 又以独立自定义 System 消费 owner-local spec。
- 当前 ActiveEffect removal 使用 `RemoveAt(slotIndex)`，slot index 会移动，不是稳定 slab。
- `ActiveEffectGlobalIndex`、bucket/stable row 与 owner-local slot 同时存在，当前代码没有证明它们共享一个唯一同步 owner。

证据：

- `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeEntityArchetypes.cs:473-514`
- `Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs:1377,2297`
- `Assets/GAS/Runtime/System/Effect/GEExecutionCalculationSystem.cs`
- `Assets/GAS/Runtime/System/Effect/GEExecutionCalculationOutputModifierSystem.cs`
- `Assets/AutoChessDemo/Battle/Ecs/AutoChessExecuteDamageCalculationSystem.cs`

## Definition / Requirement 当前语义被压平

Catalog 已有 immutable Blob、generated lookup 和 pure magnitude evaluator，这些是正向基础。但生成阶段把：

- Application Required Tags
- Ongoing Required Tags
- Immunity Tags

写入同一个 `RequirementStart/RequirementCount` range。Application 与 Ongoing 都生成 `RequiredTags`，Immunity 生成 `BlockedTags`；Runtime evaluator 对整段 range 做一次统一判断，无法保留 requirement phase。

当前后果：

1. Ongoing requirement 被当作 application gate，未形成持续重评和 inhibit/reactivate 语义。
2. Immunity 被折叠成普通 blocked gate。
3. requirement blob 中的 attribute compare 字段没有进入当前 evaluator 分支。
4. ActiveEffect apply 当前调用手写 `GASRuntimeRequirementEvaluator`，不是本页旧版本曾记录的 generated evaluator。

证据：

- `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs:1247-1259`
- `Assets/GAS/Runtime/Definition/GASRuntimeDefinitionResolver.cs:465-541`
- `Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs:650-672`

## Cue 当前是断链

当前 instant/active GE 只投影单个 `GameplayCueCode` 的 `OnApply` fact。Boundary projection 创建 `CueRequestBuffer` 时写入 `CueEntity = Entity.Null`；`CueRequestBridgeSystem` 对 Null cue entity 直接返回，并且该 System 没有注册到运行调度。

因此当前代码可以观察到 Cue fact/counter，但不能据此宣称 managed Cue 播放链可用，也没有 OnActive/WhileActive/Executed/Removed 四阶段闭环。

证据：

- `Assets/GAS/Runtime/System/Effect/GEEffectCommandSpecStreamPhases.cs:599-636,1048-1075`
- `Assets/GAS/Runtime/System/Cue/CueRequestBridgeSystem.cs:42-45`
- `Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs:264-272`

## Fact 与 Boundary 当前事实

`OwnerLocalGameplayFactBuffer` 已进入 ASC，当前 fact exporter 是 Boundary 内的 `GameplayBoundaryFactExportSystem`。代码中不存在 `GameplayOwnerLocalFactFlushSystem`；旧文档中的“Core flush 回 GameplayEventBuffer”描述已过期。

Boundary 仍由多个 System 分别读取 observation buffer、维护各自 cursor 或派生 buffer。Presentation、Replay、Diagnostics、Cue 尚未收敛为“单 ECS Drain -> managed immutable batch”。

证据：

- `Assets/GAS/Runtime/System/Effect/GEEffectCommandSpecStreamPhases.cs:646-920`
- `Assets/GAS/Runtime/System/Event/PresentationOutboxProjectionSystem.cs`
- `Assets/GAS/Runtime/System/Event/ReplayLogSystem.cs`

## 当前 tick 行为

| 行为 | 当前可观察时序 |
|---|---|
| tick 开始前已有 Ability command | CommandResolve 内 resolve/activate/commit，随后 GE spec、effect/attribute 可在同 tick进入 Core |
| Attribute threshold cancel/end | threshold、lifecycle、cleanup 按 Core 顺序在同 tick处理 |
| owner-local period due GE | ActiveEffect pre-tick 较早写入 command，后续 Normalize / SpecBuild / DeltaApply 仍在本 tick，因此 DueTick 同 tick生效 |
| post-apply overflow / reaction / cross-owner dynamic child | ActiveMutation apply 时已错过本 tick Normalize / SpecBuild，写 next-frame owner-local buffer，下一 tick处理 |
| 普通或 Effect 授予 Ability | ECB 创建 Entity，最早下一 tick被 Ability 查询消费 |
| ASC physical destroy | `ASCDestroyFinalizeSystem` 在 Boundary 写入已经错过本帧 StructuralCommit 的 ECB，通常延迟到下一 tick playback |
| Boundary observation | 当前每次自定义 Boundary group update 后投影；不是 catch-up ticks 后的单一 Drain |

这些时序是迁移回归输入，不代表目标态必须逐项照搬。任何改变必须在 v1 任务中明确列为保留、例外或有意破坏。

AutoChess 的实际 Spawn/Grant/Target/Effect/Death/Cue/Result 链、9203 配置异常、x50 负载形态和现有 hash/测试缺口见 [AutoChess 真实业务链二轮审查事实](AutoChess真实业务链二轮审查事实.md)。

## AutoChess 手工 tick 事实

`AutoChessGasRuntimeTicker` 当前依次直接更新五个 GAS 自定义 group，而不是更新完整 `FixedStepSimulationSystemGroup`。因此仅把自定义 ECB 改成标准 `EndFixedStepSimulationEntityCommandBufferSystem`，不会自动让 AutoChess runner 获得 playback；runner/session tick owner 必须同步迁移。

证据：

- `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeTicker.cs:15-19,46-50`
- `Assets/AutoChessDemo/AutoRunner/AutoChessRuntimeSystemBootstrap.cs:19-33`

## 验证缺口

- 当前 `Assets/_Test` 不存在。
- tracked `Assets` 下没有可执行的 GAS `*Tests.cs` 测试源码。
- `com.exhard.exgas.runtime.tests.csproj` 仍引用一批不存在的测试文件，只能视为陈旧工程投影。
- 历史 AutoChess 日志、validation report 和静态门不能替代 Unity Test Runner 回归。

因此 v1 破坏性实现开始前，必须先建立真实 characterization/semantic tests。

## 文档消费规则

1. 本页只证明当前代码是什么，不证明 v1 已实现。
2. 与 2026-06-08 旧事实冲突时，以本页和当前代码为准。
3. 目标删改、single kernel、slab、Boundary ring 和 Cue 四阶段只消费 `01/17`。
4. 执行顺序和验收门只消费 `02/RuntimeV1不可兼容迁移/`。
