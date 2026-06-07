# AttributeDelta owner-local fact projection 事实归档

> 日期：2026-06-08
> 范围：P0-D singleton stream owner / AttributeDelta fact projection carrier
> 状态：阶段性收口，未退出 P0-D

## 变更事实

本切片把“已经直接应用 Attribute 值、仅用于 fact projection 的 AttributeDelta record”从 singleton `AttributeModifierBuffer` stream 中退掉，改为直接写入目标 ASC 的 `OwnerLocalGameplayFactBuffer`：

1. `GASRuntimeEntityArchetypes.EffectCommandStream()` 不再包含 `AttributeModifierBuffer`，`EffectCommandSpecStream.HasRequiredBuffers(...)` 和 frame prepare 也不再要求或清理 stream 侧 delta buffer。
2. `GameplayFactProjectionSystem` 不再从 stream `AttributeModifierBuffer` 生成 Attribute fact；该系统只保留 GE spec -> cue request fact projection。
3. `GEExecutionCalculationOutputModifierSystem` 在应用 execution output modifier 后，直接把 Attribute fact 写入 `OwnerLocalGameplayFactBuffer`，并使用 `GEEffectCommandStreamComponent.NextDeltaSequence` / `NextFactSequence` 保持序列证据。
4. `RuntimeEffectInstant.gen.cs` 的 generated instant GE reduce 路径同步改为写目标 ASC owner-local fact buffer，不再通过 `DeltaLookup` 写 singleton stream delta。
5. `GasGlueCodeGenPhases.cs` 同步更新 generator 模板，防止 SourceGenerator 下次把 generated instant 路径回生到 stream delta。
6. `GASRuntimeFrameStreamOwnerPlanner` 将 `AttributeDelta` current carrier 改为 `OwnerLocalDynamicBuffer`，target carrier 保持 `OwnerLocalDynamicBuffer`。
7. `GASRuntimeQueryLayoutPlan` 将 `AttributeDeltaBuffer` 从 `GameplayEffectCommandSpecStream` 布局迁入 ASC / `ActiveEffectStore` owner-local 辅助槽。
8. `GasRuntimeDebugger.RecordEffectCommandSpecStreamPressure(...)` 不再把 `AttributeModifierBuffer` 作为 EffectCommandSpecStream pressure 采样项。
9. `Verify-GAS-RuntimeCoreBoundary.ps1` 新增防回流门，要求 stream archetype / required buffers / generated instant / CodeGen 模板 / execution output / stream owner plan / query layout 同时保持 owner-local AttributeDelta fact projection。

## 代码证据

| 事实 | 代码路径 |
|---|---|
| stream archetype 退出 AttributeModifierBuffer | `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeEntityArchetypes.cs` |
| stream required buffers / frame prepare 不再清 AttributeDelta | `Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs`、`Assets/GAS/Runtime/System/Effect/GEEffectCommandSpecStreamPhases.cs` |
| GameplayFactProjectionSystem 不再读 stream AttributeModifierBuffer | `Assets/GAS/Runtime/System/Effect/GEEffectCommandSpecStreamPhases.cs` |
| execution output Attribute fact 写 owner-local fact buffer | `Assets/GAS/Runtime/System/Effect/GEExecutionCalculationOutputModifierSystem.cs` |
| generated instant Attribute fact 写 owner-local fact buffer | `Assets/GAS/Generated/CodeGen/Runtime/RuntimeEffectInstant.gen.cs` |
| generator 防回流 | `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs` |
| AttributeDelta stream owner / layout owner 更新 | `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeStreamOwnerContract.cs`、`Assets/GAS/Runtime/System/SystemGroup/GASRuntimeQueryLayoutPlan.cs` |
| debugger stream pressure 不再采样 AttributeModifierBuffer | `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs` |
| 防回流验证 | `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` |

## 验证

已运行：

1. `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过，并输出 `GAS Runtime Core AttributeDelta owner-local fact projection contract passed...`。
2. `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；保留既有 `MSB3277` warning。
3. `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；保留既有 `MSB3277` warning。

未运行：

1. Unity headless AutoChess。
2. Unity Test Runner。
3. x100 / x1000 scale profile。
4. Profiler / Journaling enabled pass。
5. Luban / SourceGenerator 重跑。

## 不能推出的结论

1. 不能写成 P0-D 已退出；`GEEffectCommandBuffer` command source、set-by-caller、instant spec、typed fact export 与 Boundary fact export 仍未整体退出 singleton stream owner。
2. 不能写成 AttributeDelta 全链路已是 NativeStream scale-ready；本次只把直接应用后的 fact projection record 迁到 owner-local fact lane，pending core apply 仍使用 ASC owner-local `AttributeModifierBuffer`，后续还需要容量、spill、merge cost 与 x100/x1000 证据。
3. 不能写成 AutoChess headless 或性能目标已达成；本切片只有静态门与 targeted dotnet build。

## 复发入口

如果 `EffectCommandStream()` / `HasRequiredBuffers(...)` 重新要求 `AttributeModifierBuffer`，或 generated instant / execution output 重新出现 `DeltaLookup` / `DeltaBufferLookup` 写 stream delta，或 `AttributeDelta` owner contract 退回 `SingletonDynamicBuffer`，应重新打开 P0-D 的 AttributeDelta carrier 子项。
