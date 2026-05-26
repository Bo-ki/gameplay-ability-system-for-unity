# Runtime Validation Demo - AutoChess 无头验收 - Luban 配置链路落地

## 父节点

[AutoChess 无头验收](README.md)

## 任务ID

`T6-AutoChess-LubanConfig`

## 状态

`候选`

## 当前问题

1. 当前 AutoChess 有大量手写 / 生成行混在 runtime demo 代码中，不能作为长期配置权威。
2. x10w / x100w 压力测试需要 ScaleProfile 和 ValidationExpectation 表驱动，不能写死在 runner 中。
3. 表现 marker 和未来真实资源绑定需要同一套 Cue 配置入口。
4. Unity Physics / Entities Graphics 接入后，Demo 配置还必须提供 `PhysicsProfile`、`RenderProfile`、disabled reason expectation 和对应 counters policy，避免把包级可选能力写死在 runner 或 Runtime Core 中。

## 目标态参考

1. `01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`
2. `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`
3. `01-目标态架构共识/10-AutoChess无头验收Spec.md`
4. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`

## 历史方案参考

1. `../../历史方案参考/方案12.md`、`方案13.md` 的配置生成和真实 Demo 业务案例可作为配置链对照。
2. `../../历史方案参考/方案14.md`、`方案15.md` 的自走棋配置表、GE blob、SourceGenerator、Debugger workflow 优先吸收。

## 目标 / 目的

1. 建立 AutoChessDemo 专用 Luban 表结构。
2. 生成 Unit / Ability / GE / Cue / Scenario / ScaleProfile / ValidationExpectation runtime lookup。
3. 让默认 x1、x50 和未来 x10w / x100w profile 都由配置驱动。
4. 生成 PhysicsProfile / RenderProfile lookup：默认无头使用 disabled profile，专项验收才启用 physics-enabled 或 rendered profile。

## 非目标

1. 不生成 gameplay lifecycle。
2. 不把真实资源导入作为本任务验收条件。
3. 不把大量机制一次性加入默认业务链路。

## 执行范围

1. `Assets/AutoChessDemo/Config`
2. `EX_GAS_Config/ProjectConfigTable/exgas_config`
3. AutoChessDemo generated runtime / editor 输出。

## 执行细则

1. 表结构必须覆盖精链路，而不是覆盖所有可能机制。
2. `ScaleProfile` 必须包含 x1、x50、x100、x1000、x10w、x100w。
3. `Cue` 配置必须同时支持 log marker 和未来 resource binding key。
4. 生成物进入版本控制前必须区分源码契约和缓存。
5. `PhysicsProfile` 必须覆盖 query type、CollisionFilter、event opt-in、FixedStep policy 和 disabled reason。
6. `RenderProfile` 必须覆盖 render binding id、`RenderMeshArray` pack hint、`MaterialMeshInfo` default、material override schema、render evidence policy 和 disabled reason。

## 验收标准

1. AutoChessDemo 默认业务数据不再依赖巨量 hand-written rows。
2. x1 / x50 profile 可由配置生成。
3. x10w / x100w profile 有配置入口和采样策略。
4. 生成链只输出 Definition & Generation Layer 数据和 static lookup。
5. 生成报告输出 PhysicsProfile / RenderProfile 覆盖摘要，并说明 `ODF-15..18` 的采用或暂不相关理由。

## 测试链路

1. Luban process validation。
2. AutoChessDemo config diagnostics。
3. AutoChess 默认 validation。

## 交还内容

1. 更新 Definition 配置事实。
2. 更新 AutoChessDemo Spec 和 T6 任务状态。
3. 必要时新增配置链迭代记录。
