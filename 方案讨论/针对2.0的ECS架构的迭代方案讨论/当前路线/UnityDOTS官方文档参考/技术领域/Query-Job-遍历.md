# Query-Job-遍历

## 职责

本领域覆盖 EntityQuery 构建与选项、三种遍历方式（SystemAPI.Query / IJobEntity / IJobChunk）、job 调度与依赖管理、ComponentLookup 随机访问、query filter 语义、以及并行安全规则。不覆盖 SystemGroup 层次结构（见 System-World-SystemGroup.md）、不覆盖 ECB 使用细节（见 02-查询遍历与Job.md CASE-05）、不覆盖 enableable component 生命周期。

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

## 编写规范

### QRY-01: EntityQuery 必须显式声明 All/Any/None/Options

- **声明：** 使用 `EntityQueryBuilder` 显式构造 EntityQuery 时，必须完整声明 `WithAll`、`WithAny`、`WithNone`、`WithOptions`。禁止依赖隐式 query 条件或不完整声明。
- **来源：** `iterating-data-entityquery.html` —— EntityQuery 是遍历的筛选契约，隐性条件导致意外的 entity 匹配。
- **为什么：** 隐式 query 条件意味着后续维护者无法仅从代码看出该 system 处理哪些 entity。缺少 `WithNone` 可能导致已销毁/不应处理的 entity 进入 job。`IgnoreComponentEnabledState` 的省略与否决定是否触发 sync point。
- **EX-GAS 诊断：** 每个 `IJobEntity`/`IJobChunk` 旁注释列出对应的 EntityQuery 条件。Code review 时交叉验证。
- **检查方法：** Grep 搜索 `IJobEntity` 和 `IJobChunk` 在 Runtime Core 目录中的使用；检查 query 属性（`[WithAll]`/`[WithNone]`/`[WithAny]`/`[WithOptions]`）是否完整。

### QRY-02: 优先使用异步 Query 方法避免 Sync Point

- **声明：** 当需要在主线程获取 entity 数组或 component 数据时，优先使用 `ToEntityArrayAsync`、`ToComponentDataArrayAsync` 等异步变体。同步方法（`CalculateEntityCount`、`ToEntityArray`、`GetSingleton`）在 enableable 过滤且写 job 未完成时触发 sync point。
- **来源：** `performance-sync-points.md`、`components-enableable-use.html` —— 同步 query 操作在有 enableable component 且写 job 未完成时等待所有相关 job 完成。
- **为什么：** sync point 导致主线程阻塞等待所有 worker 线程完成，丧失并行度。异步变体在 job 中执行查询，不阻塞主线程。
- **EX-GAS 诊断：** 当前 AutoChess `SHeadlessAutoChessDriver` 使用 `ToEntityArray` 全量扫描是 x50 下的 top 热点。目标态应改为异步 query 或 read model。
- **检查方法：** 搜索 `CalculateEntityCount()`、`ToEntityArray(`、`GetSingleton<T>(`（非 `GetSingletonRW`）；判断是否在 enableable 过滤生效时调用。

### QRY-03: Optional Component 使用 IJobChunk chunk.Has() 判断

- **声明：** 当一组 entity 中部分含有可选 component 时，使用 `IJobChunk` + `chunk.Has(ref TypeHandle)` 在同一 system 内判断。禁止为每种 component 组合创建一个独立 system/query。
- **来源：** `iterating-data-ijobchunk.html` —— "Check if a chunk has an optional component... This avoids the need to create separate queries for each combination."
- **为什么：** 为每种组合创建 system 导致 system 数量爆炸（SYS-03）。Chunk 级判断是 O(1) 操作，entity 级判断才需要逐 entity 检查。如果某个 component 出现在 50%+ chunk 中，用 `WithAll` + `IEnableableComponent` 更优。
- **EX-GAS 诊断：** Effect application 路径中可能存在为不同 effect type 创建不同 system 的模式，应合并为单个 IJobChunk。
- **检查方法：** 搜索 Runtime Core 中是否存在 `[WithAll(typeof(A))]` 和 `[WithAll(typeof(A), typeof(B))]` 两个独立 system 处理同一类逻辑；评估合并可能性。

