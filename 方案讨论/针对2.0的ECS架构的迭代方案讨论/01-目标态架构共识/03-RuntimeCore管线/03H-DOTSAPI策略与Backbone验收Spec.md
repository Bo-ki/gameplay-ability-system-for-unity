# 03H：DOTS API 策略与 Backbone 验收 Spec

> 状态：v1 API 选择门
> 基线：Unity 6000.3 / Entities 1.4.6

## 1. 结论

Backbone 先证明“执行域、数据唯一性、Job DAG、生命周期、边界事实”成立，再承载完整 GAS 语义。v1 的 API 选择是成套约束，不能只复制单个示例：

- `GasFixedTickSystemGroup` 是 `FixedStepSimulationSystemGroup` 的直接子组，并 `UpdateAfter(PhysicsSystemGroup)`。
- `GasTickKernelSystem : ISystem` 拥有当 Tick 的完整 Job DAG 与 `WorldUpdateAllocator` scratch。
- ASC 的 Attribute/Tag/Ability/Continuation/Effect 权威状态使用 Buffer/Component，不使用托管对象和双镜像。
- 真实结构变化只记录到标准 `EndFixedStepSimulationEntityCommandBufferSystem`。
- Boundary 使用 scoped Cleanup Buffer：ASC-scope fact写所属 ASC，Battle/Session-scope fact写唯一 Session；每 fact恰有一个 owner，由一个 managed drain两阶段接管。

## 2. API 选型矩阵

| 需求 | v1 默认 API | 选择理由 | 禁止替代 |
|---|---|---|---|
| 固定 Tick 执行域 | `FixedStepSimulationSystemGroup` 子组 | 使用标准 rate manager、allocator、父链与 EndFixed | 手工五组 Update |
| GAS 主驱动 | `ISystem` | unmanaged、Burst 友好、显式依赖 | 把核心状态放 `SystemBase` 字段 |
| 托管边界 | `SystemBase` 或明确 managed service | 仅做 outbox drain/分发 | 在 Core Job 调 UnityEngine.Object |
| 稳定 ASC 遍历 | `IJobChunk` / 合适的并行 Job | 可控制 BufferAccessor、enable mask 与 chunk | 无依据地强制一种 Query API |
| 小型控制路径 | `SystemAPI.Query`/lookup | 可读性优先，需记录非热点理由 | 在热点中无测量地逐 Entity 主线程遍历 |
| 静态定义 | `BlobAssetReference<T>` | 不可变共享、Job 可读 | ScriptableObject 直接进 Job |
| 固定逻辑数组 | `DynamicBuffer<T>` | ASC-local、索引寻址 | Attribute 一属性一 Component |
| 长期实例 | non-compacting slab Buffer | 稳定 slot+generation | 默认 slot→Entity promotion |
| Tick scratch | `state.WorldUpdateAllocator` NativeContainer | 服从 FixedStep group 生命周期 | static/Singleton FrameArena |
| 高频开关提示 | 预挂载 `IEnableableComponent` | 避免结构变化 | 用 enable bit 作为业务权威 |
| 结构变化 | 标准 EndFixed ECB | 单 playback、标准父链 | 自定义 GAS ECB playback |
| 销毁后事实 | `ICleanupBufferElementData` | cleanup shell 可 drain | 两套持久 outbox |

具体 Job 类型依数据形态选择，不把 `IJobChunk`、`IJobEntity` 或 `NativeStream` 神化为全局唯一答案。任何偏离必须由 profile 和 ADR 解释。

## 3. Backbone 最小闭环

Backbone 必须先完成以下纵向切片：

1. Session 创建并冻结 Tick Rate、Catalog/Layout/Definition 哈希。
2. ASC spawn 显式建立所有固定 Buffer 和 cleanup outbox。
3. ingress 冻结一批 Command，并赋稳定 source sequence。
4. Kernel 建立单 Job DAG，解析到 target bucket。
5. `TargetPrepare` 在 target-local shadow 中求稳定态；`SessionFaultReduce` 成功后由 `TargetPublish` 无失败写 Attribute/Tag/Effect slab 与最终 intent。
6. stable merge 已发布 Core Fact，写 Boundary Fact。
7. 真实结构变化由标准 EndFixed playback。
8. 单 managed drain 读取 live/shell outbox 并完成清理。
9. 独立/manual World 通过完整 FixedStep 父链复现相同结果。

在上述闭环通过前，不扩展预测回滚、跨世界复制、可热换 Definition 或第二套 Presentation 通道。

## 4. 数据唯一性门

### 4.1 Attribute

- 一个 Session 只有一个 AttributeLayout。
- ASC Buffer 逻辑长度等于 Layout 数量，元素仅存 `Base/Current`。
- Id→index 只来自 Layout Blob。
- generated component 可以作为只读调试快照，但不能参与业务查询或写回；v1 默认不生成。

### 4.2 Tag

