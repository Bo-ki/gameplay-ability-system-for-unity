# 05 Baking、Blob、Prefab 与 Content

## 职责

本主题维护 Baker、Baking System、BlobAsset、EntityPrefabReference、PrefabLoadResult、WeakObjectReference 和 content loading 规则。这直接决定 EX-GAS 的 Definition & Generation Layer 物理形态。

## 核心概念详解

### Baking 流程

Baking 是将 GameObject authoring 数据转换为 ECS runtime entity 的过程，**只在 Editor 中执行，不在 runtime 运行**。

**两种 Baking 模式：**

| 模式 | 触发条件 | 执行方式 | 输出 |
|---|---|---|---|
| Full Baking | Subscene 关闭 / 首次导入 / BakingVersion 变更 | 后台 asset importer 进程 | 磁盘文件（Entity Scene） |
| Incremental Baking | Subscene 打开 + authoring 变更 | 主 Editor 进程，内存中 | 直接更新 ECS World |

**核心约束：**
- Full Baking 和 Incremental Baking 的 entity 顺序和 chunk 布局可能不同 → 不能依赖 baking output ordering
- BakingVersion 属性变更触发 full rebake

### Baker

Baker 是将 MonoBehaviour 转换为 ECS component 的入口，每个 authoring GameObject 会调用对应的 Baker。

```csharp
// Baker 必须无状态
// 依赖通过 DependsOn() API 声明，不是通过字段缓存
public class AbilityAuthoringBaker : Baker<AbilityAuthoring>
{
    public override void Bake(AbilityAuthoring authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);
        
        // 添加运行时组件
        AddComponent(entity, new CAbilityConfig
        {
            AbilityCode = authoring.AbilityCode,
            Duration = authoring.Duration
        });
        
        // 声明对其他 GameObject 的依赖
        DependsOn(authoring.EffectConfig);
    }
}
```

**Baker 不可做的事：**
- 缓存状态（static 字段也不行，会跨 Baking 会话残留）
- 从 runtime World 读取 gameplay 状态
- 跨 Baker 修改其他 entity（有阶段限制）
- 在 Baker 内做运行时逻辑

### Baking System

Baking System 在 baking 流程中批处理数据，类似 runtime System 但在 baking World 中：

```csharp
// 用于跨 entity 的数据处理、生成 lookup table 等
[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
public partial struct AbilityDefinitionBakingSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 收集所有 baked ability config
        // 生成 definition blob
        // 写入 baking output entity
    }
}
```

### BlobAsset

BlobAsset 是 immutable、reference-counted 的只读数据结构，支持多 entity 共享引用。

```csharp
// 定义 Blob 类型
public struct AbilityDefinitionBlob
{
    public int AbilityCode;
    public float CooldownDuration;
    public BlobArray<int> GrantedTagCodes;
    public BlobArray<ModifierBlob> Modifiers;
}

// 创建 BlobAsset
var builder = new BlobBuilder(Allocator.Temp);
ref var root = ref builder.ConstructRoot<AbilityDefinitionBlob>();
root.AbilityCode = 1001;

var tagsArray = builder.Allocate(ref root.GrantedTagCodes, 3);
tagsArray[0] = 5;
tagsArray[1] = 8;
tagsArray[2] = 12;

var blobRef = builder.CreateBlobAssetReference<AbilityDefinitionBlob>(Allocator.Persistent);
builder.Dispose();
```

**BlobAsset 关键特性：**
- Immutable：创建后不能修改
- Reference-counted：`BlobAssetReference<T>` 自动管理生命周期
- Burst-friendly：可以在 job 中直接访问
- 多 entity 共享：同一个 blob 引用可以赋给多个 entity

### EntityPrefabReference / Content Loading

```csharp
// Authoring 端：声明 prefab 引用
public struct CEffectVfxPrefab : IComponentData
{
    public EntityPrefabReference VfxPrefab;
}

// Runtime 端：加载 prefab
// 无头 Demo 中可用 log marker 占位
public struct PresentationBinding : IComponentData
{
    public EntityPrefabReference PrefabRef;
    public bool UseLogMarker;  // 无头时为 true
}
```

## 官方证据

| 证据 | 结论 |
|---|---|
| `baking-overview.html` | Baking 只在 Editor；Full baking 输出文件，Incremental baking 在内存 |
| `baking-baker-overview.md` | Baker 必须无状态；依赖通过 API 声明 |
| `baking-prefabs.md` | prefab 需 baker 注册；runtime 通过 EntityPrefabReference 加载 |
| BlobAsset API | Immutable、reference-counted、Burst-friendly；多 entity 共享 |

## 使用模式与反模式

**正确模式：**
- 大量只读定义 → BlobAsset 或 generated static lookup
- Authoring 依赖声明 → Baker.DependsOn()
- Prefab 引用 → EntityPrefabReference（Burst-compatible）
- Baker 保持无状态

**反模式：**
- 从 Baking World 读取 runtime gameplay 状态
- 在 Baker 中缓存数据到 static 字段
- 把 Ability/GE 生命周期逻辑放进 Baking System
- Runtime Core 中托管 config lookup 替代 Blob

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
- `AbilityConfigRegistry` / `GameplayEffectConfigRegistry` 仍偏托管
- managed config 对象作为运行时入口，未被 Blob/Prototype 完全替代
- 目标态：Runtime hot path 只消费 `BlobAssetReference<T>` 或 generated static table

### 无头 Demo 的 Content Loading

- AutoChess 不用真实画面资源，但必须保留 Presentation outbox → binding → log marker 的完整链路
- 文件结构和 Boundary 逻辑应与真实 Demo 一致
- 后续接入真实资源时只替换 binding 层，不改 Core

## 常见陷阱

1. **BlobArray 不能嵌套 BlobArray**：只能 `BlobArray<T>`，T 不能包含另一个 `BlobArray`
2. **Baker 持有状态**：static 字段会在多次 Baking 间残留
3. **BlobAssetReference 的 Dispose**：完全由引用计数管理，不要手动调用 `Dispose`（除非是创建者且不再传递）
4. **Full Baking vs Incremental Baking 的 entity 顺序不同**：不能依赖 chunk 顺序的一致性

## 验收指标

1. Luban 配置变更能生成稳定 id 和静态定义
2. Runtime Core hot path 无 managed config lookup
3. Demo 可在无真实资源时用 log marker 证明 Cue / UI / VFX / SFX 事件链路
