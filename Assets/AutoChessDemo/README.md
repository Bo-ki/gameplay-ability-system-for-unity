# AutoChess Demo

`Assets/AutoChessDemo` 是 EX-GAS 2.0 四层架构中的 **Layer 1 Application Shell Layer / 业务验收层**。它用真实 DOTS 业务链验证 Runtime Core，但不属于 `Assets/GAS/Runtime`，也不拥有 Runtime Debugger 模块。

当前目录已经按破坏性重构收敛为 AutoBattle 最小验证链，旧 `HeadlessAutoChess*` 配置源、组件堆叠、规模压测 DTO 已从编译面清除。Demo 只通过 `GAS.Runtime` 的公开 Runtime API、Layer 2 Diagnostics snapshot 和 official tool diff 做验收。

## 最小链路跑通计划

本轮只验证当前 Runtime Core 是否能被真实 DOTS 业务链打穿，不扩展完整 4v4 自走棋机制：

1. `HeadlessAutoBattleScenario` 创建 4 个 ASC，初始化 Health / Energy，授予普攻与斩杀 Ability。
2. `AutoBattleDefinitionCatalogBuilder` 安装最小 `GASDefinitionCatalogBlob`：普攻 GE 走 instant modifier，斩杀 GE 走 active mutation + demo execution extension。
3. `HeadlessAutoChessRuntimeSystemBootstrap` 把 `AutoBattleCommandDriveSystem` 注册到 `GASCommandResolveSystemGroup`，把 `AutoBattleExecuteDamageCalculationSystem` 注册到 `GEExecutionCalculationExtensionSystemGroup`。
4. `AutoBattleCommandDriveSystem` 在 Functional x1 使用 `SystemAPI.Query` 收集 ASC 快照到 frame-local `NativeList`，直接批量写入 `AbilityCommandBuffer`，不再为 4 个单位的小规模链路支付 `NativeStream + job schedule + Complete` 固定成本。
5. Runtime Core 消费 `AbilityCommandBuffer`，执行 Ability grant / activate、generated catalog commit、GE command stream、instant attribute delta、cue request、replay projection。
6. `AutoBattleExecuteDamageCalculationSystem` 读取 `GEEffectCommandBuffer` 中的斩杀 GE 命令，用 ECS query 顺序扫描目标 ASC 并修改 `AttributeValueBuffer`，追加 `AttributeModifierBuffer` 和 `GameplayEventBuffer{ExecutionCalculationOutputUpdated}` typed fact；event bus 只保留同一 fact 的镜像。
7. `GameplayFactProjectionSystem` / `ReplayLogSystem` 统一收集 attribute changes、execution outputs、cue requests，`GAS.AutoChessDemo.HeadlessAutoChessRuntimeRunner.RunHeadlessAutoBattleOnce()` 用这些事实做无头断言。
8. Runtime Debugger 数据来自 Layer 2 `GasRuntimeDebugger` / `GasRuntimeOfficialToolDiffCapture`；Editor Debugger Window 是 Layer 1 Editor Extension，不放在 Demo 内。
9. 实机性能验证通过 AIBridge 驱动真实 Unity Editor / Player，并优先使用 Unity Profiler、Entities Profiler Modules、Entities Journaling 等官方工具；Demo 不重复实现 Debugger UI 或 Profiler。

## DOTS 约束

- Functional x1 数据量很小，按 Entities 官方 job overhead 建议优先使用直接 ECS query，避免调度成本超过业务计算本身；x50+ 规模 profile 才切换或补充 `IJobChunk + ChunkEntityEnumerator` 路径。
- AutoBattle AI 不在单位循环中调用 `EntityManager` 做结构变化；高频 command 使用 singleton DynamicBuffer 作为 frame-local stream，结构变化保留在 Runtime Core commit / cleanup phase。
- 当前最小 execution calculation 不使用 `BufferLookup` 随机访问目标；它按目标 ASC chunk 顺序扫描并匹配 command record，优先验证线性 DOTS 数据流。
- `ExecutionCalculationOutputUpdated` 必须先作为 `GameplayEventBuffer` typed fact 进入 replay；event bus 镜像的 `SourceFactSequence` 指向该 fact sequence，不能复用属性 delta sequence。
- `GASDefinitionCatalogBlob` 是当前 Runtime Core 的权威定义入口；Luban / SourceGenerator 生成链仍是目标态，但本轮用运行时安装的最小 catalog 做闭环验证。

