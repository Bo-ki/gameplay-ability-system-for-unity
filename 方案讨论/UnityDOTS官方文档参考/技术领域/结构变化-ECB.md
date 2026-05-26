# 结构变化-ECB

## 职责

覆盖 ECS 中所有改变 entity archetype 的操作机制与管理规范：Structural Change、Sync Point、EntityCommandBuffer（ECB）。阐述为什么结构变化是性能瓶颈、如何通过 ECB 延迟合并降低成本、以及在 EX-GAS Runtime Core 中必须遵守的结构变化集中化设计。

不覆盖 Enableable Component（参见《Enableable-Component选型》），不覆盖 NativeStream 等非实体创建的数据流模式。

## 核心概念

### 结构变化 (Structural Change)

结构变化是改变 entity archetype 的任何操作，包括：

- `CreateEntity` / `DestroyEntity`
- `AddComponent` / `RemoveComponent`
- `SetComponentEnabled`（仅当改变 entity 在 query 中的可见性时）

**关键事实：结构变化不能在 job 线程中执行，必须在主线程完成。**

每次结构变化触发 entity 的 archetype 迁移——将 entity 数据从原 chunk 复制到目标 archetype 的 chunk。这一过程涉及数据复制与 chunk 重新分配，成本随 entity 数量和 component 数量上升。

### Sync Point（同步点）

当主线程需要安全访问 ECS 数据时，必须等待所有相关的已调度 job 完成。这个等待点称为 sync point。

```
主线程执行结构变化
  -> 等待所有已调度 job 完成 (sync point)
  -> 执行结构变化
  -> 所有之前的 component handle / DynamicBuffer / Lookup 引用失效
```

**触发 sync point 的操作：**
1. 直接执行结构变化（`EntityManager.CreateEntity` 等）
2. 带有 enableable 过滤的同步 query 操作：`query.ToEntityArray()`、`query.CalculateEntityCount()`
3. `SystemAPI.Query` 的 `foreach` / `Run()`
4. ECB playback

**sync point 是性能杀手的原因：**
- 阻塞主线程直到所有 worker 线程完成；期间主线程空闲
- 结构变化后所有 TypeHandle / ComponentLookup / BufferLookup 失效，必须重取
- 所有后续 job 需要重新调度，产生额外调度开销

### Sync Point 成本模型

一个 sync point 的总成本由三部分叠加：

```
Sync Point 总成本 = 等待依赖 job 完成 + 执行结构变化 + 后续调度开销
```

1. **等待依赖 job 完成**：主线程空闲，worker 线程继续消费。损失 = 最长运行 job 的剩余时间 × worker 线程数
2. **执行结构变化本身**：创建/销毁 entity、archetype 迁移、数据复制
3. **后续调度开销**：所有 TypeHandle 失效，所有 system 需重新获取；Lookup 失效需重建

**多 System 结构变化的合并效果（来自 `performance-sync-points.md`）：**

> "Two systems that both make structural changes only create one sync point if they update sequentially, unless the first one also schedules jobs, in which case the second one will immediately sync on them."

推论：
- 连续的两个 system 都做结构变化 = 只有 1 个 sync point（前提是第一个没调度 job）
- 如果第一个 system 调度了 job，第二个做结构变化前就必须等待该 job → 2 个 sync point
- **最佳实践：所有结构变化集中在同一个 SystemGroup 的末尾 playback**

**Sync Point 与帧预算：**
```
典型帧预算（60fps = 16.67ms）中：
  - 1 个 sync point ≈ 0.1-1.0ms（取决于等待的 job 量）
  - 10 个分散的 sync point ≈ 1-10ms（已经吃掉大半帧预算）
  - ECB 合并后 1 个 sync point ≈ 0.1-0.5ms
```

### EntityCommandBuffer (ECB)

ECB 是延迟结构变化的工具。它在 job 线程中安全地"录制"结构变化命令，在指定的 playback phase 由主线程统一提交，将多次结构变化合并为一次 sync point。

