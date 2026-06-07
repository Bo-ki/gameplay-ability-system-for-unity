# Runtime 主链事实

> 上次更新：2026-06-07 | 审查范围：`Assets/GAS/Runtime` + `Assets/GAS/Generated/CodeGen/Runtime`

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

当前 `GASSystemScheduleContract.RegisterSystems()` 注册 handwritten runtime systems，并通过 `GeneratedCommandResolveSystemTypeNames` / `GeneratedCoreSimulationSystemTypeNames` 这两组手写 type-name 列表反射注册 generated runtime systems。

| 物理执行域 | 当前系统 |
|---|---|
| `GASFramePrepareSystemGroup` | `GameplayEventBusClearSystem`, `GASGlobalTimerSystem`, `GEEffectCommandSpecStreamFramePrepareSystem` |
| `GASCommandResolveSystemGroup` | `ASCCommandBufferResolveSystem`, `AbilityTryActivateSystem`, generated `AbilityCatalogCommitSystem`, `AbilityCommitSystem` |
| `GASCoreSimulationSystemGroup` | `GEExecutionCalculationSystem`, `GEExecutionCalculationExtensionSystemGroup`, `GEExecutionCalculationOutputModifierSystem`, `GASAttributeModifierDeltaApplySystem`, `AttributeOwnerMarkerRequestSystem`, `AttributeRecalculateSystem`, `GameplayTagChangeProcessSystem`, `AbilityStateTickSystem`, `AttributeThresholdAbilityLifecycleRequestSystem`, `AbilityLifecycleRequestSystem`, `AbilityStateCleanupSystem`, `GameplayFactProjectionSystem`, generated `GEEffectCommandCatalogNormalizeSystem`, `GEEffectSpecBuildSystem`, `GASActiveEffectMutationApplySystem`, `GASAttributeSetReduceApplySystem`, `GASActiveEffectPreTickSystem`, `GASActiveEffectRemoveSystem` |
| `GASStructuralCommitSystemGroup` | `BeginGASStructuralCommitECBSystem`, `EndGASStructuralCommitECBSystem` |
| `GASBoundaryProjectionSystemGroup` | `GameplayFactBoundaryProjectionSystem`, `PresentationOutboxProjectionSystem`, `ReplayLogSystem`, `DiagnosticsSnapshotSystem`, `CueRequestBridgeSystem`, `CueManagedLifecycleSystem`, `ASCDestroyFinalizeSystem` |

补充事实：

1. `Assets/GAS/Runtime` + `Assets/GAS/Generated/CodeGen/Runtime` 当前共检出 31 个 `ISystem` 类型。
2. `GASManagerInputSystem : SystemBase`、`GEEffectCommandIngestSystem`、`AttributeChangeEventProjectionSystem` 均已删除；当前 Runtime 主链不再保留这些未注册残留类型。
3. 旧 `GameplayFactEventBridgeSystem` 与 `GEInstantEffectCueRequestProjectionSystem` 已删除，不再作为未注册残留系统保留；当前 Attribute/Cue/Tag 派生职责由 `GameplayFactBoundaryProjectionSystem` 承担。
4. `AbilityCommitSystem` 是 CommandResolve 组内的真实 fence。跨组 `[UpdateBefore(typeof(GEEffectCommandIngestSystem))]` 已从 `ASCCommandBufferResolveSystem`、`AbilityCommitSystem`、generated `AbilityCatalogCommitSystem` 及 codegen 模板移除；CommandResolve -> CoreSimulation 的先后由 physical group 顺序保证，避免 Entities 忽略无效跨组排序属性。

### 2.1 证据等级矩阵

| 项 | 等级 | 说明 |
|---|---|---|
| `FixedStepGroupTypes` / `RegisterSystems()` | runtime-active | 当前 World 初始化真实创建和注册 |
| `RuntimeCoreFramePhaseContracts` | contract-only | 描述 8 个逻辑 phase 的读写权限；不是 8 个物理 group |
| `RuntimeCoreFramePhaseSystemContracts` | partial mapping | 当前只覆盖 5 个系统的 phase mapping，不能视为全系统 phase table |
| generated registration reflection | runtime-active but fragile | `GASSystemScheduleContract` 通过手写 generated type-name 列表反射注册；若 generated asmdef/type 缺失会静默跳过，且 `RuntimeSystemRegistration.gen.cs` 当前已退场 |
| AutoChess system bootstrap | demo-extension | 只为 demo world 插入 command drive / execution extension，不属于通用 Runtime 注册表 |

### 3. Command / Spec / Delta / Fact 迁移链

当前代码已经有一条 generated + runtime 混合的迁移期主链：

