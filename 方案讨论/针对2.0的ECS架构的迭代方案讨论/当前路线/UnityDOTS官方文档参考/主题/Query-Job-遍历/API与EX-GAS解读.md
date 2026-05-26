# Query-Job-遍历: API 与 EX-GAS 解读

## 核心概念

### 三种遍历方式对比

| 方式 | 执行线程 | Burst | 适用规模 | Sync Point | EX-GAS 推荐场景 |
|------|----------|-------|----------|------------|----------------|
| `SystemAPI.Query` (idiomatic foreach) | 主线程 | 否 | 小规模（<100 实体） | **是** | Debugger 快照、Editor 工具、proof 验证 |
| `IJobEntity` | Worker 线程 | 是 | 中大规模（100-10K） | 否 | 简单 per-entity 变换、stateless 计算 |
| `IJobChunk` | Worker 线程 | 是 | 大规模（>10K） | 否 | chunk skip/mask/optional、批量统计、复杂循环 |

**核心规则：Runtime Core hot path 默认优先 IJobEntity 或 IJobChunk。`SystemAPI.Query` 触发的主线程 sync 在高频场景下不可接受。**

### SystemAPI.Query（主线程遍历）

```csharp
// 底层机制：source generator 创建并缓存 EntityQuery
// 每次 foreach 触发主线程 sync —— 等待所有相关 job 完成
// 适用于 proof、debug、Editor，不适用于 hot path
foreach (var (health, translation) in SystemAPI.Query<RefRO<Health>, RefRW<Translation>>())
{
    translation.ValueRW.Value += health.ValueRO.Value;
}
```

**关键事实：**
- Source generator 为每个 `SystemAPI.Query` 调用自动创建并缓存 `EntityQuery`
- `foreach` 触发主线程 sync point —— 等待所有写入相关 component 的 job 完成
- 主线程遍历导致所有 worker 线程闲置等待
- **CASE-01**：`SystemAPI.Query` 仅用于 Debugger 快照、Editor 工具、<100 entity 的 proof。任何 >100 entity 的 hot path 拒绝使用。

### IJobEntity（per-entity 并行 job）

IJobEntity 是 per-entity 遍历的首选。只需定义 `Execute` 方法，source generator 自动生成 IJobChunk 实现。

```csharp
[BurstCompile]
public partial struct AttributeDeltaApplyJob : IJobEntity
{
    [ReadOnly] public NativeHashMap<int, float> DeltaMap;

    public void Execute(ref BAttribute attribute, in CAttributeDeltaConsumer consumer)
    {
        if (DeltaMap.TryGetValue(consumer.AttributeCode, out var delta))
            attribute.CurrentValue += delta;
    }
}

// 调度
var job = new AttributeDeltaApplyJob { DeltaMap = deltaMap };
state.Dependency = job.ScheduleParallel(state.Dependency);
```

**Execute 参数支持的访问方式：**

| 参数类型 | 读写方式 | 说明 |
|----------|----------|------|
| `IComponentData` | `ref` = 读写, `in` = 只读 | 构成 query 条件 |
| `ICleanupComponentData` | `ref` / `in` | cleanup 组件 |
| `ISharedComponent` | `in` 只读 | 托管类型时 **不能 Burst** |
| `DynamicBuffer<T>` | `ref` / `in` | Buffer 访问 |
| `Entity` | 值拷贝 | 当前 entity ID |
| `IAspect` | — | **已废弃（deprecated since 1.4.6），禁止新代码使用** |
| `int` + `[ChunkIndexInQuery]` | 值拷贝 | chunk 索引 |
| `int` + `[EntityIndexInChunk]` | 值拷贝 | chunk 内 entity 索引 |
| `int` + `[EntityIndexInQuery]` | 值拷贝 | **性能差，内部调用 `CalculateBaseEntityIndexArray`** |

**IJobEntity 的 query 定制属性：**

```csharp
[WithAll(typeof(BTargetable))]
[WithNone(typeof(CDead))]
[WithChangeFilter(typeof(BAttribute))]
[BurstCompile]
public partial struct MyJob : IJobEntity
{
    public void Execute(ref BAttribute attr) { ... }
}
```

**CASE-02**：`IJobEntity` 是 Runtime Core hot path 主力，用于普通 per-entity 计算。当需要 chunk 级条件跳过时改用 IJobChunk。

### IJobChunk（chunk 级批处理）

