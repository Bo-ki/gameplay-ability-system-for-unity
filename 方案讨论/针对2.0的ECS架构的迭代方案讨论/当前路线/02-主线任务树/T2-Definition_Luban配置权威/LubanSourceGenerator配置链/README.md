# Definition / Luban 配置权威 - Luban SourceGenerator 配置链

## 父节点

[T2 Definition / Luban 配置权威](../README.md)

## 节点定位

本支线负责真实 Luban process、generated source、manifest、DefinitionTable、BakePlan、RuntimeIntegrationPlan 的端到端配置生成链路。

## 当前问题

1. Definition / Bake / RuntimeIntegration contract 已存在，但还需要通过真实生成链路持续验收。
2. Runtime Core 重构需要静态 definition lookup，不能继续依赖动态配置读取。
3. 自动生成代码的版本控制边界需要清晰，生成缓存不应进入仓库。
4. DOTS 深读后，生成链路还需要提供 query layout hint、buffer capacity hint、Baking dependency summary、WeakObjectReference load plan、SceneSection / ScaleProfile plan 和 Burst calculation registry，而不是只生成表访问代码。
5. 官方案例深挖后，生成链路必须对齐 `CASE-10` 的 Baker / Baking System / Blob 路径和 `CASE-11` 的 WeakObjectReference / UnityObjectRef / SceneSystem 边界。
6. 官方文档查缺补漏后，生成链路还必须对齐 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 覆盖矩阵中的 Baking filter、content management、Transform stale-data policy、LinkedEntityGroup、Mathematics RNG 和 Burst vectorization / FunctionPointer 证据。
7. Unity Physics / Entities Graphics 新包接入后，生成链路还必须能生成 PhysicsProfile、CollisionFilter、RenderProfile、RenderMeshArray / MaterialMeshInfo binding 和 material override schema。

## 目标态参考

1. `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`
2. `01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`
3. `01-目标态架构共识/02-四层架构Spec.md`
4. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
5. `UnityDOTS官方文档参考/主题/12-官方案例模式.md`
6. `UnityDOTS官方文档参考/README.md`
7. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`

## 历史方案参考

1. `历史方案参考/方案14.md`、`方案15.md` 的 Luban + SourceGenerator 方案可参考。
2. `方案10.md` 的 Luban -> Blob / static lookup 方向可参考。
3. 生成 gameplay lifecycle 的设计不可照搬。

## 支线目标

验证真实 Luban process gate、generated package、source / manifest export、DefinitionTable、BakePlan、RuntimeIntegrationPlan 全链路，确保生成链路只提供 Definition & Generation Layer 输入。

## 当前状态

就绪。可与 Runtime Core 重构并行，但不能生成 lifecycle 替代 Runtime 语义。

## 非目标

1. 不生成 Ability / GE active lifecycle。
2. 不把 `cfg / XLuban / SimpleJSON` 暴露给 runtime gameplay contract。
3. 不提交自动生成缓存。

## 前置依赖

1. `.gitignore` 已排除 Luban 自动生成缓存。
2. Definition contract 和 bake plan 已有基础实现。

## 执行范围

1. `EX_GAS_Config/ProjectConfigTable/exgas_config`
2. `Assets/GAS/Runtime/Definition`
3. `Assets/AutoChessDemo/Config`
4. 当前迁移来源：`Assets/GAS/Runtime/Demo/AutoChess`
5. 本地 `Assets/DataGenerated/Luban` 生成结果。

## 执行细则

1. generated source / manifest 必须可 hash 验证。
2. runtime package 只消费生成后的 definition table / static lookup。
3. 任何生成物纳入版本控制前必须说明其职责是源码契约还是缓存。
4. 任务行动报告必须说明 BlobBuilder、Baker glue、Baking System、WeakObjectReference / UnityObjectRef、SubScene / Streaming、LinkedEntityGroup、TransformUsageFlags 的 API 选型。
5. 任务行动报告必须说明 Baker 是否 stateless、外部依赖如何声明、Baking System 如何维护 incremental dependency / rollback、哪些产物使用 `BakingOnlyEntity` / `TemporaryBakingType` 过滤。
6. 生成 calculation registry 时必须说明为什么采用 generated static switch / Blob lookup / FunctionPointer，禁止生成托管 delegate 或可变静态表。
7. 任务行动报告必须新增"官方案例对照"：至少说明 `CASE-10` 和 `CASE-11` 的采用方式，以及是否拒绝 generated runtime lifecycle。
8. 任务行动报告必须新增"官方文档覆盖检查"：先说明 `UnityDOTS官方文档参考` 中的相关主题，再至少说明 Baking dependency / output filter、weak resource load / release、TransformUsageFlags、LinkedEntityGroup、Random seed / radians、FunctionPointer / vectorization、Unity Physics 和 Entities Graphics 是否受影响，并引用相关 `ODF-*`。

## 验收门槛

1. Luban process gate 成功。
2. generated source 可进入 `GASDefinitionTable`。
3. BakePlan / BakeContract / BakePipeline / RuntimeIntegrationPlan 均能消费 generated result。
4. 生成报告输出 query layout hint、buffer capacity hint、Baking dependency summary、WeakObjectReference load plan、TransformUsageFlags 和 SceneSection / ScaleProfile plan。
5. 生成报告输出官方文档覆盖检查摘要，能说明哪些 `ODF-*` 规则已满足，哪些主题暂不相关。
6. 生成报告输出 PhysicsProfile / RenderProfile 覆盖摘要：`ODF-15..18` 采用或暂不相关理由，以及 profile 字段是否只进入 Definition / Boundary / Presentation。

## 测试链路

1. 真实 Luban process validation。
2. Runtime definition tests。
3. AutoChess generated source validation。

## 当前任务看板

| 任务ID | 任务名 | 状态 | 目标 Spec | 任务文件 |
|---|---|---|---|---|
| T2-LubanSG-Chain-1 | Definition / Luban 配置权威 - Luban SourceGenerator 配置链 - 配置生成链路验收 | 就绪 | `01/08 Luban SourceGenerator` | [Chain-1-配置生成链路验收.md](Chain-1-配置生成链路验收.md) |
| T2-LubanSG-AutoChessConfig | Definition / Luban 配置权威 - Luban SourceGenerator 配置链 - AutoChessDemo 配置方案验收 | 候选 | `01/11 AutoChessDemo Luban` | [AutoChessConfig-配置方案验收.md](AutoChessConfig-配置方案验收.md) |
| T2-LubanSG-Chain-2 | Definition / Luban 配置权威 - Luban SourceGenerator 配置链 - Generated Runtime Glue 边界 | 候选 | `01/08 Luban SourceGenerator` | [Chain-2-GeneratedRuntimeGlue边界.md](Chain-2-GeneratedRuntimeGlue边界.md) |
