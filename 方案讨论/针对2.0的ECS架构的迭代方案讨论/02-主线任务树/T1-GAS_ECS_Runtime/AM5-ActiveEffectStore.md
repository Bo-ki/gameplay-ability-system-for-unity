# GAS ECS Runtime - Runtime Core 重构 - Active Effect Store 重建

## 节点定位

本节点承接 AM3（Instant Spec Evaluation），负责把 duration / stack / period / granted state 从旧 runtime GE entity lifecycle 迁移到 ASC owner-local store（`CActiveEffectStore` + `BActiveEffectSlot`）。输出给 T4（Debugger）作为 slot pressure diagnostics 的权威数据源，输出给 AM7（Observation Split）作为稳定 store state。

## 父节点

[RuntimeCore重构](README.md)

## 任务ID

`T1-RuntimeCore-AM5`

## 状态

进行中

## 兄弟关系

`sequential`。前置：AM3（typed fact 输出是 store-driven lifecycle 的上游）。后继：AM7（Observation Split）依赖稳定的 store state；T4 Debugger 消费 slot pressure 数据。

## 拆分历史

从 `RuntimeCore重构` 拆分（S3 多关注点 + S4 依赖链：store 状态管理与 AM3 instant evaluation 关注点独立且 sequential），2026-05-20。

## 领取轮次

第 36 轮（累计 35 轮已完成）

## 当前进展

owner-local store 第一刀已落地：`CActiveEffectStore` + `BActiveEffectSlot` 在 ASC 创建时默认添加，slot state / flags 和 `ActiveEffectStore` helper 就位。旧 duration lifecycle 的 Activate / Inhibit / Reactivate / PendingRemove / RefreshDuration / SetStackCount / Remove 已镜像到 owner slot。Query layout 新增 `ActiveEffectStore` entry，标记 `TargetContract`、`NoPerHitStructuralChange`。Debugger 已输出 owner-local store owners、slot count、capacity、state distribution baseline。

Period / overflow derived command proof 已落地：`EffectRuntimeUtility.CreateDerivedApplyRequest` 对 period / overflow 派生 GE 先尝试写 simple instant `BEffectCommand`（`EEffectCommandSource` 新增 `Overflow`），`GameplayEffectRequestWriter` 支持从派生 runtime GE 复制 SetByCaller range。`ActiveEffectStore.TryRefreshPeriodFrame` 在 period cursor 更新后刷新 owner-local slot。`StackingRuntimeTests` 覆盖 period simple instant child GE 写 command、复制 SetByCaller、同步 slot cursor。

Granted ability runtime buffer 收缩已落地：`GameplayEffectEntityFactory` 在 runtime GE 初始化阶段按 `BGrantedAbilityConfig` 预置空 `BGrantedAbilityRuntime` buffer；`EffectRuntimeUtility.AddGrantedAbilities` / `TryAddRuntimeGrantedAbility` 不再为了读取新 buffer 在 helper 内调用 `PlaybackAndReset`，缺 runtime buffer 时只写入当前 ECB `AddBuffer` 返回的 pending buffer。`GrantedAbilitiesReadDefinitionFromStaticDefinitionBlob` 覆盖激活前空 runtime buffer 与移除 config 后仍从 static definition blob 授予 ability；`RuntimeStructuralChangePlanTests` 禁止 `EffectRuntimeUtility` 再出现内部 `PlaybackAndReset(ref ecb, em);` 调用。

Granted ability runtime state 镜像已推进：`BActiveEffectSlot` 新增 `ActiveGrantedAbilityCount`，`ActiveEffectStore.TryUpsertDurationEffect` / `TryRefreshGrantedAbilityState` 会从 `BGrantedAbilityRuntime` 统计仍存活的 granted ability。`SAbilityStateCleanup` 在 ability 自结束/取消触发 `ClearRuntimeGrantedAbility` 后刷新 owner-local slot；`ActiveEffectStoreTests.GrantedAbilitySelfCleanupRefreshesOwnerActiveEffectSlotCount` 覆盖 ability 自清理后 runtime record 清空、目标 `BGrantedAbility` 移除、slot count 归零。

Granted tag active state 与 Debugger 聚合已补齐：`BActiveEffectSlot` 新增 `ActiveGrantedTagCount`，`ActiveEffectStore.TryUpsertDurationEffect` / `TryRefreshGrantedTagState` 从 target `BTempTagSource` 按 source effect 去重统计 active granted tag。`EffectRuntimeUtility.AddGrantedTags` / `RemoveGrantedTags` 在更新 `CTagMask` 后刷新 owner-local slot；`GasRuntimeDebugger` 聚合输出 `grantedTags` / `grantedAbilities` 与 diagnostic event 字段。`ActiveEffectStoreTests.GrantedTagLifecycleMirrorsIntoOwnerActiveEffectSlot` 覆盖 activate / deactivate / reactivate 的 tag count、`CTagMask` 和 `BTempTagSource`，`GasRuntimeDebuggerTests` 覆盖 Debugger 聚合文本。

Remove request pending-remove handoff 已接入 store 镜像：`SRemoveGameplayEffectRequest` 不再直接 `ecb.AddComponent<CEffectDestroy>`，改为调用 `EffectRuntimeUtility.MarkEffectForRemoval`，在 command 阶段统一写入 `CEffectDestroy` 并同步 owner-local slot 的 `PendingRemove` / `PreviousState` / `StateStartFrame`。`ActiveEffectStoreTests.RemoveGameplayEffectRequestMirrorsPendingRemoveIntoOwnerActiveEffectSlot` 覆盖 request 消费、slot pending remove 镜像、cleanup 后 slot 移除；`RuntimeStructuralChangePlanTests` 固化 remove request 入口必须走 helper，不允许直接添加 `CEffectDestroy`。

Ability cleanup created-effect pending-remove handoff 已接入 store 镜像：`SAbilityStateCleanup.CleanupAbilityCreatedEffects` 不再直接添加 `CEffectDestroy`，改为携带当前 `GlobalTimer.Frame` 调用 `EffectRuntimeUtility.MarkEffectForRemoval`。Ability 自结束/取消触发的 ability-created duration GE 清理会在 AbilityGroup cleanup 阶段同步 owner-local slot 的 `PendingRemove` / `PreviousState` / `StateStartFrame`。`ActiveEffectStoreTests.AbilityCleanupMirrorsCreatedEffectPendingRemoveIntoOwnerActiveEffectSlot` 覆盖 ability-created active GE 进入 pending remove、slot 镜像和 cleanup 后移除；`RuntimeStructuralChangePlanTests` 固化 ability cleanup 入口必须走 helper，不允许直接添加 `CEffectDestroy`。

