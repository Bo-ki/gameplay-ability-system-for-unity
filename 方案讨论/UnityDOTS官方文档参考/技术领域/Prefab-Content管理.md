# Prefab-Content管理

## 职责

覆盖 ECS Prefab 生命周期管理、Content loading 管线、场景流式加载的资源引用规范。负责界定 Prefab 与 BlobAsset 的用途边界、跨 World 资源引用方式、场景分区（Scene Section）加载策略和自定义实例化模式。不覆盖 Baker／Baking System 的内部机制（见 `Baking-BlobAsset.md`），不覆盖 runtime Entity 创建销毁的生命周期。

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

Authoring 端通过 `EntityPrefabReference` 声明 prefab 引用，runtime 通过 `PrefabLoadResult` 获取加载结果：

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

无头 Demo 中可用 log marker 占位，保留完整的 Presentation outbox → binding → log marker 链路，后续接入真实资源时只替换 binding 层。

### WeakObjectReference / UnityObjectRef

`UnityObjectRef<T>`（继承自 `WeakObjectReference` 概念）用于跨 World 的托管资源引用，Burst-compatible。适用于引用场景中的 MonoBehaviour、Texture、Mesh 等 Unity 对象，不适用于 ECS entity 引用。

### Scene Section 与场景流式加载

SubScene 可以被划分为多个 Scene Section，每个 section 独立流式加载。ECS component 的 `Entity` 字段只能引用同一 section 或 section 0 的 entity，跨 section 引用在加载时静默变为 `Entity.Null`。

## 编写规范

### CONTENT-01: Prefab 数量受控；静态定义优先 BlobAsset 而非 prefab

**声明：** 每个 prefab 占用至少 16 KiB 的独立 chunk。大量不同的 prefab 导致显著内存浪费。静态定义数据应用 BlobAsset 或 generated static table。

- **来源：** `performance-chunk-allocations.html` (P1-07)，DOTS 编写规范与性能陷阱文档。
- **为什么：** Prefab component 使每个 prefab 成为独立 archetype。N 个不同 prefab = N 个独立 archetype = N × 16 KiB chunk 至少。对于配置定义类数据（技能、效果、Buff），使用 Entity Prefab 承载是严重的内存浪费 —— 一个仅有 `IComponentData` 配置数据的 prefab 浪费 16 KiB chunk 而实际 payload 可能仅数百字节。BlobAsset 允许多 entity 共享同一数据块，无 chunk 开销。
- **EX-GAS 诊断：** GE 定义、Ability 定义、Tag 配置不应是 prefab，而应是 BlobAsset 或 static data table。仅需要实体原型（如角色模型、特效 entity）才使用 prefab。Debugger 应输出 prefab archetype 数量和总 chunk 内存。
- **检查方法：** 审查所有使用 `EntityPrefabReference` 的场景。区分"实体原型"（允许 prefab）与"配置定义"（必须用 BlobAsset）。在 Debugger 中观察 `prefabArchetypeCount`。

### CONTENT-02: WeakObjectReference／UnityObjectRef 用于跨 World 资源引用

**声明：** Managed Unity 对象引用必须使用 `UnityObjectRef<T>`（Burst-compatible）或 `WeakObjectReference`，不能使用常规 C# 引用字段。

- **来源：** `systems-data.md` (CASE-45 上下文)，官方案例模式高级文档。
- **为什么：** ECS component 中的常规 C# object 引用会导致 archetype 包含托管 component，chunk 失去 Burst-compatible 特性且增加 GC 压力。`UnityObjectRef<T>` 是 Burst-compatible 的间接引用，在内部使用 `WeakObjectReference` 机制跟踪 Unity 对象生命周期，不会阻止对象被卸载。
- **EX-GAS 诊断：** 所有跨 World 的资源引用（Material、Texture、AudioClip、GameObject Prefab）必须在 ECS component 中使用 `UnityObjectRef<T>`。非托管资源引用（如 entity、BlobAssetReference）使用原生类型。
- **检查方法：** 搜索 ECS component 中 `IComponentData` 或 `IBufferElementData` 的 `Object` 类型字段或泛型 object 引用。确认替换为 `UnityObjectRef<T>`。