1. `ASCCommandPort` / AutoChess command drive 写入 ASC owner-local `ASCCommandBuffer`、`AbilityCommandBuffer`、`ASCDestroyCommandBuffer`；`ASCCommandBufferResolveSystem` 在 CommandResolve phase 消费。2026-06-07 本轮已把 `ASCCommandPort` 内散落的 ASC/Ability/GE remove 写入集中到 `ASCBoundaryCommandWriter`，CommandPort 只保留 shell 写入口；外部 GE apply 成功时返回 owner ASC，不再把 `GEEffectCommandStreamComponent` singleton entity 暴露给 shell 调用方。`ASCReadModel` 构造阶段捕获 level、tag mask、attribute、presentation event snapshot，getter 不再读 live ECS buffer，也不再在 read model 内部抓全局 facade。
2. `GameplayEffectRequestWriter` 和 generated `AbilityCatalogCommitSystem` 可写入 `GEEffectCommandBuffer`；`AbilityCatalogCommitSystem` 的 stored query 必须使用 `EntityQueryOptions.IgnoreComponentEnabledState`，再由 `AbilityCatalogCommitJob` 显式检查 `AbilityCommitRequestComponent` chunk `EnabledMask`。否则同 archetype 上默认 disabled 的 `AbilityEndRequestComponent` 会把已启用 commit request 的 ability 从查询中过滤掉，导致 AutoChess 只发出 `AbilityCommandBuffer`，但 Core requests/specs/deltas/facts 全为 0。
3. `AbilityCatalogCommitJob` 当前用 `ComponentTypeHandle<AbilityCommitRequestComponent>` + chunk `EnabledMask` 关闭 commit request，并用 `AbilityEndRequestComponent` chunk `EnabledMask` 写 auto-end 请求，不再做当前 ability 同实体 random-access enableable 开关。
4. generated `GEEffectSpecBuildSystem` 从 `GEEffectCommandBuffer` 构建 `GEEffectSpecBuffer`；当前 `InstantSpecBuildJob` 是 scheduled `[BurstCompile] IJob`，且 cue-only instant GE 可通过 `GameplayCueCode > 0` 生成 spec。
5. generated `GASAttributeSetReduceApplySystem` 读取 `GEEffectSpecBuffer`，修改目标 ASC 的 `AttributeValueBuffer`，写入 `AttributeModifierBuffer`；当前 `AttributeSetReduceApplyJob` 是 scheduled `[BurstCompile] IJob`。
6. `GameplayFactProjectionSystem` 从 `AttributeModifierBuffer` 投影 `GameplayEventBuffer` typed facts；它不再持有 EventBus lookup，也不再写 Attribute/Cue 边界缓冲。
7. `GameplayFactBoundaryProjectionSystem` 位于 `GASBoundaryProjectionSystemGroup`，从 `GameplayEventBuffer` 派生 Attribute/Cue/Tag 边界缓冲，并用 `GEEffectCommandStreamComponent.EventBridgeFactCursor` 防止重复投影。
8. `PresentationOutboxProjectionSystem` 与 `ReplayLogSystem` 只消费 `GameplayEventBuffer` typed facts 输出表现和 replay，不再双读 Attribute/Cue/Tag/Damage 边界缓冲。

这说明 AM3 不是纯 Contract，但当前仍属于 proof/migration 阶段：

