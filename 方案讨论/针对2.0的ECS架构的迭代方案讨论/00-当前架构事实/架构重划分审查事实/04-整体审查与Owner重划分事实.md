# 04 整体审查与 Owner 重划分事实

> Owner：00-当前架构事实/架构重划分审查事实 | 状态：当前事实子页 | 拆分来源：../架构重划分审查事实.md | 拆分时间：2026-06-08

承载 2026-06-08 / 2026-06-07 整体审查、Owner 错位、重新划分事实结论和下一步事实验证需求。

本文件只记录当前代码事实、证据和 DOTS 判定；目标态设计正文回到 `../../01-目标态架构共识/`，任务拆分回到 `../../02-主线任务树/`。

## 2026-06-08 整体架构审查：最新事实截面

### 本轮整体 owner 重划分事实

本轮以 codedb live 截面、Runtime module-map、关键 owner outline、Runtime / Generated runtime 静态 API 搜索和 DOTS 官方规则对照重新审查整体架构。当前事实结论保持不变但需要更精确表达：项目已经具备可继续演进的 DOTS backbone，但 Runtime Core、Boundary Shell、Debugger、Definition、SourceGenerator 和 AutoChess adapter 仍未形成目标态的深 Module。

当前 live 输入：

| 证据面 | 本轮事实 | 当前判定 |
|---|---|---|
| codedb 总截面 | 434 files / 434 outlines / 766 chunks / graph 448 nodes / 1831 edges / 34 communities / scan ready | 索引覆盖 Runtime、Generated runtime、Editor CodeGen 和 AutoChess integration；数字只能作为本轮 `00` 事实输入 |
| Runtime 主社群 | 146 files / 2227 indexed symbols，dependency edges internal 604 / boundary 133 / incoming 101 / outgoing 32 | Runtime 文件夹仍是跨 Core、Definition、Debugger、Shell、schedule、generated runtime 的大依赖社群；目录名不能证明 owner locality |
| Shell import 面 | direct imported_by 为 AutoChess runtime access wrapper、Editor watcher、Runtime binding；transitive imported_by 扩展到 AutoChess lifecycle / catalog / observation / host、Editor watcher、Runtime binding 和 TargetCatcher | AutoChess wrapper 只是收口 direct import，不是 Thin Adapter 完成态 |
| Runtime API health | Runtime `SystemAPI.Query=0`、`EntityManager.CreateEntityQuery=0`；`CompleteAllTrackedJobs=1` 位于 Shell drain；`ToEntityArray` 可执行运行调用集中在 Debugger observation 2 处和 Cue managed boundary 1 处 | Core API health 明显改善；manual drain、Debugger observation 和 Cue materialization 必须按 owner / timing domain 归类 |
| Generated runtime | `RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs` 与 `RuntimeActiveEffect.gen.cs` 当前均为 12 行级 `RuntimePureGlue` marker；ability / instant / active-effect 真实 owner 分别转入手写 resolver、`GEEffectInstantSystems.cs`、`GASActiveEffectRuntime.cs` / `GEActiveEffectLifecycleSystems.cs`；`rg --files` 当前未发现 `ActiveEffectLifecycleOwnerSystems.cs` | generated pure glue 与 marker 是正向资产；当前风险已从 active generated lifecycle 转为 report / manifest / file / schedule 对账、catalog lifetime / dispose owner 证据、system budget、release-ready gate 和 generated lifecycle 防回流 |
| Stream / fan-in carrier | `GEEffectCommandSpecStream.cs` outline 967 行；required singleton stream buffers 已收窄为 command 与 set-by-caller，文件仍集中 command、set-by-caller、spec/delta/mutation 类型、gameplay event、owner-local fact、singleton owner、merge helper 和 pressure evidence | 这是迁移期 carrier owner；局部 owner-local spec / fact 与 NativeStream 切片不能推出全链路 scale-ready |
| Owner-local instant spec | 手写 `GEEffectInstantSystems.cs` 按 owner 写 spec / set-by-caller buffer，Attribute reduce/apply 改为 owner chunk `IJobChunk` 消费 owner-local spec / attribute / fact | 这是 spec/reduce 退出 singleton stream carrier 的正向切片；active-effect 也已由 hand-written Runtime owner 接管，但仍缺 carrier、scale profile、catalog lifetime 和 generated gate 对账，不能写成 Pure ECS Core 完成 |
| Debugger evidence | `GasRuntimeDebugger.cs` outline 4156 行，覆盖 runtime counter、frame backbone、observation materialization、magnitude source、stream pressure、official diff / derived export | Debugger 已是 evidence owner；但 TopN、official capture、pass split、overhead owner 与 derived export source 仍需矩阵化 |
| CodeGen implementation | `GasGlueCodeGenPhases.cs` 当前 5556 行级，同时承载 definition catalog、pure glue、runtime lifecycle migration marker、Baker glue、query layout、validation report | CodeGen pipeline 是统一入口，但 phase implementation 仍是大聚合浅 Module；artifact owner / template owner / validation rule owner 还未充分拆分 |

按 DOTS 参考规则，本轮采用以下事实判定：

| DOTS 规则 | 对当前事实的解释 |
|---|---|
| `SYS-01` / `SYS-02` | 5 段 physical group 和大量 `ISystem` / job 化是正向事实；但 Shell / manager / generated lifecycle 不能继续承担 gameplay 中间层或 schedule owner |
| `QRY-01` / `PRF-05` | Runtime Core 主线程 query 截面为 0 是正向事实；Debugging / Cue 的 query materialization 必须独立计时，不能算 Core hot path |
| `BUF-02` / `NAT-03` | singleton DynamicBuffer 可作为 proof / 低量迁移 carrier；scale-ready fan-in 必须交还 deterministic merge、allocator owner、segment / capacity / spill 与 battle hash evidence |
| `DBG-01..05` | Debugger / Journaling / Profiler / validation evidence 互补；字符串日志、Mermaid 图、战报和 disabled reason 都不能单独证明性能达标 |
| `BLOB-01` / `BLOB-02` / `BUR-01` | generated catalog / lookup / pure evaluator 是目标方向；generated runtime lifecycle、query、ECB、NativeContainer owner 不能作为 SourceGenerator 成熟形态 |

### 本轮代码级解读

本轮代码证据说明，架构风险不再是“有没有 ECS 代码”，而是多个浅 Interface 仍把复杂度暴露给调用方。以下判断只记录当前实现事实，不替代 `01` 目标态 Spec：

| 代码面 | 当前实现事实 | 架构解读 |
|---|---|---|
| Shell capability | `GASRuntimeShell` 同时解析 runtime world、`EntityManager`、command port、read model、job drain、presentation bind、event bus、event log sink、runtime debugger 和 runtime singleton | 这是多 capability ECS handle facade；可以作为迁移收口点，但不能作为目标态 Thin Shell |
| Command resolve lane | `ASCCommandBufferResolveSystem` 是 `ISystem + IJobChunk`，同一个 job 持有 ASC command、Ability command、Destroy command、Attribute value、Ability slot、OwnerLocal fact、Tag source、ComponentLookup、BufferLookup、ECB 和 catalog | DOTS API 方向正确，但 lane interface 过宽；后续应按 Boundary command ingest、Ability resolve、Tag / Attribute mutation、Gameplay fact 和 Structural intent 拆 owner |
| Owner-local fact | `OwnerLocalGameplayFactBuffer` 已在 ASC command resolve 中写 Ability lifecycle request fact，并由 fact flush / export 链路继续导出 | 这是 Attribute / ASC command fact 多写入面正向切片；它不等于 command / spec / delta / fact 全链路退出 singleton carrier |
| Stream carrier | `GEEffectCommandSpecStream` 的 required stream buffer 已收窄为 command 与 set-by-caller，文件内仍集中 spec、mutation、delta、gameplay event、owner-local fact、singleton owner、merge helper和 pressure counter | 它仍是迁移期 carrier owner；可保留为 proof / 低量兼容，但 scale-ready 默认必须转向 owner-local range 或 `NativeStream` deterministic merge |
| Debugger evidence | `GasRuntimeDebugger` 集中 runtime counter、frame backbone、observation materialization、magnitude source、stream pressure、official diff 与 derived export | Debugger 已具备 evidence owner 雏形；但 TopN、official capture、pass split、overhead owner 和 derived export source 仍需矩阵化，不能用日志文本替代性能证据 |
| Hand-written definition resolver | `GASRuntimeDefinitionResolver` / `GASRuntimeRequirementEvaluator` 由 `AbilityCommitSystem` 调用，用 catalog lookup 构建 activation plan、requirement result 和 GE command seed；`GASGeneratedRuntimeDefinitionResolver` 当前无 runtime caller | ability activation 从 generated lifecycle 转向手写 Runtime owner 是正向事实；但它也制造了 generated pure glue 与 hand-written resolver 的语义对账任务，不能写成 SourceGenerator 全链路完成 |
| Definition / Generation | generated pure glue、activation marker 和 Blob catalog 是正向资产；`GasGlueCodeGenPhases` 仍集中多 phase implementation；generated runtime lifecycle、lookup、ECB 或 NativeContainer owner 当前 report 分项为 0，但仍需要防回流门 | SourceGenerator 目标态只能继承 immutable catalog、lookup、pure evaluator 和 validation；runtime lifecycle owner 必须退出、迁给手写 lane 或继续标记为 `MigrationProofOnly` |

因此，当前现实 owner 应按下表继续重划分，而不是继续扩一个更大的 Shell 或更大的 generated runtime：

