# Runtime Validation Demo - AutoChess 无头验收

## 父节点

[T6 Runtime Validation Demo](../README.md)

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

| 任务ID | 任务名 | 状态 | 关联主线 | 任务文件 |
|---|---|---|---|---|
| T6-AutoChess-AM0 | Runtime Validation Demo - AutoChess 无头验收 - Freeze Safety Gate 验收 | 就绪 | T1 | [AM0-FreezeSafetyGate.md](AM0-FreezeSafetyGate.md) |
| T6-AutoChess-AM1 | Runtime Validation Demo - AutoChess 无头验收 - RuntimeCoreDebugger 基线验收 | 就绪 | T4 | [AM1-RuntimeCoreDebugger基线验收.md](AM1-RuntimeCoreDebugger基线验收.md) |
| T6-AutoChess-DemoRefactor | Runtime Validation Demo - AutoChess 无头验收 - AutoChessDemo 目录迁移与分层重构 | 暂停 | T6/T2/T4 | [DemoRefactor-目录迁移与分层重构.md](DemoRefactor-目录迁移与分层重构.md) |
| T6-AutoChess-LubanConfig | Runtime Validation Demo - AutoChess 无头验收 - Luban 配置链路落地 | 候选 | T6/T2 | [LubanConfig-配置链路落地.md](LubanConfig-配置链路落地.md) |
| T6-AutoChess-AM9 | Runtime Validation Demo - AutoChess 无头验收 - x50/x100/x1000/x10w/x100w Scale Gates | 后置 | T5 | [AM9-ScaleGates.md](AM9-ScaleGates.md) |
