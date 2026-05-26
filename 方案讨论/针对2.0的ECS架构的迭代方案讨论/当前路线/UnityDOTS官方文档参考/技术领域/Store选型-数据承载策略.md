# Store 选型 / 数据承载策略

## 职责

覆盖 EX-GAS 中不同性质数据的承载方式选型框架：gameplay / transient / telemetry / presentation 四类数据的承载策略、Store 命名规范、确定性要求。不覆盖底层 ECS 数据结构（DynamicBuffer / Archetype / Chunk 物理布局见 DynamicBuffer-Chunk-Archetype）、不覆盖 BlobAsset 构建细节、不覆盖 NativeContainer allocator 管理。

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

## 编写规范

### STORE-01: Store 命名表达 owner、生命周期、索引方式

- **声明**：所有 Store 类型名称必须明确表达三个维度：owner（谁持有）、生命周期（帧内/跨帧/持久）、索引方式（直接/哈希查找/顺序）。
- **来源**：EX-GAS 项目内部约定（由 ActiveEffectStore 物理设计演化而来）
- **为什么**：Store 是 ECS 中跨多 entity 和多 system 的数据集合。名称含糊导致新加入者不知道数据在哪、谁可以写、生命周期多长。明确命名降低认知负担和误写风险。
- **EX-GAS 诊断**：
  - `OwnerLocalStore`：per-ASC，同 chunk 内访问
  - `GlobalIndexedStore`：跨 entity 查询，需要 lookup 或 secondary index
  - `LifecycleCleanupStore`：用于追踪待清理的 granted tag/ability
  - 命名模式：`{生命周期前缀}{数据描述}Store`（如 `FrameCommandStore`、`PersistEffectStore`）
- **检查方法**：Code review 检查新 Store 类型是否包含 owner/lifetime/index 维度信息。

### STORE-02: 影响 battle hash 的输出 deterministic

- **声明**：任何影响 battle hash（战斗状态校验/回放一致性）的数据输出，其生产顺序必须在相同输入下完全可复现。使用 `NativeStream` + deterministic merge（按 target ID 排序）或在 `DynamicBuffer` 中使用固定遍历顺序。
- **来源**：EX-GAS 项目确定性回放需求 + `systems-entity-command-buffer-playback.md`（sortKey 确定性回放）
- **为什么**：battle hash 用于校验多个客户端或 replay 中游戏状态是否一致。非确定性排序导致即使输入相同，输出顺序不同 -> hash 不匹配 -> 误判为不同步。
- **EX-GAS 诊断**：EffectCommand fan-in 的 merge 阶段必须 deterministic。AttributeDelta 按 target 分组汇总必须在确定性排序后执行。`TypedSimulationFact` 的生产顺序需可复现。
- **检查方法**：检查所有 gameplay 分类的数据通路，确认写入顺序在相同输入下产生相同输出。NativeStream 的 foreach 按 ForEachCount 和 segment 内部顺序遍历是可复现的。

### STORE-03: Store 选型按数据性质分类：gameplay / transient / telemetry / presentation

- **声明**：所有数据承载方式选型必须按照 gameplay / transient / telemetry / presentation 四类数据性质进行，不得混淆类别。
- **来源**：EX-GAS 数据承载框架设计决议
- **为什么**：不同性质的数据对确定性、持久性、实时性、丢失容忍度的要求完全不同。混淆类别导致：
  - Gameplay 数据用 transient 承载 -> 帧丢失导致状态不一致
  - Presentation 数据用 gameplay 承载 -> 不必要的确定性开销拖慢 hot path
  - Telemetry 数据混入 gameplay 流程 -> 污染 battle hash 或不必要地增加 gameplay 数据量
- **EX-GAS 诊断**：
  - Owner-local active effects：`DynamicBuffer<BActiveEffectSlot>`，固定容量（如 64 slot），gameplay 分类
  - EffectCommand fan-in：`NativeStream`（并行），per-frame scratch，transient 分类
  - AttributeDelta：per-target `DynamicBuffer<BAttributeDelta>`，per-frame clear，transient 分类
  - TypedSimulationFact：`DynamicBuffer<BTypedFact>` on singleton，per-frame clear，transient 分类
  - Presentation outbox：transient `NativeStream` 或 boundary managed queue，presentation 分类
  - Debug telemetry：sampled `NativeList`（Persistent）+ periodic export，telemetry 分类
  - Static definition：`BlobAssetReference` 或 generated static array，不分类（只读共享）
