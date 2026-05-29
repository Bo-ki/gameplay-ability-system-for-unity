# 02 查询遍历与 Job

## 职责

本主题维护 EntityQuery、IJobEntity、IJobChunk、SystemAPI.Query、ComponentLookup、query filter 和 job 调度规则，用于约束 Runtime Core 热路径的遍历和计算方式。

## 核心概念详解

### 三种遍历方式对比

| 方式 | 执行线程 | Burst | 适用规模 | Sync Point | EX-GAS 推荐场景 |
|---|---|---|---|---|---|
| `SystemAPI.Query` (idiomatic foreach) | 主线程 | 可（受 `ISystem` / Burst 上下文约束） | 小规模（<100 实体） | **是** | Debugger 快照、Editor 工具、proof 验证 |
| `IJobEntity` | Worker 线程 | 是 | 中大规模（100-10K） | 否 | 简单 per-entity 变换、stateless 计算 |
| `IJobChunk` | Worker 线程 | 是 | 大规模（>10K） | 否 | chunk skip/mask/optional、批量统计、复杂循环 |

**核心规则：Runtime Core hot path 默认优先 IJobEntity 或 IJobChunk。`SystemAPI.Query` 触发的 main thread sync 在高频场景下不可接受。**

### SystemAPI.Query（主线程遍历）

```csharp
// 底层机制：source generator 创建并缓存 EntityQuery
// foreach 前 source generator 会完成必要依赖；相关 job 未完成时主线程等待
// 适用于 proof、debug、Editor，不适用于 hot path
foreach (var (health, translation) in SystemAPI.Query<RefRO<Health>, RefRW<Translation>>())
{
    // 主线程执行；可在合适 ISystem/Burst 上下文编译，但不是 worker-thread 并行 job
    translation.ValueRW.Value += health.ValueRO.Value;
}
```

**关键事实：**
- Source generator 为每个 `SystemAPI.Query` 调用自动创建并缓存 `EntityQuery`
- `foreach` 前会自动完成必要 read/write 依赖；若相关 job 未完成，会表现为主线程等待
- `Run()` 方法同理：同步执行，block 主线程
- 主线程遍历导致所有 worker 线程闲置等待
- `upgrade-guide.md` 明确 `SystemAPI.Query` 在不需要 job 的遍历中仍可 Burst 编译；EX-GAS 拒绝它进入 hot path 的理由不是“绝对不能 Burst”，而是主线程串行遍历、自动完成依赖、没有 worker 并行度

### IJobEntity（per-entity 并行 job）

IJobEntity 是 per-entity 遍历的首选。只需定义 `Execute` 方法和参数，source generator 生成 IJobChunk 实现。

```csharp
[BurstCompile]
public partial struct AttributeDeltaApplyJob : IJobEntity
{
    [ReadOnly] public NativeHashMap<int, float> DeltaMap;

    // Execute 的参数即 query 条件
    public void Execute(ref BAttribute attribute, in CAttributeDeltaConsumer consumer)
    {
        if (DeltaMap.TryGetValue(consumer.AttributeCode, out var delta))
        {
            attribute.CurrentValue += delta;
        }
    }
}

// 调度
var job = new AttributeDeltaApplyJob { DeltaMap = deltaMap };
state.Dependency = job.ScheduleParallel(state.Dependency);
```

**Execute 参数支持的访问方式：**

| 参数类型 | 读写方式 | 说明 |
|---|---|---|
| `IComponentData` | `ref` = 读写, `in` = 只读 | 构成 query 条件 |
| `ICleanupComponentData` | `ref` / `in` | cleanup 组件 |
| `ISharedComponent` | `in` 只读 | 托管类型时**不能 Burst** |
| `DynamicBuffer<T>` | `ref` / `in` | Buffer 访问 |
| `Entity` | 值拷贝 | 当前 entity ID |
| `IAspect` | `ref` / `in` | **已废弃（deprecated since 1.4.6），禁止新代码使用。** 官方推荐直接使用 component access 和 query 方法。已有 Aspect 需在重构窗口替换为直接 component 访问 |
| `int` + `[ChunkIndexInQuery]` | 值拷贝 | chunk 索引 |
| `int` + `[EntityIndexInChunk]` | 值拷贝 | chunk 内 entity 索引 |
| `int` + `[EntityIndexInQuery]` | 值拷贝 | **性能差，内部用 `CalculateBaseEntityIndexArray`** |

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

