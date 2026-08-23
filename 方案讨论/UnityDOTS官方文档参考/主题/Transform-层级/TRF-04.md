# TRF-04：TransformUsageFlags 声明最小需求，以烘焙结果验收

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities 1.4.6
**严重度**：P1
**Primary Owner**：Transform-层级
**来源**：`transforms-usage-flags.md`

## 规则声明

Baker 按实际需求声明 `None`、`Renderable`、`Dynamic`、`WorldSpace`、`NonUniformScale` 或 `ManualOverride`。Flags 会与同 GameObject 其他 Bakers 的请求合并；最终组件集合以烘焙结果为准。

## 关键边界

- `None` 不会阻止其他 Baker 添加 transform 需求。
- `Renderable` 不保证永远只有 `LocalToWorld`；Dynamic parent 等条件会改变结果。
- `Dynamic` 只在确实需要运行时移动时请求。
- `ManualOverride` 忽略其他 flags，并且不会自动添加任何 transform component。
- Entity Prefab 自动视为 Dynamic。

## 检查方法

审查 Baker flags 后，在 Baking Preview/Entity Inspector 验证实际组件和 hierarchy。性能结论基于 chunk/transform system profile，不使用固定“节省 2/3”一类无上下文数字。
