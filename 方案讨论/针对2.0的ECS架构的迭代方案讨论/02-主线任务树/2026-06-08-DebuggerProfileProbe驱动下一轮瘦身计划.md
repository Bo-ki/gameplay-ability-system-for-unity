# 2026-06-08 Debugger Profile Probe 驱动下一轮瘦身计划

> Owner：`02-主线任务树`
> 状态：优先就绪
> 输入事实：`../00-当前架构事实/ISSUE-003-RuntimeCoreDebugger证据不足.md`、`../00-当前架构事实/ISSUE-014-Headless纯逻辑预算超标.md`、`../00-当前架构事实/_归档/2026-06-08-DebuggerProfileProbe-Run6.md`
> 目标约束：`../01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`

本计划只定义下一轮可领取切片。实现事实回写 `00`，验证流水回写 `04`，目标态约束回写 `01`。

## 领取结论

DebuggerProbe Run3 证明 x50 strict pure logic budget 已通过，但不能宣称 DOTS 性能优秀：Profiler evidence disabled，且 Debugger/Journaling 已定位到明确的数据形态热点。下一轮不应泛泛压总 ms，而应按 Debugger evidence 领取以下 owner：

1. R4：Debugger hotspot attribution matrix 与 diagnostic materialization owner 拆分。
2. R3：OwnerLocalGameplayFactBuffer fact fan-in / dirty span lane。
3. R7/R3：ActiveEffect pre-tick 与 instant command prepare buffer RW 热点拆分。
4. R5/R3：execution calculation code -> effect spec generated index。
5. R8：Profiler enabled / x100 / x1000 scale gate。

## 切片 A：Debugger Hotspot Attribution Matrix

目标：让 Debugger 不只输出 TopN 字符串，而是输出 `GAS concept -> phase/lane -> system -> component/buffer -> DOTS risk -> next owner` 的机器矩阵。

执行范围：

1. `Assets/GAS/Runtime/Debugger/GasRuntimeDerivedExportSink.cs`
2. `Assets/GAS/Runtime/Debugger/GasRuntimeDataOrientedScorecard.cs`
3. `Assets/GAS/Runtime/Debugger/GasRuntimeMetricFamilySnapshot.cs`
4. `Tools/Diagnostics/Analyze-AutoChessProfile.ps1`
5. AutoChess validation report 的 hotspot summary。

验收：

1. Run report 能同时输出 RW TopN、owner-local range、execution scan ratio、dependency wait、sync query 和 profiler disabled reason。
2. 每个 High finding 都能给出下一轮 owner，不允许只输出“性能慢”。
3. performance pass 的 `performanceObservationPollutionRisks` 仍为 0。

## 切片 B：OwnerLocalGameplayFactBuffer Dirty Span Lane

目标：降低 `OwnerLocalGameplayFactBuffer=11250`、`ownerLocalFactFlushes=5200`、`ownerLocalFactMaxOwnerRange=9` 暴露的 fact fan-in 成本。

执行范围：

1. `OwnerLocalGameplayFactBuffer` producer / flush system。
2. pending AttributeDelta fact producer。
3. instant reduce / execution output fact producer。
4. Debugger fact dirty owner / dirty fact span counters。

验收：

1. 新增 dirty owner count、dirty fact count、unchanged skip count、max dirty span。
2. AutoChess x50 report 能说明 fact flush 是否按 dirty owner 减少。
3. Journaling TopN 中 `OwnerLocalGameplayFactBuffer` 每 tick RW 下降，或报告给出无法下降的具体数据形态原因。

## 切片 C：ActiveEffect PreTick / Instant Command Prepare RW 拆分

目标：拆解 `GASActiveEffectPreTickSystem=15800`、`OwnerLocalInstantCommandFramePrepareSystem=9000`、`ActiveEffectOwnerLocalMutationFramePrepareSystem=9000` 三个 GetBufferRW 热点。

执行范围：

1. `GASActiveEffectPreTickSystem`
2. `OwnerLocalInstantCommandFramePrepareSystem`
3. `ActiveEffectOwnerLocalMutationFramePrepareSystem`
4. 相关 active effect slot / owner-local command buffers。

验收：

1. 每个系统输出 chunk count、owner count、active slot count、skipped slot count、buffer write count。
2. 能证明热点来自必要 active slot 遍历，或改为 dirty / due slot lane。
3. AutoChess x50 report TopN 可对比优化前后。

## 切片 D：Execution Spec Generated Index

目标：降低 `executionSpecScans=1350` / `executionMatchedEffectSpecs=350` 的 3.86:1 scan/match ratio。

执行范围：

1. execution calculation spec selection。
2. Definition / generated runtime lookup。
3. AutoChess execution calculation sample。

验收：

1. 生成 calculation code -> effect spec index 或等价 runtime lookup。
2. report 输出 spec scan count、matched effect count、index hit / miss count。
3. x50 scan/match ratio 明确下降，或给出业务配置导致不可下降的 evidence。

## 切片 E：Profiler Enabled / Scale Gate

目标：把 `performanceExcellentPassed=False` 从“Profiler disabled”推进到 official-profiler-backed 结论。

执行范围：

1. Unity batchmode profiler capture mode。
2. `GasRuntimeOfficialToolDiff` profiler state。
3. AutoChess x100 / x1000 scale profile。

验收：

1. 如果 Profiler enabled 可用，报告必须有 profiler frame range / category state / capture artifact。
2. 如果 batchmode 无法启用 Entities profiler module，必须输出硬理由，不能把 disabled 当通过。
3. x50 / x100 / x1000 使用同构 summary 和 analysis script。

## 本轮交还包

每个切片完成后必须交还：

1. 代码改动路径。
2. Run summary 路径。
3. profile analysis JSON / Markdown 路径。
4. TopN 对比。
5. `00` fact 更新。
6. `01` 目标约束是否需要调整。
7. 未跑项和不能宣称的结论。
