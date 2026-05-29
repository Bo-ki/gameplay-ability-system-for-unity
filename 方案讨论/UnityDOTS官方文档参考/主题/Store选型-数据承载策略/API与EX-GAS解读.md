# Store 选型 / 数据承载策略: API 与 EX-GAS 解读

## 核心概念

### 数据性质分类框架

EX-GAS 中所有运行时数据按性质分为四大类别。这是 Store 选型的第一原则 —— 承载方式由数据性质决定，而非由实现便利决定。

#### 1. Gameplay Data（玩法核心数据）

**特征**：跨帧持久、影响 gameplay 逻辑、需要 deterministic 处理、参与 battle hash。

**典型数据**：
- ActiveEffect slot（当前生效的 gameplay effect）
- 属性值（Attribute 当前值 / base value / modifier）
- 已授予的 tag（GrantedTag mask）
- Ability cooldown 状态
- 技能/效果堆叠计数

**承载要求**：
- 必须使用 `DynamicBuffer` 或 `IComponentData` 直接存在于 entity chunk 内
- 不能是 transient 或 Temp allocator
- 读写必须通过 ECS system（gameplay system group）
- 确定性：生产顺序必须在相同输入下完全可复现

#### 2. Transient Data（帧内临时数据）

**特征**：单帧生命周期、同一帧内消费即弃、不需要跨帧持久、不需要确定性（除非用于 debug）。

**典型数据**：
- EffectCommand（每帧的 GE 请求流）
- AttributeDelta（属性变更汇总）
- TypedSimulationFact（系统内事件/事实）
- CueRequest（表现事件请求）

**承载要求**：
- 首选 `NativeStream`（并行 fan-in + 确定性 merge）
- 备选 per-thread ECB（EntityCommandBuffer 统一 playback）
- 帧末必须 drain / clear
- 不写入持久化 buffer
- 确定性：EffectCommand 等用于 gameplay 结算的 transient 数据需要确定性 merge 顺序；Presentation outbox 不需要确定性

#### 3. Telemetry Data（诊断/调试/统计）

**特征**：采样频率低于 gameplay tick、容量上限可控、不需要实时性、不需要确定性。

**典型数据**：
- Frame time breakdown
- Buffer spill rates
- Entity create/destroy 计数
- Archetype 统计快照
- Per-system 性能指标

**承载要求**：
- `NativeList`（Persistent allocator） + periodic export
- 固定容量截断（环形 buffer 或采样窗口）
- 不阻塞 gameplay hot path
- Debugger 输出层与 gameplay layer 隔离

#### 4. Presentation Data（表现层数据）

**特征**：从 gameplay 层单向流入、不需要确定性、丢失一帧不影响正确性、可能存在多平台差异。

**典型数据**：
- CueRequest（VFX/SFX 触发信号）
- 位置同步修正
- HP bar 更新
- 状态变化通知

**承载要求**：
- `NativeStream`（无需确定性）或边界 managed queue
- 帧末 drain 到 presentation layer
- 不反向影响 gameplay
- 允许降频/丢帧

### 数据承载方式对照表

| 承载方式 | 存储位置 | 生命周期 | 确定性 | 适用场景 |
|---|---|---|---|---|
| `DynamicBuffer<T>` | Chunk 内（内联/外部化） | entity 生命周期 | 取决于写入顺序 | Owner-local gameplay data |
| `IComponentData` | Chunk 内 per-entity | entity 生命周期 | 是 | 单值 gameplay state |
| `NativeStream` | 临时分配 | 帧内 | 可选（排序后确定） | 并行 fan-in |
| `NativeList` | 自定义 (Temp/TempJob/Persistent) | 按 allocator | 不保证 | Telemetry / 临时统计 |
| `ECB` (EntityCommandBuffer) | 延迟结构变化队列 | 帧内 | 取决于 sortKey | 结构变化统一提交 |
| `BlobAssetReference<T>` | 只读共享内存 | World 生命周期 | 只读 | 静态定义数据 |
| `ChunkComponentData` | Chunk 内每 chunk 一份 | chunk 生命周期 | 是 | Chunk 级元数据 |

### 数据承载选型决策流

```
数据需要跨帧持久？
  |-- 是 -+-> 数据是所有 entity 各自一份？
  |       |   |-- 是 -> DynamicBuffer（集合）或 IComponentData（单值）
  |       |   +-- 否 -> Chunk Component（chunk 级）或 Singleton（全局）
  |       +-> 数据是只读共享定义？
  |               -> BlobAssetReference<T> 或 generated static array
  |
  +-- 否（帧内临时）-+-> 需要确定性 merge？
                      |   |-- 是 -> NativeStream + sorted merge
                      |   +-- 否 -> NativeStream（无需排序）或 managed queue
                      |
                      +-> 是结构变化？
                              -> ECB（EntityCommandBuffer）

数据影响 battle hash？
  |-- 是 -> gameplay 分类 -> 确定性承载（DynamicBuffer / IComponentData）
  +-- 否 -> 检查数据性质
              |-- telemetry -> NativeList + 容量截断
              +-- presentation -> NativeStream 或 boundary queue
```

### Singleton vs DynamicBuffer 选型

| 条件 | 推荐方式 |
|---|---|
| 全局唯一 + 低频写入 | Singleton component + native container |
| 全局唯一 + 单 writer | Singleton DynamicBuffer |
| 全局唯一 + 多 writer 并行 | NativeStream（每线程独立段） |
| per-owner 数据 | Owner entity 上的 DynamicBuffer |

