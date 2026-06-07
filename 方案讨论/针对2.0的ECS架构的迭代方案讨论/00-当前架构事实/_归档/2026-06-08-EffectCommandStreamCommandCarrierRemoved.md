# EffectCommandStream command carrier 移除归档

> 日期：2026-06-08
> 范围：P0-D singleton command / set-by-caller carrier
> 状态：阶段性收口，未退出 P0-D

## 变更事实

本切片把 `GEEffectCommandBuffer` / `GESetByCallerValueBuffer` 从 singleton `EffectCommandStream` carrier 上移除，保留 ASC owner-local command lane：

1. `GASRuntimeEntityArchetypes.EffectCommandStream(...)` 现在只创建 `GEEffectCommandStreamComponent`。
2. `EffectCommandSpecStream.HasRequiredBuffers(...)` 不再要求 stream entity 带 `GEEffectCommandBuffer` / `GESetByCallerValueBuffer`。
3. `GEEffectCommandSpecStreamFramePrepareSystem` 不再获取或清理 stream command / set-by-caller buffer，只重置 `GEEffectCommandStreamComponent` 的 frame-local counters。
4. `EffectCommandSpecStream.CommandWriter` 删除旧 `AppendCommand(...)`、`MergeParallelCommandFanIn(...)` 和 `ParallelCommandFanInRecord`，不再持有 stream command buffer / stream set-by-caller buffer。
5. `GameplayEffectRequestWriter` 继续通过 `CommandWriter.AppendOwnerLocalInstantCommand(...)` / `AppendOwnerLocalActiveMutationCommand(...)` 写目标 ASC owner-local buffer，stream component 只负责 sequence / context / telemetry。
6. `AutoChessExecuteDamageCalculationSystem` 从 execution extension slot 移到 `GASCoreSimulationSystemGroup`，排在 `GEEffectSpecBuildSystem` 之后、`GASAttributeModifierDeltaApplySystem` 之前，消费 ASC owner-local `GEEffectSpecBuffer` 产生 pending delta / execution fact。
7. `Verify-GAS-RuntimeCoreBoundary.ps1` 新增防回流规则，禁止 `EffectCommandStream` archetype、`HasRequiredBuffers(...)`、FramePrepare 或 helper writer 重新访问 singleton command / set-by-caller carrier，并要求 AutoChess damage execution 走 owner-local spec。

## 代码证据

| 事实 | 代码路径 |
|---|---|
| stream archetype 只保留 component | `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeEntityArchetypes.cs` |
| CommandWriter 不再持有 stream command / payload buffer | `Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs` |
| FramePrepare 不再清 stream command / payload buffer | `Assets/GAS/Runtime/System/Effect/GEEffectCommandSpecStreamPhases.cs` |
| Runtime request writer 仍写 owner-local ASC command lane | `Assets/GAS/Runtime/System/Effect/GameplayEffectRequestWriter.cs` |
| AutoChess damage execution 消费 owner-local spec | `Assets/AutoChessDemo/Battle/Ecs/AutoChessExecuteDamageCalculationSystem.cs`、`Assets/AutoChessDemo/AutoRunner/AutoChessRuntimeSystemBootstrap.cs` |
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

1. 不能写成所有 `GEEffectCommandBuffer` 使用都已终局 owner-local；生成 AbilityActivation / ActiveEffect lifecycle 仍存在跨 owner `BufferLookup` 随机访问面。
2. 不能写成 `GEEffectCommandStreamComponent` 已可删除；sequence、context、delta/fact/spec/debugger counter 仍由该 component 承担。
3. 不能写成 AutoChess headless 全链路或性能目标已通过；本切片只有静态边界和 targeted dotnet build 证据。

## 复发入口

如果 `EffectCommandStream` archetype、`HasRequiredBuffers(...)`、FramePrepare、`CommandWriter` 或 `MergeParallelCommandFanIn(...)` 重新引入 singleton `GEEffectCommandBuffer` / `GESetByCallerValueBuffer` carrier，应重新打开 P0-D 的 command carrier 子项。
