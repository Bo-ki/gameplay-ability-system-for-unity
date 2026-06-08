# 00 当前架构事实

> 上次更新：2026-06-08 | 审查范围：`Assets/GAS/Runtime` + `Assets/GAS/Generated/CodeGen/Runtime` + `Assets/AutoChessDemo`

本目录维护当前版本的架构事实、核心问题诊断和合规审查。当前事实以现实代码为第一性参考；旧路线文档、旧目标态文档和旧 issue 结论只能作为历史背景。

## 00 / 01 目录边界

`00-当前架构事实/` 是当前事实 owner，回答“现在真实代码是什么、证据在哪里、按官方 DOTS 规则判定有什么问题”。这里可以记录缺陷、`MigrationProofOnly` 实现证据、违约证据、历史计划与现实偏差，也可以引用目标态 Spec 作为判定标准；但不能把理想架构方案写成新的框架设计 Spec。

`01-目标态架构共识/` 是目标态 Spec owner，回答“理想的 GAS 架构应该如何设计、为什么更优秀、为什么必须这样做、如何验收”。那里不记录当前生成了哪些文件、哪些 gate 已通过、当前 P0/P1 命中、下一轮计划或复审流水。

归位规则：

1. 当前代码事实、缺陷诊断、generated artifact 清单、`MigrationProofOnly` 实现证据、validation report 命中数写入本目录。
2. 目标态分层、数据流、禁止方向、API 选型和验收门槛写入 `../01-目标态架构共识/`。
3. 可执行任务、下一轮目标、拆分切片写入 `../02-主线任务树/` 或当前进度目录；本目录只保留这些任务背后的事实约束。
4. 文件标题或正文出现 `计划`、`复审`、`当前状态`、`事实`、`落点`、`进度` 时，默认归属本目录或任务/进度目录，不归属 `01`。
5. 已从 `01` 清理出的 CodeGen / Luban-SourceGenerator 复审内容，当前事实归入 [CodeGen链路复审事实](CodeGen链路复审事实.md) 和 [SourceGenerator链路复审事实](SourceGenerator链路复审事实.md)；纯目标态设计归入 `../01-目标态架构共识/14-DefinitionCodeGen目标链路Spec.md` 与 `../01-目标态架构共识/15-SourceGenerator职责边界Spec.md`。

## 文件索引

| 文件 | 内容 |
|---|---|
| [Runtime主链事实](Runtime主链事实.md) | 当前 5 段 GAS 主链、generated catalog / pure glue 接入、command/spec/delta/fact 链 |
| [架构重划分审查事实](架构重划分审查事实.md) | 本轮纯 ECS Core / OOP Shell / Thin Adapter / Debugger / Luban SourceGenerator 重划分事实索引；正文已拆入同名子目录 |
| [架构重划分审查事实子页](架构重划分审查事实/README.md) | 架构总览、API 健康、R1-R6 证据、Owner 重划分、Boundary Snapshot / Magnitude Source、ActiveEffectSlot snapshot、端到端消息流、TagRequirement query 和文档矛盾 / 过时口径事实子页 |
| [当前架构图](当前架构图.md) | 当前实际链路图、EffectCommand 链路、DOTS 对照热图 |
| [模块索引](模块索引.md) | Runtime / Generated Runtime / AutoChessDemo / 配置生成当前索引 |
| [AutoChessDemo事实](AutoChessDemo事实.md) | 当前业务 demo 分层、GAS bridge、demo ECS 扩展和风险 |
| [Definition配置事实](Definition配置事实.md) | runtime catalog blob、managed table、generated glue、sourcegen/batchmode 驱动、baking contract 当前状态 |
| [Authoring编辑链事实](Authoring编辑链事实.md) | 当前 GAS Center / Excel / Luban / CodeGen 编辑链路、业务编辑摩擦和不能推出的结论 |
| [CodeGen链路复审事实](CodeGen链路复审事实.md) | CodeGen 到 Runtime 的当前链路事实、历史计划与现实偏差、违约点和后续验收约束 |
| [SourceGenerator链路复审事实](SourceGenerator链路复审事实.md) | Luban / SourceGenerator 当前链路事实、生成器越权证据、静态门禁缺口和目标态内容归位关系 |
| [架构瘦身事实约束](架构瘦身事实约束.md) | GAS 架构瘦身后续任务背后的当前事实、官方规则约束和禁止回流口径；当前默认执行入口见 `../02-主线任务树/GAS架构重划分主线任务.md` 的 R2/R3/R4/R6/R7，旧瘦身任务文件仅作兼容和补充切片 |
| [P0-致命缺陷](P0-致命缺陷.md) | 当前最高风险：`Complete()` 防回流、generated active mutation serial job/store 残留、singleton stream、结构变化证据闭环 |
| [P1-高风险缺陷](P1-高风险缺陷.md) | 物理/逻辑 phase 并存、EventBus 迁移、managed registry、AutoChess bridge |
| [P2-改进建议](P2-改进建议.md) | 文档口径、证据拆分、旧文件名清理、contract/proof 标注 |
| ISSUE-001~014 | 当前核心问题按现实代码重审后的单项诊断；`ISSUE-014` 记录 headless 纯逻辑预算超标 |

