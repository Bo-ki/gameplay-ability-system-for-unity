# AutoChessDemo 策划配置验收样例 Spec

## 目的

本 Spec 从真实策划配置能力视角定义 AutoChessDemo 的验收测试样例。它连接：

1. `10-AutoChess无头验收Spec.md`：验收基础设施、规模门槛和 evidence model。
2. `10B-AutoChess完整业务案例设计Spec.md`：具名棋子、技能、GE、业务走查和交互矩阵。
3. `11-AutoChessDemo-Luban配置方案Spec.md`：Luban 表、SourceGenerator 输出、ScaleProfile、ValidationExpectation。
4. `20-策划配置能力交叉审查Spec.md`：Business Package、Change Set、Impact Analysis、Runtime Trace Preview、Scenario Validation Binding。

本文件不新增 Runtime Core 规则；它定义 AutoChessDemo 应如何作为“策划配置能力验收场”。

## 相邻 Spec Owner 边界

| 相邻 Spec | 唯一职责 | 本文件消费方式 | 禁止重复 |
|---|---|---|---|
| `10-AutoChess无头验收Spec.md` | runner、scale gate、Debugger / Profiler / Journaling evidence、无头表现 marker、性能阈值 | 样例绑定到 `10` 的 Scenario Validation Evidence 和 runner / scale gate | 不重复维护 runner pass、performance / diagnostic split 或 Goal 停止阈值 |
| `10B-AutoChess完整业务案例设计Spec.md` | 具名单位、Ability / GE / Tag / Attribute、业务走查、System 链路 | 把 10B 的具体业务内容拆成可发布、可回归的 Acceptance Ability Sample | 不重复维护完整棋子 / System 设计正文 |
| `11-AutoChessDemo-Luban配置方案Spec.md` | Luban 表结构、生成物、ScaleProfile、ValidationExpectation、DiagnosticsThreshold | 每个样例声明 row projection、trace、expectation 和 generated metadata 消费点 | 不重复维护配置 schema、generated artifact 清单或性能阈值字段 |
| `20-策划配置能力交叉审查Spec.md` | 通用 Business Package、Change Set、Impact Analysis、Runtime Trace Preview、Scenario Validation Binding | 将通用策划配置能力落到 AutoChess Acceptance Ability Sample | 不重复维护通用配置能力规则 |

本文件的目标态边界是“样例化消费”：把业务包、row projection、trace preview、scenario binding、balance preview、impact analysis 和 acceptance assertions 组织成可发布的验收样例。它不定义 Runtime Core 规则，不定义 Luban schema，也不定义 runner infrastructure。

## AutoChess Acceptance Ability Sample

AutoChess 验收测试样例的最小单位不是单个 C# test，也不是单张 Excel row，而是一个 **AutoChess Acceptance Ability Sample**。每个样例必须包含：

| 字段 | 说明 |
|---|---|
| `SampleId` | 样例稳定 id，例如 `ACCEPT-AC-001` |
| `ValidationTier` | Tier A LegacyCharacterization / Tier B TargetSemanticConformance / Tier C Extension |
| `BusinessPackage` | Ability Package / Effect Package 名称 |
| `PlannerIntent` | 策划意图，用业务语言描述 |
| `Template` | 使用的 Business Template |
| `RowProjection` | 需要生成或修改的 unit / ability / GE / tag / cue / scenario / expectation rows |
| `ConfigReferenceGraph` | 正向和反向引用关系 |
| `RuntimeTracePreview` | 预期进入的 plan / seed / modifier / fact / cue 链 |
| `ScenarioValidationBinding` | 绑定的 scenario、scale profile 和 validation expectation |
| `BalancePreview` | 关键公式、tick、stack、cooldown、cost 和异常曲线 |
| `AcceptanceAssertions` | 机器可读断言 |
| `ImpactAnalysis` | 修改该样例核心 GE / Tag / Cue / Formula 时应影响的对象 |

## 核心测试能力样例

样例按三层报告：Tier A 只刻画真实 9101–9104 主战；Tier B 证明目标语义；Tier C 的盾击、冰霜、治疗与羁绊只证明扩展性。三层不得合并为一个“AutoChess 已通过”。Tier B 的固定微场景清单由 `10-AutoChess无头验收Spec.md` 持有，本页只定义配置投影。