```csharp
// 从 ECB system 获取 ECB
var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
    .CreateCommandBuffer(state.WorldUnmanaged);

// 在 job 中录制变化
[BurstCompile]
public struct CreateEffectEntityJob : IJobEntity
{
    public EntityCommandBuffer ECB;

    public void Execute(Entity e, in CApplyEffectRequest request)
    {
        var effectEntity = ECB.CreateEntity();
        ECB.AddComponent(effectEntity, new CEffectInUsage { ... });
    }
}

// 调度 job 时传入 ECB
var job = new CreateEffectEntityJob { ECB = ecb };
state.Dependency = job.ScheduleParallel(state.Dependency);
// ECB 在对应的 ECBSystem 的 update 中自动 playback
```

**ECB 的 buffer 操作：**
```csharp
ECB.AppendToBuffer<T>(entity, new T { ... });   // 追加到已有 buffer
ECB.SetBuffer<T>(entity);                         // 设置整个 buffer（覆盖）
// 注意：AppendToBuffer 要求 buffer 已存在；不存在时需先 AddComponent<T>
```

**默认 ECB System 位置：**

每个根 Group 的首尾各有一个 ECB System。例如 `SimulationSystemGroup`：
- `BeginSimulationEntityCommandBufferSystem`（Simulation 最前 playback）
- `EndSimulationEntityCommandBufferSystem`（Simulation 最后 playback）

#### ECB Playback 的内部机制

```
ECB 录制阶段（在 job 中）:
  线程安全地将命令追加到 ECB 内部链式缓冲区
  （每个线程有独立 segment，无竞争）
      ↓
ECB playback 阶段（在主线程）:
  1. 主线程 sync point：等待所有依赖 job 完成
  2. 按录制顺序播放所有命令
  3. 所有命令合并为一次结构变化
  4. rewind ECB allocator（释放内部缓冲区）
```

#### 自定义 ECB System 的摆放

```csharp
// 在 GAS Structural Playback Group 中定义专用 ECB System
[UpdateInGroup(typeof(GasStructuralPlaybackSystemGroup), OrderLast = true)]
public partial class GasEndStructuralECBSystem : EntityCommandBufferSystem { }
```

#### ECB PlaybackPolicy.MultiPlayback（CASE-21）

通过 `new EntityCommandBuffer(Allocator.TempJob, PlaybackPolicy.MultiPlayback)` 创建可多次 playback 的 ECB。同一 ECB 内先 `CreateEntity()` 后通过 placeholder entity 引用该实体，deferred entity remapping 自动完成映射。用于多 pass 效果应用或将同一批命令 playback 到不同 World state。配合 `[ChunkIndexInQuery] int sortKey` 实现确定性 playback 顺序。

#### EntityQueryCaptureMode.AtPlayback（CASE-34）

ECB 录制时存储 query 引用，playback 时重新评估 query 结果。适用于批量 chunk 级结构变化：1M entity 场景下 `ecb.AddComponent(query, new C(), AtPlayback)` 耗时 3.5ms，远优于逐个 entity 的 170ms。

#### [ChunkIndexInQuery] sortKey（CASE-35）

并行 job 中录制顺序不确定但 playback 需要确定顺序时，将 `[ChunkIndexInQuery] int sortKey` 作为 ECB 方法的第一个参数传递。playback 前按 sortKey 排序，大者后执行。适用于多 effect 并行评估的 ECB 写入、确定性 replay 调试。

### Handle 失效

结构变化后所有持有的 handle 都会失效：

```csharp
// 错误：结构变化后继续使用旧 handle
var buffer = state.EntityManager.GetBuffer<MyElement>(entity);
state.EntityManager.CreateEntity();  // 结构变化！
var x = buffer[0];  // 安全系统抛异常：handle 已失效

// 正确：结构变化后重取所有 handle
var buffer = state.EntityManager.GetBuffer<MyElement>(entity);
state.EntityManager.CreateEntity();  // 结构变化
buffer = state.EntityManager.GetBuffer<MyElement>(entity);  // 重取
var x = buffer[0];  // 安全
```

失效范围包括：**所有** TypeHandle、ComponentLookup、BufferLookup、DynamicBuffer 引用。

### 批量优化：EntityQuery Bulk

```csharp
// 低效：逐个 entity（每个都触发内部检查与 archetype 迁移）
foreach (var entity in entities)
    EntityManager.AddComponent<CDead>(entity);  // O(n) 次结构变化

// 高效：EntityQuery 批量操作（一次遍历，内部批量处理）
EntityManager.AddComponent<CDead>(query);
```