Actual cleanup compact frame 已接入 store 镜像：`ActiveEffectStore.TryRemove` 在实际移除 owner-local slot 时写入 `CActiveEffectStore.LastCompactedFrame`，`SEffectRemove` 解析当前 `GlobalTimer.Frame` 并显式传入 `EffectRuntimeUtility.CleanupActiveEffect`。`RemoveEffectFromTarget` 先执行 owner-local store compact，再按存在性清理 legacy `BGameplayEffect` buffer，避免无 legacy buffer 的 owner-local target 被跳过。`ActiveEffectStoreTests.CleanupActiveEffectCompactsOwnerStoreWithoutLegacyGameplayEffectBuffer` 覆盖只有 `CActiveEffectStore` + `BActiveEffectSlot`、没有 `BGameplayEffect` 的 target 仍可 compact slot 并记录 frame；`RuntimeStructuralChangePlanTests` 固化 `SEffectRemove` 必须显式传入 `currentFrame`。

Granted ability cleanup runtime-policy 已接入：`EffectRuntimeUtility.RemoveGrantedAbilities` 清理阶段不再读取 GE static definition blob，也不再把 `BGrantedAbilityRuntime` 复制到临时 `NativeArray`；改为直接遍历 runtime record，并使用 granted ability 自身的 `CGrantedByEffect` 作为 deactivation / remove policy 权威来源。没有 static definition blob 的 active GE cleanup 仍能清理 `SyncWithEffect` granted ability、移除目标 `BGrantedAbility` 并刷新 owner-local slot。`ActiveEffectStoreTests.CleanupActiveEffectRemovesRuntimeGrantedAbilityWithoutStaticDefinitionBlob` 覆盖无 static blob 的 granted ability cleanup；`RuntimeStructuralChangePlanTests` 固化 `RemoveGrantedAbilities` 方法体不得回流到 `TryGetStaticDefinitionBlob` 或 `NativeArray<BGrantedAbilityRuntime>`。

Active granted ability cancel 全链路证据已固化：active GE cleanup 遇到 active `SyncWithEffect` granted ability 时，会先清目标 `BGrantedAbility` 和 owner-local slot，再通过 `AbilityRuntimeActions.RequestAbilityCancel` + `CAbilityDestroyOnCleanup` 交给 AbilityGroup cleanup 完成 ability entity 销毁。`ActiveEffectStoreTests.CleanupActiveEffectCancelsAndDestroysActiveRuntimeGrantedAbility` 覆盖 GE cleanup 发起 cancel、destroy marker 落地、AbilityGroup cleanup 后 ability 销毁、target granted buffer 清空和 owner-local slot compact 保持一致。

LifecycleCleanupStore owner-local cleanup record 第一刀已接入：`CActiveEffectStore` 新增 `LastCleanupFrame` / `CleanupRecordCount`，ASC factory 默认创建 `BActiveEffectCleanupRecord`。`EffectRuntimeUtility.CleanupActiveEffect` 在实际 destroy 前调用 `ActiveEffectStore.TryRecordLifecycleCleanup`，把 effect / source / target / context、slot state、duration / period cursor、granted tag / ability count 与 cleanup frame / state 写入 owner-local cleanup record（上限 16，缺 store 不补结构）。`ActiveEffectStoreTests.CleanupActiveEffectRecordsLifecycleCleanupStoreBeforeDestroy` 覆盖 record 先于 entity destroy 写入、slot compact 与 `LastCleanupFrame`；`RuntimeStructuralChangePlanTests` 固化 cleanup 流程必须写 lifecycle cleanup snapshot。本轮不改变 runtime GE entity 实际销毁语义。

Cleanup work requested/resolved flags 已接入：`BActiveEffectCleanupRecord` 新增 `ActiveModifierCount`、`RequestedCleanupWorkFlags`、`ResolvedCleanupWorkFlags`、`CleanupResolvedFrame`。`CleanupActiveEffect` 改为 begin/resolve 两段式：cleanup 开始时先记录 owner-local slot、legacy target buffer、runtime modifier、granted tag、granted ability、duration runtime、entity destroy 等清理工作，再在同步清理完成并把 destroy 交给 ECB 前回填 resolved flags。`ActiveEffectStoreTests.CleanupActiveEffectRecordsRequestedAndResolvedCleanupWork` 覆盖 active cleanup 前的 granted tag / ability / modifier 计数、requested work flags、resolved frame 与 cleanup 后目标 buffer 清空；`RuntimeStructuralChangePlanTests` 固化 cleanup 流程必须同时写 `TryRecordLifecycleCleanup` 与 `TryResolveLifecycleCleanup`。本轮仍不切真正 cleanup component shell。

ChunkSkipIndex owner-local evidence 已接入：`CActiveEffectStore` 新增 `LastChunkSkipIndexFrame`、matched/skipped/due/no-op slot 计数与 reason flags；`ActiveEffectStore.CreateChunkSkipIndexSnapshot` / `TryRefreshChunkSkipIndex` 可在 owner-local slot 上计算当前帧可跳过事实，区分 active period due、duration due、inactive / pending-remove no-op。`GasRuntimeDebugger` 聚合输出 `chunkSkipMatched` / `chunkSkipSkipped` / `chunkSkipDuePeriod` / `chunkSkipNoop` / `chunkSkipOwners`，diagnostic event 文本同步输出；`ActiveEffectStoreTests.ChunkSkipIndexRefreshesOwnerLocalNoopAndDuePeriodFacts` 与 `GasRuntimeDebuggerTests` 覆盖 due slot 不跳过、inactive/pending slot 可跳过和 Debugger 可观测性。本轮仍不引入真正 chunk component / IJobChunk skip system。

SEffectTick store-first tick gate 已接入：`SEffectTick` 会先遍历 `CActiveEffectStore + BActiveEffectSlot` owner，刷新当前帧 ChunkSkipIndex，并只对 `ActiveEffectStore.IsTickDue(slot, currentFrame)` 命中的 owner-local slot 调用 duration tick；随后 legacy `_activeDurationQuery` 会跳过已经存在 owner-local slot 的 store-backed GE，避免未到期 slot 被旧 runtime duration 字段误判过期。`BActiveEffectSlot.StartFrame` 明确作为 duration timer anchor，duration refresh 会同步刷新该 anchor；`EActiveEffectSlotFlags.TicksWhenInactive` 覆盖 `StopTickWhenDeactivated == false` 的 inhibited duration 继续 tick 场景。`ActiveEffectStoreTests.ChunkSkipIndexKeepsInactiveTickingDurationAsDueWork` 与 `StackingRuntimeTests.StoreBackedEffectTickUsesOwnerLocalSlotBeforeLegacyRuntimeDuration` 覆盖 inactive ticking duration 不被 noop skip、store-backed GE 优先使用 owner-local slot gate。