- 一个 Session 只有一个 TagCatalog。
- 权威计数 Buffer 同时保存 exact 与 inclusive count。
- presence/ancestor bitset 是可重建派生缓存。
- grant/remove 只能通过 target-local writer 更新，不能从 bitset 反推权威计数。

### 4.3 长期实例

- Ability/Continuation/Effect 句柄统一为 `slot index + generation`。
- 释放不移动 live slot。
- Definition-time 不允许切换“这个定义用 Entity、那个定义用 slot”；否则所有查找、保存、调试与生命周期都形成双模型。
- 真正的 projectile/hitbox 等独立游戏对象可以是 Entity，但只引用 Effect/Activation handle，不接管权威实例状态。

## 5. 查询与依赖门

- Kernel 的 ComponentTypeHandle、BufferTypeHandle、Lookup 每 Tick 更新。
- 所有 JobHandle 从 `state.Dependency` 串接，最终返回给 `state.Dependency`；ECB producer dependency 完整登记。
- 同一个 ASC/target 的可写 Buffer 不同时交给多个无分区 Job。
- enableable query 的启用语义必须明确；`IgnoreComponentEnabledState` 不能用来绕开安全依赖。
- phase/业务 lane 之间不 `Complete()`；性能采样必须把每次主线程完成依赖标注原因。
- 临时 NativeContainer 不跨 System/Tick，长期 continuation/effect 不使用临时 allocator。

## 6. 时序门

- 一渲染帧固定 Tick 数是 `0..N`。
- Duration/Period/Cooldown 全部用整数 Tick/截止 Tick。
- same-tick 只允许 Ability 直接输出与已证明 closed、finite、bounded 的 pre-apply program。
- application 后 Fact reaction 默认形成下一 Tick Command。
- stabilization 未收敛是 deterministic fatal，不是“多跑若干固定 pass”或静默截断。

## 7. 生命周期门

- Session 与 ASC spawn 都显式添加 scoped cleanup outbox/state；不依赖 prefab 复制 cleanup component。
- Destroy 前完成 Boundary Fact 写入，EndFixed 后 shell 保留 cleanup buffer。
- drain 冻结 `InFlight(BatchId, InFlightWatermark)`；成功 receipt 后只清 accepted `<=InFlightWatermark` prefix，late tail 保留并回到 Pending。shell 无 tail才标记 Accepted；下一次 Kernel cleanup prepass 才把 shell remove 记录到该 Tick 的标准 EndFixed，drain 不持有跨 batch ECB。
- 业务存活检查依据 ASC identity/generation，不依据 `EntityManager.Exists`。
- World shutdown 先完成 producer、最后 drain、清 shell，再释放 Blob/managed registry。

## 8. ScaleProfile 门

Spec 不硬编码实体数、毫秒、Buffer 容量或 chunk overflow 百分比。每个目标平台建立版本化 ScaleProfile，至少包含：

- Unity/Entities/Burst/Jobs 版本与目标硬件；
- Tick Rate、ASC/Command/Effect/Tag/Attribute 分布；
- buffer 高水位、扩容、chunk 内/外比例；
- target bucket 分布与最坏热点 target；
- scratch 峰值与临时分配次数；
- stabilization 迭代/故障；
- 主线程同步点、ECB playback、managed drain 耗时；
- 可接受门槛、采样方法与基线 commit。

只有 profile 数据可以决定 InternalBufferCapacity、初始 scratch 容量、并行 batch size 与报警阈值。

## 9. Backbone 验收场景

| 场景 | 必须证明 |
|---|---|
| 0/1/N Tick 帧 | 结果只依 Command 与 SimulationTick，不依渲染帧 |
| 多 ASC 同 target | canonical order、单 target writer、结果可复现 |
| Activate 后 Commit/Cancel | 同 Tick 槽状态正确，不依赖 ECB 新结构 |
| Effect inhibition/stack/period | TargetPrepare 在 shadow 中收敛，SessionFaultReduce 成功后 TargetPublish durable state/Fact |
| ASC 同 Tick 销毁 | Boundary Fact 从 cleanup shell 不重不漏 drain |
| manual World | 完整 FixedStep 父链、Physics/GAS/EndFixed/allocator 顺序一致 |
| 定义故障 | 环/无有限界在 build 阶段拒绝；运行时越界 fatal |
| stale handle | slot 复用后旧 generation 拒绝 |

## 10. 性能红线

以下是形态红线，不依赖某台机器：

- 热路径存在 phase 级 `Complete`。
- Attribute/Tag 每 Tick resize 或增删 Component。
- 同一 target 多 writer 竞争同一 Buffer。
- Tick scratch 存进 ECS/managed/static 并跨生命周期。
- 每个 Ability/Effect 实例默认创建 Entity。
- Core 内维护多消费者游标或两套 outbox。
- Profile 未记录输入分布，却以单场景平均值宣称 scale-ready。

触发任一项即不通过 Backbone；数值性能是否通过则由 ScaleProfile 判定。
