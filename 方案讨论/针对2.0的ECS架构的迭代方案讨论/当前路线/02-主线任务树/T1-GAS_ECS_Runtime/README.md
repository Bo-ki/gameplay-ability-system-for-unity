# T1 GAS ECS Runtime

## 节点定位

本主线负责 EX-GAS 2.0 的 GAS Runtime Core Layer。它承载 ASC、Ability、GameplayEffect、Attribute、Tag、Spec/Context、Capture、ExecutionCalculation、EffectCommand、AttributeDelta、ActiveEffectStore 等 runtime 权威语义。

## 当前问题

1. 当前 Runtime 仍有旧 GE entity lifecycle、global observation stream、managed query / helper 和 structural change 成本混在热路径中。
2. AutoChess 性能诊断显示继续做局部 fast path 不能解决核心管线失真。
3. 新业务如果继续接入旧 instant GE lifecycle，会扩大后续重构面。
4. DOTS 深读后确认 Query / Filter / Allocator / Dependency / Chunk layout / DynamicBuffer spill / Burst calculation 也属于 Runtime Core 架构输入；任务不能只报告 GAS 语义和 system timing。
5. 官方案例深挖后确认，Runtime Core 任务还必须对照 DocCodeSamples / Tests / PerformanceTests 的实际写法，避免把入门 `SystemAPI.Query`、ECB 示例、SceneSystem load 或 Baking System 案例误用于 Core hot path。
6. 官方文档查缺补漏后确认，Runtime Core 任务还必须先读 `UnityDOTS官方文档参考/README.md`，再检查 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 覆盖矩阵和 `ODF-*` 规则，避免官方文档已经写入但任务执行没有消费。
7. Unity Physics / Entities Graphics 新包接入后，Runtime Core 任务必须明确二者是否相关：Physics 只能作为目标获取 / 命中确认 / 空间 query 输入层，Entities Graphics 只能作为 Presentation / Boundary 渲染桥。
8. Unity DOTS 官方参考反推后确认，继续扩张 AM3 / AM5 前必须先建立 Runtime Core Frame Backbone；否则局部 proof stream 和 owner-local mirror 会继续遮蔽 query、lookup、allocator、dependency、structural playback 和 Debugger evidence 的真实归因。

## 目标态参考

