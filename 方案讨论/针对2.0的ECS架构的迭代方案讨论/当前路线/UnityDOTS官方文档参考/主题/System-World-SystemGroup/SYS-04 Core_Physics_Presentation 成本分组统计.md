# SYS-04: Core/Physics/Presentation 成本分组统计

**严重度**: P1
**Primary Owner**: System-World-SystemGroup
**来源**: EX-GAS 架构推论，源于 `systems-optimizing.html` 的线性开销理论

## 规则声明
性能归因必须按 Core（GAS Runtime）/ Physics / Presentation / Runner（AutoChess Demo）分组统计。禁止将所有 PlayerLoop 成本笼统归因到 GAS Core。

## 为什么
混淆分组使性能优化失去方向——Presentation 的渲染抖动可能被误判为 Core 管线退化。只有分组统计才能准确决策哪个领域需要优化投资。

## EX-GAS 诊断
当前性能报告缺少分类。`GASRuntimeFrameBudgetContract` 应指定每组预算上限。

## 检查方法
- 性能报告必须包含至少四组的开销分解图
- 任何优化 PR 必须说明影响的组别
