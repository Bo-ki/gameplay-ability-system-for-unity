# ISSUE-008 目标态 Spec 尚未充分 Unity Entities 机制化

## 状态

| 字段 | 内容 |
|---|---|
| 状态 | Active |
| 严重度 | P1 |
| 最近复核 | 2026-05-25 |
| 所属层 | GAS Runtime Core Layer / Definition & Generation Layer / 文档治理 |

## DOTS规则引用

| 规则编号 | 规则摘要 | 关联原因 |
|---|---|---|
| `SYS-02` | SystemGroup 是 phase owner，禁止手写 Tick 顺序 | 目标态 Spec 必须落到具体 ComponentSystemGroup |
| `QRY-01` | Hot path 优先 job 化；SystemAPI.Query 限于小规模/debug | Spec 需明确遍历方式选择标准 |
| `QRY-02` | Query contract 写清 All/Any/None/Disabled/ChangeFilter | Spec 需定义各 phase 的 query contract |
| `JOB-01` | 并行批处理说明 IJobEntity/IJobChunk/主线程选择理由 | Spec 需落到具体 job 类型 |
| `SC-01` ~ `SC-03` | 结构变化规则 | Spec 需明确哪个 SystemGroup 拥有结构变化权限 |
| `ECB-01` ~ `ECB-04` | ECB 规则 | Spec 需明确 ECB playback phase 和 allocator 策略 |
| `EN-01` ~ `EN-03` | Enableable 规则 | Spec 需落到具体 enableable toggle phase |
| `BUF-01` ~ `BUF-04` | Buffer 规则 | Spec 需声明 buffer capacity/spill/生命周期 |
| `CASE-01` ~ `CASE-47` | 官方案例模式 | 每个设计决策需能映射到官方案例的采用/拒绝理由 |
| `SEL-01` ~ `SEL-05` | API 选型基线 | 每个模块需完成 API 选型表和重选型触发条件 |
| `ODF-01` ~ `ODF-18` | 官方文档覆盖流程 | Spec 更新需走反哺闭环 |
| `PRF-09` | Query/Filter/Allocator/Dependency/Chunk layout 是架构输入 | 不是调优阶段才补的实现细节 |
| `PRF-33` | EntityQuery 通过 SystemState.GetEntityQuery 创建 | 禁止 EntityManager.CreateEntityQuery |
| `PRF-17` | 禁止使用 IAspect（已废弃 API） | API 选型以当前 PackageCache 非 deprecated API 为准 |

## 问题陈述

当前 GAS ECS 目标态在概念上已经从 OOP runtime 主链转向四层架构、phase / stream、typed facts 和 generated definition，但大量描述仍停留在项目自定义 ECS 抽象语言上。若不对齐 Unity Entities 1.4.6、Unity Physics 1.4.6、Entities Graphics 1.4.19 以及 Burst / Collections / Mathematics 官方机制和使用规则，后续实现仍可能出现“概念正确，但落地继续使用 request entity 高频结构变化、全局 DynamicBuffer 扫描、未定义 SystemGroup、未区分 ECB playback、Physics query / event 错帧、Entities Graphics 反向污染 Core、Profiler 无法对照、业务系统不遵守 Query / Buffer / Enableable / Baker 规范”等偏差。

## 当前证据

文档证据：