## 当前目录

- `AutoBattle`: 最小 Runtime Core 验收链路，包含 command drive、definition catalog、execution calculation、scenario。
- `Simulation`: 只保留 GAS group bootstrap。
- `Validation`: 只保留 batchmode/headless runner。
- `Presentation`: 只保留 scene runner / cue 占位，Simulation 不依赖它。
- `Config`: 旧手写配置源已清除，后续只承载 Luban / SourceGenerator 目标态产物。

## 验收命令

```powershell
E:\Unity\UnityEditor\6000.3.14f1\Editor\Unity.exe -batchmode -quit -projectPath E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity -executeMethod GAS.AutoChessDemo.HeadlessAutoChessRuntimeRunner.RunHeadlessAutoBattleOnce -logFile E:\Unity\UnityProjects\_Git\gameplay-ability-system-for-unity\Temp\AutoBattleValidation.log
```

真实 Editor PlayMode + 官方 Profiler 链路使用 AIBridge。Profiler 采集窗口由 `HeadlessAutoChessDemoSceneRunner` 这个 MonoBehaviour 控制，不再用 Editor code execute 包住 runner：

```powershell
$CLI = 'Library\PackageCache\cn.lys.aibridge@f203de2ec848\Tools~\CLI\win-x64\AIBridgeCLI.exe'
& $CLI compile unity --timeout 120000 --pretty
& $CLI scene load --scenePath 'Assets/AutoChessDemo/Presentation/Scenes/HeadlessAutoChessDemo.unity' --mode single --on-dialog discard --timeout 60000 --pretty
& $CLI menu_item invoke --menuPath 'Window/Analysis/Profiler' --timeout 30000 --pretty
& $CLI editor play --timeout 30000 --pretty
& $CLI get_logs --regex 'HeadlessAutoChessPlayMode|Exception|Error|failed|Profiler' --count 80 --pretty
& $CLI get_logs --logType Error --count 50 --pretty
```

PlayMode runner 会以 `ProfilerDriver.profileEditor=False` 保存 Runtime-only 官方 capture 到 `Temp/AutoChessDemo-PlayMode-X50-RuntimeProfile.data`，并导出 `TestResults/AutoChess/T6-CHESS-AF-SceneRuntime/AutoChessPlayModeProfileSummary.txt`。这两个目录都是本地运行产物，不纳入版本控制。

一次有效跑通至少需要满足：

- `DriverIssuedCommands > 0`
- `EventCounts.AttributeChanges > 0`
- `EventCounts.ExecutionCalculationOutputUpdated > 0`
- `EventCounts.CueRequests > 0`
- `Completed == true`
- `RuntimeDiagnostics.EventCount > 0`
- `OfficialToolDiff.JournalingCaptured == true`
- `blockingDebugErrors == 0`

## 当前跑通数据

2026-05-29 Functional x1 普通 batchmode 无头链路已跑通，并已修正性能采样口径：`avgTickMs` 只统计 bootstrap 之后、丢弃 3 个 runtime warmup tick 后的 measured ticks。旧版 `15ms+` 平均值把 ASC 创建、Ability grant、首帧 SystemGroup 初始化、Journaling 启用、Burst / Editor warmup 和 frame 1-3 的尖峰摊进了 10 个 tick，不是 DOTS 稳态热路径数据。

