# 10B-06 完整业务流程走查

## A. Tier A 真实主战链

```text
SpawnBatch Pending -> Initialize/Grant -> Ready
BattleLocalTick: AI priority ActiveDue > Finisher > Primary
Target: same BattleInstance enemy, stable ScenarioUnitId tie-break
AscOwnerCommandWave: Activate -> Commit second check -> atomic CommitPlan
AscTargetStateWave: Frozen ASC + AliveOnly -> application/result/facts
EndFixed -> one Boundary Drain -> immutable report/replay input
```

Tier A legacy 主战 Definition 固定为：`9101 Player Primary -> 9201`、`9102 Enemy Primary -> 9202`、`9103 Finisher -> 9207`、`9104 ActiveDue Poison -> 9203 -> 9204`。Tier B 目标态将 period evaluator inline编译进 9203并删除 9204 Catalog identity。Tier A 先刻画 legacy winner/count/finish tick；它不证明 cost/cooldown、cancel/wait、immunity/inhibition、grant removal、多目标与 Exact/Inclusive Tag。

### 9207 斩杀

```text
HealthIndex = EffectDefinition.ValueView.HealthLayoutIndex (bake-resolved)
HealthBefore = Target.AttributeValueSlot[HealthIndex].Base
MissingHealth = Max(0, MaxValue - HealthBefore)
Damage = Clamp(16 + 0.5 * MissingHealth, 12, 42)
Target.AttributeValueSlot[HealthIndex].Base -= Damage; Revision++
Aggregator recomputes/clamps the same slot Current
```

9207 是 Instant pure evaluator，不建立无限 ActiveEffect marker。Base/Current/MaxValue 与 bake-resolved layout index 的 ValueView 是配置与 bake 契约，禁止 evaluator 自选读取口径或回退 per-attribute component。

## B. Tier B 主业务毒语义

```text
T0 successful apply: Stack=1, replace SourceAttackSnapshot/provenance, NextDue=T0+2, End=T0+8
T1 successful reapply: Stack=2, replace snapshot/provenance, NextDue=T1+2, End remains T0+8
T3 Due: Health Base -= LatestSnapshotAttack * 0.3 * 2
cap reapply: Stack remains 3, replace payload/provenance and reset NextDue; End unchanged
DueTick == EndTick: execute period first, then expiry remove-one
expiry with remaining stack: refresh duration and reset period
```

唯一目标参数为 `StackKey=(Definition,TargetASC,SourceASC)`、`AggregateBySource`、`ReplaceLatest`、Duration 8、Period 2、limit 3、`ExecuteOnApply=false`。每次成功 application（含 cap reapply）替换 Source Attack Snapshot 和 period-kill provenance、重置 next due，但不刷新 duration。Inhibit 时 duration 继续、period skip、无 catch-up。9204 的固定 `-1`、不乘 stack 及 9203 非法 enum 都是 breaking current fact，必须 bake fail，不留兼容支路。

Owner-local PeriodDue 在 due tick 同 tick；post-apply overflow/reaction/cross-owner dynamic child 进入 `T+1`，静态闭合 `DirectEffectProgram` 仍可 same tick。

## C. Death、Battle terminal 与 Session teardown

任一 application 首次令 Health crossing 到 0：致死 application 记录 damage/overkill 与冻结 provenance；后续 AliveOnly application typed reject且不算 damage/assist。source 在 Commit 后死亡不撤回远端工作。死者未 Commit 行动取消，ActiveEffect/Cue 清理，但 corpse ASC 保留至 BattleOutcome 快照和 teardown。

单 BattleInstance 终局只关闭自身新 gameplay ingress；本 tick 完整 DAG 结束并完成 managed `FactPlane=Gameplay` FinalDrain 后冻结 BattleOutcome。全部战局终局或显式 stop 后 Session 才 Terminalizing；teardown 产生 `FactPlane=TeardownAudit` facts 与 cleanup audit，最终 ValidationResult 在 audit 后返回，tear down 不改 battle hash。FactPlane 不取代 Asc/Battle/Session identity scope。

## D. Tier C 扩展走查

- 盾击：静态闭合 `{Damage, Stun}` program，Stun Tag count 与 Cue 四阶段成对。
- 冰霜新星：稳定选取最多 3 个 target，三个 target transaction 独立成功/失败，不做跨 ASC 回滚。
- 羁绊：roster diff 只产生 Shell intent，后续 tick 进入通用 pipeline。

这些样例不得冒充当前 AutoChess 真实覆盖；必须在 Tier A/B 验收完成后单独报告。

## 走查验收

每条走查固定 input/content/session hash，断言 request/outcome、tick 序列、slot identity、Attribute/Tag 最终值、Effect/Cue lifecycle、Boundary order、BattleOutcome、battle hash 与 teardown audit。Tier A legacy count characterization 与 Tier B target semantic conformance 分开出具结论。
