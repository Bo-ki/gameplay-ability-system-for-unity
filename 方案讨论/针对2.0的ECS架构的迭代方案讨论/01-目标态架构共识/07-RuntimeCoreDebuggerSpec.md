# Runtime Core Debugger Spec

## 结论

Debugger 是只读证据系统，不是 gameplay EventBus、System 调度 owner 或日志集合。它只消费 Kernel/Drain 导出的结构化 evidence、immutable BoundaryBatch 和 ReadModelSnapshot；关闭 Debugger 后 simulation 状态、顺序和 hash 必须完全不变。

## 证据分层

| 层 | owner | Debugger 读取内容 |
|---|---|---|
| Core | GasTickKernelSystem | tick/stage/job counters、command/target/slot/buffer/stabilization、hash |
| Structural | 标准 EndFixed ECB + Unity 工具 | playback、structural count、sync/Journaling refs |
| Boundary | GasBoundaryDrainSystem | live/dead-shell drain、batch、ring、overflow/reconcile |
| Consumers | Cue/UI/Replay/Headless | immutable batch consumption timing/result，不反写 Core |
| Definition | Catalog/Generator validation | content/schema/layout hash、projection/program/dependency/scale report |

Profiler、Entities Journaling、Memory Profiler 和 Burst Inspector 是独立官方证据源。Runtime 自报 timing/counter 不能替代它们；Debugger 负责关联 capture id 与运行参数。

## Tick Summary

每个 SimulationTick 至少记录：

```text
Epoch / SimulationTick / ContentHash
sealedCommands / dueWork / stale / rejectReasons
activationStarted / committed / cancelled / ended
continuationsCreated / woken / removed / lateWakeDropped
effectApplications / instant / active / inhibited / removed / stackOverflow
targetGroups / canonicalSortCost / scratchHighWater / spillOrFault
tagTransitions / stabilizationIterations / stabilizationFaults
dirtyAttributes / aggregatorContributions / captureReevaluations
coreReactionCount / nextTickScheduled
boundaryFacts / terminalFacts / deterministicTickHash
```

RenderFrame 汇总可以列出 0..N ticks，但不得把它当权威时序。

## Slot/Handle 诊断

按 ASC/slot pool 导出 capacity、live、tombstone、free、high-water、generation reuse、stale lookup、oldest tombstone 与回收阻塞原因。默认只导出聚合和 TopN；完整逐槽 trace 只在显式 capture window 开启，不能常态化污染 hot path。

## Stabilization 诊断

记录 target、initial dirty set、transition count、max iterations、dependency ids、final state hash 与 fault reason。中间试探 fact/Cue 不进入普通 Boundary；fault capture 可以保存有界诊断 trace，并终止当前 battle/session。

## Reaction 诊断

每条公开 reaction 可按 CausalityId 追踪 `EmitTick -> DeliverTick -> TargetAsc -> Result`。Debugger 必须能证明每条公开边至少增加一 tick，并区分：

- kernel invariant same tick；
- DirectEffectProgram same tick；
- stable-state deferred reaction；
- structural entity next-tick visibility；
- Boundary managed delivery latency。

## Capture/Aggregator 诊断

记录 ProjectionContractId、Source/Target、Snapshot/Live、CapturePhase、Scalar/AggregatorSnapshot、captured revision、last-seen revision、dependent effect、late inputs 和 bake/runtime rejection。Aggregator trace 使用 ContributorId、Channel、Op、OrderKey 与 qualifier result，不能仅显示最终 float。

## Boundary 诊断

Drain 记录：

- live ASC/outbox 数与 dead cleanup shell 数；
- copied/cleared/removed-shell facts；
- first/last EventId、tick/sequence range；
- ring bytes/batches/high-water；
- overflow range、policy、snapshot reconcile；
- Cue lifecycle unmatched key、consumer lag（只影响观察层）。

Core 不为每个 consumer 保存 cursor；consumer 状态属于 managed Boundary ring。

## API Health

机器可读检查至少覆盖：

- target single writer 与跨 ASC random writes；
- WorldUpdateAllocator 和跨 System/tick temp escape；
- phase `Complete()`/正常 `CompleteAllTrackedJobs()`；
- standard EndFixed structural ownership；
- public raw Entity/writable buffer；
- generated lifecycle/query/ECB/container owner；
- Ability/ActiveEffect Entity 或旧 runtime authority 残留；
- Prediction schema/API 残留。

## Pass 模式

| 模式 | 目的 | 限制 |
|---|---|---|
| Functional | 语义与 deterministic hash | 轻量 counters，不能据此给性能结论 |
| Scale | replicated/hot-target/period/death/Boundary/wait/live capacity pressure | 固定 ScaleProfile、关闭非必要详细 trace；x50 等只是参数 |
| Profiler | Core/Drain/consumer 分离成本 | 关联 Profiler capture id |
| Journaling | structural/query 证据 | 只在 capture window 开启 |
| Deep Trace | 单场因果/slot/capture 追踪 | 有界窗口，不作为常态性能 |

## 禁止方向

- Debugger 自己 query/write gameplay components。
- 用 managed string log 作为 hot-path 唯一证据。
- 把 consumer 是否处理/ack 反馈给 Core。
- 为图表重新增加多个物理 phase/SystemGroup。
- 以“生成报告存在”代替 generated artifact 静态审查。
- 以平均耗时掩盖 p95/p99、memory high-water、spill、fault 或 catch-up burst。

## 验收

- Debugger 开/关、不同消费者组合下 deterministic result 相同。
- Tick、Slot、Capture、Stabilization、Boundary 与 API health evidence 可机器读取。
- Profiler/Journaling/counter 使用同一 Epoch/SimulationTick/capture id 对账。
- 正常运行零 managed hot-path allocation/string formatting。
