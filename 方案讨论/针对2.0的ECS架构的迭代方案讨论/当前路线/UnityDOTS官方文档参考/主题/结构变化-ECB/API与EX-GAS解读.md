# 结构变化-ECB: API 与 EX-GAS 解读

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

---

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

---

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
