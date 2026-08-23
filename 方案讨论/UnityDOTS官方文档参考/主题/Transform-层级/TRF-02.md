# TRF-02：Child / PreviousParent 只由 ParentSystem 管理

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities 1.4.6
**严重度**：P1
**Primary Owner**：Transform-层级
**来源**：`transforms-concepts.md`、`transforms-using.md`

## 规则声明

应用代码不得直接添加、移除或修改 `Child` / `PreviousParent`。通过添加、移除或修改 child entity 的 `Parent` 声明层级；`ParentSystem` 在其下一次更新中同步反向 `Child` buffer 和 `PreviousParent`。

## 注意事项

修改 `Parent` 后到 `ParentSystem` 更新前，层级是暂时不一致的：旧 parent 的 `Child` 仍可能包含该 child，新 parent 尚未出现。不要在这个窗口依赖 `Child` 进行即时裁决。
