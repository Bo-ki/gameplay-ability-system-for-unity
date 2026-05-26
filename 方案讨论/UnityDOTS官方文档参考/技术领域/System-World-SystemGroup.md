# System-World-SystemGroup

## 职责

本领域覆盖 ECS 中 World 生命周期管理、System 类型选择（ISystem vs SystemBase）、SystemGroup 层次结构与更新排序、以及系统数量成本管控。不覆盖 job 调度细节（见 Query-Job-遍历.md）、不覆盖单个 System 内部数据流（见 15-数据流-系统生命周期规范.md）、不覆盖 Baking 期系统。

## 核心概念

### World

World 是 ECS 数据和系统调度的最高边界。每个 World 拥有独立的 `EntityManager`、System 集合和 entity ID 命名空间——同一 entity ID 在不同 World 中指向不同实体。

**World 生命周期：**

```csharp
// 默认由 Unity 自动创建
// 手动控制使用 ICustomBootstrap
public class GASBootstrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        var world = new World("GASWorld", WorldFlags.Game);
        world.GetOrCreateSystem<GASCommandIngestSystem>();
        ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
        return true; // 阻止默认 World 创建
    }
}
```

**关键事实：**
- `WorldFlags` 控制行为：`Game`（运行时）、`Editor`（编辑器预览）、`ThinClient`（无渲染）
- 默认禁用：`#UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_RUNTIME_WORLD`
- `World.Dispose()` 销毁所有 entity 和 system，按创建逆序销毁
- 多 World 场景（如 Netcode 客户端/服务器分离）在同进程中运行独立 ECS 实例

### ISystem vs SystemBase

| 特性 | ISystem | SystemBase |
|------|---------|------------|
| 类型 | unmanaged struct | managed class |
| Burst 编译 | `OnCreate`/`OnUpdate`/`OnDestroy` 均可 `[BurstCompile]` | 仅 job 体内可 Burst |
| 开销 | 较低（无托管对象） | 较高（GC 分配、虚调用） |
| 状态访问 | `ref SystemState` 参数 | `this.World` / `this.EntityManager` |
| **EX-GAS 推荐** | Runtime Core 热路径 | Boundary / Editor / 低频 |

**ISystem 标准结构：**

```csharp
[BurstCompile]
public partial struct SAttributeRecalculate : ISystem
{
    public void OnCreate(ref SystemState state) { }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 只做 job 调度，不放业务逻辑在主线程
        var job = new AttributeRecalculateJob { ... };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state) { }
}
```

**System 生命周期回调顺序：**

```
OnCreate -> OnStartRunning -> OnUpdate（每帧）-> OnStopRunning -> OnDestroy
```

- `OnStartRunning`：首次 `OnUpdate` 前 + 从停止/禁用恢复时调用
- `OnStopRunning`：无匹配 entity（`RequireForUpdate` 不满足）或 `Enabled = false` 时调用
- `RequireMatchingQueriesForUpdate`：没有任何 query 匹配 entity 时跳过 `OnUpdate`

### SystemGroup 与 Update Order

SystemGroup 是 phase owner——它把子系统和子 Group 组织成有序更新树。

**默认三层根 Group：**

```
InitializationSystemGroup    (PlayerLoop Initialization 末尾)
SimulationSystemGroup        (PlayerLoop Update 末尾)
PresentationSystemGroup      (PlayerLoop PreLateUpdate 末尾)
```

**自定义 SystemGroup：**

```csharp
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(GASSpecEvaluationSystemGroup))]
public partial class GASCommandIngestSystemGroup : ComponentSystemGroup { }
```

**排序控制优先级：**
1. `OrderFirst` / `OrderLast`（最高优先级）
2. `UpdateBefore` / `UpdateAfter`（同级 Group 内）
3. 默认按创建顺序

**系统创建顺序**独立于更新顺序，由 `CreateBefore` / `CreateAfter` 控制。在 `OnCreate` 中访问其他系统的 singleton 时，必须加 `[CreateAfter(typeof(OtherSystem))]`。

### 系统数量成本

