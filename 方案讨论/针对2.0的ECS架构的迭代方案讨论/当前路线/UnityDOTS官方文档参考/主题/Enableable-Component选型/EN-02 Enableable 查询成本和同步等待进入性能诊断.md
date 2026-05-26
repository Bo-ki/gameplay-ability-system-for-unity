# EN-02: Enableable 查询成本和同步等待进入性能诊断

**严重度**: P1
**Primary Owner**: Enableable-Component选型
**来源**: `components-enableable-use.html`; `主题/13-DOTS编写规范与性能陷阱.md` P1-05

## 规则声明
Enableable 不是完全免费的。同步 EntityQuery 操作（如 `CalculateEntityCount()`、`ToEntityArray()`、`GetSingleton<T>()`）在存在 enableable 写 job 未完成时，会触发 sync point 等待写 job 完成。这些查询成本必须纳入性能诊断范围。

## 为什么
Enableable 过滤需要读取 enabled mask。如果存在写入 mask 的 job 尚未完成，主线程必须等待它完成才能获取准确的过滤结果。高频 enableable toggle + 同步 query = 仍有 sync point。

## EX-GAS 诊断
Debugger 应追踪每秒 sync point 中由 enableable query 触发的比例。如果 `syncPointByEnableableQuery > 0` 且高频出现，说明 enableable toggle job 和同步 query 在同一帧冲突。

## 检查方法
- 对于热点路径中的同步 query 操作，检查是否可使用 `IgnoreFilter` 变体或 `Async` 变体避免 sync point
- 场景中大量 `CalculateEntityCount()` 紧跟 enableable 写 job → 标为待优化
- 使用 `EntityQueryOptions.IgnoreComponentEnabledState` 构建 query 可彻底绕开 enableable 过滤
