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

### Entity 2: DefinitionCatalogSingleton（只读静态定义目录）

| 属性 | 值 |
|---|---|
| 数量 | **1（每个 World / battle config set 一个）** |
| 生命周期 | SubScene / bootstrap 载入后创建；World dispose 或 scene unload 时释放 |
| 创建方式 | generated stateless Baker / bootstrap；Runtime Core 不解析 Luban row |

**Component 布局：**

| Component | Type | 用途 |
|---|---|---|
| `GASDefinitionCatalogComponent` | `IComponentData` | 持有 `BlobAssetReference<GASDefinitionCatalogBlob>`、schema/content hash；只读配置入口 |

**关键约束：**
- `GASDefinitionCatalogComponent` 只保存不可变 `BlobAssetReference` 和 hash，不保存 `NativeArray`、`NativeHashMap`、managed row、JSON reader 或 runtime state。
- PackageCache `components-singleton.md` / `systems-systemapi.md` 明确 singleton API 不自动完成依赖。Definition Catalog 的安全前提是 bootstrap 后无 runtime writer；各 Runtime system 使用 `RequireForUpdate<GASDefinitionCatalogComponent>()`，在 `OnUpdate` 只读 `GetSingleton` 后把 BlobRef 传给 job。
- Ability grant 可把 `AbilityCode` 解析成 `AbilityDefinitionIndex` 缓存在 Ability Entity；Ability activation / Fan-In / Magnitude Resolve 通过 index + `ref readonly` 访问 `GASDefinitionCatalogBlob`，不在热路径扫描 per-definition entity。
- `GASGeneratedDefinitionBlobComponent<T>` 只作为 baking output / bootstrap 收集入口；不得作为每帧 Runtime lookup query。
- `GASGeneratedRuntimeDefinitionResolver`、`GASGeneratedRequirementEvaluator`、`GASGeneratedMagnitudeEvaluator`、`GASGeneratedTargetRuleTable` 不对应任何 Entity / Component / Singleton。它们是 generated static pure functions，作为 job 可调用代码存在；BlobRef、snapshot、writer/list 由调用 System 作为 job field 传入。

**Generated Runtime Glue 物理归属：**

| 产物 | 是否 Entity | 是否 Component | 物理归属 | 生命周期 |
|---|---:|---:|---|---|
| `GASGeneratedRuntimeDefinitionResolver` | 否 | 否 | generated Runtime source static class | 随程序集加载；无 runtime state |
| `GASGeneratedRequirementEvaluator` | 否 | 否 | generated Runtime source static class | 随程序集加载；无 runtime state |
| `GASGeneratedMagnitudeEvaluator` | 否 | 否 | generated Runtime source static class / 可选 FunctionPointer 批处理表 | 随程序集加载；无 runtime state |
| `GASGeneratedTargetRuleTable` | 否 | 否 | generated Runtime source static class + Blob range / small static switch | 随程序集加载；无 runtime state |
| `AbilityActivationPlanRecord` | 否 | 否 | `NativeStream` / `NativeList` / stack local | frame-local，由 owner System dispose/rewind |
| `GECommandSeedRecord` | 否 | 否 | `NativeStream` / deterministic merge list | frame-local，由 `GASEffectFanInSystem` owner 管理 |
| `ResolvedModifierRecord` | 否 | 否 | target-grouped `NativeList` / small owner-local range | frame-local，由 Attribute lane owner 管理 |

这张表的目的不是增加抽象层，而是防止把生成胶水误建成 `DefinitionResolverSingleton`、`RuntimeConfigManagerComponent` 或 per-definition entity。PackageCache `components-nativecontainers.md` 还要求：若某个 component 内含 NativeContainer，不能对该 component 调度 `IJobChunk` / `IJobEntity`；因此 frame scratch 的 container owner 必须是 System 或主线程提取后的 job field，而不是胶水 component。

---

### Frame Fan-In Scratch（帧内扇入数据，非持久 Entity）

| 属性 | 值 |
|---|---|
| 数量 | 非 Entity；由 owner `ISystem` 每帧创建 NativeContainer |
| 生命周期 | frame-local，随 `World.UpdateAllocator` / `Allocator.TempJob` rewind 或 dispose |
| 状态 | **目标态默认**；AM2-AM3 的 `GEStreamOwnerSingleton` 只允许作为 proof-only 兼容落点 |

