# Unity DOTS 官方参考反推目标态与主线审查

任务ID：`T0/T1-AM1I`

状态：`已完成`

## 审查目的

本轮使用当前路线级 `UnityDOTS官方文档参考/` 重新审视 `01-目标态架构共识/` 与 `02-主线任务树/`，判断目标态架构和主线任务是否需要因 Unity DOTS 官方机制重新切分。

本轮不是重新推翻 GAS 概念模型，也不是把官方参考文档搬回目标态 Spec；重点是检查官方机制是否已经进入执行顺序、任务提示词和验收门槛。

## 输入文档

1. `UnityDOTS官方文档参考/README.md`
2. `UnityDOTS官方文档参考/主题/01-Entities系统与World.md`
3. `UnityDOTS官方文档参考/主题/02-查询遍历与Job.md`
4. `UnityDOTS官方文档参考/主题/03-结构变化-ECB-Enableable.md`
5. `UnityDOTS官方文档参考/主题/04-数据承载-Buffer-Chunk-Store.md`
6. `UnityDOTS官方文档参考/主题/06-Diagnostics-Profiler-Journaling.md`
7. `UnityDOTS官方文档参考/主题/09-Burst-编译-向量化-AOT.md`
8. `UnityDOTS官方文档参考/主题/10-Collections-Allocator-NativeStream.md`
9. `UnityDOTS官方文档参考/主题/20-GASRuntimeCore-API选型基线.md`
10. `UnityDOTS官方文档参考/主题/21-官方文档覆盖与流程闭环.md`
11. `01-目标态架构共识/00-总览Spec.md`
12. `01-目标态架构共识/03-RuntimeCore管线Spec.md`
13. `01-目标态架构共识/04-EffectCommand-SpecStream-AttributeDeltaSpec.md`
14. `01-目标态架构共识/05-ActiveEffectStoreSpec.md`
15. `01-目标态架构共识/07-RuntimeCoreDebuggerSpec.md`
16. `02-主线任务树/README.md`
17. `02-主线任务树/T1-GAS_ECS_Runtime/RuntimeCore重构.md`

## 对目标态架构的判断

四层架构模型可以保留。`Application Shell / Runtime Boundary / GAS Runtime Core / Definition & Generation` 的职责边界仍然符合当前目标态，尤其适合隔离 OOP、表现资源、Luban 配置和 Runtime Core 权威状态。

需要修正的是落地顺序：四层是工程边界，不是 DOTS 执行骨架。Runtime Core 不能只依靠 `EffectCommand`、`ActiveEffectStore` 等 GAS 语义对象逐个迁移；必须先形成 DOTS 原生的 frame backbone，明确 SystemGroup、query / lookup、allocator、dependency、deterministic stream、structural playback 和 Debugger evidence 的统一归属。

## 对主线任务树的判断

当前任务树已经把 Unity DOTS 官方参考纳入行动报告、API 选型和验收，但当前推荐仍偏向继续 `AM-5 Active Effect Store 重建`。这会让 Agent 继续在局部功能链上迁移旧实现，而不是先解决 Runtime Core 每帧执行骨架、结构变化屏障和性能证据归因。

因此主线不应继续直接推进 AM3 / AM5 的功能扩张。应新增并优先领取：

```text
GAS ECS Runtime - Runtime Core 重构 - Runtime Core Frame Backbone
```

该任务是 `AM-2 EffectCommand 与 SpecStream 契约` 后、继续扩展 `AM-3 / AM-5` 前的前置骨架任务。

## 核心偏差

| 偏差 | 当前表现 | 风险 |
|---|---|---|
| Spec 有 DOTS 规则，但任务顺序仍像旧管线迁移 | `03-RuntimeCore管线Spec` 已有 Frame Arena / Query Preparation，任务树却推荐继续 AM5 | Agent 容易继续扩展 owner-local mirror，而不是建立统一帧骨架 |
| Debugger 是目标态能力，但还不是每个 Runtime Core 任务的 evidence gate | T4 已有 counters baseline，AM3 / AM5 仍可用局部测试交还 | 无法稳定定位 query、lookup、allocator、structural、Burst warmup 的真实成本 |
| Frame Arena / Query Preparation 有概念但没有独立任务 owner | SystemGroup 映射表存在，但缺少可领取任务承载 | 后续每个 system 仍可能临时创建 query、临时分配、隐式等待 dependency |
| Structural Playback 已是目标态 phase，但没有成为唯一热路径屏障的验收门 | AM5 仍可能围绕旧 lifecycle mirror 扩展 | 结构变化成本继续分散，ObjectDisposed / handle invalidation 风险难归因 |

## 推荐新路线

```text
Freeze Safety Gate
-> Runtime Core Debugger baseline
-> Unity DOTS official calibration
-> EffectCommand / SpecStream contract
-> Runtime Core Frame Backbone
-> Deterministic command / spec / delta / fact stream
-> Debugger evidence gate
-> AM3 / AM5 feature migration
-> AutoChess scale validation
```

## Runtime Core Frame Backbone 任务边界

目标：

1. 建立 `GasRuntimeFramePrepareSystemGroup`，统一准备 query、lookup、type handle、frame scratch、allocator 和 dependency budget。
2. 明确 command / spec / delta / fact / active mutation stream 的 frame owner、clear phase、merge policy 和 deterministic ordering。
3. 建立 `GasStructuralPlaybackSystemGroup` 作为 Runtime Core hot path 唯一结构变化屏障。
4. 建立 Debugger frame backbone counters，至少输出 query count、lookup update count、allocator owner、structural playback count、stream merge policy、dependency wait 和 Burst / safety 口径。
5. 将 AM3 / AM5 后续功能迁移改为基于该 backbone 扩展，而不是继续沿旧 lifecycle mirror 添加局部 fast path。

非目标：

1. 不迁移所有 GameplayEffect。
2. 不完成 ActiveEffectStore 全生命周期。
3. 不推进 AutoChessDemo 业务拆分。
4. 不接入真实 Physics / Graphics 资源。

## 验收口径

1. 任务树当前推荐改为 `Runtime Core Frame Backbone`。
2. `T1-GAS_ECS_Runtime/RuntimeCore重构.md` 中存在可直接领取的任务提示词。
3. 目标态总览或 Runtime Core 管线 Spec 明确 `DOTS Backbone First` 约束。
4. `04-当前进度状态/当前窗口.md` 明确 AM3 / AM5 后续扩张前必须先完成 frame backbone。
5. 后续 AM3 / AM5 行动报告必须引用 frame backbone 的 SystemGroup、frame owner、structural playback gate 和 Debugger counters。

## 本轮反哺

| Owner | 反哺内容 | 状态 |
|---|---|---|
| `01-目标态架构共识` | 增加 `DOTS Backbone First` 作为 Runtime Core 落地顺序约束 | FedBack |
| `02-主线任务树` | 新增 `Runtime Core Frame Backbone` 推荐优先任务，并调整当前路线看板 | FedBack |
| `04-当前进度状态` | 当前推荐领取从继续 AM5 改为先领取 frame backbone | FedBack |
| `04-当前进度状态/迭代摘要` | 记录本轮官方参考反推路线重排 | FedBack |