1. `03-RuntimeCore管线Spec.md` 原先只定义 `Command Ingest / Spec Evaluation / Delta Apply / Typed Fact Projection / Observation Projection`，没有明确每个 phase 对应的 Unity `ComponentSystemGroup`、ECB playback phase 和结构变化权限。
2. `04-EffectCommand-SpecStream-AttributeDeltaSpec.md` 原先把 `EffectCommand` 定义为语义契约，但没有明确它不等同 request entity，也没有说明高频 instant GE 应优先使用 command data / buffer / stream。
3. `05-ActiveEffectStoreSpec.md` 原先定义了 duration / stack / period 状态，但没有明确 Enableable、stable archetype、DynamicBuffer slot 和结构变化边界。
4. `06-Observation-Presentation-ReplaySpec.md` 原先把 typed facts / GameplayFactStream / outbox 分层，但没有明确 CoreReactionFact 与 BoundaryObservationFact 的 Unity ECS 承载差异。
5. `07-RuntimeCoreDebuggerSpec.md` 原先强调 request/spec/delta/fact counters，但没有明确对齐 Unity Profiler、Entities Structural Changes、Entities Journaling、GC alloc、sync point 和 query match。
6. `08-Luban-SourceGenerator配置生成链路Spec.md` 原先强调 static lookup / bake plan，但没有明确 generated artifact 到 Blob / Baker / validation graph 的分层落点。
7. `UnityDOTS官方文档参考/主题/01-Entities系统与World.md` 已补机制落点，`UnityDOTS官方文档参考/主题/90-规则编号索引.md` 已补初版规则编号和检查表。
8. 本轮进一步全量研读 `Entities 1.4.6 / Burst 1.8.29 / Collections 2.6.6 / Mathematics 1.3.3` 官方文档，发现此前校准仍偏 API 名称和机制入口，缺少 archetype / chunk 成本、system / job 固定开销、dependency、allocator、Burst HPC#、Mathematics、Profiler / Journaling 对照等跨包约束，已新增 `../../UnityDOTS官方文档参考/README.md`。
9. 再次结合官方文档复核目标态，发现现有设计仍容易把当前实现惯性固化为目标态，例如全局 DynamicBuffer、逐实体 ECB、request entity、singleton 或 enableable marker。已新增 `../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`，要求 Runtime Core 任务按使用情形评估 `NativeStream`、Chunk Component、Cleanup Component、EntityQuery bulk、`ComponentTypeSet`、`EntityQueryCaptureMode.AtPlayback`、system-associated entity、WriteGroup、WeakObjectReference / UnityObjectRef、Baking System 等候选 API。
10. 本轮继续深读 Query filter、SystemGroup / ICustomBootstrap、Job dependency、DynamicBuffer capacity、Cleanup Shared、Profiler Memory / Structural Changes、Journaling、Baking filter、Streaming、Collections allocator 和 Burst function pointer / vectorization 文档后，确认 `16` 还需要从“API 候选”升级为“DOTS API 能力地图”，并反推 Runtime Core 增加 Frame Arena / Query Preparation、ActiveEffectStore 四分层、Debugger 外部证据对照和 Luban 生成 query / bake / content plan。
11. 本轮继续深挖官方 DocCodeSamples / Tests / PerformanceTests 后，发现目标态还缺少“官方示例实际写法”的执行层证据。已新增 `../../UnityDOTS官方文档参考/主题/12-官方案例模式.md`，将 `SystemAPI.Query`、`IJobChunk`、`IJobEntity`、Lookup、DynamicBuffer、ECB、Enableable、Baker / Blob、SceneSystem / WeakObjectReference、PerformanceTests 等案例转成 `CASE-*` 规则。
12. 本轮继续按官方文档体系查缺补漏后，发现 `UnityDOTS官方文档参考/主题/*` 仍缺少统一覆盖矩阵和流程闭环 owner。已新增 `../../UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`，覆盖 sync point、system/job 固定开销、chunk fragmentation、buffer externalized、world time、allocator rewind、singleton dependency、Transform stale data、content management、LinkedEntityGroup、Baking filter、version / WriteGroup、Collections deterministic output、Burst vectorization、Mathematics RNG 和官方诊断工具，并转成 `ODF-*` 流程规则。
13. 本轮文档治理后，发现 `UnityDOTS官方文档参考/主题/*` 已经构成官方文档参考体系，但缺少独立主题入口。已新增 `../../UnityDOTS官方文档参考/README.md`，把 PackageCache 第一性版本、阅读顺序、规则层关系、行动报告要求和反哺路径收束为主题 owner。
14. Unity 升级到 `6000.3.14f1` 并新增 Unity Physics / Entities Graphics 后，目标态还必须把物理输入层和渲染表现层纳入官方参考体系。当前已将 Physics pipeline、`PhysicsWorldSingleton` / `SimulationSingleton`、collision query / event lifetime、collider authoring，以及 Entities Graphics SRP 前提、single render world、`RenderMeshArray` / `MaterialMeshInfo`、runtime creation、material override 和 render performance evidence 转成 `ODF-15..18`、`PHY-*`、`GFX-*` 规则。
15. 本轮使用 `../../UnityDOTS官方文档参考/` 重新审视目标态和任务树后，确认问题已经从“Spec 需要 Unity Entities 机制化”推进到“任务执行顺序需要 DOTS Backbone First”。`Frame Arena / Query Preparation` 已存在于 `../../01-目标态架构共识/03-RuntimeCore管线Spec.md`，但此前缺少独立任务 owner。当前已新增 `../../02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCore重构/README.md` 中的 `T1-RuntimeCore-AM2B Runtime Core Frame Backbone`，要求 AM3 / AM5 继续扩张前先建立 SystemGroup、frame owner、query / lookup / allocator / dependency budget、deterministic stream、structural playback gate 和 Debugger evidence gate。

当前版本证据：