| 现实 owner | 当前可保留面 | 当前退出 / 隔离面 |
|---|---|---|
| RuntimeSession / Backbone | World bootstrap、5 段 physical group、registered singleton owner、generated type fail-fast | 向 Application Shell 暴露 `World` / `EntityManager` / singleton / live SystemGroup |
| Boundary command / snapshot | command 写入口集中、opaque handle 和 capture read model 正向存在 | command、snapshot、diagnostics、runner sync、definition lifetime 共享同一 ECS handle facade |
| GASFrameKernel / lane owner | owner chunk applicator、enableable mask、registered query、scheduled job 方向正向 | 单个宽 job 同时承担过多 gameplay 语义；lane query / allocator / carrier / evidence owner 仍需拆 |
| EffectFanIn / GameplayFact carrier | owner-local mutation / delta / Attribute fact 切片、execution output NativeStream merge、pressure counter | command / spec / delta / mutation / fact 继续共用迁移 stream carrier，且全链路 capacity / spill / merge evidence 不完整 |
| ActiveEffectStore | owner-local slot、global index 注册、SourceAttribute snapshot key 和 cleanup helper 已有迁移资产 | lifecycle、query、ECB、NativeContainer 和 magnitude snapshot owner 已从 generated helper / companion 文件面退出；剩余审查点转为手写 owner budget、capacity / spill、snapshot lane 证据和 generated lifecycle 防回流 |
| DiagnosticsSink | Runtime counter、observation materialization、magnitude source、official diff、AutoChess evidence 字段已存在 | Debugger 大聚合、derived export、observation pass 与 Core timing 的隔离仍不充分 |
| Definition / SourceGenerator | Blob catalog、static lookup、pure resolver、activation marker、manifest category、validation gate，ability activation 已有手写 runtime resolver consumer | runtime lifecycle system、lookup / ECB / NativeContainer owner、generated pure glue 与 hand-written resolver 职责重复、GE evaluator 仍被 generated lifecycle consumer 消费 |
| AutoChess Application Shell | Host / CatalogSession / Ticker / Observation / Lifecycle / report projector 已拆 owner，业务验收价值明确 | runtime access wrapper 仍代理 session world、definition/lifecycle `EntityManager`、diagnostics singleton、command port 和 job drain |

本轮事实性重划分结论：当前架构的主要风险已从“是否开始 ECS 化”转为“接口是否足够深、owner 是否足够 local、evidence 是否足够可归因”。后续执行 Agent 若声称某个 owner 完成，必须同时交还 query / lookup / allocator / dependency / carrier / capacity / merge / structural playback / timing domain / evidence 字段；缺任一项只能写成迁移期事实。

### 事实消费卡

