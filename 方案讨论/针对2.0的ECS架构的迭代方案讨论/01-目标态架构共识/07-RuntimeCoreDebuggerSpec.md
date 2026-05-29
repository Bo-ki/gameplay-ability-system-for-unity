# Runtime Core Debugger Spec

## 目的

Runtime Core Debugger 用于回答“GAS 语义上发生了什么、哪些 Core 计数异常、哪些数据可以和 Unity 官方工具对齐”。在四层架构中，它的 Runtime 采样、snapshot、official tool diff 和 replay/export API 归属 **Layer 2 Runtime Boundary Layer** 的 `DiagnosticsSink` / `ReplaySink`，不是 simulation input，也不是 Unity Profiler 的替代品。

Editor Debugger Window 归属 **Layer 1 Application Shell Layer** 的 Editor Extension；无头 runner、AIBridge CLI 驱动和实机 Editor profile runner 也归属 Layer 1。二者必须消费同一套 Layer 2 `RuntimeDiagnosticsSnapshot` / official diff API，不能把窗口实现放入 Demo，也不能让 Demo 私有化 Debugger 数据链。

性能判断采用 **官方工具优先**：能用 Unity Profiler、Entities Profiler Modules、Entities Journaling、ProfilerRecorder / ProfilerDriver 和 AIBridge 真实 Editor / Player Runtime Bridge 获取的数据，不在项目内重复造完整 profiler。项目内 Debugger 只保留 GAS 语义 counter、thin snapshot、official tool diff 标记、无头可调用导出和必要的 runtime safety gate。

## 数据流图

```mermaid
flowchart TD
    Core["Layer 3 Runtime Core\nfacts / counters / markers"] --> Sink["Layer 2 DiagnosticsSink\nRuntime Debugger + ReplaySink"]
    Sink --> Snapshot["RuntimeDiagnosticsSnapshot"]
    Sink --> Official["OfficialToolDiff\nEntities Journaling / Profiler category state"]
    Sink --> Bridge["AIBridge / Unity Editor\nProfilerDriver / Profiler Window"]
    Snapshot --> Headless["Layer 1 Headless Runner\nbatchmode summary"]
    Snapshot --> Editor["Layer 1 Editor Debug Window\nEditor extension"]
    Official --> Headless
    Official --> Editor
    Bridge --> Editor
    Bridge --> Headless
```

## UML 类图

```mermaid
classDiagram
    class RuntimeDiagnosticsCounter {
        int Frame
        int RequestCount
        int SpecCount
        int DeltaCount
        int FactCount
        int EntityCreateCount
        int EntityDestroyCount
        int EcbPlaybackCount
    }
    class BufferPressureCounter {
        int BufferCode
        int Length
        int Capacity
        float UsageRatio
    }
    class SystemTimingCounter {
        FixedString64Bytes SystemName
        double AvgMs
        double MaxMs
        int Samples
    }
    class RuntimeDiagnosticsSnapshot {
        RuntimeDiagnosticsCounter core
        BufferPressureCounter buffers
        SystemTimingCounter timings
    }

    RuntimeDiagnosticsSnapshot --> RuntimeDiagnosticsCounter
    RuntimeDiagnosticsSnapshot --> BufferPressureCounter
    RuntimeDiagnosticsSnapshot --> SystemTimingCounter
```

## 时序图：x50 性能诊断

```mermaid
sequenceDiagram
    participant Shell as Layer 1 Runner / Editor Window
    participant Boundary as Layer 2 DiagnosticsSink
    participant Runtime as Layer 3 Runtime Core
    participant Unity as Unity Profiler / Journaling
    participant AIBridge as AIBridge CLI

    Shell->>Runtime: Tick Runtime through normal SystemGroup chain
    Runtime->>Boundary: Emit request/spec/delta/fact counters
    Runtime->>Boundary: Emit ECB / entity lifecycle / buffer pressure / cursor lag
    Boundary->>Unity: Enable/Read Entities Journaling when available
    Shell->>AIBridge: Drive real Editor / Player profile when official profiler evidence is needed
    AIBridge->>Unity: Open Profiler / capture ProfilerDriver data
    Boundary-->>Shell: RuntimeDiagnosticsSnapshot + OfficialToolDiff
    Shell->>Shell: Editor window render or headless validation summary
```

## 四层职责边界

