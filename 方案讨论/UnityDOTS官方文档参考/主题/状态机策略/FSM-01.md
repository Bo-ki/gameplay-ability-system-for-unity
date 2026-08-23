# FSM-01：高频状态切换默认禁用普通 Tag Add/Remove

**严重度**：P0
**Primary Owner**：状态机策略
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `state-machine.md`、`structural-changes-enableable-components.md`、`performance-chunk-allocations.md`

## 规则声明
每帧可能大量切换的状态默认不用普通 tag component 的 Add/Remove 表达；优先评估 enum/bit field 或 enableable component。若聚类收益证明高于结构变化成本，可申请例外。

## 为什么
Tag Add/Remove 会改变 archetype并移动 entity；多个独立 tag 类型存在 `2^N` 个潜在组合。实际 sync point 和成本取决于批量方式、依赖与已有 archetype，不能一概声称每次切换都是 P0 阻塞。

## EX-GAS 诊断
ActiveEffect 的互斥 lifecycle 状态继续使用 enum；需要 query 过滤的独立活动状态评估 enableable component。

## 检查方法
搜索热路径 Add/Remove tag，结合 Structural Changes Profiler、Archetypes Window 与切换频率判定；例外必须附数据。