每个活跃 System 的固定开销来自三处：

1. **TypeHandle 刷新**：每个 System 在 `OnUpdate` 前获取自己的 `ComponentTypeHandle`/`EntityTypeHandle` 副本。结构变化使其失效，必须每帧重取。N 个 system = N 次 type handle 操作。
2. **Lookup 重复创建**：不同 System 可能创建相同的 `ComponentLookup<T>`，每帧各自更新。
3. **Dependency 链**：每个 System 通过 `Dependency` JobHandle 传递依赖。N 个 system = N 个 JobHandle 链接点。

**优化策略：**
- 合并不必要的细小 system 拆分
- 同 query 的多个操作放在一个 system 的不同 job 中
- 用 `RequireMatchingQueriesForUpdate` 跳过空跑 system
- 对始终有 entity 匹配的 system，不添加此属性以减少检查开销

## 编写规范

### SYS-01: 权威计算落在 ECS System/Job 数据流，禁止托管 manager 驱动

- **声明：** 所有 gameplay 权威计算（属性变化、效果执行、冷却管理）必须经由 ECS System 调度 job 完成，禁止由托管 C# manager 类（MonoBehaviour、ScriptableObject 等）在主线程驱动计算逻辑。
- **来源：** `concepts-worlds.md`、`systems-intro.md` —— System 是 ECS 数据流的基本调度单元；Unity 官方将 World 定义为"系统调度边界"。
- **为什么：** 托管 manager 驱动意味着计算在主线程顺序执行、无法 Burst、无法利用 worker 线程并行度、且与 ECS 数据的安全句柄系统隔离。当 x50 规模下每秒数万次 GE 操作时，托管 manager 成为中心瓶颈。
- **EX-GAS 诊断：** 当前 `GASManager.cs` 仍持有一部分托管路由职责。Debugger 应报告 System/Job 执行的运算量 vs 托管路径运算量的比例，目标是 System/Job 占比 > 95%。
- **检查方法：** 搜索 `OnUpdate` 中直接调用托管 manager 方法的模式；统计 Runtime Core 中 `ISystem.OnUpdate` 与托管 `Update()` 的执行时间占比。

### SYS-02: SystemGroup 是 phase owner，禁止手写 Tick 顺序

- **声明：** 每个 phase 边界必须通过显式 `SystemGroup` 定义。禁止通过手动调用其他 System 的 `Update()` 或依赖 `MonoBehaviour.Update()` 顺序控制 phase 执行时机。
- **来源：** `systems-update-order.html` —— "SystemGroup provides hierarchical update ordering"。官方明确三层根 Group 对应 PlayerLoop 阶段。
- **为什么：** 手动 Update 调用（P2-11）会破坏 EntityQuery 的变更版本号，导致 `SetChangedVersionFilter` 失效。手写 Tick 顺序也无法被 ECS 安全系统跟踪，丢失自动依赖管理。
- **EX-GAS 诊断：** `GasStructuralPlaybackSystemGroup` 设计已存在但尚未完全落地。每个 phase 必须有对应的 `ComponentSystemGroup` 子类和明确的 `[UpdateInGroup]` 归属。
- **检查方法：** 审计所有 `ISystem.OnUpdate` 或 `SystemBase.OnUpdate` 中的 `.Update()` 调用；确认每个 phase 首尾有明确的 SystemGroup 边界。

### SYS-03: 系统数量是成本源，避免不必要的 system 拆分

- **声明：** 每个活跃 System 有 TypeHandle 刷新、Lookup 创建、Dependency 链三种固定开销。功能相近且共享 query 的 system 应评估合并，而非为每个细小职责创建一个新 system。
- **来源：** `systems-optimizing.html` —— "Every system has a fixed performance overhead... the CPU overhead of these accesses grows linearly with the number of active systems."
- **为什么：** N 个 system = N 次 type handle 操作 + N 次 lookup 更新 + N 个 JobHandle 链节点。在 100+ system 规模下，这些开销累计可达毫秒级。
- **EX-GAS 诊断：** Debugger 应输出 `ActiveSystemCount` 分组统计。Runtime Core 每 phase 超过 5 个 system 时触发合并评估。
- **检查方法：** 在 Debugger 中查看按 `core/diagnostics/presentation/demo` 分组的 System 计数；review 时注意为单一职责创建新 system 的冲动。

