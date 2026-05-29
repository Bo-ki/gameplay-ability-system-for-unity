# AutoChess Demo

`Assets/AutoChessDemo` 是 EX-GAS 2.0 四层架构中的 **Layer 1 Application Shell Layer / 业务验收层**。它用真实 DOTS 业务链验证 Runtime Core，但不属于 `Assets/GAS/Runtime`，也不拥有 Runtime Debugger 模块。

当前目录已经按破坏性重构收敛为 AutoBattle 最小验证链，旧 `HeadlessAutoChess*` 配置源、组件堆叠、规模压测 DTO 已从编译面清除。Demo 只通过 `GAS.Runtime` 的公开 Runtime API、Layer 2 Diagnostics snapshot 和 official tool diff 做验收。

## 最小链路跑通计划

本轮只验证当前 Runtime Core 是否能被真实 DOTS 业务链打穿，不扩展完整 4v4 自走棋机制：

1. `HeadlessAutoBattleScenario` 创建 4 个 ASC，初始化 Health / Energy，授予普攻与斩杀 Ability。
2. `AutoBattleDefinitionCatalogBuilder` 安装最小 `GASDefinitionCatalogBlob`：普攻 GE 走 instant modifier，斩杀 GE 走 active mutation + demo execution extension。
3. `HeadlessAutoChessRuntimeSystemBootstrap` 把 `AutoBattleCommandDriveSystem` 注册到 `GASCommandResolveSystemGroup`，把 `AutoBattleExecuteDamageCalculationSystem` 注册到 `GEExecutionCalculationExtensionSystemGroup`。
4. `AutoBattleCommandDriveSystem` 使用 `SystemAPI.Query` 收集 ASC 快照到 frame-local `NativeList`，按 BattleGroup 构建 frontline / lowest-health 目标缓存，O(1) 选择目标后直接批量写入 `AbilityCommandBuffer`，不再为小规模链路支付 `NativeStream + job schedule + Complete` 固定成本。
5. Runtime Core 消费 `AbilityCommandBuffer`，执行 Ability grant / activate、generated catalog commit、GE command stream、instant attribute delta、cue request、replay projection。
6. `AutoBattleExecuteDamageCalculationSystem` 读取 `GEEffectCommandBuffer` 中的斩杀 GE 命令，用 `BufferLookup<AttributeValueBuffer>` 按 TargetAsc 直接写目标属性，追加 `AttributeModifierBuffer` 和 `GameplayEventBuffer{ExecutionCalculationOutputUpdated}` typed fact；event bus 只保留同一 fact 的镜像。
7. `GameplayFactProjectionSystem` / `ReplayLogSystem` 统一收集 attribute changes、execution outputs、cue requests，`GAS.AutoChessDemo.HeadlessAutoChessRuntimeRunner.RunHeadlessAutoBattleOnce()` 用这些事实做无头断言。
8. Runtime Debugger 数据来自 Layer 2 `GasRuntimeDebugger` / `GasRuntimeOfficialToolDiffCapture`；Editor Debugger Window 是 Layer 1 Editor Extension，不放在 Demo 内。
9. 实机性能验证通过 AIBridge 驱动真实 Unity Editor / Player，并优先使用 Unity Profiler、Entities Profiler Modules、Entities Journaling 等官方工具；Demo 不重复实现 Debugger UI 或 Profiler。

## DOTS 约束

