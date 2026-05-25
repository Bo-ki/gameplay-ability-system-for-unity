# Runtime Validation Demo - AutoChess 无头验收

## 父节点

[T6 Runtime Validation Demo](README.md)

## 节点定位

本支线负责 AutoChess 无头业务验收链路，用自动运行、自动结算、自动验证的业务场景验证 Runtime Core、配置链、Debugger 和表现/Cue 边界。

## 当前问题

1. AutoChess 能暴露 Runtime Core 管线问题，但 diagnostics 还不够强，难以解释热点。
2. 无头链路如果只保留数值测试，会丢失 UI / VFX / SFX / FloatingText / Cue 的表现边界验证。
3. x50 / x100 / x1000 / x10w / x100w scale gates 需要先服务诊断，再服务优化和目标架构压力设计。
4. AutoChess 根目录已迁移到 `Assets/AutoChessDemo`，文件级目录已拆为 Config / Simulation / Observation / Presentation / Validation / Debugging。
5. 当前实现仍存在 `HeadlessAutoChessScenario`、generated definition rows、presentation marker projection 等巨类，配置层、逻辑层、表现层和验证层在文件内部仍有混杂；其中 `HeadlessAutoChessScenario` 已开始拆为 constants / state / bootstrap / variants / unit definitions / unit resolution / runtime timing / runtime lifecycle / types partial files。
6. 官方案例深挖后，AutoChess 验收必须对齐 `CASE-12` 的 performance case 口径，并用 `CASE-10/11` 保留配置、资源和表现边界。
7. 官方文档查缺补漏后，AutoChess 验收必须对齐 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 覆盖矩阵中的 world time、fixed step / custom world、allocator rewind、chunk fragmentation、buffer externalized、weak resource load state、Transform stale-data policy、LinkedEntityGroup 和 deterministic RNG。其中 custom world 创建必须采用 `CASE-17`（ICustomBootstrap）模式，headless runner 通过 `ICustomBootstrap.Initialize` + `DefaultWorldInitialization.GetAllSystems` 创建 `FixedStepTime(1.0f / 60f)` 独立 World，不隐式依赖 Editor `World.Time` 或 `VariableStepTime`。
8. Unity Physics / Entities Graphics 新包接入后，AutoChess 必须新增三种验收 profile 口径：默认 headless profile 输出 Physics / Graphics disabled reason；physics-enabled profile 验证 target acquisition / hit confirmation / collision event 到 GAS command / fact 的转换；rendered profile 验证 presentation marker 到 `RenderMeshArray` / `MaterialMeshInfo` / material override 的边界。
9. `10B-AutoChess完整业务案例设计Spec` 已提供完整的 GAS 业务设计预演（棋子/技能/GE/羁绊系统 + C# ISystem 代码 + 15+ Runtime Core 基础设施类型），本支线的代码实现需以 10B 为目标态设计参照，但实际代码迁移仍需等 Runtime Core Frame Backbone 闭合。

## 目标态参考