1. **Layer 3 Runtime Core**：只产出数值 counter、typed fact、presentation marker、replay marker，不读取 Debugger 结果改变 gameplay。
2. **Layer 2 Runtime Boundary**：维护 `GasRuntimeDebugger`、`RuntimeDiagnosticsSnapshot`、`GasRuntimeOfficialToolDiffCapture` 等采样与导出 API；可读 Unity Entities Journaling，Profiler module 未启用时只能输出 disabled reason。
3. **Layer 1 Editor Extension**：实现 Debugger Window、图表、筛选、导出按钮；窗口不得直接写 Runtime Core component / buffer。
4. **Layer 1 Headless Runner**：复用 Layer 2 API 输出 batchmode log、Mermaid 数据流图、时序图和 CI gate；它不是 Debugger 数据源。
5. **AutoChessDemo**：作为 Layer 1 业务验收 Demo，只消费 DiagnosticsSink，不拥有 Runtime Debugger 模块。

## 必备 counters

1. request/spec/delta/fact/cue/presentation 计数。
2. entity create/destroy 与 ECB playback 次数。
3. transient buffer length/capacity/peak。
4. cursor lag / projection lag。
5. group/system timing aggregate（项目内只做薄采样，TopN / Timeline 以 Unity Profiler 为准）。
6. sync point / structural change phase / hot path structural violation。
7. query matched entity / chunk 数。
8. GC alloc per tick 与 managed callback count。
9. ActiveEffectStore owner / slot / capacity / state distribution / legacy-backed / externalized owner 计数。
10. official package version、PackageCache hash path、manifest / lock / actual version skew。
11. Burst AOT / Player 口径：Enable Burst、OptimizeFor、Safety Checks、CPU architecture、warning policy、warmup dropped ticks。
12. Boundary resource / managed component / Baking / prefab load state：managed bridge count、weak resource load state、`RequestEntityPrefabLoaded` / `PrefabLoadResult`、`IncludePrefab` policy。
13. Unity Physics 口径：physics step count、fixed-step cost、query count / batch size、broadphase sync count、collision event / trigger event count、event dropped / converted count。
14. Entities Graphics 口径：presentation marker cost、render proxy count、draw command、instances per draw、BRG marker、material override write count、render disabled reason。
15. bounded reaction pass 口径：pass count、command count、cutoff reason、next-frame seed count。

## Unity Entities 证据对齐

Runtime Core Debugger 是项目内证据源，但必须能与 Unity 官方工具对照：

| Unity 证据 | 项目内对应 |
|---|---|
| Profiler system markers | Unity Profiler / AIBridge-triggered `.data` capture 为主，`SystemTimingCounter` / group timing 只作无头兜底 |
| Entities Structural Changes | entity create/destroy/add/remove / ECB playback counters |
| Entities Journaling | structural event timeline / owner system |
| GC Alloc | `gcAllocBytesPerTick` |
| sync point | `syncPointCount` / `GASStructuralCommitSystemGroup` playback |
| query match | matched chunks / matched entities |

Debugger 不直接替代 Unity Profiler；它负责把 GAS 语义计数与 Unity ECS 机制计数绑定起来。实机 Editor / Player 调试默认通过 AIBridge 驱动 Unity 官方 Profiler 工具链，而不是在 GAS Runtime 内重复实现 profiler UI、timeline 或采样存储。

本轮 PackageCache 官方文档交叉检查后的硬约束：

1. Entities Journaling 可通过 `Unity.Entities.EntitiesJournaling` API 程序化读取，因此是当前无头链路的最小官方差分来源。
2. Unity Profiler / Entities Profiler Modules 是官方性能证据源；batchmode 未启用 module 时只能输出 disabled reason，真实 Editor / Player 则通过 AIBridge 调用 Profiler Window / `ProfilerDriver` 保存官方 `.data` capture。
3. Runtime Debugger 是项目自诊断，不能替代 Profiler / Entities Journaling / Burst Inspector；结论必须保留两套证据的差异。凡是官方工具能直接给出的数据，不在项目内重复造轮子。
4. Entities Journaling 会分配自己的记录内存；Layer 2 无头 official diff 若在 batchmode 中临时开启 Journaling，结束时必须恢复状态并清理本次采样产生的官方工具持久状态，避免污染 Runtime Core leak 验证。

## DOTS API 选型健康指标

Runtime Core Debugger 必须能解释 API 选型是否健康，而不只是记录运行结果：