```markdown
来源类型：整体架构审查 / codedb 截面 / DOTS 官方规则对照
原始证据：
  - `codedb_status`: 434 files / 434 outlines / 766 chunks / graph 448 nodes / 1831 edges / 34 communities / scan ready
  - `codedb_module_map path_prefix=Assets/GAS/Runtime`: Runtime 主社群 146 files / 2227 indexed symbols，dependency edges 为 internal 604、boundary 133、incoming 101、outgoing 32；Core、Definition、Debugger、Shell、generated runtime 仍聚在同一依赖社群
  - `codedb_deps GASRuntimeShell.cs imported_by`: `AutoChessGasRuntimeAccess.cs`、Editor `GASWatcher`、Runtime `AbilitySystemBinding` 共 3 个直接图依赖；AutoChess 内部再由 runtime access wrapper 分发 capability
  - `codedb_outline GASRuntimeShell.cs`: 281 lines，包含 runtime world / entity manager、command port、read model capture、job drain、presentation bind、runtime singleton resolver
  - `codedb_outline GEEffectCommandSpecStream.cs`: 实际索引路径为 `Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs`，967 lines，集中 singleton carrier、frame-local counter、command/spec/delta/mutation/fact buffer 和 merge helper
  - `codedb_outline GasRuntimeDebugger.cs`: 4156 lines，集中 diagnostic event、runtime counter、observation materialization、magnitude source evidence、owner-local fact counters 和 derived export
  - `codedb_deps GASSystemScheduleContract.cs imported_by`: GASManager、AutoChessRuntimeSystemBootstrap、FrameBudget / StreamOwner / EvidenceGate / Rebind contract 共 6 个直接消费者
  - `codedb_text_search Assets/GAS/Runtime/**/*.cs`: `CreateEntityQuery=0`、`SystemAPI.Query=0`、`state.Dependency.Complete=0`、`.Run(=0`、`CompleteAllTrackedJobs=1`、`ToEntityArray` 可执行运行调用 3 处
  - `codedb_read RuntimeAbilityActivation.gen.cs`: 当前仅为 12 行 marker，`HandwrittenRuntimeOwner = true`
  - `codedb_callers GASRuntimeDefinitionResolver`: `AbilityCommitSystem` 中 activation plan 与 GE command seed 两处调用；`GASGeneratedRuntimeDefinitionResolver` 当前 0 caller；`GASRuntimeRequirementEvaluator` 被 `AbilityCommitSystem` 与手写 `GEEffectInstantSystems.cs` 调用，`GASRuntimeMagnitudeEvaluator` 被手写 `GEEffectInstantSystems.cs` 调用；`GASGeneratedRequirementEvaluator` 当前只剩 `RuntimeActiveEffect.gen.cs` 调用
  - `rg Assets/GAS/Generated/CodeGen/Runtime/**/*.cs`: generated runtime 当前不再命中 `ISystem`、`OnUpdate(ref SystemState)`、`ComponentLookup`、`BufferLookup` 或 `EntityCommandBuffer`；`StaticLookups.gen.cs` 仍有 lookup `NativeArray`，report 中 generated runtime boundary hit 当前为 0
  - `2026-06-08-AutoChessBattleValidation-ObservationMaterialization-Run1.log`: `completed=True`、`passed=True`、`runtimeChainPassed=True`、`repeatRunPassed=True`、`runtimeObservationMaterialization|queries=12|entities=2400|elapsedUs=92|performancePollutionRisks=12`
第一 owner：00
长期有效：待复核；本截面会随着 R1/R3/R4/R5/R6/R7 实现变化
需要反哺：01 owner map / target invariant，02 R 任务领取前截面，04 最近验证摘要
```

续轮校准：当前 codedb 状态为 434 files / 434 outlines / 766 chunks / graph 448 nodes / 1831 edges / 34 communities / scan ready。`GASRuntimeShell.cs` 直接图依赖仍覆盖 `AutoChessGasRuntimeAccess.cs`、Editor `GASWatcher` 和 `AbilitySystemBinding`；`AutoChessGasRuntimeAccess.cs` 本身把 World / EntityManager / diagnostics singleton / command port / job drain 聚合成 wrapper，AutoChess host / catalog / observation / lifecycle / ticker 通过该 wrapper 继续间接消费 Shell capability。本次数字校准只更新事实截面，不改变“多能力 Shell facade、singleton carrier、generated report/file/schedule 对账和 Debugger / Cue observation cost 仍需 owner 分类”的判定；wrapper 是收口点，不是 Thin Adapter 完成证明。

### 2026-06-08 五次校准：整体架构 Owner 复审事实

本次继续使用 codedb live 截面复核整体架构，重点不是重复“是否 ECS 化”，而是确认每个现实 Module 的 interface 是否已经足够深。当前 codedb 状态为 434 files / 434 outlines / 766 chunks / graph 448 nodes / 1831 edges / 34 communities / scan ready；`Assets/GAS/Runtime` module-map 主社群为 146 files / 2227 indexed symbols，dependency edges 为 internal 604、boundary 133、incoming 101、outgoing 32。该数字只代表本轮 live 输入，不写入 `01` 目标态。

| 审查对象 | 本轮 codedb 证据 | 当前事实判定 |
|---|---|---|
| Runtime 主社群 | 中心文件仍覆盖 `GASRuntimeEntityArchetypes.cs`、`ActiveEffectStore.cs`、`GASDefinitionTable.cs`、`GasRuntimeDebugger.cs`、`GASSystemScheduleContract.cs`、`GameplayEffectConfigRegistry.cs`、`GASRuntimeQueryLayoutPlan.cs` 和 `ASCCommandBufferResolveSystem.cs` | Runtime 已有 DOTS backbone，但 Core、Definition、Debugger、Shell、generated runtime 仍在同一个大依赖社群内；目录名和 namespace 不能证明 owner locality |
| Shell transitive import | `GASRuntimeShell.cs` transitive imported_by 为 AutoChess lifecycle/catalog/observation/runtime access/runtime host、Editor `GASWatcher`、Runtime `AbilitySystemBinding`、`CatchAreaBox3D` 共 8 个文件 | Shell 仍是跨 Demo、Editor、Runtime binding 的多 capability implementation facade；AutoChess wrapper 收敛 direct import 面，但没有完成 capability 分级 |
| Shell outline | `GASRuntimeShell.cs` 281 行，包含 runtime world / entity manager、command port、read model capture、job drain、presentation bind、GlobalTimer / EventBus / EventLogSink / RuntimeDebugger singleton resolver、raw ASC resolve | 当前 Shell 注释方向虽然是 Thin OOP shell，但实现仍同时拥有 session、command、snapshot、diagnostics、presentation、runner sync 和 singleton resolver；不能写成目标态 Shell 完成 |
| Command facade / binding | `ASCCommandGateway.cs` 445 行，`ASCCommandPort` 暴露 `RuntimeEntity`、command writer、request gameplay effect / ability / destroy 等；`AbilitySystemBinding.cs` 89 行，持有 Commands / ReadModel / Handle，并可为 Boundary 解析 runtime entity | command 写入口已集中是正向事实；但 public / internal interface 仍允许 Boundary assembly 理解 raw ECS identity、live buffer writer 和 handle 解析，R1/R6 仍需收权 |
| Core command resolve | `ASCCommandBufferResolveSystem.cs` 952 行，43 个 indexed symbols，单个 job 覆盖 ASC command、Ability grant/activate/remove、Tag diff、Attribute base value、Ability lifecycle request 和 GameplayEvent fact | DOTS API 形态正向，但 interface 过宽；它应作为 lane 拆分输入，而不是“深 Core Module 完成”证明 |
| Stream carrier | `GEEffectCommandSpecStream.cs` 967 行，集中 command / set-by-caller / spec / mutation / delta / gameplay fact / owner-local fact buffer、singleton owner、writer、merge helper、pressure evidence | 它是迁移期多语义 carrier owner；proof-only pressure counter 有价值，但 singleton stream 仍不能晋升为 scale-ready fan-in 目标 |
| Generated pure glue | `RuntimeDefinitionGlue.gen.cs` 295 行，仅提供 definition resolver、requirement evaluator、magnitude evaluator、target rule table 等 pure function；`RuntimeAbilityActivation.gen.cs` 是 `RuntimePureGlue` marker | 这是 Definition & Generation 的正向面；只有 pure lookup/evaluator/marker 能进入目标态生成物职责 |
| Hand-written definition resolver | `GASRuntimeDefinitionResolver.cs` 175 行；`AbilityCommitSystem` 通过它构建 activation plan 与 GE command seed，`GASGeneratedRuntimeDefinitionResolver` 当前 0 caller | ability activation owner 已转向手写 Runtime lane，是 pure glue 收权的正向事实；但 duplicate resolver / requirement evaluator 的职责边界需要 R5 继续对账 |
| Generated runtime residue | `RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs` 与 `RuntimeActiveEffect.gen.cs` 当前都只保留 `RuntimePureGlue` marker；ability / instant / active-effect 主体已分别转入手写 resolver、`GEEffectInstantSystems.cs`、`GASActiveEffectRuntime.cs` / `GEActiveEffectLifecycleSystems.cs`；`ActiveEffectLifecycleOwnerSystems.cs` 当前磁盘缺失 | manifest 中 `RuntimeLifecycleMigration` artifact 当前为 0，generated runtime boundary / lifecycle / ownership / structural / random lookup report hit 也为 0；剩余风险是 report/manifest/file/schedule 对账、catalog lifetime / dispose owner 证据、system budget、release-ready gate 和 future companion 防回流 |
| Debugger evidence | `GasRuntimeDebugger.cs` 4156 行，包含 diagnostic event、runtime counter、observation materialization、magnitude source evidence、effect stream pressure、frame backbone evidence 和 derived text export | Debugger 已是 evidence owner；但 evidence tier、official capture、observation pass、derived export 和 Core timing 必须分层消费，不能用日志文本替代性能证据 |
| AutoChess adapter | `AutoChessGasRuntimeAccess.cs` 75 行代理 session world、definition/lifecycle `EntityManager`、diagnostics world/singleton、command port 和 job drain；Host 负责 runtime init / system registration / catalog install / tick group lookup；CatalogSession 仍依赖 wrapper 返回 `EntityManager`；Ticker 逐段 update 5 个 group 后 drain jobs | AutoChess 是 Layer 1 业务验收 Shell，当前 adapter 已拆 owner 但仍通过 wrapper 共享 ECS handle；它不是 Thin Adapter 完成态 |

本轮 owner 事实结论：当前最应保留的是 5 段 physical backbone、chunk-local applicator、owner-local spec / fact 切片、generated immutable catalog / pure glue 和 structured evidence。当前最应退出或隔离的是多 capability Shell facade、剩余 singleton stream carrier、generated lifecycle / lookup / ECB / NativeContainer owner、Debugger / Cue materialization 混入 Core 性能口径、以及 AutoChess wrapper 共享 ECS handle 的迁移期访问层。

当前正确的重新划分输入不是新建更厚的 `RuntimeShell` / `RuntimeAccess`，而是把 `RuntimeSession`、`CommandPort`、`SnapshotReadModel`、`DiagnosticsSink`、`RunnerSync`、`DefinitionCatalogLifetime`、`GASFrameKernel`、`EffectFanInStore`、`ActiveEffectStore`、`StructuralCommit` 和 `GeneratedDefinitionGlue` 分别作为 owner 审查。每个 owner 交还时必须说明 query、lookup、allocator、dependency、carrier、capacity、merge、structural playback、timing domain 和 evidence 字段；缺任一项只能写成迁移期事实。

### 2026-06-08 Debugger evidence owner 续审事实

本次定点复核 `GasRuntimeDebugger`、official tool diff、AutoChess validation evidence 和 battle flow 的实际接线，并对照 `UnityDOTS官方文档参考/主题/06-Diagnostics-Profiler-Journaling.md` 与 `20-GASRuntimeCore-API选型基线.md`。新增事实只描述当前实现，不写入目标态 Spec。

| 审查面 | 当前证据 | 当前事实判定 |
|---|---|---|
| Debugger 聚合度 | `GasRuntimeDebugger.cs` 4156 行，集中 diagnostic event buffer、runtime core counters、frame backbone counters、observation materialization counters、magnitude source counters、owner-local fact counters、system timing aggregate、buffer pressure、derived text export 和 singleton resolver | Debugger 已是核心 evidence owner，但 Interface 仍很宽；调用方要理解多类 evidence tier、cost domain 和 derived export 才能判断含义，后续 R4 不能只用“Debugger 有 counter”作为完成证明 |
| Timing / system aggregate | `GasRuntimeDebugger.RecordTickTiming(...)`、`RecordSystemTimingAggregate(...)`、`RecordRuntimeCoreCounters(...)` 等方法已经能记录 tick timing、system timing aggregate 和 Runtime Core counter | 正向事实是 timing 已进入机器字段；缺口是 TopN / system / component / lane / job / lookup / buffer 的归因矩阵仍需 R4/R8 对账，不能只消费平均 tick |
| Observation materialization | `RecordObservationMaterialization(...)` 与 snapshot / text export 都能记录 materialized query / entity / elapsed / pollution risk | observation 成本已有独立字段，performance pass 与 diagnostic pass 可以分离；但任何保留的 `ToEntityArray` observation 仍必须带 overhead owner，不能混入 CoreSimulation 性能结论 |
| Magnitude source evidence | `RecordMagnitudeSourceEvidence(...)`、`AppendMagnitudeSourceCounters(...)` 和 AutoChess validation evidence 已消费 current value lookup、snapshot hit / miss、live lookup、fallback value / fact、source / target attribute lookup、execution input lookup | Magnitude source 已是机器可读 evidence 字段；但当前字段存在不等于业务非零覆盖、active effect slot tick lane 完成或 generated lifecycle owner 退出，R4/R8 仍需非零样本、capacity / spill 和 lane-specific attribution |
| Official tool diff | `GasRuntimeOfficialToolDiff` 在可用条件下读取 Entities Journaling records，统计 structural create / destroy / add / remove、enable / disable、RW access，并格式化 record / system / component TopN；Profiler 状态输出 available / capture state / disabled reason | Official diff 已有最小入口；但 Profiler enabled / Journaling cap / TopN 映射仍是 evidence gap。官方工具 disabled reason 不能被写成 captured performance evidence |
| AutoChess validation evidence | `AutoChessBattleValidationReport.CreateEvidence(...)` 从 diagnostic result、performance result、presentation snapshot 和 official diff 组装 counters、timing、observation pollution、magnitude source、factsHash / summaryHash、physics / render disabled reason | AutoChess evidence 是正向统一消费点；但当前仍需要证明 field family 同构、performance / diagnostic / official diff pass 分离和 derived export source，否则中文日志、Mermaid 图或 summary 只能算派生输出 |
| Measured window / debugger export | `AutoChessBattleFlow` 在 measured window 内启动 / 关闭 official tool diff capture，Complete 时导出 runtime diagnostics，并把 debugger export ticks 写入 runtime timing | runner 已能把 Debugger export 成本单独计时；但 runner sync、debugger export、official diff 和 Core tick 仍必须在 summary 中拆分，不得用 x50 functional pass 证明 DOTS 性能优秀 |

本轮 Debugger owner 结论：当前 Debugger 的正向价值很高，已经能承接结构化 evidence、official diff 状态和 AutoChess 验收字段；但它仍是一个大而宽的 evidence Module。后续 R4/R8 交还必须把 evidence tier、TopN、pass、overhead owner、official capture state、derived export source 和 lane-specific hotspot attribution 矩阵化。否则 Debugger 会从“性能优化证据系统”退化为“更大的日志总线”，无法支撑 DOTS 优秀水平判断。

### 2026-06-08 CodeGen / SourceGenerator artifact responsibility 续审事实

本轮继续复核 CodeGen / Luban / SourceGenerator 链路，使用 codedb 当前截面 434 files / 434 outlines / 766 chunks / graph 448 nodes / 1831 edges / 34 communities / scan ready；Runtime module-map 主社群为 146 files / 2227 indexed symbols，dependency edges internal 604 / boundary 133 / incoming 101 / outgoing 32。该截面只作为本轮事实输入，不作为任务完成证明。

| 审查面 | 当前证据 | 事实判定 |
|---|---|---|
| CodeGen Module depth | `GasGlueCodeGenPhases.cs` 为 5556 行，单文件承载 asmdef、definition index、Blob / lookup / catalog、runtime pure glue、runtime lifecycle marker、Baker glue、component type set、query layout、validation report 和 boundary collectors | CodeGen 已形成统一 pipeline，但 phase implementation 仍是大聚合浅 Module；后续优化应拆 artifact owner / template owner / validation rule owner，而不是新增并行扫描器 |
| Manifest responsibility | manifest 支持 `ArtifactCategory`，当前 `RuntimeDefinitionGlue.gen.cs`、`RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs` 与 `RuntimeActiveEffect.gen.cs` 均为 `RuntimePureGlue`，`RuntimeLifecycleMigration` artifact 当前为 0 | manifest 是 generated artifact 责任分类的一手事实；R5 必须交还 artifact responsibility matrix，不能只按目录或文件名判断 |
| Report / manifest 对账 | validation report 输出 pure / lifecycle migration artifact 计数、boundary hit 分项，并在 Manifest Entries 表逐行展示 `ArtifactCategory`；manifest JSON 是机器可读事实源 | 当前 report 可用于 gate；R5 仍需同时读取 report 与 manifest JSON，防止 marker、pure glue、lifecycle migration 和 bootstrap materialization owner 混读 |
| Pure glue 当前消费者 | generated definition resolver / requirement evaluator 是 pure glue 正向资产；ability / instant / active-effect runtime 主体已转到手写 Runtime owner，`.gen.cs` 文件当前只保留 marker / pure glue 责任 | pure glue 存在不等于 SourceGenerator 收权完成。目标态还需要 hand-written Runtime Core lane 直接拥有 query / dependency / writer，并只调用 generated pure glue |
| Catalog materialization | generated catalog builder 被 AutoChess 初始化 install/dispose 消费，也被 generated Editor Baker 通过 `AddBlobAsset` 消费 | `BuildCatalog()` 有 Baking / Bootstrap 低频证据；仍需独立 DefinitionCatalogLifetime owner、hot path 禁用和 dispose evidence |
| Boundary gate 语义 | validation classifier 允许 manifest `RuntimeLifecycleMigration` artifact 的 lifecycle / structural / native-container / random-lookup hit 作为 `MigrationProofOnly`，但 system-registration / managed-config 不能豁免 | gate 已经能阻断未知回流；剩余工作是把已分类迁移证明退出、阈值化或 release-ready fail-fast |

本轮 CodeGen / SourceGenerator 结论：当前架构已经具备可保留的 immutable catalog、static lookup、pure definition glue、manifest 分类和 boundary gate；但生成器仍不能作为 Runtime Core lifecycle owner。重新划分时，SourceGenerator 的目标 owner 只能是 Definition / Blob / lookup / pure glue / validation，Runtime lifecycle、query、ECB、NativeContainer、fan-in carrier 和 performance attribution 必须回到手写 ECS Core owner。

### 2026-06-08 续轮整体架构审查补充

本轮继续以 codedb 当前索引和生成报告复核整体架构。新增或修正的事实是：active effect lifecycle 主体已经转到手写 Runtime owner：`RuntimeActiveEffect.gen.cs` 当前只是 12 行级 marker / pure glue，真实 helper、job、snapshot、mutation、tick、remove 逻辑集中在 `GASActiveEffectRuntime.cs` 和 `GEActiveEffectLifecycleSystems.cs`。`ActiveEffectLifecycleOwnerSystems.cs` 当前已从磁盘文件列表、manifest、validation report 和 schedule type-name 列表退出；这个变化是 SourceGenerator 收权正向事实，但还需要防回流扫描、system budget、catalog bootstrap owner 和 hand-written owner 对账。

| 审查面 | 当前证据 | 事实判定 |
|---|---|---|
| codedb 截面 | `codedb_status` 为 434 files / 434 outlines / 766 chunks / graph 448 nodes / 1831 edges / 34 communities；Runtime module-map 主社群 146 files / 2227 indexed symbols，dependency edges internal 604 / boundary 133 / incoming 101 / outgoing 32 | 当前索引覆盖 `GEEffectInstantSystems.cs`、`GEActiveEffectCommandNormalizeSystem.cs`、Runtime 主链、OwnerLocal fact lane、AutoChess runtime access wrapper 和 Editor watcher；`ActiveEffectLifecycleOwnerSystems.cs` 当前未被索引且磁盘缺失。旧 431 / 432 / 433 / 434 / 759 / 780 / 781 / 782 / 803 / 1798 / 1803 / 1820 / 1826 / 2218 截面不再代表本轮最新事实 |
| Shell consumers | `codedb_deps GASRuntimeShell.cs imported_by` 为 `AutoChessGasRuntimeAccess.cs`、Editor `GASWatcher` 与 Runtime `AbilitySystemBinding` 共 3 个图依赖 | Shell 仍是多 capability implementation facade；AutoChess 直接 import 面已收口到 wrapper，但 adapter / editor / binding 仍共享同一 ECS handle 解析面 |
| Command resolve | `ASCCommandBufferResolveSystem.cs` 为 952 行，单个 `ASCCommandBufferResolveJob` 同时处理 ASC command、Ability grant / activate / remove、Tag diff、Attribute base value、lifecycle request 和 fact | DOTS API 形态正向；但 lane interface 仍过宽，调用方和审查者仍需理解多类 gameplay 语义如何在同一 job 内交织 |
| Generated runtime residue | `RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs` 与 `RuntimeActiveEffect.gen.cs` 均为 `RuntimePureGlue` marker；ability / instant / active-effect 主体已转到手写 Runtime owner；generated runtime lifecycle 当前不再由 manifest 分类承载；`ActiveEffectLifecycleOwnerSystems.cs` 当前磁盘缺失 | `RuntimeLifecycleMigration` artifact 当前为 0，generated runtime boundary / ownership report hit 当前为 0；剩余问题是 report / manifest / file / schedule 对账、future generated lifecycle 回流阻断、release-ready gate、system budget 和 catalog lifetime / dispose owner 证据 |
| CodeGen gate | `GasCodeGenValidationReport.md` 显示 `GeneratedRuntimePureGlueArtifacts=4`、`GeneratedRuntimeLifecycleMigrationArtifacts=0`、`GeneratedRuntimeBoundaryHits=0`、`GeneratedRuntimeLifecycleHits=0`、`GeneratedRuntimeStructuralChangeHits=0`、`GeneratedRuntimeOwnershipHits=0`、`GeneratedRuntimeRandomWriteLookupHits=0`、`GeneratedRuntimeSystemRegistrationHits=0` | gate 已能分类并阻断 unclassified lifecycle migration；但 0 命中不是目标态完成证明，release-ready SourceGenerator mode 仍要补负例验证、system budget、manifest/report/file/schedule 对账和 stale lifecycle 防回流 |
| DOTS 官方对照 | 本轮直接对照 `SYS-01`、`QRY-01`、`NAT-03`、`DBG-05` 和 `20-GASRuntimeCore-API选型基线.md` | OOP Shell 不能成为 gameplay 中间层；`NativeStream` 只有定义 deterministic merge、allocator owner、segment / memory budget 后才是目标 carrier；Debugger 成本必须分 cost domain |

本轮整体判断：当前架构不是“未 ECS 化”，而是“已有 ECS backbone、但 owner 深度不足”。正确重新划分不应新增一个更厚的 OOP manager，也不应让 SourceGenerator 继续生成 runtime lifecycle owner；应把 runtime session、command write、snapshot read、diagnostics、runner sync、definition lifetime、core lane、fan-in store、active effect store 和 structural commit 分别收权。

### 2026-06-08 二次校准：代码级 owner 深度事实

本次继续用 codedb 对关键文件做局部读取和调用方复核。新增事实只写入本文件，不进入目标态 Spec。

| 审查对象 | 本次代码证据 | 当前事实判定 |
|---|---|---|
| `GASRuntimeShell` | `GASRuntimeShell.cs:49-147` 同时创建 command port、capture read model、drain runtime jobs；`:150-233` 又处理 presentation bind 和 GlobalTimer / EventBus / EventLogSink / RuntimeDebugger singleton resolver；`codedb_deps` 显示 `AutoChessGasRuntimeAccess.cs`、Editor `GASWatcher`、`AbilitySystemBinding` 仍直接消费 | 它是多 capability ECS handle facade，不是目标态 Application Shell；AutoChess wrapper 是收口点但不是能力分级完成，后续必须把 command、snapshot、diagnostics、runner sync、definition lifetime 和 presentation capability 拆成可独立授权 / 计时 / 验收的 owner |
| `ASCCommandBufferResolveSystem` | `OnCreate` query 一次性要求 ASC identity、tag mask、command buffer、ability command、destroy command、attribute value、ability slot、fixed / temp tag source；`ASCCommandBufferResolveJob` 同时处理 destroy、ASC command、ability command、attribute dirty、tag mask、lifecycle request 和 GameplayEvent fact | API 形态已进入 stored query + `IJobChunk`，但 lane interface 仍宽；不能因为 job 化就写成 command resolve owner 已深模块化 |
| `GEEffectCommandSpecStream` | 静态 `_cachedEntityManager` / `_cachedStreamEntity`、`CommandWriter` / `OwnerLocalFactWriter` 持有 `EntityManager` 和 singleton stream owner；`HasRequiredBuffers` 当前只要求 `GEEffectCommandStreamComponent`，owner-local fact 通过 ASC 本地 buffer 写入，再由 `GEEffectCommandSpecStreamPhases` 收集并排序导出到 Boundary observation | fallback query 删除、required stream buffer 收窄和 owner-local fact lane 是正向事实；但 command / set-by-caller / typed fact 仍处迁移 carrier 与 stream export 链路附近，按 `BUF-02` / `SEL-02` 只能是迁移证明，不是目标态 fan-in store |
| `ActiveEffectLifecycleOwnerSystems.cs` | `rg --files` 当前未发现该文件，manifest / validation report / generated schedule type-name 列表也未登记该文件或其 systems | 当前不能再把它写成磁盘 companion owner；R5 仍需把它列入防回流扫描和历史口径清理项，防止重新生成、移动到别处或改名回流 |
| `RuntimeActiveEffect.gen.cs` | 当前只是 12 行级 `RuntimePureGlue` marker；source attribute snapshot gather、pre-tick job、mutation gather/apply helper、magnitude counter、cleanup / granted ability / tag / event helper 已迁到手写 Runtime owner | `.gen.cs` 收成 marker 是正向事实；R5/R7 的剩余审查对象转为手写 `GASActiveEffectRuntime.cs` / `GEActiveEffectLifecycleSystems.cs`、manifest/report/file/schedule 对账和 stale generated lifecycle 防回流 |
| API health | Runtime `SystemAPI.Query=0`、`EntityManager.CreateEntityQuery=0`、`state.Dependency.Complete=0`、`.Run(` 静态命中 0；`ToEntityArray` 可执行运行调用仍集中在 Debugger observation 两处和 Cue managed boundary 一处；`new EntityQueryDesc` Runtime 命中 19 处，分布在 13 个文件 | 当前已经不是主线程 foreach 旧形态，但 query owner、materialization owner 和 stored query cost 仍要分 Core / Boundary / Diagnostics；不能把静态 0 命中写成 DOTS 性能优秀 |

### 2026-06-08 三次校准：整体 owner map 事实

本次用 codedb 当前索引继续校准整体架构，不引入新目标态设计。新增事实如下：

| 审查面 | 当前证据 | 事实判定 |
|---|---|---|
| Runtime dependency graph | `codedb_module_map path_prefix=Assets/GAS/Runtime` 返回主社群 146 files / 2227 indexed symbols，internal edges 604、boundary edges 133、incoming 101、outgoing 32；中心文件覆盖 Runtime archetype、ActiveEffectStore、Definition table、Debugger、ScheduleContract、Config registry、QueryLayoutPlan、tag component 和 command resolve | Runtime 已有 DOTS backbone，但不是按 Shell / Boundary / Core / Definition / Diagnostics 切开的深 Module；后续不能用移动文件夹、改 namespace 或 facade 名称替代 dependency graph 证据 |
| Shell consumers | `GASRuntimeShell.cs` imported_by 图依赖当前为 `AutoChessGasRuntimeAccess.cs`、Editor `GASWatcher`、Runtime `AbilitySystemBinding` 3 个文件 | Shell 消费面仍比“业务层外壳”更宽；它横跨 Demo adapter wrapper、Editor watcher 和 runtime binding，是 R1/R6 的当前事实输入 |
| Command resolve depth | `ASCCommandBufferResolveSystem.cs` depends_on 18 个 runtime 文件，覆盖 Ability、ASC、Attribute、Definition、Event、Archetype、Tag 等目录 | 它是 job 化正向面，但 also 是 lane 过宽事实；目标重划分必须继续拆 command / ability / tag / attribute / fact owner，而不是只保留一个更大的 command resolver |
| ActiveEffect lifecycle depth | `RuntimeActiveEffect.gen.cs` 当前只是 12 行级 marker，active-effect snapshot、mutation、pre-tick、magnitude、cleanup/grant/tag/event helper 主体已迁到手写 Runtime 文件；`GASSystemScheduleContract` 直接注册手写 `GASActiveEffectPreTickSystem`、`GASActiveEffectRemoveSystem` 和 `GASActiveEffectMutationApplySystem` | active effect lifecycle 主体已退出 SourceGenerator 输出和 generated runtime 文件面；R5/R7 仍必须把 hand-written lifecycle owner、manifest/report/file/schedule 对账、system budget、catalog lifetime / dispose owner 证据和防回流 gate 拆清 |
| Debugger depth | `GasRuntimeDebugger.cs` 4156 行，覆盖 diagnostics event、observation materialization、magnitude source counters、runtime core counters、owner-local fact counters、official diff/export 读面 | Debugger 已是核心 evidence owner；但其 interface 深度应体现在 evidence tier 和 cost domain，不应变成 command / snapshot / gameplay decision 的共享 seam |

### 当前职责重新划分事实

| 当前现实 owner | 证据 | 事实判定 | 目标态差距 |
|---|---|---|---|
| Bootstrap / Session | `GASManager.Initialize(...)` 创建 World、5 段 group、GlobalTimer、SpecStream、ActiveEffectGlobalIndex、EventBus、Replay sink 和 Debugger singleton；`Shutdown()` reset cache / dispose world | 当前 OOP bootstrap 是 runtime session owner 的现实实现，不是 Gameplay Core 计算层 | 目标态 public session seam 只能暴露 install / fixed tick / dispose / evidence，不暴露 `World`、`EntityManager`、singleton entity |
| Schedule backbone | `GASSystemScheduleContract` 固定 5 段 physical group；当前直接注册 hand-written Runtime system arrays，`GeneratedCommandResolveSystemTypeNames` 与 `GeneratedCoreSimulationSystemTypeNames` 均为空数组；missing generated type fail-fast 代码仍作为防回流机制存在 | Backbone 方向正确，generated type-name registry 不再是当前主调度来源；system 数量、phase budget、type mismatch / assembly unavailable 负例仍需交还 | 目标态 SourceGenerator 不应生成 lifecycle system；任何新增 generated registration helper 默认失败 |
| Shell capability | `GASRuntimeShell` 同时解析 World、`EntityManager`、command port、read model、singleton、presentation binding 和 job drain；直接消费者含 `AutoChessGasRuntimeAccess`、Editor watcher、AbilitySystemBinding，AutoChess host / lifecycle / observation / catalog / runner sync 通过 wrapper 间接消费 | 外部 public 面已有收窄，AutoChess direct import 已集中，但 assembly 内仍是多能力 ECS 句柄 facade | 必须拆成 bootstrap、command write、snapshot read、diagnostics/export、definition/catalog、runner sync 六类 capability，并分别计时 / 授权 / 验收 |
| Core stream / fan-in | `EffectCommandSpecStream` 当前只走 cached registered owner，不再 fallback query；但 command / set-by-caller 仍围绕 singleton DynamicBuffer owner，spec / delta / mutation / fact 已形成 owner-local 与 stream export 混合 carrier；frame counter、writer、pressure evidence 和 projection phase 仍分散在相邻 owner | fallback query 风险已退场，carrier 仍是 `MigrationProofOnly` | 按 `SEL-01/02`、`BUF-02`、`NAT-03`，不同数据性质要拆为 `NativeStream` deterministic merge、owner-local range 或 bounded buffer |
| Owner-local spec / fact lane | `GEEffectSpecBuffer` 已退出 required singleton stream buffer；hand-written `GEEffectInstantSystems.cs` 会按 ASC owner 写 instant spec / set-by-caller，并以 owner chunk `IJobChunk` reduce/apply；`OwnerLocalGameplayFactBuffer` 纳入 ASC archetype / factory，`ASCCommandBufferResolveSystem`、`GASAttributeModifierDeltaApplySystem`、hand-written instant reduce 和 `GEExecutionCalculationOutputModifierSystem` 均可追加 owner-local fact；`GameplayOwnerLocalFactFlushSystem` 排序 flush 到现有 stream export；Debugger export owner-local spec / fact count | 这是 owner-local spec / fact 正向扩展切片，证明 spec/reduce 与多类 Attribute / ASC command fact 不必直接写 singleton carrier；但 active-effect hand-written owner、generated marker 对账和防回流仍需按 `MigrationProofOnly` 退出门审查 | 目标态还需要 command/spec/delta/fact 全链路 owner-local 或 NativeStream carrier、BoundaryObservationFact export、capacity / spill / timing split；当前 flush 回 stream 仍是迁移出口 |
| Generated runtime | generated catalog lookup / Blob builder 是正向定义链；`RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs` 与 `RuntimeActiveEffect.gen.cs` 都是 `RuntimePureGlue` marker；`ActiveEffectLifecycleOwnerSystems.cs` 当前磁盘缺失 | Generated 链路已经反哺 Runtime，active-effect 主体也已转向手写 Runtime owner；当前风险不再是 manifest lifecycle artifact，而是 pure glue / hand-written resolver 对账、catalog lifetime / dispose owner 证据、system budget、release-ready gate 和 generated lifecycle 防回流 | SourceGenerator 目标只生成 immutable catalog、static lookup、pure record、validation / Editor binding；不能继续拥有 query、ECB、NativeContainer 或 lifecycle |
| Debugger / Observation | `GasRuntimeDebugger` singleton lookup 已是 registered/cache owner；`ToEntityArray` 运行调用集中在 Debugger observation 两处与 Cue managed boundary 一处；`ExportToText` 是 derived export；observation materialization 已进入 snapshot 和 AutoChess evidence。2026-06-08 最新 x50 已拆 performance / diagnostic pass：performance summary 为 `performancePassObservationPollutionRisks=0`，diagnostic Debugger 仍显示 `observationMaterializedQueries=12` | Debugger 可作为 evidence owner；observation materialization 成本已能独立归因，performance pass 污染风险已隔离，但 Profiler enabled 与规模性能闭环未完成 | 按 `DBG-01..05`，contract counter、runtime counter、official capture、validation evidence、derived export 必须分层；不能用字符串日志或 disabled profiler 状态证明 Core 性能 |
| Cue / Presentation | `CueManagedLifecycleSystem` 位于 `GASBoundaryProjectionSystemGroup`，使用 UnityEngine `Time.time` 和 `ToEntityArray` 处理 managed cue lifecycle | 这是 Boundary / Presentation 成本，不是 Runtime Core hot path | 目标态允许 managed boundary，但必须从 CoreSimulation timing 与 DOTS hot path 结论中拆出 |
| AutoChess adapter | `AutoChessGasRuntimeHost` 通过 Shell 初始化 runtime / catalog / tick group；`AutoChessGasObservationGateway` 直接 reset singleton、创建 diagnostics snapshot；`AutoChessGasBattleEntityLifecycle` 用 registry 保存 `ASCHandle` 并创建 driver / unit，driver public handle 已为 opaque id/version | AutoChess 是业务验收 Shell，当前 Thin Adapter 未完成；其 direct ECS 句柄仍是内部实现事实 | 目标态 Battle Runtime Adapter 只暴露业务动作、opaque handle、snapshot 和 evidence；driver runtime store 内部 `_driverEntity`、singleton reset、job drain 必须被 capability 分类并从 Core 性能结论中排除 |

### 官方规则判定

| 规则 | 对本截面的判定 |
|---|---|
| `SYS-01` / `SYS-02` | 5 段 group 与多数 hot path job 化是正向事实；但 `GASManager` / `GASRuntimeShell` 仍是 bootstrap 与 boundary 句柄聚合 owner，不能写成 OOP gameplay 中间层合格。 |
| `QRY-01` / `PRF-05` / `PRF-33` | Runtime 可执行 `SystemAPI.Query`、`.Run()`、`EntityManager.CreateEntityQuery` 已为 0，这是静态正向截面；剩余 `ToEntityArray` 必须按 Debugger / Boundary 分类，而不是平均进 Core tick。 |
| `SEL-01` / `SEL-02` / `BUF-02` / `NAT-03` | singleton stream fallback query 退场和 owner-local Attribute fact 切片都不等于 stream carrier 达标；command/spec/delta/fact/mutation 必须继续按数据性质拆 carrier，并给出 allocator owner、merge order、capacity / spill evidence。 |
| `DBG-01..05` / `SYS-04` | Debugger 已能采样和导出，但必须把 observation materialization、official tool disabled reason、derived string export 与 Core counters 分层；否则 AutoChess x50 跑通不能证明 DOTS 优秀水平。 |
| `BLOB-01` / `BLOB-02` / `BUR-01` | generated catalog / lookup 方向正确；generated lifecycle system 当前已退出物理输出面，但 runtime-visible generated artifact、lookup memory、catalog lifetime 和防回流 gate 仍必须按 `MigrationProofOnly` 退出门审查，不得在目标态中扩大。 |

### 本轮结论

当前 GAS 已有可继续演进的 DOTS backbone，但“纯血 ECS GamePlay Core”还没有完成。最新事实不支持继续建设更大的 OOP 中间层，也不支持把 generated lifecycle 当成 SourceGenerator 成熟形态。正确的重新划分应把 `GASManager` / `GASRuntimeShell` 降为 bootstrap 与 Boundary capability implementation，把 `GEEffectCommandSpecStream` 降为迁移期 carrier，把 owner-local fact lane 扩展为真正的 CoreReactionFact / BoundaryObservationFact 分离，把 generated runtime lifecycle 回流风险按 `MigrationProofOnly` 退出门阻断，把 Debugger / Cue / AutoChess observation 从 Core performance pass 中拆出。

后续任何任务声称“Runtime Core 达标”，必须至少同时提交：owner 分类后的 API health、Core / Boundary / Debugger / Runner timing split、stream carrier pressure、structural playback official evidence、generated lifecycle 退出或接管表，以及 AutoChess x50 之外的 scale profile。缺任一项都只能写成阶段性迁移事实。

### 本轮代码级 owner 重划分

本轮不再只按“Runtime / Generated / AutoChess / Debugger”目录归类，而是按调用方必须知道多少实现细节来判定 owner 深度。当前代码已经把部分复杂度集中到可保留 owner 中，但还没有把 public / internal interface 切到目标态。

| 目标 owner 名称 | 当前对应现实模块 | 代码级事实 | 本轮判定 |
|---|---|---|---|
| `RuntimeSession` | `GASManager`、`GASSystemScheduleContract`、AutoChess runtime host | World 创建、SystemGroup install、catalog install、singleton bootstrap 和 shutdown cache reset 已集中 | 保留为 bootstrap implementation；但 Shell / Demo 不能继续通过它直接取得 ECS handle |
| `RunnerSync` | `GASRuntimeShell.TryDrainRuntimeJobs()`、AutoChess runtime ticker | 手动 `CompleteAllTrackedJobs()` 只剩 runner / measurement 入口 | 只能归 runner sync / diagnostics measurement；不能作为 Core 性能健康证明 |
| `CommandPort` | `ASCCommandPort`、`ASCCommandGateway`、`AbilitySystemBinding`、`ASCCommandBufferResolveSystem` | command 写入口已经集中，但 Boundary implementation 仍直接追加 live buffer，Core resolve job 同时处理多类语义 | 继续保留为迁移期 command interface；目标态需拆出 request validation、owner-local append 和 core command resolve lane |
| `SnapshotReadModel` | `ASCReadModel`、AutoChess structured snapshot projector、structured log export | snapshot 已从 live read 迁到 structured log / report key 的部分链路，但 Shell capture 仍可从 ECS 直接构造 read model | 正向切片成立；仍需统一 BoundaryProjection / snapshot ring 和 version cursor |
| `DiagnosticsSink` | `GasRuntimeDebugger`、`DiagnosticsSnapshotSystem`、`GasStructuredLogExporter`、AutoChess observation gateway | runtime counter 与 derived export 并存；observation materialization 已能计数，但 exporter 仍是托管派生消费 | Debugger 继续作为 evidence owner；derived export / observation pass 必须从 Core timing 中隔离 |
| `DefinitionCatalogLifetime` | generated `DefinitionCatalog`、runtime catalog component、AutoChess catalog session | Blob catalog / static lookup 正向存在；install/dispose owner 仍和 runtime host / bootstrap builder 混合 | catalog lifetime 要独立于 command、snapshot、runner sync；generated lifecycle 不得借 catalog 名义回流 |
| `GASFrameKernel` | handwritten `ISystem`、stream owner contracts、generated pure glue / marker | 多数 hot path 已 job 化，且 query / lookup owner 能静态追踪；generated runtime lifecycle / ECB 当前已退出 report 和磁盘 companion 面 | 保留 handwritten lane，阻断 generated lifecycle 回流；所有 lane 必须显式声明 query、lookup、allocator、dependency 和 evidence |
| `EffectFanInStore` | `GEEffectCommandSpecStream`、execution output typed fact NativeStream 切片 | stream owner contract 已把 singleton carrier 标记为 migration；局部 NativeStream 切片不能覆盖全部 command/spec/delta/fact/mutation | singleton carrier 继续按 proof-only 处理；scale-ready 必须补 deterministic merge、spill、segment、allocator 和 battle hash |
| `ActiveEffectStore` | `ActiveEffectStore`、hand-written `GASActiveEffectRuntime` / `GEActiveEffectLifecycleSystems`、`EffectRuntimeUtility` | owner-local slot 和 global index 方向正向；lifecycle helper、slot tick、source magnitude snapshot 已向手写 Core lane 集中，但 capacity / spill / 非零样本和规模 profile 未闭合 | Store owner 可以保留；lifecycle 应保持手写 Core lane，helper 只作迁移兼容 |
| `StructuralCommit` | `Begin/EndGASStructuralCommitECBSystem`、Core ECB 写入点 | 结构变化 gate 已存在；多个系统仍可拿到 ECB 并写 structural intent / create / destroy | 保留唯一 playback phase；后续必须给出 source TopN、official diff 和 generated ECB 退出证据 |

本轮 owner 结论：不要再把 `GASRuntimeShell` 扩成更大的 Application Shell，也不要把 SourceGenerator 扩成更大的 Runtime Core。正确方向是让每个 owner 拥有自己的 query、lookup、allocator、dependency、carrier、capacity、structural playback 和 evidence，并让 Shell 只看到业务 intent、opaque handle、snapshot 和 evidence。

### 2026-06-08 四次校准：Shell / Catalog / Runner capability 事实

本次继续用 codedb live 截面复核 Shell capability、RuntimeSession、DefinitionCatalogLifetime 和 RunnerSync。当前 codedb 状态为 434 files / 434 outlines / 766 chunks / graph 448 nodes / 1831 edges / 34 communities / scan ready。该数字只代表本次 live 截面，后续领取任务仍必须重跑 codedb。

| 审查对象 | 本次代码证据 | 当前事实判定 |
|---|---|---|
| `GASRuntimeShell` 注释与实现 | `GASRuntimeShell.cs:6-8` 注释称其为 Thin OOP shell；但 `:16-47` 返回 runtime `World` / `EntityManager`，`:49-117` 创建 command port 并可返回 `EntityManager`，`:119-133` 直接用 `EntityManager` 构造 `ASCReadModel`，`:141-147` 调用 `CompleteAllTrackedJobs()`，`:150-166` 处理 presentation bind，`:168-233` 解析 GlobalTimer / EventBus / EventLogSink / RuntimeDebugger singleton，`:236-274` 解析 runtime singleton / ASC raw entity | 注释目标方向正确，但当前实现仍是多 capability ECS handle facade；不能把 `GASRuntimeShell` 当前形态写成 Thin Shell 完成。R1/R6 必须继续拆 RuntimeSession、CommandPort、SnapshotReadModel、DiagnosticsSink、RunnerSync、DefinitionCatalogLifetime 和 PresentationBridge |
| `GASManager` RuntimeSession | `GASManager.cs:43-67` 创建 World、SystemGroup、GlobalTimer、EffectCommandSpecStream、ActiveEffectGlobalIndex、EventBus、EventLogSink、RuntimeDebugger；`:79-110` shutdown reset frame context、Debugger singleton、stream singleton、ActiveEffectStore global index、GameplayEffectConfigRegistry cache 和 presentation binding，并 dispose World | RuntimeSession / bootstrap owner 已集中，这是保留面；但 `EntityManager` 和 singleton entity 仍被 Shell / Adapter internal capability 继续解析，不能作为 Application Shell public seam |
| AutoChess runtime host | `AutoChessGasRuntimeHost.cs:8-17` 初始化 GAS runtime、通过 access wrapper 取得 World 注册 AutoChess systems，并调用 catalog session install；`:19-29` uninstall catalog、reset bootstrap / lifecycle cache 后 shutdown；`:31-63` 通过 World 取得 5 段 tick group，并通过 wrapper drain jobs | Host 聚合 runtime bootstrap、system registration、catalog install、tick group lookup 和 runner sync；它是 Demo adapter implementation，不是目标态单一 capability seam |
| AutoChess catalog session | `AutoChessGasCatalogSession.cs:8-15` 通过 wrapper 取得 definition `EntityManager` 后 install battle definition catalog 和 driver runtime store；`:18-25` 通过同一路径 uninstall store / catalog | Catalog install / dispose 已从 Host 拆出为正向事实；但仍依赖 wrapper 返回 `EntityManager`，DefinitionCatalogLifetime 还没有形成 opaque catalog handle、schema/version evidence 和独立 dispose result |
| AutoChess runner sync | `AutoChessGasRuntimeTicker.cs:8-39` 逐段 update 5 个 GAS SystemGroup，recordTiming 时再 `CompleteRuntimeJobsTimed()`；`:53-57` 通过 Host -> wrapper -> Shell 调用 `CompleteAllTrackedJobs()`，并把 drain ticks 写入 runtime timing | RunnerSync 成本已有独立 timing 字段，是正向事实；但 dependency drain 仍通过 Shell facade 解析 `EntityManager`，必须继续作为 runner / diagnostics measurement cost，不能进入 CoreSimulation hot path 结论 |
| AutoChess runtime access wrapper | `AutoChessGasRuntimeAccess.cs:8-73` 分别代理 session world、definition / lifecycle entity manager、diagnostics world、diagnostics GlobalTimer / EventBus / EventLogSink / RuntimeDebugger、battle unit command port 和 runner job drain | Wrapper 降低了 AutoChess direct import 面，但仍把多个 capability 聚到一个 internal access layer；它是迁移期收口点，不是 Thin Adapter、RuntimeSession、DefinitionCatalogLifetime 或 RunnerSync 完成证明 |

本次校准后的新增事实结论：`GASRuntimeShell` 的命名和注释已经表达目标方向，但当前实现仍泄露多个 capability 的 ECS handle；`AutoChessGasRuntimeAccess` 的存在只证明 direct import 集中化，不证明 capability 分级。下一步 R1/R6 交还必须输出 capability access matrix：每个方法对应的目标 owner、是否返回 `World` / `EntityManager` / singleton / raw `Entity`、调用方、timing domain、evidence 字段和退出门。

### 2026-06-08 六次校准：AutoChess Runtime Access / RunnerSync capability 事实

本次只复核 Runtime Access wrapper、RunnerSync、DefinitionCatalogLifetime 与 Diagnostics capability 的当前实现面。codedb 默认索引当前为 434 files / 434 outlines / 766 chunks / graph 448 nodes / 1831 edges / 34 communities / scan ready；Runtime path-prefix 主社群为 146 files / 2227 indexed symbols，dependency edges 为 internal 604、boundary 133、incoming 101、outgoing 32。该数字只代表本轮 `00` 事实截面，后续领取任务仍必须重跑。

| 审查对象 | 当前代码证据 | 当前事实判定 |
|---|---|---|
| `GASRuntimeShell` | `Assets/GAS/Runtime/General/GASRuntimeShell.cs:16-47` 解析 runtime `World` / `EntityManager`，`:49-117` 创建 command port，`:119-133` 构造 live read model，`:141-147` drain jobs，`:150-166` presentation bind，`:175-233` 解析 GlobalTimer / EventBus / EventLogSink / RuntimeDebugger singleton | 当前仍是多 capability ECS handle facade；可作为迁移期收口点，但不能写成 Thin Shell 完成 |
| `GASManager` | `Assets/GAS/Runtime/General/GASManager.cs:43-67` 创建 World、SystemGroup、GlobalTimer、SpecStream、ActiveEffectGlobalIndex、EventBus、EventLogSink、RuntimeDebugger；`:79-110` shutdown 并 reset cache / singleton / binding | RuntimeSession / bootstrap owner 已集中；但不应成为业务 Shell 可取得 `EntityManager` 或 singleton 的 public seam |
| `AutoChessGasRuntimeAccess` | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeAccess.cs:8-73` 代理 session world、definition / lifecycle `EntityManager`、diagnostics world / singleton、battle command port 和 runner job drain | 它降低 direct import 面，但仍把 RuntimeSession、DefinitionCatalogLifetime、DiagnosticsSink、CommandPort 和 RunnerSync 聚到同一 access layer；这是迁移期 access concentrator |
| `AutoChessGasRuntimeHost` | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeHost.cs:8-17` 初始化 runtime 并注册 systems，`:31-57` 通过 World 获取 5 段 tick group，`:60-63` 通过 wrapper drain jobs | Host 是 Demo adapter implementation，当前同时承载 bootstrap、system registration、tick group lookup 和 runner sync 消费；不能作为单一目标 capability seam |
| `AutoChessGasCatalogSession` | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasCatalogSession.cs:8-25` 通过 wrapper 取得 definition `EntityManager` 后 install / uninstall catalog 与 driver runtime store | 从 Host 拆出 catalog lifetime 是正向集中；但仍缺 opaque catalog handle、schema/version evidence、release result 和独立 dispose owner |
| `AutoChessGasObservationGateway` | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasObservationGateway.cs:19-57` reset diagnostics singleton，`:60-82` 创建 structured log / runtime diagnostics snapshot，`:85-198` 记录 group / owner split timing | Diagnostics evidence 已有独立消费面；但 reset、snapshot 和 timing 仍依赖 wrapper 返回 singleton / `EntityManager`，不能和 Core hot path 性能口径混算 |
| `AutoChessGasRuntimeTicker` | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeTicker.cs:8-39` 逐段 update 5 个 GAS group，recordTiming 时才 `CompleteRuntimeJobsTimed()`；`:53-57` 通过 Host -> wrapper -> Shell 调用 `CompleteAllTrackedJobs()` | RunnerSync 成本已有 `dependencyDrainTicks` 字段，是正向事实；但 drain 仍通过 Shell facade，应归 runner / diagnostics measurement，不得写进 CoreSimulation 优秀结论 |

本次 facts 进一步确认：wrapper 集中化不是 Thin Adapter 完成态。后续 R1/R6 交还时，必须把 `RuntimeSession`、`CommandPort`、`SnapshotReadModel`、`DiagnosticsSink`、`RunnerSync`、`DefinitionCatalogLifetime` 分别列出 public seam、internal ECS handle scope、timing domain、required evidence、forbidden reuse 和退出任务。若只把所有 Shell 调用集中到一个 wrapper，最多算 direct import 收口事实。

### 2026-06-08 七次校准：Structured Evidence / Presentation Boundary 事实

本次复核 Debugger、Replay、Presentation outbox、structured log export、Cue managed lifecycle 和 AutoChess observation gateway。codedb 默认索引当前为 434 files / 434 outlines / 766 chunks / graph 448 nodes / 1831 edges / 34 communities / scan ready；Runtime path-prefix 主社群为 146 files / 2227 indexed symbols，dependency edges 为 internal 604、boundary 133、incoming 101、outgoing 32。该数字只代表本轮 `00` 事实截面，后续领取任务仍必须重跑。

| 审查对象 | 当前代码证据 | 当前事实判定 |
|---|---|---|
| Runtime structured evidence module | `codedb_module_map path_prefix=Assets/GAS/Runtime` 将 `Assets/GAS/Runtime/Event/GasStructuredLogExport.cs`、`GasReplaySinkPolicy.cs`、`GasStructuredLogView.cs` 识别为独立 `structured/gas` 小社群：3 files / 74 indexed symbols，cohesion 0.13，incoming 11 / boundary 14 / outgoing 3 | structured log 已形成独立 derived export 面，但 cohesion 低且依赖 AutoChess report consumer；它是 Boundary / validation asset，不是 Runtime Core 计算 owner |
| `PresentationOutboxProjectionSystem` | `Assets/GAS/Runtime/System/Event/PresentationOutboxProjectionSystem.cs:10-13` 位于 `GASBoundaryProjectionSystemGroup`，`:22-52` 从 `GameplayEventBusComponent` 取 `BoundaryObservationFactBuffer`，`:66-90` 用 processed cursor 投影，`:100-228` 将 Attribute / Cue / Tag / Damage / generic fact 转为 `PresentationEventBuffer`，`:236-255` 通过 `EventBusHelper.AppendPresentationEvent(...)` 写 outbox | Presentation outbox 已从 Core fact 派生，是正向 BoundaryProjection；但仍通过 singleton event bus / `EntityManager` / global outbox 写入，属于 Boundary migration owner，不是 CoreReactionFact 或 scale-ready carrier |
| `ReplayLogSystem` / replay sink | `ReplayLogSystem.cs:9-12` 位于 `GASBoundaryProjectionSystemGroup`，`:20-52` 读取 event bus 和 log sink，`:64-87` 从 `BoundaryObservationFactBuffer` 逐条投影 replay event，`:233-239` 分配 log index；`GasReplaySinkPolicy.cs:142-159` 按 `MaxRetainedEvents` 截断并累计 `DroppedEventCount` | Replay sink 已有 cursor / retention / dropped count，这是正向 evidence；但 replay 仍是 BoundaryObservationFact 的派生 sink，不能反向驱动 simulation，也不能替代 Profiler / Journaling / Core counters |
| `GasStructuredLogExporter` | `GasStructuredLogExport.cs:306-366` 可从 `ReplayLogEventBuffer` 创建 snapshot，`:368-399` 用 `StringBuilder` / file IO 导出文本，`:419-457` 用 managed `List` 聚合 entries，`:460-473` 可通过 `EntityManager` 读取 `ASCBoundaryReportKeyComponent` 解析 report key | structured log export 是 derived export；解析 report key 是 Boundary export 行为。它适合作为 AutoChess report / assertion source，但不能作为 Core hot path 机器 evidence source，也不能进入 performance pass timing |
| `CueManagedLifecycleSystem` | `Assets/GAS/Runtime/System/Cue/CueManagedLifecycleSystem.cs:11-13` 位于 `GASBoundaryProjectionSystemGroup`，`:19-28` 创建 cue query，`:31-47` 用 `Time.time`、`_cueQuery.ToEntityArray(Allocator.Temp)`、managed Cue 回调处理 request / start / tick / end / destroy，`:153-154` destroy 时调用 `GASRuntimeEntityArchetypes.DeactivateCueEntity(...)` | Cue runtime 是 managed Boundary / Presentation 成本，不是 Core hot path。它当前仍有运行时 `ToEntityArray` 和 managed callback，必须在 evidence 中单独标为 PresentationBoundary cost domain |
| Debugger observation materialization | `GasRuntimeDebugger.cs:2299-2337` 记录 `ObservationMaterialization`，Module 为 `Presentation`，BufferName 为 `ToEntityArray`，写入 query/entity/elapsed/pollution risk 字段；静态扫描当前 Event / Cue / Debugger 面 `ToEntityArray` 运行命中为 3：Debugger active-effect-store、Debugger presentation-outbox、Cue managed boundary | observation 成本已经能被单独计数，这是正向事实；但任何保留的 materialization 都不能混入 CoreSimulation 性能结论。R4/R8 必须继续证明 performance pass 与 diagnostic pass 分离 |
| `AutoChessGasObservationGateway` | `AutoChessGasObservationGateway.cs:19-57` reset GlobalTimer / EventBus / EventLogSink / RuntimeDebugger singleton，`:60-82` 创建 structured log snapshot、assertion text、runtime diagnostic snapshot 和 diagnostic text，`:85-198` 分 PhysicalGroup 和 OwnerSplit 记录 timing | AutoChess observation 已能消费 structured evidence 与 timing split；但 reset / snapshot / export 仍依赖 `AutoChessGasRuntimeAccess` 返回 diagnostics `EntityManager` / singleton，属于 DiagnosticsSink implementation，不是业务 public seam 或 Core hot path |

本次校准后的新增事实结论：

1. `BoundaryObservationFact -> PresentationOutbox / ReplaySink / StructuredLog` 的方向是正向资产；它使 AutoChess report、assertion text、中文日志和图表能够从同一 evidence 派生。
2. 当前 Presentation / Replay 仍以 singleton event bus 和 `EntityManager` append 为中心，属于 Boundary migration owner；它不能回流成 Core reaction 输入，也不能当作 scale-ready fact carrier。
3. `GasStructuredLogExporter` 和 Debugger text export 是 managed derived export，适合离线 / validation pass 消费；任何文件 IO、字符串拼接、report-key live resolve 都不能进入 Core performance pass。
4. `CueManagedLifecycleSystem` 的 `Time.time`、`ToEntityArray` 和 managed Cue callback 是明确的 PresentationBoundary 成本；它存在不影响 Core 已 ECS 化事实，但必须从 DOTS 性能优秀结论中排除或单独计量。
5. R4/R6/R8 后续交还必须输出 presentation/replay/structured-log owner 表：cursor lag、dropped count、projection count、derived export byte count、report-key resolve count、observation materialization overhead、performance / diagnostic pass 归属和 disabled reason。

### 本轮重新划分事实结论

当前代码中最接近目标态的不是 Shell，而是 physical backbone、chunk-local applicator、registered owner、generated immutable catalog 和 structured evidence。当前最需要治理的也不是“是否 ECS 化”，而是这些深 owner 之外仍残留的浅 interface：

1. Shell shallow interface：`GASRuntimeShell` 的源码注释称其是 Thin OOP shell，但当前实现仍是多个 capability 共用的 ECS handle resolver。注释不能覆盖事实，后续 R1/R6 必须按 capability 拆读写授权和 timing 归因。
2. Carrier shallow interface：`GEEffectCommandSpecStream` 隐藏了多类数据的容量、清理、合并和 pressure counter。它现在适合作为迁移 evidence owner，不适合作为目标态统一总线。
3. Generated shallow interface：`GasGlueCodeGenPhases` 同时承载 pure definition glue、marker、catalog、validation 和 boundary classifier 等多类模板 / 规则；manifest 当前已经没有 `RuntimeLifecycleMigration` artifact，generated runtime 物理目录也不再有 active-effect companion 文件。目标态只能继承 pure glue / marker / validation 这类无 lifecycle owner 胶水；任何 generated lifecycle / query / ECB / NativeContainer owner 回流都必须被 R5 gate 阻断。
4. Evidence shallow interface：`GasRuntimeDebugger` 已有足够多 counter，但 evidence、official capture、validation report、derived export 和 observation materialization 成本必须继续分层消费。

因此，本轮事实重划分把“目标态设计应当如何拆”交给 `01/16`，把“当前代码哪里仍混在一起”保留在本文件；二者不能互相替代。

## 2026-06-07 整体架构复核：Owner 重划分事实

### 事实消费卡

```markdown
来源类型：2026-06-07 历史整体架构审查 / codedb 截面 / DOTS 官方规则对照
原始证据：
  - 历史 `codedb_status`: 428 files / scan ready；当前 2026-06-08 live 截面已更新为 434 files / 434 outlines / 766 chunks / graph 448 nodes / 1831 edges / 34 communities
  - 历史 `codedb_module_map`: `Assets/GAS/Runtime` 主要社群 109 files / 1253 indexed symbols；当前 Runtime 主社群为 146 files / 2227 indexed symbols
  - `Assets/GAS/Runtime/General/GASRuntimeShell.cs:11-279`
  - `Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs:99-434`
  - `Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs:483-698`
  - `Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs:7-421`
  - `Assets/GAS/Runtime/Effect/Component/Dynamic/ActiveEffectStore.cs:2260-2292`
  - `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs:882-895`
  - `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs` 当前为 5556 行级大聚合
  - `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` 当前为 12 行级 marker；active-effect 主体已转到 `GASActiveEffectRuntime.cs` / `GEActiveEffectLifecycleSystems.cs`
  - `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeHost.cs:8-65`
  - `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasObservationGateway.cs:19-168`
  - `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasBattleEntityLifecycle.cs:14-165`
  - `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasBattleUnitSnapshotProjector.cs:8-56`
第一 owner：00
长期有效：待复核；随着 R1/R3/R4/R5/R6/R7 执行变化
需要反哺：01 目标态 Owner Map、02 任务领取前截面、04 最近验证摘要
```

### 当前 Owner 错位

| 当前 Owner / 现实模块 | 正向事实 | 仍然错位的职责 | DOTS 判定 |
|---|---|---|---|
| `GASRuntimeShell` | 对外 public 面已主要集中到 command port / read model；World、`EntityManager`、runtime singleton 和 job drain 多为 internal | 同一 facade 仍同时服务 bootstrap、command、snapshot、diagnostics、presentation binding、runtime singleton 和 dependency drain | 不符合目标态 Shell capability 分级；属于 R1/R6 收权事实 |
| `GASSystemScheduleContract` | 5 段 physical group 已固定；当前直接注册 hand-written Runtime system arrays，`GeneratedCommandResolveSystemTypeNames` 与 `GeneratedCoreSimulationSystemTypeNames` 均为空数组；缺失 generated type fail-fast 仅作为防回流机制保留 | system 数量、phase budget、缺失 artifact / type mismatch / assembly unavailable 负例验证仍需交还；stale generated lifecycle 回流仍需扫描 | physical backbone 成立，generated type-name registry 不再是当前主调度来源；后续不能再把 7 个 generated runtime systems 写成当前注册事实 |
| `GEEffectCommandSpecStream` | fallback query 已退场，写入需要显式 stream owner；frame prepare 已有 buffer-only path 和 pressure counters；owner-local fact writer / instant / mutation / delta 切片已出现 | command / set-by-caller 仍依赖 singleton stream owner，spec / mutation / delta / fact 是 owner-local 与 stream export 混合承载；capacity / spill / merge order / allocator owner 仍未形成 scale-ready 闭环 | 只能作为 `MigrationProofOnly` carrier，不能作为 scale-ready fan-in |
| `EffectRuntimeUtility` | cleanup、removed event、modifier/tag 移除和 store cleanup 有真实实现 | instant apply、ongoing requirement、stack merge、duration activate/deactivate/reactivate、reject 判定等多处为空或固定返回；`TryGetStaticDefinitionBlob` 固定 false，导致 `ActiveEffectStore.ResolveFlags(...)` 无法从静态 Blob 补齐 granted tag / ability / requirement flag | 不是目标态 Effect lifecycle owner；R7 必须把生命周期回收到明确 system / job / store |
| `GasRuntimeDebugger` | Debugger singleton 已改为 registered/cache owner；structured log / official diff / validation export 已存在；observation materialization 已进入 dedicated counter 和 AutoChess evidence | singleton fallback query 已退场，但 Debugger observation `ToEntityArray`、derived export、official capture 和 runtime counter 分层还需要阈值/消费约束 | Debugger 是 Boundary evidence owner，不能和 Core hot path 成本混算 |
| `GasGlueCodeGenPhases` / generated runtime | validation report 已统计 boundary hit，并把 mode 写成 `blocking-unclassified-lifecycle-migration`；manifest 当前有 4 个 `RuntimePureGlue` artifact、0 个 `RuntimeLifecycleMigration` artifact；`RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs` 与 `RuntimeActiveEffect.gen.cs` 均为 marker / pure glue；`ActiveEffectLifecycleOwnerSystems.cs` 当前磁盘缺失；`GeneratedRuntimeBoundaryHits=0` | classified lifecycle / structural / ownership / random lookup hit 归零不等于 release-ready 完成；仍缺 report/manifest/file/schedule 对账、catalog lifetime / dispose 证据、system budget 和 future generated lifecycle 回流阻断 | SourceGenerator 目标态应退出 runtime lifecycle owner，只保留 immutable catalog / lookup / pure glue / validation；R5 仍需补 release-ready gate |
| AutoChess `Integration/GasCore` | 单位结果已由 structured log snapshot projector 回放 health / energy；report key 正向替代 raw ASC index | Host / ObservationGateway / Lifecycle 仍解析 World、`EntityManager`、runtime singleton、tick group 和 driver store | Demo Adapter 方向正确，但 Thin Adapter 未完成；需要按 bootstrap、catalog、lifecycle、snapshot、diagnostics、runner sync 分 owner |

### 重新划分事实结论

1. 当前最有价值的保留面是 **5 段 FixedStep physical backbone**、**owner chunk applicator**、**structured evidence** 和 **generated immutable catalog**；这些应继续作为目标态设计的输入。
2. 当前最不应继续扩大的面是 **多能力 Shell facade**、**singleton stream carrier**、**EffectRuntimeUtility 生命周期 helper** 和 **generated runtime lifecycle system**；这些都属于迁移期浅 interface。
3. 下一轮事实审查不能再用“用了 ECS / 有 Burst / 没有 `Complete()`”作为完成判断。必须检查 owner 是否隐藏了 query、lookup、allocator、dependency、capacity、merge、structural playback 和 diagnostics 归因。
4. `EntityManager.CreateEntityQuery` 的当前事实已经重新收口为 Runtime 可执行 0 命中；后续任务若重新引入 singleton fallback query，必须由诊断脚本和事实审查同时阻断。
5. AutoChess 业务 report 从 structured evidence 派生是正向进展，但 Host / Observation / Lifecycle 仍不是目标态 OOP Shell；业务层不能长期通过 adapter implementation 间接拥有 live ECS 句柄。

## 下一步事实验证需求

后续执行 Agent 交还时，至少要给出：

1. 静态扫描：`GASManager.EntityManager`、`GASRuntimeShell.TryResolveRuntimeEntityManager`、`state.Dependency.Complete()`、`.Run()`、legacy EventBus writer、generated lifecycle owner。
2. 生成链：`Tools/CodeGen/Generate-GAS-SourceGen.bat` 或等价 CLI，且不手改 `.gen.cs`。
3. 编译域：runtime、generated runtime、AutoChessDemo asmdef 对应 csproj 或 Unity batchmode 编译。
4. 运行证据：AutoChess x50 / x100 / x1000 中至少一档，并输出 core / boundary / debugger / runner timing split。
5. 官方工具证据：`GasRuntimeOfficialToolDiff`、Entities Journaling 或 Profiler disabled reason；不能用内部日志替代官方工具状态。
