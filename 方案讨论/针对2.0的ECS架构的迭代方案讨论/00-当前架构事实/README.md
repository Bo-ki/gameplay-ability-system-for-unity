# 00 当前架构事实

> 上次更新：2026-06-06 | 审查范围：`Assets/GAS/Runtime` + `Assets/GAS/Generated/CodeGen/Runtime` + `Assets/AutoChessDemo`

本目录维护当前版本的架构事实、核心问题诊断和合规审查。当前事实以现实代码为第一性参考；旧路线文档、旧目标态文档和旧 issue 结论只能作为历史背景。

## 文件索引

| 文件 | 内容 |
|---|---|
| [Runtime主链事实](Runtime主链事实.md) | 当前 5 段 GAS 主链、generated runtime 接入、command/spec/delta/fact 链 |
| [当前架构图](当前架构图.md) | 当前实际链路图、EffectCommand 链路、DOTS 对照热图 |
| [模块索引](模块索引.md) | Runtime / Generated Runtime / AutoChessDemo / 配置生成当前索引 |
| [AutoChessDemo事实](AutoChessDemo事实.md) | 当前业务 demo 分层、GAS bridge、demo ECS 扩展和风险 |
| [Definition配置事实](Definition配置事实.md) | runtime catalog blob、managed table、generated glue、sourcegen/batchmode 驱动、baking contract 当前状态 |
| [下一轮GAS架构瘦身计划](下一轮GAS架构瘦身计划.md) | 本轮瘦身执行记录与下一步破坏性清理计划：旧事实源已删除，后续聚焦 generated active mutation、global facade 和结构变化证据 |
| [P0-致命缺陷](P0-致命缺陷.md) | 当前最高风险：`Complete()` 防回流、generated active mutation serial job/store 残留、singleton stream、结构变化证据闭环 |
| [P1-高风险缺陷](P1-高风险缺陷.md) | 物理/逻辑 phase 并存、EventBus 迁移、managed registry、AutoChess bridge |
| [P2-改进建议](P2-改进建议.md) | 文档口径、证据拆分、旧文件名清理、contract/proof 标注 |
| ISSUE-001~011 | 当前核心问题按现实代码重审后的单项诊断 |

## 当前核心事实

1. Runtime 主链已经不是旧 `GASEffectGroup / GASAbilityGroup / GASCueGroup / GasStructuralPlaybackSystemGroup`。
2. 当前物理主链是：
   - `GASFramePrepareSystemGroup`
   - `GASCommandResolveSystemGroup`
   - `GASCoreSimulationSystemGroup`
   - `GASStructuralCommitSystemGroup`
   - `GASBoundaryProjectionSystemGroup`
3. `GEExecutionCalculationExtensionSystemGroup` 是 CoreSimulation 内扩展插槽。
4. `RuntimeSystemRegistration.gen.cs` 已把 7 个 generated systems 注册进主链，generated runtime 是当前执行事实的一部分。
5. `GASDefinitionCatalogBlob` 已被 generated runtime 读取；旧 managed config/prototype path 不能再代表 hot path。
6. `ToEntityArray` 主要留在 Debugger observation 和 AutoChess catalog 低频安装路径；`SystemAPI.Query<...>` 在 `Assets/GAS` 当前只剩 Cue managed boundary。当前热路径风险重点已收窄到 generated active mutation 的 singleton serial `IJob`、Buffer/ComponentLookup random access、singleton stream owner、剩余 global facade 使用面分类和容量/ordering 证据。
7. AutoChessDemo 当前已恢复为业务分层 demo，不是“删除后待重构”状态。
8. Luban/sourcegen 已不再强制依赖 Unity Editor UI 或 Unity batchmode：`Tools/CodeGen/Generate-GAS-SourceGen.bat` 与 `Tools/GasCodeGenCli` 可直接通过 dotnet 驱动 Luban JSON/C# export + GAS CodeGen；Unity batchmode 仅作为编译域、`BeanUpdater`、`AssetDatabase` 和 asmdef import 验证路径。
9. AutoChess 已从手写最小 catalog 切到通用 generated catalog：`AutoChessBattleDefinitionCatalogBuilder` 安装 `GASGeneratedDefinitionCatalogBuilder.BuildCatalog()`，最新 runner 验证 `completed=True`、`blockingDebugErrors=0`。

