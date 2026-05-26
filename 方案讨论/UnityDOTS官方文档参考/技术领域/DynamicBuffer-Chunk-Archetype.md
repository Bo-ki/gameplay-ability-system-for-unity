# DynamicBuffer / Chunk / Archetype

## 职责

覆盖 DynamicBuffer 容量管理与溢出监控、Archetype 布局与碎片化诊断、Chunk 物理布局（16 KiB 固定块）、Chunk Component / SharedComponent 选型。不覆盖 Store 选型框架（见 Store选型-数据承载策略）、不覆盖 BlobAsset / NativeContainer / ECB 等其他数据承载方式。

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

来自官方文档 `performance-chunk-allocations.html`：

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

来自官方文档 `components-buffer-introducing.html`：

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

来自官方文档 `performance-chunk-allocations.html`：

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

## 编写规范

### BUF-01: DynamicBuffer 声明容量策略和 externalized 监控

- **声明**：每个 `DynamicBuffer` 声明必须通过 `[InternalBufferCapacity(N)]` 指定明确的内联容量策略，并在 Debugger 中输出 `bufferExternalizedRatio`。
- **来源**：`components-buffer-introducing.html` / `components-buffer-set-capacity.md`
- **为什么**：默认 128 字节容量很容易溢出，溢出后数据永不自动迁回 chunk，导致 cache locality 永久丧失。不监控则 externalized ratio 随 entity 数量线性上升而无感知。
- **EX-GAS 诊断**：Debugger 必须输出 `bufferLength / bufferCapacity / externalizedCount / spillRate / peakUsage / avgUsage`。`externalizedCount / totalBufferCount > 30%` 触发告警。
- **检查方法**：搜索所有 `IBufferElementData` 声明，确认有 `[InternalBufferCapacity]` 属性；检查 Debugger 是否输出 buffer pressure 指标。

### BUF-02: 单一全局 buffer 限于 proof/低量；不做百万实体 fan-in

- **声明**：单一全局 `DynamicBuffer` 不能作为百万实体并行 fan-in 的唯一通道。必须使用 `NativeStream`（并行 writer）或 per-thread ECB 替代。
- **来源**：`components-buffer-introducing.html` — DynamicBuffer 无 NativeContainer 调度限制但不等于全局百万级 fan-in 无瓶颈
- **为什么**：单一全局 buffer 作为所有 entity 的并行写入目标时，写入顺序不确定、内存写入互斥、失去并行意义。
- **EX-GAS 诊断**：EffectCommand fan-in 使用 `NativeStream` + deterministic merge，而非全局 `DynamicBuffer`。
- **检查方法**：审查所有使用 `SystemAPI.GetSingletonBuffer<...>()` 或全局 entity 上 DynamicBuffer 的写入路径，超过 100 并行 writer 则违规。

### BUF-03: Buffer handle 结构变化后必须重取

- **声明**：任何结构变化（`CreateEntity`/`DestroyEntity`/`AddComponent`/`RemoveComponent`）之后，先前获取的 `BufferHandle` / `BufferLookup` / `DynamicBuffer` 引用全部失效，必须重新获取。
- **来源**：`components-buffer-introducing.html` — "A dynamic buffer stored in a chunk is unmoved when you add or remove components to or from the entity"
- **为什么**：结构变化可能移动 entity 到新 chunk，使原有 buffer 指针指向旧 chunk 的已释放内存。ECS 安全系统在检测到后抛出异常，但若在 Playback 或特殊路径下未检测则产生静默数据错误。
- **EX-GAS 诊断**：ISSUE-004 曾暴露 `BufferTypeHandle invalidated by structural change` 错误。`SApplyGameplayEffectRequest` 在同一系统中读 buffer 后创建/销毁 entity。
- **检查方法**：搜索 GetBuffer / GetBufferLookup 后出现 CreateEntity/DestroyEntity/AddComponent/RemoveComponent 的模式。

### BUF-04: InternalBufferCapacity 按实际容量需求设定