- Functional x1 数据量很小，按 Entities 官方 job overhead 建议优先使用直接 ECS query，避免调度成本超过业务计算本身；x50+ 规模 profile 才切换或补充 `IJobChunk + ChunkEntityEnumerator` 路径。
- AutoBattle AI 不在单位循环中调用 `EntityManager` 做结构变化；高频 command 使用 singleton DynamicBuffer 作为 frame-local stream，结构变化保留在 Runtime Core commit / cleanup phase。
- 当前最小 execution calculation 使用 `BufferLookup` 只处理 GE command 已解析出的少量 TargetAsc，避免 target x command 嵌套扫描；该随机访问边界必须保留在业务 execution extension 内，不能扩散成 Runtime Core 默认数据访问模式。
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
& .\Tools\Diagnostics\Analyze-AutoChessProfile.ps1 -PrintMarkdown
```

PlayMode runner 会以 `ProfilerDriver.profileEditor=False` 保存 Runtime-only 官方 capture 到 `Temp/AutoChessDemo-PlayMode-X50-RuntimeProfile.data`，并导出 `TestResults/AutoChess/T6-CHESS-AF-SceneRuntime/AutoChessPlayModeProfileSummary.txt`。这两个目录都是本地运行产物，不纳入版本控制。

`Tools/Diagnostics/Analyze-AutoChessProfile.ps1` 负责把 summary 文本解析为 `TestResults/AutoChess/Analysis/AutoChessProfileAnalysis.json` 和 `.md`，自动计算 cost split、RW lookup / enableable toggle 每 tick、Journaling TopN、Debugger drop rate 和反推架构失误。直接读原始 summary 会浪费上下文，后续性能复盘默认先看脚本报告。

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

2026-05-29 已将 x50 PlayMode 从 7 个 measured tick 的短样本改为 30 秒真实 Editor PlayMode 采样：`HeadlessAutoChessDemoSceneRunner` 逐帧推进 Runtime tick，场景序列化为 `scale=50`、`scenarioHealthMultiplier=2048`、`minimumProfileSeconds=30`，Profiler 使用 Runtime-only binary log + `ProfilerDriver.profileEditor=False` 保存到 `Temp/AutoChessDemo-PlayMode-X50-RuntimeProfile.data`。该文件属于本机证据，不提交。

```text
completed=True, winner=Player, scale=50, units=200,
battleTicks=4130, totalTicks=4131, warmupDroppedTicks=3, measuredTicks=4128,
commands=478300, finishers=244800, attributeChanges=478250,
executionOutputs=244750, cueRequests=233500,
debugErrors=0, blockingDebugErrors=0,
totalElapsedMs=30004.994, avgTickMs=1.561
```

```text
GASTickTotal(samples=4128, avgMs=1.548, maxMs=34.517)
GASFramePrepareSystemGroup(samples=4128, avgMs=0.042, maxMs=0.262)
GASCommandResolveSystemGroup(samples=4128, avgMs=0.612, maxMs=2.056)
GASCoreSimulationSystemGroup(samples=4128, avgMs=0.747, maxMs=2.202)
GASStructuralCommitSystemGroup(samples=4128, avgMs=0.020, maxMs=0.169)
GASBoundaryProjectionSystemGroup(samples=4128, avgMs=0.128, maxMs=32.746)
```

```text
Profiler capture:
saved=True, driverSaved=True, binaryLogBytes=44620948,
firstFrameIndex=3616, lastFrameIndex=4127,
profileEditor=False, profilingEnabled=False
```

同一 PlayMode run 的 Diagnostic pass 会打开 Runtime Debugger 和 presentation raw fact 投影，用于输出数据流图、时序图和 Layer 2 counters；它不作为性能 pass 的 `avgTickMs`：

```text
debugEvents=4096, dropped=18262, warnings=2052, errors=0, blockingErrors=0,
requests=1191000, specs=233500, deltas=956500, facts=3103100,
cues=233500, presentation=2869600, peakEventBus=1300, replayLag=0
```

Official tool diff 仍使用独立 pass，且本轮已经确认 Runtime hot window 内 create/destroy/add/remove 结构变化为 0；`EnableComponent` / `DisableComponent` 单独按 enableable toggle 解释，不再误算为 structural change：

```text
journalingWorldRecords=524288, journalingStructural=0,
journalingCreates=0, journalingDestroys=0,
journalingAddComponents=0, journalingRemoveComponents=0,
journalingEnableComponents=80400, journalingDisableComponents=40305,
journalingGetComponentDataRW=210478, journalingGetBufferRW=193105,
journalingRecordTopN=GetComponentDataRW=210478;GetBufferRW=193105;EnableComponent=80400;DisableComponent=40305
```

`journalingWorldRecords=524288` 已触达 Entities Journaling 当前记录上限，因此它只能用于 TopN 与热点方向，不能把绝对记录数当作完整总量。旧版 `journalingStructural=13502 / creates=1601 / destroys=1100` 是采样窗口污染和 enableable 误归类后的过时结论，已废弃。

x50 是 50 组独立 2v2，共 200 units。该数据用于放大 Runtime Core 热点，不替代 Unity Profiler Timeline / Entities module 的最终归因；项目 Debugger 只保留 GAS 语义 counters、official diff 和无头导出。性能 pass 明确关闭 Runtime Debugger 和 presentation raw fact 投影；Diagnostic pass 再打开完整 Layer 2 观测链，避免把 Debugger / presentation 成本误判为 Core Simulation 成本。

本轮定位到旧数据“不像 DOTS”的直接原因：

1. 7 个 measured tick 太短，无法形成稳定性能曲线；当前已改为 30 秒、4128 个 measured ticks。
2. 旧 scene runner 每 tick 在 `HeadlessAutoBattleScenario.RefreshUnits` 用 `EntityManager.GetBuffer` 刷新单位结果，污染 `NoExecutingSystem` 下的 `GetBufferRW`；当前胜负判断改由 `AutoBattleCommandDriveSystem` 在 ECS 内写 driver alive counters。
3. 旧 official diff 把 enableable toggle 当 structural change；当前 `EnableComponent` / `DisableComponent` 单列，create/destroy/add/remove 为 0。
4. ProfilerDriver frame history 在 Unity 6 Editor 内不能单独保证 `.data` 保存；当前以官方 `UnityEngine.Profiling.Profiler` binary log 为主，ProfilerDriver 保存为补充。

本轮修复结论：

1. 默认 World 自动创建、PlayerLoop 双推进、Profiler 采样窗口污染和 presentation raw fact 投影污染都已拆开；剩余性能数据才可用于 Runtime Core 归因。
2. 内部 `AbilityCommandBuffer` 是可信 Runtime command stream，不再每 tick 创建 request entity、读 Catalog 或为所有 stream command 做 Grant 扫描；外部 Grant 仍走请求实体路径。
3. AutoBattle 业务层不再用全局 hash 或 target x command 嵌套扫描模拟真实游戏规模；BattleGroup 目标缓存和 TargetAsc BufferLookup 使 Demo 成本不再遮蔽 GAS Core。
4. 当前 x50 仍未达到目标态 DOTS 性能线：`GASCoreSimulationSystemGroup avgMs=0.747`、`GASCommandResolveSystemGroup avgMs=0.612`、`GASTickTotal avgMs=1.548`。剩余热点是真实架构问题：`AbilityCatalogCommitSystem`、`AbilityCommandRequestSystem`、`AbilityStateCleanupSystem`、`AttributeRecalculateSystem` 和 frame stream prepare 仍有主线程随机访问 / enableable 写入 / replay fact 投影成本；后续应继续向 chunk-local batch、generated lookup cache、dirty attribute set 和更粗粒度 command fan-in 推进。
5. 脚本反推的核心错误不是“结构变化还很多”，而是 Phase / SystemGroup 已命名但模块 seam 不够深：CommandResolve + CoreSimulation 占 `87.8%`，StructuralCommit 只有 `1.3%`；Journaling 显示 `GetComponentDataRW=51.0/tick`、`GetBufferRW=46.8/tick`、enableable toggle `29.2/tick`。这说明当前设计仍把低层 ECS lookup、Ability 生命周期 marker flip 和属性重算细节泄漏给多个系统。
6. `AttributeValueBuffer` 是最大 component RW 热点（`95,150` 次），说明 Attribute store 仍缺少 dirty owner / dirty attribute set；`Debugger drop rate=81.7%` 且 Journaling 达到 `524,288` 上限，说明诊断层也必须默认聚合 counters，raw trace 只做短窗口采样。
