# 数据流 / System 生命周期：API 与 EX-GAS 解读

> 精确基线：Unity 6000.3 / Entities 1.4.6
> 结论：v1 用“整数 Tick + 单 Kernel DAG + ASC 持久槽 + cleanup outbox”闭合生命周期

## 1. 官方机制

### 1.1 System 生命周期

System 具有创建、运行、停止与销毁阶段；`RequireForUpdate`/query 可以控制是否更新。停止更新不等于已完成外部资源清理，`OnDestroy` 仍需完成 owner 的 teardown。

System 访问数据时：

- TypeHandle/ComponentLookup/BufferLookup 需要按更新周期刷新；
- Singleton API 不自动替所有自定义 Job 依赖兜底；
- early-out 不能丢失已经调度的 `JobHandle`；
- `SystemAPI.Query` 是生成的当前迭代表达式，不能缓存枚举器跨帧复用；
- enableable component 会参与 query 匹配与安全依赖。

官方快照：

- [System concepts](../../官方文档原件/com.unity.entities/Documentation~/concepts-systems.md)
- [Access data](../../官方文档原件/com.unity.entities/Documentation~/systems-access-data.md)
- [Schedule jobs](../../官方文档原件/com.unity.entities/Documentation~/systems-scheduling-jobs.md)
- [SystemAPI Query](../../官方文档原件/com.unity.entities/Documentation~/systems-systemapi-query.md)
- [Lookup data](../../官方文档原件/com.unity.entities/Documentation~/systems-looking-up-data.md)

关联规则：[CASE-46](./CASE-46.md)、[CASE-48](./CASE-48.md)、[PRF-19](./PRF-19.md)、[PRF-29](./PRF-29.md)、[PRF-30](./PRF-30.md)、[PRF-33](./PRF-33.md)。

### 1.2 Cleanup 生命周期

Destroy 含 cleanup component 的 Entity 时，普通数据被移除而 cleanup 数据保留；移除最后一个 cleanup component 后 Entity 最终消失。shutdown 也必须处理仍存在的 cleanup 数据。

官方快照：[Cleanup lifecycle](../../官方文档原件/com.unity.entities/Documentation~/components-cleanup-introducing.md)；关联 [CASE-15](./CASE-15.md)。

## 2. EX-GAS Tick 数据流

```text
External Requests
  -> [optional ingress]
  -> Tick Command Freeze + Canonical Order
  -> Validate / Target Resolve
  -> Ability + Continuation
  -> Closed Bounded Pre-Apply Expansion
  -> Target Buckets
  -> Target-local Apply + Stabilization
  -> Dirty Finalize
  -> Stable Core Fact Merge
  -> Boundary Projection
  -> EndFixed Structural Playback
  -> Single Managed Drain
```

`GasTickKernelSystem` 拥有从 freeze 到 ECB record 的 Job DAG；managed drain 不回写当 Tick Core。

## 3. 时间与输入截止

- `GasFixedTickSystemGroup` 每渲染帧更新 `0..N` 次。
- Session 使用整数 `SimulationTick`，Tick Rate 创建后不可变并进入 hash。
- ingress 为每 Tick 定义明确 cutoff；cutoff 后到达的请求进入下一 Tick。
- Command 稳定 key 不依赖线程/到达墙钟顺序，至少包含 Tick、source stable id/sequence 与 kind。
- manual World 由 TickBatch owner 更新完整 FixedStep 父链。

浮点 seconds 只用于配置烘焙/展示换算；运行时 duration/period/cooldown 使用 Tick/end tick。

## 4. 权威状态与 writer

| 状态 | owner | writer |
|---|---|---|
| Attribute/Tag | target ASC | target-local Apply/Stabilize/Finalize |
| Granted Ability/Continuation | owner ASC | owner-local Ability lane |
| Active Effect | target ASC | target-local writer |
| Pending reaction Command | ASC/Session queue | stable Fact→next-Tick route |
| Boundary Fact | ASC cleanup outbox | owner/target-local projection |
| Tick scratch | Kernel | Job DAG partition owner |

