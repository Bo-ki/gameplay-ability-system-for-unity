# Snapshot、Identity 与 API Health Spec

## Identity

长期身份分三类：

1. Session：`SimulationEpoch`。
2. ASC/Actor：`AscInstanceId`、OwnerStableId、AvatarStableId、BindingGeneration。
3. 领域实例：强类型 generational handle、EffectSpec/Application/Contributor/Continuation/Cue/Causality identity。

`CausalityId` 只参与追踪、排序与环检测，不参与 prediction accept/reject/rollback。v1 schema/API 不出现 PredictionKey、IsPredicted、ack 或 undo。

## Snapshot

Snapshot 是 immutable value view，至少携带 Epoch、SimulationTick、projection version、content hash 和 stable identity。它可以落后一或多个 tick；API 必须返回 freshness/staleness，而不是 live buffer 引用。

ReadModel snapshot 与 Capture snapshot 不同：前者服务 Boundary 观察，后者服务 Core magnitude 语义；不得共享 carrier 或生命周期。

## API Health 分类

| 分类 | 健康 | 红线 |
|---|---|---|
| Query | Kernel 内缓存/刷新明确，按 chunk/owner 分区 | Shell/Debugger/generated code 隐藏查询 |
| Allocator | tick scratch 用 WorldUpdateAllocator | 自定义 FrameArena、跨 tick temp reference |
| Dependency | 一个 Kernel DAG、最终 handle | phase Complete、CompleteAllTrackedJobs 常态化 |
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
- runtime/generated/public API 扫描无 Prediction/raw Entity/writable buffer 越界。
