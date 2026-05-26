# 02 主线任务树

本目录维护 EX-GAS 2.0 的任务树，并承担原 `迭代路线总览` 的职责。当前路线根入口统一为 `../README.md`。

任务树的每个节点（不论处于树的哪一层）都必须能作为 Agent prompt 上下文。分支节点提供子树索引和验收门槛，叶子节点提供可直接领取的执行单元。节点文档不是目录索引或任务列表——必须写清当前问题、目标态参考、历史方案参考、目标/非目标、执行范围、验收标准和测试链路。

任务树承接目标态主 Spec，不重新定义目标态。它的工作是把 `01-目标态架构共识/` 的总目标拆成主线、支线和任务，并把每个节点写成 Agent 可以直接领取、循环推进、验证交还的任务提示词。

```text
历史方案讨论 -> 目标态架构共识 -> 主线任务树 -> Goal 循环推进
```

## 当前路线看板

本看板就是当前迭代路线总览，不再拆出独立 `路线总览/` 文件夹。路线阶段必须能落到具体分支节点和可领取叶子节点；如果阶段无法落到任务节点，先补任务树上下文。

| 顺序 | 任务名 | 状态 | 所属主线 | 路线作用 |
|---|---|---|---|---|
| 1 | GAS ECS Runtime - Runtime Core 重构 - Freeze Safety Gate | 契约已确立 | T1 | AM-0，冻结旧 pipeline 扩张 |
| 2 | Observation / Presentation / Debugger - Runtime Core Debugger - Diagnostics Counters Baseline | 契约已确立 | T4 | AM-1，补 counters 和诊断出口 |
| 3 | 文档治理与目标态共识 - Unity DOTS 官方文档全覆盖系列（AM-1B~AM-2G，25轮） | 已完成 | T0/T1 | ~175 个 PackageCache 文档全部读完，CASE 47 / PRF 34 / FSM 6 / SEL 5 / ODF 18 规则体系建立。完整历史见 [已完成文档校准日志](T0-文档治理与目标态共识/已完成文档校准日志.md) |
| 4 | GAS ECS Runtime - Runtime Core 重构 - EffectCommand 与 SpecStream 契约 | 契约已确立 | T1 | AM-2，定义新 effect 入口和 phase，command/spec/delta/fact 数据契约落地 |
| 5 | GAS ECS Runtime - Runtime Core Frame Backbone - Schedule / Phase Contract | 契约已确立 | T1 | AM-2.5A，SystemGroup / phase contract |
| 6 | GAS ECS Runtime - Runtime Core Frame Backbone - Frame Arena 与 Query Budget | 契约已确立 | T1 | AM-2.5B，query / lookup / allocator / dependency budget |
| 7 | GAS ECS Runtime - Runtime Core Frame Backbone - Stream Owner 与 Deterministic Merge | 契约已确立 | T1 | AM-2.5C，command/spec/delta/fact owner、clear/read/write/merge |
| 8 | GAS ECS Runtime - Runtime Core Frame Backbone - Structural Playback Gate | 契约已确立 | T1 | AM-2.5D，唯一结构变化屏障 |
| 9 | GAS ECS Runtime - Runtime Core Frame Backbone - Debugger Evidence Gate | 契约已确立 | T1/T4 | AM-2.5E，frame backbone counters 和 Profiler / Journaling 对照 |
| 10 | GAS ECS Runtime - Runtime Core Frame Backbone - AM3 / AM5 Rebind 与交还验证 | 契约已确立 | T1/T4 | AM-2.5F，AM3 / AM5 绑定到 backbone |
| 11 | GAS ECS Runtime - Runtime Core 重构 - Instant Spec Evaluation 迁移 | 契约已确立 | T1 | AM-3，simple instant GE 默认不创建 runtime GE entity。已完成：激活/cost/Timeline ApplyEffects single+multi target producer，attribute/cue/generic/damage typed fact native consumer，EventBus System/Effect callsite 收尾，parallel fan-in contract proof。Unity Test Runner 待补跑 |
| 12 | GAS ECS Runtime - Runtime Core 重构 - Active Effect Store 重建 | 进行中 | T1 | AM-5，duration/stack/period/granted state 稳定存储。owner-local store + Debugger slot baseline + period/overflow derived command proof + granted runtime buffer Playback 收缩 + granted tag/ability active count 镜像、Debugger 聚合、remove request / ability cleanup created-effect pending-remove handoff、actual cleanup compact frame、granted ability cleanup runtime-policy、active granted ability cancel 全链路证据、LifecycleCleanupStore owner-local cleanup record 与 requested/resolved work flags、ChunkSkipIndex owner-local evidence / Debugger 指标面、SEffectTick store-first tick gate、slot cursor execution、owner-local IJobChunk refresh、duration candidate IJobChunk collector、duration candidate parallel writer / deterministic sort 第一刀、due-slot parallel writer / deterministic sort 第一刀、store-only update gate 第一刀、store-only duration expire 第一刀、owner-local action candidate classification 第一刀、legacy duration action candidate classification 第一刀、GlobalIndexedStore candidate fan-in / deterministic merge 第一刀、SEffectRemove cleanup candidate IJobChunk collector 第一刀、SEffectRemove cleanup action candidate classification 第一刀、CEffectCleanup execution plan shell 第一刀、SEffectRemove owner-local pending-remove cleanup gate、CEffectCleanup cleanup component shell、CEffectFinalDestroy / SEffectFinalDestroy actual destroy finalizer、SEffectFinalDestroy final destroy candidate IJobChunk collector 第一刀、GlobalIndexedStore 显式 owner + DynamicBuffer 第一刀、GlobalIndexedStore bucket owner 第一刀、GlobalIndexedStore stable row 第一刀已落地 |
| 13 | Runtime Validation Demo - AutoChess 无头验收 - AutoChessDemo 目录迁移与分层重构 | 暂停 | T6 | DEMO-0，基础拆迁已做，等 Runtime Core 形成可验收链路后恢复 |
| 14 | Observation / Presentation / Debugger - Observation Split - Core 与表现分离 | 后置 | T4 | AM-7，Core simulation tick 与 observation 分离 |
| 15 | Burst / Generated 后置优化 - Generated Runtime Glue | 后置 | T5 | AM-8，Jobify 和 generated static lookup |
| 16 | Runtime Validation Demo - AutoChess 无头验收 - 规模压力测试 | 后置 | T6 | AM-9，x50/x100/x1000/x10w/x100w scale gates |

