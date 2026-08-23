# TRF-01：Simulation 中按所需精度读取世界变换

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities 1.4.6
**严重度**：P1
**Primary Owner**：Transform-层级
**来源**：`transforms-concepts.md` > `The LocalToWorld component`；`transforms-helpers.md`

## 规则声明

- 延迟/平滑偏移可接受的 UI、VFX 和近似判断允许直接读取 `LocalToWorld`。
- 需要当前精确世界矩阵的 gameplay 决策使用 `ComputeWorldTransformMatrix`。
- 无 parent 的根 entity 可优先读取当前 `LocalTransform`。

EX-GAS 将目标获取、精确范围判定和 Physics query origin 归类为“默认要求精确”；如某调用点允许近似，必须由业务验收明确声明。

## 为什么

官方明确允许 `LocalToWorld` 作为 latency acceptable 的快速近似，也明确警告它在 Simulation 中可能过期或包含图形平滑偏移。正确规则是按精度分流，而不是一刀切禁止所有读取。
