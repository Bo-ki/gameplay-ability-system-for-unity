# ISSUE-010 代码执行范式未切换到 DOTS

> 最近复核：2026-06-02 | 状态：Active | 严重度：P0

## 当前结论

旧“全是 `ToEntityArray` + foreach”的诊断已经过期。当前代码已经大量改为 `ISystem`、`QueryBuilder`、ECB system singleton、generated blob catalog。但执行范式仍没有完全切到 DOTS scale-ready：主要风险转为主线程 `SystemAPI.Query`、DynamicBuffer for loop、`EntityManager` direct access、`Complete()`。

## 当前事实

- Runtime + generated runtime 当前有 39 个 `ISystem`。
- Runtime 仍有 1 个 `SystemBase`：`GASManagerInputSystem`，未注册进 5 段 GAS 主链。
- `SystemAPI.Query<...>` 当前命中 generated active effect、Cue systems、AutoChess command drive 等路径。
- `state.Dependency.Complete()` 当前命中 10 处。
- `ToEntityArray()` 当前主要在 Debugger observation 和 AutoChess catalog 初始化。

## 仍成立风险

1. `SystemAPI.Query` 主线程 foreach 不等于 job/chunk pipeline。
2. generated systems 需要同等接受 Burst/job/query 审查。
3. managed Cue / Registry / Helper 不能进入 CoreSimulation hot path。
4. `EntityManager.GetBuffer/GetComponentData/SetComponentData` 在 hot path 中仍多。

## 退出条件

1. CoreSimulation hot path 的主扫描迁移到 `IJobChunk` / 明确 job chain，或有充分规模证据证明主线程路径可接受。
2. `Complete()` 有明确低频边界或被移除。
3. generated runtime 输出 DOTS 审查报告。
4. managed boundary 与 unmanaged core 的成本报告分离。