### AM3 完成边界

AM3 的"完成"不要求迁移所有旧 producer。以下条件全部满足后 AM3 可标为已完成：

1. activation / cost / Timeline ApplyEffects single+multi target simple instant producer 全部走 `BEffectCommand` 主链（已满足）
2. attribute / cue / generic gameplay / damage typed fact 的 native Presentation / Replay consumer 全部就位（已满足）
3. EventBus legacy callsite writer 迁移完成，无静态 `EventBusHelper.Enqueue*` 裸调用残留在 AM3 / System/Effect 热路径（`EffectRuntimeUtility`、`EffectMagnitudeResolver`、`SExecutionCalculation*` 已迁移为显式 `GameplayEventBusWriter`；`Assets/GAS/Runtime/System/Effect/` 搜索为空）
4. parallel fan-in / deterministic merge 在 AM2B backbone 约束下可演示（`ParallelFanInCommandMergeIsStableByTargetThenSequence` 覆盖 stream carrier、merge policy、排序键和重复 merge 稳定性）
5. AM3 路径不再产生临时 `CApplyGameplayEffectRequest` 或 runtime GE entity（已满足 simple instant 路径；复杂 GE fallback 明确标记为 Boundary request 并由 AM5 承接）

### 看板变更说明（2026-05-26 AM5 CEffectCleanup execution plan shell 第一刀轮）

相比上轮看板：
- **cleanup shell 承载执行计划**：`CEffectCleanup.RequestedCleanupWorkFlags` 写入 cleanup requested work plan，不再只作为 cleanup 语义 marker
- **work plan 构造入口统一**：`ActiveEffectStore.CreateCleanupWorkFlags` 汇总 owner-local slot、legacy target buffer、runtime modifier、granted tag、granted ability、duration runtime 与 entity destroy 子工作
- **写 shell 的路径同步补 plan**：`EffectRuntimeUtility.MarkEffectForRemoval` 与 `SAscDestroyRequest` 在写 `CEffectCleanup` 时同步写入 requested cleanup work flags
- **cleanup 执行阶段优先消费 shell plan**：`CleanupActiveEffect` 通过 `ResolveRequestedCleanupWorkFlags` 读取 shell plan，并用 `HasCleanupWork` gate granted ability / modifier / tag / target buffer / duration runtime / final destroy 子步骤
- **合同与行为测试补齐**：新增 `ActiveEffectStoreTests.CleanupActiveEffectConsumesCleanupShellExecutionPlan`；`RuntimeStructuralChangePlanTests` 固化 requested flags、plan helper 和 gate token
- **下一推荐领取保持 AM5**：cleanup shell 已承载 requested work plan，cleanup 执行阶段开始按 plan 消费，但 cleanup/finalize execution 主体仍在主线程 EntityManager / ECB 语义阶段，继续推进 cleanup execution system、jobified tick execution、chunk component skip 与 store-only active effect lifecycle

### 看板变更说明（2026-05-26 AM5 SEffectRemove cleanup action candidate classification 第一刀轮）

相比上轮看板：
- **cleanup 动作分类进入 candidate 数据面**：`EffectCleanupCandidate.ActionFlags` 与 `EEffectCleanupCandidateActionFlags` 区分 `CleanupActiveEffect`、`DestroyEffectEntity`、`SkipFinalDestroy`
- **collector 负责上下文判断**：cleanup shell / legacy destroy collector 通过 `ComponentTypeHandle<CEffectContext>` 预分类，owner-local pending slot collector 通过 `ComponentLookup<CEffectContext>` / `ComponentLookup<CEffectFinalDestroy>` 跳过已 final-destroy 或缺失实体
- **主线程只消费已分类 action**：`ExecuteCleanupCandidate` 按 action flags 调用 `CleanupActiveEffect` 或 `DestroyEffectEntity`，不再在执行阶段临时 `HasComponent` 判定 cleanup / destroy 路径
- **合同与行为测试补齐**：新增 `ActiveEffectStoreTests.SEffectRemoveDestroysOrphanCleanupCandidatesWithoutEffectContext`；`RuntimeStructuralChangePlanTests` 固化 action flags、lookup 和 helper token，并禁止 `SEffectRemove` 回退到主线程 context / final destroy 判定
- **下一推荐领取保持 AM5**：cleanup candidate collection 与 action classification 都已进入 IJobChunk candidate 数据面，但 cleanup execution / finalize execution 主体仍在主线程 EntityManager / ECB 语义阶段，继续推进 cleanup execution system、jobified tick execution、chunk component skip 与 store-only active effect lifecycle

### 看板变更说明（2026-05-26 AM5 SEffectTick legacy duration action candidate classification 第一刀轮）

相比上轮看板：
- **legacy duration tick 动作分类下沉到 store 纯值层**：新增 `ActiveEffectStore.CreateLegacyDurationTickActionFlags`，统一分类 legacy duration fallback 的 active period due、active duration expire 与 inactive ticking duration expire
- **duration candidate 携带 action flags**：`CollectDurationEffectCandidatesJob` 读取 `CDurationDefinition` / `CDurationRuntime` / `CPeriodDefinition` / `CPeriodRuntime` / `CEffectLifecycle` 与 `BufferLookup<BGameplayEffect>`，只输出带 `DurationEffectCandidate.ActionFlags` 的有效 candidate
- **主线程只消费已分类 candidate**：legacy fallback 分支改为 `TickLegacyDurationEffect`，只按 `HasTickAction` 执行 period / duration-expire，不再在主线程重复 active / inactive / due 判定
- **合同与行为测试补齐**：新增 `ActiveEffectStoreTests.TickActionFlagsClassifyLegacyDurationPeriodAndDurationWork`；`RuntimeStructuralChangePlanTests` 固化 legacy duration action flags、component handles 与 `BufferLookup<BGameplayEffect>`，并禁止恢复 `TickDurationEffect` / `TickInactiveDuration`
- **下一推荐领取保持 AM5**：owner-local 与 legacy duration fallback 的 tick 动作分类都已进入 Burst job candidate 数据面；period derived command、duration expired cleanup / stacking policy、Blob / ECB / EntityManager 执行主体仍在主线程，继续推进 tick execution 主体 job 化、cleanup/finalize execution 主体拆分、chunk component skip 与 store-only active effect lifecycle

