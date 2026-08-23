# V7 确定性、规模与 Profiler 门

> 状态：V6 后领取 | 前置：V0-V6

## Owner 输入

- [当前迁移基线](../../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- [目标验收门](../../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)
- [无头验收/Hash Owner](../../01-目标态架构共识/10-AutoChess无头验收Spec.md)
- [ScaleProfile Owner](../../01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md)
- [配置语义与 CapacityProof](../../01-目标态架构共识/25-配置语义编译契约与CapacityProof统一裁决Spec.md)
- [DOTS 性能红线与数值确定性 ADR 门](../../01-目标态架构共识/18-DOTS官方规范复核与性能红线Spec.md)

## 目标

以真实 tests、semantic hash、scale workload、Profiler 和 Journaling证明 v1 可发布，而不是只证明功能能跑。

## 执行范围

1. 全量 EditMode/PlayMode/Headless semantic suite。
2. 重复运行只扰动physical enumeration：worker/chunk/segment、target bucket physical layout、consumer组合与bounded TickBatch partition。`RequestSequence/AssignedAvailableTick/ProducerSourceSequence`属于冻结语义输入；改变它们不能伪装成调度扰动。
3. 按 workload 类型分别执行：ReplicatedGroups、HotTarget、PeriodBurst、MassDeathTeardown、BoundaryBurstRetry、WaitFanoutCancel、CrossAscLiveDirty；legacy x50 只作为 50 个隔离 4 单位战局 alias。
4. Kernel/EndFixed/Drain/managed consumer分域 timing和memory evidence。
5. Generator/CI证据覆盖单次generation收敛、第二次零diff、跨culture/path canonical hash、artifact bytes、candidate failure与atomic promotion；Headless只证明exact installed package。
6. 每个profile输出EnvironmentFingerprint、BuildArtifactHash、BaselineCommit、WorkloadParameterHash、warmup/measurement/repetition/sample/quantile、bounded TickBatch、CapacityProof requested/granted/consumed、shadow/publish/scratch/allocator high-water及typed overflow/fault。
7. semantic hash include：stable identity、Definition/Spec/slot、SimulationTick、canonical sequence、typed outcome、Attribute/Tag/state/fact、BattleOutcome、`DeterminismDomain/NumericEncoderVersion`；exclude：batch 切分、raw Entity、chunk/job 顺序、wall-clock、Profiler、allocator address、teardown facts。

## 验收

- 相同seed、package、AssignedAvailableTick、producer order和payload下，不同physical enumeration得到批准的稳定hash。
- 固定partition集包含逐tick、bounded N chunks与插入0-tick outer update；semantic hash一致，TransportEnvelopeHash可不同，teardown audit不改BattleHash。
- 所有 semantic、stale handle、capture、inhibition、Cue、destroy测试通过。
- scale budget达到任务领取时冻结的阈值；未达标不得以平均值或关闭 Profiler降级通过。
- Journaling证明结构变化只来自批准 owner。
- `EndFixedPlayback/OuterBatchToDrain/TerminalFinalDrain/DiagnosticsCaptureOnly`四类合法fence逐类有callsite与次数证据，`UnexpectedSyncPointCount=0`；无Kernel/Ingress/逐source/隐式EntityManager fence、跨System scratch或多Boundary consumer。
- 七类 workload 均有独立结果和 high-water；任一 capacity/allocator overflow 均显式失败且无部分 mutation。
- Functional、Determinism、Scale、Profiler、Journaling、DeepTrace六个pass均有独立EvidenceId；missing/unrun/skipped/disabled/count-only不得替代。
- `R3-STB/HOT`验证TargetPrepare/SessionFaultReduce/TargetPublish与MaxTargetWorkUnitsPerTick；`R3-CFG`验证candidate/promotion，`R3-TCK/RSL`验证0FixedStep Drain和ValidationResultSeal。
- 数值确定性 ADR 必须已有显式决策，evidence 的 Build/Platform/encoder 域与该决策一致；ADR 仍为 Pending 时不得 release，本任务不得替用户默认选择 A/B。
- `00` 更新为实现后事实，`04` 写最终验证，`02` 标记完成；`01/17` 仅在目标规范确实变化时更新。

## 交还

Release evidence bundle、失败/豁免为零声明、最终 deletion/static scan、Profiler/Journaling 摘要与可复现命令。
