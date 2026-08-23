# PRF-28：不直接修改 Child / PreviousParent

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities 1.4.6
**严重度**：P1
**Primary Owner**：Transform-层级
**来源**：`transforms-using.md`

`Child` 和 `PreviousParent` 由 `ParentSystem` 管理。应用代码只修改 child entity 的 `Parent`；不得直接添加、移除或改写前两者。修改 `Parent` 后到下一次 `ParentSystem` 更新前，`Child` 仍可能反映旧关系。
