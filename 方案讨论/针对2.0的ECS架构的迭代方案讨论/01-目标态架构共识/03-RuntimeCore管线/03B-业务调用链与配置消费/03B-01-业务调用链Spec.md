# 业务调用链 Spec

## 权威链

1. Shell 以稳定 ASC/Target ID 和 Definition ID 提交 intent。
2. CommandPort 校验 Session/schema/capacity，在 `SessionIngressGate` 上分配 `RequestSequence`，把完整 record 复制到 Boundary 持久 `BoundaryIngressJournal` 后才返回 Accepted。
3. pre-Fixed `GasCommandIngressSystem` 是唯一 writer，把 journal 记录搬入 ECS `BoundaryCommandInbox`；Kernel 在合法 SimulationTick seal 输入，并 resolve 强类型 handle。
4. Activation/Continuation 从 Catalog 读取 AbilityDefinition，执行 CanActivate/Commit/Cancel。
5. Target rule 通过 pure evaluator 形成稳定 target records。
6. EffectApplicationSpec 冻结 Context、SetByCaller、capture contract 和 provenance。
7. Effect commands canonical sort 后进入 target-owned transaction。
8. target transaction 完成 requirement、stack、tag/inhibition、aggregator、attribute 与 cleanup。
9. public reaction 写入下一 tick pending work，final observation 写 cleanup outbox。
10. 标准 EndFixed 处理派生实体结构变化，唯一 Drain 发布 immutable BoundaryBatch。

## Identity 链

```text
AbilityDefinitionId
 -> GrantedAbilityHandle
 -> AbilityActivationHandle
 -> EffectSpecId / EffectApplicationId
 -> ActiveEffectHandle or Instant result
 -> ContributorId / BoundaryEventId / CueLifecycleKey or ExecutedCueKey
```

不同身份不能互相替代。尤其 ApplicationId 不等于 ActiveEffectHandle，BoundaryEventId 不等于 `CueLifecycleKey=(Epoch, ActiveEffectHandle, ActiveCycleOrdinal, CueDefinitionOrdinal)`，CausalityId 不等于 PredictionId；Executed 另用 Application/PeriodExecution identity。

## 时序

Kernel invariant 和静态闭合 DirectEffectProgram 可同 tick；Apply 后 GameplayEvent/OwnedTag/Attribute reaction、外部 wake 与跨 ASC reaction 最早下一 tick。所有 deferred payload 在 emit 时冻结。

## 禁止

- Ability Entity 作为 grant/activation authority。
- Shell/Adapter 执行 gameplay formula 或手工更新内部 System。
- consumer 直接读/清理 Core buffer。
- Runtime 根据资源/UI/日志结果改变 simulation。
