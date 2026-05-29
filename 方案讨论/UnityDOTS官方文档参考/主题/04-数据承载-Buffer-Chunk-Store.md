# 04 数据承载、Buffer、Chunk 与 Store

## 职责

本主题维护 DynamicBuffer、InternalBufferCapacity、chunk locality、archetype/chunk 物理布局、shared/cleanup/chunk component、singleton NativeContainer 和 store 选型。这直接决定 EX-GAS 的 Command Stream、ActiveEffectStore 和 Fact Buffer 的物理形态。

## 核心概念详解

### Archetype 与 Chunk 物理布局

#### Archetype 本质

Archetype 是同一 world 内具有相同 component type 组合的所有 entity 的唯一标识符。**Archetype 决定 entity 存储在哪些 chunk 中。**

```
世界中所有 entity 的 component type 组合相同的 → 属于同一 Archetype
不同 component 组合 = 不同 Archetype = 不同 chunk 集合
```

#### 16 KiB Chunk 的内部布局

每个 chunk 固定 16 KiB（16384 字节），内部结构：

```
[Chunk Header]
[Entity ID Array]          -- 按 index 存储 entity ID
[Component A Array]        -- 所有 entity 的 Component A 值连续存储
[Component B Array]        -- 所有 entity 的 Component B 值连续存储
[Buffer Data (inline)]     -- DynamicBuffer 内联数据
[... padding ...]
```

**关键特征：**
- 每个 chunk 最多 128 个 entity（由 chunk 大小和组件大小决定）
- 数组紧密打包：entity[0] 在各数组的 index=0，entity[1] 在 index=1，依此类推
- entity 移除时，**最后一个 entity 移动到空缺位置**（swap-remove）
- 最后一个 entity 从 chunk 移除后，chunk 被销毁
- Archetype 仅在 World 销毁时被销毁（不会在 entity 全部移除后销毁）

#### 为什么频繁移动 entity 是性能杀手

从官方文档 `concepts-archetypes.html`：

> "Moving entities frequently is resource-intensive and reduces the performance of your application."

每次 AddComponent/RemoveComponent：
1. 当前 chunk 中 entity 被移除 → 最后一个 entity swap 到空位
2. 目标 archetype 的 chunk 中插入 entity → 可能触发新 chunk 分配
3. 所有 component 数据复制到新 chunk
4. 所有 handle/lookup 失效

#### 三种 Chunk 碎片化类型

从官方文档 `performance-chunk-allocations.html`，这是 ECS 性能的关键诊断框架：

##### 类型 1：大型 Entity

| 特征 | 原因 | 后果 |
|---|---|---|
| unused entity 少 + chunk capacity 低 | entity component 过大 | 每 chunk 容纳 entity 少 → 更多 chunk → 更多 cache miss |

**解决：拆分大型 entity 为多个小 entity。** 不像 OOP 中一个对象表示一个东西，ECS 中 entity 只是 component 集合的索引。人物的 AI、物理、动画、渲染 component 可以分属不同 entity。

##### 类型 2：Shared Component 误用

| 特征 | 原因 | 后果 |
|---|---|---|
| unused entity 多 + chunk capacity 高 | 不同的 SharedComponent 值强制 entity 分到不同 chunk | 明明能塞一起的 entity 被碎片化 |

```
SharedComponent 有效性三条件（来自官方）：
1. System 需要按 subgroup 操作 → 有用
2. Subgroup 数量较少 → 有用
3. 节省的内存 > 因更多 chunk 浪费的内存 → 有用
否则 → 不要用 SharedComponent
```

**替代：Chunk Component** 提供类似的"共享同值"能力但无需 chunk 分裂。

##### 类型 3：过多 Archetype（最关键）

| 特征 | 原因 | 后果 |
|---|---|---|
| 大量 archetype + 每个 chunk 数少 | entity 有太多不同的 component 集合 | **100,000 entity 各有独特 archetype = >1.5 GB 浪费 + 每个 entity 间 cache miss** |

> 来自官方文档："100,000 entities that all have their own unique archetype → more than 1.5 GB of chunk data (16 KiB x 100,000 chunks), most of it empty."

**Archetype 过多的根因：**

| 根因 | 官方建议 |
|---|---|
| 过多 tag component | 每个 tag 翻倍 archetype 排列 → 改用 enableable component |
| 临时 Add/Remove component | 临时数据存 DynamicBuffer 而非 Add/Remove |
| 大型 entity | 按使用频率拆分 entity（AI/physics/animation/render 分开） |

