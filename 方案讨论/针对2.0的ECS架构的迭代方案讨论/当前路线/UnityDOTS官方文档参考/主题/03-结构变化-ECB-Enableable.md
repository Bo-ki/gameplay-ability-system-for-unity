# 03 结构变化、ECB 与 Enableable

## 职责

本主题维护 structural change、sync point、EntityCommandBuffer、Enableable component 和 DynamicBuffer handle 失效规则。这是 Runtime Core 热路径安全性和性能的基石。

## 核心概念详解

### 结构变化 (Structural Change)

结构变化是改变 entity archetype 的操作，包括：
- `CreateEntity` / `DestroyEntity`
- `AddComponent` / `RemoveComponent` / `SetComponentEnabled`（仅当改变 entity 在 query 中的可见性时）
- `AddComponentData` / `SetComponentData`（仅当组件未存在且需要添加到 entity 时）

**关键事实：结构变化不能在 job 线程中执行，必须在主线程完成。**

### Sync Point（同步点）

当主线程需要安全访问 ECS 数据时，必须等待所有相关的 job 完成。这称为 sync point。

```
主线程执行 structual change
    ->
等待所有已调度 job 完成（sync point）
    ->
执行结构变化
    ->
所有之前的 component/handle/DynamicBuffer 引用失效
```

**触发 sync point 的操作：**
1. 直接执行结构变化（`EntityManager.CreateEntity` 等）
2. `query.ToEntityArray()` / `query.CalculateEntityCount()`（enableable 过滤时）
3. `SystemAPI.Query` 的 `foreach` / `Run()`
4. ECB playback

**为什么 sync point 是性能杀手：**
- 阻塞主线程直到所有 worker 线程完成
- 结构变化后的下一帧，所有后续 job 需要重新调度
- handle/lookup/query 结果全部失效，必须重取

### Sync Point 成本模型

#### 成本构成

一个 sync point 的实际成本由以下部分叠加：

```
Sync Point 总成本 = 等待所有依赖 job 完成 + 执行结构变化 + 下次调度开销

1. 等待所有依赖 job 完成
   - 主线程空闲，worker 线程继续消费
   - 损失 = 最长运行 job 的剩余时间 × worker 线程数

2. 执行结构变化本身
   - 创建/销毁 entity、添加/移除 component、archetype 迁移
   - 复制 entity 数据到新 chunk

3. 后续调度开销
   - 所有 TypeHandle 失效，所有 system 需重新获取
   - Lookup 失效，需重建
   - 第一个后续 job 需完整的调度开销
```

#### 多 System 结构变化的合并效果

从官方文档 `performance-sync-points.md`：

> "Two systems that both make structural changes only create one sync point if they update sequentially, unless the first one also schedules jobs, in which case the second one will immediately sync on them."

**关键推论：**
- 连续的两个 system 都做结构变化 = 只有 1 个 sync point（前提是第一个没调度 job）
- 但如果第一个 system 调度了 job，第二个 system 做结构变化前就必须等待该 job → 2 个 sync point
- **最佳实践：所有结构变化集中在同一个 SystemGroup 的末尾 playback**

#### Sync Point 与 frame budget

```
典型帧预算（60fps = 16.67ms）中：
  - 1 个 sync point ≈ 0.1-1.0ms（取决于等待的 job 量）
  - 10 个分散的 sync point ≈ 1-10ms（已经吃掉大半帧预算）
  - ECB 合并后 1 个 sync point ≈ 0.1-0.5ms
```

**EX-GAS 解读：x50 AutoChess profile 中 `avgTickMs=13.77ms` 说明结构变化散落是主要瓶颈之一。**

### EntityCommandBuffer (ECB)

ECB 延迟记录结构变化，在指定 playback phase 统一提交，把多次结构变化合并为一个 sync point。

