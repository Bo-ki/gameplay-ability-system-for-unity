# 2026-06-08 ASC / ActiveEffect Fact Owner-Local 写入归档

## 本次切片

将 ASC command resolve 与 ActiveEffect lifecycle 生成链路中的 Core fact 写入从 singleton `GameplayEventBuffer` 迁到 ASC `OwnerLocalGameplayFactBuffer`。

## 当前代码事实

- `ASCCommandBufferResolveSystem` 的 TagChanged、AttributeBaseValueChanged、AbilityCancelRequested / AbilityEndRequested fact 写入 ASC owner-local fact buffer。
- `GASGeneratedActiveEffectRuntime.GEActiveEffectMutationChunkApplyJob` 与 `GEActiveEffectPreTickJob` 改为通过 `OwnerFactLookup` 写目标 ASC owner-local fact buffer。
- `ActiveEffectLifecycleOwnerSystems` 三个 system wrapper 改为注入 `OwnerFactLookup`，不再给 active effect lifecycle job 注入 `GameplayEventBuffer` 写权限。
- `GasGlueCodeGenPhases` 同步更新 Ability commit 与 ActiveEffect lifecycle 生成模板，防止下一次 CodeGen 重新生成 singleton fact writer。
- `GameplayBoundaryFactExportSystem` 仍负责把 owner-local facts 投影到 `BoundaryObservationFactBuffer`，并继续兼容 legacy stream facts 作为迁移输入。
- fact sequence 当前仍由 `GEEffectCommandStreamComponent.NextFactSequence` 分配；sequence owner 还没有迁出 stream owner。

## 已增加防回流验证

`Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` 新增检查：

- ASC command resolve 必须获取 `OwnerLocalGameplayFactBuffer`，不得获取或写入 singleton `GameplayEventBuffer`。
- ActiveEffect lifecycle owner systems / generated runtime / CodeGen template 必须使用 `OwnerFactLookup`。
- ActiveEffect lifecycle generated runtime 和模板不得声明 `BufferLookup<GameplayEventBuffer> FactLookup`，也不得 `FactLookup[StreamEntity].Add(evt)`。

## 本次验证

- `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1`：通过。
- `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`：通过，仍有既有 `MSB3277` warning。
- `dotnet build .\com.exhard.exgas.editor.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`：通过，仍有既有 `MSB3277`、Unity obsolete API 与 `_autoSaveRunning` 未使用字段 warning。
- `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`：通过，仍有既有 `MSB3277` warning。

## 仍未完成

本次没有迁移所有 legacy `GameplayEventBuffer` writer。后续仍需处理：

- `GASAttributeModifierDeltaApplySystem`
- `GEExecutionCalculationSystem`
- AutoChess demo damage calculation writer

`AttributeThresholdAbilityLifecycleRequestSystem` 与 `AbilityStateCleanupSystem` 已在同日 hand-written ability fact owner-local 切片中迁出 singleton fact stream。
