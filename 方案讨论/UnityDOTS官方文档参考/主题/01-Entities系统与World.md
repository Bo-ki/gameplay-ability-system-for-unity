# 01 Entities 系统与 World

## 职责

本主题维护 Entities 的 World、System、SystemGroup、phase 和 system 数量成本规则，用于校准 EX-GAS Runtime Core 管线。

## 核心概念详解

### World

World 是 ECS 数据和系统调度的最高边界。每个 World 拥有独立的 `EntityManager`、System 集合和 entity ID 命名空间。同一 entity ID 在不同 World 中可以指向不同实体。

**World 的生命周期：**

```csharp
// 默认World由Unity自动创建和管理
// 若需要手动控制，使用 ICustomBootstrap
public class GASBootstrap : ICustomBootstrap
{
    public bool Initialize(string defaultWorldName)
    {
        // 创建自定义World
        var world = new World("GASWorld", WorldFlags.Game);
        // 手动添加系统
        world.GetOrCreateSystem<GASCommandIngestSystem>();
        // 插入PlayerLoop
        ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
        return true;
    }
}
```

**关键事实：**
- `WorldFlags` 控制 World 行为：`Game`（运行时）、`Editor`（编辑器预览）、`ThinClient`（无渲染）等
- 默认禁用自动创建：`#UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_RUNTIME_WORLD`
- World 销毁处理 `World.Dispose()`，系统按创建逆序销毁
- 多 World 场景（如 Netcode 的客户端/服务器分离）在同进程中运行独立 ECS 实例

### ISystem vs SystemBase

Unity 1.4.6 有两种 System 基类型：

| 特性 | ISystem | SystemBase |
|---|---|---|
| 类型 | unmanaged interface | managed class |
| Burst 编译 | `OnCreate`/`OnUpdate`/`OnDestroy` 均可 `[BurstCompile]` | 仅 job 体内可 Burst |
| overhead | 较低（无托管对象） | 较高（GC 分配、虚调用） |
| 状态访问 | `ref SystemState` 参数 | `this.World` / `this.EntityManager` |
| **EX-GAS 推荐** | Runtime Core 热路径 | Boundary / Editor / 低频 |

**ISystem 标准结构：**

```csharp
[BurstCompile]
public partial struct SAttributeRecalculate : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        // 初始化 query、lookup、allocator
        // 此时其他系统的创建顺序可能未定，依赖其他系统需用 [CreateAfter]
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 只做 job 调度，不放业务逻辑在主线程
        var job = new AttributeRecalculateJob { ... };
        state.Dependency = job.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        // 释放 Persistent allocator 等资源
    }
}
```

**System 生命周期回调顺序：**
```
OnCreate -> OnStartRunning -> OnUpdate（每帧）-> OnStopRunning -> OnDestroy
```

- `OnStartRunning`：首次 `OnUpdate` 前 + 从停止/禁用恢复时调用
- `OnStopRunning`：无匹配 entity（`RequireForUpdate` 不满足）或 `Enabled = false` 时调用
- `RequireMatchingQueriesForUpdate`：当没有任何 query 匹配到 entity 时跳过 `OnUpdate`

### SystemGroup 与 Update Order

SystemGroup 是 phase owner — 它把子系统和子 Group 组织成有序更新树。

**默认三层根 Group：**

```text
InitializationSystemGroup    (PlayerLoop Initialization 末尾)
SimulationSystemGroup        (PlayerLoop Update 末尾)
PresentationSystemGroup      (PlayerLoop PreLateUpdate 末尾)
```

**自定义 SystemGroup：**

```csharp
// 定义 EX-GAS 自己的 phase group
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(GASSpecEvaluationSystemGroup))]
public partial class GASCommandIngestSystemGroup : ComponentSystemGroup { }
```

**排序控制优先级：**
1. `OrderFirst` / `OrderLast`（最高优先级）
2. `UpdateBefore` / `UpdateAfter`（同级 Group 内）
3. 默认按创建顺序

```csharp
[UpdateInGroup(typeof(GASCommandIngestSystemGroup), OrderFirst = true)]
public partial struct SCommandNormalize : ISystem { }

[UpdateBefore(typeof(SAbilityActivation))]
public partial struct SCostValidation : ISystem { }
```

**系统创建顺序**独立于更新顺序，由 `CreateBefore` / `CreateAfter` 控制。在 `OnCreate` 中访问其他系统的 singleton 时，必须加 `[CreateAfter(typeof(OtherSystem))]`。

### 系统数量成本

每个活跃 System 的固定开销来自三处：

1. **TypeHandle 更新**：每个 System 在 `OnUpdate` 前获取自己的 `ComponentTypeHandle`/`EntityTypeHandle` 副本。结构变化使其失效，所以必须每帧重取。N 个 system = N 次 type handle 操作。