## 核心问题看板

| ID | 当前问题 | 状态 | 严重度 |
|---|---|---|---|
| ISSUE-001 | GE 生命周期从 request/entity pipeline 迁入 command/spec/active store，但 legacy fallback 和 generated proof 仍未闭合 | Active | P0 |
| ISSUE-002 | Observation 已进入 BoundaryProjection；gameplay event 已统一为 `GameplayEventBuffer` typed fact，Attribute/Cue/Tag 边界缓冲已由 BoundaryProjection 派生，Damage/EventBus helper 兼容写入口已删除，Presentation/Replay 只读 typed fact | Mitigated | P1 |
| ISSUE-003 | Debugger/Official diff 已有工具，但证据还需区分 Core 与 Boundary 成本 | Active | P1 |
| ISSUE-004 | StructuralCommit gate 已真实存在；boundary request entity 已退场，但 direct-EM 分类和证据闭环仍需收口 | Active | P0 |
| ISSUE-005 | Generated 链路已反哺 Runtime Core；ability commit、instant spec/reduce、active mutation job 化和 static hot path gate 已同步模板，剩余风险集中在 active mutation store 选型、random lookup 与证据闭环 | Mitigated | P1 |
| ISSUE-006 | AutoChessDemo 已移出 Runtime Core；当前风险转为 bridge 直接 `EntityManager` | Mitigated | P1 |
| ISSUE-007 | 文档口径仍需持续防止旧事实回流 | Active | P2 |
| ISSUE-008 | DOTS 官方机制已部分进入规则，但 contract 与 runtime proof 需继续拆分 | Active | P1 |
| ISSUE-009 | 5 段主链已存在；frame/query/stream owner 仍未完全目标态化 | Active | P1 |
| ISSUE-010 | 执行范式旧风险已收窄；ASC dirty/present、execution output applied 已收口到 owner chunk applicator；当前主要是 generated active mutation singleton serial job、singleton stream 与 boundary managed query 风险 | Active | P1 |
| ISSUE-011 | 临时 query 泛滥旧口径已缓解；当前 API 承载风险集中在 singleton owner、global facade 和 generated active mutation random lookup store | Active | P1 |

## 下一轮清理主线

本轮复查后，下一轮不再围绕“兼容旧链路”做小步迁移，而是按删除旧事实源的方式推进：

1. **已完成：删除 Damage/Attribute/Cue/Tag helper 兼容写入口**：`DamageEventBuffer`、`EventBusHelper.EnqueueDamageEvent`、`EventBusHelper.EnqueueAttributeChangeEvent`、`EventBusHelper.EnqueueCueRequest`、`EventBusHelper.EnqueueTagChangeEvent` 已退场；Damage 进入 `GameplayEventBuffer` typed fact，Attribute/Cue/Tag 只能由 `GameplayFactBoundaryProjectionSystem` 派生。
2. **已完成：收缩 Presentation/Replay 输入**：`PresentationOutboxProjectionSystem` 与 `ReplayLogSystem` 不再双读 Attribute/Cue/Tag/Damage 边界缓冲，只从 `GameplayEventBuffer` typed fact 投影。
3. **继续：generated active mutation 模板瘦身**：不手改 `.gen.cs`，只改 `GasGlueCodeGenPhases` 模板和 validation gate。目标是把 active mutation 从单 stream owner serial loop + random lookup，拆成 owner-grouped apply / deterministic merge / capacity proof。
4. **继续：global facade 分层**：`GASManager.EntityManager` 只允许 bootstrap、authoring/prototype、Boundary facade、Debugger 和 demo adapter 使用；Runtime Core hot path、runtime helper、config component 和 generated template 禁止通过全局 facade 写 ECS。当前 `AbilityRuntimeActions` / `AttributeHelper` 无参全局 overload、`GameplayEffectComponentConfig` / `AbilityComponentConfig` protected static facade 已删除。
5. **继续：结构变化证据闭环**：用 `GasRuntimeOfficialToolDiff`、Profiler/Debugger counters 和 AutoChess 规模门拆分 Core/Boundary/Demo/Observation 成本。

## 关键数据点

