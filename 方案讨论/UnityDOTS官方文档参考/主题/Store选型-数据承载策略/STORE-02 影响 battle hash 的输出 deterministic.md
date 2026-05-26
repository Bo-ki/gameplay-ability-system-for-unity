# STORE-02: 影响 battle hash 的输出 deterministic

**严重度**: P1
**Primary Owner**: Store选型-数据承载策略
**来源**: EX-GAS 项目确定性回放需求 + `systems-entity-command-buffer-playback.md`

## 规则声明
任何影响 battle hash（战斗状态校验/回放一致性）的数据输出，其生产顺序必须在相同输入下完全可复现。使用 `NativeStream` + deterministic merge（按 target ID 排序）或在 `DynamicBuffer` 中使用固定遍历顺序。

## 为什么
battle hash 用于校验多个客户端或 replay 中游戏状态是否一致。非确定性排序导致即使输入相同，输出顺序不同 -> hash 不匹配 -> 误判为不同步。

## EX-GAS 诊断
EffectCommand fan-in 的 merge 阶段必须 deterministic。AttributeDelta 按 target 分组汇总必须在确定性排序后执行。`TypedSimulationFact` 的生产顺序需可复现。

## 检查方法
检查所有 gameplay 分类的数据通路，确认写入顺序在相同输入下产生相同输出。NativeStream 的 foreach 按 ForEachCount 和 segment 内部顺序遍历是可复现的。
