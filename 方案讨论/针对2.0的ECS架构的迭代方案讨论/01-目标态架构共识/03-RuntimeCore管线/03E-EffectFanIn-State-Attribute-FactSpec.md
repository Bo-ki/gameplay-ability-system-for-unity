# Effect Fan-In、State、Attribute、Fact 总纲

## 结论

Effect、Tag、Attribute 与 Fact 是同一个 target-owned transaction 的连续阶段。每个目标 ASC 由一个 mutation lane 串行稳定化，不同目标并行；公开 Fact 只在最终稳定状态生成，并在下一 tick 触发 Reaction。

## 子 Spec

| 文档 | 唯一职责 |
|---|---|
| [03E-01 Effect Fan-In](03E-EffectFanIn-State-Attribute-Fact/03E-01-EffectFanInSpec.md) | 命令规范化、canonical order、按目标分区 |
| [03E-02 State/ActiveEffect](03E-EffectFanIn-State-Attribute-Fact/03E-02-StateEvaluateActiveEffectStoreSpec.md) | Apply、stack、tag、ongoing/inhibition 稳定化 |
| [03E-03 Attribute](03E-EffectFanIn-State-Attribute-Fact/03E-03-AttributeReduceApplySpec.md) | Aggregator、Base/Current、capture 与 dirty apply |
| [03E-04 GameplayFact](03E-EffectFanIn-State-Attribute-Fact/03E-04-GameplayFactSpec.md) | Core fact、deferred reaction、Boundary export |

## Transaction 边界

```text
Pre-apply commands
  -> requirement / immunity / stack decision
  -> ActiveEffect and contribution mutations
  -> TagCount and ongoing/inhibition fixed point
  -> dirty aggregator recompute
  -> Attribute Base/Current commit
  -> terminal cleanup
  -> final Core facts
```

owner-local commit 可以原子处理 cost/cooldown 与 activation state，但不承诺多个远端 ASC 的分布式原子性。跨目标结果按独立 target transaction 成功或失败，并保留统一 Application/Causality provenance。

## 同 tick 与下一 tick

同 tick 处理已密封命令、tick-start 已存在的 PeriodDue、Ability 直接输出、生成期闭合且静态有界的 DirectEffectProgram，以及 target transaction 内的完整稳定化。Apply 后才产生的 GameplayEvent、Tag/Attribute reaction、Continuation resume、overflow/post-apply 动态 child 和跨 ASC reaction 默认写入 `DueTick = CurrentTick + 1`。

Attribute 阈值导致“不能再行动/必须终止”的规则若属于 gameplay 不变量，应在当前 target transaction 内直接执行，不通过通用 Fact reaction 偷回同 tick 重入。

## 失败语义

- Definition 无效、projection 不闭合、跨 ASC Live 不受支持：bake fail。
- stale handle、重复 remove/commit、count underflow/overflow：确定性 runtime fault 或显式 reject，不静默修正。
- stabilization 重复状态或超出安全预算：`StabilizationFault`，终止 battle/session，禁止半状态继续。
- capacity/outbox overflow：按配置显式失败或 reconcile，永不静默 drop。
