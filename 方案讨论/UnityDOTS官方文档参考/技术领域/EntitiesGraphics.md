# Entities Graphics

## 职责

本领域维护 Entities Graphics 在 EX-GAS 中的边界：Runtime 渲染 entity 创建规范、无头/有头 Demo 等价性、表现层与 Core simulation 的分离。Entities Graphics 只负责 Presentation/Boundary 层的表现桥接，不涉及 Core simulation 数据。不覆盖 URP 配置、shader 开发、材质美术管线等渲染技术子领域。

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

// 使用场景：多个 entity 共享同一 RenderMeshArray，通过 MaterialMeshInfo 区分
ecb.SetComponent(entity, new MaterialMeshInfo { ... });
```

### BRG / DOTS Instancing

- `BatchRendererGroup`（BRG）是底层渲染 API，Entities Graphics 基于它构建
- DOTS Instancing 在 BRG 之上提供自动 batch 和 instancing
- 大量 entity 渲染时，BRG 可大幅减少 draw call

### 无头与有头 Demo 的等价性

```
Core Simulation 产出 Presentation Outbox
    |
    ├── 无头模式：Outbox -> Log Marker -> validation check
    |
    └── 有头模式：Outbox -> Entities Graphics binding -> 真实渲染
```

两种模式的 Core simulation hash 相同，只替换 Presentation binding 层。

## 编写规范

### GFX-01: Core simulation 不得直接添加 Graphics component

**声明：** Runtime Core system（在 `SimulationSystemGroup` 中运行的所有 GAS system）禁止直接添加、修改或读取 Entities Graphics 专有 component（`RenderMeshArray`、`MaterialMeshInfo`、`RenderBounds` 等）。Graphics component 的操作限于 Presentation/Boundary 层。

- **来源：** `requirements-and-compatibility.md` — Entities Graphics 组件边界；`12-官方案例模式.md` CASE-10 — Graphics runtime create 限于 Presentation 层
- **为什么：** 1) Core simulation 直接添加 graphics component 产生非必要的结构变化和 archetype 膨胀；2) 无头模式不加载 Entities Graphics 包，直接引用 graphics component 导致编译/加载错误；3) 违反表现与逻辑分离原则
- **EX-GAS 诊断：** Debugger 检测 `SimulationSystemGroup` 中对 `RenderMesh*` / `MaterialMeshInfo` / `RenderBounds` 等 graphics 组件的引用
- **检查方法：** Grep `RenderMesh` / `MaterialMeshInfo` / `RenderBounds` 在 `Assets/GAS/Runtime/System/` 目录下；确认 Core layer 零引用

### GFX-02: Runtime 创建渲染 entity 必须使用 RenderMeshUtility.AddComponents

**声明：** Runtime 路径下创建具有渲染表现的 entity 时，必须使用 `RenderMeshUtility.AddComponents` API。禁止手动拼装 Entities Graphics 内部组件。

- **来源：** `runtime-entity-creation.md` — Runtime 创建用 `RenderMeshUtility.AddComponents`；`overview.md` — Runtime 用 prefab 或 `RenderMeshUtility.AddComponents`
- **为什么：** `RenderMeshUtility.AddComponents` 自动添加所有必需内部组件（`RenderMesh`、`RenderBounds`、`RenderFilterSettings`、`ChunkWorldRenderBounds`、`WorldRenderBounds`）。手动拼装遗漏任何必需的内部组件导致渲染静默失败（entity 存在但不渲染），无编译错误或运行时异常
- **EX-GAS 诊断：** 搜索 `AddComponent<RenderMesh` / `AddComponent<RenderBounds` 等模式，标记为潜在违规
- **检查方法：** 搜索 `AddComponent<RenderMesh` / `AddComponent<MaterialMeshInfo` 按组件名手动拼装的模式；标记要求改为 `RenderMeshUtility.AddComponents`

### GFX-03: Presentation group 内禁止结构变化

**声明：** Entities Graphics 的 Presentation system group 内不允许结构变化操作。需要创建/销毁渲染 entity 的结构变化必须通过 ECB 延迟到合适的 playback phase。

- **来源：** `requirements-and-compatibility.md` — Presentation group 有结构变化限制
- **为什么：** Graphics 渲染管线（BRG）在 Presentation group 内持有内部缓冲区引用。结构变化使这些引用失效，导致渲染错误或安全系统异常
- **EX-GAS 诊断：** Debugger 标记 `PresentationSystemGroup` 内的直接结构变化调用
- **检查方法：** 搜索 `CreateEntity` / `DestroyEntity` / `AddComponent` / `RemoveComponent` 在 `[UpdateInGroup(typeof(PresentationSystemGroup))]` 标记的 system 中

### GFX-04: 无头模式与有头模式的 Core simulation hash 必须相同

**声明：** 无论是否启用 Entities Graphics，Core simulation 的 battle hash 必须一致。表现层（Entities Graphics binding）不能反向修改 Core state。无头 Demo 通过 Presentation Outbox -> Log Marker 链路验证表现正确性。

- **来源：** 项目架构决定（AutoChess 无头验收）
- **为什么：** 1) 无头模式是自动验收的基础，必须与有头模式共享相同 Core simulation；2) 表现 entity 创建/销毁如果影响 Core random state 或 Attribute 值，破坏无头验证；3) Presentation Outbox 确保表现请求被记录但不会反馈修改 Core
- **EX-GAS 诊断：** 压测报告对比有头/无头模式下的 battle hash；Debugger 输出 outbox count 和 cue count 用于验证
- **检查方法：** 检查表现层 system 是否对 Core component 有写访问；确认 Core simulation 在 Graphics 启用/禁用时输出相同 hash

### GFX-05: Rendered profile 必须与 Core 成本独立计时

**声明：** Profiling 输出中，`coreTickMs`（纯 GAS Runtime Core 时间）与 `renderMs`（Entities Graphics 时间）必须分离。无头模式中 `renderMs` 标记为 disabled，不能空白跳过。

- **来源：** `entities-graphics-performance.md` — BRG 和 DOTS Instancing 是高数量渲染路径
- **为什么：** 1) 无头模式渲染时间为 0，Core 时间不变；2) Rendered profile 的 renderMs 应额外记录 batch、draw call、material override、render world 信息；3) 渲染成本混入 coreTickMs 掩盖 Core 性能瓶颈
- **EX-GAS 诊断：** Summary 输出包含 `renderMs` 列（即使 0 也写入 disabled reason）；rendered profile 输出 batch count 和 draw call count
- **检查方法：** 确认 `GASManager` 或 `Debugger` 中 renderTime 与 coreTime 使用独立计时器；无头 profile 中输出 "Entities Graphics disabled" 标记

## EX-GAS 项目解读

### 有头/无头 Demo 等价性

```csharp
// Core Simulation —— 与 Graphics 无关
public struct PresentationOutbox : IComponentData
{
    public DynamicBuffer<CCueRequest> CueRequests;
    public DynamicBuffer<CVFXRequest> VFXRequests;
    public DynamicBuffer<CUIRequest> UIRequests;
}

