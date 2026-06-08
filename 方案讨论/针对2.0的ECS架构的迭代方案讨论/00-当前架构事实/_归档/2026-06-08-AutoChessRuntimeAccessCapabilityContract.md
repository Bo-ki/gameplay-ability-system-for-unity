# AutoChess RuntimeAccess Capability Contract 归档

> 日期：2026-06-08
> 切片：R4/R6 RuntimeAccess direct ECS capability 机器证据
> 代码范围：`Assets/AutoChessDemo/Integration/GasCore/AutoChessGasRuntimeAccess.cs`、`Assets/AutoChessDemo/Battle/AutoChessBattleContracts.cs`、`Assets/AutoChessDemo/Battle/Validation/AutoChessBattleValidationReport.cs`

## 结论

本轮没有把 `Integration/GasCore` 写成目标态 Thin Adapter，也没有删除 direct ECS capability。实际完成的是把 `AutoChessGasRuntimeAccess` 从“集中调用 wrapper”升级为“可机读 capability / owner contract”：validation evidence 和 boundary owner summary 可以稳定输出 adapter 当前仍代理哪些 ECS handle、哪些入口有 manual sync、哪些入口影响 battle hash，以及后续应迁往哪个 owner。

这对 R4/R6 的价值是让 direct ECS 风险不再只靠人工读代码追踪；后续瘦身可以直接以计数字段下降为验收门。

## 机器字段

| 字段 | 当前值 | 含义 |
|---|---:|---|
| `runtimeAccessContractEntries` | 15 | `AutoChessGasRuntimeAccess` 当前登记的 ECS capability 入口数 |
| `runtimeAccessEcsHandleProxies` | 15 | 仍代理 `World` / `EntityManager` / runtime singleton / command port / job drain 的入口数 |
| `runtimeAccessManualSync` | 1 | 仍显式 drain runtime jobs 的入口数 |
| `runtimeAccessPerformancePassRisks` | 15 | 不允许进入 performance pass 或带 manual sync 的入口数 |
| `runtimeAccessBattleHashAffecting` | 4 | 会影响 battle hash / gameplay authority 的入口数 |
| `runtimeAccessCapabilityMask` | `0x3F` | 已覆盖 `RuntimeSession`、`DefinitionCatalogLifetime`、`CommandPort`、`DiagnosticsSink`、`RunnerSync`、`DriverLifecycle` 六类 capability |

## Capability 矩阵口径

| capability | 代表入口 | 当前 owner 候选 | 退出任务 |
|---|---|---|---|
| `RuntimeSession` | runtime system registration、tick group lookup | `RuntimeSession` | R1/R4 |
| `DefinitionCatalogLifetime` | generated catalog install / uninstall | `DefinitionCatalogLifetime` | R5/R6 |
| `CommandPort` | battle unit command port | `CommandPort` | R1/R6 |
| `DiagnosticsSink` | official diff、global timer、event bus、event log sink、runtime debugger | `DiagnosticsSink` | R4/R6 |
| `RunnerSync` | runtime job drain | `RunnerSync` | R4/R6 |
| `DriverLifecycle` | driver create/read/snapshot/disable | `BoundaryStructuralOwner` / `SnapshotReadModel` / `DiagnosticsSink` | R6 |

## 已改动事实

1. `AutoChessGasRuntimeAccessContract` 在 runtime access 文件内登记完整矩阵，并提供 `EntryCount`、`EcsHandleProxyCount`、`ManualSyncCount`、`PerformancePassRiskCount`、`BattleHashAffectingCount`、`CapabilityMask` 和完整 `CreateSummary()`。
2. `AutoChessValidationEvidence` 增加 runtime access contract 计数字段。
3. `AutoChessBattleValidationReport.CreateEvidence(...)`、`CreateSummary(...)`、`CreateBoundaryOwnerSummary(...)` 输出 compact 机器字段。
4. `AutoChessBattleValidationReport.CreateRuntimeAccessContractSummary()` 保留完整矩阵导出口，供 runner / debugger 后续接入。

## 不能推出的结论

1. 不能写成 Thin Adapter 完成。当前 15 个 entry 全部仍代理 ECS handle。
2. 不能写成 performance pass 已安全。`runtimeAccessPerformancePassRisks=15` 说明这些入口仍需从 hot path / measurement pass 中剥离或单列。
3. 不能写成 RunnerSync 已退出。`runtimeAccessManualSync=1` 对应 `TryDrainRunnerJobs()`，仍应独立计入 runner / debugger sync cost。
4. 不能写成 DefinitionCatalogLifetime 已完成。catalog install / uninstall 已集中，但 runtime-created Blob / entity dispose owner 仍需最终 owner 化。
5. 不能写成 driver lifecycle 已完成。public handle 已 opaque，但 driver runtime store 内部 `_driverEntity` 仍需 structural owner 或 bootstrap-only 证明。

## 后续验收建议

1. 每轮 RuntimeAccess 瘦身都必须同步更新 contract，并让 `runtimeAccessEcsHandleProxies`、`runtimeAccessManualSync`、`runtimeAccessPerformancePassRisks` 或 `runtimeAccessBattleHashAffecting` 至少一个指标下降。
2. 新增 adapter capability 时必须先进入 contract，不能绕过 validation evidence。
3. 后续 runner 可把 `CreateRuntimeAccessContractSummary()` 打入单独日志行，但该日志行必须保持 diagnostics 口径，不应作为 gameplay authority 输入。
