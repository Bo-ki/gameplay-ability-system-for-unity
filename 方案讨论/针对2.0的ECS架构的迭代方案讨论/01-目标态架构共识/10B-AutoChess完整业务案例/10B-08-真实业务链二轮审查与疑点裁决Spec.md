# 10B-08 真实业务链二轮审查与疑点裁决 Spec

## 结论

第二轮审查已把 AutoChess 从“展示 GAS 机制的示例集合”收敛为一条可实现、可迁移、可验收的真实业务链。最终方案保留 UE GAS 的领域含义，但有意放弃同步可重入调用栈：Runtime v1 使用同一 Kernel DAG 内的 ASC owner plan/commit 与 target state 两段 mutation wave，所有普通公开 reaction 和跨 ASC maintenance 最早下一 tick。

本轮最终交叉回归在限定设计范围内为 `P0=0、P1=0`。尚未完成的是代码、Unity 测试、Profiler 与真实运行证据，不能把本文件当作实现完成证明。

## 审查方法

审查采用三轮三视角交叉反驳：

1. 当前 EX-GAS/AutoChess：从 Spawn、Grant、AI、Commit、Effect、Period、Death、Report 到 teardown 逐段读取真实源码与生成配置。
2. UE GAS：反查 Commit、End/Cancel、Wait、Stack、Period、Immunity、Granted Ability、Owner/Avatar 与 Cue 源码语义。
3. Unity DOTS：复核 FixedStep、group allocator、Job DAG、单 writer、EndFixed、Cleanup Buffer 与 shutdown 安全性。

一审分别发现问题，二审交换结论并要求从另外两个视角反驳，三审在文档修正后回归检查 writer、identity、payload 与 lifecycle 的物理闭环。最终裁决以“业务语义闭合、DOTS 可实现、迁移差异显式”三项同时成立为准。

## 验收内容分层

| 层级 | 内容 | 用途 |
|---|---|---|
| Tier A：Legacy Characterization | 当前 `9101/9102` 普攻、`9103/9207` 斩杀、`9104/9203/9204` 毒、Health 推导死亡、50 个隔离 BattleGroup | 记录旧行为与缺陷；不能作为 v1 conformance golden |
| Tier B：Runtime v1 Conformance | 双 Commit、Commit/Cancel、target dead、Wait 注册边界、Period 精确 tick、Death crossing、Cue 多 active cycle、Boundary 重试、teardown | v1 发布前必须通过的语义微场景 |
| Tier C：扩展业务样例 | 盾击、冰霜新星、羁绊、治疗、复杂 immunity/inhibition | 验证扩展能力；不得冒充当前 Demo 已覆盖 |

Tier A 中非法配置、静默失败、raw Entity 排序、self fallback 和污染 hash 只作为问题证据；目标 golden 必须来自 Tier B 的稳定语义投影。

## 唯一业务执行链

```text
Session Install
 -> SpawnBatch Pending
 -> setup EndFixed creates Pending ASC/Registry entries
 -> next FixedStep Kernel SpawnFinalize maintenance（不计 gameplay tick）
 -> whole-batch Ready publish handles
 -> BattleInstance Running
 -> deterministic AI intent / CommandPort + SessionIngressGate（RequestSequence）-> BoundaryIngressJournal
 -> pre-Fixed GasCommandIngressSystem -> ECS BoundaryCommandInbox
 -> Gather/TickStartSnapshot + PlanExpandScratchProvision/token
 -> OwnerPlanBuild（ASC-local shadow read-your-writes）
 -> TargetResolve / bounded expansion
 -> WholeTickInfraAdmission（失败时权威零写并 Fault）
 -> AscOwnerCommandWave（no-fail CommitPlan）
 -> SourceSpecProjection
 -> GroupByTarget / AscTargetStateWave（application / stack / period / attribute / death）
 -> target-local stabilization
 -> StableFactMerge / per-BattleInstance TerminalResolve
 -> destination-grouped T+1 route
 -> BoundaryProject / Record standard EndFixed
 -> scoped outbox / managed staging accept / immutable consumers
 -> Battle FinalDrain / Outcome snapshot
 -> Session teardown audit / ValidationResult
```

两个 mutation wave 由 JobHandle 依赖连接，不使用 phase `Complete()`。不同 ASC 可并行，同一 ASC 在每个 wave 内按 canonical key 串行。