- 未注册旧 system 第一轮瘦身已删除：空的 `GEEffectCommandIngestSystem`、未注册的 `AttributeChangeEventProjectionSystem`、未进入主链的 `GASManagerInputSystem : SystemBase` 已退场。
- `state.Dependency.Complete()` 当前 `Assets/GAS/**/*.cs` 扫描为 0；第二轮已移除 destroy/finalize、ExecutionCalculation、OutputModifier、generated ActiveEffect pre-tick 等旧同步等待。
- `ASCCommandBufferResolveSystem` 已拆成先标记 destroying、再解析命令的两段 scheduled job；`ASCCommandPendingComponent`、`ASCDestroyingComponent`、`AttributeDirtyComponent` 的 ASC current-entity 开关已用 chunk `EnabledMask` 处理，避免同 job 内按 chunk 顺序判断 target 可用性。
- generated `AbilityCatalogCommitJob` 已从 `ComponentLookup<AbilityCommitRequestComponent>.SetComponentEnabled` 随机开关改为 `ComponentTypeHandle<AbilityCommitRequestComponent>` + `chunk.GetEnabledMask(ref ...)`；auto-end 写 `AbilityEndRequestComponent` 也已改为 current ability chunk `EnabledMask`，符合 `EN-03` / `CASE-20` / `PRF-22` 的批量 enableable 口径。
- generated `RuntimeEffectInstant.gen.cs` 当前输出 `using Unity.Burst`，`InstantSpecBuildJob` 与 `AttributeSetReduceApplyJob` 均为 `[BurstCompile] IJob`；`GasGlueCodeGenPhases` 模板也已同步，不能再把 instant spec/reduce 写作未 Burst 的主线程 proof。
- generated `GASActiveEffectMutationApplySystem` 已调度 `GASGeneratedActiveEffectRuntime.GEActiveEffectMutationApplyJob : IJob`；旧 public/static `TryApplyActiveMutation(EntityManager, ...)` 路径已退场。当前残留是单 stream owner 的 serial job、lookup random access 与 capacity/ordering 证据不足；generated/runtime/demo gameplay event 已统一写入 `GameplayEventBuffer` typed fact，旧 `GameplayEventBusEventBuffer` 类型和承载已删除。
- generated `AbilityCatalogCommitSystem` 已移除 per-frame `NativeList<GECommandSeedRecord>(Allocator.TempJob)` seed scratch；模板现在直接构造单条 `GECommandSeedRecord` 并追加 command。
- generated instant GE spec build 已支持 cue-only instant GE：`CanBuildInstantSpec` 允许 `ModifierCount > 0 || GameplayCueCode > 0`。
- `ASCDestroyingComponent` 当前按 enableable bit 判定销毁态；默认 disabled 的 ASC 不再因持有组件而被 generated spec/commit 路径误判为 destroying。
- generated active lifecycle 虽已 scheduled job 化，且 explicit remove 的 `GERemoveCommandPendingComponent` 关闭已改为 chunk `EnabledMask`；ability cancel/destroy-on-cleanup 已进一步收口到 frame-local `AbilityLifecycleRequestBuffer`，由 `AbilityLifecycleRequestSystem` 在 ability chunk 内统一应用。ASC dirty / active modifier present 也已收口到 frame-local `AttributeOwnerMarkerRequestBuffer`，由 `AttributeOwnerMarkerRequestSystem` 在 ASC chunk 内统一应用。
- `GEExecutionCalculationOutputModifierSystem` 的 output applied marker 已改为 effect-owned chunk applicator；ASC dirty 通过 `AttributeOwnerMarkerRequestBuffer` 归并，不再由 `AppliedLookup` / `AttributeDirtyLookup` 随机开 enableable。
- Core stream/job 前置计数已从 `CalculateChunkCount()` 改为 `CalculateChunkCountWithoutFiltering()`，避免 enableable/filter sync 并匹配 `IJobChunk` 的 unfiltered chunk index。
- `AbilityLifecycleRequestSystem`、`AbilityStateCleanupSystem` 和 `AttributeOwnerMarkerRequestSystem` 已把 ability / attribute owner marker 的 enableable 写入收口到 owner chunk `EnabledMask`；cleanup 仍通过 lookup 访问 owner/effect store、临时 tag 和 EventBus，不能写成完整 owner-local 归并终局。
- `GEEffectCommandSpecStreamFramePrepareSystem` 已从主线程 `EntityManager.GetBuffer` 清理/compact 改为 scheduled `IJob`，复用 `EffectCommandSpecStream.PrepareFrameLocalData(ref stream, buffers...)` 的 buffer-only 路径。
- `GameplayFactProjectionSystem` 已从主线程 `EntityManager.GetBuffer` + legacy EventBus writer 改为 scheduled `IJob`，并进一步瘦身为只写 `GameplayEventBuffer` typed fact；Attribute/Cue/Tag 边界缓冲派生已移到 `GASBoundaryProjectionSystemGroup` 内的 `GameplayFactBoundaryProjectionSystem`。旧 `GameplayFactEventBridgeSystem`、`GEInstantEffectCueRequestProjectionSystem`、Damage/EventBus helper 兼容写入口已直接退场。
- `GasCodeGenValidationReport.md` 已加入 generated runtime hot path 静态门禁；当前 `GeneratedHotPathRegressionHits: 0`，会扫描并报告 `Complete()`、`.Run()`、legacy EventBus writer、旧 active mutation helper、ability seed scratch、generated/template `GASManager.EntityManager` 等回流项。
- `Tools/CodeGen/Generate-GAS-SourceGen.bat` 是不启动 Unity 的快速生成驱动；`Tools/CodeGen/Generate-GAS-CodeGen.bat` 是 Unity batchmode 验证驱动，不需要打开 Editor UI。
- AutoChess command drive 与 execute calculation 已改为 scheduled job；x50 batchmode 验证通过，`debugErrors=0`、`blockingDebugErrors=0`。
- 最新 AutoChess runtime runner 验证：`completed=True, winner=Player, battleTicks=9, commands=700, attributeChanges=650, executionOutputs=250, cueRequests=700, debugErrors=0, blockingDebugErrors=0, coreRequests=2100, coreFacts=5300, coreDeltas=1300, coreCues=700`；该轮使用 generated catalog 安装路径。同轮诊断仍暴露 `syncQueryBudget=14`、`dependencyWaitRisks=5`、`requiredStructuralPlaybacks=5`、`recordedStructuralPlaybacks=0`，说明结构变化证据闭环仍未完成。
- `ToEntityArray()` 当前命中主要在 `GasRuntimeDebugger` 和 `AutoChessBattleDefinitionCatalogBuilder`。
- AutoChessDemo 当前 2 个 demo ECS systems 插入 current GAS groups：command drive 和 execute calculation extension。

