# GAS 官方概念对照复核 Spec

## 目的

用 Epic UE GAS 官方文档与 GameplayAbilities 源码复核 Unity DOTS 目标态，确认在放弃 UObject、Actor callback、网络预测实现和每对象 Entity 映射后，仍保留 GAS 的领域语义、不变量与可诊断身份。

本文是完整语义验收矩阵的唯一 owner；具体概念、业务链、Command/Target、Capture 与 ActiveEffect 存储分别由 [01](01-GAS概念模型Spec.md)、[01B](01B-GAS业务语义链路概念设计Spec.md)、[03D](03-RuntimeCore管线/03D-CommandResolve与TargetResolveSpec.md)、[04](04-EffectCommand-SpecStream-AttributeDeltaSpec.md)、[05](05-ActiveEffectStoreSpec.md) 所有。

本文件只描述目标态，不记录当前完成度、迁移流水或验证数字。

## 证据基线

源码证据相对于 UE-GAS snapshot `7d4942e4ce958c04bbd404569f1156224a4b9a15` 的 `Source/GameplayAbilities` 目录。该源码树没有独立业务设计文档；头文件契约、实现顺序与测试共同构成语义证据。

| 主题 | 精确源码证据 | 结论 |
|---|---|---|
| Granted Ability Spec | `Public/GameplayAbilitySpec.h:160-265` | Spec 是 ASC 上的跨帧授权状态，持有 level/input/source、ActiveCount、instances 与 granting GE provenance |
| Ability activation concurrency | `Private/AbilitySystemComponent_Abilities.cpp:1592-1690` | PerExecution 可产生多个实例；PerActor 有拒绝/重触发策略；Activate 可在返回前 End |
| Ability remove | `Private/AbilitySystemComponent_Abilities.cpp:563-644` | revoke 会结束实例、注销触发并解除 granting GE 关系 |
| Commit | `Private/Abilities/GameplayAbility.cpp:408-505` | CanActivate 与延迟 Commit 分离；Commit 重新检查 cost/cooldown |
| Granted-by-GE removal policy | `Public/GameplayAbilitySpec.h:44-54`; `Private/GameplayEffect.cpp:4275-4300` | Cancel immediately、remove when activations end、leave granted 是三个不同结果；“DoNothing”不是 tombstone |
| Task lifecycle | `Public/Abilities/Tasks/AbilityTask.h:17-42,95-170` | Task 是 Ability 内的异步/等待操作，必须随 owner cleanup |
| 多 Task | `Private/Abilities/GameplayAbility.cpp:618-676,1325-1424` | ActiveTasks 是数组，同名可多实例，Ability End 清理全部 Task |
| 外部事件等待 | `Private/Abilities/Tasks/AbilityTask_WaitGameplayEvent.cpp:17-100` | one-shot/persistent、exact/hierarchical，并可监听外部 ASC |
| Level / handle lifecycle wait | `Private/Abilities/Tasks/AbilityTask_WaitGameplayTag.cpp:78-105`; `Private/Abilities/Tasks/AbilityTask_WaitGameplayEffectRemoved.cpp:24-76` | Level 已满足/handle 已无效可以立即完成；注册、取消与 owner cleanup 不能丢事件或晚到误命中 |
| GE apply | `Private/AbilitySystemComponent.cpp:762-944` | requirement、instant/duration 分流、Cue、Execute、OnApplied 在应用调用链中闭合 |
| GE execution | `Private/GameplayEffect.cpp:2708-2851,3502-3565` | modifier/execution、conditional effects、Pre/Post attribute execute 具有同步语义 |
| ActiveEffect inhibit/remove | `Private/AbilitySystemComponent.cpp:278-304`; `Private/GameplayEffect.cpp:3906-4032,4065-4249` | Inhibit 保留 effect identity 但撤销/恢复 side effects；Remove 精确清理 |
| Stack / period temporal policy | `Public/GameplayEffect.h:2181-2199,2223-2233,2328-2346`; `Private/GameplayEffect.cpp:3125-3162,3678-3692,3838-3848,4634-4689` | stack key、refresh、period reset、execute-on-apply、overflow、expiration/final period 是独立策略；period execution 可能 self-remove，必须复检 |
| Cue inhibit/remove | `Private/GameplayEffect.cpp:3901-3904,4005-4016,4306-4316` | ActiveEffect identity 与 Cue active lifecycle 可分离；撤销 side effects 时发 Removed，恢复时重新 Add/WhileActive |
| Capture API | `Public/GameplayEffect.h:739-874` | Capture 支持 Final/Base/Bonus/Channel/Contribution/Aggregator/ModList，不是单一 float |
| Capture timing | `Private/GameplayEffect.cpp:1446-1602,2708-2727,3738-3785` | Source 在 Spec 初始化捕获；Target 在 apply 捕获；periodic execute 可重捕 TargetTags |
| Snapshot / Live | `Private/GameplayEffect.cpp:1968-2102,2230-2239,2978-3033,3351-3362` | Snapshot 复制 Aggregator；Live 登记 dependent 并在 dirty 时重算 |
| Aggregator data | `Public/GameplayEffectAggregator.h:53-75`; `Private/GameplayEffectAggregator.cpp:10-79,555-559` | qualifier 依赖 tags/filter/ignore/contributor；snapshot 复制 Base + ModChannels |
| GameplayEvent | `Private/AbilitySystemComponent_Abilities.cpp:2220-2252,2288-2328` | 当前调用栈按原始 tag 与父 tag 激活并广播 |
| OwnedTag trigger | `Private/AbilitySystemComponent_Abilities.cpp:2370-2420` | tag count 0/非 0 transition 同步激活/取消 |
| EffectContext | `Public/GameplayEffectTypes.h:217-450` | Instigator、EffectCauser、SourceObject、Ability、Actors、Hit、Origin 独立存在 |
| Prediction | `Public/GameplayPrediction.h:17-132,172-212,263-285,436-572` | PredictionKey 专门解决 accept/reject、undo/redo、catch-up 与依赖，不是一般因果 id |

