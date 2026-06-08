# 2026-06-08 R7 Frame Lane Counters Run8

> Owner：`00-当前架构事实/_归档`
> 类型：AutoChess x50 headless + Debugger frame-lane counter evidence
> 结论：通过业务链路和 strict pure logic budget；未通过 performance excellent，因为 Profiler disabled。

## 证据路径

| 类型 | 路径 |
|---|---|
| headless summary | `TestResults/AutoChess/Headless/AutoChessHeadlessValidation-DebuggerProbe-20260608-Run8-FrameLaneCounters.txt` |
| brief | `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run8-FrameLaneCounters/AutoChessProfileBrief.md` |
| performance budget | `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run8-FrameLaneCounters/SubReports/PerformanceBudget.md` |
| hotspot matrix | `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run8-FrameLaneCounters/SubReports/HotspotAttribution.md` |
| debugger evidence | `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run8-FrameLaneCounters/SubReports/DebuggerEvidence.md` |

## Run8 关键结果

| 指标 | 值 | 判定 |
|---|---:|---|
| `passed` | true | 业务链路通过 |
| `headlessLogicBudgetPassed` | true | strict pure logic budget 通过 |
| `performanceExcellentPassed` | false | Profiler disabled，不能写成 DOTS 优秀 |
| `avgTickMs` | 1.217ms | 低于 1.500ms |
| `GASTickTotal.avgMs` | 1.201ms | 低于 1.500ms |
| `GASCoreSimulationSystemGroup.avgMs` | 0.726ms | 低于 0.900ms |
| `BoundaryOwner.avgMs` | 0.273ms | 低于 0.350ms |
| `DebuggerOwner.avgMs` | 115.012ms | diagnostic-only，不并入 performance pass |

## 新增 Frame-Lane Counters

| Lane | Counter | 值 | 事实结论 |
|---|---|---:|---|
| OwnerLocalInstant prepare | scanned / skipped / dirty owners | 2400 / 1450 / 950 | 60.4% owner 被扫描后跳过 |
| OwnerLocalInstant prepare | promoted commands | 0 | 本轮热点不是 command promotion 成本 |
| ActiveMutation prepare | scanned / skipped / dirty owners | 2400 / 1750 / 650 | 72.9% owner 被扫描后跳过 |
| ActiveMutation prepare | promoted commands | 0 | 本轮热点不是 command promotion 成本 |
| ActiveEffect pre-tick | scanned / processed / skipped owners | 2400 / 950 / 1450 | pre-tick 仍有稀疏 owner scan |
| ActiveEffect pre-tick | scanned / due / noop slots | 1200 / 550 / 650 | 45.8% slot due，54.2% slot noop |
| ActiveEffect pre-tick | mutation writes | 600 | pre-tick 会派生 mutation write |

## 架构结论

1. R7/R3 热点已经从 Journaling TopN 推断升级为 Runtime-owned counter evidence。
2. 下一轮不应恢复 `OwnerLocalInstantCommandPendingComponent` / `ActiveEffectMutationPendingComponent` 这类高频 enableable marker；Run6 已证明该方向会把 buffer scan 转移为 enableable toggle、CoreSimulation 和 Job safety 回归。
3. 下一轮应该围绕 dirty owner span、due slot lane、buffer clear/copy policy、generated index 或 owner-local compact range 设计，而不是继续向 Debugger 增加人读日志。
4. 本轮只能证明 Debugger 定位能力增强，不能证明 R7/R3 数据形态优化完成。

## 验证记录

| 验证 | 结果 |
|---|---|
| `dotnet build .\com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` | 通过，仅既有 `MSB3277` |
| `dotnet build .\com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` | 通过，仅既有 `MSB3277` |
| AutoChess x50 headless Run8 | `passed=True`、`headlessLogicBudgetPassed=True`、`blockingDebugErrors=0` |
| split report | 已生成 brief + PerformanceBudget / HotspotAttribution / JournalingTopN / DebuggerEvidence / RuntimeBattleLog |

## 不能宣称

1. 不能宣称 `performanceExcellentPassed=True`，因为 `profiler disabled; Entities profiler modules collect no data`。
2. 不能宣称 R7 ActiveEffect lifecycle 或 R3 fan-in 已完成，只能宣称 counters 已能定位下一刀。
3. 不能把 `DebuggerOwner.avgMs=115.012ms` 并入 CoreSimulation；它是 diagnostic-only materialization / export 成本。
