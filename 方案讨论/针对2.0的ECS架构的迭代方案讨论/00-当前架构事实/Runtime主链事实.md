# Runtime 主链事实

> 上次更新：2026-06-06 | 审查范围：`Assets/GAS/Runtime` + `Assets/GAS/Generated/CodeGen/Runtime`

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
| `GASCommandResolveSystemGroup` | `ASCCommandBufferResolveSystem`, `AbilityTryActivateSystem`, generated `AbilityCatalogCommitSystem`, `AbilityCommitSystem` |
| `GASCoreSimulationSystemGroup` | `GEExecutionCalculationSystem`, `GEExecutionCalculationExtensionSystemGroup`, `GEExecutionCalculationOutputModifierSystem`, `AttributeRecalculateSystem`, `GameplayTagChangeProcessSystem`, `AbilityStateTickSystem`, `AttributeThresholdAbilityLifecycleRequestSystem`, `AbilityLifecycleRequestSystem`, `AbilityStateCleanupSystem`, `GameplayFactProjectionSystem`, generated `GEEffectCommandCatalogNormalizeSystem`, `GEEffectSpecBuildSystem`, `GASActiveEffectMutationApplySystem`, `GASAttributeSetReduceApplySystem`, `GASActiveEffectPreTickSystem`, `GASActiveEffectRemoveSystem` |
| `GASStructuralCommitSystemGroup` | `BeginGASStructuralCommitECBSystem`, `EndGASStructuralCommitECBSystem` |
| `GASBoundaryProjectionSystemGroup` | `PresentationOutboxProjectionSystem`, `ReplayLogSystem`, `DiagnosticsSnapshotSystem`, `CueRequestBridgeSystem`, `CueStartSystem`, `CueTickSystem`, `CueEndSystem`, `CueDestroySystem`, `ASCDestroyFinalizeSystem` |

补充事实：

1. `Assets/GAS/Runtime` + `Assets/GAS/Generated/CodeGen/Runtime` 当前共检出 33 个 `ISystem` 类型。
2. `GASManagerInputSystem : SystemBase` 仍存在于 Runtime，但当前 `GASSystemScheduleContract` 没有把它注册进 5 段 GAS 主链。
3. `GEEffectCommandIngestSystem`、`AttributeChangeEventProjectionSystem` 等类型存在，但当前主注册表未直接注册；后续若要启用，需要先说明与 existing generated systems / `GameplayFactProjectionSystem` 的职责关系，避免重复 projection。旧 `GameplayFactEventBridgeSystem` 与 `GEInstantEffectCueRequestProjectionSystem` 已删除，不再作为未注册残留系统保留。
4. `AbilityCommitSystem` 是 CommandResolve 组内的真实 fence。跨组 `[UpdateBefore(typeof(GEEffectCommandIngestSystem))]` 已从 `ASCCommandBufferResolveSystem`、`AbilityCommitSystem`、generated `AbilityCatalogCommitSystem` 及 codegen 模板移除；CommandResolve -> CoreSimulation 的先后由 physical group 顺序保证，避免 Entities 忽略无效跨组排序属性。

### 2.1 证据等级矩阵

| 项 | 等级 | 说明 |
|---|---|---|
| `FixedStepGroupTypes` / `RegisterSystems()` | runtime-active | 当前 World 初始化真实创建和注册 |
| `RuntimeCoreFramePhaseContracts` | contract-only | 描述 8 个逻辑 phase 的读写权限；不是 8 个物理 group |
| `RuntimeCoreFramePhaseSystemContracts` | partial mapping | 当前只覆盖 5 个系统的 phase mapping，不能视为全系统 phase table |
| generated registration reflection | runtime-active but fragile | 通过程序集名字符串反射注册；若 generated asmdef/type 缺失会静默跳过 |
| AutoChess system bootstrap | demo-extension | 只为 demo world 插入 command drive / execution extension，不属于通用 Runtime 注册表 |

### 3. Command / Spec / Delta / Fact 迁移链

当前代码已经有一条 generated + runtime 混合的迁移期主链：