**这直接验证 ISSUE-001/004：instant GE 创建临时 entity = 典型"临时 Add/Remove component"反模式。每个 GE runtime entity 创建独特 archetype，scale 时导致内存爆炸和 cache 灾难。**

### DynamicBuffer

DynamicBuffer 是 entity 上的动态数组，优先内联存储在 chunk 中（避免额外内存跳转），溢出时外部化。

```csharp
// 定义 buffer element
[InternalBufferCapacity(8)]  // 初始容量 8 个元素
public struct BActiveModifier : IBufferElementData
{
    public int AttributeCode;
    public float Magnitude;
    public int SourceEffectCode;
}

// 获取和操作 buffer
var buffer = SystemAPI.GetBuffer<BActiveModifier>(entity);
buffer.Add(new BActiveModifier { ... });
buffer.RemoveAt(0);
buffer.Length    // 当前元素数
buffer.Capacity  // 当前容量
```

**InternalBufferCapacity 计算：**
- 默认：sizeof(element) 能塞进 128 字节的最大数量
- 自定义：`[InternalBufferCapacity(N)]` 指定初始容量
- 溢出：buffer 数据移到 chunk 外独立分配，访问成本增加

**Buffer 的 Chunk 内布局：**
```
[Chunk Header][Component A][Component B][Buffer Data (inline)][...other components]
```
内联时，buffer 数据紧邻其他 component 数据在同一 chunk 中 → cache friendly。
溢出后，chunk 内只存指针 → 访问多一次间接跳转。

#### DynamicBuffer 容量管理

从官方文档 `components-buffer-introducing.html`：

```
初始状态:
  Length = 0
  Capacity = InternalBufferCapacity (默认 128 bytes 能装的数量)
  Pointer = null (表示数据在 chunk 中)

溢出后:
  Length 不变
  Capacity 扩大（新分配）
  Pointer → 新外部数组地址
  chunk 中只保留指针
```

**溢出代价：**
- 每次 buffer 访问多一次间接内存跳转
- 外部数组不在 chunk 中 → 失去 cache locality
- 大量 entity 的 buffer 同时溢出 → 内存碎片化

**EX-GAS 要求：** Debugger 必须跟踪 `bufferExternalizedRatio`（内联 vs 外部化比例），超过阈值告警。

### Chunk Component

Chunk Component 是**每个 chunk 存储一份**的 component，极低存储成本。

```csharp
public struct ChunkAllDead : IComponentData { }

// 当整个 chunk 的 entity 都死亡时标记 chunk
// 后续 system 跳过整个 chunk，而非逐个 entity 检查
```

**使用场景：**
- 标记整个 chunk 的属性（全死/全活/全静止）
- Chunk 级统计信息
- 替代一些简单的 SharedComponent 用法（避免 chunk 分裂）

#### Chunk Component vs SharedComponent

| | Chunk Component | SharedComponent |
|---|---|---|
| 创建新 chunk | 手动添加时 | **每次值改变强制 entity 移动** |
| 存储 | 每个 chunk 一份 | 每个 chunk 一份 |
| 适用 | 同 chunk entity 的"标签" | 分组+跨 chunk 相同值共享 |
| Chunk 分裂 | 不会 | **值改变时强制分裂** |

### Prefab 的 Chunk 问题

从官方文档 `performance-chunk-allocations.html`：

> "prefabs have a different archetype to the entities they instantiate... each prefab occupies its own 16 KiB chunk"

**关键事实：**
- Prefab entity 有 `Prefab` component → 单独 archetype
- 每个 prefab archetype 至少 1 个 chunk（16 KiB），即使只有 1 个 entity
- 100 个不同的 prefab = 至少 1.6 MB 的 chunk 内存，**大部分为空**
- Instantiation 时 `Prefab` component 被剥离 → 实例 entity 使用不同 archetype

**EX-GAS 影响：** 大量不同配置的 GE prefab 会导致 prefab chunk 内存爆炸。**优先使用 BlobAsset + static definition 替代 prefab。**

### Component 类型分类与选型

ECS 中有多种 component 类型，选型直接决定数据布局和访问模式：

| Component 类型 | 存储位置 | 典型用途 | 注意事项 |
|---|---|---|---|
| `IComponentData` | Chunk 内 per-entity | 运行时状态、配置引用 | 热路径首选 |
| `IBufferElementData` | Chunk 内（内联或指针） | 可变长度数组 | 溢出后性能下降 |
| `ISharedComponent` | Chunk 外，同值 entity 共享 | 分组、渲染 LOD | 值改变 = 移动 entity 到新 chunk |
| `ICleanupComponent` | Chunk 内 | 生命周期清理追踪 | 普通 destroy 不删除 cleanup component |
| `Chunk Component` | 每个 chunk 一份 | chunk 级元数据 | 极低存储成本，适合 chunk skip/统计 |
| `ISystem` Singleton | World 级唯一 | 全局配置、调度控制 | 通过 `SystemAPI.GetSingleton<T>()` 访问 |

