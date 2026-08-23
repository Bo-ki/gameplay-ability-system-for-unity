# 10B-05 Attribute、ActiveEffect 与 Death 投影

## Damage Apply

target transaction 读取冻结 source capture 与目标 phase snapshot，计算防御减伤，执行 Health Base mutation，再按 Aggregator 重算 Current/Clamp。Pre/execute/Post 与 meta damage 转换在同一 transaction 完成。

## ActiveEffect

- Stun：Active 时 grant `State.Stunned`/block action/Cue；Inhibited 时按 policy 撤销贡献但保留槽；Removed 时精确清理。
- Slow：以 ContributorId 作用于 MoveSpeed/AttackSpeed Aggregator，移除后恢复。
- Poison：按 source/target stack key 合并，period 到期修改 Health Base；inhibit 时不执行。

Tag/ongoing/inhibition/contribution 必须先稳定。中间试探态不发 Cue/fact；无固定点 fatal。

## Death invariant

Health 首次从大于 0 crossing 到 0 后，当前 target transaction 必须：

1. 写入不可行动/死亡权威状态，并冻结 `Killer/SourceSpec/Application/PeriodExecution/Overkill` provenance。
2. 取消尚未 Commit 的行动，清理该单位 ActiveEffect 与对应 Cue 生命周期。
3. 阻止本 tick 后续同目标 action commit，并使后续 `AliveOnly` application typed reject。
4. 记录本次致死 damage、overkill、Death/Attribute/Cue/Boundary facts；通用 Death reaction 最早下一 tick。
5. 保留 corpse ASC 至 BattleOutcome 快照和 teardown；Session teardown 才执行结构销毁。

不能依赖下一 tick 通用 reaction 才阻止死者行动，也不能在 Drain 前丢失 terminal facts。首次 crossing 之后拒绝的 application 不得计入 damage、assist 或 Executed Cue。`UnitDeath`、`BattleTerminal`、`SessionTerminal` 必须分别建模。

## Kill credit

Death fact 使用最后一个已提交 Health contributor/Application/Causality provenance 计算 killer/assist；不得从表现 hit 或已回收 ActiveEffectSlot 反推。

## Poison 与 Granted Ability 生命周期

- 9203 使用 `10B-02` 唯一毒语义；owner-local period due 在 `DueTick` 同 tick执行，inhibit 时 duration 继续、period skip 且不 catch-up。
- ActiveEffect 授予的 Ability slot 采用稳定 handle；公开 `LeaveGranted` 策略在 source effect 结束后仍保持可调用，并在内部 detach ownership、冻结 provenance，以实现 UE `DoNothing` 的可观察语义。
- `RemoveWhenAllActivationsEnd` 只约束 granted slot 的移除时机；activation 只撤销自身 `OwnedContributionRanges`，`EmittedApplicationRefs` 不因 activation end 自动撤回。

## 验收

- 多伤害同 tick 的顺序、overkill、killer/assist 确定。
- 若 death 已在该 ASC 的 `AscOwnerCommandWave` canonical RYW 中可见，后续 owner work 不得成功 Commit；若 death 由稍后的 `TargetPrepare` incoming application 在 shadow 中产生，则 `SessionFaultReduce` 成功后随 `TargetPublish` 发布，且不逆向撤回本 tick 已 Commit work（committed-work-wins）。
- poison period、stun/slow remove、inhibit/reactivate 精确。
- source Commit 后死亡不撤回远端工作；首次死亡后续 AliveOnly 工作稳定拒绝。
- corpse ASC 在 outcome 前可观测、teardown 后为零；Death/Removed/Cue facts 完整 FinalDrain。