1. `01-目标态架构共识/01-GAS概念模型Spec.md`
2. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
3. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
4. `01-目标态架构共识/05-ActiveEffectStoreSpec.md`
5. `01-目标态架构共识/10B-AutoChess完整业务案例设计Spec.md`
6. `01-目标态架构共识/12-命名规范Spec.md`
7. `UnityDOTS官方文档参考/README.md`
8. `UnityDOTS官方文档参考/主题/01-Entities系统与World.md`
9. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
10. `UnityDOTS官方文档参考/README.md`
11. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
12. `UnityDOTS官方文档参考/主题/12-官方案例模式.md`
13. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`
14. `01-目标态架构共识/90-目标态不变量.md`

## 历史方案参考

1. `历史方案参考/方案14.md`、`方案15.md` 的自走棋验收、四层模型和 Runtime Core 重构方向可参考。
2. `方案10.md`、`方案11.md` 的 OOP shell、Luban/Blob、显式调度信号可参考。
3. 托管 EventBus、SourceGenerator 生成 gameplay lifecycle、Debugger 反向驱动 simulation 的方向不可照搬。

## 主线目标

把 GAS 核心语义稳定在 GAS Runtime Core Layer，先建立 DOTS 原生 Runtime Core Frame Backbone，再用 `EffectCommand -> SpecStream -> AttributeDelta -> ActiveEffectStore -> TypedFacts` 替代旧生命周期混合管线。

## 非目标

1. 不为了短期性能继续扩张旧 GE lifecycle fast path。
2. 不让 Presentation / Debugger 成为 gameplay state 权威。
3. 不在语义稳定前推进大规模 jobify / generated runtime lifecycle。

## 执行范围

1. `Assets/GAS/Runtime/System`
2. `Assets/GAS/Runtime/Effect`
3. `Assets/GAS/Runtime/Ability`
4. `Assets/GAS/Runtime/Attribute`
5. `Assets/_Test/GAS/Runtime`

## 执行细则

1. 新链路必须使用目标态术语：`EffectCommand`、`SpecStream`、`AttributeDelta`、`ActiveEffectStore`、`TypedFacts`。
2. Simulation hot path 不拼接托管字符串，不依赖 global observation stream 做 high-frequency reaction。
3. 结构变化必须集中到明确 phase / ECB playback，禁止隐式 helper 触发。
4. 每个 Runtime Core 任务必须输出 API 选型表，说明是否评估 `NativeStream`、Chunk Component、Cleanup Component、EntityQuery bulk、system-associated entity、WeakObjectReference / UnityObjectRef 等候选 DOTS API。
5. 每个 Runtime Core 任务必须说明 query/filter 口径、allocator 生命周期、dependency wait、chunk utilization、DynamicBuffer spill 和 Burst warmup 影响。
6. AM3 相关任务必须区分 Boundary request、Core frame command、parallel fan-in stream、structural mutation request；AM5 相关任务必须区分 OwnerLocalStore、GlobalIndexedStore、LifecycleCleanupStore、ChunkSkipIndex。
7. 每个 Runtime Core 任务必须新增“官方案例对照”小节，说明采用 / 拒绝哪些 `CASE-*` 模式，以及这些模式如何影响实现取舍。
8. 每个 Runtime Core 任务必须新增“官方文档覆盖检查”小节，先说明 `UnityDOTS官方文档参考` 中的相关主题，再说明 PackageCache 证据、`ODF-*` 规则、采用 / 拒绝 / 暂不相关理由、反哺 owner 和验收指标。
9. 如果任务涉及目标获取、命中、范围、碰撞、触发器、表现资源或 rendered profile，行动报告必须覆盖 `ODF-15..18`；如果不涉及，必须给出 disabled / not-related reason。
10. AM3 / AM5 后续任务继续扩张前必须先引用 `T1-RuntimeCore-AM2B Runtime Core Frame Backbone`，并说明 SystemGroup、frame owner、structural playback gate 和 Debugger backbone counters 如何被复用。

## 验收门槛

1. 新业务默认走 command/spec/delta/store/facts 链路。
2. 旧 instant GE entity lifecycle 被标记为待迁移并逐步退出热路径。
3. AutoChess x1 / x50 能用 diagnostics 解释 Runtime Core 热点。
4. 涉及 Physics / Graphics 的链路能把 `physicsStepMs` / `renderMs` 从 `coreTickMs` 中拆出；未启用时 summary 输出 disabled reason。
5. Runtime Core Frame Backbone 完成前，AM3 / AM5 的局部 proof 不得被描述为 scale-ready 目标态主线。

## 测试链路

1. Runtime EditMode 测试。
2. AutoChess headless validation。
3. x50 profile / diagnostics summary。

## 支线索引

| 支线 | 文档 | 状态 |
|---|---|---|
| Runtime Core 重构 | [RuntimeCore重构.md](RuntimeCore重构.md) | 进行中 |
| Runtime Core Frame Backbone | [RuntimeCoreFrameBackbone.md](RuntimeCoreFrameBackbone.md) | 已完成 AM2B-A -> AM2B-F contract-first / rebind handoff；当前继续 RuntimeCore AM3 / AM5 缺口迁移，activation simple producer、ability cost self producer、Timeline ApplyEffects single-target / multi-target simple instant producer 与 AM5 period / overflow simple instant derived command 已迁入 stream |

## 交还规则

交还 Runtime 任务时必须同步 `00-当前架构事实/核心问题诊断.md`、相关 `01` Spec、当前支线状态和验证证据。
