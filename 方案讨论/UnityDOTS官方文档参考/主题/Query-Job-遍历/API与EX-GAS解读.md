# Query-Job-遍历: API 与 EX-GAS 解读

## 核心概念

### 三种遍历方式对比

| 方式 | 执行位置 | Burst | 依赖行为 | EX-GAS 推荐场景 |
|------|----------|-------|----------|----------------|
| `SystemAPI.Query` (idiomatic foreach) | 主线程 | 可（受 `ISystem` / Burst 上下文约束） | foreach 前自动完成必要依赖 | 简单主线程遍历、立即消费结果、Debugger/Editor |
| `IJobEntity` | `Run` 时主线程；`Schedule*` 时 job system | job 可 Burst | 通过 `JobHandle` 传递 | 常规 per-entity 变换 |
| `IJobChunk` | `Run` 时主线程；`Schedule*` 时 job system | job 可 Burst | 通过 `JobHandle` 传递 | chunk skip/mask/optional、批量统计、复杂循环 |

**核心规则：Runtime Core hot path 默认评估 IJobEntity / IJobChunk 调度，但最终选择由数据访问形态、依赖等待、可并行工作量和目标设备 Profiler 决定；Unity 没有提供按实体数量划分的固定阈值。**

### SystemAPI.Query（主线程遍历）

```csharp
// 底层机制：source generator 创建并缓存 EntityQuery
// foreach 前 source generator 会完成必要依赖；相关 job 未完成时主线程等待
// 主线程遍历；是否适合 hot path 由实际工作量和 Profiler 决定
foreach (var (health, translation) in SystemAPI.Query<RefRO<Health>, RefRW<Translation>>())
{
    translation.ValueRW.Value += health.ValueRO.Value;
}
```

**关键事实：**
- Source generator 为每个 `SystemAPI.Query` 调用自动创建并缓存 `EntityQuery`
- `foreach` 前会自动完成必要 read/write 依赖；若相关 job 未完成，会表现为主线程等待
- 只有必要依赖尚未完成时才会发生主线程等待；无关 job 不会因此全部停止
- `SystemAPI.Query` 可在合适上下文 Burst 编译，但遍历本身仍在主线程，不提供 worker 并行
- **CASE-01**：用于逻辑简单、需要立即消费结果或不值得调度 job 的主线程遍历；hot path 需要 Profiler 证据

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
| `ISharedComponent` | `in` 只读 | managed shared component 不能 Burst 或 schedule，只能 `.Run()`；unmanaged 版本可使用对应 job/Burst API |
| `DynamicBuffer<T>` | `ref` / `in` | Buffer 访问 |
| `Entity` | 值拷贝 | 当前 entity ID |
| `IAspect` | — | **已废弃（deprecated since 1.4.6），禁止新代码使用** |
| `int` + `[ChunkIndexInQuery]` | 值拷贝 | chunk 索引 |
| `int` + `[EntityIndexInChunk]` | 值拷贝 | chunk 内 entity 索引 |
| `int` + `[EntityIndexInQuery]` | 值拷贝 | 调度时需 `CalculateBaseEntityIndexArray[Async]` 准备 chunk offset；只在需要 packed index 时使用 |

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

        if (!useEnabledMask)
        {
            for (var i = 0; i < chunk.Count; i++)
                attrs[i] = ApplySpec(attrs[i], specs[i]);
            return;
        }

        var enumerator = new ChunkEntityEnumerator(true, chunkEnabledMask, chunk.Count);
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
| `Run` | 主线程同步 | 需要立即得到结果或不值得调度 | 完成必要输入依赖；等待成本取决于依赖是否已完成 |

### EntityQuery 与 Query Filter

```csharp
var query = new EntityQueryBuilder(Allocator.Temp)
    .WithAll<BAttribute, CTagMask>()
    .WithNone<CDead>()
    .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
    .Build(ref state);
```

**EntityQueryOptions 关键选项：**
- `IgnoreComponentEnabledState`：忽略 enableable 状态；不会为 enabled 状态写 job 等待，但会改变匹配语义
- 默认（无此选项）：遵守 enableable 过滤；同步 Query 操作仅在相关 enableable 写 job 尚未完成时等待

**同步 vs 异步 Query 操作：**

| 方法类型 | Sync Point | 返回值 | 说明 |
|----------|------------|--------|------|
| 同步（`ToEntityArray`） | 依赖未完成时等待 | `NativeArray` | 立即返回可读结果 |
| 异步（`ToEntityArrayAsync`） | 调用点不等待 | `NativeList` | 调度收集 job；完成前不能访问列表 |

**ChangeFilter 与 chunk.DidChange：**

| 机制 | 作用层级 | 判断依据 | 适用场景 |
|------|----------|----------|----------|
| `[WithChangeFilter]` | Query 级 | chunk 中**任意** entity 的**任意**指定 component 变更 → 整个 chunk 通过 | 粗粒度：只想处理"有变化"的 chunk |
| `chunk.DidChange` | Chunk 内逐 component | **每种** component type 独立判断（CASE-18） | 精细：只重新计算变更的输入源 |
| `chunk.DidOrderChange` | Chunk 内 | entity 在 chunk 中的顺序是否变化 | entity 重排检测 |

