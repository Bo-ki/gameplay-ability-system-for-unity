# UnityPhysics：API 与 EX-GAS 解读

## 结论

Unity Physics 在 `FixedStepSimulationSystemGroup` 内构建、模拟并回写 physics 数据。官方要求的是物理实体的 ECS 数据和 rigid-body chunk 布局在 physics pipeline 内保持稳定，而不是禁止期间发生任何无关 ECS 结构变化。Physics 查询与事件必须明确读取时点和生命周期；“Physics 只提供候选、GAS 最终裁决”属于 EX-GAS 架构策略。

**适用版本**：Unity Physics 1.4.6、Entities 1.4.6（项目安装版本）

## 官方机制

### Pipeline 与 timestep

```text
FixedStepSimulationSystemGroup
└── PhysicsSystemGroup
    ├── PhysicsInitializeGroup
    ├── PhysicsSimulationGroup
    └── ExportPhysicsWorld
```

`PhysicsSystemGroup` 是 fixed-step group 的子组，所以一个渲染帧可能执行零次、一次或多次 physics step。步长来源是 `FixedStepSimulationSystemGroup.Timestep` / 其 `IRateManager`；Entities 1.4.6 的默认构造值是 1/60 秒。不要把它描述为自动由 `UnityEngine.Time.fixedDeltaTime` 决定；若项目需要相同值，应显式配置并验证。

来源：Physics `physics-pipeline.md`；Entities `systems-time.md`；安装包 `Unity.Entities/DefaultWorld.cs` 中 `FixedStepSimulationSystemGroup`。

### 物理 ECS 数据的稳定窗口

`PhysicsInitializeGroup` 从 physics entity 的 ECS components 构建 `PhysicsWorld`，`ExportPhysicsWorld` 再把模拟结果写回 ECS。两端要求 rigid body 的 chunk layout 一致。因此：

- physics entity 及其 physics component membership 只能在 `PhysicsSystemGroup` 之前或之后改变。
- pipeline 内写 physics ECS 数据通常不会成为本 step 的模拟输入，并可能被导出结果覆盖或触发 integrity error。
- 非 physics entity 的无关结构变化不属于这条 Physics 特有禁令，但仍必须满足一般 ECS job 依赖与结构变化安全规则。
- 要修改中间 simulation data，应使用 `PhysicsSimulationGroup` 的官方子组和相应 API，而不是改 physics ECS component。

来源：`physics-data-types.md`、`simulation-modification.md`。

### PhysicsWorldSingleton 与 OverlapAabb

`PhysicsWorldSingleton` 是获取 `PhysicsWorld` 的受支持入口。直接从旧的 `BuildPhysicsWorld` system 取内部数据没有线程安全保证，官方强烈不建议。

`OverlapAabbInput` 使用 `Aabb` 字段，结果是 rigid-body index 列表，不是 `DistanceHit`：

```csharp
var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
var bodyIndices = new NativeList<int>(Allocator.Temp);

var extents = new float3(5f);
var input = new OverlapAabbInput
{
    Aabb = new Aabb
    {
        Min = sourcePosition - extents,
        Max = sourcePosition + extents
    },
    Filter = new CollisionFilter
    {
        BelongsTo = 1u << AbilityLayer,
        CollidesWith = (1u << EnemyLayer) | (1u << NeutralLayer),
        GroupIndex = 0
    }
};

physicsWorld.OverlapAabb(input, ref bodyIndices);
for (int i = 0; i < bodyIndices.Length; i++)
{
    Entity candidate = physicsWorld.Bodies[bodyIndices[i]].Entity;
    // 复制候选 Entity，后续交给 GAS 规则过滤。
}

bodyIndices.Dispose();
```

来源：`physics-singletons.md`、`collision-queries.md`、`OverlapAabbInput` API。

### Broadphase 时效性

Broadphase 由 `PhysicsInitializeGroup` 构建。默认情况下，simulation 后不会再为最新位置同步 broadphase，查询结果有效于它上一次构建时的 physics state。若启用 `PhysicsStep.SynchronizeCollisionWorld`，Unity 会付出额外成本更新 collision world；这类查询应放在 `PhysicsSimulationGroup` 之后或下一次 `PhysicsInitializeGroup` 之前。

“上一 step”只是一种常见描述，真正需要记录的是：查询 system 的 update order、是否启用同步、消费的是哪个构建时点的 broadphase。

来源：`collision-queries.md` > 开头 Broadphase 说明。

### Collision/Trigger event 生命周期

Events 在 `PhysicsSimulationGroup` 完成后有效，直到下一次该 group 开始更新。一个渲染帧可能有多个 fixed steps，因此不要按“渲染帧末”推断生命周期。若后续阶段需要使用，应在有效窗口内复制必要的 `Entity`、位置、法线等值；不能保存 iterator、stream 引用或内部指针。

来源：`simulation-results.md` > `Events`。

## EX-GAS 项目策略

1. Physics query/event 只生成 TargetData 候选；伤害、治疗、Buff 和 Tag 结果由 GAS Core 裁决。
2. Physics 是否启用不得改变相同输入下的 Core 规则定义；若 Physics 候选本身参与战斗输入，则必须将输入序列纳入 battle hash/回放协议，而不能简单宣称 hash 天然相同。
3. `coreTickMs` 与 `physicsStepMs` 分开记录。
4. 每个 Physics query 调用点记录 broadphase policy 和 system update order。

以上四项是 EX-GAS 的架构与验收策略，不是 Unity Physics 通用规范。

## 官方证据

| 官方文档 | 可裁决结论 |
|---|---|
| `physics-pipeline.md` | Pipeline 组成；一个渲染帧可多次 physics step |
| `physics-data-types.md` | physics entity 数据/rigid-body chunk layout 的稳定窗口 |
| `physics-singletons.md` | 获取 PhysicsWorld/Simulation 的受支持入口 |
| `collision-queries.md` | Broadphase 构建、同步选项、Overlap 结果语义 |
| `simulation-results.md` | Event 有效窗口 |
| Entities `systems-time.md` | FixedStep group 的时间语义 |