### SYS-04: Core/Physics/Presentation 成本分组统计

- **声明：** 性能归因必须按 Core（GAS Runtime）/ Physics / Presentation / Runner（AutoChess Demo）分组统计。禁止将所有 PlayerLoop 成本笼统归因到 GAS Core。
- **来源：** EX-GAS 架构推论，源于 `systems-optimizing.html` 的线性开销理论。
- **为什么：** 混淆分组使性能优化失去方向——Presentation 的渲染抖动可能被误判为 Core 管线退化。只有分组统计才能准确决策哪个领域需要优化投资。
- **EX-GAS 诊断：** 当前性能报告缺少分类。`GASRuntimeFrameBudgetContract` 应指定每组预算上限。
- **检查方法：** 性能报告必须包含至少四组的开销分解图；任何优化 PR 必须说明影响的组别。

### SYS-05: World 边界：Debugger/Demo/Presentation 只能通过 Boundary 观察 Core

- **声明：** Debugger、AutoChess Demo Runner、Presentation 层不允许直接修改 Core World 的 ECS 数据。它们必须通过只读观察通道（如 `ComponentLookup`、`SystemAPI.Query` 只读、Shared Read Model）或预定义的 Boundary API 获取 Core 状态。
- **来源：** `concepts-worlds.md` —— World 是 entity ID 唯一性和系统调度边界；多 World 场景中 ECS 实例独立运行。
- **为什么：** 允许 Debugger 直接写入 Core 数据会引入非确定性行为 —— Debugger 的一次写入可能在 release build 中不存在，导致 Core 行为在 debug/release 间不一致。Demo Runner 的写入可能绕过 Core 的业务规则。
- **EX-GAS 诊断：** 当前 `GasRuntimeDebugger.cs` 持有部分观察者职责。需要明确区分 read-only 诊断和 read-write 调试命令。
- **检查方法：** 搜索 Debugger/Presentation/Demo 目录中对 Core component 的写入操作；确认跨 World 访问是否通过只读通道。

### PRF-07: 避免不必要的 System 拆分 (P1)

- **声明：** 不要为每个细小计算步骤创建一个 System。当多个操作共享相同 EntityQuery 时，评估是否合并到同一个 System 的多个 job 中。
- **来源：** `systems-optimizing.html`；严重度 P1（严重）。
- **为什么：** 同 query 拆分为多个 system 意味着每个 system 各自获取相同的 TypeHandle 和 Lookup、重复建立依赖链、增加调度开销。合并后在同一个 System 内通过多个 job 串行调度可共享 handle 和 lookup。
- **EX-GAS 诊断：** Debugger 应报告 system 合并建议；Same Query Diff System 检测模式。
- **检查方法：** 审计同一 query 条件（All/Any/None 相同）是否分布在多个 system 中。

### PRF-16: System 创建依赖用 CreateAfter；ISystem 优于 SystemBase (P2)

- **声明：**
  - 当 System A 的 `OnCreate` 依赖 System B 创建的 singleton 或资源时，必须加 `[CreateAfter(typeof(SystemB))]`。
  - 新 system 优先使用 `ISystem`（unmanaged struct）而非 `SystemBase`（managed class），特别是需要 Burst 编译时。
- **来源：** `systems-update-order.html`（CreateAfter）、`systems-optimizing.html`（ISystem 开销更低）；严重度 P2（注意）。
- **为什么：** 没有 `CreateAfter` 的情况下，`OnCreate` 中访问其他系统的 singleton 可能因创建顺序不确定而访问空引用。`ISystem` 无托管对象、虚调用、GC 分配，Burst 开销更低。
- **EX-GAS 诊断：** Runtime Core 所有新 system 必须为 `ISystem`。当前 `SAbilityCommit`/`SAbilityStateCleanup` 等 system 已使用 `ISystem` 结构。
- **检查方法：** Code review 检查新 system 类型选择；搜索 `SystemBase` 在 Runtime Core 目录的出现（仅允许旧 system）。

