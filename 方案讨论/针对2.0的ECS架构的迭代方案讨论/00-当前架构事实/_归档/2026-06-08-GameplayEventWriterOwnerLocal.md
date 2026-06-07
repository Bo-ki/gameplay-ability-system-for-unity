# GameplayEventWriter owner-local 事实归档

> 日期：2026-06-08
> 范围：P0-D singleton typed fact helper / EntityManager runtime helper fact writer
> 状态：阶段性收口，未退出 P0-D

## 变更事实

本切片把 `EffectCommandSpecStream.GameplayEventWriter` 的写目标从 singleton `GameplayEventBuffer` stream 改为 ASC owner-local fact lane：

1. `GameplayEventWriter` 不再持有 `DynamicBuffer<GameplayEventBuffer> _facts`，`BeginGameplayEventWriter(...)` 也不再获取 stream 上的 fact buffer 作为写目标。
2. `AppendGameplayEvent(...)` 仍接收 `GameplayEventBuffer` 作为 fact 数据结构，但先按 `TargetAsc` 优先、`SourceAsc` 兜底解析 owner，只有 owner 存在且带 `OwnerLocalGameplayFactBuffer` 时才分配 `NextFactSequence` 并写入 owner-local fact buffer。
3. `Flush()` 保留对 `GEEffectCommandStreamComponent` 的回写，用于保存 `NextFactSequence` 等 stream-level sequence / counter 状态。
4. `AbilityRuntimeActions`、`ExecutionCalculationRuntimeActions`、`EffectRuntimeUtility`、`EffectMagnitudeResolver` 的调用端 API 形状暂不打散；它们仍构造 `GameplayEventBuffer`，但实际 append 已由 writer 内部路由到 owner-local carrier。
5. `Verify-GAS-RuntimeCoreBoundary.ps1` 新增防回流规则，要求 writer 内部通过 `OwnerLocalGameplayFactBuffer` append，并禁止 `_facts` / singleton `GameplayEventBuffer` 写目标回生。

## 代码证据

| 事实 | 代码路径 |
|---|---|
| GameplayEventWriter 写 owner-local fact buffer | `Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs` |
| runtime helper 防回流验证 | `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` |
| helper 调用端仍复用 GameplayEventWriter API | `Assets/GAS/Runtime/Ability/AbilityRuntimeActions.cs`、`Assets/GAS/Runtime/Effect/ExecutionCalculationRuntimeActions.cs`、`Assets/GAS/Runtime/System/Effect/EffectRuntimeUtility.cs`、`Assets/GAS/Runtime/System/Effect/EffectMagnitudeResolver.cs` |

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

## 不能推出的结论

1. 不能写成 singleton `GameplayEventBuffer` carrier 已删除；FramePrepare clear、legacy migration input 与 Boundary export 兼容读面仍存在。
2. 不能写成 runtime helper API 已完成终局重命名；本切片刻意保留 `GameplayEventWriter` 名称与 `GameplayEventBuffer` fact payload，降低同轮改动面。
3. 不能写成 AutoChess headless 全链路已通过；本切片没有运行 Unity headless 验收。

## 复发入口

如果 `EffectCommandSpecStream.GameplayEventWriter` 重新持有 `DynamicBuffer<GameplayEventBuffer>`，或 `BeginGameplayEventWriter(...)` 重新把 stream `GameplayEventBuffer` 作为 append 目标，应重新打开 P0-D 的 runtime helper typed fact writer 子项。