```csharp
// 从 ECB system 获取 ECB
var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
    .CreateCommandBuffer(state.WorldUnmanaged);

// 在 job 中记录变化
[BurstCompile]
public struct CreateEffectEntityJob : IJobEntity
{
    public EntityCommandBuffer ECB;

    public void Execute(Entity e, in CApplyEffectRequest request)
    {
        // 记录创建，不立即执行
        var effectEntity = ECB.CreateEntity();
        ECB.AddComponent(effectEntity, new CEffectInUsage
        {
            Source = request.Source,
            Target = e,
            Level = request.Level
        });
    }
}

// 调度 job 时传入 ECB
var job = new CreateEffectEntityJob { ECB = ecb };
state.Dependency = job.ScheduleParallel(state.Dependency);
// ECB 在对应的 ECBSystem 的 update 中自动 playback
```

**ECB 的 buffer 操作：**
```csharp
ECB.AppendToBuffer<T>(entity, new T { ... });  // 追加到已有 buffer
ECB.SetBuffer<T>(entity);                       // 设置整个 buffer
// 注意：AppendToBuffer 要求 buffer 已存在；不存在时需先 AddComponent
```

**默认 ECB System 位置：**
每个根 Group 的首尾各有一个 ECBSystem。例如 `SimulationSystemGroup` 有：
- `BeginSimulationEntityCommandBufferSystem`（在 Simulation 最前 playback）
- `EndSimulationEntityCommandBufferSystem`（在 Simulation 最后 playback）

**ECB 的 allocator 注意事项：**
- ECB 内部使用 RewindableAllocator，每次 playback 后 rewind
- 不能在 ECB playback 后继续使用从已释放 ECB allocator 分配的内存

#### ECB Playback 的内部机制

```
ECB 录制阶段（在 job 中）:
  线程安全地把命令追加到 ECB 内部链式缓冲区
  （每个线程有独立的 segment，无竞争）
      ↓
ECB playback 阶段（在主线程）:
  1. 主线程 sync point：等待所有依赖 job
  2. 按录制顺序播放所有命令
  3. 所有命令合并为一次结构变化
  4. rewind ECB allocator
```

#### 自定义 ECB System 的摆放

```csharp
// 在 GAS Structural Playback Group 中定义专用 ECB System
[UpdateInGroup(typeof(GasStructuralPlaybackSystemGroup), OrderLast = true)]
public partial class GasEndStructuralECBSystem : EntityCommandBufferSystem { }
```

**EX-GAS 约束：`GasStructuralPlaybackSystemGroup` 是 hot path 中唯一允许结构变化的位置。所有需要结构变化的系统必须通过此 Group 的 ECB。**

### Enableable Component

Enableable 是避免结构变化的关键机制。通过 enable/disable component 改变 entity 在 query 中的可见性，无需 archetype 迁移。

```csharp
// 定义 enableable component
public struct CAbilityActive : IComponentData, IEnableableComponent
{
    public int AbilityCode;
}

// 在 job 中切换（无需 ECB！）
[BurstCompile]
public partial struct DisableFinishedAbilityJob : IJobEntity
{
    void Execute(EnabledRefRW<CAbilityActive> active, in CAbilityRuntimeState state)
    {
        if (state.RemainingTime <= 0)
            active.ValueRW = false;  // 无结构变化，无 sync point
    }
}
```

#### Enableable 的三种操作方式

| 方式 | 上下文 | 性能 | 说明 |
|---|---|---|---|
| `EnabledRefRW<T>.ValueRW` | IJobEntity / idiomatic foreach | **最快** | 利用线性遍历的可预测访问模式 |
| `EnabledMask[index]` | IJobChunk | 快 | chunk 级 enabled bit 数组索引 |
| `ComponentLookup.SetComponentEnabled` | 随机访问 | **有额外开销** | 需定位 entity 数据，高频场景避免 |
| `EntityManager.SetComponentEnabled` | 主线程 | 有额外开销 + 可能触发 sync point | 仅用于调试/low freq |

**Enableable vs Add/Remove Component 选择：**