SEffectTick slot cursor execution 已接入：store-backed duration GE 的 tick 执行阶段不再只用 owner-local slot 做 gate，而是优先读取 `BActiveEffectSlot.LastPeriodFrame` / `PeriodFrame` 判断 period due，读取 `StartFrame` / `RemainingFrame` 判断 duration expired；legacy `CDurationRuntime` / `CPeriodRuntime` 仍保留为 fallback 和旧组件同步出口。`StackingRuntimeTests.StoreBackedPeriodTickUsesOwnerLocalSlotCursorBeforeLegacyRuntimeCursor` 覆盖 owner-local slot cursor 已到期、legacy `CPeriodRuntime.StartTime` 未到期时仍能触发 period simple instant command，并刷新 slot / legacy cursor 到当前帧。

SEffectRemove owner-local pending-remove cleanup gate 已接入：`SEffectRemove` 新增 `CActiveEffectStore + BActiveEffectSlot` owner 查询，先收集 owner-local `PendingRemove` slot，再调用 `EffectRuntimeUtility.CleanupActiveEffect(em, ref ecb, ge, currentFrame)`；legacy `CEffectDestroy` query 仍保留为 fallback，并会跳过本轮已由 owner-local gate 处理过的 effect。无 pending slot 且无 legacy marker 时系统直接释放临时容器返回，不再播放空 ECB。`ActiveEffectStoreTests.SEffectRemoveCleansPendingRemoveOwnerLocalSlotWithoutEffectDestroyMarker` 覆盖没有 `CEffectDestroy` 的 pending-remove slot 仍能 cleanup、compact slot、写 cleanup record 与 resolved flags；`RuntimeStructuralChangePlanTests` 固化 `SEffectRemove` 必须声明 owner-local pending slot 路径。

CEffectCleanup cleanup component shell 已接入：`CEffectCleanup` 作为 cleanup 语义 shell 写入 `CEffectDestroy` 同文件，记录 `RequestedFrame` / `CleanupState` / `SourceAsc` / `TargetAsc`。`EffectRuntimeUtility.MarkEffectForRemoval` 现在先写 `CEffectCleanup` 再保留 legacy `CEffectDestroy` marker，`ResolveCleanupState` 优先读取 shell；`SEffectRemove` 新增 cleanup query，并按 `CEffectCleanup` shell -> owner-local pending slot -> legacy marker 顺序消费，shell-only GE 也能进入 `CleanupActiveEffect`。`SAscDestroyRequest` 同步写 shell 并镜像 owner-local slot 到 `PendingRemove`；`SEffectApply` / `SEffectTick` / `SOngoingTagRequirements` / `SExecutionCalculation*` / AutoBattle execute calculation 等热路径已排除 `CEffectCleanup`。`ActiveEffectStoreTests.MarkEffectForRemovalWritesCleanupShellBeforeLegacyDestroyMarker` 与 `ActiveEffectStoreTests.SEffectRemoveCleansCleanupShellWithoutEffectDestroyMarker` 覆盖 shell 写入和无 legacy marker cleanup；`RuntimeStructuralChangePlanTests` 固化 `SEffectRemove` / `EffectRuntimeUtility` 的 shell 合同。

CEffectFinalDestroy actual destroy finalizer 第一刀已接入：`CEffectFinalDestroy` 作为 cleanup 后的实际销毁 marker，记录 `RequestedFrame` / `CleanupSequence` / `TargetAsc`；`CleanupActiveEffect(em, ref ecb, ge, currentFrame)` 现在只完成语义 cleanup、owner-local slot compact、cleanup record before-entity-destroy resolved flags，并投递 `CEffectFinalDestroy`，不再直接 destroy runtime GE entity。新增 `SEffectFinalDestroy`，在 `GASEffectGroup` 中显式排在 `SEffectRemove` 之后、`SEffectTick` 之前，同帧集中调用 `EffectRuntimeUtility.FinalizeEffectDestroy` 完成完整 cleanup record resolved flags 与实际 destroy。便捷 `CleanupActiveEffect(em, ge, currentFrame)` 保留同步完成语义，但拆成 cleanup ECB 与 destroy ECB 两段，避免同一个 ECB playback 后复用。`SEffectApply` / `SEffectTick` / `SOngoingTagRequirements` / `SExecutionCalculation*` / remove request / ASC destroy / AutoBattle execute calculation 等热路径已排除 `CEffectFinalDestroy`。`ActiveEffectStoreTests.CleanupActiveEffectEcbPathDefersEntityDestroyUntilFinalDestroySystem`、`SystemScheduleContractTests.EffectFinalDestroyRunsAfterRemoveAndBeforeTick`、`RuntimeQueryLayoutPlanTests.GameplayEffectActiveRuntimeDeclaresCleanupAndFinalDestroyMarkers` 与 `RuntimeStructuralChangePlanTests` 覆盖 final marker、系统调度、layout / structural slots 和源码合同。

GlobalIndexedStore 第一刀已接入：新增 `CActiveEffectGlobalIndexStore` 与 `BGlobalActiveEffectIndex`，由 `ActiveEffectStore.EnsureGlobalIndexStore` / `GASManager.EntityActiveEffectGlobalIndex` 在低频 bootstrap 显式创建全局索引 owner；owner-local upsert / duration refresh / stack refresh / period cursor / granted tag/ability refresh / remove / chunk skip refresh 会同步或移除 global index，`DestroyEffectEntity` 在实际销毁前移除 stale index。Debugger 已输出 `globalIndexOwners` / `globalIndexCount` / active / inhibited / pendingRemove / periodDue / durationDue / stale 计数；QueryLayout / StructuralPlan 声明 `ActiveEffectGlobalIndexStore` / `ActiveEffectGlobalIndexBuffer` slot。`ActiveEffectStoreTests.EnsureGlobalIndexStoreCreatesExplicitOwnerAndHotPathDoesNotCreateMissingOwner`、`DurationLifecycleMirrorsIntoGlobalIndexedStore`、`GlobalIndexedStoreOwnerRefreshUpdatesPeriodAndDurationDueCounters` 与 `GasRuntimeDebuggerTests` 覆盖显式 owner、hot path 不创建、duration lifecycle 镜像、due counter 和 Debugger 导出。本轮采用 singleton DynamicBuffer index proof，不引入 per-effect stable entity 或 chunk bucket。

