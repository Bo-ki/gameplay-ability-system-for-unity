# GAS ECS Runtime - Runtime Core 重构 - Unity DOTS API 选型修正

## 父节点

[RuntimeCore重构](README.md)

## 任务ID

`T1-RuntimeCore-AM1B`

## 状态

已完成

## 当前问题

1. AM1 已校准 Unity Entities 机制基线，但 Runtime Core 还缺少具体 API 选型修正表。
2. EffectCommand、ActiveEffectStore、Debugger、Luban 配置链不能只沿用全局 DynamicBuffer / 逐实体 ECB / request entity / singleton 这些当前实现惯性，必须按官方文档重新评估更适合的 API。

## 目标态参考

1. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
2. `UnityDOTS官方文档参考/README.md`
3. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`

## 目标 / 目的

1. 建立 Runtime Core 各模块的 API 候选评估表和采用 / 拒绝口径。
2. 把 `SEL-*` 规则体系与目标态 Spec 绑定。

## 非目标

1. 不修改 Runtime 代码。
2. 不修改 `Library/PackageCache`。

## 执行范围

1. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
2. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`

## 已完成交付物

1. `SEL-01` 至 `SEL-32` 规则体系建立，覆盖 DynamicBuffer、NativeStream、ECB、EntityQuery、Enableable、Cleanup Component、Chunk Component、Blob Asset、WorldUpdateAllocator、RewindableAllocator 等 API 选型。
2. 每个 `SEL-*` 规则包含候选 API、采用/拒绝理由和反推 Runtime Core 模块入口。
3. 完整历史见 [T0 已完成文档校准日志](../../T0-文档治理与目标态共识/已完成文档校准日志.md)。

## 验收标准

1. `SEL-*` 规则能进入 Runtime Core 任务的行动报告和 API 选型表。
2. AM2/AM3/AM5 任务能引用具体 SEL 规则编号。

## 测试链路

1. `rg -n "SEL-01|SEL-02|SEL-03|SEL-21|SEL-32" 当前路线`
