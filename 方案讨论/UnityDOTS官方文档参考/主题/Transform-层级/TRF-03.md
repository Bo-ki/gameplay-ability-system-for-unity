# TRF-03：Child Buffer 顺序任意，不承载 sibling index

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities 1.4.6
**严重度**：P1
**Primary Owner**：Transform-层级
**来源**：`transforms-comparison.md`

## 规则声明

`DynamicBuffer<Child>` 可用于枚举直接 children，但顺序是 arbitrary。禁止将 `children[i]` 的 i 当作稳定 sibling index 或业务身份。

## 替代方案

给 child 添加 slot/id/order 等稳定业务键。若输出需要全序，按完整键排序并定义相同主键下的 tie-breaker。
