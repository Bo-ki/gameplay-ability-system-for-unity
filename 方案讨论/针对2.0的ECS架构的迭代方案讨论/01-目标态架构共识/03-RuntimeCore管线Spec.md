# Runtime Core 管线 Spec

## 结论

Runtime Core 只有一个固定步物理执行域和一个 tick kernel。Command、Target、Ability、Effect、State、Attribute、Fact 是 Kernel 内的逻辑 stage/job，不是多套 `ComponentSystemGroup`。完整规范由 [03-RuntimeCore 管线目录](03-RuntimeCore管线/README.md) 维护。

## 固定管线

```text
Gather / TickStartSnapshot + PlanExpandScratchProvision
  -> OwnerPlanBuild（ASC-local shadow RYW；零权威写）
  -> TargetResolve / bounded DirectEffectProgram Expand
  -> WholeTickInfraAdmission（任何 gameplay 权威写之前）
  -> AscOwnerCommandWave（no-fail CommitPlan）
  -> SourceSpecProjection
  -> GroupByTarget
  -> TargetPrepare（Requirement / Immunity / Stack / Attribute / Stabilize / Death in shadow）
  -> SessionFaultReduce / TargetPublish（no-fail durable publish）
  -> StableFactMerge / per-BattleInstance TerminalResolve
  -> GroupNextTickRouteByDestination
  -> BoundaryProject / Cleanup Outbox
  -> standard EndFixed ECB
  -> managed staging accept / Managed Drain
```

## 导航

| 文档 | 唯一职责 |
|---|---|
| [03A](03-RuntimeCore管线/03A-执行域与数据流Spec.md) | FixedStep/PostPhysics、0..N tick、完整父链与数据流 |
| [03B](03-RuntimeCore管线/03B-业务调用链与配置消费Spec.md) | 业务调用与 Definition 消费 |
| [03C](03-RuntimeCore管线/03C-SystemGroup合约与核心数据形态Spec.md) | 单 Group/Kernel、dependency 与长期数据形态 |
| [03D](03-RuntimeCore管线/03D-CommandResolve与TargetResolveSpec.md) | 命令、Ability、Continuation、Target resolve |
| [03E](03-RuntimeCore管线/03E-EffectFanIn-State-Attribute-FactSpec.md) | Effect fan-in、稳定化、Attribute、Fact |
| [03F](03-RuntimeCore管线/03F-StructuralCommit与BoundaryProjectionSpec.md) | 标准 EndFixed ECB、cleanup outbox 与唯一 Drain |
| [03G](03-RuntimeCore管线/03G-Component矩阵-TickScratch-Job拓扑Spec.md) | Component 矩阵、tick scratch 与 Job DAG |
| [03H](03-RuntimeCore管线/03H-DOTSAPI策略与Backbone验收Spec.md) | DOTS API 选型和验收 |
| [03I](03-RuntimeCore管线/03I-SystemLaneCatalog与禁止方向Spec.md) | 唯一物理 System、逻辑 stage catalog、禁止方向 |

## 全局门槛

- 所有 tick 临时容器归 `GasTickKernelSystem`，使用 `WorldUpdateAllocator`。
- 同一 ASC 只有一个逻辑 writer，不通过 unsafe restriction 绕过所有权。
- 本 tick Apply 后 fact 不重入本 tick；公开 reaction 默认下一 tick。
- 结构变化实体最早下一 tick 被 Core 查询。
- 最终 outbox 在 Drain 前持久存在，ASC Destroy 不丢 terminal fact。
- 任何物理 stage 扩张都必须先证明无法在现有 Kernel Job DAG 表达，并另立 ADR。
