# GAS ECS Runtime - Runtime Core Frame Backbone - Structural Playback Gate

## 父节点

[AM2B-FrameBackbone](README.md)

## 任务ID

`T1-RuntimeCore-AM2B-D`

## 状态

已完成（contract-first，Unity验证待补跑）

## 当前问题

1. 当前 Runtime 已有 `RecordRuntimeCoreEcbPlayback`，但结构变化还不是唯一 playback gate。
2. ISSUE-004 说明结构变化边界脆弱，ObjectDisposed / handle invalidation 风险可能回归。
3. AM5 store 后续 cleanup / compact / granted cleanup 都会触发结构变化策略选择。

## 目标 / 目的

1. 定义 `GasStructuralPlaybackSystemGroup` 或等价 structural playback gate。
2. 明确哪些 phase 只能记录 mutation，不能直接结构变化。
3. 明确 ECB command、EntityQuery bulk、ComponentTypeSet、cleanup component 的采用口径。

## 执行范围

1. `Assets/GAS/Runtime/System/SystemGroup`
2. `Assets/GAS/Runtime/System/Effect`
3. `Assets/GAS/Runtime/Debugger`
4. `Assets/_Test/GAS/Runtime/Event/RuntimeStructuralChangePlanTests.cs`

## 验收标准

1. Runtime Core hot path 有唯一 structural playback gate 契约。
2. Debugger 能按 gate 统计 structural playback。
3. AM3 / AM5 后续任务不得绕过该 gate。

## 测试链路

1. `git diff --check`
2. `rg -n "StructuralPlayback|RuntimeStructuralChange|ComponentTypeSet|AtPlayback|Cleanup" Assets/GAS/Runtime Assets/_Test/GAS/Runtime`

## 本轮进展

1. 新增 `GasStructuralPlaybackSystemGroup` 与 `GasEndStructuralEcbSystem` 作为 Runtime Core hot path 的唯一 structural playback gate type anchor。
2. 将 `GASSystemScheduleContract` 的 `StructuralPlayback` phase 指向 `GasStructuralPlaybackSystemGroup`。
3. 新增 `GASRuntimeStructuralPlaybackGateContract`，声明 `ContractOnly`、`EcbCommandBuffer`、`EntityQueryBulkCandidate`、`ComponentTypeSetBulkCandidate`、`CleanupComponentCandidate`、`EnableablePreferred` 等 policy。
4. 明确 observation / managed presentation 不进入 hot path gate。
5. `GasRuntimeDebugger` 新增 `RecordRuntimeCoreStructuralPlaybackGate`，输出 `ecbCommands` 与 `bulkQueries` 字段。
6. 新增 / 扩展 `RuntimeStructuralChangePlanTests` 与 `GasRuntimeDebuggerTests`。