### 数据 Store 选型框架

EX-GAS 中不同性质的数据需要不同的承载方式：

| 数据性质 | 首选承载 | 容量策略 | 确定性要求 |
|---|---|---|---|
| **Owner-local active effects** | `DynamicBuffer<BActiveEffectSlot>` | 固定容量（如 64 slot） | 不需要排序 |
| **每帧 EffectCommand fan-in** | `NativeStream`（并行）或 per-thread ECB append | per-frame scratch | 需 deterministic merge |
| **Attribute Delta（需归并）** | per-target `DynamicBuffer<BAttributeDelta>` | per-frame clear | 需按 target 分组 + deterministic reduce |
| **Typed Simulation Facts** | `DynamicBuffer<BTypedFact>` on singleton | per-frame clear | 生产顺序需可复现 |
| **Presentation outbox** | transient `NativeStream` 或 boundary managed queue | per-frame drain | 不需要确定性 |
| **Debug telemetry** | sampled `NativeList`（Persistent） + periodic export | 容量上限截断 | 不需要确定性 |
| **Static definition lookup** | `BlobAssetReference` 或 generated static array | 不变 | 不需要 |

### Singleton vs DynamicBuffer 选择

```csharp
// Singleton 方式：全局唯一容器
public struct GASFrameCommandQueue : IComponentData
{
    public NativeStream CommandStream;
    public int CommandCount;
}

// Buffer 方式：挂在 singleton entity 上
// 优势：ECS 安全系统自动管理；劣势：一帧内多个 writer 时成为瓶颈
var commandBuffer = SystemAPI.GetSingletonBuffer<CCommandEntry>();
```

**选择依据：**
- 全局唯一 + 低频写入 → singleton component + native container
- 全局唯一 + 单 writer → singleton DynamicBuffer
- 全局唯一 + 多 writer 并行 → NativeStream（每线程独立段）
- per-owner 数据 → owner entity 上的 DynamicBuffer

### DynamicBuffer 的 Job 调度优势

从官方文档 `components-buffer-introducing.html`：

> "Dynamic buffers don't have the job scheduling restrictions that native containers on components have"

**关键差异：**
- `NativeList` / `NativeArray` 在 component 上有 job 调度限制（同一帧内不能多 job 写）
- `DynamicBuffer` 没有这些限制——ECS 安全系统原生管理
- 所以："when there's more than one entity that needs a collection on it, use a dynamic buffer"

---

## 官方证据

| 证据 | 结论 |
|---|---|
| `concepts-archetypes.html` | Archetype = 相同 component 组合的 entity；每个 chunk 16 KiB，最多 128 entity；world 生命周期内 archetype 不销毁 |
| `performance-chunk-allocations.html` | 三种碎片化：大型 entity / SharedComponent 误用 / Archetype 过多；100K entity 各有独特 archetype = >1.5 GB 浪费 |
| `performance-chunk-allocations.html` (prefabs) | 每个 prefab 占 16 KiB chunk；大量不同 prefab → 内存爆炸 |
| `performance-chunk-allocations.html` (tag) | "Each tag component multiplies the number of archetypes permutations by two" → 用 enableable 替代 |
| `performance-chunk-allocations.html` (temp data) | "Temporary addition and removal of components... store it in a dynamic buffer" → 直接验证 ISSUE-001 |
| `components-buffer-introducing.html` | Buffer 内联在 chunk 中；溢出外部化；结构变化使 handle 失效；无 NativeContainer 调度限制 |
| `components-buffer-create.md` | `InternalBufferCapacity` 控制初始容量；溢出后数据外部化 |
| `api_index.md:21-22` | SharedComponent 和 CleanupComponent 语义不同 |

## 使用模式与反模式

**正确模式：**
- Owner-local collection → DynamicBuffer
- 并行 fan-in → NativeStream + deterministic merge
- 生命周期 cleanup → CleanupComponent
- Chunk 级条件 → Chunk Component
- 只读定义 → BlobAsset + generated static table
- 高频状态标记 → Enableable（不翻倍 archetype）

**反模式：**
- 单一全局 DynamicBuffer 做百万实体并行 fan-in
- Buffer 溢出不监控
- 结构变化后不重取 buffer handle
- 每帧创建+销毁 buffer entity
- CleanupComponent 被当作普通状态组件使用
- **用 tag component 做高频状态切换（每个 tag 翻倍 archetype 排列数！）**
- **临时数据用 Add/Remove Component 而非 DynamicBuffer**
- **过多不同 GE prefab（每个 prefab 占 16 KiB chunk）**

