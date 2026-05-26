# PRF-18: LocalToWorld 在 SimulationSystemGroup 可能过期；用 ComputeWorldTransformMatrix

**严重度**: P1
**Primary Owner**: Transform-层级
**来源**: `transforms-concepts.md`（13-DOTS编写规范与性能陷阱 P1-10）

## 规则声明
`LocalToWorld` component 的值在 `SimulationSystemGroup` 运行期间可能过期或无效。需要精确世界坐标用于 gameplay 计算时，必须使用 `TransformHelpers.ComputeWorldTransformMatrix`，不能直接读 `LocalToWorld`。

## 为什么
`TransformSystemGroup` 的更新时机与 `SimulationSystemGroup` 不同步。读取过期 `LocalToWorld` 导致目标位置、距离计算、范围判断等基于错误坐标。`LocalToWorld` 还可能包含图形平滑目的的额外偏移。

## EX-GAS 诊断
AutoChess 目标获取（target acquisition）、范围技能的距离判定、Physics query 的 input position 都需要精确世界坐标。

## 检查方法
Grep `LocalToWorld` 在 Runtime Core 或 Simulation system 中的直接 `.Position` / `.Rotation` 读取。确认该读取是否在 `SimulationSystemGroup` 中。若是 gameplay 决策依据则违规。
