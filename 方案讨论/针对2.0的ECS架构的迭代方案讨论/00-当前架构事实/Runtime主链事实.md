# Runtime 主链事实

> 上次更新：2026-06-02 | 审查范围：`Assets/GAS/Runtime` + `Assets/GAS/Generated/CodeGen/Runtime`

## 已成立事实

### 1. World 与物理调度

1. `GASManager.Initialize()` 创建专属 `World("EX_GAS_World")`，创建 Unity 根 group，并把 `FixedStepSimulationSystemGroup` 加入 `SimulationSystemGroup`。
2. `GASSystemScheduleContract.CreateFixedStepGroups()` 当前创建 5 个 GAS 物理执行域：
   - `GASFramePrepareSystemGroup`
   - `GASCommandResolveSystemGroup`
   - `GASCoreSimulationSystemGroup`
   - `GASStructuralCommitSystemGroup`
   - `GASBoundaryProjectionSystemGroup`
3. `GEExecutionCalculationExtensionSystemGroup` 是 `GASCoreSimulationSystemGroup` 内的扩展插槽，挂在 `GEExecutionCalculationSystem` 之后、`GEExecutionCalculationOutputModifierSystem` 之前。
4. `GASStructuralCommitSystemGroup` 内有 `BeginGASStructuralCommitECBSystem` 和 `EndGASStructuralCommitECBSystem`，是当前结构变化集中化的物理 gate。
5. 当前旧文档中提到的 `GASCommandGroup / GASEffectGroup / GASAttributeGroup / GASAbilityGroup / GASCueGroup / GasStructuralPlaybackSystemGroup` 已不是当前真实注册主链。

### 2. 当前注册系统

当前 `GASSystemScheduleContract.RegisterSystems()` 注册 handwritten runtime systems，并通过 `TryRegisterGeneratedRuntimeSystems()` 注册 generated runtime systems。

| 物理执行域 | 当前系统 |
|---|---|
| `GASFramePrepareSystemGroup` | `GameplayEventBusClearSystem`, `GASGlobalTimerSystem`, `GEEffectCommandSpecStreamFramePrepareSystem` |
| `GASCommandResolveSystemGroup` | `ASCEntityCreateSystem`, `ASCInitializeRequestSystem`, `ASCCommandRequestSystem`, `AbilityCommandRequestSystem`, `AbilityTryActivateSystem`, `ASCDestroyRequestSystem`, generated `AbilityCatalogCommitSystem` |
| `GASCoreSimulationSystemGroup` | `GEExecutionCalculationSystem`, `GEExecutionCalculationExtensionSystemGroup`, `GEExecutionCalculationOutputModifierSystem`, `AttributeRecalculateSystem`, `GameplayTagChangeProcessSystem`, `AbilityStateTickSystem`, `AttributeThresholdAbilityLifecycleRequestSystem`, `AbilityLifecycleRequestSystem`, `AbilityStateCleanupSystem`, `GameplayFactProjectionSystem`, generated `GEEffectCommandCatalogNormalizeSystem`, `GEEffectSpecBuildSystem`, `GASActiveEffectMutationApplySystem`, `GASAttributeSetReduceApplySystem`, `GASActiveEffectPreTickSystem`, `GASActiveEffectRemoveSystem` |
| `GASStructuralCommitSystemGroup` | `BeginGASStructuralCommitECBSystem`, `EndGASStructuralCommitECBSystem` |
| `GASBoundaryProjectionSystemGroup` | `PresentationOutboxProjectionSystem`, `ReplayLogSystem`, `DiagnosticsSnapshotSystem`, `CueRequestBridgeSystem`, `CueStartSystem`, `CueTickSystem`, `CueEndSystem`, `CueDestroySystem`, `ASCDestroyFinalizeSystem` |

补充事实：

