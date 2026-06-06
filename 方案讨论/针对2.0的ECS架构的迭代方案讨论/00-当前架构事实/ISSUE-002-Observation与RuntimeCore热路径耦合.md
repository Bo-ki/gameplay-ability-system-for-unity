# ISSUE-002 Observation 与 Runtime Core 热路径耦合

> 最近复核：2026-06-06 | 状态：Active | 严重度：P1

## 当前结论

Observation 已经被放到 `GASBoundaryProjectionSystemGroup`，并且 typed fact / replay / presentation 方向成立。当前问题不再是“Observation 完全混在 Runtime Core”，而是 typed fact 与 legacy EventBus 并存，且性能证据容易把 Boundary/Debugger 成本混入 CoreSimulation。

## 已缓解部分

1. `PresentationOutboxProjectionSystem`、`ReplayLogSystem`、`DiagnosticsSnapshotSystem` 位于 BoundaryProjection。
2. `GameplayFactProjectionSystem` 写 typed facts，Boundary systems 可消费。
3. `GasRuntimeOfficialToolDiff` 可基于 `EntitiesJournaling` 导出结构变化证据。
4. AutoChess Presentation 只消费 battle result/log，不回读 ECS state。

## 仍成立风险

1. legacy `GameplayEventBusComponent` 仍是 singleton buffer owner。
2. `GameplayFactProjectionSystem` / demo execution extension 仍可能桥接 legacy event。
3. `GasRuntimeDebugger` 仍有 observation-only query 和 `ToEntityArray`。
4. 如果性能报告不拆 Core / Boundary / Demo，架构判断会失真。

## 代码证据

| 事实 | 文件 |
|---|---|
| BoundaryProjection 注册 | `GASSystemScheduleContract.cs` |
| presentation/replay/debug | `PresentationOutboxProjectionSystem.cs`, `ReplayLogSystem.cs`, `DiagnosticsSnapshotSystem.cs` |
| legacy event bus singleton | `GameplayEventBusComponent.cs`, `GASManager.cs` |
| debugger query | `GasRuntimeDebugger.cs` |
| AutoChess log-only presentation | `AutoChessDemoSceneRunner.cs`, `AutoChessBattleLog.cs` |

## 退出条件

1. CoreSimulation 的性能与 BoundaryProjection/Debugger/Presentation 成本分开报告。
2. gameplay reaction 不再依赖 observation bus 扫描。
3. typed fact、replay、presentation、debugger 的 owner 和 phase 清晰。
