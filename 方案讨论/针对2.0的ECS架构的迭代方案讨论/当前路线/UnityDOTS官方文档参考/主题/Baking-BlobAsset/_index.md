# Baking-BlobAsset

## 职责边界

本主题覆盖 Baker、Baking System、BlobAsset 三个 Baking 期核心机制的编码规范与模式约束。负责静态定义数据从 authoring 到 ECS runtime 的转换管线。不覆盖 Prefab／Content 加载（见 `Prefab-Content管理/_index.md`），不覆盖 runtime 动态数据存储。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解 Baker/Baking System/BlobAsset 机制和 Definition & Generation Layer 设计）
2. **核心规范**（按严重度）
   - `BAKE-01: Baker 只添加不读取 — Baker 间无依赖.md` — P0: Baker 只添加不读取，Baker 间无依赖
   - `BAKE-02: Baker 必须无状态 — 单例实例、Bake() 多次调用且无序.md` — P0: Baker 必须无状态，单例实例多次调用且无序
   - `BAKE-03: Baking System 必须手动追踪依赖和增量还原.md` — P0: Baking System 必须手动追踪依赖和增量还原
   - `BLOB-01: BlobAsset 用于 immutable 静态定义；runtime 只读.md` — P1: BlobAsset 用于 immutable 静态定义；runtime 只读
   - `BLOB-02: BlobBuilder 在 Baking 或初始化时使用；不在 hot path 构建.md` — P1: BlobBuilder 在 Baking 或初始化时使用；不在 hot path 构建
3. **模式与案例**
   - `CASE-07: Baker + Blob 定义管线.md` — Baker + Blob 定义管线
   - `CASE-24: BlobBuilder 标准构建模式.md` — BlobBuilder 标准构建模式
   - `CASE-31: DependsOn() 在 Early-Out 之前.md` — DependsOn() 在 Early-Out 之前
   - `CASE-32: TemporaryBakingType + Baking System Burst 计算.md` — TemporaryBakingType + Baking System Burst 计算
   - `CASE-39: Baker 只添加不读取 — Baker 间无依赖.md` — Baker 只添加不读取 — Baker 间无依赖
   - `CASE-40: Baker 必须无状态 — 禁止在 Baker 实例中缓存数据.md` — Baker 必须无状态 — 禁止缓存数据
   - `CASE-41: Baking System 必须手动追踪依赖和增量还原.md` — Baking System 必须手动追踪依赖和增量还原
   - `CASE-44: Custom Section Metadata — 烘焙阶段向 Section Meta Entity 附加自定义元数据.md` — Custom Section Metadata
4. **拓展阅读**（按需）
   - Prefab/Content 加载 → `Prefab-Content管理/_index.md`
   - 结构变化与 ECB → `结构变化-ECB/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | Baker/Baking System/BlobAsset 机制详解 + EX-GAS Definition 层设计 |
| `BAKE-01: Baker 只添加不读取 — Baker 间无依赖.md` | 规范 P0 | Baker 只添加不读取，Baker 间无依赖 |
| `BAKE-02: Baker 必须无状态 — 单例实例、Bake() 多次调用且无序.md` | 规范 P0 | Baker 必须无状态，单例实例、Bake() 多次调用且无序 |
| `BAKE-03: Baking System 必须手动追踪依赖和增量还原.md` | 规范 P0 | Baking System 必须手动追踪依赖和增量还原 |
| `BLOB-01: BlobAsset 用于 immutable 静态定义；runtime 只读.md` | 规范 P1 | BlobAsset 用于 immutable 静态定义；runtime 只读 |
| `BLOB-02: BlobBuilder 在 Baking 或初始化时使用；不在 hot path 构建.md` | 规范 P1 | BlobBuilder 在 Baking 或初始化时使用；不在 hot path 构建 |
| `CASE-07: Baker + Blob 定义管线.md` | 模式 | Baker + Blob 定义管线 |
| `CASE-24: BlobBuilder 标准构建模式.md` | 模式 | BlobBuilder 标准构建模式 |
| `CASE-31: DependsOn() 在 Early-Out 之前.md` | 模式 | DependsOn() 在 Early-Out 之前 |
| `CASE-32: TemporaryBakingType + Baking System Burst 计算.md` | 模式 | TemporaryBakingType + Baking System Burst 计算 |
| `CASE-39: Baker 只添加不读取 — Baker 间无依赖.md` | 模式 | Baker 只添加不读取 |
| `CASE-40: Baker 必须无状态 — 禁止在 Baker 实例中缓存数据.md` | 模式 | Baker 必须无状态 |
| `CASE-41: Baking System 必须手动追踪依赖和增量还原.md` | 模式 | Baking System 手动追踪依赖 |
| `CASE-44: Custom Section Metadata — 烘焙阶段向 Section Meta Entity 附加自定义元数据.md` | 模式 | Custom Section Metadata |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `BAKE-01` | P0 | Baker 只添加不读取，Baker 间无依赖 | 审查所有 Baker 的 `Bake()` 方法，确认无 `GetComponent`/`SetComponent` |
| `BAKE-02` | P0 | Baker 必须无状态，Bake() 多次调用且无序 | 审查所有 Baker 类确保无实例字段（除 `readonly` 常量外） |
| `BAKE-03` | P0 | Baking System 必须手动追踪依赖和增量还原 | 审查 `[WorldSystemFilter(BakingSystem)]` ISystem 的 `DependsOn()` 覆盖 |
| `BLOB-01` | P1 | BlobAsset 用于 immutable 静态定义；runtime 只读 | 审查 BlobAsset 写入路径，确认只在 Baking/OnCreate 中写入 |
| `BLOB-02` | P1 | BlobBuilder 不在 runtime hot path 使用 | Grep 搜索 `new BlobBuilder` 在 Runtime Core 目录 |

## 跨主题引用

其他主题引用了本主题的规则：
| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `BAKE-01` | Prefab-Content管理 | Baker 数据搬运职责边界 |
| `BAKE-03` | Prefab-Content管理 | Baking System 与 Prefab 注册的关系 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| Prefab 如何加载 | `Prefab-Content管理/_index.md` |
| ECB 在 Baking 中的使用 | `结构变化-ECB/_index.md` |
| Runtime 数据承载策略 | `Store选型-数据承载策略/_index.md` |

## 验收指标

1. Luban 配置变更能生成稳定 id 和静态定义，经过 Baker → BlobAsset 管线产出 runtime 可用数据。
2. Runtime Core hot path 无 managed config lookup，所有静态定义通过 `BlobAssetReference<T>` 或 generated static table 访问。
3. 所有 Baker 类零 `GetComponent`／`SetComponent` 调用，零实例状态字段。
4. 所有 Baking System 显式声明 `DependsOn()` 并实现增量还原逻辑。
5. 代码中无 `new BlobBuilder` 出现在 Runtime Core hot path（Baking 或初始化 System 的 `OnCreate` 除外）。
6. GE／Ability definition 不通过 Prefab 承载，仅通过 BlobAsset 或静态 component 定义。