## EX-GAS 项目解读

### ActiveEffectStore 的物理设计

目标态 ActiveEffectStore 不应是"GE entity 镜像到 owner buffer"。正确的物理形态：

```
ASC Entity
  ├── BActiveEffectSlot[N]    (DynamicBuffer, InternalBufferCapacity=16)
  │    每个 slot: EffectCode, StackCount, RemainingDuration, PeriodTimer, Flags
  ├── TagMaskComponent         (IComponentData bitmask)
  ├── TagStatusFlagsComponent  (可选派生 cache，同 archetype)
  └── PeriodDueTag / ChunkComponent（可选 skip cache，必须有 profiler 证据）
```

`IEnableableComponent` 在这里不是“per-effect slot”的默认表达。Unity enableable 是 component/entity 级开关，不是 `DynamicBuffer` 内某个 slot 的开关；slot 内状态默认用 enum / bit flags。只有当 query 过滤收益明确大于 enableable 同步等待和额外 component 成本时，才把某个派生状态升格为 enableable 或 chunk-level cache。

**Store 命名必须表达职责：**
- `OwnerLocalStore`：per-ASC，同 chunk 内访问
- `GlobalIndexedStore`：跨 entity 查询，需要 lookup 或 secondary index
- `LifecycleCleanupStore`：用于追踪待清理的 granted tag/ability

### 对 EffectCommand fan-in 的设计

```csharp
// 并行收集阶段
ParallelWriter writer = commandStream.AsWriter();

// Merge 阶段 —— 必须 deterministic
for (int i = 0; i < commandStream.ForEachCount; i++)
{
    var segment = commandStream[i];
    for (int j = 0; j < segment.Length; j++)
    {
        sortedCommands.Add(segment[j]);  // 按 target ASC 排序
    }
}
// 排序后按 target 分发到 per-owner buffer
```

### 避免 Prefab Chunk 内存爆炸

```
当前（有风险）：
  - 100 个不同 GE prefab → 100 个 prefab archetype → 至少 1.6 MB 空 chunk
  - 1000 个？→ 16 MB 空 chunk

目标态：
  - GE 定义 → BlobAsset 或 generated static table
  - 运行时 GE → EffectCommand stream（本帧），ActiveEffectStore slot（跨帧）
  - 仅少量 "原型 entity" 使用 prefab
```

### Debugger 的 Buffer Pressure 指标

Debugger 必须输出：
- `bufferLength` / `bufferCapacity` / `externalizedCount`
- `spillRate`（溢出比例）
- `peakUsage` / `avgUsage`
- **per-archetype** chunk count、entity count、unused capacity

这些不是"nice to have"，而是架构健康指标。

### Archetype 审计

Debugger 应定期输出：
- 当前 active archetype 总数
- 每个 archetype 的 chunk count 和 entity count
- **只有 1 个 entity 的 archetype 列表**（最可疑的碎片化信号）
- Prefab archetype 数量和总 chunk 内存

---

## 常见陷阱

1. **Buffer 溢出无声**：默认 128 字节容量很容易溢出，看不出来但每次访问多一次间接跳转
2. **Allocator 生命周期混乱**：Temp 用于一帧内，TempJob 用于 job 内（需 dispose），Persistent 必须明确 owner
3. **`ISharedComponent` 值改变**：触发 entity 移动 chunk，成本很高
4. **CleanupComponent 不被 destroy 清理**：手动 destroy 后 cleanup component 仍在，需要专门的 cleanup system
5. **Prefab 内存静默消耗**：每个 prefab 占用 16 KiB chunk，大量不同 prefab 导致显著内存浪费
6. **Archetype 数量失控**：100 entity 有 100 个不同 archetype = 1.6 MB（仅 chunk 结构，不算数据）
7. **Tag Component 排列爆炸**：N 个 tag = 2^N 个潜在 archetype 排列

## 验收指标

1. Debugger 报告 buffer length、capacity、externalized、spill rate
2. API 选型表说明为什么不用 NativeStream、Chunk Component 或 CleanupComponent
3. 百万实体压测中 store 访问呈线性 chunk 扫描或可解释的 deterministic merge
4. ActiveEffectStore 不出现 per-effect-application entity churn
5. Debugger 输出 per-archetype 统计，能识别单 entity archetype 和 prefab chunk 浪费
6. 所有 ISharedComponent 的使用都有明确必要性说明
