# State Evaluate 与 ActiveEffect 稳定化 Spec

## 结论

ActiveEffect 是目标 ASC 的非压缩 slab 槽。Apply、stack、tag grant、ongoing requirement、inhibition、contribution 和 removal 必须在该目标的单 writer transaction 内达到稳定点，再提交 Attribute 与最终事实。

## ActiveEffect 状态

```text
Empty -> Active -> Inhibited -> Active
                 \            /
                  -> Removing -> Tombstone -> Free
```

- `Active`：时间/stack/context 存活，contribution/tag/grant/cue side effects 生效。
- `Inhibited`：槽、duration/period、stack 和 context 仍存活，按 policy 暂停或继续时间；可逆 side effects 已撤销。
- `Removing`：终止原因冻结，执行一次精确撤销并生成 terminal facts。
- `Tombstone`：不再参与 gameplay，等待子记录、队列与 Boundary 交接完成。

槽含 `SlotIndex + Generation`、Definition/Application provenance、source key、duration/period/stack policy、capture ranges、contributor ranges、granted ability/tag/cue ranges、next due tick 和 free-list metadata。禁止 `RemoveAt`、SwapBack 与 compact。

## Application 决策顺序

`TargetResolve` 只产生稳定 target binding、TargetData 与可诊断 precheck evidence；它不是 application result 的 owner，也不得提前写 requirement、immunity、capture、stack、damage 或 Cue 结果。每条 `EffectApplicationId` 的最终线性化点在目标 ASC single-writer transaction 内，并按 canonical application order 执行：

1. 校验 target epoch/binding、definition/spec/handle 与 `TargetLifePolicy`。
2. 在此线性化点建立 Target pre-application capture；它读取该目标此前 canonical application 已提交的 Attribute/Tag/ActiveEffect 状态。
3. 以同一份前序可见状态重新评估完整 application requirements 与 immunity；precheck 只能优化，不能替代最终评估。
4. 选择 stack key（source/target policy）和既有槽，校验 WholeTickInfraAdmission 已授予的 reservation token，并评估业务 AtLimit/Overflow policy；本阶段不得再发生基础设施容量失败。
5. 按 `StackTemporalContract` 处理 stack limit、payload、refresh、overflow、clear、period 与 expiry。
6. Instant：直接执行 Base mutation/Executed Cue，不创建槽。
7. Duration/Infinite：分配或更新 ActiveEffectSlot。
8. 应用/撤销 tag、ability grant、block 与 aggregator contribution。
9. 进入 target-local stabilization，并只在稳定提交后发布 application outcome/facts。

最终结果必须是强类型 `ApplicationOutcome`，至少区分 `AppliedInstant / CreatedActive / MergedStack / OverflowApplied / RejectedRequirement / RejectedImmunity / RejectedTargetLife / RejectedStackPolicy / RejectedStaleBinding`。`RejectedImmunity` 必须冻结 blocker Definition、ActiveEffectHandle/ContributorId 与匹配 requirement/tag provenance；不能只输出布尔值。失败路径不得暴露 capture、slot、stack、Attribute 或 Cue 的部分 mutation。基础设施容量不足不是 ApplicationOutcome：它必须由 WholeTickInfraAdmission 在任何 owner/target 权威写前锁存 `InfraAdmissionFault`，并使本 tick gameplay 零写。

同一目标的 canonical 序列提供 read-your-writes：先前 application 若造成首次死亡 crossing，后续 `TargetLifePolicy=AliveOnly` application 在本 tick 以 `RejectedTargetLife` 结束，且不得产生 damage、overkill 或 assist。致死 application 自身仍完成其已经线性化的全部 modifier/execution/clamp/fact 节点；具体 Death 契约见 [03E-03](03E-03-AttributeReduceApplySpec.md)。

多个目标不是分布式事务；每个目标独立给出 Applied/Rejected/Overflow 等结果，但使用同一 parent causality 关联。

## 稳定化算法

```text
repeat in stable SlotIndex order
  apply pending mutation
  update Tag Exact/Inclusive count
  reevaluate dirty ongoing/removal requirements
  toggle Active/Inhibited or mark Removing
  update granted tag/ability/block/contribution
until no state transition

recompute dirty aggregators
emit final facts only
```

实现可以先用 dirty flags + 稳定顺序重复扫描；不能用固定 pass 到点后静默截断。生成期根据 granted tags、ongoing/removal requirements 和 inhibition 构建有符号依赖图，保守拒绝含非单调负边的循环 SCC。运行时仍必须做重复 state hash 或 transition safety budget；无固定点触发 fatal `StabilizationFault`。

稳定化内部试探 transition 不生成 Cue/fact；显式且已经提交的 Add→Remove 生命周期是否保留 OnActive/Removed，由 Cue Spec 明确定义，不能与试探态混淆。

## StackTemporalContract

