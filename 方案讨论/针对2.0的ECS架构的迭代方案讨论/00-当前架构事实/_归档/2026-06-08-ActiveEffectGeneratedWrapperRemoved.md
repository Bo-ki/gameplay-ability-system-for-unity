# ActiveEffect generated wrapper 删除归档

日期：2026-06-08

## 背景

上一轮已将 active-effect lifecycle 主执行迁入手写 Runtime owner：

- helper / job / snapshot / mutation / tick / remove 主体位于 `Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs`。
- 调度系统位于 `Assets/GAS/Runtime/System/Effect/GEActiveEffectLifecycleSystems.cs`。
- `RuntimeActiveEffect.gen.cs` 已退为 `GASGeneratedActiveEffectRuntimeMarker.HandwrittenRuntimeOwner = true` 的 pure glue marker。
- `GASSystemScheduleContract` 直接注册手写 Runtime systems，`GeneratedCoreSimulationSystemTypeNames` 已为空。

本轮处理的剩余风险是 generated runtime 物理 asmdef 下仍保留未调度的 `ActiveEffectLifecycleOwnerSystems.cs` wrapper。它已经不是 SourceGenerator 输出，也不进入 manifest，但继续存在会扩大编译面并形成防回流歧义。

## 本轮变更

已删除：

- `Assets/GAS/Generated/CodeGen/Runtime/ActiveEffectLifecycleOwnerSystems.cs`
- `Assets/GAS/Generated/CodeGen/Runtime/ActiveEffectLifecycleOwnerSystems.cs.meta`

当前 active-effect lifecycle 的唯一执行 owner 保持在手写 Runtime 路径；generated 路径只保留 `RuntimeActiveEffect.gen.cs` marker。

## 验证

- `dotnet run --project Tools\GasCodeGenCli\GasCodeGenCli.csproj -- core`：通过；生成器没有重建 `ActiveEffectLifecycleOwnerSystems.cs`。
- `dotnet build .\com.exhard.exgas.generated.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`：通过；仅保留既有 `MSB3277` 版本冲突警告。
- `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1`：当前工作树通过；注意该脚本本身处于并行脏改状态，结论只能作为当前工作树门禁，不写成干净基线。
- `Assets/GAS/Generated/CodeGen/GasCodeGenValidationReport.md`：`GeneratedRuntimePureGlueArtifacts = 4`，`GeneratedRuntimeLifecycleMigrationArtifacts = 0`，`GeneratedRuntimeRandomWriteLookupHits = 0`。
- `Assets/GAS/Generated/CodeGen/GasCodeGen.manifest.json`：`RuntimeActiveEffect.gen.cs` 仍分类为 `RuntimePureGlue`，没有 `ActiveEffectLifecycleOwnerSystems.cs` artifact。

## 结论

active-effect generated companion wrapper 已从 generated runtime 编译面删除。后续 R5/R7 的重点不再是这个 stale wrapper，而应继续推进 remaining runtime pure glue / hand-written owner 对账、system budget、release-ready gate 和 AutoChess 全链路性能证据。