**目标物理承载：**

| 承载 | Type | Owner | 每帧操作 | 说明 |
|---|---|---|---|---|
| `NativeStream` command record | `NativeStream` | `GASEffectFanInSystem` | create → parallel write `GEEffectCommandRecord` → deterministic merge → dispose/rewind | 多 producer command fan-in |
| `NativeList<GEEffectCommandRecord>` / sort buffer | NativeContainer | merge system | fill → sort by `(TargetSortKey, Sequence)` → consume | 确定性排序和分组 |
| Compact owner-local command range | small `DynamicBuffer` 或 buffer range | ASC owner | write merged range → consume → clear | 只保存合并后的局部范围 |
| Target-grouped modifier range | `NativeStream` / `NativeList` / small buffer | `GASAttributeReduceApplySystem` | reduce → apply → clear | Attribute apply 不默认 random lookup 写 |
| Core fact range | `NativeStream` / per-owner fact buffer | `GASGameplayFactSystem` | write → reaction consume / boundary projection | Core reaction 与 Boundary observation 分流 |

**关键约束：**
- Frame fan-in scratch 不以 singleton entity 的大 `DynamicBuffer` 作为目标态；`GEStreamOwnerComponent`、`GEEffectCommandBuffer(256)`、`AttributeModifierBuffer(512)` 等只保留迁移期 proof 解释。
- 所有 NativeContainer 必须声明 allocator owner、dispose/rewind 位置、merge 顺序和 Debugger counters（`NAT-01` `NAT-03` `NAT-05`）。
- 写回 ASC 的 owner-local buffer 必须小容量、compact、可清空；禁止把 proof 阶段的大容量全局 buffer 原样复制到每个 ASC。

**Scale 路径（更新）：**
```
AM2-AM3: 全局 stream owner（当前 proof-only）
     ↓ 当 global buffer pressure 超过阈值 / 需要 ScheduleParallel
AM4+: `NativeStream` producer + deterministic merge
     - 多 producer job 写 per-thread / per-chunk stream（CASE-12）
     - merge 阶段按 TargetSortKey / Sequence 排序（MAT-05 NAT-03）
     - 只把合并后的 compact command range 写入 ASC 上的小容量 owner-local buffer
     - AttributeModifierBuffer / GameplayEventBuffer 只保留 target grouped 小容量 range
     - 禁止把 proof 阶段 256/512 大容量 buffer 原样搬到每个 ASC
```

**AM4 的并行收益：**
- 全局 singleton → 必须串行写或产生写竞争 → `ScheduleParallel` 收益被 fan-in 吃掉
- `NativeStream` → producer 真正并行，merge 成本可观测、可排序、可替换
- Compact owner-local range → 消费端按 target 局部读取，避免全局 scan；同时避免每个 ASC 携带大 inline buffer

---

### Entity 3: ASC Entity（核心权威状态）

| 属性 | 值 |
|---|---|
| 数量 | **N（每个角色/单位一个）** |
| 目标 N（x1） | ~10-50 |
| 目标 N（x50） | ~300-500 |
| 目标 N（压力测试） | ~10,000-100,000 |
| 生命周期 | 角色创建到销毁 |
| 创建方式 | Structural Commit ECB（低频） |

**Component 布局：**

| Component | Type | Per-Entity Size | Enableable? | 用途 |
|---|---|---|---|---|
| `ASCIdentityComponent` | `IComponentData` | ~8 bytes | 否 | ASC 身份标识（PlayerId, TeamId） |
| `CombatAttributeCurrentSetComponent` / `ResourceAttributeCurrentSetComponent` 等 AttributeSet family | `IComponentData` | 按 set 固定 | 否 | **默认按热路径和变更频率打包生成属性集 component**，例如 Combat set 承载 Health/Shield/Attack/Defense/MagicPower 当前值；不再默认每种属性一个 component type |
| `CombatAttributeBaseSetComponent` / `ResourceAttributeBaseSetComponent` 等 Base set | `IComponentData` | 按 set 固定 | 否 | 低频 base/min/max/config 值；与 Current set 分离，避免 Current 写入触发 base 相关 ChangeFilter |
| `AttributeDirtyMaskComponent` | `IComponentData` | 8-24 bytes | 否 | 本帧发生变化的 AttributeCode bitset，用于 GameplayFact / BoundaryProjection 精确过滤 |
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

