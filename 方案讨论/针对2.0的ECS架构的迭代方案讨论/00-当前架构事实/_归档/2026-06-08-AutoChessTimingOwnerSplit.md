# AutoChess Timing Owner Split 归档

## 原问题

- R6/R8 timing evidence 仍容易被消费成纯 GAS physical group timing。
- Runtime Debugger 侧只有 `PhysicalGroup` aggregate 时，业务审查无法直接从 debugger evidence 区分 core runtime owner、boundary owner 和 runner/dependency-drain owner。

## 归档日期

2026-06-08

## 本轮代码变更

1. `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasObservationGateway.cs`
   - `RecordRuntimeTickTiming(...)` 继续发布 `PhysicalGroup` timing。
   - 新增 `OwnerSplit/CoreRuntimeOwner` aggregate，口径为 `FramePrepare + CommandResolve + CoreSimulation + StructuralCommit`。
   - 新增 `OwnerSplit/BoundaryOwner` aggregate，口径为 `BoundaryProjection`。
   - 新增 `OwnerSplit/RunnerOwner` aggregate，口径为 dependency drain sync。
2. `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1`
   - 增加 `AutoChessGasObservationGateway.cs` 静态门禁。
   - 阻断 Runtime Debugger timing evidence 退回只有 physical group、没有 owner split 的实现。

## 验证命令

```powershell
powershell -ExecutionPolicy Bypass -File Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1
dotnet build com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal
```

当前切片验证结果：上述命令通过；`dotnet build` 仍有既有 `MSB3277` warning，不是本切片新增错误。

## 能推出的结论

- AutoChess Runtime Debugger timing evidence 已有 `OwnerSplit/CoreRuntimeOwner`、`OwnerSplit/BoundaryOwner`、`OwnerSplit/RunnerOwner` 三类 owner aggregate。
- Validation summary 已有 `core/boundary/debugger/runner/physics/render` owner split 字段和 disabled reason 口径。
- R6/R8 后续审查可以直接检查 owner cost evidence，而不必从 physical group timing 反推 owner。

## 不能推出的结论

- 不能证明 x100/x1000 规模性能优秀。
- 不能证明 Unity headless、Unity Test Runner、Profiler enabled、Entities Journaling 或官方 profiler module 已跑通。
- 不能证明 physics/render 有实测成本；当前仍只能通过 disabled reason 表达未启用。
- 不能证明 PlayerLoop / scene presentation 成本已经纳入 ECS runtime tick。

## 复发入口

如果后续 `AutoChessGasObservationGateway.RecordRuntimeTickTiming(...)` 删除 `OwnerSplit` aggregate，或 validation summary 不再输出 owner split / disabled reason，应重新打开 R6/R8 timing evidence 问题。