### 看板变更说明（2026-05-26 AM5 SEffectTick owner-local action candidate classification 第一刀轮）

相比上轮看板：
- **owner-local tick 动作分类下沉到 store 纯值层**：新增 `EActiveEffectTickActionFlags` 与 `ActiveEffectStore.CreateTickActionFlags`，统一输出 `Period` / `DurationExpire` 动作分类，`ActiveEffectStore.IsTickDue` 复用该分类入口
- **due-slot candidate 携带 action flags**：`RefreshOwnerLocalChunkSkipIndexJob` 在 IJobChunk 阶段写入 `OwnerLocalDueSlotCandidate.ActionFlags`，主线程只按 `HasTickAction` 消费已分类 candidate，不再重新推导 owner-local duration due
- **合同与行为测试补齐**：新增 `ActiveEffectStoreTests.TickActionFlagsClassifyOwnerLocalPeriodAndDurationWork`；`RuntimeStructuralChangePlanTests` 固化 `ActionFlags` / `ActiveEffectStore.CreateTickActionFlags`，并禁止 `SEffectTick` 恢复本地 `IsStoreDurationDue`
- **下一推荐领取保持 AM5**：本轮只把 tick execution 的动作决策数据面推进到 Burst job candidate；period derived command、duration expired cleanup / stacking policy、Blob / ECB / EntityManager 执行主体仍在主线程，继续推进 tick execution 主体 job 化、cleanup/finalize execution 主体拆分、chunk component skip 与 store-only active effect lifecycle

### 看板变更说明（2026-05-26 AM5 SEffectTick store-only duration expire 第一刀轮）

相比上轮看板：
- **store-only duration expire 链路打开**：`SEffectTick` 的 store-backed due slot 到期分支不再硬要求 `CDurationDefinition` / `CDurationRuntime`，无 legacy duration runtime 的 owner-local slot 也能进入 pending-remove cleanup 链
- **owner-local pending-remove helper 补齐**：新增 `ActiveEffectStore.TryMarkPendingRemove`，只更新已有 owner-local slot，不在 hot path 创建缺失 store / slot，并同步 ChunkSkipIndex / GlobalIndexedStore
- **removal helper 同步 store slot**：`EffectRuntimeUtility.MarkEffectForRemoval` 在 legacy `TryUpsertDurationEffect` 失败或缺少 `CDurationRuntime` 时，会 fallback 到 owner-local pending-remove 标记，同时继续写 `CEffectCleanup` 与 legacy `CEffectDestroy`
- **合同与行为测试补齐**：新增 `StackingRuntimeTests.StoreOnlyOwnerLocalSlotExpiresWithoutLegacyDurationRuntime`；`RuntimeStructuralChangePlanTests` 固化 `TryMarkPendingRemove` 与 `MarkEffectForRemoval(em, ref ecb, ge, currentFrame)` token
- **下一推荐领取保持 AM5**：tick execution 主体仍是主线程 EntityManager / ECB 语义，stacking expiration policy / duration refresh 仍依赖 legacy `CDurationRuntime`；继续推进 tick execution 主体 job 化、cleanup/finalize execution 主体拆分、chunk component skip 与 store-only active effect lifecycle

### 看板变更说明（2026-05-26 AM5 SEffectTick store-only update gate 第一刀轮）

相比上轮看板：
- **系统更新 gate 脱离 legacy duration query**：`SEffectTick.OnCreate` 不再 `RequireForUpdate(_activeDurationQuery)`，改为 `RequireForUpdate<GlobalTimer>`，避免 store-only owner-local slot 因缺少 `CDurationDefinition` / `CDurationRuntime` 而跳过整帧 tick 系统
- **空 work 早退改为双 query 判断**：`OnUpdate` 开头只在 `_activeEffectStoreQuery` 与 `_activeDurationQuery` 同时为空时返回，保证 `CActiveEffectStore + BActiveEffectSlot` 可以独立驱动 ChunkSkipIndex refresh
- **合同与行为测试补齐**：新增 `StackingRuntimeTests.StoreOnlyOwnerLocalSlotDrivesTickWithoutLegacyDurationQuery`；`RuntimeStructuralChangePlanTests` 固化 `RequireForUpdate<GlobalTimer>`、双 query 空早退，并禁止恢复 `RequireForUpdate(_activeDurationQuery)`
- **下一推荐领取保持 AM5**：本轮只解除更新条件对 legacy duration query 的绑定，tick execution 主体仍是主线程 EntityManager / ECB 语义；继续推进 tick execution 主体 job 化、cleanup/finalize execution 主体拆分、chunk component skip 与 store-only active effect lifecycle

### 看板变更说明（2026-05-26 AM5 SEffectTick legacy duration candidate parallel writer 第一刀轮）

相比上轮看板：
- **legacy duration candidate 收集并行化**：`SEffectTick` 的 `_activeDurationQuery` collector 从 `NativeList<Entity>` + 单线程 `.Schedule(_activeDurationQuery)` 推进为 `NativeQueue<DurationEffectCandidate>.ParallelWriter` + `ScheduleParallel(_activeDurationQuery, ...)`
- **并行收集后保持确定性执行**：主线程 drain 后执行 `SortDurationEffectCandidates`，按 effect entity Index / Version 稳定排序，再执行现有 legacy duration fallback，避免并行队列改变语义顺序
- **合同防回退补齐**：`RuntimeStructuralChangePlanTests` 固化 `DurationEffectCandidate`、parallel writer、`ScheduleParallel(_activeDurationQuery` 与 `SortDurationEffectCandidates`，并禁止恢复 `.Schedule(_activeDurationQuery` / `NativeList<Entity>`
- **下一推荐领取保持 AM5**：继续推进 tick execution 主体 job 化、cleanup/finalize execution 主体拆分、chunk component skip 与 store-only active effect lifecycle；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 SEffectFinalDestroy final destroy candidate collector 第一刀轮）

