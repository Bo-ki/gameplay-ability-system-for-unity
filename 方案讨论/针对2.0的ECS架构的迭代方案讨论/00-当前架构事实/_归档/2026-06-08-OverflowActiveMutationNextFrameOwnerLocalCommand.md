# Overflow ActiveMutation Next-Frame Owner-Local Command 归档

归档日期：2026-06-08

## 原问题

`GASActiveEffectMutationApplySystem` 在 active mutation apply 阶段处理 stack overflow 时，会派生 overflow GameplayEffect command。该阶段已经晚于本帧 active mutation command collect，如果把 overflow active command 写回 current owner-local `ActiveEffectMutationCommandBuffer`，下一帧 `ActiveEffectOwnerLocalMutationFramePrepareSystem` 会清空 current buffer，命令存在丢失风险；如果继续写 singleton `GEEffectCommandBuffer`，则 active mutation command source 仍会从 singleton stream owner 回流。

## 本轮处理

1. ASC runtime archetype / factory / completeness check 新增 `ActiveEffectNextFrameMutationCommandBuffer` 与 `ActiveEffectNextFrameMutationSetByCallerValueBuffer`。
2. `ActiveEffectOwnerLocalMutationFramePrepareSystem` 在清理 current owner-local active mutation carrier 后，把 next-frame payload 先搬到 current `ActiveEffectMutationSetByCallerValueBuffer`，再搬 next-frame command 到 `ActiveEffectMutationCommandBuffer`，最后清理 deferred carrier。
3. `GASActiveEffectMutationApplySystem` 的 overflow active mutation 分支写目标 ASC 的 next-frame owner-local carrier；overflow instant 仍走现有 `EffectCommandSpecStream.AppendPreparedCommand(...)`。
4. `GASRuntimeQueryLayoutPlan` 和 `GASRuntimeStreamOwnerContract` 将 next-frame carrier 归为 ASC owner-local migration carrier，不把它标为 scale-ready 终局。
5. `GasGlueCodeGenPhases` 同步生成模板，避免只改 generated 输出。

## 证据

- `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` 通过，新增防回流门覆盖 next-frame buffer 定义、ASC archetype/factory/completeness、FramePrepare 搬运、stream owner contract、overflow active mutation 分流和 generated/template 同步。
- `git diff --check` 针对本轮相关 runtime/generated/template/diagnostic 文件通过，仅有既有 LF/CRLF warning。
- `dotnet build com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` 通过，仅有既有 `MSB3277` 引用冲突 warning。
- `dotnet build com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` 通过，仅有既有 `MSB3277` 引用冲突 warning。

## 未退出风险

- overflow instant command、period instant command、instant spec build、非 active mutation set-by-caller payload 仍在 singleton command/spec stream 上。
- next-frame carrier 只解决 post-collect active mutation producer 的 frame-boundary 正确性，不等同于 active mutation 全链路 scale-ready。
- 仍需要后续 x50/x1000 headless AutoChess profile 证明 owner-local active mutation command pressure、deferred carrier 容量和排序成本。

## 复发入口

如果 `EmitOverflowCommand(...)` 重新把 `GEEffectCommandKind.ActiveMutation` 写入 `GEEffectCommandBuffer` singleton stream，或 `ActiveEffectOwnerLocalMutationFramePrepareSystem` 不再搬运 next-frame carrier，应重新打开 P0-D：singleton stream owner 被当作多条 hot path 的事实源。
