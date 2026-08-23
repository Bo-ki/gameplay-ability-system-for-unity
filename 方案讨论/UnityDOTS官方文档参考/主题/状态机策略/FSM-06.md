# FSM-06：从最简单可验证方案开始，以 Profiler 选择 FSM 布局

**严重度**：P2
**Primary Owner**：状态机策略
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Entities `1.4.6`
**官方来源**：Entities `state-machine.md`

## 规则声明
新 FSM 从满足语义且最简单的方案开始；enum/single-job 常是起点，但若需求天然依赖状态 query/数据聚类，可直接选择 enableable/per-state。增加复杂度必须用 Profiler 或明确功能约束证明。

## 为什么
官方只说明 single-job branching 可能是最简单方案，并要求 profile；没有“switch 占比 >5%”“收益 >=10%”等统一阈值。过早拆 job 或 archetype 会引入调度、依赖和维护成本，机械坚持单 job 也可能浪费大量数据抓取。

## EX-GAS 诊断
设计记录列出候选方案、预期瓶颈和验证指标；数值门槛由对应 workload 基准建立并可随版本更新。

## 检查方法
PR 说明为什么当前方案最简单且满足语义；复杂方案附对比数据，简单方案在明显 idle/依赖问题下也需给出验证。