## 事实审计快照

| 事实项 | 当前归类 | 证据路径 | 文档使用规则 |
|---|---|---|---|
| 5 段 GAS physical group | runtime-active | `GASSystemScheduleContract.cs`, `GASGroups.cs`, `GASManager.cs` | 可作为当前主链事实 |
| 8 个 logical phase contract | contract-only / mapping aid | `GASSystemScheduleContract.RuntimeCoreFramePhases` | 不能写成 8 个 physical group 已落地 |
| 7 个 generated runtime systems | runtime-active | `RuntimeSystemRegistration.gen.cs` | 必须纳入 Runtime 审查范围 |
| `GASDefinitionCatalogBlob` | runtime-active data source | `GASDefinitionCatalogRuntimeTypes.cs`, `DefinitionCatalog.gen.cs` | 可作为 generated runtime catalog 事实 |
| `GASGeneratedDefinitionBake*` 计划链 | contract-only | `Assets/GAS/Runtime/Definition` | 不能当作实际 runtime authoring/Baker 目标态完成证明 |
| GAS sourcegen CLI / bat 驱动 | tooling-active | `Tools/CodeGen`, `Tools/GasCodeGenCli` | 可证明生成链可脱离 Unity Editor UI；不能替代 Unity 编译域/AssetDatabase 验证 |
| `GasRuntimeOfficialToolDiff` | evidence tool | `GasRuntimeOfficialToolDiff.cs` | 工具存在不是结构变化已收口的证明 |
| AutoChess generated catalog install | app-boundary / initialization | `AutoChessBattleDefinitionCatalogBuilder.cs` | 已消费通用 SourceGenerator catalog；不能替代 Baker、unit/scenario/scale/validation 配置链 |
| Debugger / Replay / Presentation | boundary / observation | `GasRuntimeDebugger.cs`, `ReplayLogSystem.cs`, `PresentationOutboxProjectionSystem.cs` | 性能数据必须与 CoreSimulation 拆开 |

