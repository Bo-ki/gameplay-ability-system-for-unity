# Runtime 主链事实

> Owner：`00-当前架构事实` | 最近复核：2026-08-24 | 状态：当前代码事实，不代表 Runtime v1 已实现

本文件记录当前可执行调度与主要数据流。v1 迁移总基线见 [Runtime v1 不可兼容迁移基线事实](RuntimeV1不可兼容迁移基线事实.md)，目标态见 [17-GAS业务链路破坏性重划分 Spec](../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)。

## 当前 World 与五段物理组

`GASManager` 创建独立 `EX_GAS_World`、基础 Unity group、GlobalTimer、Effect stream、ActiveEffect global index、EventBus、Replay sink 与 Debugger singleton。`GASSystemScheduleContract` 在 `FixedStepSimulationSystemGroup` 下创建五个自定义 GAS group：

1. `GASFramePrepareSystemGroup`
2. `GASCommandResolveSystemGroup`
3. `GASCoreSimulationSystemGroup`
4. `GASStructuralCommitSystemGroup`
5. `GASBoundaryProjectionSystemGroup`

`GEExecutionCalculationExtensionSystemGroup` 嵌套在 Core。当前 StructuralCommit 使用自定义 Begin/End ECB；这只是当前实现，不能再写成 v1 保留结论。

证据：

- `Assets/GAS/Runtime/General/GASManager.cs:112-124`
- `Assets/GAS/Runtime/System/SystemGroup/GASGroups.cs`
- `Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs:213-272`

## 当前实际注册表

| 物理执行域 | 当前注册系统 |
|---|---|
| FramePrepare | `GameplayEventBusClearSystem`、`GASGlobalTimerSystem`、`GEEffectCommandSpecStreamFramePrepareSystem`、`OwnerLocalInstantCommandFramePrepareSystem`、`ActiveEffectOwnerLocalMutationFramePrepareSystem`、`GameplayOwnerLocalFactFramePrepareSystem` |
| CommandResolve | `ASCCommandBufferResolveSystem`、`AbilityTryActivateSystem`、`AbilityCommitSystem` |
| CoreSimulation | `GASActiveEffectPreTickSystem`、`GASActiveEffectRemoveSystem`、`GEEffectCommandCatalogNormalizeSystem`、`GEEffectSpecBuildSystem`、`GASActiveEffectMutationApplySystem`、`GEExecutionCalculationSystem`、`GEExecutionCalculationExtensionSystemGroup`、`GEExecutionCalculationOutputModifierSystem`、`GASAttributeSetReduceApplySystem`、`GASAttributeModifierDeltaApplySystem`、`AttributeOwnerMarkerRequestSystem`、`AttributeRecalculateSystem`、`GameplayTagChangeProcessSystem`、`AbilityStateTickSystem`、`AttributeThresholdAbilityLifecycleRequestSystem`、`AbilityLifecycleRequestSystem`、`AbilityStateCleanupSystem`、`GameplayFactProjectionSystem` |
| StructuralCommit | `BeginGASStructuralCommitECBSystem`、`EndGASStructuralCommitECBSystem` |
| BoundaryProjection | `GameplayBoundaryFactExportSystem`、`GameplayFactBoundaryProjectionSystem`、`PresentationOutboxProjectionSystem`、`ReplayLogSystem`、`DiagnosticsSnapshotSystem`、`ASCDestroyFinalizeSystem` |

`GeneratedCommandResolveSystemTypeNames` 与 `GeneratedCoreSimulationSystemTypeNames` 当前为空；主链由 handwritten type array 注册。generated lookup/pure glue 是运行输入，但 generated type-name registry 不是当前调度 owner。

## Cue 调度校准

`CueRequestBridgeSystem` 和 `CueManagedLifecycleSystem` 的 attribute 声明位于 `GASBoundaryProjectionSystemGroup`，但没有进入 `BoundaryProjectionSystemTypes`，因此当前正常 GAS World 不会执行它们。

同时，`GameplayFactBoundaryProjectionSystem.TryCreateCueRequest(...)` 当前写入 `CueEntity = Entity.Null`，而 `CueRequestBridgeSystem.Consume(...)` 对 Null cue entity 直接返回。当前只可证明 Cue fact/counter 存在，不能证明 managed Cue 播放可用。

## 当前 Command -> Spec -> Apply -> Fact

