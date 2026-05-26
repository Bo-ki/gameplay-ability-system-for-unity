# GAS ECS Runtime - Runtime Core 重构 - Unity DOTS 官方案例反推 Runtime Core

## 父节点

[RuntimeCore重构](README.md)

## 任务ID

`T1-RuntimeCore-AM1D`

## 状态

已完成

## 当前问题

1. `UnityDOTS官方文档参考` 已完成官方依据和 API 选型层校准，但仍偏规则和候选 API。
2. Agent 执行代码任务时还需要知道"官方示例实际怎么写"，否则可能把入门 `SystemAPI.Query`、ECB immediate playback、SceneSystem load、Baking System 等案例误当成 Core hot path 模板。
3. Runtime Core 后续 AM3 / AM5 / T4 / T2 任务需要一套 `CASE-*` 规则，能直接进入行动报告、任务交还和 Debugger 验收。

## 目标态参考

1. `UnityDOTS官方文档参考/主题/12-官方案例模式.md`
2. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
3. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
4. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
5. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`

## 目标 / 目的

1. 从官方 `DocCodeSamples.Tests`、`Unity.Entities.Tests`、`Unity.Scenes.Hybrid.Tests` 和 `PerformanceTests` 中提炼可执行模式。
2. 建立 `CASE-01` 到 `CASE-12`，补充 `SEL-*` 不能表达的"官方案例对照"层。
3. 反推 Runtime Core、ActiveEffectStore、EffectCommand、Debugger、Luban / SourceGenerator 和 AutoChess 验收。

## 非目标

1. 不修改 `Library/PackageCache`。
2. 不在本任务推进 Runtime 代码实现。
3. 不把官方案例当作 GAS 业务语义第一性来源。

## 执行范围

1. `UnityDOTS官方文档参考/主题/12-官方案例模式.md`
2. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
3. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
4. 本支线任务树和当前进度状态。

## 执行细则

1. 官方案例证据必须用本地 PackageCache 路径和行号定位。
2. 每个 CASE 规则必须能反推到一个 GAS Runtime Core 模块或验证链路。
3. 后续任务行动报告必须新增"官方案例对照"小节。

## 验收标准

1. `CASE-*` 规则出现在 `90-规则编号索引` 中。
2. RuntimeCore 任务树看板登记 `AM1D`，后续 AM3 / AM5 / T4 / T2 能引用。
3. 当前进度摘要记录本轮文档校准和反哺状态。

## 测试链路

1. `rg -n "UnityDOTS官方案例|CASE-01|AM1D|DocCodeSamples|PerformanceTests" 当前路线`
2. `git diff --check -- "方案讨论/针对2.0的ECS架构的迭代方案讨论/当前路线"`
