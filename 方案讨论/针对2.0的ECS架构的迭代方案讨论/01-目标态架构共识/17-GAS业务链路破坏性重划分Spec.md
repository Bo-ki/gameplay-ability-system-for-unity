# GAS 业务链路破坏性重划分 Spec

> Owner：`01-目标态架构共识` | 版本：Runtime v1 | 最近裁决：2026-08-24 | 状态：目标态唯一正文

## 目的

EX-GAS Runtime v1 采用一次性、不可兼容的权威切换：以 ASC-local 稳定 slab、单 Core tick kernel、target-owned single writer、标准 EndFixed ECB 和单 Boundary Drain 实现 GAS 语义。旧五段 physical group、Ability Entity、legacy GE entity、双 ActiveEffect store、singleton/EventBus 主链和多 Boundary ECS consumer 不进入目标态。

当前代码事实只读 [Runtime v1 不可兼容迁移基线事实](../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)与[第三轮多 Agent 架构与性能审查事实](../00-当前架构事实/RuntimeV1第三轮多Agent架构与性能审查事实.md)；执行顺序只读 [Runtime v1 不可兼容迁移任务](../02-主线任务树/RuntimeV1不可兼容迁移/README.md)。本文件不记录当前命中、验证流水或任务状态。

## v1 最终裁决

| 议题 | v1 裁决 |
|---|---|
| Gameplay authority | ASC Entity 是 Ability、Activation/Continuation、ActiveEffect、Tag、Attribute 的唯一权威 owner |
| 物理调度 | 单 `GasFixedTickSystemGroup`；Core 只有一个 `GasTickKernelSystem` |
| 逻辑 phase | 保留为 Kernel 内命名 Job/DAG/counter，不再拆成业务 SystemGroup |
| 结构变化 | 使用标准 `EndFixedStepSimulationEntityCommandBufferSystem`；Core 权威状态不依赖结构变化 |
| 长期实例 | ASC-local 非压缩 slab：slot index + generation + free-list |
| Definition backend | 禁止按 Definition 选择 slot 或 Entity；v1 没有 per-definition Entity promotion |
| 跨 owner 写 | `AscOwnerCommandWave -> TargetPrepare -> SessionFaultReduce -> TargetPublish`依赖串联；Prepare只写shadow，Publish才写durable，跨 ASC route按destination分组 |
| Commit | scratch plan + 全 tick infrastructure admission 后，source-local CommitPlan no-fail 原子提交 |
| Boundary | managed staging 先接管，成功后才清 ECS outbox；单 Drain 发布 immutable batch/ring |
| Cue | OnActive / WhileActive / Executed / Removed 四阶段 request，Core 不持有 Cue Entity |
| 迁移 | 单次切换，无 runtime selector、feature flag、fallback 或双事实源 |

## 非目标

1. v1 不实现 prediction、rollback 或网络复制分支。
2. v1 不兼容旧 Ability Entity/legacy GE runtime handle、旧 save snapshot 或旧 EventBus consumer。
3. v1 不为任意 managed AbilityTask 对象提供逃生口。
4. v1 不按单个 Definition 暴露物理存储策略。
5. v1 不把 Debugger、Replay、UI 或 Cue consumer 变成 gameplay 输入。

## 物理调度与 tick owner

目标物理顺序：

```text
SimulationSystemGroup
├─ FixedStepSimulationSystemGroup
│  ├─ PhysicsSystemGroup
│  ├─ GasFixedTickSystemGroup                 [UpdateAfter PhysicsSystemGroup]
│  │  ├─ GasCommandIngressSystem              Boundary journal → ECS inbox
│  │  └─ GasTickKernelSystem
│  └─ EndFixedStepSimulationEntityCommandBufferSystem
└─ GasBoundaryDrainSystem
   └─ Managed Immutable Batch Ring
```

约束：

1. 必装 `GasCommandIngressSystem` 只在 pre-Fixed window 把 `SessionIngressGate` 已接收的 Boundary journal records 搬入 ECS inbox，不拥有 tick scratch，也不写 gameplay 权威状态。
2. `GasTickKernelSystem` 是唯一 gameplay state writer 与 tick scratch owner。
3. 标准 EndFixed system 在独立 GAS World 中必须显式创建、注册和排序。
4. PlayerLoop 场景由 `SimulationSystemGroup` 在 FixedStep 子组之后调用一次 Drain；本 outer update 即使是 0 FixedStep 也仍 Drain 一次。已有 InFlight retry 复用同一 Batch，且不运行 Kernel、不增加 `SimulationTick`。
5. Headless/AutoChess 只能调用 session `TickBatch(...)`；不得直接更新内部 group 或 system。
6. logical phase 名称用于 Job、Profiler marker、counter 和 semantic hash，不用于恢复多组调度。

