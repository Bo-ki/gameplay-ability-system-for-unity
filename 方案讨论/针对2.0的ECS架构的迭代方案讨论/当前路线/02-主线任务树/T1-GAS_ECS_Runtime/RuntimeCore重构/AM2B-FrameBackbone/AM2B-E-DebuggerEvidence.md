# GAS ECS Runtime - Runtime Core Frame Backbone - Debugger Evidence Gate

## 父节点

[AM2B-FrameBackbone](README.md)

## 任务ID

`T1-RuntimeCore-AM2B-E`

## 状态

已完成（contract-first / debugger evidence gate，Unity验证待补跑）

## 当前问题

1. Debugger 已有业务 counters、ECB playback、slot pressure。
2. 仍缺 query count、lookup update、allocator owner、dependency wait、stream segment、merge cost、Burst / safety 口径。
3. 缺少这些 counters 时，AutoChess 性能优秀线无法作为自动停止 Goal 的证据。

## 目标 / 目的

1. 将 AM2B-A 到 AM2B-D 的 frame backbone 证据接入 RuntimeCoreDebugger。
2. 建立 frame backbone counters export。
3. 明确 Debugger 自身 overhead、采样策略和 disable policy。

## 执行范围

1. `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs`
2. `Assets/_Test/GAS/Runtime/Debugger/GasRuntimeDebuggerTests.cs`

## 验收标准

1. Debugger 输出 frame backbone counters。
2. counters 能区分 core / physics / render / runner。
3. 性能报告不再只依赖 `avgTickMs`。

## 测试链路

1. `git diff --check`
2. `rg -n "FrameBackbone|QueryCount|LookupUpdate|AllocatorOwner|DependencyWait|MergeCost|StructuralPlayback" Assets/GAS/Runtime Assets/_Test/GAS/Runtime`

## 本轮进展

1. 新增 `GASRuntimeDebuggerEvidenceGateContract`，聚合 AM2B-A 到 AM2B-D 的 phase、frame budget、stream owner、structural playback、Profiler / Journaling 对照、cost split、overhead budget、disable policy 和 Burst warmup policy。
2. `GasRuntimeDebugger` 新增 `RuntimeCoreFrameBackbone` diagnostic kind、`GasRuntimeFrameBackboneDiagnosticCounters`、snapshot counters 和文本导出。
3. `RecordRuntimeCoreStructuralPlaybackGate` 现在同时累计 frame backbone 的 recorded structural playback、ECB command 和 bulk query counters。
4. 新增 `RecordCurrentRuntimeCoreFrameBackboneEvidence`，输出 `runtimeCoreFrameBackbone|...` 等结构化日志。
5. 扩展 `GasRuntimeDebuggerTests`。
