# Baking-BlobAsset

## 职责

覆盖 Baker、Baking System、BlobAsset 三个 Baking 期核心机制的编码规范与模式约束。负责静态定义数据从 authoring 到 ECS runtime 的转换管线。不覆盖 Prefab／Content 加载（见 `Prefab-Content管理.md`），不覆盖 runtime 动态数据存储。

## 核心概念

### Baker

Baker 是 authoring GameObject 到 ECS component 的转换入口，**每个 authoring GameObject 对应一个 Baker 实例**，只在 Editor 中执行。

```csharp
public class AbilityAuthoringBaker : Baker<AbilityAuthoring>
{
    public override void Bake(AbilityAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        AddComponent(entity, new CAbilityConfig { AbilityCode = authoring.AbilityCode });
        DependsOn(authoring.EffectConfig);
    }
}
```

**两种 Baking 模式：**

| 模式 | 触发条件 | 执行方式 | 输出 |
|---|---|---|---|
| Full Baking | Subscene 关闭／首次导入／BakingVersion 变更 | 后台 asset importer 进程 | 磁盘文件（Entity Scene） |
| Incremental Baking | Subscene 打开 + authoring 变更 | 主 Editor 进程，内存中 | 直接更新 ECS World |

核心约束：Full Baking 与 Incremental Baking 的 entity 顺序和 chunk 布局可能不同，不能依赖 baking output ordering。

### Baking System

Baking System 在 Baking 流程中执行批处理数据，类似 runtime `ISystem` 但在 Baking World 中运行。

```csharp
[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
public partial struct AbilityDefinitionBakingSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 收集所有 baked ability config，生成 definition blob，写入 output entity
    }
}
```

与 Baker 不同，Baking System **不自动追踪依赖或结构变化**，所有依赖必须手动声明。

### BlobAsset

BlobAsset 是 immutable、reference-counted 的只读数据结构，支持多 entity 共享引用。

```csharp
public struct AbilityDefinitionBlob
{
    public int AbilityCode;
    public float CooldownDuration;
    public BlobArray<int> GrantedTagCodes;
}

// 构建
var builder = new BlobBuilder(Allocator.Temp);
ref var root = ref builder.ConstructRoot<AbilityDefinitionBlob>();
root.AbilityCode = 1001;
var blobRef = builder.CreateBlobAssetReference<AbilityDefinitionBlob>(Allocator.Persistent);
builder.Dispose();
```

**关键特性：**
- Immutable：创建后不可修改
- Reference-counted：`BlobAssetReference<T>` 自动管理生命周期，不可手动 `Dispose`（除非创建者且不再传递）
- Burst-friendly：可在 job 中直接访问
- 多 entity 共享：同一 blob 引用可赋给多个 entity

## 编写规范

### BAKE-01: Baker 只添加不读取 — Baker 间无依赖

**声明：** Baker 内 `GetComponent<T>()`／`SetComponent<T>()` 不可用；只能 `AddComponent<T>()`。

- **来源：** `baking-phases.md` (CASE-39)，官方案例模式高级文档。
- **为什么：** Baker 之间无依赖关系。Baker 只能向当前 entity 添加新 component，不能读取已有 component 的值或访问／修改其他 entity。若在 Baker 中访问其他 entity 会导致未定义行为。
- **EX-GAS 诊断：** Ability／GE／Buff definition 的 Baker 只负责烘焙期数据搬运；GE 的 modifier 计算逻辑放在 Baking System。
- **检查方法：** 审查所有 Baker 的 `Bake()` 方法，确认无 `GetComponent`／`SetComponent` 调用。需读取已有数据做复杂计算时，使用 Baking System（参考 BAKE-03 + CASE-32 TemporaryBakingType）。

### BAKE-02: Baker 必须无状态 — 单例实例、Bake() 多次调用且无序

**声明：** Baker 只实例化一次，`Bake()` 被多次调用且顺序不确定；禁止在 Baker 实例字段中缓存数据。