## 当前核心事实

1. Runtime 主链已经不是旧 `GASEffectGroup / GASAbilityGroup / GASCueGroup / GasStructuralPlaybackSystemGroup`。
2. 当前物理主链是：
   - `GASFramePrepareSystemGroup`
   - `GASCommandResolveSystemGroup`
   - `GASCoreSimulationSystemGroup`
   - `GASStructuralCommitSystemGroup`
   - `GASBoundaryProjectionSystemGroup`
3. `GEExecutionCalculationExtensionSystemGroup` 是 CoreSimulation 内扩展插槽。
4. generated runtime 不再通过 `RuntimeSystemRegistration.gen.cs` 生成注册 helper；当前 `GASSystemScheduleContract` 的 `GeneratedCommandResolveSystemTypeNames` 与 `GeneratedCoreSimulationSystemTypeNames` 均为空数组，active-effect pre-tick / remove / normalize / mutation apply、instant spec build、attribute reduce/apply 等主链系统改由手写 Runtime 类型直接注册。缺失 generated type 的 fail-fast 仍是防回流机制，但不能再把 generated type-name registry 写成当前主调度来源。
5. `GASDefinitionCatalogBlob` 已被 hand-written Runtime consumers 和 generated pure glue 读取；旧 managed config/prototype path 不能再代表 hot path。
6. `EntityManager.CreateEntityQuery` 当前 Runtime 可执行命中 0 处；`ToEntityArray` 当前运行命中 3 处：Debugger observation 两处、Cue managed boundary 一处。`GasRuntimeDebugger` singleton lookup 已改为 registered/cache owner，`GASRuntimeFrameContext` current-frame lookup 与 `ActiveEffectStore` global index owner 也均已改为 registered/cache owner，cache miss 不再创建 fallback query。Debugger observation materialization 已进入 `ObservationMaterialization` event、`runtimeObservationMaterialization` snapshot/export 和 AutoChess `observationMaterializedQueries` / `performancePassObservationPollutionRisks` evidence 字段；这证明成本可归因，不证明它属于 CoreSimulation hot path。`GEEffectCommandSpecStream` 的隐式 singleton writer/append helper已删除，Runtime helper 改为显式解析 stream owner 后写入。`GEExecutionCalculationSystem` 的 execution output typed fact 已从 `EndGASStructuralCommitECBSystem` / `AppendToBuffer` 迁到 `NativeStream` 收集 + `GEExecutionCalculationFactMergeJob` 稳定 merge，并在 merge 阶段分配 `NextFactSequence`。pending AttributeDelta 产生的 Attribute fact 已新增 ASC owner-local fact lane：chunk-local apply 先写 `OwnerLocalGameplayFactBuffer`，再由 owner-local fact flush 按 owner / sequence / local index 排序导出现有 stream export。active mutation command 与对应 set-by-caller payload 已由 normalize 阶段投影到 ASC owner-local buffer，collect 阶段展平成 frame-local command / set-by-caller list，apply 阶段不再把 singleton stream set-by-caller 当 active mutation 输入。`DiagnosticsSnapshotSystem` 现在会通过 `GasRuntimeDebugger.RecordEffectCommandSpecStreamPressure(...)` 采集 `GEEffectCommandBuffer` / set-by-caller / spec / active mutation / attribute delta / typed fact 的 proof-only stream carrier pressure，并由 AutoChess validation 输出 `streamCarrierPressureWarnings` / peak / capacity；Debugger 也能看到 owner-local fact count / owner group / max owner range / flush count。当前热路径风险重点已收窄到剩余 singleton stream carrier、generated delta record carrier、`GASRuntimeShell` internal capability 面、Debugger observation 成本隔离和容量/ordering 证据；active mutation apply 与 pending AttributeDelta owner-local apply 均已进入 ASC chunk-local IJobChunk，旧 random lookup 估算为 0。
7. AutoChessDemo 当前已恢复为业务分层 demo，不是“删除后待重构”状态。
8. Luban/sourcegen 已不再强制依赖 Unity Editor UI 或 Unity batchmode：`Tools/CodeGen/Generate-GAS-SourceGen.bat` 与 `Tools/GasCodeGenCli` 可直接通过 dotnet 驱动 Luban JSON/C# export + GAS CodeGen；Unity batchmode 仅作为编译域、`BeanUpdater`、`AssetDatabase` 和 asmdef import 验证路径。
9. AutoChess 已从手写最小 catalog 切到通用 generated catalog：`AutoChessBattleDefinitionCatalogBuilder` 安装 `GASGeneratedDefinitionCatalogBuilder.BuildCatalog()`；2026-06-07 Run3 归档验证显示 `completed=True`、`blockingDebugErrors=0`，证据见 `_归档/2026-06-07-AutoChessBattleValidation-ActiveMutationChunkApply-Run3.log`。
10. `GASRuntimeShell` 当前仍是多能力 ECS 句柄口：同一 facade 暴露 World、`EntityManager`、command port、read model、job drain 和 runtime singleton；消费者包括 runtime binding、AutoChess adapter 和 Editor watcher。端到端消息流事实见 [07-端到端重划分事实](架构重划分审查事实/07-端到端重划分事实.md)。该事实归入 R1/R6 收权，不应写成目标态 Shell 设计。
11. `EffectRuntimeUtility` 当前是迁移期静态 helper，不是成熟 Runtime lane owner：`TryGetStaticDefinitionBlob`、`ApplyInstantEffect`、`ApplyInactiveDurationEffect`、`HasOngoingRequirements`、`TryMergeStackingApplication`、`HandleDurationExpired`、`ActivateDurationEffect`、`DeactivateOngoingEffect`、`ReactivateOngoingEffect`、`ShouldReject` 和无 ECB 版本 `FinalizeEffectDestroy` 仍为空/固定返回；已实现部分集中在 removal / cleanup、active modifier/tag 移除、typed removed event 写入和 store cleanup。这说明 active effect lifecycle 仍需回到明确 owner system / job / store，而不能把 helper 当目标态接口。
12. 2026-06-08 最新 codedb live 截面为 434 files / 434 outlines / 766 chunks / graph 448 nodes / 1831 edges / 34 communities / scan ready；Runtime path-prefix module-map 主社群为 146 files / 2227 indexed symbols，dependency edges 为 internal 604、boundary 133、incoming 101、outgoing 32。`RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs` 与 `RuntimeActiveEffect.gen.cs` 当前都已退为 12 行级 `RuntimePureGlue` marker；真实 instant spec build / attribute reduce owner 在手写 `Assets/GAS/Runtime/System/Effect/GEEffectInstantSystems.cs`，active-effect helper/job/snapshot/mutation/tick/remove 聚合到手写 `Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs`（3389 行）并由 `Assets/GAS/Runtime/System/Effect/GEActiveEffectLifecycleSystems.cs`（372 行）中的 Runtime systems 调度；`Assets/GAS/Runtime/System/Effect/GEActiveEffectCommandNormalizeSystem.cs` 作为 149 行手写 active mutation command normalize owner 进入 Runtime 主社群。`GASRuntimeShell.cs` 直接 imported_by 仍为 AutoChess wrapper、Editor watcher 和 Runtime binding，`GASSystemScheduleContract.cs` 直接 imported_by 仍覆盖 GASManager、AutoChess bootstrap、FrameBudget / StreamOwner / EvidenceGate / Rebind contract，说明 Shell capability 和 schedule evidence owner 仍需继续拆分。`ActiveEffectLifecycleOwnerSystems.cs` 当前磁盘缺失，不再是 generated runtime 物理 asmdef companion；它只作为防回流扫描项保留。
13. 2026-06-08 Shell / Catalog / Runner capability 续审：`GASRuntimeShell` 注释称 Thin OOP shell，但当前实现仍直接分发 runtime `World`、`EntityManager`、command port、live read model、presentation bind、runtime singleton 和 `CompleteAllTrackedJobs()`；`AutoChessGasRuntimeAccess` 只是将 AutoChess direct import 收口到 wrapper，仍代理 session world、definition / lifecycle `EntityManager`、diagnostics singleton、command port 和 runner job drain；`AutoChessGasCatalogSession` 已把 catalog install / uninstall 从 Host 拆出，但仍依赖 wrapper 返回 `EntityManager`；`AutoChessGasRuntimeTicker` 已记录 dependency drain ticks，但 drain 仍通过 Shell facade 完成。该事实已写入 `架构重划分审查事实/04-整体审查与Owner重划分事实.md`，后续由 R1/R6 消费。
14. `OwnerLocalGameplayFactBuffer` 已纳入 ASC archetype、初始化容量和完整性检查；当前写入面已覆盖 pending AttributeDelta chunk-local apply、ASC command resolve、hand-written instant reduce 和 execution output modifier apply 产生的 Attribute / command fact；`GameplayOwnerLocalFactFramePrepareSystem` 在 FramePrepare 清理 ASC 本地 fact，`GameplayOwnerLocalFactFlushSystem` 在 CoreSimulation 收集后 flush 到现有 `GameplayEventBuffer` stream export，`GameplayFactBoundaryProjectionSystem` 仍从 stream fact 派生 Attribute/Cue/Tag 边界缓冲。该切片是 owner-local fact 的正向局部实现，但不能推出 command/spec/delta/fact 全链路已退出 singleton stream；active effect hand-written owner、capacity / spill、scale evidence 和 R5 gate 仍需继续审查。
15. 2026-06-08 owner-local instant spec 续审：`GEEffectSpecBuffer` 已从 EffectCommand stream owner 的 required singleton buffers 中退出；手写 `GEEffectInstantSystems.cs` 会按 owner 写入 `GEEffectSpecBuffer` / `GESetByCallerValueBuffer`，并用 `OwnerLocalSpecCount` 作为 stream 侧计数；手写 Attribute reduce/apply 已改为 owner chunk `IJobChunk` 消费 owner-local spec、set-by-caller、attribute 和 fact buffer。该切片是 command/spec/reduce 链路的正向 owner-local 进展，但不能写成 Pure ECS Core 完成或 SourceGenerator 目标态完成；active-effect hand-written owner、capacity / spill、SourceAttribute snapshot 非零样本和 release-ready gate 仍要继续验收。
16. 2026-06-08 CodeGen / SourceGenerator artifact responsibility 续审：CodeGen 已具备统一 pipeline、manifest `ArtifactCategory`、runtime pure glue、generated catalog、validation boundary gate 和 Demo standalone phase 分离等正向资产；`GasGlueCodeGenPhases.cs` 当前为 5556 行级大聚合。当前 `GasCodeGenValidationReport.md` 显示 `GeneratedRuntimeBoundaryHits=0`、`GeneratedRuntimePureGlueArtifacts=4`、`GeneratedRuntimeLifecycleMigrationArtifacts=0`、`GeneratedRuntimeLifecycleHits=0`、`GeneratedRuntimeStructuralChangeHits=0`、`GeneratedRuntimeOwnershipHits=0`、`GeneratedRuntimeRandomWriteLookupHits=0`、`GeneratedRuntimeManagedConfigHits=0`、`GeneratedRuntimeUnclassifiedBoundaryHits=0`；manifest 中 `RuntimeDefinitionGlue.gen.cs`、`RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs` 与 `RuntimeActiveEffect.gen.cs` 均为 `RuntimePureGlue`。因此 R5 当前重点不是继续追 lifecycle migration artifact 或 companion 删除，也不是追当前 report hit，而是对账 manifest、validation report、marker 文件、generated pure glue 与手写 Runtime owner，并为 catalog lifetime / dispose owner、system budget、release-ready gate、缺失 artifact / type mismatch 负例和 stale generated lifecycle 防回流补证。详见 [CodeGen链路复审事实](CodeGen链路复审事实.md)、[SourceGenerator链路复审事实](SourceGenerator链路复审事实.md)、[整体审查与 Owner 重划分事实](架构重划分审查事实/04-整体审查与Owner重划分事实.md) 和 [整体架构重审事实](架构重划分审查事实/10-整体架构重审事实.md)。
17. 本轮整体架构重审后，当前可保留的现实 owner 是 5 段 physical backbone、registered/cache singleton owner、owner chunk applicator、owner-local instant spec / fact 正向切片、generated immutable catalog / pure glue、structured evidence 和 AutoChess 业务验收 Shell；当前必须继续退出或隔离的是多 capability `GASRuntimeShell` facade、剩余 singleton stream carrier、stale generated lifecycle 回流风险、Debugger / Cue materialization 混入 Core 性能口径、以及 AutoChess runtime access wrapper 共享 ECS handle。该重划分事实详见 [04-整体审查与Owner重划分事实](架构重划分审查事实/04-整体审查与Owner重划分事实.md)。
18. 2026-06-08 性能口径已从“x50 headless 功能通过”收紧为“headless pure logic strict budget”。上一轮 x50 / 200 units / measuredTicks=9 的 `avgTickMs=3.692`、`GASTickTotal.avgMs=3.664`、`CoreRuntimeOwner.avgMs=2.540`、`GASCoreSimulationSystemGroup.avgMs=2.255`、`BoundaryOwner.avgMs=1.006` 均超过严格预算，当前只能作为性能瘦身输入，不能写成 DOTS 性能优秀。详见 [ISSUE-014 Headless 纯逻辑预算超标](ISSUE-014-Headless纯逻辑预算超标.md)。
19. 2026-06-08 Debugger 模块复审后，当前性能优化 owner 必须前移到 Debugger 架构本身：`GasRuntimeDebugger.cs` 当前把配置、cache、counter、event buffer、materialization、retention、snapshot 和 text export 混成单体；`GASRuntimeDiagnosticEventBuffer` 是稀疏大事件行，已经不适合继续承载新的数据导向指标。该事实详见 [ISSUE-003 Runtime Core Debugger 证据不足](ISSUE-003-RuntimeCoreDebugger证据不足.md)。
20. 2026-06-08 第一轮 Debugger 改造已把 AutoChess headless logic budget 的数据导向指标前移到 Runtime Debugger 模块：新增 `GasRuntimeDataOrientedScorecard`，由 AutoChess validation 创建 scorecard 后再做预算阈值判定，summary 显式输出 `scorecardSource=GasRuntimeDataOrientedScorecard`。第二刀已新增 `GasRuntimeDerivedExportSink`，让 `GasRuntimeDebugger.ExportDataOrientedScorecardToText(...)` 只作为 facade 并输出 Runtime-owned `runtimeDataOrientedScorecard`；AutoChess runner / report 现在还能输出 `metricFamilyMask` 与 `dominantRisk`。第三刀已新增 `GasRuntimeMetricFamilySnapshot` 并挂入 `GasRuntimeDiagnosticSnapshot.MetricFamilies`：scorecard 和 derived export 不再直接依赖 raw counter 拼接，AutoChess summary 新增 `metricFamilySource=GasRuntimeMetricFamilySnapshot`。第四刀已新增 `GasRuntimeDiagnosticEvidenceSnapshot` 并挂入 `GasRuntimeDiagnosticSnapshot.Evidence`：AutoChess validation、hotspot、runtime chain、repeat-run、presentation bridge 和 README 已改为消费 `RuntimeDiagnostics.Evidence.*`，不再直接绑定 `RuntimeDiagnostics.CoreCounters` / `Events` / `Stats` 等易变内部 schema。这只是 Runtime-owned scorecard、family snapshot、evidence envelope 与 derived export owner 的起点，尚未拆除 `GasRuntimeDebugger.cs` 的单体事件结构、稀疏大事件 row 和 diagnostic materialization 链。

