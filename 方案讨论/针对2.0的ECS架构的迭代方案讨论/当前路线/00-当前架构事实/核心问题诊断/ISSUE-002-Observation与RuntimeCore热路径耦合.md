# ISSUE-002 Observation 与 Runtime Core 热路径耦合

## 状态

| 字段 | 内容 |
|---|---|
| 状态 | Active |
| 严重度 | P0 |
| 最近复核 | 2026-05-24 |
| 所属层 | Runtime Boundary Layer / GAS Runtime Core Layer |

## DOTS规则引用

| 规则编号 | 规则摘要 | 关联原因 |
|---|---|---|
| `BUF-02` | 单一全局 buffer 不做百万实体 fan-in | `CGameplayEventBus` 是全局 singleton，5 种 buffer 承载所有事件 |
| `SEL-02` | 禁止 proof API 固化为 scale-ready 方案 | EventBus 原型不应固化为 simulation routing |
| `SYS-05` | World 边界：Debugger/Demo/Presentation 只能通过 Boundary 观察 Core | AutoChess reaction 直接读取全局 event bus 作为业务输入 |
| `DBG-01` | Debugger 不能驱动 Runtime Core | observation 数据量反向回压 Core tick |
| `STORE-03` | Store 选型按数据性质分类：gameplay/transient/telemetry/presentation | 业务 reaction、presentation、replay、debugger 共享同一 event stream |
| `PRF-26` | 读写数据分离到不同 Component | reaction 读 event bus + 写 gameplay event 产生响应式误触发风险 |
| `PRF-09` | Query 操作的 Sync 触发监控 | event bus buffer 读写增加隐性 sync point |
| `SEL-01` | 数据性质分类优先 | 应拆分为 typed simulation facts（内部）+ outbox（边界） |

## 问题陈述

`CGameplayEventBus` 当前既是 observation fact stream，又被部分 AutoChess reaction 当成业务输入扫描；Presentation、Replay、Debugger 和业务 reaction 共享全局事件流，导致观察数据量反向回压 Runtime Core tick。

## 当前证据

代码证据：

1. `CGameplayEventBus` 持有全局 sequence/context metadata，多类 buffer 共挂在同一个 event bus entity：`Assets/GAS/Runtime/Event/CGameplayEventBus.cs:11-15`, `Assets/GAS/Runtime/Event/CGameplayEventBus.cs:340-389`。
2. `EventBusHelper` 同时追加 `BGameplayEvent`、`BAttributeChangeEvent`、`BCueRequest`、`BDamageEvent`，并在 batch 中直接 `GetBuffer(...).Add(...)`：`Assets/GAS/Runtime/Event/EventBusHelper.cs:24-47`, `Assets/GAS/Runtime/Event/EventBusHelper.cs:97-134`, `Assets/GAS/Runtime/Event/EventBusHelper.cs:157-174`。
3. AutoChess reaction 会读取全局 event bus，snapshot attribute/damage/gameplay 事件后再写回新的 gameplay event：`Assets/GAS/Runtime/Demo/AutoChess/SHeadlessAutoChessCounterReaction.cs:27-53`, `Assets/GAS/Runtime/Demo/AutoChess/SHeadlessAutoChessCounterReaction.cs:130-195`。
4. Presentation / Replay projection 也读取同一批 event bus buffer，当前模块索引已将 `Assets/GAS/Runtime/Event` 标为 Observation 与 typed fact 分界未清晰：`../模块索引.md`。
5. AM3 已让 `SPresentationOutboxProjection` / `SDebugReplayLogProjection` 直接消费 `BTypedSimulationFact(AttributeBaseValueChanged)`、`BTypedSimulationFact(CueRequested)`、generic typed gameplay fact 与 damage typed fact，并通过 `SourceFactSequence` 跳过同源 legacy `BAttributeChangeEvent`、`BCueRequest`、`BGameplayEvent(CueRequested)`、generic legacy `BGameplayEvent` 和 `BDamageEvent`；这证明 attribute / cue / gameplay / damage observation 可以绕过旧 event bus，但 `CGameplayEventBus` 仍保留 legacy attribute / cue / gameplay / damage event 和业务 reaction 迁移期全局出口。

Profile / 记录证据：

1. x50 下 `BGameplayEvent` 峰值达到 `3910 / 4096`，presentation outbox 达到 `39423`：`../../../迭代记录/91-T6-CHESS-AL-ArchitecturePipelineDiagnosis.md:21-26`。
2. 架构诊断明确指出 EventBus 同时承担 Simulation、Observation、Presentation、Debug：`../../../迭代记录/91-T6-CHESS-AL-ArchitecturePipelineDiagnosis.md:58-68`。
3. 结构变化复盘要求 `CGameplayEventBus` 只保留为 observation fact stream，业务 reaction 迁移到 typed fact / cursor：`../../../迭代记录/88-T6-CHESS-AG-RuntimeTickProfileAndArchitectureFeedback.md:178-184`。

## 执行路径

```text
Runtime Core writes BGameplayEvent / BAttributeChangeEvent / BDamageEvent
-> AutoChess reaction snapshots global buffers
-> reaction writes new gameplay events / GE requests
-> PresentationOutboxProjection reads same global buffers
-> DebugReplayLogProjection reads same global buffers
-> buffer pressure and projection cost enter runtime tick
```

## 影响

1. 业务 reaction、表现 outbox、replay、debugger 互相放大事件数量。
2. 高频业务逻辑依赖全局 switch / scan，无法表现 ECS 的 typed stream 和 chunk 处理优势。
3. 无头 Demo 的表现 marker 完整性是必要验收，但不应污染 Core tick 成本。

## 根因反推

当前实现把历史方案中的 `EventBus` 信号实施得过中心化。方案14/15 的 EventBus 示例本应是 ECS -> OOP / Presentation bridge，如果缺少硬分层，就会被误用为 gameplay routing：`../../../迭代记录/92-T6-CHESS-AL-RouteReflectionFromPlans12To15.md:38-49`。

目标态应拆分：

1. Runtime Core 内部：typed simulation facts / typed delta stream / read cursor。
2. Runtime Boundary：fact stream projection、presentation outbox bridge、replay sink、diagnostics sink。
3. Application Shell：只消费 read model 和 marker。

## 目标态入口

1. `../../01-目标态架构共识/02-四层架构Spec.md`
2. `../../01-目标态架构共识/06-Observation-Presentation-ReplaySpec.md`
3. `../../01-目标态架构共识/12-命名规范Spec.md`

## 任务入口

`../../02-主线任务树/T4-Observation_Presentation_Debugger/RuntimeCoreDebugger.md`

## 退出条件

1. 高频业务 reaction 不再读取 `BGameplayEvent` 作为主输入。
2. Presentation / Replay / Debugger 的 projection tick 与 Core simulation tick 分开统计。
3. AutoChess summary 能分别输出 typed fact 数量、observation fact 数量、presentation marker 数量和 cursor lag。