```text
completed=True, winner=Player, battleTicks=9, totalTicks=10,
warmupDroppedTicks=3, measuredTicks=7, commands=18, finishers=8,
attributeChanges=21, executionOutputs=5, cueRequests=8,
debugEvents=62, debugWarnings=22, debugErrors=0, blockingDebugErrors=0,
coreRequests=54, coreFacts=96, coreDeltas=34, coreCues=8,
peakEventBus=26, replayLag=0, journalingRecords=2128,
totalElapsedMs=169.515, factsHash=0x7C84FE91,
summaryHash=0x53F70297, avgTickMs=0.599
```

`debugErrors=0` 表示 Runtime Debugger 的错误语义不再被慢 timing 事件污染；`SystemTiming` / `TickSummary` 只用 Warning 暴露性能慢点，阻断口径仍以 `blockingDebugErrors=0` 为准。`totalElapsedMs` 仍包含 bootstrap / cleanup 周边成本，只用于 runner 总耗时观察，不用于评价 Runtime Core hot path。

Debugger summary：

```text
events=62, dropped=0, warnings=22, errors=0, blockingErrors=0,
requests=54, specs=8, deltas=34, facts=96, cues=8, presentation=88,
activeEffectOwners=4, activeEffectSlots=0, queryBudget=14,
lookupBudget=1, randomLookupBudget=1, syncQueryBudget=14,
frameBackbonePhases=8, streams=6, migrationCarriers=6,
profilerMarkerContracts=4, journalingMarkerContracts=1
```

Timing summary：

```text
ecsRuntimeTickOnly=true
GASTickTotal(samples=7, avgMs=0.489, maxMs=0.876)
GASFramePrepareSystemGroup(samples=7, avgMs=0.036, maxMs=0.161)
GASCommandResolveSystemGroup(samples=7, avgMs=0.082, maxMs=0.203)
GASCoreSimulationSystemGroup(samples=7, avgMs=0.227, maxMs=0.321)
GASStructuralCommitSystemGroup(samples=7, avgMs=0.013, maxMs=0.024)
GASBoundaryProjectionSystemGroup(samples=7, avgMs=0.131, maxMs=0.187)
```

Official tool diff：

```text
journalingAvailable=True, journalingCaptured=True,
journalingWorldRecords=2128, runtimeStructuralApprox=18,
journalingStructural=286, deltaStructural=-268,
runtimeCreates=18, journalingCreates=33, deltaCreates=-15,
runtimeDestroys=0, journalingDestroys=22, deltaDestroys=-22,
journalingAddComponents=5, journalingRemoveComponents=0,
journalingSetComponentData=0, journalingSetBuffer=0,
journalingGetComponentDataRW=349, journalingGetBufferRW=1493,
profilerAvailable=True, profilerEnabled=False,
structuralProfilerCategoryEnabled=False, memoryProfilerCategoryEnabled=False,
profilerCaptureState=profiler disabled; Entities profiler modules collect no data
```

Functional x1 稳态样本只证明最小链路已避开小规模 `NativeStream + Complete` 固定成本；它不能代表真实规模优秀态。x50 已通过 AIBridge 跑通真实 Editor + Runtime-only 官方 Profiler capture，后续 x100 / Player profile 必须继续用 Unity Profiler 与 Runtime Debugger counters 做差分，再决定 Runtime Core 内部哪些 `NativeStream` fan-in、sync query 和 observation projection 需要合并或改为更粗粒度批处理。

Leak trace 复跑使用 `UNITY_JOBS_NATIVE_LEAK_DETECTION_MODE=2`，同一链路得到 `completed=True`、`summaryHash=0x53F70297`、`debugErrors=0`、`blockingDebugErrors=0`，日志中没有 `Leak Detected` 或 Native Collection 未释放提示。Layer 2 official diff 在 batchmode 临时启用 Entities Journaling 后会恢复状态并释放本次采样产生的 Journaling 持久状态，避免把官方工具缓存误判为 Runtime Core 泄漏。