**属性布局为何改为 AttributeSet family：**

本轮直接对照 `Library/PackageCache/com.unity.entities@e90944159b94/Documentation~/systems-data-granularity.md` 原文后，目标态不再把“每种属性一个 `IComponentData`”作为默认答案。官方原文的关键约束是双向的：细粒度 component 有利于 query 和 cache，但过度 component 粒度会增加 entity query、archetype 和内部流程开销；同时 read-only 和 read-write 数据应分离，否则读写访问会把整个 chunk 标记为 changed。

真实 GAS Runtime 的属性热路径不是 OOP 的 `UAttributeSet` 对象树，也不是“一个属性一个系统”。一次攻击/治疗/护盾/吸血通常同时需要 Health、Shield、Attack、Defense、MagicPower、TagStatus、Source/Target 上下文；把这些字段拆成几十个 component type 会带来：

- `GASAttributeReduceApplySystem` 需要大量 `ComponentTypeHandle<T>` / `ComponentLookup<T>`，或者退化为为每个属性生成一个同形 system，违反 `systems-optimizing.md` 对 system 固定开销的警告。
- `systems-data-granularity.md` 警告的“过度 component 粒度”会体现在 query/archetype/type handle 组合爆炸，而不是体现在单次字段读写本身。
- Current 与 Base 放在同一 component 会违反官方 reactive systems 段落：只要以 write access 遍历 Current，包含 Base 的整个 component type 所在 chunk 也被标记 changed。

**目标规则：**

1. 默认生成 AttributeSet family，而不是 per-attribute component：
   - `CombatAttributeCurrentSetComponent`：Health、Shield、Attack、Defense、MagicPower 等同一战斗热路径常共现字段。
   - `CombatAttributeBaseSetComponent`：Combat set 的 base/min/max/clamp 参数，低频写或只读。
   - `ResourceAttributeCurrentSetComponent`：Mana、Energy、Rage 等资源当前值。
   - `ResourceAttributeBaseSetComponent`：资源上限、回复参数、消耗倍率等低频字段。
2. 每个 set 内字段是生成代码固定布局，禁止运行时字典、托管 delegate 或 OOP 属性对象。
3. `AttributeDirtyMaskComponent` 记录本帧变化的 `AttributeCode`，`GameplayFact` / `BoundaryProjection` 先看 dirty mask，再读取 set 字段，避免因为 set 打包而把所有字段都当作业务变化。
4. 独立 per-attribute component 只作为例外：必须证明该属性长期稀有、查询隔离收益大于新增 component type / system / handle 成本，并且不会导致 ASC archetype 超预算。

**推荐物理形态：**

```csharp
public struct CombatAttributeCurrentSetComponent : IComponentData
{
    public float Health;
    public float Shield;
    public float Attack;
    public float Defense;
    public float MagicPower;
}

public struct CombatAttributeBaseSetComponent : IComponentData
{
    public float MaxHealth;
    public float MaxShield;
    public float BaseAttack;
    public float BaseDefense;
    public float BaseMagicPower;
}

public struct ResourceAttributeCurrentSetComponent : IComponentData
{
    public float Mana;
    public float Energy;
}

public struct ResourceAttributeBaseSetComponent : IComponentData
{
    public float MaxMana;
    public float MaxEnergy;
    public float ManaRegen;
}

public struct AttributeDirtyMaskComponent : IComponentData
{
    public ulong CombatWord;
    public ulong ResourceWord;
}
```

**Archetype 一致性约束：**

同一玩法模式内 ASC Entity 默认拥有相同 AttributeSet component family。角色“不使用某字段”通过定义层默认值、tag requirement 或 effect requirement 表达，不通过移除 component 表达。若某玩法确实存在大规模不同属性域，应新增 set family 并在 archetype 审计中证明 `ascArchetypeCount` 仍满足目标，而不是按角色类型随意增删属性 component。

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

