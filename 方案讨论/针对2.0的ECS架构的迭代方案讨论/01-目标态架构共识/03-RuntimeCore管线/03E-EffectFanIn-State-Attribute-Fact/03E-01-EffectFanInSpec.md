# Effect Fan-In Spec

## 结论

所有 Effect 应用先规范化成不可变 application command，再按目标 ASC 稳定 ID 和 canonical key 分组。Fan-In 的输出是 target owner-local range，不是全局 singleton buffer，也不允许 producer 随机写目标 buffer。所有候选 work 必须先进入整 Tick基础设施准入；`OwnerPlanBuild` 只在 shadow 中计算每个计划自己的 post-commit capture candidate。只有 admission 与对应 Owner Commit 成功，`SourceSpecProjection` 才为该 candidate 建立权威 identity并密封 source snapshot/live policy，随后 target writer 才获得可执行 range并写已预留的 target payload/capture。

## 输入来源

- 已形成 CommitPlan 的 Ability activate/commit 直接输出。
- 当前 tick 到期的 period/pending work。
- Boundary 已密封的 apply/remove/inhibit 请求。
- 生成期 `DirectEffectProgram` 的有限展开。
- 上一 tick route 的 reaction。

每条 command 至少携带：

```text
TargetAscInstanceId
SourceAscInstanceId
EffectDefinitionId + ContentHash
EffectApplicationId
ParentCausalityId
SourceSequence
TopologicalNodeOrdinal
TargetOrdinal
ApplicationSpec payload/range
TargetLifePolicy
SemanticPhaseOrdinal + WorkClassOrdinal
```

Instant Effect 也必须有 `EffectApplicationId`；它没有 `ActiveEffectHandle`。stack 合并时 ApplicationId 仍是本次应用尝试身份，不能被既有 ActiveEffectHandle 替代。

## Canonical order

默认全序必须由语义字段构成，不依赖线程、chunk 或 Job 完成顺序：

```text
(SimulationEpoch,
 AvailableTick,
 TargetAscId,
 SemanticPhaseOrdinal,
 WorkClassOrdinal,
 EmitTick,
 SourceAscId,
 SourceSequence,
 ParentCausalityId,
 TopologicalNodeOrdinal,
 TargetOrdinal,
 EffectApplicationId)
```

`SemanticPhaseOrdinal/WorkClassOrdinal` 来自版本化 schema/catalog 并进入 content hash，绝不能由 Job lane、producer、worker、chunk 或 ECB sort key 派生。v1 最低优先级序列固定为：

```text
DestinationMaintenance / LiveDependencyDirty
  -> PeriodDue
  -> Expiration
  -> SealedRemove / Inhibit
  -> CommittedApplication
```

若业务 policy 需要更细优先级，必须在上述区间内由 Definition/Catalog 冻结并保证形成全序。完整 sort key 相等但语义不等价是 validation failure，不得用输入/完成顺序打破平局。

## Job 形态

```text
TargetResolve / Expand
  -> WholeTickInfraAdmission
  -> AscOwnerCommandWave
  -> SourceSpecProjection
  -> deterministic sort / prefix-range build
  -> TargetRange[targetOrdinal]
  -> AscTargetStateWave
  -> Stabilize / Death
```

具体使用 `NativeStream`、list+prefix sum 或 radix sort 不写死，由 ScaleProfile 选型；但 owner、生命周期、确定性结果和 pressure counters 必须一致。临时容器归 Kernel 且使用 `WorldUpdateAllocator`。所有节点以 `JobHandle` 串接，不允许为了获得数量或进入下一 lane 调用 `Complete()`。

`WholeTickInfraAdmission` 使用已展开 work 的生成上界，为 scratch、target slab、non-compacting payload/capture range、PendingCommand、fact partition 和 scoped outbox 完成逻辑预算检查与物理容量预留。下游 Job 已预排在同一 DAG；失败时它们读取 `AdmissionResult` 后统一 no-op，gameplay 权威零写，只有 `FaultLatchJob` 锁存确定性 Session Fault；成功后 target application 不再发生基础设施容量失败。

多 target work 只保证每个 target application 在 canonical 线性化点业务原子，不保证跨 target rollback。target writer 在每条 application 前按当前 `AscLifecycle` 执行 `TargetLifePolicy`；`AliveOnly` 的首个 death crossing 冻结后，range 内后续 `AliveOnly` work typed reject。已 Commit 的远端 work 不因 source 后续死亡而撤回。

## DirectEffectProgram

允许 same tick 展开的程序必须同时满足：

1. 生成期可完全展开的闭合 DAG；
2. 只包含静态 Effect Definition 边；
3. 不读取本 tick post-apply facts；
4. 不动态调用 Ability/Definition；
5. 节点数和最大输出数静态有界；
6. 输出及其静态生成上界在整 Tick admission 前全部产生；
7. edge/node ordinal 进入 canonical key。

仅“无环”不够，因为 DAG 仍可能指数爆炸。动态 overflow/conditional child、跨目标 wave 和依赖稳定后状态的边默认下一 tick。

## 拒绝方向

- singleton `DynamicBuffer` 作为默认 fan-in 总线。
- producer 通过 `BufferLookup` 随机写其他 ASC。
- 解除并行安全限制来掩盖 owner 冲突。
- command 引用 temp memory、复用槽或 raw Entity。
- Effect Definition 在运行时选择 slot/Entity 双 backend。
- 把 Job/producer lane 编号写入 gameplay canonical key。
- admission 后才发现 payload/outbox/slab 逻辑容量不足并留下半 target mutation。

## 证据

每 tick 输出 command count、target group count、sort/merge cost、admission 预算/失败项、scratch high-water、spill/overflow、stale/life-policy reject reason 分布及 deterministic command hash。语义 hash 记录 schema-hashed ordinal，不记录 physical batch/Job lane。