ChunkSkipIndex IJobChunk refresh 第一刀已接入：`SEffectTick` 不再对 owner-local `_activeEffectStoreQuery` 做 `ToEntityArray` 主线程 materialization，而是调度 `RefreshOwnerLocalChunkSkipIndexJob : IJobChunk`，通过 `ComponentTypeHandle<CActiveEffectStore>` 与 `BufferTypeHandle<BActiveEffectSlot>` 按 chunk 刷新 owner-local ChunkSkipIndex counters。`ActiveEffectStore.RefreshChunkSkipIndexCounters` 作为纯值计算入口返回 snapshot；`RuntimeStructuralChangePlanTests` 固化 `SEffectTick` 必须声明 `IJobChunk` / `BufferTypeHandle<BActiveEffectSlot>`，并禁止恢复 `_activeEffectStoreQuery.ToEntityArray`。本轮不把 period derived command / duration expire 的 EntityManager 逻辑塞入 job，也不引入 chunk component bucket。

SEffectTick duration candidate IJobChunk collector 第一刀已接入：legacy `_activeDurationQuery` 不再调用 `ToEntityArray(Allocator.Temp)`，改为调度 `CollectDurationEffectCandidatesJob : IJobChunk`，通过 `EntityTypeHandle` 收集 duration fallback candidate。`SEffectTick` 先完成 owner-local ChunkSkipIndex refresh，再调度 duration candidate collector，主线程只对 collector 输出的 candidate 执行现有 legacy fallback，并继续跳过已存在 owner-local slot 的 store-backed GE。`RuntimeStructuralChangePlanTests` 禁止恢复 `_activeDurationQuery.ToEntityArray`。

SEffectTick due-slot parallel writer / deterministic sort 第一刀已接入：owner-local refresh job 从输出 due owner 改为直接用 `NativeQueue<OwnerLocalDueSlotCandidate>.ParallelWriter` 输出 due slot candidate，并以 `ScheduleParallel(_activeEffectStoreQuery, ...)` 并行刷新 owner-local store。主线程 drain queue 后按 owner entity、slot sequence、slot index、effect entity 做稳定排序，再执行现有 store-backed period / duration 语义。`RuntimeStructuralChangePlanTests` 固化 `OwnerLocalDueSlotCandidate`、`NativeQueue<OwnerLocalDueSlotCandidate>.ParallelWriter`、`ScheduleParallel(_activeEffectStoreQuery` 与 `SortDueSlotCandidates`，并禁止恢复 `TickOwnerLocalDueSlots` / `ownersWithDueWork` 二次 owner buffer 全量扫描。本轮仍不把 Blob / ECB / EntityManager execution 放进 job。

SEffectTick legacy duration candidate parallel writer / deterministic sort 第一刀已接入：legacy `_activeDurationQuery` collector 从固定容量 `NativeList<Entity>` + 单线程 `.Schedule(_activeDurationQuery)` 推进为 `NativeQueue<DurationEffectCandidate>.ParallelWriter` + `ScheduleParallel(_activeDurationQuery, ...)`。主线程 drain 后执行 `SortDurationEffectCandidates`，按 effect entity Index / Version 稳定排序，再执行现有 legacy duration fallback；源码合同新增 `DurationEffectCandidate`、parallel writer、`ScheduleParallel(_activeDurationQuery` 与 `SortDurationEffectCandidates` token，并禁止回退到 `.Schedule(_activeDurationQuery` 或 `NativeList<Entity>`。本轮仍只迁移 candidate collection，不把 Blob / ECB / EntityManager period-duration execution 塞入 job。

SEffectTick store-only update gate 第一刀已接入：`SEffectTick.OnCreate` 不再用 legacy `_activeDurationQuery` 作为系统更新 gate，改为 `RequireForUpdate<GlobalTimer>`，并在 `OnUpdate` 开头只在 owner-local store query 与 legacy duration query 同时为空时早退。这样只有 `CActiveEffectStore + BActiveEffectSlot` 的 store-only owner-local slot，即使没有 `CDurationDefinition` / `CDurationRuntime` 命中 legacy duration query，也能驱动本帧 ChunkSkipIndex refresh。`StackingRuntimeTests.StoreOnlyOwnerLocalSlotDrivesTickWithoutLegacyDurationQuery` 覆盖无 legacy duration 组件的 owner-local slot 仍刷新 skip counters；`RuntimeStructuralChangePlanTests` 固化 `RequireForUpdate<GlobalTimer>` 与双 query 空早退，并禁止恢复 `RequireForUpdate(_activeDurationQuery)`。本轮只解除更新条件对 legacy runtime duration query 的绑定，`TickStoreBackedDurationEffect` 主体仍保留 `CDurationDefinition` / `CDurationRuntime` fallback 边界，完整 store-only tick execution 继续后续迁移。

SEffectTick store-only duration expire 第一刀已接入：`ActiveEffectStore.TryMarkPendingRemove` 可在不创建缺失 store / slot 的前提下，把已有 owner-local slot 镜像为 `PendingRemove` 并同步 ChunkSkipIndex / GlobalIndexedStore；`EffectRuntimeUtility.MarkEffectForRemoval` 在 legacy `TryUpsertDurationEffect` 失败或缺少 `CDurationRuntime` 时，会 fallback 到 owner-local pending-remove 标记。`SEffectTick.TickStoreBackedDurationEffect` 不再要求 store-backed GE 必须带 `CDurationDefinition` / `CDurationRuntime`；owner-local due slot 到期时，如果仍有 legacy `CDurationRuntime` 就保留 `HandleDurationExpired` stacking expiration policy，如果没有 legacy duration runtime 就直接 `MarkEffectForRemoval`，交由 `SEffectRemove` / `SEffectFinalDestroy` 完成 cleanup / finalize。`StackingRuntimeTests.StoreOnlyOwnerLocalSlotExpiresWithoutLegacyDurationRuntime` 覆盖无 legacy duration runtime 的 store-only slot 进入 `CEffectCleanup` / `CEffectDestroy` / lifecycle pending-remove，并在下一轮 EffectGroup cleanup 后 compact owner-local slot；`RuntimeStructuralChangePlanTests` 固化 `TryMarkPendingRemove` 与 `MarkEffectForRemoval(em, ref ecb, ge, currentFrame)` token。本轮仍不是完整 store-only active effect lifecycle：stacking expiration policy / duration refresh 仍依赖 legacy `CDurationRuntime`，period / duration execution 主体仍是主线程 EntityManager / ECB 语义。

