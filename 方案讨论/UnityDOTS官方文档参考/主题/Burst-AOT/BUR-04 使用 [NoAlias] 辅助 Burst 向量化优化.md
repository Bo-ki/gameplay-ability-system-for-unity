# BUR-04: 使用 `[NoAlias]` 辅助 Burst 向量化优化

**严重度**: P1
**Primary Owner**: Burst-AOT
**来源**: `09-Burst-编译-向量化-AOT.md` — 向量化与别名分析

## 规则声明
在 Burst 编译的 job 中对 `NativeArray`/`NativeSlice` 参数和 `ref`/`in` struct 参数标注 `[NoAlias]`，帮助编译器证明无内存别名冲突，启用自动向量化。

## 为什么
Burst 的自动向量化要求编译器能证明访问无别名冲突。`[NoAlias]` 显式消除编译器保守推断，生成 SIMD 向量化代码。

## EX-GAS 诊断
EffectDeltaApply、AttributeAggregate 等大规模 per-entity 循环计算的 job，对 `NativeArray` 参数和 `ref BAttribute`/`in CModifier` 参数标注 `[NoAlias]`。

## 检查方法
Burst Inspector 检查循环是否已向量化；代码审查确认 hot path job 中 `[NoAlias]` 的标注是否合理。