## 最终复核结论

### 必须保留的 UE 领域语义

目标态必须形成两条不可压平的链：

```text
AbilityDefinition → GrantedAbilitySpec → AbilityActivation → Continuation/Subscription
GameplayEffectDefinition → GameplayEffectApplicationSpec → EffectApplication → ActiveEffect(optional)
```

ASC-local generational slab 可以替代 UE 对象实例，但前提是：Granted、Activation、Continuation、Subscription、ActiveEffect 使用独立强类型 Handle；支持多实例并发；禁止 compact；具有 tombstone 与 stale-handle rejection。

此外必须保留：CanActivate 与 Commit 分离、Commit 时重新检查 cost/cooldown、Activation End/Cancel cleanup、GE Definition/Spec/Active 分离、per-target requirement/immunity/capture、Base/Current/Aggregator、stack/period/inhibition、granted ability removal 三态、Task wait 多实例、Tag/Event/Cue/Context provenance 与精确 contribution removal。

### v1 更强的确定性保证

以下不是对 UE UObject/Timer/Delegate 代码形状的复制，而是保持玩法结果所需的更强闭世界保证：

- source-local CommitPlan 在全量 requirement/capacity/cost/cooldown 预检后 no-fail 原子提交 `Committed + cost + cooldown + activation-owned contributions`；同 ASC AscOwnerCommandWave canonical read-your-writes。
- Gather 先以 sealed/due count 与 Catalog bake maxima 建立 `PlanExpandScratchEnvelopeToken`，在 Plan/Expand 写前 provision 其 scratch；WholeTickInfraAdmission 再验证 token并在任何 Owner/Target 权威写前预留下游/持久基础设施容量。任一步逻辑失败都由唯一 AdmissionResult 使本 Tick gameplay 零写并锁存 InfraAdmissionFault，成功后 capacity 不再是 application 业务分支。
- 每条 target application 在目标 ASC writer 内最终线性化，重新 target capture/requirement/immunity/TargetLifePolicy，并输出 typed outcome/blocker provenance。
- wait 的 sample+register 在 observed ASC writer 内线性化；Handle Generation 阻断晚到 Ack/Completion。
- stack/period/cue 由完整 StackTemporalContract、DueClaim/PeriodExecutionOrdinal 与 CueLifecycleKey 消除 Timer/callback 竞态。
- Boundary 以 accept-before-clear、BatchId/InFlightWatermark 与 semantic hash 保证重试不重不漏。

### 有意偏离 UE 的时序/实现语义

v1 采用 stable-state deferred reaction。GameplayEvent、OwnedTag 触发 Ability、外部 Continuation 与跨 ASC reaction 默认下一 tick；只有 kernel invariant 与生成期闭合、完全展开、有限且静态有界的 DirectEffectProgram same-tick。这是明确的时序语义差异，不能宣称 UE call-stack parity。

| 偏离点 | v1 正式语义 | 配置/玩法补救 |
|---|---|---|
| 普通 self GE 对同 AscOwnerCommandWave 后续 CanActivate 的可见性 | 普通 GE 进入 AscTargetStateWave，不提供同步反馈 | 改为 CommitPlan/activation-owned invariant；否则 Definition bake fail |
| GameplayEvent/OwnedTag/Task delegate | emit T，公开 delivery/resume 最早 T+1 | 改写为 kernel invariant/DirectEffectProgram，或接受 tick latency |
| apply/overflow/period/reaction 动态 child | 默认 T+1 | 只有静态闭合、有界 DirectEffectProgram 可 same-tick |
| 跨 ASC Live dependency | source revision T，destination dirty T+1；同 tick 先 dirty 后 PeriodDue | 玩法需要 same-tick 跨 owner 闭环时不属于 v1 |
| Commit callback 顺序 | 全量预检后 no-fail 原子 CommitPlan，不暴露 cooldown 已写而 cost 写失败的中间态 | 自定义 cost/cooldown 必须生成纯 evaluator + reservation，不能托管回调写状态 |
| callback re-entry | target transaction 完成后只发布稳定事实 | 依赖 application/execute/remove callback 同栈重入的 Definition 拒绝或改写 |

### 项目策略，不宣称 UE GAS 通用语义

AutoChess 默认 `TargetLifePolicy=AliveOnly`、首次 Health `old>0 && UnclampedResult<=0` 冻结 DeathTransition/killer/overkill、后续 AliveOnly application 零 damage/overkill/assist，是本项目的确定性战斗规则。UE GAS 提供 Attribute/Effect hook，但不内建这一套死亡/斩杀结算；文档与测试必须把它标为项目策略而不是“UE 官方等价”。

### 有意不实现 Prediction / Replication

v1 不只是不执行预测，而是从 schema/API 删除 PredictionKey、预测模式、预测 modifier、rollback/ack/caught-up 与 predictive overlay。Identity/provenance 仍完整保留，为确定序、cleanup、debug/replay 服务。

## 官方概念目标映射

