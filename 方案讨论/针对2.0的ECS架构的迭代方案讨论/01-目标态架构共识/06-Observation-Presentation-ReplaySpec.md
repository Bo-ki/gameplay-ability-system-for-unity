# Observation、Presentation、Replay Spec

## 结论

Core 只产生最终权威事实；Runtime Boundary 只有一个 managed Drain；Cue、UI、Replay、Debugger 和 Headless 从同一个不可变 batch 派生。任何消费者都不能直接查询、清空或确认 ECS outbox，也不能反向驱动 gameplay。

## 出站链

```text
Stable transaction / TerminalResolve reaches final state
  -> BoundaryFactBuffer on unique scoped owner (ASC or Session)
   + IngressTerminalJournal for gate/fault-close RequestTerminalOutcome only
  -> optional OutboxDirty marker
  -> EndFixed structural playback / ASC destroy
  -> GasBoundaryDrainSystem
      -> live ASC outboxes + live Session outbox + persistent ingress-terminal source
      -> ASC/Session cleanup shells
      -> stable sort / freeze BatchId + source watermarks
      -> copy to managed staging
      -> staging Accepted
      -> clear only accepted source ranges
      -> queue dead-shell cleanup removal for next Kernel prepass / standard EndFixed
  -> Immutable BoundaryBatch / retained ring
      ├─ Cue consumer
      ├─ UI/ReadModel projector
      ├─ Replay writer
      ├─ Runtime debugger
      └─ Headless marker/validator
```

使用 cleanup buffer 的目的，是保证 ASC 或 Session 在 EndFixed 销毁后，terminal Death/Remove/Cue/Battle/Session facts 仍可被 Drain。ASC-scope fact 只写所属 ASC，BattleInstance/Session-scope fact 只写 Session；同一事实不得镜像。Cleanup buffer/state 必须在两类 runtime spawn 时显式添加，不能假设 prefab instantiate 会复制 cleanup component/buffer。

## BoundaryFact 契约

```text
BoundaryEventId              (Epoch, PhysicalOwnerKind/Id/Generation, OwnerSequence) 复合交付/去重身份
ScopeKind / ScopeStableId    Asc|BattleInstance|Session identity 与物理 outbox route
FactPlane                    Gameplay|TeardownAudit 结果分层；不决定物理 owner
CueLifecycleKey?             仅 Duration Cue active cycle 身份
ExecutedCueKey?              仅 Instant/普通/Period execute 身份
SimulationTick               Epoch/OwnerSequence 仅存于 BoundaryEventId
FactKind / SemanticId
Source / Target stable identity
Definition / Activation / EffectApplication identity
Owner / Avatar / BindingGeneration
Instigator / EffectCauser / Context / TargetData references or frozen payload
ParentCausalityId
Payload
```

`BoundaryEventId` 只用于 Boundary 交付与幂等去重，不是 gameplay provenance，也不能代替 application、period execution 或 Cue lifecycle identity。consumer 必须比较完整复合键，不得只比较 owner-local sequence。`ScopeKind` 只决定 identity/route，`FactPlane` 只决定 BattleHash 或 teardown audit inclusion。

每个已 Accepted Request 的出站集合还必须携带一个且仅一个 `RequestTerminalOutcome` envelope。envelope 只冻结 `RequestKey/RequestSequence/TerminalKind` 与 `0..N child ApplicationOutcome` 的 identity count + canonical digest/range 引用，不复制 child gameplay fact，也不能以“目前看见的若干 child”推断集合结束。正常 outcome 一旦逻辑产生，即使其 Boundary batch 尚未 Accepted 也必须留在原 outbox/range 重试；fault aggregate 只能覆盖尚无 terminal outcome 的精确 ledger membership。RequestKey、ledger 生命周期及 gate tail 的唯一协议见 [16-02](16-纯ECS内核与边界重划分/16-02-BoundaryCommand与CoreCommandResolveSpec.md)。

```text
CueLifecycleKey =
    (SimulationEpoch,
     ActiveEffectHandle,
     ActiveCycleOrdinal,
     CueDefinitionOrdinal)

ExecutedCueKey =
    Instant/ordinary: (EffectApplicationId, CueDefinitionOrdinal)
    Periodic: (SimulationEpoch, ActiveEffectHandle,
               PeriodExecutionOrdinal, CueDefinitionOrdinal)
```

Duration Effect 首次稳定 Active 产生 OnActive + WhileActive；`Active → Inhibited` 对当前 lifecycle key 产生一次 Removed；reactivate 增加 `ActiveCycleOrdinal` 并建立新 key；Inhibited 状态 remove 不再重复 Removed。stack count 改变本身不建立新 cycle。稳定化内部试探态不出 Cue。

