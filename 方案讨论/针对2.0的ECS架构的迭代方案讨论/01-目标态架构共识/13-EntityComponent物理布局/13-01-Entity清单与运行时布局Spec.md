# 13-01：Entity 清单与运行时布局 Spec

> 状态：v1 目标态
> 设计原则：Entity 表示独立身份/查询/生命周期；高频细粒度 GAS 实例保留在 ASC-local slab

## 1. Entity 清单

| Entity | 是否长期 | 职责 | 不承载 |
|---|---:|---|---|
| GAS Session Entity | 是 | 唯一 Tick domain、Epoch/Tick Rate/SimulationTick、生命周期、BattleInstance/ASC registry、规则与 Catalog/Layout/Definition Blob、Battle/Session Boundary outbox | 每 ASC gameplay 状态 |
| ASC Entity | 是 | 稳定 authority owner与 BattleInstance member；Attribute、Tag、Ability/Activation/Continuation/Subscription、Effect/Aggregator/LiveDependency、Command、Boundary outbox | UnityEngine.Object |
| Integration Request Entity | 可选、短期 | 仅作为外部 ECS adapter送入同一 CommandPort/SessionIngressGate journal，消费后销毁 | 直写 ECS inbox、Ability/Effect 权威状态 |
| Derived Gameplay Entity | 按需 | projectile/hitbox/aura volume 等独立 Transform/Physics/查询对象 | 替代 ActiveEffect slot |
| ASC/Session Cleanup Shell | 短期 | outbox owner Destroy 后仅保留 cleanup outbox 与 BoundaryDrainState，等待单 drain/prepass | 业务存活身份 |

Definition、Catalog 与 Layout 是 Blob，不因静态内容被烘焙成“每个定义一个运行时 Entity”。

## 2. Session Entity

建议固定形态：

| Component | 内容 | 写入 |
|---|---|---|
| `GasSessionIdentity` | `SimulationEpoch`、Session stable id | 创建时一次 |
| `GasSessionConfig` | Tick Rate、ScaleProfile id、规则版本 | 创建时一次 |
| `GasDefinitionRegistry` | Blob 引用与哈希 | 创建时一次 |
| `GasCatalogRegistry` | Attribute/Tag Catalog/Layout Blob 与哈希 | 创建时一次 |
| `SimulationTickState` | 当前整数 Tick、稳定序号根 | 每固定 Tick 单写 |
| `GasSessionLifecycle` | `Install/SpawnPending/Ready/Running/Terminalizing/FinalDrain/Faulted/Disposing/Disposed`；`SpawnBatch` 仅为操作名 | Kernel/teardown 状态机单写 |
| `BattleInstanceSlot[]` | 稳定 `BattleInstanceId`、BattleLocalTick、ingress/lifecycle、member range/count、terminal/outcome state | Kernel SpawnFinalize/TerminalResolve 与 teardown 单写 |
| `AscRegistrySlot[]` | `OwnerAscHandle`、内部 Entity、BattleInstance handle、SpawnBatchId、`Pending/Ready/Tombstone`；非压缩 | SpawnFinalize/teardown 单写；Gather 只发布 Ready entry 到 tick-local lookup |
| `SessionFaultLatch` | FaultId/Epoch/FaultTick/Reason、Detected/IngressClosed、sealed subset 证据、fault-close 时 accepted-outstanding first/last/count/hash | `FaultLatchJob` 写 Detected；DAG 完成后 Boundary `FaultCloseHandshake` 只终结 fixed-size 控制状态 |
| `BoundaryCommandInbox[]` | Epoch、RequestKey、PayloadHash、RequestSequence、ProducerSourceSequence、Source/Battle stable identity、AssignedAvailableTick、generated unmanaged payload/range | pre-Fixed `GasCommandIngressSystem` 唯一 append；Kernel Gather seal/consume 期间零 writer |
| `BoundaryFactBuffer[]` | BattleInstance/Session-scope final facts；元素为 cleanup buffer | BoundaryProject 单写；单 managed drain 接管 |
| `BoundaryDrainState` | Session outbox 的 frozen physical owner identity、持久 `NextOwnerSequence`、Idle/Pending/InFlight/Accepted、BatchId/InFlightWatermark；Session owner 不伪造唯一 Battle identity | Kernel projection/prepass + managed drain 协议写 |

一个 World 同时只能有一个 active GasSession Tick domain。Session 建立后 Epoch、Tick Rate 与规则哈希不可变；需要切换规则时完成旧 Session 的 FinalDrain/Disposed 后创建新 Session/World，而不是热改使已存在 duration/period 含义变化。

