# AutoChess driver owner snapshot 切片

> 归档日期：2026-06-08
> 关联问题：R1/R6 AutoChess adapter raw ECS identity 收口
> 当前状态：adapter public raw driver `Entity` 防回流已补强；driver runtime store 内部 `_driverEntity` structural owner 仍未迁出。

## 本轮变更

1. `AutoChessBattleDriverRuntimeStore` 新增 `AutoChessBattleDriverOwnerSnapshot`，把 driver owner 是否安装、是否启用、opaque handle 是否匹配，以及 structural create / enable / disable / uninstall 次数输出为 raw-Entity-free 证据。
2. `AutoChessBattleSession` 在 `Complete()` 读取 driver stats 的同一时点读取 owner snapshot，`AutoChessBattleResult` 持有该证据，避免 session close 后 handle 失效污染验证口径。
3. `AutoChessBattleValidationReport.CreateBoundaryOwnerSummary(...)` 新增：
   - `driverOwnerInstalled`
   - `driverOwnerEnabled`
   - `driverOwnerHandleMatched`
   - `driverStructuralCreates`
   - `driverEnableRequests`
   - `driverDisableRequests`
   - `driverUninstallRequests`
4. `AutoChessGasCatalogSession` 进一步收拢 catalog install/uninstall 的 `EntityManager` capability，host 不再直接把 `EntityManager` 作为参数传入 catalog session。
5. `Verify-GAS-RuntimeCoreBoundary.ps1` 新增防回流门，要求 driver owner snapshot、session/result/validation evidence 链存在，并阻断 `TryGetEntityForAdapter` 与 public/internal static raw driver `Entity` 导出口回流。

## 验证记录

1. `.\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1`
   - 通过。
   - 新增输出：`AutoChess R6 driver owner contract passed: driver owner snapshot is exposed without raw Entity adapter APIs.`
2. `dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal`
   - 通过。
   - 保留 8 个既有 `MSB3277` warning。

## 本轮不能推出的结论

1. 未跑 Unity headless AutoChess x50/x100/x1000，因此不能把本轮写成新业务验证通过。
2. 未跑 Unity Test Runner。
3. 未启用 Profiler capture。
4. driver lifecycle 仍未迁入 structural commit owner；当前只是 adapter raw `Entity` 防回流和 owner evidence 收口。

## 复发入口

如果后续重新出现 public raw driver `Entity` resolver、`TryGetEntityForAdapter`、validation summary 丢失 driver owner snapshot，或 host/catalog/lifecycle 再次扩散 `EntityManager` capability，应从 R1/R6 AutoChess adapter 边界重新打开。
