# PRF-29: Singleton API 不自动完成 Job 依赖

**严重度**: P1
**Primary Owner**: 数据流-系统生命周期
**来源**: `components-singleton.md`、`systems-systemapi.md`

## 规则声明

`GetSingleton` / `GetSingletonRW` / `TryGetSingleton` 等 singleton API 不会等待正在运行的 job 完成。这与普通 component API（`GetComponentData` / `SystemAPI.GetComponent`）行为不同——后者会自动等待可能写入的 job 完成。使用 singleton API 前必须手动 `CompleteDependencyBeforeRO` / `CompleteDependencyBeforeRW`。

## 为什么

`GetSingletonRW` 返回 component 数据引用——在 job 仍在读写时通过引用修改数据 = 竞态条件。Jobs Debugger 仅在开发模式报错；发布版本为静默竞态。`GetSingletonRW` 最佳实践：仅用于访问 NativeContainer（NativeContainer 有独立安全机制）；否则必须配合 `CompleteDependencyBeforeRW`。

## EX-GAS 诊断

所有 `SystemAPI.GetSingletonRW<T>()` 调用必须审计。若无 `state.EntityManager.CompleteDependencyBeforeRW<T>()` 前置调用，且 T 不包含 NativeContainer 字段 → 标记为潜在竞态。

## 检查方法

搜索 `GetSingletonRW` 调用：确认是否在 job 调度之后、有未完成 job 依赖时访问。检查返回的 RefRW 是否通过引用修改数据，且无 `CompleteDependencyBeforeRW`。若无 NativeContainer 包装且无 CompleteDependency → 标记为潜在竞态。
