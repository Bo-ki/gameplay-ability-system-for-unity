# T4 Observation / Presentation / Debugger

## 节点定位

本主线负责 Runtime Boundary Layer 中的 Observation、Presentation Boundary、Replay Sink、Structured Log 和 Runtime Core Debugger。它只观察和导出事实，不反向驱动 gameplay state。

## 当前问题

1. 当前热点定位依赖 systemTiming、validation summary 和人工日志对照，难以解释 Runtime Core 真实阻塞点。
2. Replay / StructuredLog 能说明发生了什么，但不足以说明哪里慢、哪里结构变化多、哪个 buffer 接近容量。
3. 无头 demo 需要保留 UI / VFX / SFX / Cue 逻辑占位，但不能污染 core simulation tick。

## 目标态参考

1. `01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`
2. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
3. `01-目标态架构共识/10-AutoChess无头验收Spec.md`
4. `UnityDOTS官方文档参考/主题/90-规则编号索引.md`
5. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
6. `01-目标态架构共识/12-命名规范Spec.md`

## 历史方案参考

1. `历史方案参考/方案15.md` 的 Debugger 日志模块、时序图、排查 workflow 可参考。
2. `方案14.md` 的自走棋表现 / Cue 验收视角可参考。
3. Debugger 参与 gameplay routing 或托管事件总线的方向不可照搬。

## 主线目标

维护 facts、presentation outbox、replay sink、structured log 和 runtime diagnostics 的分层边界，让性能热点和业务事实可机器读取、可自动验收。

## 非目标

1. 不让 Debugger 输出影响 simulation。
2. 不在 hot path 拼接人读日志。
3. 不用 Presentation outbox 替代 GAS typed facts。

## 执行范围

1. `Assets/GAS/Runtime/Debugger`
2. `Assets/GAS/Runtime/Event`
3. `Assets/AutoChessDemo/Runtime/Observation`
4. `Assets/AutoChessDemo/Runtime/Presentation`
5. 当前迁移来源：`Assets/GAS/Runtime/Demo/AutoChess`
6. validation summary / profile export

## 执行细则

1. hot path 只记录轻量结构化计数，格式化后置。
2. Debugger、Replay、Presentation 三者职责独立。
3. system timing 必须标明是否污染 `ecsRuntimeTickOnly` 口径。
4. Debugger 任务必须输出 API 选型健康指标：global buffer pressure、NativeStream merge、per-chunk skip、structural query batch、singleton dependency warning。

## 验收门槛

1. AutoChess x1 / x50 输出 runtime diagnostics。
2. summary 能解释 slow system、entity lifecycle、ECB playback、buffer pressure、cursor lag。
3. UI / VFX / SFX / Cue marker 完整，但与 core simulation 成本分开报告。

## 测试链路

1. AutoChess 默认 validation。
2. AutoChess x50 profile。
3. systemTiming / diagnostics summary 对照。

## 支线索引

| 支线 | 文档 | 状态 |
|---|---|---|
| Runtime Core Debugger | [RuntimeCoreDebugger/README.md](RuntimeCoreDebugger/README.md) | 就绪 |

## 交还规则

交还时必须同步 Debugger Spec、当前架构问题诊断、AutoChess 验证摘要和对应支线状态。