1. `01-目标态架构共识/10-AutoChess无头验收Spec.md`
2. `01-目标态架构共识/10B-AutoChess完整业务案例设计Spec.md`
3. `01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`
4. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
5. `01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`
6. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
7. `UnityDOTS官方文档参考/主题/12-官方案例模式.md`（特别是 `CASE-17: ICustomBootstrap` 用于 headless validation world 创建）
8. `UnityDOTS官方文档参考/README.md`
9. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`

## 历史方案参考

1. `历史方案参考/方案14.md`、`方案15.md` 的自走棋业务闭环、四层模型、Debugger workflow 和图表表达可参考。
2. `方案12.md`、`方案13.md` 的当前 demo 真实业务案例不作为本支线主输入，只作为后置对照。

## 支线目标

用无画面但表现逻辑完整的 AutoChess demo，持续验证 Runtime Core、Definition 配置链、Debugger、Observation / Presentation 边界是否成立。默认业务链路必须精而完整：少数代表性链路打穿 GAS 全链路，而不是堆叠大量机制。

## 当前状态

暂停。本轮已完成 `T6-AutoChess-DemoRefactor` 的基础目录迁移、独立 asmdef、runtime system bootstrap、Runtime Core 反向依赖拆除、文件级分层，并开始 `HeadlessAutoChessScenario` 内部职责拆分；但按当前路线，GAS Runtime 架构重构完成前不继续推进 AutoChess 拆分。后续 AutoChess 只作为 Runtime 重构后的全链路验收、性能日志分析和问题归因入口。

## 非目标

1. 不接真实 UI / VFX / SFX 资源。
2. 不新增业务机制掩盖 Runtime Core 问题。
3. 不把无头 demo 降级为纯战斗数值测试。

## 前置依赖

1. T1 Runtime Core 重构提供待验收链路。
2. T4 Runtime Core Debugger 提供 diagnostics counters。

## 执行范围

1. 目标目录：`Assets/AutoChessDemo`
2. 原迁移来源：`Assets/GAS/Runtime/Demo/AutoChess`
3. AutoChess validation summary / profile export
4. SceneRuntime runner / headless runner

## 执行细则

1. UI / VFX / SFX / FloatingText / Cue 用 log marker 占位，但逻辑触发不能省略。
2. 报告必须区分 core simulation、observation projection、presentation marker、export / bootstrap 成本。Headless runner 必须通过 `ICustomBootstrap`（CASE-17）创建独立 `FixedStepTime` World，不隐式依赖 Editor `World.Time`。
3. x50 以上规模 gate 先用于放大热点和定位问题。
4. 十万 / 百万实体压力测试通过 `ScaleProfile` 配置设计，允许采样表现 marker，但不能绕过 Ability / GE / Attribute / Facts contracts。
5. validation summary 必须输出 API 选型健康指标，至少包含 global buffer pressure、NativeStream merge、chunk skip、lookup count、structural query batch 和 deterministic output policy。
6. validation summary 必须输出官方案例对照结果，至少覆盖 hot path foreach、buffer spill、ECB sort key / playback、Baker / Blob、Scene / WeakObjectReference boundary 和 performance warmup / measurement。
7. validation summary 必须输出官方文档覆盖检查结果，至少覆盖官方文档参考体系主题、`ODF-*`、world time / fixed step、allocator rewind、chunk fragmentation、DynamicBuffer externalized、resource load / release、deterministic RNG 和 Unity 工具对照。
8. 涉及目标获取、命中确认、碰撞、触发器、表现资源或 rendered profile 时，必须显式检查 `ODF-15..18`；默认无头未启用真实 Physics / Graphics 时，也必须输出 `physicsDisabledReason` 和 `entitiesGraphicsDisabledReason`。
9. Physics profile 只能把 `PhysicsWorldSingleton` / `SimulationSingleton` query / event 转换成 Demo command 或 fact，不能直接修改 GAS Core state。
10. Entities Graphics profile 只能由 Presentation / Boundary 消费 marker 后写入渲染桥；`RenderMeshUtility.AddComponents` 只能作为 prototype / 低频入口，不能进入规模化 runtime hot path。

## 验收门槛

1. 默认链路 `passed=true`。
2. x50 能输出 request/spec/delta/fact/entity lifecycle/buffer pressure 级别 counters。
3. 表现/Cue marker 可追溯且不污染 core simulation tick。
4. x10w / x100w 具备配置入口、采样策略和 diagnostics 输出口径。
5. x10w / x100w 具备 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 覆盖矩阵对应的机制指标入口，而不是只输出 battle result 和平均 tick。
6. 默认 headless profile 必须记录 Physics / Graphics 未启用原因；启用 profile 时必须分别输出 `physicsStepMs`、`physicsQueryMs`、`physicsEventConsumeMs`、`presentationMarkerMs`、`renderMs`、draw command、instances per draw 或 BRG / Profiler 证据。

## 测试链路

1. AutoChess 默认 validation。
2. AutoChess x50 profile。
3. SceneRuntime runner 与 headless runner 对照。

## 当前任务看板

| 任务ID | 任务名 | 状态 | 关联主线 |
|---|---|---|---|
| T6-AutoChess-AM0 | Runtime Validation Demo - AutoChess 无头验收 - Freeze Safety Gate 验收 | 就绪 | T1 |
| T6-AutoChess-AM1 | Runtime Validation Demo - AutoChess 无头验收 - RuntimeCoreDebugger 基线验收 | 就绪 | T4 |
| T6-AutoChess-DemoRefactor | Runtime Validation Demo - AutoChess 无头验收 - AutoChessDemo 目录迁移与分层重构 | 暂停 | T6/T2/T4 |
| T6-AutoChess-LubanConfig | Runtime Validation Demo - AutoChess 无头验收 - Luban 配置链路落地 | 候选 | T6/T2 |
| T6-AutoChess-AM9 | Runtime Validation Demo - AutoChess 无头验收 - x50/x100/x1000/x10w/x100w Scale Gates | 后置 | T5 |

## 三级任务：RuntimeCoreDebugger 基线验收

任务ID：`T6-AutoChess-AM1`

状态：`就绪`

任务名：`Runtime Validation Demo - AutoChess 无头验收 - RuntimeCoreDebugger 基线验收`

当前问题：

1. AutoChess 已经能覆盖复杂业务链，但当前验证报告不足以解释 Runtime Core 热点。
2. 无头表现/Cue marker 完整，但 core simulation 与 observation projection 的成本边界仍需分开报告。
3. Unity Physics / Entities Graphics 接入后，当前报告还缺少 physics / render disabled reason 或独立 counters，容易把未启用 profile 与链路缺失混淆。

目标态参考：

1. `01-目标态架构共识/10-AutoChess无头验收Spec.md`
2. `01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`
3. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
4. `01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`

历史方案参考：

1. `../../历史方案参考/方案14.md`、`方案15.md` 的自走棋业务闭环、Debugger workflow 和图表表达可参考。
2. 不吸收托管 EventBus 作为 gameplay routing。

目标 / 目的：

1. x1 默认业务链路通过。
2. x50 输出 runtime diagnostics counters。
3. 表现/Cue marker 继续完整输出。
4. core simulation tick 与 observation projection tick 能分开报告。
5. physics / render 成本与 core tick 分开报告；默认无头 profile 输出 disabled reason，启用 profile 输出 `ODF-15..18` 对应 counters。

非目标：

1. 不接真实 UI / VFX / SFX 资源。
2. 不新增更多业务机制掩盖 Runtime Core 问题。

执行范围：

1. 原迁移来源：`Assets/GAS/Runtime/Demo/AutoChess`
2. 当前承载：`Assets/AutoChessDemo`
3. AutoChess validation summary / profile export。
4. Runtime Core Debugger counters 接入点。

执行细则：

1. 无头只用 log marker 占位 UI/VFX/SFX/FloatingText/Cue，但业务表现逻辑不能省略。
2. diagnostics summary 必须机器可读。
3. 报告必须区分 core simulation、observation projection、export / bootstrap 成本。
4. 报告必须区分 physics fixed-step / query / event consume、presentation marker 和 Entities Graphics render cost；未启用 Physics / Graphics 时输出 disabled reason。
5. 若启用 physics-enabled profile，collision / trigger event 只能在 `SimulationSingleton` 事件有效窗口内转换为 fact / command，并记录 event dropped / converted count。
6. 若启用 rendered profile，必须输出 `RenderMeshArray` / `MaterialMeshInfo` binding、material override write count、draw command / instances per draw 或 BRG / Profiler 证据。

验收标准：

1. `passed=true`。
2. `runtimeDiagnostics`、`runtimeDiagnosticsPeak` 或等价字段可解释热点。
3. x50 能输出 request/spec/delta/fact/entity lifecycle/buffer pressure 级别 counters。
4. 默认 profile 输出 Physics / Graphics disabled reason；启用 profile 时 `ODF-15..18` 指标齐全，且不会把 physics / render cost 计入 `coreTickMs`。

测试链路：

1. 默认 AutoChess validation。
2. x50 profile。
3. systemTiming profile 仅用于排序，必须标注口径。

交还内容：

1. 更新 `04-当前进度状态/最近验证摘要.md`。
2. 更新 `00-当前架构事实/核心问题诊断.md`。
3. 需要时新增迭代记录。

## 三级任务：AutoChessDemo 目录迁移与分层重构

任务ID：`T6-AutoChess-DemoRefactor`

状态：`暂停`

任务名：`Runtime Validation Demo - AutoChess 无头验收 - AutoChessDemo 目录迁移与分层重构`

当前问题：

1. AutoChess 根目录已从 `Assets/GAS/Runtime/Demo/AutoChess` 迁移到 `Assets/AutoChessDemo`，Runtime Core 对 AutoChess 系统类型的静态依赖已拆除。
2. 当前实现已完成文件级分层，并已开始拆 `HeadlessAutoChessScenario` 的 constants、state、bootstrap、variants、unit definitions、unit resolution、runtime timing、runtime lifecycle、types；但 unit bootstrap、validation summary、event/outbox counting、generated rows 和 presentation marker projection 等内部职责尚未完全拆开。该内部拆分在 GAS Runtime 重构完成前暂停。
3. 历史方案 12/13/14/15 中已有可吸收的 Demo 业务样板，但尚未折算成当前 Demo 目录与任务规则。

目标态参考：

1. `01-目标态架构共识/10-AutoChess无头验收Spec.md`
2. `01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`
3. `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`
4. `01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`
5. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`