**Status / Buff / Debuff 标记布局：**

`Stun`、`Silence`、`Disarm`、`Slow`、`Burn` 这类 gameplay status 不创建独立 tag component，也不为每种 status 创建独立 enableable component。默认做法是进入 `TagMaskComponent` 的 bitset；当热路径分支频繁且 status 数量超过 3 个时，可从 `TagMaskComponent` 派生一个 compact `TagStatusFlagsComponent` / bit field 作为同 archetype 的查询缓存。

约束：

1. 如果引入 `TagStatusFlagsComponent`，同一游戏模式内所有 ASC Entity 都必须拥有该 component，避免按角色或按 status 产生 archetype 分裂。
2. `TagStatusFlagsComponent` 只能是 `TagMaskComponent` 的派生 cache，不是新的 gameplay 权威；权威仍来自 granted tag / active effect store。
3. Status flag 写入 phase 属于 `GASCoreSimulationSystemGroup` 的 State lane 或 Attribute lane；Target Resolve / Effect Fan-In 只读。
4. Debugger 必须能输出 status bit distribution，并校验 `TagMaskComponent` 与派生 flags 的一致性。

---

### Entity 3b: Ability Entity（能力运行时实例）

| 属性 | 值 |
|---|---|
| 数量 | **P（每个 ASC 的每个 granted ability 一个）** |
| 目标 P（x1） | ~10-50 |
| 目标 P（x50） | ~300-500 |
| 生命周期 | Ability grant → revoke |
| 创建方式 | Structural Commit ECB（低频） |

**Component 布局：**

| Component | Type | 用途 |
|---|---|---|
| `AbilityStateComponent` | `IComponentData` | ability code, current state (Granted/Ready/Active/Cooldown/Ending), flags (Executable/Activating/Blocked), activation frame |

**与 ASC Entity 的关系：**
- Ability Entity 通过 `AbilityStateComponent.OwnerAsc` 指向 owning ASC
- **ASC → Ability 反向查找**：通过 ASC Entity 上的 `AbilitySlotBuffer` 直接定位所有 Ability Entity，O(N_slots_per_asc) 而非 O(N_abilities_global) 全量扫描
- Ability Entity 可独立 query，不增加 ASC Entity 的 archetype 复杂度
- Ability grant/revoke 是低频生命周期变化：grant 创建或更新 Ability Entity，revoke 进入 Structural Commit 销毁队列并从 `AbilitySlotBuffer` 移除对应 slot；默认不通过 enableable toggle 表达
- `AbilityExecutableTag` 可作为可选 enableable skip cache，但必须先有 profiler 证明大量不可执行 ability 被热路径 query 扫描

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
- Ability 的激活/冷却/结束等高频状态切换默认通过 `AbilityStateComponent.State/Flags` 表达，不触发结构变化；`AbilityExecutableTag` 只在 profiler 证明 query skip 收益时作为可选 enableable cache
- Ability Entity 不承载单次激活上下文；`TargetDataBuffer`、命令状态、目标 sort key 都属于 request/command entity
- Ability grant 时向 `AbilitySlotBuffer` 添加 slot；revoke 时移除 slot 并销毁 Ability Entity（通过 Structural Commit ECB）

---

### Entity 4: Request Entity（低频边界命令）

| 属性 | 值 |
|---|---|
| 数量 | **M（每帧创建的临时 entity）** |
| 目标 M（x1） | ~1-10 |
| 目标 M（x50） | ~10-100 |
| 生命周期 | 本帧：`GASCommandResolveSystemGroup` 的 Boundary lane 消费 → `GASStructuralCommitSystemGroup` 销毁 |
| 创建方式 | Boundary CommandGateway ECB |
| 销毁方式 | Structural Commit ECB |

**Component 布局（Ability 激活 request archetype）：**

| Component | Type | 说明 |
|---|---|---|
| `AbilityActivationRequestComponent` | `IComponentData` | Boundary 输入意图：source ASC、Ability Entity、显式目标、input sequence、request frame、target mode/sort seed |
| `AbilityCommandComponent` | `IComponentData` | Ingest 后的归一化命令：source、ability、primary GE、level、target params、status |
| `TargetDataBuffer` | `IBufferElementData` | Target Resolve 写入的 invocation-local 目标解析结果 |

