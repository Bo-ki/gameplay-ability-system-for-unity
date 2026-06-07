# 01 DOTS API 健康 Owner 分类事实

> Owner：00-当前架构事实/架构重划分审查事实 | 状态：当前事实子页 | 拆分来源：../架构重划分审查事实.md | 拆分时间：2026-06-08

承载 Runtime / generated runtime / AutoChess 的 API 健康 owner 分类、静态复核补充和事实消费卡。

本文件只记录当前代码事实、证据和 DOTS 判定；目标态设计正文回到 `../../01-目标态架构共识/`，任务拆分回到 `../../02-主线任务树/`。

## 整体项目审查：DOTS API 健康 owner 分类

2026-06-08 对 `Assets/GAS/Runtime`、`Assets/GAS/Generated/CodeGen/Runtime` 和 `Assets/AutoChessDemo` 做整体静态截面后，旧“所有 `ToEntityArray` 都是 hot path”的口径需要修正；`GlobalTimer`、`ActiveEffectStore` 和 `GasRuntimeDebugger` 的 singleton fallback query 均已删除。当前更准确的事实是：同步等待和临时 query 已明显收窄，但 API 健康证据必须按 owner 分类，否则会把 Core、Boundary、Debugger 和 Demo adapter 成本混在一起。

| API / 模式 | 当前命中 | owner 分类 | 判定 |
|---|---|---|---|
| `state.Dependency.Complete()` / `.Run()` | `Assets/GAS/Runtime` 与 generated runtime 当前 0 命中 | Runtime Core | 正向事实；不能据此宣称 Core 已达 DOTS 优秀水平 |
| `CompleteAllTrackedJobs()` | `GASRuntimeShell.cs:141-147`，由 `AutoChessGasRuntimeHost.TryCompleteRuntimeJobs()` 和 `AutoChessGasRuntimeTicker.CompleteRuntimeJobsTimed()` 间接调用 | runner sync / debugger measurement | 只属于 AutoChess group timing wrapper 和 Shell internal drain capability，不是 CoreSimulation hot path 优化证明 |
| `GASRuntimeShell.TryResolveRuntimeEntityManager` | `GASRuntimeShell.cs:30-44`、`:66-115`、`:243-271` | Boundary facade / internal Shell capability risk | 外部 EM 面已收窄，但 assembly 内 adapter / diagnostics 仍可拿到 `EntityManager`；R1 需要继续收缩为 opaque handle + command/snapshot/diagnostics capability |
| `GASRuntimeShell` singleton / diagnostics capability | `GASRuntimeShell.cs:88-153`、`GASWatcher.cs:396-551`、`AutoChessGasObservationGateway.cs:11-257`、`AutoChessGasRuntimeTicker.cs:44-78` | Editor / Demo diagnostics / runner sync | 这些调用集中化是正向事实，但 capability 未分级；读 watcher、observation reset、debugger timing 和 job drain 必须从 Core/Battle 性能证据中拆出 |
| registered/cache owner | `GASGlobalTimerSystem.cs:73-126` | Runtime frame current-frame owner | cache miss 直接失败，不再创建 singleton fallback query |
| registered/cache owner | `ActiveEffectStore.cs:1368-1419` | ActiveEffectStore global index owner | 不再创建 fallback query；仍需 capacity、cache integrity 和 hot path 触发证明 |
| registered/cache owner | `GasRuntimeDebugger.TryGetSingleton(...)` / `TryResolveRegisteredSingleton(...)`，`GASManager.Initialize(...)` / `Shutdown(...)` | Debugger singleton owner | 不再创建 fallback query；Debugger singleton 必须由 initialization 注册并在 shutdown reset |
| `ToEntityArray` | `CueManagedLifecycleSystem.cs:39` | Boundary managed presentation | 承载 managed cue lifecycle，可接受但必须单独计入 Boundary / Presentation cost |
| `ToEntityArray` / `StringBuilder` | `GasRuntimeDebugger.activeEffectStoreQuery` / `presentationOutboxQuery` | Debugger / Observation | observation-only；materialization 已进入 Debugger / AutoChess evidence，仍必须与 CoreSimulation timing 拆开 |
| `MergeParallelCommandFanIn` managed `List` | `GEEffectCommandSpecStream.cs:627-662` | singleton fan-in proof carrier | deterministic ordering 是正向事实，但 managed list + `EntityManager` 写回不是 scale-ready `NativeStream` fan-in |
| handwritten ECB create / destroy | `ASCCommandBufferResolveSystem.cs:52-89`、`:538`、`:785`；`ASCDestroyFinalizeSystem.cs:62`、`:305` | StructuralCommit route | 方向正确；仍需 Journaling / Profiler / Debugger route-level required vs recorded 证据 |
| generated ECB create / destroy | `ActiveEffectLifecycleOwnerSystems.cs:133`、`:245`、`:361` 创建 structural ECB；`RuntimeActiveEffect.gen.cs` helper job 内仍包含 `StructuralEcb` create / destroy 调用点 | hand-written companion / generated runtime lifecycle `MigrationProofOnly` | active-effect owner 已退出 SourceGenerator 输出，但仍在 generated runtime 物理 asmdef 内；剩余 generated lifecycle 仍拥有 structural owner，目标态应迁回更深的手写 Core owner 或被 validation gate 阻断 |
| Definition / authoring managed collection | `Assets/GAS/Runtime/Definition/*`、`GameplayEffectConfigRegistry.cs` | Definition / prototype / validation | 不按 Core hot path 缺陷处理；进入 Runtime Core hot path 才算违约 |
| AutoChess direct ECS access via RuntimeShell | `AutoChessGasRuntimeHost.cs:7-60`、`AutoChessGasObservationGateway.cs:11-257`、`AutoChessGasBattleEntityLifecycle.cs:8-249`、`AutoChessGasCatalogSession.cs:6-16`、`AutoChessGasRuntimeTicker.cs:9-78` | Demo adapter host / lifecycle / observation / runner sync | 已集中到 `Integration/GasCore` facade 之后的 owner 文件，但仍通过 internal Shell capability 取得 live ECS 句柄；继续按 R6 owner 表退出 raw ECS handle 和 direct EM capability |