`SpawnFinalize` 是 Session=`SpawnPending` 时替代整条 gameplay DAG 的 Kernel maintenance mode，不是新增 System：它全量校验 EndFixed 已创建的 Pending batch，再一次 no-fail发布 Session `AscRegistrySlot[]` 与所有 ASC Ready；失败则 Faulted并记录整批销毁。ASC自身不承载 Physics/Transform，Pending batch不得提前创建可参与 Physics的派生 gameplay Entity；setup fixed update不进入 SimulationTick、BattleLocalTick、业务 hash或计数。Drain、runner与 post-EndFixed系统都没有 Ready写权。

## 最终裁决

### 1. Owner plan、Commit 与普通 self Effect

- `OwnerPlanBuild` 在 scratch 中按 ASC 模拟前序成功计划；后请求可读前序计划的 cost、cooldown、Committed 与 activation-owned contribution。
- Gather 先以 sealed/due count、tick-start Definition lookup 与 Catalog bake maxima checked 计算 `PlanExpandScratchEnvelopeToken`，在 OwnerPlanBuild/TargetResolve 写前 provision 定长 scratch；逻辑超限使两者 no-op，并由后续唯一 `WholeTickInfraAdmission` 提升为失败。
- `WholeTickInfraAdmission` 验证 envelope token，在任何权威写之前只为实际展开后的 downstream scratch、slab、payload、pending、fact/outbox 预留逻辑/物理容量；失败使本 tick进入确定性 fault，权威状态零写。
- 所有下游 Job 预排在同一 DAG并读取 tick-local `AdmissionResult`；失败时 Owner/Target/Fact/Structural分支统一 no-op，DAG 内只有 FaultLatch写固定大小 `SessionFaultLatch=Detected` 与 sealed subset 证据，禁止为决定是否调度而中途 `Complete()`。失败 Tick 不写逐 request Boundary fact；outer completion 后 `FaultCloseHandshake` 与 CommandPort accept 在同一 gate 线性化，按全程透传的 `RequestSequence` 将关闭前全部 accepted-outstanding（含 unsealed/future tail）的 first/last/count/hash 写入 IngressClosed latch，由同一 FaultId 统一终结；关闭后请求同步拒绝，原 inbox/接收 journal 仅留诊断审计且不重放。
- 标准 EndFixed ECB没有公开 command reserve API；structural intent只做逻辑 count/token budget。宿主 allocator/OOM是 fatal environment failure，不能伪装成可恢复 `InfraAdmissionFault`。
- `AscOwnerCommandWave` 只执行已经 admission 的 no-fail CommitPlan。
- CommitPlan 原子提交 `Committed state + CostMutationContract + CooldownGateContract + activation-owned contribution`，成功后才产生 Effect operation。Cost 直接更新同一 `AttributeValueSlot`；Cooldown 创建独立于 Activation/ActiveEffect 的 ASC-owned `CooldownGateSlot`。
- TargetResolve 位于 Commit前，只能携带 tick-local `SpecDraftToken` 和 generated canonical `ProgramNodeOrdinal/SpecOrdinal/PlannedApplicationOrdinal`；成功 Owner Commit的同一线性化点仅以 `(Epoch, SourceAscHandle, canonical CommitSequence, ProgramNodeOrdinal, SpecOrdinal[, PlannedApplicationOrdinal])` 提升正式EffectSpecId/ApplicationId并写owner EmittedApplicationRef，`SourceSpecProjection`随后密封Spec/Capture。SpecDraftToken/scratch/worker 序不进入正式 ID 或 hash；失败plan不得留下权威身份或审计ref。
- 普通 self-target GameplayEffect 也在 `AscTargetStateWave` 应用，因此不对本 tick `AscOwnerCommandWave` 后续 CanActivate 可见。确需该可见性的内容必须建成显式 Cost/Cooldown owner commit contract 或真正 activation-owned contribution；否则 Definition bake fail。
- OwnerPlanBuild 先在 shadow 释放 `EndTick <= CurrentTick` 的 gate，OwnerWave 再先提交 gate/Tag cleanup、后提交新 Commit。v1 gate 只支持 `RejectWhileActive + ExpireOnly`；无法无损编译的 GE stack/refresh/period/dispel 字段 bake fail。
- Commit 先于 Cancel：保留已提交 cost/cooldown/work；Cancel 只终止剩余生命周期。Cancel/End 先于 Commit：后续 Commit 返回 `Rejected.OwnerEnding`。