1. 项目使用 `com.unity.entities` `1.4.6`、`com.unity.physics` `1.4.6`、`com.unity.entities.graphics` `1.4.19`：`Packages/manifest.json`。
2. 当前核心问题已经暴露出 Unity ECS 机制层风险：结构变化导致 `DynamicBuffer` handle invalidation，见 `ISSUE-004-结构变化边界脆弱.md`。
3. 当前性能诊断已证明只看 `avgTickMs` 和 system timing 不足以定位 Runtime Core 架构问题，见 `ISSUE-003-RuntimeCoreDebugger证据不足.md`。

## 执行路径

```text
目标态只写 GAS phase / stream 概念
-> Agent 实现时自行选择 Unity ECS 承载
-> 高频 command 继续创建 request entity 或 runtime GE entity
-> 结构变化、buffer pressure、sync point 和 query match 缺少硬预算
-> AutoChess profile 继续出现性能异常但难以归因
```

## 影响

1. Runtime Core 任务无法在领取前明确自己要修改哪个 Unity SystemGroup、哪个 ECB playback 点、哪个 buffer 生命周期。
2. Debugger 无法和 Unity Profiler / Entities Journaling 对照，性能结论仍可能停留在项目自定义指标。
3. Luban / SourceGenerator 的成果可能继续停留在 generated C# 文件，而不是 Blob / Baker / static lookup / query layout 优势。
4. 任务树虽然引用目标态 Spec，但 Spec 本身不够 Unity 化，会继续造成执行偏差。
5. 若任务缺少 API 选型表，Agent 仍可能把 AM2 的 singleton DynamicBuffer、旧 request entity 或逐实体 ECB 当作最终设计，继续造成性能和结构变化风险。
6. 若 AutoChess validation summary 不输出 API 健康指标，即使性能数字下降，也无法判断改善来自正确 DOTS 选型，还是来自绕过链路、减少业务负载或隐藏同步点。
7. 若 Runtime Core 不把 Query / Filter / Allocator / Dependency / Chunk layout 写进 phase contract，后续仍会出现 helper 隐式创建 query、SystemAPI foreach 隐式 complete、DynamicBuffer spill 无证据、job 过碎和 Burst function pointer 误用等偏差。
8. 若不拆分 Unity Physics / Entities Graphics 口径，后续会把 physics fixed-step、query、event consume、presentation marker 或 render cost 误归因到 GAS Runtime Core tick，继续干扰性能诊断。

## 根因反推

历史方案参考主要从 GAS 语义和业务案例推导目标架构；当前路线已经吸收了正确方向，但缺少以 Unity Entities 官方机制、API 选型、官方案例和官方文档参考体系为第一性约束的校准。目标态需要官方文档参考体系主题入口、机制级 Spec、使用规则 Spec、官方研读摘要、API 选型 Spec、官方案例研读 Spec 和查缺补漏流程闭环 Spec，并反向修订 Runtime Core、EffectCommand、ActiveEffectStore、Observation、Debugger 和 Luban 配置链。

本轮新增判断：问题不是“缺少几个 API 名称”，而是目标态曾把 Query、Allocator、Dependency、Chunk layout、Burst calculation 当成后续实现细节。对 Unity DOTS 来说这些都是架构输入，必须在任务领取前进入行动报告和验收指标。

本轮官方案例新增判断：问题也不是“把示例 API 抄进项目”。官方案例展示了写法边界，例如 `SystemAPI.Query` 入门 foreach 不等于百万实体 hot path 模板，ECB 并行记录不等于确定性 gameplay stream，SceneSystem / WeakObjectReference 只能是 Boundary / Presentation。后续任务必须用 `CASE-*` 明确采用或拒绝这些案例模式。

本轮主题化新增判断：问题还包括“官方文档结论没有独立主题 owner”。如果行动报告只引用 `UnityDOTS官方文档参考/主题/*` 文件名，而没有先通过 `UnityDOTS官方文档参考` 说明主题覆盖、PackageCache 第一性版本、规则层关系、`ODF-*` 规则和反哺 owner，仍可能继续发生文档已写但执行不消费的问题。

本轮 Physics / Graphics 新增判断：Unity Physics 和 Entities Graphics 都是 DOTS 体系的重要组成，但它们不能改变 GAS Runtime Core 的语义层边界。Physics 只进入 target acquisition / hit confirmation / spatial query / collision event 输入层；Entities Graphics 只进入 Presentation / Boundary 的真实渲染接入层。目标态必须把它们作为可选 profile 和独立成本口径，而不是把物理或渲染系统默认混入 Core tick。

本轮路线重排新增判断：当前四层架构模型不需要推翻，但继续从 AM3 / AM5 局部功能迁移推进会让旧 lifecycle mirror 和 proof-only stream 继续膨胀。Runtime Core 应先完成 `DOTS Backbone First`，把 SystemGroup / Frame Arena / query / lookup / allocator / dependency / deterministic stream / structural playback / Debugger evidence gate 变成前置骨架，再回到 GAS 语义功能迁移。

