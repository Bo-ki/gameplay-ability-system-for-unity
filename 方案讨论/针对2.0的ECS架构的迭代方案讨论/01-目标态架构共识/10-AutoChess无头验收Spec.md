# AutoChess 无头验收 Spec

## 结论

Headless、Scene 和 Scale runner 必须使用同一 `GasRuntimeSession`、同一 FixedStep/Physics/GAS/EndFixed/Drain 链和同一业务输入。无头只省略真实渲染/音频资源，不省略 Cue marker、Boundary Drain、Replay、Debugger evidence 或 physics/time semantics。

验收不把当前 AutoChess count 链当作目标语义已完成：Tier A 只做 legacy real characterization，Tier B 用固定微场景验证 Runtime v1 语义，Tier C 的盾击/冰霜/羁绊等扩展只在对应场景真实运行后计入覆盖。

## Runner Contract

```text
Install one active Session in one World
  -> validate package/content/schema/layout/tick-rate/scale-profile hash
  -> SpawnBatch Pending: preflight every BattleInstance/ASC/grant/capacity
  -> EndFixed creates Pending batch
  -> next FixedStep Kernel SpawnFinalize maintenance -> whole-batch Ready/publish（not a gameplay tick）
  -> feed deterministic external commands
  -> TickBatch complete parent chain until each BattleInstance terminal condition
  -> consume immutable BoundaryBatch/ReadModel/Diagnostics
  -> complete terminal tick DAG -> managed gameplay FinalDrain -> freeze BattleOutcome
  -> after all battles terminal or explicit stop: Session Terminalizing
  -> teardown + cleanup audit -> freeze ValidationResult -> dispose Session
```

Runner 只调用 Session capability，不缓存/更新内部 Group/System，也不直接读取/清理 ECS buffers。独立 World 显式安装标准 Begin/End FixedStep ECB 与唯一 Drain。

一个 World 同时只有一个 active GAS Session，但 Session 可包含多个 `BattleInstanceId`。单个战局终局只封闭该战局的 ingress，不能误停其他 replicated battle groups。

## 时间口径

- 权威口径是 SimulationTick；一个 RenderFrame 可以 0..N fixed ticks。
- tick rate 来自 Scenario/ScaleProfile，Session 启动后不可变并进入 hash。
- Spawn/Ready、measurement warmup、measured、gameplay FinalDrain 和 teardown 分开统计。Ready 不是 warmup tick 的别名。
- bootstrap、Burst/Editor warmup、Journaling、资源加载不混入 Core tick。
- hard-coded 60Hz、采样 tick 数或毫秒阈值不是框架不变量；具体值由 profile 声明。
- 终局后禁止再运行固定数量的完整 gameplay tick 作为 flush；FinalDrain 以事实 watermark/quiescence 完成，不以 wall clock 或固定 tick 数决定。

## 分层验收基线

| Tier | 作用 | 可保留结论 | 不能冒充 |
|---|---|---|---|
| A legacy real characterization | 固定当前 9101/9102 攻击、9103 斩杀、9104/9203 毒、AI `ActiveDue > Finisher > Primary` 和胜负输出 | 业务级 damage/winner/fact count 及有意 breaking diff；时间归一为 `BattleLocalTick`，破平归一为稳定 `ScenarioUnitId` | cost/cooldown、wait/cancel、immunity/inhibition、Tag count、Cue lifecycle、cleanup |
| B target semantic conformance | 使用小而固定的单一机制场景 | Runtime v1 定义的精确 success/reject/state/tick/hash | 用 Tier A count > 0 代替精确断言 |
| C extension scenarios | 盾击、冰霜新星、羁绊、治疗、资源表现 | 对应场景实际运行后的覆盖 | 目标文档或配置样例存在本身 |

## P0 业务覆盖

| 链路 | 最小场景 | 断言 |
|---|---|---|
| Activate/Commit | 普攻或盾击 | CanActivate/Commit 重查、cost/cooldown 原子、重复 commit 拒绝 |
| Multi-target | 冰霜新星 | 稳定目标序、多 target 独立 Application/capture/result |
| Duration/Tag | Stun/Slow | Active/Inhibited/Removed、Exact/Inclusive count、Cue 配对 |
| Stack/Period | Poison | source key、stack merge、due tick、overflow next-tick policy |
| Capture/Aggregator | Damage/Poison | Source/Target × Snapshot/Live、late tags/filter、stable override/order |
| Continuation | 外部/定时 wake | N waits、cancel/late event、双 Generation 校验 |
| Reaction | GameplayEvent/Tag | emit T、deliver T+1、payload 冻结、链每边+1 tick |
| Death | Health=0 | Owner wave 已可见的 death 阻止后续 owner Commit；Target wave incoming death 不撤回已 Commit work，Death reaction T+1，terminal drain |
| Boundary/Cue | Instant+Duration | Executed 与 OnActive/WhileActive/Removed、Destroy 后不丢 |

完整业务样例见 [10B](10B-AutoChess完整业务案例设计Spec.md)。

Tier B 固定微场景不得缺少：