| 选型维度 | Counter / Snapshot | 用途 |
|---|---|---|
| global buffer pressure | length / capacity / peak / spill / clear phase | 判断全局 DynamicBuffer 是否成为热点 |
| `NativeStream` merge | foreach count / block count / merge ms / allocator | 判断多 job fan-in 是否健康 |
| per-chunk skip | matched chunks / skipped chunks / skip reason | 判断 Chunk Component / chunk precheck 是否减少工作 |
| structural query batch | EntityQuery bulk count / `ComponentTypeSet` count / ECB playback count | 判断是否退化为逐实体结构变化 |
| singleton / system-associated state | singleton access count / dependency completion warning / cursor owner | 判断 singleton 和 system state 是否引入同步 |
| query / lookup | query count / lookup update count / random lookup count | 判断是否过多依赖随机访问 |
| reaction feedback | bounded pass count / next-frame seed count / cutoff reason | 判断 Gameplay Fact 是否形成同帧无界循环 |
| Burst evidence | Burst target count / warmup state / synchronous compile flag | 判断 hot path 是否真的 Burst 化 |
| deterministic output | sort key policy / stream partition / post-sort count / order violation count | 判断 gameplay 并行输出是否可复现 |
| NativeContainer owner | owner system / singleton / allocator / dispose state | 判断 Persistent / sampled container 是否泄漏或隐藏同步 |
| API proof marker | proofOnlyApi / scaleReadyApi / reselectTrigger | 区分 AM proof 承载和目标态承载 |
| query filter health | filtered / unfiltered entity count、changed chunk count、enableable wait | 判断 filter 是否真的降低工作量，是否隐藏同步等待 |
| chunk memory health | archetype count、chunk count、chunk capacity、unused entities、external components | 判断是否出现 chunk fragmentation、shared misuse 或过多 tag |
| DynamicBuffer storage | inline capacity、spill count、externalized buffer count、EnsureCapacity count | 判断 buffer 是否已经从 chunk-local 退化为外置 cache miss |
| allocator / arena | WorldUpdateAllocator、group allocator、TempJob、Persistent、rewind count、dispose handle | 判断 frame scratch 与长期 sink 生命周期是否正确 |
| dependency budget | read/write component set、system dependency wait、manual NativeContainer dependency | 判断 system/job 拆分是否造成无意义等待 |
| content / scene boundary | weak resource load state、SceneSystem structural count、SceneSection metadata count | 判断表现 / streaming 是否污染 Core hot path |
| official package evidence | package name / actual version / PackageCache hash path / manifest-lock skew | 判断官方文档证据是否可复核 |
| Burst AOT evidence | Editor or Player、Enable Burst、OptimizeFor、Safety Checks、CPU architecture、warning suppression、Inspector target | 判断性能数据是否混入 Burst 编译 / 设置差异 |
| managed boundary | managed component count、managed shared count、clone/dispose policy、GC allocation | 判断 Boundary / Presentation 是否反向污染 Core |
| prefab / baking boundary | embedded prefab count、EntityPrefabReference load state、PrefabLoadResult missing count、IncludePrefab query count | 判断 Demo / Cue / Summon 的资源和生命周期边界是否完整 |
| Unity Physics | physics step count / fixed step ms / query count / event count / broadphase sync | 判断物理输入是否成为热点，或是否错帧消费 |
| Entities Graphics | draw command / instances per draw / material override writes / BRG marker / render cost | 判断真实表现层是否健康，或是否污染 Core tick |

长期 cursor、phase state、sample sink owner 优先存 system-associated entity 或明确 singleton。Debugger hot path 只能写 numeric、FixedString、小型 counter 或 sampled stream；托管字符串 export 放 Boundary。

## 官方案例对齐

Debugger 的自动验收形态必须吸收官方 PerformanceTests 的写法：固定实体规模、warmup、measurement、allocator cleanup 和按 SampleGroup / TopN 输出，而不是单一平均值。参考 `UnityDOTS官方文档参考/主题/12-官方案例模式.md` 的 `CASE-12`。

Runtime summary 需要额外输出 `casePattern` / `caseViolation` 字段，用来标记当前 hot path 是否符合官方案例模式：

| CASE | Debugger 需要证明 |
|---|---|
| `CASE-01` | hot path 是否仍使用主线程 `SystemAPI.Query` 扫大规模实体 |
| `CASE-04` | enableable / chunk skip 是否用 `ChunkEntityEnumerator` / `EnabledMask`，skip count 是否有效 |
| `CASE-07` | DynamicBuffer 是否有 internal capacity、spill、clear phase 和 Append 目标稳定性 |
| `CASE-08` | ECB 是否只在 `GASStructuralCommitSystemGroup` 播放，是否有 sort key policy |
| `CASE-10` | Definition 是否进入 Blob / Baker / generated lookup，而不是 runtime managed config |
| `CASE-11` | Scene / WeakObjectReference / UnityObjectRef 是否只在 Boundary / Presentation 产生 load marker |
| `CASE-12` | performance summary 是否包含 warmup、measurement、p95/max、TopN、allocator cleanup |

