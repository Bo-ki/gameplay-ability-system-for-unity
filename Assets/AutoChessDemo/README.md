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

当前真实热路径仍不是最终优秀态：Functional x1 只有 4 个单位，AutoBattle 业务侧已移除 `NativeStream + Complete` 固定成本，`GASCommandResolveSystemGroup` 回落到 `0.082ms`；剩余热点主要在 `GASCoreSimulationSystemGroup` 和 Boundary Projection。后续 x50/x100 profile 必须用 CPU Profiler 与 Runtime Debugger counters 做差分，再决定 Runtime Core 内部哪些 `NativeStream` fan-in、sync query 和 observation projection 需要合并或改为更粗粒度批处理。

Leak trace 复跑使用 `UNITY_JOBS_NATIVE_LEAK_DETECTION_MODE=2`，同一链路得到 `completed=True`、`summaryHash=0x53F70297`、`debugErrors=0`、`blockingDebugErrors=0`，日志中没有 `Leak Detected` 或 Native Collection 未释放提示。Layer 2 official diff 在 batchmode 临时启用 Entities Journaling 后会恢复状态并释放本次采样产生的 Journaling 持久状态，避免把官方工具缓存误判为 Runtime Core 泄漏。

按 `Library/PackageCache/com.unity.entities@e90944159b94/Documentation~/profiler-modules-entities-introduction.md`，Entities Profiler module 未启用时不会采集数据；因此当前只声明 Profiler disabled reason，不声明 profiler captured。Entities Journaling 则按 `entities-journaling.md` 的 `Unity.Entities.EntitiesJournaling` API 作为无头官方差分来源。