相比上轮看板：
- **final destroy candidate 收集迁移到 IJobChunk**：`SEffectFinalDestroy` 不再对 `_finalDestroyQuery` 做 `ToEntityArray`，改为调度 `CollectFinalDestroyCandidatesJob : IJobChunk`，通过 `NativeQueue<FinalDestroyCandidate>.ParallelWriter` 收集 `CEffectFinalDestroy` 候选
- **同帧 finalizer 语义保持稳定**：主线程 drain 后执行 `SortFinalDestroyCandidates`，按 effect entity Index / Version 稳定排序，再调用 `EffectRuntimeUtility.FinalizeEffectDestroy(em, ref ecb, ge, currentFrame)` 保持 cleanup record resolved flags 与实际 destroy 语义
- **合同与测试补齐**：`ActiveEffectStoreTests.SEffectFinalDestroyConsumesDeferredFinalDestroyCandidates` 覆盖 cleanup ECB defer 后由系统消费 final marker；`RuntimeStructuralChangePlanTests` 固化 collector / queue / sort / schedule token，并禁止恢复 `_finalDestroyQuery.ToEntityArray`
- **下一推荐领取保持 AM5**：继续推进 cleanup execution 主体拆分、chunk component skip / jobified tick execution 与 store-only active effect lifecycle；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 GlobalIndexedStore stable row 第一刀轮）

相比上轮看板：
- **stable row 表面接入**：`CActiveEffectGlobalIndexStableRow` 复用 runtime GE entity 作为稳定行，`GameplayEffectEntityFactory` 在 runtime GE 创建 / prototype instantiate 低频边界预置 row，hot path 只更新已有 row
- **bucket/root counter 扩展**：global index entry 新增 `StableRowBacked` flag，bucket/root store 新增 `IndexedStableRowCount` / `StaleStableRowCount`，Debugger 输出 `globalIndexStableRows` / `globalIndexStaleStableRows`
- **合同与测试补齐**：QueryLayout / StructuralPlan 声明 `ActiveEffectGlobalIndexStableRow` 为 active runtime GE optional / affected slot，`ActiveEffectStoreTests` 覆盖 factory 预置、duration lifecycle row sync 和 remove 前 row compact
- **下一推荐领取保持 AM5**：继续推进 cleanup execution system、chunk component skip / jobified tick execution 与 store-only active effect lifecycle；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 GlobalIndexedStore bucket owner 第一刀轮）

相比上轮看板：
- **global index 写入面落桶**：`ActiveEffectStore.EnsureGlobalIndexStore` 低频创建 / 修复固定 16 个 `CActiveEffectGlobalIndexBucket` owner，root `BGlobalActiveEffectIndexBucketOwner` 记录 bucket owner map
- **root buffer 降为聚合镜像**：`TrySyncGlobalIndex`、`TryMergeGlobalIndexEntries`、`TryRemoveGlobalIndex`、`TryRefreshGlobalIndexCounters` 写 bucket owner buffer，再刷新 root `CActiveEffectGlobalIndexStore + BGlobalActiveEffectIndex` 聚合/debug 镜像
- **Debugger bucket 指标补齐**：Runtime Core Debugger 输出 `globalIndexBucketOwners`、`globalIndexBucketIndexCount`、`globalIndexMaxBucketLength`，diagnostic event 文本同步输出 `activeEffectGlobalIndexBucket...`
- **下一推荐领取保持 AM5**：继续推进 GlobalIndexedStore stable entity、chunk component skip / jobified tick execution、跨帧 cleanup execution system 与 store-only active effect lifecycle；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 SEffectRemove cleanup candidate collector 第一刀轮）

相比上轮看板：
- **cleanup candidate 收集迁移到 IJobChunk**：`SEffectRemove` 不再对 `_cleanupQuery` / `_removeQuery` / `_activeEffectStoreQuery` 做 `ToEntityArray`，改为用 `CollectCleanupEffectCandidatesJob`、`CollectPendingRemoveSlotCandidatesJob`、`CollectLegacyDestroyEffectCandidatesJob` 收集三类 cleanup work candidate
- **deterministic cleanup merge 收口**：三类来源统一写入 `NativeQueue<EffectCleanupCandidate>.ParallelWriter`，主线程 drain 后执行 `SortCleanupCandidates`，按 cleanup shell -> owner-local pending slot -> legacy marker 的 source order 去重消费
- **合同测试锁定迁移边界**：`RuntimeStructuralChangePlanTests` 固化 collector / queue / sort token，并禁止 `SEffectRemove` 回退到三处 `ToEntityArray`、`foreach (var owner in owners)` 与旧 `CollectPendingRemoveEffects`
- **下一推荐领取保持 AM5**：继续推进 GlobalIndexedStore chunk bucket / stable entity、chunk component skip / jobified tick execution、跨帧 cleanup execution system 与 store-only active effect lifecycle；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 GlobalIndexedStore candidate fan-in 第一刀轮）

相比上轮看板：
- **global index owner rescan 收缩**：`SEffectTick` 的 `RefreshOwnerLocalChunkSkipIndexJob` 不再输出 owner 让主线程 `TrySyncGlobalIndexOwner` 二次扫描 owner buffer，改为直接用 `NativeQueue<BGlobalActiveEffectIndex>.ParallelWriter` 输出 global index candidate
- **deterministic merge 入口前移**：主线程 drain candidate queue 后执行 `SortGlobalIndexCandidates`，再通过 `ActiveEffectStore.TryMergeGlobalIndexEntries` 批量 merge 到显式 global index owner；`ActiveEffectStore.CreateGlobalIndexEntry` 提供 Burst-safe value 构造入口
- **合同测试锁定迁移边界**：`RuntimeStructuralChangePlanTests` 固化 `GlobalIndexCandidates`、`NativeQueue<BGlobalActiveEffectIndex>.ParallelWriter`、`SortGlobalIndexCandidates` 与 `TryMergeGlobalIndexEntries`，并禁止 `SEffectTick` 回退到 `OwnersToSyncGlobalIndex` / `NativeQueue<Entity>` / `TrySyncGlobalIndexOwner(em`
- **下一推荐领取保持 AM5**：继续推进 GlobalIndexedStore chunk bucket / stable entity 选型、chunk component skip、jobified tick execution、跨帧 cleanup system 与 store-only active effect lifecycle；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 SEffectTick due-slot parallel writer 第一刀轮）