### ACCEPT-AC-001：普攻伤害闭环

| 项 | 内容 |
|---|---|
| ValidationTier | Tier A `LegacyCharacterization` |
| BusinessPackage | `BasicAttackDamagePackage` |
| PlannerIntent | 存活单位按 `ActiveDue > Finisher > Primary` 选择行动，按稳定 `ScenarioUnitId` tie-break 选择同 BattleInstance 敌方 Frozen ASC；9101/9102 普攻分别固定扣 12/8 Health |
| Template | `SingleTargetDamageAbility` + `InstantDamageEffect` |
| RowProjection | Ability 9101/9102；Instant GE 9201/9202；ValidationExpectation 标记 `LegacyCharacterization` |
| ConfigReferenceGraph | Unit -> 9101/9102 -> 9201/9202 -> fixed Health delta -> semantic_x1 / replicated_groups_50 |
| RuntimeTracePreview | `RequestActivate -> AscOwnerCommandWave Commit -> AscTargetStateWave -> Health Base Delta(-12/-8) -> typed outcome/fact` |
| ScenarioValidationBinding | `semantic_x1`；legacy `x50` 只能映射 50 个隔离 4 单位 `replicated_groups_50` |
| AcceptanceAssertions | winner/count/finish tick、BattleLocalTick、ability priority 与 ScenarioUnitId target order 稳定；不得据此宣称 cost/cooldown、wait、immunity 或 Cue 四阶段已通过 |
| ImpactAnalysis | 修改 9201/9202 时影响 9101/9102、Tier A baseline 与 replicated-groups expectation |

为什么它是第一样例：它覆盖最短 GAS 链路，能证明 Ability -> GE -> Modifier -> AttributeDelta -> Fact -> Cue 的基本闭环。

### ACCEPT-AC-002：盾击眩晕与行动阻断（Tier C 扩展）

| 项 | 内容 |
|---|---|
| ValidationTier | Tier C `Extension`；不构成当前覆盖 |
| BusinessPackage | `ShieldBashStunPackage` |
| PlannerIntent | 霜甲剑士释放盾击，对最近敌人造成伤害，并施加 2 秒眩晕；眩晕期间目标不可释放主动技能 |
| Template | `SingleTargetDamageAbility` + `DurationTagEffect` |
| RowProjection | Ability 3001（BlockTagRequirementId=7001）；GE 5001 盾击伤害；GE 5002 眩晕（GrantedTagIds=[1]）；TagId 1=`State.Stunned`、TagId 3=`State.Frozen`；TagRequirement 7001=`Any Inclusive [1,3]`；Cue `ShieldBashVFX/SFX`；ValidationExpectation required facts `DamageResolved,EffectApplied,EffectExpired` |
| ConfigReferenceGraph | Unit 2001 / 2005 -> Ability 3001 -> GE 5001 + GE 5002 -> TagId 1 -> TagRequirementId 7001(TagIds 1,3) -> ShieldBash cue |
| RuntimeTracePreview | `AbilityActivationCommand(3001) -> DirectEffectProgram(5001/5002) -> AttributeDelta(Health,-Attack*0.8) + ActiveEffectSlot(Stunned) -> BoundaryFact(EffectApplied/Expired/ShieldBashCue)` |
| ScenarioValidationBinding | x1 默认场景；专用 expectation 要求 target 在 Stunned window 内不产生主动 Ability activation |
| BalancePreview | ManaCost 40、Cooldown 180、Damage ATK*0.8、Duration 120 frames；展示眩晕覆盖率和技能阻断窗口 |
| AcceptanceAssertions | Stunned tag 授予和移除各出现一次；眩晕窗口内目标主动技能 activation count = 0；到期后 tag 清除；ShieldBash cue marker 存在 |
| ImpactAnalysis | 修改 TagId 1=`State.Stunned`、TagId 3=`State.Frozen`、TagRequirement 7001 或 GE 5002 时，影响所有 BlockTagRequirementId 引用、盾击样例与 Frozen/Stunned 互斥校验 |

为什么它重要：它验证 Duration GE、GrantedTagId、BlockTagRequirement、Effect expire、状态对 Ability 激活的约束，而不是只验证数值扣血。

### ACCEPT-AC-003：毒刃周期叠加

