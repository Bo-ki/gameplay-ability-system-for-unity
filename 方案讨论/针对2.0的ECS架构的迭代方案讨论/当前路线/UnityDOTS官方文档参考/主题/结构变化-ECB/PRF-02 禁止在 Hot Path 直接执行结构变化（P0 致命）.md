# PRF-02: 禁止在 Hot Path 直接执行结构变化（P0 致命）

**严重度**: P0
**Primary Owner**: 结构变化-ECB
**来源**: `performance-sync-points.md`

## 规则声明

`ISystem.OnUpdate` 或 job 内直接调用 `EntityManager.CreateEntity`、`AddComponent`、`RemoveComponent`、`DestroyEntity` 是 P0 违规。所有结构变化必须通过 ECB 延迟到 playback phase。

## 为什么

每个结构变化触发 sync point 阻塞主线程。散落的结构变化使 ECB 合并优化失效。在帧预算紧张（60fps = 16.67ms）的场景下，多个 sync point 可直接导致帧超时。

## EX-GAS 诊断

ISSUE-004 — `SApplyGameplayEffectRequest` 在系统中读 buffer 后创建/销毁 entity，触发 `BufferTypeHandle invalidated` 错误。AutoChess x50 下结构变化散落是主要瓶颈之一（`avgTickMs=13.77ms`）。

## 检查方法

- Grep 搜索 `EntityManager.CreateEntity` / `DestroyEntity` / `AddComponent` / `RemoveComponent` 在 Runtime Core hot path 中的出现
- Debugger 输出 `structuralChangeCount` 并按来源 system 分类
- 出现任意 hot path 直接结构变化则标为 P0 违规