**重要限制：**
- `EntityIndexInQuery` 性能很差，避免在高频路径使用
- 带 `ISharedComponent` 时不能 Burst 也不能 ScheduleParallel
- `[WithChangeFilter]` 是 chunk 级检查，不是精确 entity 级变更检测

### IJobChunk（chunk 级批处理）

当需要 chunk 级跳过、optional component 判断、或非标准遍历顺序时使用 IJobChunk。

```csharp
[BurstCompile]
public struct EffectSpecChunkJob : IJobChunk
{
    [ReadOnly] public ComponentTypeHandle<CSpecRequest> SpecRequestHandle;
    public ComponentTypeHandle<BAttribute> AttributeHandle;
    [ReadOnly] public ComponentTypeHandle<CActiveTag> ActiveTagHandle;

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

        // useEnabledMask=true 时必须跳过 disabled entity
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
- 大多数 per-entity 遍历应使用 IJobEntity
- IJobEntity 底层就是生成 IJobChunk，自动享受未来 source gen 优化
- 需要实现 `IJobEntityChunkBeginEnd` 接口来做 chunk 级前后处理

---

### Job 调度、依赖管理与并行安全

#### SystemState.Dependency：Job 依赖链

所有通过 System 调度的 job 都通过 `SystemState.Dependency` 形成依赖链。这是 ECS 保证并行安全的核心机制。

```csharp
public partial struct MySystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Job1 依赖于前一个 system 的 Dependency（若有写入相同 component）
        var job1 = new Job1 { ... };
        state.Dependency = job1.ScheduleParallel(state.Dependency);

        // Job2 依赖于 Job1 完成
        var job2 = new Job2 { ... };
        state.Dependency = job2.ScheduleParallel(state.Dependency);

        // Job3 使用不同的 query，不依赖 Job1/Job2
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
2. 每个 system 的 `OnUpdate` 被调用前，ECS 注入对前驱 system 写入 component 的等待
3. 手动组合依赖用 `JobHandle.CombineDependencies`
4. **依赖链的长度和复杂度直接影响调度性能**

**NativeContainer 依赖补充（PackageCache `scheduling-jobs-dependencies.md` 原文校准）：**

`SystemState.Dependency` / `SystemBase.Dependency` 只根据 ECS component 的读写访问建立系统级依赖，不会追踪通过 `NativeArray`、`NativeList`、`NativeStream` 等 NativeContainer 传递的数据。如果 Job A 写 `NativeList<GECommandSeedRecord>`，Job B 读同一个 list，B 的输入依赖必须显式包含 A 的 `JobHandle`；多路生产者合并时必须用 `JobHandle.CombineDependencies`。EX-GAS 的 Fan-In / Target / Modifier record 管线因此必须由 owner system 明确保存 producer handle、merge handle、consumer handle，并在返回 `OnUpdate` 前写回 `state.Dependency`。

#### 并行安全规则

| 问题 | 说明 | 后果 |
|---|---|---|
| 两个 job 写同一 component type | ECS 安全系统自动建立依赖，串行化 | 失去并行性 |
| 未传递正确依赖 | job 可能读到脏数据或写冲突 | 不确定行为 |
| 在主线程直接操作 ECS 数据 | 触发 sync point，等待所有相关 job | 主线程阻塞 |
| Job 内访问托管对象 | Burst 编译失败 | 性能退化至解释执行 |
| 忘记 CombineDependencies | 后续 job 可能先于前驱完成 | 数据竞争 |

#### Job 调度开销分析

每个 system 有固定的性能开销（来自官方文档 `systems-optimizing.html`）：

1. **EntityTypeHandle 刷新**：每个 system 在 `OnUpdate` 前自动获取自己的 `EntityTypeHandle` 副本。结构变化使这些 handle 失效，因此每次都要刷新。开销随活跃 system 数量线性增长。
2. **ComponentLookup/BufferLookup 重复创建**：不同 system 可能创建相同的 lookup。每个 lookup 需要每帧更新（以应对结构变化）。
3. **Dependency 计算链**：更多 system → 更多 job → 更复杂的 `JobHandle` 依赖链。

**EX-GAS 性能推论（`SYS-03`）：每增加一个 system 不是零成本的。不必要的 system 拆分增加三种开销：TypeHandle 刷新、Lookup 创建、Dependency 链计算。**

