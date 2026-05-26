# MAT-02: Random State 必须显式归属

**严重度**: P2
**Primary Owner**: Mathematics-确定性
**来源**: `random-numbers.md` — Mathematics.Random 是显式 state

## 规则声明
每个 `Unity.Mathematics.Random` 的 state 字段必须通过 IComponentData 明确归属 owner（battle singleton / entity / frame-scoped system），使用全局变量或静态字段存储 Random state 是违规。

## 为什么
确定性要求 seed 和消耗序列完全可控。隐式/全局 state 无法追踪消耗计数，破坏 replay 验证。

## EX-GAS 诊断
每个 `CBattleRandomState` 的 `ConsumptionCount` 在 Debugger 中可视化，支持 per-battle 比对。

## 检查方法
搜索 `Random` 类型字段声明，确认均在 IComponentData 中而非 static 变量。
