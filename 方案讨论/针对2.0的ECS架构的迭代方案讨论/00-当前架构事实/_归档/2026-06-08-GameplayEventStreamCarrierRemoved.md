# GameplayEvent stream carrier 移除归档

> 日期：2026-06-08
> 范围：P0-D singleton typed fact carrier / EffectCommandStream frame-local fact buffer / AutoChess execution command consumer
> 状态：阶段性收口，未退出 P0-D

## 变更事实

本切片把 `GameplayEventBuffer` 从 singleton `EffectCommandStream` carrier 上移除，并把剩余读面改成 owner-local fact / owner-local command 语义：

1. `GASRuntimeEntityArchetypes.EffectCommandStream(...)` 不再给 stream entity 挂载 `GameplayEventBuffer`。
2. `EffectCommandSpecStream.HasRequiredBuffers(...)`、`ClearFrameLocalData(...)`、`PrepareFrameLocalData(...)` 不再要求、清理或接收 stream `GameplayEventBuffer`。
3. `GEEffectCommandStreamComponent` 删除 `FactProjectionDeltaCursor` 和 `EventBridgeFactCursor`，stream 不再维护 legacy fact carrier cursor。
4. `GameplayBoundaryFactExportSystem` 删除 `LegacyFactLookup` migration loop，不再从 singleton stream `GameplayEventBuffer` 导出 boundary observation fact。
5. `EBoundaryObservationFactSource` 删除 `LegacyStream`，当前只保留 `OwnerLocalCore`，且 enum 值归零。
6. `AutoChessExecuteDamageCalculationSystem` 改成按 ASC owner chunk 消费 owner-local `GEEffectCommandBuffer`，在目标 ASC 本地写 pending attribute delta 和 owner-local execution fact，不再从 singleton stream command buffer 读取 execution command。
7. `Verify-GAS-RuntimeCoreBoundary.ps1` 把 legacy stream fact input 从 required 改成 forbidden，并新增 stream archetype / required buffer / AutoChess execution consumer 防回流规则。

## 代码证据

| 事实 | 代码路径 |
|---|---|
| EffectCommandStream archetype 删除 GameplayEventBuffer | `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeEntityArchetypes.cs` |
| stream required buffer / frame prepare 删除 fact carrier | `Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs`、`Assets/GAS/Runtime/System/Effect/GEEffectCommandSpecStreamPhases.cs` |
| Boundary export 删除 legacy stream fact migration | `Assets/GAS/Runtime/System/Effect/GEEffectCommandSpecStreamPhases.cs` |
| Boundary observation fact source 删除 LegacyStream | `Assets/GAS/Runtime/Event/GameplayEventBusComponent.cs` |
| AutoChess execution command 走 ASC owner-local chunk | `Assets/AutoChessDemo/Battle/Ecs/AutoChessExecuteDamageCalculationSystem.cs` |
| 防回流验证 | `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` |

## 验证

已运行：

1. `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过。
2. `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；4 个既有 `MSB3277` warning，0 error。
3. `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；8 个既有 `MSB3277` warning，0 error。

未运行：

1. Unity headless AutoChess。
2. Unity Test Runner。
3. x100 / x1000 scale profile。
4. Profiler / Journaling enabled pass。

## 不能推出的结论

1. 不能写成 `GameplayEventBuffer` 类型已经终局删除；它仍是 owner-local fact payload 与 boundary observation payload 的统一数据结构。
2. 不能写成所有 command / payload carrier 都已 owner-local 完成；本切片只清掉 stream fact carrier，并把 AutoChess damage execution command consumer 改为 owner-local。
3. 不能写成 AutoChess headless 全链路或性能目标已通过；本切片只有静态边界和 targeted dotnet build 证据。

## 复发入口

如果 `EffectCommandStream` archetype、`HasRequiredBuffers(...)`、FramePrepare、Boundary export 或 AutoChess execution damage consumer 重新依赖 singleton stream `GameplayEventBuffer` / `GEEffectCommandBuffer`，应重新打开 P0-D 的 stream owner-local carrier 子项。