**批量操作适用 API：** `AddComponent<T>(EntityQuery)`、`RemoveComponent<T>(EntityQuery)`、`DestroyEntity(EntityQuery)`

## 编写规范

### SC-01: Hot Path 禁止直接结构变化

**声明：** Hot path（每帧执行、entity 数 > 100）禁止直接调用 `EntityManager.CreateEntity`、`DestroyEntity`、`AddComponent`、`RemoveComponent`。所有结构变化必须通过 ECB 延迟到 playback phase。

**来源：** `performance-sync-points.md`；`主题/13-DOTS编写规范与性能陷阱.md` P0-02

**为什么：** 每个直接的结构变化触发 sync point → 主线程等待所有 job 完成 → TypeHandle/Lookup 全部失效。散落的结构变化使 ECB 合并优化完全失效。

**EX-GAS 诊断：** ISSUE-004 — 真实 Scene 曾暴露三类 `BufferTypeHandle invalidated by structural change` 错误。`SApplyGameplayEffectRequest` 在同一系统中读 buffer 后创建/销毁 entity。

**检查方法：** 搜索代码中 `EntityManager.CreateEntity` / `DestroyEntity` / `AddComponent` / `RemoveComponent` 在 `OnUpdate` 或 job 中的出现；Debugger 向违规 system 告警。

---

### SC-02: 结构变化后必须重取所有 Handle

**声明：** 在结构变化发生后，所有此前获取的 TypeHandle、ComponentLookup、BufferLookup、DynamicBuffer 引用全部失效。必须重新获取后才能继续使用。

**来源：** `components-buffer-introducing.html`；`主题/03-结构变化-ECB-Enableable.md`

**为什么：** ECS 安全系统在结构变化后使所有 handle 失效以防止悬垂指针。使用失效 handle 触发安全系统异常，运行时崩溃。

**EX-GAS 诊断：** 曾出现 `DynamicBuffer invalidated by structural change` 安全异常，源于 EffectApplication 系统中在 buffer 操作前后存在隐式结构变化。

**检查方法：** Code review 时检查结构变化 API 调用后是否继续使用此前获取的 buffer/handle。优先将结构变化推迟到 playback phase 以避免此类问题。

---

### SC-03: 批量同类结构变化优先 EntityQuery Bulk

**声明：** 当需要对大量 entity 执行相同的结构变化时（如批量 AddComponent、批量 DestroyEntity），优先使用 `EntityManager.AddComponent<T>(query)` 等批量 API，而非逐个 entity 操作。

**来源：** `performance-sync-points.md`；`optimize-structural-changes.md`

**为什么：** 逐个 entity 操作导致 O(n) 次 archetype 迁移和内部检查。批量操作在内部一次性遍历所有匹配 chunk，大幅减少开销。

**EX-GAS 诊断：** 当前无批量操作模式对照。AutoChess battle 初始化/批量效果移除路径中可能存在逐个操作的性能浪费。

**检查方法：** 搜索 `foreach` + `AddComponent` / `DestroyEntity` 模式；排查批量操作的可替代性。

---

### ECB-01: ECB 是延迟结构变化工具，不是 Gameplay Event Bus

**声明：** ECB 的设计目标是延迟录制和批量提交结构变化。不应将 ECB 用作 Gameplay Event Bus、消息队列或跨 system 通信通道。

**来源：** `systems-entity-command-buffer.md`；`主题/03-结构变化-ECB-Enableable.md`

**为什么：** ECB playback 本身就是结构变化，带来 sync point。用它传递非结构变化的消息会引入不必要的 sync point，同时掩盖原本应在数据流中清晰表达的事件传递。

**EX-GAS 诊断：** EventBus 在当前实现中同时承载 gameplay event 和结构变化边界，职责不单一。目标态应将结构变化使用 ECB，gameplay 事件使用 TypedFact/NativeStream。

**检查方法：** ECB command 的语义审查：如果录制的命令不是 CreateEntity/AddComponent/RemoveComponent/DestroyEntity 等结构变化操作，而是单纯的数据消息传递 → 违规。

---