- command/spec/delta/fact 都挂在 singleton `GEEffectCommandStreamComponent` owner 的 DynamicBuffer 上。2026-06-07 本轮已删除 `GEEffectCommandSpecStream` 的隐式 singleton writer/append helper；Runtime helper 调用方现在需要先解析 stream owner，再以 `Begin*Writer(em, streamEntity, currentFrame)` 显式写入。这改善了 API 承载边界，但没有改变底层 singleton DynamicBuffer carrier 的 proof/migration 性质。
- `GEEffectCommandSpecStreamFramePrepareSystem` 已从主线程 `EntityManager.GetBuffer` compact/clear 改为 scheduled `IJob`；`GameplayFactProjectionSystem` 已从主线程 projection/legacy bridge 改为 scheduled `IJob`，并且 Attribute/Cue/Tag 边界投影已移出 CoreSimulation。
- `GEExecutionCalculationSystem`、`GEExecutionCalculationOutputModifierSystem`、`GASActiveEffectPreTickSystem`、`AbilityLifecycleRequestSystem`、`AbilityStateCleanupSystem` 已从 `Complete()` / 主线程 cleanup 消费改为 scheduled job chain；其中 output modifier 使用 `NativeStream` fan-in 后在 scheduled merge job 内排序、应用属性、写 delta，并把 ASC dirty 追加为 frame-local `AttributeOwnerMarkerRequestBuffer`。
- `ASCCommandBufferResolveSystem`、generated `AbilityCatalogCommitSystem`、generated `GEEffectCommandCatalogNormalizeSystem`、generated `GEEffectSpecBuildSystem`、generated `GASAttributeSetReduceApplySystem`、generated `GASActiveEffectMutationApplySystem`、generated `GASActiveEffectPreTickSystem`、generated `GASActiveEffectRemoveSystem` 均已迁到 scheduled `IJob` / `IJobChunk` 路径，并且 codegen 模板已同步；ASC owner-local pending/destroying/dirty、ability commit/auto-end、explicit remove pending 和 ability cleanup current-entity marker 已使用 chunk `EnabledMask`。generated active mutation 当前仍是 singleton DynamicBuffer serial gather `IJob`，但已在 job 内按 owner range applicator 批处理，同 owner range 的 store/slot/snapshot 不再逐 command 重复获取；active mutation 跨 owner SourceAttribute 已在 gather 阶段构建只读 snapshot，apply job 不再持有 `AttributeValueBuffer` lookup alias。该 lane 仍不能写成 scale-ready 终局，因为 command carrier、serial gather、capacity/spill 和其他 generated lifecycle lookup 仍未闭合。
- generated instant spec/reduce 路径的 ASC 可用性判断已按 `ASCDestroyingComponent` enabled bit 读取销毁态；默认 disabled 的正常 ASC 不再被 `HasComponent` 误判为 destroying。
- cross-entity ability lifecycle 请求已收口到 frame-local `AbilityLifecycleRequestBuffer`：ASC command、attribute threshold、generated active effect granted-ability cleanup 只追加 request record，`AbilityLifecycleRequestSystem` 再以 ability-owned `IJobChunk` + chunk `EnabledMask` 应用 cancel/end/destroy-on-cleanup marker。
- cross-entity Attribute owner marker 请求已收口到 frame-local `AttributeOwnerMarkerRequestBuffer`：generated active effect 与 execution output modifier 只追加 request record，`AttributeOwnerMarkerRequestSystem` 再以 ASC-owned `IJobChunk` + chunk `EnabledMask` 应用 `AttributeDirtyComponent` / `AttributeActiveModifierPresentComponent`。
- `GEExecutionCalculationOutputModifierSystem` 的 output applied marker 已改为 effect-owned `IJobChunk` + chunk `EnabledMask` 应用，不再通过 random `AppliedLookup.SetComponentEnabled` 写 arbitrary effect entity。
- generated active effect 当前剩余风险不再是 ASC dirty/present marker、execution output applied marker 或 legacy gameplay EventBus buffer 写入。`GASActiveEffectMutationApplySystem` 已通过 codegen 模板生成 frame-local `NativeList<GEEffectCommandBuffer>`，按 `TargetAsc -> Sequence -> ParentContextId` 排序后进入 ASC chunk-local `IJobChunk` applicator，同 owner range 在当前 ASC chunk 内读取 active store、slot buffer 和 set-by-caller snapshot，并在 `GEEffectCommandStreamComponent` 写入 `ActiveMutationCommandCount`、`ActiveMutationOwnerGroupCount`、`ActiveMutationMaxOwnerRange`、`ActiveMutationSortMoveCount`。2026-06-07 追加后，跨 owner `SourceAttribute` 先由 gather 阶段 `[ReadOnly] BufferLookup<AttributeValueBuffer>` 构建 `ActiveMutationSourceAttributeSnapshots`，chunk apply 只按 `command.Sequence + modifierIndex` 读取 snapshot。剩余问题是 active mutation command source 仍是 singleton DynamicBuffer carrier，pre-tick / execution calculation 的 source magnitude snapshot lane 仍未闭合。
- 规模化 fan-in 只在部分链路落地；singleton stream 仍是 command/spec/delta/fact/active mutation 的 proof carrier。
- gameplay event 的 legacy EventBus bridge 已删除；Damage 边界缓冲和 EventBus gameplay enqueue helper 已删除；Attribute/Cue/Tag 边界投影已移动到 BoundaryProjection；但 Attribute/Cue/Tag 边界缓冲仍存在，不能把全部 observation 数据承载当成已终局。

### 4. ActiveEffectStore 当前状态