## Session、BattleInstance 与 Ready

v1 一个 World 同时只允许一个 active GAS Session tick domain；一个 Session 可以承载多个稳定 `BattleInstanceId`，例如 replicated scale profile 中的多个隔离战局。

Session 状态至少为：

```text
Install -> SpawnPending -> Ready -> Running
        -> Faulted -> Terminalizing
Running -> Terminalizing -> FinalDrain
                           ├─ failure -> FinalDrainBlocked -> retry FinalDrain
                           └─ accepted -> CleanupAudited -> Disposing -> Disposed
                                                        -> DisposedReceipt -> ValidationResultSeal
```

SpawnBatch 的原子性是 gameplay 可见性原子，而不是 ECB/OOM 的物理回滚承诺。进入 SpawnPending 前先验证 Catalog、稳定身份、初始 grant 与逻辑容量；setup update 的 EndFixed只创建 Pending Entity/Registry entry。下一 FixedStep由 Kernel `SpawnFinalize` maintenance lane全量查询校验，再 no-fail整体发布 handle并进入 Ready；该 update不递增 gameplay Tick，也不运行 gameplay lanes。失败批次从不进入 Ready lookup，Session进入 Faulted并teardown；禁止 Drain/runner在 post-EndFixed写 Ready。

Unit Death、BattleInstance Terminal 和 Session Terminalizing 是三个层级。单 BattleInstance 终局通过同一个 `SessionIngressGate` 的 per-Battle accept 状态关闭，只封闭该实例 ingress；Core terminal token 是 winner authority，Boundary registry 只是策略镜像。全部实例终局或显式 shutdown 后才终止 Session。Request ledger/per-Battle gate 只见 [16-02](16-纯ECS内核与边界重划分/16-02-BoundaryCommand与CoreCommandResolveSpec.md)，Session API 只见 [16-04](16-纯ECS内核与边界重划分/16-04-ShellCapabilityContractSpec.md)，shutdown/Disposed 的物理顺序只见 [03F](03-RuntimeCore管线/03F-StructuralCommit与BoundaryProjectionSpec.md)。wall clock、测量 warmup 与表现等待不得改变 gameplay tick、winner 或 hash。

## 单 Kernel 不是巨型单 Job

`GasTickKernelSystem.OnUpdate` 只负责解析 query、申请 tick-local container、组装 Job DAG 并返回最终 dependency。推荐 DAG：

1. `GatherTickInputJob` / `TickStartSnapshotJob` + `PlanExpandScratchProvision`：按 sealed/due count 与 bake maxima 先建立 checked envelope token和定长 Plan/Expand scratch
2. `OwnerPlanBuildJob`：按 ASC 在 scratch 中执行 canonical read-your-writes，不写权威状态
3. `TargetResolveExpandJob`
4. `WholeTickInfrastructureAdmissionJob`：验证 envelope token，只预留下游 scratch/slab/payload/pending/fact/outbox
5. `AscOwnerCommandWaveJob`：只执行已 admission 的 no-fail CommitPlan
6. `SourceSpecProjectionJob`
7. `GroupWorkByTargetJob`
8. `TargetPrepareJob`：在 tick-local shadow 中执行 application/stack/Attribute/Tag/Grant/stabilization/Death
9. `SessionFaultReduceJob`：归约所有 target 的 Ready/Fatal record
10. `TargetPublishJob`：持 publish token 无失败写 durable state
11. `StableFactMergeAndTerminalResolveJob`
11. `GroupNextTickRouteByDestinationJob`
12. `ProjectBoundaryAndDiagnosticsJob`

硬约束：