相比上轮看板：
- **owner-local due-slot writer 并行化第一刀**：`RefreshOwnerLocalChunkSkipIndexJob` 从输出 due owner 改为用 `NativeQueue<OwnerLocalDueSlotCandidate>.ParallelWriter` 直接输出 due slot candidate，并通过 `ScheduleParallel(_activeEffectStoreQuery, ...)` 并行刷新 owner-local store
- **deterministic sort 收口**：主线程 drain due slot queue 后按 owner entity、slot sequence、slot index、effect entity 稳定排序，再执行现有 store-backed period / duration 语义；GlobalIndexedStore owner sync 也从 queue 输出后排序消费
- **合同测试锁定迁移边界**：`RuntimeStructuralChangePlanTests` 固化 `OwnerLocalDueSlotCandidate`、`NativeQueue<OwnerLocalDueSlotCandidate>.ParallelWriter`、`ScheduleParallel(_activeEffectStoreQuery` 与 `SortDueSlotCandidates`，并禁止恢复 `TickOwnerLocalDueSlots` / `ownersWithDueWork`
- **下一推荐领取保持 AM5**：继续推进 GlobalIndexedStore scale-ready 扫描、chunk component skip、jobified tick execution、跨帧 cleanup system 与 store-only active effect lifecycle；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 SEffectTick duration candidate collector 第一刀轮）

相比上轮看板：
- **legacy duration candidate collection 迁移到 IJobChunk**：`SEffectTick` 不再对 `_activeDurationQuery` 做 `ToEntityArray(Allocator.Temp)`，改为调度 `CollectDurationEffectCandidatesJob : IJobChunk`，用 `EntityTypeHandle` 按 chunk 收集 fallback duration candidate
- **owner-local 与 legacy fallback 串行收敛**：系统先调度 `RefreshOwnerLocalChunkSkipIndexJob` 刷新 owner-local store，再调度 duration candidate collector；主线程仍跳过已有 owner-local slot 的 store-backed GE，只保留 legacy fallback 语义
- **执行语义边界保持清晰**：collector 不访问 Blob / ECB / EntityManager，period derived command 与 duration expire 仍由主线程消费候选实体执行
- **下一推荐领取保持 AM5**：继续推进 GlobalIndexedStore scale-ready 扫描、chunk component skip、parallel due-slot writer / deterministic merge / jobified tick execution、跨帧 cleanup system 与 store-only active effect lifecycle；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 ChunkSkipIndex IJobChunk refresh 第一刀轮）

相比上轮看板：
- **SEffectTick owner-local scan 迁移到 IJobChunk**：`SEffectTick` 不再对 `_activeEffectStoreQuery` 做 `ToEntityArray`，改为调度 `RefreshOwnerLocalChunkSkipIndexJob : IJobChunk`，按 chunk 刷新 `CActiveEffectStore` 的 ChunkSkipIndex counters
- **due owner 仍由主线程语义执行**：job 通过固定容量 `NativeList.AddNoResize` 输出存在 due work 的 owner，主线程只消费这些 owner 的 slot buffer 执行现有 period derived command / duration expire 逻辑，并同步 proof-only GlobalIndexedStore owner
- **合同测试锁定迁移边界**：`RuntimeStructuralChangePlanTests` 固化 `IJobChunk` / `BufferTypeHandle<BActiveEffectSlot>` / `_activeEffectStoreQuery` schedule，并禁止恢复 `_activeEffectStoreQuery.ToEntityArray`
- **下一推荐领取保持 AM5**：继续推进 GlobalIndexedStore scale-ready 扫描、chunk component skip / parallel tick execution、跨帧 cleanup system 与 store-only active effect lifecycle；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 GlobalIndexedStore 第一刀轮）

相比上轮看板：
- **GlobalIndexedStore 显式 owner 接入**：`CActiveEffectGlobalIndexStore` + `BGlobalActiveEffectIndex` 已作为低频 bootstrap 创建的 global index owner 落地，`GASManager.EntityActiveEffectGlobalIndex` 持有索引 owner
- **owner-local slot 同步 global index**：duration activate / inhibit / reactivate / pending-remove、duration / stack / period / granted tag / ability refresh、chunk skip refresh 和 final destroy 前移除会同步或清除 global index
- **Debugger 与合同补齐**：Runtime Core Debugger 输出 `globalIndexOwners` / `globalIndexCount` / active / inhibited / pendingRemove / periodDue / durationDue / stale，QueryLayout / StructuralPlan 声明 `ActiveEffectGlobalIndexStore` / `ActiveEffectGlobalIndexBuffer`
- **下一推荐领取保持 AM5**：继续推进 GlobalIndexedStore scale-ready 扫描、真正 chunk component / `IJobChunk` skip、跨帧 cleanup system 与 store-only active effect lifecycle；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 actual destroy finalizer 轮）

相比上轮看板：
- **cleanup 与 actual destroy 两段化**：`CleanupActiveEffect(em, ref ecb, ge, currentFrame)` 只完成语义 cleanup、owner-local slot compact 与 cleanup record before-entity-destroy resolved flags，然后投递 `CEffectFinalDestroy`
- **SEffectFinalDestroy 同帧收口**：新增 `SEffectFinalDestroy`，在 `GASEffectGroup` 中排在 `SEffectRemove` 之后、`SEffectTick` 之前，集中调用 `EffectRuntimeUtility.FinalizeEffectDestroy` 完整 resolved cleanup record 并实际 destroy runtime GE entity
- **热路径排除 final marker**：apply / tick / ongoing tag requirements / execution calculation / remove request / ASC destroy / AutoBattle execute calculation 等路径已排除 `CEffectFinalDestroy`
- **下一推荐领取保持 AM5**：继续推进 GlobalIndexedStore scale-ready 扫描、跨帧 cleanup system、真正 chunk component / `IJobChunk` skip 与更彻底的 store-only active effect lifecycle；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 cleanup component shell 轮）

