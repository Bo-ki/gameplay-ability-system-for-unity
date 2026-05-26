# MAT-01: Runtime Core 禁用 UnityEngine.Random

**严重度**: P0
**Primary Owner**: Mathematics-确定性
**来源**: `compatibility.md` — UnityEngine 与 Mathematics 在 Random 上存在差异

## 规则声明
Runtime Core（包括 Ability、Effect、Attribute、Targeting 相关所有 system）禁止使用 `UnityEngine.Random`。所有随机数需求必须通过 `Unity.Mathematics.Random` 满足。

## 为什么
`UnityEngine.Random` 是全局静态 state，无法控制 seed 和消耗顺序，破坏 battle hash 确定性。

## EX-GAS 诊断
Debugger 应检测 Runtime Core system 目录中对 `UnityEngine.Random` 的任何引用，输出违规 system 列表。

## 检查方法
Grep 搜索 `UnityEngine.Random` 在 `Assets/GAS/Runtime/` 目录下；CI 中禁止新引入。