### QRY-04: ChangeFilter 是 chunk 级机制，非 entity 级变更检测

- **声明：** `[WithChangeFilter]` 和 `chunk.DidChange` 都是 chunk 级判断——只要 chunk 中任意 entity 的指定 component 变更，整个 chunk 被视为"已变更"。不能用于精确的单 entity 变更事件语义。
- **来源：** `systems-version-numbers.md`、`systems-entityquery-filters.md` —— Change filter chunk 级触发于 write access 声明（非实际数据变更）。
- **为什么：** 将 chunk 级机制当 entity 级事件使用会导致：1) 假阳性——未变更的 entity 被处理；2) `DidChange` 返回 true 表示 chunk 中至少一个 entity 变更，不承诺哪个 entity。精确 event 语义需要独立的 event buffer 或 change tracking per entity。
- **EX-GAS 诊断：** GAS Effect 重评估可能依赖变更检测触发。`chunk.DidChange`（CASE-18）在 IJobChunk 内部按 component type 逐一检查，比 query 级 filter 更灵活，但仍不是 per-entity 级。
- **检查方法：** 审计 `[WithChangeFilter]` 的使用场景；判断业务逻辑是否依赖"仅变更的 entity 被处理"假设。若是 → 需要改为 event buffer 或 per-entity change tracking。

### JOB-01: Hot Path 必须使用 IJobEntity 或 IJobChunk + Burst

- **声明：** Hot path（每帧执行、entity 数 > 100）禁止使用 `SystemAPI.Query` foreach 或 `Run()`。必须使用 `IJobEntity` 或 `IJobChunk` 并标记 `[BurstCompile]`。
- **来源：** `performance-sync-points.md` —— "Sync points can also happen when you use Run to run a job, or when you use idiomatic foreach"；`iterating-data-ijobentity.html`、`iterating-data-ijobchunk.html`。
- **为什么：** 主线程 foreach 先触发 sync point（等待所有相关 job）→ 再串行遍历。同时间内所有 worker 线程空闲。失去 Burst 编译（托管代码性能 10-100x 慢）。
- **EX-GAS 诊断：** 搜索 `SystemAPI.Query` 在 Runtime Core system 目录中的出现。标记为 proof-only 或要求迁移到 IJobEntity。
- **检查方法：** 每小时级别的 Grep 扫描；Code review 确认新增遍历路径的遍历方式和规模。

### JOB-02: state.Dependency 必须正确传递，使用 CombineDependencies 合并多 job 依赖

- **声明：** 所有通过 system 调度的 job 必须通过 `state.Dependency` 形成依赖链。当在一个 system 中调度多个独立 job 时，必须使用 `JobHandle.CombineDependencies` 合并所有依赖。
- **来源：** `systems-looking-up-data.md` —— Job 依赖管理是 ECS 并行安全的核心机制。
- **为什么：** 未正确传递依赖的 job 可能在依赖的写入 job 完成前读取脏数据。依赖链断裂导致不确定行为——在测试中偶尔正确，在规模化下频繁错误。
- **EX-GAS 诊断：** 每个 ISystem 的 `OnUpdate` 应审计 job 调度和依赖传递。`state.Dependency` 赋值缺失是典型缺陷。
- **检查方法：** 审查每个 `OnUpdate` 中所有 `.Schedule` 调用的返回值是否被正确组合到 `state.Dependency`。

### JOB-03: 每个并行 Job 使用独立 ECB，禁止复用

- **声明：** 多个并行 job（IJobEntity/IJobChunk）各自使用独立的 `EntityCommandBuffer` 实例。禁止多个 job 复用同一 ECB。
- **来源：** `systems-entity-command-buffers.md` —— "It's best practice to use a separate ECB for each distinct job."
- **为什么：** 复用 ECB 时，多个 job 使用相同 sortKey 域（如均为 `[ChunkIndexInQuery]`）会使命令交错排列而非依序排列。静默破坏确定性回放。
- **EX-GAS 诊断：** StructuralPlayback phase 的 ECB 使用需审计。每个 job 独立 ECB，各自在 playback 时独立排序。
- **检查方法：** 审计 `ISystem.OnUpdate` 中 `CreateCommandBuffer` 调用次数与并行 job 数量的对应关系。

