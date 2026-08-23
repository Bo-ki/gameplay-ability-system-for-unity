# Boundary Command 与 Core Resolve Spec

## 结论

Boundary 只负责可靠接收和冻结外部意图；`GasTickKernelSystem` 才负责 resolve、CanActivate、commit、target 与 gameplay mutation。CommandPort 不是同步 GAS facade。

## CommandPort

允许的公开操作示例：

```text
RequestActivateAbility(BattleInstanceHandle, AscHandle, GrantedAbilityHandle, EventPayload, RequestId)
RequestCommitActivation(BattleInstanceHandle, AbilityActivationHandle, RequestId)
RequestCancelActivation(BattleInstanceHandle, AbilityActivationHandle, CancelReason, RequestId)
RequestApplyEffect(BattleInstanceHandle, SourceAscHandle?, BoundaryTargetRef, EffectDefinitionId, Payload, RequestId)
RequestRemoveEffect(BattleInstanceHandle, ActiveEffectHandle, RemovalReason, RequestId)
```

每个请求先验证 Session/Epoch、BattleInstance handle、`BoundaryTargetRef` tag/schema、inline tagged payload size和ingress capacity，再复制到 Runtime Boundary 持久 `BoundaryIngressJournal`。source存在时必须验证其membership与显式Battle一致；source缺失的Battle/Session command仍必须显式带Battle。此时不能声称Ability已激活或Effect已应用，也不能把裸payload index或含义不明的TargetStableId留到跨tick。

`Accepted` 必须有严格线性化点：Runtime Boundary 为每 Session 持有唯一 `SessionIngressGate { Open/Closing/FaultClosed, NextRequestSequence, AcceptedHighWatermark }` 与受其所有的持久 `BoundaryIngressJournal`，CommandPort 只能在该 gate 上序列化分配 `RequestSequence`、把该 sequence 连同完整 record 放入 journal 后才返回 Accepted。重复 `RequestId` 必须复用原 record/sequence，不得推进 `NextRequestSequence`。`RequestSequence` 是 fault-close/outstanding/交付审计顺序；业务 canonical key 使用独立、冻结的 `SourceSequence`，两者不得互相冒充。journal 不是 tick scratch，0 FixedStep 不丢失；每条记录在搬入 ECS inbox 前仍受 gate/接收审计所有。Fault close 与 accept 在同一 gate 上线性化：先于 close 的请求必定 `RequestSequence <= AcceptedHighWatermark` 并被本 FaultId 终结，后于 close 的请求同步拒绝 `SessionFaulted` 且不进 journal。禁止“已向调用者返回 Accepted，但仅存在未受 gate 管理的临时队列”。

## Tick seal

`GasCommandIngressSystem` 是 CommandPort capability 的必装配对，必须在 world-owner 主线程的 pre-Fixed ingress window 中 `OrderFirst/UpdateBefore<GasTickKernelSystem>` 运行；它是唯一能从 journal 搬运到 ECS `BoundaryCommandInbox` 的 writer，并在该 window 之后关闭 ECS append。搬运必须原样保留 `RequestSequence`、`RequestId`、`SourceSequence` 与完整身份/payload。Kernel 只消费 `AvailableTick <= CurrentTick` 且未消费的 inbox 记录。0 FixedStep 时 journal/inbox 都保留；N catch-up 时由首个合法 tick 恰好消费一次。Kernel Job 读 `BoundaryCommandInbox` 期间绝不得 append 该 DynamicBuffer；Entities safety/dependency 按整个 buffer/component 跟踪，“sealed prefix 可读且 tail 可并发写”不是合法性保证。window 后新 Accepted 请求仅留在 journal，最早下一 ingress window 搬运；不为此强制 Kernel 同步。

准入/稳定化 fault 的 `FaultLatchJob` 只在 gameplay DAG 内写 `SessionFaultLatch=Detected`与当 Tick sealed subset 证据，不写 fact/outbox。DAG 在既有 runner/Boundary completion fence 完成后，`FaultCloseHandshake` 在不运行 gameplay 的控制路径上原子执行 `SessionIngressGate.Open -> FaultClosed`，按 `RequestSequence` 冻结截至该关闭点全部“已 Accepted 且未终结”请求的 first/last sequence、count 与 canonical hash，并将 fixed-size latch 终结为 `IngressClosed`。这一 outstanding set 可同时包含本 Tick sealed inbox subset、ECS inbox unsealed/future records 和未搬运 journal tail；全部 carrier 都原样持有同一 sequence，全部请求由同一 FaultId 终结、保留 inbox/journal 供审计且永不重放。

## Resolve 顺序

1. Gather/seal 后解析 Session/Battle/source/handle generation，完成 command 去重、stale 与 typed reject；仍不写 gameplay authority。
2. `OwnerPlanBuild` 在 ASC-local shadow 中执行 CanActivate、Activation lifecycle 与 CommitCheck，产生完整 CommitPlan、post-commit capture candidate、direct program和生成上界；零权威写。
3. `TargetResolve/Expand` 对 planned token 产生稳定目标序列和全部 bounded effect ops；不写目标 ASC，也不产生正式 EffectSpec/Application身份。
4. `WholeTickInfraAdmission` 对整 Tick可预留资源做逻辑预算/物理预留；后续 Job已在同一 DAG预排，失败时统一读取 `AdmissionResult` 后 no-op，只有 FaultLatch写 Session Faulted。
5. `AscOwnerCommandWave` 对 admitted CommitPlan执行no-fail owner-local原子提交；cost/cooldown/Committed flag与draft→正式 EffectSpec/Application identity提升只在此处写，并同时记录owner EmittedApplicationRef；普通self GE不在此处应用。
6. `SourceSpecProjection` 只为已 Commit且已有正式identity的计划密封source-bound Spec/Capture，随后GroupByTarget并进入canonical `AscTargetStateWave`。

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

- 0..N FixedStep、重复 RequestId、stale Epoch/Generation、inbox overflow 测试。
- CanActivate→等待→Commit 资源变化回归。
- 并发 Activation、多个 Continuation、外部 subscription cancel/late event 回归。
- command trace 与 target order 在重复运行中一致。
