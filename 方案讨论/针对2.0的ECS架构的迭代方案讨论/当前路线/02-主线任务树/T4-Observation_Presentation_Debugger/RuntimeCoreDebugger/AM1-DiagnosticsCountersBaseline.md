# Observation / Presentation / Debugger - Runtime Core Debugger - Diagnostics Counters Baseline

## 父节点

[Runtime Core Debugger](README.md)

## 任务ID

`T4-Debugger-AM1`

## 状态

`契约已确立`

## 当前问题

1. 当前热点定位依赖 systemTiming、validation summary 和人工日志对照。
2. Replay / StructuredLog 能说明发生了什么，但不能充分说明哪里慢、哪里结构变化多、哪个 buffer 接近容量。
3. Debugger 能力不足使 Agent 容易继续做局部 fast path，而不是从架构管线定位问题。

## 目标态参考

1. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
2. `01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`
3. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
4. `00-当前架构事实/核心问题诊断.md`

## 历史方案参考

1. `../../历史方案参考/方案15.md` 的 GASDebugger 时序图和排查 workflow 可参考。
2. 只吸收 Debugger 可视化、时序图、查询体验；不吸收托管 Debugger 作为 simulation routing。

## 目标 / 目的

1. 输出 request/spec/delta/fact/entity lifecycle/ECB playback/buffer pressure/cursor lag counters。
2. AutoChess x50 能从 summary 解释热点。
3. 为 AM-2 之后迁移提供机器可读证据。

## 非目标

1. 不让 Runtime system 读取 Debugger 结果改变 gameplay。
2. 不在 hot path 拼接托管字符串。
3. 不替代 Replay / Presentation outbox。

## 执行范围

1. `Assets/GAS/Runtime/Debugger`
2. `Assets/AutoChessDemo/Runtime/Debugging`
3. 当前迁移来源：`Assets/GAS/Runtime/Demo/AutoChess`
4. `Assets/GAS/Runtime/System/Event`
5. 相关 validation summary/export 代码。

## 执行细则

1. counters 必须是结构化数据，不是人读字符串。
2. hot path 只记录轻量数值；格式化和导出后置。
3. system timing 必须标明是否污染 `ecsRuntimeTickOnly`。
4. Debugger / Replay / Presentation 三者保持独立职责。
5. 输出 `SEL-*` API 选型健康指标，并能说明哪些指标用于判断当前承载是否应该从 DynamicBuffer / singleton 切换到 NativeStream、chunk counters 或 system-associated entity。

## 验收

1. x1 默认链路输出 diagnostics。
2. x50 输出 runtimeDiagnosticsPeak 和 runtimeSlowSystem。
3. 能定位 entity create/destroy、ECB playback、buffer pressure、cursor lag 的数量级。

## 测试链路

1. AutoChess 默认 validation。
2. AutoChess x50 profile。
3. systemTiming 仅用于热点排序，报告必须注明口径。

## 交还内容

1. 更新 `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`。
2. 更新 `00-当前架构事实/核心问题诊断.md` 的诊断能力事实。
3. 写入迭代记录和验证摘要。
