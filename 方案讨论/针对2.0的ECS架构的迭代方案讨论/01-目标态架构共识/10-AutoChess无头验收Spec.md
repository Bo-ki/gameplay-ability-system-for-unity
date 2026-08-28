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
  -> TickBatch(absolute elapsed) complete parent chain until each BattleInstance terminal condition
  -> consume immutable BoundaryBatch/ReadModel/Diagnostics
  -> complete terminal tick DAG -> per-Battle Gate close -> managed gameplay FinalDrain
  -> CoreOutboxCut + GateRequestCut accepted + SnapshotCut reached -> BattleOutcomeSeal
  -> after all battles terminal or explicit stop: Session Terminalizing
  -> Session Gate Closing -> accepted tail terminal outcomes -> FinalDrain Accepted
  -> cleanup/resource audit -> dispose World/Blob/registry -> DisposedReceipt
  -> ValidationResultSeal -> return immutable ValidationResult
```

Runner 只调用 Session capability，不缓存/更新内部 Group/System，也不直接读取/清理 ECS buffers。独立 World 显式安装标准 Begin/End FixedStep ECB 与唯一 Drain。

一个 World 同时只有一个 active GAS Session，但 Session 可包含多个 `BattleInstanceId`。单个战局终局只封闭该战局的 ingress，不能误停其他 replicated battle groups。

## 时间口径

- 权威口径是 SimulationTick；一个 RenderFrame 可以 0..N fixed ticks。
- fresh Session先显式执行一次RateManager `t=0` maintenance prime；它不增加SimulationTick，业务初始态进入BootstrapSemanticHash。
- `TickBatch`输入是单调absolute elapsed time，并返回PhysicalFixedUpdates、GameplayTicks、MaintenanceUpdates和RemainingDebt；MaximumDeltaTime只限制单批追债量。
- tick rate 来自 Scenario/ScaleProfile，Session 启动后不可变并进入 hash。
- Spawn/Ready、measurement warmup、measured、gameplay FinalDrain 和 teardown 分开统计。Ready 不是 warmup tick 的别名。
- bootstrap、Burst/Editor warmup、Journaling、资源加载不混入 Core tick。
- hard-coded 60Hz、采样 tick 数或毫秒阈值不是框架不变量；具体值由 profile 声明。
- 终局后禁止再运行固定数量的完整 gameplay tick 作为 flush；FinalDrain 以事实 watermark/quiescence 完成，不以 wall clock 或固定 tick 数决定。
- FixedStep执行0次时仍运行outer Drain一次；冻结BatchId的retry不重跑Kernel、不增加SimulationTick。

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
| Death | Health=0 | OwnerWave 已可见的 death 阻止后续 owner Commit；TargetPrepare incoming death 不撤回已 Commit work，经 SessionFaultReduce/TargetPublish 后发布，Death reaction T+1，terminal drain |
| Boundary/Cue | Instant+Duration | Executed 与 OnActive/WhileActive/Removed、Destroy 后不丢 |

完整业务样例见 [10B](10B-AutoChess完整业务案例设计Spec.md)。

## ValidationVectorManifest

本文件唯一拥有版本化 `ValidationVectorManifest`。`10B-08`只定义业务场景与预期，不再拥有另一份数量清单；V0-V7只按稳定VectorId映射实现/验收owner。任一VectorId缺失、重复、无真实test source、未运行、skipped或非green都阻止release。

Tier B 17项业务向量以 `TB-01..TB-17` 稳定编号，对应 `10B-08` 当前17项。第三轮必须额外登记以下向量族：

| 向量族 | 必证内容 | Owner任务 |
|---|---|---|
| `R3-CFG-*` | sidecar/非法9203/missing capture/257 Tag、一次生成收敛、candidate失败不promotion | V1/V6/V7 |
| `R3-CMT-*` | Cost/Cooldown字段矩阵、双Commit竞争、Commit→Cancel、失败零写 | V3 |
| `R3-STB-*` | 双target单/多fault、canonical FaultId、全target shadow discard、N/N+1 credit | V3/V4 |
| `R3-ING-*` | Producer-scoped RequestKey、payload conflict、future hole、Fault/Closing exact membership | V3/V5 |
| `R3-BTL-*` | 同Session A终局竞态close、B继续、双cut managed accepted + SnapshotCut后BattleOutcomeSeal | V3/V5/V6 |
| `R3-TCK-*` | t=0 prime、0/1/N/debt、插入0-tick outer update、同语义输入不同partition | V5/V7 |
| `R3-GRT/LIV/REF-*` | Grant child cleanup、Live frozen projection、EmittedRef retention/ack上界 | V4 |
| `R3-SNP/CUE-*` | overflow reconcile、destroy/late join、异步load乱序与Avatar rebind | V5/V6 |
| `R3-RSL-*` | FinalDrain retry、DisposedReceipt、ValidationResult后零事实 | V5/V6 |
| `R3-HOT-*` | 单target work-unit N/N+1与不同偏斜布局 | V7 |

每个manifest entry至少包含：`VectorId/Version/InputManifestHash/ExpectedSemanticHashSet/TestAssembly/TestSourceHash/TestId/OwnerTask/RequiredPass/LastRunEvidenceId/Status`。

原有业务向量不得缺少：

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

以上十组只是阅读聚类，不是另一份VectorId清单；完整业务成员以`TB-01..TB-17` manifest为准。

## ValidationEvidence

最低字段：

```text
Package: PackageId, SchemaHash, ContentHash, LayoutHash, ArtifactManifestHash,
         GeneratorVersion, SourceInputHash, BuildArtifactHash, PublishTransactionId
