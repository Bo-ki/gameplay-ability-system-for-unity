# Baking-BlobAsset: API 与 EX-GAS 解读

## 核心概念

### Baker

Baker 是 authoring GameObject 到 ECS component 的转换入口，只在 Editor 中执行。PackageCache `baking-baker-overview.md` 明确 Baker 实例会被复用，`Bake()` 会被多次、无序调用，因此 Baker 必须无状态，不能在实例字段或 static 字段缓存跨调用数据。

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

BlobAsset 是 immutable 的只读数据结构，支持多 entity 共享引用。生命周期要看来源：运行时通过 `BlobBuilder.CreateBlobAssetReference()` 创建的 Blob 必须由创建者手动 `Dispose()`；Baker / Entity Scene 路径通过 BlobAssetStore 和引用计数释放。

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
- Lifetime-aware：运行时创建者负责释放；Baker / Entity Scene 输出由 BlobAssetStore / 引用计数释放
- Burst-friendly：可在 job 中直接访问
- 多 entity 共享：同一 blob 引用可赋给多个 entity

---

## EX-GAS 项目解读

### Definition & Generation Layer 的物理形态

目标态 config 链：

```
Luban Excel/JSON → SourceGenerator → .gen.cs → Baker → BlobAsset / Entity
                         ↓
                  GASDefinitionCatalogBlob / generated static lookup
                         ↓
                  Runtime Core（BlobRef + component lookup only）
```

**当前差距：**
- `AbilityConfigRegistry`／`GameplayEffectConfigRegistry` 仍偏托管
- managed config 对象作为运行时入口，未被 Blob/Prototype 完全替代
- 目标态：Runtime hot path 只消费 `BlobAssetReference<T>`、只读 Catalog Blob 或 generated O(1)/O(log n) static lookup

### EX-GAS 中 Baker 的职责边界

- Ability／GE／Tag 的 Baker 只进行 authoring 到 component 的数据搬运
- 复杂计算（modifier 解析、条件过滤、定义关联）放入 `[WorldSystemFilter(BakingSystem)]` 标记的 ISystem
- Baker 调用 `DependsOn()` 追踪外部资源变更，确保增量烘焙正确性
- `CreateAdditionalEntity` 用于一个 authoring 产出多个 runtime entity

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

---

## 常见陷阱

1. **含内部指针的 Blob 数据不能按值复制**：`BlobArray`、`BlobString`、`BlobPtr` 必须通过 `BlobBuilder` 构建，并通过 `ref` 或 `BlobAssetReference<T>` 访问。EX-GAS 多层配置默认展平为 range/index；只有能证明按 `ref` 构建和访问正确时才允许嵌套形态。
2. **Baker 持有状态**：static 字段会在多次 Baking 间残留；实例字段在单例 Baker 中跨 `Bake()` 调用残留。
3. **BlobAssetReference 的 Dispose**：运行时创建的 Blob 必须由创建者手动 `Dispose`；Entity Scene 载入或 Baker `AddBlobAsset()` 注册的 Blob 不要手动释放。
4. **Full Baking vs Incremental Baking 的 entity 顺序不同**：不能依赖 chunk 顺序的一致性，必须使用确定性索引（如 `[ChunkIndexInQuery]`）否则 output 不可重现。
5. **Baking System 创建的 entity 不进入 baked scene**：该 entity 仅存在于 Baking World 的系统间数据传递，不会序列化到磁盘。如需输出到 scene 必须在 Baker 中创建。
6. **`DependsOn` 放在 early-out 之后**：当 external reference 为 null 时 `DependsOn` 未执行，之后引用被赋值时 Baker 不会重新触发。违反 CASE-31 模式约束。

## 官方证据

| 官方文档 | 关键结论 | 关联规则 |
|---|---|---|
| `baking-overview.html` | Baking 只在 Editor；Full baking 输出文件，Incremental baking 在内存；两种模式的 entity 顺序可能不同 | BAKE-01, BAKE-02 |
| `baking-baker-overview.md` | Baker 必须无状态；依赖通过 API 声明；Baker 是单例实例 | BAKE-02, CASE-40 |
| `baking-phases.md` | Baker 间无依赖；Baker 只添加不读取；Baking phases 顺序 | BAKE-01, CASE-39 |
| `baking-baking-systems-overview.md` | Baking System 不自动追踪依赖；需手动增量还原；entity 不进入 baked scene | BAKE-03, CASE-41 |
| `blob-assets-create.md` | BlobAsset immutable、Burst-friendly；含内部指针的数据必须用 `ref` / `BlobAssetReference<T>` 访问；运行时创建的 Blob 需手动 Dispose；Baker 创建的 Blob 必须向 Baker 注册；`TryGetBlobAssetReference` + custom hash 去重 | BLOB-01, BLOB-02, CASE-24 |
| `baking-prefabs.md` | Prefab 需 Baker 注册；runtime 通过 EntityPrefabReference 加载 | BAKE-01 (context) |
| `components-buffer-introducing.html` | DynamicBuffer 用于可变数据对比 BlobAsset 不可变语义 | BLOB-01 |
