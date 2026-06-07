# Ability Commit / Period ActiveMutation Owner-Local Command

## 原问题

P0-D `singleton stream owner` 中，ability commit 产生的 GameplayEffect command 无论 instant 还是 active mutation，都先写入 singleton `GEEffectCommandBuffer`。虽然 normalize 阶段已会把 active mutation 投影到 ASC owner-local command buffer，但真实业务里 ability primary / cooldown / cost 等 active GE 仍先制造一次 singleton command stream 压力。

同一轮审查发现 `GEActiveEffectPreTickJob` 仍通过 `MutationLookup.HasBuffer(StreamEntity)` / `MutationLookup[StreamEntity]` 访问 `ActiveEffectMutationBuffer`。该 buffer 已从 `EffectCommandStream` archetype 迁出，继续读 stream owner 会让 pre-tick / remove mutation output 保留旧实现风险。

## 本切片改动

1. `AbilityCatalogCommitSystem` 新增 `ActiveMutationCommandLookup` 与 `ActiveMutationSetByCallerLookup`。
2. `AppendEffectCommand(...)` 对 `GEEffectCommandKind.ActiveMutation` 先确认目标 ASC 拥有 owner-local command/payload buffer，再分配 sequence/context 并写入目标 ASC 的 `ActiveEffectMutationCommandBuffer`。
3. instant command 仍写 singleton `GEEffectCommandBuffer`，继续由 `GEEffectSpecBuildSystem` 生成 `GEEffectSpecBuffer`。
4. `GEActiveEffectPreTickJob` 改为通过 chunk `BufferTypeHandle<ActiveEffectMutationBuffer>` 获取当前 ASC owner-local mutation buffer，pre-tick period tick / explicit remove 产生的 mutation 不再访问 stream owner。
5. `GEActiveEffectPreTickJob.EmitPeriodCommand(...)` 对 active period GE 写目标 ASC 的 `ActiveEffectMutationCommandBuffer`，并把 active effect snapshot set-by-caller 复制到 `ActiveEffectMutationSetByCallerValueBuffer`。
6. `GasGlueCodeGenPhases.cs` 同步生成模板，避免 generated output 回流。
7. `Verify-GAS-RuntimeCoreBoundary.ps1` 新增 ability commit / pre-tick mutation output / period active mutation owner-local route 防回流断言。

## 证据

- `Assets/GAS/Generated/CodeGen/Runtime/RuntimeAbilityActivation.gen.cs`
- `Assets/GAS/Generated/CodeGen/Runtime/ActiveEffectLifecycleOwnerSystems.cs`
- `Assets/GAS/Generated/CodeGen/Runtime/RuntimeActiveEffect.gen.cs`
- `Assets/GAS/Editor/CodeGen/Phases/GasGlueCodeGenPhases.cs`
- `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1`

## 验证

- `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过。
- `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过，仅保留既有 `MSB3277` warning。
- `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal` 通过，仅保留既有 `MSB3277` warning。

## 剩余范围

本切片移除 ability commit 与 active period GE 对 singleton command stream 的 active mutation 首跳依赖，并修复 pre-tick/remove mutation output 的旧 stream owner 访问。以下范围仍属于 P0-D 后续：

- `GameplayEffectRequestWriter` 的 runtime/simple apply producer 仍写 singleton command stream。
- overflow 派生命令 append 仍写 singleton command stream。
- period instant 派生命令仍写 singleton command stream。
- instant spec build 仍读取 singleton `GEEffectCommandBuffer` / `GESetByCallerValueBuffer` / `GEEffectSpecBuffer`。
- 非 active mutation set-by-caller payload 仍是 command/spec stream payload。

## 复发入口

如果 `AbilityCatalogCommitSystem.AppendEffectCommand(...)` 再次对 active mutation command 无条件执行 `CommandLookup[StreamEntity].Add(resolved)`，`GEActiveEffectPreTickJob` 再次出现 `MutationLookup[StreamEntity]` / `MutationLookup.HasBuffer(StreamEntity)`，或 codegen 模板丢失 owner-local active mutation route，应重新打开 P0-D。
