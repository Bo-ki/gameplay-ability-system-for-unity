# DynamicBuffer-Chunk-Archetype: API 与 EX-GAS 解读

## 核心概念

### Archetype 本质

Archetype 是同一 world 内具有相同 component type 组合的所有 entity 的唯一标识符。Archetype 决定 entity 存储在哪些 chunk 中。

```
世界中所有 entity 的 component type 组合相同的 -> 属于同一 Archetype
不同 component 组合 = 不同 Archetype = 不同 chunk 集合
```

Archetype 仅在 World 销毁时被销毁（不会在 entity 全部移除后销毁）。

### 16 KiB Chunk 的内部布局

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
- 数组紧密打包：entity[i] 在各数组的 index=i
- entity 移除时，最后一个 entity 移动到空缺位置（swap-remove）
- 最后一个 entity 从 chunk 移除后，chunk 被销毁
- Archetype 在 World 销毁前持续存在

### 三种 Chunk 碎片化类型

| 类型 | 特征 | 根因 | 后果 |
|---|---|---|---|
| 大型 Entity | unused entity 少 + chunk capacity 低 | entity component 过大 | 每 chunk 容纳 entity 少 -> 更多 chunk -> 更多 cache miss |
| SharedComponent 误用 | unused entity 多 + chunk capacity 高 | 不同的 SharedComponent 值强制 entity 分到不同 chunk | 能塞一起的 entity 被碎片化 |
| 过多 Archetype | 大量 archetype + 每个 chunk 数少 | entity 有太多不同 component 集合 | 100K entity 各有独特 archetype = >1.5 GB 浪费 |

**Archetype 过多的三大根因：**

| 根因 | 官方建议 |
|---|---|
| 过多 tag component | 每个 tag 翻倍 archetype 排列 -> 改用 enableable component |
| 临时 Add/Remove component | 临时数据存 DynamicBuffer 而非 Add/Remove |
| 大型 entity | 按使用频率拆分 entity（AI/physics/animation/render 分开） |

### DynamicBuffer

DynamicBuffer 是 entity 上的动态数组，优先内联存储在 chunk 中（避免额外内存跳转），溢出时外部化。

```csharp
[InternalBufferCapacity(8)]
public struct BActiveModifier : IBufferElementData
{
    public int AttributeCode;
    public float Magnitude;
    public int SourceEffectCode;
}

var buffer = SystemAPI.GetBuffer<BActiveModifier>(entity);
buffer.Add(new BActiveModifier { ... });
buffer.RemoveAt(0);
```

**InternalBufferCapacity 语义：**
- 默认值：sizeof(element) 能塞进 128 字节的最大数量
- 自定义值：`[InternalBufferCapacity(N)]` 指定初始内联容量
- 溢出条件：buffer Length > InternalBufferCapacity 时，数据移到 chunk 外独立分配

**溢出代价：**
- 每次 buffer 访问多一次间接内存跳转
- 外部数组不在 chunk 中 -> 失去 cache locality
- 大量 entity 的 buffer 同时溢出 -> 内存碎片化

**关键限制（来自官方文档）：** 如果 Unity 将 DynamicBuffer 数据移出 chunk，数据永远不会自动移回。即使后续 buffer 缩容到 capacity 以内，数据也不会迁回 chunk。浪费的 inline 空间在该 entity 生命周期内永久存在。可通过 `InternalBufferCapacity = 0` 始终外部化以避免迁移开销；`TrimExcess` 可减少 padded capacity 但不恢复 inline 存储。

**结构变化导致 handle 失效：** 任何结构变化（AddComponent / RemoveComponent / CreateEntity / DestroyEntity）会使之前获取的 `BufferHandle` / `BufferLookup` 失效。结构变化后必须重新获取 buffer handle，否则 ECS 安全系统抛出异常。

### DynamicBuffer 的 Job 调度优势（对比 NativeContainer）

- `NativeList` / `NativeArray` 在 component 上有 job 调度限制（同一帧内不能多 job 写）
- `DynamicBuffer` 没有这些限制 —— ECS 安全系统原生管理
- 所以：当有多个 entity 各自需要一个集合时，使用 DynamicBuffer

