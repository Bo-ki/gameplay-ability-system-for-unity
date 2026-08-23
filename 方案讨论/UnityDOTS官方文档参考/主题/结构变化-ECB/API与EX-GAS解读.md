# 结构变化-ECB: API 与 EX-GAS 解读

## 核心概念

### 结构变化 (Structural Change)

结构变化是改变 entity archetype 的任何操作，包括：

- `CreateEntity` / `DestroyEntity`
- `AddComponent` / `RemoveComponent`
- 修改 shared component 值（实体会移动到具有目标 shared value 的 chunk）

**关键事实：job 不能直接执行结构变化；可以在 job 中用 ECB 录制命令，之后在主线程 playback。启用/禁用 `IEnableableComponent` 不属于结构变化。**

每次结构变化触发 entity 的 archetype 迁移——将 entity 数据从原 chunk 复制到目标 archetype 的 chunk。这一过程涉及数据复制与 chunk 重新分配，成本随 entity 数量和 component 数量上升。

### Sync Point（同步点）

当主线程需要安全访问 ECS 数据时，必须等待所有相关的已调度 job 完成。这个等待点称为 sync point。

```
主线程执行结构变化
  -> 等待所有已调度 job 完成 (sync point)
  -> 执行结构变化
  -> 所有指向 chunk 组件数据的直接引用失效
```

**触发 sync point 的操作：**
1. 直接执行结构变化（`EntityManager.CreateEntity` 等）
2. 带有 enableable 过滤的同步 query 操作：`query.ToEntityArray()`、`query.CalculateEntityCount()`
3. `SystemAPI.Query` foreach / job `Run()` 在必要依赖尚未完成时会等待
4. ECB playback

**sync point 是性能杀手的原因：**
- 阻塞主线程直到所有 worker 线程完成；期间主线程空闲
- 结构变化后重新获取 `DynamicBuffer`、chunk array、Ref 等直接引用；缓存 TypeHandle/Lookup 在下一次使用前 `.Update(ref state)`
- 后续访问必须重新取得直接引用，或在调度前刷新缓存 TypeHandle/Lookup

### Sync Point 成本模型

一个 sync point 的总成本由三部分叠加：

```
Sync Point 总成本 = 等待依赖 job 完成 + 执行结构变化 + 后续调度开销
```

1. **等待依赖 job 完成**：主线程在必要依赖完成前不能继续。实际损失取决于剩余依赖链、可用并行度和关键路径，不能用“最长剩余时间 × worker 数”估算
2. **执行结构变化本身**：创建/销毁 entity、archetype 迁移、数据复制
3. **后续访问准备**：直接引用需重取；缓存 TypeHandle/Lookup 在使用前需刷新

**多 System 结构变化的合并效果（来自 `performance-sync-points.md`）：**

> "Two systems that both make structural changes only create one sync point if they update sequentially, unless the first one also schedules jobs, in which case the second one will immediately sync on them."

推论：
- 连续的两个 system 都做结构变化 = 只有 1 个 sync point（前提是第一个没调度 job）
- 如果第一个 system 调度了 job，第二个做结构变化前就必须等待该 job → 2 个 sync point
- **EX-GAS 策略：Runtime Core 默认集中在约定 Structural Commit 区域；其它时序需求可建立有证据的独立 phase**

**Sync Point 与帧预算：** Unity 没有给出每个 sync point 的固定毫秒成本。实际耗时取决于调用点尚未完成的相关 job 和结构变化工作量；必须在目标设备 Profiler 中分别测量等待与 playback 本体。

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
    public EntityCommandBuffer.ParallelWriter ECB;

    public void Execute([ChunkIndexInQuery] int sortKey, Entity e, in CApplyEffectRequest request)
    {
        var effectEntity = ECB.CreateEntity(sortKey);
        ECB.AddComponent(sortKey, effectEntity, new CEffectInUsage { ... });
    }
}

