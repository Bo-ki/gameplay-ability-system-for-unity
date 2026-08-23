# V7 确定性、规模与 Profiler 门

> 状态：V6 后领取 | 前置：V0-V6

## Owner 输入

- [当前迁移基线](../../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- [目标验收门](../../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)
- [无头验收/Hash Owner](../../01-目标态架构共识/10-AutoChess无头验收Spec.md)
- [ScaleProfile Owner](../../01-目标态架构共识/11-AutoChessDemo-Luban配置方案Spec.md)

## 目标

以真实 tests、semantic hash、scale workload、Profiler 和 Journaling证明 v1 可发布，而不是只证明功能能跑。

## 执行范围

1. 全量 EditMode/PlayMode/Headless semantic suite。
2. 重复运行、输入顺序扰动、slot churn、ring overflow与destroy handoff。
3. 按 workload 类型分别执行：ReplicatedGroups、HotTarget、PeriodBurst、MassDeathTeardown、BoundaryBurstRetry、WaitFanoutCancel、CrossAscLiveDirty；legacy x50 只作为 50 个隔离 4 单位战局 alias。
4. Kernel/EndFixed/Drain/managed consumer分域 timing和memory evidence。
5. 最终 generated artifact、文档、删除门和工作树对账。
6. 每个 profile 输出 bounded TickBatch、command/slot/wait/boundary capacity、scratch/allocator high-water、overflow/retry typed outcome。
7. semantic hash include：stable identity、Definition/Spec/slot、SimulationTick、canonical sequence、typed outcome、Attribute/Tag/state/fact、BattleOutcome；exclude：batch 切分、raw Entity、chunk/job 顺序、wall-clock、Profiler、allocator address、teardown facts。

## 验收

- 相同 seed/input与物理输入排列得到批准的稳定 hash。
- `TickBatch(1/N)`、不同 chunk/batch/input 切分 semantic hash 一致；teardown audit 不改已冻结 battle hash。
- 所有 semantic、stale handle、capture、inhibition、Cue、destroy测试通过。
- scale budget达到任务领取时冻结的阈值；未达标不得以平均值或关闭 Profiler降级通过。
- Journaling证明结构变化只来自批准 owner。
- 无正常 tick `CompleteAllTrackedJobs()` fence、跨 System scratch或多 Boundary consumer。
- 七类 workload 均有独立结果和 high-water；任一 capacity/allocator overflow 均显式失败且无部分 mutation。
- `00` 更新为实现后事实，`04` 写最终验证，`02` 标记完成；`01/17` 仅在目标规范确实变化时更新。

## 交还

Release evidence bundle、失败/豁免为零声明、最终 deletion/static scan、Profiler/Journaling 摘要与可复现命令。
