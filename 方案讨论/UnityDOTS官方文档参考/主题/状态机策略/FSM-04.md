# FSM-04：多 FSM 仅在访问/依赖证据支持时拆分 Entity

**严重度**：P1
**Primary Owner**：状态机策略
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `state-machine.md`、`performance-chunk-allocations.md`

## 规则声明
同一 entity 承载多个独立 FSM 时，评估数据共现性、entity 大小、重复抓取、依赖并行度与 lookup 成本；只有拆分能改善这些指标且生命周期关系清楚时才拆。

## 为什么
多个 FSM 只有在用 tag/shared/component 类型聚类状态时才可能让 archetype/chunk 组合相乘；enum/bit field 不会改变 archetype。拆分 entity 也增加引用跳转、查询和生命周期协调，不能以“状态 component >3”作为硬阈值。

## EX-GAS 诊断
ASC、ability、active-effect 的状态是否拆分由实际共同访问的 system 决定；先画出读写矩阵，再用 chunk capacity/依赖数据验证。

## 检查方法
对候选布局比较 entity size、chunk capacity、系统读写集合、lookup 次数和 Profiler 时间。
