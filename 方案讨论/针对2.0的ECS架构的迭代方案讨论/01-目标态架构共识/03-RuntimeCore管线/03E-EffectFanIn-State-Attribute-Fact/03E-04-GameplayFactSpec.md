# GameplayFact 与 Deferred Reaction Spec

## 结论

GameplayFact 分为 Core 内部权威反应记录和 Boundary 最终观察事实。Core fact 只由稳定后的 target transaction 产生；公开 reaction 最早下一 tick；Boundary consumer 永不直接读取或清理 Core reaction carrier。

## 两类事实

| 类型 | owner | 生命周期 | 消费者 |
|---|---|---|---|
| `CoreReactionRecord` | GasTickKernel | 当前 tick route 后转成持久 pending work | 下一 tick Ability/Effect/Continuation ingress |
| `BoundaryFactBuffer` | scoped cleanup outbox（ASC fact → ASC；Battle/Session fact → Session）→ single Drain | 持续到 drain/overflow policy 完成 | Cue、UI、Replay、Debugger、Headless |

两者可以由同一个稳定 transition 派生，但不是同一个可竞争消费的 buffer。

## Stable-state deferred reaction

正式时序：GameplayEvent、OwnedTag trigger、Attribute/Tag reaction、外部 Continuation wake 和跨 ASC reaction 在 emit tick 不重入。它们冻结 payload 并设置 `DeliverTick >= EmitTick + 1`。CanActivate 与其他当前状态检查读取投递前的下一 tick 稳定状态。

每经过一条公开 reaction 边至少增加一 tick：

```text
A emits event at T
  -> B may activate at T+1
  -> B emits event at T+1
  -> C may activate at T+2
```

这是 v1 相对 UE GAS 同步 call-stack 的明确语义差异。不得通过“有限 reaction pass”重新引入同 tick re-entry。

## 必须冻结的 payload

- 原始 EventTag 及 exact/parent routing 所需信息。
- Instigator、Target、EffectContext、TargetData、Magnitude。
- SimulationEpoch、BattleInstanceId、相关 ScenarioUnitId、Source/Target ASC、Owner/Avatar binding generation。
- Definition、Activation、EffectApplication、Contributor identity。
- Tag old/new exact/inclusive count 与 transition sequence。
- `EmitTick、SemanticPhaseOrdinal、WorkClassOrdinal、SourceSequence、ParentCausalityId`。
- wait completion 需要的 `ContinuationHandle、SubscriptionHandle、WaitSemantic、WakeOrdinal`。
- application rejection 需要的 typed outcome 与 requirement/immunity blocker provenance。

queued work 不得在 T+1 从已回收槽、raw Entity 或当前 tag 状态重建原 payload。

## 投递全序

```text
(SimulationEpoch,
 DeliverTick,
 TargetAscId,
 SemanticPhaseOrdinal,
 WorkClassOrdinal,
 EmitTick,
 SourceAscId,
 SourceSequence,
 FactKind,
 SemanticId,
 RecipientKindPriority,
 MatchedTagDepth,
 RecipientStableId,
 DefinitionOrdinal or RegistrationSequence,
 WakeOrdinal)
```

`SemanticPhaseOrdinal` 与 `WorkClassOrdinal` 是由发布 schema/content 生成并进入 hash 的业务序，不是 SystemGroup、Job 名称或调度 pass enum。前半段确定事件/事实全序，后半段按 `RecipientKindPriority asc → MatchedTagDepth desc → RecipientStableId asc → DefinitionOrdinal/RegistrationSequence asc → WakeOrdinal asc` 确定同一事实的 recipient 全序。`RecipientKindPriority` 区分 trigger/Continuation/observer 等类别，`MatchedTagDepth` 使用 Catalog depth 并令 exact/更深 tag 先于 parent，`RecipientStableId` 定位接收对象，Definition recipient 使用 `DefinitionOrdinal`，动态 Subscription 使用 `RegistrationSequence`，persistent wake 使用单调 `WakeOrdinal`。所有可并列 recipient 必须在生成期或注册时取得稳定 ordinal；Job 完成顺序不能影响激活或 Boundary 顺序。

## Wait completion fact

`WaitSemantic = Level / Edge / Event / HandleLifecycle / Timer`，其 sample/register 线性化与生命周期由 [01B](../../01B-GAS业务语义链路概念设计Spec.md) 所有。本阶段只发布 observed ASC writer 已确定的 completion：

