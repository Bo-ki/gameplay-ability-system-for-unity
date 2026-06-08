# ActiveEffect Runtime Owner 手写化事实归档

日期: 2026-06-08

## 本轮事实

- `RuntimeActiveEffect.gen.cs` 已从大型 runtime helper/jobs 产物降级为 `GASGeneratedActiveEffectRuntimeMarker`，只保留 `HandwrittenRuntimeOwner = true` 的 ownership 标记。
- active-effect 生命周期核心 helper/jobs 已迁入手写 Runtime Core: `Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs`。
- active-effect 生命周期系统 owner 已迁入手写 Runtime Core: `Assets/GAS/Runtime/System/Effect/GEActiveEffectLifecycleSystems.cs`，覆盖 `GASActiveEffectPreTickSystem`、`GASActiveEffectRemoveSystem`、`GASActiveEffectMutationApplySystem`。
- `GASSystemScheduleContract` 不再注册 generated active-effect lifecycle systems；CoreSimulation 直接注册手写 pre-tick/remove/normalize/spec-build/apply 顺序。
- `GEEffectCommandCatalogNormalizeSystem` 明确 `UpdateAfter(GASActiveEffectRemoveSystem)`，避免显式 remove 与命令归一化顺序漂移。
- `ActiveEffectLifecycleOwnerSystems.cs` 仍保留在 generated 路径中作为未调度的残留 wrapper；为保持当前 generated runtime assembly 编译，它已改为引用手写 `GASActiveEffectRuntime` 和 `GASDefinitionCatalogLookup`。后续可整块删除或迁出 generated 路径。

## CodeGen 边界事实

- `GasCodeGen.manifest.json` 将 `RuntimeActiveEffect.gen.cs` 分类为 `RuntimePureGlue`。
- `GasCodeGenValidationReport.md` 当前关键指标:
  - `GeneratedRuntimeBoundaryHits: 1`
  - `GeneratedRuntimePureGlueArtifacts: 4`
  - `GeneratedRuntimeLifecycleMigrationArtifacts: 0`
  - `GeneratedRuntimeLifecycleHits: 0`
  - `GeneratedRuntimeStructuralChangeHits: 0`
  - `GeneratedRuntimeRandomWriteLookupHits: 0`

## 已执行验证

- `dotnet run --project Tools\GasCodeGenCli\GasCodeGenCli.csproj -- core`: 通过。
- `dotnet build .\com.exhard.exgas.generated.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`: 通过；仅有既有 `MSB3277` 引用版本冲突警告。
- `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1`: 当前完整工作树通过。注意该脚本本次运行读取了工作树里的 AutoChess capability 并行改动，因此不能作为纯 active-effect staged diff 的完全隔离门禁。

## 未覆盖验证

- 尚未执行 Unity Test Runner。
- 尚未执行无头 AutoChess demo 全链路测试。
- 尚未执行 x100/x1000 profile 或性能回归采样。