## 官方文档覆盖对齐

Debugger 必须先对齐 `UnityDOTS官方文档参考/README.md`，再吸收 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 的 `ODF-*` 规则。Runtime summary 需要额外输出 `officialDocTopic` / `odfRule` / `unityToolEvidence` 或等价字段，用来标记当前热点是否已经具备官方文档证据闭环：

| ODF / 主题 | Debugger 需要证明 |
|---|---|
| `ODF-01` 官方文档包 | 当前任务是否读取官方文档参考体系主题入口，并引用 `UnityDOTS官方文档参考/主题/*` 中相关文件 |
| `ODF-06` API 选型覆盖 | 当前 proof API 是否存在重新选型触发条件 |
| `ODF-07` 官方工具证据 | 项目 counter 是否能映射到 Systems window、Query window、Profiler、Journaling 或 Binary debugging |
| Sync point / direct reference | 是否存在结构变化后继续读写 buffer / lookup / handle 的风险 |
| System / job 固定开销 | system count、job count、lookup update count、schedule overhead 是否异常 |
| Chunk / buffer 质量 | chunk utilization、unused entities、buffer externalized、shared unique value 是否异常 |
| Allocator rewind | WorldUpdateAllocator / group allocator / Persistent owner / dispose 是否符合生命周期 |
| World time / deterministic | fixed step、world time、warmup dropped tick、battle hash 是否可复现 |
| PackageCache version evidence | official package version、hash path、manifest / lock 差异是否输出 |
| Burst AOT / Inspector evidence | Player AOT 设置、OptimizeFor、warning policy、Inspector target 是否可追溯 |
| Managed / prefab / baking boundary | managed resource clone / dispose、EntityPrefabReference load、Baking world 策略是否被记录 |
| `ODF-15` Unity Physics 时序 | `PhysicsSystemGroup`、FixedStep、`PhysicsWorldSingleton` / `SimulationSingleton`、query broadphase 和 event 有效窗口是否记录 |
| `ODF-16` Physics 配置生成 | collider / body / filter 是否来自 Definition & Generation，runtime 修改 collider 是否说明共享 blob 和时序风险 |
| `ODF-17` Entities Graphics 边界 | URP/HDRP、single render world、`RenderMeshArray` / `MaterialMeshInfo` 和 `RenderMeshUtility.AddComponents` 边界是否记录 |
| `ODF-18` Render evidence | `coreTickMs`、presentation marker cost、Entities Graphics render cost 是否拆分，并有 draw command / BRG / Profiler 证据 |

## 官方工具对照矩阵

项目内 Debugger 必须能和 Unity 官方工具形成最小闭环：

| 项目内问题 | 官方工具 / 文档机制 | 对照方法 |
|---|---|---|
| 结构变化过多 | Entities Structural Changes Profiler / Entities Journaling | 按 world -> system -> create/destroy/add/remove 对照 `structural query batch` 和 ECB playback |
| chunk 利用率差 | Entities Memory Profiler / Archetypes window | 对照 archetype count、chunk count、unused entities、external components |
| query filter 无效 | Query filtered / unfiltered count + change version | 对照 changed chunk count、ignore filter count、enableable wait |
| Burst 未真实生效 | Burst Inspector / warmup marker | 对照 Burst target count、synchronous compile flag、first tick dropped |
| job 调度过碎 | Profiler system marker / job marker | 对照 system count、job count、schedule overhead、TopN |
| allocator 泄漏或生命周期错误 | Collections safety / dispose / rewind marker | 对照 allocator owner、Persistent dispose、TempJob age、rewind invalidation |
| resource / scene 污染 Core | SceneSystem structural count / weak reference load state | 对照 Boundary load marker，不允许 Core tick 等待资源 |
| Player AOT 与 Editor 数据混淆 | Burst AOT Settings / Burst Inspector | 对照 Enable Burst、OptimizeFor、Safety Checks、CPU architecture、warning policy、warmup dropped ticks |
| managed 表现桥污染 Core | Managed component 文档 / GC Alloc | 对照 managed bridge count、GC alloc、clone/dispose policy，Core tick 内必须为 0 |
| prefab 加载状态缺失 | Entity prefab / SceneSystem 文档 | 对照 RequestEntityPrefabLoaded、PrefabLoadResult、IncludePrefab query policy |
| 物理输入错帧或过慢 | Unity Physics pipeline / singleton / event 文档 | 对照 physics step、query broadphase、Simulation event window、event dropped / converted count |
| 渲染成本混入 Core tick | Entities Graphics performance / Frame Debugger / Profiler | 对照 draw command、instances per draw、BRG markers、presentation marker cost 和 render cost |