当需要 chunk 级跳过、optional component 判断、或非标准遍历顺序时使用 IJobChunk。

```csharp
[BurstCompile]
public struct EffectSpecChunkJob : IJobChunk
{
    [ReadOnly] public ComponentTypeHandle<CSpecRequest> SpecRequestHandle;
    public ComponentTypeHandle<BAttribute> AttributeHandle;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                        bool useEnabledMask, in v128 chunkEnabledMask)
    {
        var specs = chunk.GetNativeArray(ref SpecRequestHandle);
        var attrs = chunk.GetNativeArray(ref AttributeHandle);

        var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
        while (enumerator.NextEntityIndex(out var i))
        {
            attrs[i] = ApplySpec(attrs[i], specs[i]);
        }
    }
}
```

**IJobChunk 适用场景：**
- 不遍历 entity（如收集 chunk 统计信息）
- 多次遍历同一 chunk 的 entity
- 需要异常遍历顺序
- 需要 chunk 级条件跳过

**IJobEntity vs IJobChunk 选择：**
- 大多数 per-entity 遍历应使用 IJobEntity（CASE-02）
- IJobEntity 底层生成 IJobChunk，自动享受未来 source gen 优化
- 需要 `IJobEntityChunkBeginEnd` 接口做 chunk 级前后处理
- **CASE-03**：`IJobChunk` 用于批量统计、enableable 过滤、非标准遍历。简单 per-entity 变换用 IJobEntity。

### Job 调度与依赖管理

```csharp
public partial struct MySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var job1 = new Job1 { ... };
        state.Dependency = job1.ScheduleParallel(state.Dependency);

        var job2 = new Job2 { ... };
        state.Dependency = job2.ScheduleParallel(state.Dependency);

        var job3 = new Job3 { ... };
        state.Dependency = JobHandle.CombineDependencies(
            state.Dependency,
            job3.ScheduleParallel(m_otherQueryDependency)
        );
    }
}
```

**依赖链工作原理：**
1. `state.Dependency` 是当前 system 的"等待门"——下一个 system 的 job 自动等待它
2. 每个 system 的 `OnUpdate` 前，ECS 注入对前驱 system 写入 component 的等待
3. 手动组合依赖用 `JobHandle.CombineDependencies`
4. **依赖链长度和复杂度直接影响调度性能**（SYS-03）

**调度模式对比：**

| 方式 | 并行度 | 适用场景 | 注意事项 |
|------|--------|----------|----------|
| `ScheduleParallel` | 多 worker 线程并行 | 无 entity 间依赖的遍历 | 默认模式，首选 |
| `ScheduleSingle` | 单 worker 线程 | 需要顺序处理、全局状态 | 不阻塞主线程，但并行度低 |
| `Schedule`（按 chunk） | 每个 chunk 一个 job | chunk 间无依赖的批处理 | 注意 chunk count 与 job count 的关系 |
| `Run` | 主线程同步 | proof/debug 小规模 | 触发 sync point，不推荐 hot path |

### EntityQuery 与 Query Filter

```csharp
var query = new EntityQueryBuilder(Allocator.Temp)
    .WithAll<BAttribute, CTagMask>()
    .WithNone<CDead>()
    .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
    .Build(ref state);
```

**EntityQueryOptions 关键选项：**
- `IgnoreComponentEnabledState`：忽略 enableable 状态。**不需要 sync point**，效率更高
- 默认（无此选项）：遵守 enableable 过滤，**每次同步查询触发 sync point**

**同步 vs 异步 Query 操作：**

| 方法类型 | Sync Point | 返回值 | 说明 |
|----------|------------|--------|------|
| 同步（`ToEntityArray`） | 是 | `NativeArray` | 等待所有相关 job 完成 |
| 异步（`ToEntityArrayAsync`） | 否 | `NativeList` | 调度 job 完成操作 |

**ChangeFilter 与 chunk.DidChange：**

| 机制 | 作用层级 | 判断依据 | 适用场景 |
|------|----------|----------|----------|
| `[WithChangeFilter]` | Query 级 | chunk 中**任意** entity 的**任意**指定 component 变更 → 整个 chunk 通过 | 粗粒度：只想处理"有变化"的 chunk |
| `chunk.DidChange` | Chunk 内逐 component | **每种** component type 独立判断（CASE-18） | 精细：只重新计算变更的输入源 |
| `chunk.DidOrderChange` | Chunk 内 | entity 在 chunk 中的顺序是否变化 | entity 重排检测 |