相比上轮看板：
- **CEffectCleanup shell 接入**：`EffectRuntimeUtility.MarkEffectForRemoval` 和 `SAscDestroyRequest` 会写入 `CEffectCleanup`，记录 cleanup requested frame、lifecycle state、source / target ASC；`CEffectDestroy` 保留为 legacy marker 和 broad query fallback
- **SEffectRemove shell-first 消费**：`SEffectRemove` 按 `CEffectCleanup` shell -> owner-local pending slot -> legacy `CEffectDestroy` 顺序收集 cleanup work，shell-only GE 不再依赖 legacy marker 进入 `CleanupActiveEffect`
- **热路径排除待清理 GE**：apply / tick / ongoing tag requirements / execution calculation / AutoBattle execute calculation 等路径已排除 `CEffectCleanup`，remove request 的重复处理判断也把 shell 视为 pending cleanup
- **下一推荐领取保持 AM5**：继续推进 actual destroy entity 深迁移、GlobalIndexedStore、跨帧 cleanup system 与真正 chunk component / `IJobChunk` skip；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 pending-remove cleanup gate 轮）

相比上轮看板：
- **SEffectRemove owner-local cleanup gate 接入**：`SEffectRemove` 先扫描 `CActiveEffectStore + BActiveEffectSlot` owner，把 `PendingRemove` slot 收集为本轮 cleanup work；没有 `CEffectDestroy` marker 的 store-backed GE 也能进入 `CleanupActiveEffect`
- **legacy marker fallback 保留并去重**：`CEffectDestroy` query 仍覆盖旧路径，但会跳过本轮已经由 owner-local pending slot 处理过的 effect，避免双 cleanup
- **cleanup shell 前置证据补齐**：新增测试覆盖无 `CEffectDestroy` 的 pending slot cleanup、slot compact、cleanup record 与 resolved flags；真正 cleanup component shell / actual destroy entity 深迁移仍待 AM5 后续

### 看板变更说明（2026-05-26 AM5 slot cursor execution 轮）

相比上轮看板：
- **store-backed tick 执行读 slot cursor**：`SEffectTick` 的 owner-local store 分支会用 `BActiveEffectSlot.LastPeriodFrame` / `PeriodFrame` 判断 period due，用 `StartFrame` / `RemainingFrame` 判断 duration expired；legacy `CDurationRuntime` / `CPeriodRuntime` 仅作为 fallback 和同步出口保留
- **period cursor 反向证据补齐**：当 owner-local slot cursor 已到期而 legacy `CPeriodRuntime.StartTime` 未到期时，store-backed GE 仍会按 slot cursor 触发 period simple instant command，并刷新 slot / legacy cursor
- **下一推荐领取保持 AM5**：继续推进真正 chunk component / `IJobChunk` skip、GlobalIndexedStore、真正 cleanup component shell 与 actual destroy entity 深迁移；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 store-first tick gate 轮）

相比上轮看板：
- **SEffectTick store-first tick gate 接入**：`SEffectTick` 先刷新 owner-local `ActiveEffectStore` 的 ChunkSkipIndex，再按 `BActiveEffectSlot` 的 period/duration due 判断执行 duration tick；legacy duration query 会跳过已有 owner-local slot 的 store-backed GE，避免未到期 slot 被旧 runtime duration 字段误过期
- **inactive ticking duration 语义补齐**：`EActiveEffectSlotFlags.TicksWhenInactive` 承接 `StopTickWhenDeactivated == false`，inhibited 但仍需 tick 的 duration 不再被 ChunkSkipIndex 误判为 noop
- **下一推荐领取保持 AM5**：继续推进真正 chunk component / `IJobChunk` skip、GlobalIndexedStore、真正 cleanup component shell 与 actual destroy entity 深迁移；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 ChunkSkipIndex owner-local evidence 轮）

相比上轮看板：
- **ChunkSkipIndex owner-local evidence 接入**：`CActiveEffectStore` 已记录 matched/skipped/due/no-op slot 计数和 reason flags，`GasRuntimeDebugger` 聚合输出 `chunkSkipMatched` / `chunkSkipSkipped` / `chunkSkipDuePeriod` / `chunkSkipNoop` / `chunkSkipOwners`，为后续真正 chunk component / IJobChunk skip system 提供可审计输入
- **下一推荐领取保持 AM5**：继续推进 actual destroy entity 深迁移、GlobalIndexedStore、真正 cleanup component shell 与真正 chunk-level skip system；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM5 cleanup handoff、compact frame 与 active cancel 轮）

相比上轮看板：
- **AM5 granted ability runtime buffer 初始化前移**：runtime GE 创建阶段按 `BGrantedAbilityConfig` 预置空 `BGrantedAbilityRuntime`，授予链路不再依赖 helper 内中途 ECB Playback 后才能写 runtime record
- **PlaybackAndReset 收缩**：`EffectRuntimeUtility.AddGrantedAbilities` / `TryAddRuntimeGrantedAbility` 去除内部 `PlaybackAndReset(ref ecb, em);`，新增结构合同防止该调用回流
- **granted runtime state 镜像推进**：`BActiveEffectSlot.ActiveGrantedAbilityCount` 和 `ActiveEffectStore.TryRefreshGrantedAbilityState` 已落地，`SAbilityStateCleanup` 在 ability 自清理后刷新 owner-local slot count
- **granted tag active state 镜像补齐**：`BActiveEffectSlot.ActiveGrantedTagCount`、`ActiveEffectStore.TryRefreshGrantedTagState`、`EffectRuntimeUtility.AddGrantedTags/RemoveGrantedTags` 已串接，slot 可观察 activate / deactivate / reactivate 后的 active granted tag 数量
- **Debugger 聚合补齐**：Runtime Core counters / diagnostic event / text export 已输出 `grantedTags` / `grantedAbilities`，并由 `GasRuntimeDebuggerTests` 覆盖
- **remove request handoff 收束**：`SRemoveGameplayEffectRequest` 统一调用 `EffectRuntimeUtility.MarkEffectForRemoval`，不再直接添加 `CEffectDestroy`，request 消费阶段即可把 owner-local slot 镜像到 `PendingRemove`
- **ability cleanup created-effect handoff 收束**：`SAbilityStateCleanup.CleanupAbilityCreatedEffects` 统一调用 `EffectRuntimeUtility.MarkEffectForRemoval`，ability 自结束/取消清理自身创建的 active GE 时也能把 owner-local slot 镜像到 `PendingRemove`
- **actual cleanup compact frame 接入**：实际 `CleanupActiveEffect` 阶段会在 owner-local slot 被 compact 后写入 `CActiveEffectStore.LastCompactedFrame`，且不再依赖 target 存在 legacy `BGameplayEffect` buffer
- **granted ability cleanup runtime-policy 接入**：`EffectRuntimeUtility.RemoveGrantedAbilities` 清理阶段改为 runtime record + ability 自身 `CGrantedByEffect` 策略驱动，不再依赖 GE static definition blob，也不再复制 `BGrantedAbilityRuntime` 到临时 `NativeArray`
- **active granted ability cancel 全链路证据固化**：active GE cleanup 对 active `SyncWithEffect` granted ability 会先落 cancel / destroy marker，再由 AbilityGroup cleanup 完成 ability entity 销毁，测试覆盖 target granted buffer 与 owner-local slot compact 一致性
- **LifecycleCleanupStore owner-local cleanup record 第一刀接入**：`CActiveEffectStore.LastCleanupFrame` / `CleanupRecordCount` 与 `BActiveEffectCleanupRecord` 已落地，`CleanupActiveEffect` 在 destroy 前记录 cleanup snapshot，不改变当前 runtime GE entity 实际销毁语义
- **cleanup work requested/resolved flags 接入**：`BActiveEffectCleanupRecord` 已能记录 cleanup 前待处理 work flags、active modifier/granted count，并在同步清理完成后回填 resolved flags / resolved frame，为后续真正 cleanup component shell 提供可审计语义面
- **下一推荐领取保持 AM5**：继续推进 actual destroy entity 深迁移、GlobalIndexedStore、ChunkSkipIndex 与真正 cleanup component shell；Unity Test Runner 仍待环境可用时补跑