历史方案参考：

1. `../../历史方案参考/方案12.md`、`方案13.md` 的真实业务 Demo 和配置生成链可作为非自走棋对照。
2. `../../历史方案参考/方案14.md`、`方案15.md` 的自走棋业务闭环、Luban 配置、Debugger workflow、四层架构优先吸收。

目标 / 目的：

1. 建立 `Assets/AutoChessDemo` 目标目录和分层规范。已建立根目录、asmdef、README 和最小 runtime system bootstrap。
2. 将 Config / Generated / Simulation / Observation / Presentation / Validation / Debugging 从根目录平铺结构拆出。已完成文件级拆分。
3. 将 Config / Generated / Simulation / Observation / Presentation / Validation / Debugging 从巨类内部进一步拆出。`HeadlessAutoChessScenario` 的 constants / state / bootstrap / variants / unit definitions / unit resolution / runtime timing / runtime lifecycle / types 已拆出；unit bootstrap / report builder / event-outbox counting / generated rows 后置到 Runtime Core 重构之后。
3. 保持无头自动验收和 SceneRuntime 验收链路可运行。
4. 保留真实 Demo 资源接入结构，默认只使用 log adapter。

非目标：

1. 不在本任务接真实美术资源。
2. 不修改 GAS Runtime Core 概念语义。
3. 不新增业务机制掩盖 Runtime Core 性能问题。

