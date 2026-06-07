# ActiveEffectMutation owner-local carrier 事实归档

> 日期：2026-06-08
> 范围：P0-D singleton stream owner / ActiveEffectMutation output carrier

## 变更事实

1. `ActiveEffectMutationBuffer` 已从 `GASRuntimeEntityArchetypes.EffectCommandStream()` 移除，不再作为 `GEEffectCommandStreamComponent` singleton stream owner 的 required buffer。
2. `EffectCommandSpecStream.HasRequiredBuffers()`、`ClearFrameLocalData(...)` 与 `GEEffectCommandSpecStreamFramePrepareSystem` 不再要求或清理 stream 侧 `ActiveEffectMutationBuffer`。
3. 新增 `ActiveEffectOwnerLocalMutationFramePrepareSystem`，在 `GASFramePrepareSystemGroup` 中清空 ASC owner-local `ActiveEffectMutationBuffer`。
4. `GASRuntimeFrameStreamOwnerPlanner` 将 `ActiveEffectMutation` 的 current carrier 从 `SingletonDynamicBuffer` 调整为 `OwnerLocalDynamicBuffer`。
5. `GASRuntimeQueryLayoutPlan` 将 `ActiveEffectMutationBuffer` 从 command/spec stream 辅助槽迁入 ActiveEffectStore / ASC owner 辅助槽。
6. 新增 `ActiveEffectMutationCommandBuffer` 作为 ASC owner-local command carrier；`GEEffectCommandCatalogNormalizeSystem` 将 normalized `GEEffectCommandBuffer` 中的 `ActiveMutation` command 投影到目标 ASC。
7. `GASActiveEffectMutationApplySystem` 的 generated gather 已拆成 `GEActiveEffectMutationOwnerCommandCollectJob : IJobChunk` 和 `GEActiveEffectMutationOwnerCommandFinalizeJob : IJob`：消费侧从 ASC owner-local command buffer 收集，再按 owner range 进入 chunk-local apply。
8. `GEEffectCommandStreamComponent.ActiveMutationCommandCursor` 已删除；下一帧 stream compaction 只依赖 `SpecBuildCommandCursor`，active mutation lane 不再把 singleton command cursor 当消费事实。
9. `GasGlueCodeGenPhases.cs` 模板已同步 owner-local command collect/finalize，防止 SourceGenerator 回生 `GEActiveEffectMutationGatherJob : IJob` 的 singleton stream gather。

## 明确不能推出的结论

1. 本切片不证明 active mutation 全链路 scale-ready。
2. `GEEffectCommandBuffer` producer、set-by-caller range、instant spec build、execution extension 以及 overflow/period command append 仍依赖 singleton command/spec stream；本切片只收口 ActiveMutation 的消费侧 command source。
3. x50/x100/x1000 profile、Profiler enabled pass、Unity headless AutoChess 与 Journaling source-phase 对账仍未由本切片证明。

## 本轮验证

1. 自定义静态门通过：确认 stream required buffers / clear / frame prepare 不再触碰 `ActiveEffectMutationBuffer`，ASC owner-local clear system、schedule contract、stream owner plan 和 query layout 已同步，并禁止 `GEActiveEffectMutationGatherJob : IJob` 回流。
2. `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过。
3. `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过，保留既有 `MSB3277` warning。
4. `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过，保留既有 `MSB3277` warning。

说明：上述 build / diagnostics 在当前 owner-local carrier 工作树上运行；本轮提交收口 ActiveEffectMutation owner-local carrier / owner-local command source，其他目标态文档拆分治理不纳入本提交。

## 后续入口

后续 P0-D/R3 继续处理 command producer / set-by-caller / instant spec / execution extension / Boundary fact export 的 carrier 归属；P0-C/R5 继续把 overflow / period derived command append 从 singleton stream 迁出。
