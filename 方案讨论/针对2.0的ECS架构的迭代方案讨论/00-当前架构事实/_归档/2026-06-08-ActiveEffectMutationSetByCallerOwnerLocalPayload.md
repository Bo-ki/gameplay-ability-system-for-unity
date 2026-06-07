# ActiveEffectMutation SetByCaller Owner-Local Payload

归档日期：2026-06-08

## 原问题

P0-D `singleton stream owner` 中，active mutation command 虽已投影到 ASC owner-local `ActiveEffectMutationCommandBuffer`，但 command 的 `SetByCallerStart/SetByCallerCount` 仍指向 singleton `GESetByCallerValueBuffer`。这会让 `GEActiveEffectMutationChunkApplyJob` 在 magnitude resolution / set-by-caller snapshot copy 时继续把 singleton stream 当 active mutation 输入源。

## 本轮处理

1. 新增并接线 ASC owner-local `ActiveEffectMutationSetByCallerValueBuffer`。
2. `GEEffectCommandCatalogNormalizeSystem` 在投影 `ActiveMutation` command 到目标 ASC 时，同步把该 command 的 `GESetByCallerValueBuffer` range 复制到目标 ASC owner-local payload buffer，并重写 owner-local command 的 `SetByCallerStart/SetByCallerCount`。
3. `GASActiveEffectMutationApplySystem` 的 collect 阶段读取 owner-local command + owner-local set-by-caller payload，展平成 frame-local `NativeList<GEEffectCommandBuffer>` 与 `NativeList<GESetByCallerValueBuffer>`。
4. `GEActiveEffectMutationChunkApplyJob` 的 active mutation magnitude resolution 与 active effect set-by-caller snapshot copy 读取 frame-local payload list，不再读取 singleton stream set-by-caller 作为 active mutation 输入。
5. `StreamSetByCallerLookup` 只保留给 overflow 派生命令写回 singleton command stream；这不代表 command/spec stream 全链路退出。
6. `GasGlueCodeGenPhases.cs` 模板同步，防止 `.gen.cs` 回流。
7. `Verify-GAS-RuntimeCoreBoundary.ps1` 新增 owner-local payload projection / collect / apply / template 防回流断言。

## 代码证据

- `Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs`
- `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeEntityArchetypes.cs`
- `Assets/GAS/Runtime/AbilitySystem/ASCEntityFactory.cs`
- `Assets/GAS/Runtime/System/Effect/GEEffectCommandSpecStreamPhases.cs`
- `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeQueryLayoutPlan.cs`
- `Assets/GAS/Generated/CodeGen/Runtime/ActiveEffectLifecycleOwnerSystems.cs`
- `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs`
- `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs`
- `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1`

## 仍未解决

本切片只退出 active mutation 输入侧的 set-by-caller singleton 依赖。以下内容仍是 P0-D / R3 后续范围：

- 原始 `GEEffectCommandBuffer` producer 仍写 singleton command stream。
- 非 active mutation 的 `GESetByCallerValueBuffer` 仍是 command/spec stream payload。
- instant spec build 仍读取 singleton command/spec stream。
- overflow / period derived command append 仍写 singleton command stream。
- `GameplayEventBuffer` stream export 仍是 BoundaryProjection 前的 typed fact export carrier。

## 复发入口

如果后续出现以下任一回流，应重新打开 P0-D / R3：

- `GEActiveEffectMutationChunkApplyJob` 重新以 `CommandSetByCallerLookup[StreamEntity]` 作为 active mutation 输入。
- generated 模板重新生成 active mutation 的 singleton set-by-caller precondition。
- ASC archetype / factory / frame prepare / query layout 丢失 `ActiveEffectMutationSetByCallerValueBuffer`。
- active mutation command 投影不再同步复制 set-by-caller payload。
