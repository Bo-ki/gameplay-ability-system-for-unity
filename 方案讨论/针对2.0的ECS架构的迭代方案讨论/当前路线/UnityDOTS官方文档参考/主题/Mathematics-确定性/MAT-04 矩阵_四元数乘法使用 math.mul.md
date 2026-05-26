# MAT-04: 矩阵/四元数乘法使用 math.mul

**严重度**: P1
**Primary Owner**: Mathematics-确定性
**来源**: `4x4-matrices.md` — 矩阵乘法用 math.mul；`quaternion-multiplication.md`

## 规则声明
矩阵乘法（`float4x4`）和四元数乘法（`quaternion`）必须使用 `math.mul()` 方法。禁止使用 C# 的 `*` 操作符进行矩阵或四元数乘法。

## 为什么
C# 的 `*` 操作符不保证矩阵乘法语义，在 Burst 编译路径下行为可能与预期不同。`math.mul` 在 Burst 中正确展开。

## EX-GAS 诊断
搜索 `float4x4` / `quaternion` 类型的 `*` 操作符使用，报告违规位置。

## 检查方法
Grep `float4x4 *` 和 `quaternion *` 模式；确认在 Runtime Core 中零出现。