执行范围：

1. `Assets/AutoChessDemo`
2. `Assets/AutoChessDemo/Config`
3. `Assets/AutoChessDemo/Simulation`
4. `Assets/AutoChessDemo/Observation`
5. `Assets/AutoChessDemo/Presentation`
6. `Assets/AutoChessDemo/Validation`
7. `Assets/AutoChessDemo/Debugging`
8. 原迁移来源：`Assets/GAS/Runtime/Demo/AutoChess`
3. AutoChess asmdef、tests、validation runner、summary export。

执行细则：

1. 先建目标目录、asmdef、README 和最小 bootstrap，再迁移业务。本轮已完成。
2. Demo 业务只能依赖 Runtime Core public contract，不允许 Runtime Core 反向引用 Demo。本轮已完成静态拆除，待 Unity 编译验证。
3. 表现 marker 迁移到 Presentation 层，Debugger / Validation 迁移到独立层。
4. Luban / SourceGenerator 相关内容迁到 Config 层，不再把大量 generated rows 留在场景巨类。
5. 默认业务链路保持精简，迁移时优先保证核心链路完整，不把所有历史机制一次性搬入默认场景。
6. 文件级归位已完成；巨类内部拆分已开始但当前暂停，不把 partial 化等同于架构完成。

验收标准：

1. `Assets/AutoChessDemo` 成为 AutoChess 新增代码唯一入口。已完成根目录迁移。
2. `Assets/GAS/Runtime/Demo/AutoChess` 不再承接新增业务。已完成。
3. Runtime Core 不再静态引用 AutoChessDemo 系统。已完成静态扫描验证。
4. AutoChessDemo 目录形成 Config / Simulation / Observation / Presentation / Validation / Debugging 第一层边界。已完成。
5. 默认 headless validation 与 SceneRuntime runner 仍能输出 summary。待 Unity license 恢复后补验证。
6. `HeadlessAutoChessScenario` 内部至少拆出 constants / state / bootstrap / variants / unit definitions / unit resolution / runtime timing / runtime lifecycle / public DTO/timing types，且 `RunDefault/RunVariant` 外部 API 不变。已完成静态拆分，待 Unity 编译验证。
7. 文档和任务树同步记录迁移状态。已更新。