## 核心问题看板

| ID | 当前问题 | 状态 | 严重度 | 当前主线入口 |
|---|---|---|---|---|
| ISSUE-001 | GE 生命周期从 request/entity pipeline 迁入 command/spec/active store，但 legacy fallback 和 generated proof 仍未闭合 | Active | P0 | R2 / R7 |
| ISSUE-002 | Observation 已进入 BoundaryProjection；gameplay event 已统一为 `GameplayEventBuffer` typed fact，Attribute/Cue/Tag 边界缓冲已由 BoundaryProjection 派生，Damage/EventBus helper 兼容写入口已删除，Presentation/Replay 只读 typed fact；Debugger observation materialization 已可归因，performance pass 污染风险已在 x50 pass-split 日志中收口为 0 | Mitigated | P1 | R3 防回流 / R4 evidence |
| ISSUE-003 | Debugger/Official diff 已有工具；Observation materialization、Magnitude Source evidence、strict budget scorecard、`GasRuntimeMetricFamilySnapshot`、`GasRuntimeDiagnosticEvidenceSnapshot` 与 `GasRuntimeDerivedExportSink` 已进入证据链，AutoChess 外部消费面已改走 `RuntimeDiagnostics.Evidence.*`，但 `GasRuntimeDebugger` 当前仍是 diagnostics proof 单体，稀疏大事件结构、metric family buffer、materialization 和 official diff 仍需拆成数据导向 Debugger owner | Active | P1 | R4 / R8 |
| ISSUE-004 | StructuralCommit gate 已真实存在；boundary request entity 已退场，但 direct-EM 分类和证据闭环仍需收口 | Active | P0 | R4 / R6 |
| ISSUE-005 | Generated 链路已反哺 Runtime Core；ability commit、instant spec/reduce、active mutation job 化和 static hot path gate 已同步模板，剩余风险集中在 active mutation store 选型、random lookup 与证据闭环 | Mitigated | P1 | R2 / R5 |
| ISSUE-006 | AutoChessDemo 已移出 Runtime Core；当前风险转为 bridge 直接 `EntityManager` | Mitigated | P1 | R6 |
| ISSUE-007 | 文档口径仍需持续防止旧事实回流 | Active | P2 | R0 |
| ISSUE-008 | DOTS 官方机制已部分进入规则，但 contract 与 runtime proof 需继续拆分 | Active | P1 | R0 / R4 / R5 |
| ISSUE-009 | 5 段主链已存在；frame/query/stream owner 仍未完全目标态化 | Active | P1 | R2 / R3 / R4 |
| ISSUE-010 | 执行范式旧风险已收窄；ASC dirty/present、execution output applied、active mutation apply、pending AttributeDelta owner-local apply 已收口到 owner chunk applicator，active mutation set-by-caller 输入已退出 singleton stream，execution output typed fact 已进入 NativeStream deterministic merge；当前主要是剩余 singleton stream、SourceAttribute snapshot lane 与 boundary managed query 风险 | Active | P1 | R2 / R3 / R7 |
| ISSUE-011 | 临时 query 泛滥旧口径已缓解；隐式 stream writer/append helper 已收窄为显式 owner 写入，execution output fact 已退出 structural ECB singleton append，active mutation command source 与 set-by-caller payload 已投影到 ASC owner-local buffer；当前 API 承载风险集中在剩余 command / 非 active mutation set-by-caller / spec / typed fact singleton stream carrier、`GASRuntimeShell` 多能力 facade、global facade 和 generated delta record carrier | Active | P1 | R1 / R2 / R3 / R6 |
| ISSUE-012 | 策划配置能力缺失：默认编辑对象仍是技术表行和协议字段，缺少业务能力包、影响分析、保存前配置图校验、Runtime trace preview 和发布校验快照 | Active | P1 | R5 / Authoring 配置链 / Spec 19 |
| ISSUE-013 | 新增能力业务推进链路过长：策划和程序无法区分配置型能力、胶水扩展型能力与 Runtime 语义型能力，纯配置变化也容易被迫进入程序排错 | Active | P1 | R5 / Authoring 配置链 / Spec 22 |
| ISSUE-014 | AutoChess x50 headless 业务链路跑通但纯逻辑平均耗时超过真实游戏预算；`headlessLogicBudgetPassed` 已成为验证硬门，`performanceExcellentPassed` 还要求 Profiler evidence | Active | P0/P1 | R4 / R8 / CoreSimulation 瘦身 |

