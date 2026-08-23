# MAT-03：角度单位必须显式，按具体 API 转换

**严重度**：P1
**Primary Owner**：Mathematics-确定性
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Mathematics `1.3.3`；Unity `6000.3.14f1`
**官方来源**：Mathematics `compatibility.md`、`quaternion-multiplication.md`

## 规则声明
角度变量/字段必须用命名或类型表达 degrees/radians；在 API 边界按该 API 契约使用 `math.radians` 或 `math.degrees`。传给 `math.sin/cos/tan`、`quaternion.Euler`、`quaternion.AxisAngle` 的角度为 radians。

## 为什么
不能概括为“Unity.Mathematics 全部 radians、UnityEngine.Mathf 全部 degrees”：`Mathf.Sin/Cos/Tan` 同样接收 radians，而 `UnityEngine.Quaternion.Euler` 使用 degrees。错误来自具体 API 语义混用。

## EX-GAS 诊断
公共配置若以 degrees 表达，在进入 Runtime Core 时统一转换并保存为 radians；热路径不重复转换。

## 检查方法
审查三角函数和 quaternion 构造调用，沿数据源确认单位；禁止只凭类型库名称猜测单位。