## 模式与反模式

### 正确模式

- System 只调度 job，job 做实际计算。`OnUpdate` 体的职责仅是初始化 job 参数 + 调用 `.ScheduleParallel()`。
- SystemGroup 显式定义 phase 边界，每个 group 有明确的 enter/exit 条件。
- 多 phase 共享数据通过 singleton component 或 native container 传递，不通过托管 manager 字段。
- Runtime Core system 全部用 ISystem + Burst。
- World 间通信通过只读 Boundary 层，不直接跨 World 调用。

### 反模式

- **在 `OnUpdate` 中写主线程业务逻辑**：`OnUpdate` 不是业务逻辑处理器，是 job 调度器。主线程逻辑阻塞所有 worker 线程。
- **System 爆炸**：为每个细小职责创建新 system（如 `UpdateHealthSystem`、`UpdateManaSystem`、`UpdateStaminaSystem`）。应合并为 `UpdateAllAttributesSystem` 的一个 job。
- **用 `World.GetOrCreateSystem` 替代 `CreateAfter` 控制创建顺序**：`GetOrCreateSystem` 不保证创建顺序。如果 `OnCreate` 需要访问其他系统的数据，必须用 `[CreateAfter]`。
- **把 `DefaultWorldInitialization.AutoGetAndRegisterSystems` 作为 GAS 核心调度来源**：GAS Runtime Core 必须通过显式 phase contract（`GASSystemScheduleContract`）控制系统集合，而非自动发现。
- **手动调用其他 System 的 Update()**：违反 P2-11，破坏 EntityQuery 变更版本号，导致响应式系统误触发。
- **Debugger 直接修改 Core component**：违反 SYS-05，导致 debug/release 行为不一致。

## EX-GAS 项目解读

### Runtime Core Phase 设计

目标态 Runtime Core SystemGroup 映射：

| GAS Phase | SystemGroup | 挂载位置 | 结构变化权限 |
|-----------|-------------|----------|--------------|
| Frame Arena / Query Prep | `GasRuntimeFramePrepareSystemGroup` | Simulation 最前 | 禁止 |
| Command Ingest | `GasCommandIngestSystemGroup` | Frame Prep 之后 | 只读 request；不 playback |
| Spec Evaluation | `GasSpecEvaluationSystemGroup` | Command Ingest 之后 | 禁止 |
| Delta Apply | `GasDeltaApplySystemGroup` | Spec Eval 之后 | 禁止 |
| Active Effect Lifecycle | `GasActiveEffectLifecycleSystemGroup` | Delta 之后 | 禁止直接结构变化 |
| Typed Fact Projection | `GasTypedFactProjectionSystemGroup` | Active Lifecycle 之后 | 禁止 |
| Structural Playback | `GasStructuralPlaybackSystemGroup` | 以上之后 | **唯一 hot path 结构变化点** |
| Observation Projection | `GasObservationProjectionSystemGroup` | 最后 | 只读 |

### 当前代码对齐

- 当前 `GASManager.CreateSystems()` 已收束到 `GASSystemScheduleContract`，方向正确。
- 不应使用 `DefaultWorldInitialization` 自动发现替代显式 phase contract。
- `GASCommandGroup -> GASTagGroup -> GASEffectGroup -> GASAttributeGroup -> GASAbilityGroup -> GASCueGroup` 骨架已有，但需要按目标态重排。
- `GASRuntimeFrameBudgetContract` 应指定每组预算上限。
- `GASRuntimeQueryLayoutPlan` 应与 phase 设计对齐。

### 性能归因要求

```
Core / Physics / Presentation / Runner 必须分组统计
不能把所有 PlayerLoop 成本归因到 GAS Core
```

### ICustomBootstrap 的使用时机

AutoChess 无头验收 Demo 如果使用隔离 World，必须通过 `ICustomBootstrap` 或手动 World 控制，并明确 world time / tick source，不能隐式依赖 Editor frame delta。参见 CASE-17。

