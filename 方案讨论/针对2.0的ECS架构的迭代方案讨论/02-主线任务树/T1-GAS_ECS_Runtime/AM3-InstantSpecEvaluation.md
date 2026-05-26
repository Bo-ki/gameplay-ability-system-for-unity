# GAS ECS Runtime - Runtime Core 重构 - Instant Spec Evaluation 迁移

## 节点定位

本节点承接 AM2（EffectCommand/SpecStream 数据契约）和 AM2B（Frame Backbone），负责把 simple instant GE 从旧 entity lifecycle 迁移到 `BEffectCommand → BInstantEffectSpec → BAttributeDelta → BTypedSimulationFact` 四阶段主链。输出给 AM5（ActiveEffectStore）作为 store-driven lifecycle 的上游，输出给 T4（Debugger）作为 diagnostics 的权威数据源。

## 父节点

[RuntimeCore重构](README.md)

## 任务ID

`T1-RuntimeCore-AM3`

## 状态

`契约已确立`

## 兄弟关系

`sequential`。前置：AM2（数据契约）、AM2B（Frame Backbone）全部完成。后继：AM5（ActiveEffectStore）的部分功能依赖 AM3 的 typed fact 输出。AM3 闭合后 AM5 的 store-driven lifecycle 才能获得稳定的上游事实输入。

## 拆分历史

从 `RuntimeCore重构` 拆分（S3 多关注点 + S4 依赖链：AM3 instant evaluation 与 AM5 active store 关注点独立且 sequential），2026-05-20。

## 领取轮次

第 14 轮（累计 13 轮已完成）

## 当前进展

activation / cost / Timeline ApplyEffects single+multi target simple instant producer 已全部走 `BEffectCommand` 主链，不创建 `CApplyGameplayEffectRequest` 或 runtime GE entity。attribute / cue / generic gameplay / damage typed fact 的 native Presentation / Replay consumer 全部就位。

2026-05-26 第 14 轮闭合 AM3 完成边界条件 3/4：`EffectRuntimeUtility`、`EffectMagnitudeResolver`、`SExecutionCalculation`、`SExecutionCalculationOutputModifier` 的 AM3 / System/Effect 静态 `EventBusHelper.Enqueue*` 裸调用已迁移为显式 `GameplayEventBusWriter` 参数传递。`rg -n "EventBusHelper\.Enqueue" Assets/GAS/Runtime/System/Effect/ -g "*.cs"` 结果为空。

`EffectCommandSpecStream` 新增 `ParallelCommandFanInRecord` / `MergeParallelCommandFanIn`，按 `TargetAsc(Index,Version) -> Command.Sequence -> ProducerIndex -> LocalIndex` 做确定性合并，并推进 `NextCommandSequence` / `NextContextId`。`EffectCommandSpecStreamContractTests.ParallelFanInCommandMergeIsStableByTargetThenSequence` 已覆盖 parallel fan-in stream 承载、merge policy、排序键和重复 merge 顺序稳定性。

验证：`git diff --check` 通过；`dotnet build com.exhard.exgas.runtime.csproj --nologo --verbosity:minimal` 通过；`dotnet build com.exhard.exgas.runtime.tests.csproj --nologo --verbosity:minimal` 通过。Unity Test Runner 未跑，仍需环境可用时补跑 EditMode。

## 本轮目标

本轮已闭合 AM3 完成边界条件 3（EventBus callsite 收尾）和条件 4（parallel fan-in contract test）。下一轮推荐转入 AM5 granted cleanup / store-driven lifecycle；若 Unity Test Runner 环境可用，先补跑 AM3 EditMode 作为交还验证。

## 当前问题

1. 旧 direct bypass（`TryApplyLegacyInstantModifierBypass`）仍是 migration-only 冻结路径，不能继续作为新业务默认入口。
2. AM3 direct command 主链已具备 parallel fan-in contract proof，但 Unity Test Runner / AutoChess 真实业务链路仍未执行，不能视为全局 Goal 完成。
3. `Assets/GAS/Runtime/System/Effect/` 内 AM3 相关静态 enqueue 已清零；`AbilityRuntimeActions`、`SAbilityTimelineAction`、`SAscCommandRequest`、`AttributeHelper` 等非 AM3 System/Effect legacy callsite 仍属于 ISSUE-011 后续收缩范围。
4. `GASManager.EntityEventBus` 仍是迁移期 singleton DynamicBuffer 承载，后续 scale-ready EventBus / fact fan-in 仍需 NativeStream 或 owner-local fact buffer 选型。

## 目标

