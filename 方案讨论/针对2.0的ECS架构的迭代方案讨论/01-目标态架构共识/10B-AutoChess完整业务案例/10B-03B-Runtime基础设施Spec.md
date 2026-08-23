# 10B-03B Runtime 基础设施 Spec

## 结论

AutoChess 只使用通用 `GasRuntimeSession + GasCommandPort + Snapshot/ObservationReader`。一个 World 同时最多一个 active GAS Session，但该 Session 可以承载多个 `BattleInstance`。旧的逐组 ticker、Demo 自定义 Core System 与直接 ECS observation 不属于目标态。

## Session、BattleInstance 与可见性

```text
Session: Install -> SpawnPending -> Ready -> Running -> Terminalizing -> FinalDrain -> Disposing -> Disposed
         \-> Faulted -> FinalDrain/Disposing
Battle:  SpawnPending -> Ready -> Running -> BattleTerminal -> OutcomeFrozen -> CleanupAudited
```

- `SpawnBatch` setup 只建立 Pending ASC；下一 Kernel SpawnFinalize 先在 scratch 完成整批 `SpawnInitializationTransaction`，再整批发布 ASC；失败时全有或全无，不暴露半初始化单位。
- 单位只有在稳定身份、Attribute init、initial tags、default grants 与 initial effects 全部成功后才进入 `Ready`；`Ready` 前 AI/Activation ingress 不可见。v1 initial effect 只能是 self-target、生成期闭合/静态有界的 bootstrap program，否则 publish/bake fail。
- 单个 `BattleInstance` 达成终局时只关闭该战局的新 gameplay ingress；其他 BattleInstance 继续运行。
- 仅当全部 BattleInstance 已终局，或收到显式 Session stop，Session 才进入 `Terminalizing`。
- `UnitDeath`、`BattleTerminal`、`SessionTerminal` 是三个不同事实，不得用同一个布尔状态替代。

## Runner

`TickBatch` 更新完整 Simulation/FixedStep/Physics/GAS/EndFixed/Drain 链。一个渲染帧 0..N fixed updates；只有 Session Ready/Running 的 gameplay update递增 SimulationTick，SpawnFinalize/teardown maintenance update不计 gameplay tick。catch-up 后返回 BoundaryBatch/Diagnostics 元数据。Headless 和 Scene 用同一实现。Batch 边界不是语义边界；warmup只能位于 `Ready` 前的 setup scope，不得混入 gameplay tick、hash 或业务计数。

## Ingress

AI/BattleCommandDrive 只生成 attack/ability/move/cancel intent，复制稳定目标与 payload 到 CommandPort。Damage 公式不是 Demo System；它作为 Definition 的 pure execution evaluator 由 Kernel 调用。

## Observation

Battle report、winner、damage summary、finisher、Cue marker 和 Replay 都消费同一 immutable Boundary batch/read model。消费者不能持有 Entity、query、buffer 或触发 Drain。Managed staging 接管成功后才允许清 ECS outbox。

## 终局、FinalDrain 与 teardown

1. 致死所在 logical tick 必须完成完整 DAG，不得在中间 phase 提前冻结结果。
2. 关闭该 BattleInstance 新 gameplay ingress 后，Boundary 执行 gameplay `FinalDrain`；全部 gameplay 事实接管成功后，才冻结 `BattleOutcome` 与 `BattleSemanticHash`。
3. Battle cleanup 与 Session teardown 产生 `FactPlane=TeardownAudit` 的事实，执行 ASC/Buffer/Allocator/Cue/Boundary 零残留审计；每个事实仍以正交的 Asc/Battle/Session `ScopeKind` 选唯一 outbox。
4. teardown 不得改写已冻结 battle hash；最终 `ValidationResult` 必须在 teardown audit 后返回。
5. 不允许用 wall-clock delay 或固定追加 gameplay tick 代替 FinalDrain；没有下一 logical tick 时，由 teardown 直接处理 Accepted removal 与残留清理。

## Session 配置

- Unity/package/content/schema hash。
- fixed tick rate/time policy。
- AttributeLayout/TagCatalog。
- AutoChess Scenario/ScaleProfile。
- Boundary ring/overflow policy。

## 验收

- Spawn Pending -> Ready 对 AI/Activation 的可见性全有或全无；Initialize/Grant 失败产生 typed outcome。
- Runner 静态扫描零内部 Group/System 缓存或 direct Update。
- Scene/Headless 相同输入得到相同 winner、damage、finish tick 与 semantic hash。
- 0 tick 输入保留、N tick catch-up 无重复消费或 fact 丢失。
- 无表现资源时 Cue marker/Drain/Replay/Diagnostics 完整。
- 单 BattleInstance 终局不关闭同 Session 其他战局 ingress。
- FinalDrain 前不冻结 BattleOutcome；teardown audit 前不返回最终 ValidationResult。
