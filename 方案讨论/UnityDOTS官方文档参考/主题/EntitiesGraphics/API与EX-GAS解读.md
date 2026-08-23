# EntitiesGraphics：API 与 EX-GAS 解读

## 结论

Entities Graphics 是 ECS 到 SRP 渲染的桥接层。运行时创建渲染实体有两条官方路径：复杂或批量对象优先烘焙 Prefab 后 `Instantiate`；确需从 C# 从零创建时，在主线程使用 `RenderMeshUtility.AddComponents` 建立原型，再批量实例化。表现层与 Core simulation 隔离属于 EX-GAS 架构策略，不是 Entities Graphics 的通用强制规则。

**适用版本**：Entities Graphics 1.4.19（项目安装版本）

## 官方机制

### 运行环境

- Entities Graphics 依赖 SRP；URP 仅支持 Forward+。
- 1.4.19 不支持同时渲染多个 World。
- 运行时不应手工拼装内部 Graphics 组件，因为必需组件集合可能随渲染管线和包版本变化。

来源：`requirements-and-compatibility.md`、`overview.md`。

### 运行时创建路径

`RenderMeshUtility.AddComponents` 只有 `EntityManager` 重载，是主线程 API，会产生结构变化。运行时必须使用接收 `RenderMeshArray` 的重载；接收 `RenderMesh` 的重载仅供 GameObject Baking 使用，在运行时不会产出可渲染实体。

```csharp
var description = new RenderMeshDescription(
    shadowCastingMode: ShadowCastingMode.Off,
    receiveShadows: false);
var renderMeshArray = new RenderMeshArray(
    new[] { material },
    new[] { mesh });

var prototype = entityManager.CreateEntity();
RenderMeshUtility.AddComponents(
    prototype,
    entityManager,
    description,
    renderMeshArray,
    MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
entityManager.AddComponentData(prototype, new LocalToWorld { Value = float4x4.identity });
```

大量实例不应逐个调用 `AddComponents`。先准备一个已包含完整 Graphics 组件集的 baked prefab 或 runtime prototype，再用 `EntityManager.Instantiate` 或 `EntityCommandBuffer.ParallelWriter.Instantiate` 克隆，并仅设置实例差异数据。

来源：`runtime-entity-creation.md` > `RenderMeshUtility - AddComponents`、`Usage instructions`。

### PresentationSystemGroup 写入窗口

官方约束不是“整个 PresentationSystemGroup 一律禁止修改”，而是：

- 普通 Presentation 系统不允许修改 ECS 数据。
- `UpdatePresentationSystemGroup` 可以修改组件数据，但不能做结构变化。
- `StructuralChangePresentationSystemGroup` 可以修改组件数据并执行结构变化。
- Presentation 结束后不能再修改 ECS 数据；后续 culling jobs 依赖该状态。若要延迟到帧末，应改到下一帧开始执行。

来源：`overview.md` > `Runtime functionality` 的 NOTE。

## EX-GAS 项目策略

以下是项目边界，不应表述为 Unity 官方通用规范：

1. Runtime Core 不引用 `Unity.Rendering` 类型，只向 Presentation Outbox 写表现请求。
2. 有头模式由 Presentation binding 消费 Outbox；无头模式由 log marker 消费同一请求。
3. 表现系统不得反向修改影响 battle hash 的 Core state。
4. `coreTickMs` 与渲染成本分开统计；渲染侧同时记录 CPU/GPU 时间、draw command、instance 和 batch 证据。

```text
Core Simulation -> Presentation Outbox
    |-> Headless: Log Marker
    `-> Rendered: Presentation Binding -> baked prefab/prototype Instantiate
```

## 常见错误

1. 把 `RenderMeshDescription` 误写成同时承载 mesh/material 的类型。
2. 给 `RenderMeshUtility.AddComponents` 传入 ECB；1.4.19 API 只接收 `EntityManager`。
3. 运行时调用接收 `RenderMesh` 的 Baking 专用重载。
4. 为每个实例重复 `AddComponents`，而不是创建一次原型后 `Instantiate`。
5. 把 `PresentationSystemGroup` 的两个例外组遗漏，形成过度禁止。

## 官方证据

| 官方文档 | 可裁决结论 |
|---|---|
| `overview.md` | Prefab 与 `RenderMeshUtility.AddComponents` 是两条运行时路径；Presentation 有两个例外组 |
| `runtime-entity-creation.md` | 运行时使用 `RenderMeshArray` 重载；API 仅主线程；批量实例优先 `Instantiate` |
| `requirements-and-compatibility.md` | SRP/URP Forward+ 与多 World 限制 |
| `entities-graphics-performance.md` | BRG/DOTS Instancing 的分析指标与 Profiler marker |
