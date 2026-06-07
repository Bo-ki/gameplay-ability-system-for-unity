# AbilityLifecycleFactOwnerLocal 事实归档

> 日期：2026-06-08
> 范围：P0-D singleton stream owner / generated ability lifecycle fact

## 原问题

generated `AbilityCatalogCommitSystem` 的 ability lifecycle fact 仍直接持有 singleton `GameplayEventBuffer` lookup，并通过 `FactLookup[StreamEntity].Add(evt)` 把 `AbilityActivated` / `AbilityEnded` / `AbilityEndRequested` 等 fact 追加到 stream owner。这会让 ability lifecycle 继续绕过 ASC owner-local fact lane，即使 Attribute / Cue fact 已经迁到 `OwnerLocalGameplayFactBuffer`。

## 本切片结果

1. generated `RuntimeAbilityActivation.gen.cs` 的 commit job 改为获取 `BufferLookup<OwnerLocalGameplayFactBuffer>`。
2. ability lifecycle fact 统一由 `EnqueueGameplayEvent(...)` 根据 `TargetAsc` / `SourceAsc` 解析 owner，并写入 `OwnerFactLookup[owner]`。
3. `AbilityEndRequested` fact 补齐 `SourceAsc` / `TargetAsc`，避免 end-request 事件因为缺少 owner 被 owner-local lane 丢弃。
4. `GasGlueCodeGenPhases.cs` 的 runtime ability activation 模板同步更新，防止下次 CodeGen 重新生成 singleton fact append。
5. `Verify-GAS-RuntimeCoreBoundary.ps1` 新增防回流断言，要求 generated ability lifecycle fact 使用 `OwnerLocalGameplayFactBuffer`，并禁止 `FactLookup[StreamEntity].Add(evt)` 回归。

## 验证

- `powershell -ExecutionPolicy Bypass -File Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1`
- `dotnet build com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`
- `dotnet build com.exhard.exgas.editor.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`
- `dotnet build com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal`

三项 build 均通过，仅保留既有 `MSB3277` 引用冲突 warning；editor build 另有既有 Unity API obsolete warning。

## 后续复发入口

若 generated ability activation 再次出现 `FactLookup = SystemAPI.GetBufferLookup<GameplayEventBuffer>()`、`FactLookup[StreamEntity].Add(evt)`，或 lifecycle fact 缺少 ASC owner，应重新打开 P0-D。
