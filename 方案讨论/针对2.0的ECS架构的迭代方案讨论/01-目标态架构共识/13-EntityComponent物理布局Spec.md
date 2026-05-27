# Entity/Component 物理布局 Spec

## 目的

定义 Runtime Core 目标态的 Entity 和 Component 物理布局，确保：
1. Archetype 数量小且稳定（不随 entity/event 数量暴涨）
2. Component 的 type 选型正确（Data vs Buffer vs Enableable vs Chunk vs Tag）
3. Buffer 容量策略明确（InternalBufferCapacity、spill 监控）
4. 遵守 `UnityDOTS官方文档参考/主题/13-DOTS编写规范与性能陷阱.md` 的 `PRF-*` 规范

**物理布局是架构的"硬件层"—— 概念流再正确，Entity/Component 布局错误也会导致 archetype 爆炸和 chunk 碎片化。**

## Entity 清单与 Component 布局

### Entity 1: FrameArenaSingleton（帧基础设施）

| 属性 | 值 |
|---|---|
| 数量 | **1（全局唯一）** |
| 生命周期 | World 级别，world init 时创建，world dispose 时销毁 |
| 创建方式 | `ICustomBootstrap` 或 `GasFramePrepareSystem.OnCreate` |

**Component 布局：**

| Component | Type | 用途 |
|---|---|---|
| `FrameArenaStateComponent` | `IComponentData` | RewindableAllocator handle、current frame index、previous frame timing |
| `FrameArenaOwnerComponent` | `IComponentData` | 标记此 entity 为 frame arena owner（用于 query target） |

**不含 Buffer。** Arena allocator 是 NativeContainer（在 `FrameArenaStateComponent` 中 by ref），非 DynamicBuffer。

> **`PRF-34` 警告**：`FrameArenaStateComponent` 包含 `AllocatorManager.AllocatorHandle`（NativeContainer 相关类型）。**禁止**对该 entity 调度 `IJobChunk`/`IJobEntity`（安全系统无法追踪 component 内嵌容器的读写依赖）。正确做法：
> 1. 主线程通过 `SystemAPI.GetSingletonRW<FrameArenaStateComponent>()` 读取 allocator handle
> 2. 将 handle 作为 Job 参数传入，不让 Job 通过 `ComponentLookup` 访问 `FrameArenaStateComponent`
> 3. `AllocatorHandle` 本身是轻量 struct（index + version），作为 Job 参数传递无性能问题

---

### Entity 2: EffectCommandStreamOwner（帧命令流）

| 属性 | 值 |
|---|---|
| 数量 | **1（全局唯一）** |
| 生命周期 | World 级别 |
| 状态 | **AM2-AM3 proof-only**；scale-ready 需评估 per-owner 或 NativeStream |

**Component 布局：**

| Component | Type | InternalBufferCapacity | 每帧操作 | 说明 |
|---|---|---|---|---|
| `GEStreamOwnerComponent` | `IComponentData` | — | 更新 version/sequence | stream 元数据 |
| `GEEffectCommandBuffer` | `IBufferElementData` | 256 | **clear → write → read → clear** | 本帧命令 |
| `GESetByCallerValueBuffer` | `IBufferElementData` | 256 | clear → write → read → clear | SetByCaller 附属数据 |
| `GEEffectSpecBuffer` | `IBufferElementData` | 256 | clear → write → read → clear | 即时 GE spec |
| `AttributeModifierBuffer` | `IBufferElementData` | 512 | clear → write → read → clear | 属性变更 |
| `ActiveEffectMutationBuffer` | `IBufferElementData` | 128 | clear → write → read → clear | 激活效果变更 |
| `GameplayEventBuffer` | `IBufferElementData` | 256 | clear → write → read → clear | 类型化事实 |

**关键约束：**
- 全部 Buffer 为 frame-local，帧末必须清空
- 全局 buffer 的并行 write 是瓶颈 —— **AM3 后必须评估 scale 上限**
- `InternalBufferCapacity` 设定基于 x50 AutoChess profile 的峰值观测，后续按实际数据调整

