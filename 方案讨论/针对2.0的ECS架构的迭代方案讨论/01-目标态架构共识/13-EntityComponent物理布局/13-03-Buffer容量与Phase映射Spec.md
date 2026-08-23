# 13-03：Buffer 容量与 Lane 映射 Spec

> 状态：v1 目标态
> 说明：文件名沿用 Phase；v1 物理执行为单 Kernel Job DAG，表中均指逻辑 lane

## 1. 结论

Buffer 的“逻辑长度/上限”和“InternalBufferCapacity/初始容量”是两件事：

- Attribute、Tag 与对应 bitset 的逻辑长度由 Session Layout/Catalog 决定，ASC 出生后固定。
- Ability、Activation、Continuation、Subscription、CooldownGate、Contribution、Effect、Aggregator 与 LiveDependency slab 使用 non-compacting high-water + free list，可增长但不移动 live slot。
- ActiveEffect/Continuation 等 variable payload 使用 non-compacting range + generation，可复用 free range但不移动 live range。
- BoundaryCommandInbox、Pending Command 与 Boundary outbox 是三类持久队列，分别服务外部 ingress、内部 T+1 route 与 managed drain，不得合并 owner/生命周期。
- IBC、预热容量、scratch 初始容量与报警阈值全部由 ScaleProfile 测量；本 Spec 不写死数值。
- IBC/初始容量不是逻辑 capacity。admission 前已需写入的 Plan/Expand scratch 由 Gather 根据 sealed/due count + bake maxima 产生并验证 `PlanExpandScratchEnvelopeToken`；`WholeTickInfraAdmission` 仍是唯一权威准入，它验证 token 并在本 Tick 任何 gameplay 权威写前一次完成 downstream/durable 逻辑预算与物理预留。
- Target mutation 在 `TargetPrepare` 使用 tick-local shadow/intent ranges，`SessionFaultReduce` 成功后才由 `TargetPublish` 写 durable Buffer。shadow credit 与 durable publish credit 必须分别预留并同时计入峰值；fatal 时丢弃全部 target shadow，但保留 OwnerWave committed prefix。

## 2. Buffer 总表

| Buffer | 逻辑长度/增长规则 | 主要写 lane | 消费/读取 | 生命周期 |
|---|---|---|---|---|
| `AttributeValueSlot` | 固定为 AttributeLayout count；Base/Current/Revision | OwnerWave 的 Cost contract + TargetPrepare shadow/TargetPublish durable、Dirty Finalize | 计算、查询、快照/live dirty | ASC |
| `AttributeDirtyWord` | 固定为 Layout word count | Apply/Finalize | Finalize/调试 | ASC；派生 |
| `TagCountSlot` | 固定为 TagCatalog count | OwnerWave 的 gate/owned contribution + TargetPrepare shadow/TargetPublish durable | requirement/tag query | ASC |
| `TagPresenceWord` | 固定为 Catalog word count | Dirty Finalize | 匹配查询 | ASC；派生 |
| `GrantedAbilitySlot` | high-water + free list；不压缩 | direct command: OwnerWave；GE-derived: TargetPrepare shadow/TargetPublish durable | activation/查询/grant cleanup | ASC |
| `AbilityActivationSlot` | high-water + free list；不压缩 | OwnerWave；GE grant child cleanup 可由同 ASC TargetPrepare/Publish | ability/continuation/End | owner ASC |
| `ContinuationSlot` | high-water + free list；不压缩 | owner command lane；GE grant child cleanup 可由同 ASC TargetPrepare/Publish | 后续 Tick resume | ASC |
| `AbilitySubscriptionSlot` | high-water + free list；不压缩 | observed ASC owner lane；外部 cleanup 仅接收 bounded cancel intent | event/wait 投递/cleanup Ack | observed ASC |
| `CooldownGateSlot` | high-water + free list；不压缩；`EndTick`/explicit policy前持续 | owner command wave | CanActivate/cooldown due maintenance/Tag contribution cleanup | owner ASC |
| `ActivationOwnedContributionSlot` | high-water + free list；不压缩 | OwnerWave；GE grant child cleanup 可由同 ASC TargetPrepare/Publish | Activation End cleanup | owner ASC |
| `EmittedApplicationRefSlot` | high-water + free list；不压缩；`AuditOnly/CleanupRight` 分型；保存 RetentionState/TerminalAckGeneration/AuditHandoffWatermark | owner command wave | AuditOnly 在 target terminal Ack + audit handoff 后释放；CleanupRight 在 cleanup terminal Ack 后释放；禁止静默淘汰 | owner ASC |
| `ActiveEffectSlot` | high-water + free list；不压缩 | TargetPrepare shadow/TargetPublish durable | period/ongoing/aggregate | target ASC |
| `ActiveEffectPayload/Capture` | variable range + generation；不压缩 live range | SourceSpecProjection/TargetPrepare shadow/TargetPublish durable | effect/capture evaluator | target ASC |
| `AggregatorSlot` | high-water + free list；不压缩 | TargetPrepare shadow/TargetPublish durable | Attribute finalize | target ASC |
| `LiveDependencySlot/RouteSlot` | high-water + free list；不压缩；route payload 原子保存 `{Revision,FrozenProjection}` | destination TargetPrepare / observed source owner | Revision dirty route/coalesce/Ack | 两端各自 ASC |
| `BoundaryCommandInbox` | append/seal/consume；跨 render/fixed tick；`RequestKey + PayloadHash` 幂等并冻结 `AssignedAvailableTick` | 唯一 ingress | Gather/TickStartSnapshot | Session |
| `PendingCommand` | append/consume；跨 Tick | ingress/reaction | 下一 Tick freeze | ASC/Session |
| `BoundaryFactBuffer` | append；按 accepted watermark prefix 清，late tail 保留；shell 在下一 EndFixed 移除 | Boundary Projection | 单 managed drain | ASC + Session scoped cleanup 生命周期 |