### PRF-11: 控制 Prefab 数量；静态定义用 BlobAsset

**声明 （P1-07 别名）：** 同 CONTENT-01。Prefab 数量必须审计，静态定义数据禁止使用 Entity Prefab 承载。

- **来源：** `performance-chunk-allocations.html`，DOTS 编写规范与性能陷阱文档。
- **为什么：** （同 CONTENT-01 为什么）Prefab 独立 archetype 的 16 KiB chunk 开销在大量定义场景下不可接受。静态数据 BlobAsset 共享无需额外 chunk。
- **EX-GAS 诊断：** Debugger 报告 prefab archetype 数量和占用 chunk 内存。Prefab 数量超出 100 时触发告警审查。
- **检查方法：** 运行时查询所有 `Prefab` 标记的 entity 数量。与 "应有 prefab" 列表交叉比对发现冗余。

## 模式与反模式

### 正确模式

1. **EntityPrefab Reference 加载（CASE-08）**：baked prefab 包含完整 archetype，通过 `EntityPrefabReference` 在运行时加载和实例化。适用于角色模型实体、特效实体、UI 实体等真实资源模板。GAS 场景：VFX prefab、SFX prefab、角色 rendering entity。
2. **CreateAdditionalEntity 单 authoring 多 entity（CASE-30）**：Baker 中使用 `CreateAdditionalEntity(TransformUsageFlags, entityName)` 从一个 authoring 产出多个 runtime entity。GAS 场景：一个 AbilityDefinition authoring → ability entity + 多个 GE entity + cue entity。
3. **LinkedEntityGroup 批量生命周期管理（CASE-37）**：root entity 持有 `DynamicBuffer<LinkedEntityGroup>`，`DestroyEntity(root)` 自动销毁 buffer 中所有关联 entity。GAS 场景：ASC entity + granted ability/effect entity 统一销毁（销毁 ASC → 自动清理所有关联 entity）。注意：第一个元素必须是根 entity 自身；不与 Transform hierarchy 递归；不可嵌套。
4. **PostLoadCommandBuffer + ProcessAfterLoadGroup 场景实例化自定义（CASE-43）**：场景加载在独立 streaming world 中进行，加载完成后 `ProcessAfterLoadGroup` 在 streaming world 中运行，允许对场景实例进行 per-instance 修改。GAS 场景：AutoChess 从同一场景文件创建多个战斗实例，每个实例不同初始位置和配置——通过 `SceneLoadFlags.NewInstance` + `PostLoadCommandBuffer`（创建 per-instance 配置 entity）+ `ProcessAfterLoadGroup`（读取配置并应用偏移）实现。
5. **Custom Section Metadata（CASE-44）**：Baking System 中通过 `SerializeUtility.GetSceneSectionEntity` 获取 section meta entity 并附加自定义 ECS component 作为元数据（包围盒、PVS 信息、加载条件），runtime 通过 `ResolvedSectionEntity` buffer 查询。GAS 场景：大战场按空间分区流式加载——section meta entity 存储包围盒，runtime 根据玩家位置决定加载／卸载。
6. **无头 Demo 的 Content Loading 模式**：保留完整链路 Presentation outbox → binding → log marker。文件结构和 Boundary 逻辑与真实 Demo 一致，后续接入真实资源时只替换 binding 层，不改 Core。
7. **`UnityObjectRef<T>` 声明 managed 资源依赖**：将 Material、Texture、AudioClip 等 Unity 对象引用封装在 `UnityObjectRef<T>` 中放入 ECS component，保持 chunk Burst-compatible。

### 反模式

