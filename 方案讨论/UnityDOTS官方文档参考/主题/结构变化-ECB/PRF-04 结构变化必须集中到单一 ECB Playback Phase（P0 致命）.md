# PRF-04: 结构变化必须集中到单一 ECB Playback Phase（P0 致命）

**严重度**: P0
**Primary Owner**: 结构变化-ECB
**来源**: `performance-sync-points.md`

## 规则声明

Frame backbone 中只允许一个（或一组连续排列的）ECB playback phase 做结构变化。所有需要结构变化的 system 必须使用该 phase 的 ECB，不得分散在多个 SystemGroup 中各自 playback。

## 为什么

每增加一个独立的 sync point ≈ 0.1-1.0ms 主线程阻塞。10 个分散 sync point = 1-10ms，在 16.67ms 帧预算中致命。连续排列的两个做结构变化的 system 可合并为一个 sync point，但中间如果任何一个调度了 job 则合并失效。

## EX-GAS 诊断

ISSUE-009 — 缺少统一 frame backbone。`GasStructuralPlaybackSystemGroup` 设计已存在但尚未完全落地。AM2B-A/B contract 已声明但 system 尚未全部迁移。

**目标态结构：**
```
GasStructuralPlaybackSystemGroup（唯一结构变化点）
  ├── BeginGasStructuralECBSystem  (playback before core phases)
  └── EndGasStructuralECBSystem    (playback after core phases)
```

## 检查方法

统计每帧 sync point 数量。如果 `syncPointCount > 1` 且排除初始化/销毁帧，标为 P0 违规。Frame backbone 设计图中检查结构变化点的数量。