// 调度 job 时传入 ECB
var job = new CreateEffectEntityJob { ECB = ecb.AsParallelWriter() };
state.Dependency = job.ScheduleParallel(state.Dependency);
// ECB 在对应的 ECBSystem 的 update 中自动 playback
```

**ECB 的 buffer 操作：**
```csharp
ECB.AppendToBuffer<T>(entity, new T { ... });   // 追加到已有 buffer
ECB.SetBuffer<T>(entity);                         // 设置整个 buffer（覆盖）
// 注意：AppendToBuffer 要求 buffer 已存在；不存在时使用 AddBuffer<T> 并保证命令排序在 append 之前
```

**默认 ECB System 位置：**

每个根 Group 的首尾各有一个 ECB System。例如 `SimulationSystemGroup`：
- `BeginSimulationEntityCommandBufferSystem`（Simulation 最前 playback）
- `EndSimulationEntityCommandBufferSystem`（Simulation 最后 playback）

#### 串行录制、并行录制与 Playback

- 普通 `EntityCommandBuffer` writer 用于单一生产者的顺序录制，不应被多个线程并发写入。
- 并行 job 必须先调用 `AsParallelWriter()`，再使用 `EntityCommandBuffer.ParallelWriter` 的命令重载传入 `sortKey`。并行录制的实际先后受调度影响，不能当作业务顺序。
- playback 始终在主线程单线程执行。执行前必须完成生产者 job；若 job 尚未完成，这个边界会产生实际等待。
- 对并行命令，Unity 在 playback 前按 `sortKey` 排序，较小 key 先执行。只有 key 独立于调度且能处理同 key/跨 job 的顺序关系时，才能得到所需确定性；连续 job 复用同一 ECB 且 key 域重叠可能导致命令交错，官方建议不同 job 使用独立 ECB。
- ECB 内部命令链或线程分段属于实现细节，不作为 EX-GAS 的稳定 API 契约。
- ECB System 负责播放并释放由它创建的 ECB；手工创建的 ECB 则由调用方完成依赖、playback 和 `Dispose()`。

#### 自定义 ECB System 的摆放

```csharp
// 当前项目在结构提交阶段末尾播放专用 ECB
[UpdateInGroup(typeof(GASStructuralCommitSystemGroup), OrderLast = true)]
public partial class EndGASStructuralCommitECBSystem : EntityCommandBufferSystem { }
```

#### ECB PlaybackPolicy.MultiPlayback（CASE-21）

通过 `new EntityCommandBuffer(Allocator.TempJob, PlaybackPolicy.MultiPlayback)` 创建可多次 playback 的 ECB。同一 ECB 内先 `CreateEntity()` 后通过 placeholder entity 引用该实体，deferred entity remapping 会在每次 playback 中完成映射。MultiPlayback 与并行排序是两个独立维度：只有先取得 `AsParallelWriter()`，并调用 `ParallelWriter` 的带 `sortKey` 重载时，`[ChunkIndexInQuery]` 等 key 才参与并行命令排序。

#### EntityQueryCaptureMode.AtPlayback（CASE-34）

ECB 录制时保存 query，playback 时重新评估匹配结果。它适合需要以 playback 时实体集合为准的批量结构变化；是否优于录制期捕获或 EntityManager bulk API 必须按目标场景测量。

#### [ChunkIndexInQuery] sortKey（CASE-35）

并行 job 中录制顺序不确定但 playback 需要稳定 query-local 顺序时，将 `[ChunkIndexInQuery] int sortKey` 传给 `EntityCommandBuffer.ParallelWriter`。它只保证当前 query/chunk 域内的调度无关排序；battle replay 或跨 query/job 的业务全序仍需稳定业务 total key 和 tie-breaker。

### 直接引用失效与 Handle 更新

结构变化后，指向组件存储的直接数据引用会失效：

```csharp
// 错误：结构变化后继续使用旧 handle
var buffer = state.EntityManager.GetBuffer<MyElement>(entity);
state.EntityManager.CreateEntity();  // 结构变化！
var x = buffer[0];  // 安全系统抛异常：handle 已失效

// 正确：结构变化后重取直接数据引用
var buffer = state.EntityManager.GetBuffer<MyElement>(entity);
state.EntityManager.CreateEntity();  // 结构变化
buffer = state.EntityManager.GetBuffer<MyElement>(entity);  // 重取
var x = buffer[0];  // 安全
```

`DynamicBuffer`、chunk `NativeArray` / `BufferAccessor`、`RefRO/RefRW` 等直接引用必须重取。缓存的 TypeHandle/Lookup 可保留为字段，在下一次使用或调度前调用 `.Update(ref state)`；`EntityQuery` 可跨帧缓存。

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

1. **Instant GE 不创建 runtime GE entity**：当前实现由 `GEEffectSpecBuildSystem` 将 owner-local command 解析为 spec，再由 `GASAttributeSetReduceApplySystem` 应用属性变化；审查时应防止重新引入“一次效果一个临时 entity”的路径。

2. **Active Effect Store 使用 owner-local slot**：duration、stack、period 与 granted state 进入 `ActiveGameplayEffectBuffer`；状态字段与索引负责筛选，是否引入 enableable 必须由查询收益和 Profiler 数据决定。

3. **ECB playback 默认结构提交点**：`GASStructuralCommitSystemGroup` 是 Runtime Core 的默认结构提交阶段；有不同可见性需求的例外必须声明 phase contract 并提供 Profiler 证据。

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
Phase 8: STRUCTURAL PLAYBACK ← Runtime Core 默认结构提交点
```