### Chunk Component

Chunk Component 是**每个 chunk 存储一份**的 component，极低存储成本。

```csharp
public struct ChunkAllDead : IComponentData { }
```

**Chunk Component vs SharedComponent：**

| | Chunk Component | SharedComponent |
|---|---|---|
| 创建新 chunk | 手动添加时 | 每次值改变强制 entity 移动 |
| 存储 | 每个 chunk 一份 | 每个 chunk 一份 |
| 适用 | 同 chunk entity 的 "标签" | 分组 + 跨 chunk 相同值共享 |
| Chunk 分裂 | 不会 | 值改变时强制分裂 |

Chunk Component 的修改不触发结构变化、不移动 entity、不翻倍 archetype 排列。

### Prefab 的 Chunk 内存问题

> Prefabs have a different archetype to the entities they instantiate... each prefab occupies its own 16 KiB chunk.

- Prefab entity 有 `Prefab` component -> 单独 archetype
- 每个 prefab archetype 至少 1 个 chunk（16 KiB），即使只有 1 个 entity
- 100 个不同 prefab = 至少 1.6 MB chunk 内存，大部分为空
- Instantiation 时 `Prefab` component 被剥离 -> 实例 entity 使用不同 archetype

### ComponentTypeSet 批量操作

使用 `ComponentTypeSet` 可在一次结构变化中添加/移除多个组件，最小化 archetype 中间态：

```csharp
var typeSet = new ComponentTypeSet(typeof(A), typeof(B), typeof(C));
EntityManager.AddComponent(entity, typeSet);  // 一次结构变化
```

### DynamicBuffer.Reinterpret

同 sizeof 的不同类型可重解释到同一块内存，共享安全句柄：

```csharp
myBuffer.Reinterpret<int>();  // 当 sizeof(T) == sizeof(int) 时
```

修改 `intBuffer[i]` 等价于修改 `myBuffer[i]` 的对应字节。跨大小类型编译失败。

### BufferAccessor

IJobChunk 中可使用 `BufferAccessor<T>` 批量访问 chunk 内所有 entity 的 buffer：

```csharp
var accessor = chunk.GetBufferAccessorRO<BMyData>(ref bufferHandle);
for (int i = 0; i < chunk.Count; i++)
{
    var buffer = accessor[i];
    // 操作该 entity 的 buffer
}
```

`BufferTypeHandle<T>` 与 `BufferLookup<T>` 不同，专门用于 IJobChunk 的 chunk 级批量访问。

---

## EX-GAS 项目解读

### ActiveEffectStore 的目标物理形态

ActiveEffectStore 不应是 "GE entity 镜像到 owner buffer"。正确的物理形态：

```
ASC Entity
  +-- BActiveEffectSlot[N]    (DynamicBuffer, InternalBufferCapacity=16)
  |    每个 slot: EffectCode, StackCount, RemainingDuration, PeriodTimer, Flags
  +-- BGrantedTagMask          (CTagMask, IComponentData)
  +-- CActiveEffectEnableable  (IEnableableComponent, per-effect-type)
```

- 每个 ASC entity 持有一个固定容量的 `BActiveEffectSlot` buffer
- 不创建独立的 GE runtime entity
- `CActiveEffectEnableable` 用于 per-effect-type 的状态开关

### EffectCommand fan-in 的数据流

```csharp
// 并行收集阶段 —— NativeStream 而非全局 buffer
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

### Debugger 的 Buffer Pressure 指标

每个关键 buffer 类型必须报告：
- `bufferLength` / `bufferCapacity` / `externalizedCount`
- `spillRate`（溢出比例）
- `peakUsage` / `avgUsage`
- `per-archetype` chunk count、entity count、unused capacity

这些是架构健康指标，非 "nice to have"。

### Archetype 审计

Debugger 应定期输出：
- 当前 active archetype 总数
- 每个 archetype 的 chunk count 和 entity count
- **只有 1 个 entity 的 archetype 列表**（最可疑的碎片化信号）
- Prefab archetype 数量和总 chunk 内存

### 避免 Prefab Chunk 内存爆炸

```
当前（有风险）：
  - 100 个不同 GE prefab -> 100 个 prefab archetype -> 至少 1.6 MB 空 chunk
  - 1000 个 -> 16 MB 空 chunk

