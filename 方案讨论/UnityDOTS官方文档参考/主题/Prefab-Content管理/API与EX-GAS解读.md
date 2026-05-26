# Prefab-Content管理: API 与 EX-GAS 解读

## 核心概念

### ECS Prefab 与 BlobAsset 的边界

在 ECS 中，Prefab 是包含完整 archetype 的 entity 模板，每个 prefab 有独立的 archetype（因为 `Prefab` component）。BlobAsset 是共享的只读数据块。两者用途不同：

| 特性 | Prefab | BlobAsset |
|---|---|---|
| 内存占用 | 至少 16 KiB chunk（独立 archetype） | 共享引用，无独立 chunk |
| 可变性 | Instantiate 后可修改 | 完全不可变 |
| 适用场景 | 实体原型（角色模型、特效实体） | 静态定义（配置表、查找表） |
| 数量限制 | 受控，大量不同 prefab 导致内存浪费 | 几乎无限（共享存储） |

### EntityPrefabReference / Content Loading

```csharp
// Authoring 端：声明 prefab 引用
public struct CEffectVfxPrefab : IComponentData
{
    public EntityPrefabReference VfxPrefab;
}

// Runtime 端：加载 prefab（Burst-compatible）
public struct PresentationBinding : IComponentData
{
    public EntityPrefabReference PrefabRef;
}
```

无头 Demo 中可用 log marker 占位，保留完整的 Presentation outbox → binding → log marker 链路。

### WeakObjectReference / UnityObjectRef

`UnityObjectRef<T>`（继承自 `WeakObjectReference` 概念）用于跨 World 的托管资源引用，Burst-compatible。适用于引用场景中的 MonoBehaviour、Texture、Mesh 等 Unity 对象，不适用于 ECS entity 引用。

### Scene Section 与场景流式加载

SubScene 可以被划分为多个 Scene Section，每个 section 独立流式加载。ECS component 的 `Entity` 字段只能引用同一 section 或 section 0 的 entity，跨 section 引用在加载时静默变为 `Entity.Null`。

---

## EX-GAS 项目解读

### Prefab 用途边界

EX-GAS 中 Prefab 仅用于以下场景：
- **角色／单位实体原型**：AutoChess 中的棋子模型 entity
- **VFX／SFX 实体模板**：技能特效、音效的实体原型
- **UI 控件实体**：HUD、血条等 UI 元素的实体模板

以下场景 **禁止** 使用 Prefab：
- GE 定义：必须使用 `GameplayEffectDefinitionBlob`（BlobAsset）
- Ability 定义：必须使用 `AbilityDefinitionBlob`（BlobAsset）
- Tag 配置：必须使用 `TagDefinitionBlob`（BlobAsset）或 generated static table
- Buff 定义：必须使用 `BuffDefinitionBlob`（BlobAsset）

### Scene Section 策略

EX-GAS AutoChess 场景的 Section 策略：
- **Section 0**：所有 ASC entity、Ability entity、全局 GameState entity —— 常驻
- **Section 1..N**：空间分区的战斗区域
- 跨 section 引用统一指向 section 0，避免静默 null

### LinkedEntityGroup 生命周期绑定链

```
ASC Entity (root)
  ├── DynamicBuffer<LinkedEntityGroup> [0] = ASC Entity (self)
  ├── DynamicBuffer<LinkedEntityGroup> [1] = Granted Ability Entity A
  ├── DynamicBuffer<LinkedEntityGroup> [2] = Granted Ability Entity B
  └── DynamicBuffer<LinkedEntityGroup> [N] = Active Effect Entity Z
```

`DestroyEntity(ascEntity)` → 自动销毁 ASC + 所有 granted ability + 所有 active effect entity。

### 无头 Demo Content Loading

- AutoChess 无头 Demo 不用真实画面资源，但保留 Presentation outbox → binding → log marker 的完整链路
- `PresentationBinding` component 中使用 `EntityPrefabReference` 声明依赖，但 `UseLogMarker` 为 true 时跳过实例化

### 代码目录映射

| 机制 | 主要代码目录 | 文档关联 |
|---|---|---|
| Prefab 定义 | `Assets/GAS/Runtime/Ability/`、`Assets/GAS/Runtime/Effect/` | CONTENT-01, PRF-11 |
| Content Loading | `Assets/GAS/Runtime/System/Event/SPresentationOutboxProjection.cs` | CONTENT-02 |
| LinkedEntityGroup | `Assets/GAS/Runtime/Effect/Component/Dynamic/CActiveEffectStore.cs` | CASE-37 |
| Scene Section | `Assets/GAS/Runtime/System/SystemGroup/` | CASE-42, CASE-43, CASE-44 |
| WeakObjectReference | Asset reference component 定义 | CONTENT-02 |

---

## 常见陷阱

1. **Prefab 当作配置数据**：每个 GE／Ability 定义做一个 prefab，消耗大量 chunk 内存（16 KiB × N）。
2. **Scene Section 跨引用无声失效**：ECS component 中 `Entity` 字段跨 section 引用在加载时被静默设为 `Entity.Null`。
3. **Prefab 实例继承 SceneSection**：加载在 section N 的 prefab 实例自动获得 `SceneSection` component，卸载 section N 时所有实例被销毁。
4. **LinkedEntityGroup 嵌套不生效**：LinkedEntityGroup 不支持递归嵌套。
5. **托管引用在 IComponentData 中**：在 component 中使用 `Object` 字段导致 archetype 包含托管类型。
6. **ProcessAfterLoadGroup 在错误 World 中运行**：PostLoadCommandBuffer 的 ECB playback 和 `ProcessAfterLoadGroup` 在 streaming world 中运行。

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `performance-chunk-allocations.html` | 每个 prefab 至少 16 KiB chunk；静态数据用 BlobAsset | CONTENT-01, PRF-11 |
| `baking-prefabs.md` | Prefab 需 Baker 注册；runtime 通过 EntityPrefabReference 加载 | CONTENT-01 |
| `systems-data.md` | UnityObjectRef<T> Burst-compatible 托管引用 | CONTENT-02, CASE-45 |
| `linked-entity-group.md` | LinkedEntityGroup 批量生命周期管理 | CASE-37 |
| `streaming-scene-sections.md` | Scene Section 跨引用限制 | CASE-42 |
| `streaming-scene-instancing.md` | PostLoadCommandBuffer + ProcessAfterLoadGroup | CASE-43 |
| `streaming-meta-entities.md` | Section meta entity 自定义元数据 | CASE-44 |
