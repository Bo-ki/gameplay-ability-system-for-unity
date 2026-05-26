# Enableable-Component选型: API 与 EX-GAS 解读

## 核心概念

### 什么是 Enableable Component

`IEnableableComponent` 是 Unity ECS 提供的标记接口，继承自 `IComponentData`。实现此接口的 component 可以在不改变 entity archetype 的前提下被启用或禁用，从而控制 entity 在 EntityQuery 中的可见性。

```csharp
public struct CAbilityActive : IComponentData, IEnableableComponent
{
    public int AbilityCode;
}
```

**关键区别：** AddComponent/RemoveComponent 触发结构变化（archetype 迁移 + sync point）；Enableable toggle 只修改 chunk 内的 enabled bit mask，不改变 archetype，无需 sync point。

### Enableable 的三种操作方式

| 方式 | 上下文 | 性能 | 说明 |
|---|---|---|---|
| `EnabledRefRW<T>.ValueRW` | IJobEntity / idiomatic foreach | **最快** | 利用线性遍历的可预测访问模式 |
| `EnabledMask[index]` | IJobChunk | 快 | chunk 级 enabled bit 数组索引 |
| `ComponentLookup.SetComponentEnabled` | 随机访问 | **有额外开销** | 需定位 entity 数据，高频场景避免 |
| `EntityManager.SetComponentEnabled` | 主线程 | 有额外开销 + 可能触发 sync point | 仅用于调试 / 低频操作 |

#### 方式一：EnabledRefRW（IJobEntity 推荐）

```csharp
[BurstCompile]
public partial struct DisableFinishedAbilityJob : IJobEntity
{
    void Execute(EnabledRefRW<CAbilityActive> active, in CAbilityRuntimeState state)
    {
        if (state.RemainingTime <= 0)
            active.ValueRW = false;  // 无结构变化，无 sync point
    }
}
```

`EnabledRefRW<T>` 是 IJobEntity 中最直接的 enableable 操作方式。编译器将其编译为 chunk 内 enabled mask 的批量位操作，访问模式完全可预测。

#### 方式二：EnabledMask（IJobChunk 批量操作）

```csharp
var mask = chunk.GetEnabledMask(ref myTypeHandle);
for (int i = 0; i < chunk.Count; i++)
{
    if (ShouldDisable(i))
        mask[i] = false;  // 批量位操作
}
```

`EnabledMask` 提供 chunk 级 enabled bit 数组的直接索引。适用于批量激活/禁用场景（如批量复活、批量击杀），比逐个 `SetComponentEnabled` 快 10-100x。

参考 `CASE-20.md`。

#### 方式三：ComponentLookup.SetComponentEnabled（随机访问）

```csharp
ComponentLookup<CAbilityActive> lookup = ...;
lookup.SetComponentEnabled(targetEntity, false);  // 随机访问
```

有额外的 entity 定位开销（内部哈希查找 + 随机内存访问）。适用于低频、非批量场景。

#### 方式四：EntityManager.SetComponentEnabled（主线程）

在主线程使用 `EntityManager.SetComponentEnabled` 修改 enableable 状态。如果存在写 job 未完成，可能触发 sync point。仅建议在调试、初始化、拆解阶段使用。

#### Enableable 查询的三种变体

| 方法 | 语义 | Sync Point 风险 |
|---|---|---|
| `.CalculateEntityCount()` | 仅统计 enabled entity | 有写 job 未完成时触发 sync point |
| `.CalculateEntityCountIgnoreFilter()` | 统计全部 entity（忽略 enabled） | 无 sync point |
| `.ToEntityArrayAsync()` | 异步获取 entity 数组 | 不阻塞主线程，返回 NativeList |

### Enableable vs Add/Remove Component 选型对照

| 场景 | 推荐 | 原因 |
|---|---|---|
| 高频状态开关（每帧可能多次） | Enableable | 无结构变化，无 sync point，无 archetype 迁移 |
| 低频生命周期（创建/销毁时一次） | Add/Remove Component | 语义更明确，减少 enableable 查询复杂度 |
| active/inactive 标记 | Enableable | 一次写入，所有依赖 query 自动反应 |
| tag/role 授予 | Enableable 或 owner-local bitset | 避免每个 tag 创建一个 component 类型 |
| 永久性 component 添加 | Add Component | entity 整个生命周期都需要 |

### Enableable 的竞态条件

> "Avoid enabling or disabling a component on an entity that another thread might process in a job because this often leads to a race condition."

```csharp
// 危险：两个 job 同时操作同一 entity 的 enable state
// Job A: 遍历并 disable component
// Job B: 遍历并 enable component
// 同一帧内 -> 不确定结果

// 安全：确保同一帧内每个 entity 只有一个 writer
// 通过 phase 分离读写窗口
```

### EntityQueryMask（CASE-22）

`EntityQueryMask` 提供 O(1) 实体-query 匹配检查：`query.GetEntityQueryMask()` + `mask.MatchesIgnoreFilter(entity)`。用于 entity 分类过滤、archetype 分组路由。构建 mask 有初始开销，高频时才值得。

### ChunkEntityEnumerator（CASE-26）

