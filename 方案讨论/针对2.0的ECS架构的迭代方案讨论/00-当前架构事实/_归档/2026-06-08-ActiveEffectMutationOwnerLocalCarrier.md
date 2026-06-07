# ActiveEffectMutation owner-local carrier 事实归档

> 日期：2026-06-08
> 范围：P0-D singleton stream owner / ActiveEffectMutation output carrier

## 变更事实

1. `ActiveEffectMutationBuffer` 已从 `GASRuntimeEntityArchetypes.EffectCommandStream()` 移除，不再作为 `GEEffectCommandStreamComponent` singleton stream owner 的 required buffer。
2. `EffectCommandSpecStream.HasRequiredBuffers()`、`ClearFrameLocalData(...)` 与 `GEEffectCommandSpecStreamFramePrepareSystem` 不再要求或清理 stream 侧 `ActiveEffectMutationBuffer`。
3. 新增 `ActiveEffectOwnerLocalMutationFramePrepareSystem`，在 `GASFramePrepareSystemGroup` 中清空 ASC owner-local `ActiveEffectMutationBuffer`。
4. `GASRuntimeFrameStreamOwnerPlanner` 将 `ActiveEffectMutation` 的 current carrier 从 `SingletonDynamicBuffer` 调整为 `OwnerLocalDynamicBuffer`。
5. `GASRuntimeQueryLayoutPlan` 将 `ActiveEffectMutationBuffer` 从 command/spec stream 辅助槽迁入 ActiveEffectStore / ASC owner 辅助槽。

## 明确不能推出的结论

1. 本切片不证明 active mutation 全链路 scale-ready。
2. `GEEffectCommandBuffer` command source、generated gather 和 overflow/period command append 仍依赖 singleton command carrier。
3. x50/x100/x1000 profile、Profiler enabled pass、Unity headless AutoChess 与 Journaling source-phase 对账仍未由本切片证明。

## 本轮验证

1. 自定义静态门通过：确认 stream required buffers / clear / frame prepare 不再触碰 `ActiveEffectMutationBuffer`，ASC owner-local clear system、schedule contract、stream owner plan 和 query layout 已同步。
2. `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过。
3. `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过，保留既有 `MSB3277` warning。
4. `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过，保留既有 `MSB3277` warning。

说明：上述 build / diagnostics 在当前工作树上运行，工作树中仍有其他未提交的 AutoChess timing / owner-local fact diagnostics / 文档整理改动；本切片提交时只暂存 active mutation carrier 相关文件。

## 后续入口

后续 P0-D/R3 继续处理 command / set-by-caller / instant spec / AttributeDelta / Boundary fact export 的 carrier 归属；P0-C/R5 继续处理 generated active mutation command source 与模板防回流。
