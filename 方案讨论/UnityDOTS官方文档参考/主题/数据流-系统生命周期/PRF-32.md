# PRF-32: 主线程 foreach 与 Run 按语义和 Profiler 选择

**严重度**: P2
**Primary Owner**: 数据流-系统生命周期
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**: Unity 6000.3 / Entities 1.4.6
**官方来源**: `job-overhead.md`、`performance-sync-points.md`、`systems-systemapi-query.md`

## 规则声明

已确定工作必须同步在主线程执行时，通常优先用 `SystemAPI.Query` foreach 表达简单遍历；已有可复用 job、需要统一 job 实现或实测更合适时可以使用 `.Run()`。两者都会在执行前完成必要的数据依赖，不能把 foreach 描述成“没有 sync point 风险”。

## 为什么

官方说明 `.Run()` 仍承担 job dependency system 的保护开销，而直接 foreach 不承担这部分 job 运行开销；但 `performance-sync-points.md` 同时明确说明，`.Run()` 与 idiomatic foreach 都可能阻塞主线程等待必要依赖。是否值得改写取决于工作量、依赖状态、代码复用和目标设备上的 Profiler 数据，不使用固定实体数阈值。

## EX-GAS 诊断

Debugger、一次性 initialization 等本就必须在主线程完成的路径，评估是否用 foreach 简化。Runtime hot path 若主线程等待明显，优先评估 `Schedule` / `ScheduleParallel` 和依赖链，而不是只在 `.Run()` 与 foreach 间切换。

## 检查方法

搜索 `.Run()`、`SystemAPI.Query` 与紧随 `.Schedule()` 的 `.Complete()`；在 Profiler 中确认等待与调度成本，再选择 foreach、`Run`、`Schedule` 或 `ScheduleParallel`。并行化不是自动结论，小工作量或依赖受限时单线程调度可能更合适。
