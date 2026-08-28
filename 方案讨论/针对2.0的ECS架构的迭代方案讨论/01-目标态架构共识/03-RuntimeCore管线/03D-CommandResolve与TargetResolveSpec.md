# 03D：Command Resolve 与 Target Resolve

> Owner：`01-目标态架构共识/03-RuntimeCore管线` | 状态：目标态子 Spec | 拆分来源：`../03-RuntimeCore管线Spec.md`

本文件只描述目标态 Ability command normalization、Activation identity、TargetData ownership 与 per-target application intent。物理 SystemGroup/phase 见 [03A](03A-执行域与数据流Spec.md)，核心数据外形见 [03C](03C-SystemGroup合约与核心数据形态Spec.md)，Effect Fan-In 见 [03E](03E-EffectFanIn-State-Attribute-FactSpec.md)，结构提交与 Boundary 见 [03F](03F-StructuralCommit与BoundaryProjectionSpec.md)，Tick Scratch 与 Job 拓扑见 [03G](03G-Component矩阵-TickScratch-Job拓扑Spec.md)。本文不维护第二份调度代码骨架。

## 相邻 Spec Owner 裁决

| 主题 | 唯一正文 owner | 本文件职责 |
|---|---|---|
| Shell intent 进入 Runtime Boundary | `16-02-BoundaryCommand与CoreCommandResolveSpec.md` | 只要求输入携带稳定 ASC/Granted handles 与业务 payload |
| Ability command normalization、Activation 分配、Target Resolve | 本文件 | 定义语义字段、失败条件、owner 与确定序 |
| Effect Spec、Capture、Modifier/Delta | `04-EffectCommand-SpecStream-AttributeDeltaSpec.md` | 输出 per-target application intent，不重复计算契约 |
| ActiveEffect 生命周期 | `05-ActiveEffectStoreSpec.md` | 只传递 ApplicationId/Context，不直接写 active slot |
| 物理 producer/merge/apply | `03A/03C/03E/03G` | 引用，不复制 Job/NativeStream/ECB 细节 |

## 输入身份

Ability command 必须从 GrantedAbilitySpec 开始，而不是从 Ability Entity 或裸 DefinitionId 开始：

```text
Boundary/Core Intent
    + SourceAscInstanceId
    + GrantedAbilityHandle
    + InputSequence
    + optional EventData/TargetData
        ↓ validate owner + generation
AbilityActivationCommand
        ↓ CanActivate success
AbilityActivationHandle
```

### AbilityIntent 最小语义

| 字段 | 语义 |
|---|---|
| `SimulationEpoch` | 阻断跨 world stale command |
| `SourceAscInstanceId` | owner-local command route |
| `GrantedAbilityHandle` | 强类型 `(Epoch, OwnerAscHandle, SlotIndex, Generation, Kind)` |
| `InputSequence` | source-local 确定序 |
| `EmitTick/SemanticPhaseOrdinal/WorkClassOrdinal` | 因果与 deferred reaction 的 schema-hashed 语义全序；不得使用 Job lane 充当 phase |
| `ExplicitTarget` | 可选目标 ASC 或 target query seed |
| `EventDataId/TargetDataId` | 冻结 payload；不得引用临时托管对象 |
| `ParentCausalityId` | 诊断与环检测，不是 PredictionKey |

禁止输入 `AbilityEntity` 作为 gameplay authority。projectile/aura Entity 可以是 target 或 EffectCauser，但 GrantedSpec 与 Activation 始终由 Source ASC slab 拥有。

## Command Normalize 与 CommitPlan 契约

Command Normalize 由 `OwnerPlanBuild` 在 ASC-local shadow 上按以下顺序裁决：

1. 校验 SimulationEpoch 与 SourceAscInstanceId。
2. 校验 GrantedAbilityHandle 的 kind、owner、slot、Generation 与 Live/PendingRemove 状态。
3. 读取 GrantedSpec 的 DefinitionId、level、grant provenance 与 activation policy。
4. 运行 CanActivate requirement、block/cancel policy 与基础 resource availability。
5. 按 policy 处理 reentry/retrigger：拒绝、结束旧 Activation 或允许 N 个并发 Activation。
6. 成功时在 CommitPlan 中预定独立 AbilityActivationSlot 与所需持久 payload range；失败时输出 typed failure record，不伪造 ActivationHandle。
7. 把 EventData/TargetData/Context 的 owner 转交计划写入 Activation；tick-local 输入如需跨 tick 必须纳入持久 payload 上界。
8. 将本命令的计划跃迁应用到同 ASC shadow，供 canonical 后续命令 read-your-writes；此时仍不写任何 gameplay 权威 Buffer。