| 场景 | 推荐 | 原因 |
|---|---|---|
| 高频状态开关（每帧可能多次） | Enableable | 无结构变化，无 sync point，无 archetype 迁移 |
| 低频生命周期（创建/销毁时一次） | Add/Remove Component | 语义更明确，减少 query 复杂度 |
| active/inactive 标记 | Enableable | 一次写入，所有依赖 query 自动反应 |
| tag/role 授予 | Enableable 或 owner-local bitset | 避免每个 tag 创建一个 component 类型 |

**Enableable 的查询成本：**
- 同步 EntityQuery 操作（如 `CalculateEntityCount()`）在有 enableable 写 job 未完成时，会触发 sync point
- 以 `Ignore` 结尾的方法忽略 enableable 过滤，无需 sync point
- 异步操作（`Async` 结尾）返回 `NativeList` 而非 `NativeArray`（因为最终匹配量未知）

#### Enableable 竞态条件

从官方文档：

> "Avoid enabling or disabling a component on an entity that another thread might process in a job because this often leads to a race condition."

```csharp
// 危险：两个 job 同时操作同一 entity 的 enable state
// Job A: 遍历并 disable component
// Job B: 遍历并 enable component
// 同一帧内两个 job 可能冲突 → 不确定结果

// 安全：确保同一帧内每个 entity 只有一个 writer
// 通过 phase 分离 read/write window
```

### 结构变化后的 Handle 失效

```csharp
// 典型问题场景
var buffer = state.EntityManager.GetBuffer<MyElement>(entity);
state.EntityManager.CreateEntity();  // 结构变化！
var x = buffer[0];  // 安全系统抛异常：handle 已失效

// 正确做法
var buffer = state.EntityManager.GetBuffer<MyElement>(entity);
state.EntityManager.CreateEntity();  // 结构变化
buffer = state.EntityManager.GetBuffer<MyElement>(entity);  // 重取
var x = buffer[0];  // 安全
```

### 结构变化的批量优化

#### EntityQuery Bulk 操作 vs 逐个操作

```csharp
// 低效：逐个 entity 操作（每个都触发内部检查）
foreach (var entity in entities)
{
    EntityManager.AddComponent<CDead>(entity);  // O(n) 次 archetype 迁移
}

// 高效：EntityQuery 批量操作
EntityManager.AddComponent<CDead>(query);  // 一次操作，内部批量处理
```

**批量操作适用：**
- `AddComponent<T>(EntityQuery)`
- `RemoveComponent<T>(EntityQuery)`
- `DestroyEntity(EntityQuery)`

---

## 官方证据

| 证据 | 结论 |
|---|---|
| `performance-sync-points.md` | 结构变化是最主要的 sync point 来源；ECB 合并结构变化降低 sync point 次数；连续两个做结构变化的 system 合并为一个 sync point |
| `components-enableable-use.html` | Enableable 无 archetype 迁移；worker thread 可通过 `ComponentLookup.SetComponentEnabled` 安全修改；迭代优先于 random-access |
| `concepts-archetypes.html` | 每次 Add/Remove Component 触发 archetype 迁移；频繁移动 entity 降低性能 |
| `performance-chunk-allocations.html` | 每个 tag component 使 archetype 排列数翻倍；临时数据应存 DynamicBuffer |
| `systems-entity-command-buffer.md` | ECB 可用 `CreateEntity`/`AddComponent`/`SetBuffer`/`AppendToBuffer`；playback 位置决定结构变化集中点 |
| `components-buffer-introducing.html` | 结构变化使 DynamicBuffer handle 失效 |

## 使用模式与反模式

**正确模式：**
- Hot path 状态切换用 Enableable
- 结构变化集中在 ECB playback phase
- 结构变化后立即重取所有 handle
- 大批量同类结构变化优先 EntityQuery bulk
- 低频生命周期变更用 Add/Remove Component

**反模式：**
- 在 `ISystem.OnUpdate` 或 job 内直接 `EntityManager.CreateEntity`
- 把 ECB 当 gameplay event bus
- 结构变化后继续使用旧 DynamicBuffer handle
- 高频 toggle 用 Add/Remove Component
- 每帧创建+销毁 entity 表示临时状态
- 用 tag component 做高频状态标记（application 中每个 tag 使 archetype 排列翻倍）