| UE GAS 概念 | v1 必保语义 | DOTS 目标表达 | 可舍弃 UE 实现偶然性 |
|---|---|---|---|
| ASC / ActorInfo | owner 聚合、Owner/Avatar 分离、Attribute/Tag/Ability/Effect owner | ASC Entity + AscInstanceId + owner-local slabs | UObject component、Actor weak pointer、global handle map |
| GameplayAbility Definition | immutable policy、requirements、cost/cooldown、tags、program | Blob definition + generated pure evaluator + source-local CommitPlan | CDO、Blueprint virtual call |
| Granted Ability Spec | grant provenance、level/input、reentry、0..N activation、三态 removal | GrantedAbilitySlot；LeaveGranted 保持 Live | FFastArray/UObject instance arrays；`DoNothing` 命名 |
| Ability Activation | 独立 context/commit/owned state/end reason；普通 emitted GE 不随 End | AbilityActivationSlot + OwnedContributionRanges + audit-only EmittedApplicationRefs | Activation UObject/Entity |
| AbilityTask | 多实例异步、Level/Edge/Event/HandleLifecycle/Timer、owner cleanup、外部监听 | AbilityContinuationSlot + AbilitySubscriptionSlot + observed-writer sample/register | UGameplayTask、delegate、coroutine |
| GE Definition | modifier/execution/duration/period/stack/requirement/grant/cue | Blob definition | UGameplayEffect object |
| GE Spec | source/target context、level、SetByCaller、capture、per-target final application | optional pre-Commit SpecDraft → post-Commit source-bound Spec + target-writer Application View/outcome | mutable UObject wrapper；TargetResolve 预写结果 |
| Active GE | target-local duration/stack payload/period/inhibit/removal/contribution | ActiveEffectSlot + StackTemporalContract + optional per-application ledger | per-effect Entity、timer delegate |
| Attribute/Aggregator | Base/Current、ordered contribution、snapshot/live、exact removal、unclamped/effective mutation evidence | Session AttributeLayout + fixed AttributeValueSlot + accumulator + CaptureProjectionContract + AttributeMutationFact；generated 仅提供 id/index/init projection/纯访问器 | pointer graph、dirty delegate、generated component mirror |
| GameplayTag | hierarchy、reference count、requirements/query | dense id/mask/count/query | string lookup hot path |
| GameplayEvent | original tag、parent routing、payload | frozen deferred reaction record | synchronous multicast delegate |
| GameplayCue | OnActive/WhileActive/Executed/Removed、inhibit/reactivate cycle、context payload | CueLifecycleKey + application/period Executed identity + Boundary outbox | Actor notify/RPC implementation |
| TargetData/Context | heterogeneous target payload；Instigator/Causer/SourceObject/hit/origin | generated unmanaged variants + immutable context | UObject polymorphism |
| Prediction | 不属于 v1 | 无 schema | 全部 prediction/replication machinery |

## Handle 与并发复核

### 正式契约

```text
TypedHandle =
    (SimulationEpoch,
     HandleKind,
     OwnerAscInstanceId,
     SlotIndex,
     Generation)
```

运行时紧凑形态可不在每个值重复 Epoch/Kind，但跨 owner、跨 phase 和序列化边界必须能够验证它们。

```text
GrantedAbility 1 → 0..N Activation
Activation      1 → 0..N Continuation
Continuation    1 → 0..N Subscription
ASC             N ↔ N ASC through Subscription edges
```

同一 slab backing storage 可以容纳不同 slot kind，但不能把 Activation 与唯一 Continuation 合并成一条记录。外部 subscription 投递必须验证 Activation 与 Continuation 两级 Generation。

## CaptureProjectionContract 复核

Capture contract 必须由三轴组成：

| 轴 | 值 |
|---|---|
| ValueView | Base / Final / Bonus / ToChannel / Contribution / ModList |
| EvaluationBinding | FrozenAtCapture / late SourceTags / late TargetTags / filters / ignore / alternate base |
| Lifetime | OneShot / PeriodicReevaluate / LiveDependency |

### ScalarSnapshot 最终裁决

> ScalarSnapshot 只有在生成器能证明结果于捕获边界唯一确定时合法；Calculation 最终返回 float 不构成证明。Final 若依赖捕获后绑定的 TargetTags、filters、ignore、channel/contribution 或 periodic reevaluation，仍必须使用 Normalized AggregatorSnapshot。

### AggregatorSnapshot 最小语义

- Attribute/source/capture boundary/revision
- BaseValue
- AggregationPolicy/FormulaVersion
- ContributorId、Op、Channel、EffectiveMagnitude、稳定 OrderKey
- Source/Target requirement ids
- 契约需要时的 contributor source/target tag provenance

可以裁掉 transient qualification、dirty/dependency、UE pointer、NetUpdateId、Prediction flag 与未声明视图所需字段。完整字段与生成门禁见 [04](04-EffectCommand-SpecStream-AttributeDeltaSpec.md)。

### Live Capture 最终裁决

- Source Snapshot：Ability 成功 Owner Commit 后才由 `SourceSpecProjection` 建立；Target Snapshot 在每条 application 的 target pre-application 最终线性化点建立。
- 同 ASC：revision 驱动 dependent ActiveEffect 在 target-local stabilization 重算。
- binding 必须区分 CaptureOrdinal/ConsumerNode/ConsumerField，并声明 SourceGonePolicy、cycle guard 与 propagation budget。
- 跨 ASC：source revision T → destination dirty T+1；target 校验 handle 后重算并双向 cleanup，且同 tick dirty 在 PeriodDue 前。
- 未实现跨 ASC 闭环时，Definition bake fail。
- phase 当前值读取命名为 PhaseSample，不得冒充 Live。

## Stable-state Deferred Reaction 复核

### 可直接写入实现/配置文档的措辞

