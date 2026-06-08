# 2026-06-08 ActiveEffect NextTickFrame Skip Run9b

> Owner：`00-当前架构事实/_归档`
> 类型：AutoChess x50 headless + R7/R3 ActiveEffect pre-tick due lane evidence
> 结论：通过业务链路和 strict pure logic budget；未通过 performance excellent，因为 Profiler disabled。

## 代码改动

| 路径 | 改动 |
|---|---|
| `Assets/GAS/Runtime/Effect/Component/Dynamic/ActiveEffectStore.cs` | `ASCActiveEffectsComponent` / `ActiveEffectChunkSkipIndexSnapshot` 增加 duration due slot 与 `NextTickFrame`；新增 `ShouldProcessTickOwner(...)` 和 owner-level next tick frame 计算 |
| `Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs` | pre-tick source attribute snapshot gather 和 pre-tick owner tick 在未到 `NextTickFrame` 时跳过 owner |
| `Assets/GAS/Runtime/System/Effect/GEActiveEffectLifecycleSystems.cs` | snapshot gather job 读取 `ASCActiveEffectsComponent`，让 gather pass 与 tick pass 使用同一 skip gate |

## 证据路径

| 类型 | 路径 |
|---|---|
| headless summary | `TestResults/AutoChess/Headless/AutoChessHeadlessValidation-DebuggerProbe-20260608-Run9b-NextTickFrameSkip.txt` |
| cold polluted summary | `TestResults/AutoChess/Headless/AutoChessHeadlessValidation-DebuggerProbe-20260608-Run9-NextTickFrameSkip.txt` |
| brief | `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run9b-NextTickFrameSkip/AutoChessProfileBrief.md` |
| performance budget | `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run9b-NextTickFrameSkip/SubReports/PerformanceBudget.md` |
| hotspot matrix | `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run9b-NextTickFrameSkip/SubReports/HotspotAttribution.md` |
| debugger evidence | `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run9b-NextTickFrameSkip/SubReports/DebuggerEvidence.md` |
| Unity log | `Logs/AutoChessHeadless-Run9b-NextTickFrameSkip-ExecuteMethod.log` |

## Run9b 关键结果

| 指标 | 值 | 判定 |
|---|---:|---|
| `passed` | true | 业务链路通过 |
| `headlessLogicBudgetPassed` | true | strict pure logic budget 通过 |
| `performanceExcellentPassed` | false | Profiler disabled，不能写成 DOTS profiler-backed 优秀 |
| `avgTickMs` | 1.057ms | 低于 1.500ms |
| `GASTickTotal.avgMs` | 1.041ms | 低于 1.500ms |
| `GASCoreSimulationSystemGroup.avgMs` | 0.571ms | 低于 0.900ms |
| `BoundaryOwner.avgMs` | 0.268ms | 低于 0.350ms |
| `RunnerOwner.avgMs` | 0.065ms | 低于 0.150ms |
| `DebuggerOwner.avgMs` | 179.795ms | diagnostic-only，不并入 performance pass |

## Run8 -> Run9b Frame-Lane 对比

| Lane | Run8 | Run9b | 结论 |
|---|---:|---:|---|
| ActiveEffect pre-tick scanned owners | 2400 | 2400 | owner entity 枚举仍存在 |
| ActiveEffect pre-tick processed owners | 950 | 600 | 下降 350 |
| ActiveEffect pre-tick skipped owners | 1450 | 1800 | 上升 350 |
| ActiveEffect pre-tick scanned slots | 1200 | 750 | 下降 450 |
| ActiveEffect pre-tick due slots | 550 | 550 | 业务到期 work 未丢失 |
| ActiveEffect pre-tick noop slots | 650 | 200 | 下降 450 |
| ActiveEffect pre-tick mutation writes | 600 | 600 | mutation 输出保持一致 |
| `avgTickMs` | 1.217ms | 1.057ms | x50 strict budget 仍通过 |
| `GASCoreSimulationSystemGroup.avgMs` | 0.726ms | 0.571ms | core simulation 下降 |

## Run9 首跑降级说明

同一代码的 Run9 首跑输出 `passed=False`、`headlessLogicBudgetPassed=False`、`avgTickMs=3.650ms`。该 run 的 Unity 日志显示启动过程包含脚本编译和 domain reload；随后 Run9b 复跑在相同业务 hash / counter 口径下通过。因此 Run9 首跑只作为冷启动 / 编译污染样本保留，不能作为本切片性能回归或通过证据。

## 架构结论

1. `NextTickFrame` 是有效的 owner-local due lane：它能跳过未到期 owner 的 source snapshot gather、resource capture 和 slot scan，不需要恢复高频 enableable marker。
2. 本切片没有减少 owner chunk/entity 枚举，因此 ActiveEffect pre-tick 仍在 hotspot matrix 中；但无效 slot scan 已被压下，下一步应转向 active mutation prepare / instant prepare 的 buffer clear-copy 和 R3 fact fan-in。
3. Explicit remove command 与 cleanup record 仍必须绕过 skip gate；本轮没有改变业务完成、deterministic counts、battle hash 和 mutation write 数量。
4. `performanceExcellentPassed=False` 仍由 Profiler disabled 阻塞；Run9b 只能写成 strict headless budget pass，不能写成 DOTS profiler-backed excellent。

## 验证记录

| 验证 | 结果 |
|---|---|
| `dotnet build .\com.exhard.exgas.runtime.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` | 通过，仅既有 `MSB3277` |
| `dotnet build .\com.exhard.exgas.autochessdemo.csproj --no-restore -m:1 -p:UseSharedCompilation=false --nologo --verbosity:minimal` | 通过，仅既有 `MSB3277` |
| AutoChess x50 headless Run9b | `passed=True`、`headlessLogicBudgetPassed=True`、`runtimeChainPassed=True`、`repeatRunPassed=True`、`blockingDebugErrors=0` |
| split report | 已生成 brief + PerformanceBudget / HotspotAttribution / JournalingTopN / DebuggerEvidence / RuntimeBattleLog |

## 不能宣称

1. 不能宣称 `performanceExcellentPassed=True`，因为 `profiler disabled; Entities profiler modules collect no data`。
2. 不能宣称 R7/R3 全部完成：active mutation prepare 与 instant command prepare 仍是 High row。
3. 不能把 Run9 首跑失败写成代码回归；它是冷启动 / 编译污染样本，Run9b 才是本轮有效性能样本。
4. 不能把 `DebuggerOwner.avgMs=179.795ms` 并入 CoreSimulation；它是 diagnostic-only materialization / export 成本。