### 看板变更说明（2026-05-26 AM3 交还轮）

相比上轮看板：
- **AM3 完成边界条件 3 闭合**：`EffectRuntimeUtility`、`EffectMagnitudeResolver`、`SExecutionCalculation`、`SExecutionCalculationOutputModifier` 已迁移到显式 `GameplayEventBusWriter`，`System/Effect` 静态 enqueue 搜索为空
- **AM3 完成边界条件 4 闭合**：`EffectCommandSpecStream` 增加 deterministic fan-in merge，合同测试覆盖 `PerThreadNativeStream` / `StableSortByTargetThenSequence` / `TargetAscThenCommandSequence`
- **下一推荐领取切换**：AM3 Unity Test Runner 可作为环境可用时补跑；主线实现继续推进 AM5 granted cleanup / store-driven lifecycle

### 看板变更说明（2026-05-26 合规审查轮）

相比更早看板：
- **新增审查发现录入**：`SAbilityTick` 为首个 IJobEntity+Burst 合规范例（A+），`SEffectCommandSpecStreamPhases` 游标遍历为正面范本，`GASManager.cs` 已收口为 130 行纯 ECS 启动器
- **合规缺陷生命周期正式化**：Fixed = 直接删除，Improved = 重写当前状态，详见 `00-当前架构事实/维护规范.md`

### 看板变更说明（上轮文档治理）

相比更早看板：
- **AM-1B~AM-2G 25 条文档校准条目压缩为 1 条归档摘要**（条目 3），完整历史迁入 [已完成文档校准日志](T0-文档治理与目标态共识/已完成文档校准日志.md)
- **AM-4（Attribute Delta / Typed Facts）移除**：AM3 已完成 attribute/cue/generic/damage typed fact 的 native consumer，AM-4 独立存在的价值已被 AM3 覆盖
- **AM-6（AutoChess Read Model）移除**：Runtime Core 重构完成前 AutoChess 细节无法确定，届时重新评估
- **状态标签统一**：所有"已完成（Unity验证待补跑）"和"已完成（文档校准）"替换为规范的"契约已确立"或"已完成"

当前状态：`AM-0` 静态收口 + `AM-1` Debugger counters baseline 已完成。Unity DOTS 官方文档全覆盖系列（AM-1B~AM-2G，25轮）已读完 ~175 个 PackageCache 文档，CASE 47 / PRF 34 / FSM 6 / SEL 5 / ODF 18 规则体系建立。`AM-2 EffectCommand / SpecStream 契约` 已落地 ECS 数据契约。`AM-2.5A -> AM-2.5F` Frame Backbone 连续任务链完成 contract-first：phase contract、frame budget、stream owner / deterministic merge、structural playback gate、Debugger evidence gate、AM3 / AM5 rebind handoff。`AM-3` 已完成 activation/cost/Timeline ApplyEffects single+multi target simple instant producer 迁移，attribute/cue/generic/damage typed fact native consumer、EventBus System/Effect callsite 收尾和 parallel fan-in contract proof 全部就位；Unity Test Runner 待补跑。`AM-5` 已完成 owner-local store + Debugger slot baseline + period/overflow derived command proof + granted runtime buffer Playback 收缩 + granted tag/ability active count 镜像、Debugger 聚合、remove request / ability cleanup created-effect pending-remove handoff、actual cleanup compact frame、granted ability cleanup runtime-policy、active granted ability cancel 全链路证据、LifecycleCleanupStore owner-local cleanup record requested/resolved work flags、ChunkSkipIndex owner-local evidence / Debugger 指标面、`SEffectTick` store-first tick gate、slot cursor execution、owner-local IJobChunk refresh、duration candidate IJobChunk collector、duration candidate parallel writer / deterministic sort 第一刀、due-slot parallel writer / deterministic sort 第一刀、store-only update gate 第一刀、store-only duration expire 第一刀、owner-local action candidate classification 第一刀、GlobalIndexedStore candidate fan-in / deterministic merge 第一刀、`SEffectRemove` owner-local pending-remove cleanup gate、cleanup candidate IJobChunk collector 第一刀、`CEffectCleanup` cleanup component shell、`CEffectFinalDestroy` / `SEffectFinalDestroy` actual destroy finalizer、`SEffectFinalDestroy` final destroy candidate IJobChunk collector 第一刀、GlobalIndexedStore 显式 owner + DynamicBuffer 第一刀、bucket owner 第一刀与 stable row 第一刀。