这比当前代码的顺序 append 更强，也与 UE 同调用栈 self-effect 可见性有意不同。

### 2. Committed-work-wins 与目标生命策略

- Effect operation 在成功 Commit/SpecCreation 后自包含 Definition、Context、Source capture 与 provenance。
- source 在稍后的 target wave 死亡、取消或销毁，不撤回已 Commit 的远端工作。
- 是否应用仍由目标线性化点决定；`TargetLifePolicy` 至少支持 `AliveOnly / RequireDead / AnyLifeState`。
- AutoChess damage、execute 与 poison 固定为 `AliveOnly`。首次 Death latch 后，同 canonical range 的后续 AliveOnly application 产生 typed reject，不回滚 source cost/cooldown。
- 致死 application 完成自己的全部节点；其 unclamped 结果产生 overkill。后续 reject 的 AppliedDamage、Overkill 与 AssistContribution 均为 0。
- AutoChess v1 无隐式复活；通用复活必须是显式 lifecycle policy 与新 ordinal。

### 3. Target identity 不再混为一个枚举

每个 Definition 独立声明：

1. 逻辑目标：稳定 ASC identity，`FrozenTarget` 或显式重新解析。
2. Avatar：`FollowAsc` 或 `RequireSameAvatar(binding generation)`。
3. 空间：冻结 hit/origin/shape，或在批准 phase 重新采样。
4. 生命：`TargetLifePolicy`。

`FrozenSpatial` 是 TargetData 变体，不是 actor 失效 fallback。Self 只能来自显式 `SelfTarget` rule；任何敌方目标失效都不得回退 Owner。

### 4. Activation 只清理自己拥有的贡献

- `OwnedContributionRanges` 保存 activation-owned tag、block/cancel、tracked cue 与 Continuation。
- `EmittedApplicationRefs` 只作因果和审计，不拥有普通 cost/cooldown/damage/duration effect。
- 只有显式 `RemoveOnActivationEnd` 的 application 才随 End 撤销；未落地时 cancel-before-apply，已落地时只撤精确 application/contributor。
- `RemoveOnActivationEnd` 与聚合 stack 组合若没有逐 application ledger，Definition bake fail；禁止删除整个共享 ActiveEffectSlot。
- Death 在 target wave 才要求跨 ASC remove 时，默认生成 T+1 destination command。

### 5. Wait 注册没有 lost wakeup

- Wait 明确区分 `Level / Edge / Event / HandleLifecycle / Timer`。
- sample 与 register 在 observed ASC single-writer transaction 内完成。
- 跨 ASC 使用 `PendingRegistration -> Registered/Ack 或 Completed` 两端协议；取消与晚到消息校验两端 Generation。
- Level 已满足时不保留 SubscriptionSlot，emit tick 生成 completion；所有 Continuation resume 统一在 T+1。
- Edge 以注册样本为 baseline，Event 以注册 watermark 为起点，均不追溯旧事件。
- one-shot 在 observed writer 第一次 match 时立即 `Consumed`；persistent completion 带单调 `WakeOrdinal`。
- recipient 顺序由 catalog-hashed priority、tag depth、stable recipient id、definition/registration ordinal 与 wake ordinal组成。

### 6. Capture phase 与 Live 时延

- Source Snapshot：`OwnerPlanBuild` 在 shadow 中为每个计划计算“该计划成功 Commit 后”的 capture candidate；它可见本 owner 前序 CommitPlan、排除后序计划与本 tick incoming target Effect。candidate 在 admission 前不具权威身份，只有对应 CommitPlan 成功后才由 `AscTargetStateWave` 前的 `SourceSpecProjection` 密封。
- Target Snapshot、application requirement 与 immunity：每条 target application 的线性化点读取，可见同目标前序 canonical application 已提交状态。
- 同 ASC Live：纳入 target-local stabilization。
- 跨 ASC Live：source revision 在 T 产生 destination-grouped dirty command，T+1 由目标 writer更新；T+1 maintenance 在 PeriodDue 前执行。
- Live binding 必须包含 Capture ordinal、consumer node/field、LastSeenRevision、source-gone policy 与两端 Generation；缺任一闭环即 bake fail。

### 7. Period 的精确时序