`BattleInstanceSlot` 是 Session-local 非压缩 registry：slot identity/generation 与稳定 `BattleInstanceId` 分离，单战局 Terminal 只封闭自身 ingress。只有全部 live slot 已 Terminal，或收到显式 stop，才允许 `GasSessionLifecycle` 进入 Terminalizing。成员关系由 ASC 的 `AscBattleMembership` 指向稳定 BattleInstance slot；v1 Ready 后不支持把 live ASC静默迁移到另一战局。

`BoundaryCommandInbox` 是跨 render frame/跨 fixed tick 的 Session 持久队列，不是 WorldUpdateAllocator scratch，也不同于内部 T+1 `PendingCommand`。0 fixed tick时 Boundary journal 与 ECS inbox 都保留；pre-Fixed ingress window 关闭后，Gather只 seal `AssignedAvailableTick<=CurrentTick` 的稳定 membership，并保证每个 `RequestKey` 恰好消费一次。Entities 依赖按整个 buffer 跟踪，不存在 Kernel 读 prefix 时 CommandPort 合法并发写 tail 的路径。

SpawnBatch 不承诺 ECB 物理回滚，只承诺 gameplay 可见性原子。setup update S 只记录 Pending ASC 和 Pending `AscRegistrySlot` 到标准 EndFixed；playback 后不允许 Drain/runner 写 Ready。下一次完整 FixedStep 中，`GasTickKernelSystem` 运行 `SpawnFinalize` maintenance lane：它对该 Batch 全量查询，并在 scratch 建立 canonical `SpawnInitializationTransaction`，先校验实体数、固定 Buffer 长度、hash、cleanup 类型，再对 shadow 按配置 ordinal 应用 Attribute init、initial tags、default grants 和 initial effects。initial effect 复用正式 requirement/capture/stack/contribution/grant/Cue/fact 纯 evaluator，但 v1 只允许 self-target、生成期闭合且静态有界的 bootstrap program；跨 ASC、空间目标、Live capture、动态 reaction/结构 child 必须 bake fail。全批 shadow 结果、slab/payload/outbox 上界与所有 typed outcome 均成功后，才以 no-fail 单 writer 一次拷贝权威状态、发布 initial fact/Cue，并提交全部 `AscLifecycle=Ready`/Registry Ready；任一失败则无成员可见，Session→Faulted并记录整批销毁。

`SpawnInitializationTransaction` 冻结 `ReadyTick = CurrentSimulationTick + 1`；initial duration/period/cooldown 的 Start/Due/EndTick 都以 ReadyTick 为起点，maintenance update 不消耗一个 gameplay Tick。它可复用 gameplay 纯 evaluator，但不运行 Gather/Owner/Target Job DAG，不接收 ingress；initial `FactPlane=Gameplay` 事实在 Ready 同一 no-fail publish 后才可 Drain。首个 gameplay Tick 从下一次 FixedStep读取全量 Ready Registry。

## 3. ASC Entity 固定布局

### 3.1 固定 Component

- `GasAscIdentity`：Session id、ASC stable id、生命周期 generation。
- `AscBattleMembership`：BattleInstance handle/id、稳定 `ScenarioUnitId`、side/team 与 membership ordinal；Ready 后不可变，所有跨单位 target/terminal 分组以此为准。
- `AscLifecycle`：`Pending、Ready/Alive、Terminal、DestroyPending` 等业务生命状态；不能以 `Entity.Exists` 推导。
- `AscActorRefs`：OwnerActor 与 AvatarActor 的稳定引用；允许分离和换 Avatar。
- `AscRandomState`：确定性随机状态/序列根。
- `AscSlabHeads`：各 slab/payload range 的 free-head/high-water 等元数据。
- `BoundaryDrainState`：冻结 ASC outbox physical owner identity及 `Idle/Pending/InFlight/Accepted + BatchId/InFlightWatermark`，并保持不回绕的 `NextOwnerSequence`；ASC Destroy 后仍供 drain/prepass 与 NoFactReceipt 清理。Battle membership 只在事实元素中冻结，不是 drain owner key。
- 可选的预挂载 enableable work marker；只做派生调度提示。

可空语义用 null/invalid 句柄表示，不因 Owner/Avatar/工作状态增删 Component。

### 3.2 固定逻辑长度 Buffer

| Buffer | 逻辑长度 | 权威字段 |
|---|---|---|
| `AttributeValueSlot` | AttributeLayout count | Base、Current、Revision |
| `AttributeDirtyWord` | Layout 计算的 word count | 派生 dirty mask |
| `TagCountSlot` | TagCatalog count | ExactCount、InclusiveCount |
| `TagPresenceWord` | Catalog 计算的 word count | 派生 presence/ancestor bitset |

