# CASE-09: Physics query 用于目标获取

**Primary Owner**: UnityPhysics
**来源**: 官方案例模式
**关联规则**: PHY-02, PHY-04

## 使用场景
使用 `OverlapAabb` / `Raycast` + `CollisionFilter` 获取候选目标列表，作为 GAS 规则裁决的输入。

## 模式描述
```csharp
// 1. Physics query -> 候选目标列表
var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
var hits = new NativeList<DistanceHit>(Allocator.TempJob);

physicsWorld.OverlapAabb(
    new OverlapAabbInput
    {
        Center = sourcePos,
        Extent = new half3(5f),
        Filter = new CollisionFilter
        {
            BelongsTo = (uint)CollisionLayer.PlayerAbility,
            CollidesWith = (uint)(CollisionLayer.Enemy | CollisionLayer.Neutral)
        }
    },
    ref hits
);

// 2. GAS TargetData 规则过滤（阵营/距离/可见性）
// Physics 只提供候选列表，不决定结果
```

## 注意事项
- Physics query 只提供候选列表，不替代 GAS 规则裁决
- 必须声明 broadphase 时效性策略
- 非 Physics 时用格子系统或范围表替代
- 目标态：Physics 可选 profile，不影响 Core battle hash

## EX-GAS 适用点
- AutoChess 范围技能目标获取
- Physics query 作为 GAS TargetData 规则的输入