当前 `EntityManager.CreateEntityQuery()` Runtime 可执行调用点为 0 处。当前 `ToEntityArray()` 运行调用点为 3 处：`GasRuntimeDebugger` 的 `activeEffectStoreQuery` / `presentationOutboxQuery`、`CueManagedLifecycleSystem` 的 cue query。Debugger / Cue 仍按 Debugger 和 managed boundary 分类；pending AttributeDelta owner-local apply 与 GlobalTimer frame lookup 已不再归入 query materialization。`GasRuntimeDebugger` singleton lookup、`GASGlobalTimerSystem` current-frame owner 与 `ActiveEffectStore` global index owner 均已改为 registered/cache owner，不再创建 fallback query。AutoChess catalog builder 的旧 `ToEntityArray` 事实已过期，当前 `AutoChessBattleDefinitionCatalogBuilder.ResolveCatalogEntity()` 通过缓存 `_catalogEntity`，缓存失效时直接 `CreateEntity(ComponentType.ReadWrite<GASDefinitionCatalogComponent>())`。

### 拆分后静态复核补充（2026-06-07）

R0-R8 叶子任务拆分后，重新对 `Assets/GAS/Runtime`、`Assets/GAS/Generated/CodeGen/Runtime` 和 `Assets/AutoChessDemo` 执行 `rg` 静态复核。本截面形成时 `codedb_status` 曾不可用，报 `failed to load project: 系统找不到指定的文件。 (os error 2)`，因此该截面的 API 分类采用文本扫描和定点读取作为证据；本轮收口复测 `codedb_status` 已恢复 ready，当前为 429 files / 429 outlines / 767 chunks / scan ready，后续代码截面可优先恢复 codedb + `rg` 交叉复核。

1. `state.Dependency.Complete()`、`.Run()`、`SystemAPI.Query` 在 Runtime / generated runtime / AutoChess 扫描中仍无运行命中；这只是同步等待和主线程 query 语法的正向截面，不能证明 DOTS 优秀水平。
2. `GASRuntimeShell.` 命中必须区分可执行调用和验证报告字符串：`Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationReport.cs:315` 当前输出 `snapshotOwner=AutoChessGasBattleUnitSnapshotProjector.StructuredLog`，不再把 AutoChess result snapshot 归因到 Shell live read；真正的 Shell capability 调用仍集中在 `AbilitySystemBinding` 与 `AutoChessDemo/Integration/GasCore`。
3. `Dictionary<Entity` 文本命中当前来自 `Assets/GAS/Runtime/General/Helper/EntityHelper.cs:13` 的 `Dictionary<EntityBindingKey, GameObject>` presentation binding map。它是 Runtime Boundary / Presentation 绑定事实，不是 ActiveEffectStore、Runtime Core store 或 gameplay state 容器；后续 API 健康扫描不得把该命中直接写成 Core hot path `Dictionary<Entity,...>` 违规。
4. SourceGenerator gate 截面仍与当前生成报告一致：`GeneratedRuntimeBoundaryHits=63`、`GeneratedRuntimePureGlueArtifacts=1`、`GeneratedRuntimeLifecycleMigrationArtifacts=3`、`GeneratedRuntimeLifecycleHits=9`、`GeneratedRuntimeStructuralChangeHits=5`、`GeneratedRuntimeOwnershipHits=1`、`GeneratedRuntimeRandomWriteLookupHits=48`、`GeneratedRuntimeBoundaryGateMode=blocking-unclassified-lifecycle-migration`、`GeneratedRuntimeUnclassifiedBoundaryHits=0`。这说明未分类 generated runtime boundary hit 已被阻断，但 manifest 分类为 `RuntimeLifecycleMigration` 的 `MigrationProofOnly` artifact 仍必须走 R2/R3/R5 退出；`ActiveEffectLifecycleOwnerSystems.cs` 已退出 SourceGenerator 输出和 manifest，但仍是 generated runtime 物理 asmdef 内的手写 companion owner，不是完成证明。

