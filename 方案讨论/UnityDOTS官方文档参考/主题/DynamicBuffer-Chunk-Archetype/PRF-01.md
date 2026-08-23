# PRF-01：无独立身份的高频瞬时记录不默认建 Entity

**严重度**：P1
**Primary Owner**：DynamicBuffer-Chunk-Archetype
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`；Collections `2.6.6`
**官方来源**：Entities `performance-chunk-allocations.md`、`optimize-structural-changes.md`

## 规则声明
只在帧内传递、没有独立 identity/lifecycle/query/cleanup 需求的高频记录，默认使用值流、NativeStream、owner-local buffer 或其他明确 owner 的容器，不为每条记录 Create/Destroy Entity。若 entity 模型能提供必要语义，可保留并以 Profiler 验证。

## 为什么
Create/Destroy 都是结构变化，频繁执行可能产生 sync point、chunk 分配和管理成本；但相同组件集合的临时 entity 会共享 archetype，不能错误声称“每个临时 entity 都创建独立 archetype”。

## EX-GAS 诊断
Instant GE 优先 command/value pipeline；duration/periodic effect 若需要跨帧身份、查询或 cleanup，可使用持久 slot/entity，按实际模型选择。

## 检查方法
记录每帧 Create/Destroy 数和 Structural Changes Profiler 成本；`created - destroyed ≈ 0` 只表示数量平衡，不代表没有 churn。
