# Definition / Luban 配置权威 - Luban SourceGenerator 配置链 - 配置生成链路验收

## 父节点

[Luban SourceGenerator 配置链](README.md)

## 任务ID

`T2-LubanSG-Chain-1`

## 状态

`就绪`

## 当前问题

1. Definition / Bake / RuntimeIntegration contract 已存在，但还没有充分转化为 Runtime Core 的 static lookup / query glue 优势。
2. 当前配置链完整性需要用真实 Luban process gate 和 generated artifact 验收继续压实。

## 目标态参考

1. `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`
2. `01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`
3. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
4. `00-当前架构事实/Definition配置事实.md`

## 历史方案参考

1. `../../历史方案参考/方案14.md`、`方案15.md` 的 Luban + SourceGenerator 设计视角可参考。
2. 不吸收"SourceGenerator 生成 gameplay lifecycle"的方向。

## 目标 / 目的

1. 验证真实 Luban process gate、generated package、source/manifest export、DefinitionTable、BakePlan、RuntimeIntegrationPlan 全链路。
2. 保证 generated 只生成 Definition & Generation Layer 输入和 static lookup glue。
3. 为 AM-8 generated runtime glue 提供前置事实。

## 非目标

1. 不生成 Ability / GE active lifecycle。
2. 不把 `cfg / XLuban / SimpleJSON` 暴露给 runtime package contract。

## 执行范围

1. `EX_GAS_Config/ProjectConfigTable/exgas_config`
2. `Assets/GAS/Runtime/Definition`
3. `Assets/AutoChessDemo/Config`
4. 当前迁移来源：`Assets/GAS/Runtime/Demo/AutoChess`
5. `Assets/DataGenerated/Luban` 只作为本地生成物，不纳入版本控制。

## 验收标准

1. Luban process gate 成功时，generated source / manifest 输出 hash 可验证。
2. generated source 可进入 `GASDefinitionTable`。
3. BakePlan / BakeContract / BakePipeline / RuntimeIntegrationPlan 均能消费 generated result。

## 测试链路

1. 真实 Luban process validation。
2. Runtime definition tests。
3. AutoChess generated source validation。

## 交还内容

1. 更新 `00-当前架构事实/Definition配置事实.md`。
2. 更新 `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`。
