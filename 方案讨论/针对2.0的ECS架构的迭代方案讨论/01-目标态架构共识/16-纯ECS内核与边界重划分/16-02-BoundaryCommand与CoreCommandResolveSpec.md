# Boundary Command 与 Core Resolve Spec

## 结论

Boundary 只负责可靠接收和冻结外部意图；`GasTickKernelSystem` 才负责 resolve、CanActivate、commit、target 与 gameplay mutation。CommandPort 不是同步 GAS facade。

## CommandPort

允许的公开操作示例：

```text
RequestActivateAbility(BattleInstanceHandle, AscHandle, GrantedAbilityHandle, EventPayload, RequestKey, ProducerSourceSequence)
RequestCommitActivation(BattleInstanceHandle, AbilityActivationHandle, RequestKey, ProducerSourceSequence)
RequestCancelActivation(BattleInstanceHandle, AbilityActivationHandle, CancelReason, RequestKey, ProducerSourceSequence)
RequestApplyEffect(BattleInstanceHandle, SourceAscHandle?, BoundaryTargetRef, EffectDefinitionId, Payload, RequestKey, ProducerSourceSequence)
RequestRemoveEffect(BattleInstanceHandle, ActiveEffectHandle, RemovalReason, RequestKey, ProducerSourceSequence)
```

每个请求先验证 Session/Epoch、BattleInstance handle、`BoundaryTargetRef` tag/schema、inline tagged payload size、producer window/gap 与 ingress capacity，再复制到 Runtime Boundary 持久 `BoundaryIngressJournal`。source存在时必须验证其 membership 与显式 Battle 一致；source 缺失的 Battle/Session command 仍必须显式带 Battle。此时不能声称 Ability 已激活或 Effect 已应用，也不能把裸 payload index 或含义不明的 TargetStableId 留到跨 tick。

### Request ledger 与 Accepted 线性化（唯一 owner）

本节是 Request 接收、去重、终结与回收协议的唯一 owner；其他文档只能引用本节，不得另定义第二套 dedupe/outstanding 规则。

- `RequestKey = (SessionEpoch, ProducerId, ProducerGeneration, ProducerLocalRequestSequence)`；`RequestId` 若保留，仅是可选 correlation id，绝不是幂等键。producer generation 必须在重连/重建时变化。
- `ProducerSourceSequence = (ProducerId, ProducerGeneration, ProducerLocalSourceSequence)` 是业务 canonical order 的来源域；裸 `SourceSequence` 禁止跨 producer 比较。若调用方不能给出稳定单调序列，CommandPort 必须 typed reject，v1 不用 arrival order 静默补造业务顺序。
- 首次 Accepted 冻结 canonical `PayloadHash`、全局单调 `RequestSequence` 与 `AssignedAvailableTick=SessionIngressGate.NextAssignableTick`。每个 pre-Fixed transfer 在同一 gate 临界区先以当前 tick 冻结 `TransferHighWatermark/membership`，再把 `NextAssignableTick` 推到下一合法 SimulationTick 后释放 gate；因此 accept 与 freeze 谁先取得 gate 就唯一决定本 tick/next tick，不读取 wall-clock。同 `RequestKey + PayloadHash` 重试复用原 record/sequence/tick；同 key 不同 hash 同步返回 `IdempotencyConflict`；key 已越过可证明历史范围返回 `IdempotencyHistoryExpired`，不得当作新请求接收。
- Accepted receipt 至少返回 `RequestKey/RequestSequence/AssignedAvailableTick/PayloadHash`。每个 Accepted 最终恰有一个 `RequestTerminalOutcome` envelope；它记录 terminal kind 及 `0..N` child ApplicationOutcome 的 identity count + canonical hash/range 引用，不能用“看到了若干 child facts”推断集合已经完整。

Runtime Boundary 为每 Session 持有唯一 `SessionIngressGate`，并在同一临界区内拥有 session 状态、per-Battle accept 状态、`NextRequestSequence/AcceptedHighWatermark`、ledger 索引与持久 journal。只有在该 gate 内为 full record、ledger/tombstone 与恰一个 terminal envelope carrier 预留生命周期容量，冻结上述字段并把完整 record 发布到 journal 后才可返回 Accepted；任一容量不足必须在线性化点之前 `InboxFull`，禁止“已返回 Accepted，但只存在临时队列”或 Accepted 后才发现无终结槽。

每条记录严格经过：

```text
AcceptedJournal
  -> TransferFrozen
  -> InboxDurable
  -> Sealed
Sealed --Core resolve---------------------> TerminalProduced(Normal)
Any pre-terminal state --Gate/FaultClose--> TerminalProduced(Aggregate | Rejected* )
TerminalProduced
  -> BoundaryAccepted
  -> RetiredTombstone
  -> HistoryExpired
```