> v1 采用 stable-state deferred reaction。GameplayEvent、OwnedTag 触发 Ability、外部 Continuation 唤醒与跨 ASC Reaction 在发射 tick 不重入；冻结后的事件最早在下一 tick 投递。Payload、EffectContext、TargetData、原始 EventTag 与 tag count transition 固定于发射时，CanActivate 及当前状态检查读取投递 tick 的 reaction 前稳定状态。每经过一条公开 Reaction 边至少增加一 tick。

### 必须 same-tick

- target pre-application capture / requirement / immunity / TargetLifePolicy / stack decision
- source-local CommitPlan/cost/cooldown/activation-owned contribution
- attribute execute/clamp/meta conversion
- modifier/tag/grant contribution 增删
- target-local ongoing/inhibition stabilization
- Ability direct program self-end
- 闭合、完全展开、有限、静态有界且不读取 post-apply fact 的 definition-local DirectEffectProgram

### 默认 next-tick

- GameplayEvent/父 tag 路由触发 Ability
- OwnedTag trigger
- Continuation/Subscription wakeup
- 跨 ASC reaction
- apply/overflow/period/reaction 动态 child application
- 跨 ASC Live dirty（source T → destination T+1）
- 对外 gameplay listener

UE 同步 Event→Ability/Task 链无法自动等价；玩法需要 same-tick 时必须转写为 kernel invariant/DirectEffectProgram，否则属于 v1 明确差异。

## v1 必保 / 明确不做矩阵

| 领域 | v1 必保 | v1 明确不做 |
|---|---|---|
| Ability | Definition→GrantedSpec→Activation；source-local atomic CommitPlan；Ending 屏障；OwnedContribution/Emitted refs 分离；并发 policy | Ability/Activation Entity authority；UGameplayAbility 继承树；普通 self GE 给同 OwnerWave 同步反馈 |
| Grant | CancelImmediately / RemoveWhenAllActivationsEnd / LeaveGranted；SuspendWhileInhibited 独立 | 用 “DoNothing” 模糊 LeaveGranted；LeaveGranted 后 tombstone 或持 dangling effect handle |
| Task | 多 Continuation、同名多实例、五类 WaitSemantic、one-shot/persistent、跨 ASC PendingRegistration/Ack、owner cleanup | UAbilityTask、delegate、coroutine、单 continuation 压缩、Edge/Event 历史追溯 |
| Effect | Definition/Spec/Active 分离；target final linearization；完整 StackTemporalContract；RemoveOnActivationEnd 精确 ledger | 每 Effect 默认 Entity；TargetResolve 预写 result；异质 payload 压 count；普通 GE 随 Activation End 删除 |
| Attribute | Base/Current、ordered contribution、exact removal、Capture/Live、unclamped/effective fact、first Death crossing | 仅存最终 float；managed callback 写权威值；从 clamp 后 0 反推 overkill |
| Capture | 三轴 contract、严格 scalar proof、normalized snapshot、live dependency | arbitrary full-Aggregator runtime API；silent scalar fallback |
| Tag/Event | hierarchical count、original tag、frozen payload | 用 Event 代替 OwnedTag；用最终 tag 状态重建 transition |
| Reaction | stable-state deferred、recipient full order、same-tick invariant/closed bounded DAG | UE 任意同步重入；动态 same-tick recursion；容器遍历决定 recipient 顺序 |
| Cue | 四阶段与 immutable context、CueLifecycleKey/cycle、application/period Executed identity | Cue 反向决定 gameplay；BoundaryEventId 代替 lifecycle identity |
| Context | Owner/Avatar、Instigator/Causer、SourceObject、hit/origin | 合并字段或仅存裸 Entity |
| Identity | Epoch、typed handles、EffectSpec/Application/Contributor/Death/Cue ids、causality | 以 ActiveEffectHandle/BoundaryEventId 代替 Application/Cue lifecycle identity |
| Storage | ASC-local non-compacting slab、free-list、tombstone | compact/swap-back、Ability/ActiveEffect Entity authority |
| Boundary | accept-before-clear、BatchId/InFlightWatermark retry、semantic hash 与 teardown audit 分层 | clear-before-accept；transport/Profiler/presentation 污染 gameplay hash |
| Prediction | 无 | PredictionKey、modes、rollback、ack、predicted modifiers/overlay |
| Replication | 无 | 复制协议、network reconciliation、replicated AbilityTask |

## 配置链发布契约

### AbilityLifecycleContract

必须输出：GrantedBy、ActivationPolicy、CanActivateRequirements、全量可预检/no-fail CommitPlan、CancelPolicy、BlockPolicy、EndPolicy/首次 EndReason、GrantedRemovalPolicy、OwnedContributionRanges、EmittedApplication audit shape、FailureReason、TraceShape、Continuation/Subscription capability。普通 self GE 与 AscOwnerCommandWave barrier、RemoveOnActivationEnd ledger requirement 必须可静态验证。

### GameplayEffectSpecShape

必须输出：DefinitionId/version、source/target binding、TargetLifePolicy、level、SetByCaller、EffectContext、CaptureProjectionContract/phase、duration/period、完整 StackTemporalContract、requirements/immunity、typed outcome/blocker provenance、granted state、Cue lifecycle/execution policy。

### AbilityTaskSemanticMapping

任何 wait delay/event/tag/attribute/handle/target/presentation completion 语义必须显式映射 `WaitSemantic=Level/Edge/Event/HandleLifecycle/Timer`、ContinuationKind、Subscription query、one-shot/persistent、ObservedASC、recipient ordinal、Target lane 或 typed gameplay fact。跨 ASC 必须声明 PendingRegistration/Ack/cancel generation；没有承载时属于 Runtime Semantic Extension，不能因为 row 可保存字段而归为配置型。

### ASCBindingContract