### ComponentLookup / BufferLookup

随机访问 component 数据的工具，但每次 lookup 只有 entity 级别精度，无法批量优化。

```csharp
var healthLookup = state.GetComponentLookup<BAttribute>(isReadOnly: true);

public struct MyJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<BAttribute> AttributeLookup;

    public void Execute(Entity source, ref BEffectTarget target)
    {
        if (AttributeLookup.TryGetComponent(source, out var attr))
        {
            // 单 entity 随机访问 —— 高频场景下成本显著
        }
    }
}
```

**关键事实：**
- Lookup 在结构变化后失效，必须重取
- 高频 random lookup 成本远高于顺序遍历
- 优先考虑 owner-local buffer 替代跨 entity random lookup

### IJobEntity Execute 参数语义

| 修饰符 | 读写 | Query 条件 | Job 依赖 | 说明 |
|--------|------|------------|----------|------|
| `ref` | 读写 | 必需存在（WithAll） | 自动建立写依赖 | 修改影响其他 system |
| `in` | 只读 | 必需存在（WithAll） | 只读依赖 | ECS 安全系统保证不被写 |
| 值类型（无修饰符） | 值拷贝 | 不构成 query 条件 | 无依赖 | Entity/chunkIndex 等元数据 |
| `EnabledRefRW<T>` | 读写 enable 状态 | 必需存在 | 写依赖 | 只操作 enable 位 |
| `EnabledRefRO<T>` | 只读 enable 状态 | 必需存在 | 只读依赖 | 查询 enable 位 |

**Optional Component 的 Chunk 级处理（QRY-03）：**

```csharp
[BurstCompile]
public struct OptionalComponentJob : IJobChunk
{
    public void Execute(in ArchetypeChunk chunk, ...)
    {
        bool hasOptional = chunk.Has(ref OptionalTypeHandle);
        if (hasOptional)
        {
            var data = chunk.GetNativeArray(ref OptionalTypeHandle);
        }
    }
}
```

---

## EX-GAS 项目解读

### EffectCommand 批处理

使用 IJobChunk + chunk 级效果分类。同类 GE 效果（如所有即时伤害）聚合同一 chunk，减少 query 次数。EffectCommand 使用 IJobChunk 的 optional component 模式处理不同类型的效果参数。

### Attribute Delta 归并

优先按 target ASC 分组后顺序 apply，避免 random lookup 散落。DeltaMap 使用 `NativeHashMap<int, float>` 通过 IJobEntity 传入，实现 per-entity 的批量 delta 应用。

### ActiveEffectStore 周期 tick

使用 enableable 标记 active/inactive，IJobEntity 只遍历 active slot。禁止用 `ToEntityArray` 全量扫。参考 CASE-06（Enableable toggle）和 CASE-20（EnabledMask 批量操作）。

### Tag Query / 目标扫描

使用 `WithChangeFilter` + cached query result 减少重复扫描。对实时性要求高的目标列表，使用只读 `ComponentLookup` 增量更新。参考 CASE-18（`chunk.DidChange`）做精细变更检测。

### 事件总线与观察者解耦

`CGameplayEventBus` 应避免在 event handling 中做同步 query 操作。Event handler 只做 fan-in 收集，不触发 query sync point。参考 CASE-12（NativeStream 并行 fan-in + deterministic merge）。

### 关于当前实现的诊断

- 当前 `SHeadlessAutoChessDriver` 使用 `ToEntityArray` 全量扫描 + 大 `UnitSnapshot` 构造 → 违反 PRF-05（Hot Path 禁止主线程遍历）。
- Effect application 路径中的 `ComponentLookup<BAttribute>` 跨 entity 读取 → 注意 PRF-19 竞态风险。
- EventBus 中同步 query 操作 → 注意 PRF-09 sync 触发。
- 各 IJobEntity 缺少 EntityQuery 注释 → 增加 PRF-20 风险。

### 相关 CASE 模式

- **CASE-01 (SystemAPI.Query)**：仅用于 Debugger 快照、Editor 工具、<100 entity 的 proof。任何 >100 entity 的 hot path 拒绝使用。
- **CASE-02 (IJobEntity)**：Runtime Core hot path 主力。简单 per-entity 变换首选。
- **CASE-03 (IJobChunk)**：批量统计、enableable 过滤、非标准遍历。简单 per-entity 变换用 IJobEntity。
- **CASE-18 (chunk.DidChange)**：在 IJobChunk 内按 component type 逐一检查 chunk 是否变更，比 query 级 filter 更灵活。属性脏标记检测、effect 重评估跳过。

