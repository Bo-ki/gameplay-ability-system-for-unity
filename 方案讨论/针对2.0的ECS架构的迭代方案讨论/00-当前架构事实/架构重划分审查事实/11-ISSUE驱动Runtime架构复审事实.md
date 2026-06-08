# ISSUE 驱动 Runtime 架构复审事实

> Owner：`00-当前架构事实/架构重划分审查事实` | 最近复核：2026-06-08 | 范围：`Assets/GAS/Runtime`、`Assets/GAS/Generated/CodeGen/Runtime`、`Assets/AutoChessDemo`

本文件只记录本轮继续审查当前代码后的事实结论。目标态接口、理想 Module 和验收门槛回到 `../../01-目标态架构共识/`；可领取计划回到 `../../02-主线任务树/`。

## 本轮复核输入

| 证据面 | 本轮事实 | 使用规则 |
|---|---|---|
| codedb live 截面 | `434 files / 434 outlines / 766 chunks / graph 448 nodes / 1659 edges / 41 communities / scan ready` | 这是本轮事实输入；早前文档中的旧 chunks / edges 统计不能继续当作最新统计 |
| Runtime module-map | `Assets/GAS/Runtime` 当前主模块为 `143 files / 2162 indexed symbols`，dependency edges 为 internal 462 / boundary 111 / incoming 78 / outgoing 33 | Runtime 仍是跨 Core、Definition、Debugger、Shell、Generated runtime 的大依赖社群；目录不等于 owner locality |
| API health 静态扫描 | `SystemAPI.Query`、`EntityManager.CreateEntityQuery`、`state.Dependency.Complete()`、`.Run(` 在本轮扫描范围没有热路径命中；`ToEntityArray` 运行命中集中在 Debugger observation 2 处和 Cue managed boundary 1 处；`CompleteAllTrackedJobs` 只在 Shell runner drain | 旧的“临时 query 泛滥”不再是主矛盾；当前主矛盾转为 capability、carrier、lifetime 和 evidence owner |
| Shell / Adapter | `GASRuntimeShell` 仍解析 `World`、`EntityManager`、command port、live read model、presentation binding、runtime singleton 和 job drain；`AutoChessGasRuntimeAccess` 15 个 contract entry 均 `ProxiesEcsHandle=true`，`ManualSync=1`，`PerformancePassRisk=15`，`BattleHashAffecting=4` | wrapper 集中化只能算收口点，不是 Thin Adapter 完成 |
| Stream / fact owner | `GEEffectCommandStreamComponent` 仍保存 `NextContextId`、`NextCommandSequence`、`NextSpecSequence`、`NextDeltaSequence`、`NextFactSequence` 字段；但 Runtime / AutoChess 内部 producer 已统一通过 `GASRuntimeSequenceAllocator` 分配 GE stream context / command / spec / delta / fact sequence，`GASRuntimeSequenceOwnerContract` 显式声明 fact / delta / command / spec owner | sequence owner 语义已从散点 System 收口到 Runtime contract；底层字段仍是 singleton 临时存储，carrier 物理拆分尚未完成 |
| Generated runtime | `RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs`、`RuntimeActiveEffect.gen.cs` 均为 12 行 marker；`GasCodeGenValidationReport.md` 当前 `GeneratedRuntimeBoundaryHits=0`、`GeneratedRuntimePureGlueArtifacts=4`、lifecycle / registration / structural / ownership / random lookup / managed config hit 均为 0 | lifecycle 文件面已退出是正向事实；0 hit 不是 release-ready 完成，仍缺负例、manifest/report/file/schedule 对账和 catalog lifetime |
| Static lookup lifetime | `StaticLookups.gen.cs` 的 lookup struct 拥有 `NativeArray`、`OwnsMemory` 与 `Dispose()` | generated lookup 不再是 lifecycle system，但 runtime-visible NativeArray 仍需要明确 install / release / owner |
| AutoChess execution extension | `AutoChessExecuteDamageCalculationSystem` 在 `GASCoreSimulationSystemGroup` 中读取 owner-local spec 和 attribute，写 `AttributeModifierBuffer`、`OwnerLocalGameplayFactBuffer`，delta/fact sequence 通过 `GASRuntimeSequenceAllocator` 取得；validation report 现在输出 `gasConceptCoverageMask`、`gasConceptMissingMask`、`runtimeTraceStageMask`、`runtimeTraceMissingStageMask`、seed / modifier / fact / cue 计数 | 业务 extension 是正向 Core 扩展示例；但它只证明真实业务 proof 已接入 sequence owner 与 concept trace evidence，不代表通用 carrier 物理拆分、Shell capability 和 catalog lifetime 完成 |
| Debugger / Presentation | `GasRuntimeDebugger` 仍是 4156 行大聚合；`AutoChessGasObservationGateway` 能记录 PhysicalGroup / OwnerSplit timing；Cue managed lifecycle 仍有 `Time.time`、`ToEntityArray` 和 managed callback | evidence 已可派生，但 Profiler / Journaling / TopN / pass split 仍需闭环；Presentation 成本不能并入 Core hot path |