OnActive、WhileActive、Removed 共享 lifecycle key 以表达同一 cycle，但 delivery/dedup 必须使用 `(CueEventKind, CueLifecycleKey)`；不能只按 CueLifecycleKey 去重而吞掉 WhileActive/Removed，也不能用新的 BoundaryEventId 把 retry 伪装成新 Cue。

## Drain ownership

- 每个 World/Session 恰有一个 `GasBoundaryDrainSystem`，Headless 也必须安装。
- Drain 在每次 outer `SimulationSystemGroup` 更新的 FixedStep 子组之后恰运行一次，而不是每个消费者各跑一次；本次即使是 0 FixedStep 也仍执行 Drain。
- Drain 使用三类互斥 query：live ASC（有 `GasAscIdentity`）、live Session（有 `GasSessionIdentity`）、cleanup shell（有 `BoundaryDrainState` 且两种 live identity 均无）。禁止仅用 `WithNone<GasAscIdentity>` 判定 shell，否则 live Session 会被误清理。
- 同一 Drain 还接管 16-02 定义的持久 `IngressTerminalJournal`；它只承载没有进入 Core 的 gate/fault-close Request terminal envelope，正常 Core envelope 仍只写 Session cleanup outbox。ledger terminal CAS 保证同 RequestKey 只有一个 carrier winner。
- `EntityManager.Exists` 对 cleanup shell 仍为 true；registry 必须用 identity/generation 判活。
- Drain 必须执行两阶段 `accept-before-clear`，不能在 managed owner 接管前清空 ECS outbox。
- Drain 复制并排序后才发布 immutable batch，消费者不能持有 ECS buffer 或 managed mutable list。

### 两阶段接管与重试

1. 对本次稳定 source range 冻结 `BatchId + SourceWatermarks[(OutboxOwnerKind, OwnerStableId, OwnerGeneration, OwnerSequence)]`；相同 source range 的 retry 必须得到相同 BatchId。
2. 把该 range 完整复制到 managed staging；复制/排序/容量失败时 source outbox 保持不变。
3. managed staging 返回 Accepted receipt 后才拥有该冻结 range的交付责任；随后可向 Cue/UI/Replay/Debugger 发布同一 immutable batch。
4. 仅在 Accepted receipt 后清除各 source `<= watermark` 的记录；晚于 watermark 的新事实留在 outbox。live source有 tail时回到 Pending，无 tail才进入 Accepted/Idle交接。clear/retry 必须按 BatchId幂等，不能重复发布或越界清除。
5. dead cleanup shell 在 Accepted 后由下一次 Kernel prepass 记录 cleanup-buffer removal，并在标准 EndFixed提交。空 shell必须在 producer completion后获得显式 `NoFactReceipt` 才能进入 Accepted。若 Session不再有下一 tick，teardown直接按 Accepted watermark/no-fact receipt清理；未 Accepted的事实必须先完成 final drain，不能丢弃。

若开始 outer update 时已经存在 InFlight Batch，Drain 必须先重试同一 `BatchId/source range`，不能为了重试运行 Kernel或增加 `SimulationTick`，也不能把后到 tail 混入旧 Batch。0/1/N FixedStep 只改变 transport batch 分区；相同 command/tick 语义输入的 gameplay hash 必须一致。

具体 structural playback 位置由 [03F](03-RuntimeCore管线/03F-StructuralCommit与BoundaryProjectionSpec.md) 所有；本文只拥有“接管成功前不得清源”和 cleanup shell 的交付不变量。

## per-Battle 双切面封印

多个 Battle 可共用 Session outbox 的物理 owner sequence，但 A 的终局不能等待仍在运行的 B。每个 `BattleOutcomeSeal(A)` 必须同时证明：

1. `CoreOutboxCut(A)`：由 `CoreTerminalToken` 定位 A 的 terminal resolve，并冻结 A 在各物理 outbox 中的最终 gameplay membership、最后 owner sequence、count 与 canonical digest；这些 range 已被 managed staging Accepted。
2. `GateRequestCut(A)`：同一 `SessionIngressGate` 在消费该 Core token 时冻结的 per-Battle request cut，以及 terminal token 后、gate close 前 accepted tail 的逐请求 `RejectedBattleTerminal` membership/digest；该集合的 terminal envelopes 已被 Boundary 接管。
3. 与上述 gameplay cut 对齐的 `SnapshotCut(A)`：managed read-model cache 已原子推进到能重建 A 最终状态的 cut。

