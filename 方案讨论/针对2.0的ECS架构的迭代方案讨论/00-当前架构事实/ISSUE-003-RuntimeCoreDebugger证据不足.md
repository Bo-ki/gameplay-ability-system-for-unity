# ISSUE-003 Runtime Core Debugger 证据不足

> 最近复核：2026-06-06 | 状态：Active | 严重度：P1

## 当前结论

Debugger 与 official diff 工具已经存在，不再是“没有证据工具”。当前问题是证据还没有系统性服务于架构退出条件：结构变化相位、buffer pressure、dependency complete、Core/Boundary 成本拆分、battle hash 稳定性仍需要固定报告。

## 已成立事实

1. `GasRuntimeDebugger` 能创建 runtime snapshot 并导出文本。
2. `GasRuntimeOfficialToolDiff` 使用 `EntitiesJournaling` 统计 create/destroy/add/remove/enable/disable/set/get。
3. `ReplayLogSystem` 与 structured log exporter 能输出 replay/断言文本。
4. AutoChess bridge 能导出 replay、diagnostics、official diff snapshot。
5. AutoChess validation report 已能输出 runtime diagnostics、physical group timing、hotspot summary、official diff summary、dataflow 和 sequence 导出；这些导出是 structured evidence 的消费面，不是机器验收源本身。

## 仍成立风险

1. Debugger 自身有 query / `ToEntityArray` 成本，不能混入 CoreSimulation hot path。
2. 当前文档不能仅凭“工具存在”宣称结构变化已收口。
3. generated runtime 的 query/dependency/buffer pressure 缺少专项 counters。
4. x50/x1000 profile 与 battle hash 需要与 Debugger snapshot 联动。
5. `GASRuntimeDebuggerEvidenceGateContract` 当前提供的是 contract / plan 计数；`ProfilerMarkerCount`、`JournalingMarkerCount`、cost group split 不能直接消费为 Unity Profiler / Entities Journaling 已 captured。
6. CoreSimulation 的 query materialization、Boundary managed query、Debugger observation query 和 runner sync 必须分别计入 evidence owner，否则平均 tick 会掩盖真实热点。

## 代码证据

| 事实 | 文件 |
|---|---|
| runtime debugger | `Assets/GAS/Runtime/Debugger/GasRuntimeDebugger.cs` |
| official diff | `Assets/GAS/Runtime/Debugger/GasRuntimeOfficialToolDiff.cs` |
| structured log | `Assets/GAS/Runtime/Event/GasStructuredLogExport.cs`, `GasStructuredLogView.cs` |
| AutoChess export | `Assets/AutoChessDemo/Integration/GasCore/AutoChessGasCoreBridge.cs` |

## 退出条件

1. 每次架构验证报告拆分 CoreSimulation、BoundaryProjection、Demo Integration。
2. Journaling 能列出结构变化来源 system 和 phase。
3. Debugger 输出 buffer pressure、query count、dependency complete、event/fact counts。
4. AutoChess 验证报告包含 battle hash 与 blocking debug error count。
