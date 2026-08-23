# Shell Capability Contract Spec

## 结论

Shell 只有 Session、Command、Snapshot、Observation 四类能力；每类接口窄而实现深。不存在万能 `GASManager/RuntimeAdapter` 同时暴露 World、Catalog、Entity lifecycle、Debugger 与业务计算。

## Capability

| capability | 公开内容 | 深层实现 |
|---|---|---|
| `IGasRuntimeSession` | install、tick batch、dispose、status | World/Epoch、groups、ECB、catalog/layout、drain |
| `IGasCommandPort` | `Request*`、accept/reject | `SessionIngressGate`、BoundaryIngressJournal、RequestSequence、capacity、dedupe；pre-Fixed Ingress 再搬入 ECS inbox |
| `IGasSnapshotReader` | versioned immutable snapshot | projection/cache/staleness |
| `IGasObservationReader` | immutable BoundaryBatch cursor | ring retention、overflow/reconcile |
| `IGasDiagnosticsReader` | evidence snapshot | Kernel/Drain counters 与 capture refs |

接口可由一个内部 facade 聚合注入，但 facade 不新增写权限或 gameplay 语义；调用方不需要知道 ECS 类型。

## Session

Session install 固定 `SimulationEpoch`、tick rate、content/schema hash、AttributeLayout、TagCatalog、ScaleProfile 和 Boundary policy。`TickBatch` 推进完整 Simulation/FixedStep/Physics/GAS/EndFixed/Drain 路径；不得接受“只更新 GAS 子组”的快速模式。

Dispose 流程：停止 ingress → 完成已调度 Jobs → 产生/Drain terminal facts → 释放 runtime-created Blob/World → Epoch 失效。旧 handle/request 在新 Session 必须因 Epoch 不匹配而失败。

## Avatar 与派生 Entity

Shell 可以请求绑定/切换 Avatar，但 ASC 的逻辑 Owner、granted abilities 和 active effects 不因此销毁。公开 snapshot 使用 StableAvatarId/BindingGeneration。Projectile/Aura/Zone 通过 CommandPort 或预定义 spawn intent 创建，不把 Entity 返回给业务层作为权威 handle。

## 无头与 Scene 一致性

Headless、AutoChess、Scene runner 使用同一 Session capability。无头模式可以不加载资源，但必须执行相同 SimulationTick、EndFixed、Drain、Cue marker、Replay 和 Diagnostics 路径。

## API 错误模型

所有能力返回结构化状态：SessionMismatch、StaleHandle、UnknownDefinition、InboxFull、SnapshotStale、RangeDropped、Disposed 等。禁止 silent false、异常吞掉或让调用方通过 ECS 查询猜测结果。

## 禁止方向

- public `EntityManager/World/Entity/EntityQuery/DynamicBuffer/NativeArray`。
- Shell 同步调用 `CanActivate` 后据此修改 Core。
- Demo runner 持有 System 引用并手工分阶段 Update。
- 表现资源、日志或消费者确认控制 Session tick。
- Compatibility mode 同时运行旧/新 Runtime。