## 后续清理事实约束

本轮复查后，后续任务不再围绕“兼容旧链路”做小步迁移，而是按删除旧事实源和证据闭环的方式推进；默认领取入口是 `../02-主线任务树/GAS架构重划分主线任务.md`，旧 `GAS架构瘦身后续任务.md` 只作为已完成瘦身切口的兼容参考：

1. **已完成：删除 Damage/Attribute/Cue/Tag helper 兼容写入口**：`DamageEventBuffer`、`EventBusHelper.EnqueueDamageEvent`、`EventBusHelper.EnqueueAttributeChangeEvent`、`EventBusHelper.EnqueueCueRequest`、`EventBusHelper.EnqueueTagChangeEvent` 已退场；Damage 进入 `GameplayEventBuffer` typed fact，Attribute/Cue/Tag 只能由 `GameplayFactBoundaryProjectionSystem` 派生。
2. **已完成：收缩 Presentation/Replay 输入**：`PresentationOutboxProjectionSystem` 与 `ReplayLogSystem` 不再双读 Attribute/Cue/Tag/Damage 边界缓冲，只从 `GameplayEventBuffer` typed fact 投影。
3. **已完成：generated active mutation 模板瘦身切口**：不把 `.gen.cs` 当唯一事实源，模板 `GasGlueCodeGenPhases` 与当前 generated 输出同步。active mutation 已从直接 serial stream loop 推进为 owner-local command / set-by-caller payload、frame-local command / set-by-caller list、owner/sequence/context 排序、owner range applicator 和 runtime counters。它仍不是 owner-local store 终局，后续按新任务继续拆 store。
4. **已完成：global facade Runtime helper 收缩切口**：`GASManager.EntityManager` 只允许 bootstrap、authoring/prototype、Boundary facade、Debugger、demo adapter 和 store guard 使用；Runtime Core helper、config component 和 generated template 禁止通过全局 facade 写 ECS。当前 `EffectCommandSpecStream` fallback、`PresentationEntityBindingRegistry`、`GameplayCueUnit`、`GameplayCueBase` 的内部全局读取已删除，GameObject binding 已按 `World.SequenceNumber + Entity` 做 world-aware key。
5. **已完成：Runtime helper 隐式 stream writer 收缩切口**：`GEEffectCommandSpecStream` 不再暴露 `BeginCommandWriter(EntityManager)`、`BeginGameplayEventWriter(EntityManager)` 或 `AppendCommand/AppendGameplayEvent(EntityManager, ...)`；Runtime helper 必须显式解析 stream owner 和 frame 后写 command/fact。该切口不改变 singleton DynamicBuffer carrier 仍是 proof/migration 的事实。
6. **继续：结构变化证据闭环**：用 `GasRuntimeOfficialToolDiff`、Profiler/Debugger counters 和 AutoChess 规模门拆分 Core/Boundary/Demo/Observation 成本。

