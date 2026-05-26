# UnityPhysics: API 与 EX-GAS 解读

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
var simulation = SystemAPI.GetSingleton<SimulationSingleton>();
// 事件只在当前 physics step 有效
// 不能跨 frame 保存原始 event iterator
```

### CollisionFilter 配置

```csharp
var filter = new CollisionFilter
{
    BelongsTo = 1 << 0,        // Layer 0
    CollidesWith = (1 << 1) | (1 << 2)  // Layer 1 和 Layer 2
};
```

---

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

---

## 常见陷阱

1. **Broadphase 延迟**：上一 step broadphase 可能不包含刚创建/移动的 entity
2. **Physics step 内结构变化**：会破坏确定性，触发安全系统错误
3. **Collision event 跨 frame**：event iterator 生命周期只在当前 physics step
4. **Filter 配置错误**：`BelongsTo` / `CollidesWith` 位配置错误导致查不到或误命中
5. **"Physics 查询结果直接用于伤害计算"**：违反 PHY-02，必须经过 GAS 规则层过滤

## 官方证据

| 官方文档文件 | 关键结论 | 关联规则编号 |
|---|---|---|
| `physics-pipeline.md` | Physics jobs 在 `PhysicsSystemGroup` 内；pipeline 内不能结构变化 | PHY-01 |
| `physics-singletons.md` | `PhysicsWorldSingleton` 是获取 PhysicsWorld 的正规方式 | PHY-02, PHY-04 |
| `collision-queries.md` | collision query 默认用上一 step broadphase；同步有额外成本 | PHY-04 |
| `simulation-results.md` | Collision/Trigger events 只能从 Simulation 获取 | PHY-03 |
| 官方案例模式 CASE-09 | Physics query 用于目标获取输入（候选列表） | PHY-02 |
