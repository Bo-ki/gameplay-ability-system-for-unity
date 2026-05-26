# TRF-02: 禁止直接修改 Child / PreviousParent

**严重度**: P1
**Primary Owner**: Transform-层级
**来源**: `transforms-using.md`

## 规则声明
`Child`（`DynamicBuffer<Child>`）和 `PreviousParent` component 始终由 `ParentSystem` 内部管理。应用代码禁止直接添加、移除或修改这两个组件的值。修改层级关系只能通过设置 `Parent` component 的值完成。

## 为什么
1) 直接修改 `Child` buffer -> `ParentSystem` 下次更新时覆盖为正确值，修改被静默丢弃；2) 修改 `Parent` 后到下一次 `ParentSystem` 更新前层级关系处于不一致状态；3) 手动操作可能破坏 `Child` / `PreviousParent` 的内部一致性。

## EX-GAS 诊断
Grep `Buffer<Child>` / `DynamicBuffer<Child>` 上的 `Add` / `Remove` / `Insert` / `Clear` 调用；`PreviousParent` 的 `SetComponent` / `AddComponent`。

## 检查方法
搜索 `Buffer<Child>` 的 Add/Remove/Clear 操作在应用代码中；搜索 `PreviousParent` 的直接写入。