- tick scratch 只能由 Kernel 创建和拥有，不保存到下个 tick；其物理回收服从 World update allocator 生命周期。
- tick scratch 统一使用 `SystemState.WorldUpdateAllocator`；跨 tick 数据必须回到 ECS 持久 buffer/component。
- 禁止 phase 间为读取 length/结果调用 `Complete()`。
- 禁止 static NativeContainer、跨 System scratch、unsafe system ref 或 `NativeDisableParallelForRestriction` 掩盖 owner 错误。
- Job/evaluator 各自独立类型并可单测；“一个 System”不等于“所有业务写进一个方法”。
- OwnerPlan/admission 失败发生在任何语义 mutation 前；infrastructure capacity 不足使本 tick 权威零写并进入确定性 fault。
- AscOwnerCommandWave 到 `TargetPrepare -> SessionFaultReduce -> TargetPublish` 之间只有 JobHandle 依赖，不调用 `Complete()`。
- 普通 self-target GE 也进入 `TargetPrepare -> SessionFaultReduce -> TargetPublish`；必须在同 OwnerWave 影响后续 CanActivate 的内容只能建成 activation-owned invariant，否则 Definition bake fail。

## ASC identity 与 Owner/Avatar

ASC identity 必须稳定且与 raw Entity 分离：

```text
AscHandle = SessionId + AscStableId + AscGeneration
SlotHandle = AscHandle + SlotIndex + SlotGeneration
```

Actor 绑定分为：

- OwnerActor：控制者/所有者身份，不保存 Attribute、Tag、Ability、Effect 权威副本。
- AvatarActor：可替换的表现、Transform、Physics 绑定。

约束：

1. Avatar 销毁、重生或换身不销毁 ASC。
2. Core 内可缓存 Avatar Entity，但必须同时保存 stable avatar id 与 binding generation。
3. Cue/Boundary fact 携带 avatar binding generation 或空间快照，避免延迟消费绑定到新 Avatar。
4. Team/Faction/Alive 等 gameplay 数据进入 ASC unmanaged snapshot，Core 不回查 managed OwnerActor。
5. public handle、immutable batch 和 replay key 禁止携带 raw Entity。

## ASC-local 稳定 slab

长期生命周期数据统一放在 ASC：

- `GrantedAbilitySlot`
- `AbilityActivationSlot`
- `AbilityContinuationSlot`
- `AbilitySubscriptionSlot`
- `ActiveEffectSlot`
- `ActiveEffectPayloadSlot` / capture records
- `AttributeAggregatorModifierSlot`

每种 slab 必须：

1. 不执行 `RemoveAt`、`RemoveAtSwapBack` 或存活期 compact。
2. 删除时写 tombstone，并把 slot 放入 owner-local free-list。
3. 复用时 generation 递增；generation 溢出显式失败，禁止回绕。
4. 所有命令校验 ASC identity、slot index 和 generation。
5. terminal fact/outbox 写入完成后才能回收。
6. payload/capture range 不能引用会压缩的 companion buffer。
7. DynamicBuffer 引用不得跨 Job、结构变化或 tick 保存。

Tag count entry、临时 command 和已 drain fact 没有外部 slot handle时可按各自数据性质整理；“非压缩”专指有长期 identity/引用的生命周期 slab。

## 禁止 Definition -> Entity promotion

Ability Definition 与 GameplayEffect Definition 只能选择语义，不得选择物理 backend。

禁止原因：

- slot/entity 混用会让 stacking、inhibition、period、cleanup、hash、Debugger 和 save/replay 各有两条管线。
- 更改 Definition 配置会改变实例物理语义。
- 设计配置不应泄露 ECS 存储策略。

Projectile、Aura Volume、Zone 等具有独立 Transform/Physics 生命周期的对象可以创建派生 gameplay Entity，但：

1. 它们不是 Ability/ActiveEffect authority。
2. 它们只保存 stable correlation/spawn id。
3. stack、period、inhibition、granted tag/ability 仍只存 ASC slot。
4. 派生 Entity 销毁不能代替 slot cancel/remove。

## Definition -> Spec -> Active

### Definition

Definition 是 immutable Blob，至少独立保存：

- Application requirement
- Ongoing requirement
- Removal rule
- Immunity query
- modifier/capture descriptor
- stacking/overflow/period policy
- granted tag/ability policy
- Cue 四阶段 policy

禁止把不同 phase 压入一个无 phase 的 requirement range。

### Spec

每次 application 建立 Spec，至少包含：

