# 10B-07 执行链、生成物与验收矩阵

## 唯一执行链

```text
AutoChess AI/Roster
 -> CommandPort/SessionIngressGate -> BoundaryIngressJournal
 -> pre-Fixed GasCommandIngressSystem -> ECS BoundaryCommandInbox
 -> FixedStep Physics
 -> GasTickKernel
    Gather/TickStartSnapshot + PlanExpandScratchProvision/token
    -> OwnerPlanBuild -> TargetResolve/BoundedExpand
    -> WholeTickInfraAdmission
    -> AscOwnerCommandWave -> SourceSpecProjection
    -> GroupByTarget -> TargetPrepare/Stabilize/Death (shadow)
    -> SessionFaultReduce -> TargetPublish -> StableFactMerge/per-BattleInstance TerminalResolve
    -> GroupNextTickRouteByDestination -> BoundaryProject -> Record EndFixed
 -> standard EndFixed ECB
 -> one Boundary Drain
 -> Battle report / Cue / UI / Replay / Headless
```

AutoChess 自定义名称可以出现在 Definition/evaluator/consumer，不注册进 Kernel 中间成为第二 schedule owner。

## 三层验收矩阵

| 层级 | 场景 | 证明内容 | 不证明内容 |
|---|---|---|---|
| Tier A | 9101/9102/9103/9104 legacy 实战 | winner/count/finish tick、AI priority、BattleLocalTick、ScenarioUnitId tie-break | v1 完整语义 |
| Tier B | 固定 semantic micro-scenarios | Commit 原子性、wait、9203、9207、life policy、Cue/Boundary/teardown | 扩展玩法内容完整性 |
| Tier C | 盾击、冰霜、羁绊等 | 公共契约可扩展性 | 当前 AutoChess 覆盖 |

Tier B 固定微场景必须覆盖：SpawnFinalize整批 Ready/typed failure、cost/cooldown rollback、admission fault预排 Job no-op且权威零写、Commit 后 source death、same-target death fan-in、wait sample+register、9203 cap/expiry/inhibit、9207 ValueView、Cue lifecycle、Boundary retry/late tail/NoFactReceipt/FinalDrain、零 ASC Session terminal、multi-BattleInstance terminal isolation。

## 生成物

- Unit/Ability/Effect/Attribute/Tag/Cue Catalog Blob。
- AttributeLayout、Tag ancestor chains 与 Definition indices。
- target/damage/magnitude pure evaluator。
- requirement phase ranges、CaptureProjectionContract、DirectEffectProgram。
- dependency/cycle validation、content/schema hash、ScaleProfile report。
- Editor metadata/diagnostics；零 generated Runtime System。

Luban 是 authoring 语义唯一权威，sourcegen 只生成 glue；任何同字段 overlay、非法 enum/range 或虚构 modifier 都必须携 provenance bake fail。Runtime 只消费 bake 后 immutable compiled Blob snapshot。

## Semantic hash 与规模证据

Semantic hash 包含：stable ASC/Definition/Spec/Effect/Cue identity、SimulationTick、canonical sequence、typed outcome、Attribute/Tag/slot 最终值、BattleOutcome 与 `FactPlane=Gameplay` facts。明确排除：batch 切分、raw Entity、chunk/job 顺序、wall-clock、Profiler sample、allocator 地址与 `FactPlane=TeardownAudit` facts；后者只进独立 TeardownAuditHash。

规模验证按 workload 类型拆分，不能用一个 `x50` 数字替代：

- replicated groups；
- hot target fan-in；
- period burst；
- mass death + teardown；
- boundary burst/retry；
- wait fanout/cancel；
- cross-ASC live dirty。

每类报告 bounded `TickBatch`、slot/scratch/boundary capacity、allocator high-water、retry/overflow typed outcome 与稳定 semantic hash。

## P0 验收

- 业务：Tier A legacy count characterization 与 Tier B target semantic conformance 分开报告；winner、damage、finisher、finish tick、kill credit 与 Cue sequence 可追溯。
- 语义：并发 Activation/Continuation、commit、stack/period/inhibition、capture、Tag count、Death。
- 架构：旧五组/Ability Entity/legacy GE/自定义 ECB/多 Boundary consumer 零运行引用。
- 时序：0..N FixedStep、owner-local PeriodDue same tick、dynamic reaction T+1、结构实体 next tick、FinalDrain 后冻结结果。
- 确定性：相同输入/hash 的 trace/state/fact/battle hash 一致。
- 规模：七类 workload 的 memory/scratch/slot/stabilization/Core/Drain/consumer evidence；禁止以 raw Entity 或 batch 切分参与语义 hash。
