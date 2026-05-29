# System-World-SystemGroup: API 与 EX-GAS 解读

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

**FixedStepSimulationSystemGroup：**

`FixedStepSimulationSystemGroup` 是 `SimulationSystemGroup` 下的固定步模拟域。PackageCache `systems-time.md` 明确说明固定步系统按固定时间间隔更新，并且一帧可能运行多次。EX-GAS Runtime Core 若以 battle tick / frame index / replay hash 为权威时序，核心物理执行域默认挂在 `FixedStepSimulationSystemGroup` 下；variable-step profile 必须额外给出确定性证据。

**自定义 SystemGroup：**

```csharp
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(GASCoreSimulationSystemGroup))]
public partial class GASCommandResolveSystemGroup : ComponentSystemGroup { }
```

**排序控制优先级：**
1. `OrderFirst` / `OrderLast`（最高优先级）
2. `UpdateBefore` / `UpdateAfter`（同级 Group 内）
3. 默认按创建顺序

### 系统数量成本

每个活跃 System 的固定开销来自三处：

1. **TypeHandle 刷新**：每个 System 在 `OnUpdate` 前获取自己的 `ComponentTypeHandle`/`EntityTypeHandle` 副本。结构变化使其失效，必须每帧重取。N 个 system = N 次 type handle 操作。
2. **Lookup 重复创建**：不同 System 可能创建相同的 `ComponentLookup<T>`，每帧各自更新。
3. **Dependency 链**：每个 System 通过 `Dependency` JobHandle 传递依赖。N 个 system = N 个 JobHandle 链接点。

---

## EX-GAS 项目解读

### Runtime Core Phase 设计

目标态 Runtime Core SystemGroup 映射采用 FixedStep 下的少量物理执行域，不再按每个 GAS 语义阶段建立 group：

| 物理执行域 | 默认挂载位置 | 承载的 kernel lane | 结构变化权限 |
|-----------|-------------|----------|--------------|
| `GASFramePrepareSystemGroup` | `FixedStepSimulationSystemGroup` 最前 | frame clock、allocator / budget、debug counters；query 由 owner system 创建 | 禁止 |
| `GASCommandResolveSystemGroup` | FramePrepare 之后 | Boundary Command Ingest、Target Resolve | 禁止 |
| `GASCoreSimulationSystemGroup` | CommandResolve 之后 | Effect Fan-In、State Evaluate、Attribute Reduce/Apply、Gameplay Fact | 禁止直接结构变化 |
| `GASStructuralCommitSystemGroup` | CoreSimulation 之后 | grant/remove/spawn/destroy/cleanup 的 ECB playback 或 EntityQuery bulk | **唯一 hot path 结构变化点** |
| `GASBoundaryProjectionSystemGroup` | StructuralCommit 之后 | ReadModel、Presentation outbox、Replay、Debugger | 只读 |

旧 `SpecEvaluation / DeltaApply / TypedFactProjection` 是 GAS 语义链，不是 DOTS 物理边界。只有新边界对应独立同步点、结构变化点、固定步策略或投影边界时，才允许新增 `ComponentSystemGroup`。

### 当前代码对齐

- `GASManager.CreateSystems()` 已收束到 `GASSystemScheduleContract`，方向正确。
- 不应使用 `DefaultWorldInitialization` 自动发现替代显式 phase contract。
- `GASRuntimeFrameBudgetContract` 应指定每组预算上限。
- `GASRuntimeQueryLayoutPlan` 应与 phase 设计对齐。

### 性能归因要求

Core / Physics / Presentation / Runner 必须分组统计，禁止将所有 PlayerLoop 成本归因到 GAS Core。

### ICustomBootstrap 的使用时机

AutoChess 无头验收 Demo 如果使用隔离 World，必须通过 `ICustomBootstrap` 或手动 World 控制，并明确 world time / tick source，不能隐式依赖 Editor frame delta。

---

## 常见陷阱

1. **在 `OnCreate` 中访问未创建的系统**：使用 `CreateAfter` 确保依赖系统已存在
2. **System 数量膨胀**：每个 system 都有 type handle + lookup + dependency 成本
3. **忘记 `ref` 关键字**：ISystem 回调参数 `ref SystemState state` 是 ref 传递，漏掉编译不报错但语义错误
4. **World 生命周期**：`World.Dispose()` 后所有 entity/component 失效，无自动恢复
5. **多 World 混用**：entity ID 只在 World 内唯一，跨 World 传递需用 entity mapping
6. **ISystem 中意外使用托管对象**：Burst 编译时如果字段有托管对象会静默退化为非 Burst
7. **`[RequireMatchingQueriesForUpdate]` 的误用**：始终有 entity 匹配的 system 加此属性反而增加检查开销
8. **SystemGroup 嵌套过深**：一般不超过 3 层嵌套
9. **手动调用其他 System 的 Update()**：破坏 EntityQuery 变更版本号

## 官方证据

| 官方文档 | 关键结论 | 关联规则 |
|----------|----------|----------|
| `concepts-worlds.md` | World 是 entity ID 唯一性和系统调度边界 | SYS-01, SYS-05 |
| `systems-intro.md` | ISystem 是 unmanaged 首选，SystemBase 退居 managed 场景 | SYS-01, PRF-16 |
| `systems-update-order.html` | SystemGroup 提供分层排序；三层根 Group 对应 PlayerLoop | SYS-02 |
| `systems-optimizing.html` | 每个 system 的 type handle + lookup + dependency 产生线性 CPU 开销 | SYS-03, SYS-04, PRF-07 |
| `systems-isystem.html` | ISystem 回调签名使用 `ref SystemState`；Burst 友好 | PRF-16 |
| `systems-version-numbers.md` | 手动调用其他 System Update() 破坏变更版本号 | SYS-02 |
| `systems-entity-command-buffers.md` | ECB 最佳实践、独立 ECB per job | SYS-02 |