`BoundaryDrainState` 是 Session 与 ASC 都预挂载的 cleanup Component，不是 Buffer；它保存 frozen physical owner identity、持久不重置的 `NextOwnerSequence`、`Idle/Pending/InFlight/Accepted` 与 staging `BatchId/InFlightWatermark` receipt。Session state 没有单一 Battle identity；每个 fact 自带 Battle identity。Tick-local resolved commands、buckets、sort keys、fact partitions 不是 DynamicBuffer；它们由 Kernel 使用 `WorldUpdateAllocator` 创建。

`EmittedApplicationRefSlot` 的 retention 不是“Activation 活多久就无限保留多久”：

- `AuditOnly`：成功 Commit 后保留到 target 以 `EffectApplicationId` 产生 terminal ApplicationOutcome Ack，且 immutable attempt/outcome 已进入由 Boundary receipt 保护的 audit handoff watermark；之后释放 owner 热槽，审计数据由 Session/Boundary retention owner 承担。
- `CleanupRight`：用于 `RemoveOnActivationEnd`。Activation End 前保持 cleanup right；End 后生成 cancel/remove work，直到 target 返回 cleanup terminal Ack 才释放。Rejected、已自然 terminal 或 stale target也必须返回强类型 terminal Ack，不能靠超时猜完成。
- target 只能经 generation-checked、T+1 `PendingCommand` 返回 terminal Ack，不得跨 ASC直接写 owner ref。Session-fatal 时，`CommittedPrefixHash` 与 fault audit handoff 终结未完成 AuditOnly；CleanupRight 由 Terminalizing cleanup/ack 闭环终结。
- [25](../25-配置语义编译契约与CapacityProof统一裁决Spec.md) 必须生成 `MaxRefsPerActivation`、`MaxRefsPerASC`、`MaxPendingTerminalAcks`、`AuditRetentionWatermark/Duration` 与 cleanup work；WholeTick admission 在 Commit 前消费。达到上界只能 Session-fatal，禁止静默丢 ref、覆盖 live slot 或无限扩展。

## 3. 固定逻辑长度 Buffer

### 3.1 Attribute

`AttributeId -> index` 只由 `AttributeLayout` Blob 决定。每个 ASC 的 `AttributeValueSlot` 长度必须精确匹配 Layout；不允许缺省属性用“没有元素”表示，也不允许运行时追加自定义 Attribute。

`Base`、`Current` 与单调 `Revision` 是唯一权威。Revision 在该属性发生语义变化时递增，用于 Live capture dirty 判定；Dirty words 只用于增量 finalize/快照，可从变更或全量比较重建。

### 3.2 Tag

`TagId -> index` 只由 `TagCatalog` Blob 决定。`TagCountSlot` 保存 exact/inclusive 两类计数；presence/ancestor words 的逻辑长度由 Catalog 的 bit count 推导。

