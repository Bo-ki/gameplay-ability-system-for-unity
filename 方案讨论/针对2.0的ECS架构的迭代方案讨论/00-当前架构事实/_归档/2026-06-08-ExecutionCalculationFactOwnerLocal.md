# ExecutionCalculation fact owner-local 事实归档

> 日期：2026-06-08
> 范围：P0-D singleton typed fact writer / execution output fact / AutoChess damage execution adapter
> 状态：阶段性收口，未退出 P0-D

## 变更事实

本切片把 execution output typed fact 的剩余核心写入面从 singleton `GameplayEventBuffer` stream 迁到 ASC owner-local fact lane：

1. `GEExecutionCalculationSystem` 的并行计算 job 继续通过 `NativeStream` 收集 `ExecutionCalculationOutputUpdated` record，merge job 继续稳定排序并分配 `GEEffectCommandStreamComponent.NextFactSequence`。
2. merge job 不再获取或写入 singleton `GameplayEventBuffer`，而是按 `TargetAsc` 优先、`SourceAsc` 兜底解析 fact owner，并写入对应 ASC 的 `OwnerLocalGameplayFactBuffer`。
3. `GASAttributeModifierDeltaApplySystem` 不再读取 singleton fact stream 回填 linked execution fact，改为在当前 ASC 的 `OwnerLocalGameplayFactBuffer` 内按 `SourceDeltaSequence` patch `ExecutionCalculationOutputUpdated` 的 `Value` / `OldValue` / `NewValue`。
4. `AutoChessExecuteDamageCalculationSystem` 作为真实业务 damage execution adapter，不再把带 `SourceDeltaSequence` 的 execution fact 写回 singleton stream，而是写入目标 ASC owner-local fact buffer，使 pending delta apply 能在同一 owner-local lane 内完成 linked fact patch。
5. `Verify-GAS-RuntimeCoreBoundary.ps1` 新增防回流规则，覆盖 Runtime execution merge、pending AttributeDelta linked fact patch、AutoChess damage execution adapter 三个入口。

## 代码证据

| 事实 | 代码路径 |
|---|---|
| execution output fact merge 写 ASC owner-local fact buffer | `Assets/GAS/Runtime/System/Effect/GEExecutionCalculationSystem.cs` |
| pending AttributeDelta linked execution fact patch 改为 owner-local | `Assets/GAS/Runtime/System/Attribute/GASAttributeModifierDeltaApplySystem.cs` |
| AutoChess damage execution fact 写目标 ASC owner-local fact buffer | `Assets/AutoChessDemo/Battle/Ecs/AutoChessExecuteDamageCalculationSystem.cs` |
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

1. 不能写成 P0-D 已退出；`GEEffectCommandBuffer` command source、legacy stream migration/export 与部分 proof-only stream carrier 仍需要继续审查。
2. 不能写成 AutoChess headless 全链路已通过；本切片只覆盖静态门、runtime build 与 autochessdemo build。
3. 不能写成 typed fact 已完全不依赖 singleton stream；`GEEffectCommandSpecStreamPhases` 仍保留 frame prepare / legacy migration input / boundary export 相关读取或清理路径。

## 复发入口

如果 `GEExecutionCalculationSystem` 或 `AutoChessExecuteDamageCalculationSystem` 重新出现 `GetBufferLookup<GameplayEventBuffer>` / `FactLookup[StreamEntity]` / `facts.Add(new GameplayEventBuffer)`，或 `GASAttributeModifierDeltaApplySystem` 重新 patch singleton stream facts，应重新打开 P0-D 的 execution fact owner 子项。
