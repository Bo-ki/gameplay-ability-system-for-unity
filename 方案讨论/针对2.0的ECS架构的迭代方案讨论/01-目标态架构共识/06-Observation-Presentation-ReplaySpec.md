# Observation、Presentation、Replay Spec

## 结论

Core 只产生最终权威事实；Runtime Boundary 只有一个 managed Drain；Cue、UI、Replay、Debugger 和 Headless 从同一个不可变 batch 派生。任何消费者都不能直接查询、清空或确认 ECS outbox，也不能反向驱动 gameplay。

## 出站链

```text
Stable transaction / TerminalResolve reaches final state
  -> BoundaryFactBuffer on unique scoped owner (ASC or Session)
  -> optional OutboxDirty marker
  -> EndFixed structural playback / ASC destroy
  -> GasBoundaryDrainSystem
      -> live ASC outboxes + live Session outbox
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
- Drain 在完整 FixedStep catch-up batch 后运行，而不是每个消费者各跑一次。
- Drain 使用三类互斥 query：live ASC（有 `GasAscIdentity`）、live Session（有 `GasSessionIdentity`）、cleanup shell（有 `BoundaryDrainState` 且两种 live identity 均无）。禁止仅用 `WithNone<GasAscIdentity>` 判定 shell，否则 live Session 会被误清理。
- `EntityManager.Exists` 对 cleanup shell 仍为 true；registry 必须用 identity/generation 判活。
- Drain 必须执行两阶段 `accept-before-clear`，不能在 managed owner 接管前清空 ECS outbox。
- Drain 复制并排序后才发布 immutable batch，消费者不能持有 ECS buffer 或 managed mutable list。

### 两阶段接管与重试

1. 对本次稳定 source range 冻结 `BatchId + SourceWatermarks[(OutboxOwnerKind, OwnerStableId, OwnerGeneration, OwnerSequence)]`；相同 source range 的 retry 必须得到相同 BatchId。
2. 把该 range 完整复制到 managed staging；复制/排序/容量失败时 source outbox 保持不变。
3. managed staging 返回 Accepted receipt 后才拥有该冻结 range的交付责任；随后可向 Cue/UI/Replay/Debugger 发布同一 immutable batch。
4. 仅在 Accepted receipt 后清除各 source `<= watermark` 的记录；晚于 watermark 的新事实留在 outbox。live source有 tail时回到 Pending，无 tail才进入 Accepted/Idle交接。clear/retry 必须按 BatchId幂等，不能重复发布或越界清除。
5. dead cleanup shell 在 Accepted 后由下一次 Kernel prepass 记录 cleanup-buffer removal，并在标准 EndFixed提交。空 shell必须在 producer completion后获得显式 `NoFactReceipt` 才能进入 Accepted。若 Session不再有下一 tick，teardown直接按 Accepted watermark/no-fact receipt清理；未 Accepted的事实必须先完成 final drain，不能丢弃。

具体 structural playback 位置由 [03F](03-RuntimeCore管线/03F-StructuralCommit与BoundaryProjectionSpec.md) 所有；本文只拥有“接管成功前不得清源”和 cleanup shell 的交付不变量。

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

Boundary record 与 Handle 仍必须携带 SimulationEpoch 做 stale guard；hash encoder 把它归一到 Session/content identity，并把 Handle 投影为 canonical ASC/slot/generation identity，不能直接 hash 每次运行随机分配的 World epoch。业务结果层只描述 gameplay state/outcome/fact/cue；teardown audit 层另记 Drain Accepted、cleanup-shell removal、final drain、Disposed 与资源清理证据。teardown audit 可以诊断遗漏，但不得进入 gameplay semantic hash、反向改写 Result，或因 batch 切分不同制造业务不一致。

## ReadModel

ReadModel 是按稳定 ID 建立的版本化快照，不是 live ECS facade。它可以包含 ASC 公共属性、owned tags、ability/effect 摘要、Avatar binding 和 last exported tick，但不得承诺与 Core 同步可写。查询不存在或 snapshot 过期必须返回明确状态。

表现消费者若因 ring overflow 丢失增量，通过 ReadModel snapshot reconcile；reconcile 只恢复表现视图，不向 Core 写回。

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

- 0..N FixedStep/catch-up 后 facts 不重不漏且保持正式顺序。
- ASC Destroy 当 tick 的终态 fact/Cue 可从 cleanup shell 导出。
- managed staging 未 Accepted 时 source outbox 不清；Accepted 后只清到 watermark，retry 使用相同 BatchId 且不重复发布。
- 无下一 tick 的 shutdown 先完成 final drain，再按 Accepted watermark 直接清 cleanup shell。
- 多消费者读取同一 batch，移除一个消费者不改变 Core/其他消费者结果。
- Headless 无表现资源时仍产生完整 Cue marker 和 replay/diagnostics 证据。
- Cue Active→Inhibited 发送一次 Removed，reactivate 使用新 cycle，Inhibited remove 不重复 Removed，stack 不新 cycle；Executed 使用 application/period identity。
- transport batch 切分、raw Entity、wall-clock、Profiler/Journaling 与表现加载结果变化不改变 gameplay semantic hash。
- 业务 Result/hash 与 teardown audit 分层；不同 cleanup 时机不会改写业务胜负或事实内容。
- overflow、stale snapshot、dead identity 都给出机器可读 reason。
