# PHY-04: Physics 查询必须声明 broadphase 时效性

**严重度**: P1
**Primary Owner**: UnityPhysics
**来源**: `collision-queries.md`

## 规则声明
使用 `OverlapAabb` / `Raycast` / `ColliderCast` 等 Physics query 时，必须同时声明所依赖的 broadphase 时效性策略：接受默认的上一 step broadphase，或显式同步当前 frame broadphase（注明额外成本）。

## 为什么
刚创建/移动的 entity 可能不在上一 step broadphase 中，导致遗漏命中目标。使用同步 broadphase 有额外性能开销，但确保包含最新 entity。

## EX-GAS 诊断
每个 Physics query 周边注释声明 broadphase policy；Debugger 报告使用同步 broadphase 的频次。

## 检查方法
搜索 `OverlapAabb` / `Raycast` / `ColliderCast`；检查调用点是否注释或配置了 broadphase 时效性选择。
