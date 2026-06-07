# EffectInstant 手写 Runtime owner 归档

> 归档日期：2026-06-08
> 对应链路：Instant GE spec build / attribute set reduce apply
> 关联代码：`GEEffectInstantSystems.cs`、`RuntimeEffectInstant.gen.cs`、`GASRuntimeDefinitionResolver.cs`、`GASSystemScheduleContract.cs`、`GasGlueCodeGenPhases.cs`、`Verify-GAS-RuntimeCoreBoundary.ps1`

## 原问题

`RuntimeEffectInstant.gen.cs` 之前同时承载 instant spec build 和 attribute set reduce apply 的 runtime lifecycle owner。它在 generated runtime assembly 内声明 `GEEffectSpecBuildSystem` / `GASAttributeSetReduceApplySystem`，并通过 generated lookup / requirement / magnitude helper 驱动运行时 job。

这不符合当前 Runtime Core 分层：generated runtime 只能保留 pure glue、catalog 和 definition adapter；生命周期 query、job dependency、owner-local payload 消费、attribute reduce apply 和 fact output 都应由手写 Runtime Core owner 承担。

## 本轮处理

1. 新增 `Assets/GAS/Runtime/System/Effect/GEEffectInstantSystems.cs`，由手写 runtime assembly 接管 `GEEffectSpecBuildSystem` 与 `GASAttributeSetReduceApplySystem`。
2. `GEEffectSpecBuildSystem` 直接消费 ASC owner-local `GEEffectCommandBuffer` / `GESetByCallerValueBuffer`，继续保持 command sequence deterministic ordering 和 spec-local set-by-caller payload 重映射。
3. `GASAttributeSetReduceApplySystem` 迁到 hand-written Runtime Core，attribute set reduce / modifier delta / gameplay fact 输出不再由 generated instant artifact 承担。
4. `GASRuntimeDefinitionResolver.cs` 增加 runtime-side `GASRuntimeRequirementEvaluator` 与 `GASRuntimeMagnitudeEvaluator` 入口，避免手写 runtime owner 反向依赖 `GAS.Runtime.Generated`。
5. `RuntimeEffectInstant.gen.cs` 退为 `GASGeneratedEffectInstantRuntimeMarker`，`HandwrittenRuntimeOwner = true`。
6. `GASSystemScheduleContract` 把 `GEEffectSpecBuildSystem` / `GASAttributeSetReduceApplySystem` 纳入 CoreSimulation 手写系统列表，并从 generated core simulation type-name 列表移除旧 generated instant owner。
7. 手写 instant owner 不直接 `typeof` 引用 generated active-effect lifecycle system；当前残留 normalize 顺序由 active-effect owner artifact 的 `UpdateBefore(typeof(GEEffectSpecBuildSystem))` 与 schedule contract type-name 注册兜底。
8. 诊断脚本新增 marker、report、schedule、hand-written owner 和禁止 direct generated lifecycle dependency 的防回流断言。

## CodeGen report

- `GeneratedRuntimeBoundaryHits`: `31`
- `GeneratedRuntimePureGlueArtifacts`: `3`
- `GeneratedRuntimeLifecycleMigrationArtifacts`: `1`
- `GeneratedRuntimeLifecycleHits`: `0`
- `GeneratedRuntimeRandomWriteLookupHits`: `25`
- `RuntimeEffectInstant.gen.cs` 已归类为 `RuntimePureGlue`。
- 当前仅 `RuntimeActiveEffect.gen.cs` 仍为 `RuntimeLifecycleMigration`。

相对上一切片 ability commit 后的基线，本轮把 generated runtime boundary hit 从 `47` 降到 `31`，lifecycle migration artifact 从 `2` 降到 `1`，lifecycle hit 从 `6` 降到 `0`，random lookup hit 从 `33` 降到 `25`。

## 验证

- `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1`
- `dotnet build .\com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`
- `dotnet build .\com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`
- `dotnet build .\com.exhard.exgas.autochessdemo.editor.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`
- `Temp/AutoChessBattleValidation-ExecutionFactOwnerLocal-FinalRun2.log`：`passed=True` / `thresholdsPassed=True` / `runtimeChainPassed=True` / `repeatRunPassed=True`，`executionOutputs=350`、`executionMatchedEffectSpecs=350`、`pendingAttributeAppliedDeltas=250`、`ownerLocalFacts=5200`。

边界门和目标 build 均通过。保留既有 `MSB3277` 引用版本冲突 warning。

## 仍未完成

- `RuntimeActiveEffect.gen.cs` 是唯一剩余 runtime lifecycle migration artifact，也是下一轮 owner 迁移主目标。
- `GasGlueCodeGenPhases.cs` 中旧 `WriteGeneratedInstantSpecBuildSystem(...)` / `WriteGeneratedAttributeDeltaApplySystem(...)` 方法仍残留但不再被调用，后续可作为 codegen dead template 清理项删除。
- 本切片已有 AutoChess x50 业务链路证据，但仍未覆盖 Unity Test Runner、x100/x1000 profile 和 Profiler enabled 采样，不能作为性能终局结论。
