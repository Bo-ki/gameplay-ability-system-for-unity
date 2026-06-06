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
| [Definition配置事实](Definition配置事实.md) | runtime catalog blob、managed table、generated glue、baking contract 当前状态 |
| [P0-致命缺陷](P0-致命缺陷.md) | 当前最高风险：`Complete()` 防回流、generated active mutation 残留、singleton stream、结构变化证据闭环 |
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
6. `ToEntityArray` 主要留在 Debugger observation 和 AutoChess catalog 低频安装路径；`SystemAPI.Query<...>` 在 `Assets/GAS` 当前只剩 Cue managed boundary。当前热路径风险重点已收窄到 generated active mutation 的 direct `EntityManager` helper、DynamicBuffer serial store、legacy EventBus writer 和 singleton stream owner。
7. AutoChessDemo 当前已恢复为业务分层 demo，不是“删除后待重构”状态。

## 核心问题看板

| ID | 当前问题 | 状态 | 严重度 |
|---|---|---|---|
| ISSUE-001 | GE 生命周期从 request/entity pipeline 迁入 command/spec/active store，但 legacy fallback 和 generated proof 仍未闭合 | Active | P0 |
| ISSUE-002 | Observation 已进入 BoundaryProjection，但 legacy EventBus 与 typed fact 仍并存 | Active | P1 |
| ISSUE-003 | Debugger/Official diff 已有工具，但证据还需区分 Core 与 Boundary 成本 | Active | P1 |
| ISSUE-004 | StructuralCommit gate 已真实存在；boundary request entity 已退场，但 direct-EM 分类和证据闭环仍需收口 | Active | P0 |
| ISSUE-005 | Generated 链路已反哺 Runtime Core；ability commit 与 instant spec/reduce 的 DOTS 修复已同步模板，剩余风险集中在 active mutation 与防回流 | Mitigated | P1 |
| ISSUE-006 | AutoChessDemo 已移出 Runtime Core；当前风险转为 bridge 直接 `EntityManager` | Mitigated | P1 |
| ISSUE-007 | 文档口径仍需持续防止旧事实回流 | Active | P2 |
| ISSUE-008 | DOTS 官方机制已部分进入规则，但 contract 与 runtime proof 需继续拆分 | Active | P1 |
| ISSUE-009 | 5 段主链已存在；frame/query/stream owner 仍未完全目标态化 | Active | P1 |
| ISSUE-010 | 执行范式旧风险已收窄；当前主要是 generated active mutation direct-EM、singleton stream 与 boundary managed query 风险 | Active | P1 |
| ISSUE-011 | 临时 query 泛滥旧口径已缓解；当前 API 承载风险集中在 singleton owner 和 global facade | Active | P1 |

## 关键数据点

- `Assets/GAS/Runtime` + `Assets/GAS/Generated/CodeGen/Runtime` 当前共检出 33 个 `ISystem` 类型。
- Runtime 中仍有 `GASManagerInputSystem : SystemBase`，但未进入 5 段 GAS 主链注册。
- `state.Dependency.Complete()` 当前 `Assets/GAS/**/*.cs` 扫描为 0；第二轮已移除 destroy/finalize、ExecutionCalculation、OutputModifier、generated ActiveEffect pre-tick 等旧同步等待。
- generated `AbilityCatalogCommitJob` 已从 `ComponentLookup<AbilityCommitRequestComponent>.SetComponentEnabled` 随机开关改为 `ComponentTypeHandle<AbilityCommitRequestComponent>` + `chunk.GetEnabledMask(ref ...)`，符合 `EN-03` / `CASE-20` / `PRF-22` 的批量 enableable 口径。
- generated `RuntimeEffectInstant.gen.cs` 当前输出 `using Unity.Burst`，`InstantSpecBuildJob` 与 `AttributeSetReduceApplyJob` 均为 `[BurstCompile] IJob`；`GasGlueCodeGenPhases` 模板也已同步，不能再把 instant spec/reduce 写作未 Burst 的主线程 proof。
- generated instant GE spec build 已支持 cue-only instant GE：`CanBuildInstantSpec` 允许 `ModifierCount > 0 || GameplayCueCode > 0`。
- `ASCDestroyingComponent` 当前按 enableable bit 判定销毁态；默认 disabled 的 ASC 不再因持有组件而被 generated spec/commit 路径误判为 destroying。
- generated active lifecycle 虽已 scheduled job 化，但仍有 `RemovePendingLookup`、`AbilityCancelRequestLookup`、`AbilityDestroyOnCleanupLookup`、`ActiveModifierPresentLookup`、`AttributeDirtyLookup` 等 `ComponentLookup.SetComponentEnabled(...)` 随机 enableable 开关；这是新的 `EN-03` / `CASE-20` P1 残留，不等同于已修复的 ability commit request 关闭。
- Core stream/job 前置计数已从 `CalculateChunkCount()` 改为 `CalculateChunkCountWithoutFiltering()`，避免 enableable/filter sync 并匹配 `IJobChunk` 的 unfiltered chunk index。
- `AbilityStateCleanupSystem` 已从主线程 `SystemAPI.Query` + `EntityManager` cleanup 改为 scheduled `IJobChunk` cleanup。
- `GEEffectCommandSpecStreamFramePrepareSystem` 已从主线程 `EntityManager.GetBuffer` 清理/compact 改为 scheduled `IJob`，复用 `EffectCommandSpecStream.PrepareFrameLocalData(ref stream, buffers...)` 的 buffer-only 路径。
- `GameplayFactProjectionSystem` 已从主线程 `EntityManager.GetBuffer` + `EventBusHelper.GameplayEventBusWriter` 改为 scheduled `IJob`；旧 `GameplayFactEventBridgeSystem`、`GEInstantEffectCueRequestProjectionSystem` 已直接退场。
- AutoChess command drive 与 execute calculation 已改为 scheduled job；x50 batchmode 验证通过，`debugErrors=0`、`blockingDebugErrors=0`。
- 最新 AutoChess runtime runner 验证：`completed=True, winner=Player, battleTicks=9, commands=700, attributeChanges=650, executionOutputs=250, cueRequests=700, debugErrors=0, blockingDebugErrors=0, coreRequests=2100, coreFacts=5300, coreDeltas=1300, coreCues=700`；同轮诊断仍暴露 `syncQueryBudget=14`、`dependencyWaitRisks=5`、`requiredStructuralPlaybacks=5`、`recordedStructuralPlaybacks=0`，说明结构变化证据闭环仍未完成。
- `ToEntityArray()` 当前命中主要在 `GasRuntimeDebugger` 和 `AutoChessBattleDefinitionCatalogBuilder`。
- AutoChessDemo 当前 2 个 demo ECS systems 插入 current GAS groups：command drive 和 execute calculation extension。