### 关键代码映射

| 代码位置 | 角色 | 结构变化约束 |
|---|---|---|
| `Assets/GAS/Runtime/System/SystemGroup/GASGroups.cs` | 定义 `GASStructuralCommitSystemGroup` 及首尾 ECB system | 默认结构提交阶段；例外按 phase contract 审查 |
| `Assets/GAS/Runtime/System/Effect/GEEffectInstantSystems.cs` | 即时效果 spec 构建与属性应用 | 保持 owner-local 数据流，不为即时效果创建临时 entity |
| `Assets/GAS/Runtime/System/Effect/GameplayEffectRequestWriter.cs` | 效果请求写入入口 | 写入方式必须服从请求的可见性时序，结构变化集中到声明的提交阶段 |
| `Assets/GAS/Runtime/Effect/Component/Dynamic/GEEffectCommandSpecStream.cs` | Effect command/spec 与帧内事实缓冲 | 使用 singleton owner 上的 buffer 承载帧内数据，不把 ECB 当作事件总线 |
| `Assets/GAS/Runtime/Effect/Component/Dynamic/ActiveEffectStore.cs` | Active Effect owner-local slot 与索引 | 使用 `ActiveGameplayEffectBuffer`；筛选策略依据访问模式和测量结果选择 |

---

## 常见陷阱

1. **"我用 ECB 就能随便创建 entity"** — ECB 仍然产生结构变化，只是延迟合并。Instant GE 创建 entity 即使通过 ECB 也不应成为默认路径。能使用 enableable 或 DynamicBuffer 的地方优先使用。

2. **"连续 System 做结构变化只产生一个 sync point"** — 前提是中间没有调度 job。如果第一个 system 调度了 job，第二个 system 的结构变化会触发另一个 sync point。

3. **结构变化后区分引用与访问器** — DynamicBuffer/chunk array/ref 等直接引用必须重取；缓存 TypeHandle/Lookup 在下次使用前 Update。

4. **`AppendToBuffer` 不自动创建 buffer** — 如果 buffer 组件不存在会在 playback 失败。必须在 `AppendToBuffer` 前确保 buffer 已通过 `AddComponent` 添加或 entity 静态持有。

5. **ECB allocator 生命周期** — ECB System 创建的 ECB 自动 playback/dispose；手工 ECB 按 allocator、PlaybackPolicy 和显式 Dispose 管理。

6. **`EntityQueryCaptureMode.AtPlayback` 非默认** — 默认是 `EntityQueryCaptureMode.AtRecord`。需要使用 `AtPlayback` 时必须显式传递参数。

7. **不要混淆索引成本** — `ChunkIndexInQuery` 是高效 ECB sort key；`CalculateBaseEntityIndexArrayAsync` 的准备成本属于 `EntityIndexInQuery`。

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则 |
|---|---|---|
| `performance-sync-points.md` | 结构变化是 sync point 主因；连续两个做结构变化的 system 可合并为一个 sync point；ECB 合并结构变化降低 sync point 次数 | SC-01, SC-03, PRF-02, PRF-04, ECB-03 |
| `concepts-archetypes.md` | 每次 Add/Remove Component 触发 archetype 迁移；频繁移动 entity 降低性能 | SC-03 |
| `components-buffer-introducing.md` | 结构变化使 DynamicBuffer handle 失效 | SC-02 |
| `systems-entity-command-buffers.md` | ECB 录制/playback 机制；ECB 不是 gameplay event bus；allocator rewind | ECB-01, ECB-04 |
| `systems-entity-command-buffer-playback.md` | 确定性 ECB 回放；sortKey + ChunkIndexInQuery | CASE-21, CASE-35 |
| `components-buffer-command-buffer.md` | ECB AppendToBuffer vs SetBuffer 语义差异；AppendToBuffer 要求 buffer 已存在 | ECB-02 |
| `optimize-structural-changes.md` | EntityQuery bulk 操作批量结构变化；EntityQueryCaptureMode.AtPlayback | SC-03, CASE-34 |
| `systems-data-granularity.md` | IJobEntityChunkBeginEnd；chunk 级跳过 | CASE-38 |
| `systems-optimizing.md` | 每个 system 固定开销（TypeHandle + Lookup + Dependency） | PRF-04 |
| `common-errors.md` | IAspect deprecated；`[NativeDisableParallelForRestriction]` 安全抑制 | CASE-13 |
