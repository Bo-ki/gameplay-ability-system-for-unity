# Enableable-Component 选型

## 职责

覆盖 `IEnableableComponent` 的选型决策、操作方式、性能特征与约束。阐述为什么 Enableable 是高频状态切换的首选机制、何时仍应使用 Add/Remove Component、以及 Enableable 在实际使用中容易被忽视的成本（查询同步、random-access 开销、竞态条件）。

不覆盖 ECB 或结构变化的通用规则（参见《结构变化-ECB》），不覆盖非 ECS 的状态管理方式。

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
// 在 IJobChunk 中按 index 操作 enable 状态
var mask = chunk.GetEnabledMask(ref myTypeHandle);
for (int i = 0; i < chunk.Count; i++)
{
    if (ShouldDisable(i))
        mask[i] = false;  // 批量位操作
}
```

`EnabledMask` 提供 chunk 级 enabled bit 数组的直接索引。适用于批量激活/禁用场景（如批量复活、批量击杀），比逐个 `SetComponentEnabled` 快 10-100x。

参考 `主题/12-官方案例模式.md` CASE-20。

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

## 编写规范

### EN-01: 高频开关优先 Enableable，低频生命周期再考虑 Add/Remove

**声明：** 场景需要高频（每帧多次）切换 entity 状态时（active/inactive、alive/dead、tag on/off），必须使用 `IEnableableComponent` 而非 `AddComponent`/`RemoveComponent`。仅当操作是低频的实体生命周期变更（创建时一次性添加、销毁前一次性移除）才使用 Add/Remove。

**来源：** `components-enableable-use.html`；`主题/03-结构变化-ECB-Enableable.md`

**为什么：** Enableable toggle 只修改 enabled bit，无结构变化、无 archetype 迁移、无 sync point。Add/Remove 每次触发完整的 archetype 迁移与数据复制，高频执行导致 chunk 碎片化和 sync point 堆积。

**EX-GAS 诊断：** ActiveEffect 的 duration 到期切换、ability cooldown 状态切换、attribute modifier 的 granted tag 开关——这些高频状态切换当前可能使用 Add/Remove，应统一改为 Enableable。

**检查方法：** 搜索高频路径（每帧执行）中出现 `AddComponent` / `RemoveComponent` 的位置，评估是否可替换为 `SetComponentEnabled`。Debugger 输出 `enableableToggleCount` 和 `componentAddRemoveCount` 作为对比指标。

---

### EN-02: Enableable 查询成本和同步等待进入性能诊断

**声明：** Enableable 不是完全免费的。同步 EntityQuery 操作（如 `CalculateEntityCount()`、`ToEntityArray()`、`GetSingleton<T>()`）在存在 enableable 写 job 未完成时，会触发 sync point 等待写 job 完成。这些查询成本必须纳入性能诊断范围。

**来源：** `components-enableable-use.html`；`主题/13-DOTS编写规范与性能陷阱.md` P1-05

**为什么：** Enableable 过滤需要读取 enabled mask。如果存在写入 mask 的 job 尚未完成，主线程必须等待它完成才能获取准确的过滤结果。高频 enableable toggle + 同步 query = 仍有 sync point。

**EX-GAS 诊断：** Debugger 应追踪每秒 sync point 中由 enableable query 触发的比例。如果 `syncPointByEnableableQuery > 0` 且高频出现，说明 enableable toggle job 和同步 query 在同一帧冲突。

**检查方法：**
- 对于热点路径中的同步 query 操作，检查是否可使用 `IgnoreFilter` 变体或 `Async` 变体避免 sync point
- 场景中大量 `CalculateEntityCount()` 紧跟 enableable 写 job → 标为待优化
- 使用 `EntityQueryOptions.IgnoreComponentEnabledState` 构建 query 可彻底绕开 enableable 过滤

---

### EN-03: Random-Access Enableable 方法有额外开销；迭代优先

**声明：** 通过 `ComponentLookup.SetComponentEnabled(entity, value)` 随机访问修改 enableable 状态有额外的 entity 定位开销（哈希查找 + 随机内存访问）。高频路径应优先使用迭代式方法（`EnabledRefRW<T>` 或 `EnabledMask[index]`）。

**来源：** `components-enableable-use.html`；`主题/13-DOTS编写规范与性能陷阱.md` P1-02

**为什么：** `SetComponentEnabled` 通过 ComponentLookup 查找到目标 entity 的 chunk 和 index，涉及哈希表查找和可能的 cache miss。在 IJobEntity 的紧密循环中，一次 random lookup 的成本 ≈ 10-20 次顺序 entity 处理。

**EX-GAS 诊断：** Effect 生命周期管理中逐 entity 调用 `SetComponentEnabled` 切换状态——应评估是否可改为 IJobChunk 批量操作或 IJobEntity 中 `EnabledRefRW<T>` 的顺序遍历。

**检查方法：** Grep 搜索 `SetComponentEnabled` 在 job 中的调用。如果在 IJobEntity.Execute 内部出现按 entity 参数的随机访问 → 评估是否可改为 `EnabledRefRW<T>` 参数声明。

---

### PRF-03: 禁止用 Tag Component 做高频状态标记（P0 致命）

**声明：** 每个 tag component（无数据字段的 `IComponentData`）使 archetype 排列数翻倍。高频 toggle 的状态标记必须使用 `IEnableableComponent` 或 owner-local bitset，不得使用 Add/Remove Tag Component。

**来源：** `performance-chunk-allocations.html`；`主题/13-DOTS编写规范与性能陷阱.md` P0-03

**为什么：** N 个独立的 tag component → 最多 2^N 种 archetype 排列。10 个 tag → 最多 1024 种 archetype → 1024 × 16 KiB = 16 MB 仅 chunk header。50 个 tag → 天文数字。即使实际未达上限，每次 entity 获得/失去 tag = archetype 迁移。

**EX-GAS 诊断：** 审计所有 `IComponentData` 中无数据字段的 struct——这些是隐式的 tag component，可能在不同 archetype 间产生排列爆炸。特别是 GrantedTags、AbilityTag、EffectTag 等标记类数据。

**检查方法：**
- Archetype 窗口检查：archetype 总数是否接近 entity 总数（archetype 膨胀的典型信号）
- 审计所有 `IComponentData` struct 中无数据字段的 "marker" component
- 高频 toggle 的 tag → 改为 `IEnableableComponent`
- Debugger 报告 `archetypeCount` 和 `tagComponentCount` 作为监控指标

## 模式与反模式

### 正确模式

**模式 1：高频状态切换用 EnabledRefRW（CASE-06）**
```csharp
void Execute(EnabledRefRW<CAbilityActive> active, in CAbilityRuntimeState state)
{
    if (state.RemainingTime <= 0)
        active.ValueRW = false;
}
```
IJobEntity 中最自然的 enableable 写法，零额外开销，无 sync point。

**模式 2：批量状态切换用 EnabledMask（CASE-20）**
```csharp
var mask = chunk.GetEnabledMask(ref abilityActiveHandle);
for (int i = 0; i < chunk.Count; i++)
    if (ShouldDeactivate(i))
        mask[i] = false;
