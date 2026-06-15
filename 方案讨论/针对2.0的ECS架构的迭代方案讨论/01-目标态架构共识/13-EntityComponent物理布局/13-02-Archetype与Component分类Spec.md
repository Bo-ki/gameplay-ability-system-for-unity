# 13-02：Archetype 与 Component 分类 Spec

> Owner：`01-目标态架构共识/13-EntityComponent物理布局` | 状态：目标态 Spec 子页 | 来源：`../13-EntityComponent物理布局Spec.md` 同 owner 拆分

本文件只描述目标态 Entity / Component 物理布局，不记录当前实现状态、迁移进度、验证数字或任务计划。现实代码事实必须回到 `../../00-当前架构事实/`，任务拆分必须回到 `../../02-主线任务树/`。

## ASC Entity 批量创建策略（`PRF-24`）

> **`PRF-24`**：禁止逐 Component 构建 Entity Archetype。使用 `EntityManager.CreateEntity()` 后逐次 `AddComponent<T>()` 会在每次调用时创建中间 archetype，这些中间 archetype 在应用剩余生命周期内持续存在并增加所有 `EntityQuery` 的计算开销。

**正确做法 —— 预建 Archetype 批量创建：**

```csharp
// 在 battle bootstrap / ASC factory 中一次性创建
var ascArchetype = EntityManager.CreateArchetype(
    typeof(ASCIdentityComponent),
    typeof(CombatAttributeCurrentSetComponent),
    typeof(CombatAttributeBaseSetComponent),
    typeof(ResourceAttributeCurrentSetComponent),
    typeof(ResourceAttributeBaseSetComponent),
    typeof(AttributeDirtyMaskComponent),
    typeof(TagMaskComponent),
    typeof(ASCActiveEffectsComponent),
    typeof(AbilitySlotBuffer),
    typeof(ActiveGameplayEffectBuffer),
    typeof(PresentationEventBuffer),
    typeof(GameplayEventBuffer)      // 小容量 Core fact range；高规模可迁移到 compact owner range
);

// 批量创建 ASC Entity（AutoChess battle init 等场景）
var entities = new NativeArray<Entity>(count, Allocator.Temp);
EntityManager.CreateEntity(ascArchetype, entities);
```

**为什么必须预建 Archetype：**
- ECB 逐个 `AddComponent` → N-1 个冗余中间 Archetype 永久存在
- AutoChess 50 棋子逐个添加 5 个 component → 4 个冗余 Archetype，每次 Query 创建/更新都要遍历
- 预建 Archetype → 零中间 Archetype，所有 ASC Entity 共享同一 Archetype

**验收指标**：Debugger 报告 `ascArchetypeCount = 1`（所有 ASC Entity 属于同一 Archetype），`intermediateArchetypeCount = 0`。

---

---

## Archetype 审计目标

| 指标 | 目标值 | 告警阈值 | 关联风险 |
|---|---|---|---|
| Runtime Core archetype 总数 | < 10 | > 20 | ISSUE-001/004 |
| 仅含 1 个 entity 的 archetype | 0（除 singleton） | > 3 | `performance-chunk-allocations.html`: "100K entity with unique archetypes = >1.5 GB" |
| Prefab archetype 数量 | 0（GE 定义不用 prefab） | > 10 | 每个 prefab = 16 KiB chunk |
| Tag component 数量 | 0 | > 0 | 每个 tag 翻倍 archetype 排列 |
| SharedComponent unique value 数 | < 5 | > 10 | 每个 unique value 创建新 chunk |

---

## Component 分类完整清单

### IComponentData（unmanaged 数据）

| Component | 挂载 Entity | 大小估计 | 说明 |
|---|---|---|---|
| `FrameArenaStateComponent` | FrameArenaSingleton | ~32 bytes | allocator handle + frame index |
| `FrameArenaOwnerComponent` | FrameArenaSingleton | ~4 bytes | owner 标记 |
| `GASDefinitionCatalogComponent` | DefinitionCatalogSingleton | ~24 bytes | `BlobAssetReference<GASDefinitionCatalogBlob>` + schema/content hash，只读 |
| `ASCIdentityComponent` | ASC Entity | ~8 bytes | PlayerId, TeamId |
| `CombatAttributeCurrentSetComponent` / `ResourceAttributeCurrentSetComponent` | ASC Entity | 按 set 固定 | 高频 Current 值，按真实热路径聚合 |
| `CombatAttributeBaseSetComponent` / `ResourceAttributeBaseSetComponent` | ASC Entity | 按 set 固定 | 低频 Base/Min/Max/Clamp/Config 值 |
| `AttributeDirtyMaskComponent` | ASC Entity | 8-24 bytes | 本帧 AttributeCode 变化 bitset，供 fact/projection 精确过滤 |
| `TagMaskComponent` | ASC Entity | ~24 bytes | 三层 uint64 bitmask（192 tags）+ 层级查询 |
| `TagStatusFlagsComponent`（可选，同 archetype） | ASC Entity | ~4-8 bytes | 高频 status 分支 cache，从 `TagMaskComponent` 派生 |
| `ASCActiveEffectsComponent` | ASC Entity | ~4 bytes | store version marker |
| `AbilityActivationRequestComponent` | Request Entity | ~40 bytes | source ASC, ability entity, explicit target, input sequence, request frame, target mode |
| `AbilityCommandComponent` | Request Entity | ~56 bytes | normalized activation command, primary GE, level, target params, status |
| `GEEffectOwnerComponent` | Active Effect Query Entity (可选) | ~8 bytes | owning ASC ref |
| `GEStreamOwnerComponent`（非目标态 proof） | `GEStreamOwnerSingleton`（proof-only） | ~16 bytes | version, sequence；scale-ready 不作为默认 owner |