#### 调度模式对比

| 调度方式 | 并行度 | 适用场景 | 注意事项 |
|---|---|---|---|
| `ScheduleParallel` | 多 worker 线程并行 | 无 entity 间依赖的遍历 | 默认模式，首选 |
| `ScheduleSingle` | 单 worker 线程 | 需要顺序处理、全局状态 | 不阻塞主线程，但并行度低 |
| `Schedule`（按 chunk） | 每个 chunk 一个 job | chunk 间无依赖的批处理 | 注意 chunk count 与 job count 的关系 |
| `Run` | 主线程同步 | proof/debug 小规模 | 触发 sync point，不推荐 hot path |

#### IJobEntity 的并行执行模型

```
主线程调度:
  state.Dependency = job.ScheduleParallel(state.Dependency)
      ↓
Worker 线程池并行执行:
  chunk[0] 线程A ── entity 0..63
  chunk[1] 线程B ── entity 0..63
  chunk[2] 线程C ── entity 0..63
  ...
      ↓
所有 chunk 完成 → state.Dependency 标记完成
```

**关键理解：** IJobEntity 的并行以 chunk 为单位分发，不是以 entity 为单位。100 个 chunk = 最多 100 个并行任务，chunk 内 entity 数不影响并行度。

#### 避免过度碎片化的 Job

```csharp
// 反模式：为每个小任务创建一个 system + job
public partial struct UpdateHealthJob : IJobEntity { ... }
public partial struct UpdateManaJob : IJobEntity { ... }
public partial struct UpdateStaminaJob : IJobEntity { ... }

// 正确：应评估合并（如果它们用同一个 query）
public partial struct UpdateAllAttributesJob : IJobEntity
{
    void Execute(ref Health h, ref Mana m, ref Stamina s) { ... }
}
```

### EntityQuery 与 Query Filter

```csharp
// 构建 query
var query = new EntityQueryBuilder(Allocator.Temp)
    .WithAll<BAttribute, CTagMask>()
    .WithNone<CDead>()
    .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
    .Build(ref state);

// query 的使用
int count = query.CalculateEntityCount();  // 遵守 enableable 过滤；可能触发 sync point
var entities = query.ToEntityArray(Allocator.TempJob);  // 同步操作，有 sync point
```

**EntityQueryOptions 关键选项：**
- `IgnoreComponentEnabledState`：忽略 enableable 状态，所有 component 视为 enabled。**不需要 sync point**，效率更高
- 默认（无此选项）：遵守 enableable 过滤，**每次同步查询触发 sync point**

**同步 vs 异步 Query 操作：**

| 方法类型 | Sync Point | 返回值 | 说明 |
|---|---|---|---|
| 同步（`ToEntityArray`） | 是 | `NativeArray` | 等待所有相关 job 完成 |
| 异步（`ToEntityArrayAsync`） | 否 | `NativeList` | 调度 job 完成操作，长度未知直到 job 运行 |

**ChangeFilter：**
- 在上一帧 System 写入 component 后才触发匹配
- 是 chunk 级检查（整个 chunk 有任意 entity 变更即匹配）
- 不能用于精确的 per-entity event 语义

**`chunk.DidChange`（IJobChunk 内精细变更检测）：**

`chunk.DidChange(ref TypeHandle, LastSystemVersion)` 是 ChangeFilter 的更精细替代方案——在 IJobChunk.Execute 内部逐 chunk 判断每个 component type 是否变更，而非在 query 级别一刀切过滤。

```csharp
[BurstCompile]
struct UpdateOnChangeJob : IJobChunk
{
    [ReadOnly] public ComponentTypeHandle<InputA> InputATypeHandle;
    [ReadOnly] public ComponentTypeHandle<InputB> InputBTypeHandle;
    public ComponentTypeHandle<Output> OutputTypeHandle;
    public uint LastSystemVersion;

    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
                        bool useEnabledMask, in v128 chunkEnabledMask)
    {
        bool inputAChanged = chunk.DidChange(ref InputATypeHandle, LastSystemVersion);
        bool inputBChanged = chunk.DidChange(ref InputBTypeHandle, LastSystemVersion);

        // 两个输入都没变 → 整个 chunk 跳过
        if (!(inputAChanged || inputBChanged))
            return;

        var inputAs = chunk.GetNativeArray(ref InputATypeHandle);
        var inputBs = chunk.GetNativeArray(ref InputBTypeHandle);
        var outputs = chunk.GetNativeArray(ref OutputTypeHandle);

        if (!useEnabledMask)
        {
            for (var i = 0; i < chunk.Count; i++)
                outputs[i] = new Output { Value = inputAs[i].Value + inputBs[i].Value };
            return;
        }

        var enumerator = new ChunkEntityEnumerator(true, chunkEnabledMask, chunk.Count);
        while (enumerator.NextEntityIndex(out var i))
        {
            outputs[i] = new Output { Value = inputAs[i].Value + inputBs[i].Value };
        }
    }
}

// System 端传入
job.LastSystemVersion = this.LastSystemVersion;
```

