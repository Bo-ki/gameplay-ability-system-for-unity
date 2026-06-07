# Overflow Instant Next-Frame Owner-Local Command 归档

归档时间：2026-06-08

## 背景

`GASActiveEffectMutationApplySystem` 运行在 `OwnerLocalInstantCommandFlushSystem` 和 `GEEffectSpecBuildSystem` 之后的 active effect lifecycle 阶段。overflow active mutation 已经通过 `ActiveEffectNextFrameMutationCommandBuffer` 延迟到下一帧，但 overflow instant 仍在 apply 阶段直接调用 `EffectCommandSpecStream.AppendPreparedCommand(...)`，把派生命令写回 singleton command/spec stream。

直接把 overflow instant 写入 current owner-local `GEEffectCommandBuffer` 不成立：同帧的 owner-local instant flush 已经执行完成，下一帧 FramePrepare 又会清空 current owner-local instant lane，导致命令丢失。

## 决策

新增 ASC owner-local next-frame instant lane：

- `OwnerLocalInstantNextFrameCommandBuffer`
- `OwnerLocalInstantNextFrameSetByCallerValueBuffer`

overflow instant 派生命令写入目标 ASC 的 next-frame instant lane。下一帧 `OwnerLocalInstantCommandFramePrepareSystem` 先清 current owner-local instant command/payload，再把 next-frame command/payload 晋升到 current `GEEffectCommandBuffer` / `GESetByCallerValueBuffer`，最后由既有 `OwnerLocalInstantCommandFlushSystem` 按 command sequence 确定性 flush 到当前 spec stream consumer。

## 改动面

- `GEEffectCommandSpecStream.cs` 定义 next-frame instant command/payload buffer。
- `GASRuntimeEntityArchetypes.cs`、`ASCEntityFactory.cs`、`GASRuntimeQueryLayoutPlan.cs`、`GASRuntimeStreamOwnerContract.cs` 将新 buffer 纳入 ASC core layout、capacity 初始化、layout 分类和 owner contract。
- `GEEffectCommandSpecStreamPhases.cs` 将 `OwnerLocalInstantCommandFramePrepareSystem` 从单纯清空改为清空 current lane 后晋升 next-frame instant lane。
- `RuntimeActiveEffect.gen.cs` 和 `GasGlueCodeGenPhases.cs` 将 overflow instant 从 singleton append 改为写目标 ASC next-frame instant lane；active overflow 继续使用 next-frame active mutation lane。
- `Verify-GAS-RuntimeCoreBoundary.ps1` 增加 new lane 结构断言、overflow instant owner-local 断言和 `EmitOverflowCommand` 禁止 `EffectCommandSpecStream.AppendPreparedCommand(...)` 的回流护栏。

## 剩余风险

本切片只迁出 overflow instant producer 的首跳 singleton 写入。`GEEffectSpecBuildSystem` 仍消费 singleton spec stream，非 active mutation set-by-caller 与 spec carrier 仍在 singleton carrier 上；目标达成前仍需要继续迁 spec build consumer / carrier，并用无头 AutoChess x50/x1000 进行真实业务全链路验证。

## 验证

- `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过。
- `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；仅既有 `MSB3277` assembly version conflict warnings。
- `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；仅既有 `MSB3277` assembly version conflict warnings。
