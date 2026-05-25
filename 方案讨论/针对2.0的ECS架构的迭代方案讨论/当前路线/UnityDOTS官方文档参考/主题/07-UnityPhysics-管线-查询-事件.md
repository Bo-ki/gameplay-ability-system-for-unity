# 07 Unity Physics 管线、查询与事件

## 职责

本主题维护 Unity Physics 在 EX-GAS 中的边界：目标获取、范围检测、碰撞确认、触发器、Physics profile。Physics 只能作为 GAS 的输入数据源，不能替代 GAS 规则裁决。

## 核心概念详解

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
// 获取 Physics World（这是唯一受支持的方式）
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

## 官方证据

| 证据 | 结论 |
|---|---|
| `physics-pipeline.md` | Physics jobs 在 PhysicsSystemGroup 内；pipeline 内不能结构变化 |
| `physics-singletons.md` | PhysicsWorldSingleton 是获取 PhysicsWorld 的正规方式 |
| `collision-queries.md` | collision query 默认用上一 step broadphase |
| `simulation-results.md` | Collision/Trigger events 只能从 Simulation 获取，且按生命周期窗口读取 |

## 使用模式与反模式

**正确模式：**
- Physics → 候选目标/命中事实 → GAS 规则裁决
- 无头 Demo 默认不开 Physics，输出 disabled reason
- Physics-enabled profile 输出独立的 physics step count 和 timing

**反模式：**
- Physics 直接决定伤害/治疗数值
- 跨 frame 保存 collision event iterator
- Physics cost 混入 `coreTickMs`

## EX-GAS 项目解读

### AutoChess 中的 Physics 角色

自走棋默认不需要真实物理。如有范围技能/目标获取需求：
1. Physics query → 候选目标列表
2. GAS TargetData 规则过滤（阵营/距离/可见性）
3. 非 Physics 时用格子系统或范围表替代

### Physics 与 Core 的成本拆分

```
coreTickMs    = 纯 GAS Runtime Core 时间（Command → Spec → Delta → Fact）
physicsStepMs = PhysicsSystemGroup 时间（如启用）
renderMs      = Entities Graphics 时间（如启用）
```

不启用时 summary 输出 disabled reason，不能空白跳过。

## 常见陷阱

1. **Broadphase 延迟**：上一 step broadphase 可能不包含刚创建/移动的 entity
2. **Physics step 内结构变化**：会破坏确定性
3. **Collision event 跨 frame**：event iterator 的生命周期只在当前 physics step
4. **Filter 配置错误**：BelongsTo/CollidesWith 用 uint mask，位配置错误 → 查不到 or 误命中

## 验收指标

1. Debugger 输出 physics step count、query count、event count、broadphase policy
2. Range/hit 任务能复现 deterministic target result
3. 压测报告拆出 Core 与 Physics 两类成本
4. Headless profile 输出 Physics disabled reason
