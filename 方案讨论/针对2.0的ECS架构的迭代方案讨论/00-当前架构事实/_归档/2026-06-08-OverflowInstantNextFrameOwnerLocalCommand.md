# Overflow instant next-frame owner-local command 归档

> 归档日期：2026-06-08
> 范围：P0-D singleton stream owner / overflow instant producer

## 原问题

`GASActiveEffectMutationApplySystem` 在 active effect lifecycle 阶段处理 stack overflow 时，active mutation overflow 已进入 ASC next-frame owner-local carrier，但 instant overflow 仍直接调用 `EffectCommandSpecStream.AppendPreparedCommand(...)` 追加到 singleton `GEEffectCommandBuffer` / `GESetByCallerValueBuffer`。该阶段晚于本帧 `OwnerLocalInstantCommandFlushSystem` 与 `GEEffectSpecBuildSystem`，不能写 current owner-local instant lane，否则下一帧 FramePrepare 会清空或错过本帧 flush。

## 解决事实

1. 新增 ASC owner-local next-frame instant carrier：
   - `OwnerLocalInstantNextFrameCommandBuffer`
   - `OwnerLocalInstantNextFrameSetByCallerValueBuffer`
2. `ASCEntityFactory`、`GASRuntimeEntityArchetypes.ASC(...)`、`GASRuntimeQueryLayoutPlan`、`GASRuntimeFrameStreamOwnerPlanner` 均把 next-frame instant carrier 归到 ASC owner-local layout。
3. `OwnerLocalInstantCommandFramePrepareSystem` 在 FramePrepare 清 current owner-local instant lane 后，把 next-frame instant command/payload 晋升到 current `GEEffectCommandBuffer` / `GESetByCallerValueBuffer`，再清空 deferred carrier。
4. `GASActiveEffectMutationApplySystem.EmitOverflowCommand(...)` 的 instant 分支不再持有 singleton command/payload buffer 参数，也不再调用 `EffectCommandSpecStream.AppendPreparedCommand(...)`；active 与 instant overflow 都写目标 ASC 的 next-frame owner-local carrier。
5. `GasGlueCodeGenPhases` 与 generated runtime 同步，防止下一次生成把 overflow instant producer 写回 singleton stream。
6. `Verify-GAS-RuntimeCoreBoundary.ps1` 新增防回流门：要求 next-frame instant buffer / archetype / factory / frame prepare / layout / stream owner contract / generated-template 路由存在，并禁止 `EmitOverflowCommand(...)` 回退到 `EffectCommandSpecStream.AppendPreparedCommand(...)`。

## 验证记录

- `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过。
- `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；仅既有 `MSB3277` assembly version conflict warnings。
- `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过；仅既有 `MSB3277` assembly version conflict warnings。

## 剩余风险

- `GEEffectSpecBuildSystem` 仍消费 singleton command/spec stream，本切片只是把 overflow instant producer 首跳移出 singleton append。
- 非 active mutation set-by-caller payload 与 spec carrier 仍在 singleton carrier 上。
- 需要后续以无头 AutoChess x50/x1000 验证 next-frame overflow instant 不引入 global buffer pressure、ordering 或延迟语义问题。

## 复发入口

如果 `EmitOverflowCommand(...)` 重新调用 `EffectCommandSpecStream.AppendPreparedCommand(...)`，或 `OwnerLocalInstantCommandFramePrepareSystem` 不再晋升 / 清理 next-frame instant carrier，应重新打开 P0-D：singleton stream owner 被当作多条 hot path 的事实源。