Session owner 的全局 watermark 只是传输证明，不能替代 per-Battle membership。A 的三个条件满足即可封印，不要求 B terminal、B gate close 或 Session outbox 未来 tail 为空；没有有效 `CoreTerminalToken` 时 Boundary registry 不能自行制造 winner。Core/Gate 接收协议由 16-02 所有，SnapshotCut API 由 [16-05](16-纯ECS内核与边界重划分/16-05-SnapshotIdentityApiHealthSpec.md) 所有，本节只拥有双切面汇合与 `BattleOutcomeSeal`。

## Semantic hash 与审计分层

Gameplay semantic hash 必须包含：

- Session/content/schema/layout/tick-rate hash、SimulationTick 与 schema-hashed SemanticPhaseOrdinal/WorkClassOrdinal/source sequence；
- Battle/Scenario、source/target ASC、slot generation、Definition/content version、Activation/EffectApplication/Contributor/DeathTransition 等 replay-canonical stable identity；
- typed ApplicationOutcome、Attribute/Tag/stack/inhibition/death facts 的规范内容；
- Fact、Reaction recipient 与 Cue 的正式稳定顺序及 lifecycle/execution identity。

Gameplay semantic hash 必须排除：

- `BoundaryEventId`、`BatchId`、source watermark envelope、transport batch 切分与 retry 次数；
- 未归一化的 runtime-only SimulationEpoch/World identity；
- raw Entity、chunk/job/worker/container 顺序；
- wall-clock、frame、Profiler、Journaling、managed allocation/IO timing；
- presentation resource id、加载/播放结果、Debugger/UI/Replay consumer 状态。

Boundary record 与 Handle 仍必须携带 SimulationEpoch 做 stale guard；hash encoder 把它归一到 Session/content identity，并把 Handle 投影为 canonical ASC/slot/generation identity，不能直接 hash 每次运行随机分配的 World epoch。hash 分层固定为：

- `GameplaySemanticHash`：成功 Battle 的 canonical gameplay state/outcome/fact/cue；0/1/N TickBatch 与 transport retry 下必须相同。
- `CommittedPrefixHash + FaultSemanticHash`：Session-fatal run 对已提交 cost/cooldown、Commit/Application provenance、fault point、受影响 Battle evidence 与未完成 child 集合的独立证明；`ValidationStatus=Faulted`，不得把 fault run伪装成正常 `GameplaySemanticHash`。
- `TransportEnvelopeHash`：可包含 `BoundaryEventId/BatchId/watermark/batch partition`，只用于交付完整性；不同 0/1/N 物理分区可以不同，不能参与 winner/replay semantic equality。
- `TeardownAuditHash`：Drain Accepted、cleanup-shell removal、FinalDrain 与资源清理证据；不得进入或反向改写 Battle outcome。

`BattleOutcomeSeal` 冻结对应 gameplay/fault semantic hash 与双 cut；Session teardown 完成、World/Blob/managed resource 已释放并产生 `DisposedReceipt` 后，才生成最终 `ValidationResultSeal`，其物理顺序由 [03F](03-RuntimeCore管线/03F-StructuralCommit与BoundaryProjectionSpec.md) 所有。`ValidationResultSeal` 后不得再新增 gameplay、Boundary 或 teardown fact。

## ReadModel

ReadModel 是按稳定 ID 建立的版本化 immutable snapshot，不是 live ECS facade。它可以包含 ASC 公共属性、owned tags、ability/effect 摘要、Avatar binding 与 `SnapshotCut`，但不得承诺与 Core 同步可写。查询不存在、snapshot 过期或历史 cut 已不可恢复必须返回明确状态。

managed staging 接受一个 batch 时，必须先原子应用 read-model delta 并推进 cache `SnapshotCut`，再把可能因 retention 丢失的 immutable ring cursor 对消费者可见。表现消费者若因 ring overflow 丢失增量，使用 dropped watermark 请求 snapshot reconcile；reconcile 只恢复表现视图，不向 Core 写回。Snapshot 字段/API 的唯一 owner 是 16-05。

### Cue managed 异步所有权

每 Session 恰有一个 Cue lifecycle consumer/ledger；资源 loader、pool 与实例只属于 managed Presentation，不进入 Core。active reconcile snapshot 至少按同一 `SnapshotCut` 完整携带：Epoch、含 owner/generation 的 `ActiveEffectHandle`、`CueDefinitionOrdinal`、`ActiveCycleOrdinal`、`Active/Inhibited/Removing`、stack/规范化参数、Context provenance 与 `AvatarBindingGeneration`。快照中缺席的旧 cycle 按该 cut 关闭；瞬时 `Executed` 不能由 active snapshot 重建，缺失 range 必须显式保留。