**迁移期残留：**

| Request 类型 | Component | 说明 |
|---|---|---|
| Effect 施加（legacy） | `CApplyGameplayEffectRequest` | 目标态逐步缩减，不作为 scale-ready Runtime Core 主链 |

**关键约束：**
- Request entity 数量不随 hit/modifier 数量线性增长
- Request entity 承载一次激活的 invocation-local 数据；Ability Entity 只承载 granted ability 跨帧状态
- Request entity 只作为 Boundary / 网络 / 玩家输入这类低频外部意图的物化入口；AI autocast、passive、period、reaction 等 Core 内部高频来源必须走 frame-local `AbilityActivationCommandRecord` / `AbilityTargetRecord`
- Request entity 不承载高频 instant GE —— instant GE 走 EffectCommand buffer
- Boundary 创建 request 时必须一次性带齐 request、command、target buffer，避免 Ingest / TargetResolve 热路径 AddComponent
- Boundary 一次玩家输入 → 最多 1 个 request entity（不是每个 target 一个）

---

### Frame-local Command / Target Records（非 Entity）

| Record | 载体 | 生命周期 | 用途 |
|---|---|---|---|
| `AbilityActivationCommandRecord` | `NativeStream` → `NativeList` | 当帧 | Core 内部高频 ability 激活命令；不创建 request entity |
| `AbilityTargetRecord` | `NativeStream` → `NativeList` | 当帧 | Target Resolve 扁平化输出，每 target 一个 record |
| `GEEffectCommandRecord` | `NativeStream` → sorted `NativeList` | 当帧 | Effect Fan-In 统一 command record |

**关键约束：**
- 这些 record 不是 `IComponentData` / `IBufferElementData`，不进入 archetype，不产生结构变化。
- owner `ISystem` 负责创建、排序、传递、dispose；不得把 NativeContainer 放进 `IComponentData`。
- 当 `TargetDataBuffer` spill 或 request create/destroy 成本超过 budget 时，必须把该业务路径迁移到 record path。
- record 必须显式携带 `Sequence` / `TargetIndex` / `TargetSortKey`，禁止依赖 worker 调度顺序、chunk 顺序或 ECB append playback 顺序。

---

### Entity 5: Active Effect Query Entity（可选，GlobalIndexedStore）

| 属性 | 值 |
|---|---|
| 数量 | **K（仅当采用 stable entity 方案时）** |
| 生命周期 | GE apply → GE expire/remove |
| 创建/销毁 | Structural Commit ECB |
| 状态 | **可选索引结构 —— 不推荐作为默认方案，仅在需要跨 ASC 全局 query / period bucket 时使用** |

**如果使用，Component 布局：**

| Component | Type | Enableable? | 用途 |
|---|---|---|---|
| `GEActiveEffectStateComponent` | `IComponentData` | 否 | remaining time, stack count, enum/bit flags |
| `GEEffectOwnerComponent` | `IComponentData` | 否 | 指向 owning ASC entity |
| `GEPeriodBucketComponent`（可选） | `IComponentData` / Chunk Component | 否 | 低频重分桶或整 chunk skip 依据 |
| `GEActiveEffectCleanupComponent`（可选） | Cleanup Component | 否 | owner destroyed / effect removed 后释放 granted state |

**注意：** 此方案增加 entity 数量和 archetype。它不是“每个 active effect 默认实体化”，也不为 `Active/Inhibited/PendingRemove/PeriodDue` 等轻量状态创建独立 enableable component。仅在 `ActiveGameplayEffectBuffer` slot 数量不足（如超过 32 slot/ASC）、需要跨 ASC query、或 period bucket 能明显减少整帧扫描时考虑。

---

## ASC Entity 批量创建策略（`P1-14`）

> **`P1-14`**：禁止逐 Component 构建 Entity Archetype。使用 `EntityManager.CreateEntity()` 后逐次 `AddComponent<T>()` 会在每次调用时创建中间 archetype，这些中间 archetype 在应用剩余生命周期内持续存在并增加所有 `EntityQuery` 的计算开销。

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
| `GEStreamOwnerComponent`（迁移期） | `GEStreamOwnerSingleton`（proof-only） | ~16 bytes | version, sequence；scale-ready 不作为默认 owner |