每个可叠层 Definition 必须在生成期完整声明以下互相独立的轴；缺失任一轴不得由 Runtime 猜默认值：

| 轴 | 必须回答的问题 |
|---|---|
| `StackKeyPolicy` | Definition、TargetASC、SourceASC、source object 或显式 group 中哪些字段构成合并键 |
| `StackPayloadPolicy` | 新 application 是只增加同质 count、替换共享 payload，还是保存逐 application payload/expiry ledger |
| `DurationRefreshPolicy` | 成功合并、到达上限或 overflow 时是否刷新 remaining/expiry，刷新哪一层 |
| `PeriodResetPolicy` | 合并/刷新/reactivate 时 next due 保持、重置或按明确规则对齐 |
| `ExecuteOnApplyPolicy` | 首次 apply 与每次 stack application 是否立即执行一次 period body |
| `AtLimitPolicy` | 到达 stack limit 时 deny、refresh-only、replace、overflow 或 clear 的决策 |
| `OverflowPolicy` | overflow effect、deny overflow application、clear stack/target 的精确顺序与 outcome |
| `ExpirationPolicy` | 整槽到期、每次移除一层、按最旧/最新 ledger entry 到期或其他生成期闭合策略 |
| `FinalPeriodPolicy` | expiry 与 period 同 tick 时执行 final period 还是先 remove |
| `InhibitResumePolicy` | Inhibited 时 duration/period 是暂停、继续但跳过、累计、重置，reactivate 如何恢复 |
| `StackCuePolicy` | stack count 改变是无 Cue、更新参数还是产生 Executed；不得建立新的 lifecycle cycle |

`StackPayloadPolicy=HomogeneousCount` 只能表达“共享 runtime payload × StackCount”。毒、充能或独立来源若要求每层保留不同 magnitude、source、apply tick、duration 或 expiry，必须选择逐 application ledger，并使 application/contributor/removal 可精确寻址；存储模型不支持该 ledger 时 Definition bake fail，禁止把异质层压成一个 count。

## Period、overflow 与 removal 竞争

1. owner-local due 在 `DueTick` 的当前 tick 被 claim；每次 claim 生成唯一 `DueClaim` 与单调 `PeriodExecutionOrdinal`，重复扫描/重试不能再次执行。
2. 跨 ASC Live dirty 的 T+1 destination work 在语义序上先于同一目标同 tick 的 `PeriodDue`；period 读取 dirty 重算后的 canonical state。
3. 执行前校验 Handle Generation、Active/Inhibited/Removing 与 `DueClaim` ownership；执行后再次校验，因为 period body 可能使自己进入 Removing。
4. ExecuteOnApplyPolicy 决定当前 application 是否在本事务执行一次自身 period body；该 body 本身可经 target single writer 写本次 delta/fact。只有该 body、普通 apply、overflow、period 或 reaction **动态派生的 child application** 默认 `DueTick = CurrentTick + 1`。静态闭合 DirectEffectProgram 例外由 [01B](../../01B-GAS业务语义链路概念设计Spec.md) 所有。
5. expiry、final period、self-remove 与 overflow clear 必须按 `StackTemporalContract` 在一个 removal arbitration 中决定。进入 Removing 后不得刷新 duration、重排 next due 或再次撤销贡献；terminal cleanup 只能成功一次。
6. Duration、Period、Wake 只保存整数 tick；tick rate 变化不迁移存活槽。

## Tag authority

`TagCountSlot` 同时保存 `ExactCount` 和 `InclusiveCount`。grant 叶 Tag 时按 Catalog ancestor chain 更新自身与祖先；remove 必须用稳定 source provenance 精确反向一次。presence/ancestor bitset 只是带 revision 的派生缓存。underflow、overflow、重复撤销都为 fault。

## 验收

- 同一 target 的前序 application 改变 requirement/immunity/target life 时，后序 application 读取已提交 canonical state；TargetResolve precheck 不会预写结果。
- immunity rejection 输出 blocker provenance；requirement/immunity/stack policy 失败均无部分 mutation；基础设施容量不足则整 Tick 零写。
- StackTemporalContract 的 key/payload/refresh/reset/execute-on-apply/limit/overflow/expiration/final-period/inhibit/cue 轴均有固定向量测试。
- 同质 count 与逐 application payload/expiry ledger 不混用；异质毒层缺 ledger 在 bake 失败。
- period due 只 claim 一次；expiry 同 tick、period self-remove 与 overflow clear 不会双执行、双 refresh 或双删。
- 多来源 Tag grant/remove 后 Exact/Inclusive count 正确。
- inhibit/reactivate 保留槽身份且贡献精确恢复。
- remove 后 stale handle 必定失败，slot churn 不改变其他句柄。
- 振荡配置在 bake 或 runtime 明确失败，绝不输出半稳定状态。
