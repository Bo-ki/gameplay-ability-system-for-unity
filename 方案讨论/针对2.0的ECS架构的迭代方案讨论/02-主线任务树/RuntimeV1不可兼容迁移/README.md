# Runtime v1 不可兼容迁移

> Owner：`02-主线任务树` | 状态：当前唯一可领取路线 | 最近更新：2026-08-24

本路线把当前 Runtime 一次性切换到 `01/17` 的单 Kernel / ASC slab / 单 Drain 目标。这里不复写设计规则。

## 必读

- 当前事实：[Runtime v1 不可兼容迁移基线事实](../../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- 第三轮增量事实：[Runtime v1 第三轮多 Agent 架构与性能审查事实](../../00-当前架构事实/RuntimeV1第三轮多Agent架构与性能审查事实.md)
- 目标规范：[17-GAS业务链路破坏性重划分 Spec](../../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)
- 二轮裁决与最低向量：[10B-08 真实业务链二轮审查](../../01-目标态架构共识/10B-AutoChess完整业务案例/10B-08-真实业务链二轮审查与疑点裁决Spec.md)
- 总任务入口：[02 主线任务树](../README.md)

## 执行顺序

```text
V0 -> V1 -> V2 -> V3 -> V4 -> V5 -> V6 -> V7
```

V2-V4 是同一个不可分割的 Runtime authority 集成窗口：允许在隔离分支分提交，但旧/新 Runtime 不得同时注册、双写或通过配置选择。V6 删除门通过前不得发布。

| 任务 | 结果 |
|---|---|
| [V0](V0-语义冻结与真实测试基线.md) | 真实测试与当前语义基线 |
| [V1](V1-DefinitionCatalog与稳定身份.md) | phase-aware Catalog、Owner/Avatar、stable identity |
| [V2](V2-ASC稳定Slab与Handle.md) | ASC-local non-compacting authority |
| [V3](V3-单TickKernel与AbilityCommit.md) | 单 Core writer、标准 EndFixed、Ability transaction |
| [V4](V4-EffectAttributeTag语义闭合.md) | Effect/Attribute/Tag 语义闭环 |
| [V5](V5-BoundaryDrainCue与销毁交接.md) | Accepted Request ledger、per-Battle双cut+SnapshotCut seal、单Drain/Cue、Disposed/ValidationResultSeal |
| [V6](V6-AutoChess迁移与旧链删除.md) | Demo 迁移与旧链零运行命中 |
| [V7](V7-确定性规模与Profiler门.md) | Release evidence |

## ValidationVectorManifest 消费

V0建立由`01/10`唯一拥有的versioned manifest。`TB-01..TB-17`覆盖第二轮业务向量，第三轮`R3-*`覆盖配置、target transaction、ingress/battle、runner/result与规模缺口；任何entry缺owner/test source/evidence或最终非green都阻止V6删除门与V7 release evidence：

| `10B-08` 向量 | 实现/验收 owner |
|---|---|
| 1-4：资源竞争、双向 Commit/Cancel、Instant cooldown、self GE barrier | V3 |
| 5-7：target RYW requirement/immunity、committed-work-wins、AliveOnly/overkill | V3 + V4 |
| 8：Wait sample/register/cancel/ack | V3 |
| 9：9203 全时序与 period self-delete | V4 |
| 10-11：LeaveGranted、Cue active cycle | V4 + V5 |
| 12：Avatar rebind/FrozenSpatial | V1 + V3 + V6 |
| 13：双杀/平局与 multi-BattleInstance 终局隔离 | V3 + V5 + V6 |
| 14：SpawnInitializationTransaction整批Ready/失败零发布 | V1 + V3 + V4 |
| 15：Admission zero-write与FaultClose exact membership | V3 + V5 |
| 16：scoped outbox retry/late tail/NoFactReceipt/无下一tick teardown | V5 + V6 |
| 17：ValidationResultSeal后零事实与TickBatch partition semantic等价 | V5 + V7 |
| `R3-CFG-*` | V1 + V6 + V7 |
| `R3-CMT/STB/ING/BTL-*` | V3 + V4 + V5 |
| `R3-TCK/SNP/CUE/RSL-*` | V5 + V6 + V7 |
| `R3-GRT/LIV/REF-*` | V4 |
| `R3-HOT-*` | V7 |

## 全局停止条件

出现任一情况必须停止并回到对应前置任务：

1. 需要兼容旧 Ability/GE handle 或按 Definition 选择 backend。
2. 旧/新 authority 同时写同一 gameplay 状态。
3. Kernel phase 需要跨 System NativeContainer 或主线程 `Complete()` 才能串联。
4. Boundary consumer 需要 raw Entity/World/EntityManager 或直接清 ECS buffer。
5. 没有真实测试却准备删除旧链。
6. Target stabilization需要先写durable再判断fatal，或Runtime/配置发布需要fallback/双事实源。

## 交还规则

每个任务交还必须列出：完成的验收项、未完成项、删除清单、验证命令/结果、反哺 `00/01/02/04` 的路径。只有后续任务依赖的事实发生变化时才更新 `00`，不得把计划写成当前完成态。