1. **配置定义数据使用 Prefab**（违反 CONTENT-01／PRF-11）：将 GE 定义、Ability 定义等纯配置数据做成 Entity Prefab，浪费 16 KiB chunk 每定义。替代：使用 BlobAsset 或 generated static table。
2. **跨 Section Entity 引用**（违反 CASE-42 约束）：ECS component 中 `Entity` 字段引用不同 Scene Section 的 entity。加载时被静默设为 `Entity.Null`，无编译或运行时警告。替代：将所有需要跨 section 引用的 entity 放入 section 0，或放在同一 section。
3. **Prefab 实例继承 SceneSection 未注意**（CASE-42 延伸）：卸载 section 时所有关联 prefab 实例被销毁。若需持久化需手动移除 `SceneSection` component。
4. **常规 C# 引用在 IComponentData 中**（违反 CONTENT-02）：在 `IComponentData` 中使用 `Object` 类型或直接 `Material` 引用字段，导致 archetype 包含托管 component，chunk 失去 Burst-compatible。替代：使用 `UnityObjectRef<T>`。
5. **LinkedEntityGroup 嵌套**（CASE-37 限制）：LinkedEntityGroup A 包含 B 的内容，认为嵌套生效。实际上 LinkedEntityGroup 不支持嵌套，B 中的 linked entities 不会被 A 的 DestroyEntity 传播到。替代：将生命周期范围的所有 entity 平铺到一个 buffer 中。

## EX-GAS 项目解读

### Prefab 用途边界

EX-GAS 中 Prefab 仅用于以下场景：
- **角色／单位实体原型**：AutoChess 中的棋子模型 entity，包含 rendering component、transform、gameplay tag
- **VFX／SFX 实体模板**：技能特效、音效的实体原型，通过 `EntityPrefabReference` 在 Cue 触发时实例化
- **UI 控件实体**：HUD、血条等 UI 元素的实体模板

以下场景 **禁止** 使用 Prefab：
- GE 定义：必须使用 `GameplayEffectDefinitionBlob`（BlobAsset）
- Ability 定义：必须使用 `AbilityDefinitionBlob`（BlobAsset）
- Tag 配置：必须使用 `TagDefinitionBlob`（BlobAsset）或 generated static table
- Buff 定义：必须使用 `BuffDefinitionBlob`（BlobAsset）

### Scene Section 策略

EX-GAS AutoChess 场景的 Section 策略：
- **Section 0**：所有 ASC entity、Ability entity、全局 GameState entity —— 常驻，被所有其他 section 引用
- **Section 1..N**：空间分区的战斗区域 —— 每个 section 包含该区域的 unit、obstacle、terrain
- 跨 section 引用（如 Ability → ASC）统一指向 section 0，避免静默 null
- Section meta entity 附带包围盒和队伍信息，用于运行时智能加载

### LinkedEntityGroup 生命周期绑定链

```
ASC Entity (root)
  ├── DynamicBuffer<LinkedEntityGroup> [0] = ASC Entity (self)
  ├── DynamicBuffer<LinkedEntityGroup> [1] = Granted Ability Entity A
  ├── DynamicBuffer<LinkedEntityGroup> [2] = Granted Ability Entity B
  ├── ...
  └── DynamicBuffer<LinkedEntityGroup> [N] = Active Effect Entity Z
```

`DestroyEntity(ascEntity)` → 自动销毁 ASC + 所有 granted ability + 所有 active effect entity。

### 无头 Demo Content Loading

- AutoChess 无头 Demo 不用真实画面资源，但必须保留 Presentation outbox → binding → log marker 的完整链路
- `PresentationBinding` component 中使用 `EntityPrefabReference` 声明依赖，但 `UseLogMarker` 为 true 时跳过实例化，仅输出结构化日志
- 后续接入真实资源时：将 `UseLogMarker` 设为 false + 替换 `PresentationBinding` 中的 `EntityPrefabReference` 指向真实 prefab。无需修改 Core 层代码

### 代码目录映射

| 机制 | 主要代码目录 | 文档关联 |
|---|---|---|
| Prefab 定义 | `Assets/GAS/Runtime/Ability/`、`Assets/GAS/Runtime/Effect/` | CONTENT-01, PRF-11 |
| Content Loading | `Assets/GAS/Runtime/System/Event/SPresentationOutboxProjection.cs` | CONTENT-02 |
| LinkedEntityGroup | `Assets/GAS/Runtime/Effect/Component/Dynamic/CActiveEffectStore.cs` | CASE-37 |
| Scene Section | `Assets/GAS/Runtime/System/SystemGroup/` | CASE-42, CASE-43, CASE-44 |
| WeakObjectReference | Asset reference component 定义 | CONTENT-02 |