运行时 Tag grant/remove 不改变 Buffer 长度。需要新 Tag 必须生成新 Catalog/Session 哈希，不能热插入破坏索引。

## 4. slab 容量与句柄

每个 slab 都需要：

- Buffer 的 high-water 长度；
- free-head/空槽链；
- 每槽 generation；
- live/free 状态；
- 可选的 profile/debug 计数。

分配：

1. 有 free slot 时复用并递增/校验 generation；
2. 无 free slot 时扩展 high-water；
3. 返回 `slot index + generation`；
4. 不对 live slot 做 swap-back/compaction。

逻辑上不写死“最大 Ability/Effect 数”。工程上必须由 ScaleProfile 给出预热、内存预算、扩容报警与产品级拒绝策略；拒绝必须显式，不得覆盖 live slot。

所有跨系统/跨 Tick 槽句柄统一为：

```text
OwnerAscHandle = (AscStableId, AscGeneration)
StableSlotHandle = (SimulationEpoch, OwnerAscHandle, SlotIndex, Generation, Kind)
PayloadRangeHandle = (SimulationEpoch, OwnerAscHandle, Offset, Length, Generation, PayloadKind)
```

variable payload/capture store 的 live range 不做 compaction；释放只登记 free range 并递增 generation，复用前验证 kind/owner/epoch。禁止保存指向 Buffer 内存的跨 Tick pointer，也禁止因整理碎片移动 live range。

## 5. Lane 访问映射

| Lane | 只读 | 可写 | scratch |
|---|---|---|---|
| Gather/TickStartSnapshot + PlanExpandScratchProvision | Session tick/config、durable inbox/ASC state、Catalog maxima、ScaleProfile | 无持久写；冻结输入并在 Job 写前 provision plan/expand envelope | Tick frozen input/snapshot、envelope token/fault candidate |
| OwnerPlanBuild | Tick-start snapshot、同 ASC 前序 shadow plan、Blob | 无 gameplay 权威写 | CommitPlan、shadow RYW、生成上界 |
| TargetResolve/Expand | CommitPlan、PostPhysics sample、verified program | 无持久写 | bounded target/effect ops |
| WholeTickInfraAdmission | 全部上界、PlanExpandScratchEnvelopeToken、ScaleProfile 逻辑预算、slab/queue 元数据 | 仅 shadow/durable 双份物理 capacity/reservation 与失败时 Session Fault latch | admission token/ranges |
| AscOwnerCommandWave | admitted CommitPlan + due cooldown maintenance | Activation/Continuation/Subscription/OwnedContribution/EmittedRef、Cost Attribute mutation、CooldownGate/Tag contribution | committed source work |
| SourceSpecProjection | 已 Commit 计划与其 shadow post-commit capture candidate、committed source identity/live policy | 无持久写 | 仅在对应 Commit 成功后密封 immutable source spec |
| GroupByTarget | admitted resolved ops、CapacityProof weights | 无持久写 | target ranges/canonical keys/`TargetWorkUnits` |
| TargetPrepare/Stabilize/Death | OwnerWave 后 target state、canonical range、source projection、shadow/durable reservations | 只写 shadow slot image/overlay/payload 与 fixed Ready/FaultCandidate record；零 durable 写 | prepared delta、death/fact/cue/route/ECB intents |
| SessionFaultReduce | 全部 target Ready/Fatal record、sealed input hash、CommittedPrefixHash | 仅 publish token 或固定大小 Session fault latch | deterministic winner key/semantic hash |
| TargetPublish | publish token、PreparedTargetDelta、预分配 durable slot/range | target Attribute/Tag/Effect/Grant/Activation cleanup/Aggregator/LiveDependency/ledger；no-fail | published facts/intents |
| StableFactMerge/TerminalResolve | 已发布 facts/death candidates、BattleInstance registry | per-BattleInstance terminal state；全部实例终局或显式 stop 时推进 Session Terminalizing；Session fault 仍可独立写入 | merged Core Facts |
| GroupNextTickRouteByDestination | next-tick reaction/live dirty | 已预留 PendingCommand ranges | destination ranges |
| BoundaryProject | merged Facts | 已预留 scoped cleanup outbox ranges；ASC facts→ASC，Battle/Session facts→Session | routing indices |
| Record EndFixed | admitted spawn/destroy intents | parallel standard EndFixed ECB | ECB stream |

