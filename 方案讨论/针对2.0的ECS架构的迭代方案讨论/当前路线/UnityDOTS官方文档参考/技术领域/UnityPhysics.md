# Unity Physics

## 职责

本领域维护 Unity Physics 在 EX-GAS 中的边界：Physics pipeline 时序约束、CollisionWorld 查询规范、Collision/Trigger 事件处理、Physics 与 GAS 规则裁决的分离。Physics 只能作为 GAS 的输入数据源，不能替代 GAS 规则裁决。不覆盖 Physics authoring workflow、joint 和约束系统、character controller 等 GAS 不使用的子领域。

## 核心概念

### PhysicsSystemGroup 与 FixedStep

Physics 运行在 `FixedStepSimulationSystemGroup` 内，一帧可多步：

```
FixedStepSimulationSystemGroup
  └── PhysicsSystemGroup
        ├── PhysicsInitializeGroup
        ├── PhysicsSimulationGroup
        └── PhysicsExportGroup
```

**关键约束：**
- Physics pipeline 内不允许结构变化
- Physics step 次数 = `Time.fixedDeltaTime` 决定的步数
- Physics system 读写必须放在正确的 update order 位置

### PhysicsWorldSingleton 与 CollisionWorld

```csharp
// 获取 Physics World（唯一受支持的方式）
var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

// Collision query
var filter = new CollisionFilter
{
    BelongsTo = (uint)CollisionLayer.PlayerAbility,
    CollidesWith = (uint)(CollisionLayer.Enemy | CollisionLayer.Neutral)
};

// OverlapAabb 范围检测
var hits = new NativeList<DistanceHit>(Allocator.TempJob);
physicsWorld.OverlapAabb(
    new OverlapAabbInput
    {
        Center = sourcePos,
        Extent = new half3(5f),
        Filter = filter
    },
    ref hits
);
```

**Broadphase 语义：**
- `OverlapAabb` / `Raycast` 默认使用**上一 physics step 的 broadphase**
- 同步 broadphase（确保当前 frame）有额外成本
- 基于命中结果做 GAS target selection 时必须声明 broadphase 时效性

### SimulationSingleton 与 Collision/Trigger Events

```csharp
// 获取碰撞事件
var simulation = SystemAPI.GetSingleton<SimulationSingleton>();
// 事件只在当前 physics step 有效
// 不能跨 frame 保存原始 event iterator
```

### CollisionFilter 配置

```csharp
// BelongsTo: 当前碰撞体的所属层
// CollidesWith: 当前碰撞体与哪些层碰撞
// 用 uint mask 表示，位运算配置
var filter = new CollisionFilter
{
    BelongsTo = 1 << 0,        // Layer 0
    CollidesWith = (1 << 1) | (1 << 2)  // Layer 1 和 Layer 2
};
```

## 编写规范

### PHY-01: Physics pipeline 内禁止结构变化

**声明：** `PhysicsSystemGroup` 的 pipeline 执行期间（`PhysicsInitializeGroup`、`PhysicsSimulationGroup`、`PhysicsExportGroup` 的运行阶段），禁止任何结构变化操作（CreateEntity、DestroyEntity、AddComponent、RemoveComponent）。结构变化必须通过 ECB 延迟到 `PhysicsSystemGroup` 之后。

- **来源：** `physics-pipeline.md` — "Physics jobs in PhysicsSystemGroup; pipeline 内不能结构变化"
- **为什么：** Physics pipeline 依赖稳定的 CollisionWorld 结构。结构变化破坏 Physics 内部数据结构的确定性，导致碰撞检测结果不可预测，甚至在 Physics job 运行时触发安全系统错误
- **EX-GAS 诊断：** Debugger 标记 `PhysicsSystemGroup` 内 system 的结构变化操作；ECB playback 应在 `PhysicsSystemGroup` 结束之后
- **检查方法：** 搜索 `CreateEntity` / `DestroyEntity` / `AddComponent` / `RemoveComponent` 在 `[UpdateInGroup(typeof(PhysicsSystemGroup))]` 标记的 system 中

### PHY-02: Physics 只能作为 GAS 输入数据源，不能替代 GAS 规则裁决

**声明：** Physics 查询结果（碰撞命中、重叠检测）只能作为 GAS 候选目标列表的输入。伤害/治疗数值、Buff/BuffStack 决策、状态移除等必须由 GAS Attribute 系统、GameplayEffect 系统和 TargetData 规则系统裁决。Physics 不能直接修改 GAS 核心 state。

- **来源：** 项目架构决定（EX-GAS Runtime Core 不依赖 Physics 做规则裁决）
- **为什么：** 1) 无头 AutoChess 默认不启用 Physics，但必须保持 Core simulation hash 与有头模式一致；2) Physics 的 broadphase 延迟和浮点精度差异引入不确定性，影响 battle hash；3) 规则变化时（如修改伤害公式）只需要改动 GAS 层，不需要触及 Physics
- **EX-GAS 诊断：** Debugger 输出 "Physics -> candidate list -> GAS rule filter" 的链路审计
- **检查方法：** Code Review 确认 Physics system 不直接读写 Attribute / GameplayEffect / Tag 等 GAS Core component。所有 Physics 输出经过 TargetData 规则层过滤

### PHY-03: Collision/Trigger Events 不能跨 frame 保存

**声明：** `SimulationSingleton` 提供的 collision / trigger event iterator 只在当前 physics step 有效。禁止将 event iterator 或其引用保存到下一帧或跨 frame 使用。