1. `Assets/GAS/Runtime` + `Assets/GAS/Generated/CodeGen/Runtime` 当前共检出 39 个 `ISystem` 类型。
2. `GASManagerInputSystem : SystemBase` 仍存在于 Runtime，但当前 `GASSystemScheduleContract` 没有把它注册进 5 段 GAS 主链。
3. `GEEffectCommandIngestSystem`、`GameplayFactEventBridgeSystem`、`GEInstantEffectCueRequestProjectionSystem`、`AttributeChangeEventProjectionSystem` 等类型存在，但当前主注册表未直接注册；后续若要启用，需要先说明与 existing generated systems / `GameplayFactProjectionSystem` 的职责关系，避免重复 projection。

### 3. Command / Spec / Delta / Fact 迁移链

当前代码已经有一条 generated + runtime 混合的迁移期主链：

1. `GameplayEffectRequestWriter` 和 `AbilityCatalogCommitSystem` 可写入 `GEEffectCommandBuffer`。
2. generated `GEEffectSpecBuildSystem` 从 `GEEffectCommandBuffer` 构建 `GEEffectSpecBuffer`。
3. generated `GASAttributeSetReduceApplySystem` 读取 `GEEffectSpecBuffer`，修改目标 ASC 的 `AttributeValueBuffer`，写入 `AttributeModifierBuffer`。
4. `GameplayFactProjectionSystem` 从 `AttributeModifierBuffer` 投影 `GameplayEventBuffer` typed facts，并桥接 legacy EventBus。
5. `PresentationOutboxProjectionSystem` 与 `ReplayLogSystem` 可消费 typed facts / legacy events 输出表现和 replay。

这说明 AM3 不是纯 Contract，但当前仍属于 proof/migration 阶段：

- command/spec/delta/fact 都挂在 singleton `GEEffectCommandStreamComponent` owner 的 DynamicBuffer 上。
- 关键 generated systems 仍以主线程 `SystemAPI.Query` 和 buffer for 循环为主。
- 规模化 fan-in 的 `NativeStream` / deterministic merge / owner-local compact range 尚未落地。
- legacy EventBus bridge 仍存在，不能把 typed fact 完整闭环当成已完成事实。

### 4. ActiveEffectStore 当前状态

1. `ActiveEffectStore` 已有 `ASCActiveEffectsComponent`、`ActiveGameplayEffectBuffer`、global index owner/bucket/row 等数据结构。
2. generated `GASActiveEffectMutationApplySystem`、`GASActiveEffectPreTickSystem`、`GASActiveEffectRemoveSystem` 已挂入 CoreSimulation。
3. 当前 ActiveEffectStore 是 owner-local store + generated/runtime 混合迁移期实现，不是完全 store-driven lifecycle 终局。
4. Duration/stack/period/granted cleanup 仍需要继续核对是否完全脱离 legacy runtime GE entity lifecycle。

### 5. Definition / Generated 链

1. `GASDefinitionCatalogRuntimeTypes` 定义 `GASDefinitionCatalogBlob` 与 runtime catalog component。
2. generated runtime 通过 `GASDefinitionCatalogComponent` 读取 blob catalog，而不是在 hot path 直接读取 JSON/Excel row。
3. `GameplayEffectConfigRegistry`、`GameplayEffectComponentConfig`、GE static component config 类仍存在，并在 prototype/static definition 路径使用 `GASManager.EntityManager` 写 ECS。
4. 这些 managed config/prototype 写入可作为初始化/authoring 迁移期路径，但不应进入 Runtime Core hot path。

### 6. Observation / Debugger

1. `GameplayEventBusComponent` 仍作为 singleton event bus 存在，承载 damage/tag/attribute/cue/gameplay/presentation owner buffers。
2. `GameplayFactProjectionSystem` 会把 typed facts 桥接 legacy EventBus。
3. `PresentationOutboxProjectionSystem`、`ReplayLogSystem`、`DiagnosticsSnapshotSystem` 位于 `GASBoundaryProjectionSystemGroup`。
4. `GasRuntimeOfficialToolDiff` 使用 `EntitiesJournaling` 统计 create/destroy/add/remove/enable/disable/set/get 等记录，是结构变化收口的有效证据工具。
5. `GasRuntimeDebugger` 仍有 observation-only `ToEntityArray` 和同步 query；这类成本必须与 Core Simulation 成本拆分报告。