### ECB-02: AppendToBuffer 前必须确保 Buffer 已存在

**声明：** 使用 `ECB.AppendToBuffer<T>(entity, element)` 前，必须确保目标 entity 已拥有 `DynamicBuffer<T>` 组件。如果 buffer 可能不存在，必须先通过 `ECB.AddComponent<T>(entity, new T())` 添加。

**来源：** `components-buffer-command-buffer.md`；`主题/16-官方案例模式-高级.md` CASE-47

**为什么：** `AppendToBuffer` 在 playback 时不会自动创建 buffer。如果 buffer 组件不存在，playback 执行 `AppendToBuffer` 会失败。`SetBuffer` 同理——它设置整个 buffer，但也不自动创建组件。

**EX-GAS 诊断：** EffectCommand fan-in 中多个并行 job 通过 ECB 向同一 ASC entity 的 outbox buffer 追加元素时，必须确保 buffer 组件在首个 `AppendToBuffer` 前已存在。

**检查方法：** 搜索 `ECB.AppendToBuffer` / `ecb.AppendToBuffer` 的出现，逐处确认上游是否有 `ECB.AddComponent<TBuffer>` 或 playback 前 entity 已静态持有该 buffer 组件。

---

### ECB-03: ECB Playback 位置必须属于明确 SystemGroup Phase

**声明：** ECB 的 playback 位置（即所属 ECBSystem 在 SystemGroup 中的排列顺序）必须是一种有意识的设计决策，归属于明确的 frame backbone phase。不允许随意选择 ECBSystem 或隐式依赖默认 Simulation ECB。

**来源：** `performance-sync-points.md`；`systems-entity-command-buffer.md`；`主题/03-结构变化-ECB-Enableable.md`

**为什么：** ECB playback 即 sync point。如果多个 ECB 散布在不同 phase，就产生多个分散的 sync point，失去集中化优势。所有结构变化用户必须使用同一 phase 的 ECB，确保合并为一次 sync point。

**EX-GAS 诊断：** `GasStructuralPlaybackSystemGroup` 是 hot path 中唯一允许结构变化的位置。所有需要结构变化的 system 必须通过此 Group 的 ECB，不绕道使用 `BeginSimulationECBSystem` 或 `EndSimulationECBSystem`。

**检查方法：** 审计所有 `CreateCommandBuffer` 调用来源。如果源头不是 `GasStructuralPlaybackSystemGroup` 中的 ECBSystem（除非有明确的 phase contract 豁免），视为违规。

---

### ECB-04: ECB Allocator 在 Playback 后 Rewind

**声明：** ECB 内部使用 `RewindableAllocator`，每次 playback 后自动 rewind 释放内部缓冲区。不能在 playback 后继续持有或访问 ECB 分配的内存（包括通过 ECB 创建的 entity、buffer 引用等）。

**来源：** `systems-entity-command-buffer.md`；`主题/03-结构变化-ECB-Enableable.md`

**为什么：** Rewind 后 allocator 释放了所有内部段，继续访问已释放内存导致未定义行为或崩溃。ECB 本质是单帧工具，不应跨帧使用。

**EX-GAS 诊断：** Debugger 应监控 ECB 生命周期，确保没有任何 structure 在 playback 后被持有。每帧的 ECB 在帧末释放，新帧重新创建。

**检查方法：** 搜索 ECB 类型的字段或跨帧缓存。ECB 必须在 `OnUpdate` 中通过 `CreateCommandBuffer` 获取，不能存储在 system 字段中跨帧复用。

---

### PRF-02: 禁止在 Hot Path 直接执行结构变化（P0 致命）

**声明：** `ISystem.OnUpdate` 或 job 内直接调用 `EntityManager.CreateEntity`、`AddComponent`、`RemoveComponent`、`DestroyEntity` 是 P0 违规。所有结构变化必须通过 ECB 延迟到 playback phase。

**来源：** `performance-sync-points.md`；`主题/13-DOTS编写规范与性能陷阱.md` P0-02

**为什么：** 每个结构变化触发 sync point 阻塞主线程。散落的结构变化使 ECB 合并优化失效。在帧预算紧张（60fps = 16.67ms）的场景下，多个 sync point 可直接导致帧超时。

