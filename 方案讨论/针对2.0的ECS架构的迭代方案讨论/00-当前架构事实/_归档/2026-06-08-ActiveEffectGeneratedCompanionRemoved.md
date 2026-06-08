# ActiveEffect generated companion 物理退场

> 日期：2026-06-08
> 范围：`Assets/GAS/Generated/CodeGen/Runtime/ActiveEffectLifecycleOwnerSystems.cs`

## 本轮结论

`ActiveEffectLifecycleOwnerSystems.cs` 已不再是 SourceGenerator 输出、manifest artifact 或 Runtime 主调度 owner。ActiveEffect lifecycle 的实际 runtime owner 已转移到手写 Runtime Core：

- `Assets/GAS/Runtime/System/Effect/GEActiveEffectCommandNormalizeSystem.cs`
- `Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs`
- `Assets/GAS/Runtime/System/Effect/GEActiveEffectLifecycleSystems.cs`

因此 generated runtime 物理 asmdef 内保留的 companion wrapper 没有继续存在价值，只会扩大编译面、重复 system 名称认知和防回流风险。本轮将该文件及 `.meta` 物理删除。

## 本轮改动

1. 删除 `Assets/GAS/Generated/CodeGen/Runtime/ActiveEffectLifecycleOwnerSystems.cs`。
2. 删除 `Assets/GAS/Generated/CodeGen/Runtime/ActiveEffectLifecycleOwnerSystems.cs.meta`。
3. `Verify-GAS-RuntimeCoreBoundary.ps1` 增加 `Assert-FileNotExists`，阻断该 generated companion 文件和 `.meta` 回流。

## 不能推出的结论

1. 这不代表 SourceGenerator 链路已完成目标态；当前仍需继续对账 `RuntimePureGlue` marker、manifest、validation report、generated definition glue 和手写 runtime owner。
2. 这不代表 active-effect store 的 capacity / spill / scale profile 已完成。
3. 这不代表 x100/x1000 无头 AutoChess、Profiler enabled 或 Luban / SourceGenerator 全流程已经完成。

## 验收要求

1. `rg "ActiveEffectLifecycleOwnerSystems" Assets/GAS` 不应命中可执行代码文件。
2. `Verify-GAS-RuntimeCoreBoundary.ps1` 必须在文件物理不存在时通过，并在该文件回流时失败。
3. `com.exhard.exgas.generated.runtime.csproj` 的本地编译不能再依赖该删除文件。

## 复发入口

如果 SourceGenerator、manifest、validation report 或 generated runtime asmdef 重新出现 `ActiveEffectLifecycleOwnerSystems.cs`，重新打开 R5 Generated companion 清理任务。