## 当前 DOTS 合规缺口

### P0：Runtime Boundary 仍可绕过结构变化 gate

`ASCCommandGateway` 仍直接通过 `GASManager.EntityManager` 创建 request entity：

- `ASCDestroyRequest`
- `ASCInitializeRequest`
- `GERemoveRequest`
- `AbilityCommandRequest`
- `ASCCommandRequest`

这与 `SC-01`、`PRF-04` 的目标冲突。即使这些操作来自外部边界，也应该进入明确 command sink / request phase，而不是让 facade 自己同步创建 entity。

### P0：同步等待仍在核心系统中存在

当前 `state.Dependency.Complete()` 命中 10 处，涉及：

- `ASCDestroyRequestSystem`
- `ASCDestroyFinalizeSystem`
- `AttributeThresholdAbilityLifecycleRequestSystem`
- `GEExecutionCalculationSystem`
- `GEExecutionCalculationOutputModifierSystem`
- generated `RuntimeActiveEffect.gen.cs`

其中 destroy/finalize/cleanup 可作为低频边界单独评估；ExecutionCalculation、ActiveEffect tick/remove 需要优先做 job chain / deterministic candidate pipeline 复核。

### P1：Hot path 主线程 Query 问题从 `ToEntityArray` 转为 `SystemAPI.Query` / buffer for loop

旧文档中的 “22 个 ToEntityArray” 已不符合当前代码。当前更准确的问题是：

1. `ToEntityArray` 主要留在 Debugger observation 路径。
2. Runtime Core 多处仍使用主线程 `SystemAPI.Query`、DynamicBuffer for 循环、`EntityManager.GetBuffer/GetComponentData`。
3. 根据官方文档，`SystemAPI.Query` 是主线程 foreach，并会自动完成相关依赖；因此不能把它当作 hot path scale-ready 答案。

### P1：Singleton DynamicBuffer 仍是 proof-only carrier

`GEEffectCommandStreamComponent` owner 上承载：

- `GEEffectCommandBuffer`
- `GESetByCallerValueBuffer`
- `GEEffectSpecBuffer`
- `AttributeModifierBuffer`
- `ActiveEffectMutationBuffer`
- `GameplayEventBuffer`

这符合迁移期最小接入成本，但违反 `BUF-02` 的终局要求。后续必须按数据性质分别收敛到 `NativeStream`、target-grouped range、owner-local store 或 compact buffer。

### P1：Generated runtime 需要同等 DOTS 审查

generated code 当前是实际执行链一部分，不能被“生成代码”身份豁免：

1. 使用 `SystemAPI.Query` 时要说明为什么不是 job。
2. 使用 `EntityManager` 时要说明是否属于低频边界、是否触发结构变化。
3. 使用 singleton/blob 时要说明 dependency policy。
4. 写 attribute/active store 时要给出 query、buffer pressure、deterministic ordering 和 battle hash 证据。

## 当前总诊断

当前 Runtime Core 的事实已经从“旧 lifecycle + 大量 ToEntityArray”推进到“5 段物理 phase + generated catalog + singleton stream proof”。这是重要进展，但不能据此判断架构已经优秀。

真正的下一步不是继续堆新业务机制，而是：

1. 收紧 Runtime Boundary 写入口，消除 facade 同步创建 request entity。
2. 把 generated runtime 纳入 DOTS 规则审查。
3. 将 ExecutionCalculation / ActiveEffect tick/remove 的 scan + `Complete()` 改为可解释的 job chain。
4. 把 singleton stream proof 拆成按数据性质选型的 scale-ready carrier。
5. 用 `GasRuntimeOfficialToolDiff`、Profiler、Debugger counters、battle hash 和 x50/x1000 规模门证明结构变化、fan-in、observation 成本真正收口。
