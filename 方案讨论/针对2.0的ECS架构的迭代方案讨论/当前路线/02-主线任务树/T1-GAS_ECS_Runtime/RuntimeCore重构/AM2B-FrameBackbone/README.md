# GAS ECS Runtime - Runtime Core Frame Backbone

## 父节点

[RuntimeCore重构](../README.md)

## 节点定位

本支线从 `RuntimeCore重构` 中拆出 `T1-RuntimeCore-AM2B Runtime Core Frame Backbone`，把原本过粗的单个任务拆成可连续领取、可独立交还、可逐步验证的任务链。

本目录是**分支节点**，不可直接领取。Agent 应从下方看板选取具体叶子任务文件领取。当前所有 AM2B 子任务已完成，推荐领取 AM3。

本支线不重新设计 GAS 语义。它只负责把 Unity DOTS 官方参考要求的 SystemGroup、Frame Arena、query / lookup、allocator、dependency、stream owner、structural playback 和 Debugger evidence 变成 Runtime Core 的可执行骨架。

## 当前问题

1. `ISSUE-009-RuntimeCoreFrameBackbone缺失` 已确认当前实现缺少统一 frame backbone。
2. AM2 / AM3 / AM5 已有局部 proof，但仍挂在旧 group / helper / singleton buffer 形态上。
3. DOTS 官方参考要求 query / buffer / job / structural / allocator / determinism / Profiler 证据分别进入交还。

## 目标态参考

1. `01-目标态架构共识/00-总览Spec.md`
2. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
3. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
4. `01-目标态架构共识/05-ActiveEffectStoreSpec.md`
5. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
6. `UnityDOTS官方文档参考/README.md`
7. `UnityDOTS官方文档参考/主题/02-查询遍历与Job.md`
8. `UnityDOTS官方文档参考/主题/03-结构变化-ECB-Enableable.md`
9. `UnityDOTS官方文档参考/主题/04-数据承载-Buffer-Chunk-Store.md`
10. `UnityDOTS官方文档参考/主题/06-Diagnostics-Profiler-Journaling.md`
11. `UnityDOTS官方文档参考/主题/10-Collections-Allocator-NativeStream.md`
12. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
13. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`

## 当前事实参考

1. `00-当前架构事实/核心问题诊断/ISSUE-001` 到 `ISSUE-009`

## 支线目标

建立 Runtime Core DOTS frame backbone：
1. SystemGroup / phase 顺序
2. Frame preparation owner
3. Query / lookup / allocator / dependency budget
4. Stream / store frame ownership
5. Deterministic merge policy
6. 唯一 structural playback gate
7. Debugger evidence gate

## 非目标

1. 不迁移所有 GameplayEffect。
2. 不完成 ActiveEffectStore 全生命周期。
3. 不推进 AutoChessDemo 业务拆分。
4. 不接入真实 Physics / Graphics 资源。

## 任务链看板

| 顺序 | 任务ID | 任务名 | 状态 | 文件 |
|---|---|---|---|---|
| 1 | T1-RuntimeCore-AM2B-A | Schedule / Phase Contract | 已完成 | [AM2B-A-SchedulePhase.md](AM2B-A-SchedulePhase.md) |
| 2 | T1-RuntimeCore-AM2B-B | Frame Arena 与 Query Budget | 已完成 | [AM2B-B-FrameArenaBudget.md](AM2B-B-FrameArenaBudget.md) |
| 3 | T1-RuntimeCore-AM2B-C | Stream Owner 与 Deterministic Merge | 已完成 | [AM2B-C-StreamOwner.md](AM2B-C-StreamOwner.md) |
| 4 | T1-RuntimeCore-AM2B-D | Structural Playback Gate | 已完成 | [AM2B-D-StructuralPlayback.md](AM2B-D-StructuralPlayback.md) |
| 5 | T1-RuntimeCore-AM2B-E | Debugger Evidence Gate | 已完成 | [AM2B-E-DebuggerEvidence.md](AM2B-E-DebuggerEvidence.md) |
| 6 | T1-RuntimeCore-AM2B-F | AM3 / AM5 Rebind 与交还验证 | 已完成 | [AM2B-F-RebindHandoff.md](AM2B-F-RebindHandoff.md) |

## 官方文档覆盖矩阵

| 任务 | 必读主题 | 必须交还的 DOTS 证据 |
|---|---|---|
| AM2B-A | `01`, `03`, `20`, `21` | SystemGroup 顺序、phase owner、结构变化权限 |
| AM2B-B | `02`, `10`, `20`, `21` | query contract、lookup update count、allocator owner、dependency wait |
| AM2B-C | `04`, `10`, `20`, `21` | stream owner、buffer pressure、deterministic merge、proof-only marker |
| AM2B-D | `03`, `04`, `06`, `20`, `21` | structural playback count、ECB command count、bulk query policy |
| AM2B-E | `02`, `03`, `04`, `06`, `09`, `10`, `21` | Debugger counters、Profiler / Journaling 对照 |
| AM2B-F | `20`, `21` | AM3 / AM5 action report 模板、验收和反哺状态 |

## 任务领取总规则

1. 当前所有子任务已完成，不推荐继续领取任何 AM2B 子任务。下一步推荐领取 AM3。
2. 每个任务行动报告都必须引用 `ISSUE-009`。
3. 每个任务交还后必须更新 `04-当前进度状态/迭代摘要.md`。

## 任务链状态流转规则

| 完成节点 | 下一推荐节点 | 必须确认的交还条件 |
|---|---|---|
| AM2B-A | AM2B-B | Schedule / phase contract 已可测试 |
| AM2B-B | AM2B-C | Query / lookup / allocator / dependency budget 已形成显式表 |
| AM2B-C | AM2B-D | Stream / store owner、clear / write / read / merge phase 已明确 |
| AM2B-D | AM2B-E | 结构变化入口收敛到唯一 playback gate |
| AM2B-E | AM2B-F | Debugger 能观测 frame backbone 证据 |
| AM2B-F | AM3 | AM3 / AM5 已重绑定新骨架 |