### JOB-04: IJobEntity Execute 参数与 EntityQuery 必须手动同步

- **声明：** `IJobEntity` 不验证 `Execute` 方法的参数是否与关联的 `EntityQuery` 匹配。参数不匹配不会产生编译错误或运行时警告。每次修改 query 或 Execute 签名后必须交叉验证。
- **来源：** `concepts-safety.md` —— "IJobEntity does not verify that the Execute method's parameters match the EntityQuery. You must manually keep them in sync."
- **为什么：** Execute 参数比 query 多 → job 不调度任何 entity（query 不含该 component 的 entity 被静默排除）。Execute 参数比 query 少 → 遗漏 component 写入，数据不一致。重构 query 忘记同步 Execute 参数 → 静默错误，零警告。
- **EX-GAS 诊断：** Runtime Core 中所有 IJobEntity 使用（参数复杂，重构风险高）。每个 IJobEntity 旁应注释列出对应的 EntityQuery 条件。
- **检查方法：** Code review 时交叉验证 query 属性和 Execute 参数。新增/删除 component 参数时同步检查 query 声明。

### PRF-05: Hot Path 禁止主线程遍历 (P0)

- **声明：** 每帧执行且 entity 数 > 100 的遍历路径，禁止使用主线程 `SystemAPI.Query` 或 `.Run()`。必须使用 `IJobEntity` 或 `IJobChunk` + Burst。（同 JOB-01，此处为 P0 级强约束。）
- **来源：** `performance-sync-points.md`；严重度 P0（致命）。
- **为什么：** 主线程遍历触发 sync point → 等待所有 worker 线程 → 丧失并行度 → 帧时间随着 entity 数量线性增长而非 sub-linear。在 x50 规模下，一次 `ToEntityArray` 全量扫描的 sync 成本可达毫秒级。
- **EX-GAS 诊断：** 当前 `SHeadlessAutoChessDriver` 的 `ToEntityArray` 全量扫描 + 大 `UnitSnapshot` 构造是 x50 下的 top 热点。目标态改为 stable read model。
- **检查方法：** 搜索 Runtime Core 目录中的 `SystemAPI.Query` 和 `.Run(`；确认每个出现处的 entity 规模和执行频率。

### PRF-06: 禁止高频 Random Access Lookup (P0)

- **声明：** 高频路径（每帧执行、遍历 entity 数 > 100）中使用 `ComponentLookup.TryGetComponent` 或 `BufferLookup.TryGetBuffer` 做跨 entity 随机访问，其成本在规模下显著，必须重构为 owner-local data 或顺序遍历。
- **来源：** `components-enableable-use.html` —— "Random-access methods have some additional overhead because they need to look up the target entity's data. When performance is a priority, use the iteration-based methods where possible."；严重度 P0（致命）。
- **为什么：** 每次 lookup 是哈希查找 + 内存随机访问。在 IJobEntity 的紧密循环中，一个 random lookup 的 cache miss 成本 ≈ 10-20 个顺序 entity 处理成本。百万 entity 遍历中 10% 做 random lookup ≈ 10 万次 cache miss。
- **EX-GAS 诊断：** Effect application 中 `ComponentLookup<BAttribute>` 跨 entity 读取 source ASC 属性、ActiveEffect MMC 计算中 lookup source/target 属性。应优先使用 owner-local buffer。
- **检查方法：** Grep `TryGetComponent` 和 `TryGetBuffer` 在 IJobEntity/IJobChunk 中的使用；评估 lookup 频率和 entity 规模。

### PRF-08: 禁止 EntityIndexInQuery 在 Hot Path 使用 (P1)