---

## EX-GAS 项目解读

### 数据分类映射

EX-GAS 的完整数据分类映射：

```
Gameplay（参与 battle hash）：
  - ActiveGameplayEffectBuffer DynamicBuffer slot, per-ASC
  - AttributeSet current/base   IComponentData, per-ASC
  - TagMaskComponent            IComponentData bitmask, per-ASC
  - AbilityStateComponent       IComponentData state/flags, per-ability entity
  - PeriodDueTag / ChunkComponent optional skip cache, only after profiler proof

Transient（帧内，用于 gameplay 但不跨帧持久）：
  - GEEffectCommandRecord       NativeStream segment -> deterministic merge
  - AttributeModifierRecord     NativeStream / target grouped range
  - GameplayFactRecord          NativeStream / owner-local fact range
  - EffectCommand merge result  NativeList, sorted by target/sequence

Telemetry（诊断，不参与 gameplay）：
  - Debug frame metrics         NativeList(Allocator.Persistent), sampled
  - Buffer pressure snapshot    NativeArray, periodic export
  - System profiling data       Managed buffer, capped

Presentation（表现层，单向输出）：
  - CueRequest                  NativeStream -> presentation system
  - Visual state change          Boundary managed queue
  - UI update event              Presentation outbox
```

### 每帧 Clear vs 跨帧持久决策

Transient 数据（EffectCommand、AttributeDelta、TypedFact）每帧 clear。关键设计约束：

```csharp
// 帧末 clear
var buffer = SystemAPI.GetSingletonBuffer<BAttributeDelta>();
buffer.Clear();

// 注意：Clear 不重置 InternalBufferCapacity
// 外部化数据不会因 Clear 迁回 chunk
// Clear 后 Capacity 保持，Length 归零
```

### Deterministic Merge 实现

```csharp
// EffectCommand deterministic merge 模式的规范实现
// 1. 并行收集：NativeStream ParallelWriter
// 2. 合并排序：按 target ASC entity 排序
// 3. 分发：写入 per-target BAttributeDelta buffer

struct EffectCommandMergeJob : IJob
{
    [ReadOnly] public NativeStream CommandStream;
    public NativeList<EffectCommand> SortedCommands;

    public void Execute()
    {
        SortedCommands.Clear();
        // ForEachCount 和 segment 遍历顺序在相同 job 调度下可复现
        for (int i = 0; i < CommandStream.ForEachCount; i++)
        {
            var reader = CommandStream.AsReader(i);
            for (int j = 0; j < reader.Length; j++)
            {
                SortedCommands.Add(reader.Read<EffectCommand>());
            }
        }
        // 排序保证确定性
        SortedCommands.Sort(new EffectCommandByTargetComparer());
    }
}
```

### Debugger 的 Store 健康指标

Debugger 应输出每种 Store 的健康状态：
- Store 类型和分类（gameplay/transient/telemetry/presentation）
- 当前容量和使用率
- 是否参与 battle hash
- 生命周期（帧内 clear / 跨帧持久）
- 当前 Entity 数量、chunk 数量、unused capacity

---

## 常见陷阱

1. **数据分类混淆**：将 presentation 数据（如 cue request）放入 gameplay buffer，增加不必要的确定性开销，且可能污染 battle hash。
2. **Transient 数据误用跨帧持久承载**：EffectCommand 如果不小心写入 DynamicBuffer 且未在帧末 clear，跨帧积累导致内存膨胀和状态不一致。
3. **Telemetry 数据影响 gameplay hot path**：在 gameplay system 中直接采集 telemetry（如 `Time.realtimeSinceStartup`），增加非必要分支和性能开销。应通过 TelemetrySystem 异步采样。
4. **Store 命名模糊**：`DataStore` / `EffectStore` 等命名不表达 owner 和生命周期，导致新成员不清楚谁持有数据、何时有效。
5. **非确定性 merge 隐含的 battle hash 风险**：在 CPU 多线程环境下，非排序的并行写入自然产生不可预测的顺序。任何参与 battle hash 的数据必须确定性排序。
6. **Presentation outbox 阻塞 gameplay**：如果 Presentation outbox 是同步 drain 且目标系统响应慢，会拖慢 gameplay 帧。应使用异步/批处理方案。

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则 |
|---|---|---|
| `components-buffer-introducing.html` | DynamicBuffer 无 NativeContainer 调度限制，适合 per-owner 集合 | STORE-01, STORE-03 |
| `systems-entity-command-buffer-playback.md` | sortKey + ChunkIndexInQuery 实现确定性 ECB 回放 | STORE-02 |
| `performance-chunk-allocations.html` | 临时数据用 DynamicBuffer 而非 Add/Remove Component | STORE-03 |
| `components-nativecontainers.md` | NativeContainer 在 component 上时禁止调度 IJobChunk/IJobEntity | STORE-03 |
| `systems-entity-command-buffers.md` | ECB 最佳实践、独立 ECB per job | STORE-03 |
| `systems-systemapi.md` | SystemAPI.GetSingleton 不触发 sync point vs EntityManager.GetComponentData 触发 | STORE-02 |
| `systems-data.md` | 系统级数据存为 component 而非 system 字段（CASE-45） | STORE-01 |
