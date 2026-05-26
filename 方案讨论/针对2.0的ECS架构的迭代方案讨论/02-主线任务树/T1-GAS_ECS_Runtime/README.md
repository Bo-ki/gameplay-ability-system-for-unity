# T1 GAS ECS Runtime

## 节点定位

本主线负责 EX-GAS 2.0 的 GAS Runtime Core Layer。承载 ASC、Ability、GameplayEffect、Attribute、Tag、Spec/Context、Capture、ExecutionCalculation、EffectCommand、AttributeDelta、ActiveEffectStore 等 runtime 权威语义。

## 当前问题

1. 当前 Runtime 仍有旧 GE entity lifecycle、global observation stream、managed query / helper 和 structural change 成本混在热路径中。
2. 继续做局部 fast path 不能解决核心管线失真。
3. 继续扩张 AM3 / AM5 前必须先建立 Runtime Core Frame Backbone。

## 目标态参考

- `../../01-目标态架构共识/` 中的 01, 03, 04, 05, 10B, 12, 90 Spec
- `../../../UnityDOTS官方文档参考/` 中的 01, 12, 20, 21, 90 主题

## 历史方案参考

- `../../历史方案参考/方案14.md`、`方案15.md`：自走棋验收、四层模型和 Runtime Core 重构方向
- 托管 EventBus、SourceGenerator 生成 gameplay lifecycle 的方向不可照搬

## 主线目标

先建立 DOTS 原生 Runtime Core Frame Backbone，再用 `EffectCommand -> SpecStream -> AttributeDelta -> ActiveEffectStore -> TypedFacts` 替代旧生命周期混合管线。

## 叶子任务

| 任务 | 文件 | 状态 |
|---|---|---|
| AM-0 Freeze Safety Gate | [AM0-FreezeSafetyGate](AM0-FreezeSafetyGate.md) | 契约已确立 |
| AM-1 机制校准 | [AM1-机制校准](AM1-机制校准.md) | — |
| AM-1B API 选型修正 | [AM1B-API选型修正](AM1B-API选型修正.md) | — |
| AM-1C DOTS 深读反推 | [AM1C-DOTS深读反推](AM1C-DOTS深读反推.md) | — |
| AM-1D 官方案例反推 | [AM1D-官方案例反推](AM1D-官方案例反推.md) | — |
| AM-1E 文档体系主题化 | [AM1E-文档体系主题化](AM1E-文档体系主题化.md) | — |
| AM-1G 物理表现覆盖 | [AM1G-物理表现覆盖](AM1G-物理表现覆盖.md) | — |
| AM-2 EffectCommand 契约 | [AM2-EffectCommand契约](AM2-EffectCommand契约.md) | 契约已确立 |
| AM-3 Instant Spec Evaluation | [AM3-InstantSpecEvaluation](AM3-InstantSpecEvaluation.md) | 契约已确立 |
| AM-5 Active Effect Store | [AM5-ActiveEffectStore](AM5-ActiveEffectStore.md) | 进行中 |

### AM2B Frame Backbone 子任务

| 任务 | 文件 |
|---|---|
| A - Schedule Phase | [AM2B-A-SchedulePhase](AM2B-A-SchedulePhase.md) |
| B - Frame Arena Budget | [AM2B-B-FrameArenaBudget](AM2B-B-FrameArenaBudget.md) |
| C - Stream Owner | [AM2B-C-StreamOwner](AM2B-C-StreamOwner.md) |
| D - Structural Playback | [AM2B-D-StructuralPlayback](AM2B-D-StructuralPlayback.md) |
| E - Debugger Evidence | [AM2B-E-DebuggerEvidence](AM2B-E-DebuggerEvidence.md) |
| F - Rebind Handoff | [AM2B-F-RebindHandoff](AM2B-F-RebindHandoff.md) |

另有 [RuntimeCoreFrameBackbone](RuntimeCoreFrameBackbone.md) 总览和 [RuntimeCore重构](RuntimeCore重构.md) 原始规划。

## 执行细则

1. 新链路使用目标态术语：`EffectCommand`、`SpecStream`、`AttributeDelta`、`ActiveEffectStore`、`TypedFacts`
2. Simulation hot path 不拼接托管字符串
3. 结构变化集中到明确 phase / ECB playback
4. 每个 Runtime Core 任务必须输出 API 选型表

## 验收门槛

1. 新业务默认走 command/spec/delta/store/facts 链路
2. 旧 instant GE entity lifecycle 标记为待迁移
3. AutoChess x1/x50 能用 diagnostics 解释 Runtime Core 热点
4. Frame Backbone 完成前，AM3/AM5 局部 proof 不得被描述为 scale-ready 目标态主线
