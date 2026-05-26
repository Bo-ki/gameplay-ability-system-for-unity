# ISSUE-004 结构变化边界脆弱

## 状态

| 字段 | 内容 |
|---|---|
| 状态 | Active |
| 严重度 | P0 |
| 最近复核 | 2026-05-24 |
| 所属层 | GAS Runtime Core Layer |

## DOTS规则引用

| 规则编号 | 规则摘要 | 关联原因 |
|---|---|---|
| `SC-01` | Hot path 不直接结构变化；集中在 mutation phase/ECB playback | `AbilityRuntimeActions` 直接 `EntityManager.AddComponentData`，违反 phase 隔离 |
| `SC-02` | 结构变化后重取所有 handle | 历史 `BufferTypeHandle invalidated by structural change` 异常 |
| `SC-03` | 批量同类变化优先 EntityQuery bulk | 当前逐个 entity 操作，未使用 bulk API |
| `ECB-01` | ECB 是延迟结构变化工具，不是 gameplay event bus | ECB playback 散落在 helper 便捷重载中 |
| `ECB-02` | AppendToBuffer 前确保 buffer 已存在 | 结构变化后 buffer handle 失效的根因 |
| `ECB-03` | ECB playback 位置属于明确 SystemGroup phase | `GasStructuralPlaybackSystemGroup` 已声明但未强制执行 |
| `PRF-02` | 禁止在 Hot Path 直接执行结构变化 | `entityManager.AddComponentData(ability, ...)` 在 hot path |
| `PRF-04` | 结构变化必须集中到单一 ECB playback phase | 当前无全局 phase contract 约束 |
| `BUF-03` | Buffer handle 结构变化后必须重取 | 历史三类 buffer invalidation 问题 |
| `EN-01` | 高频开关优先 Enableable，低频生命周期再考虑 Add/Remove | inhibited/period due 不应通过 add/remove component 表达 |

## 问题陈述

当前 Runtime Core 已修复若干 `DynamicBuffer` 句柄失效问题，但问题根源仍存在：部分链路会在读取 buffer / event stream 的过程中创建或销毁 entity、增删 component 或 append buffer。只要读写 phase 不清晰，后续新增系统仍可能重新引入结构变化异常或隐性 sync point。

## 当前证据

代码证据：

1. `SApplyGameplayEffectRequest` 读取 request 和 target buffer 后，在同一系统中可能创建 GE runtime entity，并销毁 request entity：`Assets/GAS/Runtime/System/Effect/SApplyGameplayEffectRequest.cs:25-58`, `Assets/GAS/Runtime/System/Effect/SApplyGameplayEffectRequest.cs:100-134`, `Assets/GAS/Runtime/System/Effect/SApplyGameplayEffectRequest.cs:203-236`。
2. `EventBusHelper.BeginGameplayEventBatch` 当前只缓存 metadata / capability，并在 dispose 时写回 `CGameplayEventBus`，这是结构变化异常后的修复形态：`Assets/GAS/Runtime/Event/EventBusHelper.cs:372-410`。
3. AutoChess reaction 已改为 `SnapshotBufferRange<T>` 后再处理，但这也说明全局 buffer 与写入逻辑仍需要显式隔离：`Assets/GAS/Runtime/Demo/AutoChess/SHeadlessAutoChessCounterReaction.cs:130-195`。
4. AM5 第一刀中 `ActiveEffectStore` helper 明确要求目标 ASC 已具备 `CActiveEffectStore` / `BActiveEffectSlot`，缺少时返回失败，不在 duration lifecycle hot path 自动补结构变化；该约束由 `ActiveEffectStoreDoesNotCreateMissingStoreInHotPath` 覆盖。
5. AM2B-D 已新增 `GASRuntimeStructuralPlaybackGateContract`、`GasStructuralPlaybackSystemGroup` 和 `GasEndStructuralEcbSystem`，并把 `GASSystemScheduleContract.StructuralPlayback` 指向该 gate；这说明唯一 structural playback gate 的 contract-first 锚点已存在，但真实 SystemGroup 搬迁、旧分散 structural playback 迁移和 Runtime evidence 仍未闭合。

异常 / 记录证据：

1. 真实 Scene 曾暴露 `BGameplayEvent`、`BAttributeChangeEvent`、`BAbilityTargetEffectOnActivate` 三类 `BufferTypeHandle invalidated by structural change`：`../../../迭代记录/88-T6-CHESS-AG-RuntimeTickProfileAndArchitectureFeedback.md:158-166`。
2. 短期稳定规则要求不能跨结构变化缓存 buffer 视图、可能结构变化时必须先 snapshot、读 facts 和写 commands 分相位：`../../../迭代记录/88-T6-CHESS-AG-RuntimeTickProfileAndArchitectureFeedback.md:168-177`。
3. 架构反推明确要求 EventBus 不能作为主执行队列，Simulation hot path 迁移到 typed fact / command stream / phase pipeline：`../../../迭代记录/88-T6-CHESS-AG-RuntimeTickProfileAndArchitectureFeedback.md:178-184`。

## 执行路径

```text
System reads DynamicBuffer / query result
-> same loop creates request/runtime entity or adds/removes component
-> ECS structural change invalidates buffer handle/type handle
-> next read/write of old handle throws ObjectDisposedException
```

## 影响

1. 结构安全依赖每个系统作者记住局部规则，缺少全局 phase contract。
2. 隐性 sync point 和 structural change 成本会破坏规模曲线。
3. 新业务 reaction 很容易恢复“边读 event bus 边写 request”的写法。

## 根因反推

结构变化不是单个 API 使用问题，而是 phase 设计问题。Runtime Core 需要显式区分：

1. Command ingest。
2. Spec resolve。
3. Delta apply。
4. Active effect lifecycle。
5. Gameplay reaction。
6. Observation projection。
7. Cleanup。

每个 phase 的读集合、写集合和 structural boundary 都必须能在 Debugger 中被观察。

## 目标态入口

1. `../../01-目标态架构共识/03-RuntimeCore管线Spec.md`
2. `../../01-目标态架构共识/05-ActiveEffectStoreSpec.md`
3. `../../01-目标态架构共识/90-目标态不变量.md`

## 任务入口

`../../02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCore重构/README.md`

## 退出条件

1. 所有 hot path 系统的读写集合和 structural change phase 在 Runtime structural plan 中可审查。
2. Debugger 能输出 entity create/destroy、component add/remove、ECB playback、gate-level ECB command / bulk query 计数，并能对照 Profiler / Journaling。
3. ActiveEffectStore 普通状态切换不触发 archetype churn，缺 store 目标不在 hot path 隐式 add component / add buffer。
4. AutoChess Scene runtime 不再出现 buffer/type handle invalidation，且新 reaction 任务模板明确禁止边读边结构变化。