## 当前 AutoBattle 无头证据

2026-05-29 使用 AIBridge 1.4.1 驱动真实 Unity Editor，调用 `GAS.AutoChessDemo.HeadlessAutoChessRuntimeRunner.RunHeadlessAutoBattleOnce` 跑通 Diagnostic x50。当前证据采用 warmup-dropped 口径：bootstrap / ASC 创建 / Ability grant / 首帧 SystemGroup 初始化 / Journaling 启用 / Burst 与 Editor warmup 不计入 `avgTickMs`，Runtime Debugger 只从 measured tick 开始记录 `SystemTiming`。

```text
completed=True, winner=Player, scale=50, units=200,
battleTicks=9, totalTicks=10, warmupDroppedTicks=3, measuredTicks=7,
commands=900, finishers=400, attributeChanges=1050,
executionOutputs=250, cueRequests=400,
debugEvents=62, debugWarnings=27, debugErrors=0, blockingDebugErrors=0,
coreRequests=2700, coreFacts=4800, coreDeltas=1700, coreCues=400,
peakEventBus=1300, replayLag=0, journalingRecords=48288,
processWarmupRuns=1, totalElapsedMs=36.488,
factsHash=0xA5CD85FF, summaryHash=0x69F5175B, avgTickMs=2.646
```

Timing snapshot：

```text
ecsRuntimeTickOnly=true
GASTickTotal(samples=7, avgMs=2.628, maxMs=6.696)
GASFramePrepareSystemGroup(samples=7, avgMs=0.061, maxMs=0.100)
GASCommandResolveSystemGroup(samples=7, avgMs=0.853, maxMs=2.055)
GASCoreSimulationSystemGroup(samples=7, avgMs=1.114, maxMs=2.912)
GASStructuralCommitSystemGroup(samples=7, avgMs=0.026, maxMs=0.056)
GASBoundaryProjectionSystemGroup(samples=7, avgMs=0.573, maxMs=1.572)
```

Official tool diff：

```text
journalingAvailable=True, journalingCaptured=True, journalingWorldRecords=48288,
runtimeStructuralApprox=900, journalingStructural=14202, deltaStructural=-13302,
runtimeCreates=900, journalingCreates=1601, deltaCreates=-701,
runtimeDestroys=0, journalingDestroys=1100, deltaDestroys=-1100,
journalingAddComponents=201, journalingRemoveComponents=0,
journalingSetComponentData=0, journalingSetBuffer=0,
journalingGetComponentDataRW=6791, journalingGetBufferRW=27295,
profilerAvailable=True, profilerEnabled=True,
structuralProfilerCategoryEnabled=True, memoryProfilerCategoryEnabled=True,
profilerCaptureState=profiler enabled; module counter data not exported by headless runner
```

解释：

1. `blockingDebugErrors=0` 表示功能 gate 没有非 timing 类诊断错误；`debugErrors=0` 表示慢 timing 事件已经从错误语义中拆出。`SystemTiming` / `TickSummary` 最高只产生 Warning，用 slow timing / TopN 解释性能，不污染功能错误计数。
2. x50 当前代表 50 组独立 2v2 并行跑在同一个 ECS World，用数量模拟真实游戏规模；AutoBattle AI 已按 BattleGroup 分组索引选敌，避免 Demo O(n^2) 全局搜敌污染 Runtime Core 判断。
3. 新口径只采样 measured ticks；AutoBattle 业务侧已按官方 `ecs-workflow-intro.md` / `job-overhead.md` 建议，把小批量 `NativeStream + job schedule + Complete` 固定成本切回主线程直通，大批量保留 chunk/job 路径。剩余热点集中在 `GASCoreSimulationSystemGroup`、`GASCommandResolveSystemGroup` 和 Boundary Projection，需要用 Unity Profiler `.data` 的 Timeline / Entities module TopN 继续定位。
4. AIBridge 1.4.1 已接入项目并通过真实 Unity Editor 跑通：`compile unity` 成功、Error 日志 0、`Window/Analysis/Profiler` 可由 CLI 打开，`ProfilerDriver` 已保存官方 capture 到 `Temp/AutoChessDemo-AIBridge-X50-EditorProfile.data`。该文件位于 ignored `Temp/`，作为本轮本机证据，不纳入版本控制。
5. Journaling 记录数明显高于项目 `runtimeStructuralApprox`，说明当前项目 counter 只覆盖 Core 自认结构变化，官方记录还包含 bootstrap / cleanup / package 内部读写；结构变化归因以 Unity Entities Structural Changes Profiler / Journaling 为准，项目 counter 只做 GAS 语义映射。
6. 后续 Debugger 不再扩展成自研 profiler UI。Layer 2 保留无头 snapshot 和 official diff；Layer 1 Editor Extension / AIBridge / Unity Profiler 负责可视化、timeline、TopN、Profiler capture 和实机工具差分。