`TransferFrozen` 转移的是 gate 冻结的精确 membership；`InboxDurable` 后 ECS inbox 承担待消费载荷，ledger 继续承担幂等与终结证明。正常 Core terminal envelope 由唯一 Session cleanup outbox 承载并把其 `BoundaryEventId` 回填 ledger；尚未进入 Core 的 gate/fault-close terminal envelope 则先写同 gate 所有的持久 `IngressTerminalJournal`，由同一个 Drain 与 ECS outbox 一起 accept-before-clear，禁止只放瞬时 callback/返回值。两种 carrier 对同一 RequestKey 互斥，ledger 的 terminal CAS 是 winner。

`TerminalProduced` 后既有正常 outcome 不得被 fault aggregate 覆盖；`BoundaryAccepted` 表示承载该 terminal outcome 的 Boundary batch 已被 managed consumer Accepted。只有同时满足 terminal semantics 已存在、Boundary 已接受且 ECS inbox/pending/outbox/journal 不再引用载荷，才能释放 full payload 并进入 `RetiredTombstone`。tombstone 至少保留 key、payload hash、sequence、assigned tick 与 terminal digest。

保留上界按 producer 明确配置 `MaxOutstanding/MaxSparseGap/RetiredHistoryWindow`。ledger 使用 `RetiredContiguousWatermark + bounded sparse tombstones` 表示孔洞；GC 只推进已经连续满足 retire 条件的前缀，稀疏项逐项保留。first/last/count/hash 只可作集合摘要，不能替代精确 membership。历史窗口到期后进入 `HistoryExpired`，相同 key 的迟到重试必须显式拒绝，不能 last-writer-wins。

## Tick seal

`GasCommandIngressSystem` 是 CommandPort capability 的必装配对，必须在 pre-Fixed ingress window 中 `OrderFirst/UpdateBefore<GasTickKernelSystem>` 运行。Gate 先冻结 `TransferHighWatermark` 及其精确 ledger membership；系统再通过显式 Job dependency/single-writer 路径把该批 record 搬入 ECS `BoundaryCommandInbox`，不得以 world-owner 主线程直接改 DynamicBuffer 并隐式完成 Kernel Job。搬运原样保留 `RequestKey/RequestSequence/ProducerSourceSequence/AssignedAvailableTick/PayloadHash` 与完整身份/payload；完成后逐项推进为 `InboxDurable`。

Kernel 只消费 `AssignedAvailableTick <= CurrentTick` 且未消费的 inbox 记录。`AssignedAvailableTick` 在 Accepted 时冻结，不能在搬运时按当下 tick 重算。0 FixedStep 时 journal/inbox 与 assigned tick 都保留；N catch-up 时由第一个满足条件的合法 tick 恰好消费一次。Kernel Job 读 `BoundaryCommandInbox` 期间绝不得 append 同一 DynamicBuffer；Entities safety/dependency 按整个 buffer/component 跟踪，“sealed prefix 可读且 tail 可并发写”不是合法性保证。window 后新 Accepted 请求只留在 journal，最早下一 ingress window 搬运，不为此强制 Kernel 同步。

InfraAdmission/Stabilization/identity overflow 等 Session-fatal 路径由 `FaultLatchJob` 在 gameplay DAG 内只写 fixed-size latch 与当 Tick sealed evidence，不直接写 managed ledger/fact/outbox。既有合法 completion fence 后，`FaultCloseHandshake` 在同一 `SessionIngressGate` 上原子 `Open -> FaultClosed`，从 ledger 精确冻结“已 Accepted 且尚无 terminal outcome”的 membership；随后为这些记录产生可由每个 `RequestKey` 映射到同一 `FaultId` 的 aggregate terminal envelope。已有正常 terminal outcome、已提交 cost/cooldown 与 provenance 必须保留并继续按原 Batch/watermark 重试，不得被 aggregate 覆盖；first/last/count/hash 仍只是审计摘要。关闭后请求同步拒绝 `SessionFaulted` 且不进 journal。

正常 `BeginClose` 也在同一 gate 上冻结 session close cut：已经 Sealed 的当前事务允许完成并产生正常 terminal outcome；尚未 Sealed 的 Accepted journal/inbox tail 逐请求产生 `RejectedSessionClosing` envelope；cut 后新请求同步返回 `SessionClosing` 且不进 journal。它与 Session-fatal aggregate 是互斥 close reason，不能用一个模糊 `BattleClosed/SessionClosed` 摘要吞掉 Accepted 请求。

## per-Battle 接收关闭与双切面