### IBufferElementData

| Buffer Element | 挂载 Entity | InternalBufferCapacity | 每元素大小 |
|---|---|---|---|
| `GEEffectCommandBuffer` | ASC Entity compact range / `GEStreamOwnerSingleton`（非目标态 proof） | 4-16（目标）/ 256（proof-only） | ~32 bytes |
| `GESetByCallerValueBuffer` | command range owner / `GEStreamOwnerSingleton`（非目标态 proof） | 8-32（目标）/ 256（proof-only） | ~16 bytes |
| `GEEffectSpecBuffer` | frame scratch / `GEStreamOwnerSingleton`（非目标态 proof） | 目标态优先 NativeContainer / 256（proof-only） | ~48 bytes |
| `AttributeModifierBuffer` | target grouped range / `GEStreamOwnerSingleton`（非目标态 proof） | 8-32（目标）/ 512（proof-only） | ~24 bytes |
| `ActiveEffectMutationBuffer` | ASC Entity / frame scratch / `GEStreamOwnerSingleton`（非目标态 proof） | 4-16（目标）/ 128（proof-only） | ~32 bytes |
| `GameplayEventBuffer` | per-owner fact/outbox / `GEStreamOwnerSingleton`（非目标态 proof） | 8-32（目标）/ 256（proof-only） | ~32 bytes |
| `AbilitySlotBuffer` | ASC Entity | 8 | ~16 bytes |
| `PresentationEventBuffer` | ASC Entity | 4 | ~32 bytes |
| `ActiveGameplayEffectBuffer` | ASC Entity | 8 | ~64 bytes |
| `TargetDataBuffer` | Request Entity | 16 | ~8 bytes |

> **注：** `TargetDataBuffer` 承载一次 Ability 激活的目标解析结果，挂在 request/command entity 上。每个 target 对应一个 entry。若 target 数量超过 `InternalBufferCapacity`，spill 监控同 `ActiveGameplayEffectBuffer` 策略；不得把它挂在 Ability Entity 上作为跨帧状态。

### IEnableableComponent

| Component | 挂载 Entity | Toggle 频率 | Toggle Phase |
|---|---|---|---|
| `AbilityExecutableTag`（可选） | Ability Entity | 仅当 profiler 证明大量不可执行 ability 需要 query skip 时启用 | `GASCoreSimulationSystemGroup` State lane |
| `PeriodDueTag`（可选） | ASC Entity | 仅当 profiler 证明大量 idle slot 需要 enableable skip 时启用 | `GASCoreSimulationSystemGroup` State lane |

> **注：** Per-slot active/inhibited 标记通过 `ActiveGameplayEffectBuffer.Flags` 的 bit 表示，不使用独立的 enableable component。这样可以避免每个 slot 一个 component type 的 archetype 爆炸。Chunk 级跳过通过 `ChunkComponent` 实现。目标态禁止为单个 effect slot 定义独立 enableable component。

### ChunkComponent

| Component | 挂载 Chunk（entity 所在 chunk） | 用途 |
|---|---|---|
| `AllIdleChunkComponent` | 所有 ASC entity 都是 idle 的 chunk | chunk 级跳过 |
| `NoActiveEffectsChunkComponent` | 无任何 ASC entity 有 active effect 的 chunk | chunk 级跳过 |

### 明确禁止的 Component 类型

| 禁止项 | 原因 | 对应规范 |
|---|---|---|
| 任何纯标记 tag component（无数据字段的 IComponentData） | 每个 tag 翻倍 archetype 排列数 | `PRF-03` |
| 任何 managed IComponentData（含托管引用） | 不能 Burst，hot path 退化 | `SYS-01` |
| 用 `ISharedComponent` 做 ASC 分组 | 值改变 = entity 迁移 chunk；除非满足三条件 | `PRF-12` |
| 用 `ICleanupComponent` 做普通状态存储 | Cleanup 有特殊生命周期；误用导致内存泄漏 | `PRF-12` |
| NativeContainer（如 `NativeArray`/`NativeHashMap`）放在 `IComponentData` 上并对其所在 entity 调度 `IJobChunk`/`IJobEntity` | 安全系统无法追踪 component 内嵌容器的读写依赖，导致竞态或 job 依赖缺失 | `PRF-34` |
| 依赖 ECS Child Buffer 的 sibling index 做确定性排序 | Child buffer 迭代顺序在 Entities 1.4.6 中不保证确定，跨帧/跨平台可能变化 | `PRF-31` |

---