- **来源：** `baking-baker-overview.md` (CASE-40)，官方案例模式高级文档。
- **为什么：** Baker 是单例实例（每个 Baker 类型一个实例），其 `Bake()` 方法在非确定性顺序下被多次调用（增量烘焙跨长时间运行）。在 Baker 字段中缓存任何值违反不变式，导致烘焙行为异常。所有数据访问必须通过 Baker 方法（自动记录依赖和产出以支持增量烘焙的 undo/redo）。
- **EX-GAS 诊断：** GE／Ability definition Baker 的 `Bake()` 方法必须无状态，定义数据通过 `BlobBuilder` 或 component 产出。
- **检查方法：** 审查所有 Baker 类，确保无实例字段（除 `readonly` 常量外）。static 字段同样禁止，会跨 Baking 会话残留。

### BAKE-03: Baking System 必须手动追踪依赖和增量还原

**声明：** Baking System 不自动追踪依赖和结构变化；需显式 `DependsOn()` 并在添加组件时手动追踪／撤销变更。

- **来源：** `baking-baking-systems-overview.md` (CASE-41)，官方案例模式高级文档。
- **为什么：** Baking System 与 Baker 不同，不会自动记录依赖或产出。必须：1) 对所有外部数据引用显式调用 `DependsOn()`；2) 当向 entity 添加 component 时手动追踪并实现撤销逻辑以支持增量烘焙。Baking System 中创建的 entity **不会**出现在 baked entity scene 中（仅用于系统间数据传递）；要输出到 scene 的 entity 必须在 Baker 中通过 `CreateAdditionalEntity` 创建。
- **EX-GAS 诊断：** 批量 GE 属性计算、Definition 数据关联与过滤等场景必须通过 Baking System 实现，并手动管理增量还原。
- **检查方法：** 审查所有 `[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]` 标记的 ISystem，确认 `DependsOn()` 调用覆盖所有外部依赖，且添加 component 时有匹配的撤销逻辑。

### BLOB-01: BlobAsset 用于 immutable 静态定义；runtime 只读

**声明：** BlobAsset 存放 Baking 期或初始化期确定的不可变数据；runtime hot path 只消费 `BlobAssetReference<T>`，不修改。

- **来源：** `blob-assets-create.md`、官方 BlobAsset API 文档。
- **为什么：** BlobAsset 的设计目标是 immutable、reference-counted、Burst-friendly 的共享只读数据。创建后不可修改，适合存放 Ability 定义、GE 定义、Tag 查找表等静态配置。Runtime 可变状态必须使用 `DynamicBuffer` 或普通 component。
- **EX-GAS 诊断：** 目标态 Runtime Core hot path 只消费 `BlobAssetReference<T>` 或 generated static table，无 managed config lookup。`AbilityConfigRegistry`／`GameplayEffectConfigRegistry` 应逐步被 Blob/Prototype 替代。
- **检查方法：** 审查 BlobAsset 数据类型的写入路径，确认只在 Baking System 或 `OnCreate` 中写入，runtime `OnUpdate` 中只读不写。搜索 `BlobAssetReference` 的 `.Value` 赋值出现位置。

### BLOB-02: BlobBuilder 在 Baking 或初始化时使用；不在 hot path 构建

**声明：** `BlobBuilder` 分配临时内存并执行数据复制，成本较高，禁止在 runtime hot path 中使用。

- **来源：** `blob-assets-create.md` (CASE-24)，官方案例模式高级文档。
- **为什么：** `BlobBuilder.ConstructRoot` + `Allocate` + `CreateBlobAssetReference` 涉及大量内存分配和数据拷贝，不适合 per-frame 或 per-event 调用。正确模式：`ISystem.OnCreate` 中一次性构建并将 `BlobAssetReference<T>` 存入 singleton component，`OnDestroy` 时 `Dispose()`。
- **EX-GAS 诊断：** Definition & Generation Layer 中，Luban Excel/JSON → SourceGenerator → .gen.cs → Baker → BlobAsset 管线仅在 Baking 期执行。Runtime 无 `BlobBuilder` 调用。
- **检查方法：** Grep 搜索 `new BlobBuilder` 在 Runtime Core 目录中的出现，确认仅存在于 Baking System 或初始化 System 的 `OnCreate` 中。hot path system 中出现 `BlobBuilder` 为违规。