1. SpawnBatch 中一个 init/grant 失败，整批不进入 Ready。
2. 同 ASC 两条 Commit，第二条读到第一条 cost/cooldown 写入；重复 Commit 不重复扣费。
3. Frozen ASC target 在 Commit 后死亡；source 死亡不撤回已 Commit work，而 target AliveOnly 在 canonical 顺序中明确拒绝，绝不 fallback self。
4. 9203 毒的 1/2/3 层、cap reapply、Due=End、expiry remove-one、inhibit skip 和 period kill provenance。
5. 同 target 多伤害致死：首次 Death 冻结 killer/overkill，后续 AliveOnly reject 不计 damage/assist。
6. N 个 wait、Level sample/register、Edge/Event 不追溯、cancel/late completion 和 observed ASC destroy。
7. application requirement、immunity、ongoing inhibit/reactivate 与 exact/inclusive Tag 多来源计数。
8. Effect-granted Ability 的 `CancelImmediately`、`RemoveWhenAllActivationsEnd`、`LeaveGranted`（UE `DoNothing` 的可观察语义；内部 detach ownership）与 ASC teardown。
9. Cue OnActive/WhileActive/Executed/Removed 键配对，Boundary staging 失败时 outbox 不清空。
10. BattleOutcome 在 gameplay FinalDrain 后冻结，ValidationResult 在 teardown audit 后返回，teardown facts 不改 battle hash。

## ValidationEvidence

最低字段：

```text
Session: Epoch, Unity/Package, TickRate, Content/Schema/Layout/ScaleProfile Hash
Scenario: Seed, UnitCount, Start/EndTick, Winner, Commands, Damage, Kills
Semantics: Activations, Commits, Continuations, Applications, Stacks, Periods,
           Captures, TagTransitions, Reactions, Cues, TerminalFacts
Runtime: TargetGroups, Scratch/Slot/Buffer HighWater, Spill/Fault,
         Stabilization Iterations, Stale/Reject Reasons
Boundary: Batches, Facts, DeadShells, RingHighWater, Overflow/Reconcile
Determinism: CommandTraceHash, SlotLifecycleHash, FinalStateHash,
             BoundarySequenceHash, BattleHash
Evidence: ProfilerCaptureId, JournalingCaptureId, GeneratedReportHash
```

日志和 Markdown summary 只能从该模型派生。

Semantic hash 字段口径：

- 包含：Session/content/schema hash、`BattleInstanceId`、`ScenarioUnitId`、`BattleLocalTick`、Request/Activation/Application/Causality identity、semantic code/reason、canonical ordinal、Attribute/Tag/stack/Cue lifecycle 值、BattleOutcome。
- 排除：raw Entity index/version、进程静态 report key、RenderFrame、wall clock、Job/chunk/batch 切分、Profiler/Journaling 记录数、presentation 采样/截断、teardown-only audit facts。
- `BattleHash` 在 gameplay FinalDrain 后冻结；`TeardownAuditHash` 独立记录 cleanup，不回写 BattleHash。

## 功能、规模与证据 Pass

| pass | 目的 | 开启内容 |
|---|---|---|
| Functional x1 | 全语义与业务 golden | 详细断言，轻量 trace |
| Determinism | 相同输入重复运行 | 比较全部 semantic hashes |
| Scale typed profiles | 内存/pressure/复杂度趋势 | replicated groups、hot target、period burst、mass death+teardown、Boundary burst/retry、wait fanout/cancel、cross-ASC live dirty |
| Profiler | Core/Drain/consumer 分项成本 | capture window 与运行 hash |
| Journaling | structural/query/change 证据 | 有界窗口，不用于常态性能 |
| Deep Trace | 单场 causality/slot/capture | 有界 TopN/窗口 |

性能门槛来自 ScaleProfile，报告必须同时给出 avg/p95/p99、memory/allocator/slab/ring high-water、spill/fault、有界 `TickBatch` 切分、catch-up burst 与业务活动量。空转 tick 或过短样本不能证明可扩展。

## Scene/Headless 同构

允许差异：启动入口、真实资源 bridge、画面/音频消费者和输出目标。

必须相同：Session config、commands、SimulationTick、Core schedule、physics policy、EndFixed、Drain、semantic result、Cue requests/markers、Replay/Diagnostics schema。资源缺失只改变 consumer result，不改变 Core。

## Death、Battle Terminal 与 Session Teardown

- UnitDeath：当 target transaction 立即写死亡权威、取消未 Commit 行动、清理 ActiveEffect/Cue 贡献；尸体 ASC 保留到结果快照和 teardown。
- BattleTerminal：只封闭该 `BattleInstanceId` 的新 gameplay ingress，完成整个当前 tick DAG 和 managed gameplay FinalDrain，然后冻结 BattleOutcome/BattleHash。
- SessionTerminal：仅当 Session 内全部 BattleInstance 终局或显式 stop 才进入；随后 teardown 销毁 ASC、完成 cleanup shell 与 subscription/slab/outbox audit。
- ValidationResult：teardown audit 成功后才返回。`FactPlane=TeardownAudit` 事实可进入审计 hash，不得改写已冻结 BattleOutcome/BattleHash；物理路由仍由正交的 Asc/Battle/Session `ScopeKind` 决定。

完成条件不能只检查 `EntityManager.Exists`，也不能依赖固定 post-victory gameplay ticks。

## 失败门

- 真实 Unity test source/golden 不存在或未运行。
- 旧五组/自定义 ECB/Ability Entity/legacy GE/multiple Boundary consumers 有运行命中。
- public raw Entity、generated lifecycle、Prediction schema 有命中。
- 正常 tick 有跨 stage sync fence、跨 System scratch 或 random cross-owner write。
- 语义 hash 不一致、outbox overflow 静默、stabilization 半状态继续。
- Headless 与 Scene 对相同输入产生不同 gameplay 结果。

## 交付

每个验收运行输出原始 ValidationEvidence、业务 summary、determinism diff、Profiler/Journaling 引用、generated/scale report hash 和明确 pass/fail reason。Unity 未执行时只能写“文档/静态验证”，不得把目标态描述冒充运行完成。