## 当前事实红线

1. 文件名或旧 issue 标题保留历史问题名时，正文必须明确当前状态；不能让标题反向覆盖现实代码。
2. `Contract`、`Plan`、`Spec`、`Gate`、`Tool` 只能说明约束或检测能力，不能单独证明 runtime 完成。
3. `Generated` 代码只要被注册进主链，就按 Runtime Core 规则审查。
4. AutoChessDemo 的 demo-only bridge、catalog、log scene 不能当作通用 Runtime Core 实现。
5. Observation / Debugger / Official diff 的同步成本不能混入 CoreSimulation 热路径结论。

## 官方规则校验口径

本目录所有 P0/P1 判断必须能追溯到 Unity Entities 官方文档或本仓库 `方案讨论/UnityDOTS官方文档参考/主题` 的规则编号；规则细节以该目录为准。本轮复核采用当前项目本地包文档 `Library/PackageCache/com.unity.entities@e90944159b94/Documentation~`，避免用网页最新版覆盖当前编译版本。

本轮直接对照的官方文档：

| 官方文档 | 本轮采用结论 |
|---|---|
| `components-enableable-use.md` | 高频 enableable 写入优先用 `EnabledRefRW` 或 chunk `EnabledMask`；random `ComponentLookup.SetComponentEnabled` 可用但有随机访问开销和竞态风险，性能优先时应转 owner iteration |
| `structural-changes-enableable-components.md` | enableable 不产生结构变化，但 disabled 组件仍被 `HasComponent` 视为存在；因此 `ASCDestroyingComponent` 等 enableable 状态必须读 enabled bit，不能只用 `HasComponent` |
| `iterating-data-ijobchunk.md` | `IJobChunk` 会按 query 选 chunk；处理 enableable 时必须尊重 `useEnabledMask/chunkEnabledMask`，当前文档用 `ChunkEntityEnumerator` 或 chunk `EnabledMask` 作为合规证据 |
| `systems-entityquery-create.md` | query 的 enabled/disabled 匹配语义要明确；长期 stored query 归属 `SystemState`，当前 Runtime Core 用 `state.GetEntityQuery(EntityQueryDesc)` 作为事实口径 |
| `components-buffer-jobs.md` | `BufferLookup` 是 job 内随机访问 DynamicBuffer 的工具，不等同于 scale-ready store；大量 target random access 需要 owner-local store、target-grouped merge 或容量/ordering 证据 |
| `systems-entity-command-buffer-use.md` | job 内结构变化必须记录到 ECB，集中 playback 可以减少 sync point；当前 StructuralCommit gate 存在但仍需 Journaling/Profiler 证明来源和相位 |

当前审查采用以下约束：

| 领域 | 采用规则 | 对当前事实的约束 |
|---|---|---|
| World/SystemGroup | `SYS-01`、`SYS-02`、`SYS-03`、`SYS-04`、`SYS-05` | Runtime Core 必须由 ECS System/Job 数据流承载；FixedStep 下少量 physical group 是事实口径；Demo/Debugger/Presentation 只能通过 Boundary 观察 Core |
| Query/Job | `QRY-01`、`QRY-02`、`JOB-01`、`PRF-05`、`CASE-01/02/03` | `SystemAPI.Query` 可用于 proof/debug/small scale，但 hot path 默认应迁到 `IJobEntity` / `IJobChunk` |
| 结构变化 | `SC-01`、`SC-02`、`SC-03`、`ECB-03`、`PRF-02`、`PRF-04`、`CASE-05` | hot path 不直接 `EntityManager.CreateEntity/DestroyEntity`；结构变化要集中在明确 ECB playback phase，并用 Journaling/Profiler 证明 |
| Enableable | `EN-03`、`CASE-20`、`PRF-22` | 高频 enableable 切换优先 `EnabledRefRW` / chunk `EnabledMask`；`IJobChunk` 必须处理 `useEnabledMask/chunkEnabledMask` 或明确断言 |
| Burst/AOT | `BUR-01` | Runtime Core hot path system/job 必须 `[BurstCompile]` 且无托管依赖；generated output 与 codegen 模板同等受审 |
| Buffer/Store | `BUF-01`、`BUF-02`、`BUF-03`、`STORE-03`、`SEL-01`、`SEL-02`、`NAT-03` | singleton DynamicBuffer 只能作为 proof/低量 carrier；fan-in、delta、fact 需按数据性质重新选型 |
| Baking/Blob | `BAKE-01..03`、`BLOB-01/02`、`CASE-07`、`CASE-39/40` | `Baker<T>` 必须是实际 baking 产物且无状态；contract/template 不能证明 Baker 已落地；runtime hot path 应消费 Blob/generated lookup |
| Diagnostics | `DBG-01..05`、`SYS-04` | Debugger/Journaling/Profiler 互补，不能互相替代；性能报告必须拆 Core/Boundary/Demo/Observation |