**Scale 路径：**
```
AM2-AM3: 全局 stream owner（当前 proof-only）
     ↓ 当 global buffer pressure 超过阈值 / 需要 ScheduleParallel
AM4+: 切换到 per-owner (ASC) DynamicBuffer
     - GEEffectCommandBuffer → 挂 ASC Entity（per-owner，可并行）
     - GEEffectSpecBuffer → 挂 ASC Entity（per-owner，可并行）
     - AttributeModifierBuffer → 挂 ASC Entity（per-owner，消除 O(N²) 扫描，PRF-06）
     - GameplayEventBuffer → 挂 ASC Entity（per-owner，消除 O(N²) 扫描，PRF-06）
     - GESetByCallerValueBuffer → 挂 ASC Entity（同 owner，SetByCaller range 生命周期一致）
     - 保留 NativeStream fan-in 作为高规模备选（CASE-12 NAT-03）
```

**AM4 per-owner 的并行收益：**
- 全局 singleton → 必须串行写 → `ScheduleParallel` 收益为零
- Per-owner → 每个 ASC 独立写自己的 buffer → 真正的 `ScheduleParallel`，O(N_asc) 并行

---

### Entity 3: ASC Entity（核心权威状态）

| 属性 | 值 |
|---|---|
| 数量 | **N（每个角色/单位一个）** |
| 目标 N（x1） | ~10-50 |
| 目标 N（x50） | ~300-500 |
| 目标 N（压力测试） | ~10,000-100,000 |
| 生命周期 | 角色创建到销毁 |
| 创建方式 | Structural Playback ECB（低频） |

**Component 布局：**

| Component | Type | Per-Entity Size | Enableable? | 用途 |
|---|---|---|---|---|
| `ASCIdentityComponent` | `IComponentData` | ~8 bytes | 否 | ASC 身份标识（PlayerId, TeamId） |
| `AttributeComponent` (属性统称，每个属性一个独立 IComponentData) | `IComponentData` | ~16 bytes × N_attrs | 否 | **每种属性一个独立的 component type**（如 `HealthAttribute`, `ManaAttribute`）。属性 component 统一使用 `[属性名]Attribute` 命名（`IComponentData`），`Buffer` 后缀严格保留给 `IBufferElementData` |
| `TagMaskComponent` | `IComponentData` | ~24 bytes | 否 | 当前 granted tag 的三层 bitmask（192 tags），支持层级查询 |
| `ASCActiveEffectsComponent` | `IComponentData` | ~4 bytes | 否 | active effect store 版本标记 |
| `AbilitySlotBuffer` | `IBufferElementData` | ~16 bytes/slot | — | ASC → Ability Entity 反向查找，O(1) 定位，避免全量扫描 |
| `PresentationEventBuffer` | `IBufferElementData` | ~32 bytes/event | — | per-ASC 表现事件 outbox，UI 直接读取对应 ASC 的 buffer，无需全量扫描 |
| `ActiveGameplayEffectBuffer` | `IBufferElementData` | ~64 bytes/slot | — | **核心跨帧存储**；每个 slot 记录一个 active effect 的状态 |

**`ActiveGameplayEffectBuffer` 结构：**

```csharp
[InternalBufferCapacity(8)]  // 8 slots × 64 bytes = 512 bytes inline
public struct ActiveGameplayEffectBuffer : IBufferElementData
{
    public int EffectCode;           // 4 bytes — definition reference
    public int StackCount;           // 4 bytes
    public float RemainingDuration;  // 4 bytes
    public float PeriodAccumulator;  // 4 bytes
    public int SourceEffectCode;     // 4 bytes
    public int ContextId;            // 4 bytes
    public Entity SourceAsc;         // 8 bytes — Entity 是 8 bytes
    public Entity TargetAsc;         // 8 bytes
    public byte Flags;               // 1 byte — Active/Inhibited/PendingRemove/LegacyBacked
    // Total: ~41 bytes + padding ≈ 48-64 bytes
}
```

**属性为何拆成多个 Component Type：**

`HealthAttribute`、`ManaAttribute` 各自一个 `IComponentData` 而非一个属性数组：
- **Query 精确性**：`WithAll<HealthAttribute>` 只匹配有 health 的 entity，不需要遍历所有 entity 检查 attribute code
- **Cache 优化**：Attribute Delta Apply 只读/写变化的属性 component，无关属性不受影响
- **Job 依赖精确性**：写 `HealthAttribute` 的 job 不会 block 读 `ManaAttribute` 的 job

**关键约束 —— 属性集一致性（`PRF-03` `PRF-07`）：**