- Definition index/version
- source/target stable identity
- source activation/effect correlation id
- level/context/parent context
- SetByCaller values
- target data/hit/origin 必要快照
- capture descriptors 与 snapshot values
- deterministic sequence/sort key

Instant Spec 在 apply 后终止；Duration/Infinite Spec 的必要快照进入 ActiveEffect slot。

### Active

ActiveEffect slot 是持续实例唯一权威，至少包含：

- effect instance id、slot generation、definition index
- source stable id / source activation id
- stack key/count
- Active/Inhibited/PendingRemove flags
- start/end/next period absolute tick
- capture/payload range
- granted contribution ledger

## Capture 语义

每个 capture descriptor 显式标记：

- Source 或 Target
- Snapshot 或 Live
- capture phase
- source/target 消失时策略

解释：

- Source Snapshot：OwnerPlanBuild 为每个计划在 shadow 中计算该计划成功 Commit 后的 capture candidate，只可见同 source 前序 CommitPlan，不可见后序计划或本 tick incoming target Effect；candidate 在 admission 前无权威身份，只有对应 Commit 成功后才由 `TargetPrepare` 前的 `SourceSpecProjection` 密封并持久化。
- Target Snapshot、application requirement 与 immunity：在每条 target application 线性化点解析，可见同目标前序 canonical application 已提交状态。
- 同 ASC Live：在 target-local stabilization 中以 Attribute revision 更新。
- 跨 ASC Live：source revision 在 T 生成 destination-grouped dirty command，T+1 由目标 writer 消费；缺少 consumer identity、两端 Generation、source-gone/cycle/budget 任一闭环时 bake fail。

禁止把 Live 解释为任意并行 Job 随时随机读取正在写入的 component。

## Activate / Commit / Cancel

### Activate

1. 解析 granted ability slot 与 concurrency/instancing policy。
2. 分配 Activation/Continuation slot 和稳定 ActivationId。
3. 执行 activation requirements 与 target resolve。
4. Activate 成功不自动等同 Commit 成功。

### Commit

1. 以 ActivationId 关联，并在 Commit 时重新检查 requirement、cost affordability 和 cooldown availability。
2. Commit 幂等；重复 Commit 返回 `AlreadyCommitted`，不得重复扣费。
3. CommitPlan 只包含 source-local `Committed state + cost + cooldown + activation-owned contribution`；先全量预检和 infrastructure admission，再一次 no-fail 提交。
4. 对远端多个 ASC 的 effect application 不是分布式原子事务；必须输出逐目标结果。
5. Commit failure 输出结构化 reason fact。
6. Commit 先于 Cancel 时 cost/cooldown 与已 Commit work 保留；Cancel/End 先于 Commit 时后者返回 `Rejected.OwnerEnding`。

### Cancel / End

1. Cancel/End 以 ActivationId 幂等。
2. 当 tick立即把 continuation 标记 terminal；迟到 completion 因 generation/state 不匹配被拒绝。
3. Commit 后退款只能由显式 compensation effect 定义。
4. 派生 projectile/aura 的物理销毁可延迟，但 gameplay 失效必须立即生效。
5. Activation 只拥有 activation-owned Tag/Block/Cue/Continuation；普通 emitted application 只作审计，不随 End 自动撤回。只有显式 `RemoveOnActivationEnd` 拥有精确 application/contributor cleanup。

## Continuation

Continuation 使用生成的封闭 tagged union，至少包含：

- ActivationId / AbilitySlotHandle
- ProgramCounter / CommitState
- WaitKind / WakeTick / CorrelationId
- 固定大小 generated payload 或稳定 payload slot

禁止 arbitrary managed task object、每个 AbilityTask 一个 Entity、无类型可无限扩张 byte blob。并行 task 可使用 child continuation slot，但仍由 owning ASC writer串行修改。

## Target-owned single writer

每 tick 先按 source ASC 形成 `AscOwnerCommandWave`，再按 Target AscStableId 形成 `TargetPrepare`，全部 Prepare 完成后执行唯一 `SessionFaultReduce`，成功才进入 `TargetPublish`。同一 ASC 在 owner/prepare/publish lane 内按 canonical key 串行，不同 ASC 可并行；各 lane 依赖串联。