测试链路：

1. AutoChess 默认 validation。
2. SceneRuntime runner。
3. 若改代码，运行受影响 runtime tests。

交还内容：

1. 更新当前架构事实中的 Demo 目录事实。
2. 更新本任务状态和最近验证摘要。
3. 必要时新增迁移迭代记录。

## 三级任务：Luban 配置链路落地

任务ID：`T6-AutoChess-LubanConfig`

状态：`候选`

任务名：`Runtime Validation Demo - AutoChess 无头验收 - Luban 配置链路落地`

当前问题：

1. 当前 AutoChess 有大量手写 / 生成行混在 runtime demo 代码中，不能作为长期配置权威。
2. x10w / x100w 压力测试需要 ScaleProfile 和 ValidationExpectation 表驱动，不能写死在 runner 中。
3. 表现 marker 和未来真实资源绑定需要同一套 Cue 配置入口。
4. Unity Physics / Entities Graphics 接入后，Demo 配置还必须提供 `PhysicsProfile`、`RenderProfile`、disabled reason expectation 和对应 counters policy，避免把包级可选能力写死在 runner 或 Runtime Core 中。

目标态参考：

1. `01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`
2. `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`
3. `01-目标态架构共识/10-AutoChess无头验收Spec.md`
4. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`

历史方案参考：

1. `../../历史方案参考/方案12.md`、`方案13.md` 的配置生成和真实 Demo 业务案例可作为配置链对照。
2. `../../历史方案参考/方案14.md`、`方案15.md` 的自走棋配置表、GE blob、SourceGenerator、Debugger workflow 优先吸收。

目标 / 目的：

1. 建立 AutoChessDemo 专用 Luban 表结构。
2. 生成 Unit / Ability / GE / Cue / Scenario / ScaleProfile / ValidationExpectation runtime lookup。
3. 让默认 x1、x50 和未来 x10w / x100w profile 都由配置驱动。
4. 生成 PhysicsProfile / RenderProfile lookup：默认无头使用 disabled profile，专项验收才启用 physics-enabled 或 rendered profile。

非目标：

1. 不生成 gameplay lifecycle。
2. 不把真实资源导入作为本任务验收条件。
3. 不把大量机制一次性加入默认业务链路。

执行范围：

1. `Assets/AutoChessDemo/Config`
2. `EX_GAS_Config/ProjectConfigTable/exgas_config`
3. AutoChessDemo generated runtime / editor 输出。

执行细则：

1. 表结构必须覆盖精链路，而不是覆盖所有可能机制。
2. `ScaleProfile` 必须包含 x1、x50、x100、x1000、x10w、x100w。
3. `Cue` 配置必须同时支持 log marker 和未来 resource binding key。
4. 生成物进入版本控制前必须区分源码契约和缓存。
5. `PhysicsProfile` 必须覆盖 query type、CollisionFilter、event opt-in、FixedStep policy 和 disabled reason。
6. `RenderProfile` 必须覆盖 render binding id、`RenderMeshArray` pack hint、`MaterialMeshInfo` default、material override schema、render evidence policy 和 disabled reason。

验收标准：

1. AutoChessDemo 默认业务数据不再依赖巨量 hand-written rows。
2. x1 / x50 profile 可由配置生成。
3. x10w / x100w profile 有配置入口和采样策略。
4. 生成链只输出 Definition & Generation Layer 数据和 static lookup。
5. 生成报告输出 PhysicsProfile / RenderProfile 覆盖摘要，并说明 `ODF-15..18` 的采用或暂不相关理由。

测试链路：

1. Luban process validation。
2. AutoChessDemo config diagnostics。
3. AutoChess 默认 validation。

交还内容：

1. 更新 Definition 配置事实。
2. 更新 AutoChessDemo Spec 和 T6 任务状态。
3. 必要时新增配置链迭代记录。


