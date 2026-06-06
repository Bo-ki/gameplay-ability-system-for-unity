# ISSUE-009 Runtime Core Frame Backbone 仍未目标态化

> 最近复核：2026-06-06 | 状态：Active | 严重度：P1

## 当前结论

“Frame Backbone 完全缺失”已经不准确。当前已有 5 段 physical backbone，并且 `GASGlobalTimerSystem`、stream frame prepare、command resolve、core simulation、structural commit、boundary projection 都在主链中。

剩余问题是 frame owner / query owner / stream owner 仍没有完全目标态化。

## 已缓解部分

1. `GASManager.Initialize()` 创建 `EX_GAS_World` 和 FixedStep 主链。
2. `GASFramePrepareSystemGroup` 负责 event bus clear、global timer、stream frame prepare。
3. `GASStructuralCommitSystemGroup` 位于 CoreSimulation 与 BoundaryProjection 之间。
4. AutoChess tick 按 5 段 group 手动推进并记录 timing。

## 仍成立风险

1. `GASManager.EntityGlobalTimer` 是 static known owner。
2. `GEEffectCommandStreamComponent` singleton owner 承载过多数据。
3. 部分未注册系统和 contract-only phase 容易混淆 frame backbone。
4. Query layout / capacity / buffer pressure 还没有形成统一 frame budget evidence。

## 代码证据

| 事实 | 文件 |
|---|---|
| world/frame setup | `GASManager.cs` |
| groups | `GASGroups.cs` |
| schedule contract | `GASSystemScheduleContract.cs` |
| stream frame prepare | `GEEffectCommandSpecStreamPhases.cs` |
| AutoChess timing | `AutoChessGasCoreBridge.cs` |

## 退出条件

1. frame owner、query owner、stream owner 的生命周期明确。
2. 每个 phase 的 reads/writes 与 physical group 对齐。
3. frame budget 证据覆盖 query count、buffer pressure、dependency complete、structural changes。