目标态：
  - GE 定义 -> BlobAsset 或 generated static table
  - 运行时 GE -> EffectCommand stream（本帧），ActiveEffectStore slot（跨帧）
  - 仅少量 "原型 entity" 使用 prefab
```

### Instant GE 的无 entity 路径

```
Instant GE -> EffectCommand -> InstantEffectSpec -> AttributeDelta -> TypedFact
（全程无 entity 创建）
```

---

## 常见陷阱

1. **Buffer 溢出无声**：默认 128 字节容量很容易溢出，看不出来但每次访问多一次间接跳转。数据永不自动迁回。
2. **Prefab 内存静默消耗**：每个 prefab 占用 16 KiB chunk，大量不同 prefab -> 显著内存浪费。
3. **Archetype 数量失控**：100 entity 有 100 个不同 archetype = 1.6 MB（仅 chunk 结构，不算数据）。
4. **Tag Component 排列爆炸**：N 个 tag = 2^N 个潜在 archetype 排列。10 个 tag = 最多 1024 种。
5. **`ISharedComponent` 值改变**：触发 entity 移动到新 chunk，成本很高（结构变化）。
6. **CleanupComponent 不被 destroy 清理**：手动 destroy 后 cleanup component 仍在，需要专门的 cleanup system。
7. **结构变化使 handle 失效**：GetBuffer 后任何结构变化使 buffer 引用失效，再读时抛异常。
8. **Buffer 内联空间永久浪费**：缩容不恢复 inline —— `TrimExcess` 减少外部数组 padding 但不迁回 chunk。

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则 |
|---|---|---|
| `concepts-archetypes.html` | Archetype = 相同 component 组合的 entity；每个 chunk 16 KiB，最多 128 entity；world 生命周期内 archetype 不销毁 | PRF-01, PRF-14 |
| `performance-chunk-allocations.html` | 三种碎片化类型；100K entity 各有独特 archetype = >1.5 GB 浪费 | PRF-01, PRF-24, PRF-14 |
| `performance-chunk-allocations.html` (prefabs) | 每个 prefab 占 16 KiB chunk；大量不同 prefab -> 内存爆炸 | PRF-01 |
| `performance-chunk-allocations.html` (tag) | "Each tag component multiplies the number of archetypes permutations by two" | PRF-01 |
| `performance-chunk-allocations.html` (temp data) | "Temporary addition and removal of components... store it in a dynamic buffer" | PRF-01 |
| `components-buffer-introducing.html` | Buffer 内联在 chunk 中；溢出外部化；结构变化使 handle 失效；无 NativeContainer 调度限制 | BUF-01, BUF-02, BUF-03 |
| `components-buffer-set-capacity.md` | InternalBufferCapacity 控制初始容量；溢出后永不迁回；TrimExcess 不恢复 inline | BUF-01, BUF-04, PRF-10 |
| `components-buffer-reinterpret.md` | Buffer.Reinterpret 同大小类型重解释共享安全句柄 | CASE-36 |
| `optimize-structural-changes.md` | 禁止逐 component 构建 entity；使用 CreateArchetype + 批量 CreateEntity；ComponentTypeSet | PRF-14, PRF-24, CASE-33 |
| `components-chunk-use.md` / `ChunkComponentExamples.cs` | Chunk Component 用法，每 chunk 一份，不触发 chunk 分裂 | CASE-28 |
| `iterating-data-ijobchunk.md` / `chunk.GetBufferAccessorRO` | BufferAccessor 批量 chunk 级 buffer 访问 | CASE-23 |
| `components-enableable-use.html` | Enableable 替代 tag component | PRF-01 |