## EX-GAS 项目解读

### 对 Runtime Core 的直接约束

这是当前 2.0 实现偏差的核心领域：

1. **Instant GE 不应创建 runtime GE entity**：当前 instant GE 走 `CApplyGameplayEffectRequest -> runtime GE entity -> SEffectApply -> destroy`，导致大量 entity churn。目标态应直接走 `EffectCommand -> Spec Resolve -> Attribute Delta`，不创建 entity。

2. **Active Effect Store 应用 enableable + slot**：duration/stack/period/granted state 进入 owner-local DynamicBuffer slot，用 enableable 标记 active，避免 per-frame entity create/destroy。

3. **ECB playback 唯一结构变化点**：`GasStructuralPlaybackSystemGroup` 是 hot path 中唯一允许结构变化的位置。Debugger 必须报告每帧 ECB command 数量和来源 system。

4. **Presentation outbox 不创建 entity**：Cue/UI/VFX/SFX marker 是 transient stream，不应创建 entity。

### 结构变化集中化设计

```
正确 phase 设计（目标态）:

Phase 1: Command Ingest          ← 只读 EntityQuery，不结构变化
Phase 2: Effect Spec Resolve     ← IJobChunk，不结构变化
Phase 3: Attribute Delta Apply   ← IJobEntity，不结构变化
Phase 4: Typed Fact Fan-in       ← NativeStream merge，不结构变化
Phase 5: Gameplay Reaction       ← 只读事实，写 ECB command（延迟）
Phase 6: Active Effect Lifecycle ← IJobEntity + Enableable toggle
Phase 7: Cleanup/Observation     ← 读写分离
Phase 8: STRUCTURAL PLAYBACK ← 唯一 ECB playback 点 ← 唯一 sync point
```

### 当前实现的架构问题诊断

AutoChess x50 profile 显示结构变化异常膨胀的原因：
- Instant GE 创建 entity → tick → destroy entity
- Period/Overflow 子 GE entity 重复创建
- 大量 entity 创建/销毁散落各处，而非集中在 ECB playback

**目标态规则：**
```
Hot path 结构变化 P0 违规 = 在 GASRuntimePhase 中直接 EntityManager 结构变化
唯一例外 = GasStructuralPlaybackSystemGroup 内的 ECB playback
```

### Debugger 观测项

Debugger 必须报告以下结构变化指标：
- `structuralChangeCount` per frame
- `entityCreated` / `entityDestroyed` per frame
- `componentAdded` / `componentRemoved` per frame
- `ecbCommandCount` per ECB system
- `enableableToggleCount` per frame
- `syncPointCount` per frame
- **违反 phase contract 的结构变化来源 system（告警）**

---

## 常见陷阱

1. **Enableable check 不是免费的**：同步 query 在有 enableable 写 job 时会阻塞
2. **`SetComponentEnabled` 的竞态**：worker thread 可调用，但注意不要和其他线程操作同一 entity
3. **ECB allocator 生命周期**：ECB 的 allocator 在 playback 后 rewind，不要在 playback 后持有其分配的内存
4. **`AppendToBuffer` 前提**：buffer 必须已存在；否则先用 `AddComponent<T>`
5. **结构变化后不止 buffer 失效**：**所有** TypeHandle、ComponentLookup、BufferLookup、DynamicBuffer 都失效
6. **连续 system 结构变化仅合并为一个 sync point 的前提是中间没有调度 job**
7. **Archetype 数量 ≥ entity 数量时**：>1.5GB wasted + 每次 entity 间 cache miss（来自官方 chunk allocations 文档）

## 验收指标

1. Debugger 能报告每帧结构变化数量、ECB command 数量、playback phase
2. 不再出现结构变化后继续使用旧 DynamicBuffer handle 的安全异常
3. AutoChessDemo 压测中 structural changes 不随单位数线性暴涨
4. Instant GE entity 创建/销毁随 instant 伤害数量呈零增长
5. 所有结构变化来源可通过 Debugger 追溯到具体 System