1. `ActiveEffectStore` 已有 `ASCActiveEffectsComponent`、`ActiveGameplayEffectBuffer`、global index owner/bucket/row 等数据结构。
2. generated `GASActiveEffectMutationApplySystem`、`GASActiveEffectPreTickSystem`、`GASActiveEffectRemoveSystem` 已挂入 CoreSimulation。
3. `GASActiveEffectPreTickSystem` 当前已不再 `SystemAPI.Query` 预扫，也不再 `NativeStream scan -> state.Dependency.Complete() -> 主线程 ApplyActiveEffectTickRecord`；旧 scan/apply helper 已从 generated 输出和模板中删除。它调度 `GEActiveEffectPreTickJob`，在 job 内处理 period command、duration expire、modifier/tag/ability cleanup、mutation/event 输出。
4. `GASActiveEffectRemoveSystem` 当前也复用 scheduled `GEActiveEffectPreTickJob` 的 explicit remove 分支消费 ASC owner-local `GERemoveCommandBuffer`，不再使用 `SystemAPI.Query` 主线程 foreach / `EventBusHelper` / `EntityManager` remove helper。
5. 当前 ActiveEffectStore 仍是 owner-local store + generated/runtime 混合迁移期实现，不是完全 store-driven lifecycle 终局。
6. `GASActiveEffectMutationApplySystem` 当前由 generated `GEActiveEffectMutationGatherJob : IJob` 收集 active mutation command，并由 `GEActiveEffectMutationChunkApplyJob : IJobChunk` 在 ASC owner chunk 内应用。旧 public/static `TryApplyActiveMutation(EntityManager, ...)`、旧 `GEActiveEffectMutationApplyJob : IJob` serial apply、逐 command owner resource lookup 口径均已退场。当前 active mutation command 会先复制到 frame-local `NativeList`，再按 `TargetAsc -> Sequence -> ParentContextId` 排序，构建 owner range，chunk apply 直接使用当前 ASC 的 active store、slot、mutation、tag、attribute、modifier 与 ability buffer；mutation 结果写入 ASC owner-local `ActiveEffectMutationBuffer` scratch，不再写回 stream owner carrier。2026-06-07 追加后，active mutation 跨 owner `SourceAttribute` 已通过 `ActiveMutationSourceAttributeSnapshots` 从 apply job 退场。剩余风险不再是 active mutation random lookup，而是 singleton command carrier、gather 阶段 serial command scan、snapshot capacity/spill evidence，以及 pre-tick / execution calculation 的 source magnitude snapshot lane。
7. 2026-06-07 AutoChess batchmode 验收已把 `GASActiveEffectPreTickSystem` 发出的 period child GE 变成结构化业务证据：稳定摘录见 `_归档/2026-06-07-GAS架构瘦身执行记录.md` 的追加验收章节，原始易失日志路径为 `Temp/AutoChessBattleValidation-PeriodGate.log`；关键字段为 `periodTickDamageFacts=150`、`periodTickDamageTotal=150`，并且 validation result 同时满足 `thresholdsPassed=True` / `runtimeChainPassed=True`。这只能证明 period tick -> child GE -> AttributeChange fact -> AutoChess validation gate 链路成立；不能抹除 `GASActiveEffectMutationApplySystem` 的 singleton stream serial job、lookup random access 和 migration carrier 风险。
8. 2026-06-07 x50 batchmode 验收稳定摘录见 `_归档/2026-06-07-GAS架构瘦身执行记录.md` 的 `追加验收：ActiveMutation chunk-local apply`，原始日志路径为 `_归档/2026-06-07-AutoChessBattleValidation-ActiveMutationChunkApply-Run3.log`；关键字段显示 `activeMutationCommands=200`、`activeMutationOwnerGroups=200`、`activeMutationMigrationCarriers=0`、`activeMutationEstimatedRandomLookups=0`、`activeMutationOwnerResourceLookups=0`、`periodTickDamageFacts=150`，并且 `passed=True` / `thresholdsPassed=True` / `runtimeChainPassed=True` / `repeatRunPassed=True`。这说明 active mutation 的旧 global mutation carrier 与旧 random lookup 估算已退出当前验收门。
9. 2026-06-07 x50 batchmode 复测稳定摘录见 `_归档/2026-06-07-GAS架构瘦身执行记录.md` 的 `追加验收：PendingAttribute chunk-local apply`，原始日志路径为 `_归档/2026-06-07-AutoChessBattleValidation-PendingAttributeChunkApply-Run3.log`；pending AttributeDelta owner-local apply 已进入 ASC chunk-local 路径：`pendingAttributeDeltas=350`、`pendingAttributeAppliedDeltas=250`、`pendingAttributeSkippedDeltas=100`、`pendingAttributeTargetGroups=250`、`pendingAttributeEstimatedRandomLookups=0`、`pendingAttributeMigrationCarriers=0`，并且 `passed=True` / `thresholdsPassed=True` / `runtimeChainPassed=True` / `repeatRunPassed=True`。
10. 2026-06-07 追加复测 `Temp/AutoChessBattleValidation-PendingAttributeNoStreamFallback.log` 显示旧 `ApplyStreamMigrationPendingAttributeModifierDeltaJob` 已删除后，runner 仍满足 `passed=True` / `thresholdsPassed=True` / `runtimeChainPassed=True` / `repeatRunPassed=True`；关键字段保持 `pendingAttributeDeltas=350`、`pendingAttributeAppliedDeltas=250`、`pendingAttributeSkippedDeltas=100`、`pendingAttributeTargetGroups=250`、`pendingAttributeEstimatedRandomLookups=0`、`pendingAttributeMigrationCarriers=0`。随后 active mutation 跨 owner `SourceAttribute` 已追加 snapshot lane，下一轮热点应转向 singleton stream carrier、generated instant delta record / fan-in 证明、`dependencyWaitRisks=4` / `syncQueryBudget=13` frame backbone 风险、active mutation snapshot capacity / spill 证据，以及 pre-tick / execution 的 source magnitude snapshot 语义闭环。

### 5. Definition / Generated 链