- **声明：** `[EntityIndexInQuery]` 的内部实现调用 `CalculateBaseEntityIndexArray`，不是简单索引访问。禁止在高频遍历中使用。
- **来源：** `iterating-data-ijobchunk-implement.md` —— `CalculateBaseEntityIndexArrayAsync` per-entity 索引有额外数组构建步骤；严重度 P1（严重）。
- **为什么：** 需要额外的内部数组构建步骤。不是 O(1) 简单索引。在 IJobEntity 的每次 `Execute` 中调用会显著增加开销。
- **EX-GAS 诊断：** 搜索 `EntityIndexInQuery` 在 Runtime Core 中的使用。AutoChess 遍历代码中可能隐含此属性。
- **检查方法：** Grep `EntityIndexInQuery`、`EntityIndexInChunk`、`ChunkIndexInQuery`；确认使用场景和频率。

### PRF-09: 注意 Query 操作的 Sync 触发 (P1)

- **声明：** 以下操作在有 enableable component 且写 job 未完成时触发 sync point：`CalculateEntityCount()`、`ToEntityArray()`、`ToComponentDataArray()`、`GetSingleton<T>()`。使用 `IgnoreFilter` 或 `Async` 变体可规避。
- **来源：** `performance-sync-points.md`、`components-enableable-use.html`、`systems-systemapi.md`（`SystemAPI.GetSingleton` 不 sync vs `EntityManager.GetComponentData` 触发 sync）；严重度 P1（严重）。
- **为什么：** 无意识的 sync point 使主线程阻塞等待 worker 线程，丧失并行度。在 16.67ms 帧预算中，5 个意外 sync point 各 0.5ms = 15% 帧时间浪费。
- **EX-GAS 诊断：** `SHeadlessAutoChessDriver` 使用 `GetComponentData` 而非 `GetSingleton` 的路径可能引入额外 sync。EventBus 的 query 操作也可能触发 sync。
- **检查方法：** 搜索 `CalculateEntityCount`、`ToEntityArray(`、`ToComponentDataArray(` 在 OnUpdate/Body 中的使用；判断 enableable 过滤是否生效。

### PRF-11: ComponentLookup/BufferLookup 随机访问与遍历数据重叠导致竞态 (P1)

- **声明：** 在 IJobEntity/IJobChunk 中使用 `ComponentLookup` 或 `BufferLookup` 随机访问其他 entity 的 component 时，若被访问数据与 job 直接遍历的 component type 重叠，会导致 race condition。必须确保随机访问的 entity 集合与直接遍历的 entity 集合无交集，否则标记 `[NativeDisableParallelForRestriction]` 并承担正确性责任。
- **来源：** `systems-looking-up-data.md` —— "If the data you look up overlaps the data you want to read and write to in the job, then random access might lead to race conditions."；`common-errors.md`；严重度 P1（严重）。
- **为什么：** 两个 worker 线程可能同时通过 lookup 和直接遍历访问同一 entity 的同一 component。ECS 安全系统能检测并行写入冲突，但不能检测"lookup 读 + 遍历写"的微妙竞态。
- **EX-GAS 诊断：** Effect application 中 `ComponentLookup<BAttribute>` 跨 entity 读取 source ASC 属性、ActiveEffect MMC 计算中 lookup source/target 属性。若 lookup entity 与遍历 entity 同组 → 竞态风险。
- **检查方法：** Grep `ComponentLookup`/`BufferLookup` 在 IJobEntity/IJobChunk 中的使用；检查 lookup 的 entity 来源是否与 job 的 query 条件可能重叠。

### PRF-12: IJobEntity Execute 参数与 EntityQuery 不匹配的未保护安全风险 (P1)

- **声明：** `IJobEntity` 不验证 `Execute` 方法的参数是否与关联的 `EntityQuery` 匹配。参数不匹配是静默错误——无编译错误、无运行时警告。每次修改 query 或 Execute 签名后必须交叉验证。
- **来源：** `concepts-safety.md`；`common-errors.md`；严重度 P1（严重）。
- **为什么：** Execute 参数比 query 多 → job 不调度任何 entity（静默跳过）。Execute 参数比 query 少 → 遗漏 component 的写入，数据不一致。重构 query 忘记同步 Execute 参数 → 几个月后才被发现。
- **EX-GAS 诊断：** Runtime Core 中所有 IJobEntity 使用。每个 Execute 参数列表应与 query 属性一一对应。
- **检查方法：** Code review 时交叉验证 query 属性（`[WithAll]`/`[WithNone]`）和 Execute 参数。每个 IJobEntity 旁注释列出对应的 EntityQuery 条件。