### codedb 恢复后的整体项目审查补充（2026-06-07）

本截面使用 `codedb_status`（429 files / 429 outlines / 767 chunks / scan ready）和 `rg` 交叉复核。它只记录当前代码事实；目标态约束写入 `../../01-目标态架构共识/90-目标态不变量.md`。

1. Shell capability 风险仍集中在 `GASRuntimeShell`：`Assets/GAS/Runtime/General/GASRuntimeShell.cs:16-44` 解析 runtime `World` / `EntityManager`，`:49-115` 创建 command port，`:119-134` 捕获 read model，`:141-147` 执行 `CompleteAllTrackedJobs()`，`:168-234` 继续解析 runtime singleton，`:255-271` 解析 ASC runtime entity。public 面已经比旧版本窄，但 assembly 内仍是 bootstrap / command / snapshot / diagnostics / drain 共用 facade。
2. AutoChess adapter 当前直接消费 Shell internal capability：`Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeHost.cs:13-17` 注册 runtime systems 并安装 catalog，`:25-30` 卸载 catalog / reset cache / shutdown，`:36-59` 读取 5 段 physical group，`:62-65` 通过 Shell drain runtime jobs。`AutoChessGasBattleEntityLifecycle.cs:51-55` 创建 battle driver，`:85-102` 读写 driver runtime store，`:105-113` 通过 command port 请求销毁 ASC。`AutoChessBattleResultBuilder.cs:25-29` 与 `AutoChessGasBattleUnitSnapshotProjector.cs:8-56` 已把 unit result snapshot 改为 structured log 派生。
3. `ASCHandle` 已是外部 opaque handle 的正向过渡，但 raw identity 仍保留在 implementation 内：`Assets/GAS/Runtime/AbilitySystem/ASCHandle.cs:17-27` 暴露 internal `RuntimeEntity` / `TryResolveRuntimeEntity` / `MatchesRuntimeEntity`。当前 `codedb_callers` 显示 `MatchesRuntimeEntity` 调用方为 0；AutoChess report projection 与 unit result snapshot 已改为 report key / structured log 消费，driver public handle 也已收缩为 opaque `driverId/version`。剩余 raw identity 风险在 `BattleUnitRegistry` 内部 `ASCHandle` command compatibility、`AutoChessBattleDriverRuntimeStore` 内部 `_driverEntity` owner 和 Boundary export 解析 report key。
4. 同步等待当前运行命中仍只有 `GASRuntimeShell.cs:146` 的 `CompleteAllTrackedJobs()`；没有在 Runtime / generated runtime / AutoChess 运行代码中发现 `state.Dependency.Complete()`、`.Run()`、`SystemAPI.Query<...>` 或 `SystemAPI.QueryBuilder()`。这只能证明这些 API 没有当前运行命中，不能证明整体 DOTS 性能已达标。
5. `EntityManager.CreateEntityQuery()` 当前 Runtime 可执行命中 0 处；`ToEntityArray()` 当前 3 个运行调用点：`GasRuntimeDebugger` active-effect-store / presentation-outbox observation，和 `CueManagedLifecycleSystem` managed presentation boundary。pending AttributeDelta stream migration fallback 已删除，`GasRuntimeDebugger` singleton lookup、`GASRuntimeFrameContext` current-frame lookup 与 `ActiveEffectStore` global index owner 已改为 registered/cache owner；Debugger materialization 已进入 `ObservationMaterialization` / `runtimeObservationMaterialization` / AutoChess `performancePassObservationPollutionRisks` evidence。这些命中必须按 owner 分类，不能合并写成 Core hot path，也不能因 fallback 退场而忽略 Boundary / Debugger observation 成本。
6. generated runtime 有正向 job / Blob 进展：`RuntimeAbilityActivation.gen.cs:14-76` 使用 stored query 并调度 `AbilityCatalogCommitJob : IJobChunk`，`ActiveEffectLifecycleOwnerSystems.cs:210-312` / `:330-404` 的 active effect pre-tick / remove path 也调度 `IJobChunk` helper，`DefinitionCatalog.gen.cs:21-77` 是 sorted code -> index lookup，`:79-84` 用 `BlobBuilder(Allocator.Temp)` 构建 runtime catalog blob。
7. generated runtime 仍不是 pure glue 完成态：`RuntimeEffectInstant.gen.cs:25-46` 的 `GEEffectSpecBuildSystem` 调度 serial `IJob`，从 singleton stream 读取 command/spec/set-by-caller buffer；`RuntimeActiveEffect.gen.cs:142-201` 当前先 gather active mutation command 和跨 owner `SourceAttribute` snapshot，再用 `GEActiveEffectMutationChunkApplyJob : IJobChunk` 按 ASC chunk apply。active mutation 和 pending AttributeDelta owner-local apply 的 lookup 面积已经大幅收窄，pending AttributeDelta stream migration fallback 已删除，但 command carrier、generated instant delta record / fan-in 证明、pre-tick / execution SourceAttribute snapshot lane 仍未达到 pure glue / store-only 终局。
8. 生成报告当前仍显示 `RuntimeForbiddenDependencyHits=0`、`GeneratedRuntimeBoundaryHits=63`、`GeneratedRuntimePureGlueArtifacts=1`、`GeneratedRuntimeLifecycleMigrationArtifacts=3`、`GeneratedRuntimeLifecycleHits=9`、`GeneratedRuntimeSystemRegistrationHits=0`、`GeneratedRuntimeStructuralChangeHits=5`、`GeneratedRuntimeOwnershipHits=1`、`GeneratedRuntimeRandomWriteLookupHits=48`、`GeneratedRuntimeManagedConfigHits=0`、`GeneratedRuntimeBoundaryGateMode=blocking-unclassified-lifecycle-migration`、`GeneratedRuntimeUnclassifiedBoundaryHits=0`。因此当前状态是“未分类 generated boundary hit 被阻断，已分类 `MigrationProofOnly` 仍存在”，不是 SourceGenerator pure glue 完成证明。
9. `GeneratedRuntimeSystemRegistrationHits=0` 不能消费为 registry 完成证明：`GASSystemScheduleContract.AddSystemsByTypeName()` 当前使用 `Type.GetType(systemTypeName)`，并在 `systemType == null` 时抛 `InvalidOperationException("Generated GAS runtime system type is missing: ...")`。缺失 type 静默漏注册风险已缓解，但还缺缺失 artifact、类型名漂移、generated runtime assembly 不可用等负例验证，以及 system 数量 / phase budget evidence。
10. 当前正向 NativeStream 使用点集中在 `Assets/GAS/Runtime/System/ASCDestroyFinalizeSystem.cs:50-109`：三类 `NativeStream(Allocator.TempJob)` 随 scan/apply job handle 串联 dispose，并把 dispose handle 写回 `state.Dependency`。它是 R4/R3 可复用的正向模式，但仍需对照 `NAT-01` / `NAT-03` 补 owner、segment budget 和 deterministic merge evidence，不能自动泛化为所有 fan-in 已达标。

