# Observation / Presentation / Debugger - Runtime Core Debugger

## 父节点

[T4 Observation / Presentation / Debugger](README.md)

## 节点定位

本支线负责 Runtime Core Debugger 的结构化 counters、profile summary、diagnostics export 和 AutoChess 验收接入。

## 当前问题

1. 当前查找业务重点性能热力点困难，systemTiming 与人工日志不足以定位管线阻塞。
2. 旧 Runtime 管线问题需要 request/spec/delta/fact/entity lifecycle/ECB/buffer/cursor 级别证据。
3. Debugger 若建设不足，后续 Agent 容易继续做局部 fast path。
4. DOTS 深读后，Debugger 还必须解释 Query filter、enableable wait、DynamicBuffer externalized、allocator owner、chunk fragmentation、job overhead 和 Burst warmup，否则仍无法判断 Runtime Core 是否真正进入 ECS 优势区间。
5. 官方案例深挖后，Debugger 还必须能标记 hot path 是否符合 `CASE-*` 模式，尤其是 `CASE-01/04/07/08/10/11/12`。
6. 官方文档查缺补漏后，Debugger 还必须把项目 counters 对齐 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 覆盖矩阵中的官方诊断工具、sync point、chunk fragmentation、buffer externalized、allocator rewind 和 singleton dependency 证据。
7. Unity Physics / Entities Graphics 新包接入后，Debugger 必须输出 physics / graphics counters 或 disabled reason，并把 physics fixed-step / render cost 从 Runtime Core tick 中拆出。

## 目标态参考

1. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
2. `01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`
3. `01-目标态架构共识/10-AutoChess无头验收Spec.md`
4. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
5. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
6. `UnityDOTS官方文档参考/主题/12-官方案例模式.md`
7. `UnityDOTS官方文档参考/README.md`
8. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`

## 历史方案参考

1. `历史方案参考/方案15.md` 的 Debugger 时序图、日志模块、排查 workflow 可参考。
2. `方案14.md` 的自走棋业务验证链路可参考。
3. 托管 Debugger 参与 gameplay routing 不可照搬。

## 支线目标

建立 Runtime Core 诊断 counters 和导出能力，让 AutoChess 默认链路与 x50 profile 能用机器可读数据解释热点。

## 当前状态

`T4-Debugger-AM1` 已完成代码侧 baseline，AM-5 已补 ActiveEffectStore owner-local slot pressure / state distribution / legacy-backed / externalized owner baseline，Unity 验证待补跑。下一步保持 T4 作为 AM-2/AM-5 后续迁移的诊断出口，不继续在 Runtime 未稳定前扩展 AutoChess 业务验证。

## 非目标

1. 不让 Runtime system 读取 Debugger 结果改变 gameplay。
2. 不在 hot path 拼接托管字符串。
3. 不替代 Replay / Presentation outbox。

## 前置依赖

1. T1 已明确 Runtime Core rebuild 方向。
2. AutoChess validation summary 已具备基础导出。

## 执行范围

1. `Assets/GAS/Runtime/Debugger`
2. `Assets/AutoChessDemo/Runtime/Debugging`
3. 当前迁移来源：`Assets/GAS/Runtime/Demo/AutoChess`
4. `Assets/GAS/Runtime/System/Event`
5. validation summary / profile export。

## 执行细则

1. counters 必须是结构化数据，不是人读字符串。
2. hot path 只记录轻量数值，格式化和导出后置。
3. system timing 必须标明是否污染 `ecsRuntimeTickOnly`。
4. diagnostics 必须能解释 API 选型健康度，至少覆盖 global buffer pressure、NativeStream merge、per-chunk skip、structural query batch、singleton dependency warning。
5. diagnostics 必须能对照 Unity Profiler Memory / Structural Changes / Entities Journaling / Burst Inspector 中至少一种外部证据源。
6. diagnostics 必须输出 filtered / unfiltered query count、enableable dependency wait、DynamicBuffer spill/externalized、allocator owner / dispose、archetype / chunk utilization。
7. diagnostics 必须输出 `casePattern` / `caseViolation` 或等价字段，说明当前热点是否违反 `CASE-*` 官方案例模式。
8. diagnostics 必须输出 `officialDocTopic` / `odfRule` / `unityToolEvidence` 或等价字段，说明当前热点可对应到 `ODF-*` 和 Unity Systems window、Query window、Profiler、Journaling、Binary debugging 中的哪个证据源。
9. diagnostics 必须输出 `physicsStepCount/queryCount/collisionEventCount/triggerEventCount/broadphaseSyncCount` 或 disabled reason。
10. diagnostics 必须输出 `entitiesGraphicsDrawCommand/instancesPerDraw/BRGMarker/renderCostMs` 或 disabled reason。

## 验收门槛

1. x1 默认链路输出 diagnostics。
2. x50 输出 runtimeDiagnosticsPeak 和 runtimeSlowSystem。
3. 能定位 entity create/destroy、ECB playback、buffer pressure、cursor lag 和 ActiveEffectStore owner-local slot pressure 的数量级。
4. 能解释热点来自结构变化、chunk fragmentation、query filter 无效、buffer externalized、dependency wait、job overhead、Burst warmup 或 GAS 语义本身中的哪一类。
5. 能解释热点是否违反 `CASE-01` 主线程 foreach、`CASE-07` buffer、`CASE-08` ECB、`CASE-12` 性能测试口径等官方案例规则。
6. 能解释热点是否违反 `ODF-07` 外部证据闭环，以及是否缺失 `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md` 覆盖矩阵要求的官方工具对照。
7. 能解释 `ODF-15..18` 是否相关；相关时能输出 Physics / Graphics 指标，不相关时能输出 disabled reason。

## 测试链路

1. AutoChess 默认 validation。
2. AutoChess x50 profile。
3. systemTiming / diagnostics summary 对照。

## 当前任务看板

| 任务ID | 任务名 | 状态 | 目标 Spec |
|---|---|---|---|
| T4-Debugger-AM1 | Observation / Presentation / Debugger - Runtime Core Debugger - Diagnostics Counters Baseline | 契约已确立 | `01/07 RuntimeCoreDebugger` |
| T4-Debugger-Export-1 | Observation / Presentation / Debugger - Runtime Core Debugger - Validation Summary Export | 候选 | `01/07 RuntimeCoreDebugger` |

## 三级任务：Diagnostics Counters Baseline

任务ID：`T4-Debugger-AM1`

状态：`契约已确立`

任务名：`Observation / Presentation / Debugger - Runtime Core Debugger - Diagnostics Counters Baseline`

当前问题：

1. 当前热点定位依赖 systemTiming、validation summary 和人工日志对照。
2. Replay / StructuredLog 能说明发生了什么，但不能充分说明哪里慢、哪里结构变化多、哪个 buffer 接近容量。
3. Debugger 能力不足使 Agent 容易继续做局部 fast path，而不是从架构管线定位问题。

目标态参考：

1. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
2. `01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`
3. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
4. `00-当前架构事实/核心问题诊断.md`

历史方案参考：

1. `../../历史方案参考/方案15.md` 的 GASDebugger 时序图和排查 workflow 可参考。
2. 只吸收 Debugger 可视化、时序图、查询体验；不吸收托管 Debugger 作为 simulation routing。

目标 / 目的：

1. 输出 request/spec/delta/fact/entity lifecycle/ECB playback/buffer pressure/cursor lag counters。
2. AutoChess x50 能从 summary 解释热点。
3. 为 AM-2 之后迁移提供机器可读证据。

非目标：

1. 不让 Runtime system 读取 Debugger 结果改变 gameplay。
2. 不在 hot path 拼接托管字符串。
3. 不替代 Replay / Presentation outbox。

执行范围：

1. `Assets/GAS/Runtime/Debugger`
2. `Assets/AutoChessDemo/Runtime/Debugging`
3. 当前迁移来源：`Assets/GAS/Runtime/Demo/AutoChess`
4. `Assets/GAS/Runtime/System/Event`
5. 相关 validation summary/export 代码。

执行细则：

1. counters 必须是结构化数据，不是人读字符串。
2. hot path 只记录轻量数值；格式化和导出后置。
3. system timing 必须标明是否污染 `ecsRuntimeTickOnly`。
4. Debugger / Replay / Presentation 三者保持独立职责。
5. 输出 `SEL-*` API 选型健康指标，并能说明哪些指标用于判断当前承载是否应该从 DynamicBuffer / singleton 切换到 NativeStream、chunk counters 或 system-associated entity。

验收：

1. x1 默认链路输出 diagnostics。
2. x50 输出 runtimeDiagnosticsPeak 和 runtimeSlowSystem。
3. 能定位 entity create/destroy、ECB playback、buffer pressure、cursor lag 的数量级。

测试链路：

1. AutoChess 默认 validation。
2. AutoChess x50 profile。
3. systemTiming 仅用于热点排序，报告必须注明口径。

交还内容：

1. 更新 `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`。
2. 更新 `00-当前架构事实/核心问题诊断.md` 的诊断能力事实。
3. 写入迭代记录和验证摘要。


