# T2 Definition / Luban 配置权威

## 节点定位

本主线负责 Definition & Generation Layer 的配置权威链。Excel / Luban / SourceGenerator / generated source 只能生成定义数据、校验摘要、bake plan 和 runtime static lookup 输入，不能直接生成 gameplay lifecycle。

## 当前问题

1. Definition / Bake / RuntimeIntegration contract 已有基础，但还需要用真实 Luban process gate 和 generated artifact 压实。
2. Runtime Core 重构需要稳定的 static lookup / query glue，避免 hot path 动态查表和托管配置读取。

## 目标态参考

1. `01-目标态架构共识/08-Luban-SourceGenerator配置生成链路Spec.md`
2. `01-目标态架构共识/02-四层架构Spec.md`
3. `01-目标态架构共识/09-Authoring-EditorSpec.md`
4. `01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md`
5. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
6. `01-目标态架构共识/12-命名规范Spec.md`

## 历史方案参考

1. `历史方案参考/方案14.md`、`方案15.md` 的 Luban + SourceGenerator 配置生成链路可参考。
2. `方案10.md` 的 Luban -> Blob、Burst-friendly lookup 信号可参考。
3. 生成 Ability / GE active lifecycle 的方向不可照搬。

## 主线目标

把 Excel / Luban / generated source 折算到 Definition & Generation Layer，并通过 registry summary、validation graph、bake plan、runtime integration plan 为 Runtime Core 提供稳定输入。

## 非目标

1. 不把 `cfg / XLuban / SimpleJSON` 暴露给 runtime gameplay contract。
2. 不让 SourceGenerator 生成 simulation lifecycle。
3. 不把 Editor authoring 作为 runtime 配置权威。

## 执行范围

1. `EX_GAS_Config/ProjectConfigTable/exgas_config`
2. `Assets/GAS/Runtime/Definition`
3. `Assets/GAS/Editor`
4. `Assets/AutoChessDemo/Config`
5. `Assets/DataGenerated/Luban` 本地生成物和忽略规则

## 执行细则

1. generated artifact 必须有 manifest / hash / diagnostics。
2. Runtime 只消费生成后的静态定义表和 lookup，不消费原始 Luban 运行时。
3. 生成物进入版本控制前必须确认是否为源码契约，自动生成缓存默认不提交。
4. 配置链任务必须说明 BlobBuilder、Baker glue、Baking System、WeakObjectReference / UnityObjectRef、SubScene / Streaming、LinkedEntityGroup 的采用或拒绝理由。

## 验收门槛

1. Luban process gate 可自动验证。
2. generated source 可进入 `GASDefinitionTable`。
3. BakePlan / RuntimeIntegrationPlan 能消费 generated result。

## 测试链路

1. Luban process validation。
2. Definition runtime tests。
3. AutoChess generated source validation。

## 支线索引

| 支线 | 文档 | 状态 |
|---|---|---|
| Luban SourceGenerator 配置链 | [LubanSourceGenerator配置链.md](LubanSourceGenerator配置链.md) | 就绪 |

## 交还规则

交还时必须同步 Definition 配置事实、Luban SourceGenerator Spec、生成物忽略规则和验证摘要。

