# ECB-03: ECB Playback 位置必须属于明确 SystemGroup Phase

**严重度**: P1
**Primary Owner**: 结构变化-ECB
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `performance-sync-points.md`、`systems-entity-command-buffer-automatic-playback.md`

## 规则声明

ECB 的 playback 位置（即所属 ECBSystem 在 SystemGroup 中的排列顺序）必须是一种有意识的设计决策，归属于明确的 frame backbone phase。不允许随意选择 ECBSystem 或隐式依赖默认 Simulation ECB。

## 为什么

含结构变化命令的 playback 会在该位置执行结构变化，并可能等待生产者/相关依赖。多个 playback phase 是 Entities 的合法设计，默认 World 本身就提供多组 begin/end ECB System；EX-GAS 选择集中 phase 是为了减少和稳定同步边界，不是 Unity 的“只能有一个”限制。

## EX-GAS 诊断

`GasStructuralPlaybackSystemGroup` 是 EX-GAS Runtime Core 的默认结构提交位置。确需其它时序的路径可以增加显式 phase contract，但必须说明可见性需求，并提供 Profiler/同步点证据。

## 检查方法

审计所有 `CreateCommandBuffer` 调用来源。如果源头不是 `GasStructuralPlaybackSystemGroup` 中的 ECBSystem（除非有明确的 phase contract 豁免），视为违规。