### IBufferElementData

| Buffer Element | 挂载 Entity | InternalBufferCapacity | 每元素大小 |
|---|---|---|---|
| `GEEffectCommandBuffer` | ASC Entity compact range / `GEStreamOwnerSingleton`（迁移期） | 4-16（目标）/ 256（proof-only） | ~32 bytes |
| `GESetByCallerValueBuffer` | command range owner / `GEStreamOwnerSingleton`（迁移期） | 8-32（目标）/ 256（proof-only） | ~16 bytes |
| `GEEffectSpecBuffer` | frame scratch / `GEStreamOwnerSingleton`（迁移期） | 目标态优先 NativeContainer / 256（proof-only） | ~48 bytes |
| `AttributeModifierBuffer` | target grouped range / `GEStreamOwnerSingleton`（迁移期） | 8-32（目标）/ 512（proof-only） | ~24 bytes |
| `ActiveEffectMutationBuffer` | ASC Entity / frame scratch / `GEStreamOwnerSingleton`（迁移期） | 4-16（目标）/ 128（proof-only） | ~32 bytes |
| `GameplayEventBuffer` | per-owner fact/outbox / `GEStreamOwnerSingleton`（迁移期） | 8-32（目标）/ 256（proof-only） | ~32 bytes |
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
| `GEEffectCommandBuffer` | 4-16（目标 compact）/ 256（proof-only singleton） | ~128-512 bytes / ~8 KB | 16-64 per owner / 1024 proof | > 50% 或 singleton pressure | frame-local command range；scale-ready 优先 `NativeStream` |
| `GESetByCallerValueBuffer` | 8-32（目标 range）/ 256（proof-only singleton） | ~128-512 bytes / ~4 KB | 32-128 per range / 512 proof | > 50% | command/spec 附属 range |
| `GEEffectSpecBuffer` | 目标态优先 NativeContainer / 256（proof-only singleton） | 由 allocator budget 控制 / ~12 KB | 按 fan-in budget / 1024 proof | allocator budget / proof spill | frame scratch，不固定为全局 DynamicBuffer |
| `AttributeModifierBuffer` | 8-32（target grouped）/ 512（proof-only singleton） | ~192-768 bytes / ~12 KB | 32-128 per target range / 2048 proof | > 50% 或 random lookup count | target-grouped reduce/apply |
| `ActiveEffectMutationBuffer` | 4-16（owner-local）/ 128（proof-only singleton） | ~128-512 bytes / ~4 KB | 16-64 per owner / 512 proof | > 50% | active slot mutation |
| `GameplayEventBuffer` | 8-32（per-owner/fact range）/ 256（proof-only singleton） | ~256 bytes-1 KB / ~8 KB | 32-128 per range / 1024 proof | cursor lag / backpressure / > 50% | Core fact / Boundary observation 分流 |
| `AbilitySlotBuffer` | 8 | ~128 bytes | 32 slots | > 50% | **跨帧存储**，ASC→Ability 反向查找 |
| `PresentationEventBuffer` | 4 | ~128 bytes | 16 events | > 50% | per-ASC outbox，UI 直接读取 |
| `ActiveGameplayEffectBuffer` | 8 | ~512 bytes | 64 slots | spill（溢出）或 > 32 slots | **跨帧存储** |
| `TargetDataBuffer` | 16 | ~128 bytes | 32 targets | > 50% | frame-local，Boundary request 低量目标解析结果；高频/高目标数走 `AbilityTargetRecord` |

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
| DefinitionCatalogSingleton | Bake / World init | Bootstrap only | BoundaryCommandIngest, TargetResolve, EffectFanIn, StateEvaluate, AttributeReduceApply, GameplayFact | World dispose / scene unload |
| Frame Fan-In Scratch（NativeContainer，非 Entity） | 每帧由 owner system 创建 | BoundaryCommandIngest, EffectFanIn, StateEvaluate, AttributeReduceApply, GameplayFact | owner system / downstream kernel | 当帧 dispose/rewind |
| `GEStreamOwnerSingleton`（proof-only） | World init | BoundaryCommandIngest, EffectFanIn, StateEvaluate, AttributeReduceApply, GameplayFact | EffectFanIn, AttributeReduceApply, GameplayFact, StructuralCommit, BoundaryProjection | World dispose |
| ASC Entity | StructuralCommit | StateEvaluate, AttributeReduceApply, StructuralCommit | 所有 kernel | StructuralCommit |
| Ability Entity | StructuralCommit（grant） | BoundaryCommandIngest, StateEvaluate, StructuralCommit | BoundaryCommandIngest, GameplayFact | StructuralCommit（revoke） |
| Request Entity | Boundary ECB (Application Shell, low-frequency only) | BoundaryCommandIngest（归一化 command）, TargetResolve（写 target buffer） | BoundaryCommandIngest, TargetResolve, EffectFanIn | StructuralCommit |
| Frame-local Command / Target Records（非 Entity） | owner `ISystem` 当帧分配 | BoundaryCommandIngest, Core producer, TargetResolve, EffectFanIn | TargetResolve, EffectFanIn, AttributeReduceApply | owner `ISystem` dispose / allocator rewind |
| Active Effect Query Entity（可选） | StructuralCommit | StateEvaluate | StateEvaluate, AttributeReduceApply | StructuralCommit |