1. `GASDefinitionCatalogRuntimeTypes` 定义 `GASDefinitionCatalogBlob` 与 runtime catalog component。
2. generated runtime 通过 `GASDefinitionCatalogComponent` 读取 blob catalog，而不是在 hot path 直接读取 JSON/Excel row。
3. `LubanDefinitionRowProvider` 读取 Luban JSON 并规范化 Ability/GE/Attribute/Tag/Cue/Timeline rows；`GasCodeGenPipeline` 输出 generated catalog、runtime glue、runtime systems、manifest 和 validation report。
4. `AutoChessBattleDefinitionCatalogBuilder` 当前安装 `GASGeneratedDefinitionCatalogBuilder.BuildCatalog()`，说明 Luban/sourcegen catalog 已被 Runtime Core 消费端实际使用。
5. `GameplayEffectConfigRegistry`、`GameplayEffectComponentConfig`、GE static component config 类仍存在；本轮已删除 `GameplayEffectComponentConfig` / `AbilityComponentConfig` 基类里的隐式 `GASManager.EntityManager` facade，`LoadToGameplayEffectEntity` / `LoadToGameplayAbilityEntity` 改为显式接收 `EntityManager`。
6. prototype/static definition 路径仍可在初始化/authoring 阶段写 ECS，但写入 owner 现在由调用方传入的 world `EntityManager` 决定，不再由 config component 自己偷取全局 world。该路径仍不得进入 Runtime Core hot path。

### 5.1 Runtime Core Query 创建事实

1. 本轮复核后，`Assets/GAS/Runtime/System` 与 `Assets/GAS/Generated/CodeGen/Runtime` 内 `SystemAPI.QueryBuilder().Build()` 命中为 0。
2. 手写 Runtime Core stored `EntityQuery` 已统一改为 `state.GetEntityQuery(new EntityQueryDesc { ... })` 创建，覆盖 `ASCCommandBufferResolveSystem`、`ASCDestroyFinalizeSystem`、Ability lifecycle/cleanup/tick/try-activate/threshold 系统、`AttributeRecalculateSystem`、`AttributeChangeEventProjectionSystem`、`GEExecutionCalculationSystem`、`GEExecutionCalculationOutputModifierSystem`。
3. 这条事实用于对齐本地官方规则 `PRF-33` / `CASE-46`：stored query 归属 `SystemState`，不再由 `SystemAPI.QueryBuilder().Build()` 承载长期生命周期。
4. 同轮扫描中，`IJobChunk.Execute` 内直接 `for (... chunk.Count ...)` 遍历已经清零；涉及 enableable mask 的 job 使用 `ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count)` 或已经不属于 `IJobChunk` 语义。
5. 这不等于所有 query 成本已经终局优化：`CalculateChunkCountWithoutFiltering()`、singleton stream sizing、Debugger observation query、Cue managed lifecycle query 仍需按 owner / phase / cost 分类报告。
6. 2026-06-07 codedb + 文本复核确认 `EntityManager.CreateEntityQuery(...)` 当前运行命中 0。`GASRuntimeFrameContext` current-frame lookup 与 `ActiveEffectStore` global index owner 均已改为 registered/cache owner，不再创建 fallback query；剩余工作是容量、cache integrity 和 hot path 触发证据。

### 6. Observation / Debugger

1. `GameplayEventBusComponent` 仍作为 Boundary singleton 存在，承载 tag/attribute/cue 派生缓冲、presentation owner buffer 和 projection state；不再承载 Damage 边界缓冲或 gameplay fact enqueue API。
2. `GameplayFactBoundaryProjectionSystem` 会把 Attribute/Cue/Tag typed facts 投影到边界缓冲；当前投影已由 BoundaryProjection 内的 scheduled `IJob` 完成，不再由 CoreSimulation 的 `GameplayFactProjectionSystem` 或 CommandResolve 的 `ASCCommandBufferResolveSystem` 写 EventBus lookup。
3. `PresentationOutboxProjectionSystem`、`ReplayLogSystem`、`DiagnosticsSnapshotSystem` 位于 `GASBoundaryProjectionSystemGroup`；Presentation/Replay 只读 typed fact。
4. `GasRuntimeOfficialToolDiff` 使用 `EntitiesJournaling` 统计 create/destroy/add/remove/enable/disable/set/get 等记录，是结构变化收口的有效证据工具。
5. `GasRuntimeDebugger` 仍有 observation-only `ToEntityArray` 和同步 query；这类成本必须与 Core Simulation 成本拆分报告。

## 当前 DOTS 合规缺口

### 已缓解：Runtime Boundary request entity 链路已退场

`ASCCommandPort` 当前不再创建 transient request entity。`RequestDestroy()`、`RequestInitialize()`、ASC command、Ability command、GE remove/clear 均通过 `ASCBoundaryCommandWriter` 写入 ASC owner-local command buffer，并通过 `ASCCommandPendingComponent` / `GERemoveCommandPendingComponent` 触发消费。`RequestGameplayEffectTo` 仍暂时写入迁移期 `GEEffectCommandSpecStream`，但该 singleton stream owner 不再作为 CommandPort 返回值暴露给 shell。

