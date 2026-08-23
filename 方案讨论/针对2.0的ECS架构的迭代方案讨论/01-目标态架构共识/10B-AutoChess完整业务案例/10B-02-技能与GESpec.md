# 10B-02 技能与 GameplayEffect

## 分层业务能力

### Tier A：legacy real characterization

| Ability | Effect | 当前业务意义 | v1 特征化口径 |
|---|---|---|---|
| 9101 | 9201 | 己方 primary attack | 保留攻击/伤害/胜负结果，不保留 raw Entity 排序 |
| 9102 | 9202 | 敌方 primary attack | 同上 |
| 9103 | 9207 | 对低血量目标斩杀 | 9207 改为 Instant pure evaluator，不保留无限 ActiveEffect marker |
| 9104 | 9203 -> 9204 | 主动毒，period 扣血 | 9203 采用本页唯一目标毒语义，当前异常全部列 breaking/bake-fail |

AI 优先级冻结为 `ActiveDue > Finisher > Primary`。时间输入使用 `BattleLocalTick`，目标破平使用稳定 `ScenarioUnitId`，不使用 wall clock 或 raw Entity index/version。

Tier A 当前 cost/cooldown/requirement 均为 0/空，因此只做业务结果特征化，不作为 Commit、cancel/wait、immunity/inhibition、Tag count、grant removal 或 cleanup 证明。

### Tier B：目标语义微场景

Tier B 固定覆盖 Commit cost/cooldown、Frozen target/AliveOnly、9203 精确 period/stack、Death、wait/cancel、immunity/inhibition、Exact/Inclusive Tag、LeaveGranted 和 Cue/FinalDrain。每个机制使用独立微场景，不依赖 Tier A 的 count > 0。

### Tier C：扩展样例

| Ability | Target | Effect | 关键语义 |
|---|---|---|---|
| 盾击 | 敌方前排单体 | damage + duration stun | 多 Effect DirectEffectProgram、Tag grant/remove、Duration Cue |
| 冰霜新星 | 距离最近 3 个敌人 | damage + slow | 多目标 fan-in、target ordinal、各目标独立 capture |
| 羁绊/治疗 | roster/低血量友军 | duration/infinite buff 或 instant heal | 低频 intent、target rule、positive modifier |

Tier C 只是目标扩展，不能冒充当前 AutoChess 已覆盖。

## Ability Definition

每个 Definition 配置 activation required/blocked tags、cancel/block tags、cost/cooldown、instance/retrigger policy、target rule、TargetLifePolicy、Avatar/spatial binding policy、DirectEffectProgram 和 Cue/trace policy。Grant 后进入 `GrantedAbilitySlot`；每次执行创建独立 ActivationSlot，不创建 Ability Entity。

普攻/技能的 cost/cooldown 在 Commit 时重查并在 owner-local CommitPlan 中原子提交。已 Commit 的远程 work 不因 source 后续死亡而撤回；各 target application 仍按自己的 TargetLifePolicy 输出 accepted/typed reject。

AutoChess hostile ability 默认使用 Frozen ASC target + AliveOnly；失效、死亡或 binding mismatch 只能拒绝，绝不 implicit fallback self。Self target 必须由 Definition 显式声明。

## GameplayEffect Definition

每个 GE 保持 Application/Ongoing/Removal/Immunity requirements、duration/period、stack/overflow、modifiers/executions、granted tags/abilities/cues 与 capture contracts 的独立 ranges。

- Damage/Periodic Poison execute 修改 Health Base，随后 Aggregator 重算 Current。
- Stun/Slow Duration Effect 创建 ActiveEffectSlot；inhibit 与 remove 语义分离。
- stack 合并保持 ActiveEffectHandle，但每次 application 有独立 ApplicationId。
- owner-local PeriodDue 在 DueTick 同 tick 执行；post-apply overflow/reaction/cross-owner dynamic child 默认 T+1。
- 静态闭合、生成期可完全展开且有界的 DirectEffectProgram 仍可 same tick；不得用 post-apply fact 回灌伪造同 tick。

## 9203 主业务毒的唯一目标语义