**EX-GAS 诊断：** ISSUE-004 — `SApplyGameplayEffectRequest` 在系统中读 buffer 后创建/销毁 entity，触发 `BufferTypeHandle invalidated` 错误。AutoChess x50 下结构变化散落是主要瓶颈之一（`avgTickMs=13.77ms`）。

**检查方法：**
- Grep 搜索 `EntityManager.CreateEntity` / `DestroyEntity` / `AddComponent` / `RemoveComponent` 在 Runtime Core hot path 中的出现
- Debugger 输出 `structuralChangeCount` 并按来源 system 分类
- 出现任意 hot path 直接结构变化则标为 P0 违规

---

### PRF-04: 结构变化必须集中到单一 ECB Playback Phase（P0 致命）

**声明：** Frame backbone 中只允许一个（或一组连续排列的）ECB playback phase 做结构变化。所有需要结构变化的 system 必须使用该 phase 的 ECB，不得分散在多个 SystemGroup 中各自 playback。

**来源：** `performance-sync-points.md`；`主题/13-DOTS编写规范与性能陷阱.md` P0-04

**为什么：** 每增加一个独立的 sync point ≈ 0.1-1.0ms 主线程阻塞。10 个分散 sync point = 1-10ms，在 16.67ms 帧预算中致命。连续排列的两个做结构变化的 system 可合并为一个 sync point，但中间如果任何一个调度了 job 则合并失效。

**EX-GAS 诊断：** ISSUE-009 — 缺少统一 frame backbone。`GasStructuralPlaybackSystemGroup` 设计已存在但尚未完全落地。AM2B-A/B contract 已声明但 system 尚未全部迁移。

**目标态结构：**
```
GasStructuralPlaybackSystemGroup（唯一结构变化点）
  ├── BeginGasStructuralECBSystem  (playback before core phases)
  └── EndGasStructuralECBSystem    (playback after core phases)
```

**检查方法：** 统计每帧 sync point 数量。如果 `syncPointCount > 1` 且排除初始化/销毁帧，标为 P0 违规。Frame backbone 设计图中检查结构变化点的数量。

## 模式与反模式

### 正确模式

**模式 1：Hot Path 状态切换用 Enableable（替代结构变化）**
高频 toggle 的场景（active/inactive、alive/dead）使用 `IEnableableComponent` + `EnabledRefRW<T>`，不触发结构变化和 sync point。参考 `主题/12-官方案例模式.md` CASE-06。

**模式 2：结构变化集中在 ECB Playback Phase**
所有需要创建/销毁 entity、添加/移除 component 的操作，录制到 ECB 中，在 `GasStructuralPlaybackSystemGroup` 末尾统一 playback。参考 `主题/12-官方案例模式.md` CASE-05。

**模式 3：结构变化后立即重取所有 Handle**
使用 `EntityManager` 或 ECB playback 后，所有后续访问必须先重新获取 TypeHandle、Lookup、DynamicBuffer。

**模式 4：大批量同类结构变化优先 EntityQuery Bulk**
批量添加 `AddComponent<T>(EntityQuery)`、批量删除 `RemoveComponent<T>(EntityQuery)`、批量销毁 `DestroyEntity(EntityQuery)`。

**模式 5：自定义 ECB System 精确控制 Playback Phase**
继承 `EntityCommandBufferSystem` + 实现 `IECBSingleton` + `RegisterSingleton<Singleton>`，在 frame backbone 的特定 phase 放置专用的 ECB playback。参考 `主题/16-官方案例模式-高级.md` CASE-25。

**模式 6：sortKey 保证确定性 ECB Playback**
并行 job 中使用 `[ChunkIndexInQuery] int sortKey` 作为 ECB 方法的第一个参数，确保 playback 顺序确定。参考 CASE-35。

**模式 7：EntityQueryCaptureMode.AtPlayback 批量操作**
ECB 录制 query 并在 playback 时评估，利用批量 chunk 级操作优势。参考 CASE-34。

### 反模式

**反模式 1：在 ISystem.OnUpdate 或 Job 内直接 EntityManager.CreateEntity**
- 原因：直接触发 sync point，handle 失效，失去 ECB 合并优势
- 替代：通过 ECB 录制，在 playback phase 统一提交