### 相关 CASE 模式

- **CASE-16 (SystemGroup Allocator)**：`SetRateManagerCreateAllocator` + `DoubleRewindableAllocators` + `SetGroupAllocator`，为 `GasRuntimeFramePrepareSystemGroup` 建立 per-group scratch allocator。降低临时分配开销。
- **CASE-17 (ICustomBootstrap 多世界)**：无头 AutoChess runner 通过 ICustomBootstrap 创建 `FixedStepTime(1.0f/60f)` 独立 World，各 SystemGroup 通过 `[UpdateInGroup]` 分发到正确 World；不隐式依赖 Editor `World.Time` 或 `VariableStepTime`。

## 常见陷阱

1. **在 `OnCreate` 中访问未创建的系统**：使用 `CreateAfter` 确保依赖系统已存在，而非用 `GetOrCreateSystem` 在 `OnCreate` 中补救。
2. **System 数量膨胀**：每增加一个 system 都有 type handle + lookup + dependency 成本。对每 phase 5 system 阈值保持警觉。
3. **忘记 `ref` 关键字**：ISystem 回调参数 `ref SystemState state` 是 `ref` 传递。漏掉 `ref` 编译不报错但语义错误。
4. **World 生命周期**：`World.Dispose()` 后所有 entity/component 失效，无自动恢复。World 销毁不能撤销。
5. **多 World 混用**：entity ID 只在 World 内唯一，跨 World 传递 entity 必须用其他方式（如 entity mapping）。
6. **ISystem 中意外使用托管对象**：`ISystem` 是 struct，但字段可以是托管引用。Burst 编译时如果字段有托管对象会静默退化为非 Burst 执行。
7. **`[RequireMatchingQueriesForUpdate]` 的误用**：始终有 entity 匹配的 system 加此属性反而增加检查开销（P2-06）。
8. **SystemGroup 嵌套过深**：过多嵌套层次的 SystemGroup 增加调度决策成本。一般不超过 3 层嵌套。
9. **手动调用其他 System 的 Update() 导致响应式系统误触发**：调用方 System 的 EntityQuery 看到被调用方产生的变更版本号（P2-11）。

## 官方证据

| 官方文档 | 关键结论 | 关联规则 |
|----------|----------|----------|
| `concepts-worlds.md` | World 是 entity ID 唯一性和系统调度边界 | SYS-01, SYS-05 |
| `systems-intro.md` | ISystem 是 unmanaged 首选，SystemBase 退居 managed 场景 | SYS-01, PRF-16 |
| `systems-update-order.html` | SystemGroup 提供分层排序；三层根 Group 对应 PlayerLoop 阶段 | SYS-02 |
| `systems-optimizing.html` | 每个 system 的 type handle + lookup + dependency 链产生线性 CPU 开销 | SYS-03, SYS-04, PRF-07 |
| `systems-isystem.html` | ISystem 回调签名使用 `ref SystemState`；Burst 友好 | PRF-16 |
| `systems-version-numbers.md` | 手动调用其他 System Update() 破坏变更版本号 | SYS-02 (反模式) |
| `systems-entity-command-buffers.md` | ECB 最佳实践、独立 ECB per job | SYS-02 (phase 设计) |

## 验收指标

1. Runtime Core 行动报告能说明新增或修改的 system 属于哪个 SystemGroup / phase。
2. 性能报告至少拆分 Core / Physics / Presentation / Runner 四组成本，每组有独立帧时间预算。
3. 当系统数量增长时，Debugger 能报告 active system count（按 core/diagnostics/presentation/demo 分组）、query count、lookup pressure。
4. AutoChess 无头验收不隐式依赖 Editor frame delta，使用独立 World + FixedStepTime。
5. Runtime Core 所有新 system 为 `ISystem` 结构；现有 `SystemBase` 有明确的迁移计划。
6. 无手动调用其他数据处理 System 的 `Update()` 模式（P2-11 检查通过）。
7. Debugger/Demo/Presentation 层不对 Core component 进行写入操作（SYS-05 检查通过）。
