# EntitiesGraphics: API 与 EX-GAS 解读

## 核心概念

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

`RenderMeshUtility.AddComponents` 自动添加所有必要的内部组件（`RenderMesh`、`RenderBounds`、`RenderFilterSettings`、`ChunkWorldRenderBounds` 等）。手动拼装遗漏任何必需组件导致渲染异常且无明确报错。

### RenderMeshArray / MaterialMeshInfo

```csharp
// RenderMeshArray：共享的 mesh + material 集合
// MaterialMeshInfo：选择 RenderMeshArray 中的哪个 mesh 和 material
// 支持 Burst-compatible 选择，不依赖托管对象
```

### 无头与有头 Demo 的等价性

```
Core Simulation 产出 Presentation Outbox
    |
    ├── 无头模式：Outbox -> Log Marker -> validation check
    |
    └── 有头模式：Outbox -> Entities Graphics binding -> 真实渲染
```

两种模式的 Core simulation hash 相同，只替换 Presentation binding 层。

---

## EX-GAS 项目解读

### 有头/无头 Demo 等价性

```csharp
public struct PresentationOutbox : IComponentData
{
    public DynamicBuffer<CCueRequest> CueRequests;
    public DynamicBuffer<CVFXRequest> VFXRequests;
    public DynamicBuffer<CUIRequest> UIRequests;
}

// 验证：两组模式产生相同 Core simulation hash
uint coreHash = ComputeCoreSimulationHash(...);
// coreHash 在 Graphics enabled/disabled 间一致
```

### AutoChess 的 Graphics 策略

- 无头验证默认不启用 Entities Graphics
- 所有 UI/VFX/SFX/Cue marker 逻辑保留，通过 outbox -> log 验证
- Scale gates 可在无头模式下直接测量 Core 热路径
- Rendered profile 作为可选 profile，额外输出渲染指标

### Presentation Outbox 链路

```
Core System 产出 PresentationOutbox buffer
    |
    v
PresentationBindingSystem（仅在有头模式注册）
    |-- 读取 outbox buffer
    |-- 调用 RenderMeshUtility.AddComponents（需新渲染 entity）
    |-- 更新已有 entity 的 MaterialMeshInfo（需切换材质）
    |
    v
PresentationOutbox 清空（Clear）或标记已消费
```

---

## 常见陷阱

1. **手动添加 graphics component**：缺少必需内部组件导致 entity 存在但不渲染
2. **Presentation group 内结构变化**：BRG 内部缓冲区引用在结构变化时失效
3. **Core simulation 修改表现状态**：Core system 不应知道 graphics 组件的存在
4. **多 render World 假设**：Entities Graphics 1.4.19 不支持多个 render World
5. **"无头模式不需要 outbox"**：无头模式也需要 outbox 链路以验证表现正确性

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `requirements-and-compatibility.md` | URP Forward+；不支持多 render World；Presentation group 有结构变化限制 | GFX-01, GFX-03 |
| `overview.md` | Runtime 创建用 prefab 或 `RenderMeshUtility.AddComponents` | GFX-02 |
| `runtime-entity-creation.md` | `RenderMeshArray` + `MaterialMeshInfo` 选择 mesh/material | GFX-02 |
| `entities-graphics-performance.md` | BRG 和 DOTS Instancing 是高数量渲染路径 | GFX-05 |
| 官方案例模式 CASE-10 | Graphics runtime create 限于 Presentation 层 | GFX-01, GFX-02 |
