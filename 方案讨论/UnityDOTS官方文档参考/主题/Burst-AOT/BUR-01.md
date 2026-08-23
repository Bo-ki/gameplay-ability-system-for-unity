# BUR-01：Runtime Core 热路径必须保持可 Burst 编译

**严重度**：P0
**Primary Owner**：Burst-AOT
**类型**: EX-GAS 项目规则
**证据等级**: 项目推导（官方机制以“来源”列出的精确版本文档为准）
**适用版本**：Unity `6000.3.14f1`；Burst `1.8.29`；Entities `1.4.6`
**官方来源**：Burst `getting-started.md`、`csharp-hpc-overview.md`、`compilation-synchronous.md`

## 规则声明
Runtime Core 中经 Profiler 确认为热路径的 job 和 unmanaged `ISystem` 回调必须标注 `[BurstCompile]`，并保持在 Burst 支持的 HPC# 子集中。`SystemBase` 的托管回调不属于可直接 Burst 编译的入口，但其调度的 job 应单独标注。

## 为什么
Burst 能为数据导向循环生成优化的原生代码，但收益取决于具体工作负载，不能使用无依据的“固定快 10–100 倍”倍率。Burst 不支持的调用应视为编译或架构问题；Editor 异步编译期间可能暂时运行托管 .NET JIT 版本，这不等于“静默降级为解释模式”。

## EX-GAS 诊断
审计 Runtime Core 的 `IJobEntity`、`IJobChunk`、`IJob` 和 `ISystem` 热路径；托管对象访问放到明确边界。不要把“有 `[BurstCompile]` 属性”当作已经成功生成 Burst 代码的证据。

## 检查方法
使用 Burst Inspector、Burst 编译日志和目标 Player 构建验证入口；结合 Profiler 确认该入口实际执行且性能收益成立。