## 事实审计快照

| 事实项 | 当前归类 | 证据路径 | 文档使用规则 |
|---|---|---|---|
| 5 段 GAS physical group | runtime-active | `GASSystemScheduleContract.cs`, `GASGroups.cs`, `GASManager.cs` | 可作为当前主链事实 |
| 8 个 logical phase contract | contract-only / mapping aid | `GASSystemScheduleContract.RuntimeCoreFramePhases` | 不能写成 8 个 physical group 已落地 |
| 7 个 generated runtime systems | runtime-active | `RuntimeSystemRegistration.gen.cs` | 必须纳入 Runtime 审查范围 |
| `GASDefinitionCatalogBlob` | runtime-active data source | `GASDefinitionCatalogRuntimeTypes.cs`, `DefinitionCatalog.gen.cs` | 可作为 generated runtime catalog 事实 |
| `GASGeneratedDefinitionBake*` 计划链 | contract-only | `Assets/GAS/Runtime/Definition` | 不能当作实际 `Baker<T>` / baking 完成证明 |
| `GasRuntimeOfficialToolDiff` | evidence tool | `GasRuntimeOfficialToolDiff.cs` | 工具存在不是结构变化已收口的证明 |
| AutoChess hand-written catalog | demo-only / initialization | `AutoChessBattleDefinitionCatalogBuilder.cs` | 不能替代通用 SourceGenerator/Baker 链 |
| Debugger / Replay / Presentation | boundary / observation | `GasRuntimeDebugger.cs`, `ReplayLogSystem.cs`, `PresentationOutboxProjectionSystem.cs` | 性能数据必须与 CoreSimulation 拆开 |

## 当前事实红线

1. 文件名或旧 issue 标题保留历史问题名时，正文必须明确当前状态；不能让标题反向覆盖现实代码。
2. `Contract`、`Plan`、`Spec`、`Gate`、`Tool` 只能说明约束或检测能力，不能单独证明 runtime 完成。
3. `Generated` 代码只要被注册进主链，就按 Runtime Core 规则审查。
4. AutoChessDemo 的 demo-only bridge、catalog、log scene 不能当作通用 Runtime Core 实现。
5. Observation / Debugger / Official diff 的同步成本不能混入 CoreSimulation 热路径结论。

## 官方规则校验口径

本目录所有 P0/P1 判断必须能追溯到 `方案讨论/UnityDOTS官方文档参考/主题` 的规则编号；规则细节以该目录为准。当前审查采用以下约束：

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

当前架构已经从旧 lifecycle/request entity 堆叠推进到“5 段物理主链 + generated catalog runtime + command/spec/delta/fact proof + typed fact / legacy bridge 并存”的迁移期。方向有实质进展，但不能宣称架构已优秀。

当下最需要治理的是：

1. ASC command resolve、generated ability commit、generated normalize/spec-build/reduce、active pre-tick/remove 和 fact projection 已进入 scheduled job 形态；剩余最重风险集中在 generated `GASActiveEffectMutationApplySystem` 的 `EntityManager` helper、serial owner-local store 写入和 legacy EventBus writer。
2. generated active lifecycle 中仍有 random `SetComponentEnabled` P1 残留，后续应按 owner-local/chunk-local enableable mask 收口。
3. singleton DynamicBuffer stream 是 proof carrier，不是 scale-ready 终局。
4. StructuralCommit gate 需要 Journaling/Profiler 证明来源和相位。
5. AutoChess bridge 需要把直接 `EntityManager` 操作从业务 adapter 中继续收口。
6. Boundary request entity 链路已退场，但 owner-local command buffer / pending marker 必须作为唯一入口防回流。

## 边界

1. 只写已对照当前代码成立的事实。
2. 不写目标态设想，目标态见 [01-目标态架构共识](../01-目标态架构共识/README.md)。
3. 不写任务状态，任务状态见 [02-主线任务树](../02-主线任务树/README.md)。
4. Contract、Plan、Spec 不是完成证明；完成度必须来自当前执行链和证据工具。