同一 target 的 Prepare/Stabilize/Death 由一个逻辑 shadow writer 负责，Publish 由同 target durable writer消费其 prepared delta；不同 target 并行。不得让多个 Job 通过 BufferLookup随机写同一 target，也不得在 `SessionFaultReduce` 前暴露 shadow。

OwnerPlanBuild 只读 Tick-start 与本 ASC shadow 前序 CommitPlan，不读本 Tick incoming target effect；普通 self GE 也进入 TargetPrepare。语义排序使用 schema/catalog-hashed `SemanticPhaseOrdinal/WorkClassOrdinal`，至少冻结 `DestinationMaintenance/LiveDependencyDirty -> PeriodDue -> Expiration -> SealedRemove/Inhibit -> CommittedApplication`；它绝不能取 physical Job lane。完整 key 冲突且 work 不等价时 validation fault。Cost/Cooldown 的支持矩阵、诊断码与 CapacityProof 只引用 [25](../25-配置语义编译契约与CapacityProof统一裁决Spec.md)，本布局 Spec 不复制。

## 6. same-tick 与队列

- ingress cutoff 前的 Command 可进入当前 Tick。
- Ability 直接输出和 verified closed/finite/bounded pre-apply program 可在 DAG 内 same-tick 到达 Apply。
- Apply 后产生的 Gameplay Fact reaction append 到下一 Tick `PendingCommand`。
- EndFixed 新结构下一 Tick 才进入 GAS 读取域。
- Boundary Fact 只在固定步进批次后由 managed drain 消费，不回灌 Core。
- source 后续死亡不撤回 OwnerWave 已 Commit 的远端 work；target `AliveOnly` 在每条 application 线性化点检查，首死后的后续 work typed reject。

## 7. 整 Tick admission 与事务边界

OwnerPlanBuild/TargetResolve/Expand 的 variable scratch 在准入前已写，因此 Gather 必须先以 sealed/due count、tick-start handle→Definition lookup 与 Catalog bake maxima 用 checked arithmetic 计算/ provision `PlanExpandScratchEnvelopeToken`。逻辑超限时 Plan/Expand 预排 no-op，由后续唯一 admission 提升 fault；物理 allocator/OOM仍为 fatal environment failure。

`WholeTickInfraAdmission` 必须在 OwnerPlanBuild/TargetResolve/Expand 已得到完整生成上界后、任何 gameplay 权威写前执行：

1. 先验证 PlanExpandScratchEnvelopeToken，再读取 [25](../25-配置语义编译契约与CapacityProof统一裁决Spec.md) 的 per-definition 上界，计算所有 downstream scratch、shadow overlay/payload/capture/fact/cue/route/ECB intent、durable slab/free range（含 Cost/Cooldown、Effect/Grant/child cleanup/Live/EmittedRef）、PendingCommand、fact partition 与 Boundary outbox 的本 Tick上界。
2. 分别验证并预留 shadow credit 与 durable publish credit；二者必须同时覆盖峰值。对支持显式扩容的 owner/target store 完成物理 capacity/range 预留；对 ECB 只冻结逻辑 command count/token。预留不发布 live slot、权威 Attribute/Tag 或 gameplay fact；宿主 allocator/OOM 属于 fatal environment failure，不伪装成可恢复 `InfraAdmissionFault`。
3. GroupByTarget 用 checked arithmetic 汇总每 bucket `TargetWorkUnits`，覆盖 evaluator、stack/ledger、最大 stabilization transition、grant-child cleanup、Live fanout 和 intents。超过 ScaleProfile `MaxTargetWorkUnitsPerTick`、溢出或 proof 缺失时，在 OwnerWave 前 Session-fatal；禁止将 hot bucket time-slice 到下一 Tick。
4. 下游 Job 已预排在同一 DAG；任一可准入资源失败时读取 `AdmissionResult` 后统一 no-op，整 Tick gameplay 权威零写，`FaultLatchJob` 只写固定大小 `SessionFaultLatch=Detected` 与 sealed subset 证据。失败 Tick 不向可能正是耗尽资源的 Fact/outbox写逐 request gameplay回执；outer completion 后 FaultClose 与 CommandPort accept在同一 gate线性化，将关闭点前 accepted-outstanding（含 unsealed/future tail）的 first/last/count/hash摘要写入 IngressClosed latch，精确成员由 Request ledger判定并逐项终结。
5. admission 成功后 CommitPlan、TargetPrepare 与 TargetPublish 不得再因容量不足失败。Prepare 中的 stabilization/identity/proof fatal 由 SessionFaultReduce 丢弃全部 target shadow；Owner committed prefix 保留，Publish 不运行。