Per-Attribute IComponentData 设计有效的前提是：**同一游戏模式内所有 ASC Entity 必须具有完全相同的属性 component 集合**。若不同角色类型有不同的属性集合（如战士有 `ArmorAttribute` 而法师没有），则每种角色类型产生一个独立 Archetype，与 Archetype < 10 目标矛盾。

```
正确（AutoChess 场景）:
  所有棋子 ASC: HealthAttribute + ManaAttribute + AttackAttribute
  → 1 个 ASC Archetype（属性层面）

错误（不同角色不同属性集）:
  战士 ASC: HealthAttribute + StrengthAttribute + ArmorAttribute
  法师 ASC: HealthAttribute + ManaAttribute + SpellPowerAttribute
  → 2 个 ASC Archetype（仅属性差异），随角色类型线性增长
```

当确实需要不同角色有不同属性时，评估两种替代方案：
1. **统一属性集 + 默认值**：所有角色包含全部属性 component，不适用者设为 0（增加内存但 Archetype 不变）
2. **FixedList 属性集**：用 `FixedList128Bytes<float2>` 替代 Per-Attribute IComponentData，所有 ASC 共享同一 Archetype，属性数量由 BlobAsset 定义（牺牲 Query 精确性换取 Archetype 统一）

当前目标态默认选择方案 1（统一属性集），方案 2 作为规模备选。此约束必须在 Archetype 审计中验证。

**Tag 为何用 Bitmask 而非 Tag Component：**

> `PRF-03`: 每个 tag component 使 archetype 排列数翻倍

```
反模式: BGrantedTag_Stun, BGrantedTag_Slow, BGrantedTag_Bleed, ...
        → 10 个 tag component → 最多 2^10 = 1024 种 archetype

正确:   TagMaskComponent (3 × uint64 bitmask = 192 tags) → 1 个 component
        → 1 个 component type → 不增加 archetype 排列数
```

**TagMaskComponent 结构：**

```csharp
public struct TagMaskComponent : IComponentData
{
    public ulong Mask0; // tag id 0-63
    public ulong Mask1; // tag id 64-127
    public ulong Mask2; // tag id 128-191

    // 层级查询：检查 tag 或其任意祖先
    // ancestorMask 由 TagHierarchyDefinition BlobAsset 预计算
    public bool HasTagOrAncestor(TagMaskComponent ancestorMask) => ...;

    // 批量检查：是否满足全部 RequiredTags
    public bool HasAllTags(TagMaskComponent required) => ...;

    // 批量检查：是否包含任意 BlockedTags
    public bool HasAnyTag(TagMaskComponent blocked) => ...;
}
```

**Tag 层级定义（`TagHierarchyDefinition` BlobAsset）：**
- 每个 tag 的祖先 mask 预计算（查询时直接 AND，零递归）
- 由 Luban/SourceGenerator 生成，不可变，Burst 可消费
- 例：`Status.Debuff.Stun` 的 ancestorMask 包含 `Status` + `Status.Debuff` + `Status.Debuff.Stun`

---

### Entity 3b: Ability Entity（能力运行时实例）

| 属性 | 值 |
|---|---|
| 数量 | **P（每个 ASC 的每个 granted ability 一个）** |
| 目标 P（x1） | ~10-50 |
| 目标 P（x50） | ~300-500 |
| 生命周期 | Ability grant → revoke |
| 创建方式 | Structural Playback ECB（低频） |

**Component 布局：**

| Component | Type | 用途 |
|---|---|---|
| `AbilityStateComponent` | `IComponentData` | ability code, current state (Ready/Active/Cooldown/Ending), activation frame |
| `AbilityActiveTag` | `IEnableableComponent` | 标记 ability 当前是否可用（grant/revoke 时 toggle） |
| `AbilityActivatingTag` | `IEnableableComponent` | 标记 ability 正在尝试激活中（防止同帧重复激活） |
| `TargetDataBuffer` | `IBufferElementData` | 目标解析结果列表（每个 target entity 一个 entry） |

**与 ASC Entity 的关系：**
- Ability Entity 通过 `AbilityStateComponent.SourceAsc` 指向 owning ASC
- **ASC → Ability 反向查找**：通过 ASC Entity 上的 `AbilitySlotBuffer` 直接定位所有 Ability Entity，O(N_slots_per_asc) 而非 O(N_abilities_global) 全量扫描
- Ability Entity 可独立 query，不增加 ASC Entity 的 archetype 复杂度
- `AbilityActiveTag` 被 revoke 后 entity 进入 Structural Playback 销毁队列，同时从 `AbilitySlotBuffer` 移除对应 slot