---

## 不变量

1. 物理 archetype 数量目标 < 10，告警 > 20。
2. 所有 Component 必须明确类型（Data/Buffer/Enableable/Chunk），不能以"ECS component"笼统称呼。
3. Tag 状态通过 bitmask（`TagMaskComponent`）表达，不使用独立 tag component。
4. 每个 DynamicBuffer 必须有 `InternalBufferCapacity` 声明和 spill 监控。
5. Per-ASC 数据挂 ASC entity；frame-local fan-in 数据目标态由 owner `ISystem` 的 NativeContainer 承载，proof 阶段才允许挂 `GEStreamOwnerSingleton`。
6. 不使用 Prefab 承载 GE/Ability 定义（用 BlobAsset + static table）。
7. 不使用 `ISharedComponent` 除非通过三条件检查。
8. 不使用 `ICleanupComponent` 做普通状态——只用于 destroy 后清理（`PRF-12`）。
9. NativeContainer（`NativeArray`/`NativeHashMap` 等）放在 `IComponentData` 上时，**禁止**对该 component 所在 entity 调度 `IJobChunk`/`IJobEntity`（`PRF-34`）。安全系统无法追踪 component 内嵌容器的读写依赖，正确做法是主线程提取容器后单独调度 job。
10. **禁止**依赖 ECS Child Buffer 的 sibling index 做确定性排序（`PRF-31`）。Buffer 遍历顺序不保证帧间/平台间一致。若 GAS 需要确定性 multi-effect 执行顺序，必须自建排序键（如 command sequence、sortKey 等）。
11. Status / Buff / Debuff 类标记默认进入 `TagMaskComponent`；若派生 `TagStatusFlagsComponent`，它必须是所有 ASC 共有的 cache component，禁止按 status 类型增删 component。
12. Frame Fan-In scratch 是 owner system 拥有的 NativeContainer 生命周期，不是全局 manager / singleton message bus；新 scale-ready 任务不得依赖 `GEStreamOwnerSingleton` 的大容量 DynamicBuffer。
13. Definition Catalog 是只读 singleton，而不是 Runtime registry manager；Runtime Core 不写 `GASDefinitionCatalogComponent`，不把 `NativeHashMap` / `NativeArray` 塞进 catalog component，且不以 per-definition entity query 作为 hot path lookup。

## 验收

1. Debugger 输出 archetype 总数、单 entity archetype 列表、prefab archetype 列表。
2. Debugger 输出 buffer length/capacity/spill/externalized 全部指标。
3. x50 AutoChess profile 中 archetype 数量不随单位数线性增长。
4. 代码审查可验证：无 tag component、无 managed component 在 hot path、无 prefab 用于 GE 定义。
5. 每个 Component type 能说清它是 Data/Buffer/Enableable/Chunk 中的哪一类、为什么。
6. Status flag 派生 cache 与 `TagMaskComponent` 一致性采样通过，且不增加按 status 分裂的 archetype。
