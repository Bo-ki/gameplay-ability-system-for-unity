# ISSUE-006 AutoChessDemo 边界混入 Runtime Core

> 最近复核：2026-06-08 | 状态：Mitigated / Evidence Hardened | 严重度：P1

## 当前结论

旧问题已明显缓解。AutoChessDemo 当前位于 `Assets/AutoChessDemo`，业务层、Battle Flow、Battle Report、GAS adapter、Demo ECS 扩展、Presentation 已拆开；Runtime Core 不再承载旧 Headless 业务系统。

当前剩余风险不是“Demo 混入 Runtime Core”，而是 `Integration/GasCore` 虽已把业务入口收敛到 `AutoChessBattleRuntime` Flow 入口和 `AutoChessGasCoreBridge` Session facade，但真实 direct EM / raw identity 能力仍分布在 `AutoChessGasRuntimeAccess` 及其下游 `AutoChessGasRuntimeHost`、`AutoChessGasCatalogSession`、`AutoChessGasObservationGateway`、`AutoChessGasBattleEntityLifecycle`、`AutoChessGasRuntimeTicker` 和 `AutoChessGasCoreContracts`；bootstrap、catalog、unit attach、driver lifecycle、observation、diagnostics、tick group、job drain、driver runtime store 内部 `_driverEntity` owner、registry command compatibility 仍通过 internal Shell capability 或内部 `ASCHandle` 取得 live ECS 句柄。`AutoChessGasRuntimeAccessContract` 当前已把这些 capability 输出成可机读矩阵：`runtimeAccessContractEntries=15`、`runtimeAccessEcsHandleProxies=15`、`runtimeAccessManualSync=1`、`runtimeAccessPerformancePassRisks=15`、`runtimeAccessBattleHashAffecting=4`、`runtimeAccessCapabilityMask=0x3F`。这让 wrapper 从“集中化入口”升级为“可验收 direct ECS owner map”，但不等于 Thin Adapter 已完成。`AutoChessGasBattleReportFactProjector` 与 unit result snapshot 当前已消费 structured log 的 `SourceReportKey` / `TargetReportKey`，driver public handle 也已收缩为 opaque `driverId/version`，不再是 raw ASC matcher / live ASC read / public raw driver handle 风险；后续应把 report key coverage、internal `_driverEntity` owner、lifecycle registry 和 snapshot owner 分别收口。

## 已缓解部分

1. `GameRoom` / `Battle` / `Battle/Flow` / `Battle/Report` / `Integration/GasCore` / `AutoRunner` / `Presentation` 分层成立。
2. Demo ECS systems 通过 `AutoChessRuntimeSystemBootstrap` 插入当前 GAS groups。
3. batchmode 与 scene stepped 模式复用 `AutoChessBattleFlow`，`AutoChessBattleManager` 不再重复维护 tick 收口细节。
4. Battle Report 只消费 Runtime structured log / Core facts，不补写伤害或死亡结果。
5. Presentation 只消费 battle log，不回读 ECS World。
6. Runtime Core 未引用 AutoChess 业务类型。
7. `AutoChessGasRuntimeAccessContract` 已把 adapter 内 direct ECS capability 分为 `RuntimeSession`、`DefinitionCatalogLifetime`、`CommandPort`、`DiagnosticsSink`、`RunnerSync`、`DriverLifecycle` 六类，并进入 validation summary / boundary owner summary。

## 仍成立风险

1. `AutoChessGasBattleEntityLifecycle` 仍直接创建 battle driver、写 driver destroy request marker、给 ASC 附加 demo component，并维护 battle unit key -> `ASCHandle` registry；`DestroyBattleUnit()` 当前已走 ASC destroy command request，旧 direct ability/effect cleanup 口径已过期。
2. Demo catalog 已由代码安装通用 generated blob，但安装 owner 仍是 demo adapter；unit/scenario/scale/validation expectation 还不是配置驱动链。
3. Demo ECS systems 主体已迁到 scheduled job，但仍是 demo extension；不能把它当作 Runtime Core 通用高规模证明。
4. `AutoChessGasRuntimeAccessContract` 当前仍显示所有 15 个 access entry 都代理 ECS handle，其中 runner sync 仍有 1 个 manual sync，且 4 个入口影响 battle hash；这证明风险可验收，不证明风险已退出。

## 代码证据

| 事实 | 文件 |
|---|---|
| demo README 边界 | `Assets/AutoChessDemo/README.md` |
| bridge facade | `AutoChessGasCoreBridge.cs` |
| flow runtime entry | `AutoChessBattleRuntime.cs` |
| runtime host / catalog | `AutoChessGasRuntimeHost.cs`、`AutoChessGasCatalogSession.cs` |
| battle entity lifecycle | `AutoChessGasBattleEntityLifecycle.cs` |
| runtime ticker | `AutoChessGasRuntimeTicker.cs` |
| observation gateway | `AutoChessGasObservationGateway.cs` |
| runtime access contract | `AutoChessGasRuntimeAccess.cs`、`AutoChessBattleValidationReport.cs` |
| report fact projector / contracts | `AutoChessGasBattleReportFactProjector.cs`、`AutoChessGasCoreContracts.cs` |
| battle flow | `AutoChessBattleFlow.cs` |
| battle report | `AutoChessBattleReportBuilder.cs` |
| system bootstrap | `AutoChessRuntimeSystemBootstrap.cs` |
| command drive | `AutoChessBattleCommandDriveSystem.cs` |
| execution extension | `AutoChessExecuteDamageCalculationSystem.cs` |
| log-only presentation | `AutoChessDemoSceneRunner.cs` |

## 退出条件

1. bridge 内结构变化分类清晰：初始化、session lifecycle、hot path。
2. battle unit create / destroy request、driver lifecycle、battle unit registry / internal `ASCHandle` command compatibility、structured snapshot projection 和 driver runtime store 内部 `_driverEntity` owner 分别进入 request/command/snapshot/diagnostics owner，report projection 继续以 stable report key coverage 验收。
3. Demo 系统的 query/dependency/ordering 有独立审查。
4. RuntimeAccess contract 的 `ecsHandleProxies`、`manualSync`、`performancePassRisks` 和 `battleHashAffecting` 必须逐轮下降，直到 direct ECS handle 只存在于明确 owner 的 bootstrap / diagnostics / structural commit 边界内。