SEffectTick owner-local action candidate classification 第一刀已接入：`EActiveEffectTickActionFlags` 与 `ActiveEffectStore.CreateTickActionFlags` 成为 owner-local slot 的 period / duration-expire 动作分类入口，`RefreshOwnerLocalChunkSkipIndexJob` 在 IJobChunk 阶段为 `OwnerLocalDueSlotCandidate.ActionFlags` 写入 `Period` / `DurationExpire`，主线程只按 `HasTickAction` 消费已分类 candidate。`SEffectTick` 已删除本地 `IsStoreDurationDue` 复制判断，避免 owner-local tick action 在主线程重新临时推导；`ActiveEffectStoreTests.TickActionFlagsClassifyOwnerLocalPeriodAndDurationWork` 覆盖 no-op、period-only、duration-only、both、inactive ticking duration 与 inhibited no-op 分类；`RuntimeStructuralChangePlanTests` 固化 `ActionFlags`、`ActiveEffectStore.CreateTickActionFlags` 与禁止 `IsStoreDurationDue` 回退。本轮仍不把 Blob 读取、period derived command 写入、duration expired cleanup / stacking policy 执行塞入 job，只把 tick execution 的动作决策数据面推进到 Burst job candidate。

SEffectTick legacy duration action candidate classification 第一刀已接入：`ActiveEffectStore.CreateLegacyDurationTickActionFlags` 把 legacy duration fallback 的 active period due、active duration expire、inactive ticking duration expire 统一为 store 纯值 action flags；`CollectDurationEffectCandidatesJob` 通过 `ComponentTypeHandle<CDurationDefinition>` / `CDurationRuntime` / `CPeriodDefinition` / `CPeriodRuntime` / `CEffectLifecycle` 与 `BufferLookup<BGameplayEffect>` 在 IJobChunk 阶段只输出带 `DurationEffectCandidate.ActionFlags` 的有效 candidate。主线程 legacy fallback 分支改为 `TickLegacyDurationEffect`，只按 `HasTickAction` 消费已分类 candidate，不再在主线程复制 active / inactive / period / duration due 判定；`ActiveEffectStoreTests.TickActionFlagsClassifyLegacyDurationPeriodAndDurationWork` 覆盖 active no-op、period-only、duration-only、both、inactive ticking expire、inactive non-applied no-op 与 inactive stopped no-op；`RuntimeStructuralChangePlanTests` 固化 legacy duration action flags、可选 component handles、`BufferLookup<BGameplayEffect>`，并禁止恢复 `TickDurationEffect` / `TickInactiveDuration`。本轮仍不把 Blob 读取、period derived command 写入、duration expired cleanup / stacking policy 执行塞入 job，只把 legacy duration tick execution 的动作决策数据面推进到 Burst job candidate。

GlobalIndexedStore candidate fan-in / deterministic merge 第一刀已接入：`RefreshOwnerLocalChunkSkipIndexJob` 不再输出 owner 让主线程 `TrySyncGlobalIndexOwner` 二次扫描 owner buffer，而是直接用 `NativeQueue<BGlobalActiveEffectIndex>.ParallelWriter` 输出 global index entry candidate。主线程 drain queue 后执行 `SortGlobalIndexCandidates`，再通过 `ActiveEffectStore.TryMergeGlobalIndexEntries` 一次性 merge 到显式 global index owner，并由 `ActiveEffectStore.CreateGlobalIndexEntry` 复用 Burst-safe value 构造。`ActiveEffectStoreTests.GlobalIndexedStoreBatchMergeSortsCandidatesAndUpdatesCounters` 覆盖无序 candidate 输入下的确定性 owner/sequence 排序与 active / periodDue / durationDue 计数刷新；`RuntimeStructuralChangePlanTests` 固化 `GlobalIndexCandidates`、`NativeQueue<BGlobalActiveEffectIndex>.ParallelWriter`、`SortGlobalIndexCandidates`、`TryMergeGlobalIndexEntries`，并禁止 `SEffectTick` 回退到 `OwnersToSyncGlobalIndex` / `NativeQueue<Entity>` / `TrySyncGlobalIndexOwner(em` owner rescan 路径。本轮仍保留 singleton DynamicBuffer index proof，不引入 chunk bucket 或 per-effect stable entity。

GlobalIndexedStore bucket owner 第一刀已接入：`CActiveEffectGlobalIndexBucket` + `BGlobalActiveEffectIndexBucketOwner` 与 `ActiveEffectStore.GlobalIndexBucketCount = 16` 落地，`EnsureGlobalIndexStore` 在低频 bootstrap / repair 阶段创建固定 bucket owner surface。`TrySyncGlobalIndex` / `TryMergeGlobalIndexEntries` / `TryRemoveGlobalIndex` / `TryRefreshGlobalIndexCounters` 现在写 bucket owner buffer，再刷新 root `CActiveEffectGlobalIndexStore + BGlobalActiveEffectIndex` 聚合镜像，保留现有 Debugger / 测试观察面。Debugger 新增 `globalIndexBucketOwners` / `globalIndexBucketIndexCount` / `globalIndexMaxBucketLength` 与 diagnostic event bucket 字段；`ActiveEffectStoreTests`、`GasRuntimeDebuggerTests`、`RuntimeStructuralChangePlanTests` 覆盖 bucket owner 数量、entry 落桶、bucket counter 导出和防回退 token。本轮不引入 per-effect stable entity，不把 bucket 创建放进 hot path，也不把 DynamicBuffer mutation 塞进并行 job。

GlobalIndexedStore stable row 第一刀已接入：`CActiveEffectGlobalIndexStableRow` 复用 runtime GE entity 作为稳定行表面，`GameplayEffectEntityFactory` 在 runtime GE 创建 / prototype instantiate 的低频边界预置 row，hot path 只在已有 row 时 `SetComponentData` 同步，不新增结构变化。`BGlobalActiveEffectIndex.IndexFlags` 新增 `StableRowBacked`，bucket/root counter 新增 `IndexedStableRowCount` / `StaleStableRowCount`；remove global index 会在实际 entity destroy 前把 row 标为 `IsIndexed = 0` 并清 stable flag。QueryLayout / StructuralPlan 将 stable row 声明为 `GameplayEffectActiveRuntime` 的 optional / affected slot，Debugger 输出 `globalIndexStableRows` / `globalIndexStaleStableRows` 与 event 字段。`ActiveEffectStoreTests` 覆盖 factory 预置 row、duration lifecycle 同步 row、global index remove 前 row compact；`GasRuntimeDebuggerTests` 覆盖 stable row counter 文本导出。本轮仍保留 runtime GE entity fallback，stable row 是 store-only lifecycle 前的低频结构表面，不在 tick hot path 创建 per-effect entity。

