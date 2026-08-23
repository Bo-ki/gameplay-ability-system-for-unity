# 判定准则与 Owner Map Spec

## 四问

任何新增机制必须回答：

1. 谁是唯一 writer/生命周期 owner？
2. 数据是 immutable、tick-local、owner-local authority、pending work、Boundary batch 还是 evidence？
3. 调用方最少需要知道什么？
4. 正确性、确定性、容量和性能如何验证？

不能明确回答的类型不得进入 Runtime Core。

## Owner Map

| 数据 | 唯一 owner | 生命周期 | 禁止方向 |
|---|---|---|---|
| Definition/Catalog/Layout | Catalog lifetime | Session | Core 修改、managed hot lookup |
| Boundary ingress journal | `GasCommandPort` + `SessionIngressGate` | Accepted 后至搬运/终结，跨渲染帧持久 | 未 append 完整 record 就返回 Accepted、CommandPort 直写 ECS DynamicBuffer |
| ECS `BoundaryCommandInbox` | `GasCommandIngressSystem` 唯一 append；Kernel seal/consume | 搬运后至消费/终结，跨 fixed tick 持久 | 其他 writer、Kernel 读期间 tail append、直接执行 gameplay |
| Tick scratch | GasTickKernelSystem | 单次 SimulationTick | singleton/cross-System 保存 |
| Granted Ability / Activation / Continuation / ActivationOwnedContribution / EmittedApplicationRef | source/owner ASC | 跨 tick | target ASC、Ability Entity 或 global store 双权威 |
| Ability Subscription | observed ASC（携带 recipient owner/activation/continuation generation） | 跨 tick，直到完成、取消或解绑 | recipient 直接修改 observed ASC；晚到投递跳过 generation 校验 |
| ActiveEffect / Payload / Capture / Aggregator | target ASC | 跨 tick | source ASC、Effect Entity 或 global store 双权威 |
| Attribute/Tag | target ASC | ASC 生命周期 | per-field component mirror |
| Pending reaction | target ASC/session ingress | DueTick 前 | 从 stale slot 重建 payload |
| Boundary outbox | 唯一 scoped cleanup owner：ASC fact→ASC，Battle/Session fact→Session | Drain 前 | 同一 fact 双写、随机代表 ASC、多消费者竞争 clear |
| Immutable batch/ring | GasBoundaryDrainSystem | Boundary policy | 反写 Core |
| Diagnostics evidence | Kernel/Drain counters 的只读导出 | snapshot | Debugger 拥有 gameplay query/write |

## 升格门

逻辑 stage 只有在无法由同一 Kernel DAG 表达、且确实需要独立 Unity update/lifecycle/managed 边界时才可升格为 System。业务概念、代码文件数量、Profiler 展示便利都不是新增 SystemGroup 的理由。

## 双权威门

任何“兼容期镜像”若可独立写入、参与 query 或影响结果，就是第二权威。v1 删除而不是兼容 Ability Entity、legacy GE Entity、global ActiveEffect index、per-attribute component 和多个 Boundary carrier。
