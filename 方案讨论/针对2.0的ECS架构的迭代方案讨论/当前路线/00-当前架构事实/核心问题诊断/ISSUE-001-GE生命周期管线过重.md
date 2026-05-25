# ISSUE-001 GE 生命周期管线过重

## 状态

| 字段 | 内容 |
|---|---|
| 状态 | Active |
| 严重度 | P0 |
| 最近复核 | 2026-05-24 |
| 所属层 | GAS Runtime Core Layer |

## DOTS规则引用

| 规则编号 | 规则摘要 | 关联原因 |
|---|---|---|
| `PRF-01` | 禁止用 Entity 表示临时/瞬时状态 | instant GE 不应创建 runtime GE entity 走完整生命周期 |
| `PRF-02` | 禁止在 Hot Path 直接执行结构变化 | GE factory 中 `AddComponentData` / `AddBuffer` 是热路径结构变化 |
| `PRF-05` | Hot Path 禁止主线程遍历 | `SApplyGameplayEffectRequest` 全主线 `foreach` + `ToEntityArray` |
| `JOB-01` | 并行批处理说明 IJobEntity/IJobChunk 选择理由 | 当前全主线程，未 job 化 |
| `SC-01` | Hot path 不直接结构变化；集中在 mutation phase | request entity 创建/销毁分散在多个系统中 |
| `ECB-01` | ECB 是延迟结构变化工具，不是 gameplay event bus | ECB playback 散落在 helper 方法中，每次都是独立 sync point |
| `BUF-02` | 单一全局 buffer 限于 proof/低量 | 不应所有 GE 都走 entity lifecycle + 全局 buffer |
| `FSM-04` | 单 entity 上多 FSM 叠加时评估拆分 entity | duration/stack/period/granted 多套生命周期耦合 |
| `SEL-01` | 数据性质分类优先：Gameplay/Transient/Telemetry/Presentation | instant spec 与 active effect lifecycle 应区分承载 |
| `CASE-02` | IJobEntity 遍历 — Runtime Core hot path 主力 | 当前未使用，全主线程 foreach |
| `CASE-05` | ECB 延迟结构变化 — 结构变化唯一方式 | 存在直接 `EntityManager.AddComponentData` 调用 |

## 问题陈述

当前 simple instant GameplayEffect 仍容易落入 `request entity -> runtime GE entity -> lifecycle -> modifier resolve -> eventbus/outbox` 的完整实体生命周期管线。这个粒度适合 Duration / Stack / Period / Granted 状态，但不适合作为普通伤害、治疗、吸血、毒爆等高频 instant spec 的默认路径。

## 当前证据

代码证据：

1. `SApplyGameplayEffectRequest` 每帧先 `ToEntityArray` 收集 request entity，再逐个处理并 `DestroyEntity`：`Assets/GAS/Runtime/System/Effect/SApplyGameplayEffectRequest.cs:25-58`。
2. 当 fast instant path 未命中时，会创建 runtime GE entity、写入 `CEffectContext` / `CEffectSpecData` / `CEffectLifecycle`，再解析 modifier 并写事件：`Assets/GAS/Runtime/System/Effect/SApplyGameplayEffectRequest.cs:203-236`。
3. GE prototype 实例化会 `Instantiate` 原型、移除 `CGameplayEffectPrototype` 并追加 runtime components：`Assets/GAS/Runtime/Effect/GameplayEffectEntityFactory.cs:34-44`。
4. `GameplayEffectRequestWriter` 仍以创建 `CApplyGameplayEffectRequest` entity 作为旧统一入口；AM0 已将局部 direct instant bypass 改名为 `TryApplyLegacyInstantModifierBypass` / `ApplyLegacyInstantBypassOrCreateSingleTargetRequest`，并新增 `GameplayEffectRuntimePipelineContract` 标记 `LegacyInstantEntityLifecycle` 为 migration-only 冻结路径。
5. AM2 已新增 `CEffectCommandSpecStream`、`BEffectCommand`、`BInstantEffectSpec`、`BAttributeDelta`、`BTypedSimulationFact` 数据契约和 phase skeleton。
6. AM3 已让 direct simple instant command 走 `BEffectCommand -> BInstantEffectSpec -> BAttributeDelta -> BTypedSimulationFact` 主链，并覆盖 constant / SetByCaller magnitude。
7. AM3 producer migration 已让 `SAbilityCommit` 的 activation self / target simple single-target GE producer、`AbilityRuntimeActions.RequestCostGameplayEffect` 的 simple instant self cost producer，以及 `TimelineApplyEffectsProducer` 的 single-target 与 multi-target simple instant ApplyEffects producer 优先写入 `EffectCommand` stream；`STypedSimulationFactEventBridge` 会把 `BTypedSimulationFact(AttributeBaseValueChanged)` 增量投影到旧 `BAttributeChangeEvent`，作为迁移期 legacy 出口；`SPresentationOutboxProjection` / `SDebugReplayLogProjection` 已能直接消费 `BTypedSimulationFact(AttributeBaseValueChanged)` 并通过 `SourceFactSequence` 跳过同源 legacy duplicate；`SInstantEffectCueRequestProjection` 会从 `BInstantEffectSpec.CueRequestOnApplyCode` 生成 `BTypedSimulationFact(CueRequested)`，并保留旧 `BCueRequest` / `BGameplayEvent(CueRequested)` 迁移期出口，Presentation / Replay 已能直接消费 cue typed fact 并跳过同源 legacy duplicate，使 simple instant Cue-on-Apply 不再强制回落 request entity；attribute / cue 之外的 generic gameplay typed fact 与 damage typed fact 也已能直接进入 Presentation / Replay，并分别跳过同源 legacy `BGameplayEvent` / `BDamageEvent` duplicate。Duration / Stack、Tag requirements、Granted state、复杂 period / overflow child GE 等复杂语义仍回落旧 request，cooldown 仍等待 AM5 / ActiveEffectStore 生命周期迁移，broader business reaction typed fact consumer 与真实 parallel fan-in / deterministic merge 仍需要继续收缩。
8. AM5 第一刀已新增 ASC owner-local `CActiveEffectStore` / `BActiveEffectSlot`，并把旧 duration runtime GE entity 的 Active / Inhibited / PendingRemove / Remove、duration refresh 和 stack count 镜像到 owner slot；本轮又让 period due / overflow 的 simple instant child GE 优先写入 `BEffectCommand(Source=Period/Overflow)`，并同步刷新 owner-local `LastPeriodFrame`。该路径仍标记为 legacy entity-backed mirror + proof-only command stream，不代表完整 active effect lifecycle 已迁移完成。

