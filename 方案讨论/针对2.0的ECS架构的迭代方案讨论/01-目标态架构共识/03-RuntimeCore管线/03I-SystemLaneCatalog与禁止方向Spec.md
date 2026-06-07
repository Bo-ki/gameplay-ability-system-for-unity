# 03I：System / Lane Catalog 与禁止方向

> Owner：`01-目标态架构共识/03-RuntimeCore管线` | 状态：目标态子 Spec | 拆分来源：`../03-RuntimeCore管线Spec.md` | 最近拆分：2026-06-07

本文件只描述理想 Runtime Core 目标态。禁止写入当前代码事实、迁移流水、验证数字或下一步任务；现实证据必须回到 `../../00-当前架构事实/`，任务拆分必须回到 `../../02-主线任务树/`。

定位：目标态 System/Lane Catalog、禁止方向和历史方案定位。

## 附录：目标 System / Lane Catalog

以下清单只描述目标命名和职责，不表达实现状态。旧 `GASSpecEvaluationSystemGroup` / `GASDeltaApplySystemGroup` / `GASGameplayEventProjectionSystemGroup` 以及“每 kernel 一个 group”的名称不是新任务目标命名。

### GASFramePrepareSystemGroup

| System | 目标职责 |
|---|---|
| `GASFrameArenaSetupSystem` | 帧首刷新 lookup handle、rewind scratch allocator、输出 dependency budget |

### GASCommandResolveSystemGroup

| Lane | System | 目标职责 |
|---|---|---|
| Boundary Command Ingest | `AbilityCommandIngestSystem` | 消费 Boundary ability request + Definition Catalog → normalized ability command / cost / cooldown seed |
| Boundary Command Ingest | `GEBoundaryCommandIngestSystem` | 消费低频外部 GE apply request → normalized GE command seed |
| Target Resolve | `AbilityTargetResolveSystem` / `ResolveAbilityCommandTargetsJob` | command record / request target rule / physics snapshot / self target → `AbilityTargetRecord` NativeStream；低量物化路径可写 request-owned `TargetDataBuffer` |
| Target Resolve | `TargetSortSystem` | 按 battle-deterministic key 排序 target，避免依赖 query/chunk 隐式顺序 |

### GASCoreSimulationSystemGroup

| Lane | System | 目标职责 |
|---|---|---|
| Effect Fan-In | `GASEffectFanInSystem` | 多来源 command producer → `NativeStream` → deterministic merge |
| Effect Fan-In | `GEEffectSpecBuildSystem` | command + GE definition → resolved modifier candidate；实现上可作为 fan-in 内部 job 或独立 lane system |
| Effect Fan-In / Attribute Reduce | `GEExecutionCalculationOutputModifierSystem` | execution output → resolved modifier record → target ASC chunk job apply；不得逐 effect 主线程 random write |
| Effect Fan-In | `PeriodOverflowCommandDeriveSystem` | period / overflow producer，作为 fan-in producer |
| State Evaluate / PreTick | `GASActiveEffectPreTickSystem` 或 `GASEffectFanInSystem` 内 producer job | owner-local active effect period / expire seed → Effect Fan-In producer；是否独立 system 由 `SYS-03` / `PRF-07` 决定 |
| State Evaluate / PostApply | `GASActiveEffectPostApplySystem` | owner-local active effect slot enum / duration / stack / inhibit / chunk skip |
| State Evaluate | `AbilityStateEvaluateSystem` | ability active / cooldown / cancel / end state |
| State Evaluate | `ChunkComponentMaintainSystem` | 维护 `AllIdleChunkComponent` / `NoActiveEffectsChunkComponent` |
| Attribute Reduce/Apply | `GASAttributeSetReduceApplySystem` | target-grouped AttributeSet modifier reduce / apply |
| Attribute Reduce/Apply | `AttributeModifierApplySystem` | owner-local / target-grouped modifier range → AttributeSet 写入 |
| Gameplay Fact | `GameplayFactProjectionSystem` | Attribute / Cue / Damage typed fact projection |
| Gameplay Fact | `GameplayReactionSystem` | Core reaction：Ability trigger / reactive GE command seed（默认 next-frame，不进入 Boundary） |
| Gameplay Fact | `CueRequestProjectionSystem` | Cue fact 进入 boundary fact，不直接表现 side effect |