必须输出：AscOwnerKey、Owner/Avatar policy、authoring attribute group、按 stable id 排序的 `AttributeInitValue[]` / initial Tag id range、`AttributeLayout` / `TagCatalog` 初始化投影、default grants、initial effects、grant source、install/revoke/cleanup policy，以及 canonical `SpawnInitializationProgram`。authoring group 不生成运行时 AttributeSet component；Spawn shadow 只能写同一 `AttributeValueSlot[]` / `TagCountSlot[]`。v1 initial effect 必须 self-target、生成期闭合且静态有界；跨 ASC/空间目标、Live capture、动态 reaction/结构 child 必须 publish fail。SpawnFinalize 以下一 gameplay Tick 为 ReadyTick，在 shadow 复用正式 Effect pure evaluator并全量准入，成功后才 no-fail 整批发布。

### SourceGenerator 门禁

生成器只产生 immutable catalog、typed handle/record、Capture accessor、pure evaluator 与 validation；`SemanticPhaseOrdinal/WorkClassOrdinal` 必须来自版本化 schema/catalog 并进入 content hash，不能从 Job lane 推导。生成器必须拒绝 runtime lifecycle System、managed delegate/task、hidden query/ECB、arbitrary capture access、Prediction schema、未声明跨 ASC Live、不完整 StackTemporalContract、无法 no-fail 的 CommitPlan、普通 self GE 同 OwnerWave反馈依赖、超出 v1 bootstrap 闭世界的 initial effect，以及缺少逐 application ledger 的异质 stack/RemoveOnActivationEnd。

## 最终语义验收矩阵

以下均为 v1 P0；任一失败都表示目标态语义未闭合。