## 关键数据点

- 未注册旧 system 第一轮瘦身已删除：空的 `GEEffectCommandIngestSystem`、未注册的 `AttributeChangeEventProjectionSystem`、未进入主链的 `GASManagerInputSystem : SystemBase` 已退场。
- `state.Dependency.Complete()` 当前 `Assets/GAS/**/*.cs` 扫描为 0；第二轮已移除 destroy/finalize、ExecutionCalculation、OutputModifier、generated ActiveEffect pre-tick 等旧同步等待。
- `ASCCommandBufferResolveSystem` 已拆成先标记 destroying、再解析命令的两段 scheduled job；`ASCCommandPendingComponent`、`ASCDestroyingComponent`、`AttributeDirtyComponent` 的 ASC current-entity 开关已用 chunk `EnabledMask` 处理，避免同 job 内按 chunk 顺序判断 target 可用性。
- generated `AbilityCatalogCommitJob` 已从 `ComponentLookup<AbilityCommitRequestComponent>.SetComponentEnabled` 随机开关改为 `ComponentTypeHandle<AbilityCommitRequestComponent>` + `chunk.GetEnabledMask(ref ...)`；auto-end 写 `AbilityEndRequestComponent` 也已改为 current ability chunk `EnabledMask`，符合 `EN-03` / `CASE-20` / `PRF-22` 的批量 enableable 口径。
- instant effect spec / reduce 当前真实 owner 是手写 `Assets/GAS/Runtime/System/Effect/GEEffectInstantSystems.cs`，包含 `GEEffectSpecBuildSystem` 与 `GASAttributeSetReduceApplySystem`；`RuntimeEffectInstant.gen.cs` 只保留 `HandwrittenRuntimeOwner` marker。不能再把 instant spec/reduce 写成 generated lifecycle 当前事实，也不能把手写 owner 接管写成 SourceGenerator 完成态。
- generated `GASActiveEffectMutationApplySystem` 已调度 `GEActiveEffectMutationOwnerCommandCollectJob : IJobChunk` + `GEActiveEffectMutationOwnerCommandFinalizeJob : IJob` + `GEActiveEffectMutationChunkApplyJob : IJobChunk`；active mutation command 与 set-by-caller payload 先投影到 ASC owner-local buffer，再展平成 frame-local command / payload list 进入 chunk-local apply。旧 public/static `TryApplyActiveMutation(EntityManager, ...)`、旧 `GEActiveEffectMutationGatherJob : IJob` singleton scan、旧 `GEActiveEffectMutationApplyJob : IJob`、旧 random lookup 估算路径、旧 active mutation singleton set-by-caller 输入均已由 static gate 阻断。当前残留是原始 command producer / 非 active mutation set-by-caller / spec singleton carrier、SourceAttribute 跨 owner magnitude snapshot 容量/ordering 证据不足；generated/runtime/demo gameplay event 已统一写入 `GameplayEventBuffer` typed fact，旧 `GameplayEventBusEventBuffer` 类型和承载已删除。
- generated `AbilityCatalogCommitSystem` 已移除 per-frame `NativeList<GECommandSeedRecord>(Allocator.TempJob)` seed scratch；模板现在直接构造单条 `GECommandSeedRecord` 并追加 command。
- hand-written instant GE spec build 已支持 cue-only instant GE：`CanBuildInstantSpec` 允许 `ModifierCount > 0 || GameplayCueCode > 0`；2026-06-08 追加后，instant GE 也会读取 target `TagMaskComponent` 并评估 hand-written `GASRuntimeRequirementEvaluator.EvaluateGameplayEffectRequirements(...)`，不再绕过 `ApplicationRequiredTags`。
- `ASCDestroyingComponent` 当前按 enableable bit 判定销毁态；默认 disabled 的 ASC 不再因持有组件而被 generated spec/commit 路径误判为 destroying。
- generated active lifecycle 虽已 scheduled job 化，且 explicit remove 的 `GERemoveCommandPendingComponent` 关闭已改为 chunk `EnabledMask`；ability cancel/destroy-on-cleanup 已进一步收口到 frame-local `AbilityLifecycleRequestBuffer`，由 `AbilityLifecycleRequestSystem` 在 ability chunk 内统一应用。ASC dirty / active modifier present 也已收口到 frame-local `AttributeOwnerMarkerRequestBuffer`，由 `AttributeOwnerMarkerRequestSystem` 在 ASC chunk 内统一应用。
- `GEExecutionCalculationOutputModifierSystem` 的 output applied marker 已改为 effect-owned chunk applicator；ASC dirty 通过 `AttributeOwnerMarkerRequestBuffer` 归并，不再由 `AppliedLookup` / `AttributeDirtyLookup` 随机开 enableable。
- `GEExecutionCalculationSystem` 的 `ExecutionCalculationOutputUpdated` typed fact 已从并行 `EntityCommandBuffer.AppendToBuffer(StreamEntity, GameplayEventBuffer)` 迁出：并行计算 job 写 `NativeStream`，`GEExecutionCalculationFactMergeJob` 读取后按 Target ASC + record order 稳定排序，统一写入 `GameplayEventBuffer` 并分配 `NextFactSequence`；`Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` 已加入防回流断言。
- Core stream/job 前置计数已从 `CalculateChunkCount()` 改为 `CalculateChunkCountWithoutFiltering()`，避免 enableable/filter sync 并匹配 `IJobChunk` 的 unfiltered chunk index。
- `AbilityLifecycleRequestSystem`、`AbilityStateCleanupSystem` 和 `AttributeOwnerMarkerRequestSystem` 已把 ability / attribute owner marker 的 enableable 写入收口到 owner chunk `EnabledMask`；cleanup 仍通过 lookup 访问 owner/effect store、临时 tag 和 EventBus，不能写成完整 owner-local 归并终局。
- `GEEffectCommandSpecStreamFramePrepareSystem` 已从主线程 `EntityManager.GetBuffer` 清理/compact 改为 scheduled `IJob`，复用 `EffectCommandSpecStream.PrepareFrameLocalData(ref stream, buffers...)` 的 buffer-only 路径。
- `GameplayFactProjectionSystem` 已从主线程 `EntityManager.GetBuffer` + legacy EventBus writer 改为 scheduled `IJob`，并进一步瘦身为只写 `GameplayEventBuffer` typed fact；Attribute/Cue/Tag 边界缓冲派生已移到 `GASBoundaryProjectionSystemGroup` 内的 `GameplayFactBoundaryProjectionSystem`。旧 `GameplayFactEventBridgeSystem`、`GEInstantEffectCueRequestProjectionSystem`、Damage/EventBus helper 兼容写入口已直接退场。
- `GasCodeGenValidationReport.md` 已加入 generated runtime hot path 静态门禁；当前 `GeneratedHotPathRegressionHits: 0`，会扫描并报告 `Complete()`、`.Run()`、legacy EventBus writer、旧 active mutation helper、active mutation 逐 command owner lookup、ability seed scratch、generated/template `GASManager.EntityManager` 等回流项。
- `Tools/CodeGen/Generate-GAS-SourceGen.bat` 是不启动 Unity 的快速生成驱动；`Tools/CodeGen/Generate-GAS-CodeGen.bat` 是 Unity batchmode 验证驱动，不需要打开 Editor UI。
- AutoChess command drive 与 execute calculation 已改为 scheduled job；2026-06-07 pending AttributeDelta chunk-local 复测显示 `completed=True`、`winner=Player`、`scale=50`、`units=200`、`battleTicks=11`、`commands=1050`、`attributeChanges=898`、`executionOutputs=344`、`cueRequests=1404`、`periodTickDamageFacts=150`、`debugErrors=0`、`blockingDebugErrors=0`、`pendingAttributeAppliedDeltas=250`、`pendingAttributeEstimatedRandomLookups=0`、`pendingAttributeMigrationCarriers=0`。
- 同一 Run3 诊断仍暴露 `syncQueryBudget=13`、`dependencyWaitRisks=4`、`requiredStructuralPlaybacks=5`、`recordedStructuralPlaybacks=0`、`profilerCaptureState=profiler disabled; Entities profiler modules collect no data`。因此该归档证据只能证明当轮 AutoChess 业务链路跑通和部分 Debugger 字段可读，不能证明 StructuralCommit evidence 闭环、DOTS 优秀水平或 R6/R8 完成。
- `EntityManager.CreateEntityQuery()` 当前 Runtime 可执行命中为 0；`ToEntityArray()` 当前运行命中为 `GasRuntimeDebugger` 的 `activeEffectStoreQuery` / `presentationOutboxQuery` 和 `CueManagedLifecycleSystem` 的 cue query；Debugger / Cue 为 observation / managed boundary。`GASAttributeModifierDeltaApplySystem` 不再物化 owner entity list；`GasRuntimeDebugger` singleton lookup、`GASRuntimeFrameContext` current-frame lookup 和 `ActiveEffectStore` global index owner 均已改为 registered/cache owner，不再创建 fallback query，也不能再归入 `ToEntityArray` fallback 命中。
- AutoChessDemo 当前 2 个 demo ECS systems 插入 current GAS groups：command drive 和 execute calculation extension。