// 有头模式：PresentationOutbox -> Entities Graphics 绑定
// 无头模式：PresentationOutbox -> Log Marker -> validation check

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

## 常见陷阱

1. **手动添加 graphics component**：缺少必需内部组件导致 entity 存在但不渲染，无明确报错。必须用 `RenderMeshUtility.AddComponents`
2. **Presentation group 内结构变化**：BRG 内部缓冲区引用在结构变化时失效，导致渲染错误或安全系统异常
3. **Core simulation 修改表现状态**：Core system 不应知道 `MaterialMeshInfo` 等 graphics 组件的存在；表现状态通过 Presentation Outbox 传递
4. **多 render World 假设**：Entities Graphics 1.4.19 不支持多个 render World
5. **"无头模式不需要 outbox"** — 无头模式也需要 outbox 链路以验证表现正确性和保持 Core simulation 完整性。outbox 在无头模式下写入 log marker，提供 validation 数据

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `requirements-and-compatibility.md` | URP Forward+; 不支持多 render World; Presentation group 有结构变化限制 | GFX-01, GFX-03 |
| `overview.md` | Runtime 创建用 prefab 或 `RenderMeshUtility.AddComponents` | GFX-02 |
| `runtime-entity-creation.md` | `RenderMeshArray` + `MaterialMeshInfo` 选择 mesh/material | GFX-02 |
| `entities-graphics-performance.md` | BRG 和 DOTS Instancing 是高数量渲染路径 | GFX-05 |
| `12-官方案例模式.md` CASE-10 | Graphics runtime create 限于 Presentation 层，Core 不得直接添加 graphics component | GFX-01, GFX-02 |

## 验收指标

1. Headless profile 记录 presentation outbox count、cue count、UI/VFX/SFX marker
2. Rendered profile 额外记录 batch、draw call、material override、render world 信息
3. Core simulation hash 不受是否启用 Entities Graphics 影响（有头/无头一致性验证）
4. Core layer 零 Entities Graphics 专有 component 引用
5. 所有 runtime 渲染 entity 创建路径使用 `RenderMeshUtility.AddComponents`