## 当前总诊断

当前架构已经从旧 lifecycle/request entity 堆叠推进到“5 段物理主链 + generated catalog runtime + command/spec/delta/fact proof + typed fact observation”的迁移期。方向有实质进展，但不能宣称架构已优秀。

本轮结合当前项目本地 Unity Entities 文档复核后的判断是：旧的主线程 helper、`Complete()`、大量 `SystemAPI.Query`、marker random enableable 和 legacy gameplay event bus 风险已经明显收窄；真正需要继续大步重构的是数据承载和证据体系。官方文档允许 `BufferLookup` / `ComponentLookup` / random enableable 作为工具，但没有把它们等价为高规模 store 终局。当前 generated active mutation、singleton stream owner、剩余边界缓冲和 structural evidence gap 仍是架构深度不足的集中暴露点。

当下最需要治理的是：

1. ASC command resolve、generated ability commit、generated normalize/spec-build/reduce、active mutation/pre-tick/remove、ability lifecycle/cleanup 和 fact projection 已进入 scheduled job 形态；current-entity enableable 清理已大幅转向 chunk `EnabledMask`。
2. 剩余最重风险集中在 generated `GASActiveEffectMutationApplySystem` 的 singleton serial job、Buffer/ComponentLookup random access、serial owner-local store 写入和 capacity/ordering 证据不足；runtime/generated/demo gameplay event 写入已切到 typed fact，legacy gameplay EventBus buffer、Damage 边界缓冲和 EventBus gameplay enqueue helper 已删除；ASC dirty/present 与 execution output applied 已按 owner chunk applicator 收口，后续不应继续作为未修事实记录。
3. singleton DynamicBuffer stream 是 proof carrier，不是 scale-ready 终局。
4. StructuralCommit gate 需要 Journaling/Profiler 证明来源和相位。
5. AutoChess bridge 需要把直接 `EntityManager` 操作从业务 adapter 中继续收口。
6. Boundary request entity 链路已退场，但 owner-local command buffer / pending marker 必须作为唯一入口防回流。

后续审查的红线也相应调整：

1. 不再把“是否有 generated code”当风险，风险来自 generated system 已注册进主链后是否满足 query/job/dependency/store/ordering 规则。
2. 不再把已退场的 `AttributeDirtyLookup` / `ActiveModifierPresentLookup` / `AppliedLookup` 写成当前缺陷；只保留 static validation 防回流。
3. 不把 `BufferLookup` / `ComponentLookup` 的 job 化迁移误写成最终优化完成；需要继续证明 owner-local store、target-grouped merge、capacity 或 scale profile。
4. 不手改 `.gen.cs` 修复架构问题；修复必须落回 `GasGlueCodeGenPhases`、codegen manifest/report、离线 sourcegen bat/CLI 或 Unity batchmode 生成链路。

## 边界

1. 只写已对照当前代码成立的事实。
2. 不写目标态设想，目标态见 [01-目标态架构共识](../01-目标态架构共识/README.md)。
3. 不写任务状态，任务状态见 [02-主线任务树](../02-主线任务树/README.md)。
4. Contract、Plan、Spec 不是完成证明；完成度必须来自当前执行链和证据工具。