SEffectRemove cleanup candidate IJobChunk collector 第一刀已接入：`SEffectRemove` 不再对 `_cleanupQuery` / `_removeQuery` / `_activeEffectStoreQuery` 做 `ToEntityArray`，而是通过 `CollectCleanupEffectCandidatesJob`、`CollectPendingRemoveSlotCandidatesJob`、`CollectLegacyDestroyEffectCandidatesJob` 三个 `IJobChunk` collector 把 cleanup shell、owner-local pending slot 与 legacy destroy marker 汇入 `NativeQueue<EffectCleanupCandidate>.ParallelWriter`。主线程 drain 后执行 `SortCleanupCandidates`，按 cleanup shell -> owner-local pending slot -> legacy marker 的 source order 和 entity / slot key 确定性去重消费；`RuntimeStructuralChangePlanTests` 固化 collector / queue / sort token，并禁止 `SEffectRemove` 回退到三处 `ToEntityArray`、`foreach (var owner in owners)` 和旧 `CollectPendingRemoveEffects` owner scan。本轮仍保留 cleanup execution 主线程 EntityManager / ECB 语义，不把 `CleanupActiveEffect` 塞入 job。

SEffectRemove cleanup action candidate classification 第一刀已接入：`EffectCleanupCandidate` 新增 `ActionFlags`，`EEffectCleanupCandidateActionFlags` 区分 `CleanupActiveEffect`、`DestroyEffectEntity` 与 `SkipFinalDestroy`。cleanup shell / legacy destroy collector 通过 `ComponentTypeHandle<CEffectContext>` 在 IJobChunk 阶段判断 cleanup 语义，owner-local pending-remove slot collector 通过 `ComponentLookup<CEffectContext>` / `ComponentLookup<CEffectFinalDestroy>` 在 candidate 数据面过滤已销毁或已 final-destroy 的 effect，并把无 `CEffectContext` 的 orphan cleanup/destroy marker 分类为实际 destroy。主线程改为 `ExecuteCleanupCandidate`，只按已分类 action flags 调用 `CleanupActiveEffect` 或 `DestroyEffectEntity`；`ActiveEffectStoreTests.SEffectRemoveDestroysOrphanCleanupCandidatesWithoutEffectContext` 覆盖无 context orphan cleanup / destroy candidate 被销毁，`RuntimeStructuralChangePlanTests` 固化 action flags / lookup / helper token，并禁止 `SEffectRemove` 回退到主线程 `em.HasComponent<CEffectContext>(ge)` / `em.HasComponent<CEffectFinalDestroy>(ge)` 判定。本轮仍不把 cleanup execution 主体塞入 job。

CEffectCleanup execution plan shell 第一刀已接入：`CEffectCleanup` 新增 `RequestedCleanupWorkFlags`，cleanup shell 不再只是语义 marker，而是承载 cleanup execution 请求计划。`ActiveEffectStore.CreateCleanupWorkFlags` 成为 store / legacy buffer / runtime modifier / granted tag / granted ability / duration runtime / entity destroy 的统一 work plan 构造入口；`EffectRuntimeUtility.MarkEffectForRemoval` 与 `SAscDestroyRequest` 写 shell 时同步写入 requested plan，`CleanupActiveEffect` 通过 `ResolveRequestedCleanupWorkFlags` 优先消费 shell plan，并用 `HasCleanupWork` gate `RemoveGrantedAbilities`、`RemoveRuntimeModifiers`、`RemoveGrantedTags`、`RemoveEffectFromTarget`、duration runtime deactivate 与 final destroy marker 投递。`ActiveEffectStoreTests.CleanupActiveEffectConsumesCleanupShellExecutionPlan` 覆盖 cleanup 前 legacy target buffer 被外部清空时，cleanup record 仍记录并消费 shell 中的 `LegacyTargetBuffer` plan；`RuntimeStructuralChangePlanTests` 固化 `RequestedCleanupWorkFlags` / `CreateCleanupWorkFlags` / `ResolveRequestedCleanupWorkFlags` / `HasCleanupWork` token。本轮把 cleanup execution 的 requested work plan 写入数据面，但具体 cleanup 子步骤仍在主线程 EntityManager / ECB 语义阶段执行。

SEffectFinalDestroy final destroy candidate IJobChunk collector 第一刀已接入：`SEffectFinalDestroy` 不再通过 `_finalDestroyQuery.ToEntityArray` materialize final destroy entity，而是调度 `CollectFinalDestroyCandidatesJob : IJobChunk`，用 `EntityTypeHandle` 与 `NativeQueue<FinalDestroyCandidate>.ParallelWriter` 并行收集 `CEffectFinalDestroy` 候选。主线程 drain 后执行 `SortFinalDestroyCandidates`，按 effect entity Index / Version 稳定排序，再调用现有 `EffectRuntimeUtility.FinalizeEffectDestroy(em, ref ecb, ge, currentFrame)` 保持 cleanup record resolved flags 与实际 destroy 语义。`ActiveEffectStoreTests.SEffectFinalDestroyConsumesDeferredFinalDestroyCandidates` 覆盖 cleanup ECB defer 后由系统消费 final destroy marker；`RuntimeStructuralChangePlanTests` 固化 collector / queue / sort / schedule token，并禁止恢复 `_finalDestroyQuery.ToEntityArray`。本轮只迁移 final destroy candidate collection，不把 `FinalizeEffectDestroy` 主体塞入 job。

