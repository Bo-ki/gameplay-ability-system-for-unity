# Runtime v1 不可兼容迁移

> Owner：`02-主线任务树` | 状态：当前唯一可领取路线 | 最近更新：2026-08-29

本路线把当前 Runtime 一次性切换到 `01/17` 的单 Kernel / ASC slab / 单 Drain 目标。这里不复写设计规则。

`HEAD 10cbd256` + dirty worktree 的 N2-G0 第一轮已完成 `CFG-01` Luban→generated Catalog、`NUM-01` Resource-only gate、`CFG-02` canonical FNV64 minimum 与 `CFG-08` 输入/TOCTOU 门；但 **N2-G0 仍只部分完成，V0-V7 均未整体完成**。下一轮先收口 `ActiveGenerationRef`、四 hash 外部 package descriptor、candidate compile gate 与三类 proof，之后才能领取 TB-03 自然结束；顺序见[当前窗口](../../04-当前进度状态/当前窗口.md)与[N2 停顿审查计划](../../04-当前进度状态/N2-停顿审查盘点与下一轮计划.md)。

## 必读

- 当前事实：[Runtime v1 不可兼容迁移基线事实](../../00-当前架构事实/RuntimeV1不可兼容迁移基线事实.md)
- 第三轮增量事实：[Runtime v1 第三轮多 Agent 架构与性能审查事实](../../00-当前架构事实/RuntimeV1第三轮多Agent架构与性能审查事实.md)
- 目标规范：[17-GAS业务链路破坏性重划分 Spec](../../01-目标态架构共识/17-GAS业务链路破坏性重划分Spec.md)
- 二轮裁决与最低向量：[10B-08 真实业务链二轮审查](../../01-目标态架构共识/10B-AutoChess完整业务案例/10B-08-真实业务链二轮审查与疑点裁决Spec.md)
- 总任务入口：[02 主线任务树](../README.md)

## 执行顺序

### N1 实施检查点

- N1-1 已把 Target、Terminal、Route、Boundary 统一为 `Prepare -> FinalPublishFaultReduce -> single decision token -> Publish`；最晚 BoundaryPrepare fatal 会同时阻止最早 Target 与全部后续 durable publisher。
- RoutePrepare 同步修正 forwarded origin 二次消费、observed owner-gone 回执与 projected destination 死亡时的 source 回退容量证明。
- N1-2 已把所有 structurally-due period slot 改为忽略 Tick 内可变 inhibition 的保守准入上界：预留一次 claim、完整 ModifierRange，并前置拒绝 identity、ordinal 与 tick 溢出。
- N1-3 已建立三态 Tier-B 执行子清单与精确 TestId runner；N2-R0 因缺 authored Ability→生产 Wait 可达链将 TB-08 降为 Pending，当前为 4 Green / 0 ApprovedRed / 13 Pending。子清单 schema 2 明确不能冒充 Tier A/R3 master manifest；旧 Stacking 0-test 入口已退役并硬失败。
- N1-4 shadow 承载、Persistent 分配、lookup 构建与目标规模证据尚未完成；V0-V7 不因上述局部闭环自动盖章。
- N2-G0 第一轮已验证：AutoChess 完整 EditMode filter 1/1 Passed，首个 PlayerAttack `DamageApplied.Value == 12f`；`NUM-01` 相关回归 61/61；`CFG-02` 回归 26/26；官方 Luban+SourceGen 通过，raw JSON/sidecar/settings 输入身份、TOCTOU gate 与双跑 `InputHash 14ef9871…`、Core `ArtifactManifestHash 0583fb25…`、Demo `d6b3943a…` 稳定。
- N2-G0 当前基础设施边界：`CFG-05` 仅保证生成进程存活时 fail-closed 双根事务目录交换，不是 crash-atomic，且无 `ActiveGenerationRef`/candidate compile gate；`CFG-07` 仅有 manifest v2 逐产物/`.meta` SHA-256 与聚合 hash，没有外部 descriptor 或 `TypedContract/LayoutProof/CapacityProof`。

N0/N1 都不是新的迁移阶段，不改变以下依赖顺序，也不得被解释为 V3/V4/V5 已完成。

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

V0建立由`01/10`唯一拥有的versioned master manifest。当前机器文件只登记`TB-01..TB-17`，并被强制标记为不能授权 V0 的 Tier-B execution submanifest；Tier A、第三轮`R3-*`、input/semantic hash 总清单仍待补齐。`Pending`表示语义或覆盖尚未裁决并始终阻断；只有绑定真实失败、批准人、批准记录与可追溯证据的`ApprovedRed`才可被V0接受。缺 owner、缺 test source、重复、零发现或未经批准的 skipped 状态同样阻止V0。全部 required vector 最终 green 属于 V6 删除门、V7 与 release gate：

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