该契约不建立跨 target **业务**事务。一个多目标 Effect 中目标 A 成功、目标 B 因 requirement/life policy typed reject 是合法 prepared result，二者在零 fatal 时一同发布；只有 v1 Session-fatal 才丢弃全部 target shadow。Boundary managed staging 发生在 gameplay 已提交之后；staging 失败不回滚 gameplay，而是保留 ECS outbox 并阻止 Session `FinalDrain -> Disposed`。

## 8. IBC 与 chunk 布局

`InternalBufferCapacity` 影响 chunk 内布局和 overflow 分配，不是逻辑限制。选择流程：

1. 使用代表性 ScaleProfile 采样长度分布、高水位和 chunk utilization；
2. 分别评估固定大 Buffer 与 slab/队列，不把同一个 IBC 套给所有类型；
3. 比较 chunk 密度、遍历 locality、overflow 分配/内存与结构创建成本；
4. 固化到 profile/代码并记录基线 commit；
5. Catalog/Layout 或玩法分布改变后重测。

不能仅因“放 chunk 内更快”就把 Attribute/Tag 大 Buffer 全部内联；也不能无测量把 IBC 设为零。

## 9. ScaleProfile 必采指标

- 每种 Buffer 的长度分位、高水位、扩容次数；
- slab live/free ratio 与 stale-handle 拒绝；
- chunk 内/外字节、chunk utilization；
- Pending Command/Boundary outbox backlog、EmittedRef AuditOnly/CleanupRight live/age/watermark 与 terminal Ack 延迟；
- target bucket 长度、`TargetWorkUnits` 分位/最大值和热点 target；
- scratch 峰值、target shadow/durable publish 两类 credit 的预算/实耗/峰值及 allocator 分配次数；
- `MaxFixedTicksPerBatch`、`MaximumDeltaTime`、outer batch 实际 Tick 分布、N×scratch/fact 与 double-rewind 高水位；
- admission 上界/实际用量/失败资源，以及 admission 后容量分支计数（必须为零）；
- stabilization 迭代/work 分布、FaultCandidate/SessionFaultReduce winner 与全 shadow discard 计数；
- ECB 命令数与主线程同步原因。

fixed-rate catch-up 的 group allocator 不是每个 SimulationTick rewind；一次 outer batch 中的 Tick scratch 即使逻辑失效，物理占用仍可累积。预算还必须覆盖 period/hot-target/mass-death/boundary burst。Headless 整局不得放进一次 outer batch，必须按 profile 上限分批更新完整 FixedStep 父链。

Profile 定义具体门槛；Spec 只规定若出现静默截断、live slot 被覆盖、固定 Buffer 热路径 resize 或 cleanup backlog 无界，则直接失败。

## 10. 验收

- 不同 Catalog/Layout 下 spawn 的固定 Buffer 长度精确且哈希匹配。
- 属性/Tag 变更不触发 resize/结构变化。
- slab 高水位扩展和 free-slot 复用保持其他句柄稳定。
- live payload range 不移动；Epoch/Owner/Kind/Generation 任一不匹配均拒绝。
- reaction 明确推迟一 Tick，closed pre-apply DAG 可复现 same-tick。
- admission 失败时整 Tick gameplay 权威零写；成功后无容量型部分 mutation。多目标 requirement reject 仍按逐 target 业务原子保留。
- outbox 在 live、同 Tick destroy、headless shutdown 三类场景不重不漏；staging 失败保留 outbox 并阻止 FinalDrain。
- 标准与 manual World 都由完整 FixedStep 父链驱动，不假设 allocator 每 Tick rewind，headless batch 不越过 profile 上限。
- `R3-STB`：一个 target 振荡时，全 target shadow/intents 丢弃、Owner committed prefix 保留；worker/batch 切分不改变 FaultCandidate winner/hash。
- `R3-GRT`：grant child/Continuation/Subscription/owned cleanup 的 shadow/durable credit N 成功、N+1 写前 Session-fatal。
- `R3-REF`：长 Activation 高频发射经 terminal Ack/audit handoff 回收热槽；Boundary/target Ack 延迟时不早释，达到硬上界显式 fault。
- `R3-HOT`：单 target work N 成功、N+1 在 OwnerWave 前失败；不得靠跨 Tick time-slice 通过。