1. `ASCCommandGateway` / AutoChess command drive 写入 ASC owner-local `ASCCommandBuffer`、`AbilityCommandBuffer`、`ASCDestroyCommandBuffer`；`ASCCommandBufferResolveSystem` 在 CommandResolve phase 消费。
2. `GameplayEffectRequestWriter` 和 generated `AbilityCatalogCommitSystem` 可写入 `GEEffectCommandBuffer`；`AbilityCatalogCommitJob` 当前用 `ComponentTypeHandle<AbilityCommitRequestComponent>` + chunk `EnabledMask` 关闭 commit request，不再做同实体 random-access enableable 开关。
3. generated `GEEffectSpecBuildSystem` 从 `GEEffectCommandBuffer` 构建 `GEEffectSpecBuffer`；当前 `InstantSpecBuildJob` 是 scheduled `[BurstCompile] IJob`，且 cue-only instant GE 可通过 `GameplayCueCode > 0` 生成 spec。
4. generated `GASAttributeSetReduceApplySystem` 读取 `GEEffectSpecBuffer`，修改目标 ASC 的 `AttributeValueBuffer`，写入 `AttributeModifierBuffer`；当前 `AttributeSetReduceApplyJob` 是 scheduled `[BurstCompile] IJob`。
5. `GameplayFactProjectionSystem` 从 `AttributeModifierBuffer` 投影 `GameplayEventBuffer` typed facts，并桥接 legacy EventBus。
6. `PresentationOutboxProjectionSystem` 与 `ReplayLogSystem` 可消费 typed facts / legacy events 输出表现和 replay。

这说明 AM3 不是纯 Contract，但当前仍属于 proof/migration 阶段：

- command/spec/delta/fact 都挂在 singleton `GEEffectCommandStreamComponent` owner 的 DynamicBuffer 上。
- `GEEffectCommandSpecStreamFramePrepareSystem` 已从主线程 `EntityManager.GetBuffer` compact/clear 改为 scheduled `IJob`；`GameplayFactProjectionSystem` 已从主线程 projection/legacy bridge 改为 scheduled `IJob`。
- `GEExecutionCalculationSystem`、`GEExecutionCalculationOutputModifierSystem`、`GASActiveEffectPreTickSystem`、`AbilityStateCleanupSystem` 已从 `Complete()` / 主线程 cleanup 消费改为 scheduled job chain；其中 output modifier 使用 `NativeStream` fan-in 后在 scheduled merge job 内排序、应用属性、写 delta。
- `ASCCommandBufferResolveSystem`、generated `AbilityCatalogCommitSystem`、generated `GEEffectCommandCatalogNormalizeSystem`、generated `GEEffectSpecBuildSystem`、generated `GASAttributeSetReduceApplySystem`、generated `GASActiveEffectPreTickSystem`、generated `GASActiveEffectRemoveSystem` 均已迁到 scheduled `IJob` / `IJobChunk` 路径，并且 codegen 模板已同步；generated `GASActiveEffectMutationApplySystem` 仍有 singleton DynamicBuffer serial loop / `EntityManager` 过渡实现，不能写成 scale-ready 终局。
- generated instant spec/reduce 路径的 ASC 可用性判断已按 `ASCDestroyingComponent` enabled bit 读取销毁态；默认 disabled 的正常 ASC 不再被 `HasComponent` 误判为 destroying。
- generated active lifecycle job 内仍有 random-access enableable 开关，包括 remove pending、ability cancel/destroy cleanup、active modifier present、attribute dirty 等 lookup；这不是旧主线程遍历问题，但按 `EN-03` / `CASE-20` 仍是 P1 残留。
- 规模化 fan-in 只在部分链路落地；singleton stream 仍是 command/spec/delta/fact/active mutation 的 proof carrier。
- legacy EventBus bridge 仍存在，不能把 typed fact 完整闭环当成已完成事实。

### 4. ActiveEffectStore 当前状态