剩余：Duration / Stack / Period / Granted state 仍保留 runtime GE entity fallback；runtime GE entity 实际销毁已从 cleanup helper 内直接 destroy 拆到 `CEffectFinalDestroy` / `SEffectFinalDestroy` finalizer，final destroy candidate collection 已推进到 IJobChunk collector，但仍不是完整 store-only active effect lifecycle。LifecycleCleanupStore 已有 owner-local cleanup record 与 requested/resolved work 证据面，cleanup work candidate collection、cleanup action classification 与 `CEffectCleanup.RequestedCleanupWorkFlags` execution plan shell 已进入数据面，final destroy candidate collection 也已推进到 IJobChunk collector；cleanup execution 主体已开始按 shell plan gate granted ability / modifier / tag / target buffer / duration runtime / entity destroy 子工作，但 cleanup/finalize execution 具体执行仍是主线程 EntityManager / ECB 语义阶段。ChunkSkipIndex 已从 Debugger-only evidence 推进到 `SEffectTick` store-first gate、slot cursor execution、owner-local IJobChunk refresh、legacy duration candidate IJobChunk collector、legacy duration candidate parallel writer / deterministic sort、due-slot parallel writer / deterministic sort 第一刀、store-only update gate 第一刀、store-only duration expire 第一刀、owner-local action candidate classification 第一刀和 legacy duration action candidate classification 第一刀；`SEffectTick` 更新条件已脱离 legacy duration query，owner-local due slot 无 legacy `CDurationRuntime` 时已能进入 pending-remove cleanup 链，owner-local 与 legacy duration fallback 的 period / duration-expire 动作分类也已进入 IJobChunk candidate 数据面，但 period command / duration expired cleanup / stacking policy 执行主体仍是主线程 EntityManager / ECB 语义。GlobalIndexedStore 已完成显式 owner + DynamicBuffer 第一刀、candidate fan-in / deterministic merge、固定 bucket owner surface 与 stable row surface，但仍不是完整 store-only lifecycle / chunk component 级 scale-ready 全局索引。真正 cleanup execution system、chunk component skip、jobified tick execution、store-only active effect lifecycle 以及更彻底的 slot compact 策略仍待迁移。

## 后续目标

继续把 duration / stack / period / granted state 从 runtime GE entity lifecycle 推向 store-driven lifecycle。下一步优先推进 cleanup execution system、chunk component skip / jobified tick execution 与 store-only active effect lifecycle；GlobalIndexedStore stable row 已有第一刀，后续重点是让它服务 cleanup / tick / scale gates，而不是继续在 hot path 增加结构表面。不在当前阶段移除 `BGameplayEffect` fallback，不一次性替换完整 duration GE apply/remove。

## 当前问题

1. Duration / Stack / Period / Granted state 仍主要依赖 runtime GE entity lifecycle 和 `BGameplayEffect` 目标 buffer。旧 GE lifecycle 的 per-hit entity create/destroy 是核心热点来源（ISSUE-001）。
2. 如果直接把所有 active effect 迁移成 entity 或全部塞入 ASC buffer，会违反 `05-ActiveEffectStoreSpec` 的分层选型要求。
3. 普通生命周期状态切换不能通过 hot path add/remove component 表达，否则会扩大 ISSUE-004 的结构变化风险。
4. `EffectRuntimeUtility`（2125行）仍有 8+ 处 `PlaybackAndReset` 碎片化 ECB（P0 缺陷 D），store helper 不得引入新的临时 ECB+Playback 模式。

## 目标

1. 建立 `OwnerLocalStore`：ASC owner 上的 `CActiveEffectStore` + `BActiveEffectSlot` 是第一落点。GlobalIndexedStore / LifecycleCleanupStore / ChunkSkipIndex 按小闭环逐步扩展，不能把 proof API 误判为最终 scale-ready 形态。
2. duration runtime GE entity 生命周期镜像到 owner slot；period due / overflow simple instant child GE 优先派生为 EffectCommand（已落地 proof）。
3. hot path 缺少 store 时必须返回失败，不允许 helper 隐式创建组件或 buffer。
4. Query layout / contract tests 必须能审查 ActiveEffectStore 是 owner-local target contract，而不是结构变化热点。
5. 后续扩张必须复用 AM2B rebind contract 的 frame owner、structural playback gate 和 Debugger backbone counters。

## 目标态约束摘要

（从关联 Spec 提取，与本任务直接相关的硬约束）

1. ActiveEffectStore 分层选型：OwnerLocalStore（当前第一落点）→ GlobalIndexedStore → LifecycleCleanupStore → ChunkSkipIndex，不得跳跃选型。
   > 来源：`05-ActiveEffectStoreSpec.md`

2. store slot 只镜像跨帧状态摘要和 owner/source/context，不把 definition 与 runtime timer 混写在同一字段。
   > 来源：`05-ActiveEffectStoreSpec.md`

3. cleanup / grant / remove 相关结构变化必须 route 到 AM2B-D structural playback gate，不得在 hot path 直接执行。
   > 来源：`03-RuntimeCore管线Spec.md`、AM2B-D

4. Observation projection 只能消费 store state 派生的 typed facts，不能反向改变 store state。
   > 来源：`03-RuntimeCore管线Spec.md`

5. store 容量在 ASC 创建时显式指定，hot path 不隐式扩容 buffer。
   > 来源：`GASRuntimeFrameBackboneRebindContract.cs`

## 当前事实约束

（从关联 ISSUE 提取，本任务必须知道的架构事实）

1. 旧 GE lifecycle 的 per-hit entity create/destroy 是核心热点（ISSUE-001）。store 的 slot 状态切换不能通过 hot path add/remove component 表达，必须用 enum state + Enableable。
   > 来源：`ISSUE-001`、`ISSUE-004`

2. AM2B Frame Backbone 已闭合（ISSUE-009 Resolved），store 后续实现必须复用 frame owner、structural playback gate 和 Debugger backbone counters。
   > 来源：`ISSUE-009`

3. `EffectRuntimeUtility`（2125行）仍有 8+ 处 `PlaybackAndReset` 碎片化 ECB（P0 缺陷 D），store helper 禁止引入 `new EntityCommandBuffer(Allocator.Temp) → 操作 → Playback → Dispose` 模式。
   > 来源：`P0-致命缺陷.md` 缺陷 D

## 适用规则摘要