Session: Epoch, Unity/Entities/Burst/Jobs, EnvironmentFingerprint, TickRate,
         ScaleProfileHash, DeterminismDomain, NumericEncoderVersion
Scenario: Seed, UnitCount, Start/EndTick, Winner, Commands, Damage, Kills
Semantics: Activations, Commits, Continuations, Applications, Stacks, Periods,
           Captures, TagTransitions, Reactions, Cues, TerminalFacts
Runtime: TargetGroups, Scratch/Slot/Buffer HighWater, Spill/Fault,
         Stabilization Iterations, Stale/Reject Reasons
Boundary: Batches, Facts, DeadShells, RingHighWater, Overflow/Reconcile,
          CoreOutboxCut, GateRequestCut, SnapshotCut, FinalOwnerWatermarks
Determinism: CommandTraceHash, SlotLifecycleHash, FinalStateHash,
             BootstrapSemanticHash, BoundarySemanticContentHash, BattleHash,
             CommittedPrefixHash, FaultSemanticHash, TeardownAuditHash,
             TransportEnvelopeHash
Lifecycle: PendingProducer/Job/ECB/Outbox/Shell counts, DisposedReceipt,
           ValidationResultSeal
Evidence: PairId, ExecutionMode, SessionConfigHash, CommandManifestHash,
          ScheduleContractHash, PhysicsPolicyHash, ProfilerCaptureId,
          JournalingCaptureId, DeepTraceArtifactId, GeneratedReportHash
