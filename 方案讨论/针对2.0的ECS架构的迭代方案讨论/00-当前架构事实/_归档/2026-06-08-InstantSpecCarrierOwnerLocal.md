# InstantSpecCarrierOwnerLocal 事实归档

> 日期：2026-06-08
> 范围：P0-D singleton stream owner / instant spec carrier / generated instant AttributeReduce

## 原问题

上一切片已经让 `GEEffectSpecBuildSystem` 直接消费 ASC owner-local instant command/payload，但 spec 结果仍写 singleton `GEEffectSpecBuffer`，下游 `GASAttributeSetReduceApplySystem` 与 cue projection 仍以 singleton spec carrier 作为事实源。这会把 instant GE 的 AttributeReduce / cue fact 重新集中到单 entity DynamicBuffer，并保留 `SpecBuildCommandCursor` / `DeltaApplySpecCursor` / `CueProjectionSpecCursor` 等旧 cursor 状态。

## 本切片结果

1. `GEEffectSpecBuffer` 迁入 ASC archetype / factory / runtime completeness check。
2. `GASRuntimeEntityArchetypes.EffectCommandStream()` 与 `EffectCommandSpecStream.HasRequiredBuffers()` 不再持有或要求 `GEEffectSpecBuffer`。
3. generated `GEEffectSpecBuildSystem` 写 `SpecLookup[record.Owner]`，不再写 `SpecLookup[StreamEntity]`。
4. generated `GASAttributeSetReduceApplySystem` 改为 `IJobChunk`，直接消费 ASC-local `GEEffectSpecBuffer` / `GESetByCallerValueBuffer` / `AttributeValueBuffer` / `OwnerLocalGameplayFactBuffer`。
5. `GameplayFactProjectionSystem` 改为 ASC-local cue fact projection，并排在 `GameplayOwnerLocalFactFlushSystem` 前。
6. `SpecBuildCommandCursor` / `DeltaApplySpecCursor` / `CueProjectionSpecCursor` 已从 `GEEffectCommandStreamComponent` 删除；FramePrepare 每帧直接清空 legacy singleton command/payload。
7. `GasRuntimeDebugger` 不再采样 stream `GEEffectSpecBuffer` pressure，instant spec 数量改读 `OwnerLocalSpecCount`。

## 验证

- `powershell -ExecutionPolicy Bypass -File Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1`
- `dotnet build com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`
- `dotnet build com.exhard.exgas.editor.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`
- `dotnet build com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`

三项 build 均通过，仅保留既有 `MSB3277` 引用冲突 warning；editor build 另有既有 Unity API obsolete warning。

## 后续复发入口

若再次出现 `SpecLookup[StreamEntity]`、stream `GEEffectSpecBuffer`、`SpecBuildCommandCursor` 或 generated AttributeReduce 退回 singleton scan，应重新打开 P0-D。
