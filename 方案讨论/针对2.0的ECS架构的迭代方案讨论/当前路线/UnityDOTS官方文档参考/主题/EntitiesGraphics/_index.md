# EntitiesGraphics

## 职责边界

本领域维护 Entities Graphics 在 EX-GAS 中的边界：Runtime 渲染 entity 创建规范、无头/有头 Demo 等价性、表现层与 Core simulation 的分离。Entities Graphics 只负责 Presentation/Boundary 层的表现桥接，不涉及 Core simulation 数据。不覆盖 URP 配置、shader 开发、材质美术管线等渲染技术子领域。

## 先读关系

1. **API 与 EX-GAS 解读** → `API与EX-GAS解读.md`（必读，理解 Entities Graphics 定位、无头/有头等价性、Presentation Outbox 链路）
2. **核心规范**（按严重度）
   - `GFX-01: Core simulation 不得直接添加 Graphics component.md` — P0: Core simulation 不得直接添加 Graphics component
   - `GFX-02: Runtime 创建渲染 entity 必须使用 RenderMeshUtility.AddComponents.md` — P0: Runtime 创建渲染 entity 必须使用 RenderMeshUtility.AddComponents
   - `GFX-03: Presentation group 内禁止结构变化.md` — P1: Presentation group 内禁止结构变化
   - `GFX-04: 无头模式与有头模式的 Core simulation hash 必须相同.md` — P1: 无头模式与有头模式的 Core simulation hash 必须相同
   - `GFX-05: Rendered profile 必须与 Core 成本独立计时.md` — P1: Rendered profile 必须与 Core 成本独立计时
3. **模式与案例**
   - `CASE-10: Graphics runtime create 限于 Presentation 层.md` — Graphics runtime create 限于 Presentation 层
4. **拓展阅读**（按需）
   - Prefab 资源加载 → `Prefab-Content管理/_index.md`
   - 数据流确定性 → `数据流-系统生命周期/_index.md`

## 文件清单

| 文件 | 类型 | 说明 |
|------|------|------|
| `API与EX-GAS解读.md` | API+解读 | Entities Graphics 定位、无头/有头等价性、Presentation Outbox 链路详解 |
| `GFX-01: Core simulation 不得直接添加 Graphics component.md` | 规范 P0 | Core simulation 不得直接添加 Graphics component |
| `GFX-02: Runtime 创建渲染 entity 必须使用 RenderMeshUtility.AddComponents.md` | 规范 P0 | Runtime 创建渲染 entity 必须使用 RenderMeshUtility.AddComponents |
| `GFX-03: Presentation group 内禁止结构变化.md` | 规范 P1 | Presentation group 内禁止结构变化 |
| `GFX-04: 无头模式与有头模式的 Core simulation hash 必须相同.md` | 规范 P1 | 无头/有头 Core simulation hash 必须相同 |
| `GFX-05: Rendered profile 必须与 Core 成本独立计时.md` | 规范 P1 | Rendered profile 与 Core 成本独立计时 |
| `CASE-10: Graphics runtime create 限于 Presentation 层.md` | 模式 | Graphics runtime create 限于 Presentation 层 |

## 规则总览

| 编号 | 严重度 | 规则摘要 | 检查方法 |
|------|--------|----------|----------|
| `GFX-01` | P0 | Core simulation 不得直接添加 Graphics component | Grep `RenderMesh`/`MaterialMeshInfo`/`RenderBounds` 在 Core layer |
| `GFX-02` | P0 | Runtime 创建渲染 entity 使用 RenderMeshUtility.AddComponents | 搜索 `AddComponent<RenderMesh` 等手动拼装模式 |
| `GFX-03` | P1 | Presentation group 内禁止结构变化 | 搜索 `CreateEntity`/`DestroyEntity` 在 `PresentationSystemGroup` 标记的 system 中 |
| `GFX-04` | P1 | 无头/有头 Core simulation hash 必须相同 | 检查表现层 system 对 Core component 的写访问 |
| `GFX-05` | P1 | Rendered profile 与 Core 成本独立计时 | 确认 renderTime 与 coreTime 使用独立计时器 |

## 跨主题引用

| 规则 | 引用者 | 使用场景 |
|------|--------|----------|
| `GFX-01` | System-World-SystemGroup | Core phase 不得引用 Graphics component |
| `GFX-04` | Prefab-Content管理 | 无头 Demo Content Loading 模式 |

## 拓展阅读路径

| 如果你想知道... | 去 |
|----------------|-----|
| Prefab 资源加载和 binding | `Prefab-Content管理/_index.md` |
| Physics 与 Graphics 的交互 | `UnityPhysics/_index.md` |

## 验收指标

1. Headless profile 记录 presentation outbox count、cue count、UI/VFX/SFX marker。
2. Rendered profile 额外记录 batch、draw call、material override、render world 信息。
3. Core simulation hash 不受是否启用 Entities Graphics 影响（有头/无头一致性验证）。
4. Core layer 零 Entities Graphics 专有 component 引用。
5. 所有 runtime 渲染 entity 创建路径使用 `RenderMeshUtility.AddComponents`。
