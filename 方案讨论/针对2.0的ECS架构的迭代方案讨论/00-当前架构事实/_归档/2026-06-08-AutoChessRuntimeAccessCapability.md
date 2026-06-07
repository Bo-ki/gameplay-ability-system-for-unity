# AutoChess runtime access capability 归档

## 原问题

R1/R6：AutoChess adapter 多处直接调用 `GASRuntimeShell.TryResolve*`、`TryCreate*`、`TryDrain*`，业务模块需要知道 raw ECS runtime seam 的存在。这样会让 `GASRuntimeShell` 多能力 facade 的复杂度扩散到 runtime host、catalog session、battle lifecycle 和 observation gateway，后续削 Shell 时需要多点修改。

## 归档日期

2026-06-08

## 本轮改动

1. 新增 `AutoChessGasRuntimeAccess`，作为 AutoChess adapter 内部唯一 raw GAS runtime access capability owner。
2. Runtime host 只通过 `TryResolveSessionWorld` 取得 session world，并通过 `TryDrainRunnerJobs` drain runtime jobs。
3. Catalog session 只通过 `TryResolveDefinitionEntityManager` 安装/卸载 generated catalog 与 driver runtime store。
4. Battle entity lifecycle 只通过 `TryResolveBattleLifecycleEntityManager` 与 `TryCreateBattleUnitCommandPort` 处理单位/driver 生命周期。
5. Observation gateway 只通过 diagnostics capability 方法访问 GlobalTimer、EventBus、EventLogSink 和 RuntimeDebugger。
6. `Verify-GAS-RuntimeCoreBoundary.ps1` 增加防回流门：上述四个消费者不得再直接调用 `GASRuntimeShell.Try*`。

## 不能推出的结论

1. 这不是彻底删除 `GASRuntimeShell`；本轮只是把 AutoChess adapter 的 raw ECS seam 认知集中到一个 owner。
2. 这不是 Editor 链路治理；Editor watcher 仍在本轮范围外。
3. 这不代表无头 AutoChess 真实业务 x100/x1000 全链路验收完成；本轮只完成编译与静态契约验证。

## 验证证据

1. `git diff --check` 通过，仅保留既有 LF/CRLF warning。
2. `powershell -ExecutionPolicy Bypass -File Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过。
3. `dotnet build com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` 通过，仅保留既有 `MSB3277` warning。
4. `dotnet build com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` 通过，仅保留既有 `MSB3277` warning。

## 复发入口

如果 `AutoChessGasRuntimeHost`、`AutoChessGasCatalogSession`、`AutoChessGasBattleEntityLifecycle` 或 `AutoChessGasObservationGateway` 重新直接调用 `GASRuntimeShell.Try*`，重新打开 R1/R6 Shell capability 收权任务。