通用 Definition 必须显式声明 execute-on-apply、period reset、duration refresh、expiry、inhibit/resume、missed-period 与 final-period policy。

AutoChess 目标毒固定为：

```text
Definition              = 9203
Period evaluator        = 由 9203 Definition 编译出的 inline pure evaluator；legacy 9204 不进入目标 Catalog
StackKey                = (DefinitionId, TargetAsc, SourceAsc)
StackPolicy             = AggregateBySource
StackPayloadPolicy      = ReplaceLatest
Magnitude               = Snapshot(Source.Attack) * 0.3 * CurrentStackCount
DurationTicks           = 8
PeriodTicks             = 2
StackLimit              = 3
ExecuteOnApplication    = false
DurationRefreshOnApply  = Never
PeriodResetOnSuccess    = true（达到上限但 application 成功也重置）
ExpiryPolicy            = RemoveOneStackAndRefreshDuration
ExpiryPeriodPolicy      = Reset
ExpirySameTickPolicy    = PeriodDueBeforeExpiry
InhibitTimePolicy       = duration 继续
MissedPeriodPolicy      = SkipNoCatchUp
```

每次成功 application（包括已达上限的成功 reapply）用最新 Source Snapshot、Context 与 Application provenance 替换 payload，并设置 `NextDueTick = ApplyTick + 2`。DueTick 的 PeriodExecution 使用最新 application provenance。

目标语义顺序为：destination maintenance/live dirty → 已有 PeriodDue → Expiration → sealed remove/inhibit → 新 committed application。Period execution 前校验 expected due/handle/state，执行后若 slot 已 Removing 则 expiry、refresh、next-due 全部停止。

当前 `StackingType=9203`、dummy Energy modifier、固定 `Health -1`、不乘 StackCount 和隐藏 sourcegen policy override 全部是迁移缺陷；目标配置必须 bake fail 或显式修正，不能保留兼容支路。

### 8. Death、胜负与三个终局层级

必须区分：

- Unit Death：首次 `old Health > 0 && unclamped result <= 0`，在当前 target transaction 写不可逆 Dead latch并冻结 killer/provenance；其后的 target work按 TargetLifePolicy 裁决。该 latch 不逆向撤回此前 Owner Commit，下一 tick owner work则由 Dead 状态拒绝。
- BattleInstance Terminal：完整 `AscTargetStateWave/StableFactMerge` 后，由唯一 `TerminalResolve` 处理双杀、平局和 winner；只关闭该 BattleInstance ingress。
- GAS Session Terminalizing：全部 BattleInstance 终局或显式 shutdown 后进入。

因此 replicated groups 可以共享一个 Session，单组胜负不会停止其他组。AutoChess corpse ASC 保留到 Battle outcome snapshot 与 gameplay FinalDrain；死亡时移除/取消其 Ability、ActiveEffect 与 Cue，Session teardown 才销毁 ASC。

Death mutation fact 至少保存 RequestedDelta、PreClamp、UnclampedResult、PostClamp、EffectiveDelta、KillingApplicationId、ContributorId、CausalityId 与 DeathTransitionId。killer 由首次 crossing冻结，后续输入不得覆盖。

### 9. Boundary、Outcome 与 teardown

Boundary 使用两阶段接管：

```text
collect + stable sort
 -> managed staging 以 BatchId/physical owner/InFlightWatermark 幂等接受所有权
 -> 成功后只 clear 各 source `EventId.OwnerSequence <= InFlightWatermark`，并把已完整接管的 cleanup shell标记 Accepted
 -> publish ring，或产生 DroppedRange/Fatal receipt
```

outbox owner由 fact identity scope唯一决定：ASC-scope fact写所属 ASC cleanup outbox；BattleInstance/Session-scope fact写唯一 Session cleanup outbox，禁止双写或选择“代表 ASC”。`FactPlane=Gameplay/TeardownAudit` 只决定 result/hash inclusion，不决定 outbox。事实自包含 scope/plane/Battle/source/target stable id + generation及 inline tagged payload，以 `(Epoch, PhysicalOwnerKind/Id/Generation, OwnerSequence)` 复合 `BoundaryEventId` 交付去重；`BoundaryDrainState` 只冻结 physical owner、持久 `NextOwnerSequence`、BatchId/InFlightWatermark，Session state 不伪造唯一 Battle。Drain使用 live ASC、live Session、两种 live identity均无的 cleanup shell三类互斥 query，不能用 `WithNone<GasAscIdentity>` 把 live Session误判为 shell。