```

日志和 Markdown summary 只能从该模型派生。

Semantic hash 字段口径：

- 包含：Session/content/schema hash、`BattleInstanceId`、`ScenarioUnitId`、`BattleLocalTick`、Request/Activation/Application/Causality identity、semantic code/reason、canonical ordinal、Attribute/Tag/stack/Cue lifecycle 值、BattleOutcome。
- 排除：raw Entity index/version、进程静态 report key、RenderFrame、wall clock、Job/chunk/batch 切分、Profiler/Journaling 记录数、presentation 采样/截断、teardown-only audit facts。
- `BattleHash` 在 gameplay FinalDrain 后冻结；`TeardownAuditHash` 独立记录 cleanup，不回写 BattleHash。
- `TransportEnvelopeHash`单独记录BatchId/watermark/retry/batch切分；相同冻结语义输入下允许不同，不得进入Gameplay hash。
- `CommittedPrefixHash/FaultSemanticHash`只用于Session-fatal结果；Faulted Session/run不得伪装成正常胜负green。

## 功能、规模与证据 Pass

| pass | 输入与预期 | Hash/机器证据 | 失败条件与采样单位 |
|---|---|---|---|
| Functional | versioned vectors + approved package；精确state/outcome/fact/Cue/ValidationResultSeal | Command/Slot/FinalState/Boundary/Battle/Teardown hash；真实TestId/source/build | missing/unrun/skipped/extra fact/no ValidationResultSeal；每vector/tick/application/fact |
| Determinism | 同一冻结语义输入；扰动worker/chunk/segment/consumer与含0的bounded TickBatch partition | 全semantic hash相同，field-level diff与perturbation manifest | 任一semantic diff或漏跑variant；每完整run/variant |
| Scale | 七类typed WorkloadParameter + Environment/Build/Profile | activity、capacity/high-water、avg/p95/p99、fault/spill及semantic hash | 负载未实际发生、样本不足、silent overflow/半写；Core每tick、Drain每outer、teardown每Session |
| Profiler | 同build/profile，Journaling/DeepTrace关闭并完成warmup | CaptureId/artifact hash、marker schema与semantic hash | disabled/unbound、GC/unexpected sync/平台row阈值失败；marker映射Tick/OuterBatch |
| Journaling | 独立bounded structural场景 | raw artifact + parsed owner/component/origin matrix，semantic hash不变 | 未捕获、非法owner/playback、窗口不完整；每structural record |
| Deep Trace | 单一bounded causal vector | typed causality/activation/application/slot/capture/Cue图及artifact hash | 必需节点被截断或trace改变结果；每causality/application |

性能门槛来自版本化平台ScaleProfile，报告必须包含EnvironmentFingerprint、BuildArtifactHash、BaselineCommit、WorkloadParameterHash、WarmupCondition、MeasurementWindow/Repetitions/SampleUnit/QuantileMethod/min sample、各成本域avg/p95/p99、memory/allocator/slab/ring/shadow high-water、spill/fault、有界`TickBatch`与业务活动量。空转tick、过短样本或无平台毫秒数字不能证明可扩展。

## Scene/Headless 同构

每个对照运行使用同一`PairId`。允许差异只有启动入口、真实资源bridge、画面/音频消费者和输出目标。

必须相同：approved package、build、Session config、command manifest/AssignedAvailableTick、Core schedule contract、physics policy、SimulationTick、EndFixed、Drain、semantic result、Cue requests/markers、Replay/Diagnostics schema。证据必须输出phase invocation、PhysicalFixedUpdates和合法FenceKind计数；资源缺失只改变consumer result，不改变Core。

## Death、Battle Terminal 与 Session Teardown

- UnitDeath：TargetPrepare在shadow中冻结死亡与cleanup delta，SessionFaultReduce成功后由TargetPublish写死亡权威；随后取消未 Commit行动并清理ActiveEffect/Cue贡献。尸体ASC保留到结果快照和teardown。
- BattleTerminal：Core TerminalResolve冻结CoreTerminalToken/CoreOutboxCut；同一SessionIngressGate关闭该Battle并终结cutoff前tail。Core/Gate两cut均被managed staging接管且ReadModel达到同identity SnapshotCut后，才冻结BattleOutcome/BattleHash；其他Battle继续运行。
- SessionTerminal：仅当 Session 内全部 BattleInstance 终局或显式 stop 才进入；随后 teardown 销毁 ASC、完成 cleanup shell 与 subscription/slab/outbox audit。
- ValidationResult：FinalDrain Accepted、cleanup/resource释放、World/Blob/registry dispose和managed DisposedReceipt均完成后才Seal并返回。`FactPlane=TeardownAudit`进入独立审计hash，不得改写BattleHash；返回后零新BoundaryBatch/GameplayFact/TeardownAudit fact。

完成条件不能只检查 `EntityManager.Exists`，也不能依赖固定 post-victory gameplay ticks。

## 失败门

- 真实 Unity test source/golden 不存在或未运行。
- 旧五组/自定义 ECB/Ability Entity/legacy GE/multiple Boundary consumers 有运行命中。
- public raw Entity、generated lifecycle、Prediction schema 有命中。
- 正常 tick 有跨 stage sync fence、跨 System scratch 或 random cross-owner write。
- 语义 hash 不一致、outbox overflow 静默、stabilization 半状态继续。
- Headless 与 Scene 对相同输入产生不同 gameplay 结果。
- package四hash不匹配、candidate失败证据缺失，或Runtime存在alternate/fallback catalog。
- 任一required vector缺失/未运行/skipped，或用count>0、x50隔离组、固定disabled reason冒充证据。

## 交付

每个验收运行输出原始 ValidationEvidence、业务 summary、determinism diff、Profiler/Journaling 引用、generated/scale report hash 和明确 pass/fail reason。Unity 未执行时只能写“文档/静态验证”，不得把目标态描述冒充运行完成。
