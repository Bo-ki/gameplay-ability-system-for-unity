# PRF-31：Child Buffer 没有 sibling 顺序语义

**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities 1.4.6
**严重度**：P1
**Primary Owner**：Transform-层级
**来源**：`transforms-comparison.md` > no equivalent for sibling APIs

`DynamicBuffer<Child>` 中的 children 顺序是 arbitrary。可以按索引遍历全部 child，但不能把索引解释为“主手/副手/第一个 sibling”等业务身份。需要稳定顺序时，为 child 存储业务键并显式排序。