staging 失败时 outbox 原样保留；catch-up 后续事实若晚于冻结 InFlightWatermark则留待新 Batch，不能被旧 receipt清掉。`NextOwnerSequence` 在 Accepted→Idle 后仍单调且回绕 fatal。零事实 cleanup shell也必须在 producer completion后以 physical owner/empty range 取得显式 NoFactReceipt，不伪造 Battle scope。正常下一 tick 的 Kernel cleanup prepass 为 Accepted shell记录当 tick标准 EndFixed removal；post-Fixed Drain不创建等待下一 batch的 ECB。无下一 tick时，只能在完整 Tick DAG与 EndFixed后 FinalDrain、完成全部 producer，再显式移除 cleanup state。

结果分两层：

1. `BattleOutcomeSnapshot`：该 BattleInstance 的 `FactPlane=Gameplay` facts 已被 staging 接管后冻结；winner 与 `BattleHash` 只含 Gameplay plane。
2. `ValidationResult`：Session teardown audit完成后返回；包含 cleanup receipt、shell/outbox 零遗留和 `FactPlane=TeardownAudit` facts。

Teardown fact 仍交付 Cue/Replay/Validation，但不得改变 winner 或 BattleHash。Result 返回后禁止产生新事实。

### 10. Cue active cycle

```text
CueLifecycleKey =
  SimulationEpoch
  + ActiveEffectHandle
  + ActiveCycleOrdinal
  + CueDefinitionOrdinal
```

- 首次稳定 Active：`ActiveCycleOrdinal=0`，发送 `OnActive + WhileActive`。
- Active → Inhibited：发送一次 `Removed(ordinal 0)`。
- Inhibited → Active：`ActiveCycleOrdinal++`，以 ordinal 1 重新发送 `OnActive + WhileActive`。
- remove while Active：`Removed(current cycle)`。
- remove while Inhibited：不重复 Removed。
- stack merge/refresh 不自动开启新 cycle；若需要 pulse，使用 ApplicationId。
- Executed 使用 `(EffectApplicationId, CueDefinitionOrdinal)`；period 使用 `(SimulationEpoch, ActiveEffectHandle, PeriodExecutionOrdinal, CueDefinitionOrdinal)`，DueTick 只是 payload/诊断字段。
- `BoundaryEventId=(Epoch, PhysicalOwnerKind/Id/Generation, OwnerSequence)` 只用于单条投递去重，绝不替代 lifecycle key，也不可缩减为裸 owner-local sequence。

### 11. Granted Ability removal

公开 removal policy 固定为：

- `CancelImmediately`
- `RemoveWhenAllActivationsEnd`
- `LeaveGranted`

`LeaveGranted` 对齐 UE `DoNothing` 的可观察语义：grant slot 继续 Live、仍可激活；内部解除 granting ActiveEffect 的 cleanup ownership，冻结历史 provenance，不保留 dangling Effect handle。若需要 inhibition 时暂停，使用独立 `SuspendWhileInhibited` policy，不能继续叫 DoNothing。

### 12. 配置与容量

- Luban 是 authoring 语义唯一权威；编译后的 Runtime Blob 是 Session 内运行快照。
- SourceGenerator 只生成 lookup/evaluator/glue，不得覆盖或补写隐藏 GameplayEffect 语义。
- Attribute/Tag 运行时唯一权威分别是 Session layout-indexed `AttributeValueSlot[]` 与 Catalog-indexed `TagCountSlot[]`；generated 只输出 id/index/layout/catalog/init projection/纯访问器，可选 presence words仅为派生缓存，不生成 AttributeSet/TagMask component镜像或 64 Tag 上限。Luban Tag authoring 只接受 typed `TagId[]` / `TagRequirementId` 外键，禁止 suffix alias或未定义 query。
- Scenario spawn rows进入 immutable Blob `SpawnEntry[]` range，并受版本化 ScaleProfile逻辑上限准入；不得用固定字节 `FixedList` 截断或隐藏内容容量。
- 重复字段、非法 enum/range、缺少 stack/capture/period policy 或 content provenance 冲突直接 bake fail。
- business stack overflow、target dead、immunity 是正常 typed result，不回滚 source Commit。
- infrastructure capacity 在 gameplay mutation 前按整个 tick admission；不足时权威零写并 Fault。
- Boundary staging 容量发生在 gameplay 后，不回滚 Core；失败时不清 outbox，并阻止 FinalDrain/ValidationResult成功。