## 模式与反模式

### 正确模式

1. **Baker + Blob 定义管线（CASE-07）**：Baker 从 authoring 读取配置数据，通过 `BlobBuilder` 创建 `BlobAssetReference<T>`，通过 `AddBlobAsset<T>` 向 Baker 注册后写入 entity component。适用于 Ability 定义、GE 定义、Tag 配置表等静态数据。
2. **TemporaryBakingType + Baking System Burst 计算（CASE-32）**：Baker 写入 `[TemporaryBakingType]` 临时数据 → Baking System 中 Burst ISystem 做复杂计算 → 写入持久化 component。适用于需要跨 entity 聚合或复杂计算的场景（如 modifier 计算、定义关联）。
3. **Baker 将 `BlobAssetReference` 注册到 Baker**：调用 `AddBlobAsset(ref blobRef, out var hash)` 向 Baker 注册 blob，自动处理去重和引用计数。`TryGetBlobAssetReference` 可在同一 Baker 会话中复用已注册的 blob。
4. **Baking System 中获取 Section Meta Entity（CASE-44）**：通过 `SerializeUtility.GetSceneSectionEntity` 获取 section meta entity 并附加自定义元数据，在 Streaming 场景中提供加载条件。

### 反模式

1. **Baker 中读取已有 component 值**（违反 BAKE-01）：Baker 不能使用 `GetComponent<T>()` 或 `SetComponent<T>()`，会导致未定义行为。替代：在 Baking System 中处理（CASE-32）。
2. **Baker 持有状态字段**（违反 BAKE-02）：static 或实例字段在多次 Bake() 调用间残留。替代：所有数据通过 Baker 方法参数访问。
3. **Baking System 未声明 DependsOn**（违反 BAKE-03）：外部资源变更后 Baking System 不重新执行，导致 stale 数据。替代：对所有外部引用显式声明 `DependsOn()`。
4. **BlobAsset 用于运行时可变状态**（违反 BLOB-01）：尝试修改已创建的 `BlobAssetReference` 或使用 BlobAsset 存储每帧变化的数据。替代：可变数据用 `DynamicBuffer`。
5. **Runtime hot path 中使用 BlobBuilder**（违反 BLOB-02）：每帧或每次 effect 应用时构建 BlobAsset。替代：Baking 期或初始化期一次性构建。
6. **Baking System 中创建 entity 期待进入 baked scene**（违反 BAKE-03）：Baking System 中创建的 entity 不会出现在 baked entity scene 中，仅用于系统间数据传递。替代：Baker 中使用 `CreateAdditionalEntity`。
7. **`DependsOn` 写在 early-out 之后**（CASE-31 警告）：`DependsOn(authoring.Reference)` 必须在所有 `if (ref == null) return` 之前调用，确保外部引用恢复时 Baker 被重新触发。

## EX-GAS 项目解读

### Definition & Generation Layer 的物理形态

目标态 config 链：

```
Luban Excel/JSON → SourceGenerator → .gen.cs → Baker → BlobAsset / Entity
                         ↓
                  GASDefinitionTable（static lookup）
                         ↓
                  Runtime Core（BlobRef + component lookup only）
```

**当前差距：**
- `AbilityConfigRegistry`／`GameplayEffectConfigRegistry` 仍偏托管
- managed config 对象作为运行时入口，未被 Blob/Prototype 完全替代
- 目标态：Runtime hot path 只消费 `BlobAssetReference<T>` 或 generated static table

### EX-GAS 中 Baker 的职责边界

- Ability／GE／Tag 的 Baker 只进行 authoring 到 component 的数据搬运
- 复杂计算（modifier 解析、条件过滤、定义关联）放入 `[WorldSystemFilter(BakingSystem)]` 标记的 ISystem
- Baker 调用 `DependsOn()` 追踪外部资源变更，确保增量烘焙正确性
- `CreateAdditionalEntity` 用于一个 authoring 产出多个 runtime entity（如一个 AbilityDefinition authoring → ability entity + 多个 GE entity）