OwnerWave 只看 tick-start 状态与本 ASC 前序 CommitPlan，不看本 tick 随后到达的 target Effect。TargetPrepare 在每条 application 线性化点重验 target life、binding、requirement、immunity 与 capture，并把正常 outcome 写入 shadow；只有 SessionFaultReduce 成功后才由 TargetPublish 发布。已 Commit operation 在 source 随后死亡时不撤回，但仍可能被目标以 typed reason 拒绝。

target identity 必须正交声明逻辑 ASC、Avatar binding、空间采样与 `TargetLifePolicy`；`FrozenSpatial` 不是失效 actor 的 fallback。Self 只能来自显式 SelfTarget rule，禁止 implicit fallback-to-owner。

canonical key 必须包含 schema/catalog-hashed `SemanticPhaseOrdinal/WorkClassOrdinal`，不能使用 Job/Profiler lane。通用语义顺序至少固定为 destination maintenance/live dirty → PeriodDue → Expiration → sealed remove/inhibit → committed application；其余稳定 identity/sequence 消除全部并列，相同全序 key 对应不同语义为 validation fault。

禁止多个并行 Job 通过 `BufferLookup` 随机写同一 ASC。跨 ASC 的二次派生 work 默认进入下一 tick；同 ASC 的状态稳定化留在当前 invocation。

## Requirement、Inhibition 与稳定化

- Application requirement：apply 前检查一次。
- Ongoing requirement：相关 Tag/Attribute version 变化后重评。
- Removal rule：明确区分 remove request gate 与自动 removal trigger。
- Immunity：独立 query/result，不复用普通 blocked requirement。
- Inhibition：保留 ActiveEffect instance，但撤销其有效 modifier/tag/ability contribution。

Tag、inhibition、aggregator 之间必须在同 target、同 tick稳定后才输出最终事实。使用显式 work queue、依赖 version 和 cycle/operation budget；检测到循环或预算溢出必须失败并给 evidence，禁止固定 pass 次数后静默截断。

## Stack / Overflow / Period

1. stacking key 显式包含 aggregate policy 所需 source/target/definition/stacking id。
2. overflow 输出 accepted/rejected/overflow-effect 明确结果。
3. duration、period 使用整数 absolute tick；不为所有 effect 每 tick递减剩余时间。
4. inhibition 时 period 的 pause/continue/reset 由 Definition 指定。
5. missed period 的 skip/single/catch-up 策略由 Definition 指定。
6. tick-start 已到期的 owner-local PeriodDue 在 DueTick 同 tick 执行；若 DueTick 与 EndTick 相同，Definition 必须明确先 period 还是先 expiry。
7. post-apply/overflow/reaction 动态 child 与跨 owner 派生 work 默认进入下一 tick；静态闭合有界 DirectEffectProgram 不受此规则影响。
8. execute-on-apply、payload replacement、refresh/reset、final period、inhibit/resume、missed-period 与 self-remove guard 均为独立 policy。
9. timing index 可以作为可重建加速结构，不能成为第二 authority。

## Attribute Aggregator

1. Effect apply/remove/inhibit/stack 只更新 contribution 与 dirty state。
2. owning ASC 统一按稳定 channel/key 顺序 recompute。
3. Add/Multiply/Divide/Override channel 顺序和 override tie-breaker 固定。
4. 浮点 reduce 使用确定性顺序。
5. Attribute Base/Current、Pre/Post hook、meta attribute 和最终 fact 从单一 apply lane产生。
6. 禁止 Instant、Execution、ActiveEffect 各自直接写最终 CurrentValue。

## Death crossing 与 terminal resolve

Death 是 Attribute apply 的同 target invariant，不是下一 tick reaction。第一次 `old Health > 0 && unclamped result <= 0` 必须冻结 RequestedDelta、Pre/PostClamp、UnclampedResult、EffectiveDelta、KillingApplication/Contributor/Causality 与 DeathTransitionId，并写不可行动 lifecycle latch。

致死 application 完成自身全部已 admission 节点；同 canonical range 后续 `AliveOnly` application typed reject，不能覆盖 killer，也不产生 applied damage、overkill 或 assist。所有 target job结束后，StableFactMerge 中的唯一 TerminalResolve 才按 BattleInstance稳定规则裁决双杀/平局；单位死亡不自动终止整个 Session。

## TagCount

Tag authority 是按 TagIndex 的 count，不是 presence bit：

