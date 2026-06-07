# Ability commit 手写 Runtime owner 归档

> 日期：2026-06-08
> 范围：Ability commit lifecycle / generated runtime migration debt
> 状态：阶段性收口，generated effect lifecycle 仍保留迁移债务

## 变更事实

本切片把 Ability commit lifecycle 从 generated `AbilityCatalogCommitSystem` 迁出，由手写 Runtime Core 系统接管：

1. `RuntimeAbilityActivation.gen.cs` 不再生成 `AbilityCatalogCommitSystem` / `AbilityCatalogCommitJob`，退为 `GASGeneratedAbilityActivationRuntimeMarker`。
2. `AbilityCommitSystem` 从空 anchor 改为真实 `ISystem`，拥有 commit query、job 调度、dependency 和 commit request enabled-mask 过滤。
3. `AbilityCommitSystem` 继续写 ASC owner-local `GEEffectCommandBuffer`、`ActiveEffectMutationCommandBuffer`、`ActiveEffectMutationSetByCallerValueBuffer` 与 `OwnerLocalGameplayFactBuffer`，不再依赖 generated runtime assembly。
4. 新增 `GASRuntimeDefinitionResolver` / `GASRuntimeRequirementEvaluator`，把 ability activation plan、GE command seed 和 requirement 判定收口到 runtime assembly。
5. `GASDefinitionCatalogLookup` 增加 runtime 可用的 catalog created / gameplay effect lookup helper，避免 runtime 反向引用 `GAS.Runtime.Generated`。
6. `GASSystemScheduleContract` 的 generated command resolve 列表退为空，不再按字符串注册 `GAS.Runtime.Generated.AbilityCatalogCommitSystem`。
7. CodeGen 仍生成 `RuntimeAbilityActivation.gen.cs`，但 manifest / validation report 已把该 artifact 分类为 `RuntimePureGlue`。
8. `GasCodeGenValidationReport.md` 的关键指标从 `GeneratedRuntimeBoundaryHits: 66` 降到 `47`，`GeneratedRuntimeLifecycleMigrationArtifacts: 3` 降到 `2`，`GeneratedRuntimeRandomWriteLookupHits: 49` 降到 `33`。

## 代码证据

| 事实 | 代码路径 |
|---|---|
| 手写 commit owner | `Assets/GAS/Runtime/System/Ability/AbilityCommitSystem.cs` |
| runtime definition / requirement helper | `Assets/GAS/Runtime/Definition/GASRuntimeDefinitionResolver.cs` |
| runtime catalog lookup helper | `Assets/GAS/Runtime/Definition/GASDefinitionCatalogLookup.cs` |
| generated command resolve 注册退场 | `Assets/GAS/Runtime/System/SystemGroup/GASSystemScheduleContract.cs` |
| ability activation generated artifact 退为 marker | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs` |
| manifest / validation 分类刷新 | `Assets/GAS/Generated/CodeGen/GasCodeGen.manifest.json`、`Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md` |
| 防回流验证 | `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` |

## 验证

已运行：

1. `dotnet run --project Tools\GasCodeGenCli\GasCodeGenCli.csproj -- core` 通过。
2. `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过。
3. `dotnet build .\com.exhard.exgas.generated.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；0 error，保留既有 `MSB3277` warning。
4. `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；0 error，保留既有 `MSB3277` warning。
5. `dotnet build .\com.exhard.exgas.editor.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；42 个既有 warning，0 error。

未运行：

1. Unity headless AutoChess。
2. Unity Test Runner。
3. x100 / x1000 scale profile。
4. Profiler / Journaling enabled pass。

## 不能推出的结论

1. 不能写成 generated runtime lifecycle debt 已全部退出；`RuntimeEffectInstant.gen.cs` 和 `RuntimeActiveEffect.gen.cs` 仍是 `RuntimeLifecycleMigration`。
2. 不能写成 ability commit 已完成最终性能形态；当前只把 owner 从 generated 迁到 hand-written Runtime Core，后续仍要继续压缩跨 owner lookup 面。
3. 不能写成 AutoChess headless 全链路或性能目标已通过；本切片只有 codegen、静态边界和 targeted dotnet build 证据。

## 复发入口

如果 `RuntimeAbilityActivation.gen.cs` 重新生成 `AbilityCatalogCommitSystem`，或 `GASSystemScheduleContract` 重新注册 generated ability commit system，应重新打开 Ability commit owner 迁移子项。