### ComponentLookup / BufferLookup

随机访问任意 entity 组件数据的官方工具；适合跨实体依赖，但局部性通常弱于 query/chunk 顺序访问。

```csharp
var healthLookup = state.GetComponentLookup<BAttribute>(isReadOnly: true);

public struct MyJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<BAttribute> AttributeLookup;

    public void Execute(Entity source, ref BEffectTarget target)
    {
        if (AttributeLookup.TryGetComponent(source, out var attr))
        {
            // 单 entity 随机访问；是否为瓶颈需要基于目标平台测量
        }
    }
}
```

**关键事实：**
- 缓存的 Lookup 在使用/调度前调用 `.Update(ref state)` 刷新；无需在每次结构变化后销毁并重新创建字段
- Unity 定性说明 random access 是最低效的数据访问方式之一，但没有给出固定倍率
- 热点成立时再评估 owner-local、分组或 chunk 顺序布局，并同时考虑复制与同步成本

### IJobEntity Execute 参数语义

| 修饰符 | 读写 | Query 条件 | Job 依赖 | 说明 |
|--------|------|------------|----------|------|
| `ref` | 读写 | 必需存在（WithAll） | 自动建立写依赖 | 修改影响其他 system |
| `in` | 只读 | 必需存在（WithAll） | 只读依赖 | ECS 安全系统保证不被写 |
| `Entity` / 带索引特性的 `int` | 值拷贝 | 不额外增加组件条件 | 元数据，无组件依赖 | 当前 entity、chunk/entity index；普通 `IComponentData` 必须用 `ref` 或 `in` |
| `DynamicBuffer<T>` | `ref` 读写 / `in` 只读 | 必需存在 | 按修饰符建立依赖 | 只读路径必须显式使用 `in` |
| Managed component | 值拷贝读写 / `in` 只读 | 必需存在 | 托管访问 | 不能 Burst 或 schedule，只能 `.Run()` |
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

当 EffectCommand 的 archetype 布局已使同类数据落在相同 chunk 时，可用 `IJobChunk` 按 chunk 读取共享字段与可选参数。是否合并不同效果类型，取决于实际 chunk 分布、无用数据抓取、分支成本和 Profiler，不能假设业务类型会自动聚集到同一 chunk。

### Attribute Delta 归并

属性 delta 先按稳定 target/attribute key 归并，再由拥有目标数据的阶段批量应用，避免多个并行写入者随机修改同一 owner。承载结构可在 owner-local buffer、稳定分区 stream 与排序数组之间选择；不能在没有并发、容量和确定性证明时固定为某一种 HashMap。

### ActiveEffectStore 周期 tick

`ActiveGameplayEffectBuffer` 的 slot 通过状态字段和辅助索引表达 active/inactive；buffer element 不能作为 enableable component 单独开关。只有 Profiler 证明 owner entity 级过滤有收益时，才评估额外的 enableable 标记；遍历策略同时参考 slot 分布、chunk early-out 和索引维护成本。

### Tag Query / 目标扫描

`WithChangeFilter` 或 `chunk.DidChange` 只能把发生过相关写入的 chunk 作为候选，不能给出 entity 级精确变更，也不会自动维护缓存列表。需要持久目标索引时，应声明其 owner、更新与失效时机；`ComponentLookup` 只是随机访问工具，不等于增量索引。查询收集、显式索引和按需 lookup 由一致性要求与 Profiler 决定。

### 事件总线与观察者解耦

Gameplay fact/event 生产阶段应避免为了立即消费而同步物化任意 query。生产者写入 owner-local fact buffer 或声明过的 fan-in 容器，消费者再按 system 依赖读取；若业务确需同阶段立即查询，必须显式记录可见性与等待成本。参考 CASE-12（NativeStream 并行 fan-in + deterministic merge）。

### 关于当前实现的诊断

- 当前 `AutoChessGasBattleUnitSnapshotProjector` 从结构化日志投影托管快照，属于边界层数组处理而非 ECS Query；其成本与 Runtime Core query/job 成本必须分开记录。
- Effect application 路径中的 `ComponentLookup<BAttribute>` 跨 entity 读取 → 注意 PRF-19 竞态风险。
- EventBus 中同步 query 操作 → 注意 PRF-09 sync 触发。
- 各 IJobEntity 缺少 EntityQuery 注释 → 增加 PRF-20 风险。

### 相关 CASE 模式

- **CASE-01 (SystemAPI.Query)**：主线程直接遍历；hot path 以依赖等待、工作量和 Profiler 决定是否 job 化。
- **CASE-02 (IJobEntity)**：Runtime Core hot path 主力。简单 per-entity 变换首选。
- **CASE-03 (IJobChunk)**：批量统计、enableable 过滤、非标准遍历。简单 per-entity 变换用 IJobEntity。
- **CASE-18 (chunk.DidChange)**：在 IJobChunk 内按 component type 逐一检查 chunk 是否变更，比 query 级 filter 更灵活。属性脏标记检测、effect 重评估跳过。