---

## 常见陷阱

1. **`query.CalculateEntityCount()` 触发 sync point**：enableable 过滤下会等待所有写 job。使用 `IgnoreFilter` 变体或异步变体。
2. **ChangeFilter 是 chunk 级不是 entity 级**：不能用于精确单 entity 变更检测。整个 chunk 中只要有一个 entity 变更，所有 entity 都被处理。
3. **`EntityIndexInQuery` 性能差**：内部依赖 `CalculateBaseEntityIndexArray`，不是 O(1) 索引。避免在高频路径使用。
4. **结构变化后 lookup/handle 失效**：任何 `CreateEntity`/`AddComponent` 后必须重取 TypeHandle 和 ComponentLookup。
5. **`ScheduleParallel` 并行度由 chunk 数量决定**：chunk 太少时并行度不足。需要评估 entity 分布对 chunk packing 的影响。
6. **`ref` 参数标记 chunk 为已变更**：即使未实际修改数据，`ref` 也触发 chunk 写标记。`in` 只读参数不触发。
7. **异步 query 返回 `NativeList` 不是 `NativeArray`**：因为最终匹配量在 job 运行时才知道。需要考虑 `NativeList` 的内存管理。
8. **`IJobEntity` 参数比 query 多 → job 不调度任何 entity**：查不出问题，纯静默。重构后必须交叉验证。
9. **Enableable component 在 `SimulationSystemGroup` 中的 `LocalToWorld` 可能过期**：不是本领域的直接陷阱，但与 query 条件组合时可能误判 entity 位置。
10. **`GetSingletonRW` 不等待 job 完成**：当 IJobEntity 还在写入被访问的 singleton 时，使用 `GetSingletonRW` 读取可能导致竞态。
11. **IJobEntity 中 `DynamicBuffer<T>` 在 `SystemAPI.Query` 内默认可读写**：只读场景需自定义实现避免不必要 sync point。
12. **ECB 复用导致命令交错**：多个 job 复用同一个 ECB 且使用相同 sortKey → 命令交错，确定性回放被破坏。

---

## 官方证据

| 官方文档 | 关键结论 | 关联规则 |
|----------|----------|----------|
| `iterating-data-ijobentity.html` | IJobEntity 自动生成 IJobChunk；支持 WithAll/Any/None/ChangeFilter | QRY-01, QRY-02, CASE-02 |
| `iterating-data-ijobchunk.html` | IJobChunk 用于 chunk 级操作和非标准遍历；chunk.Has() 判断 optional | QRY-03, CASE-03 |
| `systems-systemapi-query.md` | SystemAPI.Query 是主线程 foreach；source generator 自动创建 query | PRF-05, CASE-01 |
| `performance-sync-points.md` | `Run` 和 idiomatic foreach 导致主线程同步阻塞；sync point 成因详解 | PRF-05, PRF-09 |
| `systems-optimizing.html` | 每个 system 有 TypeHandle 刷新、Lookup 创建、Dependency 链三种固定开销 | JOB-02 (跨文档) |
| `components-enableable-use.html` | Random-access 方法额外开销；enableable 查询成本；IgnoreFilter 无 sync 成本 | PRF-06, PRF-09 |
| `systems-looking-up-data.md` | ComponentLookup 随机访问竞态条件；NativeDisableParallelForRestriction | PRF-19 |
| `concepts-safety.md` | IJobEntity 不验证 Execute 参数与 EntityQuery 匹配；ExclusiveEntityTransaction | PRF-20 |
| `common-errors.md` | 嵌套 job 的 safety handle 不正确；IJobEntity 参数不匹配问题 | PRF-23 |
| `systems-version-numbers.md` | ChangeFilter chunk 级触发；手动调用 Update 破坏版本号 | (核心概念) |
| `aspects-intro.md` | Aspects 已废弃，将移除 | PRF-17 |
| `iterating-data-ijobchunk-implement.md` | `CalculateBaseEntityIndexArrayAsync` per-entity 索引；type handle 每帧更新 | JOB-03, PRF-08 |
| `systems-entity-command-buffers.md` | ECB 最佳实践；独立 ECB per job | (常见陷阱) |
| `systems-data-granularity.md` | 读写数据分离避免响应式系统误触发 | JOB-04 |
