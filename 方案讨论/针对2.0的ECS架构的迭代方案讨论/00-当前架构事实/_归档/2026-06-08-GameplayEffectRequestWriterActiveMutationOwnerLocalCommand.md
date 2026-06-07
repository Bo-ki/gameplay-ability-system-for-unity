# GameplayEffectRequestWriter ActiveMutation Owner-Local Command

## 原问题

`GameplayEffectRequestWriter` 是 runtime boundary / ability runtime helper 进入 GE apply 主链的公共写入口。旧实现对 `GEEffectCommandKind.ActiveMutation` 与 instant command 使用同一路径：先解析 singleton `GEEffectCommandStreamComponent` owner，再通过 `EffectCommandSpecStream.BeginCommandWriter(...)` 写入 singleton `GEEffectCommandBuffer` / `GESetByCallerValueBuffer`。

这会让 runtime boundary 产生的 duration/active GE 仍制造 singleton command stream 首跳压力，即使后续 normalize/apply 阶段已经有 ASC owner-local active mutation command/payload lane。

## 本切片改动

1. `TryAppendSimpleInstantCommand(...)` 两个 set-by-caller overload 统一经过 `AppendPreparedCommand(...)` 分流。
2. `TryAppendSimpleInstantCommands(...)` 多目标路径先校验所有目标 ASC 是否具备 owner-local active mutation command/payload buffer，再逐目标 append。
3. active mutation route 仍借 `EffectCommandSpecStream.CommandWriter` 持有 `GEEffectCommandStreamComponent` 并分配 command sequence / context / frame，但 command 本体不写 singleton `_commands`。
4. `CommandWriter.AppendOwnerLocalActiveMutationCommand(...)` 把 command 写目标 ASC `ActiveEffectMutationCommandBuffer`。
5. request set-by-caller payload 复制到目标 ASC 的 `ActiveEffectMutationSetByCallerValueBuffer`，并把 owner-local payload range 写回 command 的 `SetByCallerStart` / `SetByCallerCount`。
6. instant command 仍保留原 `writer.AppendCommand(...)` 路径，由 `GEEffectSpecBuildSystem` 消费。
7. `Verify-GAS-RuntimeCoreBoundary.ps1` 新增 writer active mutation owner-local route 防回流断言。

## 证据

- `Assets/GAS/Runtime/System/Effect/GameplayEffectRequestWriter.cs`
- `Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs`
- `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1`

## 验证

- `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过。
- `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过，仅保留既有 `MSB3277` warning。
- `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过，仅保留既有 `MSB3277` warning。

## 剩余范围

本切片只迁移 `GameplayEffectRequestWriter` 的 active mutation branch。以下范围仍属于 P0-D 后续：

- `GameplayEffectRequestWriter` 的 instant route 仍写 singleton command/set-by-caller stream。
- overflow 派生命令仍写 singleton command stream。
- period instant 派生命令仍写 singleton command stream。
- instant spec build 仍读取 singleton `GEEffectCommandBuffer` / `GESetByCallerValueBuffer` / `GEEffectSpecBuffer`。
- 非 active mutation set-by-caller payload 仍是 command/spec stream payload。

## 复发入口

如果 `GameplayEffectRequestWriter` 对 active mutation command 再次无条件执行 `writer.AppendCommand(...)`，或 active mutation set-by-caller payload 再次写入 singleton `GESetByCallerValueBuffer`，应重新打开 P0-D。
