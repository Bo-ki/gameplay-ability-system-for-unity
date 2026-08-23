# PRF-24：已知最终布局时避免逐 Component 构建 Entity

**严重度**：P2
**Primary Owner**：DynamicBuffer-Chunk-Archetype
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `optimize-structural-changes.md`

## 规则声明
创建时已知完整组件集合，使用 `CreateArchetype` 后创建 entity；批量相同 entity 使用 bulk Create。运行时同时添加/移除多个类型时优先 ComponentTypeSet/query bulk API。确需分阶段可见的组件变化时允许逐步操作，并记录理由。

## 为什么
逐组件 Add 会让 entity 多次经过中间 archetype，并可能创建冗余 archetype/同步成本。成本由数量、已有 archetype 和 sync point 决定，不存在官方“10K 后从可忽略变 P1”的阈值。

## EX-GAS 诊断
初始化/加载路径优先构造最终 archetype；Runtime hot path 先评估 enableable/value 状态或 bulk 变化。

## 检查方法
审查循环中的 CreateEntity + 多次 AddComponent，并用 Structural Changes Profiler 判断遗留路径优先级。
