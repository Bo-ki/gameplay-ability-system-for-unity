# ActiveEffect generated wrapper 删除目标态归档

日期：2026-06-08

## 目标态裁决

Active-effect lifecycle 不再允许由 generated runtime wrapper 承载 query、ECB、NativeContainer、lookup refresh 或 `ISystem` 调度职责。SourceGenerator 在这条链路上只允许保留 immutable catalog / pure glue / marker 输出；生命周期 owner 必须位于手写 Runtime Core。

## 已落地边界

- `Assets/GAS/Generated/CodeGen/Runtime/ActiveEffectLifecycleOwnerSystems.cs` 与 `.meta` 已删除。
- `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs` 继续作为 `RuntimePureGlue` marker。
- `Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs` 承接 active-effect helper / job / snapshot / mutation / tick / remove。
- `Assets/GAS/Runtime/System/Effect/GEActiveEffectLifecycleSystems.cs` 承接 active-effect lifecycle systems。
- `GASSystemScheduleContract` 的 CoreSimulation 直接引用手写 Runtime systems，不再依赖 generated type-name fallback。

## 防回流要求

- `GasGlueCodeGenPhases.cs` 不得恢复 `WriteRuntimeActiveEffectLifecycleOwnerSystems` 或输出 `Runtime/ActiveEffectLifecycleOwnerSystems.cs`。
- `GasCodeGen.manifest.json` 和 `GasCodeGenValidationReport.md` 不得出现 `ActiveEffectLifecycleOwnerSystems.cs`。
- `GeneratedRuntimeLifecycleMigrationArtifacts` 必须保持为 `0`，`RuntimeActiveEffect.gen.cs` 必须保持 `RuntimePureGlue`。
- runtime boundary gate 应继续检查手写 lifecycle systems 存在，并阻断 generated marker 回填 runtime jobs。

## 验证证据

- core codegen 通过，未重建 wrapper。
- generated runtime assembly 编译通过，仅既有 `MSB3277` 警告。
- runtime core boundary 当前工作树通过；由于诊断脚本含并行脏改，该结果不作为干净基线声明。

## 下一步

把 active-effect wrapper 从风险清单中降级为已删除历史项。后续迭代继续处理 generated pure glue 与手写 Runtime owner 的负例验证、system 数量预算，以及无头 AutoChess demo 的真实业务全链路和性能指标。