### GASStructuralCommitSystemGroup

| System | 目标职责 |
|---|---|
| `BeginGASStructuralCommitECBSystem` | ECB playback（OrderFirst，默认少用） |
| `EndGASStructuralCommitECBSystem` | ECB playback（OrderLast）- destroy、cleanup、grant/revoke ability |
| `GASAbilityDestroyCommitSystem` | pending destroy ability → ECB destroy |
| `FrameEndCleanupSystem` | frame-local compact owner buffer 清空、stream counter 重置 |

### GASBoundaryProjectionSystemGroup

| System | 目标职责 |
|---|---|
| `PresentationOutboxSystem` | `GameplayEventBuffer` / typed fact range → `PresentationEventBuffer` |
| `ReplayLogSystem` | `GameplayEventBuffer` / typed fact range → replay event |
| `DiagnosticsSnapshotSystem` | Debugger counters → `RuntimeDiagnosticsSnapshot` |

> **`PRF-07` 说明 —— 不拆分 Group**：三个 System 共享相同的输入 Query（`GameplayEventBuffer`），合并在同一 SystemGroup 是正确的。每个 System 有固定的 TypeHandle 刷新 + Lookup 创建 + Dependency 链开销，不必要地拆分为多个 Group 会增加固定成本（`PRF-07`）。
>
> **`DiagnosticsSnapshotSystem` 采样频率控制**：不同于 `PresentationOutboxSystem` 需要每帧执行（低延迟 UI 反馈），`DiagnosticsSnapshotSystem` 应在其内部用帧计数器控制采样频率（如每 60 帧采样一次），而非通过拆分 Group 实现。三个 System 的写入频率、消费者和性能预算不同，通过**内部采样控制**而非**Group 拆分**来解决。
>
> ```
> PresentationOutboxSystem:  每帧执行（UI 低延迟要求）
> ReplayLogSystem:           每帧执行（确定性回放要求）
> DiagnosticsSnapshotSystem: 每 N 帧采样（N 可配置，默认 60）
> ```

---

## 禁止方向

1. Simple instant GE 默认创建 runtime GE entity。
2. 业务 reaction 扫描全局 observation event 作为主输入。
3. Presentation / Replay / Debugger 混入 core simulation tick。
4. 在 Target Resolve / Effect Fan-In / State Evaluate / Attribute Apply / Gameplay Fact 中直接执行 `EntityManager` 结构变化。
5. 把 request entity 当作高频 instant GE 的默认 command 载体。
6. **更新：** 在 Target Resolve / Effect Fan-In / Attribute Apply / Gameplay Fact 中做 enableable toggle。
7. **新增：** 使用 Unity 默认的 `BeginSimulationEntityCommandBufferSystem` / `EndSimulationEntityCommandBufferSystem` 做 Runtime Core 结构变化（playback 位置不对）。
8. **新增：** 在 hot path 临时创建 EntityQuery，或把 ComponentLookup / BufferLookup / TypeHandle 放进中央 singleton registry；EntityQuery 必须由 owner `ISystem.OnCreate` 通过 `SystemState.GetEntityQuery` 创建，Lookup / TypeHandle 必须由 owner `ISystem.OnUpdate` 刷新并计数。
9. **新增：** 使用同步 enableable-filtered query 而不评估 sync point 成本。
10. **新增：** 把 proof-only `GEStreamOwnerSingleton` / 大容量 singleton DynamicBuffer / 大容量 per-ASC frame buffer 当成 scale-ready 目标态。
11. **新增：** Runtime Core 反查 Luban managed row、JSON、`Dictionary`、`Func<>` registry，或把 per-definition entity query 当成每帧配置 lookup。

## 历史方案定位

1. Ability command 作为纯 ECS 激活入口的设计信号来自 `../../历史方案参考/方案15.md:199-232`。
2. OOP 只通过边界层 command gateway 发命令、ECS 侧写 request 的边界来自 `../../历史方案参考/方案14.md:265-335`。
3. Command Buffer / Event Buffer 单向边界来自 `../../历史方案参考/方案11.md:20-43`。
4. Attribute 计算从托管 helper 迁移到 unmanaged / Burst-friendly 计算的信号来自 `../../历史方案参考/方案15.md:350-466`。