1. `ActiveEffectStore` 已有 `ASCActiveEffectsComponent`、`ActiveGameplayEffectBuffer`、global index owner/bucket/row 等数据结构。
2. generated `GASActiveEffectMutationApplySystem`、`GASActiveEffectPreTickSystem`、`GASActiveEffectRemoveSystem` 已挂入 CoreSimulation。
3. `GASActiveEffectPreTickSystem` 当前已不再 `SystemAPI.Query` 预扫，也不再 `NativeStream scan -> state.Dependency.Complete() -> 主线程 ApplyActiveEffectTickRecord`；旧 scan/apply helper 已从 generated 输出和模板中删除。它调度 `GEActiveEffectPreTickJob`，在 job 内处理 period command、duration expire、modifier/tag/ability cleanup、mutation/event 输出。
4. `GASActiveEffectRemoveSystem` 当前也复用 scheduled `GEActiveEffectPreTickJob` 的 explicit remove 分支消费 ASC owner-local `GERemoveCommandBuffer`，不再使用 `SystemAPI.Query` 主线程 foreach / `EventBusHelper` / `EntityManager` remove helper。
5. 当前 ActiveEffectStore 仍是 owner-local store + generated/runtime 混合迁移期实现，不是完全 store-driven lifecycle 终局。
6. Active mutation apply 仍需要继续核对是否完全脱离 legacy runtime GE entity lifecycle 与主线程 `EntityManager` helper。

### 5. Definition / Generated 链

1. `GASDefinitionCatalogRuntimeTypes` 定义 `GASDefinitionCatalogBlob` 与 runtime catalog component。
2. generated runtime 通过 `GASDefinitionCatalogComponent` 读取 blob catalog，而不是在 hot path 直接读取 JSON/Excel row。
3. `GameplayEffectConfigRegistry`、`GameplayEffectComponentConfig`、GE static component config 类仍存在，并在 prototype/static definition 路径使用 `GASManager.EntityManager` 写 ECS。
4. 这些 managed config/prototype 写入可作为初始化/authoring 迁移期路径，但不应进入 Runtime Core hot path。

### 6. Observation / Debugger

1. `GameplayEventBusComponent` 仍作为 singleton event bus 存在，承载 damage/tag/attribute/cue/gameplay/presentation owner buffers。
2. `GameplayFactProjectionSystem` 会把 typed facts 桥接 legacy EventBus；当前桥接已由 scheduled `IJob` 内的 lookup writer 完成，不再使用 `EventBusHelper.GameplayEventBusWriter` 主线程 writer。
3. `PresentationOutboxProjectionSystem`、`ReplayLogSystem`、`DiagnosticsSnapshotSystem` 位于 `GASBoundaryProjectionSystemGroup`。
4. `GasRuntimeOfficialToolDiff` 使用 `EntitiesJournaling` 统计 create/destroy/add/remove/enable/disable/set/get 等记录，是结构变化收口的有效证据工具。
5. `GasRuntimeDebugger` 仍有 observation-only `ToEntityArray` 和同步 query；这类成本必须与 Core Simulation 成本拆分报告。

## 当前 DOTS 合规缺口

### 已缓解：Runtime Boundary request entity 链路已退场

`ASCCommandGateway` 当前不再创建 transient request entity。`Dispose()`、`Init()`、ASC command、Ability command、GE remove/clear 均写入 ASC owner-local command buffer，并通过 `ASCCommandPendingComponent` / `GERemoveCommandPendingComponent` 触发消费。

已删除旧 `ASCEntityCreateSystem`、`ASCInitializeRequestSystem`、`ASCCommandRequestSystem`、`AbilityCommandRequestSystem`、`ASCDestroyRequestSystem` 和对应 `*RequestComponent`。当前仍需防止旧 request entity 命名/兼容桩回流。

### 已缓解：核心 `state.Dependency.Complete()` 当前清零

当前 `Assets/GAS/**/*.cs` 中 `state.Dependency.Complete()` 扫描结果为 0。第二轮深修已把以下链路改为 scheduled job chain：