已删除旧 `ASCEntityCreateSystem`、`ASCInitializeRequestSystem`、`ASCCommandRequestSystem`、`AbilityCommandRequestSystem`、`ASCDestroyRequestSystem` 和对应 `*RequestComponent`。当前仍需防止旧 request entity 命名/兼容桩回流。

### 已缓解：核心 `state.Dependency.Complete()` 当前清零

当前 `Assets/GAS/**/*.cs` 中 `state.Dependency.Complete()` 扫描结果为 0。第二轮深修已把以下链路改为 scheduled job chain：

| 链路 | 当前状态 |
|---|---|
| `AttributeThresholdAbilityLifecycleRequestSystem` | threshold lifecycle request 由 `IJobChunk` 写入 frame-local `AbilityLifecycleRequestBuffer` 与 lifecycle fact，不再跨 entity 直接启用 ability request marker |
| `ASCDestroyFinalizeSystem` | destroy finalize 由并行 scan jobs + scheduled apply job 完成，不再主线程阻塞合并 |
| `GEExecutionCalculationSystem` | execution calculation 由 `IJobChunk` 并行解析输入并写 effect-local output buffer |
| `GEExecutionCalculationOutputModifierSystem` | 删除小规模主线程 fallback；`NativeStream` fan-in 后 scheduled merge job 确定性排序、应用属性、追加 delta；effect applied marker 由 effect chunk applicator 写入，ASC dirty 进入 AttributeOwnerMarker request |
| `AttributeOwnerMarkerRequestSystem` | 统一消费 frame-local attribute owner marker request；用 ASC-owned scheduled `IJobChunk` 写 dirty / active-modifier-present chunk `EnabledMask` |
| `AbilityLifecycleRequestSystem` | 统一消费 frame-local lifecycle request buffer 和 ability 自然结束状态；用 scheduled `IJobChunk` 写 current ability chunk `AbilityCancelRequestComponent` / `AbilityEndRequestComponent` / `AbilityDestroyOnCleanupComponent` mask，不再通过 lookup toggle |
| `AbilityStateCleanupSystem` | ability cancel/end cleanup 由 scheduled `IJobChunk` 处理，current ability 的 activation/commit/cancel/end/destroy-on-cleanup marker 使用 chunk `EnabledMask`；owner/effect store、临时 tag、event bus 仍通过 lookup 访问，旧主线程 `SystemAPI.Query` + `EntityManager` helper 退场 |
| generated `GASActiveEffectPreTickSystem` / `GASActiveEffectRemoveSystem` / `GasGlueCodeGenPhases` | 删除 generated pre-scan、explicit remove `SystemAPI.Query` 与 `Complete()`；模板和生成结果同步为 scheduled `GEActiveEffectPreTickJob` |
| `GEEffectCommandSpecStreamFramePrepareSystem` / `GameplayFactProjectionSystem` / `GameplayFactBoundaryProjectionSystem` | stream compact/clear 与 typed fact projection 已改为 scheduled `IJob`；Attribute/Cue/Tag 边界投影已移到 BoundaryProjection；未注册的旧 fact bridge / cue projection system 已删除 |
| generated `AbilityCatalogCommitSystem` | commit request 关闭与 auto-end request 写入均改为 chunk `EnabledMask`，不再使用 `ComponentLookup.SetComponentEnabled` 做当前 ability 同实体随机访问 |
| generated `GEEffectSpecBuildSystem` / `GASAttributeSetReduceApplySystem` | codegen 输出 scheduled `[BurstCompile] IJob`，并修复 cue-only spec 与 `ASCDestroyingComponent` enabled bit 判定 |

本条不等于 Runtime Core 已达终局：singleton stream carrier、ActiveMutation 的 serial gather / helper BufferLookup / ComponentLookup store、剩余边界缓冲仍需继续按 `QRY-01`、`PRF-09`、`BUF-02`、`NAT-03` 审查。隐式 stream writer/append helper 已收窄为显式 owner 写入，但 command/spec/delta/fact 仍共享同一个 stream owner。

### P1：Hot path 主线程 Query 已继续收窄，但 singleton fallback 仍需 owner 化

旧文档中的 “22 个 ToEntityArray” 已不符合当前代码。当前更准确的问题是：

