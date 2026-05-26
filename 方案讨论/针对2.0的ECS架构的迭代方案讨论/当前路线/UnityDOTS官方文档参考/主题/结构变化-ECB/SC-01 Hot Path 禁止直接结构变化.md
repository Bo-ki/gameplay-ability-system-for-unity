# SC-01: Hot Path 禁止直接结构变化

**严重度**: P0
**Primary Owner**: 结构变化-ECB
**来源**: `performance-sync-points.md`

## 规则声明

Hot path（每帧执行、entity 数 > 100）禁止直接调用 `EntityManager.CreateEntity`、`DestroyEntity`、`AddComponent`、`RemoveComponent`。所有结构变化必须通过 ECB 延迟到 playback phase。

## 为什么

每个直接的结构变化触发 sync point → 主线程等待所有 job 完成 → TypeHandle/Lookup 全部失效。散落的结构变化使 ECB 合并优化完全失效，在 60fps 帧预算下多个 sync point 可直接导致帧超时。

## EX-GAS 诊断

ISSUE-004 — 真实 Scene 曾暴露三类 `BufferTypeHandle invalidated by structural change` 错误。`SApplyGameplayEffectRequest` 在同一系统中读 buffer 后创建/销毁 entity。

## 检查方法

搜索代码中 `EntityManager.CreateEntity` / `DestroyEntity` / `AddComponent` / `RemoveComponent` 在 `OnUpdate` 或 job 中的出现；Debugger 向违规 system 告警。
