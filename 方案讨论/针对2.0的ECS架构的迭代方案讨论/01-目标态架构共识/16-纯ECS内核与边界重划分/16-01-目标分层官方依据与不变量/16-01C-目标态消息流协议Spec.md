# 目标态消息流协议 Spec

## 入站

```text
Shell Intent
  -> CommandPort + SessionIngressGate validates RequestKey/PayloadHash/capacity
  -> IngressRequestLedger allocates RequestSequence + AssignedAvailableTick
  -> BoundaryIngressJournal stores the full accepted record
  -> pre-Fixed GasCommandIngressSystem freezes a transfer cut
  -> BoundaryCommandInbox { RequestKey, PayloadHash, RequestSequence, ProducerSourceSequence,
                             BattleInstanceHandle, SourceAscHandle?, BoundaryTargetRef,
                            AssignedAvailableTick, InlineTaggedPayload }
  -> Kernel seals eligible range exactly once
```

`RequestKey=(SessionEpoch, ProducerId, ProducerGeneration, ProducerLocalRequestSequence)`；业务排序序号同样带 Producer identity。CommandPort 返回 transport receipt，其中冻结 `RequestSequence/AssignedAvailableTick`，不返回同步 gameplay 结果。每个 Accepted Request 最终恰有一个 `RequestTerminalOutcome`；完整 ledger 状态、retire/GC 与同 Gate close 由 [16-02](../16-02-BoundaryCommand与CoreCommandResolveSpec.md) 唯一维护。

## Core 内部

```text
sealed command + due work
  -> Gather/TickStartSnapshot + PlanExpandScratchProvision/token
  -> OwnerPlanBuild shadow records / bounded target expansion within envelope
  -> WholeTickInfraAdmission
  -> admitted owner-local CommitPlan mutation
  -> canonical target transaction ranges
  -> stable final facts
```

所有跨 Job 引用必须是 tick-local range/index 或强类型 generational handle。跨 tick work 必须复制不可变 payload，不能引用 WorldUpdateAllocator 或待复用槽。

## 出站

```text
scoped BoundaryFactBuffer (ASC or Session cleanup owner)
  + RequestTerminalOutcome / Battle ingress closure receipt
  -> one Drain freezes BatchId + per-owner InFlightWatermark
  -> copy/sort -> managed staging Accepted
  -> clear only <=InFlightWatermark; retain late tail
  -> Immutable BoundaryBatch
  -> ReadModel cache advances to SnapshotCut before lossy consumer publication
  -> N read-only consumers
```

单 Battle 的结果封印不是 Session outbox 的裸 watermark：它同时等待 Core terminal outbox cut 与同一 `SessionIngressGate` 冻结的 request cut。双切面与 Snapshot/Cue 消费由 [06](../../06-Observation-Presentation-ReplaySpec.md) 唯一维护。

公开协议只包含稳定 ID、强类型 handle 的序列化形态、definition/application/causality、tick/sequence 和冻结 payload。每条 ingress 都显式携带 `BattleInstanceHandle`；有 source 时必须与其 `AscBattleMembership` 一致，无 source 的 Session/Battle command仍能独立路由。目标使用generated `BoundaryTargetRef` tagged union：完整 `TargetAscHandle`，或带Battle-scope与明确`ResolveAtConsume`策略的业务selector/Definition rule；禁止裸TargetStableId隐式猜测incarnation。raw Entity、裸payload index、NativeContainer、pointer、DynamicBuffer accessor均非法。

## Stale 与幂等

- Epoch 不匹配：拒绝。
- ASC/slot Generation 不匹配：stale reject/no-op，按 command contract 记录 reason。
- 相同 `RequestKey + PayloadHash` 重送：复用原 `RequestSequence/AssignedAvailableTick`，不重复 commit/apply。
- 相同 RequestKey 但 PayloadHash 不同：同步 `IdempotencyConflict`。
- Producer sequence 早于已淘汰 history：同步 `IdempotencyHistoryExpired`，不得当作新请求执行。
- 相同复合 `BoundaryEventId=(Epoch, PhysicalOwnerKind/Id/Generation, OwnerSequence)`：consumer 可去重；禁止以跨 owner 可碰撞的裸 `ulong` 作为全局交付键。Cue lifecycle 使用包含 ActiveCycleOrdinal 的 CueLifecycleKey。