| 项 | 内容 |
|---|---|
| ValidationTier | Tier B `TargetSemanticConformance` |
| BusinessPackage | `PoisonBladePeriodicStackPackage` |
| PlannerIntent | 9104 对 Frozen target ASC 施加 9203：Duration 8、Period 2、最多 3 层，同 source 聚合；Source Attack 在 `SourceSpecProjection` snapshot，每次成功 application 用本次 snapshot 替换 payload，period 伤害随当前层数增加 |
| Template | `PeriodicStackingEffect` |
| RowProjection | Ability 9104；GE 9203；Tag `State.Poisoned`；Cue lifecycle；ValidationExpectation required facts `ApplicationAccepted,StackChanged,PeriodExecuted,EffectExpired/Removed` |
| ConfigReferenceGraph | Unit -> 9104 -> 9203 -> `(Definition,TargetASC,SourceASC)`/AggregateBySource/ReplaceLatest -> Poisoned/Cue -> semantic + period-burst scenarios |
| RuntimeTracePreview | `9104 Commit -> 9203 accepted -> replace Snapshot/Provenance + reset NextDue -> DueTick Health.Base -= SnapshotAttack*0.3*StackCount -> period fact/cue` |
| ScenarioValidationBinding | `semantic_x1` 验证 1/2/3/cap/expiry/inhibit；`period_burst` 只验证同 tick due 和容量压力 |
| BalancePreview | Duration 8、Period 2、StackLimit 3；apply 不刷新 duration；cap reapply 替换 payload 并 reset due；Due=End 时 period 先于 expiry |
| AcceptanceAssertions | ExecuteOnApply=false；expiry remove-one+refresh duration+reset period；inhibit duration 继续、period skip/no catch-up；period kill provenance 取 latest application；slot/Tag/Cue 无泄漏 |
| ImpactAnalysis | 修改 9203 任一公式/period/stack/capture 列时，影响 9104、毒微场景、period-burst profile、generated evaluator/Blob/hash；legacy 9204 不在兼容范围 |

为什么它重要：它覆盖 Duration + Period + Stack + ActiveEffectStore，是 GAS 配置复杂度最高、最容易被 raw protocol 弄错的链路。

### ACCEPT-AC-004：冰霜新星 AoE 减速（Tier C 扩展）

| 项 | 内容 |
|---|---|
| ValidationTier | Tier C `Extension`；不构成当前覆盖 |
| BusinessPackage | `FrostNovaAoeSlowPackage` |
| PlannerIntent | 冰霜女巫释放冰霜新星，对最多 3 个最近敌人造成伤害，并施加 3 秒减速 |
| Template | `AreaDamageAbility` + `DurationTagEffect` |
| RowProjection | Ability 3002；GE 5003 冰霜伤害；GE 5004 减速；Tag `State.Slowed`；Cue `IceNovaVFX/SFX`；TargetRule `NearestEnemies` `MaxTargets=3` |
| ConfigReferenceGraph | Unit 2002 -> Ability 3002 -> TargetRule NearestEnemies -> GE 5003 + GE 5004 -> Slowed tag -> IceNova cues |
| RuntimeTracePreview | `AbilityActivationCommand(3002) -> TargetRecords(max 3) -> EffectApplicationSpec(5003/5004 per target) -> Health Delta + ActiveEffectSlot(Slowed) -> BoundaryFact(IceNovaCue)` |
| ScenarioValidationBinding | x1 默认 4v4；expectation 要求 target count <= 3 且每个 target 输出 damage / slow facts |
| BalancePreview | ManaCost 80、Cooldown 300、AoE MaxTargets 3、Damage MagicPower*1.5-DEF*0.3、Slow ASPD*0.7 |
| AcceptanceAssertions | Target count 不超过 3；至少 2 个目标产生 DamageResolved；Slowed tag 授予和移除；IceNova cue marker 数与 target / cast 对齐 |
| ImpactAnalysis | 修改 TargetRule、MaxTargets、GE 5004 或 Slowed tag 时，影响 AoE scenario、presentation marker expectation 和 target resolve capacity hint |

为什么它重要：它验证 target resolve、多目标 GE seed、Boundary cue fan-out 和 tag duration 的组合。

### ACCEPT-AC-005：圣光治疗与牧师增益（Tier C 扩展）