Core 的 `BattleTerminalLatch/CoreTerminalToken` 是 Battle 终局的 gameplay winner authority；Boundary registry 只是该权威的接收策略镜像。Kernel 在 `TerminalResolve` 产生 terminal token 后，控制路径携带并校验该 token，在拥有 session accept 与 per-Battle accept 状态的同一个 `SessionIngressGate` 临界区内执行 `Accepting -> Closing -> Closed`，冻结 `GateRequestCut`。没有有效 Core token 时禁止 Boundary 自行宣告 Battle 终局。

先于 cut 完成 Accepted 的记录仍属于该 Battle：已经进入 Core terminal cut 的由 Core outcome 终结；terminal token 产生后、gate 握手前 Accepted 的 tail 必须逐请求产生 gameplay-plane `RejectedBattleTerminal`，并进入相同 ledger/Boundary retry；后于 cut 的请求同步返回 `BattleClosed` 且不进 journal。aggregate `BattleClosed` audit 不能吞掉这些逐请求 terminal outcomes。最终 `BattleOutcomeSeal` 同时要求 Core outbox cut 与 Gate request cut，且只等待本 Battle，不等待同 Session 的其他 Battle；双切面最终封印由 [06 Observation/Presentation/Replay Spec](../06-Observation-Presentation-ReplaySpec.md) 唯一定义。

## Resolve 顺序

1. Gather/seal 后解析 Session/Battle/source/handle generation，完成 command 去重、stale 与 typed reject；仍不写 gameplay authority。
2. `OwnerPlanBuild` 在 ASC-local shadow 中执行 CanActivate、Activation lifecycle 与 CommitCheck，产生完整 CommitPlan、post-commit capture candidate、direct program和生成上界；零权威写。
3. `TargetResolve/Expand` 对 planned token 产生稳定目标序列和全部 bounded effect ops；不写目标 ASC，也不产生正式 EffectSpec/Application身份。
4. `WholeTickInfraAdmission` 对整 Tick可预留资源做逻辑预算/物理预留；后续 Job已在同一 DAG预排，失败时统一读取 `AdmissionResult` 后 no-op，只有 FaultLatch写 Session Faulted。
5. `AscOwnerCommandWave` 对 admitted CommitPlan执行no-fail owner-local原子提交；cost/cooldown/Committed flag与draft→正式 EffectSpec/Application identity提升只在此处写，并同时记录owner EmittedApplicationRef；普通self GE不在此处应用。
6. `SourceSpecProjection` 只为已 Commit且已有正式 identity 的计划密封 source-bound Spec/Capture，随后 GroupByTarget 并进入 canonical `TargetPrepare -> SessionFaultReduce -> TargetPublish`。

CanActivate 成功不等于已 Commit。等待后 Commit 必须在 owner shadow重查；业务失败不产生 admitted CommitPlan，基础设施失败使整 Tick gameplay零写。成功 CommitPlan no-fail一次性写 cost/cooldown/committed flag；重复 Commit、Cancel、End 都必须有确定性幂等/拒绝语义。

## Target resolve

Target输入可以来自冻结TargetData、Definition纯target rule或generated `BoundaryTargetRef`。`AscHandle` variant携带Epoch/stable id/generation并在consume时严格校验；`BattleUnitSelector/DefinitionRule` variant必须显式声明Battle scope与`ResolveAtConsume`，在TargetResolve/Admission前解析为完整`TargetAscHandle`和ordinal。输出不得把raw Entity带到Boundary或跨tick；空间查询命中Entity后立即解析/复制稳定身份与所需空间快照。

## Continuation

Continuation/Subscription 是独立 slots。一 Activation 可有多个并行等待；外部 ASC subscription 反向保存 Activation+Continuation handle，投递与取消双重校验 Generation。Kernel 执行期间才到达的 completion 默认下一 tick。

## 拒绝方向

- Shell 直接向 `DynamicBuffer` append。
- CommandPort 直接调用 magnitude/target/effect 逻辑。
- 单个 Activation 只有一个 wait state。
- request 保存 delegate、managed object、raw Entity 或 tick temp pointer。
- target resolve producer 随机写目标 ASC。

## 验收

- 0..N FixedStep、同 RequestKey 同/异 PayloadHash、producer generation/source sequence、stale Epoch/Generation、inbox overflow 测试。
- sparse holes/retire/GC/HistoryExpired、capacity-before-Accepted 与 terminal envelope 恰一次测试。
- 多 Battle 并发终局：A 的 Core/Gate 双 cut 可独立封印，B 继续接收；terminal tail 逐请求 `RejectedBattleTerminal`。
- Session fault 只聚合尚无 terminal outcome 的精确 ledger membership，已有正常结果按原 Batch 重试且不被覆盖。
- CanActivate→等待→Commit 资源变化回归。
- 并发 Activation、多个 Continuation、外部 subscription cancel/late event 回归。
- command trace 与 target order 在重复运行中一致。