---

## 常见陷阱

1. **`query.CalculateEntityCount()` 触发 sync point**：enableable 过滤下会等待所有写 job。使用 `IgnoreFilter` 变体或异步变体。
2. **ChangeFilter 是 chunk 级不是 entity 级**：不能用于精确单 entity 变更检测。整个 chunk 中只要有一个 entity 变更，所有 entity 都被处理。
3. **`EntityIndexInQuery` 有准备成本**：调度时依赖 `CalculateBaseEntityIndexArray[Async]`；仅在需要 packed index 时使用。
4. **直接数据引用与缓存 handle 不同**：结构变化后重新获取 `DynamicBuffer`/chunk array/ref 等直接引用；缓存 TypeHandle/Lookup 在下一次使用前 `.Update(ref state)`。
5. **`ScheduleParallel` 并行度由 chunk 数量决定**：chunk 太少时并行度不足。需要评估 entity 分布对 chunk packing 的影响。
6. **`ref` 参数标记 chunk 为已变更**：即使未实际修改数据，`ref` 也触发 chunk 写标记。`in` 只读参数不触发。
7. **异步 query 返回 `NativeList` 不是 `NativeArray`**：因为最终匹配量在 job 运行时才知道。需要考虑 `NativeList` 的内存管理。
8. **显式 query 校验有边界**：Entities 1.4.6 会检查显式 query 是否包含 `Execute` 所需组件和权限，但不覆盖所有 filter/enableable 语义组合；重构后仍需交叉验证。
9. **Enableable component 在 `SimulationSystemGroup` 中的 `LocalToWorld` 可能过期**：不是本领域的直接陷阱，但与 query 条件组合时可能误判 entity 位置。
10. **`GetSingletonRW` 不等待 job 完成**：当 IJobEntity 还在写入被访问的 singleton 时，使用 `GetSingletonRW` 读取可能导致竞态。
11. **Buffer 访问权限必须按 API 分清**：`IJobEntity.Execute` 中用 `in DynamicBuffer<T>` 表达只读、`ref` 表达读写；`SystemAPI.Query<DynamicBuffer<T>>` 暴露读写访问，不能把两种 API 的权限语义混为一谈。
12. **ECB 复用导致命令交错**：多个 job 复用同一个 ECB 且使用相同 sortKey → 命令交错，确定性回放被破坏。

---

## 官方证据

| 官方文档 | 关键结论 | 关联规则 |
|----------|----------|----------|
| `iterating-data-ijobentity.md` | IJobEntity 自动生成 IJobChunk；支持 WithAll/Any/None/ChangeFilter | QRY-01, QRY-02, CASE-02 |
| `iterating-data-ijobchunk-implement.md` | IJobChunk 用于 chunk 级操作和非标准遍历；chunk.Has() 判断 optional | QRY-03, CASE-03 |
| `systems-systemapi-query.md` | SystemAPI.Query 是主线程 foreach；source generator 自动创建 query | PRF-05, CASE-01 |
| `performance-sync-points.md` | `Run` 和 idiomatic foreach 导致主线程同步阻塞；sync point 成因详解 | PRF-05, PRF-09 |
| `systems-optimizing.md` | 每个 system 有 TypeHandle 刷新、Lookup 创建、Dependency 链三种固定开销 | JOB-02 (跨文档) |
| `components-enableable-use.md` | Random-access 方法额外开销；enableable 查询成本；IgnoreFilter 不等待 enabled 写 job | PRF-06, PRF-09 |
| `iterating-data-ijobchunk-implement.md`、`common-errors.md` | ComponentLookup 随机访问竞态条件；NativeDisableParallelForRestriction 仅关闭检查 | PRF-19 |
| `concepts-safety.md` | `ExclusiveEntityTransaction` 主要服务 secondary/streaming World，不是通用 worker-thread `EntityManager` 替代 | PRF-21 |
| `common-errors.md` | IJobEntity 参数不匹配问题 | PRF-20 |
| `common-errors.md` | 嵌套 job 的 safety handle 不正确；IJobEntity 参数不匹配问题 | PRF-23 |
| `systems-version-numbers.md` | ChangeFilter chunk 级触发；手动调用 Update 破坏版本号 | (核心概念) |
| `aspects-intro.md` | Aspects 已废弃，将移除 | PRF-17 |
| `iterating-data-ijobchunk-implement.md` | `CalculateBaseEntityIndexArrayAsync` per-entity 索引；type handle 每帧更新 | JOB-03, PRF-08 |
| `systems-entity-command-buffers.md` | ECB 最佳实践；独立 ECB per job | (常见陷阱) |
| `systems-data-granularity.md` | 读写数据分离避免响应式系统误触发 | JOB-04 |