**2026-05-26 审查更新**：全量 Runtime ~222 文件审查。合规缺陷 24 活跃（P0:7, P1:12, P2:5），清除 2（ThreadStatic/ResolveCurrentFrame 重复），改善 4（A/D/E/F）。正面发现：`SAbilityTick` 为首个 IJobEntity+Burst 合规范例（A+），`SEffectCommandSpecStreamPhases` 游标遍历模式为正面范本，`EventBusHelper` ThreadStatic 已消除并改为 `GameplayEventBusWriter` struct，`GASManager.cs` 已收口为 130 行纯 ECS 启动器，`GASSystemScheduleContract`(447行) 8-phase 管线契约完整定义。核心差距：全系统主线程 foreach+ToEntityArray 仍是主要的 P0 违规（`SAbilityCommit`/`SEffectApply`/`SEffectTick`/`SApplyGameplayEffectRequest`），`MCCue` managed class 封锁 5 个 Cue System 的 Burst 编译。下一步推荐 AM5 GlobalIndexedStore scale-ready / cleanup / store-driven lifecycle，并在环境可用时补跑 AM3/AM5 Unity Test Runner。

## 路线原则

1. 先稳 GAS 语义边界，再做性能化和生成化。
2. 先稳 ECS 数据流、实体关系、SystemGroup 调度和配置图，再讨论 API 兼容。
3. OOP 只能位于 Application Shell Layer 或 Runtime Boundary Layer，不能成为 GAS Runtime Core 中间层。
4. 日志、表现 outbox、debug/replay sink 属于 Runtime Boundary Layer 的只读派生，不反向喂给 GAS Runtime Core Layer。
5. 方案12/13/14/15作为验收和架构信号来源；其中 Instant GE 实体化、EventBus 中心化示例只作为风险证据。
6. Unity DOTS 官方文档参考体系作为 Runtime Core 落地规则来源；任务必须先读 `../../UnityDOTS官方文档参考/README.md`，再让 `../../UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 的覆盖矩阵和 `ODF-*` 规则进入行动报告、任务交还和验收摘要。
7. Unity Physics / Entities Graphics 只作为物理输入层和表现边界层进入任务树；相关任务必须引用 `ODF-15..18`，并把 core / physics / render 计时拆分。
8. Runtime Core 遵守 `DOTS Backbone First`：先完成 SystemGroup / Frame Arena / Query / Lookup / Allocator / Dependency / Structural Playback / Debugger evidence，再扩展 AM3 / AM5 的功能迁移。

## 当前不进入

1. 不恢复旧 OOP runtime 主链。
2. 不引入托管 EventBus 作为实时主事件中心。
3. 不让 SourceGenerator 生成 gameplay lifecycle。
4. 不继续给旧 pipeline 堆 fast path 作为主路线。
5. 不在缺少 Runtime Core Frame Backbone 的情况下继续扩大 AM3 / AM5 的旧 lifecycle mirror 或 proof-only stream。

## 主线索引

| 主线 | 目录 | 当前重点 |
|---|---|---|
| T0 文档治理与目标态共识 | [T0-文档治理与目标态共识](T0-文档治理与目标态共识/README.md) | 当前文档拆分和 Spec 规范 |
| T1 GAS ECS Runtime | [T1-GAS_ECS_Runtime](T1-GAS_ECS_Runtime/README.md) | Runtime Core rebuild |
| T2 Definition / Luban 配置权威 | [T2-Definition_Luban配置权威](T2-Definition_Luban配置权威/README.md) | generated / bake / integration contract |
| T3 Editor / UI Toolkit Authoring | [T3-Editor_UI_Toolkit_Authoring](T3-Editor_UI_Toolkit_Authoring/README.md) | authoring follows Definition & Generation Layer |
| T4 Observation / Presentation / Debugger | [T4-Observation_Presentation_Debugger](T4-Observation_Presentation_Debugger/README.md) | Runtime Core Debugger and observation split |
| T5 Burst / Generated 后置优化 | [T5-Burst_Generated后置优化](T5-Burst_Generated后置优化/README.md) | jobify / generated glue after semantics |
| T6 Runtime Validation Demo | [T6-RuntimeValidationDemo](T6-RuntimeValidationDemo/README.md) | 10B 完整业务设计已就绪；AutoChess headless validation and scale gates |

## 任务命名

任务显示名采用职责路径拼接：

```text
主线名 - 子节点名 - 叶子节点名
```

短 ID 只用于检索，不作为对外主标题。节点命名遵守 `01-目标态架构共识/12-命名规范Spec.md`。

## 节点模型

任务树不预设固定层级。节点只有两种类型：

- **叶子节点**：无子节点，Agent 直接领取执行
- **分支节点**：有子节点，只提供上下文索引，不可直接领取

树结构通过 [规范手册](../规范手册.md) 第4节（任务树规范）中的拆分触发条件自动生长。深度不受硬性限制，通过合并触发（所有子节点完成→父节点自动完成、单子合并）自调节。

主干索引见上方「主线索引」。具体任务上下文在各节点的文档中。

## 路线维护规则

1. 主线任务树就是当前迭代路线 owner。
2. 路线原则、阶段路线和当前优先级直接维护在本 README 的 `当前路线看板`。
3. 具体执行上下文维护在各主线、支线和叶子节点中。
4. 不再维护独立 `路线总览/` 文件夹。
5. 任务节点必须引用 `01` 中的目标态 Spec；历史方案只能作为设计素材和行号定位出现。
6. Goal 模式循环推进时，以叶子节点为 prompt 单元；若上下文不足先补节点或补 Spec，不直接进入实现。
7. DOTS 相关任务节点必须引用 `../../UnityDOTS官方文档参考/README.md`，再引用 `../../UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`，并说明相关 `ODF-*` 规则和官方文档覆盖主题。
8. DOTS 相关任务节点必须显式处理 `ODF-09..18` 是否相关：PackageCache hash 路径 / 版本差异、Burst AOT / Player 口径、managed boundary、Baking world、EntityPrefabReference / prefab load、allocator aliasing、Unity Physics、Physics 配置、Entities Graphics、render evidence。相关时要进入验收字段，不相关时要写明原因。