1. grant/remove 更新 tag 与预计算 ancestor chain count。
2. `0 -> 1` 设置派生 presence mask，`1 -> 0` 清除。
3. underflow、overflow、重复撤销显式失败。
4. Effect contribution ledger 以 EffectInstanceId/generation保证恰好撤销一次。
5. presence mask 只作 query cache，不是第二事实源。

## tick 语义分类

“reaction 默认下一 tick”不包含状态稳定化。

### 当前 tick 必须完成

- tick 开始前已进入 ingress 的 Activate/Commit/Cancel。
- Commit 二次检查、owner-local cost/cooldown 和已解析 target application。
- TagCount、ongoing requirement、inhibition、aggregator 稳定化。
- Cancel 对 continuation 的 terminal 标记。
- 被声明为 lifecycle safety 的 threshold cancel/end。
- 最终 Core fact 生成。

### 默认下一 tick

- target-owned apply 中新产生的跨 ASC reaction。
- post-apply/overflow/reaction 动态 child work。
- 一般 Tag/Attribute event-triggered ability。
- 当前 tick才授予的新 Ability 的自动激活。
- kernel 执行中才到达的 completion。

任何例外必须写进 Definition/Runtime contract 和测试，不得依赖 System 恰好排序。

tick-start 已存在的 PeriodDue 是当前 tick 工作，不属于上表的动态 child。跨 ASC Wait 的 sample/register 由 observed writer 线性化；completion 可以在 emit tick 产生，但 Continuation resume 统一最早 T+1。

## 标准 EndFixed 与 Entity 可见性

1. ASC grant/remove ability/effect、commit/cancel、TagCount、Attribute、Aggregator、Outbox 都是非结构写。
2. ECB 只处理 ASC、Avatar、projectile、aura 等真实 Entity 生命周期。
3. EndFixed 创建的 Entity 不参与当前 Kernel，最早下一 simulation tick成为 Core 输入。
4. Core 不得让 Activate/Commit 成功依赖当 tick新建 Entity可查询。
5. Cancel 必须设置立即失效状态；如果 Physics 可能在 playback 前运行，物理系统也必须读取该状态。

## ASC destroy 与 Drain 交接

ASC outbox 使用 `ICleanupBufferElementData`，允许权威 ASC 在写完 terminal fact 后于同 tick EndFixed 销毁，而不丢失边界事实：

1. tick N：停止接收 gameplay work，完成 slot/dependency/subscription cleanup，把 terminal fact 写入 `BoundaryFactBuffer`，并向标准 EndFixed 记录 `DestroyEntity`。
2. EndFixed：普通 Component/Buffer 被移除；cleanup buffer 保留，Entity 变成不具备 `GasAscIdentity` 的 cleanup shell。
3. post-Fixed Drain：collect/sort 后由 managed staging 以 BatchId/InFlightWatermark 幂等接管；接管成功只清 `<=InFlightWatermark`，late tail保留，无 tail才把 shell标记为 `BoundaryDrainState.Accepted`。
4. 下一正常 tick 的 Kernel cleanup prepass 为 Accepted shell 记录本 tick 标准 EndFixed cleanup removal；post-Fixed Drain 不创建跨 batch 等待的 ECB。
5. 若 shutdown 后没有下一 tick，只能在完整 Tick DAG 与 EndFixed 结束、FinalDrain 成功接管并完成全部 producer 后，由 teardown 直接移除 cleanup state。

Registry/liveness 只能检查 `GasAscIdentity` 与 Generation，不能因 `EntityManager.Exists(shell)` 为真而把 shell 当成业务 ASC。

## Boundary 单 Drain 与 immutable ring

每条 Boundary fact 至少包含，且按 identity scope恰写一个物理 outbox owner（ASC-scope→ASC，Battle/Session-scope→Session）：

- SimulationTick
- BattleInstanceId / owner ScenarioUnitId
- FactScopeKind / ScopeStableId / ScopeGeneration
- `FactPlane=Gameplay/TeardownAudit`
- SemanticPhaseOrdinal / WorkClassOrdinal
- EventKind / semantic code
- source/target stable id + generation
- `BoundaryEventId=(Epoch, PhysicalOwnerKind/Id/Generation, OwnerSequence)`
- 可选 `CueLifecycleKey=(Epoch, ActiveEffectHandle, ActiveCycleOrdinal, CueDefinitionOrdinal)` 或独立 ExecutedCueKey
- AvatarBindingGeneration 或空间快照

