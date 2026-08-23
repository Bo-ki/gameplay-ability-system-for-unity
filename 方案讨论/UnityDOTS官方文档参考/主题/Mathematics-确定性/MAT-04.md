# MAT-04：矩阵与四元数复合使用 math.mul

**严重度**：P1
**Primary Owner**：Mathematics-确定性
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Mathematics `1.3.3`
**官方来源**：Mathematics `compatibility.md`、`4x4-matrices.md`、`quaternion-multiplication.md`

## 规则声明
`float4x4` 矩阵乘法、`quaternion` 复合和 quaternion-vector 旋转使用 `math.mul`。逐分量乘法才使用矩阵 `*` 运算符。

## 为什么
Unity.Mathematics 的 `float4x4 * float4x4` 明确定义为逐分量乘法，不是矩阵乘法；这不是 Burst 路径差异。`math.mul` 表达代数乘法语义，并要求调用者确认左右操作数的复合顺序。

## EX-GAS 诊断
变换、朝向和投射物方向计算统一审计 `float4x4` 的 `*`；若确实需要逐分量乘法，代码应明确命名。

## 检查方法
搜索矩阵 `*` 使用并根据意图判断；检查 `math.mul` 左右顺序的单元测试，而不是仅检查方法名。