| 链路 | 当前状态 |
|---|---|
| `AttributeThresholdAbilityLifecycleRequestSystem` | threshold lifecycle request 由 `IJobChunk` 直接写 enableable request，并通过 ECB 追加 lifecycle fact |
| `ASCDestroyFinalizeSystem` | destroy finalize 由并行 scan jobs + scheduled apply job 完成，不再主线程阻塞合并 |
| `GEExecutionCalculationSystem` | execution calculation 由 `IJobChunk` 并行解析输入并写 effect-local output buffer |
| `GEExecutionCalculationOutputModifierSystem` | 删除小规模主线程 fallback；`NativeStream` fan-in 后 scheduled merge job 确定性排序、应用属性、追加 delta |
| `AbilityStateCleanupSystem` | ability cancel/end cleanup 由 scheduled `IJobChunk` 写 enableable request、owner-local ability slot、临时 tag、event bus 和 structural ECB；旧主线程 `SystemAPI.Query` + `EntityManager` helper 退场 |
| generated `GASActiveEffectPreTickSystem` / `GASActiveEffectRemoveSystem` / `GasGlueCodeGenPhases` | 删除 generated pre-scan、explicit remove `SystemAPI.Query` 与 `Complete()`；模板和生成结果同步为 scheduled `GEActiveEffectPreTickJob` |
| `GEEffectCommandSpecStreamFramePrepareSystem` / `GameplayFactProjectionSystem` | stream compact/clear 与 typed fact projection 已改为 scheduled `IJob`；未注册的旧 fact bridge / cue projection system 已删除 |
| generated `AbilityCatalogCommitSystem` | commit request 关闭改为 chunk `EnabledMask`，不再使用 `ComponentLookup.SetComponentEnabled` 同实体随机访问 |
| generated `GEEffectSpecBuildSystem` / `GASAttributeSetReduceApplySystem` | codegen 输出 scheduled `[BurstCompile] IJob`，并修复 cue-only spec 与 `ASCDestroyingComponent` enabled bit 判定 |

本条不等于 Runtime Core 已达终局：singleton stream 写入、ActiveMutation 的 `EntityManager` helper、legacy EventBus writer 仍需继续按 `QRY-01`、`PRF-09`、`BUF-02`、`NAT-03` 审查。

### P1：Hot path 主线程 Query 已继续收窄，只剩 Cue managed boundary

旧文档中的 “22 个 ToEntityArray” 已不符合当前代码。当前更准确的问题是：

1. `ToEntityArray` 主要留在 Debugger observation 路径。
2. `AutoChessBattleCommandDriveSystem`、`AutoChessExecuteDamageCalculationSystem`、`AbilityStateCleanupSystem` 已迁到 scheduled job；Core 主链的 `AttributeRecalculateSystem` 小规模主线程 fallback、ASC command resolve、generated ability commit、generated normalize/spec-build/reduce、generated ActiveEffect pre-scan、explicit remove 主线程 foreach、stream frame prepare 和 fact projection 主线程 buffer loop 均已退场。
3. `Assets/GAS` 内 `SystemAPI.Query<...>` 当前只命中 Cue start/tick/end/destroy managed boundary。
4. Runtime Core 仍存在 generated active mutation DynamicBuffer for loop、singleton stream serial owner、`EntityManager.GetBuffer/GetComponentData` 过渡路径。
5. 按 `QRY-01`、`PRF-05`、`CASE-01` 的规则口径，`SystemAPI.Query` 仍只能作为 managed boundary / proof / debug 工具，不能重新进入 Core hot path。

当前检出：

| 模式 | 命中 | 当前归类 |
|---|---|---|
| `SystemAPI.Query<...>` | `CueStart/End/Tick/Destroy` | managed cue boundary |
| `ToEntityArray()` | `GasRuntimeDebugger.cs:1969/2167`, `AutoChessBattleDefinitionCatalogBuilder.cs:123/141` | observation / demo initialization |
| `CalculateEntityCount()` | `GasRuntimeDebugger.cs:534` | observation-only |
| `CalculateChunkCountWithoutFiltering()` | `AbilityStateCleanupSystem`、`GEExecutionCalculationSystem`、`GEExecutionCalculationOutputModifierSystem`、`ASCDestroyFinalizeSystem`、AutoChess command drive | scheduled job sizing；按 PRF-09 避免 enableable/filter sync，并匹配 `IJobChunk` unfiltered chunk index |
| direct `CreateEntity()` facade | `ASCCommandGateway.Create()` -> `ASCEntityFactory.Create()` | 低频 owner 创建入口；不得扩展为 transient request entity |

### P0/P1：direct `EntityManager` 命中必须按 owner 分类

当前 broad scan 能看到 44 处 `CreateEntity()` / `DestroyEntity()` 命中。它们不能被写成同一种缺陷，必须按 owner 和相位区分：