**反模式 2：把 ECB 当 Gameplay Event Bus**
- 原因：ECB 设计目标是结构变化。用作消息传递引入不必要的 sync point，语义混淆
- 替代：TypedFact / NativeStream 用于 gameplay 事件传递

**反模式 3：结构变化后继续使用旧 DynamicBuffer Handle**
- 原因：安全系统抛异常，运行时崩溃
- 替代：结构变化后所有 handle 重取，或彻底避免同一帧内在 handle 获取后做结构变化

**反模式 4：高频 Toggle 用 Add/Remove Component**
- 原因：每次 archetype 迁移，极高频下导致 chunk 碎片化和大量 sync point
- 替代：使用 Enableable Component（无结构变化）

**反模式 5：每帧创建+销毁 Entity 表示临时状态**
- 原因：entity churn → chunk 碎片化 → 结构变化膨胀
- 替代：DynamicBuffer / NativeStream / Enableable 标记

**反模式 6：ECB 跨帧复用**
- 原因：playback 后 allocator rewind，跨帧使用访问已释放内存
- 替代：每帧通过 `CreateCommandBuffer` 创建新 ECB

**反模式 7：多个 System 各自使用默认 Sim ECB 做结构变化**
- 原因：结构变化散落在不同 phase，产生多个 sync point
- 替代：统一使用 `GasStructuralPlaybackSystemGroup` 的 ECB

## EX-GAS 项目解读

### 对 Runtime Core 的直接约束

1. **Instant GE 不应创建 runtime GE entity**：当前 instant GE 走 `CApplyGameplayEffectRequest -> runtime GE entity -> SEffectApply -> destroy` 路径，导致大量 entity churn。目标态应直接走 `EffectCommand -> Spec Resolve -> Attribute Delta`，全程无 entity 创建。

2. **Active Effect Store 应用 enableable + slot**：duration/stack/period/granted state 进入 owner-local DynamicBuffer slot，用 enableable 标记 active，避免 per-frame entity create/destroy。

3. **ECB playback 唯一结构变化点**：`GasStructuralPlaybackSystemGroup` 是 hot path 中唯一允许结构变化的位置。System 目录中所有涉及 entity 创建/销毁的代码必须归属此 group。

4. **Presentation outbox 不创建 entity**：Cue/UI/VFX/SFX marker 是 transient stream，使用 NativeStream 或 DynamicBuffer，不应创建 entity。

5. **Debugger 必须报告结构变化指标**：每帧输出 `structuralChangeCount`、`entityCreated`、`entityDestroyed`、`componentAdded`、`componentRemoved`、`ecbCommandCount`、`syncPointCount`，以及**违反 phase contract 的结构变化来源 system（告警）**。

### 结构变化集中化 Phase 设计

```
目标态 frame backbone:

Phase 1: Command Ingest              ← 只读 EntityQuery，不结构变化
Phase 2: Effect Spec Resolve         ← IJobChunk，不结构变化
Phase 3: Attribute Delta Apply       ← IJobEntity，不结构变化
Phase 4: Typed Fact Fan-in           ← NativeStream merge，不结构变化
Phase 5: Gameplay Reaction           ← 只读事实，写 ECB command（延迟）
Phase 6: Active Effect Lifecycle     ← IJobEntity + Enableable toggle
Phase 7: Cleanup / Observation       ← 读写分离
Phase 8: STRUCTURAL PLAYBACK ← 唯一 ECB playback 点 ← 唯一 sync point
```

### 关键代码映射

| 代码位置 | 角色 | 结构变化约束 |
|---|---|---|
| `Assets/GAS/Runtime/System/SystemGroup/GASGroups.cs` | 定义 GasStructuralPlaybackSystemGroup | 该 group 是唯一结构变化容器 |
| `Assets/GAS/Runtime/System/Effect/SEffectApply.cs` | 效果应用处理 | 必须仅写 ECB，不能直接 EntityManager |
| `Assets/GAS/Runtime/System/Effect/GameplayEffectRequestWriter.cs` | 效果请求写入 | 必须通过 ECB 延迟 |
| `Assets/GAS/Runtime/Effect/Component/Dynamic/CEffectCommandSpecStream.cs` | EffectCommand 数据流 | 使用 NativeStream 而非 entity |
| `Assets/GAS/Runtime/Effect/Component/Dynamic/CActiveEffectStore.cs` | Active Effect 存储 | 使用 DynamicBuffer + Enableable，不创建实体 |

