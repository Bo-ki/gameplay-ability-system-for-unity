# Ability / Period Instant Owner-Local Command 归档

## 背景

P0-D 的剩余 hot path 风险之一是 generated ability commit 与 active effect period 仍可把 instant `GEEffectCommandBuffer` 直接追加到 singleton `GEEffectCommandStreamComponent` owner。request instant producer 已有 ASC owner-local command/payload lane 与 `OwnerLocalInstantCommandFlushSystem`，本切片复用这条中间闭环继续收窄 producer 入口。

## 已完成

1. `AbilityCatalogCommitSystem` 的 generated ability seed：
   - `GEEffectCommandKind.ActiveMutation` 继续写目标 ASC 的 `ActiveEffectMutationCommandBuffer`；
   - `GEEffectCommandKind.Instant` 改为写目标 ASC 的 owner-local `GEEffectCommandBuffer`；
   - command sequence / context 仍由 `GEEffectCommandStreamComponent` 分配，保持与现有 spec stream consumer 的顺序语义一致。

2. `GEActiveEffectPreTickJob.EmitPeriodCommand(...)`：
   - active period GE 继续写目标 ASC owner-local active mutation command/payload；
   - instant period GE 改为写当前 ASC owner-local `GEEffectCommandBuffer`；
   - period snapshot set-by-caller 从 `ActiveGameplayEffectSetByCallerValueBuffer` remap 到当前 ASC 的 `GESetByCallerValueBuffer` range。

3. `GasGlueCodeGenPhases` 模板同步更新，避免下一次生成把 ability / period instant producer 写回 singleton stream。

4. `Verify-GAS-RuntimeCoreBoundary.ps1` 新增 period instant 防回流断言，并复用 ability instant owner-local 断言。

## 仍未完成

- `GEEffectSpecBuildSystem` 仍消费 singleton command/spec stream，本切片只是 producer 侧的 owner-local 中间闭环。
- overflow instant 派生命令仍走 proof stream；由于 `GASActiveEffectMutationApplySystem` 在 `GEEffectSpecBuildSystem` 之后运行，迁移它需要补 next-frame instant owner-local lane，不能直接写 current owner-local instant buffer。
- 非 active mutation set-by-caller 与 spec carrier 仍在 singleton carrier 上。
- 无头 AutoChess x50/x1000 尚未作为本切片性能验收运行，不能宣称全链路 scale-ready。

## 验证

本归档对应代码验证以当前提交记录为准：

- `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1`
- `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`
- `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`
