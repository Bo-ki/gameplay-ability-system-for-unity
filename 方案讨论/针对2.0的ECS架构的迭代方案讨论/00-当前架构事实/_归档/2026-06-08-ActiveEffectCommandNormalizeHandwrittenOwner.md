# ActiveEffect command normalize 手写 Runtime owner 归档

> 归档日期：2026-06-08
> 对应链路：ActiveEffect command catalog normalize / active mutation command fan-in
> 关联代码：`GEActiveEffectCommandNormalizeSystem.cs`、`GASRuntimeDefinitionResolver.cs`、`ActiveEffectLifecycleOwnerSystems.cs`、`GASSystemScheduleContract.cs`、`Verify-GAS-RuntimeCoreBoundary.ps1`

## 原问题

`RuntimeActiveEffect.gen.cs` 之前通过 generated runtime wrapper 持有 `GEEffectCommandCatalogNormalizeSystem`。该系统虽然属于 active-effect 链路，但它实际承担的是运行时 command normalize、duration frame 解析、instant/active-mutation lane 判定，以及 ASC owner-local active mutation command fan-in。

这类 query、job dependency、owner-local buffer 写入和业务 lane 判定不应继续由 generated runtime artifact 持有。generated runtime 可以暂存 active-effect lifecycle 迁移债务，但 command normalize owner 应先迁回手写 `GAS.Runtime`，让 `GEEffectSpecBuildSystem` 前置输入链路不再依赖 generated normalize wrapper。

## 本轮处理

1. 新增 `Assets/GAS/Runtime/System/Effect/GEActiveEffectCommandNormalizeSystem.cs`，由手写 runtime assembly 接管 `GEEffectCommandCatalogNormalizeSystem`。
2. 新 owner 在 `GASCoreSimulationSystemGroup` 内声明 `UpdateBefore(typeof(GEEffectSpecBuildSystem))`，维持 Normalize -> SpecBuild 的业务顺序。
3. `GASRuntimeDefinitionResolver.TryNormalizeGameplayEffectCommand(...)` 承接 gameplay effect catalog 查找、duration frame override 解析、active mutation lane 判定和 command flag 修正。
4. 手写 normalize job 继续把 set-by-caller payload 复制到 ASC owner-local `ActiveEffectMutationCommandBuffer` / `ActiveEffectMutationSetByCallerValueBuffer`，保持后续 active mutation apply 链路输入不变。
5. `GASSystemScheduleContract` 将 `GEEffectCommandCatalogNormalizeSystem` 纳入 CoreSimulation 手写系统列表，并移除 `GAS.Runtime.Generated.GEEffectCommandCatalogNormalizeSystem` 的 generated type-name 注册。
6. generated `GASActiveEffectRemoveSystem` 增加 `UpdateBefore(typeof(GAS.Runtime.GEEffectCommandCatalogNormalizeSystem))`，维持 Remove -> Normalize -> SpecBuild 的顺序，不要求手写 runtime 反向 `typeof` generated system。
7. 诊断脚本新增 normalize owner、resolver 调用、schedule 注册和禁止 generated normalize 调度的防回流断言。

## 验证

- `dotnet run --project Tools\GasCodeGenCli\GasCodeGenCli.csproj -- core`
- `dotnet build .\com.exhard.exgas.generated.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`
- `dotnet build .\com.exhard.exgas.runtime.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`
- `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`
- `dotnet build .\com.exhard.exgas.editor.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`

上述目标 build 均通过。保留既有 `MSB3277` 引用版本冲突 warning，以及 editor 侧既有 obsolete API / `CS0414` warning。

`.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 当前不能记录为通过：脚本执行被工作树内未提交的 AutoChess capability 改动污染，失败点不属于本 normalize 切片。后续应单独收口 AutoChess runtime access capability 断言，再恢复边界脚本全量通过证据。

## 仍未完成

- generated `GEEffectCommandCatalogNormalizeSystem` 旧实现仍残留在 `RuntimeActiveEffect.gen.cs` / `ActiveEffectLifecycleOwnerSystems.cs`，但已不再由 schedule contract 注册；后续 active-effect owner cleanup 应删除该 generated normalize wrapper。
- `RuntimeActiveEffect.gen.cs` 仍是剩余 `RuntimeLifecycleMigration` artifact，下一轮应继续迁移 active-effect mutation apply / tick / remove owner。
- 本切片未运行 Unity headless AutoChess、Unity Test Runner、x100/x1000 profile，不能作为业务终局或性能终局结论。
