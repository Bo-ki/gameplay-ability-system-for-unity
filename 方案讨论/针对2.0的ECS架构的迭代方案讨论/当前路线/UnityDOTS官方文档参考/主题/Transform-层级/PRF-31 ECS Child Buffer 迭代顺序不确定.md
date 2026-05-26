# PRF-31: ECS Child Buffer 迭代顺序不确定

**严重度**: P1
**Primary Owner**: Transform-层级
**来源**: `transforms-comparison.md`（13-DOTS编写规范与性能陷阱）

## 规则声明
ECS Child Buffer 迭代顺序不确定，禁止依赖 sibling index 做确定性排序。需确定性时自建排序键。

## 为什么
`DynamicBuffer<Child>` 的迭代顺序在不同场景加载、不同平台、不同 ECS 版本下不确定。依赖 sibling index 的确定性逻辑在 replay 中不可复现。

## EX-GAS 诊断
搜索 `children[0]` / `children[i]` 等索引方式访问 `Child` buffer 的模式。

## 检查方法
搜索 `Buffer<Child>` 或 `DynamicBuffer<Child>` 的索引访问 `[`；若用于确定性输出则违规。