| 字段 | 目标值 |
|---|---|
| StackKey | `(Definition, TargetASC, SourceASC)` |
| StackType | `AggregateBySource` |
| StackPayloadPolicy | `ReplaceLatest` |
| Source capture | 每次成功 application 在 SpecCreation 捕获 Source Attack Snapshot，并替换 active stack payload |
| Period magnitude | `SnapshotAttack * 0.3 * CurrentStackCount` |
| Duration / Period / Limit | `8 / 2 / 3` SimulationTicks |
| ExecuteOnApply | `false` |
| Duration refresh | application 不刷新 duration |
| Period reset | 每次成功 application 都重置 `NextDueTick = ApplyTick + 2` |
| Cap reapply | stack 保持 3，但 application 仍 accepted；替换 latest payload/Application provenance 并 reset period，不刷新 duration |
| Due = End | 同 tick 先用当前 stack/payload 执行 period，后执行 expiry |
| Expiration | `RemoveSingleStackAndRefreshDuration`；stack > 1 时减 1，以 expiry tick 刷新 duration 并重置 period；stack=1 时 Remove |
| Inhibition | duration 继续推进；due period skip；解除后不 catch-up，从下一法定 due 继续 |
| Kill provenance | period 致死归因 latest successful application，使用已冻结 Source/Application/Causality，不反查已结束 Activation |

当前 `StackingType=9203`、`StackCode=0`、sourcegen Energy dummy modifier/refresh/expiration override、9204 固定 `-1` 且不乘 StackCount 均是 breaking/bake-fail 输入。v1 由 9203 Definition 直接编译 inline pure period evaluator，删除 9204 的目标态 identity/Catalog row，不保留兼容分支；配置事实见 [AutoChess 真实业务链二轮审查事实](../../00-当前架构事实/AutoChess真实业务链二轮审查事实.md)。

## 9207 Instant pure evaluator

9207 不创建 ActiveEffectSlot，不使用无限 duration marker，不注册 Demo Core System。它是 Catalog 声明的 Instant execution：

```text
HealthIndex = EffectDefinition.ValueView.HealthLayoutIndex (bake-resolved)
HealthBefore = Target.AttributeValueSlot[HealthIndex].Base
HealthMax = AttributeLayout[HealthIndex].MaxValue
MissingHealth = max(0, HealthMax - HealthBefore)
Damage = clamp(16 + MissingHealth * 0.5, 12, 42)
write Target.AttributeValueSlot[HealthIndex].Base -= Damage and increment Revision
then recompute/clamp the same slot Current through the single Aggregator lane
```

Base / Current / definition MaxValue 必须是 evaluator descriptor 的显式 `ValueView`，且 layout index 在 bake 时解析；禁止在 glue 中隐式改读 Current、用 Current 反写 Base或回退到 per-attribute component。9207 只输出 Application/Execution/Attribute/ExecutedCue facts，无长期 slot 与 Removed Cue。

## Cue

- Instant/period execution：`Executed`；instant identity 使用 `(EffectApplicationId,CueDefinitionOrdinal)`，period identity 使用 `(SimulationEpoch,ActiveEffectHandle,PeriodExecutionOrdinal,CueDefinitionOrdinal)`。
- Stun/Slow/Poison duration：`OnActive -> WhileActive -> Removed`，使用 ActiveEffectHandle + ActiveCycleOrdinal + CueDefinitionOrdinal 配对。
- Cue payload 包含 stable source/target、EffectContext、magnitude snapshot、Avatar binding generation/空间快照。

## 验收

- Tier A 使用 `BattleLocalTick` / `ScenarioUnitId` 的业务 golden，不锁定 raw Entity、旧 group timing 或 current hash 污染。
- Tier B 覆盖 Commit 重查、Frozen/AliveOnly/no-self-fallback、9203 全表、Death/cancel、wait、immunity/inhibition、Tag count、LeaveGranted 与 Cue lifecycle。
- 9207 无 ActiveEffectSlot，Base/Current ValueView 测试、斩杀伤害和 ExecutedCue golden 通过。
- Tier C 未运行的盾击/冰霜/羁绊不计入当前覆盖。