target 之间并行；同 target 一个逻辑 writer。Lookup 可用于读其他 ASC 的 live capture，但写入必须遵循 owner 分区，不能用 `NativeDisableParallelForRestriction` 掩盖重叠。

## 5. same-tick 与 next-tick

same-tick 只允许：

1. Ability 直接声明的输出；
2. Definition build 已证明 closed、finite、acyclic 且有静态最大深度的 pre-apply effect program。

仅“无环”不足以证明运行时扩展有界。Apply 后 Core Fact reaction 默认写下一 Tick Command；late ingress 同样下一 Tick。这样 stable Fact merge 是 DAG 终点，不产生未受控再入。

EndFixed 结构结果也在下一 GAS Tick 可见。Activate/Commit/Cancel 若需同 Tick，只更新既有 slab，不能等待 Entity 创建。

## 6. Target-local stabilization

ongoing requirement、inhibition 与 tag grant/remove 会相互改变状态。v1 在 target-local writer 内反复处理 dirty dependency worklist直到稳定：

- 生成期检查带符号依赖、环、程序/扩展界；
- 运行时按稳定 key 处理 worklist；
- 达到稳定后才 finalize Attribute/Tag 并发 Fact；
- 超过证明界是 deterministic fatal；
- 禁止固定 pass 静默截断或发布半稳定状态。

具体 worklist container/算法由 ScaleProfile 决定，不改变语义。

## 7. ASC 生命周期

### 7.1 Create

EndFixed spawn/初始化路径显式建立固定 ASC archetype、Attribute/Tag Buffer、slab headers 与 cleanup outbox。注册为可接收 Command 只能发生在布局完整后。

### 7.2 Live

长期 Ability/Continuation/Effect 全在 ASC slab；Tick scratch 不跨 Tick。OwnerActor/AvatarActor 可分离，换 Avatar 不迁移 ASC 权威状态。

### 7.3 Destroy

1. invalidate ASC identity/generation，停止新 Command；
2. 写 cancel/remove/death Boundary Fact；
3. 标准 EndFixed Destroy；
4. cleanup shell 由单 managed drain 接管；
5. 成功后清空事实，并把 remove cleanup buffer 记录到下一次标准 EndFixed，Entity 随后最终消失。

`EntityManager.Exists` 不能代表业务存活。

### 7.4 Shutdown

完成 producer dependency → 最后 EndFixed/必要结构提交 → 最后 drain live/shell → 移除 cleanup → 释放 Blob/Persistent/managed registry。

headless 也执行同一路径。

## 8. Boundary 与多消费者

Core 内只有 per-ASC cleanup outbox 和一个 drain。drain 按 `SimulationTick + stable sequence` 接管到 managed dispatch queue：

- live outbox 成功接管后 clear；
- shell 成功接管后 clear，并在下一标准 EndFixed remove cleanup buffer；
- 失败保留并显式报告；
- Cue 用 `CueInstanceKey` 幂等配对；
- UI/Audio/Log/Network 多消费者和 retention 位于 managed 层。

v1 不在 ECS 维护每消费者 cursor，也不同时建立 session persistent outbox。

## 9. Job/lifecycle 红线

- phase `Complete()` 或 early-out 丢 JobHandle。
- 缓存 Query/Lookup/TypeHandle 跨更新而不刷新。
- scratch 跨 Tick/System。
- 同 target parallel random write。
- Fact 顺序依赖 Job 完成顺序。
- cleanup outbox 未接管就清除。
- shutdown 先释放 Blob/Persistent owner 再完成 Job/drain。
- runner 只手工 Update GAS 子组。

## 10. Profile 与验收

ScaleProfile 决定容量、算法和数值门槛，至少采集 Tick backlog、target skew、slab high-water、scratch 峰值、stabilization、sync、ECB 与 drain。

必须覆盖：

- `0/1/N` Tick；
- late ingress 与 next-Tick reaction；
- same-tick bounded program；
- hot target 与不收敛 fault；
- ASC 同 Tick destroy；
- world shutdown；
- 标准/manual/headless 三种驱动的一致性。