### PRF-13: 禁止从 Job 内部启动新 Job (P1)

- **声明：** 禁止在任意 job 的执行上下文（包括 `IJobEntity.Execute`、`IJobChunk.Execute`、`IJobParallelFor.Execute`、`Entities.ForEach` lambda）中启动新的 job（`.Schedule()` 或 `.Run()`）。
- **来源：** `common-errors.md` —— "Launching jobs from jobs is not currently supported, and the resulting safety handles won't be set up correctly."；严重度 P1（严重）。
- **为什么：** 嵌套 job 的 safety handle 未正确建立 → 竞态条件无法被检测。编译器不报错，运行时可能偶然正确 → 隐蔽竞态 bug。
- **EX-GAS 诊断：** 搜索 `.Schedule(` 或 `.Run(` 出现在 `void Execute(` 或 lambda `() =>` 内部的模式。
- **检查方法：** Grep 搜索嵌套 `.Schedule()`/`.Run()` 模式。

### PRF-17: 禁止使用 IAspect (P2)

- **声明：** `IAspect` 在 Entities 1.4.6 中被官方标记为 deprecated，将在未来版本中移除。禁止在任何新代码中使用 `IAspect` 或 `readonly partial struct ... : IAspect`。
- **来源：** `aspects-intro.md` / `aspects-concepts.md` —— "Aspects are deprecated and will be removed in a future release. Use component access and query methods directly instead."；严重度 P2（注意）。
- **为什么：** 废弃 API 将在未来版本移除，代码将无法编译。IAspect 生成的源代码依赖特定代码生成器，跨版本兼容性不可靠。直接 component 访问是官方推荐的替代方案。
- **EX-GAS 诊断：** Grep 搜索 `IAspect`、`: IAspect`、`partial struct.*Aspect`。新增代码 PR 必须零 `IAspect` 出现。已有 Aspect 用法在 CASE-13 中登记为待迁移。
- **检查方法：** 全局搜索 `IAspect` 关键字；Code review 拦截任何新 Aspect 代码。

### PRF-19: ComponentLookup/BufferLookup 随机访问与遍历数据重叠竞态 (P2)

- **声明：** 在非高频路径中使用 `ComponentLookup`/`BufferLookup` 随机访问时，注意被访问数据可能与 job 直接遍历的 component type 重叠。设计阶段评估 lookup entity 集合与遍历 entity 集合是否可能重叠。如重叠，确保正确性证明或改用 owner-local buffer。
- **来源：** `systems-looking-up-data.md`；严重度 P2（注意）。
- **为什么：** 当前规模下可能不触发，但 scale 时 entity 数量的增加会提高重叠概率。设计阶段考虑清晰比事后修复成本低。
- **EX-GAS 诊断：** 同 PRF-11，但重点关注非高频路径的 lookup 使用。设计评审时评估重叠风险。
- **检查方法：** Code review 时检查所有 ComponentLookup 的使用，确认目标 entity 集合的包含关系。

### PRF-20: IJobEntity Execute 参数与 EntityQuery 不匹配的未保护安全风险 (P2)

- **声明：** 设计阶段在定义 IJobEntity 时，建立 query 注释与 Execute 参数的一致性检查清单。使用 `SystemAPI.Query` + source generator 自动生成的 IJobEntity 也需要确认 query 语义的正确性。
- **来源：** `concepts-safety.md`；严重度 P2（注意）。
- **为什么：** 同 PRF-12，P2 级强调设计阶段预防。清晰的代码组织和注释可以减少重构时的遗忘风险。
- **EX-GAS 诊断：** CODE-REVIEW 清单中包含 IJobEntity query/Execute 一致性检查项。
- **检查方法：** Code review checklist 包含此检查。团队规范要求每个 IJobEntity 有 query 属性注释。