## 常见陷阱

1. **Prefab 当作配置数据**：每个 GE／Ability 定义做一个 prefab，消耗大量 chunk 内存（16 KiB × N）。实际上这些数据应使用共享 BlobAsset。
2. **Scene Section 跨引用无声失效**：ECS component 中 `Entity` 字段跨 section 引用在加载时被静默设为 `Entity.Null`，无编译或运行时警告，导致逻辑静默错误。
3. **Prefab 实例继承 SceneSection**：加载在 section N 的 prefab 实例自动获得 `SceneSection` component，卸载 section N 时所有实例被销毁。如需持久化需手动移除该 component。
4. **LinkedEntityGroup 嵌套不生效**：LinkedEntityGroup 不支持递归嵌套。如需复杂生命周期树，将所有叶子 entity 平铺在根 entity 的 buffer 中。
5. **托管引用在 IComponentData 中**：在 component 中使用 `Object` 字段或直接 `Texture` 引用导致 archetype 包含托管类型，chunk 失去 Burst-compatible。应使用 `UnityObjectRef<T>`。
6. **Managed component 的 GC 压力**：Content loading 中不当使用托管对象导致每帧 GC alloc，影响 frame time 稳定性。
7. **ProcessAfterLoadGroup 在错误 World 中运行**：PostLoadCommandBuffer 的 ECB playback 和 `ProcessAfterLoadGroup` 在 streaming world 中运行，不是在 main world。访问 main world 数据需要显式传递引用。

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `performance-chunk-allocations.html` | 每个 prefab 至少 16 KiB chunk；大量 prefab 内存浪费严重；静态数据用 BlobAsset | CONTENT-01, PRF-11 |
| `baking-prefabs.md` | Prefab 需 Baker 注册；runtime 通过 EntityPrefabReference 加载；Burst-compatible | CONTENT-01 |
| `systems-data.md` | UnityObjectRef<T> Burst-compatible 托管引用；系统级数据存 component 而非 system 字段 | CONTENT-02, CASE-45 |
| `linked-entity-group.md` | LinkedEntityGroup 批量生命周期管理；第一个元素必须是根 entity 自身；不可嵌套 | CASE-37 |
| `streaming-scene-sections.md` | Scene Section 跨引用限制（同 section 或 section 0）；prefab 实例继承 SceneSection | CASE-42 |
| `streaming-scene-instancing.md` | PostLoadCommandBuffer + ProcessAfterLoadGroup 场景实例化自定义 | CASE-43 |
| `streaming-meta-entities.md` | Section meta entity 可用 `SerializeUtility.GetSceneSectionEntity` 获取 | CASE-44 |
| `components-buffer-introducing.html` | DynamicBuffer 管理可变数据对比 | PRF-11 (context) |
| `blob-assets-create.md` | BlobAsset 构建和共享引用模式；对比 Prefab 的不同语义 | CONTENT-01 (context) |

## 验收指标

1. Debugger 输出 prefab archetype 数量和占用 chunk 内存；Prefab 数量 ≤ 100 且仅用于实体原型。
2. GE／Ability／Tag／Buff 定义零 Prefab 使用，全部通过 BlobAsset 或 generated static table 承载。
3. 所有 `IComponentData` 中无 `Object` 类型字段；跨 World 资源引用使用 `UnityObjectRef<T>`。
4. AutoChess 场景跨 section entity 引用全部指向 section 0，零跨非零 section 引用。
5. ASC entity 通过 `LinkedEntityGroup` 统一管理 granted ability 和 active effect 的生命周期，销毁 ASC 时自动清理所有关联 entity。
6. 无头 Demo 可通过 `PresentationBinding.UseLogMarker` 在零真实资源下完成事件链路验证，切换为真实资源时只改 binding 层。
7. 场景流式加载接入 Custom Section Metadata 策略，section 根据运行时条件（位置、加载预算）智能加载／卸载。