spawn 时按照 Session Blob 一次初始化。热路径允许改元素，不允许改逻辑长度。Attribute 不生成一属性一 Component，也不存在 generated component 镜像。

### 3.3 长期 slab Buffer

| slab | owner | 典型字段 |
|---|---|---|
| Granted Ability | ASC | definition id、generation、grant state、activation policy/counters |
| Ability Activation | owner ASC | granted handle、generation、commit/end/cancel state、program state、owned/emitted range refs |
| Ability Continuation | owner ASC | activation handle、generation、program counter、wake tick、wait payload range |
| Ability Subscription | observed ASC | recipient activation/continuation handle、generation、wait policy、recipient sequence；晚到投递双重 generation 校验 |
| Cooldown Gate | owner ASC | GateKey/AbilityDefinitionId、source grant/activation/commit provenance、generation/state、StartTick/EndTick、`RejectWhileActive + ExpireOnly` policy、owned Tag contribution range |
| Activation Owned Contribution | owner ASC | activation handle、contribution kind/handle、生命周期 policy；与 emitted application 引用分离 |
| Emitted Application Ref | owner ASC | activation handle、TargetAscHandle、EffectApplicationId、audit retention与cleanup policy；成功 Commit时写入，不等待TargetPublish outcome/ActiveEffectHandle跨 ASC回写；只有显式 `RemoveOnActivationEnd` ref随End生成remove work |
| Active Effect | target ASC | definition、source/target handle、generation、stack、period/end tick、inhibition、payload/capture/aggregator range |
| Active Effect Payload/Capture | target ASC | 版本化 unmanaged variant、SetByCaller/Context、source snapshot、target capture；以 non-compacting range 引用 |
| Aggregator | target ASC | attribute/channel/contribution、dirty/revision；只由 target writer 修改 |
| Live Dependency | target ASC + observed source route | source Attribute revision、destination effect handle、dirty route generation；跨 ASC 只投递 destination work |

统一句柄：

```text
OwnerAscHandle = (AscStableId, AscGeneration)
StableSlotHandle = (SimulationEpoch, OwnerAscHandle, SlotIndex, Generation, Kind)
PayloadRangeHandle = (SimulationEpoch, OwnerAscHandle, Offset, Length, Generation, PayloadKind)
```

分配优先复用 free list/free range；释放递增 generation；live slot 与 live variable payload range 永不因压缩移动。Buffer 高水位增长不改变既有句柄。`Kind/PayloadKind` 参与校验，禁止错 slab/range 的同 index 偶然命中。

Ability 的 Activate 与 Commit 分离：Activate 建立/推进 activation slot；Commit 原子验证并直接更新同一 `AttributeValueSlot` cost 权威、创建 `CooldownGateSlot`。Cancel 只是 Activation 槽状态跃迁，不删除已 Commit gate。tick-start `EndTick <= CurrentTick` 的 gate 先于新请求释放；其 Tag contribution 由 gate handle精确撤销。三者不能依赖创建 Ability Entity 后再同 Tick读取。

`AttributeValueSlot.Revision` 在该属性权威 Base/Current 发生语义变化时单调递增；Source Live capture 只通过 revision 检测 dirty，并在下一 Tick按 destination ASC 分组投递，不允许 source job 随机写 target Aggregator。

## 4. Active Effect 的 target-local 所有权

Active Effect 权威实例永远位于 target ASC：

- source capture 保存 Source ASC stable handle 与 snapshot 值；live capture 通过受控 lookup 读取源状态。
- target capture 与 aggregator 只由 target writer 修改。
- application/ongoing/removal requirement、inhibition、stack/overflow、period 与 channel 状态随 slot 生存。
- ongoing/inhibition/tag grant/remove 在 target-local stabilization 中收敛后才发布 Fact。
- 每条 application 在 target canonical 线性化点按 `TargetLifePolicy` 检查 `AscLifecycle`；`AliveOnly` 首次 death crossing 后的后续 application typed reject。
- OwnerWave 已 Commit 的远端 work 不因 source 随后死亡而撤回；activation 结束只清 `OwnedContribution`，并仅对 `RemoveOnActivationEnd` 的 `EmittedApplicationRef` 生成 remove work。

定义可以产生独立 projectile/hitbox Entity，但该 Entity 只持有 Activation/Effect handle 或 payload；不能把 definition-time 开关变成“某些 Effect 是 Entity 权威、另一些是 slot 权威”。

## 5. Tag 布局

`TagCountSlot` 至少区分：

- `ExactCount`：该 tag 被直接授予的计数；
- `InclusiveCount`：精确计数加所有后代对祖先的传播计数。