1. `ToEntityArray` 当前运行命中 4 处，不再是旧的 22 处 hot path 诊断；当前命中集中在 Debugger observation 和 Cue managed boundary。
2. `AutoChessBattleCommandDriveSystem`、`AutoChessExecuteDamageCalculationSystem`、`AbilityLifecycleRequestSystem`、`AbilityStateCleanupSystem` 已迁到 scheduled job；Core 主链的 `AttributeRecalculateSystem` 小规模主线程 fallback、ASC command resolve、generated ability commit、generated normalize/spec-build/reduce、generated ActiveEffect pre-scan、explicit remove 主线程 foreach、stream frame prepare 和 fact projection 主线程 buffer loop 均已退场。
3. `Assets/GAS/Runtime/System` 与 generated runtime stored query 当前已统一使用 `state.GetEntityQuery(EntityQueryDesc)`；`SystemAPI.QueryBuilder().Build()` 扫描为 0。
4. `Assets/GAS/Runtime` 与 generated runtime 内 `SystemAPI.Query<...>` / `SystemAPI.Query(...)` 当前 0 命中；Cue managed lifecycle 使用 stored query + `ToEntityArray`，归 Boundary managed presentation。
5. Runtime Core 的 pending AttributeDelta owner-local apply 已迁到 ASC chunk-local `IJobChunk`，不再通过 `_ownerDeltaQuery.ToEntityArray(Allocator.TempJob)` 物化 owner，也不再把 owner chunk 写入计为 random lookup；旧 stream migration fallback 已删除并由诊断脚本防回流。剩余 Runtime Core 风险集中在 singleton stream carrier、generated instant delta record / fan-in 证明、`ActiveEffectStore` global index capacity / cache integrity 证据。generated active mutation 逐 command store/slot/snapshot lookup 已收束为 owner range 入口，但 command source 仍来自 singleton carrier。
6. `GEEffectCommandSpecStream` 不再暴露 `BeginCommandWriter(EntityManager)`、`BeginGameplayEventWriter(EntityManager)` 或 `AppendCommand/AppendGameplayEvent(EntityManager, ...)` 这种隐式 singleton 写入口；`GameplayEffectRequestWriter`、`AbilityRuntimeActions`、execution/magnitude/cleanup helper 当前都要显式解析 stream owner 后写入。该事实只说明 helper API 边界变窄，不说明 stream carrier 已达终局。
7. 按 `QRY-01`、`PRF-05`、`CASE-01` 的规则口径，`SystemAPI.Query` 仍只能作为 managed boundary / proof / debug 工具，不能重新进入 Core hot path。

当前检出：

| 模式 | 命中 | 当前归类 |
|---|---|---|
| `SystemAPI.QueryBuilder().Build()` | `Assets/GAS/Runtime/System` 与 generated runtime 当前 0 命中 | 已按 `PRF-33` / `CASE-46` 迁到 `state.GetEntityQuery(EntityQueryDesc)` |
| `IJobChunk` 直接 `for (... chunk.Count ...)` | Runtime/System 与 generated runtime 当前 0 命中 | 已按 `PRF-22` 改为 enabled mask aware 遍历 |
| `SystemAPI.Query<...>` / `SystemAPI.Query(...)` | Runtime + generated runtime 当前 0 命中 | 不再是当前主诊断；managed cue 边界使用 stored query |
| `ToEntityArray()` | `GasRuntimeDebugger.cs:889/2260/2458` | Debugger / observation-only |
| `ToEntityArray()` | `CueManagedLifecycleSystem.cs:39` | Boundary managed presentation |
| pending AttributeDelta owner-local apply | `GASAttributeModifierDeltaApplySystem.cs:37-195` | ASC chunk-local `IJobChunk`；`pendingAttributeEstimatedRandomLookups=0` / `pendingAttributeMigrationCarriers=0` 已由 AutoChess x50 复测证明，旧 stream migration fallback 已删除并由诊断脚本防回流 |
| `GASRuntimeFrameContext` current-frame owner | `GASGlobalTimerSystem.cs:73-126` | registered/cache owner；cache miss 直接失败，不再创建 singleton fallback query |
| ActiveEffectStore global index owner | `ActiveEffectStore.cs:1368-1419` | registered/cache owner；不再创建 fallback query，仍需 capacity / cache integrity / hot path 触发证据 |
| `CalculateEntityCount()` | `GasRuntimeDebugger.cs:609` | observation-only |
| `CalculateChunkCountWithoutFiltering()` | `AbilityStateCleanupSystem`、`GEExecutionCalculationSystem`、`GEExecutionCalculationOutputModifierSystem`、`ASCDestroyFinalizeSystem`、AutoChess command drive | scheduled job sizing；按 PRF-09 避免 enableable/filter sync，并匹配 `IJobChunk` unfiltered chunk index |
| direct `CreateEntity()` command port | `ASCCommandPort.Create(EntityManager)` -> `ASCEntityFactory.Create(EntityManager)` | 低频 owner 创建入口；不得扩展为 transient request entity |
| boundary writer direct buffer access | `ASCBoundaryCommandWriter` | Layer 2 Runtime Boundary 写入集中点；不是 CoreSimulation hot path，后续应继续拆成显式 request writer / read model writer |

### P0/P1：direct `EntityManager` 命中必须按 owner 分类

当前 broad scan 能看到 44 处 `CreateEntity()` / `DestroyEntity()` 命中。它们不能被写成同一种缺陷，必须按 owner 和相位区分：

