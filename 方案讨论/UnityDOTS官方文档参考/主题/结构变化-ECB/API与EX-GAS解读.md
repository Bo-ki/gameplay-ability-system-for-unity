# 结构变化 / ECB：API 与 EX-GAS 解读

> 精确基线：Unity 6000.3 / Entities 1.4.6
> 结论：EX-GAS v1 只在标准 EndFixed 提交真实结构变化

## 1. 官方机制

### 1.1 什么是结构变化

创建/销毁 Entity、增删 Component/Buffer、改变 archetype 属于结构变化。结构变化会影响 chunk 布局与直接引用有效性；热点中直接通过 EntityManager 执行会形成同步点风险。

修改已存在 Component/Buffer 的值、启停预挂载 enableable component 不改变 archetype，不属于结构变化。

官方快照：

- [Manage structural changes](../../官方文档原件/com.unity.entities/Documentation~/systems-manage-structural-changes.md)
- [EntityCommandBuffer](../../官方文档原件/com.unity.entities/Documentation~/systems-entity-command-buffers.md)

### 1.2 ECB

- ECB 记录命令并在之后 playback，不是即时变化。
- ECB system 管理 command buffer 的 allocator、producer completion 与 playback。
- ParallelWriter 需要 sort key；确定性不能依赖 worker 物理记录顺序。
- `AppendToBuffer` playback 时目标 Buffer 必须存在。
- playback 后，之前取得的直接 Component/Buffer/EntityQuery 结果可能需要重新获取；TypeHandle/Lookup 也按更新周期刷新。

官方快照：

- [Use ECB](../../官方文档原件/com.unity.entities/Documentation~/systems-entity-command-buffer-use.md)
- [Automatic playback](../../官方文档原件/com.unity.entities/Documentation~/systems-entity-command-buffer-automatic-playback.md)
- [ECB playback](../../官方文档原件/com.unity.entities/Documentation~/systems-entity-command-buffer-playback.md)

关联规则：[ECB-01](./ECB-01.md)、[ECB-02](./ECB-02.md)、[ECB-03](./ECB-03.md)、[SC-01](./SC-01.md)、[SC-02](./SC-02.md)。

## 2. EX-GAS v1 分类

| GAS 操作 | 是否结构变化 | v1 实现 |
|---|---:|---|
| Attribute Base/Current 写 | 否 | 固定 Buffer 元素 |
| Tag exact/inclusive 写 | 否 | 固定 Buffer 元素 |
| Ability Activate/Commit/Cancel | 否 | owner slab 槽状态 |
| Continuation resume/end | 否 | owner slab 槽状态 |
| Effect apply/stack/inhibit/remove | 否 | target slab 槽状态 |
| Boundary Fact append | 否 | 已存在 cleanup Buffer |
| enable/disable work marker | 否 | 预挂载 enable bit |
| ASC/Projectile/Hitbox Entity create/destroy | 是 | 标准 EndFixed ECB |
| 增删 Component/Buffer | 是 | 标准 EndFixed ECB |

禁止把业务 lane 结尾当成结构 commit，也禁止为 Ability/Effect slot 默认创建 Entity。

## 3. 唯一 playback 点

所有真实结构变化记录到 `EndFixedStepSimulationEntityCommandBufferSystem`。v1 不创建自定义 `EndGAS...ECBSystem`，也不在 Kernel lane 中途 playback。

结果可见性：

- Kernel 内不能读取刚记录但未 playback 的 Entity/Component。
- EndFixed 之后的系统/下一 GAS Tick 可见结构结果。
- GAS 默认 PostPhysics，因此新 Physics/Transform 结构最早参与下一 Physics Tick。
- Activate/Commit/Cancel 若要求 same-tick，只能在已存在的 ASC slab/Buffer 内完成。

记录 ECB 的 Job 必须把 producer 依赖完整登记给标准系统；不能 early-out 丢失已排 JobHandle。

## 4. Cleanup outbox

官方 cleanup 语义：

- Destroy 含 cleanup component 的 Entity 时，非 cleanup component 被移除；
- Entity 保留到最后一个 cleanup component 被移除；
- cleanup component 不会从 prefab entity 复制到实例，也不会跨 World copy。

官方快照：

- [Introducing cleanup components](../../官方文档原件/com.unity.entities/Documentation~/components-cleanup-introducing.md)
- [Create/use cleanup components](../../官方文档原件/com.unity.entities/Documentation~/components-cleanup-create.md)

EX-GAS 选择 per-ASC `ICleanupBufferElementData` outbox：

1. ASC spawn 显式添加 Buffer；
2. Kernel 在 Destroy 前写全自包含 Boundary Fact；
3. EndFixed Destroy 后形成 shell；
4. 单 managed drain 接管 live/shell outbox；
5. live 成功后 clear；shell 成功后 clear，并把 remove cleanup buffer 记录到下一次标准 EndFixed。

若 shutdown 不再执行 FixedStep，完成 producer 与最后 drain 后可由 teardown 直接移除 cleanup buffer；这是停止 World 的清理例外，不是 Runtime 新增 playback phase。

cleanup shell 的 `EntityManager.Exists` 可能仍为真，但业务 ASC identity 已消失。业务存活必须校验身份/generation。

## 5. manual World

standard EndFixed 是 FixedStep 父链的一部分。独立/AutoChess runner 必须更新完整 `FixedStepSimulationSystemGroup`；手工只更新 GAS Kernel/子组会漏 playback、allocator 或 Physics 顺序。

批次结束后再调用单 managed drain。headless 也必须 drain/清 shell，可以使用 no-op/log sink，但不能省略生命周期完成。

## 6. 并行与确定性

- 每个并行 producer 使用明确 ECB/ParallelWriter 契约。
- sort key 必须来自稳定逻辑索引/key，不使用 worker id 或完成顺序。
- 对同一 Entity 的冲突命令在 record 前 canonicalize；不能依赖相同 sort key 的偶然顺序。
- Boundary Fact 不是 ECB 事件总线，直接写 owner-local cleanup buffer并在事实层排序。

## 7. 禁止方向

- phase 中途 playback 以获得 same-tick Entity。
- 多个 GAS ECB system/多 playback 点。
- 热路径直接 EntityManager 结构变化。
- 对尚不存在的 Buffer `AppendToBuffer`。
- playback 后继续使用旧直接引用。
- prefab spawn 后假设 cleanup outbox 自动存在。
- live/shell outbox 未接管就清除/移除。
- 用 ECB 承载普通 Gameplay Fact 或 Cue 分发。

## 8. Profile 与验收

ScaleProfile 采集 ECB command 数、playback 耗时、结构变化来源、cleanup shell 数/存活 Tick 和主线程同步。数值门槛按平台设定。

架构验收：

- Runtime Core 只有标准 EndFixed playback；
- Attribute/Tag/slab 热路径零结构变化；
- 同 Tick Destroy 事实不丢，drain 后 shell 消失；
- 0/1/N Tick 与 manual World 顺序一致；
- 所有 ECB producer dependency 完整，无隐式 phase sync。