按 `Library/PackageCache/com.unity.entities@e90944159b94/Documentation~/profiler-modules-entities-introduction.md`，Entities Profiler module 未启用时不会采集数据；batchmode 无头只声明 disabled reason，真实 Editor PlayMode 则由 `ProfilerDriver` 保存 Runtime-only `.data`。Entities Journaling 按 `entities-journaling.md` 的 `Unity.Entities.EntitiesJournaling` API 作为无头官方差分来源。

## 当前 Diagnostic x50 数据

2026-05-29 使用 AIBridge 1.4.1 驱动真实 Unity Editor PlayMode，加载 `Assets/AutoChessDemo/Presentation/Scenes/HeadlessAutoChessDemo.unity`，由 `HeadlessAutoChessDemoSceneRunner` 控制 Unity Profiler 采集窗口，并通过 `ProfilerDriver.profileEditor=False` 保存官方 Runtime-only capture 到 `Temp/AutoChessDemo-PlayMode-X50-RuntimeProfile.data`。该文件属于本机证据，不提交。

```text
completed=True, winner=Player, scale=50, units=200,
battleTicks=9, totalTicks=10, warmupDroppedTicks=3, measuredTicks=7,
commands=900, finishers=400, attributeChanges=1050,
executionOutputs=250, cueRequests=400,
debugEvents=62, debugWarnings=30, debugErrors=0, blockingDebugErrors=0,
coreRequests=2700, coreFacts=4800, coreDeltas=1700, coreCues=400,
peakEventBus=1300, replayLag=0, journalingRecords=48288,
processWarmupRuns=1, totalElapsedMs=37.345,
factsHash=0xBA1E575F, summaryHash=0x0A96B07B, avgTickMs=2.614
```

```text
GASTickTotal(samples=7, avgMs=2.586, maxMs=6.285)
GASFramePrepareSystemGroup(samples=7, avgMs=0.117, maxMs=0.416)
GASCommandResolveSystemGroup(samples=7, avgMs=0.970, maxMs=2.682)
GASCoreSimulationSystemGroup(samples=7, avgMs=0.960, maxMs=2.070)
GASStructuralCommitSystemGroup(samples=7, avgMs=0.028, maxMs=0.056)
GASBoundaryProjectionSystemGroup(samples=7, avgMs=0.511, maxMs=1.061)
```

```text
journalingCaptured=True, journalingWorldRecords=48288,
runtimeStructuralApprox=900, journalingStructural=14202,
journalingCreates=1601, journalingDestroys=1100,
journalingAddComponents=201, journalingGetComponentDataRW=6791,
journalingGetBufferRW=27295, profilerAvailable=True,
profilerEnabled=True, structuralProfilerCategoryEnabled=True,
memoryProfilerCategoryEnabled=True
```

x50 是 50 组独立 2v2，共 200 units。该数据用于放大 Runtime Core 热点，不替代 Unity Profiler Timeline / Entities module 的最终归因；项目 Debugger 只保留 GAS 语义 counters、official diff 和无头导出。

PlayMode 官方 RawFrameDataView 在 Runtime-only capture 中过滤到 `EX_GAS_World` 后的 TopN：

```text
GASCommandResolveSystemGroup totalMs=82.738 maxMs=32.582 count=10
GASCoreSimulationSystemGroup totalMs=67.072 maxMs=32.510 count=10
GASBoundaryProjectionSystemGroup totalMs=32.601 maxMs=20.781 count=10
AbilityCommandRequestSystem totalMs=20.332 maxMs=10.406 count=20
GEExecutionCalculationExtensionSystemGroup totalMs=19.033 maxMs=12.140 count=10
AutoBattleExecuteDamageCalculationSystem totalMs=18.973 maxMs=12.123 count=10
DiagnosticsSnapshotSystem totalMs=18.149 maxMs=15.076 count=10
ASCInitializeRequestSystem totalMs=17.662 maxMs=16.385 count=11
AbilityCatalogCommitSystem totalMs=16.597 maxMs=13.989 count=10
AutoBattleCommandDriveSystem totalMs=15.491 maxMs=11.341 count=10
```
