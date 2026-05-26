# TRF-01: SimulationSystemGroup 中禁止直接读 LocalToWorld 做 gameplay 决策

**严重度**: P0
**Primary Owner**: Transform-层级
**来源**: `transforms-concepts.md`

## 规则声明
在 `SimulationSystemGroup` 运行期间（即所有 Runtime Core system 的执行期），禁止直接读取 `LocalToWorld` 的 `.Position` / `.Rotation` 用于 gameplay 决策（目标获取、距离判定、范围检测等）。必须使用 `TransformHelpers.ComputeWorldTransformMatrix` 递归计算精确世界矩阵。

## 为什么
Transform 更新只在 `TransformSystemGroup` 运行期间发生。`SimulationSystemGroup` 中的 `LocalToWorld` 可能包含上一帧数据或图形平滑偏移。基于过期坐标的 gameplay 决策（如技能范围判定）产生错误结果。

## EX-GAS 诊断
Debugger 标记所有 `SimulationSystemGroup` 内 system 对 `LocalToWorld` 的直接 `Position` / `Rotation` 读取；Grep 工具应能检测此类违规。

## 检查方法
Grep `LocalToWorld` 在 `Assets/GAS/Runtime/` 下的 `.Position` / `.Rotation` 读取；判断所在 system group；若是 `SimulationSystemGroup` sub-group 则标记。