| 项 | 内容 |
|---|---|
| ValidationTier | Tier C `Extension`；不构成当前覆盖 |
| BusinessPackage | `HolyLightHealSynergyPackage` |
| PlannerIntent | 圣光牧师治疗血量最低友军；当牧师羁绊满足阈值时，治疗量提升 20% |
| Template | `SingleTargetHealAbility` + `TypedFactReactionRequirement` |
| RowProjection | Ability 3004；GE 5006；Tag / Synergy `Synergy.Priest`；Cue `HealBeamVFX` / `HealText`；Scenario 增加 Priest synergy expectation |
| ConfigReferenceGraph | Unit 2004 -> Ability 3004 -> TargetRule LowestHPAlly -> GE 5006 -> Heal modifier -> Priest synergy requirement -> HealBonus tag/fact |
| RuntimeTracePreview | `AbilityActivationCommand(3004) -> TargetRecord(LowestHealthAlly) -> EffectApplicationSpec(5006) -> AttributeDelta(Health,+MagicPower*0.8*SynergyMultiplier) -> BoundaryFact(HealResolved/ExecutedCue)` |
| ScenarioValidationBinding | 专用 synergy scenario：2 个 Priest 单位；expectation 要求 HealResolved 和 SynergyActivated |
| BalancePreview | ManaCost 40、Cooldown 240、Heal MagicPower*0.8、Synergy multiplier 1.2；展示无羁绊 / 有羁绊治疗对比 |
| AcceptanceAssertions | LowestHPAlly 选择正确；治疗不超过 MaxHP；SynergyActivated fact 存在；HealText marker 存在；无羁绊场景 healing <= 有羁绊场景 healing |
| ImpactAnalysis | 修改 GE 5006 或 Priest synergy 阈值时，影响 Unit 2004、Priest scenario、Heal expectation 和 balance preview |

为什么它重要：它验证正向属性 modifier、target rule、typed fact reaction 和业务平衡预览。

### ACCEPT-AC-006：配置影响分析回归样例

| 项 | 内容 |
|---|---|
| ValidationTier | Tier B 配置回归 |
| BusinessPackage | `PoisonBladeBalanceChangeSet` |
| PlannerIntent | 将毒刃 tick 伤害从 `ATK*0.3` 调整为 `ATK*0.25`，发布前明确影响范围 |
| Template | `BalanceChangeSet` |
| RowProjection | 修改 GE 9203 的 `ModifierMmc`；不新增 Runtime row；更新 validation expectation 的 expected damage range / semantic hash |
| ConfigReferenceGraph | GE 9203 -> Ability 9104 -> unit/scenario -> semantic poison / period-burst scenario -> compiled evaluator glue |
| RuntimeTracePreview | 旧：`AttributeDelta(Health,-Attack*0.3*StackCount)`；新：`AttributeDelta(Health,-Attack*0.25*StackCount)` |
| ScenarioValidationBinding | 必须重新跑 Tier B poison conformance；`period_burst` 只做同 tick due/capacity 压力，不替代语义场景 |
| BalancePreview | 展示 1/2/3 层每 tick 伤害和总伤害变化，标记是否低于最低击杀阈值 |
| AcceptanceAssertions | ImpactAnalysis 列出 Ability 9104、GE 9203、Poison scenarios、period-burst profile、compiled Blob/evaluator glue；PublishValidationSnapshot content hash 变化；未受影响的 Tier C 样例不进入影响列表 |
| ImpactAnalysis | 该样例本身就是影响分析门禁 |

为什么它重要：它验证策划配置能力，而不是 GAS 运行语义。真实项目中最常见的是平衡改数值，工具必须解释改动影响，而不是只保存 Excel。

## 样例到 Luban 表的验收映射

