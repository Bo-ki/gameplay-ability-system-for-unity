# AutoChess runtime access capability 归档

## 原问题

R1/R6：AutoChess adapter 多处直接调用 `GASRuntimeShell.TryResolve*`、`TryCreate*`、`TryDrain*`，业务模块需要知道 raw ECS runtime seam 的存在。这样会让 `GASRuntimeShell` 多能力 facade 的复杂度扩散到 runtime host、catalog session、battle lifecycle 和 observation gateway，后续削 Shell 时需要多点修改。

## 归档日期

2026-06-08

## 本轮改动

1. `AutoChessGasRuntimeAccess` 继续作为 AutoChess adapter 内部唯一 raw GAS runtime access capability owner，但不再向 host / catalog / lifecycle / observation gateway 暴露 shared raw `World` / `EntityManager` resolver。
2. Runtime host 只调用 `TryRegisterRuntimeSystems()`、`TryCreateRuntimeTickGroups()` 与 `TryDrainRunnerJobs()`，不再取得 session world。
3. Catalog session 只通过 `TryInstallDefinitionCatalogSession()` / `UninstallDefinitionCatalogSession()` 安装和卸载 generated catalog 与 driver runtime store，不再取得 definition `EntityManager`。
4. Battle entity lifecycle 只通过 `TryCreateBattleDriver()`、`ReadBattleDriver()`、`CreateBattleDriverOwnerSnapshot()`、`DisableBattleDriver()` 与 battle unit command port capability 处理 driver / unit 生命周期，不再取得 lifecycle `EntityManager`。
5. Observation gateway 只通过 diagnostics capability 和 `TryBeginOfficialToolDiffCapture()` 访问 GlobalTimer、EventBus、EventLogSink、RuntimeDebugger 与 official diff capture，不再取得 diagnostics world。
6. `Verify-GAS-RuntimeCoreBoundary.ps1` 增加防回流门：禁止 AutoChess runtime access 重新公开 `TryResolveSessionWorld`、`TryResolveDefinitionEntityManager`、`TryResolveBattleLifecycleEntityManager`、`TryResolveDiagnosticsWorld`，并要求上述 concrete capability 存在。

## 不能推出的结论

1. 这不是彻底删除 `GASRuntimeShell`；本轮只是把 AutoChess adapter 的 raw ECS seam 认知集中到一个 owner。
2. 这不是 Editor 链路治理；Editor watcher 仍在本轮范围外。
3. 这不代表无头 AutoChess 真实业务 x100/x1000 全链路验收完成；本轮新增 x50 全链路证据，但仍不能写成 x100/x1000、Profiler enabled、Luban / SourceGenerator 过程门或 DOTS 性能优秀已经完成。
4. 这不代表 driver lifecycle 已进入最终 StructuralCommit owner；当前 `AutoChessBattleDriverRuntimeStore` 仍是 adapter implementation 内部 owner。

## 验证证据

1. `powershell -ExecutionPolicy Bypass -File Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` 通过。
2. `dotnet build com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` 通过，仅保留既有 `MSB3277` warning；本地 Unity 生成的 runtime csproj 需要包含新增手写 runtime 文件。
3. `dotnet build com.exhard.exgas.generated.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` 通过，仅保留既有 `MSB3277` warning。
4. `dotnet build com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` 通过，仅保留既有 `MSB3277` warning。
5. `dotnet build com.exhard.exgas.autochessdemo.editor.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` 通过，仅保留既有 `MSB3277` warning。
6. Unity batchmode `-runAutoChessValidation` x50 通过，日志 `Temp/AutoChessBattleValidation-RuntimeAccessCapability-Final.log`：`AutoChessDemoValidationRunResult: passed=True, thresholdsPassed=True, runtimeChainPassed=True, repeatRunPassed=True`；`AutoChessDemoBoundaryOwners: shellPublicRawEcsSurface=false, driverAdapterRawEntity=false, passed=True, entries=5200, missingSourceReportKeys=0, missingTargetReportKeys=0`；错误扫描未命中 Burst / BC10 / compiler errors / exception 模式。

## 复发入口

如果 `AutoChessGasRuntimeHost`、`AutoChessGasCatalogSession`、`AutoChessGasBattleEntityLifecycle`、`AutoChessGasRuntimeTicker` 或 `AutoChessGasObservationGateway` 重新直接调用 `GASRuntimeShell.Try*`，或 `AutoChessGasRuntimeAccess` 重新公开 shared raw `World` / `EntityManager` resolver，重新打开 R1/R6 Shell capability 收权任务。