| 类别 | 代表命中 | 当前分类 |
|---|---|---|
| World/bootstrap singleton 初始化 | `GASManager.cs:57/133/149`, `GEEffectCommandSpecStream.cs:356` | init-only，可保留但不能混入 hot path |
| Boundary command port owner 创建 | `ASCCommandPort.Create(EntityManager)` -> `ASCEntityFactory.Create(EntityManager)` | 低频 owner 创建入口，不计作 request entity churn |
| System 内 ECB 创建/销毁 | `ASCCommandBufferResolveSystem.cs`, `AbilityStateCleanupSystem.cs`, `RuntimeActiveEffect.gen.cs` | 结构变化方向正确，仍需 Journaling 相位证据 |
| Demo adapter 直接创建/写 ECS / report key projection | `AutoChessBattleRuntime.cs`, `AutoChessGasCoreBridge.cs`, `AutoChessGasBattleEntityLifecycle.cs`, `AutoChessGasRuntimeHost.cs`, `AutoChessGasCatalogSession.cs`, `AutoChessGasRuntimeTicker.cs`, `AutoChessGasBattleReportFactProjector.cs`, `AutoChessGasBattleUnitSnapshotProjector.cs`, `AutoChessGasCoreContracts.cs`, `AutoChessBattleDefinitionCatalogBuilder.cs` | Demo 集中接缝，unit/driver lifecycle、job drain、driver adapter raw entity、registry command compatibility 需迁入 request/commit/snapshot/diagnostics owner；report projector 与 unit result snapshot 已走 stable report key / structured log coverage，generated catalog 安装属初始化路径 |
| Managed cue/presentation | `GameplayCueUnit.cs:113`, `ConfCueBase.cs:21`, `CueRequestBridgeSystem.cs:44-103`, `CueManagedLifecycleSystem.cs:34-51` | Boundary managed path |
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

补充事实：本轮 Runtime helper 隐式写入口已收窄，`GEEffectCommandSpecStream` 只保留显式 `BeginCommandWriter(em, streamEntity, currentFrame)` / `BeginGameplayEventWriter(em, streamEntity, currentFrame)` writer 构造。`TryGetSingleton` 仍是 owner 解析入口，不能把这次 API 收缩误写成 singleton stream owner 已退出。

### P1：Generated runtime 需要同等 DOTS 审查

generated code 当前是实际执行链一部分，不能被“生成代码”身份豁免：

1. 使用 `SystemAPI.Query` 时要说明为什么不是 job；当前 Core hot path 不允许重新引入主线程 foreach。
2. 使用 `EntityManager` 时要说明是否属于低频边界、是否触发结构变化。
3. 使用 singleton/blob 时要说明 dependency policy。
4. 写 attribute/active store 时要给出 query、buffer pressure、deterministic ordering 和 battle hash 证据。
5. codegen 模板必须与 generated output 同步；当前 ability activation、instant spec/reduce、active effect pre-tick/remove 与 ability lifecycle request aggregation 模板已同步，后续仍需 static validation 防止旧 request entity / Temp ECB playback / `Complete()`、未 Burst hot job、cross-entity ability marker toggle 或 `ASCDestroyingComponent` `HasComponent` 误判回流。

## 当前总诊断

当前 Runtime Core 的事实已经从“旧 lifecycle + 大量 ToEntityArray”推进到“5 段物理 phase + generated catalog + singleton stream proof”。这是重要进展，但不能据此判断架构已经优秀。

真正的下一步不是继续堆新业务机制，而是：

1. 把 generated runtime 纳入 DOTS 规则审查。
2. active mutation 与 pending AttributeDelta 的旧 random lookup / migration fallback 均已退出当前 x50 验收门；active mutation 跨 owner `SourceAttribute` snapshot lane 已进入 generated gather/apply 模板。后续不应继续围绕这些字段打补丁，而应把 singleton stream proof 拆成按数据性质选型的 scale-ready carrier，并补 deterministic merge / capacity / spill 证据。
3. 继续拆 pre-tick / execution calculation 的 `SourceAttribute` magnitude snapshot lane，避免跨 owner attribute capture 回流到 generated tick 或 execution resolver 内。
4. 用 `GasRuntimeOfficialToolDiff`、Profiler、Debugger counters、battle hash 和 x50/x1000 规模门证明结构变化、fan-in、observation 成本真正收口。
5. 保持 Boundary request entity 旧链路退场，不允许兼容桩或旧 system 回流。
6. `PresentationEntityBindingRegistry` / Cue managed boundary 已去掉内部 `GASManager.EntityManager` 读取，改为调用方显式传入 `EntityManager` 或 `SetRuntime(EntityManager, Entity)` 初始化；GameObject binding key 已改为 `World.SequenceNumber + Entity`，避免多 World 下裸 `Entity` 碰撞；`EffectCommandSpecStream.TryResolveGasManagerSingleton()` fallback 也已删除。剩余 global facade 命中应继续按 shell/bootstrap/debug/prototype/store guard 分类，不能回流到 Runtime Core helper。