CanActivate 成功不等于已 Commit。Activation 可进入 `RunningUncommitted`，之后由 Ability program 在需要时执行 CommitCheck。OwnerPlanBuild 不提前改权威 cost/cooldown；它产出完整 CommitPlan，待 `WholeTickInfraAdmission` 成功后由 `AscOwnerCommandWave` no-fail 原子提交。

OwnerPlanBuild/OwnerWave 只能观察 Tick-start snapshot 与同 ASC 前序 CommitPlan，不能观察本 Tick incoming target effect。OwnerPlanBuild 先处理 due `CooldownGateSlot` release，再解释请求。需要 same-tick 影响后续 CanActivate 的内容必须建模为显式 owner commit invariant：`CostMutationContract` 写同一 Attribute 权威，`CooldownGateContract` 写 ASC-owned gate/其 owned Tag contribution，activation-owned ledger 仅表示随 Activation 生存的贡献。普通 self GE 仍走后续 `TargetPrepare -> SessionFaultReduce -> TargetPublish`；Definition 若依赖它先改 Attribute/Tag 再激活后续 Ability，必须在 bake 时拒绝。

## Activation 与 Continuation 输出

AbilityActivationCommand 不把整个 Ability 程序压成单个 tick-local record。直接程序执行后可能产生：

- 即时 Commit / Target Resolve / Effect Application intent
- 0..N `AbilityContinuationSlot`
- 0..N 本地或外部 `AbilitySubscriptionSlot`
- normal End / Cancel / failure

每个 Continuation 都有独立 ContinuationHandle、program counter、wait condition、payload 与 one-shot/persistent policy。外部订阅注册在 observed ASC 的 Subscription slab，反向引用 Activation 与 Continuation；晚到投递必须再次校验两级 Generation。

Continuation、Subscription 和跨 tick TargetData 的完整生命周期由 [01B](../01B-GAS业务语义链路概念设计Spec.md) 定义。

## Target Resolve 契约

Target Resolve 接收 planned activation token 或具有等价冻结 provenance 的派生 application intent，输出 0..N `AbilityTargetRecord`。本阶段仍在 Owner Commit 之前，所有身份都只是本 Tick 内的 planned token；失败 Commit 不得留下任何权威 Effect 身份：

| 字段 | 语义 |
|---|---|
| `PlannedActivationToken` | 本 Tick 内计划身份；已有已提交 Activation 派生工作可携带其正式 handle，但新激活在成功 Commit 前不得伪造正式 handle |
| `SpecDraftToken` | 非权威 source-bound spec 草案查表键，只能引用 plan 与 candidate capture；可以是 scratch-local index，但不得进入正式 ID、持久状态、semantic hash 或 Boundary |
| `ProgramNodeOrdinal/SpecOrdinal` | Definition 生成的稳定程序节点/spec 序号；进入正式 identity 且参与 content hash |
| `PlannedApplicationOrdinal` | spec 草案内每目标、每次应用的确定性序号；instant 与 stack merge 也预留，仍不是正式 ApplicationId |
| `SourceAscInstanceId` | source capture/Context owner |
| `TargetAscHandle` | 逻辑 target ASC identity 与 target writer route；包含 Epoch/generation |
| `AvatarBindingPolicy` | `RequireSameAvatar`、`FollowAsc` 等显式 Avatar 绑定策略 |
| `FrozenAvatarHandle` | 仅绑定策略要求冻结 Avatar 时存在；不可用时按策略拒绝 |
| `SpatialSample` | query 时冻结的位置/方向/shape/hit；`FrozenSpatial` 是有效 payload，不是 fallback |
| `TargetLifePolicy` | application 线性化点的 `AliveOnly/AllowTerminal/...` 判定策略 |
| `TargetIndex` | 同一 activation/spec 内目标序号 |
| `TargetSortKey` | 业务确定序，不由 chunk/worker 顺序推导 |
| `TargetDataSlice` | Definition 注册的额外 hit/shape metadata；不得覆盖逻辑 ASC、Avatar binding、SpatialSample 或 life policy |
| `ContextId` | Instigator/Causer/SourceObject/origin/hit provenance |
| `DefinitionId/Level` | Effect definition 与 scale 输入 |
| `EmitTick/SourceSequence` | command merge 与 replay 证据 |