- **声明**：`[InternalBufferCapacity(N)]` 必须根据运行时实际容量需求设定，而非使用默认值。容量不足高频溢出 -> 增大 N；极少超过内联容量 -> 保持默认或减小。
- **来源**：`components-buffer-set-capacity.md`
- **为什么**：容量过小导致高频溢出 -> 数据外部化 -> 每次访问多一次间接跳转。容量过大浪费 chunk 内宝贵空间 -> 每 chunk 容纳 entity 数减少 -> 插入成本增加。
- **EX-GAS 诊断**：ActiveEffectSlot buffer 设定 `InternalBufferCapacity=16` 而非默认，匹配一般 ASC 同时持有的 effect 数量。
- **检查方法**：审查每个 `IBufferElementData` 的 `[InternalBufferCapacity]` 值是否匹配其运行时典型长度（通过 Debugger 的 peakUsage 指标验证）。

### PRF-01: 禁止用 Entity 表示临时/瞬时状态 (P0)

- **声明**：每帧创建并随后销毁的 entity 表示临时状态，必须用 `DynamicBuffer`、`NativeStream` 或 `Enableable` 替代。
- **来源**：`performance-chunk-allocations.html` — "Temporary addition and removal of components... store it in a dynamic buffer so you can add and remove them without changing the entity archetype"
- **为什么**：每次 `CreateEntity` + `DestroyEntity` = 2 次结构变化 + 1 次 archetype 迁移。不同的临时 entity 创建不同 archetype。高频 create/destroy 导致 chunk 碎片化。100K entity 各有独特 archetype -> >1.5 GB chunk 浪费。
- **EX-GAS 诊断**：ISSUE-001 — Instant GE 走 `CApplyGameplayEffectRequest -> runtime GE entity -> lifecycle -> destroy`。x50 下 GameplayEffectApplied=3479 等呈线性增长。
- **检查方法**：Debugger 输出 `entityCreated - entityDestroyed ~= 0`。若 entityCreated 随 game event 数量线性增长 -> 违规。

### PRF-10: 监控 DynamicBuffer 溢出 (P1)

- **声明**：每个关键 buffer 类型必须设定 `InternalBufferCapacity`，Debugger 报告 `bufferExternalizedRatio`，超过 30% 触发告警。
- **来源**：`components-buffer-introducing.html` — 溢出后数据外部化且永不自动迁回。`components-buffer-set-capacity.md` — 即使缩容也不迁回。
- **为什么**：溢出后每次访问多一次间接内存跳转，大量外部化 buffer 导致缓存局部性丧失。静默性能退化 —— 不会报错，但速度越来越慢。
- **EX-GAS 诊断**：Debugger 输出 buffer pressure 指标（`bufferLength/capacity/externalizedCount/spillRate/peakUsage/avgUsage`）。per-archetype chunk count、entity count、unused capacity。
- **检查方法**：Per-buffer-type externalized 统计，`externalizedCount / totalBufferCount > 30%` 时告警。

### PRF-14: 禁止逐 Component 构建 Entity Archetype — 预创建 Archetype 批量创建 (P1)

- **声明**：使用 `EntityManager.CreateEntity()` 后逐次调用 `AddComponent<T>()` 会在每次调用时创建新的中间 archetype。这些中间 archetype 永久存在。必须预先用 `CreateArchetype()` 构建完整 archetype，再用 `CreateEntity(archetype, count)` 批量创建。
- **来源**：`optimize-structural-changes.md` — "Avoid adding components one at a time to construct entities at runtime... create the archetype that describes the entity you want to end up with and then create an entity directly from that archetype."
- **为什么**：每个 `AddComponent` = 一次结构变化 + 一个新 archetype。中间 archetype 永久存在，每次 query 创建/更新都要遍历所有 archetype。N 个 component 逐个添加 = N-1 个冗余中间 archetype。批量创建场景中 10000 entity x 3 component 逐个添加 = 20000 次结构变化 + 2 个冗余 archetype。
- **EX-GAS 诊断**：AutoChess battle 初始化（批量棋子创建）、Effect 批量应用、Definition 加载期的 entity 预分配。
- **检查方法**：搜索 `CreateEntity()` 后紧跟 `AddComponent` 的模式。Archetype 窗口检查冗余 archetype 数量。

