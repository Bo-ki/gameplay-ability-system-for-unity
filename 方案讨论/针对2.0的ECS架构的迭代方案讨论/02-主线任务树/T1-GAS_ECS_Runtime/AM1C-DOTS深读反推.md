# GAS ECS Runtime - Runtime Core 重构 - DOTS API 深读反推 Runtime Core

## 父节点

[RuntimeCore重构](README.md)

## 任务ID

`T1-RuntimeCore-AM1C`

## 状态

已完成

## 当前问题

1. `UnityDOTS官方文档参考` 已完成 API 选型层校准，但仍偏规则和候选 API。
2. 需要把 API 选型结论直接反推到 Runtime Core 的目标态 Spec 和具体任务上下文。

## 目标态参考

1. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
2. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
3. `01-目标态架构共识/05-ActiveEffectStoreSpec.md`
4. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`

## 目标 / 目的

1. 把 `SEL-*` 规则反推到 Runtime Core 的 EffectCommand、ActiveEffectStore、Debugger 和 Luban 配置链。
2. 确保目标态 Spec 中的 API 选型描述有 SEL 规则编号支撑。

## 非目标

1. 不修改 Runtime 代码。
2. 不修改 `Library/PackageCache`。

## 执行范围

1. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
2. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
3. `01-目标态架构共识/05-ActiveEffectStoreSpec.md`
4. 本支线任务树。

## 已完成交付物

1. Runtime Core 三条 Spec（03/04/05）均已补充 API 选型引用和 SEL 规则编号。
2. EffectCommand、ActiveEffectStore、Debugger 的 API 承载选型表已落入门规格。
3. 完整历史见 [T0 已完成文档校准日志](../../T0-文档治理与目标态共识/已完成文档校准日志.md)。

## 验收标准

1. `01/03`、`01/04`、`01/05` 中出现 `SEL-*` 规则编号引用。
2. AM2/AM3/AM5 任务描述能从目标态 Spec 中找到 API 选型依据。

## 测试链路

1. `rg -n "SEL-" "01-目标态架构共识/03" "01-目标态架构共识/04" "01-目标态架构共识/05"`