Profile / 记录证据：

1. x50 profile 下仅 300 单位已出现 `avgTickMs=13.769542`，`GameplayEffectApplied=3479`、`AttributeChanges=2730`、`CueRequests=3466`：`../../../迭代记录/91-T6-CHESS-AL-ArchitecturePipelineDiagnosis.md:13-26`。
2. system timing 排序中 `SEffectApply`、`SApplyGameplayEffectRequest`、`SEffectTick` 均为热点：`../../../迭代记录/91-T6-CHESS-AL-ArchitecturePipelineDiagnosis.md:28-40`。
3. 后续优化记录明确指出 simple modifier 应进入 Spec Apply Path，避免 request entity -> GE runtime entity -> lifecycle -> destroy：`../../../迭代记录/88-T6-CHESS-AG-RuntimeTickProfileAndArchitectureFeedback.md:153-156`。

## 执行路径

```text
Ability / Reaction
-> GameplayEffectRequestWriter.Create(...)
-> CApplyGameplayEffectRequest entity
-> SApplyGameplayEffectRequest.OnUpdate
-> TryApplyLegacyInstantModifierBypass 未覆盖时创建 runtime GE entity
-> EffectMagnitudeResolver / ExecutionCalculation / SEffectApply
-> Attribute / Tag / Cue / GameplayEvent
-> SEffectTick / cleanup / DestroyEntity
```

## 影响

1. 高频 instant GE 把结构变化、entity lifecycle、buffer append、cue/replay/presentation 都拖进同一条热路径。
2. GE Apply 成本随业务事件数量放大，而不是随 chunk/job 批量处理收益下降。
3. 后续继续补旧 direct bypass 会扩大分支复杂度，但不能消除 lifecycle 粒度错误；AM0 冻结门要求新入口转向 EffectCommand / InstantEffectSpec / AttributeDelta，AM2 已先固定 ECS 数据契约，AM3 已完成 direct simple instant evaluation 主链落点并迁入 ability activation simple single-target producer、ability cost self producer、Timeline ApplyEffects single-target simple instant producer 与 Timeline ApplyEffects multi-target simple instant command fan-out，且补入 attribute typed fact 到旧 attribute event bus 的迁移期观察桥、attribute typed fact native Presentation / Replay consumer、simple instant Cue-on-Apply projection、cue typed fact native Presentation / Replay consumer、generic gameplay typed fact native consumer 和 damage typed fact native consumer；AM5 已开始把 duration lifecycle 镜像到 ActiveEffectStore，并已让 period due / overflow simple instant child GE 走 EffectCommand；后续需要继续迁移剩余 producer、business reaction typed fact 原生 consumer、granted cleanup、复杂 active lifecycle fallback、真实 parallel fan-in / deterministic merge 和 store-driven lifecycle。

## 根因反推

当前实现吸收了历史方案中“GE 通过实体表达”的方向，但没有区分 instant spec 与 active effect lifecycle。方案12 的 instant GE 实体化示例已经被复盘判定为风险：`../../../迭代记录/92-T6-CHESS-AL-RouteReflectionFromPlans12To15.md:19-28`。

目标态应拆成：

```text
EffectCommand -> InstantEffectSpec -> AttributeDelta / TagDelta -> TypedSimulationFact
ActiveEffectCommand -> ActiveEffectStore -> Period/Stack/Granted lifecycle
ObservationProjection -> Replay / Presentation / Debugger
```

## 目标态入口

1. `../../01-目标态架构共识/03-RuntimeCore管线Spec.md`
2. `../../01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
3. `../../01-目标态架构共识/05-ActiveEffectStoreSpec.md`

## 任务入口

`../../02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCore重构.md`

## 退出条件

1. 普通 instant GE 默认不创建 runtime GE entity。
2. x1 / x50 AutoChess summary 输出 request/spec/delta/fact 计数，能证明 instant spec 进入 batchable pipeline。
3. ActiveEffectStore 能承载 duration / stack / period / granted cleanup 主状态，runtime GE entity 只保留明确的低频或迁移期职责。
4. `SApplyGameplayEffectRequest / SEffectApply / SEffectTick` 不再共同支配 simple damage/heal 热点。
