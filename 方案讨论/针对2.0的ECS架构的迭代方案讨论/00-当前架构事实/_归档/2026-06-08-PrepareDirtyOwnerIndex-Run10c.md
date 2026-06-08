# 2026-06-08 Prepare Dirty Owner Index Run10c

> Owner：`00-当前架构事实/_归档`
> 类型：AutoChess x50 headless + R7/R3 instant / active mutation prepare dirty owner index evidence
> 结论：通过业务链路和 strict pure logic budget；prepare 稀疏 owner 扫描已下降；未通过 performance excellent，因为 Profiler disabled。

## 代码改动

| 路径 | 改动 |
|---|---|
| `Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs` | 新增 `OwnerLocalInstantPrepareDirtyOwnerBuffer` 与 `ActiveEffectMutationPrepareDirtyOwnerBuffer`；command writer 写入 owner-local instant / active mutation 时登记 dirty owner；required buffer / stream owner contract 纳入 dirty index |
| `Assets/GAS/Runtime/System/Effect/GEEffectCommandSpecStreamPhases.cs` | `OwnerLocalInstantCommandFramePrepareSystem` 与 `ActiveEffectOwnerLocalMutationFramePrepareSystem` 从全 ASC chunk 扫描改为消费 stream-owned dirty owner index，去重后按 owner `BufferLookup` 清理 / 晋升 buffer，并把需要跨帧继续处理的 owner 写回 dirty index |
| `Assets/GAS/Runtime/System/Ability/AbilityCommitSystem.cs` | ability commit producer 写 owner-local active mutation command 时登记 dirty owner |
| `Assets/GAS/Runtime/System/Effect/GEActiveEffectCommandNormalizeSystem.cs` | active effect normalize producer 写 owner-local active mutation command 时登记 dirty owner |
| `Assets/GAS/Runtime/System/Effect/GASActiveEffectRuntime.cs` | period / overflow instant 与 active mutation producer 写 current / next-frame carrier 时登记 dirty owner |
| `Assets/GAS/Runtime/System/Effect/GEActiveEffectLifecycleSystems.cs` | active effect lifecycle apply job 传入 dirty owner buffer lookup |
| `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeEntityArchetypes.cs` | EffectCommandStream archetype 挂载两个 dirty owner index buffer |
| `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeQueryLayoutPlan.cs` | runtime layout slot 增加 dirty owner index buffer |
| `Assets/GAS/Runtime/System/SystemGroup/GASRuntimeStreamOwnerContract.cs` | stream owner id 增加 dirty owner index lane |
| `Tools/Diagnostics/Verify-GAS-RuntimeCoreBoundary.ps1` | 增加 dirty owner index contract；修正 singleton stream spec clear 断言，避免误伤 owner-local prepare 系统 |

## 证据路径

| 类型 | 路径 |
|---|---|
| cold polluted summary | `TestResults/AutoChess/Headless/AutoChessHeadlessValidation-DebuggerProbe-20260608-Run10-PrepareDirtyOwnerIndex.txt` |
| project lock sample | `Logs/AutoChessHeadless-Run10b-PrepareDirtyOwnerIndex-ExecuteMethod.log` |
| effective headless summary | `TestResults/AutoChess/Headless/AutoChessHeadlessValidation-DebuggerProbe-20260608-Run10c-PrepareDirtyOwnerIndex.txt` |
| brief | `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run10c-PrepareDirtyOwnerIndex/AutoChessProfileBrief.md` |
| performance budget | `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run10c-PrepareDirtyOwnerIndex/SubReports/PerformanceBudget.md` |
| hotspot matrix | `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run10c-PrepareDirtyOwnerIndex/SubReports/HotspotAttribution.md` |
| debugger evidence | `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run10c-PrepareDirtyOwnerIndex/SubReports/DebuggerEvidence.md` |
| journaling TopN | `TestResults/AutoChess/Analysis/DebuggerProbe-20260608-Run10c-PrepareDirtyOwnerIndex/SubReports/JournalingTopN.md` |
| Unity log | `Logs/AutoChessHeadless-Run10c-PrepareDirtyOwnerIndex-ExecuteMethod.log` |

## Run10c 关键结果

| 指标 | 值 | 判定 |
|---|---:|---|
| `passed` | true | 业务链路通过 |
| `headlessLogicBudgetPassed` | true | strict pure logic budget 通过 |
| `repeatRunPassed` | true | 确定性复跑通过 |
| `performanceExcellentPassed` | false | Profiler disabled，不能写成 DOTS profiler-backed 优秀 |
| `avgTickMs` | 0.898ms | 低于 1.500ms |
| `GASTickTotal.avgMs` | 0.886ms | 低于 1.500ms |
| `GASCoreSimulationSystemGroup.avgMs` | 0.494ms | 低于 0.900ms |
| `BoundaryOwner.avgMs` | 0.231ms | 低于 0.350ms |
| `RunnerOwner.avgMs` | 0.041ms | 低于 0.150ms |
| `DebuggerOwner.avgMs` | 121.053ms | diagnostic-only，不并入 performance pass |