presence/ancestor bitset 由 `InclusiveCount > 0` 派生，可在 Catalog/计数后重建。匹配查询读派生缓存，grant/remove 修改权威计数并标 dirty；禁止直接切 bit 来伪造 Tag 权威。

## 6. Tick scratch 不是 Entity

解析结果、target bucket、排序 key、分区 Fact 与 merge 索引只存在于 `GasTickKernelSystem` 的 `WorldUpdateAllocator` NativeContainer 中：

- backing allocator 的官方物理寿命可能更长，但项目可用期仅当前固定 Tick DAG；
- 通过一个 Job DAG 传递；
- 不放 Session/ASC Component，不用 static/managed field；
- 需要跨 Tick 的内容必须进入 Pending Command、Continuation 或 Active Effect Buffer。

fixed-rate catch-up 的 group allocator 不在同一 outer World update 的每个 SimulationTick 之间 rewind。访问权虽在 Tick DAG 结束时失效，内存预算仍必须覆盖 `MaxFixedTicksPerBatch × per-tick scratch/facts`、burst 与 double-rewind 高水位；headless 长局必须拆成有界 TickBatch。

## 7. Scoped Boundary outbox 与 cleanup shell

Session 与每个 ASC 出生时都显式添加 `DynamicBuffer<BoundaryFactBuffer>`（元素实现 `ICleanupBufferElementData`）和 cleanup `BoundaryDrainState`。ASC-scope fact 只写 ASC outbox，BattleInstance/Session-scope fact 只写 Session outbox；同一 fact 恰有一个 owner。不能依赖 prefab/原型实例化复制 cleanup component/buffer。

ASC 被 EndFixed 销毁后：

1. 普通 ASC 数据被移除；
2. outbox 与 drain state 保留，Entity 成为 cleanup shell；
3. 单 managed drain 从自包含事实或 `BoundaryDrainState` 读取 stable owner identity/tick/key，以 frozen `BatchId/InFlightWatermark` 交给幂等 staging；
4. staging receipt 成功后只清 `<=InFlightWatermark`；late tail保留并回 Pending，无 tail或显式 NoFactReceipt才写 `BoundaryDrainState=Accepted`；失败时保留 outbox与 InFlight identity，不回滚 gameplay；
5. 下一次正常 Kernel cleanup prepass 把移除两个 cleanup 类型记录到该 Tick 标准 EndFixed，playback 后 Entity 最终回收。

Post-Fixed drain 不排跨 batch ECB。若不再有下一 Tick，shutdown 先完成当前完整 EndFixed 与所有 producer，再 FinalDrain；全部 staging receipt 成功后由停止世界 teardown 直接清 shell。接管失败必须阻止 Session 完成 FinalDrain/Disposed。

cleanup shell 不是 live owner。Drain 以 `GasAscIdentity`、`GasSessionIdentity` 和 frozen `BoundaryDrainState.OwnerKind` 区分 live ASC、live Session 与 shell；任何业务 lookup/Registry 必须以 identity/generation 为准，不能只看 Entity 是否存在。

## 8. Entity promotion 裁决

v1 不允许 definition-time “slot→Entity promotion”。允许建立 Entity 的判据是它是否需要：

- 独立 Transform/Physics；
- 与其他系统独立查询/分块；
- 不随一个 ASC owner/target slab 生存；
- 独立的结构生命周期。

即便满足判据，Ability/Effect 权威仍保留在 slab，Entity 只持稳定句柄。这避免保存、网络、调试、清理与引用校验出现双模型。

## 9. 生命周期验收

- Session/ASC spawn 后固定 Buffer 长度正确、Catalog/Layout 哈希一致；Pending Batch 只由下一 Kernel `SpawnFinalize` 在 `SpawnInitializationTransaction` 的 Attribute/Tag/Grant/InitialEffect 全部成功后整批发布 Ready，post-EndFixed 无 gameplay writer。
- 每 World 只有一个 active Session；SpawnBatch 只会整批发布 Ready，失败不会部分注册。
- Owner 与 Avatar 分离/换 Avatar 不迁移 Attribute/Effect 状态。
- slab 释放/复用后 stale generation 拒绝，其他 live handle 不变。
- 同 Tick Destroy 的 ASC/Session 仍能完整 drain scoped Boundary Fact；receipt 后由下一 Kernel prepass + 同 Tick EndFixed 清 shell，shutdown 无下一 Tick时走显式 direct cleanup；零 ASC Session terminal fact仍可交付。
- Derived Entity 销毁不会隐式删除或复制 Active Effect 权威。
- World shutdown 顺序为关闭 ingress → 完整 EndFixed/完成 producer → FinalDrain receipt → 直接清 shell → Blob/managed registry 释放；staging 失败时不得进入 Disposed。