## 目标态入口

1. `../../UnityDOTS官方文档参考/主题/01-Entities系统与World.md`
2. `../../01-目标态架构共识/03-RuntimeCore管线Spec.md`
3. `../../01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
4. `../../01-目标态架构共识/05-ActiveEffectStoreSpec.md`
5. `../../01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`
6. `../../01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
7. `../../01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`
8. `../../UnityDOTS官方文档参考/README.md`
9. `../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
10. `../../UnityDOTS官方文档参考/主题/12-官方案例模式.md`
11. `../../UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`
12. `../../UnityDOTS官方文档参考/README.md`
13. `../../01-目标态架构共识/10-AutoChess无头验收Spec.md`
14. `../../01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`

## 任务入口

1. `../../02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCore重构/README.md`
2. `../../02-主线任务树/T4-Observation_Presentation_Debugger/RuntimeCoreDebugger/README.md`
3. `../../02-主线任务树/T2-Definition_Luban配置权威/LubanSourceGenerator配置链/README.md`
4. `../../02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCore重构/README.md#三级任务runtime-core-frame-backbone`

## 退出条件

1. Runtime Core 任务都能先追溯到 `UnityDOTS官方文档参考/README.md`，再追溯到 `UnityDOTS官方文档参考/主题/01-Entities系统与World.md`、`UnityDOTS官方文档参考/主题/90-规则编号索引.md`、`UnityDOTS官方文档参考/README.md`、`UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`、`UnityDOTS官方文档参考/主题/12-官方案例模式.md` 和 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`。
2. 目标态 Spec 明确 SystemGroup、ISystem/job、ECB playback、Enableable、DynamicBuffer、Blob/Baker、Query filter、Burst / Collections / Mathematics、Debugger 证据边界和 API 选型规则，并引用具体规则编号。
3. AutoChess validation summary 能输出 Unity ECS 机制级指标：entity lifecycle、ECB playback、buffer pressure、sync point、GC alloc、query matched chunks、archetype / chunk、system / job、lookup update、Burst warmup 状态。
4. 新增 Runtime Core 实现不再把 request entity / runtime GE entity 作为高频 instant GE 默认承载。
5. Runtime Core 任务行动报告和交还内容能说明遵守了 `SYS/JOB/QRY/SC/ECB/BUF/EN/FIL/DEF/NAT/DBG/SEL/CASE/ODF` 中哪些规则。
6. AM3 的 EffectCommand 和 AM5 的 ActiveEffectStore 在进入实现前完成 API 选型表，不再把全局 DynamicBuffer、request entity、逐实体 ECB 或 enableable marker 当作未经证明的默认答案。
7. AutoChess x50 / x100 / x1000 profile summary 输出 API 健康指标，并能标记 proof-only API、scale-ready API 和重新选型触发条件。
8. Runtime Core Spec 已明确 Frame Arena / Query Preparation、command 四分层、ActiveEffectStore 四分层、Boundary observation、Debugger 官方工具对照和 Luban 生成 query / bake / content plan。
9. 后续任务行动报告必须能引用 `SEL-21` 到 `SEL-32` 等新规则，否则视为尚未吸收 DOTS 深读结果。
10. 后续任务行动报告必须能引用 `CASE-01` 到 `CASE-12` 等官方案例规则，并说明采用 / 拒绝原因，否则视为尚未吸收官方案例研读结果。
11. 后续任务行动报告必须能引用 `ODF-01` 到 `ODF-18` 等官方文档覆盖流程规则，并说明相关 PackageCache 证据、采用 / 拒绝 / 暂不相关理由、反哺 owner 和验收指标，否则视为尚未吸收官方文档查缺补漏结果。
12. 涉及 Physics / Graphics 的任务必须说明 `PhysicsWorldSingleton`、`SimulationSingleton`、query broadphase、collision / trigger event 有效窗口、`RenderMeshArray`、`MaterialMeshInfo`、material override、draw command / BRG / Profiler evidence 的采用或不相关理由，并拆分 core / physics / render 成本。
13. `T1-RuntimeCore-AM2B Runtime Core Frame Backbone` 已完成 AM2B-A phase contract、AM2B-B frame budget contract、AM2B-C stream owner / deterministic merge contract、AM2B-D structural playback gate contract 和 AM2B-E debugger evidence gate contract；后续仍需完成 AM2B-F，才能要求 AM3 / AM5 行动报告完整复用 SystemGroup、frame owner、query / lookup budget、allocator owner、dependency wait、deterministic merge、structural playback 和 Debugger evidence gate。
