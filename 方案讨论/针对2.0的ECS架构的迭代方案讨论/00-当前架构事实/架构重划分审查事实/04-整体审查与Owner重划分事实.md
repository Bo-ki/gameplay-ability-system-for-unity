# 04 整体审查与 Owner 重划分事实

> Owner：00-当前架构事实/架构重划分审查事实 | 状态：当前事实子页 | 拆分来源：../架构重划分审查事实.md | 拆分时间：2026-06-08

承载 2026-06-08 / 2026-06-07 整体审查、Owner 错位、重新划分事实结论和下一步事实验证需求。

本文件只记录当前代码事实、证据和 DOTS 判定；目标态设计正文回到 `../../01-目标态架构共识/`，任务拆分回到 `../../02-主线任务树/`。

## 2026-06-08 整体架构审查：最新事实截面

### 事实消费卡

```markdown
来源类型：整体架构审查 / codedb 截面 / DOTS 官方规则对照
原始证据：
  - `codedb_status`: 429 files / 429 outlines / 767 chunks / graph 443 nodes / 1766 edges / 32 communities / scan ready
  - `codedb_module_map path_prefix=Assets/GAS/Runtime`: Runtime 主社群 141 files / 1968 indexed symbols，Core、Definition、Debugger、Shell、generated runtime 仍聚在同一依赖社群
  - `codedb_deps GASRuntimeShell.cs imported_by`: AutoChess lifecycle / observation / runtime host、Editor GASWatcher、AbilitySystemBinding 共 5 个直接消费者
  - `codedb_outline GASRuntimeShell.cs`: 281 lines，包含 runtime world / entity manager、command port、read model capture、job drain、presentation bind、runtime singleton resolver
  - `codedb_outline GEEffectCommandSpecStream.cs`: 1118 lines，集中 singleton carrier、frame-local counter、command/spec/delta/mutation/fact buffer 和 merge helper
  - `codedb_outline GasRuntimeDebugger.cs`: 4092 lines，集中 diagnostic event、runtime counter、observation materialization、magnitude source evidence 和 derived export
  - `codedb_deps GASSystemScheduleContract.cs imported_by`: GASManager、AutoChessRuntimeSystemBootstrap、FrameBudget / StreamOwner / EvidenceGate / Rebind contract 共 6 个直接消费者
  - `codedb_text_search Assets/GAS/Runtime/**/*.cs`: `CreateEntityQuery=0`、`SystemAPI.Query=0`、`state.Dependency.Complete=0`、`.Run(=0`、`CompleteAllTrackedJobs=1`、`ToEntityArray` 可执行运行调用 3 处
  - `codedb_text_search Assets/GAS/Generated/CodeGen/Runtime/**/*.cs`: generated runtime `: ISystem=7`、`OnUpdate(ref SystemState)=7`，并存在 `ComponentLookup` / `BufferLookup` / `EntityCommandBuffer`
  - `2026-06-08-AutoChessBattleValidation-ObservationMaterialization-Run1.log`: `completed=True`、`passed=True`、`runtimeChainPassed=True`、`repeatRunPassed=True`、`runtimeObservationMaterialization|queries=12|entities=2400|elapsedUs=92|performancePollutionRisks=12`
第一 owner：00
长期有效：待复核；本截面会随着 R1/R3/R4/R5/R6/R7 实现变化
需要反哺：01 owner map / target invariant，02 R 任务领取前截面，04 最近验证摘要
```

续轮校准：当前 codedb 状态为 429 files / 429 outlines / 767 chunks / scan ready。`GASRuntimeShell.cs` 仍有 5 个直接消费者：AutoChess lifecycle、observation、runtime host、Editor `GASWatcher` 和 `AbilitySystemBinding`。本次数字校准只更新事实截面，不改变“多能力 Shell facade、generated lifecycle、singleton carrier 和 Debugger / Cue observation cost 仍需 owner 分类”的判定。

### 2026-06-08 续轮整体架构审查补充

本轮继续以 codedb 当前索引和生成报告复核整体架构。新增或修正的事实是：active effect lifecycle owner 已拆到 generated runtime 物理 asmdef 下的 `ActiveEffectLifecycleOwnerSystems.cs`，并已退出 SourceGenerator 输出、manifest 和 generated boundary report；`RuntimeActiveEffect.gen.cs` 当前更多承载 generated active effect helper / job / store 逻辑。这个 companion owner 仍不是目标态纯 glue，因为它仍拥有 query、ECB、NativeContainer 和 lookup refresh。

| 审查面 | 当前证据 | 事实判定 |
|---|---|---|
| codedb 截面 | `codedb_status` 为 429 files / 429 outlines / 767 chunks / graph 443 nodes / 1766 edges / 32 communities | 当前索引已覆盖新增 `ActiveEffectLifecycleOwnerSystems.cs`；旧 428 files 截面不再代表本轮最新事实 |
| Shell consumers | `codedb_deps GASRuntimeShell.cs imported_by` 仍为 AutoChess lifecycle / observation / runtime host、Editor `GASWatcher`、`AbilitySystemBinding` 共 5 个 | Shell 仍是多 capability implementation facade；外部业务虽多经 wrapper 进入，但 adapter / editor 仍共享同一 ECS handle 解析面 |
| Command resolve | `ASCCommandBufferResolveSystem.cs` 为 924 行，单个 `ASCCommandBufferResolveJob` 同时处理 ASC command、Ability grant / activate / remove、Tag diff、Attribute base value、lifecycle request 和 fact | DOTS API 形态正向；但 lane interface 仍过宽，调用方和审查者仍需理解多类 gameplay 语义如何在同一 job 内交织 |
| Generated lifecycle | generated runtime 物理 asmdef `: ISystem` 与 `OnUpdate(ref SystemState)` 各 7 个，分布在 `RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs`、`ActiveEffectLifecycleOwnerSystems.cs`；`RuntimeActiveEffect.gen.cs` 当前含 generated helper / job / lookup / ECB / NativeContainer owner | `ActiveEffectLifecycleOwnerSystems.cs` 已退出 SourceGenerator 输出，但仍是 generated runtime 物理 asmdef 内的手写 companion owner；剩余 manifest lifecycle artifact 仍为 `MigrationProofOnly` |
| CodeGen gate | `GasCodeGenValidationReport.md` 显示 `GeneratedRuntimePureGlueArtifacts=1`、`GeneratedRuntimeLifecycleMigrationArtifacts=3`、`GeneratedRuntimeBoundaryHits=63`、`GeneratedRuntimeLifecycleHits=9`、`GeneratedRuntimeStructuralChangeHits=5`、`GeneratedRuntimeOwnershipHits=1`、`GeneratedRuntimeRandomWriteLookupHits=48`、`GeneratedRuntimeSystemRegistrationHits=0` | gate 已能分类并阻断 unclassified lifecycle migration；但分类命中不是目标态完成证明，release-ready SourceGenerator mode 仍应让 lifecycle / ownership hit 归零或由手写 Core owner 接管 |
| DOTS 官方对照 | 本轮直接对照 `SYS-01`、`QRY-01`、`NAT-03`、`DBG-05` 和 `20-GASRuntimeCore-API选型基线.md` | OOP Shell 不能成为 gameplay 中间层；`NativeStream` 只有定义 deterministic merge、allocator owner、segment / memory budget 后才是目标 carrier；Debugger 成本必须分 cost domain |

本轮整体判断：当前架构不是“未 ECS 化”，而是“已有 ECS backbone、但 owner 深度不足”。正确重新划分不应新增一个更厚的 OOP manager，也不应让 SourceGenerator 继续生成 runtime lifecycle owner；应把 runtime session、command write、snapshot read、diagnostics、runner sync、definition lifetime、core lane、fan-in store、active effect store 和 structural commit 分别收权。

### 2026-06-08 二次校准：代码级 owner 深度事实

本次继续用 codedb 对关键文件做局部读取和调用方复核。新增事实只写入本文件，不进入目标态 Spec。

| 审查对象 | 本次代码证据 | 当前事实判定 |
|---|---|---|
| `GASRuntimeShell` | `GASRuntimeShell.cs:49-147` 同时创建 command port、capture read model、drain runtime jobs；`:150-233` 又处理 presentation bind 和 GlobalTimer / EventBus / EventLogSink / RuntimeDebugger singleton resolver；`codedb_callers` 显示 AutoChess lifecycle / observation / runtime host、Editor `GASWatcher`、`AbilitySystemBinding` 仍直接消费 | 它是多 capability ECS handle facade，不是目标态 Application Shell；后续必须把 command、snapshot、diagnostics、runner sync、definition lifetime 和 presentation capability 拆成可独立授权 / 计时 / 验收的 owner |
| `ASCCommandBufferResolveSystem` | `OnCreate` query 一次性要求 ASC identity、tag mask、command buffer、ability command、destroy command、attribute value、ability slot、fixed / temp tag source；`ASCCommandBufferResolveJob` 同时处理 destroy、ASC command、ability command、attribute dirty、tag mask、lifecycle request 和 GameplayEvent fact | API 形态已进入 stored query + `IJobChunk`，但 lane interface 仍宽；不能因为 job 化就写成 command resolve owner 已深模块化 |
| `GEEffectCommandSpecStream` | 静态 `_cachedEntityManager` / `_cachedStreamEntity`、`CommandWriter` / `GameplayEventWriter` 持有 `EntityManager` 和 singleton buffers，`HasRequiredBuffers` 要求 command / set-by-caller / spec / modifier / mutation / fact 同挂一个 stream owner | fallback query 删除是正向事实；但它仍是多语义 singleton carrier，按 `BUF-02` / `SEL-02` 只能是迁移证明，不是目标态 fan-in store |
| `ActiveEffectLifecycleOwnerSystems.cs` | 4 个手写 companion `ISystem` 位于 `GAS.Runtime.Generated` namespace：catalog normalize、active mutation apply、pre-tick、remove；`OnUpdate` 中仍创建 query、ECB、`NativeParallelHashMap`、`NativeArray` 并刷新多个 lookup | 它不再由 SourceGenerator 输出，也不进入 manifest / generated boundary report；但物理 asmdef、namespace 和 owner 职责仍与 generated helper/job 绑定，不能算 ActiveEffect lifecycle release-ready |
| `RuntimeActiveEffect.gen.cs` | generated helper/job 仍包含 source attribute snapshot gather、pre-tick job、mutation gather/apply helper、magnitude counter、cleanup / granted ability / tag / event helper | 它是 pure glue 之外的 runtime helper/job 迁移面；只要 helper 仍拥有 lookup / ECB / NativeContainer / lifecycle 行为，R5/R7 必须按 `MigrationProofOnly` 审查 |
| API health | Runtime `SystemAPI.Query=0`、`EntityManager.CreateEntityQuery=0`、`state.Dependency.Complete=0`、`.Run(` 静态命中 0；`ToEntityArray` 可执行运行调用仍集中在 Debugger observation 两处和 Cue managed boundary 一处；`new EntityQueryDesc` Runtime 命中 19 处，分布在 13 个文件 | 当前已经不是主线程 foreach 旧形态，但 query owner、materialization owner 和 stored query cost 仍要分 Core / Boundary / Diagnostics；不能把静态 0 命中写成 DOTS 性能优秀 |

### 当前职责重新划分事实

| 当前现实 owner | 证据 | 事实判定 | 目标态差距 |
|---|---|---|---|
| Bootstrap / Session | `GASManager.Initialize(...)` 创建 World、5 段 group、GlobalTimer、SpecStream、ActiveEffectGlobalIndex、EventBus、Replay sink 和 Debugger singleton；`Shutdown()` reset cache / dispose world | 当前 OOP bootstrap 是 runtime session owner 的现实实现，不是 Gameplay Core 计算层 | 目标态 public session seam 只能暴露 install / fixed tick / dispose / evidence，不暴露 `World`、`EntityManager`、singleton entity |
| Schedule backbone | `GASSystemScheduleContract` 固定 5 段 physical group；handwritten systems 与 7 个 generated runtime systems 统一注册；missing generated type 当前 fail-fast | Backbone 方向正确，generated runtime 已是当前执行事实 | 目标态 SourceGenerator 不应生成 lifecycle system；generated systems 必须退出或由手写 lane owner 接管 |
| Shell capability | `GASRuntimeShell` 同时解析 World、`EntityManager`、command port、read model、singleton、presentation binding 和 job drain；直接消费者含 AutoChess host / lifecycle / observation、Editor watcher、AbilitySystemBinding | 外部 public 面已有收窄，但 assembly 内仍是多能力 ECS 句柄 facade | 必须拆成 bootstrap、command write、snapshot read、diagnostics/export、definition/catalog、runner sync 六类 capability，并分别计时 / 授权 / 验收 |
| Core stream / fan-in | `EffectCommandSpecStream` 当前只走 cached registered owner，不再 fallback query；但 command/spec/set-by-caller/delta/mutation/fact 多数仍共用 singleton DynamicBuffer carrier，`MergeParallelCommandFanIn` 仍用 managed `List` + `EntityManager` 写回 | fallback query 风险已退场，carrier 仍是 `MigrationProofOnly` | 按 `SEL-01/02`、`BUF-02`、`NAT-03`，不同数据性质要拆为 `NativeStream` deterministic merge、owner-local range 或 bounded buffer |
| Generated runtime | generated catalog lookup / Blob builder 是正向定义链；manifest lifecycle artifact 当前为 `RuntimeAbilityActivation.gen.cs`、`RuntimeEffectInstant.gen.cs`、`RuntimeActiveEffect.gen.cs`，`ActiveEffectLifecycleOwnerSystems.cs` 是同物理 asmdef 内的手写 companion owner | Generated 链路已经反哺 Runtime，但仍越过了目标态 pure glue 边界；active-effect owner 退出 SourceGenerator 输出只是收窄，不改变剩余 `MigrationProofOnly` 判定 | SourceGenerator 目标只生成 immutable catalog、static lookup、pure record、validation / Editor binding；不能继续拥有 query、ECB、NativeContainer 或 lifecycle |
| Debugger / Observation | `GasRuntimeDebugger` singleton lookup 已是 registered/cache owner；`ToEntityArray` 运行调用集中在 Debugger observation 两处与 Cue managed boundary 一处；`ExportToText` 是 derived export；observation materialization 已进入 snapshot 和 AutoChess evidence。2026-06-08 最新 x50 已拆 performance / diagnostic pass：performance summary 为 `performancePassObservationPollutionRisks=0`，diagnostic Debugger 仍显示 `observationMaterializedQueries=12` | Debugger 可作为 evidence owner；observation materialization 成本已能独立归因，performance pass 污染风险已隔离，但 Profiler enabled 与规模性能闭环未完成 | 按 `DBG-01..05`，contract counter、runtime counter、official capture、validation evidence、derived export 必须分层；不能用字符串日志或 disabled profiler 状态证明 Core 性能 |
| Cue / Presentation | `CueManagedLifecycleSystem` 位于 `GASBoundaryProjectionSystemGroup`，使用 UnityEngine `Time.time` 和 `ToEntityArray` 处理 managed cue lifecycle | 这是 Boundary / Presentation 成本，不是 Runtime Core hot path | 目标态允许 managed boundary，但必须从 CoreSimulation timing 与 DOTS hot path 结论中拆出 |
| AutoChess adapter | `AutoChessGasRuntimeHost` 通过 Shell 初始化 runtime / catalog / tick group；`AutoChessGasObservationGateway` 直接 reset singleton、创建 diagnostics snapshot；`AutoChessGasBattleEntityLifecycle` 用 registry 保存 `ASCHandle` 并创建 driver / unit，driver public handle 已为 opaque id/version | AutoChess 是业务验收 Shell，当前 Thin Adapter 未完成；其 direct ECS 句柄仍是内部实现事实 | 目标态 Battle Runtime Adapter 只暴露业务动作、opaque handle、snapshot 和 evidence；driver runtime store 内部 `_driverEntity`、singleton reset、job drain 必须被 capability 分类并从 Core 性能结论中排除 |

### 官方规则判定

| 规则 | 对本截面的判定 |
|---|---|
| `SYS-01` / `SYS-02` | 5 段 group 与多数 hot path job 化是正向事实；但 `GASManager` / `GASRuntimeShell` 仍是 bootstrap 与 boundary 句柄聚合 owner，不能写成 OOP gameplay 中间层合格。 |
| `QRY-01` / `PRF-05` / `PRF-33` | Runtime 可执行 `SystemAPI.Query`、`.Run()`、`EntityManager.CreateEntityQuery` 已为 0，这是静态正向截面；剩余 `ToEntityArray` 必须按 Debugger / Boundary 分类，而不是平均进 Core tick。 |
| `SEL-01` / `SEL-02` / `BUF-02` / `NAT-03` | singleton stream fallback query 退场不等于 stream carrier 达标；command/spec/delta/fact/mutation 必须继续按数据性质拆 carrier，并给出 allocator owner、merge order、capacity / spill evidence。 |
| `DBG-01..05` / `SYS-04` | Debugger 已能采样和导出，但必须把 observation materialization、official tool disabled reason、derived string export 与 Core counters 分层；否则 AutoChess x50 跑通不能证明 DOTS 优秀水平。 |
| `BLOB-01` / `BLOB-02` / `BUR-01` | generated catalog / lookup 方向正确；generated lifecycle system 和 lookup owner 仍是 `MigrationProofOnly`，不得在目标态中扩大。 |

### 本轮结论

当前 GAS 已有可继续演进的 DOTS backbone，但“纯血 ECS GamePlay Core”还没有完成。最新事实不支持继续建设更大的 OOP 中间层，也不支持把 generated lifecycle 当成 SourceGenerator 成熟形态。正确的重新划分应把 `GASManager` / `GASRuntimeShell` 降为 bootstrap 与 Boundary capability implementation，把 `GEEffectCommandSpecStream` 降为迁移期 carrier，把 generated runtime lifecycle 降为 `MigrationProofOnly`，把 Debugger / Cue / AutoChess observation 从 Core performance pass 中拆出。

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
| `DefinitionCatalogLifetime` | generated `DefinitionCatalog`、runtime catalog component、AutoChess catalog session | Blob catalog / static lookup 正向存在；install/dispose owner 仍和 runtime host / generated systems 混合 | catalog lifetime 要独立于 command、snapshot、runner sync；generated lifecycle 不得借 catalog 名义保留 |
| `GASFrameKernel` | handwritten `ISystem`、generated runtime systems、stream owner contracts | 多数 hot path 已 job 化，且 query / lookup owner 能静态追踪；generated runtime 仍拥有 lifecycle 和 ECB | 保留 handwritten lane，退出 generated lifecycle；所有 lane 必须显式声明 query、lookup、allocator、dependency 和 evidence |
| `EffectFanInStore` | `GEEffectCommandSpecStream`、execution output typed fact NativeStream 切片 | stream owner contract 已把 singleton carrier 标记为 migration；局部 NativeStream 切片不能覆盖全部 command/spec/delta/fact/mutation | singleton carrier 继续按 proof-only 处理；scale-ready 必须补 deterministic merge、spill、segment、allocator 和 battle hash |
| `ActiveEffectStore` | `ActiveEffectStore`、generated active effect runtime、`EffectRuntimeUtility` | owner-local slot 和 global index 方向正向；lifecycle helper、generated slot tick、source magnitude snapshot 仍分散 | Store owner 可以保留；lifecycle 应由手写 Core lane 接管，helper 只作迁移兼容 |
| `StructuralCommit` | `Begin/EndGASStructuralCommitECBSystem`、Core ECB 写入点 | 结构变化 gate 已存在；多个系统仍可拿到 ECB 并写 structural intent / create / destroy | 保留唯一 playback phase；后续必须给出 source TopN、official diff 和 generated ECB 退出证据 |

本轮 owner 结论：不要再把 `GASRuntimeShell` 扩成更大的 Application Shell，也不要把 SourceGenerator 扩成更大的 Runtime Core。正确方向是让每个 owner 拥有自己的 query、lookup、allocator、dependency、carrier、capacity、structural playback 和 evidence，并让 Shell 只看到业务 intent、opaque handle、snapshot 和 evidence。

### 本轮重新划分事实结论

当前代码中最接近目标态的不是 Shell，而是 physical backbone、chunk-local applicator、registered owner、generated immutable catalog 和 structured evidence。当前最需要治理的也不是“是否 ECS 化”，而是这些深 owner 之外仍残留的浅 interface：

1. Shell shallow interface：`GASRuntimeShell` 的源码注释称其是 Thin OOP shell，但当前实现仍是多个 capability 共用的 ECS handle resolver。注释不能覆盖事实，后续 R1/R6 必须按 capability 拆读写授权和 timing 归因。
2. Carrier shallow interface：`GEEffectCommandSpecStream` 隐藏了多类数据的容量、清理、合并和 pressure counter。它现在适合作为迁移 evidence owner，不适合作为目标态统一总线。
3. Generated shallow interface：`GasGlueCodeGenPhases` 同时生成 pure definition glue 与 runtime lifecycle system。目标态只能继承前者，后者必须退出、迁到手写 lane，或保留为有退出门的 `MigrationProofOnly`。
4. Evidence shallow interface：`GasRuntimeDebugger` 已有足够多 counter，但 evidence、official capture、validation report、derived export 和 observation materialization 成本必须继续分层消费。

因此，本轮事实重划分把“目标态设计应当如何拆”交给 `01/16`，把“当前代码哪里仍混在一起”保留在本文件；二者不能互相替代。

## 2026-06-07 整体架构复核：Owner 重划分事实

### 事实消费卡

```markdown
来源类型：整体架构审查 / codedb 截面 / DOTS 官方规则对照
原始证据：
  - `codedb_status`: 428 files / scan ready
  - `codedb_module_map`: `Assets/GAS/Runtime` 主要社群 141 files / 1968 indexed symbols
  - `Assets/GAS/Runtime/General/GASRuntimeShell.cs:11-279`
  - `Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs:99-434`
  - `Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs:483-698`
  - `Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs:7-421`
  - `Assets/GAS/Runtime/Effect/Component/Dynamic/ActiveEffectStore.cs:2260-2292`
  - `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs:882-895`
  - `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs:6597-6708`
  - `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs:377-620`
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
| `GASSystemScheduleContract` | 5 段 physical group 已固定，handwritten system 与 7 个 generated runtime systems 被纳入同一主链；generated type-name registry 缺失 type 当前 fail-fast | generated lifecycle 仍是 Runtime Core 执行事实；system 数量、phase budget 和 generated assembly 负例验证仍需交还 | physical backbone 成立，缺失 type silent skip 已缓解，但 lane owner 和 generated lifecycle 退出仍未闭合 |
| `GEEffectCommandSpecStream` | 隐式 singleton writer/append helper 已退场，写入需要显式 stream owner；frame prepare 已有 buffer-only path 和 pressure counters | command/spec/delta/mutation/fact 仍共用 singleton DynamicBuffer carrier；`MergeParallelCommandFanIn` 仍用 managed `List` + `EntityManager` 写回 | 只能作为 `MigrationProofOnly` carrier，不能作为 scale-ready fan-in |
| `EffectRuntimeUtility` | cleanup、removed event、modifier/tag 移除和 store cleanup 有真实实现 | instant apply、ongoing requirement、stack merge、duration activate/deactivate/reactivate、reject 判定等多处为空或固定返回；`TryGetStaticDefinitionBlob` 固定 false，导致 `ActiveEffectStore.ResolveFlags(...)` 无法从静态 Blob 补齐 granted tag / ability / requirement flag | 不是目标态 Effect lifecycle owner；R7 必须把生命周期回收到明确 system / job / store |
| `GasRuntimeDebugger` | Debugger singleton 已改为 registered/cache owner；structured log / official diff / validation export 已存在；observation materialization 已进入 dedicated counter 和 AutoChess evidence | singleton fallback query 已退场，但 Debugger observation `ToEntityArray`、derived export、official capture 和 runtime counter 分层还需要阈值/消费约束 | Debugger 是 Boundary evidence owner，不能和 Core hot path 成本混算 |
| `GasGlueCodeGenPhases` / generated runtime | validation report 已统计 boundary hit，并把 mode 写成 `blocking-unclassified-lifecycle-migration`；manifest 当前有 1 个 `RuntimePureGlue` artifact 和 3 个 `RuntimeLifecycleMigration` artifact；pure resolver 正向存在 | generated artifacts 仍包含 `ISystem`、`OnUpdate`、ComponentLookup / BufferLookup / ECB / NativeContainer owner；classified hit 不等于 pure glue 完成 | SourceGenerator 目标态应退出 runtime lifecycle owner，只保留 immutable catalog / lookup / pure glue / validation |
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
