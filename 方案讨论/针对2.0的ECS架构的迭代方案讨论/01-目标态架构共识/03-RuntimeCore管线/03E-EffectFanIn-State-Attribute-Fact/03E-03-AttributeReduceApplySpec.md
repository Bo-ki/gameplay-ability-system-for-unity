# Attribute Aggregator 与 Apply Spec

## 结论

每个 ASC 使用 Session 唯一 AttributeLayout 对应的固定长度 `AttributeValueSlot` buffer。Base、Current 与 Aggregator contribution 是权威状态；不存在 per-attribute component 镜像，也不存在每 tick resize。

## 固定布局

```text
AttributeDefinitionId
  -> AttributeLayoutBlob.ResolveIndex()
  -> AttributeValueSlot[index] { Base, Current, Revision }
```

- Session 启动时固定 Layout/content hash。
- ASC spawn 时一次 sizing/初始化，运行中只按 index overwrite。
- Definition/Ability/Effect 在生成期预解析 AttributeIndex。
- 越界、Layout hash 不一致为 fault。
- ECS change version 只能表示整个 buffer 改变；具体 dirty attribute 使用 `AttributeDirtyWord`、revision 或等价位图。

## Aggregator 语义

Aggregator 至少支持稳定定义的 channel、ModifierOp、qualifier、contributor identity 与 order key。推荐规范化 contribution：

```text
ContributorId
ModifierOp
Channel
EffectiveMagnitude
OrderKey = (Channel, Op, EffectApplicationSequence, ModifierOrdinal)
Source/TargetRequirementId
```

Override 返回稳定顺序中的首个 qualified Override；浮点归并也必须使用稳定次序。Effect stack 产生的 magnitude 可在 contribution 中预折叠，但必须能按 ContributorId 精确撤销。

Duration/Infinite modifier 通常改变 Current：移除 contributor 后恢复。Instant/Periodic execute 按定义改变 Base，再由 Aggregator 重算 Current。Pre/execute/Post、clamp 与 meta→real attribute 属于同一 target transaction，不能延期为公开 reaction。

## Capture

Capture 的唯一完整契约见 [04 EffectCommand/Spec](../../04-EffectCommand-SpecStream-AttributeDeltaSpec.md)。本阶段遵守：

- Snapshot 使用冻结 scalar 或规范化 AggregatorSnapshot。
- Scalar 只有在所有 evaluator 输入已冻结且 API 闭合时合法。
- Source Snapshot 只接受成功 Owner Commit 后的 `SourceSpecProjection`；Target Snapshot 在每条 target application 的最终线性化点、requirement/immunity 之前建立。
- Live binding 至少使用 `(CapturedAscInstanceId, AttributeId, ProjectionContractId, CaptureOrdinal, ConsumerNodeId, ConsumerFieldId, LastSeenRevision, DependentActiveEffectHandle, SourceGonePolicy)`；同一 effect 多个字段不能因只按 AttributeId 去重而漏更新。
- 同 ASC Live dirty 纳入当前 target-local stabilization，以 canonical read-your-writes 反复重算至稳定。
- 跨 ASC source revision 在 T 改变时，只产生 destination 在 T+1 消费的有序 dirty work；该 work 在目标语义序上先于 T+1 `PeriodDue`。未实现 generation 校验、双向 cleanup、source-gone policy、cycle guard 与预算时 Definition bake fail。
- “每 phase 读一次当前值”是 PhaseSample，不得冒充 Live Capture。

`SourceGonePolicy` 必须显式为 typed-failure/remove-dependent/freeze-last-value 等已生成策略之一，不能静默回退到 0、当前 target 值或 self。跨 ASC edge 必须携带稳定 CausalityId/edge ordinal；重复 edge、运行时 cycle 或 next-tick ping-pong 超预算时产生 deterministic fault，不提交半重算值。

## AttributeMutationFact 与 Death crossing

每次权威 Attribute mutation 必须产生可对账的 `AttributeMutationFact`；clamp 后只保留最终值不足以解释伤害、overkill、threshold 或 replay：

```text
AttributeMutationFact =
    AttributeId
    + RequestedDelta
    + PreClampBase / PreClampCurrent
    + UnclampedResult
    + PostClampBase / PostClampCurrent
    + EffectiveDelta
    + EffectApplicationId
    + ContributorId?
    + CausalityId
    + DeathTransitionId?
```

- `RequestedDelta` 是 execution/modifier 请求写入的语义量；`EffectiveDelta` 由对应 pre-clamp 权威值与 post-clamp 权威值计算，两者不能互相替代。
- `UnclampedResult` 是本次 Health domain mutation 在 clamp 前的结果，用于 crossing 与 overkill；不得从 `PostClamp=0` 反推。
- Death 只在权威 Health 的首个 `old > 0 && UnclampedResult <= 0` crossing 创建一次 `DeathTransitionId`，并冻结 killer、致死 EffectApplicationId/ContributorId/CausalityId 与 `Overkill = max(0, -UnclampedResult)`。
- crossing 立即把 target lifecycle 写入 canonical Dead，使同一 target application 序列中后续 `AliveOnly` application 以 typed outcome 拒绝；这些拒绝的 applied damage、overkill 与 assist contribution 必须为 0。
- 已经越过最终 application 线性化点的致死 application 必须完成自身全部 modifier、execution、clamp、meta conversion 与 fact 节点；Death 不是在节点中途抛出的可重入 callback。其后续节点不得改写首次冻结的 killer/overkill，也不得创建第二个 DeathTransitionId。
- `AllowTerminal`、ReviveOnly 或 CorpseTargeting 是显式 `TargetLifePolicy`；不得通过把死亡状态延迟到下一 tick 来模拟。

## Job 与所有权

`TargetOwnedApplyJob` 只写当前 ASC 的 Attribute buffer、aggregator slab、dirty word 和 final fact scratch。跨 ASC source snapshot 从明确 phase read snapshot 读取，不直接在目标 Job 随机访问/写入活跃源数据。

具体 IJobChunk/IJobEntity 选型和 IBC 由 ScaleProfile 决定；逻辑长度与单一 owner 不变。生成报告必须估算 `ASCCount × AttributeCount × slot size`、external buffer allocation 与 dirty scan 成本。

## 验收

- Base/Current、Add/Multiply/Divide/Override/channel/qualifier 的固定向量测试。
- contributor 移除后数值恢复；顺序与 battle hash 重跑一致。
- Source Snapshot/Target pre-application/同 ASC Live/跨 ASC T+1 dirty、late tags/filter/ignore 的组合测试。
- 跨 ASC dirty 在同一目标的 PeriodDue 前可见；source gone/cycle/预算超限均走显式策略或 fault。
- AttributeMutationFact 可同时还原 requested、unclamped、clamped 与 effective delta，不用表现层反推。
- 同 tick 多次致死输入只产生一个 DeathTransitionId；致死 application 完整结束，后续 AliveOnly application typed reject 且 damage/overkill/assist 为 0。
- dirty attribute 只重算必要 index，且不依赖 buffer 级 change filter 猜测 slot。
- dense 内存不达标时另立 ADR 整体替换 backend，不保留双权威。
