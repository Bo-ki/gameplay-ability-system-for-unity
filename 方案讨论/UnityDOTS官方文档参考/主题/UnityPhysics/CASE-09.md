# CASE-09：Physics query 产出 GAS 候选目标

**类型**: 模式/案例
**证据等级**: 模式/案例（官方 API 机制见“来源”；不代表唯一实现）
**适用版本**：Unity Physics 1.4.6；EX-GAS 当前 TargetData 管线
**Primary Owner**：UnityPhysics
**来源**：`physics-singletons.md`、`collision-queries.md`；EX-GAS TargetData 设计
**关联规则**：PHY-02、PHY-04

## 使用场景

使用 `OverlapAabb`、ray cast 或 collider cast 获取候选实体，再由 GAS 规则执行阵营、距离、可见性和状态过滤。

## 模式描述

```csharp
var world = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
var rigidBodyIndices = new NativeList<int>(Allocator.Temp);
var extents = new float3(radius);

world.OverlapAabb(
    new OverlapAabbInput
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
    },
    ref rigidBodyIndices);

for (int i = 0; i < rigidBodyIndices.Length; i++)
{
    Entity candidate = world.Bodies[rigidBodyIndices[i]].Entity;
    // 将 candidate 复制到 TargetData，再做 GAS 规则过滤。
}

rigidBodyIndices.Dispose();
```

`OverlapAabb` 返回 rigid-body indices，不能声明为 `NativeList<DistanceHit>`。若要距离或表面信息，应改用相应 distance/cast query。

## EX-GAS 边界

- Physics query 只提供候选集合是项目策略，不是 Unity Physics API 的通用限制。
- 调用点必须记录 query system 的 update order 与 broadphase 同步策略。
- 候选进入确定性裁决前，应按项目稳定键排序或使用不依赖枚举顺序的集合语义。