## 常见陷阱

1. **"我用 ECB 就能随便创建 entity"** — ECB 仍然产生结构变化，只是延迟合并。Instant GE 创建 entity 即使通过 ECB 也不应成为默认路径。能使用 enableable 或 DynamicBuffer 的地方优先使用。

2. **"连续 System 做结构变化只产生一个 sync point"** — 前提是中间没有调度 job。如果第一个 system 调度了 job，第二个 system 的结构变化会触发另一个 sync point。

3. **结构变化后不止 buffer 失效** — 所有 TypeHandle、ComponentLookup、BufferLookup、DynamicBuffer 引用全部失效。最容易遗漏的是 ComponentLookup。

4. **`AppendToBuffer` 不自动创建 buffer** — 如果 buffer 组件不存在会在 playback 失败。必须在 `AppendToBuffer` 前确保 buffer 已通过 `AddComponent` 添加或 entity 静态持有。

5. **ECB allocator 生命周期** — playback 后 rewind，不要跨帧持有 ECB 或从 ECB 分配的内存。

6. **`EntityQueryCaptureMode.AtPlayback` 非默认** — 默认是 `EntityQueryCaptureMode.AtRecord`。需要使用 `AtPlayback` 时必须显式传递参数。

7. **sortKey 有微开销** — `[ChunkIndexInQuery] int sortKey` 在并行 job 中需要使用 `CalculateBaseEntityIndexArrayAsync`，不是完全免费的。仅在需要确定性 playback 时使用。

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则 |
|---|---|---|
| `performance-sync-points.md` | 结构变化是 sync point 主因；连续两个做结构变化的 system 可合并为一个 sync point；ECB 合并结构变化降低 sync point 次数 | SC-01, SC-03, PRF-02, PRF-04, ECB-03 |
| `concepts-archetypes.html` | 每次 Add/Remove Component 触发 archetype 迁移；频繁移动 entity 降低性能 | SC-03 |
| `components-buffer-introducing.html` | 结构变化使 DynamicBuffer handle 失效 | SC-02 |
| `systems-entity-command-buffer.md` | ECB 录制/playback 机制；ECB 不是 gameplay event bus；allocator rewind | ECB-01, ECB-04 |
| `systems-entity-command-buffer-playback.md` | 确定性 ECB 回放；sortKey + ChunkIndexInQuery | CASE-21, CASE-35 |
| `components-buffer-command-buffer.md` | ECB AppendToBuffer vs SetBuffer 语义差异；AppendToBuffer 要求 buffer 已存在 | ECB-02 |
| `optimize-structural-changes.md` | EntityQuery bulk 操作批量结构变化；EntityQueryCaptureMode.AtPlayback | SC-03, CASE-34 |
| `systems-data-granularity.md` | IJobEntityChunkBeginEnd；chunk 级跳过 | CASE-38 |
| `systems-optimizing.html` | 每个 system 固定开销（TypeHandle + Lookup + Dependency） | PRF-04 |
| `common-errors.md` | IAspect deprecated；`[NativeDisableParallelForRestriction]` 安全抑制 | CASE-13 |

## 验收指标

1. Debugger 能报告每帧 `structuralChangeCount`、`ecbCommandCount`、`syncPointCount` 并按来源 system 分类
2. Hot path 零直接 `EntityManager.CreateEntity/DestroyEntity/AddComponent/RemoveComponent` 调用
3. 所有结构变化来源集中在 `GasStructuralPlaybackSystemGroup` 的 ECB playback phase
4. 不再出现 `BufferTypeHandle invalidated by structural change` 安全异常
5. AutoChess x50 压测中 `structuralChangeCount` 不随单位数线性增长
6. Instant GE 路径的 entity 创建/销毁随 instant 伤害数量呈零增长（迁移为 EffectCommand 直写）
7. 所有结构变化来源可通过 Debugger 追踪到具体 System 名称和所在 phase
8. Frame backbone 设计图中只标注一个结构变化 phase，sync point count 稳定在 1