| 领域 | 场景 | 验收结果 |
|---|---|---|
| Handle | slot 回收后旧 handle 再访问 | Generation 不匹配，确定性拒绝，不命中新对象 |
| Handle | 不同 HandleKind 三元组相同 | 强类型/Kind 拒绝交叉使用 |
| Handle | world/ASC identity 重用 | Epoch 阻断旧 command |
| Lifecycle | tombstone 尚有 child/queue/dependency | 不进入 free-list |
| Lifecycle | queued command 晚于对象结束 | 使用冻结 payload 或 stale no-op，不读复用 slot |
| Ability | 一个 GrantedSpec 并发激活 N 次 | 每次有独立 Activation/context/children/cleanup |
| Ability | PerActor 禁止重入/retrigger | 按 policy 拒绝或先结束旧 Activation |
| Ability | CanActivate 成功、延迟 Commit 失败 | 不消费 cost/cooldown；Activation 按 program 决定后续 |
| Ability | 两个 Activation 同 OwnerWave 竞争不足资源 | canonical 先者 no-fail Commit；后者预检失败；无部分 cost/cooldown/owned contribution |
| Ability | Commit 调用两次 | 第二次显式拒绝，不重复消费 |
| Ability | Commit 后同 tick Cancel/End/source death | cost、cooldown 与 committed remote work 保留；只清 owned contributions/Continuation，target 仍按自身 policy 决定 |
| Ability | Cancel/End 后 Commit | Ending 屏障返回 OwnerEnding；EndReason/WasCancelled 首次冻结且 cleanup 一次 |
| Ability | A 普通 self GE、B 同 OwnerWave CanActivate 依赖其结果 | B 不可见该 GE；Definition 依赖声明 bake fail 或改 activation-owned invariant |
| Ability | Activation End，普通已应用 GE 仍存活 | EmittedApplicationRefs 只审计；GE 按自身 duration/remove policy 存活 |
| Ability | RemoveOnActivationEnd 未 apply/已 apply/shared stack | cancel-before-apply / 精确 Application+Contributor 撤销 / 无 ledger bake fail |
| Continuation | 同 Activation 并行多个实例 | 独立 wake/complete/cancel |
| Continuation | 同 InstanceName 多实例 | 不覆盖，批量操作作用于全部匹配项 |
| Wait | Level 注册时已满足 | T 产生 completion、不留 Subscription；Continuation 统一 T+1 resume |
| Wait | Edge/Event 注册前已发生 | 不追溯；只观察 sample+register 线性化点之后的 transition/event |
| Wait | Handle 已失效 / Timer 到期 | HandleLifecycle 立即完成；Timer 只在声明 DueTick 完成，均 T+1 resume |
| Subscription | one-shot 同 tick 多次 match | observed writer 首次 match 即 Consumed，只完成一次 |
| Subscription | persistent 多次 match | 每次携带单调 WakeOrdinal；不覆盖、不丢醒 |
| Subscription | 跨 ASC 注册同时发生 event/cancel | PendingRegistration→Ack/Completion 有稳定序；cancel Generation 拒绝晚到 |
| Recipient | exact/parent、多 Definition/Subscription 同时匹配 | 按 KindPriority/Depth/StableId/DefinitionOrdinal或RegistrationSequence/WakeOrdinal 全序 |
| Grant | CancelImmediately | 阻止新 Activation、取消全部 child，cleanup 后 tombstone |
| Grant | RemoveWhenAllActivationsEnd | 阻止新 Activation，现有 child 结束后 tombstone |
| Grant | LeaveGranted | slot 保持 Live 且可激活；detach ownership、冻结 provenance、无 dangling effect handle |
| Grant | granting Effect Inhibited | SuspendWhileInhibited 独立生效；不偷换三态 removal policy |
| EffectSpec | 一个 source Spec 应用多个 Target | target capture/requirement/stack 互不污染 |
| EffectSpec | Continuation 跨 tick 保存 Spec | 持久不可变 owner；无 frame arena 悬空引用 |
| EffectSpec | Ability Commit 失败 | SpecDraft 可丢弃；不创建权威 EffectSpecId/SourceSpecProjection，Source Snapshot 无残留 |
| Target | TargetResolve precheck 后目标状态改变 | target writer 在最终线性化点重新 capture/evaluate，以 typed outcome 为准 |
| Target | 前序 application grant immunity，后序命中 | 后序 RejectedImmunity，并冻结 blocker Definition/handle/contributor/rule |
| Target | requirement/immunity/stack-policy 拒绝 | 无 slot/stack/dependency/Attribute/Cue/contributor 部分 mutation |
| Target | business rejection after source Commit | 不回滚 source cost/cooldown；ApplicationId/outcome 可审计 |
| Admission | infrastructure capacity 不足 | Owner/Target Job 仍在同一 DAG 中预排但读取 AdmissionResult 后 no-op；整 Tick gameplay 零写，仅 FaultLatch 锁存 InfraAdmissionFault |
| Derived | projectile 晚于 Activation 命中 | frozen Context/capture 可完整应用，不要求 Activation 保活 |
| Effect | non-predicted Instant | 不创建 ActiveEffectSlot；Base mutation/Executed Cue 一次 |
| Effect | Duration non-period modifier | Current 受 contributor 影响；Base 不变；remove 后恢复 |
| Effect | Periodic due | DueTick 当前 tick claim；DueClaim/PeriodExecutionOrdinal 保证只执行一次 |
| Effect | expiry/final-period/period self-remove 同 tick | 依合同执行；前后复检 Removing；一个 execution、一次 terminal cleanup |
| Effect | 动态 period/overflow/apply child | 默认 T+1；不借 callback 偷渡 same-tick |
| Effect | stack merge | Handle 保持、ApplicationId 独立，key/payload/refresh/reset/execute-on-apply policy 正确 |
| Effect | 同质 poison stack | frozen payload × StackCount；period magnitude、duration/expiry 按显式合同 |
| Effect | 异质 poison/charge payload 或 expiry | 使用逐 application ledger；count-only Definition bake fail |
| Effect | at-limit/overflow/deny/clear | typed outcome 与 child/clear 顺序固定；业务 deny 不产生部分 mutation |
| Effect | inhibit/reactivate | slot/time/stack/context 保留；side effects 撤销/恢复；时间按 explicit policy |
| Effect | remove | PendingRemove→Tombstone；精确 cleanup 后才 free |
| Grant-by-GE | Effect grant Ability | provenance 可追踪，remove policy 只清理对应 grant |
| Attribute | mutation + clamp | Fact 同时保存 Requested/PreClamp/Unclamped/PostClamp/EffectiveDelta 与 causality |
| Death | HP 首次 old>0 且 unclamped<=0 | 只生成一个 DeathTransitionId，冻结 killer 与 crossing overkill；致死 application 完成全部节点 |
| Death | 首次 crossing 后后续 AliveOnly application | RejectedTargetLife；damage/overkill/assist 为 0，不产生第二次 Death |
| Capture | Source Snapshot 后 source 改变 | 结果保持捕获边界值 |
| Capture | Target Snapshot | 在每条 target application 的 pre-application 最终线性化点捕获 |
| Capture | Base-only | ScalarSnapshot 与捕获时 Base 一致 |
| Capture | Final + late TargetTags | 必须 AggregatorSnapshot；强制 scalar bake fail |
| Capture | Bonus/ToChannel | Base/channel/order 结果一致 |
| Capture | Override | 返回 OrderKey 最先的 qualified Override |
| Capture | Applied filters | 使用冻结 contributor provenance |
| Capture | Contribution/Ignore | 按 ContributorId 精确选择/排除 |
| Capture | 未声明 accessor | generate/bake fail |
| Capture | periodic 重捕 TargetTags | execute 使用声明 phase 的最新 TargetTags |
| Live | 同 ASC dependency | revision 变化后 dependent magnitude 更新 |
| Live | 同 attribute 多 consumer fields | CaptureOrdinal/ConsumerNode/Field 分别 dirty，不因错误去重漏更新 |
| Live | 跨 ASC dependency | source T → destination dirty T+1，dirty 先于 PeriodDue；闭环缺失则 bake fail |
| Live | source gone / dependency cycle / budget | 显式 policy 或 deterministic failure；不 fallback self/0/target，不提交半重算 |
| Tag | 叶 tag 增减 | 父 tag count 与 0 边界准确 |
| Event | child tag 路由 parent | 原始 tag/payload 保留；T+1 固定顺序投递 |
| OwnedTag trigger | 0→1 / 1→0 | transition 冻结；next-tick 差异符合正式契约 |
| Reaction | A→B→C public chain | 每条边至少一 tick，无意外同 tick 重入 |
| DirectEffect | 闭合、有限、静态有界且不读 post-apply fact 的 chain | same-tick 固定拓扑执行；非闭合/动态/指数展开 bake fail |
| DirectEffect | 动态边/环 | publish/bake fail |
| Kernel | Commit 与 target requirement/immunity/stack/clamp | 分属 source/target 最终线性化点，均在当前权威事务内闭合 |
| Stabilize | ongoing/inhibition 收敛 | 只发布最终稳定 transition |
| Stabilize | 无固定点 | state hash/iteration cap 失败，不提交半状态 |
| Cue | Active→Inhibited→Active→Remove | cycle0 Removed；reactivate cycle1；remove cycle1 Removed；每 cycle 精确配对 |
| Cue | Inhibited 后直接 Remove | inhibition 已发 Removed，remove 不重复；stack change 不新 cycle |
| Cue | Instant/Periodic execute | 每次权威 execute 产生一次 Executed，分别使用 Application/PeriodExecution identity |
| Cue | stabilization 中间态 | 不泄漏试探 Cue |
| Provenance | effect/cue/reaction 审计 | 可还原 Owner/Avatar、Instigator/Causer、Definition、Activation/Application |
| Cross-ASC | 多 source 同 tick 写同 target | target ASC 单 writer，输入按稳定总序归并 |
| Determinism | 同输入/definition hash 重跑 | slot lifecycle、最终状态、Fact/Cue/Reaction 顺序一致 |
| Boundary | managed staging 接管失败/retry | Accepted 前不清 source；同 BatchId/InFlightWatermark retry，不重不漏 |
| Boundary | Accepted dead shell 且无下一 tick | final drain 后 teardown 直接清；业务 Result/facts 不受 cleanup 时机影响 |
| SemanticHash | batch 切分/raw Entity/wallclock/Profiler/Journaling/表现变化 | gameplay hash 不变；stable identity/content/tick/outcome/fact/cue order 进入 hash |
| Nonprediction | Definition 请求 Prediction API | generate/bake fail |
| Nonprediction | runtime schema inspection | 不存在 key/mode/predicted modifier/rollback/ack/overlay 字段 |