### PRF-23: 禁止从 Job 内部启动新 Job (P2)

- **声明：** 在非高频路径或工具代码中，也禁止从 job 内部启动新 job。此约束适用于所有 job 类型和所有执行频率。
- **来源：** `common-errors.md`；严重度 P2（注意）。
- **为什么：** 即使是非高频路径，嵌套 job 的 safety handle 仍然不会正确建立。只在测试中通过而发布版随机崩溃的风险不可接受。
- **EX-GAS 诊断：** 工具和 Debugger 代码中的 job 嵌套调用。
- **检查方法：** 全局搜索 `\.Schedule\(` 和 `\.Run\(` 出现在 job 上下文内的情况。

## 模式与反模式

### 正确模式

- Hot path 使用 IJobEntity（普通遍历）或 IJobChunk（chunk 级操作），全部 `[BurstCompile]`。
- EntityQuery 显式声明 `WithAll`/`WithAny`/`WithNone`/`WithOptions`，在 system 的 `OnCreate` 中构建并缓存。
- Optional component 使用 `IJobChunk` + `chunk.Has()` 在同一 system 中处理，不为每种组合创建 system。
- `state.Dependency` 正确传递，使用 `CombineDependencies` 合并多个 job 的依赖。
- 高频随机 lookup 使用 owner-local buffer 替代。跨 entity 只读访问使用 `[ReadOnly]` 标记的 lookup。
- Per-entity work 使用 IJobEntity 的 `ref`/`in` 参数精确控制读写访问，`in` 用于只读参数。
- `chunk.DidChange`（CASE-18）用于 IJobChunk 内精细变更检测——按 component 逐一判断。
- 每个并行 job 使用独立 ECB 实例（P2-09）。
- IJobEntity 旁注释列出对应的 EntityQuery 条件。

### 反模式

- **百万实体 hot path 用 `SystemAPI.Query` 主线程 foreach**：触发 sync point，丧失 Burst 编译。替代：IJobEntity + Burst。
- **高频 random lookup 替代 owner-local buffer**：每次 lookup 的 cache miss 成本 ≈ 10-20 个顺序 entity 处理。替代：提前将 target 属性写入 source 的 owner-local buffer。
- **`[EntityIndexInQuery]` 做 parallel array 索引**：内部调用 `CalculateBaseEntityIndexArray`，性能极差。替代：owner-local buffer 或 chunk-local counter。
- **不传递或错误传递 JobHandle 依赖**：后续 job 可能先于前驱完成，数据竞争。正确做法：始终通过 `state.Dependency` 链传递。
- **过多小粒度 job system**：每个 system 有固定开销（SYS-03）。正确做法：同 query 的操作合并到同一 system 的多 job 中。
- **ChangeFilter 做 entity 级事件检测**：ChangeFilter 是 chunk 级机制。替代：显式 event buffer 或 `chunk.DidChange`（仍为 chunk 级）。
- **IJobEntity 的 Execute 参数使用 `ref` 表示只读字段**：`ref` 触发 chunk 写标记，导致响应式系统误触发。替代：`in` 表示只读参数。
- **忽略 enableable 的 IJobChunk for 循环**：处理已禁用的 entity。替代：使用 `ChunkEntityEnumerator` 或 `Assert.IsFalse(useEnabledMask)`。

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
- Effect application 路径中的 `ComponentLookup<BAttribute>` 跨 entity 读取 → 注意 PRF-11/PRF-19 竞态风险。
- EventBus 中同步 query 操作 → 注意 PRF-09 sync 触发。
- 各 IJobEntity 缺少 EntityQuery 注释 → 增加 PRF-12/PRF-20 风险。

### 相关 CASE 模式

