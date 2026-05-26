# Observation / Presentation / Debugger - Runtime Core Debugger - Validation Summary Export

## 父节点

[Runtime Core Debugger](README.md)

## 任务ID

`T4-Debugger-Export-1`

## 状态

`候选`

## 目标 Spec

`01/07 RuntimeCoreDebugger`

## 当前问题

Diagnostics counters 已有 baseline，但 validation summary 的格式化导出仍需从 counters 到机器可读报告的全链路验证。

## 目标态参考

1. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
2. `01-目标态架构共识/10-AutoChess无头验收Spec.md`

## 非目标

1. 不让 export 逻辑污染 hot path。
2. 不替代 Replay / StructuredLog。

## 前置依赖

1. T4-Debugger-AM1 Diagnostics Counters Baseline 完成。

## 执行范围

1. `Assets/GAS/Runtime/Debugger`
2. `Assets/AutoChessDemo/Runtime/Debugging`
3. validation summary export 格式化。

## 验收标准

1. counters 可导出为机器可读格式。
2. AutoChess x1 / x50 summary 可自动解析。
3. export 不污染 core simulation tick。