## 固定微场景集

以下场景的 tick、稳定序、数值与结果都是规范的一部分；实现不得用 Job/callback/容器遍历顺序选择另一结果。

### M01：AscOwnerCommandWave Commit 竞争

ASC 初始 Mana=10；Activation A 的 OwnerSequence=10、cost=7，Activation B 的 OwnerSequence=20、cost=6，二者同 tick CanActivate 均先通过。CommitPlan 按 sequence 线性化：A 原子提交后 Mana=3 且 cooldown A 存在；B 重新预检失败，不产生 cooldown B、owned contribution、EffectSpec 或半 Committed 状态。

### M02：Commit / Cancel 顺序

- `Commit(A) sequence=10 → Cancel(A) sequence=20`：A 的 cost/cooldown 与已 committed remote work 保留；EndReason=Cancelled 只冻结一次，owned contributions/Continuation 清理。
- `Cancel(B) sequence=10 → Commit(B) sequence=20`：Commit 返回 OwnerEnding；无 cost/cooldown/spec/application。

### M03：普通 self GE 的两 Wave barrier

A Commit 后发射普通 self GE `GrantTag.Ready`；B 位于同一 AscOwnerCommandWave，CanActivate 要求 Ready 且初始 count=0。B 不读取该普通 GE，低层 phase 测试结果为拒绝；AscTargetStateWave 后 Ready 才可见。声明依赖这种同-wave反馈的 Definition 在 bake fail；改为 activation-owned CommitPlan contribution 后才允许同步可见。

### M04：target 最终 requirement / immunity

同一 target 的 Application X order=10 成功创建 immunity blocker，Application Y order=20 命中其 immunity query。即使 Y 的 TargetResolve precheck 早于 X，Y 仍在最终线性化点读取 X 的已提交状态并返回 `RejectedImmunity`，记录 X 的 Definition/ActiveEffectHandle/ContributorId/matched rule；Y 不创建 capture dependency、damage、Cue 或半 slot。

### M05：Level 与 Edge wait

T=10 注册时目标已有 Tag.Stunned：Level wait 在 T=10 产生 completion、不创建 Subscription，Continuation 在 T=11 resume。另一个 Edge wait 不追溯 T=9 的 0→1；直到 T=11 新 transition 才完成，并在 T=12 resume。observed writer 同 tick 出现两个 match 时，one-shot 只消费首个；persistent 产生 WakeOrdinal 1、2。

### M06：跨 ASC registration / cancel generation

owner ASC 建立 `PendingRegistration(generation=7)`；observed ASC 按稳定序 sample+register 后返回 Ack/Completion。若 owner Activation 先结束并把 subscription generation 推进到 8，任何携带 generation=7 的晚到 Ack/Completion 均为 stale no-op，不得唤醒复用 slot。

### M07：毒 stack payload 与 expiry

- 同质毒测试 Definition H：StackKey=`(Definition, TargetASC, SourceASC)`、Source Snapshot period magnitude=2、HomogeneousCount、limit=3、DurationRefresh=RefreshOnSuccessfulApplication、PeriodReset=NeverReset、ExecuteOnApply=false、AtLimit=Deny、Expiration=ClearEntireStack、FinalPeriod=Skip、Inhibit=SkipWithoutReset、StackCue=UpdateParameterOnly。T=0 连续成功叠到 count=3，T=2 period magnitude=6；第三次后的 expiry=T=5；T=5 整槽移除且不 final execute。
- 异质毒：两次 application 分别要求 magnitude=2/4、expiry=T+5/T+7。若 Definition 仍声明 HomogeneousCount，bake fail；只有逐 application ledger 才能分别结算与到期。

### M08：final period 与 self-remove

ActiveEffect 在 T=20 同时 period due 与 expiry，FinalPeriodPolicy=ExecuteThenRemove。它只取得一个 DueClaim/PeriodExecutionOrdinal；period body 使自己 PendingRemove 后，执行后复检直接进入一次 terminal cleanup，不再 refresh duration/next due、不再次 remove。动态 child 最早 T=21。

### M09：首次 Death crossing 与 AliveOnly

目标 Health=5。Application K order=10 请求 delta=-8：PreClamp=5、Unclamped=-3、PostClamp=0、EffectiveDelta=-5，创建唯一 DeathTransitionId，killer=K、overkill=3；K 的剩余 execution/fact 节点继续完成。Application L order=20 且 AliveOnly 请求 delta=-2：`RejectedTargetLife`，damage=0、overkill=0、assist=0，不产生第二个 DeathTransitionId。

