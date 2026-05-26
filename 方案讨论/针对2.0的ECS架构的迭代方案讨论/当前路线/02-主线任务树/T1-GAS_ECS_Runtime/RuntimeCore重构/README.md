# GAS ECS Runtime - Runtime Core 重构

## 父节点

[T1 GAS ECS Runtime](../README.md)

## 节点定位

本支线负责 Runtime Core 主链重构，承接 T1 主线，把旧 GE lifecycle / global observation stream 混合热路径迁移为 command / spec / delta / store / facts 的 ECS-first 管线。

本目录是**分支节点**，不可直接领取。Agent 应从下方看板选取具体叶子任务文件领取。

## 当前问题

1. 旧 GE lifecycle 仍承担 simple instant GE、runtime entity、event projection 和 structural change 成本。
2. 历史 fast path 没有解决 x50 曲线失真，只是在旧管线上继续补丁。
3. Runtime Core 与 Observation / Presentation 边界不清，会继续污染性能诊断。
4. 目标态 Spec 仍需落到 Unity Entities 1.4.6 的 SystemGroup、ISystem/job、ECB playback、DynamicBuffer、Enableable、Blob/Baker 和 Query filter 机制。
5. 当前目标态对 DOTS API 潜力挖掘仍不足。
6. Runtime Core 任务必须把 Query / Filter / Allocator / Dependency / Chunk layout / DynamicBuffer spill / Burst calculation 当成任务上下文。
7. Runtime Core 任务必须能对照 DocCodeSamples / Tests / PerformanceTests 的实际写法。
8. Runtime Core 任务必须先读取 `UnityDOTS官方文档参考/README.md`，再对照覆盖矩阵。
9. Unity Physics / Entities Graphics 只允许作为 Boundary / Presentation 输入输出，不得反向污染 GAS Core 语义。

## 目标态参考

1. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
2. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
3. `01-目标态架构共识/05-ActiveEffectStoreSpec.md`
4. `01-目标态架构共识/10B-AutoChess完整业务案例设计Spec.md`
5. `UnityDOTS官方文档参考/README.md`
6. `UnityDOTS官方文档参考/主题/01-Entities系统与World.md`
7. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
8. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
9. `UnityDOTS官方文档参考/主题/12-官方案例模式.md`
10. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`
11. `01-目标态架构共识/90-目标态不变量.md`

## 历史方案参考

1. `历史方案参考/方案14.md`、`方案15.md` 的 AutoChess 验收、四层模型和 Runtime rebuild 方向可参考。
2. `方案10.md`、`方案11.md` 的显式调度、Luban/Blob、Burst-friendly 数据视角可参考。
3. 托管 EventBus、生成 gameplay lifecycle、Debugger 参与 simulation routing 不可照搬。

## 支线目标

用 `Effect Command -> Instant Spec / Active Mutation -> Attribute Delta -> Typed Facts -> Observation Projection` 替换旧 GE lifecycle / global observation stream 混合管线。

## 当前状态

进行中。AM2B-A~F 已完成 contract-first 任务链。AM3 契约已确立（activation/cost/Timeline ApplyEffects simple instant producer 已接入 stream；EventBusHelper ThreadStatic 已消除；System/Effect EventBus callsite 已收尾；parallel fan-in contract test 已落地；Unity Test Runner 待补跑）。AM5 进行中（owner-local store + Debugger baseline + period/overflow proof 已落地），下一步推荐继续 granted cleanup / store-driven lifecycle。

**2026-05-26 合规审查关键发现**：
- `SAbilityTick`(64行) 为首个 `[BurstCompile]` + `IJobEntity` + `ScheduleParallel` 合规范例（A+），应作为其他 System 迁移模板
- `SEffectCommandSpecStreamPhases`(619行) 游标式 for 循环遍历 DynamicBuffer 为正面范本
- `GASManager.cs` 已收口为 130 行纯 ECS 启动器
- 全系统主线程 foreach+ToEntityArray 仍是主要 P0 违规（`SAbilityCommit`/`SEffectApply`/`SEffectTick`/`SApplyGameplayEffectRequest`）
- `MCCue` managed class 封锁 5 个 Cue System 的 `[BurstCompile]`（P0 缺陷 M）
- ~65 个热路径文件仅 `SAbilityTick` 和 `SAttributeRecalculate` 使用了 IJobEntity

## 非目标

1. 不一次性迁移所有 GameplayEffect。
2. 不在本支线内推进 Editor authoring。
3. 不用 Debugger 或 Presentation 输出替代 Runtime Core 状态。

## 前置依赖

1. `00-当前架构事实/核心问题诊断.md` 已确认旧 Runtime 管线是当前核心问题。
2. T4 Runtime Core Debugger 需要提供诊断 counters，辅助迁移验收。

## 执行范围

1. `Assets/GAS/Runtime/System/Effect`
2. `Assets/GAS/Runtime/Effect`
3. `Assets/GAS/Runtime/Attribute`
4. `Assets/GAS/Runtime/Ability`
5. Runtime tests 和 AutoChess validation。

## 执行细则（共享）

1. 新入口使用 `EffectCommand / SpecStream / AttributeDelta / ActiveEffectStore / TypedFacts` 命名。
2. 结构变化集中到明确 phase；禁止 helper 在热路径隐式创建 / 销毁实体。
3. Observation projection 只能消费 facts，不能反向改变 gameplay state。
4. Runtime Core 任务必须说明修改的 SystemGroup、使用的 `ISystem` / job 形态、是否涉及 ECB playback、DynamicBuffer 容量和 Enableable / stable archetype 策略。
5. Runtime Core 任务行动报告和交还必须列出适用的 `UnityDOTS官方文档参考/主题/90-规则编号索引.md` 规则编号。
6. Runtime Core 任务行动报告和交还必须包含 API 选型表。
7. Runtime Core 任务行动报告和交还必须包含官方文档覆盖检查。
8. 如果任务涉及目标获取、命中、范围、碰撞、触发器、表现资源或 rendered profile，行动报告必须覆盖 `ODF-15..18`、`PHY-*` 和 `GFX-*` 相关规则；如果不涉及，必须给出 not-related reason。

## 验收门槛

1. 新业务默认不依赖旧 instant GE entity lifecycle。
2. AutoChess x1 通过，x50 热点可由 diagnostics 解释。
3. 相关目标态 Spec 和当前架构事实同步更新。
4. 若任务触及 Physics / Graphics，验收必须拆分 `coreTickMs`、physics cost、presentation marker cost 和 render cost。

## 测试链路

1. Runtime EditMode tests。
2. AutoChess headless validation。
3. x50 profile / diagnostics summary。

## API 选型硬约束

AM3 / AM5 后续功能扩张前，必须先复用 AM2B Frame Backbone，并在行动报告中说明本任务如何复用 frame owner、structural playback gate、deterministic stream policy 和 Debugger backbone counters。

1. AM3 继续推进前必须提交 EffectCommand 承载选型表。
2. AM5 后续实现必须提交 ActiveEffectStore 存储选型表。
3. T4 Debugger 必须能输出 API 选型健康指标。
4. 上述选型表属于任务行动报告的一部分。
5. AM3 / AM5 / T4 继续实现前必须引用本轮新增规则：`SEL-21` 到 `SEL-32`、`QRY-06` 到 `QRY-08`、`BUF-07`、`EN-05`、`NAT-07`、`BUR-06`、`DBG-09`。
6. AM3 行动报告必须区分 Boundary request、Core frame command、parallel fan-in stream、structural mutation request 四类 command。
7. AM5 行动报告必须区分 OwnerLocalStore、GlobalIndexedStore、LifecycleCleanupStore、ChunkSkipIndex 四类 ActiveEffectStore。
8. 任何 scale gate 任务必须报告 archetype / chunk / unused entities / DynamicBuffer externalized / enableable wait / job overhead / Burst warmup，而不是只给 `avgTickMs`。
9. AM3 / AM5 / T4 / T2 后续任务行动报告必须新增"官方案例对照"小节。
10. AM3 / AM5 / T4 / T2 后续任务行动报告必须新增"官方文档覆盖检查"小节。
11. 涉及 Physics / Graphics 的任务必须先说明是否需要相关 API，这些内容只能作为 Boundary / Presentation 输入输出。

## 当前任务看板

| 任务ID | 任务名 | 状态 | 文件 |
|---|---|---|---|
| T1-RuntimeCore-AM0 | Freeze Safety Gate | 契约已确立 | [AM0-FreezeSafetyGate.md](AM0-FreezeSafetyGate.md) |
| T1-RuntimeCore-AM1 | Unity Entities 机制校准 | 契约已确立 | [AM1-机制校准.md](AM1-机制校准.md) |
| T1-RuntimeCore-AM1B | Unity DOTS API 选型修正 | 已完成 | [AM1B-API选型修正.md](AM1B-API选型修正.md) |
| T1-RuntimeCore-AM1C | DOTS API 深读反推 Runtime Core | 已完成 | [AM1C-DOTS深读反推.md](AM1C-DOTS深读反推.md) |
| T1-RuntimeCore-AM1D | Unity DOTS 官方案例反推 Runtime Core | 已完成 | [AM1D-官方案例反推.md](AM1D-官方案例反推.md) |
| T1-RuntimeCore-AM1E | Unity DOTS 官方文档参考体系主题化与流程闭环 | 已完成 | [AM1E-文档体系主题化.md](AM1E-文档体系主题化.md) |
| T1-RuntimeCore-AM1G | Unity Physics / Entities Graphics 新包覆盖 | 已完成 | [AM1G-物理表现覆盖.md](AM1G-物理表现覆盖.md) |
| T1-RuntimeCore-AM2 | EffectCommand 与 SpecStream 契约 | 契约已确立 | [AM2-EffectCommand契约.md](AM2-EffectCommand契约.md) |
| T1-RuntimeCore-AM2B | Runtime Core Frame Backbone | 已完成任务链（不再直接领取） | [AM2B-FrameBackbone/](AM2B-FrameBackbone/README.md) |
| T1-RuntimeCore-AM3 | Instant Spec Evaluation 迁移 | 契约已确立 | [AM3-InstantSpecEvaluation.md](AM3-InstantSpecEvaluation.md) |
| T1-RuntimeCore-AM5 | Active Effect Store 重建 | 进行中 | [AM5-ActiveEffectStore.md](AM5-ActiveEffectStore.md) |