## AM-1 baseline 采样口径

1. `runtimeCoreCounters` 输出累计 request/spec/delta/fact/cue/presentation、entityCreates、entityDestroys、ecbPlaybacks。
2. `runtimeCoreCountersPeak` 输出 activeEffectEntities、applyRequestEntities、eventBusBufferLength、presentationCursorLag、replayCursorLag。
3. AM-2 开始接入 EffectCommand / Spec/Delta/Fact 语义链 / AttributeDelta 权威 stream 计数；在 simple instant evaluation 迁移完成前，旧 EventBus 和 GE runtime entity 采样仍作为迁移期近似量级。
4. ECB playback baseline 可以先覆盖主要工具路径；最终验收需要能按 system / phase 定位结构变化预算。
5. AM-5 owner-local ActiveEffectStore baseline 输出 `runtimeCoreActiveEffectStore`，字段包括 owners、slots、capacity、pendingApply、active、inhibited、pendingRemove、legacyBacked、externalizedOwners；compact / cleanup / chunk skip 在对应 store 层实现后继续扩展。

## 禁止方向

1. Runtime system 读取 Debugger 结果改变 gameplay。
2. 在 hot path 拼接托管字符串或调用 `string.Format`（含 Debugger hot path）。
3. 把 Debugger 和 Replay 合并为同一事实流。
4. 只输出 `avgTickMs`，不输出结构变化、sync point、buffer pressure、query match 和语义计数。
5. 只输出 Physics / Graphics enabled 状态，不输出相关 counters 或 disabled reason。
6. 把 Physics fixed-step、Presentation marker 或 Entities Graphics render cost 混入 Runtime Core tick 后直接归因 GAS Core。

## 验收

**性能数据默认采样口径**：所有性能数据默认以 `ecsRuntimeTickOnly` 为采样口径。涉及 Physics / Presentation / Entities Graphics 成本叠加的结论须单独标注污染来源。此声明对应 `90-目标态不变量.md` 第 13 条和 `03-RuntimeCore管线Spec.md` 的 timing budget 定义。

1. x1 默认链路通过且输出 diagnostics summary。
2. x50 能输出热点系统、buffer pressure、entity lifecycle 计数。
3. system timing 报告必须标注是否污染 `ecsRuntimeTickOnly`。
4. Runtime Core Debugger counters 能与 Unity Profiler / Entities Journaling 的结构变化证据对照。
5. Debugger summary 必须输出 `SEL-*` API 选型健康指标：global buffer pressure、NativeStream merge、per-chunk skip、structural query batch、singleton dependency warning。
6. Debugger summary 必须能标记 proof-only API，并输出重新选型触发条件；否则 AM2/AM3 的临时承载容易被误判为最终目标态。
7. Debugger summary 必须输出 `ODF-*` 官方文档覆盖检查结果，并能说明哪些官方工具或文档机制可验证当前热点。
8. 涉及 Physics / Graphics 的验证必须输出 `PHY-*` / `GFX-*` 相关 counters；未启用时必须输出 disabled reason，避免把无头缺省误判为链路缺失。

## 历史方案定位

1. GASDebugger 结构化日志、Mermaid 时序导出和属性变化查询来自 `../历史方案参考/方案15.md:529-622`。
2. 自走棋 Debug 工作流中通过 GASDebugger 排查数值异常来自 `../历史方案参考/方案15.md:1214-1253`。
3. 完整 Debug workflow 和中毒爆发场景时序导出来自 `../历史方案参考/方案15.md:2661-2794`。
4. 固定容量结构化日志 / ring buffer 的参考来自 `../历史方案参考/方案14.md:635-694`。