**`AbilitySlotBuffer` 结构（挂在 ASC Entity 上）：**

```csharp
[InternalBufferCapacity(8)]  // 大多数角色 granted ability < 8 个
public struct AbilitySlotBuffer : IBufferElementData
{
    public Entity AbilityEntity;  // 指向 Ability Entity
    public int    AbilityCode;    // 冗余存储，避免 Lookup（热路径优化）
    public byte   SlotIndex;      // 技能槽位（0-7），用于 UI 绑定
}
```

**关键约束：**
- Ability Entity 是 ASC Entity 的独立子 entity，不是 ASC 上的 component
- Ability 的激活/冷却/结束等高频状态切换通过 enableable toggle + enum state，不触发结构变化
- 目标解析结果 `TargetDataBuffer` 是 frame-local buffer，消费者（EffectCommand 生成）读取后清空
- Ability grant 时向 `AbilitySlotBuffer` 添加 slot；revoke 时移除 slot 并销毁 Ability Entity（通过 Structural Playback ECB）

---

### Entity 4: Request Entity（低频边界命令）

| 属性 | 值 |
|---|---|
| 数量 | **M（每帧创建的临时 entity）** |
| 目标 M（x1） | ~1-10 |
| 目标 M（x50） | ~10-100 |
| 生命周期 | 本帧：CommandIngest 消费 → StructuralPlayback 销毁 |
| 创建方式 | Boundary CommandGateway ECB |
| 销毁方式 | Structural Playback ECB |

**Component 布局（每 request entity 只有其中一种）：**

| Request 类型 | Component | 说明 |
|---|---|---|
| Ability 激活 | `AbilityCommandRequest` + source/target/ability code | 玩家/AI 发起的能力使用意图 |
| Effect 施加（legacy） | `CApplyGameplayEffectRequest` | **迁移期残留，目标态逐步缩减** |

**关键约束：**
- Request entity 数量不随 hit/modifier 数量线性增长
- Request entity 不承载高频 instant GE —— instant GE 走 EffectCommand buffer
- Boundary 一次玩家输入 → 最多 1 个 request entity（不是每个 target 一个）

---

### Entity 5: Active Effect Entity（可选，GlobalIndexedStore）

| 属性 | 值 |
|---|---|
| 数量 | **K（仅当采用 stable entity 方案时）** |
| 生命周期 | GE apply → GE expire/remove |
| 创建/销毁 | Structural Playback ECB |
| 状态 | **待评估 —— 不推荐作为默认方案，仅在需要跨 ASC 全局 query 时使用** |

**如果使用，Component 布局：**

| Component | Type | Enableable? | 用途 |
|---|---|---|---|
| `GEActiveEffectStateComponent` | `IComponentData` | 否 | remaining time, stack count, flags |
| `GEEffectOwnerComponent` | `IComponentData` | 否 | 指向 owning ASC entity |
| `GEActiveEffectTag` | `IEnableableComponent` | **是** | active/inhibited 标记 |
| `PeriodDueTag` | `IEnableableComponent` | **是** | period tick 到期 |

**注意：** 此方案增加 entity 数量和 archetype。仅在 `ActiveGameplayEffectBuffer` slot 数量不足（如超过 32 slot/ASC）或需要跨 ASC query 时考虑。

---

## ASC Entity 批量创建策略（`P1-14`）

> **`P1-14`**：禁止逐 Component 构建 Entity Archetype。使用 `EntityManager.CreateEntity()` 后逐次 `AddComponent<T>()` 会在每次调用时创建中间 archetype，这些中间 archetype 在应用剩余生命周期内持续存在并增加所有 `EntityQuery` 的计算开销。

**正确做法 —— 预建 Archetype 批量创建：**

