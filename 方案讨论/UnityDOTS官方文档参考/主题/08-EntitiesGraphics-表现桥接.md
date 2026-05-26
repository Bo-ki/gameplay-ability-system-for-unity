# 08 Entities Graphics 表现桥接

## 职责

本主题维护 Entities Graphics 在 EX-GAS 中的边界：真实资源表现接入、无头 log marker 等价性、rendered profile 的计时拆分。

## 核心概念详解

### Entities Graphics 的定位

Entities Graphics 是 ECS 到 Unity 渲染管线的桥接层，通过 `BatchRendererGroup`（BRG）和 `DOTS Instancing` 实现高数量渲染。

**关键约束：**
- URP 仅支持 Forward+（2022.3+）
- 不支持多个 render World
- Runtime 创建渲染 entity 必须用 `RenderMeshUtility.AddComponents` 或 baked prefab
- Presentation group 有结构变化限制

### Runtime 渲染 Entity 创建

```csharp
// 正确方式：使用官方 API
var renderEntity = ecb.CreateEntity();
RenderMeshUtility.AddComponents(renderEntity, ecb, 
    new RenderMeshDescription(renderMeshArray, materialMeshInfo));

// 错误方式：手动拼装内部组件
// ecb.AddComponent<RenderMeshArray>(...);  // 缺少必需的内部组件
```

### RenderMeshArray / MaterialMeshInfo

```csharp
// RenderMeshArray：共享的 mesh + material 集合
// MaterialMeshInfo：选择 RenderMeshArray 中的哪个 mesh 和 material
// 支持 Burst-compatible 选择，不依赖托管对象
```

### BRG / DOTS Instancing

- `BatchRendererGroup`（BRG）是底层渲染 API，Entities Graphics 基于它构建
- DOTS Instancing 在 BRG 之上提供自动 batch 和 instancing
- 大量 entity 渲染时，BRG 可大幅减少 draw call

## 官方证据

| 证据 | 结论 |
|---|---|
| `requirements-and-compatibility.md` | URP Forward+; 不支持多 render World |
| `overview.md` | Runtime 创建用 prefab 或 RenderMeshUtility.AddComponents |
| `runtime-entity-creation.md` | RenderMeshArray + MaterialMeshInfo 选择 mesh/material |
| `entities-graphics-performance.md` | BRG 和 DOTS Instancing 是高数量渲染路径 |

## 使用模式与反模式

**正确模式：**
- Entities Graphics 只在 Presentation/Boundary 层
- Runtime 创建表现 entity 走 baked prefab 或 RenderMeshUtility
- 无头 Demo 用 log marker 占位，保留完整 outbox 链路
- Rendered profile 独立计时

**反模式：**
- Core simulation 直接添加 `RenderMeshArray` 等 graphics component
- 手动拼装 graphics 内部组件
- 表现 entity 反向修改 Core state

## EX-GAS 项目解读

### 无头与有头 Demo 的等价性

```
Core Simulation 产出 Presentation Outbox
    │
    ├── 无头模式：Outbox → Log Marker → validation check
    │
    └── 有头模式：Outbox → Entities Graphics binding → 真实渲染
```

**关键约束：**
- 两种模式的 Core Simulation hash 相同
- 只替换 Presentation binding 层
- 无头时 disabled reason 写入 rendered profile

### 当前 AutoChess 的关联

- 无头验证默认不启用 Entities Graphics
- 文件结构仍按真实 Demo 设计
- 所有 UI/VFX/SFX/Cue marker 逻辑保留，通过 outbox → log 验证
- Scale gates 可在无头模式下直接测量 Core 热路径

## 常见陷阱

1. **手动添加 graphics component**：缺少必需内部组件导致渲染异常且无明确报错
2. **多 render World 假设**：Entities Graphics 1.4.19 不支持
3. **Presentation group 内结构变化**：有严格限制

## 验收指标

1. Headless profile 记录 presentation outbox count、cue count、UI/VFX/SFX marker
2. Rendered profile 额外记录 batch、draw、material override、render world 证据
3. Core simulation hash 不受是否启用 Entities Graphics 影响