| 规则 | 本任务约束 |
|---|---|
| `SC-01` | store 创建仅限 ASC factory / 低频 bootstrap；hot path 缺 store 返回失败，不隐式补结构 |
| `ECB-01` | ECB Playback 统一在 System 末尾，禁止在 store helper 内中间 playback |
| `ECB-03` | 禁止在 helper 内创建临时 ECB 并立即 Playback（如 `EffectRuntimeUtility.PlaybackAndReset` 模式） |
| `PRF-02` | hot path 禁止每 entity 主线程 Get/SetComponentData 读写 store slot |
| `PRF-05` | store 遍历优先评估 IJobEntity 替代主线程 foreach（参考 `SAbilityTick` A+ 范本） |
| `PRF-14` | 生命周期状态切换通过 Enableable，避免 add/remove component 引发 archetype churn |
| `FSM-06` | 普通状态切换（PendingApply→Active→Inhibited→PendingRemove）通过 enum state，不触发 archetype churn |
| `BUF-01` | `BActiveEffectSlot` buffer 容量在创建时显式指定，不隐式扩容 |
| `BUF-02` | owner-local store buffer 仅主线程访问，不跨 job 共享写入 |
| `QRY-01` | Query 访问 store 时使用 `RefRO`/`RefRW` 显式读写意图 |
| `SEL-04` | 行动报告必须区分 OwnerLocalStore / GlobalIndexedStore / LifecycleCleanupStore / ChunkSkipIndex 四类 store surface |
| `SEL-05` | helper 不负责补 `CActiveEffectStore` 或 `BActiveEffectSlot`，缺失时返回失败 |
| `SYS-04` | store 状态通过 ECS component/buffer 传递，禁止 static 或 managed 中介 |

## 非目标

1. 不在第一刀替换完整 duration GE apply/remove。
2. 不移除 `BGameplayEffect`。
3. 不重写 granted tag / ability cleanup。
4. 不推进 AutoChess 业务拆分。
5. GlobalIndexedStore stable row 第一刀复用 runtime GE entity 作为稳定行表面，不在 tick hot path 创建 per-effect entity；chunk component skip、jobified global scan 与 store-only lifecycle 属于后续 scale-ready 迁移。

## 执行范围

1. `Assets/GAS/Runtime/Effect/Component/Dynamic/CActiveEffectStore.cs` — store 组件和 slot buffer 定义
2. `Assets/GAS/Runtime/AbilitySystem/AbilitySystemEntityFactory.cs` — ASC 创建时添加 store
3. `Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs` — GE lifecycle → slot 镜像
4. `Assets/GAS/Runtime/System/Effect/GameplayEffectRequestWriter.cs` — derived command 写入
5. `Assets/GAS/Runtime/System/Effect/SEffectTick.cs` — period tick → derived command
6. `Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs` — command stream 承载
7. `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeQueryLayoutPlan.cs` — Query layout contract
8. `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs` — slot pressure baseline
9. `Assets/GAS/Runtime/General/GASManager.cs` — GlobalIndexedStore bootstrap owner
10. `Assets/_Test/GAS/Runtime/Effect/ActiveEffectStoreTests.cs`、`StackingRuntimeTests.cs`
11. `Assets/_Test/GAS/Runtime/Event/RuntimeQueryLayoutPlanTests.cs`
12. `Assets/_Test/GAS/Runtime/Event/RuntimeStructuralChangePlanTests.cs`
13. `Assets/_Test/GAS/Runtime/Debugger/GasRuntimeDebuggerTests.cs`

## 执行细则

1. **Store surface 分层推进**：OwnerLocalStore 是 duration lifecycle 的第一落点；GlobalIndexedStore 已采用显式 owner + DynamicBuffer secondary index + bucket owner + runtime GE stable row surface，不在 tick hot path 把每个 active effect 新建为结构实体。
2. **Slot 字段分离**：`BActiveEffectSlot` 只镜像跨帧状态摘要和 owner/source/context，不把 definition 与 runtime timer 混写。
3. **Enum state 非 archetype churn**：`PendingApply / Active / Inhibited / PendingRemove` 用 enum state 表达，生命周期状态切换不触发 archetype churn。
4. **helper 不补结构**：helper 不负责补 `CActiveEffectStore` 或 `BActiveEffectSlot`，结构创建只允许 ASC factory / 低频 bootstrap 处理。hot path 缺 store 返回失败。
5. **Store surface 四分类**：AM5 行动报告必须区分 OwnerLocalStore、GlobalIndexedStore、LifecycleCleanupStore、ChunkSkipIndex 四类 store surface。
6. **结构变化路由**：cleanup / grant / remove 相关结构变化必须 route 到 AM2B-D structural playback gate。
7. **Derived command 主链**：period due / overflow simple instant child GE 已进入 `EffectCommand` proof 主链，禁止回退到创建 `CApplyGameplayEffectRequest` 或 runtime child GE entity。
8. **Debugger 证据**：slot pressure 与 global index Debugger baseline 已补，每次交还必须更新 slot count / capacity / state distribution / global index due & stale 数据。
9. **禁止 PlaybackAndReset**：store helper 中禁止出现 `new EntityCommandBuffer(Allocator.Temp) → 操作 → Playback → Dispose` 模式。ECB 从外部传入，Playback 统一在 System 末尾。

## 验收标准

1. ASC factory 创建的 ASC 必有 `CActiveEffectStore` 和 `BActiveEffectSlot`。
2. duration GE 激活、抑制、恢复、待移除和清理能同步 owner slot。
3. 缺少 store 的目标不会在 hot path 被自动补结构变化。
4. Query layout 明确 ActiveEffectStore 不属于 `StructuralEntityManagerHotspot`。
5. Debugger 能输出 owner-local slot pressure 和 legacy mirror 证据。
6. period due simple instant child GE 不创建 apply request / runtime child GE entity。
7. GlobalIndexedStore 由低频 bootstrap 显式创建，hot path 缺 global index owner 时只返回 false，不自动补结构。
8. Debugger 能输出 GlobalIndexedStore owner / index / active / inhibited / pendingRemove / due / stale 计数。
9. `rg -n "PlaybackAndReset" Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs` 数量不增加。
10. `dotnet build` Runtime + Tests 通过；Unity Test Runner 如受 LicensingClient 阻塞则记录为环境阻塞。

## 测试链路

1. `git diff --check`
2. `dotnet build` Runtime + Tests
3. `rg -n "PeriodSimpleInstantDerivedEffectWritesEffectCommandAndUpdatesStoreCursor|TryRefreshPeriodFrame|EEffectCommandSource\.Overflow|TryAppendDerivedEffectCommand" Assets/GAS/Runtime Assets/_Test/GAS/Runtime`
4. `rg -n "PlaybackAndReset" Assets/GAS/Runtime/System/Effect/ -g "*.cs"`
5. Runtime EditMode tests；如 Unity LicensingClient 阻塞，记录为环境阻塞。

## 交还内容

1. 更新本文件 `当前进展`、`领取轮次`、`状态` 字段。
2. 更新 `02-主线任务树/README.md` 根看板 AM5 条目。
3. 更新 `04-当前进度状态/当前窗口.md` 推荐领取。
4. 若 ISSUE-001/004 相关事实变化，更新 `00-当前架构事实/核心问题诊断/`。