### 本轮事实消费卡（2026-06-07）

```markdown
来源类型：整体代码审查 / 文档治理审查
原始证据：`codedb_status` ready；`GASRuntimeShell.cs`、`GASGlobalTimerSystem.cs`、`ActiveEffectStore.cs`、`RuntimeActiveEffect.gen.cs`、`GASAttributeModifierDeltaApplySystem.cs`、`GASRuntimeStreamOwnerContract.cs`、`GASRuntimeStructuralPlaybackGateContract.cs`、AutoChess `Integration/GasCore` 定点读取；`rg` API 健康扫描
第一 owner：00
长期有效：待复核，直到下一轮 Runtime / generated / AutoChess 代码变更
需要反哺：事实 / Spec 抽象约束 / R1-R8 任务验收
```

消费结论：

1. `00` 只保存当前事实：registered/cache owner、chunk-local apply、MigrationProofOnly carrier、Shell capability 仍混杂、AutoChess adapter 仍触达 live ECS、StructuralCommit evidence 未闭环。
2. `01` 只消费抽象约束：Shell capability 分级、API health owner 分类、MigrationProofOnly 退出门、Debugger evidence 分级、SourceGenerator pure glue 边界。
3. `02` 消费为 R1/R2/R3/R4/R5/R6/R8 的交还门槛，不能把本节代码行号复制进目标态 Spec。
4. `04` 只记录本轮未跑项和短期接力；本节不替代 batchmode / Unity Profiler / Entities Journaling 的后续验证。