异步 load/instantiate callback 在写 Presentation state 前必须同时校验 Session Epoch、完整 lifecycle key、当前 `BindingGeneration`、consumer generation/cancellation token，且该 cycle 仍为 Active；任一不符即释放结果且不播放。`Removed` 或 snapshot reconcile 关闭 cycle 时，先写 tombstone/取消 pending callback，再回收 instance/resource handle，避免迟到 callback 复活旧 Avatar/cycle。每 Session/definition 的 pending load、active instance 与 retained tombstone 都有 ScaleProfile 上界，超限只产生 typed presentation diagnostic/backpressure，不改变 Core outcome。

Headless 也消费同一 Cue ledger/marker 与 reconcile cut，但 resolver 是确定性 no-op，不加载资源。资源不存在、加载失败、pool 截断只进入 Presentation/Diagnostics；不能反向改变 gameplay、Cue marker 或 semantic hash。

## Replay

Replay 至少记录 Session config/content hash、外部 command、SimulationTick、canonical identity、Boundary semantic content 与必要 delivery envelope。若目标是确定性重演，权威输入是 command+definition/session hash，Boundary facts/semantic hash 用于对账；`BatchId/InFlightWatermark` 只支持交付恢复，不进入 gameplay hash。不得把表现资源加载结果作为输入。

## Diagnostics

Debugger 读取 Kernel 输出的结构化 counters/evidence snapshot 与同一 Boundary batch。它可以导出日志、图表和报告，但不能拥有 query、allocator、playback 或 gameplay mutation。Profiler/Journaling 是独立官方证据源，不能由自报 timing 替代。

## Overflow policy

容量、保留 tick 数和字节预算来自 Boundary ScaleProfile，不在框架 Spec 写死。

- Core/Headless/Validation：overflow 显式失败，保留 first-lost/last-lost tick 与 sequence。
- Presentation：允许丢弃明确 range，再进行 snapshot reconcile。
- Replay：根据产品 policy fail/stop-recording，但必须显式标记不完整。
- 永不静默覆盖，永不以消费者慢为理由阻塞或改写 gameplay。

## 禁止方向

- `PresentationOutbox`、`ReplayBuffer`、`DebuggerBuffer` 三套 Core 写入。
- 多个 ECS consumer 竞争 cursor/clear。
- managed staging Accepted 前清 outbox，或 retry 时生成新的 delivery identity。
- Boundary 携带 raw Entity、DynamicBuffer、NativeContainer 或复用槽引用。
- Cue 播放成功/资源存在与否影响 Core。
- 每渲染帧 cleanup 未消费的 fixed-tick 输入或 outbox。

## 验收

- 0..N FixedStep/catch-up 后 facts 不重不漏且保持正式顺序；0 FixedStep 仍 Drain 一次，InFlight retry 不运行 Kernel、不增加 SimulationTick。
- ASC Destroy 当 tick 的终态 fact/Cue 可从 cleanup shell 导出。
- managed staging 未 Accepted 时 source outbox 不清；Accepted 后只清到 watermark，retry 使用相同 BatchId 且不重复发布。
- 无下一 tick 的 shutdown 先完成 final drain，再按 Accepted watermark 直接清 cleanup shell。
- 多消费者读取同一 batch，移除一个消费者不改变 Core/其他消费者结果。
- Headless 无表现资源时仍产生完整 Cue marker 和 replay/diagnostics 证据。
- Cue Active→Inhibited 发送一次 Removed，reactivate 使用新 cycle，Inhibited remove 不重复 Removed，stack 不新 cycle；Executed 使用 application/period identity。
- Cue async callback 在 Epoch/cycle/BindingGeneration/cancellation 任一 stale 时不复活实例；headless、missing resource 与 pending 上界不改变 marker/semantic outcome。
- A/B 共用 Session outbox 时，A 在自己的 CoreOutboxCut + GateRequestCut + SnapshotCut 完整后独立生成 `BattleOutcomeSeal`，不等待 B。
- transport batch 切分、raw Entity、wall-clock、Profiler/Journaling 与表现加载结果变化不改变 `GameplaySemanticHash/CommittedPrefixHash/FaultSemanticHash`；`TransportEnvelopeHash` 可变化。
- `BattleOutcomeSeal`、`TeardownAuditHash`、`DisposedReceipt` 与 `ValidationResultSeal` 分层；最终 seal 后追加任意 fact 必须失败。
- overflow、stale snapshot、dead identity 都给出机器可读 reason。