**关键区别：**
| 机制 | 作用层级 | 判断依据 | 适用场景 |
|---|---|---|---|
| `[WithChangeFilter]` | Query 级 | chunk 中**任意** entity 的**任意**指定 component 变更 → 整个 chunk 通过 | 粗粒度：只想处理"有变化"的 chunk |
| `chunk.DidChange` | Chunk 内逐 component | **每种** component type 独立判断 | 精细：只重新计算变更的输入源（如 A 变了但 B 没变 → 跳过 B 相关计算） |
| `chunk.DidOrderChange` | Chunk 内 | entity 在 chunk 中的顺序是否变化 | entity 重排检测 |

**EX-GAS 适用场景（CASE-18）：**
- 属性变更检测：只重新评估属性发生变化的 unit 的 effect
- 多输入源部分变更：EffectCommand 变但 TagMask 不变 → 跳过 Tag 重评估
- chunk 级早期退出：整个 chunk 的 entity 都没有变更任何输入 → 整 chunk 跳过

### ComponentLookup / BufferLookup

随机访问 component 数据的工具，但每次 lookup 只有 entity 级别精度，无法批量优化。

```csharp
// 获取 lookup
var healthLookup = state.GetComponentLookup<BAttribute>(isReadOnly: true);

// 在 job 中使用
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

---

### IJobEntity Execute 参数完整语义

#### `ref` vs `in` 的深度含义

```csharp
void Execute(ref BAttribute attr, in CTagMask mask, Entity entity)
```

| 修饰符 | 读写 | Query 条件 | Job 依赖 | 说明 |
|---|---|---|---|---|
| `ref` | 读写 | 必需存在（WithAll） | 自动建立写依赖 | 修改会影响其他 system |
| `in` | 只读 | 必需存在（WithAll） | 只读依赖 | ECS 安全系统保证不被写 |
| 值类型（无修饰符） | 值拷贝 | 不构成 query 条件 | 无依赖 | Entity/chunkIndex 等元数据 |
| `EnabledRefRW<T>` | 读写 enable 状态 | 必需存在 | 写依赖 | 只操作 enable 位，不操作值 |
| `EnabledRefRO<T>` | 只读 enable 状态 | 必需存在 | 只读依赖 | 查询 enable 位 |

#### Optional Component 的 Chunk 级处理

```csharp
[BurstCompile]
public struct OptionalComponentJob : IJobChunk
{
    public void Execute(in ArchetypeChunk chunk, ...)
    {
        // 检查 chunk 是否有该 component
        bool hasOptional = chunk.Has(ref OptionalTypeHandle);

        if (hasOptional)
        {
            var data = chunk.GetNativeArray(ref OptionalTypeHandle);
            // 使用 data
        }
        else
        {
            // chunk 中所有 entity 都没有该 component
            // 无需逐 entity 检查
        }
    }
}
```

**这是 QRY-03 的底层机制：chunk 级判断 optional component 的存在性，避免为每种组合创建 system。**

---

## 官方证据

| 证据 | 结论 |
|---|---|
| `iterating-data-ijobentity.html` | IJobEntity 自动生成 IJobChunk；支持 WithAll/Any/None/ChangeFilter 属性 |
| `iterating-data-ijobchunk.html` | IJobChunk 用于 chunk 级操作和非标准遍历 |
| `systems-systemapi-query.md` | SystemAPI.Query 是主线程 foreach；source generator 自动创建 query |
| `performance-sync-points.md` | `Run` 和 idiomatic foreach 导致主线程同步阻塞 |
| `systems-optimizing.html` | 每个 system 有 TypeHandle 刷新、Lookup 创建、Dependency 链三种固定开销 |
| `systems-update-order.html` | SystemGroup 层次排序；UpdateBefore/UpdateAfter 控制顺序 |
| `components-enableable-use.html` | enableable 查询成本：同步查询等待写 job；IgnoreFilter 无此成本 |

## 使用模式与反模式

**正确模式：**
- Hot path：IJobEntity（普通遍历）/ IJobChunk（chunk 级操作）
- 不同 query 通过显式 `EntityQuery` 参数传给 job，复用 job struct
- 同一 chunk 内 entity 按线性顺序处理，利用 cache locality
- optional component 用 chunk 内判断，不为每种组合创建 system
- `state.Dependency` 正确传递，使用 `CombineDependencies` 合并多个 job 依赖

**反模式：**
- 百万实体 hot path 用 `SystemAPI.Query` 主线程 foreach
- 为每个 component 组合创建一个 system/query（query 爆炸）
- 高频 random lookup 替代 owner-local buffer
- 用 `EntityIndexInQuery` 做 parallel array 索引（性能极差）
- 不传递或错误传递 JobHandle 依赖
- 过多小粒度 system（每个 system 有固定开销，SYS-03）

## EX-GAS 项目解读

### 对 Runtime Core 热路径的启示

**EffectCommand 批处理：** 使用 IJobChunk + chunk 级效果分类。同类 GE 效果（如所有即时伤害）聚合同一 chunk，减少 query 次数。

**Attribute Delta 归并：** 优先按 target ASC 分组后顺序 apply，避免 random lookup 散落。

**ActiveEffectStore 周期 tick：** 默认遍历 owner-local `ActiveGameplayEffectBuffer` slot，并用 enum / bit flags 表达 active、inhibited、expired、period due；当大量 ASC 长期 idle 且 profiler 证明 chunk/entity skip 收益大于 enableable 过滤等待时，才引入 `PeriodDueTag` / Chunk Component 作为 query skip cache。禁止用 `ToEntityArray` 全量扫。

**Tag query / 目标扫描：** 用 `WithChangeFilter` + cached query result 减少重复扫描。

### 对当前实现的诊断

当前 AutoChess 的 `SHeadlessAutoChessDriver` 是 x50 下的 top 热点，根源在于：
- `ToEntityArray` 全量扫描 + 大 `UnitSnapshot` 构造
- 每 tick 重复 scan 而非 cached query/read model

目标态应将 Driver 改为 stable read model（chunk/jobified query 或 cursor-based 增量更新）。

### System 数量审计

**建议在 Debugger 中加入 `ActiveSystemCount` 指标**，并按以下分类统计：
- Runtime Core systems
- Debugger/Diagnostic systems
- Presentation systems
- Demo/Runner systems

当 Runtime Core system 数量超过设定阈值（建议初始：每 phase 不超过 5 个），应触发合并评估。

## 常见陷阱

1. **`query.CalculateEntityCount()` 触发 sync point**：enableable 过滤下会等待所有写 job
2. **ChangeFilter 是 chunk 级不是 entity 级**：不能用于精确单 entity 变更检测
3. **`EntityIndexInQuery` 性能差**：内部依赖 `CalculateBaseEntityIndexArray`，避免在高频路径使用
4. **结构变化后 lookup/handle 失效**：任何 `CreateEntity`/`AddComponent` 后必须重取
5. **`WithChangeFilter` + `ScheduleParallel`**：chunk 中只要有一个 entity 变更，整个 chunk 都被处理
6. **每增加一个 System 有三种固定开销**：TypeHandle 刷新 + Lookup 创建 + Dependency 链计算
7. **`ScheduleParallel` 并行度由 chunk 数量决定**：chunk 太少时并行度不足
8. **异步 query 返回 NativeList 不是 NativeArray**：因为最终匹配量在 job 运行时才知道

## 验收指标

1. Runtime Core 任务交还包含 query contract（`All/Any/None/ChangeFilter/IgnoreEnabledState`）
2. 性能报告包含 filtered / unfiltered query count、lookup count、job count、active system count
3. 百万实体目标下，主链 hot path 不依赖逐实体 managed callback 或主线程 foreach
4. AutoChess Driver 不再每 tick `ToEntityArray` 全量扫描
5. 每个 System 的 job 依赖链在 Debugger 中可审查