`AscOwnerCommandWave` 在 no-fail Commit 成功的同一线性化点，以结构化 canonical tuple `(Epoch, SourceAscHandle, CommitSequence, ProgramNodeOrdinal, SpecOrdinal)` 建立正式 `EffectSpecId`，再以 `(EffectSpecId, PlannedApplicationOrdinal)` 建立正式 `EffectApplicationId`。`CommitSequence` 由 owner canonical command order 单调分配，不是 append/worker 序；序号回绕是 fatal fault。结构化 tuple 是权威，若运输用定长 hash 投影则必须检测碰撞并 fault，不得用 hash 碰撞合并身份。`SpecDraftToken` 只定位 tick-local draft，不参与上述任何身份。因此 owner ASC可同时写 `EmittedApplicationRef(TargetAscHandle, EffectApplicationId, cleanup policy)`；该 ref记录 application attempt，普通记录不要求预知 TargetPublish outcome或 ActiveEffectHandle。`SourceSpecProjection` 只处理这些已 Commit身份，密封 source-bound Spec、Source Snapshot/live policy；后续 GroupByTarget、TargetPrepare/Publish、Fact与target ledger只能携带正式身份。未 Commit draft在 Tick末回收且不可观察。

### 多目标隔离

一个 source-bound Effect Spec 可以 fan-out，但 target capture、TargetTags、application requirement 与 stack result 必须每目标独立。禁止把第一个目标的 target capture 写回共享 Spec，并被后续目标复用。

整 Tick基础设施准入在任何 owner/target 权威写前覆盖所有已展开目标；准入失败则整个 Tick gameplay 权威零写并锁存 `InfraAdmissionFault`。准入成功后，普通 requirement/immunity/life/stack rejection 的业务事务边界仍是**逐 target application**：一个 target 的 typed rejection 不阻止其他 target prepared outcome，也不建立跨 ASC 分布式业务事务。Stabilization、identity 或 proof invariant fatal 不属于业务 rejection；它由 `SessionFaultReduce` 阻止全部 target publish并丢弃本 Tick所有 target shadow，只保留 OwnerWave committed prefix。

### 派生 Entity

projectile/aura 等派生 Entity 可以晚于 Source Activation 命中。创建派生对象时必须复制：

- DefinitionId/version
- source capture projection
- SetByCaller
- EffectContext
- EffectSpecId 与 parent causality

命中时生成新的 EffectApplicationId。不得要求 AbilityActivationSlot 为 projectile 整个飞行期保活，也不得只保存可能 stale 的 ActivationHandle。

## TargetData 生命周期

TargetData 按生命周期分两类：

| 类型 | owner | 规则 |
|---|---|---|
| tick-local resolved target records | 当前 tick Target Resolve owner | 在下游 Effect Fan-In 完成前有效；allocator/dispose 见 03G |
| cross-tick target payload | Activation/Continuation persistent payload store | 由 generational payload handle 拥有；结束时随 Continuation cleanup |

TargetData 可以包含多个异构 slice，但 v1 只允许生成期已注册的 unmanaged variant。禁止 UObject、多态托管实例、delegate 或指向 tick-local scratch 的跨 tick 指针。

逻辑 ASC identity、Avatar binding、spatial sample 与 `TargetLifePolicy` 必须是四个正交字段：

- 逻辑 target 不因换 Avatar、Avatar Entity 销毁或空间采样失效而偷偷改成 source/self。
- `RequireSameAvatar` 失败、`FollowAsc` 找不到当前 Avatar、`AliveOnly` 失败均输出各自 typed rejection。
- `FrozenSpatial` 表示继续使用已冻结空间数据；它既不要求 Avatar 仍存在，也不是“目标无效时 fallback self”。
- 任意“缺目标就打自己”的语义必须由 Definition 显式产生 self target record，Runtime 禁止 implicit self fallback。

## 确定序与单 Writer

Command/Target 的语义总序至少由以下键组成：

```text
(SimulationEpoch,
 DeliverTick,
 TargetAscInstanceId,
 SemanticPhaseOrdinal,
 WorkClassOrdinal,
 EmitTick,
 SourceAscInstanceId,
 SourceSequence,
 ProgramNodeOrdinal,
 TargetIndex,
 ApplicationOrderKey)
```

`ApplicationOrderKey` 在 Commit前是 `(OwnerCanonicalPlanOrdinal, ProgramNodeOrdinal, SpecOrdinal, PlannedApplicationOrdinal)`；`SpecDraftToken` 可作为 scratch lookup 但不是语义排序字段。`AscOwnerCommandWave` 在成功 Commit的同一线性化点将 canonical tuple提升为正式 `EffectApplicationId`，且必须保持相同相对顺序。失败 plan的 key不得进入持久 ledger、Fact或 Boundary。不同 worker/chunk/NativeStream 分配和不同 bounded TickBatch 切分必须产生完全相同的 Spec/Application identity。

`SemanticPhaseOrdinal/WorkClassOrdinal` 由版本化 schema/catalog 生成并进入 content hash，绝不能取 Job lane、worker、chunk、NativeStream lane 或 ECB sort key。v1 至少冻结以下优先级：

```text
DestinationMaintenance / LiveDependencyDirty
  -> PeriodDue
  -> Expiration
  -> SealedRemove / Inhibit
  -> CommittedApplication
```

