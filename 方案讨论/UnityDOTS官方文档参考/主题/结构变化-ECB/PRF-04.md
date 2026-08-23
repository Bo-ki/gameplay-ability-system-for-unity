# PRF-04: Runtime Core 默认集中到约定 ECB Playback Phase

**严重度**: P1
**Primary Owner**: 结构变化-ECB
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3.14f1；Entities 1.4.6
**来源**: `performance-sync-points.md`、`systems-entity-command-buffer-automatic-playback.md`

## 规则声明

EX-GAS Runtime Core 默认使用一个明确的 Structural Commit 区域（可包含连续的 begin/end ECB System），以稳定结构变化的可见性和同步边界。其它 playback phase 是合法的，但必须说明为何不能复用既有 phase，并通过 Profiler 验证新增等待成本。

## 为什么

同步等待取决于当时尚未完成的 job，Unity 没有提供固定的 0.1–1.0 ms 成本。连续两个执行结构变化的 system 在第一个未调度新 job 时通常只产生一次实际等待；若中间调度 job，后续结构变化可能再次等待。

## EX-GAS 诊断

ISSUE-009 — 缺少统一 frame backbone。`GasStructuralPlaybackSystemGroup` 设计已存在但尚未完全落地。AM2B-A/B contract 已声明但 system 尚未全部迁移。

**目标态结构：**
```
GasStructuralPlaybackSystemGroup（唯一结构变化点）
  ├── BeginGasStructuralECBSystem  (playback before core phases)
  └── EndGasStructuralECBSystem    (playback after core phases)
```

## 检查方法

统计每帧结构变化/依赖等待位置及实际耗时。超过项目预算或出现未声明 phase 时标记违规；不能仅以 `syncPointCount > 1` 自动判 P0。