`GasBoundaryDrainSystem` 是唯一 ECS consumer，并执行三类互斥 query：有 `GasAscIdentity` 的 live ASC、有 `GasSessionIdentity` 的 live Session、两种 live identity都没有但保留 `BoundaryDrainState` 的 cleanup shell。禁止仅用 `WithNone<GasAscIdentity>` 判 shell：

1. 在每次 outer Simulation update 的 FixedStep 子组之后收集 dirty ASC/Session scoped owner；0 FixedStep 也执行，已有 InFlight 先原 identity 重试。
2. 对本次稳定 source range冻结 `BatchId/per-owner InFlightWatermark`，并按 `(EventId.Epoch, Tick, FactPlane, ScopeKind, ScopeStableId, ScopeGeneration, SemanticPhaseOrdinal, WorkClassOrdinal, EventId)` 排序。物理 owner 的 `NextOwnerSequence` 在 Accepted→Idle 后仍持久单调，不从 buffer 重建。
3. managed staging 整 batch 预留并取得唯一所有权；失败时 ECS outbox 原样保留。
4. Accepted 后只清各 source `EventId.OwnerSequence <= InFlightWatermark`；live tail保留并回到 Pending，空 dead shell需以 physical owner/range 显式 NoFactReceipt，不伪造 Battle scope。随后标记可清理 shell，并发布 ring或机器可读 DroppedRange/Fatal receipt。
5. UI、Cue、Replay、Debugger、Headless validation 只消费 batch，不读取 ECS DynamicBuffer。

v1 不在 Core 实现 per-consumer ack：同步 consumer 在 batch 生命周期内读；异步 consumer复制自己的队列；Replay 自行持久化；late join 通过 snapshot reconcile。

Overflow：Validation/Headless 必须失败；Presentation 记录 dropped range并触发 snapshot reconcile；禁止静默丢弃或让 Core 等最慢 consumer。

每个 Battle 以双切面独立封印：本 Battle 的 `CoreOutboxCut` 与同 Gate 的 `GateRequestCut` 均完成、并且 ReadModel 已推进到对应 `SnapshotCut` 后，冻结 `BattleOutcomeSeal`；A 不等待同 Session 中仍运行的 B。`FactPlane=TeardownAudit` 仍交付 Replay/Validation，但不改变 winner/gameplay hash；它仍按 Asc/Battle/Session identity scope 路由，不是第四种 ScopeKind。Session 只有在 FinalDrain、cleanup audit、World/Blob/managed resource 释放并产生 `DisposedReceipt` 后才冻结 `ValidationResultSeal`；该 seal 后不得新增 gameplay、Boundary 或 teardown fact。双 cut、hash 与封印只见 [06](06-Observation-Presentation-ReplaySpec.md)，物理关闭顺序只见 03F。

## Cue 四阶段

Core 输出：

- OnActive
- WhileActive
- Executed
- Removed

约束：

1. `EventId` 表示一次投递；持续 Cue 使用包含 active-cycle identity 的 lifecycle key关联 OnActive/WhileActive/Removed。
2. Cue request 使用 CueCode、stable source/target、context、parameters 与 avatar binding generation。
3. Core 不创建/保存 Cue Entity、GameObject、resource handle 或 managed cue instance。
4. managed consumer负责 definition resolution、pool、resource 和生命周期。
5. Headless消费同一 batch，但不加载表现资源。
6. Duration Cue 的 lifecycle key 包含 Epoch、ActiveEffectHandle、ActiveCycleOrdinal 与 CueDefinitionOrdinal；inhibit 撤销当前 cycle，reactivate 开启新 cycle。
7. Executed 使用 ApplicationId 或 PeriodExecutionId；stack refresh 默认不开新 lifecycle cycle，BoundaryEventId 仅表示单条交付。
8. managed async callback 必须复核 Epoch、完整 lifecycle key、Avatar BindingGeneration 与 cancellation/consumer generation；Removed 先 tombstone/cancel 再释放，迟到 callback 不得复活旧 cycle。
9. snapshot reconcile 只按 `SnapshotCut` 恢复完整 active cycle 集合；Executed 不可重建，丢失 range 显式。Headless 走同一 ledger 但不加载资源；完整协议只见 06。