- **检查方法**：每个数据承载方式选型决策必须记录数据性质分类和选型理由。

---

## 模式与反模式

### 正确模式

| 数据 | 分类 | 承载方式 | 原因 |
|---|---|---|---|
| ActiveEffect slot | gameplay | `DynamicBuffer<BActiveEffectSlot>` | per-owner 持久、确定性、chunk 内联 |
| EffectCommand 流 | transient | `NativeStream` + deterministic merge | 并行 fan-in、帧内消费 |
| Attribute delta | transient | per-target `DynamicBuffer<BAttributeDelta>` | 帧内 clear、按 target 分组 |
| Simulation fact | transient | `DynamicBuffer<BTypedFact>` on singleton | 帧内 clear、需确定性 |
| Presentation outbox | presentation | `NativeStream` / managed queue | 不需要确定性、可丢帧 |
| Debug telemetry | telemetry | sampled `NativeList`(Persistent) | 容量截断、hot path 不阻塞 |
| GE definition | 只读共享 | `BlobAssetReference` / static table | 不分类、只读共享、生命周期长 |

### 反模式

| 反模式 | 原因 | 替代 |
|---|---|---|
| Gameplay 数据用 Temp NativeContainer 承载 | 帧结束后数据丢失，导致 gameplay 状态不一致 | DynamicBuffer（跨帧持久） |
| Presentation 数据混入 gameplay 确定性路径 | 不必要地增加排序/确定性计算开销，为 gameplay 层引入非确定性隐患 | 分离 presentation outbox，不参与 gameplay hash |
| Telemetry 采样写入 gameplay buffer | 污染 battle hash，增加 gameplay buffer 非必要数据量 | 独立 telemetry NativeList |
| 所有临时数据都走同一全局 DynamicBuffer | 失去并行 fan-in 能力，单一 buffer 成为写入瓶颈 | NativeStream（各线程独立段） |
| 用 prefab 承载纯定义数据（GE definition prefab） | 每个 prefab 占 16 KiB chunk，大量不同 prefab -> 内存浪费 | BlobAsset / static table |
| EffectCommand merge 不排序 | 非确定性 merge 导致相同输入产生不同 battle hash | 按 target ID 排序后 deterministic merge |
| Store 命名不表达 owner/lifetime | 新加入者不知道谁持有数据、数据何时有效 | 遵循 STORE-01 命名模式 |

---

## EX-GAS 项目解读

### 数据分类映射

EX-GAS 的完整数据分类映射：

```
Gameplay（参与 battle hash）：
  - BActiveEffectSlot          DynamicBuffer, per-ASC
  - BAttribute (current/base)  IComponentData, per-ASC
  - BGrantedTagMask             IComponentData, per-ASC
  - BAbilityCooldown            DynamicBuffer, per-ASC
  - CActiveEffectEnableable     IEnableableComponent, per-ASC

Transient（帧内，用于 gameplay 但不跨帧持久）：
  - CEffectCommandEntry         NativeStream segment -> per-target buffer
  - BAttributeDelta             DynamicBuffer, per-target, per-frame clear
  - BTypedFact                  DynamicBuffer on singleton, per-frame clear
  - EffectCommand merge result  NativeList, sorted

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

---

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

---

## 验收指标

1. 每个数据承载方式选型决策记录数据性质分类（gameplay/transient/telemetry/presentation）
2. 所有 gameplay 分类数据明确标注是否参与 battle hash，参与路径经过确定性验证
3. Transient 数据在帧末 clear 或 drain，无跨帧泄漏
4. Telemetry 数据不阻塞 gameplay hot path，通过独立采样窗口采集
5. Presentation 数据单向流入表现层，不反向影响 gameplay 状态
6. Store 名称符合 STORE-01 命名约束（owner/lifetime/index 维度）
7. 所有 NativeStream 的 gameplay 使用路径包含 deterministic merge 步骤
8. Debugger 输出 Store 分类统计和健康指标