### M10：Granted removal 三态

同一 granting Effect 分别配置：CancelImmediately 立即取消两个 child 后 tombstone；RemoveWhenAllActivationsEnd 阻止新激活并等待两个 child 自然结束后 tombstone；LeaveGranted 在 granting Effect remove 后仍 Live/可激活，只冻结 provenance 且不保留 dangling ActiveEffectHandle。SuspendWhileInhibited 另测，不改变 removal policy。

### M11：Cue inhibit/reactivate

Effect H cycle=0 首次 Active 发 OnActive+WhileActive；Active→Inhibited 发 Removed(cycle0)；reactivate 令 cycle=1 并发 OnActive+WhileActive；stack+1 不改变 cycle；Active remove 发 Removed(cycle1)。若 cycle0 Inhibited 后直接 remove，则不再发第二个 Removed。period Executed 使用 PeriodExecutionOrdinal，不使用 lifecycle key。

### M12：跨 ASC Live 与 PeriodDue

source attribute revision 在 T=30 从 4 变 7；dependent target 在 T=31 先消费指定 CaptureOrdinal/ConsumerField 的 dirty，再执行同 tick PeriodDue，period 读取 7。source gone 按 Definition 的 SourceGonePolicy；无 policy、静态 cycle 或预算不可证明时 bake fail/typed fault，不 fallback self/0/target。

### M13：RemoveOnActivationEnd 与 shared stack

Activation End 发生在 application 最终线性化前：原 EffectApplicationId cancel-before-apply。发生在独立 ActiveEffect 已落地后：精确撤销该 application/contributor。发生在多个 source 合并的 shared stack 后：存在逐 application ledger 才撤销自己 entry；无 ledger 的 Definition bake fail。普通 emitted GE 在三种情况下都不因 End 自动删除。

### M14：Boundary accept-before-clear 与 semantic hash

source outbox range `(ASC-1, OwnerSequence 1..10)` 冻结为 BatchId B；第一次 managed copy 失败时记录仍在 source。retry 使用同一 B，staging Accepted 后只清 `<=10`，sequence 11 留待下一 batch。把 B 切成不同 transport chunks、改变 raw Entity/wall-clock/Profiler/Journaling/表现加载结果，gameplay semantic hash 不变；stable content/tick/outcome/fact/cue order 任一改变则 hash 改变。

## 非目标明确化

1. 不实现 UE UObject 版 ASC、Ability、AbilityTask、GameplayEffect 继承树。
2. 不建立 Ability/Activation/ActiveEffect Entity authority；结构 Entity 只服务 projectile/aura/zone 等派生对象。
3. 不保留 compact/swap-back slot 路径。
4. 不提供 arbitrary runtime Aggregator access 或 silent scalar downgrade。
5. 不实现 UE call-stack 同步 GameplayEvent/Task reaction parity。
6. 不提供普通 self GE 对同 AscOwnerCommandWave 后续 CanActivate 的同步反馈，也不提供跨 ASC Live/dynamic child 的 same-tick 闭环。
7. 不实现 Prediction、Replication、rollback/reconciliation 或 replicated AbilityTask。
8. 不用 OOP compatibility layer 保留第二份 gameplay 权威。

## 官方文档来源

1. Epic Developer Community / Unreal Engine 5.7 Documentation: [Gameplay Ability System](https://dev.epicgames.com/documentation/unreal-engine/gameplay-ability-system-for-unreal-engine?lang=en-US)
2. Epic Developer Community / Unreal Engine 5.7 Documentation: [Using Gameplay Abilities](https://dev.epicgames.com/documentation/unreal-engine/using-gameplay-abilities-in-unreal-engine?lang=en-US)
3. Epic Developer Community / Unreal Engine 5.7 Documentation: [Gameplay Ability Tasks](https://dev.epicgames.com/documentation/unreal-engine/gameplay-ability-tasks-in-unreal-engine?lang=en-US)
4. Epic Developer Community / Unreal Engine 5.7 Documentation: [Gameplay Effects](https://dev.epicgames.com/documentation/unreal-engine/gameplay-effects-for-the-gameplay-ability-system-in-unreal-engine?lang=en-US)
5. Epic Developer Community / Unreal Engine 5.7 Documentation: [Gameplay Ability System Component and Gameplay Attributes](https://dev.epicgames.com/documentation/unreal-engine/gameplay-ability-system-component-and-gameplay-attributes-in-unreal-engine?lang=en-US)
6. Epic Developer Community / Unreal Engine 5.7 Documentation: [Gameplay Attributes and Attribute Sets](https://dev.epicgames.com/documentation/unreal-engine/gameplay-attributes-and-attribute-sets-for-the-gameplay-ability-system-in-unreal-engine?lang=en-US)
7. Epic Developer Community / Unreal Engine 5.7 Documentation: [Using Gameplay Tags](https://dev.epicgames.com/documentation/unreal-engine/using-gameplay-tags-in-unreal-engine?lang=en-US)

## DOTS 依据与历史定位

1. DOTS 技术约束以 `18-DOTS官方规范复核与性能红线Spec.md`、`../../UnityDOTS官方文档参考/主题/90-规则编号索引.md` 与 `../../UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md` 为依据。
2. 历史 Ability Entity、ActiveEffect Entity、全局 EventBus、可 compact slot 与 OOP Task 方案仅作为来源线索，不覆盖本文件裁决。
3. Business Package 只是 Authoring 聚合，必须能投影出 AbilityLifecycle、GameplayEffectSpec/Capture、Tag taxonomy、Cue parameters 与 ASC binding。