## SourceGenerator 边界

允许输出：

- immutable Blob schema/catalog/index
- static lookup
- pure evaluator/record builder
- closed tagged-union continuation payload
- baker/bootstrap glue
- validation artifact

禁止输出：

- `ISystem` / `OnUpdate` / SystemGroup registration
- query、ECB、NativeContainer owner
- EntityManager write
- Ability/ActiveEffect lifecycle
- target-owned writer或 Boundary consumer

Luban 是 authoring 语义唯一权威，编译后的 immutable Blob 是 Session 内运行快照。SourceGenerator 不得以 overlay/default 方式补写或覆盖 GE/Ability 语义；重复字段、非法 enum/range、缺失 provenance 与冲突 policy 必须 bake fail。

## 一次性切换规则

1. v1 使用独立集成分支完成 V1-V4，删除门通过前不合入稳定分支。
2. 不提供 old/new runtime selector、Definition backend flag 或兼容 fallback。
3. 新 Catalog schema、handle 和 batch protocol一次版本提升。
4. 旧 runtime types 可在集成分支短暂存在以支持编译迁移，但不得同时注册、同时写权威状态或进入发布构建。
5. 任一旧权威需要暂留时，任务状态只能是 blocked，不能标记兼容完成。

## 目标删除门

Release candidate 必须同时满足：

1. 五个旧 GAS physical group与自定义 Begin/End Structural ECB 无运行注册。
2. `GEExecutionCalculationExtensionSystemGroup` 无运行注册。
3. Ability Entity archetype、`AbilityStateComponent`、`AbilitySlotBuffer<Entity>` 和 Ability request marker退出运行链。
4. `LegacyGameplayEffectEntityBuffer`、legacy GE runtime entity factory与 legacy execution system退出运行链。
5. ActiveEffect global index/bucket/stable row不再是 authority；不存在 slot/global双写。
6. ActiveEffect/Ability/Continuation slab无 compact/remove-at。
7. EventBus 不再混合 Core command、reaction、observation 与 presentation。
8. Cue bridge不再要求 CueEntity。
9. AutoChess不直接更新旧 group，不注册自定义 Core damage System。
10. Boundary consumer不直接访问或清 ECS fact buffer。
11. Runtime public/boundary protocol无 raw Entity、World、EntityManager、NativeContainer。
12. SourceGenerator 无 lifecycle/query/ECB owner。

## 验收门

### 语义

- Activate/Commit 二次检查、Commit幂等、失败无部分 cost/cooldown。
- Owner/Avatar rebind 不改变 ASC authority，旧 Avatar Cue 不播放到新 Avatar。
- Source/Target × Snapshot/Live capture 四组合。
- application/ongoing/immunity、inhibit/reactivate、cycle detection。
- stacking by source/target、overflow、period pause/reset/catch-up。
- TagCount 多来源 grant/remove。
- Aggregator稳定顺序、override tie-breaker与 hook。
- Cue 四阶段与 lifecycle key。
- stale slot/activation/effect handle在复用后拒绝。

### 调度与结构

- 独立 World和 Headless runner均实际执行标准 EndFixed。
- ECB-created Entity不参与当前 Kernel。
- terminal fact 在 ASC destroy 前写入 cleanup outbox，并在 EndFixed 后由 live ASC 或 cleanup shell 完成 staging 接管。
- Kernel无 phase间 `Complete()`、无跨 System scratch、无跨 ASC随机并行写。
- 同一输入采用不同 bounded TickBatch 切分仍得到同一 semantic order/hash。

### Boundary

- 多 FixedStep catch-up不丢 tick。
- 多 consumer读取同一 immutable batch。
- ring overflow按 profile显式失败或重同步。
- Headless无 Presentation时仍 Drain。

### 确定性与性能

- 相同 seed/input重复运行 semantic hash一致；hash 排除 raw Entity、batch 切分、wall clock、Profiler/Journaling 与表现截断。
- 输入物理顺序变化后 canonical merge结果一致。
- replicated groups、hot-target fan-in、period burst、mass-death teardown、Boundary retry、wait fanout 与 cross-ASC live dirty profile 通过。
- Profiler/Journaling分别归因 Kernel、EndFixed、Drain和 managed consumer。
- 不能用旧五组 timing、字符串日志或单次 x50替代完成证明。
