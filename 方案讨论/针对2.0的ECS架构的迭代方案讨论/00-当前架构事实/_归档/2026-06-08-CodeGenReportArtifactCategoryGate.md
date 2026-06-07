# CodeGen validation report ArtifactCategory gate 归档

> 日期：2026-06-08
> 范围：CodeGen validation report / generated runtime hot path gate
> 状态：阶段性收口，继续保留 RuntimeLifecycleMigration 债务追踪

## 变更事实

本切片把 codegen manifest 的 `ArtifactCategory` 显式暴露到 `GasCodeGenValidationReport.md`，让 runtime generated artifact 的分类从隐含字段变成可审查证据：

1. `GasGlueCodeGenPhases` 生成的 Manifest Entries 表新增 `ArtifactCategory` 列。
2. manifest entry 未分类时在报告中显示 `None`，避免空列误读成漏生成。
3. `RuntimeDefinitionGlue.gen.cs` 在报告中显示为 `RuntimePureGlue`。
4. `RuntimeAbilityActivation.gen.cs` / `RuntimeEffectInstant.gen.cs` / `RuntimeActiveEffect.gen.cs` 在报告中显示为 `RuntimeLifecycleMigration`。
5. `Verify-GAS-RuntimeCoreBoundary.ps1` 新增断言，要求模板和生成报告都包含 `ArtifactCategory` 列，并要求关键 runtime artifact 分类不可回退。
6. 重新 codegen 暴露出 `RuntimeEffectInstant.gen.cs` 的 generated hot path 回归，已把 `GEEffectSpecBuildSystem` 的 scratch `NativeList` 从 `Allocator.TempJob` 改为 `state.WorldUpdateAllocator`。
7. `GEEffectSpecBuildSystem` 不再为 WorldUpdateAllocator scratch list 调度 dispose job，直接把 `state.Dependency` 接到 build job。
8. 修复 `RuntimeEffectInstant` 模板多输出的一层 `}`，避免后续 job/helper 被生成到 system 外。
9. 删除重复 `CompareEntity(Entity, Entity)` helper 输出，保留 shared helper，`GeneratedDuplicateMethodHits` 回到 `0`。

## 代码证据

| 事实 | 代码路径 |
|---|---|
| validation report manifest 表输出 `ArtifactCategory` | `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs` |
| generated report 已包含分类列与 runtime artifact 分类 | `Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md` |
| RuntimeEffectInstant generated hot path 改为 WorldUpdateAllocator | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` |
| DefinitionIndex codegen 刷新后的 using 输出 | `Assets/GAS/Generated/CodeGen/Runtime/DefinitionIndex.gen.cs` |
| 防回流验证 | `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` |

## 验证

已运行：

1. `dotnet run --project Tools\GasCodeGenCli\GasCodeGenCli.csproj -- core` 通过；生成报告中 `GeneratedHotPathRegressionHits: 0`、`GeneratedDuplicateMethodHits: 0`。
2. `dotnet build .\com.exhard.exgas.generated.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；0 error，保留既有 `MSB3277` warning。
3. `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过。
4. `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；4 个既有 `MSB3277` warning，0 error。
5. `dotnet build .\com.exhard.exgas.editor.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；42 个既有 warning，0 error。

未运行：

1. Unity headless AutoChess。
2. Unity Test Runner。
3. x100 / x1000 scale profile。
4. Profiler / Journaling enabled pass。

## 不能推出的结论

1. 不能写成 generated runtime 已完成目标态迁移；报告仍显示 `GeneratedRuntimeBoundaryHits: 66`。
2. 不能写成 `RuntimeLifecycleMigration` artifact 已无性能风险；当前只是把迁移证明分类显式化，并清掉本次 codegen 暴露出的 hot path / duplicate helper gate。
3. 不能写成 AutoChess headless 全链路或性能目标已通过；本切片只有 codegen、静态边界和 targeted dotnet build 证据。

## 复发入口

如果 `GasCodeGenValidationReport.md` 的 Manifest Entries 表不再包含 `ArtifactCategory`，或 `RuntimeDefinitionGlue` / `RuntimeAbilityActivation` 的分类回退，应重新打开 CodeGen validation report 分类 gate 子项。
