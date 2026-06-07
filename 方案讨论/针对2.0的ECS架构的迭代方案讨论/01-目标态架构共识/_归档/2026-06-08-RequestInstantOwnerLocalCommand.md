# Request Instant Owner-Local Command 归档

## 归档原因

本切片把 `GameplayEffectRequestWriter` 产生的 instant command 从首跳 singleton command stream 迁到目标 ASC owner-local command / payload buffer，并补上 flush 到现有 spec stream 的确定性中间层。它不是最终 `NativeStream -> compact owner-local range -> owner-local spec consumer` 形态，而是 P0-D singleton stream owner 退出过程中的中间闭环。

## 改动事实

1. ASC runtime layout 新增 owner-local instant lane：
   - `GEEffectCommandBuffer`
   - `GESetByCallerValueBuffer`

2. `GameplayEffectRequestWriter` 的 `GEEffectCommandKind.Instant` 分支不再直接调用 `writer.AppendCommand(...)` 写 singleton stream；改为：
   - 解析目标 ASC；
   - 检查目标 ASC 的 `GEEffectCommandBuffer` / `GESetByCallerValueBuffer`；
   - 通过 `EffectCommandSpecStream.CommandWriter.AppendOwnerLocalInstantCommand(...)` 分配 sequence / context；
   - 复制 request set-by-caller payload 到目标 ASC owner-local payload range；
   - 写目标 ASC owner-local command buffer。

3. 新增 `OwnerLocalInstantCommandFramePrepareSystem`：
   - 在 FramePrepare 清空 ASC owner-local instant command / set-by-caller payload；
   - 只查询带 `ASCIdentityComponent` 的 owner，避免误清 singleton stream owner。

4. 新增 `OwnerLocalInstantCommandFlushSystem`：
   - 在 CoreSimulation 收集 ASC owner-local instant command；
   - 按 `Command.Sequence` 确定性排序，owner entity / local index 作为 tie-break；
   - 将 owner-local set-by-caller payload range 重映射到现有 singleton `GESetByCallerValueBuffer`；
   - 追加到现有 singleton `GEEffectCommandBuffer`，供当前 generated instant spec build 消费。

5. generated `GEEffectSpecBuildSystem` 与 `GasGlueCodeGenPhases` 模板增加：
   - `[UpdateAfter(typeof(OwnerLocalInstantCommandFlushSystem))]`

6. 同批补齐 active mutation overflow next-frame lane：
   - `ActiveEffectNextFrameMutationCommandBuffer`
   - `ActiveEffectNextFrameMutationSetByCallerValueBuffer`
   - FramePrepare 将 next-frame command / payload 晋升到 current owner-local active mutation lane。

## 仍未完成

- `GEEffectSpecBuildSystem` 仍读取 singleton command / set-by-caller / spec stream。
- ability / period / overflow 的 instant producer 尚未全部迁出 singleton command stream。
- overflow instant 派生命令仍走 proof stream。
- 非 active mutation set-by-caller 与 spec carrier 仍在 singleton owner 上。
- 不能把本切片写成 instant 全链路 scale-ready；还需要后续迁 consumer 和剩余 producer，并跑无头 AutoChess x50 / x1000 业务验证。

## 验证

- `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1`
- `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`
- `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`

验证结果：全部通过；仅保留既有 `MSB3277` assembly version conflict warning。
