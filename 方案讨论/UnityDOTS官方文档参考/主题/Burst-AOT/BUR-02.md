# BUR-02：FunctionPointer 仅用于经验证的批量动态算法选择

**严重度**：P1
**Primary Owner**：Burst-AOT
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Burst `1.8.29`
**官方来源**：Burst `csharp-function-pointers.md`

## 规则声明
默认使用直接 job、数据分组或 `switch`。确需 `FunctionPointer<T>` 时，`T` 必须是非泛型 delegate，函数指针必须在初始化阶段注册并缓存，并以批次为粒度处理连续数据。Runtime Core 禁止未经基准验证的逐 entity 微小 FunctionPointer 调用。

## 为什么
函数指针具有间接调用/P/Invoke 边界成本，且 Burst 对 job 的别名分析通常更完整。逐元素调用可能阻止向量化；它不是功能错误，也不能笼统等同于虚方法成本，最终选择必须以目标 Player 基准为准。

## EX-GAS 诊断
ExecutionCalculation 优先按算法类型分组后分别调度 job；只有算法需运行时选择且可批量处理时，才缓存“一类算法一个 FunctionPointer”。

## 检查方法
确认 `CompileFunctionPointer` 不在每帧/逐 entity 路径；检查 delegate、静态方法、`[BurstCompile]` 和 IL2CPP 回调标注；用 Burst Inspector 验证批处理循环的向量化。
