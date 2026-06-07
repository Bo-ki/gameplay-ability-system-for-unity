# Owner-local Gameplay Fact Lane 切片归档

> 日期：2026-06-08
> 关联问题：P0-D singleton stream owner
> 状态：阶段性收口，未退出 P0-D

## 变更摘要

本切片把 pending AttributeDelta -> Attribute fact 的一条手写 Runtime Core 链路从直接写 singleton `GameplayEventBuffer` 调整为 ASC owner-local fact lane：

1. `OwnerLocalGameplayFactBuffer` 新增为 ASC 本地 fact carrier，`GASRuntimeEntityArchetypes.ASC()` 和 `ASCEntityFactory` 已把它纳入 ASC archetype、初始化容量和完整性检查。
2. `GASAttributeModifierDeltaApplySystem` 在 chunk-local `ApplyOwnerLocalPendingAttributeModifierDeltaChunkJob` 中继续应用 owner-local `AttributeModifierBuffer`，但新增 Attribute fact 先写入 `OwnerLocalGameplayFactBuffer`。
3. `GameplayOwnerLocalFactFramePrepareSystem` 在 FramePrepare 清理上一帧 ASC 本地 fact。
4. `GameplayOwnerLocalFactFlushSystem` 在 CoreSimulation 中收集 ASC 本地 fact，按 owner `Entity` + fact sequence + local index 排序后 flush 到现有 stream export；该系统显式 `UpdateBefore(GameplayFactProjectionSystem)`，避免 owner-local fact 已分配较小 `NextFactSequence` 却被物理追加到 projection fact 之后。
5. `GEEffectCommandStreamComponent` 新增 owner-local fact counter：`OwnerLocalFactCount`、`OwnerLocalFactOwnerGroupCount`、`OwnerLocalFactMaxOwnerRange`、`OwnerLocalFactFlushCount`。
6. `GasRuntimeDebugger` 采集这组 counter，并写入 retained runtime core state、diagnostic event、snapshot 文本和 `GasRuntimeCoreDiagnosticCounters`。
7. AutoChess validation evidence / summary 输出 `ownerLocalFacts`、`ownerLocalFactOwnerGroups`、`ownerLocalFactMaxOwnerRange`、`ownerLocalFactFlushes`；runtime chain gate 要求 `OwnerLocalFactFlushCount > 0`。
8. `Verify-GAS-RuntimeCoreBoundary.ps1` 新增防回流门，要求 owner-local fact buffer、frame prepare、flush system、deterministic comparer、flush-before-projection 调度、debugger counter export 和 AutoChess gate 同时存在。

## 代码证据

| 事实 | 代码路径 |
|---|---|
| owner-local fact buffer 定义和计数器 | `Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs` |
| ASC archetype / factory 拥有本地 fact buffer | `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeEntityArchetypes.cs`、`Assets/GAS/Runtime/AbilitySystem/ASCEntityFactory.cs` |
| pending AttributeDelta apply 写 ASC 本地 fact | `Assets/GAS/Runtime/System/Attribute/GASAttributeModifierDeltaApplySystem.cs` |
| 本地 fact frame clear / deterministic flush-before-projection | `Assets/GAS/Runtime/System/Effect/GEEffectCommandSpecStreamPhases.cs` |
| schedule 注册 | `Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs` |
| debugger-visible counters | `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs` |
| AutoChess validation evidence / chain gate | `Assets/AutoChessDemo/Battle/AutoChessBattleContracts.cs`、`Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationReport.cs`、`Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationRun.cs` |
| 防回流验证 | `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` |

## 验证

已运行：

1. `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过。
2. `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；保留既有 `MSB3277` warning。
3. `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；保留既有 `MSB3277` warning。

未运行：

1. Unity headless AutoChess。
2. Unity Test Runner。
3. x100 / x1000 scale profile。
4. Profiler / Journaling enabled pass。
5. Luban / SourceGenerator 重跑。

## 不能推出的结论

1. 不能写成 P0-D 已退出；command、set-by-caller、instant spec、generated reduce、active mutation 和 Boundary fact export 仍依赖 singleton stream owner。
2. 不能写成 owner-local command/spec/delta/fact 全链路完成；本次只覆盖 pending AttributeDelta 产生的 Attribute fact。
3. 不能写成性能优秀；当前只有静态门和 targeted dotnet build，没有 Unity headless、Profiler 或 x100/x1000 证据。

## 复发入口

如果 `GASAttributeModifierDeltaApplySystem` 重新直接把 Attribute fact 写入 singleton `GameplayEventBuffer`，或 `OwnerLocalGameplayFactBuffer` / `GameplayOwnerLocalFactFlushSystem` 从 schedule 中消失，或 `GameplayOwnerLocalFactFlushSystem` 又排到 `GameplayFactProjectionSystem` 之后，或 Runtime Debugger / AutoChess validation 不再输出 `ownerLocalFactFlushes`，应重新打开 P0-D。