## Canonical hash

Gameplay semantic hash 至少包含：

- Session/content/schema/layout/tick-rate hash；
- SimulationTick、BattleInstanceId、ScenarioUnitId、ASC/slot generation；
- Request、Activation、Application、Contributor、Causality 与 typed outcome；
- Attribute/Tag/Death 的规范化数值和 transition；
- Fact/Cue 的 canonical sequence。

必须排除：raw Entity、RenderFrame、静态 report key、wall clock、TickBatch/Boundary batch切分、Profiler/Journaling 数量、presentation marker/截断和日志顺序。Transport/evidence 另有独立 hash。

## 规模场景不再只写 x50

| Profile | 必证问题 |
|---|---|
| ReplicatedGroups | 多个隔离 BattleInstance 的吞吐与独立终局 |
| HotTargetFanIn | many-to-one canonical range、single writer 与 reject 顺序 |
| SynchronizedPeriodBurst | 大量同 tick PeriodDue、expiry 与 allocator/outbox 峰值 |
| MassDeathTeardown | Death crossing、cleanup shell、FinalDrain 与无下一 tick shutdown |
| BoundaryBurstRetry | staging reserve、失败重试、DroppedRange/Fatal receipt |
| WaitFanoutCancel | 跨 ASC registration ack、one-shot consume、cancel/late completion |
| CrossAscLiveDirty | destination regroup、T+1 latency、source-gone/cycle budget |

每个 Profile 显式配置 `MaxFixedTicksPerBatch`、`World.MaximumDeltaTime`、logical capacity 与双 rewind allocator高水位。Headless 长战斗必须分有界 TickBatch；batch 分割不得改变 semantic hash。

## Tier B 最低固定向量

1. 两个 Activation 争抢同一 cost/cooldown。
2. Commit→Cancel 与 Cancel→Commit。
3. instant Ability End 后 cooldown仍存活。
4. 普通 self GE 不影响同 `AscOwnerCommandWave` 后续 CanActivate。
5. 前序 application 改变后序 requirement/immunity。
6. source 先死但已 Commit 远端 work仍结算。
7. target 首死后后续 AliveOnly reject与 overkill归属。
8. already-true Level wait、注册边界 Edge/Event、跨 ASC cancel/ack。
9. poison reapply、cap reapply、due=end、expiry减层、inhibit skip和period自删。
10. `LeaveGranted` 与重复 grant provenance。
11. inhibit→reactivate 两轮 Cue lifecycle。
12. Avatar rebind projectile与 FrozenSpatial。
13. replicated group双杀/平局与独立终局。
14. SpawnBatch EndFixed后由下一 Kernel完成 `SpawnInitializationTransaction`再整批 Ready；任一 identity/layout/Attribute/Tag/Grant/initial-effect/outbox 失败零成员可见，initial duration/period 以下一 gameplay Tick 为 ReadyTick。
15. admission failure下游预排 Job全部 no-op、gameplay零写，只有 SessionFaultLatch 从 Detected 终结为 IngressClosed；accepted-outstanding RequestSequence first/last/count/hash 精确覆盖 fault close 前未终结请求（含 unsealed/future tail），close 后同步拒绝，无逐 request outbox 写入且不重放。
16. scoped outbox重试、late tail、空 shell NoFactReceipt、零 ASC Session terminal与无下一 tick teardown。
17. Result 后零事实；相同输入采用不同 TickBatch切分仍得到相同 semantic hash。

## Owner 与实施边界

- 通用 Ability/Wait/Reaction：`01B`、`03D`。
- Kernel DAG、排序、容量与 Boundary handoff：`03A/03E/03F/03G/13/18`。
- Application/Capture/ActiveEffect/Cue：`04/05/06`。
- 当前缺陷证据：`00-当前架构事实`。
- AutoChess 业务投影：本目录与 `10/11/21`。
- 实施顺序与真实验证：`02/RuntimeV1不可兼容迁移/V0-V7`。

任何实施发现若改变上述语义，必须先修改其唯一 Owner，再同步本文件、`17`、`90` 和对应测试；不得用兼容 flag保留第二套事实。
