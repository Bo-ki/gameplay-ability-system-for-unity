# PRF-07: 避免不必要的 System 拆分

**严重度**: P1
**Primary Owner**: System-World-SystemGroup
**来源**: `systems-optimizing.html`

## 规则声明
不要为每个细小计算步骤创建一个 System。当多个操作共享相同 EntityQuery 时，评估是否合并到同一个 System 的多个 job 中。

## 为什么
同 query 拆分为多个 system 意味着每个 system 各自获取相同的 TypeHandle 和 Lookup、重复建立依赖链、增加调度开销。合并后在同一个 System 内通过多个 job 串行调度可共享 handle 和 lookup。

## EX-GAS 诊断
Debugger 应报告 system 合并建议；Same Query Diff System 检测模式。

## 检查方法
- 审计同一 query 条件（All/Any/None 相同）是否分布在多个 system 中