## 事实审计快照

| 事实项 | 当前归类 | 证据路径 | 文档使用规则 |
|---|---|---|---|
| 5 段 GAS physical group | runtime-active | `GASSystemScheduleContract.cs`, `GASGroups.cs`, `GASManager.cs` | 可作为当前主链事实 |
| 8 个 logical phase contract | contract-only / mapping aid | `GASSystemScheduleContract.RuntimeCoreFramePhases` | 不能写成 8 个 physical group 已落地 |
| generated runtime registration 防回流 | guard-only | `GASSystemScheduleContract.cs` 的 `GeneratedCommandResolveSystemTypeNames` / `GeneratedCoreSimulationSystemTypeNames` 当前为空数组，`AddSystemsByTypeName(...)` 缺失 type 会 fail-fast | 不能写成当前主调度来源；generated catalog / pure glue 必须纳入 Runtime 审查，generated lifecycle / registration 回流默认阻断 |
| `GASDefinitionCatalogBlob` | runtime-active data source | `GASDefinitionCatalogRuntimeTypes.cs`, `DefinitionCatalog.gen.cs` | 可作为 generated catalog / hand-written runtime consumer 事实 |
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
6. 本目录可以引用目标态 Spec 做判定，但不能把目标态架构正文复制成事实；若需要补充目标态设计，更新 `../01-目标态架构共识/`。
7. 从 `01` 迁出的 `计划` / `复审` 类内容必须拆分：当前证据留在本目录，执行项去任务树，纯设计去目标态 Spec。

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
2. active mutation 已完成 gather + ASC chunk-local apply，pending AttributeDelta owner-local apply 也已完成 ASC chunk-local apply；`activeMutationEstimatedRandomLookups=0`、`activeMutationOwnerResourceLookups=0`、`activeMutationMigrationCarriers=0`、`pendingAttributeEstimatedRandomLookups=0`、`pendingAttributeMigrationCarriers=0` 已有 AutoChess x50 runner 证据。pending AttributeDelta 旧 stream migration fallback 已删除；execution output typed fact 已开始从 structural ECB singleton append 迁到 NativeStream deterministic merge。剩余最重风险转向 command/spec/attribute delta/active mutation/其他 fact 的 singleton carrier、generated instant delta record / fan-in 证明、SourceAttribute magnitude snapshot lane、frame backbone 的 `syncQueryBudget=13` / `dependencyWaitRisks=4`，以及结构变化证据闭环。runtime/generated/demo gameplay event 写入已切到 typed fact，legacy gameplay EventBus buffer、Damage 边界缓冲和 EventBus gameplay enqueue helper 已删除。
3. singleton DynamicBuffer stream 是 proof carrier，不是 scale-ready 终局；本轮已删除隐式 singleton writer/append helper，并补入 stream carrier pressure evidence gate，但不能把显式 owner 写入或 pressure 可见误判为 stream carrier 已拆分。
4. StructuralCommit gate 需要 Journaling/Profiler 证明来源和相位。
5. AutoChess bridge 需要把直接 `EntityManager` 操作从业务 adapter 中继续收口；`DestroyBattleUnit()` 当前已走 ASC destroy command request，旧 direct ability/effect cleanup 口径不再作为当前事实。
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
