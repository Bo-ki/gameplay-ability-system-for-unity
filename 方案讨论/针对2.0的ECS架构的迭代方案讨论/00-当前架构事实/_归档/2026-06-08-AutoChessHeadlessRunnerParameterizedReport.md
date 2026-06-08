# AutoChess 无头 Runner 参数化报告归档（2026-06-08）

## 本轮变更

- `Assets/AutoChessDemo/AutoRunner/AutoChessRuntimeRunner.cs` 支持命令行覆盖验证场景参数：
  - `-autoChessScale`
  - `-autoChessMaxTicks`
  - `-autoChessPostVictoryFlushTicks`
  - `-autoChessProcessWarmupRuns`
  - `-autoChessHealthMultiplier`
  - `-autoChessMaxPresentationLines`
  - `-autoChessReportPath`
- BatchMode 默认输出机器可读摘要：
  - `TestResults/AutoChess/Headless/AutoChessHeadlessValidationSummary.txt`
- 报告聚合无头验收所需的关键段落：
  - `AutoChessDemoHeadlessRunnerPerformance`
  - `AutoChessDemoHeadlessValidationRunResult`
  - `AutoChessDemoHeadlessRepeatRunEvidence`
  - `AutoChessDemoHeadlessRuntimeHotspots`
  - `AutoChessDemoHeadlessBoundaryOwners`
  - `AutoChessDemoHeadlessBoundaryReportKeys`
  - `AutoChessDemoHeadlessBattlePresentation`
  - `AutoChessDemoHeadlessRuntimeDebugger`
  - `AutoChessDemoHeadlessRuntimeTiming`
  - `AutoChessDemoHeadlessOfficialToolDiff`
  - `AutoChessDemoHeadlessRuntimeBattleLog`
- Player 启动参数 `-gasAutoChessDemo` 在验证失败时会抛出 `InvalidOperationException`，避免无头 player 链路静默吞掉失败。

## 验证证据

### dotnet 局部编译

命令：

```powershell
dotnet build .\com.exhard.exgas.autochessdemo.csproj -m:1 -p:UseSharedCompilation=false --no-restore --nologo --verbosity:minimal
```

结果：通过，0 error；仍存在既有 `MSB3277` 引用冲突 warning。

### Unity BatchMode x1 参数覆盖

关键参数：

```text
-autoChessScale 1
-autoChessMaxPresentationLines 40
-autoChessReportPath TestResults\AutoChess\Headless\AutoChessHeadlessValidationSummary-X1.txt
```

报告结论：

- `passed=True`
- `thresholdsPassed=True`
- `runtimeChainPassed=True`
- `repeatRunPassed=True`
- `scale=1`
- `commands=21`
- `attributeChanges=18`
- `executionOutputs=7`
- `cueRequests=32`
- `journalingCaptured=True`
- `avgTickMs=0.779`

### Unity BatchMode x50 Run2

关键参数：

```text
-autoChessMaxPresentationLines 80
-autoChessReportPath TestResults\AutoChess\Headless\AutoChessHeadlessValidationSummary-X50-Run2.txt
```

报告结论：

- `passed=True`
- `thresholdsPassed=True`
- `runtimeChainPassed=True`
- `repeatRunPassed=True`
- `scale=50`
- `units=200`
- `commands=1050`
- `attributeChanges=900`
- `periodTickDamageFacts=150`
- `executionOutputs=350`
- `cueRequests=1600`
- `coreFacts=5200`
- `blockingDebugErrors=0`
- `journalingCaptured=True`
- `GASTickTotal avgMs=1.200`
- `CoreRuntimeOwner avgMs=0.773`
- `BoundaryOwner avgMs=0.381`

Run2 日志复核未发现 `error CS` 或 `Script Compilation Error`。

### 未确认项

- `AIBridgeCLI compile unity --timeout 120000 --pretty` 本轮超时，状态未确认，不能记为通过。
- 一次中间 Unity BatchMode 过程出现过 `AutoChessGasRuntimeAccess.cs` 中 `Predicate<>` 相关脚本编译错误；当前磁盘文件已有 `using System;`，并且 x50 Run2 未复现。该现象按 Unity 刷新中间态污染记录，不能当作最终失败证据，也不能从过程记录中删除。

## 当前结论

本轮把 AutoChess 无头验收从固定 x50 场景推进到可参数化、可落盘、可被 CI/批处理读取的证据链。x1 与 x50 Run2 已证明当前 runner 能覆盖小规模参数验证和默认大规模业务链路，并能输出性能、边界 owner、repeat run、journaling 与 battle log 证据。

仍未达到 Goal 停止条件。缺口如下：

- 未完成 x100、x1000 或更高规模压力验证。
- 未完成 player 构建口径、SceneRuntime 口径与 Profiler 开启口径的独立验收。
- 当前性能只能说明 x50 口径下表现良好，尚不能声明“性能指标优秀”。
- `AutoChessGasRuntimeAccess.cs` 仍有并行脏改与 runtime access contract 指标扩展，需要后续任务单独审查和归档。
