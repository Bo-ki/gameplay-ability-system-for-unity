# TRF-03: Child Buffer 迭代顺序不确定，禁止依赖 sibling index

**严重度**: P1
**Primary Owner**: Transform-层级
**来源**: `transforms-comparison.md`

## 规则声明
`DynamicBuffer<Child>` 中 children 的迭代顺序在不同 scene 加载、不同 platform、不同 ECS 版本下不确定。禁止依赖 sibling index（buffer 中的位置）用于确定性 gameplay 逻辑（如"第一个 child 是主手武器"）。

## 为什么
ECS 不保证 `Child` buffer 的存储顺序与 Baker 中添加顺序一致，scene deserialization 可能重排。依赖 sibling index 做确定性排序在 replay 中不可复现。

## EX-GAS 诊断
搜索以 `children[0]` / `children[i]` 等索引方式访问 `Child` buffer 的模式，要求插入明确排序逻辑。

## 检查方法
搜索 `Buffer<Child>` 或 `DynamicBuffer<Child>` 的索引访问 `[`；若用于确定性输出则违规。
