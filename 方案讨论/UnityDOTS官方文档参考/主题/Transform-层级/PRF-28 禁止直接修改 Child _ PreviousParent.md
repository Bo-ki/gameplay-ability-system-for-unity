# PRF-28: 禁止直接修改 Child / PreviousParent

**严重度**: P1
**Primary Owner**: Transform-层级
**来源**: `transforms-using.md`（13-DOTS编写规范与性能陷阱）

## 规则声明
禁止直接修改 Child / PreviousParent。`Child` 和 `PreviousParent` 由 `ParentSystem` 管理，通过 `Parent` component 建立层级关系。

## 为什么
直接修改 `Child` buffer 会被 `ParentSystem` 下次更新时覆盖；手动操作可能破坏内部一致性。

## EX-GAS 诊断
Grep `Buffer<Child>` / `DynamicBuffer<Child>` 上的 `Add`/`Remove`/`Insert`/`Clear` 调用。

## 检查方法
搜索 `Buffer<Child>` 的 Add/Remove/Clear 操作在应用代码中；搜索 `PreviousParent` 的直接写入。
