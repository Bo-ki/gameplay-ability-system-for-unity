# ECB-03: ECB Playback 位置必须属于明确 SystemGroup Phase

**严重度**: P1
**Primary Owner**: 结构变化-ECB
**来源**: `performance-sync-points.md`、`systems-entity-command-buffer.md`

## 规则声明

ECB 的 playback 位置（即所属 ECBSystem 在 SystemGroup 中的排列顺序）必须是一种有意识的设计决策，归属于明确的 frame backbone phase。不允许随意选择 ECBSystem 或隐式依赖默认 Simulation ECB。

## 为什么

ECB playback 即 sync point。如果多个 ECB 散布在不同 phase，就产生多个分散的 sync point，失去集中化优势。所有结构变化用户必须使用同一 phase 的 ECB，确保合并为一次 sync point。

## EX-GAS 诊断

`GasStructuralPlaybackSystemGroup` 是 hot path 中唯一允许结构变化的位置。所有需要结构变化的 system 必须通过此 Group 的 ECB，不绕道使用 `BeginSimulationECBSystem` 或 `EndSimulationECBSystem`。

## 检查方法

审计所有 `CreateCommandBuffer` 调用来源。如果源头不是 `GasStructuralPlaybackSystemGroup` 中的 ECBSystem（除非有明确的 phase contract 豁免），视为违规。
