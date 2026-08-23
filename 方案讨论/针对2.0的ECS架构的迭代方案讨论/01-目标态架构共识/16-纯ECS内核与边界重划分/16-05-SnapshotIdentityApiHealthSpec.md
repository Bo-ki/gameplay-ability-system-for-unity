# Snapshot、Identity 与 API Health Spec

## Identity

长期身份分三类：

1. Session：`SimulationEpoch`。
2. ASC/Actor：`AscInstanceId`、OwnerStableId、AvatarStableId、BindingGeneration。
3. 领域实例：强类型 generational handle、EffectSpec/Application/Contributor/Continuation/Cue/Causality identity。

`CausalityId` 只参与追踪、排序与环检测，不参与 prediction accept/reject/rollback。v1 schema/API 不出现 PredictionKey、IsPredicted、ack 或 undo。

## Snapshot

Snapshot 是 immutable value view，至少携带 stable identity、content hash 与：

```text
SnapshotCut = (
    SimulationEpoch,
    SimulationTick,
    ProjectionVersion,
    BoundaryWatermark | ManagedBoundarySequence)
```

`SnapshotCut` 表示 managed cache 已完整应用到哪个 Boundary cut，而不是“最后一次读取 ECS 的时间”。同一 Battle/多 ASC snapshot 必须由同一 staging transaction 原子发布：调用方只能看到旧 cut 或新 cut，不能看到部分 ASC 已推进、部分仍停留。Snapshot 可以落后一或多个 tick；API 必须返回 freshness/staleness，而不是 live buffer 引用。

```text
IGasSnapshotReader.Read(scope, minimumCut?)
    -> Fresh(snapshot, actualCut)
     | Stale(snapshot?, actualCut, requiredCut)
     | RangeUnrecoverable(oldestRecoverableCut, requiredCut)
```

`IGasSnapshotReader` 只读 managed immutable cache，禁止在 miss/stale 时 fallback 到 `EntityManager/EntityQuery/DynamicBuffer`。Drain 在接受一个 staging batch 时，必须先把该 batch 的 read-model delta 应用到 cache 并原子推进 `SnapshotCut`，再把可能丢失的 observation ring cursor 对消费者可见；这样 overflow consumer 才能以 watermark 请求可证明的 snapshot reconcile。该 drain/cache/ring 的物理顺序由 [06 Observation/Presentation/Replay Spec](../06-Observation-Presentation-ReplaySpec.md) 唯一定义，本节只拥有 Snapshot API 与 cut 语义。

ASC/Battle 销毁不是立即忘记 identity：terminal tombstone 至少保留到所属 Battle outcome seal 及 Session retention policy 都允许回收，使迟到 reader 能区分“已销毁”与“从未存在/历史已丢失”。Cue reconcile snapshot 必须以同一 cut 携带完整 active cue-cycle 集合；字段与异步资源校验由 06 唯一定义。瞬时 `Executed` cue 不能从 active snapshot 重建，丢失范围必须通过 `RangeUnrecoverable`/observation overflow 显式暴露。

ReadModel snapshot 与 Capture snapshot 不同：前者服务 Boundary 观察，后者服务 Core magnitude 语义；不得共享 carrier 或生命周期。

## API Health 分类

| 分类 | 健康 | 红线 |
|---|---|---|
| Query | Kernel 内缓存/刷新明确，按 chunk/owner 分区 | Shell/Debugger/generated code 隐藏查询 |
| Allocator | tick scratch 用 WorldUpdateAllocator | 自定义 FrameArena、跨 tick temp reference |
| Dependency | 一个 Kernel DAG、最终 handle；只允许 03F 列名的 fence | Kernel lane 内 Complete、隐式 EntityManager completion、未列入 allowlist 的 Complete/CompleteAllTrackedJobs |
| Mutation | target single writer、standard EndFixed ECB | random cross-owner write、分散 EntityManager structural write |
| Boundary | cleanup outbox + one Drain | consumer 直读/清空 Core buffer |
| Identity | Epoch + typed generation | raw Entity public、untyped slot id |
| Definition | immutable Blob + pure glue | managed hot lookup、generated lifecycle |

## Evidence

API health report 必须包含 owner、调用位置、频率、scale profile、触发 reason 与违规计数。字符串 grep 是静态门之一，不替代 Profiler/Journaling、功能测试和 deterministic trace。

## 验收

- World 重建后旧 handle/request 全部失效。
- Avatar 重绑不改变 ASC identity，旧 BindingGeneration 的表现请求可识别为 stale。
- Snapshot consumer 无法写回 Core；snapshot staleness 显式。
- multi-ASC/Battle snapshot 只观察到单一原子 cut；ring overflow 后能按 watermark reconcile，历史不足时返回 `RangeUnrecoverable`。
- ASC destroy tombstone、active cue-cycle snapshot 与瞬时 Executed 丢失语义测试。
- runtime/generated/public API 扫描无 Prediction/raw Entity/writable buffer 越界。