## 官方依据对照

本轮仍以本仓库本地官方原件为准，采用以下规则裁决当前事实：

| 官方原件 | 本轮采用结论 | 映射规则 |
|---|---|---|
| `com.unity.entities/Documentation~/common-errors.md` | System 自己未通过 `GetEntityQuery` 创建的 query 会破坏 safety 追踪；当前 Runtime Core stored query 使用 `state.GetEntityQuery(...)` 是正向事实 | `QRY-02` / `PRF-33` |
| `components-enableable-use.md`、`structural-changes-enableable-components.md` | enableable 避免结构变化，但随机 enable/disable 与主线程 query 仍可能带来同步 / 访问成本；高频写入优先 owner iteration / chunk mask | `EN-03` / `PRF-22` |
| `systems-looking-up-data.md` | `ComponentLookup` / `BufferLookup` 是随机访问工具，不等于 scale-ready store；高频跨 owner 读写必须 owner-local 或 target-grouped | `QRY-04` / `PRF-06` / `PRF-19` |
| `components-buffer-command-buffer.md`、`systems-entity-command-buffer-playback.md` | 并行 append / playback 顺序必须由 sort key 保证 deterministic；ECB 是结构变化工具，不是 gameplay fact 总线 | `ECB-01` / `ECB-03` / `CASE-47` |
| `systems-manage-structural-changes-intro.md`、`systems-entity-command-buffer-use.md` | hot path 结构变化应集中到明确 playback phase；是否使用 EntityManager / ECB 需要 Profiler 证据 | `SC-01` / `PRF-04` |
| `profiler-module-structural-changes.md`、`entities-journaling.md`、`performance-debugging.md` | 内部日志不能替代 Profiler / Journaling；结构变化来源、World、System 需要官方 evidence 或明确 disabled reason | `DBG-01..05` / `ODF-*` |
| `20-GASRuntimeCore-API选型基线.md` | 每类数据必须先分类为 gameplay / transient / telemetry / presentation，再声明采用 API、拒绝 API、allocator owner、deterministic merge 和重新选型触发 | `SEL-01..05` |

## ISSUE 复核结论

| ISSUE | 当前复核结论 | 下一轮消费 |
|---|---|---|
| ISSUE-011 临时 EntityQuery 与 API 承载 | 旧 query 风险已缓解；新风险是 `GASRuntimeShell` / `AutoChessGasRuntimeAccess` 多 capability、`GEEffectCommandStreamComponent` 多语义 carrier、fact/delta sequence owner 与 generated lookup lifetime | R1 / R6 / R3 / R5 |
| ISSUE-004 StructuralCommit evidence | 结构变化 gate 存在，本轮 x50 能输出 `journalingCaptured=True`；但 Profiler 仍是 disabled，Structural Changes module 证据和 required / recorded playback matrix 未闭合，不能写成结构变化闭环完成 | R4 |
| ISSUE-005 Generated 链路 | generated lifecycle 文件面退出，pure glue 保留；但 release-ready 仍缺负例、catalog lifetime / dispose owner、manifest/report/file/schedule 对账 | R5 |
| ISSUE-006 AutoChess bridge | Demo 分层已变好，但 runtime access wrapper 仍代理 `World` / `EntityManager` / singleton / command port / drain；AutoChess 仍不是 Thin Adapter 完成态 | R1 / R6 |
| ISSUE-003 Debugger | observation materialization、structured log、timing split 可读；但 Debugger / Cue / derived export 成本仍要和 Core、Boundary、Runner、Diagnostics 分离 | R4 / R6 / R8 |
| ISSUE-010 执行范式 | owner chunk、owner-local spec/fact 和 NativeStream deterministic merge 都有正向切片；AutoChess execution 仍写 delta/fact 并从 singleton stream 分配 sequence，不能写成 fan-in 终局 | R3 / R5 / R4 |

## 本轮 Module Depth 判断

本轮采用 `Module / Interface / Implementation / Depth / Seam / Adapter / Locality` 口径审查当前 Runtime。

