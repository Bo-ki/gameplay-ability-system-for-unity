# MAT-03: 角度单位使用 math.radians / math.degrees 显式转换

**严重度**: P2
**Primary Owner**: Mathematics-确定性
**来源**: `compatibility.md` — UnityEngine 与 Mathematics 在角度上存在差异

## 规则声明
所有角度相关计算必须显式标注单位。向 `math.sin` / `math.cos` / `math.tan` 等三角函数传入 degrees 值时必须先调用 `math.radians`。从 radians 返回 degrees 时必须调用 `math.degrees`。

## 为什么
Unity.Mathematics 全部使用 radians。与 `UnityEngine.Mathf`（degrees）混用是确定性 bug 的常见来源，在 scale 时难以定位。

## EX-GAS 诊断
Code Review 中标注所有三角函数调用，确认输入值单位。

## 检查方法
搜索 `math.sin` / `math.cos` / `math.tan` / `math.atan2` 等三角函数，检查前一行或参数来源是否显式标注单位。