## Run8 / Run9b / Run10c Frame-Lane 对比

| Lane | Run8 | Run9b | Run10c | 结论 |
|---|---:|---:|---:|---|
| instant prepare scanned owners | 2400 | 2400 | 950 | 只处理 dirty owner |
| instant prepare skipped owners | 1450 | 1450 | 0 | 全 ASC 扫描后的空跳过被消除 |
| instant prepare dirty owners | 950 | 950 | 950 | 业务有效 owner 未丢失 |
| active mutation prepare scanned owners | 2400 | 2400 | 200 | 只处理 active mutation dirty owner |
| active mutation prepare skipped owners | 1750 | 1750 | 0 | 空跳过被消除 |
| active mutation prepare dirty owners | 650 | 650 | 200 | 本轮 metric 归因为 dirty mutation owner；有效 mutation command 仍为 200 |
| active effect pre-tick owners scanned/processed/skipped | 2400 / 950 / 1450 | 2400 / 600 / 1800 | 2400 / 600 / 1800 | 保持 Run9b due lane 改善 |
| active effect pre-tick slots scanned/due/noop | 1200 / 550 / 650 | 750 / 550 / 200 | 750 / 550 / 200 | 未回退 |
| active effect pre-tick mutation writes | 600 | 600 | 600 | 业务输出保持一致 |
| `GetBufferRW` | 67884 | 60684 | 48852 | Runtime RW 热点继续下降 |
| `GetComponentDataRW` | 20210 | 20210 | 20204 | 基本持平 |
| `EnableComponent` | 650 | 650 | 650 | 未恢复 Run6 高频 marker |

## Run10 / Run10b 降级说明

1. Run10 首跑写出 summary，但日志包含 Unity 脚本编译 / domain reload；结果为 `passed=False`、`headlessLogicBudgetPassed=False`、`avgTickMs=4.369ms`。该样本只能保留为冷启动污染证据，不能作为代码性能回归或通过样本。
2. Run10b 因项目锁冲突退出：日志显示 `It looks like another Unity instance is running with this project open.`，没有生成有效业务 summary，不能作为验证样本。
3. Run10c 复跑写出有效 summary，并满足业务链路、strict budget、repeat run、blocking errors 和 dirty owner counter 验收；本归档只把 Run10c 作为有效性能样本。

## Hotspot Matrix 结论

Run10c 仍保留以下 High row：

| ID | Count | DOTS risk | 下一轮 owner |
|---|---:|---|---|
| `GAS-ARCH-07` | 11250 | `BroadBufferRW+OwnerLocality` | R3 |
| `GAS-ARCH-STREAM-RW` | 10549 | `SingletonStreamRW` | R4/R3 |
| `GAS-ARCH-AE-PRETICK` | 9100 | `PerFrameSlotScan` | R7/R3 |
| `GAS-ARCH-ATTR-RW` | 7050 | `BroadBufferRW` | R3 |
| `GAS-ARCH-CMD-PREPARE` | 4259 | `PerFrameBufferClearCopy` | R7/R3 |
| `GAS-ARCH-06` | 1350 | `FanOutScan` | R5/R3 |
| `GAS-DBG-01` | 2412 | `ObservationMaterialization` | R4 |
| `GAS-MEASURE-02` | 1 | `MissingProfilerEvidence` | R8 |

C2 的验收是 prepare 稀疏扫描第一刀完成，不是所有 R7/R3 hotspot 完成。下一轮应优先领取 R3 fact fan-in / attribute dirty range、R4/R3 stream sequence allocation、R5 execution spec generated index 与 R8 Profiler enabled scale gate。

## 验证记录

| 验证 | 结果 |
|---|---|
| `powershell -ExecutionPolicy Bypass -File .\Tools\Diagnostics\Verify-GAS-RuntimeCoreBoundary.ps1` | 通过 |
| `dotnet build .\com.exhard.exgas.runtime.csproj -v:minimal` | 通过，仅既有 `MSB3277` |
| `dotnet build .\com.exhard.exgas.autochessdemo.csproj -v:minimal` | 通过，仅既有 `MSB3277` |
| AutoChess x50 headless Run10c | `passed=True`、`headlessLogicBudgetPassed=True`、`runtimeChainPassed=True`、`repeatRunPassed=True`、`blockingDebugErrors=0` |
| split report | 已生成 brief + PerformanceBudget / HotspotAttribution / JournalingTopN / DebuggerEvidence / RuntimeBattleLog |

## 不能宣称

1. 不能宣称 `performanceExcellentPassed=True`，因为 `profiler disabled; Entities profiler modules collect no data`。
2. 不能宣称 R7/R3 全部完成：`GAS-ARCH-CMD-PREPARE=4259` 与 `GAS-ARCH-AE-PRETICK=9100` 仍是 High row。
3. 不能把 Run10 首跑失败写成代码回归；它是冷启动 / 编译污染样本。
4. 不能把 Run10b 写成测试失败；它是项目锁冲突，没有有效业务 summary。
5. 不能把 `DebuggerOwner.avgMs=121.053ms` 并入 CoreSimulation；它是 diagnostic-only materialization / export 成本。