- **CASE-01 (SystemAPI.Query)**：仅用于 Debugger 快照、Editor 工具、<100 entity 的 proof。任何 >100 entity 的 hot path 拒绝使用。
- **CASE-02 (IJobEntity)**：Runtime Core hot path 主力。简单 per-entity 变换首选。
- **CASE-03 (IJobChunk)**：批量统计、enableable 过滤、非标准遍历。简单 per-entity 变换用 IJobEntity。
- **CASE-18 (chunk.DidChange)**：在 IJobChunk 内按 component type 逐一检查 chunk 是否变更，比 query 级 filter 更灵活。属性脏标记检测、effect 重评估跳过。

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
10. **`GetSingletonRW` 不等待 job 完成**（P2-13）：当 IJobEntity 还在写入被访问的 singleton 时，使用 `GetSingletonRW` 读取可能导致竞态。
11. **IJobEntity 中 `DynamicBuffer<T>` 在 `SystemAPI.Query` 内默认可读写**：只读场景需自定义实现避免不必要 sync point（PRF-30）。
12. **ECB 复用导致命令交错**：多个 job 复用同一个 ECB 且使用相同 sortKey → 命令交错，确定性回放被破坏（P2-09）。

## 官方证据

| 官方文档 | 关键结论 | 关联规则 |
|----------|----------|----------|
| `iterating-data-ijobentity.html` | IJobEntity 自动生成 IJobChunk；支持 WithAll/Any/None/ChangeFilter | JOB-01, QRY-01, CASE-02 |
| `iterating-data-ijobchunk.html` | IJobChunk 用于 chunk 级操作和非标准遍历；chunk.Has() 判断 optional | QRY-03, CASE-03 |
| `systems-systemapi-query.md` | SystemAPI.Query 是主线程 foreach；source generator 自动创建 query | PRF-05, CASE-01 |
| `performance-sync-points.md` | `Run` 和 idiomatic foreach 导致主线程同步阻塞；sync point 成因详解 | PRF-05, PRF-09 |
| `systems-optimizing.html` | 每个 system 有 TypeHandle 刷新、Lookup 创建、Dependency 链三种固定开销 | SYS-03 (跨文档) |
| `components-enableable-use.html` | Random-access 方法额外开销；enableable 查询成本；IgnoreFilter 无 sync 成本 | PRF-06, PRF-09 |
| `systems-looking-up-data.md` | ComponentLookup 随机访问竞态条件；NativeDisableParallelForRestriction | PRF-11, PRF-19 |
| `concepts-safety.md` | IJobEntity 不验证 Execute 参数与 EntityQuery 匹配；ExclusiveEntityTransaction | PRF-12, PRF-20 |
| `common-errors.md` | 嵌套 job 的 safety handle 不正确；IJobEntity 参数不匹配问题 | PRF-13, PRF-23 |
| `systems-version-numbers.md` | ChangeFilter chunk 级触发；手动调用 Update 破坏版本号 | QRY-04 |
| `aspects-intro.md` | Aspects 已废弃，将移除 | PRF-17 |
| `iterating-data-ijobchunk-implement.md` | `CalculateBaseEntityIndexArrayAsync` per-entity 索引；type handle 每帧更新 | PRF-08 |
| `systems-entity-command-buffers.md` | ECB 最佳实践；独立 ECB per job | JOB-03 |
| `systems-data-granularity.md` | 读写数据分离避免响应式系统误触发 | P2-10 |

## 验收指标

1. Runtime Core 任务交还包含 query contract（All/Any/None/ChangeFilter/IgnoreEnabledState）的显式声明。
2. 性能报告包含 filtered / unfiltered query count、lookup count、job count、active system count。
3. 百万实体目标下，主链 hot path 不依赖逐实体 managed callback 或主线程 foreach。
4. AutoChess Driver 不再每 tick `ToEntityArray` 全量扫描（改用异步 query 或 read model）。
5. 每个 IJobEntity 旁有 EntityQuery 条件注释，Code review 时交叉验证通过（PRF-12）。
6. 零 `IAspect` 出现在新增代码中；已有 Aspect 登记为待迁移（PRF-17）。
7. 每个 System 的 job 依赖链在 Debugger 中可审查（JOB-02）。
8. 无嵌套 job 调用（PRF-13/PRF-23 检查通过）。
9. 每个并行 job 使用独立 ECB（JOB-03 检查通过）。
10. 所有 Runtime Core 遍历使用 IJobEntity 或 IJobChunk + Burst（PRF-05 检查通过）。
