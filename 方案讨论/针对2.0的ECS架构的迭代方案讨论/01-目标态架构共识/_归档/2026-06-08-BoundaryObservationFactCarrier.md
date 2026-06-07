# Boundary Observation Fact Carrier 目标态兑现记录

> 日期：2026-06-08

本记录对应 `03E-04-GameplayFactSpec` 中 “CoreReactionFact 与 BoundaryObservationFact 分离” 的落地切片。

## 兑现点

- Core reaction carrier 保持为 ASC-local `OwnerLocalGameplayFactBuffer`。
- Boundary observation carrier 新增为 event bus 上的 `BoundaryObservationFactBuffer`。
- `GameplayBoundaryFactExportSystem` 在 BoundaryProjection 阶段单向导出 owner-local fact，不让 Presentation / Replay / Debugger 直接读取 Core carrier。
- `GameplayFactBoundaryProjectionSystem`、`PresentationOutboxProjectionSystem`、`ReplayLogSystem` 只消费 Boundary observation。

## 未完成点

- 部分 legacy fact producer 仍直写 stream `GameplayEventBuffer`，本切片仅把它们作为 migration input 纳入 Boundary export。
- Fact sequence owner 仍是 `GEEffectCommandStreamComponent.NextFactSequence`。
- x50/x1000 AutoChess 规模验收仍需在下一轮补齐。