### BlobAsset 在 EX-GAS 中的适用场景

- AbilityDefinitionBlob：技能代码、冷却时间、GrantedTag 列表
- GameplayEffectDefinitionBlob：持续时间、Modifier 数组、Tag 条件
- AttributeMetaBlob：属性代码、最小值、最大值、增长率
- CueDefinitionBlob：Cue 类型、参数表

### 代码目录映射

| 机制 | 主要代码目录 | 文档关联 |
|---|---|---|
| Baker | `Assets/GAS/Editor/CodeGen/`、`Assets/GAS/Runtime/Ability/` | BAKE-01, BAKE-02 |
| Baking System | `Assets/GAS/Runtime/System/` 下与 Definition 相关的 System | BAKE-03 |
| BlobAsset 定义 | 位于 Entity Component 目录的 Blob struct | BLOB-01, BLOB-02 |

## 常见陷阱

1. **BlobArray 不能嵌套 BlobArray**：只能 `BlobArray<T>`，T 不能包含另一个 `BlobArray`。需展平为多维索引或使用 `BlobArray<BlobArray<T>>` 的子 blob 引用模式。
2. **Baker 持有状态**：static 字段会在多次 Baking 间残留；实例字段在单例 Baker 中跨 `Bake()` 调用残留。
3. **BlobAssetReference 的 Dispose**：完全由引用计数管理，不要手动调用 `Dispose`（除非是创建者且不再传递），否则导致悬挂引用或 double-free。
4. **Full Baking vs Incremental Baking 的 entity 顺序不同**：不能依赖 chunk 顺序的一致性，必须使用确定性索引（如 `[ChunkIndexInQuery]`）否则 output 不可重现。
5. **Baking System 创建的 entity 不进入 baked scene**：该 entity 仅存在于 Baking World 的系统间数据传递，不会序列化到磁盘。如需输出到 scene 必须在 Baker 中创建。
6. **`DependsOn` 放在 early-out 之后**：当 external reference 为 null 时 `DependsOn` 未执行，之后引用被赋值时 Baker 不会重新触发。违反 CASE-31 模式约束。

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `baking-overview.html` | Baking 只在 Editor；Full baking 输出文件，Incremental baking 在内存；两种模式的 entity 顺序可能不同 | BAKE-01, BAKE-02 |
| `baking-baker-overview.md` | Baker 必须无状态；依赖通过 API 声明；Baker 是单例实例 | BAKE-02, CASE-40 |
| `baking-phases.md` | Baker 间无依赖；Baker 只添加不读取；Baking phases 顺序 | BAKE-01, CASE-39 |
| `baking-baking-systems-overview.md` | Baking System 不自动追踪依赖；需手动增量还原；entity 不进入 baked scene | BAKE-03, CASE-41 |
| `blob-assets-create.md` | BlobAsset immutable、reference-counted、Burst-friendly；必须向 Baker 注册；TryGetBlobAssetReference 去重 | BLOB-01, BLOB-02, CASE-24 |
| `baking-prefabs.md` | Prefab 需 Baker 注册；runtime 通过 EntityPrefabReference 加载 | BAKE-01 (context) |
| `components-buffer-introducing.html` | DynamicBuffer 用于可变数据对比 BlobAsset 不可变语义 | BLOB-01 |

## 验收指标

1. Luban 配置变更能生成稳定 id 和静态定义，经过 Baker → BlobAsset 管线产出 runtime 可用数据。
2. Runtime Core hot path 无 managed config lookup，所有静态定义通过 `BlobAssetReference<T>` 或 generated static table 访问。
3. 所有 Baker 类零 `GetComponent`／`SetComponent` 调用，零实例状态字段。
4. 所有 Baking System 显式声明 `DependsOn()` 并实现增量还原逻辑。
5. 代码中无 `new BlobBuilder` 出现在 Runtime Core hot path（Baking 或初始化 System 的 `OnCreate` 除外）。
6. GE／Ability definition 不通过 Prefab 承载，仅通过 BlobAsset 或静态 component 定义。