| 样例 | 必改表 | 必查生成物 | 必查 expectation |
|---|---|---|---|
| ACCEPT-AC-001 | ability、gameplay_effect、cue、validation_expectation | Ability lookup、GE blob、MmcEvaluator、Cue marker | DamageResolved、DamageText |
| ACCEPT-AC-002 | ability、gameplay_effect、tag、cue、validation_expectation | Tag mask、GE active mutation seed、requirement evaluator | EffectApplied、EffectExpired、StunnedBlock |
| ACCEPT-AC-003 | ability、gameplay_effect、tag、cue、scale_profile、validation_expectation | ActiveEffectStore data、stack policy、period tick evaluator | StackChanged、PeriodTick、BufferPressure |
| ACCEPT-AC-004 | ability、gameplay_effect、tag、cue、scenario、validation_expectation | TargetRule table、multi-target seed range、Cue fan-out | TargetCount、SlowedExpire、IceNovaCue |
| ACCEPT-AC-005 | ability、gameplay_effect、tag、scenario、validation_expectation | Heal evaluator、synergy requirement evaluator、target rule table | HealResolved、SynergyActivated |
| ACCEPT-AC-006 | gameplay_effect、validation_expectation | MmcEvaluator diff、content hash、impact graph | ImpactAnalysis、PublishValidationSnapshot |

## 验收测试结构

每个样例至少派生三类测试：

| 测试层 | 输入 | 输出 | 失败条件 |
|---|---|---|---|
| Config Review Test | Luban Business Package Change Set | Config Review Gate summary | 缺 row projection/provenance、非法 enum/range、同字段 overlay、SourceGenerator 注入语义、缺 DOTS carrier hint |
| Runtime Trace Test | generated catalog metadata + pure glue | Runtime Trace Preview | plan / seed / modifier / fact / cue 链不完整，或读取 Runtime World active state |
| Scenario Validation Test | Publish Validation Snapshot + Scenario Validation Binding | AutoChessValidationEvidence | RequiredFact / RequiredCue / summary hash / threshold 失败 |

默认顺序：

```text
Config Review Test
  -> Luban bake/provenance negative tests
  -> SourceGenerator pure-glue/generated metadata check
  -> Runtime Trace Test
  -> Tier A Legacy Characterization
  -> Tier B Semantic Conformance micro-scenarios
  -> typed Scale Test（按 WorkloadKind）
  -> Tier C Extension（按需，不影响主线结论）
```

## 目标发布快照样例

`ACCEPT-AC-003` 的发布快照至少应包含：

| 字段 | 示例值 |
|---|---|
| `BusinessPackageId` | `PoisonBladePeriodicStackPackage` |
| `ValidationTier` | `TargetSemanticConformance` |
| `ChangedRows` | Ability 9104, GE 9203, Tag Poisoned, Cue lifecycle, Expectation PoisonDOT |
| `ReferenceGraphHash` | 来源于 Unit -> Ability 9104 -> GE 9203 -> Tag/Cue/Scenario |
| `ImpactAnalysisId` | `impact.poison.9203` |
| `RuntimeTracePreviewId` | `trace.poisonblade.periodic_stack.v1` |
| `ScenarioValidationEvidenceId` | `scenario.poison.9203.semantic.v1` |
| `ScaleProfileIds` | `semantic_x1`, `period_burst` |
| `LubanBakeProvenance` | workbook/table/row/column/raw/normalized 均可追溯 |
| `SourceGeneratorGateSummary` | pure glue / no field override / no dummy modifier / no lifecycle / evaluator dispatch generated |
| `SemanticHashContract` | include stable identity/tick/order/outcome/state；exclude batch/raw Entity/wall-clock/profiler/allocator/teardown |
| `DotsCoverageSummary` | `BLOB-01`, `BUR-01`, `NAT-03`, `BUF-01`, `SYS-05`, `ODF-*` |

## 禁止方向

1. 不把这些样例写成 runner 常量或 C# test 私有数据；它们必须能从业务包和 Luban rows 推导。
2. 不把 `10B` 的完整业务设计直接当作验收完成证明；必须能拆成独立 sample 并输出 Change Set / Snapshot / Evidence。
3. 不用运行后的 Debugger 事实反推配置正确；Runtime Trace Preview 必须来自 generated metadata 和 pure glue。
4. 不让 ScaleProfile / ValidationExpectation 成为测试脚本字段；它们必须是配置权威源。
5. 不为了样例快速通过而绕过 Cue / Presentation marker；无头也必须输出 marker evidence。
6. 不让 SourceGenerator sidecar 覆盖 Luban 语义；同字段冲突、非法 enum/range 与缺失 ValueView 必须携 provenance bake fail。
7. 不用 legacy count/hash、x50 replicated groups 或 Tier C 展示样例替代 Tier B semantic conformance。
