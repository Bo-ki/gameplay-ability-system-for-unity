# Shell Capability Contract Spec

## 结论

Shell 只有 Session、Command、Snapshot、Observation 四类能力；每类接口窄而实现深。不存在万能 `GASManager/RuntimeAdapter` 同时暴露 World、Catalog、Entity lifecycle、Debugger 与业务计算。

## Capability

| capability | 公开内容 | 深层实现 |
|---|---|---|
| `IGasRuntimeSession` | install、`TickBatch`、`BeginClose/PumpShutdown`、dispose receipt、status | World/Epoch、groups、ECB、catalog/layout、drain；物理关闭顺序由 03F 唯一定义 |
| `IGasCommandPort` | `Request*`、accept/reject/receipt | `SessionIngressGate`、Request ledger、capacity/dedupe、per-Battle close；协议由 16-02 唯一定义 |
| `IGasSnapshotReader` | versioned immutable snapshot + cut/freshness | projection/cache/staleness；协议由 16-05 唯一定义 |
| `IGasObservationReader` | immutable BoundaryBatch cursor | ring retention、overflow/reconcile |
| `IGasDiagnosticsReader` | evidence snapshot | Kernel/Drain counters 与 capture refs |

接口可由一个内部 facade 聚合注入，但 facade 不新增写权限或 gameplay 语义；调用方不需要知道 ECS 类型。

## Session

Session install 固定 `SimulationEpoch`、tick rate、content/schema hash、AttributeLayout、TagCatalog、ScaleProfile 和 Boundary policy。公开推进面固定为：

```text
TickBatch(absoluteElapsedTime, maxGameplayTicks) -> TickBatchReceipt
BeginClose(closeReason)                         -> CloseReceipt
PumpShutdown()                                  -> ShutdownProgress
Dispose()                                       -> DisposeResult(DisposedReceipt, ValidationResultSeal)
GetStatus()                                     -> SessionStatus
```

`TickBatch` 每次推进一次完整 outer `SimulationSystemGroup` 更新，并允许内部 `FixedStepSimulationSystemGroup` 运行 `0..maxGameplayTicks` 个固定步；不得接受“只更新 GAS 子组”的快速模式。即使本次是 0 FixedStep，也必须在 outer Simulation 的 FixedStep 之后执行一次 managed Drain：若已有 InFlight Batch，先重试同一 `BatchId`，不得运行 Kernel、不得增加 `SimulationTick`；没有 InFlight 时可以发布本次已有 staging/diagnostics。Boundary retry 由物理 outer batch 推进，不伪造 gameplay 时间。

Session 状态至少为：

```text
Running --BeginClose--> Closing --all receipts/cleanup ready--> ReadyToDispose
                         └─ retryable staging failure -> FinalDrainBlocked
FinalDrainBlocked --PumpShutdown succeeds------------> ReadyToDispose
ReadyToDispose --Dispose--> Disposing -> Disposed
```

`FinalDrainBlocked` 可在同一状态被 `PumpShutdown` 重试并在成功后进入 `ReadyToDispose`；它不是绕过 FinalDrain 的 fatal escape hatch。`BeginClose` 只发起幂等关闭并关闭 ingress，`PumpShutdown` 只推进 terminal/final drain/cleanup，不再运行 gameplay Kernel。只有 `ReadyToDispose` 才允许 `Dispose` 释放 World/Blob；释放完成后先生成 managed `DisposedReceipt`，再冻结 `ValidationResultSeal`，并作为同一不可分割 `DisposeResult` 返回。`ValidationResultSeal` 后不得再新增 gameplay、Boundary 或 teardown fact。唯一物理生命周期、Result/Disposed 边界见 [03F StructuralCommit 与 BoundaryProjection Spec](../03-RuntimeCore管线/03F-StructuralCommit与BoundaryProjectionSpec.md)；Request ledger 与 per-Battle gate 见 [16-02](./16-02-BoundaryCommand与CoreCommandResolveSpec.md)。旧 handle/request 在新 Session 必须因 Epoch 不匹配而失败。

## Avatar 与派生 Entity

Shell 可以请求绑定/切换 Avatar，但 ASC 的逻辑 Owner、granted abilities 和 active effects 不因此销毁。公开 snapshot 使用 StableAvatarId/BindingGeneration。Projectile/Aura/Zone 通过 CommandPort 或预定义 spawn intent 创建，不把 Entity 返回给业务层作为权威 handle。

## 无头与 Scene 一致性

Headless、AutoChess、Scene runner 使用同一 Session capability。无头模式可以不加载资源，但必须执行相同 SimulationTick、EndFixed、Drain、Cue marker、Replay 和 Diagnostics 路径。

## API 错误模型

所有能力返回结构化状态：`SessionMismatch`、`StaleHandle`、`UnknownDefinition`、`InboxFull`、`IdempotencyConflict`、`IdempotencyHistoryExpired`、`BattleClosed`、`SessionClosing`、`SessionFaulted`、`SnapshotStale`、`RangeDropped`、`FinalDrainBlocked`、`Disposed` 等。CommandPort receipt 必须携带 ledger 分配结果；Snapshot freshness/cut 由 [16-05](./16-05-SnapshotIdentityApiHealthSpec.md) 定义。禁止 silent false、异常吞掉或让调用方通过 ECS 查询猜测结果。

## 禁止方向

- public `EntityManager/World/Entity/EntityQuery/DynamicBuffer/NativeArray`。
- Shell 同步调用 `CanActivate` 后据此修改 Core。
- Demo runner 持有 System 引用并手工分阶段 Update。
- 表现资源、日志或消费者确认控制 Session tick。
- Compatibility mode 同时运行旧/新 Runtime。
