# Prefab-Content管理

## 职责边界

本主题覆盖 ECS Prefab 生命周期管理、Content loading 管线、场景流式加载的资源引用规范。负责界定 Prefab 与 BlobAsset 的用途边界、跨 World 资源引用方式、场景分区（Scene Section）加载策略和自定义实例化模式。不覆盖 Baker／Baking System 的内部机制（见 `Baking-BlobAsset/_index.md`），不覆盖 runtime Entity 创建销毁的生命周期。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解 Prefab/BlobAsset 边界、Content Loading、Scene Section 策略）
2. **核心规范**（按严重度）
   - `CONTENT-01: Prefab 数量受控；静态定义优先 BlobAsset 而非 prefab.md` — P1: Prefab 数量受控；静态定义优先 BlobAsset 而非 Prefab
   - `CONTENT-02: WeakObjectReference／UnityObjectRef 用于跨 World 资源引用.md` — P1: WeakObjectReference／UnityObjectRef 用于跨 World 资源引用
   - `PRF-11: 控制 Prefab 数量；静态定义用 BlobAsset.md` — P1: 控制 Prefab 数量；静态定义用 BlobAsset
3. **模式与案例**
   - `CASE-08: EntityPrefab Reference 加载.md` — EntityPrefab Reference 加载
   - `CASE-30: CreateAdditionalEntity 单 Authoring 多 Entity.md` — CreateAdditionalEntity 单 authoring 多 entity
   - `CASE-37: LinkedEntityGroup 批量生命周期管理.md` — LinkedEntityGroup 批量生命周期管理
   - `CASE-42: Scene Section 跨引用限制 — Entity 引用只能同 Section 或 Section 0.md` — Scene Section 跨引用限制
   - `CASE-43: PostLoadCommandBuffer + ProcessAfterLoadGroup 场景实例化自定义.md` — PostLoadCommandBuffer + ProcessAfterLoadGroup 场景实例化自定义
4. **拓展阅读**（按需）
   - Baking/BlobAsset 内部机制 → `Baking-BlobAsset/_index.md`
   - 结构变化与 ECB → `结构变化-ECB/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | Prefab/BlobAsset 边界、Content Loading、Scene Section 策略详解 |
| `CONTENT-01: Prefab 数量受控；静态定义优先 BlobAsset 而非 prefab.md` | 规范 P1 | Prefab 数量受控；静态定义优先 BlobAsset 而非 Prefab |
| `CONTENT-02: WeakObjectReference／UnityObjectRef 用于跨 World 资源引用.md` | 规范 P1 | WeakObjectReference／UnityObjectRef 用于跨 World 资源引用 |
| `PRF-11: 控制 Prefab 数量；静态定义用 BlobAsset.md` | 规范 P1 | 控制 Prefab 数量；静态定义用 BlobAsset（CONTENT-01 别名） |
| `CASE-08: EntityPrefab Reference 加载.md` | 模式 | EntityPrefab Reference 加载 |
| `CASE-30: CreateAdditionalEntity 单 Authoring 多 Entity.md` | 模式 | CreateAdditionalEntity 单 authoring 多 entity |
| `CASE-37: LinkedEntityGroup 批量生命周期管理.md` | 模式 | LinkedEntityGroup 批量生命周期管理 |
| `CASE-42: Scene Section 跨引用限制 — Entity 引用只能同 Section 或 Section 0.md` | 模式 | Scene Section 跨引用限制 |
| `CASE-43: PostLoadCommandBuffer + ProcessAfterLoadGroup 场景实例化自定义.md` | 模式 | PostLoadCommandBuffer + ProcessAfterLoadGroup 场景实例化自定义 |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `CONTENT-01` | P1 | Prefab 数量受控；静态定义优先 BlobAsset | 区分"实体原型"与"配置定义"；Debugger 观察 prefabArchetypeCount |
| `CONTENT-02` | P1 | UnityObjectRef 用于跨 World 资源引用 | 搜索 IComponentData 中 Object 类型字段 |
| `PRF-11` | P1 | 控制 Prefab 数量；静态定义用 BlobAsset | 运行时查询 Prefab 标记 entity 数量；与应有 prefab 交叉比对 |

## 跨主题引用

其他主题引用了本主题的规则：
| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `CONTENT-01` | Baking-BlobAsset | Prefab vs BlobAsset 用途边界 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| Baker/Baking System 内部机制 | `Baking-BlobAsset/_index.md` |
| BlobAsset 构建和使用 | `Baking-BlobAsset/BLOB-01.md` |
| LinkedEntityGroup 生命周期 | `结构变化-ECB/_index.md` |

## 验收指标

1. Debugger 输出 prefab archetype 数量和占用 chunk 内存；Prefab 数量 ≤ 100 且仅用于实体原型。
2. GE／Ability／Tag／Buff 定义零 Prefab 使用，全部通过 BlobAsset 或 generated static table 承载。
3. 所有 `IComponentData` 中无 `Object` 类型字段；跨 World 资源引用使用 `UnityObjectRef<T>`。
4. AutoChess 场景跨 section entity 引用全部指向 section 0，零跨非零 section 引用。
5. ASC entity 通过 `LinkedEntityGroup` 统一管理 granted ability 和 active effect 的生命周期。
6. 无头 Demo 可通过 `PresentationBinding.UseLogMarker` 在零真实资源下完成事件链路验证。
7. 场景流式加载接入 Custom Section Metadata 策略。