同一完整 canonical key 下若出现语义不等价的两条 work，不允许靠稳定排序实现偶然裁决，必须作为生成/运行 validation fault 暴露。

目标 ASC 是自身 Attribute/Tag/ActiveEffect slab 的唯一逻辑 writer。Source/Target 不同的命令只能作为值投递给 Target ASC；source job 不得跨 owner 直接修改目标 slab。

已通过 OwnerWave Commit 的 application 采用 `committed-work-wins`：source 随后死亡、换 Avatar 或结束 Activation 都不撤回已提交远端 work；只有显式 `RemoveOnActivationEnd` 语义生成独立 remove work。目标 writer 在每条 application 的线性化点读取 `AscLifecycle` 并执行 `TargetLifePolicy`；`AliveOnly` 的首个 death crossing 一旦冻结，canonical range 中后续 `AliveOnly` application 必须 typed reject。

slot index 只用于定位 owner-local record，不能单独作为跨 ASC 或跨 world 排序键。Job/chunk/worker/ECB append 顺序也不能成为 gameplay 顺序。

## Deferred Reaction 入口

从 GameplayEvent、OwnedTag trigger、Continuation wakeup 或跨 ASC fact 产生的 command 默认 `DeliverTick = EmitTick + 1`。事件记录必须冻结原始 tag、old/new count、EventData、Context、TargetData 与 source sequence。

Reaction 在发射 tick 不直接分配目标 ASC 的 Activation。投递 tick 重新校验 GrantedAbilityHandle 与 CanActivate；若 grant 已撤销或 handle stale，产生 typed rejection fact。该规则与 UE GAS 当前调用栈同步激活不同，不能标注为时序等价。

同 tick DirectEffectProgram 不是本节 Reaction 快捷路径。它只允许生成期证明闭合、完全展开、有限、静态有界且不读取 post-apply fact 的 definition-local Effect DAG，并由 Effect 语义链处理；任意 Event→Ability 或 Task callback 不得偷渡。

## Effect Fan-In 交接

Target Resolve 的输出必须足以在不反查 Ability/Activation Entity 的情况下构建 per-target GameplayEffectApplicationSpec：

- EffectSpecId / EffectApplicationId
- Source/Target ASC
- DefinitionId/version/level
- source capture 与 SetByCaller slice
- target payload 与 Context
- deterministic order/provenance

Effect application、CaptureProjectionContract、Attribute modifier 与 ActiveEffect mutation 分别交给 [04](../04-EffectCommand-SpecStream-AttributeDeltaSpec.md)、[03E](03E-EffectFanIn-State-Attribute-FactSpec.md) 和 [05](../05-ActiveEffectStoreSpec.md)。

## 失败模型

| 失败 | 结果 |
|---|---|
| stale/wrong-kind GrantedAbilityHandle | 拒绝，不分配 Activation |
| Source ASC/Epoch 不匹配 | 丢弃并输出 diagnostics fact |
| CanActivate failure | typed failure reason；不 Commit |
| reentry policy failure | typed rejection；不覆盖既有 Activation |
| invalid TargetData variant | 当前 application 失败；其他 target 不受污染 |
| target ASC 非 `TargetLifePolicy` 允许状态 | application rejected；保留 EffectApplicationId 作为审计 |
| late Continuation/Subscription event | generation mismatch 后 deterministic no-op |
| pending payload owner 已结束 | 不读取 arena；返回 owner-ended failure |

## 验收

1. 同一 GrantedAbilityHandle 可按 policy 同时产生多个独立 ActivationHandle。
2. 同一 Activation 可产生多个 Continuation/Subscription，结束一个不影响其他实例。
3. stale handle、wrong kind、跨 Epoch command 不会命中复用 slot。
4. CanActivate 成功后可以跨 tick Commit；Commit 失败不消费资源。
5. 同一 source Spec 的多个 target capture 互不污染。
6. Instant、stack merge 与 rejected application 都保留唯一 EffectApplicationId。
7. projectile/aura 在源 Activation 结束后仍能生成完整 per-target application intent。
8. cross-ASC command 只由目标 ASC writer 提交；已 Commit work 不因 source 后续死亡而撤回。
9. deferred reaction 在 T 不重入，T+1 使用冻结 payload 和投递前稳定状态。
10. TargetData 四类字段正交，Avatar/spatial 失效不会 implicit fallback self。
11. OwnerPlanBuild 的 shadow RYW 不读取 incoming target effect，普通 self GE 不能影响本 Tick 后续 CanActivate。
12. 多目标只保证逐 target typed业务 outcome；基础设施失败发生在 gameplay 权威写前，Prepare 阶段 fatal则由 SessionFaultReduce 丢弃全部 target shadow并阻止 Publish。
13. 本文件不引入 Ability/Activation Entity authority，也不保留 Prediction schema。
