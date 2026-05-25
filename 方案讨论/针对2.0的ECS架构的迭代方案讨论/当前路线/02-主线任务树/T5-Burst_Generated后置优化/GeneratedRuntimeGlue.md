# Burst / Generated 后置优化 - Generated Runtime Glue

## 父节点

[T5 Burst / Generated 后置优化](README.md)

## 节点定位

本支线负责 Runtime Core 语义稳定后的 generated runtime glue，包括 static lookup、query glue、chunk-friendly access helper 和 scale gate 支撑。

## 当前问题

1. 当前 Runtime Core 语义仍在重构，过早生成 runtime lifecycle 会固化错误边界。
2. Runtime 需要静态 lookup 和 query glue 减少动态查表，但不能让生成代码成为 gameplay 语义权威。
3. 官方文档查缺补漏后，Generated Runtime Glue 还必须对齐 Burst vectorization、FunctionPointer 粗粒度、SharedStatic 边界、Mathematics Random state、query layout hint 和 buffer capacity hint。
4. Unity Physics / Entities Graphics 接入后，generated glue 还需要能生成 PhysicsProfile / RenderProfile 的 static lookup 和 binding plan，但不能生成 gameplay lifecycle，也不能让渲染或物理配置反向成为 Core 语义权威。

## 目标态参考

1. `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`
2. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
3. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
4. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
5. `UnityDOTS官方文档参考/README.md`
6. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`
7. `01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`

## 历史方案参考

1. `历史方案参考/方案10.md`、`方案11.md` 的 static lookup、Blob、Burst-friendly 方向可参考。
2. `方案14.md`、`方案15.md` 的 scale gate 可参考。
3. SourceGenerator 生成 gameplay lifecycle 的方向不可照搬。

## 支线目标

为稳定后的 Runtime Core 提供 static lookup 与 query glue，使大规模 AutoChess gates 能减少动态配置读取和重复查询成本。

## 当前状态

后置。等待 T1 Runtime Core 语义稳定、T2 配置生成链路验收、T4 diagnostics baseline 成立。

## 非目标

1. 不生成 Ability / GE active lifecycle。
2. 不为错误旧管线生成加速胶水。
3. 不绕过 Runtime Core Spec。

## 前置依赖

1. T1 command/spec/delta/store/facts 链路稳定。
2. T2 generated definition table 稳定。
3. T4 diagnostics 能解释热点。

## 执行范围

1. generated static lookup。
2. runtime query glue。
3. AutoChess scale gates。
4. PhysicsProfile / RenderProfile generated binding plan。

## 执行细则

1. generated glue 只服务 lookup / query / contract 检查。
2. 生成代码不得拥有 gameplay lifecycle 决策。
3. 性能报告必须区分 generated glue 带来的收益和 Runtime Core 语义变化。
4. 行动报告必须说明适用的 `ODF-*`：Burst warmup、vectorization、FunctionPointer、SharedStatic、Random state、query / buffer hint 和官方工具对照。
5. 涉及 PhysicsProfile / RenderProfile 时，行动报告必须引用 `ODF-15..18`；生成物只能包含 CollisionFilter / query profile / render binding id / `RenderMeshArray` pack hint / `MaterialMeshInfo` default / material override schema 等 Definition & Boundary 数据。
6. generated glue 不得在 hot path 调 `RenderMeshUtility.AddComponents`，也不得生成 collision / trigger event gameplay lifecycle；event 只能由 Runtime / Demo boundary 在有效窗口内转换为 command / fact。

## 验收门槛

1. Static Lookup 与 Query Glue 可被 Runtime Core 消费。
2. x50 / x100 / x1000 profile 显示动态查表和重复 query 成本下降。
3. diagnostics 能解释优化前后热点变化。
4. 生成 glue 的性能收益能被 Burst Inspector、Debugger counters 或 AutoChess scale summary 对照证明。
5. PhysicsProfile / RenderProfile 生成报告输出 `ODF-15..18` 覆盖摘要，默认 disabled profile 与启用 profile 的 counters policy 可被 AutoChess validation 消费。

## 测试链路

1. Generated source validation。
2. Runtime Core regression tests。
3. AutoChess scale profile。

## 当前任务看板

| 任务ID | 任务名 | 状态 |
|---|---|---|
| T5-BurstGlue-AM8 | Burst / Generated 后置优化 - Generated Runtime Glue - Static Lookup 与 Query Glue | 后置 |

## 交还内容

1. 更新 T5 主线状态。
2. 更新 Luban SourceGenerator Spec 和 Runtime Core Spec。
3. 附 scale profile 与 diagnostics 对照证据。

