# InstantSpecBuild OwnerLocal Consumer 归档

> 归档日期：2026-06-08
> 对应 P0：P0-D singleton stream owner
> 关联代码：`RuntimeEffectInstant.gen.cs`、`GEEffectCommandSpecStreamPhases.cs`、`GASSystemScheduleContract.cs`、`GasGlueCodeGenPhases.cs`、`Verify-GAS-RuntimeCoreBoundary.ps1`

## 原问题

上一轮已经让 request / ability / period / overflow instant producer 退出首跳 singleton command append，但当前链路仍通过 `OwnerLocalInstantCommandFlushSystem` 把 ASC owner-local instant command 和 set-by-caller payload 重新写回 singleton `GEEffectCommandBuffer` / `GESetByCallerValueBuffer`，再由 `GEEffectSpecBuildSystem` 使用 `SpecBuildCommandCursor` 扫 singleton command stream。

这条中间链路只适合迁移期闭环，不适合作为目标态 hot path：数据已经进入 ASC owner-local lane，却又被 flush 回 global DynamicBuffer，既放大 stream owner 压力，也让后续 spec carrier 迁移的责任边界不清晰。

## 本轮处理

1. 删除 `OwnerLocalInstantCommandFlushSystem`，并从 `GASSystemScheduleContract` 的 phase/system 列表和 EffectCommandSpecStream target 列表移除。
2. `GEEffectSpecBuildSystem` 改为 `UpdateAfter(typeof(GEEffectCommandCatalogNormalizeSystem))`，直接消费 ASC owner-local `GEEffectCommandBuffer` / `GESetByCallerValueBuffer`。
3. 新的 generated spec build 分两段：
   - `CollectOwnerLocalInstantSpecCommandsJob : IJobChunk` 收集 owner-local instant command 和 payload。
   - `BuildOwnerLocalInstantSpecsJob : IJob` 按 command sequence + owner + local index 排序，分配 `NextSpecSequence` 并写 `GEEffectSpecBuffer`。
4. set-by-caller payload 不再作为 command flush 写回 stream；spec build 内部把 owner-local payload range 重映射为 spec-local payload range，暂存到 stream `GESetByCallerValueBuffer` 供下游 attribute reduce 读取。
5. `GEEffectCommandSpecStream.PrepareFrameLocalData(...)` 在 singleton command buffer 为空时直接清理 stream `GESetByCallerValueBuffer`，避免 spec-local payload 因不再经过 command compaction 而跨帧残留。
6. `GasGlueCodeGenPhases.cs` 同步生成模板；诊断脚本新增防回流，阻断旧 flush system、`SpecBuildCommandCursor` 和 generated spec build 读取 singleton `GEEffectCommandBuffer`。

## 验证

- `powershell -ExecutionPolicy Bypass -File Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1`
- `git diff --check -- Assets\GAS\Generated\CodeGen\Runtime\RuntimeEffectInstant.gen.cs Assets\GAS\Runtime\Effect\Component\Dynamic\GEEffectCommandSpecStream.cs Assets\GAS\Runtime\System\Effect\GEEffectCommandSpecStreamPhases.cs Assets\GAS\Runtime\System\SystemGroup\GASSystemScheduleContract.cs Assets\GAS\Editor\CodeGen\Phases\GasGlueCodeGenPhases.cs Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1`
- `dotnet build com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`
- `dotnet build com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`

说明：runtime 与 AutoChess demo 构建需顺序执行；并行执行会因为共享 `obj\Debug\com.exhard.exgas.runtime.dll` 触发已知 `CS2012` 文件占用。顺序构建通过，仅保留既有 `MSB3277` 引用版本冲突 warning。

## 仍未退出的风险

本轮只迁走 instant spec build 的 command 输入和旧 flush 中间层。`GEEffectSpecBuffer` 仍是 singleton spec carrier，spec-local set-by-caller payload 仍暂存在 stream `GESetByCallerValueBuffer`。下一轮 P0-D 应继续迁 spec carrier / spec payload，并用无头 AutoChess x50/x1000 证明不存在 global buffer pressure 和 merge ordering 风险。

## 复发入口

若后续重新出现以下模式，应重新打开 P0-D：

- `OwnerLocalInstantCommandFlushSystem`
- generated `GEEffectSpecBuildSystem` 读取 singleton `GEEffectCommandBuffer`
- generated `GEEffectSpecBuildSystem` 依赖 `SpecBuildCommandCursor`
- CodeGen 模板重新输出上述任一模式