### PRF-24: 禁止逐 Component 构建 Entity Archetype (P2)

- **声明**：同 PRF-14 原则。即使当前规模下逐 component 添加不明显影响性能，也应遵循一次性 Archetype 构建的习惯以避免 scale 时恶化。批量添加/移除多个 component 使用 `ComponentTypeSet` 一次完成。
- **来源**：`optimize-structural-changes.md` — 同 PRF-14。`ComponentTypeSet` 示例来自 `CASE-33`。
- **为什么**：在 scale 时（10K+ entity），逐 component 添加的代价从可忽略变为 P1 严重级。预先使用正确模式避免技术债务积累。
- **EX-GAS 诊断**：所有 entity 创建路径审计，确保使用 `CreateArchetype` + `CreateEntity(archetype, count)` 模式。AddComponent 操作优先使用 `ComponentTypeSet` 批量接口。
- **检查方法**：Code review 检查新 entity 创建路径。对有 `foreach` 中 `CreateEntity` + 逐个 `AddComponent` 的遗留代码标记迁移。

---

## 模式与反模式

### 正确模式

| 场景 | 推荐做法 | 依据 |
|---|---|---|
| Owner-local collection | DynamicBuffer | CASE-04，ECS 安全系统原生管理 |
| 并行 fan-in | NativeStream + deterministic merge | BUF-02 |
| Chunk 级条件/标签 | Chunk Component | CASE-28 |
| 静态定义数据 | BlobAsset / static table | PRF-01 替代 prefab |
| 高频状态标记 | IEnableableComponent | PRF-14 替代 tag component |
| 批量结构变化 | ComponentTypeSet | CASE-33 |
| 同大小 buffer 类型转换 | DynamicBuffer.Reinterpret | CASE-36 |
| Chunk 级批量 buffer 访问 | BufferAccessor in IJobChunk | CASE-23 |

### 反模式

| 反模式 | 原因 | 替代 |
|---|---|---|
| 单一全局 DynamicBuffer 做百万实体并行 fan-in | 写入顺序不确定、写入互斥、失去并行意义 | NativeStream + deterministic merge |
| Buffer 溢出不监控 | 静默退化，从 cache-friendly inline 变为 pointer chase | Debugger 输出 externalized ratio |
| 结构变化后不重取 buffer handle | ECS 安全系统抛异常或静默数据错误 | 结构变化后重新获取 handle |
| 每帧创建 + 销毁 buffer entity | PRF-01 — 结构变化和 archetype 碎片化 | 预分配 buffer 或 Enableable |
| CleanupComponent 被当作普通状态组件 | CleanupComponent 在 destroy 后仍在，需要 cleanup system | 使用普通 IComponentData |
| 用 tag component 做高频状态切换 | 每个 tag 翻倍 archetype 排列数 | IEnableableComponent |
| 临时数据用 Add/Remove Component 而非 DynamicBuffer | 每次触发结构变化 + archetype 迁移 | DynamicBuffer 存储临时数据 |
| 过多不同 GE prefab | 每个 prefab 占 16 KiB chunk | BlobAsset + static definition |

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

---

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

---

## 验收指标

1. Debugger 报告 buffer length、capacity、externalized、spill rate，每个关键 buffer 类型独立统计
2. 每个 `IBufferElementData` 声明具有明确的 `[InternalBufferCapacity(N)]` 值，且该值匹配运行时典型长度
3. 百万实体压测中 DynamicBuffer 访问呈线性 chunk 扫描或可解释的 O(n) 模式
4. ActiveEffectStore 不出现 per-effect-application entity churn（entityCreated ~= entityDestroyed ~= 0）
5. Debugger 输出 per-archetype 统计，能识别单 entity archetype 和 prefab chunk 浪费
6. 所有 entity 创建路径使用 `CreateArchetype` + `CreateEntity(archetype, count)` 模式，无 `CreateEntity()` 后逐个 `AddComponent` 的模式
7. Debugger 输出 archetype 总数、每个 archetype 的 chunk count/entity count、孤立的单 entity archetype 列表
8. `externalizedCount / totalBufferCount > 30%` 触发告警