IJobChunk 中处理 enableable component 时必须使用 `ChunkEntityEnumerator`，而非简单 `for` 循环：

```csharp
var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
while (enumerator.NextEntityIndex(out var i))
{
    // i 始终是 enabled entity 的索引
}
```

无 enableable 时可简单 `for` 并用 `Assert.IsFalse(useEnabledMask)` 断言。

---

## EX-GAS 项目解读

### Enableable 在 EX-GAS 中的角色

EX-GAS 2.0 中 Enableable Component 是替代高频 entity 创建/销毁的核心机制，对应三个关键场景：

1. **Active Effect Store**：每个 ASC entity 持有 `DynamicBuffer<CActiveEffectSlot>` 存储所有活跃效果。每个 slot 通过 enableable 标记 active/inactive。duration 到期时只需 `slotActive.ValueRW = false`，无需销毁 effect entity。

2. **Ability Cooldown / Granted State**：Ability 的 cooldown 状态、granted tag、attribute modifier 的开关——这些高频切换的状态使用 Enableable 标记，无结构变化。

3. **Effect Granted Tag Set**：原来每个 granted tag 是一个单独 component，导致 archetype 排列爆炸。改为 owner-local bitset（如 `DynamicBuffer<int>` 存储位掩码）或 `IEnableableComponent` 数组。

### 关键代码映射

| 代码位置 | 当前状态 | 目标态 |
|---|---|---|
| `CActiveEffectStore.cs` | 可能使用 entity 表示 active effect | DynamicBuffer + IEnableableComponent slot |
| `SEffectRemove.cs` | entity destroy 移除 effect | enableable toggle + slot reuse |
| `SAbilityCommit.cs` | 可能使用 Add/Remove 切换 cooldown 状态 | Enableable toggle |
| `SAbilityStateCleanup.cs` | entity cleanup | enableable 状态重置 |

### 选型决策树

```
需要该 entity 在 query 中是否可见？
  ├── 是，且切换频率 > 每帧 1 次
  │     └── 使用 IEnableableComponent
  ├── 是，但切换频率极低（初始化/销毁时各一次）
  │     └── 使用 AddComponent / RemoveComponent
  └── 不需要在 query 中过滤，只是标记
        └── 使用 DynamicBuffer<byte> bitset 或单字段 int flag
```

---

## 常见陷阱

1. **"Enableable 完全免费"** — Enableable toggle 本身便宜，但后续的同步 query（`CalculateEntityCount`、`ToEntityArray` 等）如果有未完成的写 job 会触发 sync point。查询成本不能忽略。

2. **"SetComponentEnabled 像 AddComponent 一样贵"** — 不是，`SetComponentEnabled` 不改变 archetype，只是位操作。但 random-access 版本有 entity 定位开销。

3. **"Simple for 循环在 IJobChunk 中没问题"** — 如果有 enableable component，简单 `for (int i = 0; i < chunk.Count; i++)` 会处理包括 disabled 在内的所有 entity。必须使用 `ChunkEntityEnumerator`。

4. **"Enableable 不影响 ComponentLookup"** — `ComponentLookup<T>` 可以读取和修改 disabled component 的数据。只是 entity 不会出现在匹配该 component 的 query 中。`ComponentLookup` 不受 enable/disable 影响。

5. **"Enableable 没有竞态风险"** — 两个 job 在同一帧 enable 和 disable 同一 entity 的同一个 component 会产生竞态。每个 entity 的 enableable 状态在同一帧只能有一个 writer。

6. **"所有 ComponentData 都应声明为 IEnableableComponent"** — 不是。IEnableableComponent 会增加 enabled mask 的开销（每个 chunk 多一个 bit array）。只在需要 toggle 的 component 上声明。

7. **`MatchesIgnoreFilter` vs `Matches`** — `EntityQueryMask.MatchesIgnoreFilter(entity)` 忽略 enableable 过滤返回 true 即使 component disabled。需要精确匹配时用 `Matches(entity)`。

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则 |
|---|---|---|
| `components-enableable-use.html` | Enableable 无 archetype 迁移；worker thread 可通过 `ComponentLookup.SetComponentEnabled` 安全修改；迭代优先于 random-access | EN-01, EN-03 |
| `performance-chunk-allocations.html` | 每个 tag component 使 archetype 排列数翻倍；临时数据应存 DynamicBuffer | PRF-03 |
| `concepts-archetypes.html` | 每次 Add/Remove Component 触发 archetype 迁移 | EN-01 |
| `performance-sync-points.md` | 同步 query 操作在 enableable 写 job 未完成时触发 sync point | EN-02 |
| `state-machine.md` | 官方 FSM 模式使用 enableable 组件结合 DynamicBuffer 存储状态数据 | FSM-01..06 |
| `components-buffer-introducing.html` | DynamicBuffer 作为 owner-local 可变数组 | CASE-04 |
| `systems-looking-up-data.md` | `ComponentLookup` 随机访问竞态 | P1-11 |
| `iterating-data-ijobchunk.md` | `ChunkEntityEnumerator` 标准 enableable 感知迭代 | CASE-26 |
