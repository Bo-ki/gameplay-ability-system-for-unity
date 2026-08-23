# BUR-04：默认不手写 `[NoAlias]`；仅在可证明契约下使用

**严重度**：P0
**Primary Owner**：Burst-AOT
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Burst `1.8.29`
**官方来源**：Burst `aliasing-job-system.md`、`aliasing-noalias.md`、`optimization-loop-vectorization.md`

## 规则声明
job struct、NativeContainer 字段及常规 ECS job 参数默认依赖 Burst/Job System 的自动 no-alias 推断，不批量添加 `[NoAlias]`。只有 Burst Inspector 证明别名阻止优化、编译器无法自动推断，并且能证明所有调用路径均不别名时，才允许对复杂指针或 struct 使用 `[NoAlias]`。

## 为什么
官方明确说明多数用例不需要该属性。`[NoAlias]` 是正确性契约，不是无害提示；实际发生别名时，Burst 可以基于错误前提重排内存访问并产生未定义行为。

## EX-GAS 诊断
删除“EffectDeltaApply、AttributeAggregate 等 job 默认给 NativeArray/ref/in 全部标注”的要求。现有 `[NoAlias]` 必须逐处给出内存所有权证明和 Inspector 前后对比；无法证明则移除。

## 检查方法
代码审查搜索 `[NoAlias]`，检查别名证明、测试和目标 Player 性能数据；Burst Inspector 用于确认是否确有向量化收益。
