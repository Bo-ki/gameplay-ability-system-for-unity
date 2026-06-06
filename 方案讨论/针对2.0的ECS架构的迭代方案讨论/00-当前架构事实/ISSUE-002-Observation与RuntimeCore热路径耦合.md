# ISSUE-002 Observation 与 Runtime Core 热路径耦合

> 最近复核：2026-06-06 | 状态：Mitigated | 严重度：P1

## 当前结论

Observation 已经被放到 `GASBoundaryProjectionSystemGroup`，并且 typed fact / replay / presentation 方向成立。当前问题不再是“Observation 完全混在 Runtime Core”，也不再是 legacy gameplay EventBus 并存；Attribute/Cue/Tag 派生已从 CoreSimulation/CommandResolve 移到 BoundaryProjection，Damage 也已进入 `GameplayEventBuffer` typed fact。`PresentationOutboxProjectionSystem` 与 `ReplayLogSystem` 现在只读 typed fact，不再双读 Attribute/Cue/Tag/Damage 边界缓冲。

## 已缓解部分

1. `PresentationOutboxProjectionSystem`、`ReplayLogSystem`、`DiagnosticsSnapshotSystem` 位于 BoundaryProjection。
2. `GameplayFactProjectionSystem` 只写 typed facts，`GameplayFactBoundaryProjectionSystem` 在 BoundaryProjection 内派生 Attribute/Cue/Tag 边界缓冲。
3. `GasRuntimeOfficialToolDiff` 可基于 `EntitiesJournaling` 导出结构变化证据。
4. AutoChess Presentation 只消费 battle result/log，不回读 ECS state。
5. `DamageEventBuffer` 与 `EventBusHelper.EnqueueDamageEvent` 已退场；Damage 由 typed fact 的 `Domain=Damage` 表达。
6. `EventBusHelper` 不再公开 Attribute/Cue/Tag/Damage gameplay fact enqueue API。
7. `GameplayEventLogSinkComponent` 与 `PresentationOutboxProjectionStateComponent` 已删除 legacy processed cursor，只保留 typed fact cursor。

## 仍成立风险

1. `GameplayEventBusComponent` 仍是 Attribute/Cue/Tag 边界缓冲和 presentation outbox owner 的 singleton owner；这些缓冲只允许作为 BoundaryProjection 派生产物和 managed Cue bridge，不再是 gameplay truth。
2. `GasRuntimeDebugger` 仍有 observation-only query 和 `ToEntityArray`。
3. 如果性能报告不拆 Core / Boundary / Demo，架构判断会失真。
4. 业务 reaction 若重新扫描边界缓冲，会把 Observation 成本和事实源重新拉回 CoreSimulation。

## 代码证据

| 事实 | 文件 |
|---|---|
| BoundaryProjection 注册 | `GASSystemScheduleContract.cs` |
| fact boundary projection | `GameplayFactBoundaryProjectionSystem` |
| presentation/replay/debug | `PresentationOutboxProjectionSystem.cs`, `ReplayLogSystem.cs`, `DiagnosticsSnapshotSystem.cs` |
| boundary buffer singleton | `GameplayEventBusComponent.cs`, `GASManager.cs` |
| boundary helper | `EventBusHelper.cs` |
| debugger query | `GasRuntimeDebugger.cs` |
| AutoChess log-only presentation | `AutoChessDemoSceneRunner.cs`, `AutoChessBattleLog.cs` |

## 退出条件

1. CoreSimulation 的性能与 BoundaryProjection/Debugger/Presentation 成本分开报告。
2. gameplay reaction 不再依赖 observation bus 扫描。
3. Presentation/Replay/Debugger 的计数与 lag 以 typed fact 为主，边界缓冲只按 Cue bridge / observation pressure 报告。
4. typed fact、replay、presentation、debugger 的 owner 和 phase 清晰。