1. direct `BEffectCommand` 能生成 `BInstantEffectSpec`。
2. simple instant modifier 能在新 stream 内解析 constant / SetByCaller magnitude，并直接更新目标 ASC attribute。
3. attribute 写入同时产生 `BAttributeDelta`，再投影为 `BTypedSimulationFact`。
4. direct command 主链不创建 `CApplyGameplayEffectRequest`，也不创建 runtime GE entity。
5. attribute typed fact 至少能投影到旧观察链路和 Presentation / Replay native consumer。
6. AM3 完成边界条件 3/4 闭合：无静态 `EventBusHelper.Enqueue*` 残留在热路径 + parallel fan-in contract test 通过。

## 目标态约束摘要

（从关联 Spec 提取，与本任务直接相关的硬约束）

1. simple instant GE 默认走 `BEffectCommand → BInstantEffectSpec → BAttributeDelta → BTypedSimulationFact` 四阶段主链，不创建 `CApplyGameplayEffectRequest` 或 runtime GE entity。复杂 GE（含 Duration/Period/Stack）允许回落 Boundary request 并由 AM5 承接。
   > 来源：`04-EffectCommand-SpecStream-AttributeDeltaSpec.md`

2. `ContextId`、`ParentContextId`、`SetByCaller` 必须从 command 连续传递到 spec → delta → fact，不允许断链。
   > 来源：`04-EffectCommand-SpecStream-AttributeDeltaSpec.md`

3. Observation projection 只能消费 typed facts，不能反向改变 gameplay state。
   > 来源：`03-RuntimeCore管线Spec.md`、`06-Observation-Presentation-ReplaySpec.md`

4. AM2B Frame Backbone 已固定 phase contract：本任务的 System 必须绑定到 `CommandIngest / SpecEvaluation / DeltaApply / TypedFactProjection`，不得绕过。
   > 来源：`GASRuntimeFrameBackboneRebindContract.cs`、AM2B-A

5. singleton `DynamicBuffer` 是当前 frame-local 承载，但不得在文档或报告中描述为 scale-ready 最终方案。
   > 来源：`20-GASRuntimeCore-API选型基线.md`、AM2B-C

## 当前事实约束

（从关联 ISSUE 提取，本任务必须知道的架构事实）

1. 旧 GE lifecycle 的 per-hit entity create/destroy 是核心热点来源（ISSUE-001）。本任务的 direct command 主链不得做 per-hit request entity 或 runtime GE entity create-destroy。
   > 来源：`ISSUE-001`、`ISSUE-004`

2. 当前 runtime 中 `EffectRuntimeUtility`、`SExecutionCalculation`、`SExecutionCalculationOutputModifier` 的 AM3 完成边界条件 3 待收尾项已闭合；非 AM3 System/Effect 的 legacy enqueue callsite 仍作为 ISSUE-011 后续问题保留。
   > 来源：`ISSUE-011`

3. AM2B Frame Backbone 已闭合（ISSUE-009 Resolved），AM3 后续实现必须复用 frame owner、structural playback gate、deterministic stream policy 和 Debugger backbone counters。
   > 来源：`ISSUE-009`

## 适用规则摘要

| 规则 | 本任务约束 |
|---|---|
| `SC-01` | 结构变化（add/remove component、create/destroy entity）禁止在 hot path；direct command 主链不做 per-hit entity 创建 |
| `BUF-01` | DynamicBuffer 容量在 create 时显式指定，不允许隐式扩容 |
| `BUF-02` | singleton DynamicBuffer 只在主线程访问，不跨 job 共享写入 |
| `BUF-03` | Buffer 写入前检查容量；有溢出风险时记录 warning 并拒绝而非扩容 |
| `ECB-01` | ECB Playback 统一在 System 末尾，禁止中间调用 |
| `SYS-01` | SystemGroup 显式声明 `[UpdateInGroup]`，不隐式依赖默认 group |
| `SYS-04` | System 间数据传递通过 ECS component/buffer，不通过 static 或 managed 中介 |
| `DBG-01` | Debugger counters 只读不写，不改变 simulation state |
| `DEF-01` | definition 数据只读；runtime 不修改 definition component |
| `QRY-01` | Query 使用 `RefRO`/`RefRW` 显式读写意图 |
| `SEL-01` | singleton DynamicBuffer 的 API 选型必须在行动报告中说明当前承载和切换条件 |
| `SEL-21` | AM3 行动报告必须区分 Boundary request、Core frame command、parallel fan-in stream、structural mutation request 四类 command |

## 非目标

1. 不迁移所有旧 producer（仅 simple instant）。
2. 不重写 Active Effect Store（由 AM5 负责）。
3. 不继续推进 AutoChess 业务拆分。
4. 不恢复 OOP 生命周期。
5. 本轮不新增 producer 类型（activation/cost/Timeline ApplyEffects 已覆盖）。