- Level 注册时已满足可在 T 产生 completion，且不留下 Subscription；Continuation 一律最早 T+1 resume。
- Edge/Event 不读取注册前历史；one-shot 在 observed writer 首次 match 时即以 `Consumed` fact 冻结，后续相同 tick match 不得重复完成。
- persistent 每次 completion 携带新的 WakeOrdinal；接收顺序不能用复用 Subscription slot 推导。
- 跨 ASC `PendingRegistration → Ack/Completion` 必须携带 owner/continuation/subscription Generation；cancel generation 使晚到 Ack/Completion 成为 stale no-op。

公开 wait completion 仍是 deferred reaction。即使 completion 在注册 tick T 已确定，也不能在 observed writer 当前调用链直接恢复 Ability program。

## 同 tick 不变量

Requirement/immunity/stack/commit/clamp/ongoing/inhibition 属于权威 transaction，不是 GameplayFact reaction。生命值归零后必须在本 tick 阻止尚未线性化的后续工作或标记取消时，应由 Attribute domain invariant 直接写 lifecycle mutation；随后再输出 Death/Threshold fact 给下一 tick 业务 reaction 和 Boundary。该 mutation 不逆向撤回本 tick AscOwnerCommandWave 已 Commit 的 cost/cooldown/remote work；后续 target work 依 TargetLifePolicy 决定。

### ApplicationOutcome

每条 target application 必须冻结一个 typed outcome；至少区分成功 Instant、创建 ActiveEffect、stack merge、overflow 与 requirement/immunity/target-life/stack-policy/stale-binding rejection。最终 outcome 只能在目标 ASC application 线性化点产生；TargetResolve precheck 不是结果。基础设施容量不足在此前以 WholeTick `InfraAdmissionFault` 结束，不产生一组伪 application rejection。

`RejectedImmunity` 必须携带 blocker Definition、ActiveEffectHandle/ContributorId、matched requirement/tag；`RejectedTargetLife` 必须保留 EffectApplicationId 与原始 provenance，但 damage/overkill/assist 均为 0。Attribute/Death 的数值事实形状由 [03E-03](03E-03-AttributeReduceApplySpec.md) 所有。

## Boundary Fact

每条 Boundary fact 至少含：

```text
BoundaryEventId
CueLifecycleKey (仅需要 Cue 生命周期时)
SimulationTick + OwnerSequence
Source/Target stable identity
Definition/Semantic id
ParentCausalityId
AvatarBindingGeneration or frozen spatial snapshot when needed
Payload
```

`BoundaryEventId` 只用于交付/去重，不能充当 gameplay、application、period execution 或 Cue 生命周期身份。`CueLifecycleKey`、Executed identity、两阶段 accept-before-clear 与 semantic hash 由 [06](../../06-Observation-Presentation-ReplaySpec.md) 所有。Presentation、Replay、Debugger 只读 Drain 发布的 immutable batch。

## 失败与压力

- Core pending work capacity 超限：确定性 validation/runtime fault。
- Headless/CI outbox overflow：测试失败并输出丢失范围前的证据。
- Presentation ring overflow：记录明确 tick/sequence range，丢弃后通过 Snapshot reconcile；不得影响 Core。
- stale target/handle：按契约 reject 或 no-op，并记录 reason；绝不命中新槽。

## 验收

- 子 Tag 事件在 T+1 按原始 Tag 路由父 Tag，payload 不变。
- 同 tick 多事件及其多个 recipient 按完整正式全序投递；exact/parent match、Definition/动态 Subscription 不依赖容器遍历。
- Level 已满足不留 Subscription且 Continuation T+1 resume；Edge/Event 不追溯；one-shot 只完成一次，persistent WakeOrdinal 单调。
- requirement/immunity/target-life/stack-policy 失败均有 typed ApplicationOutcome；immunity blocker provenance 不丢失；InfraAdmissionFault 时整 Tick gameplay 零写。
- 0 次 FixedStep 时 pending command/fact 不丢；N 次 catch-up 后 Drain 获得全部 tick。
- ASC 在 EndFixed 销毁后 terminal facts 仍从 cleanup shell 导出。
- 多消费者获得同一 immutable batch，Core 无 per-consumer cursor。
