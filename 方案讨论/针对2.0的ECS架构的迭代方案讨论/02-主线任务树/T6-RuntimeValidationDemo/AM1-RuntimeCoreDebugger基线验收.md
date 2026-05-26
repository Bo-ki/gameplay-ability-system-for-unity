# Runtime Validation Demo - AutoChess 无头验收 - RuntimeCoreDebugger 基线验收

## 节点定位

本节点承接 T4 Runtime Core Debugger（AM1 Diagnostics Counters Baseline），负责在 AutoChess x1/x50 场景中验证 Debugger 的 diagnostics counters 能正确解释 Runtime Core 热点，确保 core simulation / observation projection / presentation marker 成本可拆分报告。

## 父节点

[AutoChess 无头验收](README.md)

## 任务ID

`T6-AutoChess-AM1`

## 状态

就绪

## 兄弟关系

`sequential`。前置：T6-AutoChess-AM0（Freeze Safety Gate 验收）、T4 AM1（Diagnostics Counters Baseline）。后继：T6-AutoChess-AM9（Scale Gates）依赖本节点的 x50 diagnostics 基线。

## 拆分历史

从 `AutoChess无头验收` 拆分（S4 依赖链：Debugger 基线必须先于 scale gates 建立），2026-05-20。

## 领取轮次

第 1 轮（首次领取）

## 当前进展

尚未领取。前置依赖：T4 AM1 Diagnostics Counters Baseline（契约已确立）、T6-AutoChess-AM0（就绪，待执行）。

## 本轮目标

首次执行：在 AutoChess x1 默认链路和 x50 压力场景中运行，验证 Debugger 能输出 request/spec/delta/fact/entity lifecycle/buffer pressure 级别 counters，core simulation 与 observation projection 成本可拆分，Physics/Graphics 未启用时输出 disabled reason。本轮不新增 AutoChess 业务机制。

## 当前问题

1. AutoChess 已经能覆盖复杂业务链，但当前验证报告不足以解释 Runtime Core 热点。
2. 无头表现/Cue marker 完整，但 core simulation 与 observation projection 的成本边界仍需分开报告。
3. Unity Physics / Entities Graphics 接入后，当前报告还缺少 physics / render disabled reason 或独立 counters，容易把未启用 profile 与链路缺失混淆。

## 目标

1. x1 默认业务链路通过，`passed=true`。
2. x50 输出 runtime diagnostics counters（request/spec/delta/fact/entity lifecycle/buffer pressure 级别）。
3. 表现/Cue marker 完整，core simulation tick 与 observation projection tick 能分开报告。
4. physics / render 成本与 core tick 分开报告；默认无头 profile 输出 disabled reason。
5. diagnostics summary 机器可读，可供 scale gates 作为基线对照。

## 目标态约束摘要

（从关联 Spec 提取，与本任务直接相关的硬约束）

1. Debugger counters 只读不写，不改变 simulation state。diagnostics summary 必须机器可读。
   > 来源：`07-RuntimeCoreDebuggerSpec.md`

2. 报告必须区分 core simulation、observation projection、presentation marker、export / bootstrap 成本。
   > 来源：`10-AutoChess无头验收Spec.md`

3. 默认 headless profile 必须记录 Physics / Graphics 未启用原因；启用 profile 时必须分别输出独立 counters。
   > 来源：`10-AutoChess无头验收Spec.md`、`ODF-15..18`

4. 表现/Cue marker 可追溯且不污染 core simulation tick。
   > 来源：`06-Observation-Presentation-ReplaySpec.md`

## 当前事实约束

（从关联 ISSUE 提取，本任务必须知道的架构事实）

1. AutoChess x50 当前 `avgTickMs=13.77ms`（ISSUE-001），diagnostics counters 需要能解释热点分布到具体 phase。
   > 来源：`ISSUE-001`

2. T4 AM2 Debugger 已接入 command/spec/delta/fact stream 权威计数，AutoChess 验收应优先使用新 stream 计数而非旧 EventBus buffer。
   > 来源：`ISSUE-011`

## 适用规则摘要

| 规则 | 本任务约束 |
|---|---|
| `DBG-01` | Debugger counters 只读不写，不改变 simulation state |
| `DBG-02` | diagnostics summary 机器可读，包含 counters 数值和 phase 分布 |
| `SEL-02` | observation projection 只消费 typed facts，不通过旧 EventBus |
| `ODF-15` | 未启用 Physics 时输出 `physicsDisabledReason` |
| `ODF-16` | 未启用 Entities Graphics 时输出 `entitiesGraphicsDisabledReason` |
| `ODF-17` | 若启用 physics profile，collision/trigger event 只在 `SimulationSingleton` 有效窗口内转换 |
| `ODF-18` | 若启用 rendered profile，输出 render binding / material override count / draw evidence |

## 非目标

1. 不接真实 UI / VFX / SFX 资源。
2. 不新增更多业务机制掩盖 Runtime Core 问题。

## 执行范围

1. `Assets/AutoChessDemo/` — AutoChess validation runner 和 summary export
2. `Assets/AutoChessDemo/Debugging/` — Debugger counters 接入点
3. T4 Runtime Core Debugger — diagnostics counters 消费端

## 执行细则

1. **log marker 占位**：UI/VFX/SFX/FloatingText/Cue 用 log marker 占位，但业务表现逻辑不能省略。
2. **机器可读 summary**：diagnostics summary 必须机器可读（JSON 或结构化 log），不能只输出人类可读文本。
3. **成本拆分报告**：报告必须区分 core simulation、observation projection、export / bootstrap 成本。
4. **Physics/Graphics 拆分**：报告必须区分 physics fixed-step / query / event consume、presentation marker 和 render cost。未启用 Physics / Graphics 时输出 disabled reason。
5. **事件窗口约束**：若启用 physics-enabled profile，collision / trigger event 只能在 `SimulationSingleton` 事件有效窗口内转换为 fact / command，并记录 event dropped / converted count。
6. **渲染证据**：若启用 rendered profile，必须输出 `RenderMeshArray` / `MaterialMeshInfo` binding、material override write count、draw command / instances per draw 或 BRG / Profiler 证据。

## 验收标准

1. x1 链路 `passed=true`，diagnostics summary 可解析。
2. `runtimeDiagnostics`、`runtimeDiagnosticsPeak` 或等价字段可解释热点。
3. x50 能输出 request/spec/delta/fact/entity lifecycle/buffer pressure 级别 counters。
4. 默认 profile 输出 Physics / Graphics disabled reason；启用 profile 时 `ODF-15..18` 指标齐全，physics / render cost 不计入 `coreTickMs`。
5. `dotnet build` Runtime + Tests 通过；如 LicensingClient 阻塞，记录为环境阻塞。

## 测试链路

1. `dotnet build` Runtime + Tests
2. AutoChess 默认 x1 validation
3. AutoChess x50 profile
4. `rg "coreTickMs|physicsDisabledReason|entitiesGraphicsDisabledReason"` — 确认 summary 包含必填字段
5. systemTiming profile 仅用于排序，必须标注口径。

## 交还内容

1. 更新本文件 `当前进展`、`领取轮次`、`状态` 字段。
2. 更新 `AutoChess无头验收/README.md` 看板 AM1 条目。
3. 更新 `04-当前进度状态/当前窗口.md` 推荐领取。
4. 更新 `04-当前进度状态/最近验证摘要.md` 加入 AutoChess x1/x50 diagnostics 基线。