## 执行范围

代码目录：
1. `Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs`
2. `Assets/GAS/Runtime/System/Effect/SEffectCommandSpecStreamPhases.cs`
3. `Assets/GAS/Runtime/System/Effect/GameplayEffectRequestWriter.cs`
4. `Assets/GAS/Runtime/System/Ability/SAbilityCommit.cs`
5. `Assets/GAS/Runtime/Ability/AbilityRuntimeActions.cs`
6. `Assets/GAS/Runtime/Ability/TimelineAbility/TimelineApplyEffectsProducer.cs`
7. `Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs`（EventBus callsite 收尾）
8. `Assets/GAS/Runtime/System/Effect/SExecutionCalculation.cs`（同上）
9. `Assets/GAS/Runtime/System/Effect/SExecutionCalculationOutputModifier.cs`（同上）

测试目录：
10. `Assets/_Test/GAS/Runtime/Effect/`
11. `Assets/_Test/GAS/Runtime/Ability/`

## 执行细则

1. **SystemGroup 绑定**：所有 AM3 System 必须按 AM2B phase contract 绑定到 `CommandIngest / SpecEvaluation / DeltaApply / TypedFactProjection`。不允许创建新的隐式 group。
2. **ISystem 形态**：当前先用 `ISystem` 主线程 EntityManager 路径证明语义正确，后续 AM5 / Burst 优化再拆 job 化。不允许本任务中引入未验证的 job 并行化。
3. **结构变化边界**：direct command 主链不做 per-hit request entity / runtime GE entity create-destroy。任何必要的结构变化必须 route 到 AM2B-D structural playback gate。
4. **DynamicBuffer 约束**：以 singleton owner 上的 buffer 承载 frame-local 主链数据，但不得在代码注释、文档或报告中描述为 scale-ready。容量在 buffer create 时显式指定。
5. **Command 四分层**：新增或修改的 command 写入路径必须明确是 Boundary request、Core frame command、parallel fan-in stream、还是 structural mutation request。
6. **EventBus callsite 收尾**：`EffectRuntimeUtility`、`SExecutionCalculation*` 中的静态 `EventBusHelper.Enqueue*` 必须改为显式 `GameplayEventBusWriter` 参数传递。禁止新增静态 enqueue 调用。
7. **parallel fan-in**：至少一个多 job producer fan-in 场景通过 contract test，证明两个以上并行 ISystem 写入同一个 stream 后 merge 结果可重现。
8. **Debugger 证据**：AM2B-E counters 和 AM2B-F rebind contract 是本任务交还的证据源，每次交还必须引用。

## 验收标准

1. direct simple instant command 通过 `GASCommandGroup` 后产生 1 条 spec、1 条 delta、1 条 typed fact。
2. target attribute base/current 值按 modifier 更新，并保留 dirty / previous current value 标记。
3. SetByCaller magnitude 能从 command stream range 解析。
4. 同 effect code 不产生 `CApplyGameplayEffectRequest`，也不产生 runtime GE entity。
5. attribute typed fact 能投影到旧 `BAttributeChangeEvent`，重复更新不重复投影。
6. simple instant Cue-on-Apply 能投影到旧 `BCueRequest` / `BGameplayEvent(CueRequested)`。
7. `EffectRuntimeUtility`、`SExecutionCalculation`、`SExecutionCalculationOutputModifier` 中无静态 `EventBusHelper.Enqueue*` 裸调用。
8. 至少一个 parallel fan-in contract test 通过，验证多 job 写入后 merge 结果确定性。
9. `dotnet build` Runtime + Tests 通过；Unity Test Runner 如受 LicensingClient 阻塞则记录为环境阻塞。

## 测试链路

1. `git diff --check`
2. `dotnet build` Runtime + Tests
3. `rg -n "EventBusHelper\.Enqueue" Assets/GAS/Runtime/System/Effect/ -g "*.cs"`（应为空）
4. `rg -n "parallel.*fan.*in|fan.*in.*merge|deterministic.*merge" Assets/_Test/GAS/Runtime -g "*.cs"`
5. Runtime EditMode tests；如 Unity LicensingClient 阻塞，记录 return code 和关键日志。

## 交还内容

1. 更新本文件 `当前进展`、`领取轮次`、`状态` 字段。
2. 更新 `02-主线任务树/README.md` 根看板 AM3 条目。
3. 更新 `04-当前进度状态/当前窗口.md` 推荐领取。
4. 若完成边界条件闭合，更新 `04-当前进度状态/迭代摘要.md` 和 `ISSUE-011` 状态。