2. **Lookup 重复创建**：不同 System 可能创建相同的 `ComponentLookup<T>`，每帧各自更新。更多 system = 更多重复 lookup 更新。

3. **Dependency 链**：每个 System 通过 `Dependency` JobHandle 传递依赖。N 个 system = N 个 JobHandle 链接点。

**优化策略：**
- 合并不必要的细小 system 拆分
- 同 query 的多个操作放在一个 system 的不同 job 中
- 用 `RequireMatchingQueriesForUpdate` 跳过空跑 system
- 对始终有 entity 存在的 system（如玩家角色），不添加此属性以减少检查开销

## 官方证据

| 证据 | 结论 |
|---|---|
| `concepts-worlds.md` | World 是 entity ID 唯一性和系统调度边界 |
| `systems-intro.md` | ISystem 是 unmanaged 首选，SystemBase 逐步退居 managed 场景 |
| `systems-update-order.html` | SystemGroup 提供分层排序；三层根 Group 对应 PlayerLoop 阶段 |
| `systems-optimizing.html` | 每个 system 的 type handle + lookup + dependency 链产生线性 CPU 成本 |
| `systems-isystem.html` | ISystem 回调签名使用 `ref SystemState`；Burst 友好 |

## 使用模式与反模式

**正确模式：**
- System 只调度 job，job 做实际计算
- SystemGroup 显式定义 phase 边界
- 多 phase 共享数据通过 singleton component 或 native container 传递
- Runtime Core system 全部用 ISystem + Burst

**反模式：**
- 在 `OnUpdate` 中写主线程业务逻辑
- 为每个细小职责创建一个新 system（system 爆炸）
- 用 `World.GetOrCreateSystem` 替代 `CreateAfter` 控制创建顺序
- 把 `DefaultWorldInitialization.AutoGetAndRegisterSystems` 作为 GAS 核心调度来源

## EX-GAS 项目解读

### 对 Runtime Core Phase 设计的影响

目标态 Runtime Core SystemGroup 映射：

| GAS Phase | SystemGroup | 挂载位置 | 结构变化权限 |
|---|---|---|---|
| Frame Arena / Query Prep | `GasRuntimeFramePrepareSystemGroup` | Simulation 最前 | 禁止 |
| Command Ingest | `GasCommandIngestSystemGroup` | Frame Prep 之后 | 只读 request；不 playback |
| Spec Evaluation | `GasSpecEvaluationSystemGroup` | Command Ingest 之后 | 禁止 |
| Delta Apply | `GasDeltaApplySystemGroup` | Spec Eval 之后 | 禁止 |
| Active Effect Lifecycle | `GasActiveEffectLifecycleSystemGroup` | Delta 之后 | 禁止直接结构变化 |
| Typed Fact Projection | `GasTypedFactProjectionSystemGroup` | Active Lifecycle 之后 | 禁止 |
| Structural Playback | `GasStructuralPlaybackSystemGroup` | 以上之后 | **唯一 hot path 结构变化点** |
| Observation Projection | `GasObservationProjectionSystemGroup` | 最后 | 只读 |

### 现有代码对齐

- 当前 `GASManager.CreateSystems()` 已收束到 `GASSystemScheduleContract`，正确方向
- 不应使用 `DefaultWorldInitialization` 自动发现替代显式 phase contract
- `GASCommandGroup -> GASTagGroup -> GASEffectGroup -> GASAttributeGroup -> GASAbilityGroup -> GASCueGroup` 骨架已有，但需要按目标态重排

### 性能归因要求

```
Core / Physics / Presentation / Runner 必须分组统计
不能把所有 PlayerLoop 成本归因到 GAS Core
```

### ICustomBootstrap 的使用时机

AutoChess 无头验收 Demo 如果使用隔离 World，必须通过 `ICustomBootstrap` 或手动 World 控制，并明确 world time / tick source，不能隐式依赖 Editor frame delta。

## 常见陷阱

1. **在 `OnCreate` 中访问未创建的系统**：使用 `CreateAfter` 确保依赖系统已存在
2. **系统数量膨胀**：每增加一个 system 都有 type handle + lookup + dependency 成本
3. **忘记 `ref` 关键字**：ISystem 回调参数 `ref SystemState state` 是 `ref` 传递
4. **World 生命周期**：`World.Dispose()` 后所有 entity/component 失效，无自动恢复
5. **多 World 混用**：entity ID 只在 World 内唯一，跨 World 传递 entity 必须用其他方式

## 验收指标

1. Runtime Core 行动报告能说明新增或修改的 system 属于哪个 SystemGroup / phase
2. 性能报告至少拆分 Core / Physics / Presentation / Runner
3. 当系统数量增长时，Debugger 能报告 active system count、query count、lookup pressure
4. AutoChess 无头验收不隐式依赖 Editor frame delta
