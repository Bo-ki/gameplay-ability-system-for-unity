# FSM-06: 默认使用单 job enum 分支；仅 profiler 证明必要时切换更复杂方案

**严重度**: P2
**Primary Owner**: 状态机策略
**来源**: `state-machine.md`

## 规则声明
所有 FSM 的默认实现策略为 Per-FSM Data Branching（单 job 内 enum switch）。仅当 profiler 数据明确证明该策略是瓶颈、且复杂方案可带来可测量收益时，才切换到 Per-State Clustering 或 Per-State Branching。

## 为什么
Per-FSM Data Branching 实现最简单、无结构变化、无 archetype 影响、无多 system 调度开销。切换为更复杂方案（多 system、enableable、archetype）引入的维护成本和调试复杂度可能超过性能收益。官方明确警告不要过度工程化。

## EX-GAS 诊断
所有新状态机实现默认从 enum switch 开始；仅在 profiler 显示该 switch 在 hot path 中占比 > 5% 时，启动方案升级评估流程。

## 检查方法
PR/Code review 中若引入非 Per-FSM Data Branching 的状态机方案，必须附带 profiler 数据证明性能收益 >= 10%。