```csharp
// 在 GASFrameArenaSetupSystem.OnCreate 或 ICustomBootstrap 中一次性创建
var ascArchetype = EntityManager.CreateArchetype(
    typeof(ASCIdentityComponent),
    typeof(HealthAttribute),        // 属性 component（按属性集一致性约束，全角色相同）
    typeof(ManaAttribute),
    typeof(AttackAttribute),
    typeof(TagMaskComponent),
    typeof(ASCActiveEffectsComponent),
    typeof(AbilitySlotBuffer),
    typeof(ActiveGameplayEffectBuffer),
    typeof(GEEffectCommandBuffer),
    typeof(GESetByCallerValueBuffer),
    typeof(GEEffectSpecBuffer),
    typeof(AttributeModifierBuffer),
    typeof(GameplayEventBuffer),
    typeof(PresentationEventBuffer),
    typeof(AbilityActiveTag),       // IEnableableComponent
    typeof(PeriodDueTag)            // IEnableableComponent
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

| 指标 | 目标值 | 告警阈值 | 当前 ISSUE |
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
| `GEStreamOwnerComponent` | EffectCommandStreamOwner | ~16 bytes | version, sequence |
| `ASCIdentityComponent` | ASC Entity | ~8 bytes | PlayerId, TeamId |
| `AttributeComponent` (每种属性一个 type) | ASC Entity | ~16 bytes/type | CurrentValue, BaseValue, Bonus |
| `TagMaskComponent` | ASC Entity | ~24 bytes | 三层 uint64 bitmask（192 tags）+ 层级查询 |
| `ASCActiveEffectsComponent` | ASC Entity | ~4 bytes | store version marker |
| `AbilityCommandRequest` | Request Entity | ~32 bytes | ability code, source, target |
| `TargetAcquisitionComponent` | Ability Entity / ASC Entity | ~16 bytes | target selection mode, filter params |
| `GEEffectOwnerComponent` | Active Effect Entity (可选) | ~8 bytes | owning ASC ref |

### IBufferElementData

| Buffer Element | 挂载 Entity | InternalBufferCapacity | 每元素大小 |
|---|---|---|---|
| `GEEffectCommandBuffer` | EffectCommandStreamOwner | 256 | ~32 bytes |
| `GESetByCallerValueBuffer` | EffectCommandStreamOwner | 256 | ~16 bytes |
| `GEEffectSpecBuffer` | EffectCommandStreamOwner | 256 | ~48 bytes |
| `AttributeModifierBuffer` | EffectCommandStreamOwner | 512 | ~24 bytes |
| `ActiveEffectMutationBuffer` | EffectCommandStreamOwner | 128 | ~32 bytes |
| `GameplayEventBuffer` | EffectCommandStreamOwner | 256 | ~32 bytes |
| `AbilitySlotBuffer` | ASC Entity | 8 | ~16 bytes |
| `PresentationEventBuffer` | ASC Entity | 4 | ~32 bytes |
| `ActiveGameplayEffectBuffer` | ASC Entity | 8 | ~64 bytes |
| `TargetDataBuffer` | Ability Entity / ASC Entity | 16 | ~8 bytes |

> **注：** `TargetDataBuffer` 承载 Ability 目标解析后的 target entity 列表。每个 target 对应一个 entry。若 target 数量超过 `InternalBufferCapacity`，spill 监控同 `ActiveGameplayEffectBuffer` 策略。

### IEnableableComponent

| Component | 挂载 Entity | Toggle 频率 | Toggle Phase |
|---|---|---|---|
| `AbilityActiveTag` | ASC Entity | 低频（grant/revoke） | StructuralPlayback |
| `PeriodDueTag` | ASC Entity | 每帧（period tick 到期时置位） | ActiveLifecycle |

> **注：** Per-slot active/inhibited 标记通过 `ActiveGameplayEffectBuffer.Flags` 的 bit 表示，不使用独立的 enableable component。这样可以避免每个 slot 一个 component type 的 archetype 爆炸。Chunk 级跳过通过 `ChunkComponent` 实现。原 `CEffectSlotActive` 已废弃移除。

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

## Buffer 容量策略汇总

| Buffer | InternalBufferCapacity | 内联字节 | 逻辑上限 | Spill 告警 | 说明 |
|---|---|---|---|---|---|
| `GEEffectCommandBuffer` | 256 | ~8 KB | 1024 | > 50% | frame-local |
| `GESetByCallerValueBuffer` | 256 | ~4 KB | 512 | > 50% | frame-local |
| `GEEffectSpecBuffer` | 256 | ~12 KB | 1024 | > 50% | frame-local |
| `AttributeModifierBuffer` | 512 | ~12 KB | 2048 | > 50% | frame-local |
| `ActiveEffectMutationBuffer` | 128 | ~4 KB | 512 | > 50% | frame-local |
| `GameplayEventBuffer` | 256 | ~8 KB | 1024 | > 50% | frame-local |
| `AbilitySlotBuffer` | 8 | ~128 bytes | 32 slots | > 50% | **跨帧存储**，ASC→Ability 反向查找 |
| `PresentationEventBuffer` | 4 | ~128 bytes | 16 events | > 50% | per-ASC outbox，UI 直接读取 |
| `ActiveGameplayEffectBuffer` | 8 | ~512 bytes | 64 slots | spill（溢出）或 > 32 slots | **跨帧存储** |
| `TargetDataBuffer` | 16 | ~128 bytes | 32 targets | > 50% | frame-local，Ability 目标解析结果 |

**`ActiveGameplayEffectBuffer` 超限策略：**
- < 8 slot → chunk inline，fast
- 8-32 slot → externalized，每访问多一次间接跳转
- > 32 slot → 考虑 GlobalIndexedStore（stable entity）方案
- > 64 slot → **架构告警**，说明 active effect 数量不合理，需要更早的 expire/cleanup

---

## 物理布局与 Phase 的对应关系

| Entity | 创建 Phase | 写入 Phase | 读取 Phase | 销毁 Phase |
|---|---|---|---|---|
| FrameArenaSingleton | World init | FramePrepare | 所有 phase | World dispose |
| EffectCommandStreamOwner | World init | CommandIngest, SpecEval, DeltaApply, ActiveLifecycle, TypedFact | SpecEval, DeltaApply, TypedFact, StructuralPlayback, Observation | World dispose |
| ASC Entity | StructuralPlayback | ActiveLifecycle, DeltaApply, StructuralPlayback | 所有 phase | StructuralPlayback |
| Ability Entity | StructuralPlayback（grant） | CommandIngest, StructuralPlayback | CommandIngest, SpecEval, TypedFact | StructuralPlayback（revoke） |
| Request Entity | Boundary ECB (Application Shell) | CommandIngest（消费） | CommandIngest | StructuralPlayback |
| Active Effect Entity（可选） | StructuralPlayback | ActiveLifecycle | ActiveLifecycle, DeltaApply | StructuralPlayback |

---

## 不变量

1. 物理 archetype 数量目标 < 10，告警 > 20。
2. 所有 Component 必须明确类型（Data/Buffer/Enableable/Chunk），不能以"ECS component"笼统称呼。
3. Tag 状态通过 bitmask（`TagMaskComponent`）表达，不使用独立 tag component。
4. 每个 DynamicBuffer 必须有 `InternalBufferCapacity` 声明和 spill 监控。
5. Per-ASC 数据挂 ASC entity，frame-local 数据挂 stream owner（proof 阶段）→ per-owner（scale 阶段）。
6. 不使用 Prefab 承载 GE/Ability 定义（用 BlobAsset + static table）。
7. 不使用 `ISharedComponent` 除非通过三条件检查。
8. 不使用 `ICleanupComponent` 做普通状态——只用于 destroy 后清理（`PRF-12`）。
9. NativeContainer（`NativeArray`/`NativeHashMap` 等）放在 `IComponentData` 上时，**禁止**对该 component 所在 entity 调度 `IJobChunk`/`IJobEntity`（`PRF-34`）。安全系统无法追踪 component 内嵌容器的读写依赖，正确做法是主线程提取容器后单独调度 job。
10. **禁止**依赖 ECS Child Buffer 的 sibling index 做确定性排序（`PRF-31`）。Buffer 遍历顺序不保证帧间/平台间一致。若 GAS 需要确定性 multi-effect 执行顺序，必须自建排序键（如 command sequence、sortKey 等）。

## 验收

1. Debugger 输出 archetype 总数、单 entity archetype 列表、prefab archetype 列表。
2. Debugger 输出 buffer length/capacity/spill/externalized 全部指标。
3. x50 AutoChess profile 中 archetype 数量不随单位数线性增长。
4. 代码审查可验证：无 tag component、无 managed component 在 hot path、无 prefab 用于 GE 定义。
5. 每个 Component type 能说清它是 Data/Buffer/Enableable/Chunk 中的哪一类、为什么。
