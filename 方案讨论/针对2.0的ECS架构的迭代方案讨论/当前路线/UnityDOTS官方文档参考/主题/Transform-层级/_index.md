# Transform-层级

## 职责边界

本领域维护 ECS Transform 系统在 EX-GAS 中的正确使用边界：层级关系管理（Child/Parent）、世界坐标获取时效性（LocalToWorld）、烘焙优化（TransformUsageFlags）、自定义变换（WriteGroup）。不覆盖标准 transform 操作（LocalTransform 的 Position/Rotation/Scale 读写——这些按常规 ECS component 读写即可）。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解 ECS Transform 分层架构、LocalToWorld 时效性陷阱、层级管理规范）
2. **核心规范**（按严重度）
   - `TRF-01: SimulationSystemGroup 中禁止直接读 LocalToWorld 做 gameplay 决策.md` — P0: SimulationSystemGroup 中禁止直接读 LocalToWorld 做 gameplay 决策
   - `TRF-02: 禁止直接修改 Child _ PreviousParent.md` — P1: 禁止直接修改 Child / PreviousParent
   - `TRF-03: Child Buffer 迭代顺序不确定，禁止依赖 sibling index.md` — P1: Child Buffer 迭代顺序不确定，禁止依赖 sibling index
   - `TRF-04: 使用正确 TransformUsageFlags 避免冗余 transform component.md` — P1: 使用正确 TransformUsageFlags 避免冗余 transform component
   - `TRF-05: Custom Transform 使用 WriteGroup + ManualOverride.md` — P1: Custom Transform 使用 WriteGroup + ManualOverride
   - `PRF-18: LocalToWorld 在 SimulationSystemGroup 可能过期；用 ComputeWorldTransformMatrix.md` — P1: LocalToWorld 在 SimulationSystemGroup 可能过期（跨主题引用）
   - `PRF-28: 禁止直接修改 Child _ PreviousParent.md` — P1: 禁止直接修改 Child / PreviousParent（跨主题引用）
   - `PRF-31: ECS Child Buffer 迭代顺序不确定.md` — P1: Child Buffer 迭代顺序不确定（跨主题引用）
3. **模式与案例**
   - `CASE-19: TransformUsageFlags 烘焙优化.md` — TransformUsageFlags 烘焙优化
   - `CASE-29: Custom Transform via WriteGroup + ManualOverride.md` — Custom Transform via WriteGroup + ManualOverride
4. **拓展阅读**（按需）
   - Baking 中 Transform 设置 → `Baking-BlobAsset/_index.md`
   - 结构变化与 entity 层级 → `结构变化-ECB/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | ECS Transform 分层架构、LocalToWorld 时效性、层级管理详解 |
| `TRF-01: SimulationSystemGroup 中禁止直接读 LocalToWorld 做 gameplay 决策.md` | 规范 P0 | SimulationSystemGroup 中禁止直接读 LocalToWorld |
| `TRF-02: 禁止直接修改 Child _ PreviousParent.md` | 规范 P1 | 禁止直接修改 Child / PreviousParent |
| `TRF-03: Child Buffer 迭代顺序不确定，禁止依赖 sibling index.md` | 规范 P1 | Child Buffer 迭代顺序不确定 |
| `TRF-04: 使用正确 TransformUsageFlags 避免冗余 transform component.md` | 规范 P1 | 使用正确 TransformUsageFlags |
| `TRF-05: Custom Transform 使用 WriteGroup + ManualOverride.md` | 规范 P1 | Custom Transform 使用 WriteGroup + ManualOverride |
| `PRF-18: LocalToWorld 在 SimulationSystemGroup 可能过期；用 ComputeWorldTransformMatrix.md` | 规范 P1 | LocalToWorld 在 SimulationSystemGroup 可能过期 |
| `PRF-28: 禁止直接修改 Child _ PreviousParent.md` | 规范 P1 | 禁止直接修改 Child/PreviousParent |
| `PRF-31: ECS Child Buffer 迭代顺序不确定.md` | 规范 P1 | Child Buffer 迭代顺序不确定 |
| `CASE-19: TransformUsageFlags 烘焙优化.md` | 模式 | TransformUsageFlags 烘焙优化 |
| `CASE-29: Custom Transform via WriteGroup + ManualOverride.md` | 模式 | Custom Transform via WriteGroup + ManualOverride |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `TRF-01` | P0 | SimulationSystemGroup 中禁止直接读 LocalToWorld | Grep `LocalToWorld` 在 Runtime Core 下的 `.Position`/`.Rotation` 读取 |
| `TRF-02` | P1 | 禁止直接修改 Child / PreviousParent | 搜索 `Buffer<Child>` 的 Add/Remove/Clear 操作 |
| `TRF-03` | P1 | Child Buffer 迭代顺序不确定 | 搜索 `Buffer<Child>` 或 `DynamicBuffer<Child>` 的索引访问 |
| `TRF-04` | P1 | 使用正确 TransformUsageFlags 避免冗余 | 审计所有 Baker 的 TransformUsageFlags 参数 |
| `TRF-05` | P1 | Custom Transform 使用 WriteGroup + ManualOverride | 搜索 `[WriteGroup(typeof(LocalToWorld))]` 确认与 `ManualOverride` 配对 |
| `PRF-18` | P1 | LocalToWorld 在 SimulationSystemGroup 可能过期 | 同 TRF-01 检查方法 |
| `PRF-28` | P1 | 禁止直接修改 Child/PreviousParent | 同 TRF-02 检查方法 |
| `PRF-31` | P1 | Child Buffer 迭代顺序不确定 | 同 TRF-03 检查方法 |

## 跨主题引用

本主题承载的 PRF 规则同时被其他主题引用：
| 规则 | 来源 | 使用场景 |
|------|------|----------|
| `PRF-18` | 13-DOTS编写规范与性能陷阱 | P1-10 规范条目 |
| `PRF-28` | 13-DOTS编写规范与性能陷阱 | Transform 使用规范条目 |
| `PRF-31` | 13-DOTS编写规范与性能陷阱 | Child Buffer 顺序规范条目 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| Baking 中如何设置 Transform | `Baking-BlobAsset/_index.md` |
| LinkedEntityGroup 生命周期管理 | `结构变化-ECB/_index.md` |

## 验收指标

1. Runtime Core 中 `SimulationSystemGroup` 路径零直接 `LocalToWorld.Position` / `.Rotation` 读取。
2. 应用代码中零 `Buffer<Child>` / `DynamicBuffer<Child>` 的 Add/Remove/Set 操作。
3. 无依赖 `Child` buffer index 确定性的 gameplay 逻辑。
4. 所有 Baker 的 `TransformUsageFlags` 通过 Code Review 确认最小化。
5. 自定义 transform 路径均有 `[WriteGroup]` + `ManualOverride` 配对。