```text
Boundary/AutoChess command
  -> ASC owner-local command buffer
  -> ASCCommandBufferResolve / AbilityTryActivate / AbilityCommit
  -> owner-local instant command 或 active mutation command
  -> GEEffectSpecBuild / ActiveEffect mutation
  -> execution / attribute reduce / attribute recalculate
  -> OwnerLocalGameplayFactBuffer
  -> GameplayBoundaryFactExportSystem
  -> BoundaryObservationFactBuffer
  -> presentation / replay / diagnostics 派生
```

当前正向面：

- command、instant spec、active mutation、部分 delta/fact 已向 ASC owner-local 迁移。
- Catalog/Blob lookup 与 magnitude pure evaluator 已进入 hand-written Runtime consumer。
- execution output 已有 deterministic merge 切片。

当前未闭合面：

- `GEEffectCommandStreamComponent` 仍拥有全局 sequence/counter 和部分 singleton carrier。
- EventBus 同时承载 observation 与 lifecycle/marker request，命令和事实 owner 未完全分开。
-多个 Boundary consumer 仍直接读取 ECS buffer并维护自己的 projection state/cursor。
- `GameplayOwnerLocalFactFlushSystem` 当前不存在；旧文档中 Core flush 回 stream 的描述已过期。

## 当前 Ability 链

Ability 当前是独立 Entity。ASC 的 `AbilitySlotBuffer` 保存 Entity；Ability Entity 持有 state、target、activation/commit/cancel/end/cleanup marker。`AbilityTryActivateSystem` 主要把 target 转给 commit，`AbilityCommitSystem` 做 requirement、owned tag 和 GE command 写入。

当前语义缺口包括：

- Activate 与 Commit 没有完整二次检查/原子失败结果。
- cost affordability、cooldown availability、target rule 和结构化 failure reason 未闭合。
- Ability asset/block/cancel tag 数据没有形成完整运行消费链。
- AbilityTask/continuation 只有概念覆盖标志，没有 runtime state lane。
- 多数配置 Ability 因 `AbilityAutoEndOnCommit` 在 commit 后立即进入结束清理。

## 当前 Effect / Attribute / Tag 链

- ActiveEffect 新主线使用 ASC-local slot，但 slot removal 会 `RemoveAt` 压缩。
- ASC 仍持有 `LegacyGameplayEffectEntityBuffer`；generic execution 仍查询 legacy GE entity。
- Application/Ongoing/Immunity requirement 在生成阶段被压进同一 range，Runtime 无法恢复 phase。
- Attribute 重算按 dirty attribute 遍历全部 active modifier，顺序执行 Add/Subtract/Multiply/Divide/Override；没有统一 Aggregator channel/hook。
- Tag 使用固定 256-bit mask，保留 fixed/temp source；`GameplayTagChangeProcessSystem.OnUpdate` 当前为空。

## 当前同 tick 与下一 tick 行为

| 行为 | 当前时序 |
|---|---|
| tick 前已有 Ability command | activate/commit 及其 GE/Attribute 主链可在同 tick推进 |
| Attribute threshold cancel/end | 同 tick进入 lifecycle 和 cleanup |
| period/overflow 派生 GE | 下一 tick |
| grant Ability / Effect grant Ability | ECB 创建，最早下一 tick查询可见 |
| ASC physical destroy | Boundary finalize 记录已错过当前 StructuralCommit 的 ECB，通常下一 tick playback |

该表是 characterization 输入。目标态哪些保留、哪些有意改变，由 `01/17` 和 V0 语义冻结任务裁决。

## AutoChess runner 当前事实

`AutoChessGasRuntimeTicker` 当前手工依次 `Update()` 五个 GAS 自定义 group，不更新完整 FixedStep 根组。`AutoChessRuntimeSystemBootstrap` 还把 command drive 和 damage calculation 作为自定义 System 插入旧 group。

因此标准 EndFixed ECB、单 kernel 和纯 execution evaluator 必须与 runner/bootstrap 同步切换；只替换 Runtime schedule 会让结构 playback 或业务 damage 链断开。

## 验证状态

当前仓库没有真实 GAS Unity Test Runner 源码：`Assets/_Test` 不存在，tracked `Assets` 下没有 GAS `*Tests.cs`。陈旧 tests csproj、历史 AutoChess report、静态脚本和旧日志不能替代 characterization/semantic tests。

## 不能推出的结论

1. 五段 group 当前能运行，不等于它们应保留到 v1。
2. owner-local buffer 已出现，不等于单 writer/slab/fan-in 已完成。
3. Catalog 字段存在，不等于经典 GAS phase 语义已实现。
4. x50 历史验证通过，不等于真实测试链、Profiler 或 scale gate 已闭合。
5. Cue 类型和 managed lifecycle 代码存在，不等于 Cue 播放链已注册或可用。