| 类别 | 代表命中 | 当前分类 |
|---|---|---|
| World/bootstrap singleton 初始化 | `GASManager.cs:57/133/149`, `GEEffectCommandSpecStream.cs:356` | init-only，可保留但不能混入 hot path |
| Boundary facade owner 创建 | `ASCCommandGateway.Create()` -> `ASCEntityFactory.Create()` | 低频 owner 创建入口，不计作 request entity churn |
| System 内 ECB 创建/销毁 | `ASCCommandBufferResolveSystem.cs`, `AbilityStateCleanupSystem.cs`, `RuntimeActiveEffect.gen.cs` | 结构变化方向正确，仍需 Journaling 相位证据 |
| Demo adapter 直接创建/销毁 | `AutoChessGasCoreBridge.cs:148/199/211/335/360`, `AutoChessBattleDefinitionCatalogBuilder.cs:135` | Demo 集中接缝，unit/effect cleanup 需迁入 request/commit owner |
| Managed cue/presentation | `GameplayCueUnit.cs:113`, `ConfCueBase.cs:21`, `CueRequestBridgeSystem.cs:111`, `CueDestroySystem.cs:28` | Boundary managed path |
| Config/prototype cache | `GameplayEffectEntityFactory.cs:11/24`, `GameplayEffectConfigRegistry.cs:688/730` | 初始化/prototype path |
| ActiveEffect store owner/bucket | `ActiveEffectStore.cs:344/1495` | Core store owner 初始化或扩容，需 capacity/phase 证据 |
| Debugger entity | `GasRuntimeDebugger.cs:767` | observation-only |

### P1：Singleton DynamicBuffer 仍是 proof-only carrier

`GEEffectCommandStreamComponent` owner 上承载：

- `GEEffectCommandBuffer`
- `GESetByCallerValueBuffer`
- `GEEffectSpecBuffer`
- `AttributeModifierBuffer`
- `ActiveEffectMutationBuffer`
- `GameplayEventBuffer`

`AbilityCommandBuffer` 已从 singleton stream owner 迁出，当前只保留在 ASC owner-local command buffer。上述 stream 仍符合迁移期最小接入成本，但违反 `BUF-02` 的终局要求。后续必须按数据性质分别收敛到 `NativeStream`、target-grouped range、owner-local store 或 compact buffer。

### P1：Generated runtime 需要同等 DOTS 审查

generated code 当前是实际执行链一部分，不能被“生成代码”身份豁免：

1. 使用 `SystemAPI.Query` 时要说明为什么不是 job；当前 Core hot path 不允许重新引入主线程 foreach。
2. 使用 `EntityManager` 时要说明是否属于低频边界、是否触发结构变化。
3. 使用 singleton/blob 时要说明 dependency policy。
4. 写 attribute/active store 时要给出 query、buffer pressure、deterministic ordering 和 battle hash 证据。
5. codegen 模板必须与 generated output 同步；当前 ability activation、instant spec/reduce 与 active effect pre-tick/remove 模板已同步，后续仍需 static validation 防止旧 request entity / Temp ECB playback / `Complete()`、未 Burst hot job、random enableable 开关或 `ASCDestroyingComponent` `HasComponent` 误判回流。注意：active lifecycle 当前仍存在 random enableable 残留，因此 static validation 应区分“已修复链路防回流”和“未修复链路阻断新增”。

## 当前总诊断

当前 Runtime Core 的事实已经从“旧 lifecycle + 大量 ToEntityArray”推进到“5 段物理 phase + generated catalog + singleton stream proof”。这是重要进展，但不能据此判断架构已经优秀。

真正的下一步不是继续堆新业务机制，而是：

1. 把 generated runtime 纳入 DOTS 规则审查。
2. 继续拆 `GASActiveEffectMutationApplySystem` 的主线程 buffer loop / `EntityManager` helper，并为 singleton stream 的剩余 serial owner 写入补 deterministic merge / capacity 证据。
3. 把 singleton stream proof 拆成按数据性质选型的 scale-ready carrier，并补 deterministic merge / capacity 证据。
4. 用 `GasRuntimeOfficialToolDiff`、Profiler、Debugger counters、battle hash 和 x50/x1000 规模门证明结构变化、fan-in、observation 成本真正收口。
5. 保持 Boundary request entity 旧链路退场，不允许兼容桩或旧 system 回流。