```
IJobChunk 中批量操作，比逐 entity random-access 快 10-100x。

**模式 3：低频生命周期变更用 Add/Remove Component**
仅在创建 entity 时一次性添加、销毁时一次性移除的组件，使用常规 Add/Remove。不发生高频 toggle 时，不会产生性能问题。

**模式 4：Owner-local Bitset 替代 Tag Component**
当需要大量 tag/flag 标记时，使用 `DynamicBuffer<int>` 或位压缩字段存储 bitset，避免每个 tag 占用一个 component 类型，杜绝 archetype 排列爆炸。

### 反模式

**反模式 1：用 Tag Component 做高频状态标记**
- 原因：每个 tag 使 archetype 排列翻倍。高频 toggle 导致大量 archetype 迁移和 chunk 碎片
- 替代：`IEnableableComponent` 或 owner-local bitset

**反模式 2：每帧 AddComponent/RemoveComponent 切换状态**
- 原因：每次触发结构变化 + archetype 迁移 + 可能的 sync point
- 替代：Enableable toggle（修改 enabled bit，零结构变化）

**反模式 3：已废弃：用 Entity 存在/销毁表示临时状态**
- 原因：CreateEntity + DestroyEntity = 2 次结构变化 + 1 次 archetype 迁移
- 替代：Enableable 标记 active/inactive，entity 本身作为生命周期容器

**反模式 4：高频同步 Query 跟在 Enableable 写 Job 之后**
- 原因：Enableable 写 job 未完成时，同步 query 触发 sync point
- 替代：使用 `IgnoreFilter` 变体、`Async` 变体，或将同步 query 移到写 job 完成的 phase

**反模式 5：IJobChunk 中跳过 ChunkEntityEnumerator 直接 for 循环**
- 原因：简单 for 循环会处理 disabled entity，结果错误
- 替代：使用 `ChunkEntityEnumerator`（CASE-26），或在确认无 enableable 时用 `Assert.IsFalse(useEnabledMask)`

## EX-GAS 项目解读

### Enableable 在 EX-GAS 中的角色

EX-GAS 2.0 中 Enableable Component 是替代高频 entity 创建/销毁的核心机制，对应三个关键场景：

1. **Active Effect Store**：每个 ASC entity 持有 `DynamicBuffer<CActiveEffectSlot>` 存储所有活跃效果。每个 slot 通过 enableable 标记 active/inactive。duration 到期时只需 `slotActive.ValueRW = false`，无需销毁 effect entity。

2. **Ability Cooldown / Granted State**：Ability 的 cooldown 状态、granted tag、attribute modifier 的开关——这些高频切换的状态使用 Enableable 标记，无结构变化。

3. **Effect Granted Tag Set**：原来每个 granted tag 是一个单独 component，导致 archetype 排列爆炸。改为 owner-local bitset（如 `DynamicBuffer<int>` 存储位掩码）或 `IEnableableComponent` 数组。

### 关键代码映射

| 代码位置 | 当前状态 | 目标态 |
|---|---|---|
| `Assets/GAS/Runtime/Effect/Component/Dynamic/CActiveEffectStore.cs` | 可能使用 entity 表示 active effect | DynamicBuffer + IEnableableComponent slot |
| `Assets/GAS/Runtime/System/Effect/SEffectRemove.cs` | entity destroy 移除 effect | enableable toggle + slot reuse |
| `Assets/GAS/Runtime/System/Ability/SAbilityCommit.cs` | 可能使用 Add/Remove 切换 cooldown 状态 | Enableable toggle |
| `Assets/GAS/Runtime/System/Ability/SAbilityStateCleanup.cs` | entity cleanup | enableable 状态重置 |

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

## 验收指标

1. 所有高频（每帧 > 1 次）状态切换使用 `IEnableableComponent` 而非 Add/Remove Component
2. Debugger 输出 `enableableToggleCount`、`enableableQuerySyncCount`、`archetypeCount`、`tagComponentCount`
3. Archetype 总数远小于 entity 总数（无 archetype 排列爆炸）
4. Runtime Core hot path 零 Add/Remove Component 用于高频状态 toggle
5. 所有 IJobChunk 中涉及 enableable component 的迭代使用 `ChunkEntityEnumerator`
6. Debugger 能区分 sync point 中由 enableable query 触发的比例
7. 不存在无数据字段的 `IComponentData` 被用于高频标记（使用 `IEnableableComponent` 替代）