| Module | 当前 Interface 暴露 | Depth 判断 | 删除测试 |
|---|---|---|---|
| `GASRuntimeShell` | 外部或 adapter-facing 仍可间接取得 `World`、`EntityManager`、runtime singleton、raw `Entity`、live read model 和 runner drain | 浅 Interface；调用方仍理解 ECS handle 与 timing domain | 删除后复杂度会散回 AutoChess、Editor watcher、Runtime binding，说明它是迁移收口点，但不是深 Module |
| `AutoChessGasRuntimeAccess` | 15 个方法按 contract 标注 capability，但全部 proxy ECS handle | 只是 access concentrator；capability matrix 是证据，不是完成态 | 删除 wrapper 后 Shell 调用会散回各 adapter；说明 wrapper 有收敛价值，但仍未隐藏 ECS complexity |
| `GEEffectCommandSpecStream` | command、set-by-caller、spec、delta、mutation、fact、sequence、pressure counter、magnitude counter 共处一个 singleton owner | 多语义 carrier，Interface 近似实现复杂度 | 删除后每类数据必须重选 carrier；这正说明它当前承担过多过浅 |
| `Generated Runtime Pure Glue` | marker / catalog / lookup / pure evaluator 可被 Runtime 消费 | 正向但不完整；lookup memory/lifetime 和 hand-written consumer 对账未闭合 | 删除 pure glue 会迫使手写 resolver 回到配置查表；说明 pure glue 有价值，但不能拥有 lifecycle |
| `GasRuntimeDebugger` | counters、observation materialization、timing、structured export、text export、official diff 聚合 | evidence owner 方向正确但实现过宽 | 删除后证据会散到 runner、report、presentation；说明它需要继续分 evidence source / derived export / cost domain |
| `AutoChessExecuteDamageCalculationSystem` | 在 Core group 内扫描 specs、读取 attribute、写 delta/fact、更新 driver stats | 业务 extension 有真实价值，但仍依赖 stream sequence owner和 Demo driver state | 删除后 AutoChess 业务验证丢失；说明它是重要业务 proof，但不能替代通用 Runtime lane 设计 |

## 当前保留面与退出面

保留面：

1. 5 段 physical backbone、`ISystem` / `IJobChunk`、`state.GetEntityQuery`、chunk enabled mask、owner-local spec / fact、typed fact projection、generated catalog / pure glue、structured evidence。
2. AutoChess 作为真实业务验收 Shell 的价值：能驱动 command、execution calculation、report projection、timing split 和 validation report。
3. `AutoChessGasRuntimeAccessContract` 作为风险可视化：它把 wrapper 方法的 capability、timing、evidence、exit task 写成机器可读摘要。

退出面：

1. 业务 / Demo / Editor watcher 不得继续通过 Shell 或 wrapper 持有 `World`、`EntityManager`、runtime singleton、raw `Entity`、live read model 或 job drain。
2. `GEEffectCommandStreamComponent` 不应长期保存所有 command/spec/delta/fact sequence 和 multi-data-kind pressure owner；本轮已把分配 API 收口到 `GASRuntimeSequenceAllocator`，下一轮仍要把 fact / delta / active mutation 的物理 carrier 与 target-grouped lane 拆开。
3. SourceGenerator 不得回流生成 lifecycle、query、ECB、NativeContainer owner；`StaticLookups` 与 `DefinitionCatalog` 的 install / release / schema hash / dispose owner 必须归 `DefinitionCatalogLifetime`。
4. Debugger / Replay / StructuredLog / Cue managed boundary 的 `ToEntityArray`、string export、file IO、managed callback 必须归 Diagnostics / Presentation cost domain，不进入 CoreSimulation 性能结论。
5. 本轮 AutoChess x50 concept trace 已作为当前 proof：`passed=True`、`blockingDebugErrors=0`、`journalingCaptured=True`、`gasConceptCoverageMask=0x1FF`、`runtimeTraceStageMask=0x2CD`；但没有新跑 x100 / x1000、Unity Test Runner、Profiler enabled 或完整 headless scale profile。

## 本轮结论

当前 GAS Runtime 已经从“是否 ECS 化”的问题推进到“Module 是否足够深”的问题。本轮代码切片补上了 `GASRuntimeConceptCoverageSnapshot`、`GASRuntimeTracePreview`、`GASRuntimeSequenceOwnerContract`、`GASRuntimeSequenceAllocator`，并让 AutoChess validation summary 消费真实 generated catalog trace。最值得保留的是 ECS physical backbone、owner chunk applicator、generated immutable catalog / pure glue 和 structured evidence；最需要继续瘦身的是多 capability Shell、AutoChess access concentrator、singleton stream 物理 carrier、runtime-visible lookup lifetime、Debugger derived export 和 official evidence 闭环。

下一轮不应继续围绕旧 `QueryBuilder` 或旧 generated lifecycle 文件做小修，而应按 ISSUE 领取以下组合：

1. R1/R6：先拆 capability access，让业务层只看见 intent、opaque handle、snapshot 和 evidence。
2. R3：把 fact / delta / active mutation / command carrier 按 data-kind 选型；`NextFactSequence` / `NextDeltaSequence` 的散点分配 owner 已收口，下一步处理物理 carrier 和 target-grouped merge。
3. R5：补 DefinitionCatalogLifetime、generated lookup dispose owner、manifest/report/file/schedule 对账和 release-ready negative gate。
4. R4：建立 StructuralCommit / Debugger / Profiler / Journaling evidence matrix。
5. R8：R1-R7 至少闭合一轮后再进入 x50 / x100 / x1000 和 synthetic scale gate。