- **来源：** `simulation-results.md` — "Collision/Trigger events 只能从 Simulation 获取，且按生命周期窗口读取"
- **为什么：** Physics simulation 每帧重建 event 数据结构。跨 frame 保存的 iterator 指向已释放或复写内存，读取产生未定义行为
- **EX-GAS 诊断：** 搜索将 `SimulationSingleton`、collision event types 或 trigger event types 存储在 `IComponentData` 或 `NativeContainer` 中跨 frame 保留的模式
- **检查方法：** 搜索 `SimulationSingleton` 存储为 system 字段或 component 字段；搜索 collision/trigger event 类型的跨 frame 引用

### PHY-04: Physics 查询必须声明 broadphase 时效性

**声明：** 使用 `OverlapAabb` / `Raycast` / `ColliderCast` 等 Physics query 时，必须同时声明所依赖的 broadphase 时效性策略：接受默认的上一 step broadphase，或显式同步当前 frame broadphase（注明额外成本）。

- **来源：** `collision-queries.md` — "collision query 默认用上一 step broadphase"
- **为什么：** 刚创建/移动的 entity 可能不在上一 step broadphase 中，导致遗漏命中目标。使用同步 broadphase 有额外性能开销，但确保包含最新 entity
- **EX-GAS 诊断：** 每个 Physics query 周边注释声明 broadphase policy；Debugger 报告使用同步 broadphase 的频次
- **检查方法：** 搜索 `OverlapAabb` / `Raycast` / `ColliderCast`；检查调用点是否注释或配置了 broadphase 时效性选择

### PHY-05: Physics 与 Core 成本必须分离统计

**声明：** Debugger 和 profiling 输出中，`coreTickMs`（纯 GAS Runtime Core 时间）与 `physicsStepMs`（PhysicsSystemGroup 时间）必须分离。Physics 未启用时 summary 输出 disabled reason，不能空白跳过。

- **来源：** 项目架构决定（Physics 是可选 profile）
- **为什么：** 1) AutoChess 无头模式不启用 Physics，coreTickMs 保持可比性；2) Physics cost 混入 coreTickMs 掩盖 Core 的性能瓶颈；3) 多个 profile 之间需要对比 physicsEnabled 状态的 Core 时间差异
- **EX-GAS 诊断：** Summary 输出包含 `physicsStepMs` 列（即使 0 也写入 disabled reason）；压测报告拆分 Core 与 Physics 两类成本
- **检查方法：** 确认 `GASManager` 或 `Debugger` 中 physicsTime 与 coreTime 使用独立计时器；无 Physics profile 中输出 "Physics disabled" 标记

## EX-GAS 项目解读

### AutoChess 中的 Physics 角色

AutoChess 默认不需要真实物理。如有范围技能/目标获取需求：
1. Physics query -> 候选目标列表
2. GAS TargetData 规则过滤（阵营/距离/可见性）
3. 非 Physics 时用格子系统或范围表替代

目标态：Physics 可选 profile，不影响 Core battle hash。

### Physics 与 Core 的成本拆分

```
coreTickMs    = 纯 GAS Runtime Core 时间（Command -> Spec -> Delta -> Fact）
physicsStepMs = PhysicsSystemGroup 时间（如启用）
renderMs      = Entities Graphics 时间（如启用）
```

不启用时 summary 输出 disabled reason，不能空白跳过。

### CollisionFilter 的 EX-GAS 约定

| Layer | 用途 | 碰撞对象 |
|---|---|---|
| 0 | PlayerUnit | EnemyAbility, EnemyUnit |
| 1 | EnemyUnit | PlayerAbility, PlayerUnit |
| 2 | PlayerAbility | EnemyUnit |
| 3 | EnemyAbility | PlayerUnit |
| 4 | Neutral | All Ability |

## 常见陷阱

1. **Broadphase 延迟**：上一 step broadphase 可能不包含刚创建/移动的 entity，导致刚生成的技能范围检测遗漏目标
2. **Physics step 内结构变化**：会破坏确定性，触发安全系统错误
3. **Collision event 跨 frame**：event iterator 的生命周期只在当前 physics step，跨 frame 读取产生未定义行为
4. **Filter 配置错误**：`BelongsTo` / `CollidesWith` 用 uint mask，位配置错误导致查不到（缺少位）或误命中（多设置位）
5. **"Physics 查询结果直接用于伤害计算"** — 违反 PHY-02，使 battle hash 依赖 Physics，破坏无头验证。必须经过 GAS 规则层过滤

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `physics-pipeline.md` | Physics jobs 在 `PhysicsSystemGroup` 内；pipeline 内不能结构变化 | PHY-01 |
| `physics-singletons.md` | `PhysicsWorldSingleton` 是获取 PhysicsWorld 的正规方式 | PHY-02, PHY-04 |
| `collision-queries.md` | collision query 默认用上一 step broadphase；同步有额外成本 | PHY-04 |
| `simulation-results.md` | Collision/Trigger events 只能从 Simulation 获取，按生命周期窗口读取 | PHY-03 |
| `12-官方案例模式.md` CASE-09 | Physics query 用于目标获取输入（候选列表），不作为 GAS 规则裁决 | PHY-02 |

## 验收指标

1. Debugger 输出 physics step count、query count、event count、broadphase policy
2. Range/hit 任务能复现 deterministic target result（相同输入 -> 相同目标列表）
3. 压测报告拆出 Core 与 Physics 两类成本（`coreTickMs` / `physicsStepMs` 独立）
4. Headless profile 输出 Physics disabled reason
5. 零 Physics system 直接修改 GAS Core component
