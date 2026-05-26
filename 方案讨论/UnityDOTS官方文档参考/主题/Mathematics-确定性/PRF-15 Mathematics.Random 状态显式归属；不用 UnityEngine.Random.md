# PRF-15: Mathematics.Random 状态显式归属；不用 UnityEngine.Random

**严重度**: P1
**Primary Owner**: Mathematics-确定性
**来源**: `90-规则编号索引.md` — PRF-15；`random-numbers.md`

## 规则声明
Runtime Core 中所有随机数必须使用 `Unity.Mathematics.Random`，且其 state 必须通过 IComponentData 显式归属 owner。禁止使用全局/静态 Random 或 `UnityEngine.Random`。

## 为什么
`UnityEngine.Random` 的全局非确定性状态与 battle hash 确定性要求冲突。隐式的 Random state 无法追踪消耗计数和序列，破坏 deterministic replay 验证。

## EX-GAS 诊断
- Debugger 报告 `randomStateOwner`、`randomConsumptionCount`、`initialSeed`
- Battle 初始化时通过外部配置传入 seed，`CBattleRandomState` 作为 singleton IComponentData
- CI 检查新引入的 `UnityEngine.Random` 引用

## 检查方法
- Grep `UnityEngine.Random` 在 Runtime Core 目录下的使用
- 确认所有 `Random` 字段声明在 IComponentData 中
- CI 规则：新代码禁止引入 `UnityEngine.Random`
